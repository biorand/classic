using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace IntelOrca.Biohazard.BioRand
{
    internal class DataManager
    {
        public string[] BasePaths { get; }

        public DataManager(params string[] basePaths)
        {
            BasePaths = basePaths;
        }

        public string GetPath(string baseName)
        {
            string? best = null;
            foreach (var basePath in BasePaths)
            {
                var result = Path.Combine(basePath, baseName);
                var exists = Directory.Exists(result) || File.Exists(result);
                if (best == null || exists)
                {
                    best = result;
                    if (exists)
                        break;
                }
            }
            return best!;
        }

        public string GetPath(string baseName, string path) => GetPath(Path.Combine(baseName, path));

        public string GetPath(BioVersion version, string path)
        {
            switch (version)
            {
                case BioVersion.Biohazard1:
                    return GetPath("re1", path);
                case BioVersion.Biohazard2:
                    return GetPath("re2", path);
                case BioVersion.Biohazard3:
                    return GetPath("re3", path);
                case BioVersion.BiohazardCv:
                    return GetPath("recv", path);
                default:
                    throw new NotImplementedException();
            }
        }

        public byte[] GetData(string path)
        {
            var fullPath = GetPath(path);
            return File.ReadAllBytes(fullPath);
        }

        public byte[] GetData(BioVersion version, string path)
        {
            var fullPath = GetPath(version, path);
            return File.ReadAllBytes(fullPath);
        }

        public byte[]? TryGetData(BioVersion version, string path)
        {
            var fullPath = GetPath(version, path);
            if (!File.Exists(fullPath))
                return null;
            return File.ReadAllBytes(fullPath);
        }

        public string GetText(BioVersion version, string path)
        {
            var fullPath = GetPath(version, path);
            return File.ReadAllText(fullPath);
        }

        public T GetJson<T>(BioVersion version, string path)
        {
            var options = new JsonSerializerOptions()
            {
                ReadCommentHandling = JsonCommentHandling.Skip,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            };
            options.Converters.Add(new RdtIdConverter());

            // RdtIdConverter
            var json = GetText(version, path);
            var map = JsonSerializer.Deserialize<T>(json, options)!;
            return map;
        }

        public string[] GetDirectories(BioVersion version, string baseName) => GetDirectories(GetSubPath(version, baseName));
        public string[] GetDirectories(string baseName)
        {
            var result = new List<string>();
            foreach (var basePath in BasePaths)
            {
                var path = Path.Combine(basePath, baseName);
                var dirs = GetDirectoriesSafe(path);
                result.AddRange(dirs);
            }
            return result.ToArray();
        }

        public string[] GetFiles(BioVersion version, string baseName) => GetFiles(GetSubPath(version, baseName));
        public string[] GetFiles(string a, string b) => GetFiles(Path.Combine(a, b));
        public string[] GetFiles(string baseName)
        {
            var result = new List<string>();
            foreach (var basePath in BasePaths)
            {
                var part = GetFilesSafe(Path.Combine(basePath, baseName));
                result.AddRange(part);
            }
            return result.ToArray();
        }

        public string[] GetBgmFiles(string tag) => GetTaggedFiles("bgm", tag);
        public string[] GetHurtFiles(string actor) => GetFiles("hurt", actor);
        public string[] GetVoiceFiles(string actor) => GetFiles("voice", actor);

        public string[] GetTaggedFiles(string baseName, string tag)
        {
            var files = new List<string>();
            foreach (var basePath in BasePaths)
            {
                var top = Path.Combine(basePath, baseName);
                var directories = GetDirectoriesSafe(top);
                foreach (var directory in directories)
                {
                    var dir = Path.Combine(directory, tag);
                    var subFiles = GetFilesSafe(dir);
                    files.AddRange(subFiles);
                }
            }
            return files.ToArray();
        }

        private static string[] GetFilesSafe(string path)
        {
            if (Directory.Exists(path))
            {
                return Directory.GetFiles(path);
            }
            return new string[0];
        }

        private static string[] GetDirectoriesSafe(string path)
        {
            if (Directory.Exists(path))
            {
                return Directory.GetDirectories(path);
            }
            return new string[0];
        }

        private string GetSubPath(BioVersion version, string basePath)
        {
            return version switch
            {
                BioVersion.Biohazard1 => Path.Combine("re1", basePath),
                BioVersion.Biohazard2 => Path.Combine("re2", basePath),
                BioVersion.Biohazard3 => Path.Combine("re3", basePath),
                BioVersion.BiohazardCv => Path.Combine("recv", basePath),
                _ => throw new NotImplementedException(),
            };
        }
    }
}
