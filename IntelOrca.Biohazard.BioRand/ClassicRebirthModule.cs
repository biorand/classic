namespace IntelOrca.Biohazard.BioRand
{
    internal readonly struct ClassicRebirthModule(string fileName, byte[] data)
    {
        public string FileName => fileName;
        public byte[] Data => data;
    }
}
