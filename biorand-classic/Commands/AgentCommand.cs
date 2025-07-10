using System;
using System.ComponentModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using IntelOrca.Biohazard.BioRand.Common;
using Spectre.Console;
using Spectre.Console.Cli;

namespace IntelOrca.Biohazard.BioRand.Classic.Commands
{
    internal sealed class AgentCommand : AsyncCommand<AgentCommand.Settings>
    {
        public sealed class Settings : CommandSettings
        {
            [Description("Host")]
            [CommandArgument(0, "<host>")]
            public required string Host { get; init; }

            [Description("Seed to generate")]
            [CommandOption("-k|--key")]
            public required string ApiKey { get; init; }
        }

        public override async Task<int> ExecuteAsync(CommandContext context, Settings settings)
        {
            var gameId = await GetGameIdAsync(settings.Host, "re1")
                ?? throw new Exception("re1 game moniker not found.");
            var agent = new RandomizerAgent(
                settings.Host,
                settings.ApiKey,
                gameId,
                new RandomizerAgentHandler());
            var cts = new CancellationTokenSource();
            Console.CancelKeyPress += (sender, e) =>
            {
                e.Cancel = true;
                cts.Cancel();
            };
            try
            {
                await agent.RunAsync(cts.Token);
            }
            catch (TaskCanceledException)
            {
            }
            return 0;
        }

        private static async Task<int?> GetGameIdAsync(string uri, string moniker)
        {
            var client = new RandomizerClient(uri);
            var games = await client.GetGamesAsync();
            var game = games.FirstOrDefault(x => x.Moniker == moniker);
            return game?.Id;
        }

        private class RandomizerAgentHandler : IRandomizerAgentHandler
        {
            public IRandomizer Randomizer { get; } = Program.CreateClassicRandomizer(BioVersion.Biohazard1);

            public Task<bool> CanGenerateAsync(RandomizerAgent.QueueResponseItem queueItem)
            {
                return Task.FromResult(true);
            }

            public Task<RandomizerOutput> GenerateAsync(RandomizerAgent.QueueResponseItem queueItem, RandomizerInput input)
            {
                if (!queueItem.UserTags.Contains("re1:tester"))
                {
                    throw new RandomizerUserException("The RE 1 cloud randomizer is currently only available to testers.");
                }

                return Task.FromResult(Randomizer.Randomize(input));
            }

            public void LogInfo(string message) => AnsiConsole.MarkupLine($"[gray]{Timestamp} {message}[/]");
            public void LogError(Exception ex, string message) => AnsiConsole.MarkupLine($"[red]{Timestamp} {message} ({ex.Message})[/]");

            private static string Timestamp => DateTime.Now.ToString("[[yyyy-MM-dd HH:mm]]");
        }
    }
}
