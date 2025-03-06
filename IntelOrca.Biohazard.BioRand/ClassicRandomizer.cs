using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using IntelOrca.Biohazard.BioRand.Routing;
using SixLabors.ImageSharp;

namespace IntelOrca.Biohazard.BioRand
{

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

        private DataManager GetDataManager()
        {
            var biorandDataPath = Environment.GetEnvironmentVariable("BIORAND_DATA");
            if (biorandDataPath == null)
            {
                throw new Exception("$BIORAND_DATA not set");
            }

            var paths = biorandDataPath
                .Split(Path.PathSeparator)
                .Select(x => Path.GetFullPath(x))
                .ToArray();
            var dataManager = new DataManager(paths);
            return dataManager;
        }

        private DataManager GetGameDataManager()
        {
            var gameDataPath = Environment.GetEnvironmentVariable("BIORAND_GAMEDATA_1");
            if (gameDataPath == null)
            {
                throw new Exception("$BIORAND_GAMEDATA_1 not set");
            }

            return new DataManager(gameDataPath);
        }

        private RandomizerConfigurationDefinition CreateConfigDefinition()
        {
            var dataManager = GetDataManager();
            var map = dataManager.GetJson<Map>(BioVersion.Biohazard1, "rdt.json");

            var result = new RandomizerConfigurationDefinition();
            var page = result.CreatePage("General");
            var group = page.CreateGroup("Door Randomizer");
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

            page = result.CreatePage("Inventory");
            group = page.CreateGroup("Main");
            group.Items.Add(new RandomizerConfigurationDefinition.GroupItem()
            {
                Id = "inventory/weapon/knife",
                Label = "Knife",
                Description = "Include the knife in starting inventory.",
                Type = "dropdown",
                Options = ["Random", "Never", "Always"],
                Default = "Always"
            });
            foreach (var s in new string[] { "Primary", "Secondary" })
            {
                group = page.CreateGroup($"{s} Weapon");
                group.Items.Add(new RandomizerConfigurationDefinition.GroupItem()
                {
                    Id = $"inventory/{s.ToLowerInvariant()}/none",
                    Label = "None",
                    Type = "switch",
                    Default = true
                });
                foreach (var kvp in map.Items)
                {
                    var itemDefinition = kvp.Value;
                    var kind = itemDefinition.Kind;
                    if (!kind.StartsWith("weapon/") || kind == "weapon/knife")
                        continue;

                    group.Items.Add(new RandomizerConfigurationDefinition.GroupItem()
                    {
                        Id = $"inventory/{s.ToLowerInvariant()}/{itemDefinition.Kind}",
                        Label = itemDefinition.Name,
                        Type = "switch",
                        Default = true
                    });
                }
            }

            group = page.CreateGroup("Extras");
            group.Warning = "A random selection is chosen until inventory is full.";
            foreach (var s in new string[] { "Primary", "Secondary" })
            {
                group.Items.Add(new RandomizerConfigurationDefinition.GroupItem()
                {
                    Id = $"inventory/{s.ToLowerInvariant()}/ammo/min",
                    Label = $"Min. Ammo for {s} Weapon",
                    Description = $"Minimum ammo for the {s.ToLowerInvariant()} weapon. Percentage of weapon capacity. 100% would be fully loaded, no extra ammo.",
                    Type = "percent",
                    Min = 0,
                    Max = 8,
                    Step = 0.1,
                    Default = 1
                });
                group.Items.Add(new RandomizerConfigurationDefinition.GroupItem()
                {
                    Id = $"inventory/{s.ToLowerInvariant()}/ammo/max",
                    Label = $"Max. Ammo for {s} Weapon",
                    Description = $"Maximum ammo for the {s.ToLowerInvariant()} weapon. Percentage of weapon capacity. 100% would be fully loaded, no extra ammo.",
                    Type = "percent",
                    Min = 0,
                    Max = 8,
                    Step = 0.1,
                    Default = 2
                });
            }
            foreach (var kvp in map.Items)
            {
                var itemDefinition = kvp.Value;
                var kind = itemDefinition.Kind;
                if (!kind.StartsWith("health/"))
                    continue;

                group.Items.Add(new RandomizerConfigurationDefinition.GroupItem()
                {
                    Id = $"inventory/{itemDefinition.Kind}/min",
                    Label = $"Min. {itemDefinition.Name}",
                    Min = 0,
                    Max = 10,
                    Step = 1,
                    Type = "range",
                    Default = 0
                });
                group.Items.Add(new RandomizerConfigurationDefinition.GroupItem()
                {
                    Id = $"inventory/{itemDefinition.Kind}/max",
                    Label = $"Max. {itemDefinition.Name}",
                    Min = 0,
                    Max = 10,
                    Step = 1,
                    Type = "range",
                    Default = 2
                });
            }
            group.Items.Add(new RandomizerConfigurationDefinition.GroupItem()
            {
                Id = "inventory/ink/min",
                Label = "Min. Ink Ribbons",
                Description = "Minimum number of ink ribbons.",
                Type = "range",
                Min = 0,
                Max = 32,
                Step = 1,
                Default = 0
            });
            group.Items.Add(new RandomizerConfigurationDefinition.GroupItem()
            {
                Id = "inventory/ink/max",
                Label = "Max. Ink Ribbons",
                Description = "Maximum number of ink ribbons.",
                Type = "range",
                Min = 0,
                Max = 32,
                Step = 1,
                Default = 3
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

                if (itemDefinition.Max == 0)
                    continue;

                group.Items.Add(new RandomizerConfigurationDefinition.GroupItem()
                {
                    Id = $"items/weapon/initial/min/{itemDefinition.Kind}",
                    Label = $"Min. {itemDefinition.Name}",
                    Min = 0,
                    Max = itemDefinition.Max,
                    Step = 1,
                    Type = "range",
                    Default = 0
                });
                group.Items.Add(new RandomizerConfigurationDefinition.GroupItem()
                {
                    Id = $"items/weapon/initial/max/{itemDefinition.Kind}",
                    Label = $"Max. {itemDefinition.Name}",
                    Min = 0,
                    Max = itemDefinition.Max,
                    Step = 1,
                    Type = "range",
                    Default = itemDefinition.Max,
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

#if false
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
#endif

            page = result.CreatePage("Cutscenes");
            group = page.CreateGroup("");
            group.Items.Add(new RandomizerConfigurationDefinition.GroupItem()
            {
                Id = "cutscenes/disable",
                Label = "Disable All Cutscenes",
                Description = "Disable all cutscenes in the game.",
                Type = "switch",
                Default = false
            });

            // page = result.CreatePage("Music");
            controller.UpdateConfigDefinition(result);
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
            var dataManager = GetDataManager();
            var gameDataManager = GetGameDataManager();
            var rng = new Rng(input.Seed);

            var crModBuilder = new ClassicRebirthModBuilder($"BioRand | {input.ProfileName} | {input.Seed}");
            crModBuilder.Description =
                $"""
                BioRand 4.0 ({BuildVersion})
                Profile: {input.ProfileName} by {input.ProfileAuthor}
                Seed: {input.Seed}

                {input.ProfileDescription}
                """;
            crModBuilder.SetFile("config.json", Encoding.UTF8.GetBytes(input.Configuration.ToJson(true)));
            crModBuilder.Module = new ClassicRebirthModule("biorand.dll", dataManager.GetData("biorand.dll"));

            var context = new Context(input.Configuration, dataManager, gameDataManager, rng, crModBuilder);
            var variations = controller.GetVariations(context);
            foreach (var v in variations)
            {
                var modBuilder = new ModBuilder();
                var playerContext = new PlayerContext(context, v, modBuilder);

                Randomize(playerContext);

                var dump = modBuilder.GetDump(playerContext);
                crModBuilder.SetFile($"log_{v.PlayerName.ToLowerInvariant()}.md", Encoding.UTF8.GetBytes(dump));

                context.Variations = context.Variations.Add(modBuilder);
            }

            controller.Write(context);

            var assets = new List<RandomizerOutputAsset>();
            var debugEnv = Environment.GetEnvironmentVariable("BIORAND_DEBUG_OUTPUT");
            if (!string.IsNullOrEmpty(debugEnv))
            {
                crModBuilder.Dump(debugEnv);
            }
            else
            {
                var modFileName = $"mod_biorand_{input.Seed}.7z";
                var archiveFile = crModBuilder.Create7z();
                var asset = new RandomizerOutputAsset(
                    "mod",
                    "Classic Rebirth Mod",
                    "Drop this in your RE 1 install folder.",
                    modFileName,
                    archiveFile);
                assets.Add(asset);
            }
            return new RandomizerOutput(
                [.. assets],
                "",
                []);
        }

        private void Randomize(IClassicRandomizerPlayerContext context)
        {
            if (context.Configuration.GetValueOrDefault("doors/random", true))
            {
                throw new RandomizerUserException("Door randomizer not implemented yet.");
            }
            var inventoryRandomizer = new InventoryRandomizer();
            inventoryRandomizer.Randomize(context);
            var lockRandomizer = new LockRandomizer();
            lockRandomizer.Randomise(context);
            if (context.Configuration.GetValueOrDefault("items/random", false))
            {
                var keyRandomizer = new KeyRandomizer();
                keyRandomizer.RandomiseItems(context);
                var itemRandomizer = new ItemRandomizer();
                itemRandomizer.Randomize(context);
            }
        }

        private sealed class Context(
            RandomizerConfiguration configuration,
            DataManager dataManager,
            DataManager gameDataManager,
            Rng rng,
            ClassicRebirthModBuilder crModBuilder) : IClassicRandomizerContext
        {
            public RandomizerConfiguration Configuration => configuration;
            public DataManager DataManager => dataManager;
            public DataManager GameDataManager => gameDataManager;
            public Rng Rng => rng;
            public ClassicRebirthModBuilder CrModBuilder => crModBuilder;
            public ImmutableArray<ModBuilder> Variations { get; set; } = [];
        }

        private sealed class PlayerContext(
            Context context,
            Variation variation,
            ModBuilder modBuilder) : IClassicRandomizerPlayerContext
        {
            public RandomizerConfiguration Configuration => context.Configuration;
            public DataManager DataManager => context.DataManager;
            public DataManager GameDataManager => context.GameDataManager;
            public Rng Rng => context.Rng;
            public ClassicRebirthModBuilder CrModBuilder => context.CrModBuilder;

            public Variation Variation => variation;
            public ModBuilder ModBuilder => modBuilder;
        }
    }

    internal class InventoryRandomizer
    {
        public void Randomize(IClassicRandomizerPlayerContext context)
        {
            var config = context.Configuration;
            var rng = context.Rng;

            var inventorySize = context.Variation.PlayerIndex == 0 ? 6 : 8;
            var inventoryState = new InventoryBuilder(inventorySize);

            var knife = GetRandomEnabled("inventory/weapon/knife");
            if (knife)
            {
                var kvp = context.Variation.Map.Items.FirstOrDefault(x => x.Value.Kind == "weapon/knife");
                if (kvp.Key != 0)
                {
                    inventoryState.Add(new Item((byte)kvp.Key, (byte)kvp.Value.Max));
                }
            }

            var primary = GetRandomWeapon(context, "inventory/primary");
            if (primary != null)
            {
                inventoryState.Add(primary);
            }

            var secondary = GetRandomWeapon(context, "inventory/secondary", exclude: primary);
            if (secondary != null)
            {
                inventoryState.Add(secondary);
            }

            var itemPool = new ItemPool();
            itemPool.AddGroup(context, "health/");
            itemPool.AddGroup(context, "ink");
            while (!inventoryState.Full && itemPool.TakeStack(context) is Item stack)
            {
                inventoryState.Add(stack);
            }

            context.ModBuilder.Inventory = [inventoryState.Build()];

            bool GetRandomEnabled(string configKey)
            {
                var configValue = config.GetValueOrDefault(configKey, "Always");
                if (configValue == "Always")
                    return true;
                if (configValue == "Never")
                    return false;
                return rng.NextOf(false, true);
            }
        }

        private WeaponSwag? GetRandomWeapon(IClassicRandomizerPlayerContext context, string prefix, WeaponSwag? exclude = null)
        {
            var config = context.Configuration;
            var rng = context.Rng;
            var pool = new List<WeaponSwag?>();
            if (config.GetValueOrDefault($"{prefix}/none", false))
            {
                pool.Add(null);
            }
            foreach (var kvp in context.Variation.Map.Items)
            {
                var definition = kvp.Value;
                if (!definition.Kind.StartsWith("weapon/"))
                    continue;

                var swag = new WeaponSwag(kvp.Key, kvp.Value);
                if (swag.Group == exclude?.Group)
                    continue;

                var isEnabled = config.GetValueOrDefault($"{prefix}/{definition.Kind}", false);
                if (isEnabled)
                {
                    pool.Add(swag);
                }
            }

            var chosen = pool.Shuffle(rng).FirstOrDefault();
            if (chosen != null)
            {
                var ammoMin = config.GetValueOrDefault($"{prefix}/ammo/min", 0);
                var ammoMax = config.GetValueOrDefault($"{prefix}/ammo/max", 0);
                var ammoTotal = rng.Next(ammoMin, ammoMax + 1);
                chosen.WeaponAmount = Math.Min(ammoTotal, chosen.Definition.Max);
                if (chosen.Definition.Ammo is int[] ammo && ammo.Length != 0)
                {
                    chosen.ExtraAmount = ammoTotal - chosen.WeaponAmount;
                    chosen.ExtraType = rng.NextOf(ammo);
                    chosen.ExtraMaxStack = context.Variation.Map.Items[chosen.ExtraType].Max;
                }
            }
            return chosen;
        }


        private class InventoryBuilder
        {
            private List<RandomInventory.Entry> _entries = [];

            public int Capacity { get; }
            public bool Full => _entries.Count >= Capacity;

            public InventoryBuilder(int capacity)
            {
                Capacity = capacity;
            }

            public RandomInventory Build()
            {
                return new RandomInventory([.. _entries], null);
            }

            public void Add(Item item)
            {
                _entries.Add(new RandomInventory.Entry(item.Type, (byte)item.Amount));
            }

            public void Add(WeaponSwag swag)
            {
                Add(new Item((byte)swag.WeaponType, (ushort)swag.WeaponAmount));

                var extra = swag.ExtraAmount;
                while (extra > 0)
                {
                    var take = Math.Min(extra, swag.ExtraMaxStack);
                    Add(new Item((byte)swag.ExtraType, (ushort)take));
                    extra -= take;
                }
            }
        }

        private class WeaponSwag(int itemId, MapItemDefinition definition)
        {
            public MapItemDefinition Definition => definition;
            public int WeaponType => itemId;
            public int WeaponAmount { get; set; }
            public int ExtraType { get; set; }
            public int ExtraAmount { get; set; }
            public int ExtraMaxStack { get; set; }

            public string Group => definition.Kind.Split('/').Skip(1).First();
        }

        private class ItemPool
        {
            private List<List<Item>> _groups = [];
            private int _next = -1;

            public void AddGroup(IClassicRandomizerPlayerContext context, string prefix)
            {
                var g = GetRandomItems(context, prefix);
                if (g.Count != 0)
                    _groups.Add(g);
            }

            public Item? TakeStack(IClassicRandomizerPlayerContext context)
            {
                var rng = context.Rng;
                if (_next == -1 || _next >= _groups.Count)
                {
                    _groups = [.. _groups.Shuffle(rng)];
                    _next = 0;
                }

                var group = _groups[_next];
                var itemIndex = rng.Next(0, group.Count);
                var item = group[itemIndex];
                var definition = context.Variation.Map.Items[item.Type];
                var take = Math.Min(item.Amount, definition.Max);
                var remaining = item.Amount - take;
                if (remaining > 0)
                {
                    group[itemIndex] = new Item(item.Type, (ushort)(item.Amount - take));
                }
                else
                {
                    group.RemoveAt(itemIndex);
                    if (group.Count == 0)
                    {
                        _groups.RemoveAt(_next);
                    }
                }
                _next++;
                return new Item(item.Type, (ushort)take);
            }

            private List<Item> GetRandomItems(IClassicRandomizerPlayerContext context, string prefix)
            {
                var items = new List<Item>();
                var config = context.Configuration;
                var rng = context.Rng;
                foreach (var kvp in context.Variation.Map.Items)
                {
                    var itemId = kvp.Key;
                    var definition = kvp.Value;
                    if (!definition.Kind.StartsWith(prefix))
                        continue;

                    var min = config.GetValueOrDefault($"inventory/{definition.Kind}/min", 0);
                    var max = config.GetValueOrDefault($"inventory/{definition.Kind}/max", 0);
                    var amount = rng.Next(min, max + 1);
                    if (amount > 0)
                    {
                        items.Add(new Item((byte)itemId, (ushort)amount));
                    }
                }
                return items;
            }
        }

    }

    internal class LockRandomizer
    {
        public void Randomise(IClassicRandomizerPlayerContext context)
        {
            var map = context.Variation.Map;
            var modBuilder = context.ModBuilder;

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
                        if (door.Id == null)
                            continue;

                        var doorInfo = new DoorInfo(roomKey, room, door);
                        if (door.Target == null)
                        {
                            // Door doesn't go anywhere, lock it
                            if (!door.NoUnlock)
                            {
                                SetDoorLock(modBuilder, doorInfo, new DoorLock(255, 255));
                            }
                        }
                        else
                        {
                            doors.Add(doorInfo.Identity, doorInfo);
                        }
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

            if (context.Configuration.GetValueOrDefault("locks/random", true))
            {
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
                throw new RandomizerUserException("Lock randomizer not implemented yet.");
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
            if (doorLock.KeyItemId == 255)
                doorInfo.Door.Requires2 = [];
            else
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
        public void RandomiseItems(IClassicRandomizerPlayerContext context)
        {
            var map = context.Variation.Map;
            var seed = context.Rng.Next(0, int.MaxValue);
            var modBuilder = context.ModBuilder;

            var includeDocuments =
                context.Configuration.GetValueOrDefault("items/documents", false) &&
                context.Configuration.GetValueOrDefault("items/documents/keys", false);
            var includeHiddenItems = context.Configuration.GetValueOrDefault("items/hidden/keys", false);

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
                            if (item.Optional == true)
                                continue;

                            if (item.Document == true && !includeDocuments)
                                continue;

                            if (item.Hidden == true && !includeHiddenItems)
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
        public void Randomize(IClassicRandomizerPlayerContext context)
        {
            var config = context.Configuration;
            var map = context.Variation.Map;
            var rng = context.Rng;
            var modBuilder = context.ModBuilder;

            var documentPriority = config.GetValueOrDefault("items/documents", false)
                ? config.GetValueOrDefault("items/documents/keys", false)
                    ? ItemPriority.Normal
                    : ItemPriority.Low
                : ItemPriority.Disabled;
            var hiddenPriority = config.GetValueOrDefault("items/hidden/keys", false)
                ? ItemPriority.Normal
                : ItemPriority.Low;

            var itemSlots = new ItemSlotCollection(modBuilder, map, documentPriority, hiddenPriority, rng);

            // Inventory weapons
            var inventoryWeapons = modBuilder.Inventory[0].Entries
                .Select(x => new KeyValuePair<int, MapItemDefinition>(x.Type, map.Items[x.Type]))
                .Where(x => x.Value.Kind.StartsWith("weapon/"))
                .Select(x => new WeaponInfo(x.Key, x.Value, config))
                .ToArray();

            // Weapons
            var weapons = map.Items
                .Where(x => x.Value.Kind.StartsWith("weapon/"))
                .Select(x => new WeaponInfo(x.Key, x.Value, config))
                .Shuffle(rng);
            var wpgroup = new HashSet<string>(inventoryWeapons.Select(x => x.Group));
            var wpplaced = new List<WeaponInfo>(inventoryWeapons);
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
                    .DistinctBy(x => x.GlobalId)
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

    internal class ModBuilder
    {
        private readonly Dictionary<RdtItemId, DoorLock> _doorLock = new();
        private readonly Dictionary<int, Item> _itemMap = new();

        public ImmutableArray<RandomInventory> Inventory { get; set; } = [];
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

        public string GetDump(IClassicRandomizerPlayerContext context)
        {
            var map = context.Variation.Map;
            var sb = new StringBuilder();

            sb.AppendLine($"# Inventory");
            DumpInventory(context.Variation.PlayerName, Inventory[0]);

            sb.AppendLine($"# Items");

            var placedItems = _itemMap
                .Select(x => new PlacedItem(x.Key, x.Value, map.GetItem(x.Value.Type)))
                .GroupBy(x => x.Group);

            DumpGroup("key", "Keys");
            DumpGroup("weapon", "Weapons");
            DumpGroup("ammo", "Ammo");
            DumpGroup("health", "Health");
            DumpGroup("ink", "Ink");
            return sb.ToString();

            void DumpInventory(string playerName, RandomInventory inventory)
            {
                sb.AppendLine($"## {playerName}");
                sb.AppendLine("| Item | Amount |");
                sb.AppendLine("|------|--------|");
                foreach (var entry in inventory.Entries)
                {
                    if (entry.Part != 0)
                        continue;

                    var itemName = map.GetItem(entry.Type)?.Name ?? $"{entry.Type}";
                    sb.AppendLine($"| {itemName} | {entry.Count} |");
                }
            }

            void DumpGroup(string group, string heading)
            {
                var g = placedItems.FirstOrDefault(x => x.Key == group);
                if (g == null)
                    return;

                var filtered = g
                    .Select(x => x)
                    .OrderBy(x => x.Item.Type)
                    .ThenBy(x => x.GlobalId)
                    .ToArray();

                sb.AppendLine($"## {heading}");
                sb.AppendLine("| ID | Item | Amount | RDT | Room | Location |");
                sb.AppendLine("|----|------|--------|-----|------|----------|");
                foreach (var i in filtered)
                {
                    var (rdtId, room, location) = GetItemSlotName(map, i.GlobalId);
                    sb.AppendLine($"| {i.GlobalId} | {i.Definition?.Name} | {i.Item.Amount} | {rdtId} | {room} | {location} |");
                }
                sb.AppendLine();
            }
        }

        private static (RdtId rdtId, string room, string location) GetItemSlotName(Map map, int globalId)
        {
            foreach (var kvp in map.Rooms)
            {
                var room = kvp.Value;
                if (room.Items == null)
                    continue;

                var rdt = room.Rdts?.FirstOrDefault() ?? new RdtId();
                foreach (var item in room.Items)
                {
                    if (item.GlobalId == globalId)
                    {
                        return (rdt, room.Name ?? "", item.Name ?? "");
                    }
                }
            }
            return (default, "", "");
        }

        private readonly struct PlacedItem(int globalId, Item item, MapItemDefinition? definition)
        {
            public int GlobalId => globalId;
            public Item Item => item;
            public MapItemDefinition? Definition => definition;
            public string Group => definition?.Kind.Split('/').First() ?? "";
        }
    }

    [DebuggerDisplay("Id = {Id} Key = {KeyItemId}")]
    internal readonly struct DoorLock(int id, int keyItemId)
    {
        public int Id => id;
        public int KeyItemId => keyItemId;
    }
}
