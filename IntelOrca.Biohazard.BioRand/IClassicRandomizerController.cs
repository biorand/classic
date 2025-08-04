namespace IntelOrca.Biohazard.BioRand
{
    internal interface IClassicRandomizerController
    {
        void UpdateConfigDefinition(RandomizerConfigurationDefinition definition);
        Variation GetVariation(IClassicRandomizerContext context);
        void ApplyConfigModifications(IClassicRandomizerContext context, ModBuilder modBuilder);
    }

    public class Variation(int playerIndex, Map map)
    {
        public int PlayerIndex { get; } = playerIndex;
        public Map Map { get; } = map;
    }
}
