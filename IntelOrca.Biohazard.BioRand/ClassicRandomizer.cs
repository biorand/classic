using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using IntelOrca.Biohazard.BioRand.Routing;
using IntelOrca.Biohazard.Extensions;
using IntelOrca.Biohazard.Room;
using IntelOrca.Biohazard.Script;
using IntelOrca.Biohazard.Script.Opcodes;

namespace IntelOrca.Biohazard.BioRand
{
    public class ClassicRandomizer : IRandomizer
    {
        public RandomizerConfigurationDefinition ConfigurationDefinition => CreateConfigDefinition();
        public RandomizerConfiguration DefaultConfiguration => ConfigurationDefinition.GetDefault();

        public string BuildVersion => throw new System.NotImplementedException();

        private RandomizerConfigurationDefinition CreateConfigDefinition()
        {
            var dataManager = new DataManager(new[] {
                @"M:\git\biorand-classic\IntelOrca.Biohazard.BioRand\data"
            });
            var map = GetMap(dataManager.GetJson<Map>(BioVersion.Biohazard1, "rdt.json"));


            var result = new RandomizerConfigurationDefinition();
            var page = result.CreatePage("Player");
            var group = page.CreateGroup("");

            page = result.CreatePage("Doors");
            group = page.CreateGroup("Progression (non-door randomizer)");
            group.Items.Add(new RandomizerConfigurationDefinition.GroupItem()
            {
                Id = "progression/guardhouse",
                Label = "Guardhouse",
                Description = "Include the guardhouse in the randomizer. If disabled, the gates to the guardhouse will be locked.",
                Type = "switch",
                Default = true
            });
            group.Items.Add(new RandomizerConfigurationDefinition.GroupItem()
            {
                Id = "progression/lab",
                Label = "Lab",
                Description = "Include the lab in the randomizer. If disabled, there will be no doom books, and you can go straight to the heliport when you reach the fountain.",
                Type = "switch",
                Default = true
            });
            group.Items.Add(new RandomizerConfigurationDefinition.GroupItem()
            {
                Id = "progression/mansion/split",
                Label = "Split Mansion",
                Description =
                    "Split the mansion into two segments, before and after plant 42. " +
                    "The helmet key will be behind plant 42, and the battery will be in a mansion 2 room. " +
                    "Only applicable if guardhouse is enabled.",
                Type = "switch",
                Default = false
            });
            group.Items.Add(new RandomizerConfigurationDefinition.GroupItem()
            {
                Id = "progression/guardhouse/segmented",
                Label = "Segmented Guardhouse",
                Description = "Isolate the guardhouse in the randomizer. If enabled, the guardhouse will be a standalone segment.",
                Type = "switch",
                Default = false
            });
            group.Items.Add(new RandomizerConfigurationDefinition.GroupItem()
            {
                Id = "progression/caves/segmented",
                Label = "Segmented Caves",
                Description = "Isolate the caves in the randomizer. If enabled, the caves will be a standalone segment.",
                Type = "switch",
                Default = true
            });
            group.Items.Add(new RandomizerConfigurationDefinition.GroupItem()
            {
                Id = "progression/lab/segmented",
                Label = "Segmented Lab",
                Description = "Isolate the lab in the randomizer. If enabled, the lab will be a standalone segment.",
                Type = "switch",
                Default = true
            });
            group.Items.Add(new RandomizerConfigurationDefinition.GroupItem()
            {
                Id = "progression/guardhouse/plant42",
                Label = "Mandatory Plant 42",
                Description = "Plant 42 must be defeated to complete the randomizer. If disabled, it may optional for some seeds.",
                Type = "switch",
                Default = false
            });
            group.Items.Add(new RandomizerConfigurationDefinition.GroupItem()
            {
                Id = "progression/mansion/yawn2",
                Label = "Mandatory Yawn",
                Description = "Yawn must be defeated to complete the randomizer. If disabled, it may optional for some seeds.",
                Type = "switch",
                Default = false
            });
            group.Items.Add(new RandomizerConfigurationDefinition.GroupItem()
            {
                Id = "progression/lab/tyrant",
                Label = "Mandatory Tyrant 1",
                Description = "Tyrant 1 must be defeated to complete the randomizer. If disabled, it may optional for some seeds.",
                Type = "switch",
                Default = false
            });

            group = page.CreateGroup("Door Randomizer");
            group.Items.Add(new RandomizerConfigurationDefinition.GroupItem()
            {
                Id = "doors/random",
                Label = "Randomize Doors",
                Description = "Let BioRand randomize all the doors in the game.",
                Type = "switch",
                Default = true
            });
            group.Items.Add(new RandomizerConfigurationDefinition.GroupItem()
            {
                Id = "doors/segments",
                Label = "Number of Segments",
                Description =
                    "Choose the number the segments in the randomizer. " +
                    "Each segment usually ends with a boss or multi-key door." +
                    " Once a segment is complete, no key items are required from a previous segment.",
                Type = "range",
                Min = 1,
                Max = 4,
                Default = 3
            });
            group.Items.Add(new RandomizerConfigurationDefinition.GroupItem()
            {
                Id = "doors/rooms",
                Label = "Number of Rooms",
                Description =
                    "Choose the number of rooms to include in the randomizer. " +
                    "The total number of rooms is spread roughly evenly between each segment.",
                Type = "percent",
                Min = 0,
                Max = 1,
                Step = 0.1,
                Default = 0.4
            });

            group = page.CreateGroup("Door Locks");
            group.Items.Add(new RandomizerConfigurationDefinition.GroupItem()
            {
                Id = "locks/random",
                Label = "Randomize Locks",
                Description =
                    "Let BioRand randomize the door locks. " +
                    "Doors originally without locks may need a key, others may required a different key.",
                Type = "switch",
                Default = false
            });

            page = result.CreatePage("Items");
            group = page.CreateGroup("");
            group.Items.Add(new RandomizerConfigurationDefinition.GroupItem()
            {
                Id = "items/random",
                Label = "Randomize Items",
                Description = "Let BioRand randomize all the items in the game.",
                Type = "switch",
                Default = true
            });
            group.Items.Add(new RandomizerConfigurationDefinition.GroupItem()
            {
                Id = "items/hidden/keys",
                Label = "Hidden Key Items",
                Description = "Hidden items can be keys or weapons.",
                Type = "switch",
                Default = true
            });
            group.Items.Add(new RandomizerConfigurationDefinition.GroupItem()
            {
                Id = "items/documents",
                Label = "Replace documents",
                Description = "Documents will be replaced with items.",
                Type = "switch",
                Default = true
            });
            group.Items.Add(new RandomizerConfigurationDefinition.GroupItem()
            {
                Id = "items/documents/keys",
                Label = "Documents can be Keys",
                Description = "Documents can be keys or weapons if replaced with items.",
                Type = "switch",
                Default = true
            });

            group = page.CreateGroup("Weapons");
            foreach (var kvp in map.Items)
            {
                var itemDefinition = kvp.Value;
                var kind = itemDefinition.Kind;
                if (!kind.StartsWith("weapon/"))
                    continue;

                group.Items.Add(new RandomizerConfigurationDefinition.GroupItem()
                {
                    Id = $"items/weapons/{itemDefinition.Kind}",
                    Label = itemDefinition.Name,
                    Type = "switch",
                    Default = true
                });
            }

            group = page.CreateGroup("Item Distribution");
            foreach (var kvp in map.Items)
            {
                var itemDefinition = kvp.Value;
                var kind = itemDefinition.Kind;
                if (!kind.StartsWith("ammo/") && !kind.StartsWith("health/") && !kind.StartsWith("ink"))
                    continue;

                group.Items.Add(new RandomizerConfigurationDefinition.GroupItem()
                {
                    Id = $"items/distribution/{itemDefinition.Kind}",
                    Label = itemDefinition.Name,
                    Min = 0,
                    Max = 1,
                    Step = 0.01,
                    Type = "range",
                    Default = 0.5,
                    Category = GetCategory(kind)
                });
            }

            page = result.CreatePage("Enemies");
            group = page.CreateGroup("");
            group.Items.Add(new RandomizerConfigurationDefinition.GroupItem()
            {
                Id = "enemies/safe",
                Label = "Enemies in safe rooms",
                Description = "Allow enemies in safe rooms. i.e. rooms where safe music is playing.",
                Type = "switch",
                Default = false
            });
            group.Items.Add(new RandomizerConfigurationDefinition.GroupItem()
            {
                Id = "enemies/save",
                Label = "Enemies in save rooms",
                Description = "Allow enemies in save rooms. i.e. rooms where there is a typewriter.",
                Type = "switch",
                Default = true
            });
            group.Items.Add(new RandomizerConfigurationDefinition.GroupItem()
            {
                Id = "enemies/box",
                Label = "Enemies in box rooms",
                Description = "Allow enemies in box rooms. i.e. rooms where there is an item box.",
                Type = "switch",
                Default = true
            });
            group.Items.Add(new RandomizerConfigurationDefinition.GroupItem()
            {
                Id = "enemies/rooms",
                Label = "Number of rooms containing enemies.",
                Description = "The number of rooms that contain one or more enemies.",
                Type = "percent",
                Min = 0,
                Max = 1,
                Step = 0.01,
                Default = 0.5
            });

            group = page.CreateGroup("Room Distribution");
            foreach (var enemyGroup in map.Enemies)
            {
                var category = new RandomizerConfigurationDefinition.GroupItemCategory()
                {
                    BackgroundColor = enemyGroup.Background,
                    TextColor = enemyGroup.Foreground,
                    Label = enemyGroup.Name
                };
                foreach (var entry in enemyGroup.Entries)
                {
                    group.Items.Add(new RandomizerConfigurationDefinition.GroupItem()
                    {
                        Id = $"enemies/distribution/{entry.Key}",
                        Label = entry.Name,
                        Category = category,
                        Type = "range",
                        Min = 0,
                        Max = 1,
                        Step = 0.01,
                        Default = 0.5
                    });
                }
            }

            group = page.CreateGroup("Maximum per Room");
            foreach (var enemyGroup in map.Enemies)
            {
                foreach (var entry in enemyGroup.Entries)
                {
                    group.Items.Add(new RandomizerConfigurationDefinition.GroupItem()
                    {
                        Id = $"enemies/max/{entry.Key}",
                        Label = entry.Name,
                        Type = "range",
                        Min = 1,
                        Max = 16,
                        Step = 1,
                        Default = 6
                    });
                }
            }

            page = result.CreatePage("Cutscenes");
            page = result.CreatePage("Music");
            return result;

            static RandomizerConfigurationDefinition.GroupItemCategory GetCategory(string kind)
            {
                var topCategory = kind.Split('/').FirstOrDefault();
                if (topCategory == "ammo")
                {
                    return new RandomizerConfigurationDefinition.GroupItemCategory()
                    {
                        BackgroundColor = "#66f",
                        TextColor = "#fff",
                        Label = "Ammo"
                    };
                }
                else if (topCategory == "health")
                {
                    return new RandomizerConfigurationDefinition.GroupItemCategory()
                    {
                        BackgroundColor = "#696",
                        TextColor = "#fff",
                        Label = "Health"
                    };
                }
                else if (topCategory == "ink")
                {
                    return new RandomizerConfigurationDefinition.GroupItemCategory()
                    {
                        BackgroundColor = "#000",
                        TextColor = "#fff",
                        Label = "Ink"
                    };
                }
                else
                {
                    return new RandomizerConfigurationDefinition.GroupItemCategory()
                    {
                        BackgroundColor = "#333",
                        TextColor = "#fff",
                        Label = "None"
                    };
                }
            }
        }

        public RandomizerOutput Randomize(RandomizerInput input)
        {
            input.Configuration["distribution/ammo/handgun"] = 0.7 / (3 / 7.0);
            input.Configuration["distribution/ammo/shotgun"] = 0.3 / (3 / 7.0);
            input.Configuration["distribution/health/g"] = 0.6 / (3 / 7.0);
            input.Configuration["distribution/health/r"] = 0.2 / (3 / 7.0);
            input.Configuration["distribution/health/b"] = 0.1 / (3 / 7.0);
            input.Configuration["distribution/health/fas"] = 0.1 / (3 / 7.0);
            input.Configuration["distribution/ink"] = 1.0 / (1 / 7.0);

            var dataManager = new DataManager(new[] {
                @"M:\git\biorand-classic\IntelOrca.Biohazard.BioRand\data"
            });
            var map = GetMap(dataManager.GetJson<Map>(BioVersion.Biohazard1, "rdt.json"));

            var modBuilder = new ModBuilder();

            var lockRandomizer = new LockRandomizer();
            lockRandomizer.Randomise(input.Seed, map, modBuilder);

            var keyRandomizer = new KeyRandomizer();
            keyRandomizer.RandomiseItems(input.Seed, map, modBuilder);

            var itemRandomizer = new ItemRandomizer();
            itemRandomizer.Randomize(input.Configuration, input.Seed, map, modBuilder);

            var dump = modBuilder.GetDump(map);

            var rdts = GetRdts(0);
            foreach (var rdtId in rdts.Keys)
            {
                var rdt = rdts[rdtId];
                rdts[rdtId] = modBuilder.ApplyToRdt(rdtId, rdt);
            }

            using var tempFolder = new TempFolder();
            foreach (var rdtId in rdts.Keys)
            {
                var rdt = rdts[rdtId];
                var dir = tempFolder.GetOrCreateDirectory($"STAGE{rdtId.Stage + 1}");
                var path = Path.Combine(dir, $"ROOM{rdtId}0.RDT");
                rdt.Data.WriteToFile(path);
            }
            File.WriteAllText(
                Path.Combine(tempFolder.BasePath, "manifest.txt"),
                $"""
                [MOD]
                Name = BioRand | Orca's Profile | {input.Seed}
                """.Replace("\r\n", "\n"));
            var archiveFile = SevenZip(tempFolder.BasePath);
            var modFileName = $"mod_biorand_{input.Seed}.7z";
            return new RandomizerOutput(
                [new RandomizerOutputAsset("mod", "Classic Rebirth Mod", "Drop this in your RE 1 install folder.", modFileName, archiveFile)],
                "",
                new Dictionary<string, string>());
        }

        private static byte[] SevenZip(string directory)
        {
            var tempFile = Path.GetTempFileName() + ".7z";
            try
            {
                SevenZip(tempFile, directory);
                return File.ReadAllBytes(tempFile);
            }
            finally
            {
                try
                {
                    File.Delete(tempFile);
                }
                catch
                {
                }
            }
        }

        private static void SevenZip(string outputPath, string directory)
        {
            var sevenZipPath = Find7z();
            if (sevenZipPath == null)
                throw new Exception("Unable to find 7z");

            var psi = new ProcessStartInfo(sevenZipPath, $"a -r -mx9 \"{outputPath}\" *")
            {
                WorkingDirectory = directory
            };
            var process = Process.Start(psi);
            process.WaitForExit();
            if (process.ExitCode != 0)
                throw new Exception("Failed to create 7z");
        }

        private static string Find7z()
        {
            var pathEnvironment = Environment.GetEnvironmentVariable("PATH");
            var paths = pathEnvironment.Split(Path.PathSeparator);
            foreach (var path in paths)
            {
                var full = Path.Combine(path, "7z.exe");
                if (File.Exists(full))
                {
                    return full;
                }
            }

            var programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
            var defaultPath = Path.Combine(programFiles, "7-Zip", "7z.exe");
            if (File.Exists(defaultPath))
            {
                return defaultPath;
            }
            return null;
        }

        private void SetRemainingItems(Map map, ModBuilder modBuilder)
        {
            var assignedIds = modBuilder.AssignedItemGlobalIds.ToHashSet();
            var allItems = map.Rooms!.Values.SelectMany(x => x.Items).ToArray();
            foreach (var item in allItems)
            {
                if (item.GlobalId is short globalId)
                {
                    if (assignedIds.Add(globalId))
                    {
                        modBuilder.SetItem(globalId, new Item(19, 1));
                    }
                }
            }
        }

        private static Dictionary<RdtId, IRdt> GetRdts(int player)
        {
            var result = new Dictionary<RdtId, IRdt>();
            var installPath = @"F:\games\re1\JPN";
            for (var i = 1; i <= 7; i++)
            {
                var stagePath = Path.Combine(installPath, $"STAGE{i}");
                var files = Directory.GetFiles(stagePath);
                foreach (var path in files)
                {
                    var fileName = Path.GetFileName(path);
                    var match = Regex.Match(fileName, @"^ROOM([0-9A-F]{3})(0|1).RDT$", RegexOptions.IgnoreCase);
                    if (match.Success)
                    {
                        var rdtId = RdtId.Parse(match.Groups[1].Value);
                        var rdtPlayer = int.Parse(match.Groups[2].Value);
                        if (rdtPlayer == player)
                        {
                            var fileData = File.ReadAllBytes(path);
                            if (fileData.Length < 16)
                                continue;

                            result[rdtId] = Rdt.FromData(BioVersion.Biohazard1, fileData);
                        }
                    }
                }
            }

            foreach (var missingRoom in g_missingRooms)
            {
                var mansion2 = new RdtId(missingRoom.Stage + 5, missingRoom.Room);
                result.Add(missingRoom, result[mansion2]);
            }

            return result;
        }

        private static RdtId[] g_missingRooms { get; } =
        [
            RdtId.Parse("110"),
            RdtId.Parse("119"),
            RdtId.Parse("200"),
            RdtId.Parse("20C"),
            RdtId.Parse("213"),
            RdtId.Parse("214"),
            RdtId.Parse("215"),
            RdtId.Parse("216"),
            RdtId.Parse("217"),
            RdtId.Parse("218"),
            RdtId.Parse("219"),
            RdtId.Parse("21A"),
            RdtId.Parse("21B"),
            RdtId.Parse("21C")
        ];

        private Map GetMap(Map map)
        {
            // Apply player, scenario filter
            map = map.For(new MapFilter(false, 0, 0));

            var keys = map.Items!.Values;
            var items = map.Rooms!.Values.SelectMany(x => x.Items).ToArray();

            var guardhouseKeys = keys.Where(x => x.Group == 8).ToArray();
            var guardhouseItems = items.Where(x => x.Group == 8).ToArray();
            var mansion2Items = items.Where(x => x.Group == 2).ToArray();
            var labItems = items.Where(x => x.Group == 32).ToArray();

            foreach (var item in items)
                item.Group = -1;

            // Only guardhouse can contain guardhouse keys
            foreach (var item in items)
                item.Group &= ~8;
            foreach (var item in guardhouseItems)
                item.Group = 8 | 128;

            // Mansion 2
            foreach (var item in items)
                item.Group &= ~64;
            var plant42item = map.Rooms!["40C"].Items.First(x => x.Name == "KEY IN FIREPLACE");
            plant42item.Group = 64;
            map.Items[54].Group = 64;

            // Cave segment
            var caveDoor = map.Rooms!["302"].Doors.First(x => x.Name == "LADDER TO CAVES");
            caveDoor.Kind = "noreturn";

            // Lab segment
            var labDoor = map.Rooms!["305"].Doors.First(x => x.Name == "FOUNTAIN STAIRS");
            labDoor.Kind = "noreturn";

            // Battery
            foreach (var item in items)
                item.Group &= ~256;
            map.Items[39].Group = 256;
            foreach (var item in mansion2Items)
                item.Group |= 256;
            foreach (var item in labItems)
                item.Group |= 256;

            return map;
        }
    }

    internal class LockRandomizer
    {
        public void Randomise(int seed, Map map, ModBuilder modBuilder)
        {
            // Collect doors
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
                        if (door.Id == null || door.Target == null)
                            continue;

                        var doorInfo = new DoorInfo(roomKey, room, door);
                        doors.Add(doorInfo.Identity, doorInfo);
                    }
                }
            }

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

            var availableLocks = new Queue<byte>([200, 201, 205, 206]);

            foreach (var pair in pairs)
            {
                if (pair.A.Door.NoUnlock || pair.B.Door.NoUnlock)
                    continue;

                var lockId = pair.A.Door.LockId ?? pair.B.Door.LockId;
                if (lockId == null && availableLocks.Count != 0)
                    lockId = availableLocks.Dequeue();
                if (lockId == null)
                    continue;

                var doorLock = new DoorLock(lockId.Value, 51); // Sword Key
                SetDoorLock(modBuilder, pair.A, doorLock);
                SetDoorLock(modBuilder, pair.B, doorLock);
            }
        }

        private void SetDoorLock(ModBuilder modBuilder, DoorInfo doorInfo, DoorLock doorLock)
        {
            var doorId = (byte)(doorInfo.Door.Id ?? 0);
            foreach (var rdtId in doorInfo.Room.Rdts ?? [])
            {
                modBuilder.SetDoorLock(new RdtItemId(rdtId, doorId), doorLock);
            }
            doorInfo.Door.LockId = (byte)doorLock.Id;
            doorInfo.Door.Requires2 = [$"item({doorLock.KeyItemId})"];
        }

        [DebuggerDisplay("({A}, {B})")]
        private readonly struct DoorPair(DoorInfo a, DoorInfo b)
        {
            public DoorInfo A => a;
            public DoorInfo B => b;
        }

        [DebuggerDisplay("{Identity}")]
        private readonly struct DoorInfo(string roomKey, MapRoom room, MapRoomDoor door)
        {
            public string RoomKey => roomKey;
            public MapRoom Room => room;
            public MapRoomDoor Door => door;

            public string Identity { get; } = $"{roomKey}:{door.Id}";
        }
    }

    internal class KeyRandomizer
    {
        public void RandomiseItems(int seed, Map map, ModBuilder modBuilder)
        {
            var graphBuilder = new GraphBuilder();
            var roomNodes = new Dictionary<string, Node>();
            var flagNodes = new Dictionary<string, Node>();
            var itemIdToKey = new Dictionary<int, Key>();
            var keyToItemId = new Dictionary<Key, int>();
            var itemNodeToGlobalId = new Dictionary<Node, int>();
            var globalIdToName = new Dictionary<int, string>();
            if (map.Items != null)
            {
                foreach (var k in map.Items)
                {
                    var itemId = k.Key;
                    var label = k.Value.Name;
                    if (k.Value.Kind is not string kind)
                        continue;

                    var kindParts = kind.Split('/');
                    if (kindParts.Length != 2 || kindParts[0] != "key")
                        continue;

                    var keyKind = (KeyKind)Enum.Parse(typeof(KeyKind), kindParts[1], true);
                    var group = k.Value.Group;
                    var keyNode = graphBuilder.Key(label, group, keyKind);
                    itemIdToKey[itemId] = keyNode;
                    keyToItemId[keyNode] = itemId;
                }
            }

            var beginEnd = map.BeginEndRooms.FirstOrDefault();
            if (beginEnd != null && map.Rooms != null)
            {
                var startNode = graphBuilder.Room("START");
                foreach (var kvp in map.Rooms)
                {
                    var roomKey = kvp.Key;
                    var room = kvp.Value;
                    var label = room.Name != null ? $"{roomKey}|{room.Name}" : $"{roomKey}";
                    roomNodes[roomKey] = graphBuilder.Room(label);
                }
                graphBuilder.Door(startNode, roomNodes[beginEnd.Start ?? ""]);

                foreach (var kvp in map.Rooms)
                {
                    var roomKey = kvp.Key;
                    var room = kvp.Value;
                    var source = roomNodes[roomKey];
                    if (room.Doors != null)
                    {
                        foreach (var edge in room.Doors)
                        {
                            if (edge.Target == null)
                                continue;
                            if (edge.Kind == "blocked" || edge.Kind == "locked")
                                continue;

                            var targetKeyId = edge.Target.Split(':');
                            var target = roomNodes[targetKeyId[0]];
                            var requirements = GetRequirements(edge);
                            _ = edge.Kind switch
                            {
                                "oneway" => graphBuilder.OneWay(source, target, requirements),
                                "noreturn" => graphBuilder.NoReturn(source, target, requirements),
                                "unblock" => graphBuilder.BlockedDoor(source, target, requirements),
                                "unlock" => graphBuilder.BlockedDoor(source, target, requirements),
                                _ => graphBuilder.Door(source, target, requirements)
                            };
                        }
                    }
                    if (room.Items != null)
                    {
                        foreach (var item in room.Items)
                        {
                            if (item.AllowKey == false)
                                continue;

                            var label = item.Name != null ? $"{roomKey}|{room.Name}/{item.Name}" : $"{roomKey}";
                            var group = item.Group;
                            var itemNode = graphBuilder.Item(label, group, source, GetRequirements(item));
                            if (item.GlobalId is short globalId)
                            {
                                itemNodeToGlobalId[itemNode] = globalId;
                                globalIdToName[globalId] = label;
                            }
                        }
                    }
                    if (room.Flags != null)
                    {
                        foreach (var item in room.Flags)
                        {
                            var flagKey = item.Name ?? throw new Exception("Flag has no name");
                            var flagNode = GetOrCreateFlag(flagKey);
                            graphBuilder.Door(source, flagNode, GetRequirements(item));
                        }
                    }
                }
            }

            var graph = graphBuilder.ToGraph();
            var route = graph.GenerateRoute(seed, new RouteFinderOptions()
            {
                DebugDeadendCallback = (o) =>
                {
                }
            });
            var itemToKey = itemNodeToGlobalId
                .Select(kvp => (kvp.Value, route.GetItemContents(kvp.Key)))
                .Where(x => x.Item2 != null)
                .ToDictionary(x => x.Item1, x => x.Item2!.Value);
            var ggg = itemToKey.ToDictionary(x => globalIdToName[x.Key], x => x.Value);
            var s = route.Log;

            foreach (var kvp in itemToKey)
            {
                var key = kvp.Value;
                var itemType = (byte)keyToItemId[key];
                modBuilder.SetItem(kvp.Key, new Item(itemType, 1));
            }

            Requirement[] GetRequirements(MapEdge e)
            {
                return e.Requirements.Select(r =>
                    r.Kind switch
                    {
                        MapRequirementKind.Flag => new Requirement(GetOrCreateFlag(r.Value)),
                        MapRequirementKind.Item => new Requirement(itemIdToKey[int.Parse(r.Value)]),
                        MapRequirementKind.Room => new Requirement(roomNodes[r.Value]),
                        _ => throw new NotImplementedException()
                    }).ToArray();
            }

            Node GetOrCreateFlag(string name)
            {
                if (!flagNodes.TryGetValue(name, out var result))
                {
                    result = graphBuilder.Room(name);
                    flagNodes[name] = result;
                }
                return result;
            }
        }
    }

    internal class ItemRandomizer
    {
        public void Randomize(RandomizerConfiguration config, int seed, Map map, ModBuilder modBuilder)
        {
            if (map.Rooms == null || map.Items == null)
                return;

            var rng = new Rng(seed);
            var itemSlots = map.Rooms.Values
                .SelectMany(x => x.Items)
                .Where(x => x.GlobalId != null)
                .Where(x => x.Document != false)
                .Select(x => (int)x.GlobalId!.Value)
                .Except(modBuilder.AssignedItemGlobalIds)
                .Shuffle(rng)
                .ToQueue();

            var weights = map.Items
                .Select(x => (x.Key, config.GetValueOrDefault($"distribution/{x.Value.Kind}", 0.0)))
                .Where(x => x.Item2 != 0)
                .ToArray();
            var totalWeight = weights.Sum(x => x.Item2);
            var totalItems = itemSlots.Count;
            var itemCounts = weights
                .Select(x => (x.Item1, (int)Math.Ceiling(x.Item2 / totalWeight * totalItems)))
                .OrderBy(x => x.Item2)
                .ToArray();

            foreach (var (type, count) in itemCounts)
            {
                var itemDefinition = map.Items[type];
                for (var i = 0; i < count; i++)
                {
                    if (itemSlots.Count == 0)
                        break;

                    var globalId = itemSlots.Dequeue();
                    var amount = rng.Next(1, itemDefinition.Max + 1);
                    modBuilder.SetItem(globalId, new Item((byte)type, (ushort)amount));
                }
            }
        }
    }

    internal class ModBuilder
    {
        private readonly Dictionary<RdtItemId, DoorLock> _doorLock = new();
        private readonly Dictionary<int, Item> _itemMap = new();

        public ImmutableArray<int> AssignedItemGlobalIds => [.. _itemMap.Keys];

        public void SetDoorTarget(RdtItemId doorIdentity, RdtItemId target)
        {
        }

        public void SetDoorLock(RdtItemId doorIdentity, DoorLock doorLock)
        {
            _doorLock.Add(doorIdentity, doorLock);
        }

        public void SetItem(int globalId, Item item)
        {
            _itemMap.Add(globalId, item);
        }

        public IRdt ApplyToRdt(RdtId rdtId, IRdt rdt)
        {
            var opcodeBuilder = new OpcodeBuilder();
            rdt.ReadScript(opcodeBuilder);
            var opcodes = opcodeBuilder.ToArray();
            var doorOpcodes = opcodes.OfType<IDoorAotSetOpcode>().ToArray();
            var itemOpcodes = opcodes.OfType<IItemAotSetOpcode>().ToArray();

            var edits = false;
            foreach (var doorOpcode in doorOpcodes)
            {
                var doorIdentity = new RdtItemId(rdtId, doorOpcode.Id);
                if (_doorLock.TryGetValue(doorIdentity, out var doorLock))
                {
                    doorOpcode.LockId = (byte)doorLock.Id;
                    doorOpcode.LockType = (byte)doorLock.KeyItemId;
                    edits = true;
                }
            }
            foreach (var itemOpcode in itemOpcodes)
            {
                if (_itemMap.TryGetValue(itemOpcode.GlobalId, out var item))
                {
                    itemOpcode.Type = item.Type;
                    itemOpcode.Amount = item.Amount;
                    edits = true;
                }
            }

            if (!edits)
                return rdt;

            using (var ms = new MemoryStream(rdt.Data.ToArray()))
            {
                var bw = new BinaryWriter(ms);
                foreach (var opcode in opcodes)
                {
                    ms.Position = opcode.Offset;
                    opcode.Write(bw);
                }
                if (rdt.Version == BioVersion.Biohazard1)
                    return new Rdt1(ms.ToArray());
                else
                    throw new NotImplementedException();
            }
        }

        public string GetDump(Map map)
        {
            var sb = new StringBuilder();
            foreach (var kvp in _itemMap.OrderBy(x => x.Key))
            {
                var globalId = kvp.Key;
                var type = kvp.Value.Type;
                var amount = kvp.Value.Amount;

                var itemName = map.Items![type].Name;
                var slotName = GetItemSlotName(map, globalId);
                sb.AppendLine($"#{globalId}: {slotName} ======> {itemName} x{amount}");
            }
            return sb.ToString();
        }

        private static string? GetItemSlotName(Map map, int globalId)
        {
            if (map.Rooms == null)
                return null;

            foreach (var kvp in map.Rooms)
            {
                var room = kvp.Value;
                if (room.Items == null)
                    continue;

                foreach (var item in room.Items)
                {
                    if (item.GlobalId == globalId)
                    {
                        return $"{room.Name}/{item.Name}";
                    }
                }
            }

            return null;
        }
    }

    internal sealed class TempFolder : IDisposable
    {
        public string BasePath { get; }

        public TempFolder()
        {
            BasePath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            Directory.CreateDirectory(BasePath);
        }

        ~TempFolder() => Dispose();

        public void Dispose()
        {
            try
            {
                Directory.Delete(BasePath, true);
            }
            catch
            {
            }
        }

        public string GetOrCreateDirectory(string path)
        {
            var newPath = Path.Combine(BasePath, path);
            Directory.CreateDirectory(newPath);
            return newPath;
        }
    }

    [DebuggerDisplay("Id = {Id} Key = {KeyItemId}")]
    internal readonly struct DoorLock(int id, int keyItemId)
    {
        public int Id => id;
        public int KeyItemId => keyItemId;
    }
}
