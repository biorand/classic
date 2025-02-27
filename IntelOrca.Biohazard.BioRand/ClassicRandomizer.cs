using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using IntelOrca.Biohazard.BioRand.RE1;
using IntelOrca.Biohazard.BioRand.Routing;
using IntelOrca.Biohazard.Room;
using IntelOrca.Biohazard.Script.Opcodes;
using SixLabors.ImageSharp.PixelFormats;

namespace IntelOrca.Biohazard.BioRand
{
    public sealed class ClassicRandomizerFactory
    {
        public static ClassicRandomizerFactory Default { get; } = new ClassicRandomizerFactory();

        public IRandomizer Create(BioVersion version)
        {
            return new ClassicRandomizer(new Re1ClassicRandomizerController());
        }
    }

    internal class ClassicRandomizer(IClassicRandomizerController controller) : IRandomizer
    {
        public RandomizerConfigurationDefinition ConfigurationDefinition => CreateConfigDefinition();
        public RandomizerConfiguration DefaultConfiguration => ConfigurationDefinition.GetDefault();

        public string BuildVersion => GetGitHash();

        private static string GetGitHash()
        {
            var assembly = Assembly.GetExecutingAssembly();
            if (assembly == null)
                return string.Empty;

            var attribute = assembly
                .GetCustomAttributes<AssemblyInformationalVersionAttribute>()
                .FirstOrDefault();
            if (attribute == null)
                return string.Empty;

            var rev = attribute.InformationalVersion;
            var plusIndex = rev.IndexOf('+');
            if (plusIndex != -1)
            {
                return rev.Substring(plusIndex + 1);
            }
            return rev;
        }

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
                    Id = $"items/weapon/enabled/{itemDefinition.Kind}",
                    Label = itemDefinition.Name,
                    Type = "switch",
                    Default = true
                });
            }

            group = page.CreateGroup("Weapons (Initial Ammo)");
            foreach (var kvp in map.Items)
            {
                var itemDefinition = kvp.Value;
                var kind = itemDefinition.Kind;
                if (!kind.StartsWith("weapon/"))
                    continue;

                if (itemDefinition.Capacity == 0)
                    continue;

                group.Items.Add(new RandomizerConfigurationDefinition.GroupItem()
                {
                    Id = $"items/weapon/initial/min/{itemDefinition.Kind}",
                    Label = $"Min. {itemDefinition.Name}",
                    Min = 0,
                    Max = itemDefinition.Capacity,
                    Step = 1,
                    Type = "range",
                    Default = 0
                });
                group.Items.Add(new RandomizerConfigurationDefinition.GroupItem()
                {
                    Id = $"items/weapon/initial/max/{itemDefinition.Kind}",
                    Label = $"Max. {itemDefinition.Name}",
                    Min = 0,
                    Max = itemDefinition.Capacity,
                    Step = 1,
                    Type = "range",
                    Default = itemDefinition.Capacity,
                });
            }

            group = page.CreateGroup("Stack");
            foreach (var kvp in map.Items)
            {
                var itemDefinition = kvp.Value;
                var kind = itemDefinition.Kind;
                if (!kind.StartsWith("ammo/") && kind != "ink")
                    continue;

                group.Items.Add(new RandomizerConfigurationDefinition.GroupItem()
                {
                    Id = $"items/stack/min/{itemDefinition.Kind}",
                    Label = $"Min. {itemDefinition.Name}",
                    Min = 1,
                    Max = itemDefinition.Max,
                    Step = 1,
                    Type = "range",
                    Default = 1
                });
                group.Items.Add(new RandomizerConfigurationDefinition.GroupItem()
                {
                    Id = $"items/stack/max/{itemDefinition.Kind}",
                    Label = $"Max. {itemDefinition.Name}",
                    Min = 1,
                    Max = itemDefinition.Max,
                    Step = 1,
                    Type = "range",
                    Default = 15
                });
            }

            group = page.CreateGroup("Item Distribution");
            foreach (var kvp in map.Items)
            {
                var itemDefinition = kvp.Value;
                var kind = itemDefinition.Kind;
                if (!kind.StartsWith("ammo/") && !kind.StartsWith("health/") && kind != "ink")
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
            var dataManager = new DataManager(new[] {
                @"M:\git\biorand-classic\IntelOrca.Biohazard.BioRand\data"
            });
            var context = new Context(input.Configuration, dataManager);
            var map = GetMap(dataManager.GetJson<Map>(BioVersion.Biohazard1, "rdt.json"));

            var modBuilder = new ModBuilder();

            var lockRandomizer = new LockRandomizer();
            lockRandomizer.Randomise(input.Seed, map, modBuilder);

            var keyRandomizer = new KeyRandomizer();
            keyRandomizer.RandomiseItems(input.Seed, map, modBuilder);

            var itemRandomizer = new ItemRandomizer();
            itemRandomizer.Randomize(input.Configuration, input.Seed, map, modBuilder);

            var dump = modBuilder.GetDump(map);

            var gameData = controller.GetGameData(context, 0);
            foreach (var rrdt in gameData.Rdts)
            {
                modBuilder.ApplyToRdt(rrdt);
            }

            var crModBuilder = new ClassicRebirthModBuilder($"BioRand | Orca's Profile | {input.Seed}");
            crModBuilder.Module = new Module("biorand.dll", dataManager.GetData("biorand.dll"));
            crModBuilder.SetFile("biorand.dat", GetPatchFile(context));
            controller.WriteExtra(context, crModBuilder);

            foreach (var rrdt in gameData.Rdts)
            {
                rrdt.Save();
                crModBuilder.SetFile(rrdt.ModifiedPath!, rrdt.RdtFile.Data);
            }
            var archiveFile = crModBuilder.Create7z();
            var modFileName = $"mod_biorand_{input.Seed}.7z";
            return new RandomizerOutput(
                [new RandomizerOutputAsset("mod", "Classic Rebirth Mod", "Drop this in your RE 1 install folder.", modFileName, archiveFile)],
                "",
                new Dictionary<string, string>());
        }

        private byte[] GetPatchFile(IClassicRandomizerContext context)
        {
            using var ms = new MemoryStream();
            var pw = new PatchWriter(ms);
            controller.WritePatches(context, pw);
            return ms.ToArray();
        }

        private sealed class Context : IClassicRandomizerContext
        {
            public RandomizerConfiguration Configuration { get; }
            public DataManager DataManager { get; }

            public Context(RandomizerConfiguration configuration, DataManager dataManager)
            {
                Configuration = configuration;
                DataManager = dataManager;
            }
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
                            if (item.Optional == false)
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
            var rng = new Rng(seed);

            var documentPriority = config.GetValueOrDefault("items/documents", false)
                ? config.GetValueOrDefault("items/documents/keys", false)
                    ? ItemPriority.Normal
                    : ItemPriority.Low
                : ItemPriority.Disabled;
            var hiddenPriority = config.GetValueOrDefault("items/hidden/keys", false)
                ? ItemPriority.Normal
                : ItemPriority.Low;

            var itemSlots = new ItemSlotCollection(modBuilder, map, documentPriority, hiddenPriority, rng);

            // Weapons
            var weapons = map.Items
                .Where(x => x.Value.Kind.StartsWith("weapon/"))
                .Select(x => new WeaponInfo(x.Key, x.Value, config))
                .Shuffle(rng);
            var wpgroup = new HashSet<string>();
            var wpplaced = new List<WeaponInfo>();
            foreach (var wp in weapons)
            {
                if (!wp.Enabled)
                    continue;

                // Do not place weapons of the same group
                if (!wpgroup.Add(wp.Group))
                    continue;

                if (itemSlots.DequeueNormalFirst() is not int globalId)
                    break;

                var amount = rng.Next(wp.MinInitial, wp.MaxInitial + 1);
                modBuilder.SetItem(globalId, new Item((byte)wp.Type, (ushort)amount));
                wpplaced.Add(wp);
            }

            // Everything else
            var weights = map.Items
                .Select(x => (x.Key, x.Value, config.GetValueOrDefault($"items/distribution/{x.Value.Kind}", 0.0)))
                .Where(x => x.Item3 != 0)
                .ToArray();

            // Disable ammo that can't be used
            for (var i = 0; i < weights.Length; i++)
            {
                var itemType = weights[i].Item1;
                var definition = weights[i].Item2;
                if (definition.Kind.StartsWith("ammo/"))
                {
                    if (!wpplaced.Any(x => x.Enabled && x.SupportsAmmo(itemType)))
                    {
                        weights[i].Item3 = 0;
                    }
                }
            }

            var totalWeight = weights.Sum(x => x.Item3);
            var totalItems = itemSlots.Count;
            var itemCounts = weights
                .Select(x => (x.Item1, x.Item2, (int)Math.Ceiling(x.Item3 / totalWeight * totalItems)))
                .OrderBy(x => x.Item3)
                .ToArray();

            foreach (var (type, definition, count) in itemCounts)
            {
                var minStack = Math.Max(1, config.GetValueOrDefault($"items/stack/min/{definition.Kind}", 1));
                var maxStack = Math.Min(definition.Max, config.GetValueOrDefault($"items/stack/max/{definition.Kind}", definition.Max));
                for (var i = 0; i < count; i++)
                {
                    if (itemSlots.DequeueAny() is not int globalId)
                        break;

                    var amount = rng.Next(minStack, maxStack + 1);
                    modBuilder.SetItem(globalId, new Item((byte)type, (ushort)amount));
                }
            }

            // Set all remaining items to empty
            while (itemSlots.DequeueAny() is int globalId)
            {
                modBuilder.SetItem(globalId, new Item(0, 0));
            }
        }

        public class ItemSlotCollection
        {
            private readonly Rng _rng;
            private readonly Queue<MapRoomItem> _normalPriority = [];
            private readonly Queue<MapRoomItem> _lowPriority = [];

            public int Count => _normalPriority.Count + _lowPriority.Count;

            public ItemSlotCollection(ModBuilder modBuilder, Map map, ItemPriority documents, ItemPriority hidden, Rng rng)
            {
                _rng = rng;

                var assigned = modBuilder.AssignedItemGlobalIds.ToHashSet();
                var allItems = map.Rooms.Values
                    .SelectMany(x => x.Items)
                    .Where(x => x.GlobalId != null)
                    .Shuffle(rng);

                foreach (var item in allItems)
                {
                    if (assigned.Contains(item.GlobalId ?? 0))
                        continue;

                    if (item.Document == true && documents == ItemPriority.Disabled)
                        continue;
                    if (item.Hidden == true && hidden == ItemPriority.Disabled)
                        continue;

                    if (item.Optional == true)
                        _lowPriority.Enqueue(item);
                    else if (item.Document == true && documents == ItemPriority.Low)
                        _lowPriority.Enqueue(item);
                    else if (item.Hidden == true && hidden == ItemPriority.Low)
                        _lowPriority.Enqueue(item);
                    else
                        _normalPriority.Enqueue(item);
                }
            }

            public int? DequeueNormalFirst()
            {
                if (_normalPriority.Count != 0)
                    return _normalPriority.Dequeue().GlobalId;
                else if (_lowPriority.Count != 0)
                    return _lowPriority.Dequeue().GlobalId;
                else
                    return null;
            }

            public int? DequeueAny()
            {
                var count = Count;
                if (count == 0)
                    return null;

                var index = _rng.Next(0, count);
                if (index >= _normalPriority.Count)
                    return _lowPriority.Dequeue().GlobalId;
                else
                    return _normalPriority.Dequeue().GlobalId;
            }
        }

        public enum ItemPriority
        {
            Disabled,
            Low,
            Normal
        }

        public sealed class WeaponInfo(int type, MapItemDefinition definition, RandomizerConfiguration config)
        {
            public int Type => type;
            public MapItemDefinition Definition => definition;
            public string Group { get; } = definition.Kind.Split('/').Skip(1).First();
            public bool Enabled { get; } = config.GetValueOrDefault($"items/weapon/enabled/{definition.Kind}", false);
            public int MinInitial { get; } = config.GetValueOrDefault($"items/weapon/initial/min/{definition.Kind}", 0);
            public int MaxInitial { get; } = config.GetValueOrDefault($"items/weapon/initial/max/{definition.Kind}", 0);
            public bool SupportsAmmo(int itemId) => definition.Ammo != null && definition.Ammo.Contains(itemId);
        }
    }

    internal readonly struct Module(string fileName, byte[] data)
    {
        public string FileName => fileName;
        public byte[] Data => data;
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

        public void ApplyToRdt(RandomizedRdt rrdt)
        {
            foreach (var doorOpcode in rrdt.Doors)
            {
                var doorIdentity = new RdtItemId(rrdt.RdtId, doorOpcode.Id);
                if (_doorLock.TryGetValue(doorIdentity, out var doorLock))
                {
                    doorOpcode.LockId = (byte)doorLock.Id;
                    doorOpcode.LockType = (byte)doorLock.KeyItemId;
                }
            }
            foreach (var itemOpcode in rrdt.Items)
            {
                if (_itemMap.TryGetValue(itemOpcode.GlobalId, out var item))
                {
                    itemOpcode.Type = item.Type;
                    itemOpcode.Amount = item.Amount;
                }
            }
        }

        public string GetDump(Map map)
        {
            var sb = new StringBuilder();
            foreach (var kvp in _itemMap
                .OrderBy(x => x.Value.Type)
                .ThenBy(x => x.Key))
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

    [DebuggerDisplay("Id = {Id} Key = {KeyItemId}")]
    internal readonly struct DoorLock(int id, int keyItemId)
    {
        public int Id => id;
        public int KeyItemId => keyItemId;
    }

    internal interface IClassicRandomizerContext
    {
        public RandomizerConfiguration Configuration { get; }
        public DataManager DataManager { get; }
    }

    internal interface IClassicRandomizerController
    {
        public GameData GetGameData(IClassicRandomizerContext context, int player);
        void WritePatches(IClassicRandomizerContext context, PatchWriter pw);
        void WriteExtra(IClassicRandomizerContext context, ClassicRebirthModBuilder crModBuilder);
    }

    internal class Re1ClassicRandomizerController : IClassicRandomizerController
    {
        public GameData GetGameData(IClassicRandomizerContext context, int player)
        {
            var result = new List<RandomizedRdt>();
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

                            var rdt = Rdt.FromData(BioVersion.Biohazard1, fileData);
                            var rrdt = new RandomizedRdt(rdt, rdtId);
                            result.Add(rrdt);
                        }
                    }
                }
            }

            foreach (var missingRoom in g_missingRooms)
            {
                var mansion2 = new RdtId(missingRoom.Stage + 5, missingRoom.Room);
                var rrdt2 = result.FirstOrDefault(x => x.RdtId == mansion2);
                result.Add(new RandomizedRdt(rrdt2.RdtFile, missingRoom));
            }

            foreach (var rrdt in result)
            {
                var rdtId = rrdt.RdtId;
                rrdt.OriginalPath = $"STAGE{rdtId.Stage + 1}/ROOM{rdtId}0.RDT";
                rrdt.ModifiedPath = rrdt.OriginalPath;
                rrdt.Load();
            }

            var gd = new GameData([.. result]);
            ApplyRdtPatches(context, gd, player);
            return gd;
        }

        private void ApplyRdtPatches(IClassicRandomizerContext context, GameData gameData, int player)
        {
            const byte PassCodeDoorLockId = 209;
            var randomDoors = context.Configuration.GetValueOrDefault("doors/random", false);
            var randomItems = context.Configuration.GetValueOrDefault("items/random", false);

            FixPassCodeDoor();
            AllowRoughPassageDoorUnlock();
            ShotgunOnWallFix();
            DisableBarryEvesdrop();
            AllowPartnerItemBoxes();

            void FixPassCodeDoor()
            {
                for (var mansion = 0; mansion < 2; mansion++)
                {
                    var mansionOffset = mansion == 0 ? 0 : 5;
                    var rdt = gameData.GetRdt(new RdtId(1 + mansionOffset, 0x01));
                    if (rdt == null)
                        return;

                    var door = rdt.Doors.FirstOrDefault(x => x.Id == 1) as DoorAotSeOpcode;
                    if (door == null)
                        return;

                    door.LockId = PassCodeDoorLockId;
                    door.NextX = 11200;
                    door.NextZ = 28000;
                    door.LockType = 255;
                    door.Free = 129;

                    if (!randomDoors && player == 1)
                    {
                        rdt.AdditionalOpcodes.Add(new UnknownOpcode(0, 0x01, new byte[] { 0x0A }));
                        rdt.AdditionalOpcodes.Add(new UnknownOpcode(0, 0x04, new byte[] { 0x01, 0x25, 0x00 }));
                        rdt.AdditionalOpcodes.Add(new UnknownOpcode(0, 0x05, new byte[] { 0x02, PassCodeDoorLockId - 192, 0 }));
                        rdt.AdditionalOpcodes.Add(new UnknownOpcode(0, 0x03, new byte[] { 0x00 }));
                    }

                    rdt.Nop(0x41A34);

                    if (mansion == 1)
                    {
                        if (player == 0)
                        {
                            rdt.Nop(0x41A92);
                        }
                        else
                        {
                            rdt.Nop(0x41A5C, 0x41A68);
                        }
                    }
                }
            }

            void AllowRoughPassageDoorUnlock()
            {
                for (var mansion = 0; mansion < 2; mansion++)
                {
                    var mansionOffset = mansion == 0 ? 0 : 5;
                    var rdt = gameData.GetRdt(new RdtId(1 + mansionOffset, 0x14));
                    if (rdt == null)
                        return;

                    var doorId = player == 0 ? 1 : 5;
                    var door = (DoorAotSeOpcode)rdt.ConvertToDoor((byte)doorId, 0, 254, PassCodeDoorLockId);
                    door.Special = 2;
                    door.Re1UnkC = 1;
                    door.Target = new RdtId(0xFF, 0x01);
                    door.NextX = 15500;
                    door.NextZ = 25400;
                    door.NextD = 1024;

                    if (player == 1)
                    {
                        rdt.Nop(0x19F3A);
                        rdt.Nop(0x1A016);
                        rdt.Nop(0x1A01C);
                    }
                }
            }

            void ShotgunOnWallFix()
            {
                if (!randomItems)
                    return;

                var rdt = gameData.GetRdt(new RdtId(0, 0x16));
                if (rdt == null)
                    return;

                rdt.Nop(0x1FE16);
            }

            void DisableBarryEvesdrop()
            {
                if (player != 1)
                    return;

                var rdt = gameData.GetRdt(new RdtId(3, 0x05));
                if (rdt == null)
                    return;

                rdt.Nop(0x194A2);
            }

            void AllowPartnerItemBoxes()
            {
                // Remove partner check for these two item boxes
                // This is so Rebecca can use the item boxes
                // Important for Chris 8-inventory because the inventory
                // is now shared for both him and Rebecca and player
                // might need to make space for more items e.g. (V-JOLT)
                var room = gameData.GetRdt(new RdtId(0, 0x00));
                room?.Nop(0x10C92);

                room = gameData.GetRdt(new RdtId(3, 0x03));
                room?.Nop(0x1F920);
            }

        }

        public void WritePatches(IClassicRandomizerContext context, PatchWriter pw)
        {
            var randomDoors = context.Configuration.GetValueOrDefault("doors/random", false);

            DisableDemo(pw);
            FixFlamethrowerCombine(pw);
            FixWasteHeal(pw);
            FixNeptuneDamage(pw);
            FixChrisInventorySize(pw);
            FixYawnPoison(pw, randomDoors);
        }

        private static void DisableDemo(PatchWriter pw)
        {
            pw.Begin(0x48E031);
            pw.Write(0x90);
            pw.Write(0x90);
            pw.Write(0x90);
            pw.Write(0x90);
            pw.Write(0x90);
            pw.Write(0x90);
            pw.Write(0x90);
            pw.End();
        }

        private static void FixFlamethrowerCombine(PatchWriter pw)
        {
            // and bx, 0x7F -> nop
            pw.Begin(0x4483BD);
            pw.Write(0x90);
            pw.Write(0x90);
            pw.Write(0x90);
            pw.Write(0x90);
            pw.End();

            // and bx, 0x7F -> nop
            pw.Begin(0x44842D);
            pw.Write(0x90);
            pw.Write(0x90);
            pw.Write(0x90);
            pw.Write(0x90);
            pw.End();
        }

        private static void FixWasteHeal(PatchWriter pw)
        {
            // Allow using heal items when health is at max
            // jge 0447AA2h -> nop
            pw.Begin(0x447A39);
            pw.Write(0x90);
            pw.Write(0x90);
            pw.End();
        }

        private static void FixNeptuneDamage(PatchWriter pw)
        {
            // Neptune has no death routine, so replace it with Cerberus's
            // 0x4AA0EC -> 0x004596D0
            pw.Begin(0x4AA0EC);
            pw.Write32(0x004596D0);
            pw.End();

            // Give Neptune a damage value for each weapon
            const int numWeapons = 10;
            const int entrySize = 12;
            var damageValues = new short[] { 16, 14, 32, 40, 130, 20, 100, 200, 100, 900 };
            var enemyDataArrays = new uint[] { 0x4AF908U, 0x4B0268 };
            foreach (var enemyData in enemyDataArrays)
            {
                var neptuneData = enemyData + (Re1EnemyIds.Neptune * (numWeapons * entrySize)) + 0x06;
                for (var i = 0; i < numWeapons; i++)
                {
                    pw.Begin(neptuneData);
                    pw.Write16(damageValues[i]);
                    pw.End();
                    neptuneData += entrySize;
                }
            }
        }

        private static void FixChrisInventorySize(PatchWriter pw)
        {
            // Inventory instructions
            var addresses = new uint[]
            {
                0x40B461,
                0x40B476,
                0x40B483,
                0x414103,
                0x414022,
                0x4142CC
            };
            foreach (var addr in addresses)
            {
                pw.Begin(addr);
                pw.Write(0xB0);
                pw.Write(0x01);
                pw.Write(0x90);
                pw.Write(0x90);
                pw.Write(0x90);
                pw.End();
            }

            // Partner swap
            pw.Begin(0x0041B208);
            pw.Write(0xC7);
            pw.Write(0x05);
            pw.Write32(0x00AA8E48);
            pw.Write32(0x00C38814);
            pw.End();

            // Rebirth
            pw.Begin(0x100505A3);
            pw.Write(0xB8);
            pw.Write(0x01);
            pw.Write(0x00);
            pw.Write(0x00);
            pw.Write(0x00);
            pw.Write(0x90);
            pw.Write(0x90);
            pw.End();

            pw.Begin(0x1006F0C2 + 3);
            pw.Write(0x8);
            pw.End();
        }

        private static void FixYawnPoison(PatchWriter pw, bool doorRandomizer)
        {
            const byte ST_POISON = 0x02;
            const byte ST_POISON_YAWN = 0x20;

            pw.Begin(0x45B8C0 + 6); // 80 0D 90 52 C3 00 20
            if (doorRandomizer)
                pw.Write(ST_POISON);
            else
                pw.Write(ST_POISON_YAWN);
            pw.End();
        }

        public void WriteExtra(IClassicRandomizerContext context, ClassicRebirthModBuilder crModBuilder)
        {
            var bgPng = context.DataManager.GetData(BioVersion.Biohazard1, "bg.png");
            var bgPix = PngToPix(bgPng);
            crModBuilder.SetFile("data/title.pix", bgPix);
            crModBuilder.SetFile("type.png", bgPng);
        }

        private byte[] PngToPix(byte[] png)
        {
            using var img = SixLabors.ImageSharp.Image.Load<Rgba32>(png);
            using var ms = new MemoryStream();
            var bw = new BinaryWriter(ms);
            img.ProcessPixelRows(accessor =>
            {
                for (var y = 0; y < accessor.Height; y++)
                {
                    var row = accessor.GetRowSpan(y);
                    for (int i = 0; i < accessor.Width; i++)
                    {
                        var c = row[i];
                        var c4 = (ushort)((c.R / 8) | ((c.G / 8) << 5) | ((c.B / 8) << 10));
                        bw.Write(c4);
                    }
                }
            });
            return ms.ToArray();
        }

        private static readonly RdtId[] g_missingRooms =
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
    }
}
