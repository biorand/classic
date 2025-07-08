namespace IntelOrca.Biohazard.BioRand
{
    public interface IClassicRandomizer : IRandomizer
    {
        ClassicMod RandomizeToMod(RandomizerInput input);
    }
}
