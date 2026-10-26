using IntelOrca.Biohazard;
using IntelOrca.Biohazard.BioRand;
using IntelOrca.Biohazard.BioRand.Cli;
using Xunit;

namespace BioRand.Cli.Tests;

public class CliOptionsTests
{
    [Fact]
    public void ParsesAllOptions()
    {
        var options = CliOptions.Parse(
        [
            "-s", "12345",
            "-i", "/games/re2",
            "-o", "/out/biorand",
            "-f"
        ]);

        Assert.Equal("12345", options.Seed);
        Assert.Equal("/games/re2", options.Input);
        Assert.Equal("/out/biorand", options.Output);
        Assert.True(options.Force);
    }

    [Fact]
    public void ParsesLongEqualsSyntax()
    {
        var options = CliOptions.Parse(
        [
            "--seed=12345",
            "--input=/games/re3",
            "--output=/out/mod_biorand"
        ]);

        Assert.Equal("12345", options.Seed);
        Assert.Equal("/games/re3", options.Input);
        Assert.Equal("/out/mod_biorand", options.Output);
    }

    [Fact]
    public void RequiresOutput()
    {
        Assert.Throws<CliUsageException>(() => CliOptions.Parse(["-s", "12345"]));
    }

    [Fact]
    public void RejectsUnknownOption()
    {
        Assert.Throws<CliUsageException>(() => CliOptions.Parse(["--wat"]));
    }

    [Fact]
    public void RejectsRemovedGameOption()
    {
        Assert.Throws<CliUsageException>(() => CliOptions.Parse(["-o", "/out", "-g", "re2"]));
    }

    [Fact]
    public void RejectsRemovedConfigOption()
    {
        Assert.Throws<CliUsageException>(() => CliOptions.Parse(["-o", "/out", "-c", "settings.json"]));
    }
}

public class SeedResolverTests
{
    [Fact]
    public void NumericSeedUsesSelectedGame()
    {
        var settings = new RandoAppSettings { LastSelectedGame = 1 };
        var (config, game) = SeedResolver.Resolve("12345", settings);

        Assert.Equal(CliGame.Re2, game);
        Assert.Equal(2, config.Game);
        Assert.Equal(12345, config.Seed);
        Assert.Equal(RandoConfig.LatestVersion, config.Version);
    }

    [Fact]
    public void NumericSeedKeepsSavedConfiguration()
    {
        var saved = new RandoConfig { Game = 2, Seed = 111, RandomDoors = true, AreaCount = 2 };
        var settings = new RandoAppSettings { LastSelectedGame = 1, Seed2 = saved.ToString() };

        var (config, _) = SeedResolver.Resolve("12345", settings);

        Assert.Equal(12345, config.Seed);
        Assert.True(config.RandomDoors);
        Assert.Equal(2, config.AreaCount);
    }

    [Fact]
    public void SavedSeedKeepsConfigurationAndRandomizesSeed()
    {
        var original = new RandoConfig
        {
            Version = RandoConfig.LatestVersion,
            Game = 4,
            Seed = 111,
            RandomDoors = true,
            AreaCount = 2,
            AreaSize = 5,
            ChangePlayer = false
        };
        var settings = new RandoAppSettings { LastSelectedGame = 3, SeedCv = original.ToString() };

        var (config, game) = SeedResolver.Resolve(null, settings);

        Assert.Equal(CliGame.Cv, game);
        Assert.Equal(4, config.Game);
        Assert.True(config.RandomDoors);
        Assert.Equal(2, config.AreaCount);
        Assert.Equal(5, config.AreaSize);
        Assert.False(config.ChangePlayer);
        Assert.InRange(config.Seed, 0, RandoConfig.MaxSeed);
    }

    [Fact]
    public void SeedStringSelectsTheGame()
    {
        var seed = new RandoConfig { Game = 1, Seed = 42 }.ToString();
        var (config, game) = SeedResolver.Resolve(seed, new RandoAppSettings { LastSelectedGame = 1 });
        Assert.Equal(CliGame.Re1, game);
        Assert.Equal(1, config.Game);
        Assert.Equal(42, config.Seed);
    }

    [Fact]
    public void OlderSeedVersionIsRejected()
    {
        var oldSeed = new RandoConfig { Version = 7, Game = 4, Seed = 42 }.ToString();
        Assert.Throws<CliUsageException>(() => SeedResolver.Resolve(oldSeed, new RandoAppSettings()));
    }

    [Fact]
    public void NoSeedAndNoLastGameIsRejected()
    {
        Assert.Throws<CliUsageException>(() => SeedResolver.Resolve(null, new RandoAppSettings()));
    }

    [Fact]
    public void NumericSeedWithoutLastGameIsRejected()
    {
        Assert.Throws<CliUsageException>(() => SeedResolver.Resolve("12345", new RandoAppSettings()));
    }
}

public class RandoAppSettingsTests
{
    [Fact]
    public void RoundTripsThroughSourceGeneratedJson()
    {
        var root = Path.Combine(Path.GetTempPath(), "biorand-cli-tests", Guid.NewGuid().ToString("N"));
        var path = Path.Combine(root, "settings.json");
        try
        {
            var settings = new RandoAppSettings
            {
                SeedCv = new RandoConfig { Game = 4, Seed = 99 }.ToString(),
                LastSelectedGame = 3,
                GamePathCv = "/games/recvx.iso",
                GenerateCvPcMod = true,
                BgmVolume2 = 0.5f,
                RandomizeTitleVoice = false,
                DoorSkip = true
            };
            settings.Save(path);
            settings.Save(path);

            var loaded = RandoAppSettings.Load(path);
            Assert.Equal(settings.SeedCv, loaded.SeedCv);
            Assert.Equal(3, loaded.LastSelectedGame);
            Assert.Equal("/games/recvx.iso", loaded.GamePathCv);
            Assert.True(loaded.GenerateCvPcMod);
            Assert.Equal(0.5f, loaded.BgmVolume2);
            Assert.False(loaded.RandomizeTitleVoice);
            Assert.True(loaded.DoorSkip);
            Assert.Single(Directory.GetFiles(root));
        }
        finally
        {
            try { Directory.Delete(root, true); } catch { }
        }
    }

    [Fact]
    public void MissingCvModeDefaultsToPs2LikeTheGui()
    {
        Assert.False(new RandoAppSettings().GenerateCvPcMod);
    }
}

public class PathUtilTests
{
    [Fact]
    public void DetectsAncestors()
    {
        var root = Path.Combine(Path.GetTempPath(), "biorand-path-test");
        Assert.True(PathUtil.IsAncestor(root, Path.Combine(root, "mod_biorand")));
        Assert.True(PathUtil.IsAncestor(root, root));
        Assert.False(PathUtil.IsAncestor(root, root + "-other"));
    }

    [Fact]
    public void RefusesRootAndHome()
    {
        Assert.True(PathUtil.IsRootOrHome(Path.GetPathRoot(Path.GetTempPath())!));
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        Assert.True(PathUtil.IsRootOrHome(home));
        var homeParent = Path.GetDirectoryName(home);
        if (!string.IsNullOrEmpty(homeParent))
            Assert.True(PathUtil.IsRootOrHome(homeParent));
        Assert.False(PathUtil.IsRootOrHome(Path.Combine(Path.GetTempPath(), "biorand-output")));
    }

    [Fact]
    public void ExpandsHome()
    {
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        Assert.Equal(Path.Combine(home, "test"), PathUtil.Expand("~/test"));
    }
}
