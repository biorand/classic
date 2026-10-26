using IntelOrca.Biohazard.BioRand.Events;
using Xunit;

namespace BioRand.Cli.Tests;

public class PlotRegistrationTests
{
    [Fact]
    public void AllConcretePlotsAreRegistered()
    {
        var registered = CutsceneRandomiser.CreateRegisteredPlots()
            .Select(x => x.GetType())
            .ToHashSet();
        var concrete = typeof(CutsceneRandomiser).Assembly
            .GetTypes()
            .Where(x => !x.IsAbstract && typeof(IPlot).IsAssignableFrom(x))
            .ToArray();

        Assert.NotEmpty(concrete);
        Assert.All(concrete, x => Assert.Contains(x, registered));
    }
}
