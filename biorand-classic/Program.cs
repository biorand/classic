using System;
using System.IO;
using System.Linq;
using System.Reflection;
using IntelOrca.Biohazard.BioRand.Classic.Commands;
using Spectre.Console.Cli;

namespace IntelOrca.Biohazard.BioRand.Classic
{
    internal class Program
    {
        public static int Main(string[] args)
        {
            var app = new CommandApp();
            app.Configure(config =>
            {
                config.PropagateExceptions();
                config.Settings.ApplicationName = "biorand-classic";
                config.Settings.ApplicationVersion = GetVersion();
                config.AddCommand<AgentCommand>("agent")
                    .WithDescription("Runs a remote generator agent for generating randos")
                    .WithExample("agent", "localhost:8080", "-k", "nCF6UaetQJJ053QLwhXqUGR68U85Rcia");
                config.AddCommand<DataCommand>("data")
                    .WithDescription("Download or update the data to the latest version.")
                    .WithExample("data", "-u");
                config.AddCommand<GenerateCommand>("generate")
                    .WithDescription("Generates a new rando")
                    .WithExample("generate", "-o", "mod_biorand-35825.7z", "-g", "re1", "--seed", "35825", "--config", "tough.json")
                    .WithExample("generate", "-o", "mod_biorand", "-g", "re1", "--seed", "35825", "--config", "tough.json")
                    .WithExample("generate", "-o", "mod.json", "-g", "re1", "--seed", "35825", "--config", "tough.json")
                    .WithExample("generate", "-o", "mod_biorand-35825.7z", "--input", "mod.json");
            });
            return app.Run(args);
        }

        private static string GetVersion()
        {
            return GetGitHash();
        }

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

        public static IClassicRandomizer CreateClassicRandomizer(BioVersion version)
        {
            return ClassicRandomizerFactory.Default.Create(version, CreateDataManager(), CreateGameDataManager(version));
        }

        public static object CreateModBuilder(BioVersion version)
        {
            return ClassicRandomizerFactory.Default.CreateModBuilder(version, CreateDataManager(), CreateGameDataManager(version));
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
                dataManager.AddZip(Path.Combine(biorandDirectory, "data.zip"), "data");
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
