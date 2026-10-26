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

        /// <summary>
        /// Exact output mod folder. When null, the legacy game-folder/mod_biorand
        /// location is used.
        /// </summary>
        public string? ModPath { get; set; }

        /// <summary>
        /// CVX scratch folder for extracted room files and logs. When null,
        /// the mod folder is used.
        /// </summary>
        public string? TempPath { get; set; }

        /// <summary>
        /// Exact output path for the rebuilt PS2 ISO. When null, the legacy
        /// location next to the input ISO is used.
        /// </summary>
        public string? CvPs2OutputPath { get; set; }
    }
}
