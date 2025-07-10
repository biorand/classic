using System;
using System.ComponentModel;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using IntelOrca.Biohazard.BioRand.RE1;
using Spectre.Console;
using Spectre.Console.Cli;

namespace IntelOrca.Biohazard.BioRand.Classic.Commands
{
    internal sealed class GenerateCommand : AsyncCommand<GenerateCommand.Settings>
    {
        public sealed class Settings : CommandSettings
        {
            [Description("Seed to generate")]
            [CommandOption("-s|--seed")]
            public int Seed { get; init; }

            [Description("Configuration to use")]
            [CommandOption("-c|--config")]
            public string? ConfigPath { get; init; }

            [CommandOption("-i|--input")]
            public string? InputPath { get; init; }

            [CommandOption("-o|--output")]
            public string? OutputPath { get; init; }

            [CommandOption("-g|--game")]
            public string? Game { get; init; }
        }

        public override ValidationResult Validate(CommandContext context, Settings settings)
        {
            if (settings.InputPath == null && GetBioVersion(settings.Game) == null)
            {
                return ValidationResult.Error($"Unknown game or not specified");
            }
            if (settings.OutputPath == null)
            {
                return ValidationResult.Error($"Output path not specified");
            }
            return base.Validate(context, settings);
        }


        public override Task<int> ExecuteAsync(CommandContext context, Settings settings)
        {
            var input = new RandomizerInput();
            input.Seed = settings.Seed;
            if (!string.IsNullOrEmpty(settings.ConfigPath))
            {
                var configJson = File.ReadAllText(settings.ConfigPath);
                input.Configuration = RandomizerConfiguration.FromJson(configJson);
            }

            if (settings.InputPath == null)
            {
                // Randomizer not generated
                var version = GetBioVersion(settings.Game)!.Value;
                var randomizer = ClassicRandomizerFactory.Default.Create(version, CreateDataManager());
                var mod = randomizer.RandomizeToMod(input);
                if (settings.OutputPath!.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
                {
                    // Just generate mod JSON
                    File.WriteAllText(settings.OutputPath!, mod.ToJson());
                }
                else
                {
                    // Generate the mod too
                    return Task.FromResult(GenerateMod(mod, settings.OutputPath!));
                }
            }
            else
            {
                // Randomizer pre-generated, just generate the mod
                var mod = ClassicMod.FromJson(File.ReadAllText(settings.InputPath));
                return Task.FromResult(GenerateMod(mod, settings.OutputPath!));
            }
            return Task.FromResult(0);
        }

        private static int GenerateMod(ClassicMod mod, string outputPath)
        {
            if (GetBioVersion(mod.Game) is not BioVersion version)
            {
                Console.Error.WriteLine("Unsupported or invalid game moniker.");
                return 1;
            }

            var builder = ClassicRandomizerFactory.Default.CreateModBuilder(version, CreateDataManager(), CreateGameDataManager(version));
            if (builder is ICrModBuilder crModBuilder)
            {
                var crMod = crModBuilder.Create(mod);
                if (outputPath.EndsWith(".7z", StringComparison.OrdinalIgnoreCase))
                {
                    File.WriteAllBytes(outputPath, crMod.Create7z());
                }
                else
                {
                    crMod.Dump(outputPath);
                }
                return 0;
            }
            else
            {
                Console.Error.WriteLine("Failed to generate mod.");
                return 1;
            }
        }

        private static BioVersion? GetBioVersion(string? game)
        {
            return game?.ToLowerInvariant() switch
            {
                "re1" => BioVersion.Biohazard1,
                "re2" => BioVersion.Biohazard2,
                "re3" => BioVersion.Biohazard3,
                "recv" => BioVersion.BiohazardCv,
                _ => null
            };
        }

        private static DataManager CreateDataManager()
        {
            var dataManager = new DataManager();
            var env = Environment.GetEnvironmentVariable("BIORAND_DATA");
            if (env == null)
            {
                var biorandDirectory = GetExecutableDirectory();
                dataManager.AddFileSystem(GetCustomDataDirectory());
                dataManager.AddFileSystem(Path.Combine(biorandDirectory, "data"));
                dataManager.AddZip(Path.Combine(biorandDirectory, "data.zip"));
                dataManager.AddFileSystem(Path.Combine(biorandDirectory, "meta"));
            }
            else
            {
                var paths = env.Split(Path.PathSeparator);
                foreach (var p in paths)
                {
                    if (p.EndsWith(".zip"))
                    {
                        dataManager.AddZip(p, "data");
                    }
                    else
                    {
                        dataManager.AddFileSystem(p);
                    }
                }
            }
            return dataManager;
        }

        private static string GetCustomDataDirectory()
        {
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            return Path.Combine(appData, "biorand", "data");
        }

        private static DataManager CreateGameDataManager(BioVersion version)
        {
            var dataManager = new DataManager();
            var env = Environment.GetEnvironmentVariable("BIORAND_GAMEDATA_1");
            if (env != null)
            {
                dataManager.AddFileSystem(env);
            }
            return dataManager;
        }

        private static string GetExecutableDirectory()
        {
            var assemblyLocation = Assembly.GetEntryAssembly()?.Location;
            if (assemblyLocation == null)
                return Environment.CurrentDirectory;

            return Path.GetDirectoryName(assemblyLocation) ?? Environment.CurrentDirectory;
        }
    }
}
