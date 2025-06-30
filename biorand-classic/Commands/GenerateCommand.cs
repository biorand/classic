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

            [CommandOption("-n")]
            public bool NoMod { get; init; }

            [CommandOption("-g|--game")]
            public string? Game { get; init; }
        }

        public override ValidationResult Validate(CommandContext context, Settings settings)
        {
            if (GetBioVersion(settings.Game) == null)
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
            var game = GetBioVersion(settings.Game)!.Value;
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
                var randomizer = ClassicRandomizerFactory.Default.Create(game);
                var mod = randomizer.RandomizeToMod(input);
                if (settings.NoMod)
                {
                    File.WriteAllText(settings.OutputPath!, mod.ToJson());
                }
                else
                {
                }
            }
            else
            {
                // Randomizer pre-generated
                var mod = ModBuilder.FromJson(File.ReadAllText(settings.InputPath));
                var builder = ClassicRandomizerFactory.Default.CreateModBuilder(game);
                if (builder is ICrModBuilder crModBuilder)
                {
                    var crMod = crModBuilder.Create(mod);
                    File.WriteAllBytes("", crMod.Create7z());
                }
            }

            // var output = randomizer.Randomize(input);
            // foreach (var asset in output.Assets)
            // {
            //     var assetPath = Path.Combine(settings.OutputPath!, asset.FileName);
            //     File.WriteAllBytes(assetPath, asset.Data);
            // }

            return Task.FromResult(0);
        }

        private static BioVersion? GetBioVersion(string? game)
        {
            return game?.ToLowerInvariant() switch
            {
                "1" => BioVersion.Biohazard1,
                "2" => BioVersion.Biohazard2,
                "3" => BioVersion.Biohazard3,
                "cv" => BioVersion.BiohazardCv,
                _ => null
            };
        }
    }
}
