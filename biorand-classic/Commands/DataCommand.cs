using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
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

            var outputDirectory = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) ?? "", "datapacks");

            var currentVersion = GetDataVersion(outputDirectory);
            if (currentVersion == null)
            {
                AnsiConsole.MarkupLine($"[yellow]No data detected[/]");
            }
            else
            {
                AnsiConsole.MarkupLine($"[yellow]Current version: {currentVersion}[/]");
            }

            var releaseInfo = await AnsiConsole
                .Status()
                .Spinner(Spinner.Known.Dots2)
                .SpinnerStyle(Style.Parse("teal"))
                .StartAsync("[teal]Getting latest download URL[/]", ctx => GetLatestZipDownloadAsync());

            if (currentVersion == null || releaseInfo.Version > currentVersion || settings.Force)
            {
                AnsiConsole.MarkupLine($"[yellow]Latest version: {releaseInfo.Version}[/]");
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
                        var tasks = new List<Task>();
                        foreach (var asset in releaseInfo.Assets)
                        {
                            var outputPath = Path.Combine(outputDirectory, asset.Name);
                            tasks.Add(DownloadSingleAsync(ctx, asset.DownloadUrl, outputPath));
                        }
                        await Task.WhenAll(tasks);
                    });

                var versionPath = Path.Combine(outputDirectory, "VERSION");
                File.WriteAllText(versionPath, releaseInfo.Version.ToString());
                AnsiConsole.MarkupLine($"[lime]:check_box_with_check:  Data now up to date.[/]");
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
            var tagName = doc.RootElement.GetProperty("tag_name").GetString() ?? "";
            var tagVersion = GitHubVersion.Parse(tagName) ?? new GitHubVersion(new Version(1, 0));

            var assetsBuilder = ImmutableArray.CreateBuilder<GitHubReleaseAsset>();
            var assets = doc.RootElement.GetProperty("assets");
            foreach (var asset in assets.EnumerateArray())
            {
                var name = asset.GetProperty("name").GetString() ?? "";
                if (name.EndsWith(".zip"))
                {
                    var downloadUrl = asset.GetProperty("browser_download_url").GetString();
                    if (downloadUrl != null)
                    {
                        assetsBuilder.Add(new GitHubReleaseAsset()
                        {
                            Name = name,
                            DownloadUrl = downloadUrl
                        });
                    }
                }
            }
            return new GitHubReleaseInfo
            {
                Version = tagVersion,
                Assets = assetsBuilder.ToImmutable()
            };
        }

        private static async Task DownloadSingleAsync(ProgressContext ctx, string downloadUrl, string outputPath)
        {
            var fileName = Path.GetFileName(outputPath) ?? "";
            var downloadTask = ctx.AddTask($"[teal]{fileName}[/]");
            try
            {
                var dir = Path.GetDirectoryName(outputPath);
                if (dir != null)
                {
                    Directory.CreateDirectory(dir);
                }

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
                if (canReportProgress)
                {
                    downloadTask.MaxValue = totalBytes;
                    while ((read = await contentStream.ReadAsync(buffer, 0, buffer.Length)) > 0)
                    {
                        await fileStream.WriteAsync(buffer, 0, read);
                        totalRead += read;
                        downloadTask.Value(totalRead);
                    }
                }
                else
                {
                    downloadTask.IsIndeterminate = true;
                    while ((read = await contentStream.ReadAsync(buffer, 0, buffer.Length)) > 0)
                    {
                        await fileStream.WriteAsync(buffer, 0, read);
                        totalRead += read;
                        downloadTask.Value = totalRead;
                        downloadTask.MaxValue = totalRead;
                    }
                }
            }
            catch
            {
                downloadTask.Description = $"[[FAILED]] {downloadTask.Description}";
                throw;
            }
            finally
            {
                downloadTask.StopTask();
            }
        }

        private static GitHubVersion? GetDataVersion(string outputDirectory)
        {
            try
            {
                var versionFile = Path.Combine(outputDirectory, "VERSION");
                if (File.Exists(versionFile))
                {
                    var versionText = File.ReadAllText(versionFile).Trim();
                    return GitHubVersion.Parse(versionText);
                }
            }
            catch
            {
            }
            return null;
        }

        [DebuggerDisplay("{Version}")]
        private class GitHubReleaseInfo
        {
            public required GitHubVersion Version { get; init; }
            public ImmutableArray<GitHubReleaseAsset> Assets { get; init; } = [];
        }

        [DebuggerDisplay("{Name}")]
        private sealed class GitHubReleaseAsset
        {
            public required string Name { get; init; }
            public required string DownloadUrl { get; init; }
        }

        private readonly struct GitHubVersion : IComparable<GitHubVersion>, IEquatable<GitHubVersion>
        {
            public Version Version { get; }
            public string? Postfix { get; }

            public GitHubVersion(Version version, string? postfix = null)
            {
                Version = version;
                Postfix = postfix;
            }

            public static GitHubVersion? Parse(string text)
            {
                if (string.IsNullOrWhiteSpace(text))
                    return null;

                if (text.StartsWith("v", StringComparison.OrdinalIgnoreCase))
                    text = text[1..];

                var dashIndex = text.IndexOf('-');
                string versionPart;
                string? postfixPart = null;

                if (dashIndex >= 0)
                {
                    versionPart = text[..dashIndex];
                    postfixPart = text[(dashIndex + 1)..];
                }
                else
                {
                    versionPart = text;
                }

                return Version.TryParse(versionPart, out var version)
                    ? new GitHubVersion(version, postfixPart)
                    : null;
            }

            public int CompareTo(GitHubVersion other)
            {
                int versionComparison = Version.CompareTo(other.Version);
                if (versionComparison != 0)
                    return versionComparison;

                // Null < non-null
                if (Postfix == null && other.Postfix != null)
                    return -1;
                if (Postfix != null && other.Postfix == null)
                    return 1;

                return string.Compare(Postfix, other.Postfix, StringComparison.OrdinalIgnoreCase);
            }

            public bool Equals(GitHubVersion other) => Version.Equals(other.Version) && string.Equals(Postfix, other.Postfix, StringComparison.OrdinalIgnoreCase);
            public override bool Equals(object? obj) => obj is GitHubVersion other && Equals(other);
            public override int GetHashCode() => HashCode.Combine(Version, Postfix?.ToLowerInvariant());
            public override string ToString() => Postfix == null ? Version.ToString() : $"{Version}-{Postfix}";

            public static bool operator ==(GitHubVersion left, GitHubVersion right) => left.Equals(right);
            public static bool operator !=(GitHubVersion left, GitHubVersion right) => !left.Equals(right);
            public static bool operator <(GitHubVersion left, GitHubVersion right) => left.CompareTo(right) < 0;
            public static bool operator >(GitHubVersion left, GitHubVersion right) => left.CompareTo(right) > 0;
            public static bool operator <=(GitHubVersion left, GitHubVersion right) => left.CompareTo(right) <= 0;
            public static bool operator >=(GitHubVersion left, GitHubVersion right) => left.CompareTo(right) >= 0;
        }

    }
}
