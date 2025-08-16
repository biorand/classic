namespace IntelOrca.Biohazard.BioRand
{
    internal interface INpcHelper
    {
        string GetNpcName(byte type);
        string[] GetPlayerActors(int player);
        byte[] GetDefaultIncludeTypes(RandomizedRdt rdt);
        bool IsNpc(byte type);
        string? GetActor(byte type);
        byte[] GetSlots(RandoConfig config, byte id);
        bool IsSpareSlot(byte id);
        void CreateEmdFile(byte type, string pldPath, string baseEmdPath, string targetEmdPath, DataManager dataManager, FileRepository fileRepository, Rng rng);
    }
}
