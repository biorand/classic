using System;
using IntelOrca.Biohazard.BioRand.RE1;

namespace IntelOrca.Biohazard.BioRand
{
    public sealed class ClassicRandomizerFactory
    {
        public static ClassicRandomizerFactory Default { get; } = new ClassicRandomizerFactory();

        public IClassicRandomizer Create(BioVersion version, DataManager biorandData, DataManager gameData) =>
            new ClassicRandomizer(GetController(version), biorandData, gameData);

        private static IClassicRandomizerController GetController(BioVersion version) =>
            version switch
            {
                BioVersion.Biohazard1 => new Re1ClassicRandomizerController(),
                _ => throw new NotImplementedException()
            };

        public object CreateModBuilder(BioVersion version, DataManager biorandData, DataManager gameData) =>
            version switch
            {
                BioVersion.Biohazard1 => new Re1CrModBuilder(biorandData, gameData),
                _ => throw new NotImplementedException()
            };
    }
}
