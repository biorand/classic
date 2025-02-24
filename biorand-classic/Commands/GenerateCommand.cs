using System.ComponentModel;
using System.IO;
using System.Threading.Tasks;
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
        }

        public override ValidationResult Validate(CommandContext context, Settings settings)
        {
            if (settings.OutputPath == null)
            {
                return ValidationResult.Error($"Output path not specified");
            }
            return base.Validate(context, settings);
        }


        public override Task<int> ExecuteAsync(CommandContext context, Settings settings)
        {
            var randomizer = GetRandomizer();
            var input = new RandomizerInput();
            input.Seed = settings.Seed;
            input.GamePath = settings.InputPath;
            if (!string.IsNullOrEmpty(settings.ConfigPath))
            {
                var configJson = File.ReadAllText(settings.ConfigPath);
                input.Configuration = RandomizerConfiguration.FromJson(configJson);
            }
            var output = randomizer.Randomize(input);

            foreach (var asset in output.Assets)
            {
                var assetPath = Path.Combine(settings.OutputPath!, asset.FileName);
                File.WriteAllBytes(assetPath, asset.Data);
            }

            return Task.FromResult(0);
        }

        private IRandomizer GetRandomizer() => new ClassicRandomizer();
    }
}
