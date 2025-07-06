namespace IntelOrca.Biohazard.BioRand
{
    internal interface IClassicRandomizerContext
    {
        RandomizerConfiguration Configuration { get; }
        DataManager DataManager { get; }
        Rng Rng { get; }
    }

    internal interface IClassicRandomizerGeneratedVariation : IClassicRandomizerContext
    {
        Variation Variation { get; }
        ModBuilder ModBuilder { get; }
    }
}
