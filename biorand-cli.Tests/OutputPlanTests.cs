using IntelOrca.Biohazard.BioRand.Cli;
using Xunit;

namespace BioRand.Cli.Tests;

public class OutputPlanTests
{
    [Fact]
    public void ModFolderRequiresForceWhenNotEmpty()
    {
        var root = NewTempRoot();
        var output = Path.Combine(root, "mod_biorand");
        try
        {
            MakeBioRandOutput(output);
            File.WriteAllText(Path.Combine(output, "old.txt"), "old");

            var plan = OutputPlan.Create(output, OutputMode.ModFolder, force: false);
            Assert.Throws<CliUsageException>(() => plan.Prepare());
            Assert.True(File.Exists(Path.Combine(output, "old.txt")));
        }
        finally
        {
            Delete(root);
        }
    }

    [Fact]
    public void ModFolderRefusesToDeleteNonBioRandOutput()
    {
        var root = NewTempRoot();
        var output = Path.Combine(root, "important");
        try
        {
            Directory.CreateDirectory(output);
            File.WriteAllText(Path.Combine(output, "keep.txt"), "keep");

            var plan = OutputPlan.Create(output, OutputMode.ModFolder, force: true);
            Assert.Throws<CliUsageException>(() => plan.Prepare());
            Assert.True(File.Exists(Path.Combine(output, "keep.txt")));
        }
        finally
        {
            Delete(root);
        }
    }

    [Fact]
    public void ModFolderReplacesBioRandOutput()
    {
        var root = NewTempRoot();
        var output = Path.Combine(root, "mod_biorand");
        try
        {
            MakeBioRandOutput(output);
            File.WriteAllText(Path.Combine(output, "old.txt"), "old");

            var plan = OutputPlan.Create(output, OutputMode.ModFolder, force: true);
            plan.Prepare();
            File.WriteAllText(Path.Combine(plan.WorkingPath, "new.txt"), "new");
            plan.Commit();

            Assert.False(File.Exists(Path.Combine(output, "old.txt")));
            Assert.Equal("new", File.ReadAllText(Path.Combine(output, "new.txt")));
        }
        finally
        {
            Delete(root);
        }
    }

    [Fact]
    public void ModFolderAcceptsCvPcOutputWithoutManifest()
    {
        var root = NewTempRoot();
        var output = Path.Combine(root, "biorand");
        try
        {
            Directory.CreateDirectory(Path.Combine(output, "SYSTEM.AFS"));
            File.WriteAllText(Path.Combine(output, "RM_9090.RDX"), "room");

            var plan = OutputPlan.Create(output, OutputMode.ModFolder, force: true);
            plan.Prepare();
            Assert.True(Directory.Exists(plan.WorkingPath));
        }
        finally
        {
            Delete(root);
        }
    }

    [Fact]
    public void CommitRestoresOldOutputWhenMoveFails()
    {
        var root = NewTempRoot();
        var output = Path.Combine(root, "mod_biorand");
        try
        {
            MakeBioRandOutput(output);
            File.WriteAllText(Path.Combine(output, "old.txt"), "old");

            var plan = OutputPlan.Create(output, OutputMode.ModFolder, force: true);
            plan.Prepare();
            Directory.Delete(plan.WorkingPath, true);

            Assert.ThrowsAny<IOException>(() => plan.Commit());
            Assert.True(File.Exists(Path.Combine(output, "old.txt")));
        }
        finally
        {
            Delete(root);
        }
    }

    [Fact]
    public void Ps2ForceOnlyReplacesTheIso()
    {
        var root = NewTempRoot();
        var output = Path.Combine(root, "ps2");
        var iso = Path.Combine(output, "recvx_biorand.iso");
        try
        {
            Directory.CreateDirectory(output);
            File.WriteAllText(Path.Combine(output, "keep.txt"), "keep");
            File.WriteAllText(iso, "old");

            var plan = OutputPlan.Create(output, OutputMode.CvPs2Iso, force: false);
            Assert.Throws<CliUsageException>(() => plan.Prepare());

            var forced = OutputPlan.Create(output, OutputMode.CvPs2Iso, force: true);
            forced.Prepare();
            File.WriteAllText(forced.WorkingPath, "new-iso");
            forced.Commit();

            Assert.Equal("new-iso", File.ReadAllText(iso));
            Assert.Equal("keep", File.ReadAllText(Path.Combine(output, "keep.txt")));
        }
        finally
        {
            Delete(root);
        }
    }

    [Fact]
    public void Ps2RejectsDirectoryAtIsoPath()
    {
        var root = NewTempRoot();
        var output = Path.Combine(root, "ps2");
        try
        {
            Directory.CreateDirectory(Path.Combine(output, "recvx_biorand.iso"));
            var plan = OutputPlan.Create(output, OutputMode.CvPs2Iso, force: true);
            Assert.Throws<CliUsageException>(() => plan.Prepare());
        }
        finally
        {
            Delete(root);
        }
    }

    private static void MakeBioRandOutput(string path)
    {
        Directory.CreateDirectory(path);
        File.WriteAllText(
            Path.Combine(path, "manifest.txt"),
            "[MOD]\nName = BioRand: A Resident Evil Randomizer\nModule = biorand.dll\n");
    }

    private static string NewTempRoot()
    {
        var path = Path.Combine(Path.GetTempPath(), "biorand-cli-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }

    private static void Delete(string path)
    {
        try { Directory.Delete(path, true); } catch { }
    }
}
