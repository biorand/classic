using System.Collections.Immutable;

namespace IntelOrca.Biohazard.BioRand
{
    internal interface IClassicRandomizerContext
    {
        RandomizerConfiguration Configuration { get; }
        DataManager DataManager { get; }
        DataManager GameDataManager { get; }
        Rng Rng { get; }
        ClassicRebirthModBuilder CrModBuilder { get; }
        ImmutableArray<IClassicRandomizerPlayerContext> GeneratedVariations { get; }
    }

    internal interface IClassicRandomizerPlayerContext
    {
        RandomizerConfiguration Configuration { get; }
        DataManager DataManager { get; }
        DataManager GameDataManager { get; }
        Rng Rng { get; }
        Variation Variation { get; }
        ModBuilder ModBuilder { get; }
    }
}
