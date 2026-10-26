using IntelOrca.Biohazard.BioRand;
using IntelOrca.Biohazard.BioRand.Cli;
using Xunit;

namespace BioRand.Cli.Tests;

public class CvMetadataTests
{
    [Fact]
    public void SecondRunReplacesStaleMetadata()
    {
        var root = Path.Combine(Path.GetTempPath(), "biorand-cli-tests", Guid.NewGuid().ToString("N"));
        var output = Path.Combine(root, "ps2");
        var scratch = Path.Combine(root, "scratch");
        try
        {
            Directory.CreateDirectory(output);
            Directory.CreateDirectory(scratch);
            File.WriteAllText(Path.Combine(output, "log_pl0.txt"), "old log");
            File.WriteAllText(Path.Combine(output, "graph_pl0.dgml"), "old graph");
            File.WriteAllText(Path.Combine(scratch, "log_pl0.txt"), "new log");
            File.WriteAllText(Path.Combine(scratch, "graph_pl0.dgml"), "new graph");

            var plan = OutputPlan.Create(output, OutputMode.CvPs2Iso, force: false);
            BiorandCli.WriteCvMetadata(OutputMode.CvPs2Iso, plan, scratch);

            Assert.Equal("new log", File.ReadAllText(Path.Combine(output, "log_pl0.txt")));
            Assert.Equal("new graph", File.ReadAllText(Path.Combine(output, "graph_pl0.dgml")));
            Assert.False(File.Exists(Path.Combine(output, "seed.txt")));
        }
        finally
        {
            try { Directory.Delete(root, true); } catch { }
        }
    }

    [Fact]
    public void MissingGraphDeletesStaleGraph()
    {
        var root = Path.Combine(Path.GetTempPath(), "biorand-cli-tests", Guid.NewGuid().ToString("N"));
        var output = Path.Combine(root, "ps2");
        var scratch = Path.Combine(root, "scratch");
        try
        {
            Directory.CreateDirectory(output);
            Directory.CreateDirectory(scratch);
            File.WriteAllText(Path.Combine(output, "graph_pl0.dgml"), "old graph");
            File.WriteAllText(Path.Combine(scratch, "log_pl0.txt"), "new log");

            var plan = OutputPlan.Create(output, OutputMode.CvPs2Iso, force: false);
            BiorandCli.WriteCvMetadata(OutputMode.CvPs2Iso, plan, scratch);

            Assert.False(File.Exists(Path.Combine(output, "graph_pl0.dgml")));
        }
        finally
        {
            try { Directory.Delete(root, true); } catch { }
        }
    }
}
