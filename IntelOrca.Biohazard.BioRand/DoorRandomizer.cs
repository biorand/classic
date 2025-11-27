using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;

namespace IntelOrca.Biohazard.BioRand
{
    internal class DoorRandomizer
    {
        private Rng _rng = new Rng();
        private List<RoomPiece> _allRooms = [];
        private GroupDictionary<RoomPiece> _roomDependencies = new();
        private List<RoomPiece> _includedRooms = [];
        private Queue<int> _lockIds = [];

        public void Randomize(IClassicRandomizerGeneratedVariation context)
        {
            var rng = _rng = context.GetRng("sourceDoor");

            // Clear all the door targets that can be randomized
            var map = context.Variation.Map;

            var reservedLockIds = map.Rooms.Values
                .SelectMany(x => x.Doors)
                .Choose(x => (int?)x.LockId)
                .ToArray();
            _lockIds = Enumerable
                .Range(1, 254)
                .Except(reservedLockIds)
                .ToQueue();

            _allRooms.AddRange(GetPieces(map));

            var numRoomsRatio = context.Configuration.GetValueOrDefault("doors/rooms", 0.5);
            var numRooms = Math.Max(0, Math.Min((int)Math.Round(_allRooms.Count * numRoomsRatio), _allRooms.Count));
            var numSegments = Math.Max(1, context.Configuration.GetValueOrDefault("doors/segments", 2));

            // Find all segment enders
            var segmentEnders = _allRooms.Where(x => x.IsSegmentEnd).Shuffle(rng);
            if (numSegments == 1)
            {
                foreach (var r in segmentEnders)
                {
                    foreach (var d in r.Doors)
                    {
                        d.Door.Tags = d.Door.Tags.Remove(MapTags.SegmentEnd);
                    }
                }
                segmentEnders = [];
            }

            // Take segment enders (remove unused ones)
            var usedSegmentEnders = segmentEnders.Take(numSegments - 1).ToQueue();
            var unusedSegmentEnders = segmentEnders.Skip(numSegments - 1).ToArray();
            var allUnusedRooms = unusedSegmentEnders.ToList();
            _allRooms.RemoveMany(unusedSegmentEnders);

            // Create room groupings based on dependencies
            foreach (var room in _allRooms)
            {
                var dependencies = _allRooms
                    .Where(x => room.IsCoupled(x))
                    .ToImmutableArray();
                _roomDependencies.Add(dependencies);
            }

            // Create segments
            var headSegment = CreateSegments(numSegments);
            var segment = headSegment;
            while (segment != null)
            {
                if (segment.End == null)
                {
                    var endRoom = usedSegmentEnders.Dequeue();
                    segment.Unused.AddRange(TakeRoom(endRoom).Except([endRoom]));
                    segment.End = endRoom;
                    if (segment.Next != null)
                        segment.Next.Start = segment.End;
                }
                segment = segment.Next;
            }

            // Need some box rooms
            Distribute(headSegment, _allRooms
                .Where(x => x.IsBoxRoom));

            // Need some hub rooms
            Distribute(headSegment, _allRooms
                .Shuffle(rng)
                .OrderByDescending(x => x.Doors.Length)
                .Take(numSegments * 4));

            // Add rest of the rooms
            var remainingRoomCount = numRooms - _includedRooms.Count;
            Distribute(headSegment, _allRooms
                .Where(x => !x.IsSegmentEnd)
                .Shuffle(rng)
                .Take(remainingRoomCount));

            // Randomize rooms within segments
            segment = headSegment;
            while (segment != null)
            {
                RandomizeSegment(segment);

                // Pass unused rooms to the next segment
                if (segment.Next != null)
                {
                    segment.Next.Unused.AddRange(segment.Unused);
                }
                else
                {
                    _allRooms.AddRange(segment.Unused);
                }
                segment.Unused.Clear();
                segment = segment.Next;
            }

            allUnusedRooms.AddRange(_allRooms);
            foreach (var room in allUnusedRooms.SelectMany(x => x.Rooms))
            {
                map.Rooms.Remove(room.Key);
            }

            if (context.Configuration.GetValueOrDefault<bool>("locks/random"))
            {
                DistributeLocks(map, headSegment);
            }

            // Update door targets in map
            var allDoors = headSegment.All.SelectMany(x => x.Doors).Distinct().ToArray();
            foreach (var door in allDoors)
            {
                if (door.IsSegmentEnd)
                {
                    door.Door.Kind = DoorKinds.NoReturn;
                    door.Target!.Door.Kind = DoorKinds.Blocked;
                }

                if (door.Target == null)
                {
                    if (door.Door.NoUnlock)
                    {
                        door.Door.Kind = "blocked";
                    }
                    else
                    {
                        door.Door.LockId = 255;
                        door.Door.Kind = "locked";
                    }
                    door.Door.Requires2 = [];
                    door.Door.AllowedLocks = [];
                    continue;
                }

                var targetRoom = door.Target.Room.Key;
                var targetId = door.Target.Door.Id;
                if (targetId == null)
                    continue;

                door.Door.Target = $"{targetRoom}:{targetId}";
            }

            // Remove requirements for any doors that have no destination
            foreach (var room in map.Rooms.Values)
            {
                foreach (var door in room.Doors ?? [])
                {
                    if (door.Target == null)
                    {
                        door.Requires2 = [];
                    }
                }
            }

            // Apply door targets
            foreach (var door in allDoors)
            {
                foreach (var rdtId in door.Room.Rdts)
                {
                    if (door.Door.Id is not int doorId)
                        continue;

                    var sourceRdtId = new RdtItemId(rdtId, (byte)doorId);
                    var targetRdtId = door.Target?.Identifier ?? sourceRdtId;
                    var entrance = door.Target == null
                        ? door.Door.Entrance
                        : door.Target.Door.Entrance;
                    if (entrance == null)
                        throw new Exception($"No sourceDoor entrance found");

                    context.ModBuilder.SetDoorTarget(sourceRdtId, new DoorTarget()
                    {
                        Room = targetRdtId.Rdt,
                        X = entrance.X,
                        Y = entrance.Y,
                        Z = entrance.Z,
                        D = entrance.D
                    });
                }
            }
        }

        private static ImmutableArray<RoomPiece> GetPieces(Map map)
        {
            var visited = new HashSet<MapRoom>();
            var pieces = ImmutableArray.CreateBuilder<RoomPiece>();
            foreach (var room in map.Rooms.Values)
            {
                if (visited.Contains(room))
                    continue;

                // Get all rooms connected by non-randomized doors
                var group = new HashSet<MapRoom>();
                var queue = new Queue<MapRoom>();
                queue.Enqueue(room);
                while (queue.Count > 0)
                {
                    var current = queue.Dequeue();
                    if (!group.Add(current))
                        continue;

                    visited.Add(current);
                    foreach (var door in current.Doors ?? [])
                    {
                        if (door.Randomize == false)
                        {
                            var targetRoom = map.GetRoom(door.TargetRoom ?? "");
                            if (targetRoom != null)
                            {
                                queue.Enqueue(targetRoom);
                            }
                        }
                        else
                        {
                            // Door can be randomized, clear the target
                            door.Target = null;
                        }
                    }
                }

                pieces.Add(new RoomPiece(group));
            }
            return pieces.ToImmutable();
        }

        private Segment CreateSegments(int numSegments)
        {
            Segment? head = null;
            Segment? last = null;
            for (var i = 0; i < numSegments; i++)
            {
                var segment = new Segment(i + 1);
                if (last == null)
                    head = segment;
                else
                    last.Next = segment;

                if (i == 0)
                {
                    var startRoom = _allRooms.FirstOrDefault(x => x.IsBegin);
                    segment.Unused.AddRange(TakeRoom(startRoom).Except([startRoom]));
                    segment.Start = startRoom;
                }
                if (i == numSegments - 1)
                {
                    var endRoom = _allRooms.FirstOrDefault(x => x.IsEnd);
                    segment.Unused.AddRange(TakeRoom(endRoom).Except([endRoom]));
                    segment.End = endRoom;
                }
                last = segment;
            }
            return head ?? throw new Exception("No segments created");
        }

        private void Distribute(Segment head, IEnumerable<RoomPiece> rooms)
        {
            var segmentBag = new EndlessBag<Segment>(_rng, head.All);
            var shuffled = rooms.Shuffle(_rng);
            for (var i = 0; i < shuffled.Length; i++)
            {
                var roomGroup = TakeRoom(shuffled[i]);
                if (roomGroup.IsDefaultOrEmpty)
                    continue;

                var segment = segmentBag.Next();
                segment.Unused.AddRange(roomGroup);
            }
        }

        private ImmutableArray<RoomPiece> TakeRoom(RoomPiece room)
        {
            var roomGroup = _roomDependencies[room];
            if (roomGroup.Any(x => !_allRooms.Contains(x)))
                return default;

            _allRooms.RemoveMany(roomGroup);
            _includedRooms.AddRange(roomGroup);
            return roomGroup;
        }

        private int GetNextLockId()
        {
            return _lockIds.Dequeue();
        }

        private void RandomizeSegment(Segment segment)
        {
            if (segment.Start == null || segment.End == null)
                throw new Exception($"Segment does not have a start or end room");

            segment.UseRoom(segment.Start);
            while (ConnectAnotherDoor(segment, true))
            {
            }

            var doorToEnd = PickUnconnectedDoorForSegmentEnd(segment)
                ?? throw new Exception("Segment could not be connected up to the end room");
            var target = segment.End.Doors
                .Where(x => x.IsConnectable && !x.IsSegmentEnd && x.IsFree)
                .Shuffle(_rng)
                .First();
            doorToEnd.Door.Tags = doorToEnd.Door.Tags.Add(MapTags.LockPriority);
            doorToEnd.Connect(target);
            segment.UseRoom(segment.End);

            // This is so that any doors on the segment end can also be connected up
            while (ConnectAnotherDoor(segment, false))
            {
            }

            // Now connect doors back to other doors
            foreach (var door in segment.UnconnectedDoors.Shuffle(_rng))
            {
                if (!door.IsConnectable)
                    continue;
                if (!door.CanBeLocked)
                    continue;

                var potential = segment.UnconnectedDoors
                    .Where(x => x != door && x.IsFree && !x.MustConnectOut && x.CanBeLocked)
                    .Where(x => !x.Owner.IsConnectedTo(door.Owner))
                    .FirstOrDefault();
                if (potential != null)
                {
                    door.Connect(potential, door.Door.LockId ?? GetNextLockId());
                }
            }

            // Now seal all remaining doors
            foreach (var door in segment.Doors)
            {
                if (door.IsSealed)
                    continue;
                if (door.IsSegmentEnd)
                    continue;

                door.Seal();
            }

            foreach (var p in segment.Used)
            {
                foreach (var room in p.Rooms)
                {
                    room.Tags = room.Tags.Add($"segment-{segment.Number}");
                }
            }
        }

        private bool ConnectAnotherDoor(Segment segment, bool ensureFreeDoor)
        {
            var unconnected = segment.UnconnectedDoors.Shuffle(_rng);
            var availableDoors = segment.AvailableDoors.Shuffle(_rng);
            if (!ensureFreeDoor)
            {
                availableDoors = availableDoors.OrderByDescending(x => x.OtherAvailableDoors.Count()).ToArray();
            }

            foreach (var sourceDoor in unconnected)
            {
                if (!segment.HasBox || !segment.HasItems)
                {
                    if (!sourceDoor.IsSegmentEnd && !sourceDoor.IsFree)
                    {
                        continue;
                    }
                }

                foreach (var availableDoor in availableDoors)
                {
                    var targetDoor = availableDoor.Door;
                    var shouldLock = false;

                    if (!sourceDoor.IsSegmentEnd && sourceDoor.Door.Kind == DoorKinds.Unblock && sourceDoor.CanBeLocked)
                    {
                        if (targetDoor.CanBeLocked)
                        {
                            shouldLock = true;
                        }
                        else
                        {
                            // We need to be able to lock the target door to prevent reverse entry
                            // into an unblock door
                            continue;
                        }
                    }

                    // Nodes are already connected (stops key rando complaining)
                    if (sourceDoor.Room.Doors.Any(x => x.TargetRoom == targetDoor.Room.Key))
                        continue;

                    if (ensureFreeDoor)
                    {
                        var remainingDoorsAfterConnection = 0;
                        if (!segment.HasBox || !segment.HasItems)
                        {
                            // Simplifies things if we don't consider complex pieces
                            if (targetDoor.Owner.Rooms.Length != 1)
                            {
                                continue;
                            }

                            remainingDoorsAfterConnection += unconnected.Count(x => x.IsFree && x != sourceDoor);
                            remainingDoorsAfterConnection += availableDoor.OtherAvailableFreeDoors.Count();
                        }
                        else
                        {
                            remainingDoorsAfterConnection += unconnected.Count(x => x != sourceDoor);
                            remainingDoorsAfterConnection += availableDoor.OtherAvailableDoors.Count();
                        }

                        var numConnectBackDoors = targetDoor.Owner.Doors.Count(x => x.Door.HasTag(MapTags.ConnectBack));
                        remainingDoorsAfterConnection -= numConnectBackDoors;
                        if (remainingDoorsAfterConnection <= 0)
                            continue;
                    }

                    // Connect back check
                    if (!ConnectBackDoors(segment, sourceDoor, targetDoor.Owner))
                        continue;

                    sourceDoor.Connect(targetDoor, shouldLock ? GetNextLockId() : null);
                    segment.UseRoom(targetDoor.Owner);

                    Debug.Assert(!ensureFreeDoor || segment.UnconnectedDoors.Any());
                    return true;
                }
            }
            return false;
        }

        private bool ConnectBackDoors(Segment segment, RoomPieceDoor sourceDoor, RoomPiece targetRoom)
        {
            // Get all doors that need to connect back
            var connectBackDoors = targetRoom.Doors
                .Where(x => x.Door.HasTag(MapTags.ConnectBack))
                .ToArray();

            if (connectBackDoors.Length == 0)
                return true;

            // Find all suitable doors that we can connect back to
            var suitableDoors = FindConnectBackDoors(segment.Start!, sourceDoor).Shuffle(_rng);
            if (suitableDoors.Length < connectBackDoors.Length)
                return false;

            // Connect back to them
            for (var i = 0; i < connectBackDoors.Length; i++)
            {
                connectBackDoors[i].Connect(suitableDoors[i], lockId: GetNextLockId());
            }
            return true;
        }

        private static RoomPieceDoor[] FindConnectBackDoors(RoomPiece head, RoomPieceDoor tail)
        {
            var result = new List<RoomPieceDoor>();
            var curr = tail.Owner;
            while (curr != null)
            {
                foreach (var d in curr.Doors)
                {
                    if (d.IsConnectable && !d.MustConnectOut && !d.IsSegmentEnd && d != tail)
                    {
                        result.Add(d);
                    }
                }
                if (curr == head)
                {
                    // Reached start of segment
                    break;
                }
                curr = curr.Parent;
            }
            return result.ToArray();
        }

        private RoomPieceDoor? PickUnconnectedDoorForSegmentEnd(Segment segment)
        {
            var unconnectedDoors = segment.UnconnectedDoors.ToArray();
            if (unconnectedDoors.Length == 0)
                return null;

            var unconnectedDoorsDepth = new int[unconnectedDoors.Length];

            var visited = new HashSet<RoomPiece>();
            var q = new Queue<(RoomPiece, int)>([(segment.Start!, 0)]);
            while (q.Count != 0)
            {
                var (room, depth) = q.Dequeue();
                if (!visited.Add(room))
                    continue;

                foreach (var door in room.Doors)
                {
                    if (door.Target == null)
                    {
                        var index = Array.IndexOf(unconnectedDoors, door);
                        if (index != -1)
                        {
                            unconnectedDoorsDepth[index] = depth;
                        }
                    }
                    else
                    {
                        q.Enqueue((door.Target.Owner, depth + 1));
                    }
                }
            }

            var bestDepth = unconnectedDoorsDepth.Max();
            var potential = new List<RoomPieceDoor>();
            for (var i = 0; i < unconnectedDoorsDepth.Length; i++)
            {
                if (unconnectedDoorsDepth[i] == bestDepth)
                {
                    potential.Add(unconnectedDoors[i]);
                }
            }
            return potential.Random(_rng);
        }

        private void DistributeLocks(Map map, Segment head)
        {
            var genericKeys = map.Items
                .Where(x => x.Value.Discard)
                .Select(x => x.Key)
                .Shuffle(_rng)
                .ToQueue();

            // One key dedicated to each segment
            var segments = head.All.ToArray();
            foreach (var segment in segments)
            {
                if (genericKeys.Count != 0)
                {
                    segment.AvailableLocks.Add(genericKeys.Dequeue());
                }
            }

            // Other keys cross segments
            var segmentBag = new EndlessBag<Segment>(_rng, segments);
            while (genericKeys.Count != 0)
            {
                var key = genericKeys.Dequeue();
                var numSegmentsForKey = _rng.Next(1, Math.Min(4, segments.Length + 1));
                for (var i = 0; i < numSegmentsForKey; i++)
                {
                    var segment = segmentBag.Next();
                    segment.AvailableLocks.Add(key);
                }
            }

            // Apply available locks to doors
            foreach (var segment in segments)
            {
                foreach (var p in segment.Used)
                {
                    foreach (var room in p.Rooms)
                    {
                        foreach (var door in room.Doors ?? [])
                        {
                            door.AllowedLocks = (door.AllowedLocks ?? [])
                                .Intersect(segment.AvailableLocks)
                                .OrderBy(x => x)
                                .ToArray();
                        }
                    }
                }
            }
        }

        private class Segment(int number)
        {
            public int Number => number;
            public RoomPiece? Start { get; set; }
            public RoomPiece? End { get; set; }
            public Segment? Previous { get; set; }
            public Segment? Next { get; set; }
            public List<RoomPiece> Used { get; } = [];
            public List<RoomPiece> Unused { get; } = [];
            public HashSet<int> AvailableLocks { get; } = [];

            public bool HasItems { get; private set; }
            public bool HasBox { get; private set; }

            public void UseRoom(RoomPiece room)
            {
                Unused.Remove(room);
                if (!Used.Contains(room))
                {
                    Used.Add(room);
                }

                if (room.IsBoxRoom)
                {
                    HasBox = true;
                }
                if (room.Rooms.SelectMany(x => x.Items).Any(x => !x.Requirements.Any()))
                {
                    HasItems = true;
                }
            }

            public IEnumerable<Segment> All
            {
                get
                {
                    var curr = this;
                    while (curr != null)
                    {
                        yield return curr;
                        curr = curr.Next;
                    }
                }
            }

            public IEnumerable<RoomPieceDoor> Doors => Used.SelectMany(x => x.Doors);

            public IEnumerable<RoomPieceDoor> UnconnectedDoors
            {
                get
                {
                    foreach (var door in Doors)
                    {
                        if (!door.IsConnectable)
                            continue;

                        if (door.IsSegmentEnd && door.Owner != Start)
                            continue;

                        if (!door.IsSegmentEnd && !AreRequirementsMet(door.Door))
                            continue;

                        yield return door;
                    }
                }
            }

            public IEnumerable<AvailableDoor> AvailableDoors
            {
                get
                {
                    foreach (var room in Unused)
                    {
                        foreach (var door in room.Doors)
                        {
                            if (!door.IsConnectable)
                                continue;

                            if (door.MustConnectOut)
                                continue;

                            yield return new AvailableDoor(door);
                        }
                    }
                }
            }

            private bool AreRequirementsMet(MapEdge edge)
            {
                foreach (var r in edge.Requirements)
                {
                    if (r.Kind == MapRequirementKind.Room)
                    {
                        if (!Used.SelectMany(x => x.Rooms).Any(x => x.Key == r.Value))
                        {
                            return false;
                        }
                    }
                    else if (r.Kind == MapRequirementKind.Flag)
                    {
                        var hasFlag = Used
                            .SelectMany(x => x.Rooms)
                            .SelectMany(x => x.Flags)
                            .Any(x => x.Name == r.Value && AreRequirementsMet(x));
                        if (!hasFlag)
                        {
                            return false;
                        }
                    }
                }
                return true;
            }

            public override string ToString()
            {
                return string.Join(" -> ", Start?.Name ?? "(null)", End?.Name ?? "(null)");
            }
        }

        /// <summary>
        /// Usually one room, but sometimes rooms that are always joined together.
        /// </summary>
        private class RoomPiece
        {
            public ImmutableArray<MapRoom> Rooms { get; }
            public ImmutableArray<RoomPieceDoor> Doors { get; }
            public RoomPiece? Parent { get; set; }

            public RoomPiece(IEnumerable<MapRoom> rooms)
            {
                Rooms = [.. rooms];
                Doors = rooms
                    .SelectMany(r => (r.Doors ?? [])
                        .Where(x => x.Randomize != false)
                        .Select(d => new RoomPieceDoor(this, r, d)))
                    .ToImmutableArray();
            }

            public bool IsConnectedTo(RoomPiece other)
            {
                if (this == other) return true;
                return Doors.Any(x => x.Target?.Owner == other);
            }

            public bool IsCoupled(RoomPiece other)
            {
                if (this == other)
                    return true;

                var allRequirements = Rooms
                    .SelectMany(x => x.AllEdges ?? [])
                    .SelectMany(x => x.Requirements)
                    .Distinct()
                    .ToArray();
                foreach (var r in allRequirements)
                {
                    if (r.Kind == MapRequirementKind.Room)
                    {
                        foreach (var room in other.Rooms)
                        {
                            if (r.Value == room.Key)
                            {
                                return true;
                            }
                        }
                    }
                    else if (r.Kind == MapRequirementKind.Flag)
                    {
                        foreach (var flag in other.Rooms.SelectMany(x => x.Flags ?? []))
                        {
                            if (r.Value == flag.Name)
                            {
                                return true;
                            }
                        }
                    }
                }
                return false;
            }

            public bool IsBegin => Rooms.Any(x => x.HasTag(MapTags.Begin));
            public bool IsEnd => Rooms.Any(x => x.HasTag(MapTags.End));
            public bool IsSegmentEnd => Doors.Any(y => y.IsSegmentEnd);
            public bool IsBoxRoom => Rooms.Any(x => x.HasTag(MapTags.Box));
            public string Name => Rooms[0].Name ?? "???";
            public override string ToString() => Name;
        }

        private class RoomPieceDoor(RoomPiece owner, MapRoom room, MapRoomDoor door)
        {
            public RoomPiece Owner => owner;
            public MapRoom Room => room;
            public MapRoomDoor Door => door;

            public RoomPieceDoor? Target { get; private set; }

            public bool IsSealed { get; private set; }
            public bool IsConnected => Target != null;
            public bool IsFree => Door.Requirements.Length == 0;
            public bool MustConnectOut =>
                Door.Kind == DoorKinds.Unblock ||
                Door.HasAnyTag([MapTags.ConnectOut, MapTags.ConnectBack]) ||
                !IsFree;
            public bool CanBeLocked => !Door.NoUnlock;
            public bool IsSegmentEnd => Door.HasTag(MapTags.SegmentEnd);
            public RdtItemId Identifier => new(Room.Rdts![0], (byte)(Door.Id ?? 0));

            public void Connect(RoomPieceDoor target, int? lockId = null)
            {
                if (Target != null || target.Target != null)
                    throw new Exception("Door or target sourceDoor already connected");

                Target = target;
                target.Target = this;
                Seal();
                target.Seal();
                if (target.Owner.Parent == null)
                {
                    target.Owner.Parent = Owner;
                }

                if (Door.LockId != null)
                {
                    // Door already has a lock, so make opposite door use same lock
                    Target.Door.Kind = null;
                    Target.Door.Requires2 = Door.Requires2;
                    Target.Door.LockId = Door.LockId;
                    Target.Door.LockKey = Door.LockKey;
                    Target.Door.AllowedLocks = Door.AllowedLocks;
                }
                else if (lockId != null)
                {
                    // Opposite door should be locked
                    Door.Kind = DoorKinds.Unlock;
                    Door.LockId = (byte)lockId.Value;
                    Door.AllowedLocks = [];
                    Target.Door.Kind = DoorKinds.Locked;
                    Target.Door.LockId = (byte)lockId.Value;
                    Target.Door.AllowedLocks = [];
                }
            }

            public void Seal()
            {
                if (IsSealed)
                    throw new InvalidOperationException("Door is already sealed");
                IsSealed = true;
            }

            public bool IsConnectable
            {
                get
                {
                    if (IsSealed)
                        return false;

                    if (Door.Randomize == false)
                        return false;

                    if (Door.Kind == DoorKinds.Blocked)
                        return false;

                    return true;
                }
            }

            public override string ToString() => $"{Identifier} -> {Target?.Identifier.ToString() ?? "(null)"}";
        }

        private readonly struct AvailableDoor(RoomPieceDoor door)
        {
            public RoomPieceDoor Door => door;

            public IEnumerable<RoomPieceDoor> OtherDoors
            {
                get
                {
                    var d = door;
                    return d.Owner.Doors.Where(x => x != d);
                }
            }

            public IEnumerable<RoomPieceDoor> OtherUnsealedDoors => OtherDoors.Where(x => !x.IsSealed);
            public IEnumerable<RoomPieceDoor> OtherAvailableDoors => OtherDoors.Where(x => !x.IsSealed && !x.Door.HasTag(MapTags.ConnectBack));
            public IEnumerable<RoomPieceDoor> OtherAvailableFreeDoors => OtherAvailableDoors.Where(x => x.IsFree);
        }

        private class GroupDictionary<T> where T : notnull
        {
            private readonly Dictionary<T, ImmutableArray<T>> _map = [];

            public IEnumerable<ImmutableArray<T>> Groups => _map.Values.Distinct();

            public void Add(IEnumerable<T> items)
            {
                var count = items.Count();
                if (count <= 1)
                    return;

                var first = items.First();
                foreach (var next in items.Skip(1))
                {
                    Add(first, next);
                }
            }

            public void Add(T itemA, T itemB)
            {
                _map.TryGetValue(itemA, out var groupA);
                _map.TryGetValue(itemB, out var groupB);

                if (groupA != null && groupB != null)
                {
                    if (!groupA.Equals(groupB))
                    {
                        var merged = groupA.Concat(groupB)
                                           .Distinct()
                                           .ToImmutableArray();

                        foreach (var item in merged)
                            _map[item] = merged;
                    }
                }
                else if (groupA != null)
                {
                    var extended = groupA.Contains(itemB)
                        ? groupA
                        : groupA.Add(itemB);

                    foreach (var item in extended)
                        _map[item] = extended;
                }
                else if (groupB != null)
                {
                    var extended = groupB.Contains(itemA)
                        ? groupB
                        : groupB.Add(itemA);

                    foreach (var item in extended)
                        _map[item] = extended;
                }
                else
                {
                    var newGroup = ImmutableArray.Create(itemA, itemB);
                    _map[itemA] = newGroup;
                    _map[itemB] = newGroup;
                }
            }

            public ImmutableArray<T> this[T item] => _map.TryGetValue(item, out var group) ? group : [item];
        }
    }
}
