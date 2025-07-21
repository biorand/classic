using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

namespace IntelOrca.Biohazard.BioRand
{
    internal class DoorRandomizer
    {
        private Rng _rng = new Rng();
        private List<RoomPiece> _allRooms = [];
        private List<RoomPiece> _includedRooms = [];

        public void Randomize(IClassicRandomizerGeneratedVariation context)
        {
            var rng = _rng = context.GetRng("door");

            // Clear all the door targets that can be randomized
            var map = context.Variation.Map;
            _allRooms.AddRange(GetPieces(map));
            var numRoomsRatio = context.Configuration.GetValueOrDefault("doors/rooms", 0.5);
            var numRooms = Math.Max(0, Math.Min((int)Math.Round(_allRooms.Count * numRoomsRatio), _allRooms.Count));
            var numSegments = Math.Max(1, context.Configuration.GetValueOrDefault("doors/segments", 2));
            var headSegment = CreateSegments(numSegments);

            // Segment ends
            var segmentEnders = _allRooms
                .Where(x => x.IsSegmentEnd)
                .Shuffle(rng)
                .Take(numSegments - 1)
                .ToQueue();
            var segment = headSegment;
            while (segment != null)
            {
                if (segment.End == null)
                {
                    segment.End = TakeRoom(segmentEnders.Dequeue());
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
                .Take(numSegments * 2));

            // Add rest of the rooms
            var remainingRoomCount = numRooms - _includedRooms.Count;
            Distribute(headSegment, _allRooms
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

            var allUnusedRooms = _allRooms
                .SelectMany(x => x.Rooms)
                .ToArray();
            foreach (var room in allUnusedRooms)
            {
                var roomKey = map.GetRoomKey(room);
                map.Rooms.Remove(roomKey);
            }

            // Update door targets in map
            var allDoors = headSegment.All.SelectMany(x => x.AllDoors).Distinct().ToArray();
            foreach (var door in allDoors)
            {
                if (door.IsSegmentEnd)
                {
                    door.Door.Kind = "noreturn";
                }

                if (door.Target == null)
                {
                    door.Door.Requires2 = [];
                    continue;
                }

                var targetRoom = map.GetRoomKey(door.Target.Room);
                var targetId = door.Target.Door.Id;
                if (targetId == null)
                    continue;

                door.Door.Target = $"{targetRoom}:{targetId}";
            }

            // Remove items with room constraints
            foreach (var room in map.Rooms.Values)
            {
                foreach (var item in room.Items ?? [])
                {
                    if (item.Requirements.Any(x => x.Kind == MapRequirementKind.Room))
                    {
                        item.Requires2 = [];
                        item.Optional = true;
                    }
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
                var segment = new Segment();
                if (last == null)
                    head = segment;
                else
                    last.Next = segment;

                if (i == 0)
                {
                    segment.Start = TakeRoom(_allRooms.FirstOrDefault(x => x.IsBegin));
                }
                else if (i == numSegments - 1)
                {
                    segment.End = TakeRoom(_allRooms.FirstOrDefault(x => x.IsEnd));
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
                var segment = segmentBag.Next();
                segment.Unused.Add(TakeRoom(shuffled[i]));
            }
        }

        private RoomPiece TakeRoom(RoomPiece room)
        {
            if (!_allRooms.Contains(room))
                throw new ArgumentException("Room was already taken", nameof(room));

            _allRooms.Remove(room);
            _includedRooms.Add(room);
            return room;
        }

        private void RandomizeSegment(Segment segment)
        {
            while (ConnectAnotherDoor(segment))
            {
            }

            var doorToEnd = segment.UnconnectedDoors.Shuffle(_rng).FirstOrDefault();
            if (doorToEnd != null)
            {
                var target = segment.End!.Doors.Where(x => !x.IsSegmentEnd).Shuffle(_rng).First();
                doorToEnd.Connect(target);
            }
        }

        private bool ConnectAnotherDoor(Segment segment)
        {
            var unconnected = segment.UnconnectedDoors.Shuffle(_rng);
            foreach (var door in unconnected)
            {
                foreach (var targetDoor in segment.AvailableDoors.Shuffle(_rng))
                {
                    var targetRoom = targetDoor.Owner;
                    var targetRoomUnconnectedDoors = targetRoom.Doors.Count(x => !x.IsConnected);
                    if (targetRoomUnconnectedDoors <= 1)
                    {
                        if (unconnected.Length == 1)
                        {
                            continue;
                        }
                    }

                    door.Connect(targetDoor);
                    segment.UseRoom(targetRoom);
                    return true;
                }
            }
            return false;
        }

        private RoomPieceDoor? GetRandomDoor(Segment segment)
        {
            foreach (var door in segment.AvailableDoors.Shuffle(_rng))
            {
                return door;
            }
            return null;
        }

        private class Segment
        {
            public RoomPiece? Start { get; set; }
            public RoomPiece? End { get; set; }
            public Segment? Previous { get; set; }
            public Segment? Next { get; set; }
            public List<RoomPiece> Used { get; } = [];
            public List<RoomPiece> Unused { get; } = [];

            public void UseRoom(RoomPiece room)
            {
                if (Unused.Remove(room))
                {
                    Used.Add(room);
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

            public IEnumerable<RoomPiece> AllUsedRooms
            {
                get
                {
                    if (Start != null)
                        yield return Start;
                    foreach (var room in Used)
                        yield return room;
                    if (End != null)
                        yield return End;
                }
            }

            public IEnumerable<RoomPieceDoor> AllDoors => AllUsedRooms.SelectMany(x => x.Doors);

            public IEnumerable<RoomPieceDoor> UnconnectedDoors
            {
                get
                {
                    IEnumerable<RoomPiece> enumerable = Start == null ? Used : [Start, .. Used];
                    foreach (var door in enumerable.SelectMany(x => x.Doors))
                    {
                        if (door.IsConnected)
                            continue;

                        if (door.Door.Requirements.Any(x => x.Kind == MapRequirementKind.Room))
                            continue;

                        yield return door;
                    }
                }
            }

            public IEnumerable<RoomPieceDoor> AvailableDoors
            {
                get
                {
                    foreach (var room in Unused)
                    {
                        foreach (var door in room.Doors)
                        {
                            if (door.IsConnected)
                                continue;

                            if (door.Door.Requirements.Any(x => x.Kind == MapRequirementKind.Room))
                                continue;

                            yield return door;
                        }
                    }
                }
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

            public RoomPiece(IEnumerable<MapRoom> rooms)
            {
                Rooms = [.. rooms];
                Doors = rooms
                    .SelectMany(r => (r.Doors ?? [])
                        .Where(x => x.Randomize != false)
                        .Select(d => new RoomPieceDoor(this, r, d)))
                    .ToImmutableArray();
            }

            public bool IsBegin => Rooms.Any(x => x.HasTag(MapTags.Begin));
            public bool IsEnd => Rooms.Any(x => x.HasTag(MapTags.End));
            public bool IsSegmentEnd => Doors.Any(y => y.IsSegmentEnd);
            public bool IsBoxRoom => Rooms.Any(x => x.HasTag(MapTags.SegmentEnd));
            public string Name => Rooms[0].Name ?? "???";
            public override string ToString() => Name;
        }

        private class RoomPieceDoor(RoomPiece owner, MapRoom room, MapRoomDoor door)
        {
            public RoomPiece Owner => owner;
            public MapRoom Room => room;
            public MapRoomDoor Door => door;

            public RoomPieceDoor? Target { get; private set; }

            public bool IsConnected => Target != null;
            public bool IsSegmentEnd => Door.HasTag(MapTags.SegmentEnd);
            public string Identifier => $"{Room.Rdts![0]}:{Door.Id}";

            public void Connect(RoomPieceDoor target)
            {
                if (Target != null || target.Target != null)
                    throw new Exception("Door or target door already connected");

                Target = target;
                target.Target = this;
            }

            public override string ToString() => $"{Identifier} -> {Target?.Identifier ?? "(null)"}";
        }
    }
}
