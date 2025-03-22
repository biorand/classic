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
                var rng = context.Rng.NextFork();
                KeepBoxRouteClear(context, rng, doors);
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
            foreach (var door in chosen.Doors)
            {
                door.AllowedLocks = [];
                if (door.LockId != null)
                {
                    door.Requires2 = [];
                    door.LockId = null;

                    var doorInfo = doors.First(x => x.Door == door);
                    AssignDoorLock(context.ModBuilder, doorInfo, null);
                }
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

            var availableLocks = Enumerable.Range(0, 63)
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
                    AssignDoorLock(modBuilder, pair.A, null);
                    AssignDoorLock(modBuilder, pair.B, null);
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
                    if (pair.A.Door.Kind == "unblock")
                        doorLockB = new DoorLock(lockId.Value, 255);
                    else if (pair.B.Door.Kind == "unblock")
                        doorLockA = new DoorLock(lockId.Value, 255);

                    AssignDoorLock(modBuilder, pair.A, doorLockA);
                    AssignDoorLock(modBuilder, pair.B, doorLockB);
                    numLocks++;
                }
            }
        }

        private void AssignDoorLock(ModBuilder modBuilder, DoorInfo doorInfo, DoorLock? doorLock)
        {
            if (doorLock == null)
            {
                doorInfo.Door.LockId = null;
                doorInfo.Door.Requires2 = [];
            }
            else
            {
                doorInfo.Door.LockId = (byte)doorLock.Value.Id;
                if (doorLock.Value.KeyItemId == 255)
                    doorInfo.Door.Requires2 = [];
                else
                    doorInfo.Door.Requires2 = [$"item({doorLock.Value.KeyItemId})"];
            }
        }

        private void SetDoorLocks(IClassicRandomizerGeneratedVariation context, ImmutableArray<DoorInfo> doors)
        {
            var modBuilder = context.ModBuilder;
            foreach (var doorInfo in doors)
            {
                var door = doorInfo.Door;
                if (door.NoUnlock)
                    continue;

                if (door.Id is not int doorId)
                    continue;

                var doorLockId = door.LockId ?? 0;
                var doorLock = (DoorLock?)null;
                if (door.Target == null)
                {
                    doorLock = new DoorLock(255, 255);
                }
                else if (door.Kind == "locked")
                {
                    doorLock = new DoorLock(doorLockId, 255);
                }
                else if (door.Kind == "locked")
                {
                    doorLock = new DoorLock(doorLockId, 254);
                }
                else
                {
                    var requirement = door.Requirements
                        .Where(x => x.Kind == MapRequirementKind.Item)
                        .Select(x => (int?)int.Parse(x.Value))
                        .FirstOrDefault();

                    if (requirement is int keyItemId)
                    {
                        doorLock = new DoorLock(doorLockId, keyItemId);
                    }
                }

                foreach (var rdtId in doorInfo.Room.Rdts ?? [])
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
            public int[] AllowedLocks => A.Door.AllowedLocks ?? B.Door.AllowedLocks ?? [];
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
                        .Where(x => !x.NoUnlock || x.LockId == null)
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
                            if (!d.NoUnlock || d.LockId != null)
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
