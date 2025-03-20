using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
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
            var group = page.CreateGroup("");
            group.Items.Add(new RandomizerConfigurationDefinition.GroupItem()
            {
                Id = "variation",
                Label = "Variation",
                Description = "Set which variation of the game to randomize.",
                Type = "dropdown",
                Options = [.. controller.VariationNames],
                Default = controller.VariationNames[0]
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
            group.Items.Add(new RandomizerConfigurationDefinition.GroupItem()
            {
                Id = "locks/preserve",
                Label = "Preserve Vanilla Locks",
                Description = "Do not change doors that already have locks.",
                Type = "switch",
                Default = false
            });
            group.Items.Add(new RandomizerConfigurationDefinition.GroupItem()
            {
                Id = "locks/ratio",
                Label = "Amount of Locked Doors",
                Description = "Amount of doors to lock.",
                Type = "percent",
                Step = 0.01,
                Min = 0,
                Max = 0.5,
                Default = 0.25
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

            page = result.CreatePage("Enemies");
            group = page.CreateGroup("");
            group.Items.Add(new RandomizerConfigurationDefinition.GroupItem()
            {
                Id = "enemies/random",
                Label = "Randomize Enemies",
                Description = "Allow BioRand to place random enemies in random places in each room.",
                Type = "switch",
                Default = true
            });
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

            group = page.CreateGroup("Min/Max per Room");
            foreach (var enemyGroup in map.Enemies)
            {
                foreach (var entry in enemyGroup.Entries)
                {
                    group.Items.Add(new RandomizerConfigurationDefinition.GroupItem()
                    {
                        Id = $"enemies/min/{entry.Key}",
                        Label = $"Min. {entry.Name}",
                        Type = "range",
                        Min = 1,
                        Max = 16,
                        Step = 1,
                        Default = 1
                    });
                    group.Items.Add(new RandomizerConfigurationDefinition.GroupItem()
                    {
                        Id = $"enemies/max/{entry.Key}",
                        Label = $"Max. {entry.Name}",
                        Type = "range",
                        Min = 1,
                        Max = 16,
                        Step = 1,
                        Default = 6
                    });
                }
            }

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

            var modifiedConfig = input.Configuration.Clone();
            var ink = modifiedConfig.GetValueOrDefault("ink/enable", "Default");
            if (ink == "Default")
            {
                ink = modifiedConfig.GetValueOrDefault("variation", "Chris") == "Chris" ? "Always" : "Never";
            }
            else if (ink == "Random")
            {
                ink = rng.NextOf("Never", "Always");
            }
            if (ink != "Always")
            {
                modifiedConfig["inventory/ink/min"] = 0;
                modifiedConfig["inventory/ink/max"] = 0;
                modifiedConfig["items/distribution/ink"] = 0;
            }

            var context = new Context(modifiedConfig, dataManager, gameDataManager, rng);
            var generatedVariation = Randomize(context);
            var crModBuilder = CreateCrModBuilder(input, generatedVariation);

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

        private IClassicRandomizerGeneratedVariation Randomize(IClassicRandomizerContext context)
        {
            var chosenVariationName = context.Configuration.GetValueOrDefault("variation", controller.VariationNames[0]);
            var variation = controller.GetVariation(context, chosenVariationName ?? "");
            var generatedVariation = new GeneratedVariation(context, variation, new ModBuilder());
            if (context.Configuration.GetValueOrDefault("doors/random", true))
            {
                throw new RandomizerUserException("Door randomizer not implemented yet.");
            }
            var inventoryRandomizer = new InventoryRandomizer();
            inventoryRandomizer.Randomize(generatedVariation);
            var lockRandomizer = new LockRandomizer();
            lockRandomizer.Randomise(generatedVariation);
            if (context.Configuration.GetValueOrDefault("items/random", false))
            {
                var keyRandomizer = new KeyRandomizer();
                keyRandomizer.RandomiseItems(generatedVariation);
                var itemRandomizer = new ItemRandomizer();
                itemRandomizer.Randomize(generatedVariation);
            }
            if (context.Configuration.GetValueOrDefault("enemies/random", false))
            {
                var enemyRandomizer = new EnemyRandomizer();
                enemyRandomizer.Randomize(generatedVariation);
            }
            return generatedVariation;
        }

        private ClassicRebirthModBuilder CreateCrModBuilder(RandomizerInput input, IClassicRandomizerGeneratedVariation context)
        {
            var crModBuilder = new ClassicRebirthModBuilder($"BioRand | {input.ProfileName} | {input.Seed}");
            crModBuilder.Description =
                $"""
                BioRand 4.0 ({BuildVersion})
                Profile: {input.ProfileName} by {input.ProfileAuthor}
                Seed: {input.Seed}

                {input.ProfileDescription}
                """;
            crModBuilder.SetFile("config.json", input.Configuration.ToJson(true));

            crModBuilder.Module = new ClassicRebirthModule("biorand.dll", context.DataManager.GetData("biorand.dll"));
            crModBuilder.SetFile(
                $"log_{context.Variation.PlayerName.ToLowerInvariant()}.md",
                context.ModBuilder.GetDump(context));

            controller.Write(context, crModBuilder);
            return crModBuilder;
        }

        private sealed class Context(
            RandomizerConfiguration configuration,
            DataManager dataManager,
            DataManager gameDataManager,
            Rng rng) : IClassicRandomizerContext
        {
            public RandomizerConfiguration Configuration => configuration;
            public DataManager DataManager => dataManager;
            public DataManager GameDataManager => gameDataManager;
            public Rng Rng => rng;
        }

        private sealed class GeneratedVariation(
            IClassicRandomizerContext context,
            Variation variation,
            ModBuilder modBuilder) : IClassicRandomizerGeneratedVariation
        {
            public RandomizerConfiguration Configuration => context.Configuration;
            public DataManager DataManager => context.DataManager;
            public DataManager GameDataManager => context.GameDataManager;
            public Rng Rng => context.Rng;
            public IClassicRandomizerContext Context => context;
            public Variation Variation => variation;
            public ModBuilder ModBuilder => modBuilder;
        }
    }

    internal class InventoryRandomizer
    {
        public void Randomize(IClassicRandomizerGeneratedVariation context)
        {
            var config = context.Configuration;
            var rng = context.Rng.NextFork();

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

            var primary = GetRandomWeapon("inventory/primary");
            if (primary != null)
            {
                inventoryState.Add(primary);
            }

            var secondary = GetRandomWeapon("inventory/secondary", exclude: primary);
            if (secondary != null)
            {
                inventoryState.Add(secondary);
            }

            var itemPool = new ItemPool(rng);
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

            WeaponSwag? GetRandomWeapon(string prefix, WeaponSwag? exclude = null)
            {
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
                    var ammoMin = config.GetValueOrDefault($"{prefix}/ammo/min", 0.0) * chosen.Definition.Max;
                    var ammoMax = config.GetValueOrDefault($"{prefix}/ammo/max", 0.0) * chosen.Definition.Max;
                    var ammoTotal = (int)Math.Round(rng.NextDouble(ammoMin, ammoMax));
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
                while (_entries.Count < Capacity)
                {
                    _entries.Add(new RandomInventory.Entry());
                }
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

        private class ItemPool(Rng rng)
        {
            private List<List<Item>> _groups = [];
            private int _next = -1;

            public void AddGroup(IClassicRandomizerGeneratedVariation context, string prefix)
            {
                var g = GetRandomItems(context, prefix);
                if (g.Count != 0)
                    _groups.Add(g);
            }

            public Item? TakeStack(IClassicRandomizerGeneratedVariation context)
            {
                if (_next == -1 || _next >= _groups.Count)
                {
                    _groups = [.. _groups.Shuffle(rng)];
                    _next = 0;
                }
                if (_groups.Count == 0)
                {
                    return null;
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

            private List<Item> GetRandomItems(IClassicRandomizerGeneratedVariation context, string prefix)
            {
                var items = new List<Item>();
                var config = context.Configuration;
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

    internal class KeyRandomizer
    {
        public void RandomiseItems(IClassicRandomizerGeneratedVariation context)
        {
            var map = context.Variation.Map;
            var rng = context.Rng.NextFork();
            var seed = rng.Next(0, int.MaxValue);
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
            var globalIdToKey = itemNodeToGlobalId
                .Select(kvp => (kvp.Value, route.GetItemContents(kvp.Key)))
                .Where(x => x.Item2 != null)
                .ToDictionary(x => x.Item1, x => x.Item2!.Value);
            var locationNameToKey = globalIdToKey.ToDictionary(x => globalIdToName[x.Key], x => x.Value);
            var log = route.Log;

            var segmentTree = GetSegmentTree(map);
            foreach (var kvp in globalIdToKey)
            {
                var globalId = kvp.Key;
                var key = kvp.Value;

                var itemType = (byte)keyToItemId[key];
                var itemDefinition = map.GetItem(itemType);
                var amount = itemDefinition?.Amount ?? 1;
                if (itemDefinition?.Discard == true)
                {
                    var requireS = $"item({itemType})";
                    var segment = segmentTree.FindSegmentContaining(globalId);
                    if (segment == null)
                        continue;

                    var allEdges = segment.DescendantRooms
                        .SelectMany(x => x.AllEdges)
                        .Where(x => x.Requires2?.Contains(requireS) == true)
                        .ToList();
                    var lockIds = new HashSet<int>();
                    for (var i = 0; i < allEdges.Count; i++)
                    {
                        var edge = allEdges[i];
                        if (edge is MapRoomDoor door)
                        {
                            if (door.LockId is byte lockId)
                            {
                                if (!lockIds.Add(lockId))
                                {
                                    allEdges.RemoveAt(i);
                                    i--;
                                }
                            }
                        }
                    }
                    amount = allEdges.Count;
                }
                modBuilder.SetItem(kvp.Key, new Item(itemType, (ushort)amount));
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

        private static Segment GetSegmentTree(Map map)
        {
            var firstRoom = map.GetRoom(map.BeginEndRooms?.FirstOrDefault()?.Start ?? "");
            if (firstRoom == null)
                return new Segment([], []);

            var visited = new HashSet<MapRoom>();
            return VisitSegment(firstRoom);

            Segment VisitSegment(MapRoom room)
            {
                var rooms = ImmutableHashSet.CreateBuilder<MapRoom>();
                var children = ImmutableArray.CreateBuilder<Segment>();
                VisitRoom(room, rooms, children);
                return new Segment(rooms.ToImmutable(), children.ToImmutable());
            }

            void VisitRoom(MapRoom room, ImmutableHashSet<MapRoom>.Builder rooms, ImmutableArray<Segment>.Builder children)
            {
                if (visited.Add(room))
                {
                    rooms.Add(room);
                    foreach (var d in room.Doors ?? [])
                    {
                        if (d.Kind == "blocked")
                            continue;

                        var targetRoom = map.GetRoom(d.TargetRoom ?? "");
                        if (targetRoom == null)
                            continue;

                        if (d.Kind == "noreturn")
                            children.Add(VisitSegment(targetRoom));
                        else
                            VisitRoom(targetRoom, rooms, children);
                    }
                }
            }
        }

        private class Segment(ImmutableHashSet<MapRoom> rooms, ImmutableArray<Segment> children)
        {
            public ImmutableHashSet<MapRoom> Rooms => rooms;
            public ImmutableArray<Segment> Children => children;

            public Segment? FindSegmentContaining(int globalId)
            {
                if (rooms.SelectMany(x => x.Items ?? []).Any(x => x.GlobalId == globalId))
                {
                    return this;
                }
                return children
                    .Select(x => x.FindSegmentContaining(globalId))
                    .FirstOrDefault(x => x != null);
            }

            public IEnumerable<MapRoom> DescendantRooms
            {
                get
                {
                    foreach (var r in rooms)
                    {
                        yield return r;
                    }
                    foreach (var segment in children)
                    {
                        foreach (var r in segment.DescendantRooms)
                        {
                            yield return r;
                        }
                    }
                }
            }
        }
    }

    internal class ItemRandomizer
    {
        public void Randomize(IClassicRandomizerGeneratedVariation context)
        {
            var config = context.Configuration;
            var map = context.Variation.Map;
            var rng = context.Rng.NextFork();
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
                .Where(x => x.Type != 0)
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

    internal class EnemyRandomizer
    {
        public void Randomize(IClassicRandomizerGeneratedVariation context)
        {
            var rng = context.Rng.NextFork();

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
            var enemyPositions = context.DataManager.GetJson<EnemyRandomiser.EnemyPosition[]>(BioVersion.Biohazard1, "enemy.json");
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
                                Esp = rwe.EnemyInfo.Entry.Esp ?? []
                            };
                            context.ModBuilder.AddEnemy(placement);
                        }
                    }
                    else
                    {
                        // Clear room, add new enemies
                        var min = rwe.EnemyInfo.MinPerRoom;
                        var max = rwe.EnemyInfo.MaxPerRoom + 1;
                        if (e.MaxDifficulty is int maxDifficulty)
                            max = Math.Max(1, (int)Math.Round(max / (double)(4 - maxDifficulty)));

                        var numEnemies = rng.Next(min, max + 1);
                        var positions = enemyPositions
                            .Where(x => rwe.Room.Rdts.Contains(RdtId.Parse(x.Room ?? "")))
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

            RoomWithEnemies[] GetRoomSelection()
            {
                var map = context.Variation.Map;
                var config = context.Configuration;
                var separateRdts = config.GetValueOrDefault("progression/mansion/split", false);
                var roomWeight = config.GetValueOrDefault("enemies/rooms", 0.0);

                // Get a list of room tags where we don't want enemies
                var banTags = new List<string>();
                if (!config.GetValueOrDefault("enemies/box", false))
                    banTags.Add("box");
                if (!config.GetValueOrDefault("enemies/safe", false))
                    banTags.Add("safe");
                if (!config.GetValueOrDefault("enemies/save", false))
                    banTags.Add("save");

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
                    var roomTags = kvp.Value.Tags ?? [];
                    if (roomTags.Any(banTags.Contains))
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

    internal class ModBuilder
    {
        private readonly Dictionary<RdtItemId, DoorLock?> _doorLock = new();
        private readonly Dictionary<int, Item> _itemMap = new();
        private readonly List<EnemyPlacement> _enemyPlacements = new();

        public ImmutableArray<RandomInventory> Inventory { get; set; } = [];
        public ImmutableArray<int> AssignedItemGlobalIds => [.. _itemMap.Keys];
        public ImmutableArray<EnemyPlacement> EnemyPlacements => [.. _enemyPlacements];

        public void SetDoorTarget(RdtItemId doorIdentity, RdtItemId target)
        {
        }

        public void SetDoorLock(RdtItemId doorIdentity, DoorLock? doorLock)
        {
            _doorLock.Add(doorIdentity, doorLock);
        }

        public Item? GetItem(int globalId)
        {
            return _itemMap.TryGetValue(globalId, out var item) ? item : null;
        }

        public void SetItem(int globalId, Item item)
        {
            _itemMap.Add(globalId, item);
        }

        public void AddEnemy(EnemyPlacement placement)
        {
            _enemyPlacements.Add(placement);
        }

        public void ApplyToRdt(RandomizedRdt rrdt)
        {
            foreach (var doorOpcode in rrdt.Doors)
            {
                var doorIdentity = new RdtItemId(rrdt.RdtId, doorOpcode.Id);
                if (_doorLock.TryGetValue(doorIdentity, out var doorLock))
                {
                    if (doorLock == null)
                    {
                        doorOpcode.LockId = 0;
                        doorOpcode.LockType = 0;
                    }
                    else
                    {
                        doorOpcode.LockId = (byte)(doorLock.Value.Id | 0x80);
                        doorOpcode.LockType = (byte)doorLock.Value.KeyItemId;
                    }
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

        public string GetDump(IClassicRandomizerGeneratedVariation context)
        {
            var map = context.Variation.Map;
            var mdb = new MarkdownBuilder();

            mdb.Heading(1, "Inventory");
            DumpInventory(context.Variation.PlayerName, Inventory[0]);

            mdb.Heading(1, "Items");

            var placedItems = _itemMap
                .Select(x => new PlacedItem(x.Key, x.Value, map.GetItem(x.Value.Type)))
                .GroupBy(x => x.Group);

            DumpGroup("key", "Keys");
            DumpGroup("weapon", "Weapons");
            DumpGroup("ammo", "Ammo");
            DumpGroup("health", "Health");
            DumpGroup("ink", "Ink");

            mdb.Heading(1, "Enemies");
            mdb.Heading(3, "Summary");
            mdb.Table("Enemy", "Total", "Rooms", "Avg. per room");
            var typeToEntryMap = map.Enemies
                .SelectMany(x => x.Entries)
                .SelectMany(x => x.Id.Select(y => (x, y)))
                .ToDictionary(x => x.Item2, x => x.Item1);
            foreach (var g in _enemyPlacements.GroupBy(x => typeToEntryMap[x.Type]).OrderByDescending(x => x.Count()))
            {
                var enemyName = g.Key.Name;
                var total = g.Count();
                var rooms = g.GroupBy(x => x.RdtId).Count();
                var avg = ((double)total / rooms).ToString("0.0");
                mdb.TableRow(enemyName, total, rooms, avg);
            }

            mdb.Heading(3, "List");
            mdb.Table("Global ID", "RDT", "Room", "ID", "Enemy");
            foreach (var enemy in _enemyPlacements.OrderBy(x => x.RdtId).ThenBy(x => x.Id))
            {
                var entry = map.Enemies.SelectMany(x => x.Entries).FirstOrDefault(x => x.Id.Contains(enemy.Type));
                var enemyName = entry?.Name ?? $"{enemy.Type}";
                var roomName = map.GetRoomsContaining(enemy.RdtId).FirstOrDefault()?.Name ?? "";
                mdb.TableRow(enemy.GlobalId, enemy.RdtId, roomName, enemy.Id, enemyName);
            }

            mdb.Heading(1, "Doors");
            mdb.Table("RDT", "ID", "ROOM", "DOOR", "LOCK", "REQUIRES");
            foreach (var r in map.Rooms)
            {
                foreach (var d in r.Value.Doors ?? [])
                {
                    var rdt = r.Value.Rdts.FirstOrDefault();
                    var requires = string.Join(", ", (d.Requires2 ?? []).Select(GetRequiresString));
                    mdb.TableRow(rdt, (object?)d.Id ?? "", r.Key, r.Value.Name ?? "", d.LockId ?? 0, requires);
                }
            }


            return mdb.Build();

            void DumpInventory(string playerName, RandomInventory inventory)
            {
                mdb.Heading(2, playerName);
                mdb.Table("Item", "Amount");
                foreach (var entry in inventory.Entries)
                {
                    if (entry.Part != 0)
                        continue;

                    var itemName = map.GetItem(entry.Type)?.Name ?? $"{entry.Type}";
                    mdb.TableRow(itemName, entry.Count);
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

                mdb.Heading(2, heading);
                mdb.Table("ID", "Item", "Amount", "RDT", "Room", "Location");
                foreach (var i in filtered)
                {
                    var (rdtId, room, location) = GetItemSlotName(map, i.GlobalId);
                    mdb.TableRow(i.GlobalId, i.Definition?.Name ?? "", i.Item.Amount, rdtId, room, location);
                }
            }

            string GetRequiresString(string s)
            {
                var req = MapRequirement.Parse(s);
                if (req.Kind == MapRequirementKind.Item)
                {
                    if (int.TryParse(req.Value, out var itemId))
                    {
                        var item = map.GetItem(itemId);
                        return item?.Name ?? s;
                    }
                }
                return s;
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

    internal class EnemyPlacement
    {
        public RdtId RdtId { get; set; }
        public int GlobalId { get; set; }
        public int Id { get; set; }
        public int Type { get; set; }
        public int X { get; set; }
        public int Y { get; set; }
        public int Z { get; set; }
        public int D { get; set; }
        public bool Create { get; set; }
        public int[] Esp { get; set; } = [];
        public string? Condition { get; set; }
    }
}
