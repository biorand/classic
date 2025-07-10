using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace IntelOrca.Biohazard.BioRand
{
    internal class BgmBatchEncoder(DataManager dataManager)
    {
        public void Process(ClassicMod mod, ClassicRebirthModBuilder crModBuilder)
        {
            if (FfmpegCommand.IsSupported())
            {
                var tempConvertPath = GetOrCreateTempPath();
                Parallel.ForEach(mod.Music, kvp =>
                {
                    var path = kvp.Key;
                    var music = kvp.Value;
                    var tempInputPath = Path.Combine(tempConvertPath, $"{Guid.NewGuid()}{Path.GetExtension(music.Path)}");
                    var tempOutputPath = Path.Combine(tempConvertPath, $"{Guid.NewGuid()}.wav");
                    try
                    {
                        File.WriteAllBytes(tempInputPath, dataManager.GetData(music.Path));
                        var ffmpegCommand = new FfmpegCommand()
                        {
                            Input = tempInputPath,
                            Output = tempOutputPath,
                            Format = "pcm_u8",
                            Channels = 1,
                            SampleRate = 22050,
                            Duration = 3 * 60
                        };
                        ffmpegCommand.Go();
                        var wavData = File.ReadAllBytes(tempOutputPath);
                        crModBuilder.SetFile(path, wavData);
                    }
                    finally
                    {
                        Delete(tempInputPath);
                        Delete(tempOutputPath);
                    }
                });
            }
            else
            {
                Parallel.ForEach(mod.Music, kvp =>
                {
                    var path = kvp.Key;
                    var music = kvp.Value;
                    var builder = new WaveformBuilder(volume: 0.75f);
                    var inputStream = new MemoryStream(dataManager.GetData(music.Path));
                    builder.Append(music.Path, inputStream, 0, 2.5 * 60);
                    var wavData = builder.ToArray();
                    crModBuilder.SetFile(path, wavData);
                });
            }
        }

        private static string GetOrCreateTempPath()
        {
            var tempConvertPath = Path.Combine(Path.GetTempPath(), "biorand", "convert");
            Directory.CreateDirectory(tempConvertPath);
            return tempConvertPath;
        }

        private static void Delete(string path)
        {
            try
            {
                if (File.Exists(path))
                {
                    File.Delete(path);
                }
            }
            catch
            {
            }
        }

        private class FfmpegCommand
        {
            public required string Input { get; set; }
            public required string Output { get; set; }
            public string Format { get; set; } = "pcm_s16le";
            public int? Channels { get; set; }
            public int? SampleRate { get; set; }
            public int? BitRate { get; set; }
            public int? Duration { get; set; }

            public void Go() => GoAsync().Wait();

            public async Task GoAsync()
            {
                var ffmpegPath = FindExecutable() ?? throw new Exception("Unable to find ffmpeg");
                var args = new List<string>();
                args.Add("-i");
                args.Add(Input);

                args.Add("-c:a");
                args.Add(Format);
                if (BitRate is int bitrate)
                {
                    args.Add("-b:a");
                    args.Add(bitrate.ToString());
                }
                if (Channels is int channels)
                {
                    args.Add("-ac");
                    args.Add(channels.ToString());
                }
                if (SampleRate is int sampleRate)
                {
                    args.Add("-ar");
                    args.Add(sampleRate.ToString());
                }
                if (Duration is int duration)
                {
                    args.Add("-t");
                    args.Add(duration.ToString());
                }

                args.Add("-y");
                args.Add(Output);

                var arguments = string.Join(" ", args.Select(x => x.Contains(" ") ? $"\"{x}\"" : x));
                var psi = new ProcessStartInfo(ffmpegPath, arguments)
                {
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false
                };
                var process = System.Diagnostics.Process.Start(psi);
                var tcs = new TaskCompletionSource<int>();
                process.EnableRaisingEvents = true;
                process.Exited += (s, e) => tcs.SetResult(process.ExitCode);

                var stdoutReadTask = process.StandardOutput.ReadToEndAsync();
                var stderrReadTask = process.StandardError.ReadToEndAsync();

                var stdout = await stdoutReadTask;
                var stderr = await stderrReadTask;
                var stdouterr = string.Join("\n", stdout, stderr);

                var exitCode = process.HasExited ? process.ExitCode : await tcs.Task;
                if (exitCode != 0)
                {
                    Console.Error.WriteLine(stdouterr);
                    throw new Exception($"Failed to run ffmpeg, exit code {exitCode}\n" + stdouterr);
                }
            }

            public static bool IsSupported()
            {
                return FindExecutable() != null;
            }

            private static string? FindExecutable()
            {
                var fileName = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "ffmpeg.exe" : "ffmpeg";
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
                return null;
            }
        }
    }
}
