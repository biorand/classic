using IntelOrca.Biohazard.BioRand.Cli;
using Xunit;

namespace BioRand.Cli.Tests;

public class CaseMergeTests
{
    [Fact]
    public void MergesCaseVariantDirectories()
    {
        if (OperatingSystem.IsWindows())
            return;

        var root = Path.Combine(Path.GetTempPath(), "biorand-cli-tests", Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(Path.Combine(root, "Pl0", "Rdt"));
            Directory.CreateDirectory(Path.Combine(root, "pl0", "pld"));
            Directory.CreateDirectory(Path.Combine(root, "pl0", "voice"));
            File.WriteAllText(Path.Combine(root, "Pl0", "Rdt", "room.rdt"), "rdt");
            File.WriteAllText(Path.Combine(root, "pl0", "pld", "pl00.pld"), "pld");
            File.WriteAllText(Path.Combine(root, "pl0", "voice", "v000.sap"), "sap");

            CaseMerge.Merge(root);

            Assert.True(Directory.Exists(Path.Combine(root, "Pl0", "Rdt")));
            Assert.True(Directory.Exists(Path.Combine(root, "Pl0", "pld")));
            Assert.True(Directory.Exists(Path.Combine(root, "Pl0", "voice")));
            Assert.False(Directory.Exists(Path.Combine(root, "pl0")));
            Assert.Equal("pld", File.ReadAllText(Path.Combine(root, "Pl0", "pld", "pl00.pld")));
        }
        finally
        {
            try { Directory.Delete(root, true); } catch { }
        }
    }

    [Fact]
    public void KeepsNewestDuplicateFile()
    {
        if (OperatingSystem.IsWindows())
            return;

        var root = Path.Combine(Path.GetTempPath(), "biorand-cli-tests", Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(root);
            var a = Path.Combine(root, "DATA");
            var b = Path.Combine(root, "data");
            Directory.CreateDirectory(a);
            Directory.CreateDirectory(b);
            File.WriteAllText(Path.Combine(a, "file.bin"), "old");
            File.WriteAllText(Path.Combine(b, "file.bin"), "new");
            File.SetLastWriteTimeUtc(Path.Combine(b, "file.bin"), DateTime.UtcNow.AddMinutes(1));

            CaseMerge.Merge(root);

            Assert.False(Directory.Exists(b));
            Assert.Equal("new", File.ReadAllText(Path.Combine(a, "file.bin")));
        }
        finally
        {
            try { Directory.Delete(root, true); } catch { }
        }
    }
}
