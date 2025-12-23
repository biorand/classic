namespace IntelOrca.Biohazard.BioRand
{
    public class ReInstallConfig
    {
        public bool EnableCustomContent { get; set; }
        public bool RandomizeTitleVoice { get; set; } = true;
        public bool MaxInventorySize { get; set; }
        public bool DoorSkip { get; set; }
        public float BgmVolume { get; set; } = 1;
        public string InstallPath { get; set; } = "";
    }
}
