using System;
using System.Collections.Immutable;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;

namespace IntelOrca.Biohazard.BioRand
{
    public sealed class ClassicRebirthMod
    {
        private readonly ImmutableDictionary<string, byte[]> _files;

        internal ClassicRebirthMod(ImmutableDictionary<string, byte[]> files)
        {
            _files = files;
        }

        public void Dump(string outputPath)
        {
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
            // -mx9      Use best compression
            // -ms=e     Use a separate solid block for each file extension
            // -mqs=on   Enable sorting files by type in solid archives

            // Classic Rebirth is very slow at reading solid 7z archives and it advises
            // against using solid archives. However solid archives are so much smaller,
            // luckily CR is still fast at loading the archive if we use a separate solid
            // block per file type. The file size is still just as small which is great!

            var sevenZipPath = Find7z() ?? throw new Exception("Unable to find 7z");
            var psi = new ProcessStartInfo(sevenZipPath, $"a -r -mx9 -ms=e -mqs=on -ms=2m \"{outputPath}\" *")
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
