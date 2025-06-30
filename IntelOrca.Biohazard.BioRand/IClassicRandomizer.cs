namespace IntelOrca.Biohazard.BioRand
{
    public interface IClassicRandomizer : IRandomizer
    {
        ModBuilder RandomizeToMod(RandomizerInput input);
    }
}
