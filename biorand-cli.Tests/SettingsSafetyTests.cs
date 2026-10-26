using IntelOrca.Biohazard.BioRand;
using IntelOrca.Biohazard.BioRand.Cli;
using Xunit;

namespace BioRand.Cli.Tests;

public class SettingsSafetyTests
{
    [Fact]
    public void CorruptSettingsAreNotOverwritten()
    {
        var root = Path.Combine(Path.GetTempPath(), "biorand-cli-tests", Guid.NewGuid().ToString("N"));
        var path = Path.Combine(root, "settings.json");
        try
        {
            Directory.CreateDirectory(root);
            const string original = "{ \"seed2\": \"R820-\", ";
            File.WriteAllText(path, original);

            using var error = new StringWriter();
            BiorandCli.LoadSettings(path, error, out var canSave);
            Assert.False(canSave);
            Assert.Contains("will not be updated", error.ToString());
            Assert.Equal(original, File.ReadAllText(path));
        }
        finally
        {
            try { Directory.Delete(root, true); } catch { }
        }
    }

    [Fact]
    public void UnknownSettingsFieldsArePreserved()
    {
        var root = Path.Combine(Path.GetTempPath(), "biorand-cli-tests", Guid.NewGuid().ToString("N"));
        var path = Path.Combine(root, "settings.json");
        try
        {
            Directory.CreateDirectory(root);
            File.WriteAllText(path, """
                {
                  "seed2": "R820-T1C0-XXHH4HZZZMEZ9H0007JZZZZZZZ5ZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZ",
                  "futureSetting": { "keep": true }
                }
                """);

            using var error = new StringWriter();
            var settings = BiorandCli.LoadSettings(path, error, out var canSave);
            Assert.True(canSave);
            var config = RandoConfig.FromString(settings.Seed2!);
            Assert.True(BiorandCli.TrySaveSettings(settings, path, CliGame.Re2, config, error));

            var saved = File.ReadAllText(path);
            Assert.Contains("futureSetting", saved);
            Assert.Contains("\"keep\": true", saved);
            Assert.Contains("\"generateCvPcMod\": false", saved);
            Assert.Contains("\"lastVersion\": null", saved);
        }
        finally
        {
            try { Directory.Delete(root, true); } catch { }
        }
    }
}
