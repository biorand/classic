using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Xml;
using IntelOrca.Biohazard.BioRand.RE1;
using IntelOrca.Biohazard.Extensions;
using IntelOrca.Biohazard.Room;
using IntelOrca.Biohazard.Script;
using IntelOrca.Biohazard.Script.Opcodes;
using SixLabors.ImageSharp.PixelFormats;

namespace IntelOrca.Biohazard.BioRand
{
    internal class Re1ClassicRandomizerController : IClassicRandomizerController
    {
        public ImmutableArray<string> VariationNames { get; } = ["Chris", "Jill"];

        public void UpdateConfigDefinition(RandomizerConfigurationDefinition definition)
        {
            var page = definition.Pages.First(x => x.Label == "General");

            page.Groups[0].Items.Add(new RandomizerConfigurationDefinition.GroupItem()
            {
                Id = "cutscenes/disable",
                Label = "Remove Alpha Team",
                Description = "Remove the other members of the Alpha team, i.e. Chris, Jill, Rebecca, Barry, and Wesker. Disables all related cutscenes.",
                Type = "switch",
                Default = false
            });
            page.Groups[0].Items.Add(new RandomizerConfigurationDefinition.GroupItem()
            {
                Id = "ink/enable",
                Label = "Requires Ink Ribbons to Save",
                Description = "If disabled, no ink ribbons will be placed, and saving can be done at any time unlimited times at a typewriter. Default will enable it for Chris, but not Jill.",
                Type = "dropdown",
                Options = ["Default", "Never", "Always", "Random"],
                Default = false
            });

            page.Groups.Insert(1, new RandomizerConfigurationDefinition.Group("Progression (non-door randomizer)")
            {
                Items =
                [
                    new RandomizerConfigurationDefinition.GroupItem()
                    {
                        Id = "progression/guardhouse",
                        Label = "Guardhouse",
                        Description = "Include the guardhouse in the randomizer. If disabled, the gates to the guardhouse will be locked.",
                        Type = "switch",
                        Default = true
                    },
                    new RandomizerConfigurationDefinition.GroupItem()
                    {
                        Id = "progression/lab",
                        Label = "Lab",
                        Description = "Include the lab in the randomizer. If disabled, there will be no doom books, and you can go straight to the heliport when you reach the fountain.",
                        Type = "switch",
                        Default = true
                    },
                    new RandomizerConfigurationDefinition.GroupItem()
                    {
                        Id = "progression/mansion/split",
                        Label = "Split Mansion",
                        Description =
                            "Split the mansion into two segments, before and after plant 42. " +
                            "The helmet key will be behind plant 42, and the battery will be in a mansion 2 room. " +
                            "Only applicable if guardhouse is enabled.",
                        Type = "switch",
                        Default = false
                    },
                    new RandomizerConfigurationDefinition.GroupItem()
                    {
                        Id = "progression/guardhouse/segmented",
                        Label = "Segmented Guardhouse",
                        Description = "Isolate the guardhouse in the randomizer. If enabled, the guardhouse will be a standalone segment.",
                        Type = "switch",
                        Default = false
                    },
                    new RandomizerConfigurationDefinition.GroupItem()
                    {
                        Id = "progression/caves/segmented",
                        Label = "Segmented Caves",
                        Description = "Isolate the caves in the randomizer. If enabled, the caves will be a standalone segment.",
                        Type = "switch",
                        Default = true
                    },
                    new RandomizerConfigurationDefinition.GroupItem()
                    {
                        Id = "progression/lab/segmented",
                        Label = "Segmented Lab",
                        Description = "Isolate the lab in the randomizer. If enabled, the lab will be a standalone segment.",
                        Type = "switch",
                        Default = true
                    },
                    // new RandomizerConfigurationDefinition.GroupItem()
                    // {
                    //     Id = "progression/guardhouse/plant42",
                    //     Label = "Mandatory Plant 42",
                    //     Description = "Plant 42 must be defeated to complete the randomizer. If disabled, it may optional for some seeds.",
                    //     Type = "switch",
                    //     Default = false
                    // },
                    // new RandomizerConfigurationDefinition.GroupItem()
                    // {
                    //     Id = "progression/mansion/yawn2",
                    //     Label = "Mandatory Yawn",
                    //     Description = "Yawn must be defeated to complete the randomizer. If disabled, it may optional for some seeds.",
                    //     Type = "switch",
                    //     Default = false
                    // },
                    new RandomizerConfigurationDefinition.GroupItem()
                    {
                        Id = "progression/lab/tyrant",
                        Label = "Mandatory Tyrant 1",
                        Description = "Tyrant 1 must be defeated to complete the randomizer. If disabled, it may optional for some seeds.",
                        Type = "switch",
                        Default = false
                    },
                    new RandomizerConfigurationDefinition.GroupItem()
                    {
                        Id = "progression/heliport/tyrant",
                        Label = "Mandatory Tyrant 2",
                        Description = "Tyrant 2 must be defeated to complete the randomizer. If disabled, it may optional for some seeds.",
                        Type = "switch",
                        Default = false
                    }
                ]
            });

            page = definition.Pages.First(x => x.Label == "Inventory");
            var group = page.Groups.First(x => x.Label == "Main");
            group.Items.AddRange([
                new RandomizerConfigurationDefinition.GroupItem()
                {
                    Id = "inventory/special/lockpick",
                    Label = "Lockpick",
                    Description = "Allows you to open locked drawers and sword key doors. Default will leave Jill with lockpick, Chris without.",
                    Type = "dropdown",
                    Options = ["Default", "Random", "Never", "Always"],
                    Default = "Default"
                },
                new RandomizerConfigurationDefinition.GroupItem()
                {
                    Id = "inventory/size",
                    Label = "Size",
                    Description = "Control how many inventory slots you have. Default will leave Chris with 6 slots, Jill with 8.",
                    Type = "dropdown",
                    Options = ["Default", "Random", "6", "8"],
                    Default = "Default"
                }]);
        }

        public Variation GetVariation(IClassicRandomizerContext context, string name)
        {
            var playerIndex = VariationNames.IndexOf(name);
            if (playerIndex == -1)
                playerIndex = 0;

            return new Variation(playerIndex, VariationNames[playerIndex], GetMap(context, playerIndex));
        }

        public Map GetMap(IClassicRandomizerContext context, int playerIndex)
        {
            var rng = context.Rng.NextFork();
            var config = context.Configuration;

            // Apply player, scenario filter
            var map = context.DataManager.GetJson<Map>(BioVersion.Biohazard1, "rdt.json");
            map = map.For(new MapFilter(false, (byte)playerIndex, 0));

            // All original locks had 0x80 (some had 0x40 but we can ignore those)
            foreach (var d in map.Rooms.Values.SelectMany(x => x.Doors))
            {
                if (d.LockId is byte lockId)
                {
                    d.LockId = (byte)(0x80 | lockId);
                }
            }

            // Lockpick, remove all sword key and small key requirements
            var enableLockpick = context.Configuration.GetValueOrDefault("inventory/special/lockpick", "Always") == "Always";
            if (enableLockpick)
            {
                var allEdges = map.Rooms.SelectMany(x => x.Value.AllEdges).ToArray();
                foreach (var edge in allEdges)
                {
                    if (edge.Requires2 != null)
                    {
                        edge.Requires2 = edge.Requires2
                            .Where(x => x != "item(51)" && x != "item(61)")
                            .ToArray();
                    }
                }
            }

            // Ensure doors required for prologue cutscenes are not locked
            if (!context.Configuration.GetValueOrDefault("cutscenes/disable", false))
            {
                var prologueDoors = map.Rooms.Values
                    .SelectMany(x => x.Doors)
                    .Where(x => x.HasTag(MapTags.Prologue))
                    .ToArray();
                foreach (var d in prologueDoors)
                {
                    d.AllowedLocks = [];
                }
            }

            const int GROUP_ALL = -1;
            const int GROUP_MANSION_1 = 1;
            const int GROUP_MANSION_2 = 2;
            // const int GROUP_COURTYARD = 4;
            const int GROUP_GUARDHOUSE = 8;
            // const int GROUP_CAVES = 16;
            const int GROUP_LAB = 32;
            const int GROUP_PLANT_42 = 64;
            const int GROUP_SMALL_KEY = 128;
            const int GROUP_BATTERY = 256;
            const int GROUP_LAB_TYRANT = 512;

            // Enable / disable guardhouse rooms
            if (!config.GetValueOrDefault("progression/guardhouse", false))
            {
                if (config.GetValueOrDefault("progression/mansion/split", false))
                {
                    throw new RandomizerUserException("Split mansion requires guardhouse to be enabled.");
                }

                var guardhouseRooms = map.Rooms.Where(x => x.Value.HasTag("guardhouse")).ToArray();
                foreach (var r in guardhouseRooms)
                {
                    map.Rooms.Remove(r.Key);
                }
                var courtyardRoom = map.Rooms["302"];
                var gate = courtyardRoom.Doors.First(x => x.Name == "GATE TO GUARDHOUSE");
                gate.Target = null;
            }

            // Enable / disable lab rooms
            if (config.GetValueOrDefault("progression/lab", false))
            {
                var fountainRoom = map.Rooms["305"];
                var fountainDoor = fountainRoom.Doors.First(x => x.Name == "DOOR TO HELIPORT");
                fountainDoor.Kind = "locked";
                fountainDoor.AllowedLocks = [];

                var helipadRoom = map.Rooms["303"];
                var helipadDoor = helipadRoom.Doors.First(x => x.Name == "DOOR TO FOUNTAIN");
                helipadDoor.Kind = "unlock";
                fountainDoor.AllowedLocks = [];
            }
            else
            {
                var labRooms = map.Rooms.Where(x => x.Value.HasTag("lab")).ToArray();
                foreach (var r in labRooms)
                {
                    map.Rooms.Remove(r.Key);
                }
                var fountainRoom = map.Rooms["305"];
                var fountainDoor = fountainRoom.Doors.First(x => x.Name == "FOUNTAIN STAIRS");
                fountainDoor.Target = null;
                fountainDoor.Requires2 = [];

                var fountainToHeliportDoor = fountainRoom.Doors.First(x => x.Name == "DOOR TO HELIPORT");
                fountainToHeliportDoor.Kind = null;

                var helipadRoom = map.Rooms["303"];
                var helipadToFountainDoor = helipadRoom.Doors.First(x => x.Name == "DOOR TO FOUNTAIN");
                helipadToFountainDoor.Kind = null;

                var liftToLabDoor = helipadRoom.Doors.First(x => x.Name == "LIFT TO LAB");
                liftToLabDoor.Target = null;
            }

            // Locks
            var mansion2keyType = 54;
            if (context.Configuration.GetValueOrDefault("locks/random", false))
            {
                var genericKeys = map.Items.Where(x => x.Value.Discard).Shuffle(rng);
                if (context.Configuration.GetValueOrDefault("locks/preserve", false))
                {
                    genericKeys = genericKeys.Where(x => x.Key != mansion2keyType).ToArray();
                }
                else
                {
                    // Set all mansion 2 rooms to mansion 1
                    foreach (var item in map.Rooms.Values.SelectMany(x => x.Items ?? []))
                    {
                        if (item.Group == GROUP_MANSION_2)
                        {
                            item.Group = GROUP_MANSION_1;
                        }
                    }

                    if (config.GetValueOrDefault("progression/mansion/split", false))
                    {
                        var mansion2key = genericKeys.FirstOrDefault();
                        mansion2keyType = mansion2key.Key;
                        genericKeys = genericKeys.Skip(1).ToArray();

                        var mansion2rooms = map.Rooms.Values
                            .Where(x => x.HasTag("mansion2able"))
                            .Shuffle(rng);
                        var numMansion2rooms = rng.Next(2, 9);
                        mansion2rooms = mansion2rooms.Take(numMansion2rooms).ToArray();

                        var itemLockIds = map.Rooms.Values
                            .SelectMany(x => x.Items ?? [])
                            .Where(x => x.LockId != null)
                            .Select(x => x.LockId!.Value)
                            .ToArray();
                        var usedLockIds = map.Rooms.Values
                            .SelectMany(x => x.Doors ?? [])
                            .Where(x => x.LockId != null)
                            .Select(x => (int)x.LockId!.Value)
                            .Concat(itemLockIds)
                            .ToHashSet();
                        var lockIds = Enumerable.Range(0, 63)
                            .Except(usedLockIds)
                            .ToQueue();

                        foreach (var r in mansion2rooms)
                        {
                            foreach (var i in r.Items ?? [])
                            {
                                i.Group = GROUP_MANSION_2;
                            }
                            foreach (var d in r.Doors ?? [])
                            {
                                if (d.Target == null)
                                    continue;

                                d.AllowedLocks = [];
                                d.Requires2 = [$"item({mansion2key.Key})"];
                                d.LockId = (byte)lockIds.Dequeue();
                                d.LockKey = mansion2key.Key;

                                var otherDoor = map.GetOtherSide(d);
                                if (otherDoor != null)
                                {
                                    otherDoor.AllowedLocks = [];
                                    otherDoor.Requires2 = d.Requires2;
                                    otherDoor.LockId = d.LockId;
                                    otherDoor.LockKey = d.LockKey;
                                }
                            }
                        }
                    }
                }

                // Areas:
                // MANSION | CAVES | GUARDHOUSE | LAB
                var areaTags = new List<string[]>([
                    ["mansion", "courtyard"],
                    ["caves"]]);
                if (config.GetValueOrDefault("progression/guardhouse", true))
                {
                    areaTags.Add(["guardhouse"]);
                }
                if (config.GetValueOrDefault("progression/lab", true))
                {
                    areaTags.Add(["lab"]);
                }
                var locks = genericKeys
                    .Select(x => new DistributedLock(x.Value.Name, x.Key))
                    .ToArray();
                foreach (var l in locks)
                {
                    var b = rng.Next(0, 0b1111);
                    for (var i = 0; i < 4; i++)
                    {
                        if ((b & (1 << i)) != 0 && areaTags.Count > i)
                        {
                            l.Tags.AddRange(areaTags[i]);
                        }
                    }
                }

                foreach (var r in map.Rooms.Values)
                {
                    foreach (var d in r.Doors ?? [])
                    {
                        if (d.NoUnlock || d.AllowedLocks != null)
                            continue;

                        var otherDoor = map.GetOtherSide(d);
                        if (otherDoor == null || otherDoor.NoUnlock || otherDoor.AllowedLocks != null)
                            continue;

                        d.Kind = null;
                        d.AllowedLocks = locks
                            .Where(x => x.SupportsRoom(r))
                            .Select(x => x.Key)
                            .ToArray();

                        otherDoor.Kind = null;
                        otherDoor.AllowedLocks = d.AllowedLocks;
                    }
                }
            }

            // Restrict items, set no return points
            var keys = map.Items!.Values;
            var items = map.Rooms!.Values.SelectMany(x => x.Items).ToArray();
            var guardhouseKeys = keys.Where(x => x.Group == 8).ToArray();
            var guardhouseItems = items.Where(x => x.Group == GROUP_GUARDHOUSE).ToArray();
            var mansion2Items = items.Where(x => x.Group == GROUP_MANSION_2).ToArray();
            var labItems = items.Where(x => x.Group == GROUP_LAB).ToArray();
            var tyrantItems = items.Where(x => x.Group == GROUP_LAB_TYRANT).ToArray();

            foreach (var item in items)
                item.Group = GROUP_ALL;

            if (config.GetValueOrDefault("progression/guardhouse", true) &&
                config.GetValueOrDefault("progression/guardhouse/segmented", false))
            {
                if (context.Configuration.GetValueOrDefault("locks/random", false))
                {
                    throw new RandomizerUserException("Segmented guardhouse with lock randomizer not implemented yet.");
                }

                // Only guardhouse can contain guardhouse keys
                foreach (var item in items)
                    item.Group &= ~GROUP_GUARDHOUSE;
                foreach (var item in guardhouseItems)
                    item.Group = GROUP_GUARDHOUSE | GROUP_SMALL_KEY;
            }

            if (config.GetValueOrDefault("progression/mansion/split", false))
            {
                // Mansion 2 (helmet key in plant 42)
                foreach (var item in items)
                    item.Group &= ~GROUP_PLANT_42;
                var plant42item = map.Rooms!["40C"].Items.First(x => x.Name == "KEY IN FIREPLACE");
                plant42item.Group = GROUP_PLANT_42;
                map.Items[mansion2keyType].Group = GROUP_PLANT_42;

                // Battery restricted to mansion 2 (and lab obviously)
                foreach (var item in items)
                    item.Group &= ~GROUP_BATTERY;
                map.Items[39].Group = GROUP_BATTERY;
                foreach (var item in mansion2Items)
                    item.Group |= GROUP_BATTERY;
                foreach (var item in labItems)
                    item.Group |= GROUP_BATTERY;
            }

            if (config.GetValueOrDefault("progression/lab", true) &&
                config.GetValueOrDefault("progression/lab/tyrant", false))
            {
                // To ensure player has to fight tyrant, place flare there
                map.Items[42].Group = GROUP_LAB_TYRANT;
                foreach (var item in items)
                    item.Group &= ~GROUP_LAB_TYRANT;
                foreach (var item in tyrantItems)
                    item.Group = GROUP_LAB_TYRANT;
            }

            if (config.GetValueOrDefault("progression/caves/segmented", false))
            {
                var caveDoor = map.Rooms!["302"].Doors.First(x => x.Name == "LADDER TO CAVES");
                caveDoor.Kind = "noreturn";

                map.GetOtherSide(caveDoor)!.Kind = "blocked";
            }

            if (config.GetValueOrDefault("progression/lab", true) &&
                config.GetValueOrDefault("progression/lab/segmented", false))
            {
                var labDoor = map.Rooms!["305"].Doors.First(x => x.Name == "FOUNTAIN STAIRS");
                labDoor.Kind = "noreturn";

                map.GetOtherSide(labDoor)!.Kind = "blocked";
            }

            return map;
        }

        private class DistributedLock(string name, int key)
        {
            public string Name => name;
            public int Key => key;
            public List<string> Tags { get; set; } = [];

            public bool SupportsRoom(MapRoom room) => Tags.Count == 0 || room.HasAnyTag(Tags);
            public override string ToString() => $"Key = {Name} Tags = [{string.Join(", ", Tags)}]";
        }

        public static GameData GetGameData(DataManager gameDataManager, int player)
        {
            var result = new List<RandomizedRdt>();
            for (var i = 1; i <= 7; i++)
            {
                var files = gameDataManager.GetFiles($"JPN/STAGE{i}");
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
                            var fileData = gameDataManager.GetData(path);
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
                rrdt.OriginalPath = $"STAGE{rdtId.Stage + 1}/ROOM{rdtId}{player}.RDT";
                rrdt.Load();
            }

            var gd = new GameData([.. result]);
            return gd;
        }

        private void ApplyRdtPatches(IClassicRandomizerGeneratedVariation context, GameData gameData, int player)
        {
            const byte PassCodeDoorLockId = 209;
            var randomDoors = context.Configuration.GetValueOrDefault("doors/random", false);
            var randomItems = context.Configuration.GetValueOrDefault("items/random", false);

            EnableMoreJillItems();
            DisableDogWindows();
            DisableDogBoiler();
            AddDoor207();
            FixDoor104();
            FixDoorToWardrobe();
            FixPassCodeDoor();
            FixDrugStoreRoom();
            AllowRoughPassageDoorUnlock();
            ShotgunOnWallFix();
            DisablePoisonChallenge();
            DisableBarryEvesdrop();
            AllowPartnerItemBoxes();
            EnableFountainHeliportDoors();
            ForceHelipadTyrant();

            void EnableMoreJillItems()
            {
                // gameData.GetRdt(RdtId.Parse("106"))?.Patches.Add(new KeyValuePair<int, byte>(0x2FC02 + 1, 7));
                // gameData.GetRdt(RdtId.Parse("106"))?.Patches.Add(new KeyValuePair<int, byte>(0x2FC02 + 2, 52));
                // gameData.GetRdt(RdtId.Parse("106"))?.Patches.Add(new KeyValuePair<int, byte>(0x2FC02 + 3, 0));
                // gameData.GetRdt(RdtId.Parse("106"))?.Nop(0x2FC06);
                // gameData.GetRdt(RdtId.Parse("106"))?.Patches.Add(new KeyValuePair<int, byte>(0x2FFBC + 1, 7));
                // gameData.GetRdt(RdtId.Parse("106"))?.Patches.Add(new KeyValuePair<int, byte>(0x2FFBC + 2, 52));
                // gameData.GetRdt(RdtId.Parse("106"))?.Patches.Add(new KeyValuePair<int, byte>(0x2FFBC + 3, 0));
                // gameData.GetRdt(RdtId.Parse("106"))?.Nop(0x2FFC0);
                // gameData.GetRdt(RdtId.Parse("106"))?.Nop(0x31862);
            }

            void DisableDogWindows()
            {
                var rdt108 = gameData.GetRdt(RdtId.Parse("108"));
                rdt108?.Nop(0x19754, 0x197EE);
            }

            void DisableDogBoiler()
            {
                var rdt114 = gameData.GetRdt(RdtId.Parse("114"));
                rdt114?.Nop(0x24B80, 0x24C1C);
            }

            void AddDoor207()
            {
                foreach (var rtdId in new[] { "207", "707" })
                {
                    var rdt207 = gameData.GetRdt(RdtId.Parse(rtdId));
                    if (rdt207 != null)
                    {
                        rdt207.Nop(0x1C576);
                        rdt207.AdditionalOpcodes.Add(new DoorAotSeOpcode()
                        {
                            Opcode = 0x0C,
                            Id = 4,
                            X = 800,
                            Z = 10400,
                            W = 1900,
                            D = 1700,
                            Special = 0,
                            Re1UnkB = 0,
                            Animation = 0,
                            Re1UnkC = 2,
                            LockId = 21 | 0x80,
                            Target = new RdtId(255, 0x06),
                            NextX = 9180,
                            NextY = 0,
                            NextZ = 11280,
                            NextD = 2048,
                            LockType = 255,
                            Free = 129
                        });
                    }
                }
            }

            void FixDoor104()
            {
                var rdt104 = gameData.GetRdt(RdtId.Parse("104"));
                if (rdt104 != null)
                {
                    var door = rdt104.Doors.FirstOrDefault(x => x.Id == 2);
                    door.NextX = 12700;
                    door.NextY = -7200;
                    door.NextZ = 3300;
                }
            }

            void FixDoorToWardrobe()
            {
                var rdt112 = gameData.GetRdt(RdtId.Parse("112"));
                var rdt612 = gameData.GetRdt(RdtId.Parse("612"));
                rdt112?.Nop(0x17864, 0x17866);
                rdt112?.Nop(0x17884, 0x17886);
                rdt612?.Nop(0x17864, 0x17866);
                rdt612?.Nop(0x17884, 0x17886);
            }

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
                        rdt.AdditionalOpcodes.Add(new UnknownOpcode(0, 0x01, [0x0A]));
                        rdt.AdditionalOpcodes.Add(new UnknownOpcode(0, 0x04, [0x01, 0x25, 0x00]));
                        rdt.AdditionalOpcodes.Add(new UnknownOpcode(0, 0x05, [0x02, PassCodeDoorLockId - 192, 0]));
                        rdt.AdditionalOpcodes.Add(new UnknownOpcode(0, 0x03, [0x00]));
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

            void FixDrugStoreRoom()
            {
                if (player != 0)
                    return;

                var rdt = gameData.GetRdt(RdtId.Parse("409"));
                rdt?.Nop(0x166D4, 0x16742);
                rdt?.Nop(0x168AA, 0x16918);
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

                // Prevent placing shotgun
                var rdt116 = gameData.GetRdt(RdtId.Parse("116"));
                var rdt516 = gameData.GetRdt(RdtId.Parse("516"));
                for (var i = 2; i < 2 + 8; i++)
                {
                    rdt116?.Patches.Add(new KeyValuePair<int, byte>(0x1FE62 + i, 0));
                    rdt516?.Patches.Add(new KeyValuePair<int, byte>(0x1FE62 + i, 0));
                }

                // Lock both doors in sandwich room (since we can't put item back on wall)
                foreach (var rdtId in new[] { "115", "515" })
                {
                    var rdt115 = gameData.GetRdt(RdtId.Parse(rdtId));
                    if (rdt115 == null)
                        continue;

                    if (player == 0)
                    {
                        rdt115.Patches.Add(new KeyValuePair<int, byte>(0x22BC + 2 + 128, 1));
                        rdt115.Patches.Add(new KeyValuePair<int, byte>(0x22DE + 2 + 128, 1));
                    }
                    else
                    {
                        rdt115.Nop(0x2342);
                    }

                    // Fix locks (due to increased lock limit)
                    foreach (var opcode in rdt115.Opcodes)
                    {
                        if (opcode is UnknownOpcode unk && unk.Opcode == 5)
                        {
                            unk.Data[1] += 128;
                        }
                    }
                }

                // Unlock doors when in hall or living room
                var rdt109 = gameData.GetRdt(RdtId.Parse("109"));
                var rdt609 = gameData.GetRdt(RdtId.Parse("609"));
                rdt109?.AdditionalOpcodes.Add(new UnknownOpcode(0, 5, [2, 128, 0]));
                rdt609?.AdditionalOpcodes.Add(new UnknownOpcode(0, 5, [2, 128, 0]));
                rdt116?.AdditionalOpcodes.Add(new UnknownOpcode(0, 5, [2, 129, 0]));
                rdt516?.AdditionalOpcodes.Add(new UnknownOpcode(0, 5, [2, 129, 0]));
            }

            void DisablePoisonChallenge()
            {
                var rdt = gameData.GetRdt(RdtId.Parse("20E"));
                if (player == 0)
                {
                    rdt?.Nop(0x10724);
                    rdt?.Nop(0x1073A, 0x10740);
                    rdt?.Nop(0x1075A, 0x107FC);
                    rdt?.Nop(0x107F2, 0x107FC);
                }
                else
                {
                    rdt?.Nop(0x10724, 0x1072A);
                    rdt?.Nop(0x10744, 0x10780);
                    rdt?.Nop(0x1078A, 0x10794);
                }
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

            void EnableFountainHeliportDoors()
            {
                var rdtFountain = gameData.GetRdt(RdtId.Parse("305"));
                if (rdtFountain != null)
                {
                    var door = (DoorAotSeOpcode)rdtFountain.Doors.First(x => x.Id == 0);
                    door.LockId = 2;
                    door.LockType = 255;
                    door.Special = 11;
                    door.Animation = 11;
                    door.NextX = 29130;
                    door.NextY = 0;
                    door.NextZ = 5700;
                    door.NextD = 2048;

                    // Remove message aot_reset
                    rdtFountain.Nop(0x3E9AE);
                }

                var rdtHeliport = gameData.GetRdt(RdtId.Parse("303"));
                if (rdtHeliport != null)
                {
                    var door = (DoorAotSeOpcode)rdtHeliport.ConvertToDoor(8, 11, null, null);
                    door.Target = RdtId.Parse("305");
                    door.LockId = 2;
                    door.LockType = 255;
                    door.Special = 11;
                    door.Animation = 11;
                    door.NextX = 3130;
                    door.NextY = 0;
                    door.NextZ = 16900;
                    door.NextD = 0;

                    rdtHeliport.Nop(0x111BE);
                    rdtHeliport.Nop(0x111C0);

                    // Set cut to 4 if last room is ?05
                    rdtHeliport.AdditionalOpcodes.AddRange([
                        new UnknownOpcode(0, 0x01, [ 0x0C ]),
                        new UnknownOpcode(0, 0x06, [ 0x03, 0x00, 0x05 ]),
                        new UnknownOpcode(0, 0x23, [ 0x01 ]),
                        new UnknownOpcode(0, 0x08, [ 0x02, 0x04, 0x00 ]),
                        new UnknownOpcode(0, 0x03, [ 0x00 ])
                    ]);
                }
            }

            void ForceHelipadTyrant()
            {
                if (context.Configuration.GetValueOrDefault("progression/heliport/tyrant", false))
                {
                    var room = gameData.GetRdt(RdtId.Parse("303"));
                    room?.AdditionalOpcodes.Add(new UnknownOpcode(0, 5, [0, 43, 0]));
                }
            }
        }

        private void DisableCutscenes(IClassicRandomizerGeneratedVariation context, GameData gameData, int player)
        {
            var rdt = gameData.GetRdt(RdtId.Parse("106"));
            if (rdt == null)
                return;

            var enableLockpick = context.Configuration.GetValueOrDefault("inventory/special/lockpick", "Always") == "Always";
            var enableInk = context.Configuration.GetValueOrDefault("ink/enable", "Always") == "Always";
            if (player == 0)
            {
                rdt.AdditionalOpcodes.AddRange(
                    ScdCondition.Parse("1:0").Generate(BioVersion.Biohazard1, [
                        Set(1, 0, 0), // First cutscene
                        Set(1, 2, 0), // First zombie found
                        Set(1, 3, 0), // Second cutscene (Jill? Wesker?)
                        Set(1, 36, 0), // First Rebecca save room cutscene
                        Set(1, 69, 0), // Brad call cutscene in final lab room
                        Set(1, 72, 0), // Enrico cutscene
                        Set(1, 100, 0), // Prevent Plant 42 Rebecca switch
                        Set(1, 167, 0), // Init. dining room emblem state
                        Set(1, 171, 0), // Wesker cutscene after Plant 42
                        Set(0, 101, 0), // Jill in cell cutscene
                        Set(0, 123, (byte)(enableInk ? 0 : 1)), // Ink
                        Set(0, 124, (byte)(enableLockpick ? 0 : 1)), // Lockpick
                        Set(0, 127, 0), // Pick up radio
                        Set(0, 192, 0) // Rebecca not saved
                    ]));

                // Disable hunter / rebecca scream
                gameData.GetRdt(RdtId.Parse("60A"))?.Nop(0xF702);

                // Disable rebecca in trouble
                gameData.GetRdt(RdtId.Parse("601"))?.Nop(0x24CAA, 0x24E16);
                gameData.GetRdt(RdtId.Parse("601"))?.Nop(0x24E1E, 0x24E8A);
                gameData.GetRdt(RdtId.Parse("706"))?.Nop(0x3785C, 0x3796A);
                gameData.GetRdt(RdtId.Parse("706"))?.Nop(0x37972, 0x379F6);

                // Disable Jill in cell
                gameData.GetRdt(RdtId.Parse("512"))?.Nop(0xAF4C, 0xAF8E);

                // Disable Wesker cutscene
                gameData.GetRdt(RdtId.Parse("514"))?.Nop(0xEFCE, 0xF048);

                // Disable Wesker / Tyrant cutscene
                var rdt513 = gameData.GetRdt(RdtId.Parse("513"));
                if (rdt513 != null)
                {
                    rdt513.Patches.Add(new KeyValuePair<int, byte>(0x1C4AA + 1, 0));
                    rdt513.Patches.Add(new KeyValuePair<int, byte>(0x1C4AA + 2, 55));
                    rdt513.Patches.Add(new KeyValuePair<int, byte>(0x1C6E8 + 1, 0));
                    rdt513.Patches.Add(new KeyValuePair<int, byte>(0x1C6E8 + 2, 55));
                    rdt513.Nop(0x1C484);
                    rdt513.Nop(0x1C6B0);
                    rdt513.AdditionalOpcodes.Add(new SceEmSetOpcode()
                    {
                        Opcode = 0x1B,
                        Type = 0x0C,
                        State = 0,
                        KillId = 112,
                        Re1Unk04 = 1,
                        Re1Unk05 = 2,
                        Re1Unk06 = 0,
                        Re1Unk07 = 0,
                        D = 3072,
                        Re1Unk0A = 0,
                        Re1Unk0B = 0,
                        X = 10700,
                        Y = 0,
                        Z = 7000,
                        Id = 1,
                        Re1Unk13 = 0,
                        Re1Unk14 = 0,
                        Re1Unk15 = 0
                    });
                    rdt513.AdditionalFrameOpcodes.AddRange(
                        ScdCondition.Parse("0:55 && 4:11").Generate(BioVersion.Biohazard1, [
                            Set(4, 11, 0),
                            new UnknownOpcode(0, 0x16, [0x00]),
                            new UnknownOpcode(0, 0x16, [0x01]),
                            new UnknownOpcode(0, 0x15, [0x02])]));
                }
            }
            else
            {
                rdt.AdditionalOpcodes.AddRange(
                    ScdCondition.Parse("1:0").Generate(BioVersion.Biohazard1, [
                        Set(0, 101, 0), // Chris in cell cutscene
                        Set(0, 123, (byte)(enableInk ? 0 : 1)), // Ink
                        Set(0, 124, (byte)(enableLockpick ? 0 : 1)), // Lockpick
                        Set(0, 127, 0), // Pick up radio
                        Set(1, 0, 0), // 106 first cutscene
                        Set(1, 2, 0), // 104 first zombie found
                        Set(1, 3, 0), // 106 Wesker search cutscene
                        Set(1, 5, 0), // 106/203 Wesker search complete
                        Set(1, 7, 0), // 106/203 Barry gift cutscene (also disables 115 sandwich rescue and 20A cutscene)
                        Set(1, 69, 0), // Brad call cutscene in final lab room
                        Set(1, 72, 0), // Enrico cutscene
                        Set(1, 86, 0), // 20E Yawn poison partner recovery
                        Set(1, 97, 0), // 20D Richard receives serum
                        Set(1, 103, 0), // 212 Forrest cutscene
                        Set(1, 161, 0), // 105 first dining room cutscene
                        Set(1, 170, 0), // Init. dining room emblem state
                        Set(1, 172, 0), // 104 visted
                        Set(1, 173, 0), // 105 zombie cutscene
                        Set(1, 175, 0), // Wesker cutscene after Plant 42
                        Set(1, 192, 0) // Barry not saved
                    ]));

                // Disable Plant 42 Barry
                gameData.GetRdt(RdtId.Parse("40C"))?.Nop(0x64C8);
                gameData.GetRdt(RdtId.Parse("40C"))?.Nop(0x64D4);

                // Disable Barry in Yawn 2 room
                foreach (var rdtId in new[] { "20C", "70C" })
                {
                    var rdt20C = gameData.GetRdt(RdtId.Parse(rdtId));
                    if (rdt20C != null)
                    {
                        rdt20C.Patches.Add(new KeyValuePair<int, byte>(0x96EA + 14, 0x2C));
                        rdt20C.Nop(0x9704);
                        rdt20C.Nop(0x970A);
                    }
                }

                // Disable Wesker cutscene
                gameData.GetRdt(RdtId.Parse("514"))?.Nop(0xEFCE, 0xF0C6);

                // Disable Chris in cell
                gameData.GetRdt(RdtId.Parse("512"))?.Nop(0xAF4C, 0xAF74);

                // Disable Wesker / Tyrant cutscene
                var rdt513 = gameData.GetRdt(RdtId.Parse("513"));
                if (rdt513 != null)
                {
                    rdt513.Nop(0x1C484);
                    rdt513.Patches.Add(new KeyValuePair<int, byte>(0x1C4AA + 1, 0));
                    rdt513.Patches.Add(new KeyValuePair<int, byte>(0x1C4AA + 2, 55));
                    rdt513.Nop(0x1C724);
                    rdt513.Patches.Add(new KeyValuePair<int, byte>(0x1C7C8 + 1, 0));
                    rdt513.Patches.Add(new KeyValuePair<int, byte>(0x1C7C8 + 2, 55));
                    rdt513.AdditionalOpcodes.Add(new SceEmSetOpcode()
                    {
                        Opcode = 0x1B,
                        Type = 0x0C,
                        State = 0,
                        KillId = 112,
                        Re1Unk04 = 1,
                        Re1Unk05 = 2,
                        Re1Unk06 = 0,
                        Re1Unk07 = 0,
                        D = 3072,
                        Re1Unk0A = 0,
                        Re1Unk0B = 0,
                        X = 10700,
                        Y = 0,
                        Z = 7000,
                        Id = 1,
                        Re1Unk13 = 0,
                        Re1Unk14 = 0,
                        Re1Unk15 = 0
                    });
                    rdt513.AdditionalFrameOpcodes.AddRange(
                        ScdCondition.Parse("0:55 && 4:11").Generate(BioVersion.Biohazard1, [
                            Set(4, 11, 0),
                            new UnknownOpcode(0, 0x16, [0x00]),
                            new UnknownOpcode(0, 0x16, [0x01]),
                            new UnknownOpcode(0, 0x15, [0x02])]));
                }
            }

            static UnknownOpcode Set(byte group, byte index, byte value)
            {
                return new UnknownOpcode(0, 0x05, [group, index, value]);
            }
        }

        public void ApplyConfigModifications(IClassicRandomizerContext context)
        {
            var config = context.Configuration;
            var ink = UpdateConfigNeverAlways(context.Rng, config, "ink/enable", "Always", "Never");
            if (ink != "Always")
            {
                config["inventory/ink/min"] = 0;
                config["inventory/ink/max"] = 0;
                config["items/distribution/ink"] = 0;
            }

            UpdateConfigNeverAlways(context.Rng, config, "inventory/special/lockpick", "Never", "Always");
        }

        private static string UpdateConfigNeverAlways(
            Rng rng,
            RandomizerConfiguration config,
            string key,
            string defaultValueChris,
            string defaultValueJill)
        {
            var value = config.GetValueOrDefault(key, "Default")!;
            if (value == "Default")
            {
                value = config.GetValueOrDefault("variation", "Chris") == "Chris"
                    ? defaultValueChris
                    : defaultValueJill;
            }
            else if (value == "Random")
            {
                value = rng.NextOf("Never", "Always");
            }
            config[key] = value;
            return value;
        }

        public void Write(IClassicRandomizerGeneratedVariation context, ClassicRebirthModBuilder crModBuilder)
        {
            WriteRdts(context, crModBuilder);
            AddMiscXml(context, crModBuilder);
            AddSoundXml(context, crModBuilder);
            AddInventoryXml(context, crModBuilder);
            AddProtagonistSkin(context, crModBuilder);
            AddEnemySkins(context, crModBuilder);
            AddBackgroundTextures(context, crModBuilder);
            AddMusic(context, crModBuilder);
        }

        private void WriteRdts(IClassicRandomizerGeneratedVariation context, ClassicRebirthModBuilder crModBuilder)
        {
            var debugScripts = Environment.GetEnvironmentVariable("BIORAND_DEBUG_SCRIPTS") == "true";

            var p = context.Variation.PlayerIndex;
            var gameData = GetGameData(context.GameDataManager, p);
            if (debugScripts)
            {
                DecompileGameData(p, "scripts/");
            }
            ApplyRdtPatches(context, gameData, p);
            if (context.Configuration.GetValueOrDefault("cutscenes/disable", false))
            {
                DisableCutscenes(context, gameData, p);
            }
            ApplyPostPatches(context, gameData);
            foreach (var rrdt in gameData.Rdts)
            {
                context.ModBuilder.ApplyToRdt(rrdt);
            }
            if (context.Configuration.GetValueOrDefault("enemies/random", false))
            {
                ApplyEnemies(context, gameData);
            }
            foreach (var rrdt in gameData.Rdts)
            {
                rrdt.Save();
                crModBuilder.SetFile(rrdt.OriginalPath!, rrdt.RdtFile.Data);
            }
            if (debugScripts)
            {
                DecompileGameData(p, "scripts_modded/");
            }

            void DecompileGameData(int player, string prefix)
            {
                Parallel.ForEach(gameData.Rdts, rrdt =>
                {
                    rrdt.Decompile();
                    crModBuilder.SetFile($"{prefix}{rrdt.RdtId}{player}.bio", rrdt.Script ?? "");
                    crModBuilder.SetFile($"{prefix}{rrdt.RdtId}{player}.lst", rrdt.ScriptListing ?? "");
                    crModBuilder.SetFile($"{prefix}{rrdt.RdtId}{player}.s", rrdt.ScriptDisassembly ?? "");
                });
            }
        }

        private void ApplyPostPatches(IClassicRandomizerGeneratedVariation generatedVariation, GameData gameData)
        {
            // For each changed item, patch any additional bytes
            var map = generatedVariation.Variation.Map;
            foreach (var kvp in map.Rooms)
            {
                var rdts = kvp.Value.Rdts
                    .Select(gameData.GetRdt)
                    .Where(x => x != null)
                    .Select(x => x!)
                    .ToArray();
                foreach (var item in (kvp.Value.Items ?? []))
                {
                    if (item.GlobalId is not short globalId)
                        continue;

                    if (generatedVariation.ModBuilder.GetItem(globalId) is Item newItem && item.TypeOffsets != null)
                    {
                        foreach (var o in item.TypeOffsets)
                        {
                            var typeOffset = Map.ParseLiteral(o);
                            foreach (var rdt in rdts)
                            {
                                rdt.Patches.Add(new KeyValuePair<int, byte>(typeOffset, newItem.Type));
                            }
                        }
                    }
                }
            }
        }

        private void ApplyEnemies(IClassicRandomizerGeneratedVariation context, GameData gameData)
        {
            // Clear all enemies
            var map = context.Variation.Map;
            foreach (var rdt in gameData.Rdts)
            {
                var room = map.Rooms.Values.FirstOrDefault(x => x.Rdts.Contains(rdt.RdtId));
                if (room == null)
                    continue;

                if (room.Enemies == null || room.Enemies.Length == 0)
                    continue;

                var reservedIds = room.Enemies
                    .Where(x => x.Id != null)
                    .Select(x => x.Id!)
                    .ToArray();

                var offsets = rdt.Enemies
                    .Where(x => CanRemoveEnemy(x.Type))
                    .Where(x => !reservedIds.Contains(x.Id))
                    .Select(x => x.Offset)
                    .ToArray();
                foreach (var o in offsets)
                {
                    rdt.Nop(o);
                }
            }

            var allEffects = HarvestAllEffs(gameData);
            var groups = context.ModBuilder.EnemyPlacements.GroupBy(x => x.RdtId);
            foreach (var g in groups)
            {
                var rdt = gameData.GetRdt(g.Key);
                if (rdt == null)
                    continue;

                var requiredEsp = new HashSet<byte>();
                var opcodes = new List<OpcodeBase>();
                string? condition = null;
                foreach (var ep in g)
                {
                    if (ep.Create)
                    {
                        var opcode = CreateEnemyOpcode(ep);
                        opcodes.Add(opcode);
                        condition ??= ep.Condition;
                    }
                    else
                    {
                        foreach (var e in rdt.Enemies.Where(x => x.Id == ep.Id))
                        {
                            var oldType = e.Type;
                            var newType = (byte)ep.Type;

                            e.Type = newType;
                            if (newType == Re1EnemyIds.Snake)
                            {
                                e.State = 0;
                            }
                        }
                    }
                    foreach (var esp in ep.Esp)
                        requiredEsp.Add((byte)esp);
                }
                InsertConditions(rdt, opcodes, condition);
                AddRequiredEsps(rdt, requiredEsp, allEffects);
            }

            bool CanRemoveEnemy(byte type)
            {
                switch (type)
                {
                    case Re1EnemyIds.Zombie:
                    case Re1EnemyIds.ZombieNaked:
                    case Re1EnemyIds.Cerberus:
                    case Re1EnemyIds.WebSpinner:
                    case Re1EnemyIds.BlackTiger:
                    case Re1EnemyIds.Crow:
                    case Re1EnemyIds.Hunter:
                    case Re1EnemyIds.Wasp:
                    case Re1EnemyIds.Chimera:
                    case Re1EnemyIds.Snake:
                    case Re1EnemyIds.Neptune:
                    case Re1EnemyIds.Tyrant1:
                    case Re1EnemyIds.Plant42Vines:
                    case Re1EnemyIds.ZombieResearcher:
                        return true;
                    default:
                        return false;
                }
            }

            SceEmSetOpcode CreateEnemyOpcode(EnemyPlacement ep)
            {
                return new SceEmSetOpcode()
                {
                    Length = 22,
                    Opcode = (byte)OpcodeV1.SceEmSet,
                    Type = (byte)ep.Type,
                    State = 0,
                    KillId = (byte)ep.GlobalId,
                    Re1Unk04 = 1,
                    Re1Unk05 = 2,
                    Re1Unk06 = 0,
                    Re1Unk07 = 0,
                    D = (short)ep.D,
                    Re1Unk0A = 0,
                    Re1Unk0B = 0,
                    X = (short)ep.X,
                    Y = (short)ep.Y,
                    Z = (short)ep.Z,
                    Id = (byte)ep.Id,
                    Re1Unk13 = 0,
                    Re1Unk14 = 0,
                    Re1Unk15 = 0,
                };
            }

            void InsertConditions(RandomizedRdt rdt, List<OpcodeBase> enemyOpcodes, string? condition)
            {
                if (string.IsNullOrEmpty(condition))
                {
                    rdt.AdditionalOpcodes.AddRange(enemyOpcodes);
                    return;
                }

                var scdCondition = ScdCondition.Parse(condition!);
                var opcodes = scdCondition.Generate(BioVersion.Biohazard1, enemyOpcodes);
                rdt.AdditionalOpcodes.AddRange(opcodes);
            }

            static Dictionary<byte, EmbeddedEffect> HarvestAllEffs(GameData gameData)
            {
                var result = new Dictionary<byte, EmbeddedEffect>();
                foreach (var rdt in gameData.Rdts)
                {
                    var embeddedEffects = ((Rdt1)rdt.RdtFile).EmbeddedEffects;
                    for (var i = 0; i < embeddedEffects.Count; i++)
                    {
                        var ee = embeddedEffects[i];
                        if (ee.Id != 0xFF && !result.ContainsKey(ee.Id))
                        {
                            result[ee.Id] = ee;
                        }
                    }
                }
                return result;
            }

            static void AddRequiredEsps(RandomizedRdt rdt, HashSet<byte> espIds, Dictionary<byte, EmbeddedEffect> allEffects)
            {
                if (espIds.Count == 0)
                    return;

                var rdtFile = (Rdt1)rdt.RdtFile;
                var embeddedEffects = rdtFile.EmbeddedEffects;
                var missingIds = espIds.Except(embeddedEffects.Ids).ToArray();
                if (missingIds.Length == 0)
                    return;

                var existingEffects = embeddedEffects.Effects.ToList();
                foreach (var id in missingIds)
                {
                    existingEffects.Add(allEffects[id]);
                }

                var rdtBuilder = rdtFile.ToBuilder();
                rdtBuilder.EmbeddedEffects = new EmbeddedEffectList(rdtFile.Version, existingEffects.ToArray());
                rdt.RdtFile = rdtBuilder.ToRdt();
            }
        }

        private void AddInventoryXml(IClassicRandomizerGeneratedVariation context, ClassicRebirthModBuilder crModBuilder)
        {
            var inventories = context.ModBuilder.Inventory;

            using var ms = new MemoryStream();
            var doc = new XmlDocument();
            var root = doc.CreateElement("Init");

            var chris = CreateEmptyInventory(6);
            var jill = CreateEmptyInventory(8);
            var rebecca = CreateEmptyInventory(6);
            if (context.Variation.PlayerIndex == 0)
                chris = inventories[0].WithSize(6);
            else
                jill = inventories[0].WithSize(8);

            root.AppendChild(CreatePlayerNode(doc, jill, new RandomInventory()));
            root.AppendChild(CreatePlayerNode(doc, chris, rebecca));

            doc.AppendChild(root);
            doc.Save(ms);
            crModBuilder.SetFile("init.xml", ms.ToArray());

            static RandomInventory CreateEmptyInventory(int size)
            {
                var entries = new List<RandomInventory.Entry>();
                for (var i = 0; i < size; i++)
                {
                    entries.Add(new RandomInventory.Entry());
                }
                entries[0] = new RandomInventory.Entry(Re1ItemIds.CombatKnife, 1);
                return new RandomInventory([.. entries], null);
            }

            static XmlElement CreatePlayerNode(XmlDocument doc, RandomInventory main, RandomInventory partner)
            {
                var playerNode = doc.CreateElement("Player");
                foreach (var inv in new[] { main, partner })
                {
                    foreach (var entry in inv.Entries)
                    {
                        var entryNode = doc.CreateElement("Entry");
                        entryNode.SetAttribute("id", entry.Type.ToString());
                        entryNode.SetAttribute("count", entry.Count.ToString());
                        playerNode.AppendChild(entryNode);
                    }
                }
                return playerNode;
            }
        }

        private void AddProtagonistSkin(IClassicRandomizerGeneratedVariation context, ClassicRebirthModBuilder crModBuilder)
        {
            var characterPath = context.ModBuilder.Protagonist;
            if (characterPath == null)
                return;

            var characterName = Path.GetFileName(characterPath);
            var srcPlayer = 0;
            var emdData = GetData($"{characterPath}/char10.emd");
            if (emdData == null)
            {
                srcPlayer = 1;
                emdData = GetData($"{characterPath}/char11.emd");
            }

            var playerIndex = context.Variation.PlayerIndex;
            crModBuilder.SetFile($"ENEMY/CHAR1{playerIndex}.EMD", emdData);
            for (var i = 0; i < 12; i++)
            {
                var emwData = GetData($"{characterPath}/W{srcPlayer}{i}.EMW");
                if (emwData != null)
                {
                    crModBuilder.SetFile($"PLAYERS/W{playerIndex}{i}.EMW", emwData);
                }
            }

            var hurtFiles = GetHurtFiles(characterName);
            var hurtFileNames = new string[][]
            {
                ["chris", "ch_ef"],
                ["jill", "jill_ef"],
                [],
                ["reb"]
            };

            var soundDir = "sound";
            for (int i = 0; i < hurtFiles.Length; i++)
            {
                var waveformBuilder = new WaveformBuilder();
                waveformBuilder.Append(hurtFiles[i]);
                var arr = hurtFileNames[playerIndex];
                foreach (var hurtFileName in arr)
                {
                    var soundPath = $"{soundDir}/{hurtFileName}{i + 1:00}.wav";
                    crModBuilder.SetFile(soundPath, waveformBuilder.ToArray());
                }
            }
            if (playerIndex <= 1)
            {
                var nom = playerIndex == 0 ? "ch_nom.wav" : "ji_nom.wav";
                var sime = playerIndex == 0 ? "ch_sime.wav" : "ji_sime.wav";
                crModBuilder.SetFile($"{soundDir}/{nom}", new WaveformBuilder()
                    .Append(hurtFiles[3])
                    .ToArray());
                crModBuilder.SetFile($"{soundDir}/{sime}", new WaveformBuilder()
                    .Append(hurtFiles[2])
                    .ToArray());
            }

            string[] GetHurtFiles(string character)
            {
                var allHurtFiles = context.DataManager.GetHurtFiles(character)
                    .Where(x => x.EndsWith(".ogg", StringComparison.OrdinalIgnoreCase) || x.EndsWith(".wav", StringComparison.OrdinalIgnoreCase))
                    .ToArray();
                var hurtFiles = new string[4];
                foreach (var hurtFile in allHurtFiles)
                {
                    if (int.TryParse(Path.GetFileNameWithoutExtension(hurtFile), out var i))
                    {
                        if (i < hurtFiles.Length)
                        {
                            hurtFiles[i] = hurtFile;
                        }
                    }
                }
                return hurtFiles;
            }

            byte[]? GetData(string path)
            {
                if (!File.Exists(path))
                    return null;
                return File.ReadAllBytes(path);
            }
        }

        private void AddEnemySkins(IClassicRandomizerGeneratedVariation context, ClassicRebirthModBuilder crModBuilder)
        {
            var skinPaths = context.ModBuilder.EnemySkins;
            foreach (var skinPath in skinPaths)
            {
                var files = Directory.GetFiles(skinPath);
                foreach (var f in files)
                {
                    var fileName = Path.GetFileName(f);
                    var destination = GetDestination(fileName);
                    if (destination == null)
                        continue;

                    var fileData = File.ReadAllBytes(f);
                    if (fileData.Length == 0)
                        continue;

                    crModBuilder.SetFile(destination, fileData);
                }
            }

            string? GetDestination(string fileName)
            {
                string[] voiceFileNamesForSoundFolder = [
                    "V_JOLT.WAV",
                    "v00d_02.wav",
                    "V00D_02S.WAV",
                    "V110_00.WAV",
                    "VB00_31.WAV",
                    "VB00_31A.WAV",
                    "VB00_31B.WAV",
                    "VB00_31C.WAV"
                ];

                if (fileName.EndsWith(".EMD", StringComparison.OrdinalIgnoreCase))
                {
                    var match = Regex.Match(fileName, "EM10([0-9A-F][0-9A-F]).EMD", RegexOptions.IgnoreCase);
                    if (!match.Success)
                        return null;

                    var id = byte.Parse(match.Groups[1].Value, NumberStyles.HexNumber);
                    return $"ENEMY/EM1{context.Variation.PlayerIndex}{id:X2}.EMD";
                }
                if (!fileName.EndsWith(".WAV", StringComparison.OrdinalIgnoreCase))
                    return null;
                if (voiceFileNamesForSoundFolder.Contains(fileName, StringComparer.OrdinalIgnoreCase))
                    return $"SOUND/{fileName}";
                if (fileName.StartsWith("VN_", StringComparison.OrdinalIgnoreCase))
                    return $"SOUND/{fileName}";
                if (fileName.StartsWith("V", StringComparison.OrdinalIgnoreCase))
                    return $"VOICE/{fileName}";
                return $"SOUND/{fileName}";
            }
        }

        private void AddBackgroundTextures(IClassicRandomizerGeneratedVariation context, ClassicRebirthModBuilder crModBuilder)
        {
            var bgPng = context.DataManager.GetData(BioVersion.Biohazard1, "bg.png");
            var bgPix = PngToPix(bgPng);
            crModBuilder.SetFile("data/title.pix", bgPix);
            crModBuilder.SetFile("type.png", bgPng);

            static byte[] PngToPix(byte[] png)
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
        }

        private void AddMusic(IClassicRandomizerGeneratedVariation context, ClassicRebirthModBuilder crModBuilder)
        {
            var bgmTable = context.DataManager.GetData(BioVersion.Biohazard1, "bgm_tbl.xml");
            crModBuilder.SetFile("bgm_tbl.xml", bgmTable);

            var encoder = new BgmBatchEncoder();
            encoder.Process(context.ModBuilder, crModBuilder);
        }

        private void AddMiscXml(IClassicRandomizerGeneratedVariation context, ClassicRebirthModBuilder crModBuilder)
        {
            var miscTable = context.DataManager.GetData(BioVersion.Biohazard1, "misc.xml");
            crModBuilder.SetFile("misc.xml", miscTable);
        }

        private void AddSoundXml(IClassicRandomizerGeneratedVariation context, ClassicRebirthModBuilder crModBuilder)
        {
            var xml = context.DataManager.GetText(BioVersion.Biohazard1, "sounds.xml");
            var doc = new XmlDocument();
            doc.LoadXml(xml);

            var enemyPlacements = context.ModBuilder.EnemyPlacements;
            var roomNodes = doc.SelectNodes("Rooms/Room");
            foreach (XmlNode roomNode in roomNodes)
            {
                var idAttribute = roomNode.Attributes["id"];
                if (idAttribute == null)
                    continue;

                if (!RdtId.TryParse(idAttribute.Value, out var roomId))
                    continue;

                var firstEnemy = enemyPlacements.FirstOrDefault(x => x.RdtId == roomId);
                var firstEnemyType = (byte?)firstEnemy?.Type;
                FixRoomSounds(roomId, firstEnemyType, roomNode);
            }

            void FixRoomSounds(RdtId rdtId, byte? enemyType, XmlNode roomNode)
            {
                if (enemyType != null)
                {
                    var template = GetTemplateXml(enemyType.Value);
                    var entryNodes = roomNode.SelectNodes("Sound/Entry");
                    for (int i = 0; i < 16; i++)
                    {
                        entryNodes[i].InnerText = template[i] ?? "";
                    }
                }
                crModBuilder.SetFile($"tables/room_{rdtId}.xml", roomNode.InnerXml);
            }

            static string[] GetTemplateXml(byte enemyType)
            {
                string[]? result = null;
                switch (enemyType)
                {
                    case Re1EnemyIds.Zombie:
                        result = ["z_taore", "z_ftL", "z_ftR", "z_kamu", "z_k02", "z_k01", "z_head", "z_haki", "z_sanj", "z_k03"];
                        break;
                    case Re1EnemyIds.ZombieNaked:
                        result = ["z_taore", "zep_ftL", "z_ftR", "ze_kamu", "z_nisi2", "z_nisi1", "ze_head", "ze_haki", "ze_sanj", "z_nisi3", "FL_walk", "FL_jump", "steam_b", "FL_ceil", "FL_fall", "FL_slash"];
                        break;
                    case Re1EnemyIds.Cerberus:
                        result = ["cer_foot", "cer_taoA", "cer_unar", "cer_bite", "cer_cryA", "cer_taoB", "cer_jkMX", "cer_kamu", "cer_cryB", "cer_runMX"];
                        break;
                    case Re1EnemyIds.WebSpinner:
                        result = ["kuasi_A", "kuasi_B", "kuasi_C", "sp_rakk", "sp_atck", "sp_bomb", "sp_fumu", "sp_Doku", "sp_sanj2"];
                        break;
                    case Re1EnemyIds.BlackTiger:
                        result = ["kuasi_A", "kuasi_B", "kuasi_C", "sp_rakk", "sp_atck", "sp_bomb", "sp_fumu", "sp_Doku", "poison"];
                        break;
                    case Re1EnemyIds.Crow:
                        result = ["RVcar1", "RVpat", "RVcar2", "RVwing1", "RVwing2", "RVfryed"];
                        break;
                    case Re1EnemyIds.Hunter:
                        result = ["HU_walkA", "HU_walkB", "HU_jump", "HU_att", "HU_land", "HU_smash", "HU_dam", "HU_Nout"];
                        break;
                    case Re1EnemyIds.Wasp:
                        result = ["bee4_ed", "hatinage", "bee_fumu"];
                        break;
                    case Re1EnemyIds.Plant42:
                        break;
                    case Re1EnemyIds.Chimera:
                        result = ["FL_walk", "FL_jump", "steam_b", "FL_ceil", "FL_fall", "FL_slash", "FL_att", "FL_dam", "FL_out"];
                        break;
                    case Re1EnemyIds.Snake:
                        result = ["PY_mena", "PY_hit2", "PY_fall"];
                        break;
                    case Re1EnemyIds.Neptune:
                        result = ["nep_attB", "nep_attA", "nep_nomu", "nep_tura", "nep_twis", "nep_jump"];
                        break;
                    case Re1EnemyIds.Tyrant1:
                        result = ["TY_foot", "TY_kaze", "TY_slice", "TY_HIT", "TY_trust", "", "TY_taore", "TY_nage"];
                        break;
                    case Re1EnemyIds.Yawn1:
                        break;
                    case Re1EnemyIds.Plant42Roots:
                        break;
                    case Re1EnemyIds.Plant42Vines:
                        break;
                    case Re1EnemyIds.Tyrant2:
                        break;
                    case Re1EnemyIds.ZombieResearcher:
                        result = ["z_taore", "z_ftL", "z_ftR", "z_kamu", "z_mika02", "z_mika01", "z_head", "z_Hkick", "z_Ugoron", "z_mika03"];
                        break;
                    case Re1EnemyIds.Yawn2:
                        break;
                }
                Array.Resize(ref result, 16);
                return result;
            }
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
