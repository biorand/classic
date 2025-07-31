using System;
using System.ComponentModel;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Reflection;
using System.Text.Json;
using System.Threading.Tasks;
using Spectre.Console;
using Spectre.Console.Cli;
using Spectre.Console.Extensions;

namespace IntelOrca.Biohazard.BioRand.Classic.Commands
{
    internal sealed class DataCommand : AsyncCommand<DataCommand.Settings>
    {
        public sealed class Settings : CommandSettings
        {
            [Description("Update data")]
            [CommandOption("-u|--update")]
            public required bool Update { get; init; }

            [CommandOption("-f|--force")]
            public required bool Force { get; init; }
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

            var currentVersion = GetDataVersion(outputPath);
            if (currentVersion == null)
                AnsiConsole.MarkupLine($"[yellow]No data detected[/]");
            else
                AnsiConsole.MarkupLine($"[yellow]Current version: {currentVersion}[/]");

            var releaseInfo = await AnsiConsole
                .Status()
                .Spinner(Spinner.Known.Dots2)
                .SpinnerStyle(Style.Parse("teal"))
                .StartAsync("[teal]Getting latest download URL[/]", ctx => GetLatestZipDownloadAsync());

            if (releaseInfo.Version > currentVersion || settings.Force)
            {
                AnsiConsole.MarkupLine($"[yellow]Latest version: {releaseInfo.Version}[/]");
                AnsiConsole.MarkupLine($"[teal]Downloading [link]{releaseInfo.DownloadUrl}[/]...[/]");
                await DownloadAsync(releaseInfo.DownloadUrl, outputPath);
            }
            else
            {
                AnsiConsole.MarkupLine($"[lime]:check_box_with_check:  Data already up to date. Use -f to override.[/]");
            }
            return 0;
        }

        private static async Task<GitHubReleaseInfo> GetLatestZipDownloadAsync()
        {
            var apiUrl = "https://api.github.com/repos/biorand/classic-data/releases/latest";
            using var httpClient = new HttpClient();
            httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("request");

            var json = await httpClient.GetStringAsync(apiUrl);
            using var doc = JsonDocument.Parse(json);
            var tagName = doc.RootElement.GetProperty("tag_name").GetString();
            if (!Version.TryParse(tagName?[1..], out var tagVersion))
                tagVersion = new Version(1, 0);

            var assets = doc.RootElement.GetProperty("assets");
            foreach (var asset in assets.EnumerateArray())
            {
                var name = asset.GetProperty("name").GetString() ?? "";
                if (name.EndsWith(".zip"))
                {
                    var downloadUrl = asset.GetProperty("browser_download_url").GetString();
                    if (downloadUrl != null)
                    {
                        return new GitHubReleaseInfo(tagVersion, downloadUrl);
                    }
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

            var fileName = Path.GetFileName(outputPath) ?? "";
            if (canReportProgress)
            {
                await AnsiConsole
                    .Progress()
                    .Columns([
                        new SpinnerColumn(Spinner.Known.Dots2).Style(Style.Parse("teal")),
                        new TaskDescriptionColumn(),
                        new ProgressBarColumn(),
                        new DownloadedColumn(),
                        new PercentageColumn(),
                        new TransferSpeedColumn(),
                        new RemainingTimeColumn(),
                    ])
                    .StartAsync(async ctx =>
                    {
                        var downloadTask = ctx.AddTask($"[teal]{fileName}[/]");
                        downloadTask.MaxValue = totalBytes;

                        while ((read = await contentStream.ReadAsync(buffer, 0, buffer.Length)) > 0)
                        {
                            await fileStream.WriteAsync(buffer, 0, read);
                            totalRead += read;

                            var mib = totalRead / (1024 * 1024);
                            double progress = (double)totalRead / totalBytes * 100;
                            downloadTask.Value(totalRead);
                        }
                    });
            }
            else
            {
                while ((read = await contentStream.ReadAsync(buffer, 0, buffer.Length)) > 0)
                {
                    await fileStream.WriteAsync(buffer, 0, read);
                    totalRead += read;

                    var mib = totalRead / (1024 * 1024);
                    Console.Write($"\rDownloaded {mib:0.0} MiB...");
                }
            }
        }

        private static Version GetDataVersion(string zipPath)
        {
            try
            {
                using var zipFile = ZipFile.OpenRead(zipPath);
                var versionEntry = zipFile.GetEntry("VERSION");
                if (versionEntry != null)
                {
                    using var stream = versionEntry.Open();
                    var textReader = new StreamReader(stream);
                    var versionText = textReader.ReadToEnd().Trim();
                    if (Version.TryParse(versionText, out var result))
                    {
                        return result;
                    }
                }
            }
            catch
            {
            }
            return new Version(1, 0);
        }

        private class GitHubReleaseInfo(Version version, string downloadUrl)
        {
            public Version Version { get; } = version;
            public string DownloadUrl { get; } = downloadUrl;
        }
    }
}
