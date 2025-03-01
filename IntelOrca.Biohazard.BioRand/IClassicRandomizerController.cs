namespace IntelOrca.Biohazard.BioRand
{
    internal interface IClassicRandomizerController
    {
        public GameData GetGameData(IClassicRandomizerContext context, int player);
        void WritePatches(IClassicRandomizerContext context, PatchWriter pw);
        void WriteExtra(IClassicRandomizerContext context);
    }
}
