using System;
using System.IO;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace IntelOrca.Biohazard.BioRand
{
    public class RandoAppSettings
    {
        public string Seed1 { get; set; }
        public string Seed2 { get; set; }
        public string Seed3 { get; set; }
        public string SeedCv { get; set; }

        public int? LastSelectedGame { get; set; }

        public string GamePath1 { get; set; }
        public string GamePath2 { get; set; }
        public string GamePath3 { get; set; }
        public string GamePathCv { get; set; }

        public bool GameEnabled1 { get; set; }
        public bool GameEnabled2 { get; set; }
        public bool GameEnabled3 { get; set; }
        public bool GameEnabledCv { get; set; }

        public string GameExecutable1 { get; set; }
        public string GameExecutable2 { get; set; }
        public string GameExecutable3 { get; set; }

        public float BgmVolume1 { get; set; } = 0.75f;
        public float BgmVolume2 { get; set; } = 1.0f;
        public float BgmVolume3 { get; set; } = 0.75f;

        public bool DisableCustomContent { get; set; }

        public bool RandomizeTitleVoice { get; set; } = true;
        public bool MaxInventorySize { get; set; }
        public bool DoorSkip { get; set; }

        public string LastVersion { get; set; }


        public static string GetSettingsPath()
        {
            return Path.Combine(GetSettingsDirectory(), "settings.json");
        }

        public static string GetGenerationLogPath()
        {
            return Path.Combine(GetSettingsDirectory(), "generation.log");
        }

        public static string GetSettingsDirectory()
        {
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            return Path.Combine(appData, "biorand");
        }

        public static string GetCustomContentDirectory()
        {
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            return Path.Combine(appData, "biorand", "data");
        }

        private static JsonSerializerOptions GetJsonSerializerOptions()
        {
            return new JsonSerializerOptions()
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                ReadCommentHandling = JsonCommentHandling.Skip,
                WriteIndented = true
            };
        }

        public static RandoAppSettings Load()
        {
            try
            {
                var settingsPath = GetSettingsPath();
                if (File.Exists(settingsPath))
                {
                    var json = File.ReadAllText(settingsPath);
                    var instance = JsonSerializer.Deserialize<RandoAppSettings>(json, GetJsonSerializerOptions());
                    return instance;
                }
                else
                {
                    return AutoDetermineGames();
                }
            }
            catch
            {
            }
            return new RandoAppSettings();
        }

        public void Save()
        {
            try
            {
                var settingsPath = GetSettingsPath();
                var json = JsonSerializer.Serialize(this, GetJsonSerializerOptions());
                Directory.CreateDirectory(Path.GetDirectoryName(settingsPath));
                File.WriteAllText(settingsPath, json);
            }
            catch
            {
            }
        }

        public static void LogGeneration(string seed, string gamePath)
        {
            var dt = DateTime.Now;

            var path = GetGenerationLogPath();
            using (var fs = new FileStream(path, FileMode.Append, FileAccess.Write))
            {
                var sw = new StreamWriter(fs);
                sw.WriteLine($"Generated randomizer [{seed}] @ {dt} in \"{gamePath}\"");
                sw.Flush();
            }
        }

        private static RandoAppSettings AutoDetermineGames()
        {
            var settings = new RandoAppSettings();
            var location = Program.CurrentAssembly.Location;
            var potentialExtractionDirectory = Path.GetDirectoryName(Path.GetDirectoryName(location));
            var directories = Directory.GetDirectories(potentialExtractionDirectory);
            foreach (var dir in directories)
            {
                var dirName = Path.GetFileName(dir);
                if (Regex.IsMatch(dirName, "re[123](hd)?"))
                {
                    var game = dirName[2] - '0';
                    switch (game)
                    {
                        case 1:
                            settings.GameEnabled1 = true;
                            settings.GamePath1 = dir;
                            settings.GameExecutable1 = "Bio.exe";
                            break;
                        case 2:
                            settings.GameEnabled2 = true;
                            settings.GamePath2 = dir;
                            settings.GameExecutable2 = "bio2 1.10.exe";
                            break;
                        case 3:
                            settings.GameEnabled3 = true;
                            settings.GamePath3 = dir;
                            settings.GameExecutable3 = "BIOHAZARD(R) 3 PC.exe";
                            break;
                    }
                }
            }
            return settings;
        }
    }
}
