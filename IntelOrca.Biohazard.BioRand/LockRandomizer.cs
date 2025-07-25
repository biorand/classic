using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;

namespace IntelOrca.Biohazard.BioRand
{
    internal class LockRandomizer
    {
        public void Randomise(IClassicRandomizerGeneratedVariation context)
        {
            var doors = GetDoors(context);
            var pairs = GetDoorPairs(context, doors);

            var preserveLocks = context.Configuration.GetValueOrDefault("locks/preserve", false);
            if (preserveLocks)
            {
                var actualDoors = doors.Select(x => x.Door);
                foreach (var d in actualDoors.Where(x => x.LockId != null))
                {
                    d.AllowedLocks = [];
                }
            }

            if (context.Configuration.GetValueOrDefault("locks/random", false))
            {
                var rng = context.GetRng("lock");
                KeepBoxRouteClear(context, rng, doors);
                KeepRouteItemful(context, rng);
                RandomizeLocks(context, rng, pairs);
            }
            SetDoorLocks(context, doors);
        }

        private ImmutableArray<DoorInfo> GetDoors(IClassicRandomizerGeneratedVariation context)
        {
            var map = context.Variation.Map;
            var doors = ImmutableArray.CreateBuilder<DoorInfo>();
            if (map.Rooms != null)
            {
                foreach (var kvp in map.Rooms)
                {
                    var roomKey = kvp.Key;
                    var room = kvp.Value;
                    if (room.Doors == null)
                        continue;

                    foreach (var door in room.Doors)
                    {
                        if (door.Id == null)
                            continue;

                        var doorInfo = new DoorInfo(roomKey, room, door);
                        doors.Add(doorInfo);
                    }
                }
            }
            return doors.ToImmutable();
        }

        private List<DoorPair> GetDoorPairs(IClassicRandomizerGeneratedVariation context, ImmutableArray<DoorInfo> doors)
        {
            var dict = doors.ToDictionary(x => x.Identity);
            var pairs = new List<DoorPair>();
            while (dict.Count != 0)
            {
                var a = dict.First().Value;
                dict.Remove(a.Identity);

                var target = a.Door.Target ?? "";
                if (dict.TryGetValue(target, out var b))
                {
                    dict.Remove(b.Identity);
                    pairs.Add(new DoorPair(a, b));
                }
            }
            return pairs;
        }

        private void KeepBoxRouteClear(IClassicRandomizerGeneratedVariation context, Rng rng, ImmutableArray<DoorInfo> doors)
        {
            var map = context.Variation.Map;
            var beginEnd = map.BeginEndRooms.FirstOrDefault();
            var startRoom = map.GetRoom(beginEnd.Start ?? "");
            if (startRoom == null)
                return;

            // Get list of all possible routes to a box room
            var head = new KeylessRoute(map, [startRoom], []);
            var q = new Queue<KeylessRoute>([head]);
            var final = new List<KeylessRoute>();
            while (q.Count != 0)
            {
                var r = q.Dequeue();
                var next = r.Next;
                if (next.Length == 0)
                {
                    if (r.Tail.HasTag(MapTags.Box) == true)
                    {
                        final.Add(r);
                    }
                }
                else
                {
                    foreach (var n in r.Next)
                    {
                        q.Enqueue(n);
                    }
                }
            }

            // Filter routes to most direct ones
            var headTogether = head.Together;
            final = final
                .Where(x => !x.ContainsButNotAtStart(headTogether))
                .Shuffle(rng)
                .ToList();

            // Choose one and make sure all doors in route are never locked
            var chosen = final.FirstOrDefault();
            if (chosen == null)
                return;

            foreach (var door in chosen.Doors)
            {
                door.AllowedLocks = [];
                if (door.LockId != null)
                {
                    var doorInfo = doors.First(x => x.Door == door);
                    AssignDoorLock(doorInfo, null);
                }

                var otherSide = map.GetOtherSide(door);
                if (otherSide != null)
                {
                    otherSide.AllowedLocks = [];
                    if (otherSide.LockId != null)
                    {
                        var doorInfo = doors.First(x => x.Door == otherSide);
                        AssignDoorLock(doorInfo, null);
                    }
                }
            }
        }

        /// <summary>
        /// Very crude algorithm that forces a few early doors to be unlocked to ensure we have a few
        /// items we can pick up in order to unlock doors.
        /// </summary>
        /// <param name="context"></param>
        /// <param name="rng"></param>
        /// <param name="doors"></param>
        private void KeepRouteItemful(IClassicRandomizerGeneratedVariation context, Rng rng)
        {
            var map = context.Variation.Map;

            // Get first room of randomizer
            var beginEnd = map.BeginEndRooms.FirstOrDefault();
            var startRoom = map.GetRoom(beginEnd.Start ?? "");
            if (startRoom == null)
                return;

            // Get all the rooms at the start of a segment
            var startingRooms = new List<MapRoom> { startRoom };
            foreach (var room in map.Rooms.Values)
            {
                foreach (var door in room.Doors ?? [])
                {
                    if (door.Kind != DoorKinds.NoReturn)
                        continue;

                    var targetRoom = map.GetRoom(door.TargetRoom ?? "");
                    if (targetRoom != null)
                        startingRooms.Add(targetRoom);
                }
            }

            var requiredItemsBeforeBail = 2;
            var forceUnlockedDoors = new List<MapRoomDoor>();
            foreach (var startingRoom in startingRooms)
            {
                var visited = new HashSet<MapRoom>();
                var queue = new Queue<MapRoom>();
                var itemSpotsRemaining = 0;
                var candidateDoors = new List<MapRoomDoor>();
                queue.Enqueue(startingRoom);
                while (queue.Count != 0)
                {
                    var room = queue.Dequeue();
                    if (visited.Add(room))
                    {
                        // If we find our target number of items we can bail
                        itemSpotsRemaining += CountItems(room);
                        if (itemSpotsRemaining >= requiredItemsBeforeBail)
                            break;

                        foreach (var door in room.Doors ?? [])
                        {
                            if (door.Kind == DoorKinds.NoReturn)
                                continue;
                            if (door.Kind == DoorKinds.Blocked)
                                continue;

                            // Skip locked / puzzle doors
                            if (door.AllowedLocks?.Length == 0)
                            {
                                if ((door.Requires2 ?? []).Length != 0)
                                {
                                    continue;
                                }
                            }
                            else
                            {
                                candidateDoors.Add(door);
                                continue;
                            }

                            var nextRoom = map.GetRoom(door.TargetRoom ?? "");
                            if (nextRoom == null)
                                continue;

                            queue.Enqueue(nextRoom);
                        }
                    }

                    // Pick a random lock randomizable door to go through and force unlocked
                    while (queue.Count == 0 && candidateDoors.Count != 0)
                    {
                        var randomCandidateIndex = rng.Next(0, candidateDoors.Count);
                        var randomCandidate = candidateDoors[randomCandidateIndex];
                        candidateDoors.RemoveAt(randomCandidateIndex);

                        var nextRoom = map.GetRoom(randomCandidate.TargetRoom ?? "");
                        if (nextRoom == null)
                            continue;

                        if (visited.Contains(nextRoom))
                            continue;

                        randomCandidate.AllowedLocks = [];
                        forceUnlockedDoors.Add(randomCandidate);

                        var otherSide = map.GetOtherSide(randomCandidate);
                        if (otherSide != null)
                        {
                            otherSide.AllowedLocks = [];
                            forceUnlockedDoors.Add(otherSide);
                        }

                        queue.Enqueue(nextRoom);
                    }
                }
            }

            static int CountItems(MapRoom room)
            {
                return (room.Items ?? []).Count(x => (x.Requires2?.Length ?? 0) == 0 && x.Optional != true);
            }
        }

        private void RandomizeLocks(IClassicRandomizerGeneratedVariation context, Rng rng, List<DoorPair> pairs)
        {
            var modBuilder = context.ModBuilder;
            var lockRatio = context.Configuration.GetValueOrDefault("locks/ratio", 0.0);

            var restrictedPairs = pairs
                .Where(x => x.Restricted)
                .ToHashSet();

            var itemLockIds = context.Variation.Map.Rooms.Values
                .SelectMany(x => x.Items ?? [])
                .Where(x => x.LockId != null)
                .Select(x => x.LockId!.Value)
                .ToArray();
            var reservedLockIds = restrictedPairs
                .Where(x => x.LockId != null)
                .Select(x => (int)x.LockId!)
                .Concat(itemLockIds)
                .ToHashSet();

            var availableLocks = Enumerable.Range(1, 254)
                .Where(x => !reservedLockIds.Contains(x))
                .ToQueue();

            var lockLimit = (int)Math.Round(pairs.Count * lockRatio);
            var numLocks = restrictedPairs.Count(x => x.LockId != null);
            var shuffledPairs = pairs.Except(restrictedPairs).Shuffle(rng);
            foreach (var pair in shuffledPairs)
            {
                var lockId = pair.LockId;
                if (numLocks >= lockLimit)
                {
                    AssignDoorLock(pair.A, null);
                    AssignDoorLock(pair.B, null);
                }
                else
                {
                    if (lockId == null && availableLocks.Count != 0)
                        lockId = (byte)availableLocks.Dequeue();
                    if (lockId == null)
                        continue;

                    var keyType = rng.NextOf(pair.AllowedLocks);
                    var doorLock = new DoorLock(lockId.Value, keyType);
                    var doorLockA = doorLock;
                    var doorLockB = doorLock;

                    // Work around for key randomizer complaining about requirements on other side of blocked door
                    if (pair.A.Door.Kind == DoorKinds.Unblock)
                        doorLockB = new DoorLock(lockId.Value, 255);
                    else if (pair.B.Door.Kind == DoorKinds.Unblock)
                        doorLockA = new DoorLock(lockId.Value, 255);

                    AssignDoorLock(pair.A, doorLockA);
                    AssignDoorLock(pair.B, doorLockB);
                    numLocks++;
                }
            }
        }

        private void AssignDoorLock(DoorInfo doorInfo, DoorLock? doorLock)
        {
            if (doorLock == null)
            {
                doorInfo.Door.LockId = null;
                doorInfo.Door.LockKey = null;
                doorInfo.Door.Requires2 = [];
            }
            else
            {
                doorInfo.Door.LockId = (byte)doorLock.Value.Id;
                doorInfo.Door.LockKey = doorLock.Value.Type;
                doorInfo.Door.Requires2 = doorInfo.Door.LockKey == 255
                    ? ([])
                    : ([$"item({doorInfo.Door.LockKey})"]);
            }
        }

        private void SetDoorLocks(IClassicRandomizerGeneratedVariation context, ImmutableArray<DoorInfo> doors)
        {
            var modBuilder = context.ModBuilder;
            foreach (var doorInfo in doors)
            {
                var door = doorInfo.Door;
                if (door.Id is not int doorId)
                    continue;

                var doorLockId = door.LockId ?? 0;
                var doorLock = (DoorLock?)null;
                if (door.Target == null)
                {
                    if (!door.NoUnlock)
                    {
                        doorLock = new DoorLock(255, 255);
                    }
                }
                else if (door.Kind == DoorKinds.Locked)
                {
                    doorLock = new DoorLock(doorLockId, 255);
                }
                else if (door.Kind == DoorKinds.Unlock)
                {
                    doorLock = new DoorLock(doorLockId, 254);
                }
                else if (doorLockId != 0)
                {
                    if (door.LockKey is int keyItemId)
                    {
                        doorLock = new DoorLock(doorLockId, keyItemId);
                    }
                    else
                    {
                        doorLock = new DoorLock(doorLockId, 0);
                    }
                }

                foreach (var rdtId in doorInfo.Room.Rdts)
                {
                    modBuilder.SetDoorLock(new RdtItemId(rdtId, (byte)doorId), doorLock);
                }
            }
        }

        [DebuggerDisplay("({A}, {B})")]
        private readonly struct DoorPair(DoorInfo a, DoorInfo b)
        {
            public DoorInfo A => a;
            public DoorInfo B => b;

            public byte? LockId => A.Door.LockId ?? B.Door.LockId;
            public int[] AllowedLocks => (A.Door.AllowedLocks ?? []).Intersect(B.Door.AllowedLocks ?? []).ToArray();
            public bool Restricted => AllowedLocks.Length == 0;

            public string Identity { get; } = $"{a.Identity} <-> {b.Identity}";

            public override int GetHashCode() => Identity.GetHashCode();
            public override bool Equals(object obj) => obj is DoorPair dp && Identity == dp.Identity;
        }

        [DebuggerDisplay("{Identity}")]
        private readonly struct DoorInfo(string roomKey, MapRoom room, MapRoomDoor door)
        {
            public string RoomKey => roomKey;
            public MapRoom Room => room;
            public MapRoomDoor Door => door;

            public string Identity { get; } = $"{roomKey}:{door.Id}";

            public override int GetHashCode() => Identity.GetHashCode();
            public override bool Equals(object obj) => obj is DoorInfo di && Identity == di.Identity;
        }

        private class KeylessRoute(Map map, ImmutableList<MapRoom> rooms, ImmutableList<MapRoomDoor> doors)
        {
            public ImmutableList<MapRoom> Rooms => rooms;
            public ImmutableList<MapRoomDoor> Doors => doors;

            public MapRoom Tail => Rooms.Last();
            public ImmutableArray<KeylessRoute> Next => Neighbours
                .Where(x => !rooms.Contains(x.Item1))
                .Select(x => new KeylessRoute(map, rooms.Add(x.Item1), doors.Add(x.Item2)))
                .ToImmutableArray();

            private ImmutableArray<(MapRoom, MapRoomDoor)> Neighbours
            {
                get
                {
                    return Tail.Doors
                        .Where(x => (x.Requires2?.Length ?? 0) == 0)
                        .Select(x => (map.GetRoom(x.TargetRoom ?? ""), x))
                        .Where(x => x.Item1 != null)
                        .Select(x => (x.Item1!, x.Item2))
                        .ToImmutableArray();
                }
            }

            public ImmutableHashSet<MapRoom> Together
            {
                get
                {
                    var hash = new HashSet<MapRoom>();
                    var q = new Queue<MapRoom>([Tail]);
                    while (q.Count > 0)
                    {
                        var n = q.Dequeue();
                        foreach (var d in n.Doors ?? [])
                        {
                            if ((d.Requires2?.Length ?? 0) != 0)
                                continue;

                            var conn = map.GetRoom(d.TargetRoom ?? "");
                            if (conn == null)
                                continue;

                            if (hash.Add(conn))
                            {
                                q.Enqueue(conn);
                            }
                        }
                    }
                    return hash.OrderBy(x => x.Name).ToImmutableHashSet();
                }
            }

            public bool ContainsButNotAtStart(ImmutableHashSet<MapRoom> haystack)
            {
                var state = false;
                foreach (var r in Rooms)
                {
                    if (haystack.Contains(r))
                    {
                        if (state)
                        {
                            return true;
                        }
                    }
                    else
                    {
                        state = true;
                    }
                }
                return false;
            }

            public override string ToString()
            {
                return string.Join(" -> ", Rooms.Select(x => x.Name ?? null));
            }
        }
    }
}
