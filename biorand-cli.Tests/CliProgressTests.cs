using IntelOrca.Biohazard.BioRand.Cli;
using Xunit;

namespace BioRand.Cli.Tests;

public class CliProgressTests
{
    [Fact]
    public void UsesPrefixedOutputAndProgressPercentages()
    {
        using var writer = new StringWriter();
        var progress = new CliProgress(writer, isCvx: false);

        progress.Start();
        progress.BeginTask(null, "Randomizing doors");
        progress.BeginTask(0, "Warning: optional data was not found");
        progress.Complete();

        var lines = writer.ToString()
            .Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal("Progress: 0% Starting", lines[0]);
        Assert.Equal("Progress: 20% Randomizing doors", lines[1]);
        Assert.Equal("Warning: optional data was not found", lines[2]);
        Assert.Equal("Progress: 100% Done", lines[3]);
        Assert.All(lines, line =>
            Assert.True(
                line.StartsWith("Progress: ", StringComparison.Ordinal) ||
                line.StartsWith("Warning: ", StringComparison.Ordinal) ||
                line.StartsWith("Info: ", StringComparison.Ordinal),
                line));
    }
}
