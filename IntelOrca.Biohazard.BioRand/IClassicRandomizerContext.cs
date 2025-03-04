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
        ImmutableArray<ModBuilder> Variations { get; }
    }

    internal interface IClassicRandomizerPlayerContext
    {
        RandomizerConfiguration Configuration { get; }
        DataManager DataManager { get; }
        DataManager GameDataManager { get; }
        Rng Rng { get; }
        public int PlayerIndex { get; }
        public string PlayerName { get; }
        Map Map { get; }
        ModBuilder ModBuilder { get; }
    }
}
