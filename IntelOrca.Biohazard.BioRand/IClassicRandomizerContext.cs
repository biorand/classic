namespace IntelOrca.Biohazard.BioRand
{
    internal interface IClassicRandomizerContext
    {
        public RandomizerConfiguration Configuration { get; }
        public DataManager DataManager { get; }
    }
}
