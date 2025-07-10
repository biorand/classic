using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Text.Json;

namespace IntelOrca.Biohazard.BioRand
{
    public class DataManager : IDisposable
    {
        private readonly List<IArea> _areas = new();

        public DataManager()
        {
        }

        public DataManager(string[] strings)
        {
            throw new NotImplementedException();
        }

        public void Dispose()
        {
            foreach (var area in _areas)
            {
                area.Dispose();
            }
        }

        public void AddFileSystem(string path)
        {
            _areas.Add(new FileSystemArea(path));
        }

        public void AddZip(string path, string basePath = "")
        {
            _areas.Add(new ZipArea(path, basePath));
        }

        public string GetPath(string baseName, string path) => Path.Combine(baseName, path);

        public string GetPath(BioVersion version, string path)
        {
            return version switch
            {
                BioVersion.Biohazard1 => GetPath("re1", path),
                BioVersion.Biohazard2 => GetPath("re2", path),
                BioVersion.Biohazard3 => GetPath("re3", path),
                BioVersion.BiohazardCv => GetPath("recv", path),
                _ => throw new NotImplementedException(),
            };
        }

        public byte[]? GetData(string path)
        {
            foreach (var area in _areas)
            {
                using var result = area.GetData(path);
                if (result != null)
                {
                    var ms = new MemoryStream();
                    result.CopyTo(ms);
                    return ms.ToArray();
                }
            }
            return null;
        }

        public byte[]? GetData(BioVersion version, string path) => GetData(GetPath(version, path));

        public string? GetText(BioVersion version, string path)
        {
            var data = GetData(version, path);
            if (data != null)
            {
                // Check for UTF-8 BOM
                if (data.Length >= 3 &&
                    data[0] == 0xEF &&
                    data[1] == 0xBB &&
                    data[2] == 0xBF)
                {
                    // Skip BOM
                    return Encoding.UTF8.GetString(data, 3, data.Length - 3);
                }
                else
                {
                    return Encoding.UTF8.GetString(data);
                }
            }
            return null;
        }

        public T GetJson<T>(BioVersion version, string path)
        {
            var options = new JsonSerializerOptions()
            {
                ReadCommentHandling = JsonCommentHandling.Skip,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            };
            options.Converters.Add(new RdtIdConverter());

            var json = GetText(version, path);
            var map = JsonSerializer.Deserialize<T>(json, options)!;
            return map;
        }

        public string[] GetDirectories(BioVersion version, string baseName) => GetDirectories(GetSubPath(version, baseName));
        public string[] GetDirectories(string baseName)
        {
            var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var area in _areas)
            {
                result.AddRange(area.GetDirectories(baseName));
            }
            return result
                .OrderBy(x => x)
                .ToArray();
        }

        public string[] GetFiles(BioVersion version, string baseName) => GetFiles(GetSubPath(version, baseName));
        public string[] GetFiles(string a, string b) => GetFiles(Path.Combine(a, b));
        public string[] GetFiles(string baseName)
        {
            var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var area in _areas)
            {
                result.AddRange(area.GetFiles(baseName));
            }
            return result
                .OrderBy(x => x)
                .ToArray();
        }

        public string[] GetBgmFiles(string tag) => GetTaggedFiles("bgm", tag);
        public string[] GetHurtFiles(string actor) => GetFiles("hurt", actor);
        public string[] GetVoiceFiles(string actor) => GetFiles("voice", actor);

        public string[] GetTaggedFiles(string baseName, string tag)
        {
            var files = new List<string>();
            var directories = GetDirectories(baseName);
            foreach (var directory in directories)
            {
                files.AddRange(GetFiles(Path.Combine(directory, tag)));
            }
            return files.ToArray();
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

        internal string GetPath(string biorandModuleFilename)
        {
            throw new NotImplementedException();
        }

        private interface IArea : IDisposable
        {
            Stream? GetData(string path);
            string[] GetFiles(string path);
            string[] GetDirectories(string path);
        }

        private class FileSystemArea(string basePath) : IArea
        {
            public void Dispose()
            {
            }

            public Stream? GetData(string path)
            {
                var fullPath = Path.Combine(basePath, path);
                if (!File.Exists(fullPath))
                    return null;
                return new FileStream(fullPath, FileMode.Open, FileAccess.Read);
            }

            public string[] GetDirectories(string path)
            {
                try
                {
                    var fullPath = Path.Combine(basePath, path);
                    if (Directory.Exists(fullPath))
                    {
                        return Directory.GetDirectories(fullPath);
                    }
                }
                catch
                {
                }
                return [];
            }

            public string[] GetFiles(string path)
            {
                try
                {
                    var fullPath = Path.Combine(basePath, path);
                    if (Directory.Exists(fullPath))
                    {
                        return Directory.GetFiles(fullPath);
                    }
                }
                catch
                {
                }
                return [];
            }
        }

        private class ZipArea : IArea
        {
            private ZipArchive? _zip;
            private readonly string _basePath = "";
            private readonly object _sync = new object();

            public ZipArea(string path, string basePath = "")
            {
                try
                {
                    _zip = ZipFile.OpenRead(path);
                    _basePath = NormalizeDirectoryPath(basePath);
                }
                catch
                {
                }
            }

            public void Dispose()
            {
                _zip?.Dispose();
                _zip = null;
            }

            public Stream? GetData(string path)
            {
                if (_zip != null)
                {
                    try
                    {

                        var entry = _zip.GetEntry(TransformFilePath(path));
                        if (entry != null)
                        {
                            var ms = new MemoryStream();
                            lock (_sync)
                            {
                                using var compressedEntry = entry.Open();
                                compressedEntry.CopyTo(ms);
                            }
                            ms.Position = 0;
                            return ms;
                        }
                    }
                    catch
                    {
                    }
                }
                return null;
            }

            public string[] GetDirectories(string path)
            {
                if (_zip == null)
                    return [];

                var normalized = TransformDirectoryPath(path);
                return _zip.Entries
                    .Where(x => x.FullName.StartsWith(normalized, StringComparison.OrdinalIgnoreCase))
                    .Select(x => x.FullName[normalized.Length..])
                    .Where(x => x.Length > 0 && x.Contains('/'))
                    .Select(x => x[..x.IndexOf('/')])
                    .ToArray();
            }

            public string[] GetFiles(string path)
            {
                if (_zip == null)
                    return [];

                var normalized = TransformDirectoryPath(path);
                return _zip.Entries
                    .Where(x => x.FullName.StartsWith(normalized))
                    .Select(x => x.FullName[normalized.Length..])
                    .Where(x => x.Length > 0 && !x.Contains('/'))
                    .ToArray();
            }

            private string TransformFilePath(string path)
            {
                var normalized = NormalizeFilePath(path);
                normalized = _basePath + normalized;
                return normalized;
            }

            private string TransformDirectoryPath(string path)
            {
                var normalized = NormalizeDirectoryPath(path);
                normalized = _basePath + normalized;
                return normalized;
            }

            private static string NormalizeFilePath(string path)
            {
                return path.Replace("\\", "/");
            }

            private static string NormalizeDirectoryPath(string path)
            {
                if (path == "")
                    return "";

                var normalized = path.Replace("\\", "/");
                if (!normalized.EndsWith("/"))
                    normalized += '/';
                return normalized;
            }
        }
    }
}
