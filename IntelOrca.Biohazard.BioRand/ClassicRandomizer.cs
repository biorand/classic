using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using IntelOrca.Biohazard.BioRand.RE1;
using IntelOrca.Biohazard.Extensions;

namespace IntelOrca.Biohazard.BioRand
{
    internal class ClassicRandomizer(IClassicRandomizerController controller, DataManager dataManager, DataManager gameDataManager) : IClassicRandomizer
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
            var map = dataManager.GetJson<Map>(BioVersion.Biohazard1, "rdt.json");

            var result = new RandomizerConfigurationDefinition();
            var page = result.CreatePage("General");
            var group = page.CreateGroup("");
            group.Items.Add(new RandomizerConfigurationDefinition.GroupItem()
            {
                Id = "title/call/random",
                Label = "Random Title Call",
                Description = "Set whether to randomize the title sound that says \"RESIDENT EVIL\".",
                Type = "switch",
                Default = true
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
                Max = 6,
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
                Max = 1,
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
            group.Items.Add(new RandomizerConfigurationDefinition.GroupItem()
            {
                Id = "inventory/min",
                Label = "Min. Starting Items",
                Description = "Minimum number of inventory slots to fill up.",
                Type = "range",
                Min = 0,
                Max = 8,
                Step = 1,
                Default = 4
            });
            group.Items.Add(new RandomizerConfigurationDefinition.GroupItem()
            {
                Id = "inventory/max",
                Label = "Max. Starting Items",
                Description = "Maximum number of inventory slots to fill up.",
                Type = "range",
                Min = 0,
                Max = 8,
                Step = 1,
                Default = 8
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
                Description = "Documents & maps will be replaced with items.",
                Type = "switch",
                Default = true
            });
            group.Items.Add(new RandomizerConfigurationDefinition.GroupItem()
            {
                Id = "items/documents/keys",
                Label = "Documents can be Keys",
                Description = "Documents & maps can be keys or weapons if replaced with items.",
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

            group = page.CreateGroup("Skins");
            var enemySkins = GetEnemySkins();
            foreach (var skinPath in enemySkins)
            {
                var skinName = Path.GetFileName(skinPath);
                group.Items.Add(new RandomizerConfigurationDefinition.GroupItem()
                {
                    Id = $"enemies/skin/{skinName}",
                    Label = skinName.ToActorString(),
                    Type = "switch",
                    Default = true
                });
            }

            page = result.CreatePage("Protagonist");
            group = page.CreateGroup("");
            group.Items.Add(new RandomizerConfigurationDefinition.GroupItem()
            {
                Id = $"protagonist/random",
                Label = $"Randomize Protagonist",
                Description = "Randomizes the protagonist to one of the enabled characters.",
                Type = "switch",
                Default = false
            });

            group = page.CreateGroup("Characters");
            group.Items.AddRange(GetProtagonists()
                .Select(x => new RandomizerConfigurationDefinition.GroupItem()
                {
                    Id = $"protagonist/character/{Path.GetFileName(x)}",
                    Label = Path.GetFileName(x).ToActorString(),
                    Type = "switch",
                    Default = true
                }).OrderBy(x => x.Label));

            page = result.CreatePage("NPCs");
            group = page.CreateGroup("");
            group.Items.Add(new RandomizerConfigurationDefinition.GroupItem()
            {
                Id = $"npc/random",
                Label = $"Randomize NPCs",
                Description = "Randomizes every NPC to one of the enabled characters.",
                Type = "switch",
                Default = false
            });

            group = page.CreateGroup("Characters");
            group.Items.AddRange(GetProtagonists()
                .Select(x => new RandomizerConfigurationDefinition.GroupItem()
                {
                    Id = $"npc/character/{Path.GetFileName(x)}",
                    Label = Path.GetFileName(x).ToActorString(),
                    Type = "switch",
                    Default = true
                }).OrderBy(x => x.Label));

            page = result.CreatePage("Music");
            group = page.CreateGroup("");
            group.Items.Add(new RandomizerConfigurationDefinition.GroupItem()
            {
                Id = $"music/random",
                Label = $"Randomize Music",
                Description = "Randomizes all the background music in the game. Warning: This increases the download size considerably.",
                Type = "switch",
                Default = false
            });

            group = page.CreateGroup("Games");
            foreach (var gameDir in dataManager.GetDirectories("bgm"))
            {
                var game = Path.GetFileName(gameDir);
                group.Items.Add(new RandomizerConfigurationDefinition.GroupItem()
                {
                    Id = $"music/game/{game}",
                    Label = game.ToUpper(),
                    Type = "switch",
                    Default = true
                });
            }
            group.Items = group.Items.OrderBy(x => x.Label).ToList();

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

        private List<string> GetEnemySkins()
        {
            var result = new List<string>();
            foreach (var skinPath in dataManager.GetDirectories("re1/emd"))
            {
                result.Add(skinPath);
            }
            return result;
        }

        private List<string> GetProtagonists()
        {
            var result = new List<string>();
            foreach (var pl in new[] { "pld0", "pld1" })
            {
                var pldDir = Path.Combine("re1", pl);
                foreach (var characterPath in dataManager.GetDirectories(pldDir))
                {
                    result.Add(characterPath);
                }
            }
            return result;
        }

        public ClassicMod RandomizeToMod(RandomizerInput input)
        {
            var modBuilder = CreateModBuilder(input);
            var context = new Context(ConfigurationDefinition, input.Configuration.Clone(), dataManager, input.Seed);
            controller.ApplyConfigModifications(context, modBuilder);
            var generatedVariation = Randomize(context, modBuilder);
            return generatedVariation.ModBuilder.Build();
        }

        private ModBuilder CreateModBuilder(RandomizerInput input)
        {
            var modBuilder = new ModBuilder();
            modBuilder.Game = "re1";
            modBuilder.Name = $"BioRand | {input.ProfileName} | {input.Seed}";
            modBuilder.Description =
                $"""
                BioRand 4.0 ({BuildVersion})
                Profile: {input.ProfileName} by {input.ProfileAuthor}
                Seed: {input.Seed}

                {input.ProfileDescription}
                """;
            modBuilder.Seed = input.Seed;
            modBuilder.Configuration = input.Configuration;
            return modBuilder;

        }

        public RandomizerOutput Randomize(RandomizerInput input)
        {
            var mod = RandomizeToMod(input);
            var modBuilder = ClassicRandomizerFactory.Default.CreateModBuilder(BioVersion.Biohazard1, dataManager, gameDataManager);
            if (modBuilder is ICrModBuilder crModBuilder)
            {
                var crMod = crModBuilder.Create(mod);
                var assets = ImmutableArray.CreateBuilder<RandomizerOutputAsset>();

                var archiveFile = crMod.Create7z();
                assets.Add(new RandomizerOutputAsset(
                    "_1_mod",
                    "Classic Rebirth Mod",
                    "Drop this in your RE 1 install folder.",
                    $"mod_biorand_{input.Seed}.7z",
                    archiveFile));

                var logData = crMod.GetFile("log.html");
                if (logData != null)
                {
                    assets.Add(new RandomizerOutputAsset(
                        "_2_log",
                        "Spoiler Log",
                        "Shows you where keys are.",
                        $"mod_biorand_{input.Seed}.html",
                        logData));
                }

                return new RandomizerOutput(
                    assets.ToImmutable(),
                    "",
                    []);
            }
            else
            {
                throw new NotSupportedException("The _mod builder for this game version is not supported.");
            }
        }

        private IClassicRandomizerGeneratedVariation Randomize(IClassicRandomizerContext context, ModBuilder modBuilder)
        {
            var variation = controller.GetVariation(context);
            var generatedVariation = new GeneratedVariation(context, variation, modBuilder);
            var inventoryRandomizer = new InventoryRandomizer();
            inventoryRandomizer.Randomize(generatedVariation);
            if (context.Configuration.GetValueOrDefault("doors/random", false))
            {
                var doorRandomizer = new DoorRandomizer();
                doorRandomizer.Randomize(generatedVariation);
            }
            var lockRandomizer = new LockRandomizer();
            lockRandomizer.Randomise(generatedVariation);
            if (context.Configuration.GetValueOrDefault("items/random", false))
            {
                var keyRandomizer = new KeyRandomizer();
                keyRandomizer.Randomize(generatedVariation);
                var itemRandomizer = new ItemRandomizer();
                itemRandomizer.Randomize(generatedVariation);
            }
            if (context.Configuration.GetValueOrDefault("enemies/random", false))
            {
                var enemyRandomizer = new EnemyRandomizer();
                enemyRandomizer.Randomize(generatedVariation);
            }
            if (context.Configuration.GetValueOrDefault("protagonist/random", false) ||
                context.Configuration.GetValueOrDefault("npc/random", false))
            {
                var npcRandomizer = new CharacterRandomizer();
                npcRandomizer.Randomize(generatedVariation);
                var cutsceneRandomizer = new CutsceneRandomizer();
                cutsceneRandomizer.Randomize(generatedVariation);
            }
            RandomizeEnemySkins(generatedVariation);
            if (context.Configuration.GetValueOrDefault("music/random", false))
            {
                var musicRandomizer = new MusicRandomizer();
                musicRandomizer.Randomize(generatedVariation);
            }
            RandomizeTitleSound(generatedVariation);
            return generatedVariation;
        }

        private sealed class Context(
            RandomizerConfigurationDefinition configurationDefinition,
            RandomizerConfiguration configuration,
            DataManager dataManager,
            int seed) : IClassicRandomizerContext
        {
            public RandomizerConfigurationDefinition ConfigurationDefinition => configurationDefinition;
            public RandomizerConfiguration Configuration => configuration;
            public DataManager DataManager => dataManager;
            public Rng GetRng(string key)
            {
                var fnv1a = MemoryMarshal.AsBytes(key.AsSpan()).CalculateFnv1a();
                var a = (int)(fnv1a & 0xFFFFFFFF);
                var b = (int)(fnv1a >> 32);
                return new Rng(seed ^ a ^ b);
            }
        }

        private sealed class GeneratedVariation(
            IClassicRandomizerContext context,
            Variation variation,
            ModBuilder modBuilder) : IClassicRandomizerGeneratedVariation
        {
            public RandomizerConfigurationDefinition ConfigurationDefinition => context.ConfigurationDefinition;
            public RandomizerConfiguration Configuration => context.Configuration;
            public DataManager DataManager => context.DataManager;
            public Rng GetRng(string key) => context.GetRng(key);
            public IClassicRandomizerContext Context => context;
            public Variation Variation => variation;
            public ModBuilder ModBuilder => modBuilder;
        }

        private void RandomizeEnemySkins(IClassicRandomizerGeneratedVariation context)
        {
            var emdRegex = new Regex("EM10([0-9A-F][0-9A-F]).EMD", RegexOptions.IgnoreCase);

            var skins = ImmutableArray.CreateBuilder<string>();
            var usedIds = new HashSet<byte>();

            var skinPaths = GetEnemySkins().Shuffle(context.GetRng("enemyskin"));
            foreach (var skinPath in skinPaths)
            {
                var skinName = Path.GetFileName(skinPath);
                if (!context.Configuration.GetValueOrDefault($"enemies/skin/{skinName}", false))
                    continue;

                var ids = new List<byte>();
                var files = dataManager.GetFiles($"re1/emd/{skinPath}");
                foreach (var f in files)
                {
                    var fileName = Path.GetFileName(f);
                    var match = emdRegex.Match(fileName);
                    if (!match.Success)
                        continue;

                    var id = byte.Parse(match.Groups[1].Value, NumberStyles.HexNumber);
                    ids.Add(id);
                }

                if (usedIds.Overlaps(ids))
                    continue;

                usedIds.AddRange(ids);
                skins.Add(skinPath);
            }

            context.ModBuilder.EnemySkins = skins.ToImmutable();
        }

        private void RandomizeTitleSound(IClassicRandomizerGeneratedVariation context)
        {
            if (!context.Configuration.GetValueOrDefault<bool>("title/call/random", true))
                return;

            var rng = context.GetRng("titlesound");
            var files = context.DataManager
                .GetFiles("title")
                .Where(SupportedSoundExtension)
                .ToArray();
            if (files.Length == 0)
                return;

            var chosenFile = rng.NextOf(files);
            var chosenFilePath = $"title/{chosenFile}";
            context.ModBuilder.SetGeneral("titleSound", chosenFilePath);
        }

        private static bool SupportedSoundExtension(string fileName)
        {
            return fileName.EndsWith(".ogg", StringComparison.OrdinalIgnoreCase) ||
                   fileName.EndsWith(".wav", StringComparison.OrdinalIgnoreCase);
        }
    }
}
