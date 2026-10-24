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

        /// <summary>
        /// RE:CVX only. Generate a mods folder for the PC version instead
        /// of rebuilding the PS2 ISO.
        /// </summary>
        public bool GenerateCvPcMod { get; set; }

        /// <summary>
        /// The PC version folder containing recvx.exe and its mods folder.
        /// </summary>
        public string CvPcPortPath { get; set; } = "";
    }
}
