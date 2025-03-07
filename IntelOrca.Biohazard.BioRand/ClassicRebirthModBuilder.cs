using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace IntelOrca.Biohazard.BioRand
{
    internal sealed class ClassicRebirthModBuilder(string name)
    {
        private readonly Dictionary<string, byte[]> _files = new(StringComparer.OrdinalIgnoreCase);

        public string Name { get; set; } = name;
        public string? Description { get; set; }
        public ClassicRebirthModule? Module { get; set; }

        private void CreateManifest()
        {
            var sb = new StringBuilder();
            sb.Append("[MOD]\r\n");
            sb.Append($"Name = {Name}\r\n");
            if (Module is ClassicRebirthModule m)
            {
                sb.Append($"Module = {m.FileName}\r\n");
            }

            var data = Encoding.UTF8.GetBytes(sb.ToString());
            SetFile("manifest.txt", data);
        }

        public void SetFile(string path, ReadOnlyMemory<byte> data)
        {
            lock (_files)
            {
                _files[path] = data.ToArray();
            }
        }

        private void AddSupplementaryFiles()
        {
            CreateManifest();
            if (Module is ClassicRebirthModule m)
            {
                SetFile(m.FileName, m.Data);
            }
            if (Description is string d)
            {
                var processed = d
                    .Replace("\r\n", "\n")
                    .Replace("\r", "\n")
                    .Replace("\n", "\r\n");
                if (!processed.EndsWith("\r\n"))
                    processed += "\r\n";
                var data = Encoding.UTF8.GetBytes(processed);
                SetFile("description.txt", data);
            }
        }

        public void Dump(string outputPath)
        {
            AddSupplementaryFiles();

            try
            {
                if (Directory.Exists(outputPath))
                {
                    Directory.Delete(outputPath, true);
                }
            }
            catch
            {
            }
            Directory.CreateDirectory(outputPath);
            foreach (var kvp in _files)
            {
                var fullPath = Path.Combine(outputPath, kvp.Key);
                var dir = Path.GetDirectoryName(fullPath);
                if (dir != null)
                {
                    Directory.CreateDirectory(dir);
                }
                File.WriteAllBytes(fullPath, kvp.Value);
            }
        }

        public byte[] Create7z()
        {
            AddSupplementaryFiles();

            using var tempFolder = new TempFolder();
            foreach (var kvp in _files)
            {
                var dir = Path.GetDirectoryName(kvp.Key);
                if (dir != null)
                {
                    tempFolder.GetOrCreateDirectory(dir);
                }
                var fullPath = Path.Combine(tempFolder.BasePath, kvp.Key);
                File.WriteAllBytes(fullPath, kvp.Value);
            }
            return SevenZip(tempFolder.BasePath);
        }

        private static byte[] SevenZip(string directory)
        {
            var tempFile = Path.GetTempFileName() + ".7z";
            try
            {
                SevenZip(tempFile, directory);
                return File.ReadAllBytes(tempFile);
            }
            finally
            {
                try
                {
                    File.Delete(tempFile);
                }
                catch
                {
                }
            }
        }

        private static void SevenZip(string outputPath, string directory)
        {
            var sevenZipPath = Find7z() ?? throw new Exception("Unable to find 7z");
            var psi = new ProcessStartInfo(sevenZipPath, $"a -r -mx9 \"{outputPath}\" *")
            {
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                WorkingDirectory = directory
            };
            var process = Process.Start(psi);

            var stdoutReadTask = process.StandardOutput.ReadToEndAsync();
            var stderrReadTask = process.StandardError.ReadToEndAsync();

            process.WaitForExit();
            if (process.ExitCode != 0)
                throw new Exception("Failed to create 7z");
        }

        private static string? Find7z()
        {
            var fileName = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "7z.exe" : "7z";
            var pathEnvironment = Environment.GetEnvironmentVariable("PATH");
            var paths = pathEnvironment.Split(Path.PathSeparator);
            foreach (var path in paths)
            {
                var full = Path.Combine(path, fileName);
                if (File.Exists(full))
                {
                    return full;
                }
            }

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                var programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
                var defaultPath = Path.Combine(programFiles, "7-Zip", "7z.exe");
                if (File.Exists(defaultPath))
                {
                    return defaultPath;
                }
            }
            return null;
        }
    }
}
