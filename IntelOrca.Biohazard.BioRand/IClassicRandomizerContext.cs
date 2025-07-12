namespace IntelOrca.Biohazard.BioRand
{
    internal interface IClassicRandomizerContext
    {
        RandomizerConfiguration Configuration { get; }
        DataManager DataManager { get; }

        Rng GetRng(string hash);
    }

    internal interface IClassicRandomizerGeneratedVariation : IClassicRandomizerContext
    {
        Variation Variation { get; }
        ModBuilder ModBuilder { get; }
    }
}
