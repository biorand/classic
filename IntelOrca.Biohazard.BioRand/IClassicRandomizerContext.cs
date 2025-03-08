namespace IntelOrca.Biohazard.BioRand
{
    internal interface IClassicRandomizerContext
    {
        RandomizerConfiguration Configuration { get; }
        DataManager DataManager { get; }
        DataManager GameDataManager { get; }
        Rng Rng { get; }
    }

    internal interface IClassicRandomizerGeneratedVariation
    {
        RandomizerConfiguration Configuration { get; }
        DataManager DataManager { get; }
        DataManager GameDataManager { get; }
        Rng Rng { get; }
        Variation Variation { get; }
        ModBuilder ModBuilder { get; }
    }
}
