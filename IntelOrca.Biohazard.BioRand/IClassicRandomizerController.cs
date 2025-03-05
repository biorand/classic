using System.Collections.Immutable;

namespace IntelOrca.Biohazard.BioRand
{
    internal interface IClassicRandomizerController
    {
        ImmutableArray<Variation> GetVariations(IClassicRandomizerContext context);
        void Write(IClassicRandomizerContext context);
    }

    public class Variation(int playerIndex, string playerName, Map map)
    {
        public int PlayerIndex { get; } = playerIndex;
        public string PlayerName { get; } = playerName;
        public Map Map { get; } = map;
    }
}
