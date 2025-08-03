using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

namespace IntelOrca.Biohazard.BioRand
{
    internal class EnemyRandomizer
    {
        public void Randomize(IClassicRandomizerGeneratedVariation context)
        {
            var rng = context.GetRng("enemy");

            // Decide what room is going to have what enemy type
            var roomWithEnemies = GetRoomSelection();

            // Find all global IDs used in fixed enemies
            var usedGlobalIds = context.Variation.Map.Rooms
                .SelectMany(x => x.Value.Enemies ?? [])
                .Where(x => x.GlobalId != null)
                .Select(x => x.GlobalId)
                .ToHashSet();

            // Create bag of available global IDs
            var allEnemyIds = new List<int>();
            for (var i = 0; i < 255; i++)
            {
                if (!usedGlobalIds.Contains(i))
                {
                    allEnemyIds.Add(i);
                }
            }
            var enemyIdBag = allEnemyIds.ToEndlessBag(rng);

            // Create or set enemies
            var enemyPositions = context.DataManager.GetCsv<EnemyRandomiser.EnemyPosition>(BioVersion.Biohazard1, "enemy.csv");
            foreach (var rwe in roomWithEnemies)
            {
                var type = rng.NextOf(rwe.EnemyInfo.Type);
                var roomDefiniedEnemies = rwe.Room.Enemies ?? [];
                var reservedIds = Enumerable.Range(0, 16)
                    .Where(x => !roomDefiniedEnemies.Any(y => y.Id == x))
                    .ToQueue();
                foreach (var e in roomDefiniedEnemies)
                {
                    if (e.Id != null)
                    {
                        if (e.Kind == "npc")
                            continue;

                        foreach (var rdtId in rwe.Rdts)
                        {
                            var placement = new EnemyPlacement()
                            {
                                RdtId = rdtId,
                                GlobalId = e.GlobalId ?? 0,
                                Id = e.Id ?? 0,
                                Type = type,
                                Pose = ChoosePose(rwe.EnemyInfo),
                                Esp = rwe.EnemyInfo.Entry.Esp ?? []
                            };
                            context.ModBuilder.AddEnemy(placement);
                        }
                    }
                    else
                    {
                        // Clear room, add new enemies
                        var min = rwe.EnemyInfo.MinPerRoom;
                        var max = rwe.EnemyInfo.MaxPerRoom;
                        if (e.MaxDifficulty is int maxDifficulty)
                            max = Math.Min(max, Math.Max(1, (int)Math.Round(max / (double)(4 - maxDifficulty))));

                        var numEnemies = rng.Next(min, max + 1);
                        var positions = enemyPositions
                            .Where(x => rwe.Room.Rdts.Contains(RdtId.Parse(x.Room ?? "")))
                            .Where(x => MeetsConditions(x, rwe.EnemyInfo.Entry.Key))
                            .ToEndlessBag(rng);
                        if (positions.Count == 0)
                            continue;

                        var chosenPositions = positions.Next(numEnemies);
                        var condition = e.Condition;
                        foreach (var p in chosenPositions)
                        {
                            if (reservedIds.Count == 0)
                                break;

                            var id = reservedIds.Dequeue();
                            var globalId = enemyIdBag.Next();
                            foreach (var rdtId in rwe.Rdts)
                            {
                                var placement = new EnemyPlacement()
                                {
                                    Create = true,
                                    RdtId = rdtId,
                                    GlobalId = globalId,
                                    Id = id,
                                    Type = type,
                                    Pose = ChoosePose(rwe.EnemyInfo),
                                    X = p.X,
                                    Y = p.Y,
                                    Z = p.Z,
                                    D = p.D,
                                    Esp = rwe.EnemyInfo.Entry.Esp ?? [],
                                    Condition = condition
                                };
                                context.ModBuilder.AddEnemy(placement);
                            }
                        }
                    }
                }
            }

            bool MeetsConditions(EnemyRandomiser.EnemyPosition position, string key)
            {
                if (!position.IncludeTypes.IsDefaultOrEmpty)
                {
                    return position.IncludeTypes.Contains(key);
                }
                if (!position.ExcludeTypes.IsDefaultOrEmpty)
                {
                    return !position.ExcludeTypes.Contains(key);
                }
                return true;
            }

            int ChoosePose(EnemyInfo enemy)
            {
                var poses = enemy.Entry.Poses;
                var totalProbability = poses.Sum(x => x.Probability);
                if (totalProbability == 0)
                    return 0;

                var n = rng.NextDouble(0, totalProbability);
                var c = 0.0;
                foreach (var p in poses)
                {
                    if (n <= c + p.Probability)
                        return p.Pose;
                    c += p.Probability;
                }
                return 0;
            }

            RoomWithEnemies[] GetRoomSelection()
            {
                var map = context.Variation.Map;
                var config = context.Configuration;
                var separateRdts = config.GetValueOrDefault("progression/mansion2", "Never") == "Always";
                var roomWeight = config.GetValueOrDefault("enemies/rooms", 0.0);

                // Get a list of room tags where we don't want enemies
                var banTags = new List<string>();
                if (!config.GetValueOrDefault("enemies/box", false))
                    banTags.Add(MapTags.Box);
                if (!config.GetValueOrDefault("enemies/safe", false))
                    banTags.Add(MapTags.Safe);
                if (!config.GetValueOrDefault("enemies/save", false))
                    banTags.Add(MapTags.Save);

                // Gather the types of enemies we can use
                var enemyInfo = new List<EnemyInfo>();
                foreach (var ed in map.Enemies)
                {
                    foreach (var ede in ed.Entries)
                    {
                        enemyInfo.Add(new EnemyInfo()
                        {
                            Group = ed,
                            Entry = ede,
                            Weight = config.GetValueOrDefault($"enemies/distribution/{ede.Key}", 0.0),
                            MinPerRoom = Math.Max(1, config.GetValueOrDefault($"enemies/min/{ede.Key}", 1)),
                            MaxPerRoom = Math.Min(16, config.GetValueOrDefault($"enemies/max/{ede.Key}", 6))
                        });
                    }
                }

                // Gather up all the rooms
                var rdtSeen = new HashSet<RdtId>();
                var roomList = new List<RoomRdt>();
                foreach (var kvp in map.Rooms)
                {
                    var rdts = kvp.Value.Rdts;
                    if (rdts == null || kvp.Value.Enemies == null)
                        continue;

                    var supportedEnemies = enemyInfo.Where(x => SupportsEnemy(kvp.Value, x)).ToImmutableArray();
                    if (kvp.Value.HasAnyTag(banTags))
                    {
                        supportedEnemies = [];
                    }
                    if (!separateRdts)
                    {
                        var unseenRdts = rdts.Where(x => !rdtSeen.Contains(x)).ToImmutableArray();
                        rdtSeen.AddRange(unseenRdts);
                        roomList.Add(new RoomRdt(kvp.Value, unseenRdts, supportedEnemies));
                    }
                    else
                    {
                        foreach (var rdt in rdts)
                        {
                            if (rdtSeen.Add(rdt))
                            {
                                roomList.Add(new RoomRdt(kvp.Value, [rdt], supportedEnemies));
                            }
                        }
                    }
                }

                // Shuffle room list for main randomness
                roomList = roomList.Shuffle(rng).ToList();

                // Now remove rooms which we want to be empty
                var totalRooms = roomList.Count;
                var enemyRoomTotal = (int)Math.Round(roomWeight * totalRooms);

                // Remove rooms that have to be empty first (they are included in empty room count)
                roomList.RemoveAll(x => !x.SupportedEnemies.Any());
                // Now any excess rooms
                while (roomList.Count > enemyRoomTotal)
                    roomList.RemoveAt(roomList.Count - 1);
                // Update total rooms with enemies to remaining number of rooms (otherwise 100% would give you incorrect proportions)
                enemyRoomTotal = roomList.Count;

                // Shuffle order of rooms
                roomList = roomList
                    .OrderBy(x => x.SupportedEnemies.Count()) // Pick off more limited rooms first (otherwise they never get set)
                    .ToList();

                var totalEnemyWeight = enemyInfo.Sum(x => x.Weight);
                foreach (var ei in enemyInfo)
                {
                    ei.NumRooms = (int)Math.Ceiling(enemyRoomTotal * (ei.Weight / totalEnemyWeight));
                }
                enemyInfo = [.. enemyInfo.OrderBy(x => x.NumRooms)];

                var result = new List<RoomWithEnemies>();
                foreach (var room in roomList)
                {
                    var ei = room.SupportedEnemies
                        .Where(x => x.NumRooms > 0)
                        .Shuffle(rng)
                        .FirstOrDefault();

                    // Fallback
                    ei ??= room.SupportedEnemies
                        .OrderBy(x => x.Weight)
                        .First();

                    ei.NumRooms--;
                    result.Add(new RoomWithEnemies(room, ei));
                }

                return [.. result];
            }

            static bool SupportsEnemy(MapRoom room, EnemyInfo ei)
            {
                var enemies = room.Enemies?.FirstOrDefault(x => x.Kind != "npc");
                if (enemies == null)
                    return false;

                var includedTypes = enemies.IncludeTypes;
                if (includedTypes != null)
                {
                    if (!ei.Type.Any(x => includedTypes.Contains(x)))
                    {
                        return false;
                    }
                }
                else
                {
                    var excludedTypes = enemies.ExcludeTypes;
                    if (excludedTypes != null && ei.Type.Any(x => excludedTypes.Contains(x)))
                    {
                        return false;
                    }
                }

                return true;
            }
        }

        private readonly struct RoomRdt(MapRoom room, ImmutableArray<RdtId> rdts, ImmutableArray<EnemyInfo> supportedEnemies)
        {
            public MapRoom Room => room;
            public ImmutableArray<RdtId> Rdts => rdts;
            public ImmutableArray<EnemyInfo> SupportedEnemies => supportedEnemies;

            public override string ToString() => $"({Room.Name}, [{string.Join(", ", Rdts)}], [{string.Join(", ", supportedEnemies)}])";
        }

        private class RoomWithEnemies(RoomRdt roomRdt, EnemyInfo enemyInfo)
        {
            public MapRoom Room => roomRdt.Room;
            public ImmutableArray<RdtId> Rdts => roomRdt.Rdts;
            public EnemyInfo EnemyInfo => enemyInfo;

            public override string ToString() => $"([{string.Join(", ", Rdts)}], {EnemyInfo})";
        }

        private class EnemyInfo
        {
            public required MapEnemyGroup Group { get; set; }
            public required MapEnemyGroupEntry Entry { get; set; }
            public required double Weight { get; set; }
            public required int MinPerRoom { get; set; }
            public required int MaxPerRoom { get; set; }

            public int NumRooms { get; set; }

            public int[] Type => Entry.Id;

            public override string ToString() => Entry.Name;
        }
    }
}
