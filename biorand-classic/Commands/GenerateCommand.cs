using System;
using System.ComponentModel;
using System.IO;
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
                var randomizer = Program.CreateClassicRandomizer(version);
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

            var builder = Program.CreateModBuilder(version);
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
    }
}
