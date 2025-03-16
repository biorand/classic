using System;
using System.Collections.Generic;
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

            LockNowhereDoors(context, doors);
            if (context.Configuration.GetValueOrDefault("locks/random", true))
            {
                RandomizeLocks(context, pairs);
            }
        }

        private Dictionary<string, DoorInfo> GetDoors(IClassicRandomizerGeneratedVariation context)
        {
            var map = context.Variation.Map;
            var doors = new Dictionary<string, DoorInfo>();
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
                        doors.Add(doorInfo.Identity, doorInfo);
                    }
                }
            }
            return doors;
        }

        private List<DoorPair> GetDoorPairs(IClassicRandomizerGeneratedVariation context, Dictionary<string, DoorInfo> doors)
        {
            var pairs = new List<DoorPair>();
            while (doors.Count != 0)
            {
                var a = doors.First().Value;
                doors.Remove(a.Identity);

                var target = a.Door.Target ?? "";
                if (doors.TryGetValue(target, out var b))
                {
                    doors.Remove(b.Identity);
                    pairs.Add(new DoorPair(a, b));
                }
            }
            return pairs;
        }

        private void LockNowhereDoors(IClassicRandomizerGeneratedVariation context, Dictionary<string, DoorInfo> doors)
        {
            foreach (var doorInfo in doors.Values)
            {
                var door = doorInfo.Door;
                if (door.Target == null)
                {
                    // Door doesn't go anywhere, lock it
                    if (!door.NoUnlock)
                    {
                        SetDoorLock(context.ModBuilder, doorInfo, new DoorLock(255, 255));
                    }
                }
            }
        }

        private void RandomizeLocks(IClassicRandomizerGeneratedVariation context, List<DoorPair> pairs)
        {
            var rng = context.Rng.NextFork();
            var modBuilder = context.ModBuilder;
            var preserveLocks = context.Configuration.GetValueOrDefault("locks/preserve", false);
            var lockRatio = context.Configuration.GetValueOrDefault("locks/ratio", 0.0);

            var restrictedPairs = (preserveLocks
                ? pairs.Where(x => x.NoUnlock || x.LockId != null)
                : pairs.Where(x => x.NoUnlock))
                .ToHashSet();

            var reservedLockIds = restrictedPairs
                .Where(x => x.LockId != null)
                .Select(x => x.LockId!)
                .ToHashSet();

            var availableLocks = Enumerable.Range(128, 128)
                .Select(static x => (byte)x)
                .Where(x => !reservedLockIds.Contains(x))
                .ToQueue();

            var lockLimit = (int)Math.Round(pairs.Count * lockRatio);
            var numLocks = restrictedPairs.Count(x => x.LockId != null);
            var shuffledPairs = pairs.Except(restrictedPairs).Shuffle(rng);
            var possibleKeys = context.Variation.Map.Items
                .Where(x => x.Value.Discard && x.Value.Kind.StartsWith("key/"))
                .ToArray();
            foreach (var pair in shuffledPairs)
            {
                var lockId = pair.LockId;
                if (numLocks >= lockLimit)
                {
                    SetDoorLock(modBuilder, pair.A, null);
                    SetDoorLock(modBuilder, pair.B, null);
                }
                else
                {
                    if (lockId == null && availableLocks.Count != 0)
                        lockId = availableLocks.Dequeue();
                    if (lockId == null)
                        continue;

                    var keyType = rng.NextOf(possibleKeys).Key;
                    var doorLock = new DoorLock(lockId.Value, keyType);
                    var doorLockA = doorLock;
                    var doorLockB = doorLock;

                    // Work around for key randomizer complaining about requirements on other side of blocked door
                    if (pair.A.Door.Kind == "unblock")
                        doorLockB = new DoorLock(lockId.Value, 255);
                    else if (pair.B.Door.Kind == "unblock")
                        doorLockA = new DoorLock(lockId.Value, 255);

                    SetDoorLock(modBuilder, pair.A, doorLockA);
                    SetDoorLock(modBuilder, pair.B, doorLockB);
                    numLocks++;
                }
            }
        }

        private void SetDoorLock(ModBuilder modBuilder, DoorInfo doorInfo, DoorLock? doorLock)
        {
            var doorId = (byte)(doorInfo.Door.Id ?? 0);
            foreach (var rdtId in doorInfo.Room.Rdts ?? [])
            {
                modBuilder.SetDoorLock(new RdtItemId(rdtId, doorId), doorLock);
            }
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

        [DebuggerDisplay("({A}, {B})")]
        private readonly struct DoorPair(DoorInfo a, DoorInfo b)
        {
            public DoorInfo A => a;
            public DoorInfo B => b;

            public byte? LockId => A.Door.LockId ?? B.Door.LockId;
            public bool NoUnlock => A.Door.NoUnlock || B.Door.NoUnlock;

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
    }
}
