using System;
using IntelOrca.Biohazard.BioRand.RE1;

namespace IntelOrca.Biohazard.BioRand
{
    public sealed class ClassicRandomizerFactory
    {
        public static ClassicRandomizerFactory Default { get; } = new ClassicRandomizerFactory();

        public IRandomizer Create(BioVersion version) => new ClassicRandomizer(GetController(version));

        private static IClassicRandomizerController GetController(BioVersion version) =>
            version switch
            {
                BioVersion.Biohazard1 => new Re1ClassicRandomizerController(),
                _ => throw new NotImplementedException()
            };
    }
}
