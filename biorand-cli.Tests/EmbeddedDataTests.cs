using IntelOrca.Biohazard;
using IntelOrca.Biohazard.BioRand;
using IntelOrca.Biohazard.BioRand.Cli;
using Xunit;

namespace BioRand.Cli.Tests;

public class EmbeddedDataTests
{
    [Fact]
    public void CoreDataIsEmbedded()
    {
        DataManager.ConfigureEmbeddedData(typeof(CliProgram).Assembly, "core-data/");
        using var dataManager = new DataManager();

        var rdt = dataManager.GetText(BioVersion.Biohazard1, "rdt.json");
        Assert.NotNull(rdt);
        Assert.Contains("beginEndRooms", rdt);

        var bg = dataManager.GetData(BioVersion.BiohazardCv, "bg.png");
        Assert.NotNull(bg);
        Assert.True(bg.Length > 1000);

        var files = dataManager.GetFiles(BioVersion.Biohazard1, "");
        Assert.Contains("rdt.json", files);
    }

    [Fact]
    public void ConcurrentFirstReadIsSafe()
    {
        DataManager.ConfigureEmbeddedData(typeof(CliProgram).Assembly, "core-data/");
        using var dataManager = new DataManager();
        Parallel.For(0, 32, _ =>
        {
            var text = dataManager.GetText(BioVersion.Biohazard1, "rdt.json");
            Assert.NotNull(text);
        });
    }

    [Fact]
    public void EmbeddedModuleIsPresentWhenBuilt()
    {
        var modulePath = FindModulePath();
        if (modulePath == null || !File.Exists(modulePath))
            return;

        DataManager.ConfigureEmbeddedData(typeof(CliProgram).Assembly, "core-data/");
        using var dataManager = new DataManager();
        Assert.NotNull(dataManager.GetData("biorand.dll"));
    }

    private static string? FindModulePath()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null)
        {
            var candidate = Path.Combine(directory.FullName, "biorandmod", "bin", "biorand.dll");
            if (Directory.Exists(Path.Combine(directory.FullName, "biorandmod")))
                return candidate;
            directory = directory.Parent;
        }
        return null;
    }
}
