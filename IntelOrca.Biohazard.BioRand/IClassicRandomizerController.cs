using System.Collections.Immutable;

namespace IntelOrca.Biohazard.BioRand
{
    internal interface IClassicRandomizerController
    {
        ImmutableArray<string> Players { get; }

        void Write(IClassicRandomizerContext context);
    }
}
