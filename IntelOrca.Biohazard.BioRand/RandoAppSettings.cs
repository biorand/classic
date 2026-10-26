using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace IntelOrca.Biohazard.BioRand
{
    /// <summary>
    /// User settings shared by the BioRand GUI and biorand-cli, stored in settings.json.
    /// </summary>
    public class RandoAppSettings
    {
        public string? Seed1 { get; set; }
        public string? Seed2 { get; set; }
        public string? Seed3 { get; set; }
        public string? SeedCv { get; set; }

        /// <summary>
        /// Zero based game index: 0 = RE1, 1 = RE2, 2 = RE3, 3 = CVX.
        /// </summary>
        public int? LastSelectedGame { get; set; }

        public string? GamePath1 { get; set; }
        public string? GamePath2 { get; set; }
        public string? GamePath3 { get; set; }
        public string? GamePathCv { get; set; }
        public string? GamePathCvPc { get; set; }
        public bool GenerateCvPcMod { get; set; }

        public bool GameEnabled1 { get; set; }
        public bool GameEnabled2 { get; set; }
        public bool GameEnabled3 { get; set; }
        public bool GameEnabledCv { get; set; }

        public string? GameExecutable1 { get; set; }
        public string? GameExecutable2 { get; set; }
        public string? GameExecutable3 { get; set; }
        public string? GameExecutableCv { get; set; }

        public float BgmVolume1 { get; set; } = 0.75f;
        public float BgmVolume2 { get; set; } = 1.0f;
        public float BgmVolume3 { get; set; } = 0.75f;

        public bool DisableCustomContent { get; set; }

        public bool RandomizeTitleVoice { get; set; } = true;
        public bool MaxInventorySize { get; set; }
        public bool DoorSkip { get; set; }

        public string? LastVersion { get; set; }

        /// <summary>
        /// Settings written by a different version of BioRand, kept so that saving does not drop them.
        /// </summary>
        [JsonExtensionData]
        public Dictionary<string, JsonElement>? AdditionalSettings { get; set; }

        /// <summary>
        /// Gets the saved seed for a game, numbered as in <see cref="RandoConfig.Game"/>.
        /// </summary>
        public string? GetSeed(int game) => game switch
        {
            1 => Seed1,
            2 => Seed2,
            3 => Seed3,
            4 => SeedCv,
            _ => null
        };

        public void SetSeed(int game, string seed)
        {
            switch (game)
            {
                case 1: Seed1 = seed; break;
                case 2: Seed2 = seed; break;
                case 3: Seed3 = seed; break;
                case 4: SeedCv = seed; break;
            }
        }

        /// <summary>
        /// Gets the game data path for a game, numbered as in <see cref="RandoConfig.Game"/>.
        /// For CVX this is the PS2 ISO, which is used for both PS2 and PC output.
        /// </summary>
        public string? GetGamePath(int game) => game switch
        {
            1 => GamePath1,
            2 => GamePath2,
            3 => GamePath3,
            4 => GamePathCv,
            _ => null
        };

        public float GetBgmVolume(int game) => game switch
        {
            1 => BgmVolume1,
            2 => BgmVolume2,
            3 => BgmVolume3,
            _ => 1.0f
        };

        public static string GetSettingsDirectory()
        {
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            return Path.Combine(appData, "biorand");
        }

        /// <summary>
        /// Gets the settings.json path. BIORAND_CONFIG can point to a different file or folder.
        /// </summary>
        public static string GetSettingsPath()
        {
            var path = Environment.GetEnvironmentVariable("BIORAND_CONFIG");
            if (string.IsNullOrWhiteSpace(path))
                return Path.Combine(GetSettingsDirectory(), "settings.json");

            path = Environment.ExpandEnvironmentVariables(path!.Trim());
            if (Directory.Exists(path) || path.EndsWith("/") || path.EndsWith("\\"))
                return Path.Combine(path, "settings.json");
            return path;
        }

        public static string GetCustomContentDirectory()
        {
            return Path.Combine(GetSettingsDirectory(), "data");
        }

        /// <summary>
        /// Loads settings from a file. Returns default settings if the file does not exist,
        /// and throws if it exists but cannot be read.
        /// </summary>
        public static RandoAppSettings Load(string path)
        {
            if (!File.Exists(path))
                return new RandoAppSettings();

            var json = File.ReadAllText(path);
            return JsonSerializer.Deserialize(json, RandoAppSettingsJsonContext.Default.RandoAppSettings)
                ?? throw new JsonException("Settings file does not contain a JSON object.");
        }

        /// <summary>
        /// Saves settings to a file. The file is replaced atomically so a failed write
        /// does not leave a truncated settings file behind.
        /// </summary>
        public void Save(string path)
        {
            var directory = Path.GetDirectoryName(Path.GetFullPath(path));
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);

            var json = JsonSerializer.Serialize(this, RandoAppSettingsJsonContext.Default.RandoAppSettings);
            var tempPath = path + ".tmp";
            File.WriteAllText(tempPath, json);
            if (File.Exists(path))
            {
                File.Replace(tempPath, path, null);
            }
            else
            {
                File.Move(tempPath, path);
            }
        }
    }

    [JsonSourceGenerationOptions(
        PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
        ReadCommentHandling = JsonCommentHandling.Skip,
        WriteIndented = true)]
    [JsonSerializable(typeof(RandoAppSettings))]
    internal partial class RandoAppSettingsJsonContext : JsonSerializerContext
    {
    }
}
