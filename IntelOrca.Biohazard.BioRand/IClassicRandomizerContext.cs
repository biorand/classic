namespace IntelOrca.Biohazard.BioRand
{
    internal interface IClassicRandomizerContext
    {
        RandomizerConfiguration Configuration { get; }
        DataManager DataManager { get; }
        Map Map { get; }
        Rng Rng { get; }
        ModBuilder ModBuilder { get; }
        ClassicRebirthModBuilder CrModBuilder { get; }
    }
}
