using System.Collections.Immutable;

namespace IntelOrca.Biohazard.BioRand
{
    internal interface IClassicRandomizerController
    {
        ImmutableArray<string> VariationNames { get; }

        void UpdateConfigDefinition(RandomizerConfigurationDefinition definition);
        Variation GetVariation(IClassicRandomizerContext context, string name);
        void ApplyConfigModifications(IClassicRandomizerContext context);
        void Write(IClassicRandomizerGeneratedVariation context, ClassicRebirthModBuilder crModBuilder);
    }

    public class Variation(int playerIndex, string playerName, Map map)
    {
        public int PlayerIndex { get; } = playerIndex;
        public string PlayerName { get; } = playerName;
        public Map Map { get; } = map;
    }
}
