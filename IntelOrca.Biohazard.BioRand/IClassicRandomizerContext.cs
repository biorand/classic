namespace IntelOrca.Biohazard.BioRand
{
    internal interface IClassicRandomizerContext
    {
        public RandomizerConfiguration Configuration { get; }
        public DataManager DataManager { get; }
        public Map Map { get; }
        public Rng Rng { get; }
    }
}
