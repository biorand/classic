using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

namespace IntelOrca.Biohazard.BioRand.RE1
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
            var rng = context.GetRng("re1map");
            var config = context.Configuration;
            var randomDoors = context.Configuration.GetValueOrDefault("doors/random", false);

            // Apply player, scenario filter
            var map = context.DataManager.GetJson<Map>(BioVersion.Biohazard1, "rdt.json");
            map = map.For(new MapFilter(randomDoors, (byte)playerIndex, 0));

            // Copy door entrances from CSV
            var doorEntrances = context.DataManager.GetCsv<CsvDoorEntrance>(BioVersion.Biohazard1, "doors.csv");
            foreach (var de in doorEntrances)
            {
                var doorIdentity = RdtItemId.Parse(de.Target);
                foreach (var room in map.GetRoomsContaining(doorIdentity.Rdt))
                {
                    foreach (var door in room.Doors ?? [])
                    {
                        if (door.Id != doorIdentity.Id)
                            continue;

                        door.Entrance = new MapRoomDoorEntrance()
                        {
                            X = de.X,
                            Y = de.Y,
                            Z = de.Z,
                            D = de.D
                        };
                    }
                }
            }

            // Remove mansion 2 RDTs for door rando
            if (randomDoors)
            {
                foreach (var room in map.Rooms.Values)
                {
                    room.Rdts = room.Rdts.RemoveAll(x => x.Stage >= 5);
                }
            }

            // Lockpick, remove all sword key and small key requirements
            var enableLockpick = context.Configuration.GetValueOrDefault("inventory/special/lockpick", "Always") == "Always";
            if (enableLockpick)
            {
                map.Items[51].Implicit = true;
                map.Items[61].Implicit = true;
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

            if (!randomDoors)
            {
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
                    fountainDoor.Kind = DoorKinds.Locked;
                    fountainDoor.AllowedLocks = [];

                    var helipadRoom = map.Rooms["303"];
                    var helipadDoor = helipadRoom.Doors.First(x => x.Name == "DOOR TO FOUNTAIN");
                    helipadDoor.Kind = DoorKinds.Unlock;
                    helipadDoor.IgnoreInGraph = true;
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
            }

            // Locks
            var mansion2keyType = (int)Re1ItemIds.HelmetKey;
            if (randomDoors)
            {
                foreach (var room in map.Rooms.Values)
                {
                    foreach (var door in room.Doors ?? [])
                    {
                        if (door.Kind == DoorKinds.Locked || door.Kind == DoorKinds.Unlock)
                        {
                            door.Kind = null;
                        }
                    }
                }
            }
            else
            {
                var softlockSafeDoors = map.Rooms.Values.SelectMany(x => x.Doors).Where(x => x.HasTag("softlock-safe")).ToArray();
                foreach (var door in softlockSafeDoors)
                {
                    door.AllowedLocks = [];

                    var opposite = map.GetOtherSide(door);
                    if (opposite != null)
                        opposite.AllowedLocks = [];
                }
            }

            if (context.Configuration.GetValueOrDefault("locks/random", false))
            {
                if (!context.Configuration.GetValueOrDefault("locks/preserve", false))
                {
                    // Remove all vanilla locks that can be randomized
                    foreach (var door in map.Rooms.Values.SelectMany(x => x.Doors ?? []))
                    {
                        var opposite = map.GetOtherSide(door);
                        if (door.AllowedLocks == null && opposite?.AllowedLocks == null)
                        {
                            if (door.Kind == DoorKinds.Locked || door.Kind == DoorKinds.Unlock)
                            {
                                door.Kind = null;
                            }
                            if (door.LockId != null)
                            {
                                door.LockId = null;
                            }
                        }
                    }
                }

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
                        var mansion2key = enableLockpick
                            ? genericKeys.FirstOrDefault(x => x.Key != Re1ItemIds.SwordKey)
                            : genericKeys.FirstOrDefault();
                        mansion2keyType = mansion2key.Key;
                        genericKeys = genericKeys.Except([mansion2key]).ToArray();

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
                        var lockIds = Enumerable.Range(1, 254)
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
                        if ((b & 1 << i) != 0 && areaTags.Count > i)
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

            if (randomDoors)
            {
                // All items can go anywhere
                var items = map.Rooms!.Values.SelectMany(x => x.Items).ToArray();
                foreach (var item in items)
                    item.Group = GROUP_ALL;
            }
            else
            {
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
                    caveDoor.Kind = DoorKinds.NoReturn;

                    map.GetOtherSide(caveDoor)!.Kind = DoorKinds.Blocked;
                }

                if (config.GetValueOrDefault("progression/lab", true) &&
                    config.GetValueOrDefault("progression/lab/segmented", false))
                {
                    var labDoor = map.Rooms!["305"].Doors.First(x => x.Name == "FOUNTAIN STAIRS");
                    labDoor.Kind = DoorKinds.NoReturn;

                    map.GetOtherSide(labDoor)!.Kind = DoorKinds.Blocked;
                }
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

        public void ApplyConfigModifications(IClassicRandomizerContext context, ModBuilder modBuilder)
        {
            var rng = context.GetRng("re1config");
            var config = context.Configuration;
            var player = config.GetValueOrDefault("variation", "Chris") == "Chris" ? 0 : 1;
            modBuilder.General = modBuilder.General.SetItem("player", player);

            // inventory/size
            var inventorySizeSetting = config.GetValueOrDefault("inventory/size", "Default");
            var inventorySize = inventorySizeSetting switch
            {
                "Random" => rng.NextOf(6, 8),
                "6" => 6,
                "8" => 8,
                _ => player == 0 ? 6 : 8
            };
            modBuilder.General = modBuilder.General.SetItem("inventorySize", inventorySize);

            var ink = UpdateConfigNeverAlways(rng, config, "ink/enable", "Always", "Never");
            if (ink != "Always")
            {
                config["inventory/ink/min"] = 0;
                config["inventory/ink/max"] = 0;
                config["items/distribution/ink"] = 0;
                modBuilder.General = modBuilder.General.SetItem("ink", false);
            }
            else
            {
                modBuilder.General = modBuilder.General.SetItem("ink", true);
            }

            var lockpick = UpdateConfigNeverAlways(rng, config, "inventory/special/lockpick", "Never", "Always");
            modBuilder.General = modBuilder.General.SetItem("lockpick", lockpick == "Always");

            if (config.TryGetValue("doors/random", out var randomDoors))
                modBuilder.General = modBuilder.General.SetItem("randomDoors", randomDoors);

            if (config.TryGetValue("items/random", out var randomItems))
                modBuilder.General = modBuilder.General.SetItem("randomItems", randomItems);

            if (config.TryGetValue("enemies/random", out var randomEnemies))
                modBuilder.General = modBuilder.General.SetItem("randomEnemies", randomEnemies);

            if (config.TryGetValue("cutscenes/disable", out var cutscenesDisabled))
                modBuilder.General = modBuilder.General.SetItem("cutscenesDisabled", cutscenesDisabled);

            if (config.TryGetValue("progression/heliport/tyrant", out var forceTyrant))
                modBuilder.General = modBuilder.General.SetItem("forceTyrant", forceTyrant);
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

        private class CsvDoorEntrance
        {
            public string Target { get; init; } = "";
            public int X { get; init; }
            public int Y { get; init; }
            public int Z { get; init; }
            public int D { get; init; }
        }
    }
}
