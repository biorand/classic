using System;
using System.IO;
using System.Text.RegularExpressions;

namespace IntelOrca.Biohazard.BioRand
{
    /// <summary>
    /// Loads and saves <see cref="RandoAppSettings"/> for the GUI.
    /// </summary>
    public static class AppSettingsStore
    {
        public static string GetGenerationLogPath()
        {
            return Path.Combine(RandoAppSettings.GetSettingsDirectory(), "generation.log");
        }

        public static RandoAppSettings Load()
        {
            try
            {
                var settingsPath = RandoAppSettings.GetSettingsPath();
                if (File.Exists(settingsPath))
                {
                    return RandoAppSettings.Load(settingsPath);
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

        public static void Save(RandoAppSettings settings)
        {
            try
            {
                settings.Save(RandoAppSettings.GetSettingsPath());
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
