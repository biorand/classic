namespace IntelOrca.Biohazard.BioRand
{
    internal interface IClassicRandomizerContext
    {
        RandomizerConfigurationDefinition ConfigurationDefinition { get; }
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
