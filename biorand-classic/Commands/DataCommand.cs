using System;
using System.ComponentModel;
using System.IO;
using System.Net.Http;
using System.Reflection;
using System.Text.Json;
using System.Threading.Tasks;
using Spectre.Console.Cli;

namespace IntelOrca.Biohazard.BioRand.Classic.Commands
{
    internal sealed class DataCommand : AsyncCommand<DataCommand.Settings>
    {
        public sealed class Settings : CommandSettings
        {
            [Description("Update data")]
            [CommandOption("-u|--update")]
            public required bool Update { get; init; }
        }

        public override async Task<int> ExecuteAsync(CommandContext context, Settings settings)
        {
            if (!settings.Update)
            {
                Console.Error.WriteLine("Pass -u to download the latest data files.");
                return 1;
            }

            var outputDirectory = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) ?? "";
            var outputPath = Path.Combine(outputDirectory, "data.zip");

            Console.WriteLine("Getting latest download URL...");
            var downloadUrl = await GetLatestZipDownloadAsync();

            Console.WriteLine($"Downloading data...");
            await DownloadAsync(downloadUrl, outputPath);
            return 0;
        }

        private static async Task<string> GetLatestZipDownloadAsync()
        {
            var apiUrl = "https://api.github.com/repos/biorand/classic-data/releases/latest";
            using var httpClient = new HttpClient();
            httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("request");

            var json = await httpClient.GetStringAsync(apiUrl);
            using var doc = JsonDocument.Parse(json);
            var assets = doc.RootElement.GetProperty("assets");
            foreach (var asset in assets.EnumerateArray())
            {
                var name = asset.GetProperty("name").GetString() ?? "";
                if (name.EndsWith(".zip"))
                {
                    var downloadUrl = asset.GetProperty("browser_download_url").GetString();
                    if (downloadUrl != null)
                        return downloadUrl;
                }
            }

            throw new Exception("Unable to find latest download for data.");
        }

        private static async Task DownloadAsync(string downloadUrl, string outputPath)
        {
            using var httpClient = new HttpClient();
            using var response = await httpClient.GetAsync(downloadUrl, HttpCompletionOption.ResponseHeadersRead);
            response.EnsureSuccessStatusCode();

            var totalBytes = response.Content.Headers.ContentLength ?? -1L;
            var canReportProgress = totalBytes != -1;

            using var contentStream = await response.Content.ReadAsStreamAsync();
            using var fileStream = new FileStream(outputPath, FileMode.Create, FileAccess.Write, FileShare.None);

            var buffer = new byte[32 * 1024];
            long totalRead = 0;
            int read;

            while ((read = await contentStream.ReadAsync(buffer, 0, buffer.Length)) > 0)
            {
                await fileStream.WriteAsync(buffer, 0, read);
                totalRead += read;

                var mib = totalRead / (1024 * 1024);
                if (canReportProgress)
                {
                    double progress = (double)totalRead / totalBytes * 100;
                    Console.Write($"\rDownloaded {mib:0.0} MiB ({progress:0.0}%)...");
                }
                else
                {
                    Console.Write($"\rDownloaded {mib:0.0} MiB...");
                }
            }
        }
    }
}
