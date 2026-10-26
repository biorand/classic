using IntelOrca.Biohazard.BioRand;
using Xunit;

namespace BioRand.Cli.Tests;

public class DataManagerPathTests
{
    [Fact]
    public void ReadsAbsolutePathFromFileSystemArea()
    {
        var root = Path.Combine(Path.GetTempPath(), "biorand-cli-tests", Guid.NewGuid().ToString("N"));
        var path = Path.Combine(root, "Data", "PL00W02.PLW");
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, "plw");

            using var dataManager = new DataManager();
            dataManager.AddFileSystem(root);

            var data = dataManager.GetData(path);
            Assert.NotNull(data);
            Assert.Equal("plw", System.Text.Encoding.UTF8.GetString(data));
        }
        finally
        {
            try { Directory.Delete(root, true); } catch { }
        }
    }
}

public class FileRepositoryPathTests
{
    [Fact]
    public void ResolvesWrongCaseOnLinux()
    {
        var root = Path.Combine(Path.GetTempPath(), "biorand-cli-tests", Guid.NewGuid().ToString("N"));
        var path = Path.Combine(root, "pl0", "pld", "PL00.PLD");
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, "pld");

            using var repo = new IntelOrca.Biohazard.FileRepository(Path.Combine(root, "data"), root);
            var modPath = repo.GetModPath("pl0/pld/pl00.pld");
            Assert.True(File.Exists(modPath), modPath);
        }
        finally
        {
            try { Directory.Delete(root, true); } catch { }
        }
    }
}
