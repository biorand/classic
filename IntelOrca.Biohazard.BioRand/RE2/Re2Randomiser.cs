using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using IntelOrca.Biohazard.Extensions;
using IntelOrca.Biohazard.Model;
using IntelOrca.Biohazard.Room;
using IntelOrca.Biohazard.Script.Opcodes;

namespace IntelOrca.Biohazard.BioRand.RE2
{
    public class Re2Randomiser : BaseRandomiser
    {
        private const uint AddressInventoryLeon = 0x400000 + 0x001401B8;
        private const uint AddressInventoryClaire = 0x400000 + 0x001401D9;

        private readonly Re2EnemyHelper _enemyHelper = new Re2EnemyHelper();

        protected override BioVersion BiohazardVersion => BioVersion.Biohazard2;
        internal override IDoorHelper DoorHelper { get; } = new Re2DoorHelper();
        internal override IItemHelper ItemHelper { get; } = new Re2ItemHelper();
        internal override IEnemyHelper EnemyHelper => _enemyHelper;
        internal override INpcHelper NpcHelper { get; } = new Re2NpcHelper();

        public Re2Randomiser(ReInstallConfig installConfig, IBgCreator? bgCreator)
            : base(installConfig, bgCreator)
        {
        }

        public override string GetPlayerName(int player) => player == 0 ? "Leon" : "Claire";

        public override bool ValidateGamePath(string path)
        {
            return Directory.Exists(Path.Combine(path, "data", "pl0", "rdt")) ||
                Directory.Exists(Path.Combine(path, "pl0", "rdt"));
        }

        protected override string GetDataPath(string installPath)
        {
            if (Directory.Exists(Path.Combine(installPath, "data", "pl0", "rdt")))
            {
                return Path.Combine(installPath, "data");
            }
            return installPath;
        }

        protected override RdtId[] GetRdtIds(string dataPath)
        {
            var rdtPaths = new HashSet<RdtId>();
            for (int player = 0; player < 2; player++)
            {
                var files = Directory.GetFiles(Path.Combine(dataPath, @$"Pl{player}\Rdt"));
                foreach (var file in files)
                {
                    // Check the file is an RDT file
                    var fileName = Path.GetFileName(file);
                    if (!fileName.StartsWith("ROOM", System.StringComparison.OrdinalIgnoreCase) ||
                        !fileName.EndsWith(".RDT", System.StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    // Ignore RDTs that are not part of the main game
                    if (!char.IsDigit(fileName[4]))
                        continue;

                    if (RdtId.TryParse(fileName.Substring(4, 3), out var rdtId))
                    {
                        rdtPaths.Add(rdtId);
                    }
                }
            }
            return rdtPaths
                .Except(new[] {
                    new RdtId(6, 0x05),
                    new RdtId(6, 0x07)
                })
                .OrderBy(x => x.Stage)
                .ThenBy(x => x.Room)
                .ToArray();
        }

        protected override string GetRdtPath(string dataPath, RdtId rdtId, int player, bool mod)
        {
            var path = Path.Combine(dataPath, @$"Pl{player}\Rdt\ROOM{rdtId}{player}.RDT");
            return path;
        }

        public override void Generate(RandoConfig config, IRandoProgress progress, FileRepository fileRepository)
        {
            var reConfig = InstallConfig;
            if (config.RandomBgm && MusicAlbumSelected(config, "RE1"))
            {
                if (!reConfig.IsEnabled(BioVersion.Biohazard1))
                {
                    throw new BioRandUserException("RE1 installation must be enabled to use RE1 assets.");
                }
            }
            if (!reConfig.IsEnabled(BioVersion.Biohazard2))
            {
                throw new BioRandUserException("RE2 installation must be enabled to randomize RE2.");
            }
            if (config.RandomBgm && MusicAlbumSelected(config, "RE3"))
            {
                if (!reConfig.IsEnabled(BioVersion.Biohazard3))
                {
                    throw new BioRandUserException("RE3 installation must be enabled to use RE3 assets.");
                }
            }

            var po = new ParallelOptions();
#if DEBUG
            po.MaxDegreeOfParallelism = 1;
#endif
            if (config.GameVariant == 0)
            {
                // Leon A / Claire B
                Parallel.Invoke(po,
                    () => GenerateRdts(config.WithPlayerScenario(0, 0), progress, fileRepository),
                    () => GenerateRdts(config.WithPlayerScenario(1, 1), progress, fileRepository));
            }
            else
            {
                // Leon B / Claire A
                Parallel.Invoke(po,
                    () => GenerateRdts(config.WithPlayerScenario(0, 1), progress, fileRepository),
                    () => GenerateRdts(config.WithPlayerScenario(1, 0), progress, fileRepository));
            }

            DisableDemo();
            FixClaireWeapons();
            FixWeaponHitScan(config);
            DisableWaitForSherry();
            FixMothCrash();
            AddCreditsRoom(fileRepository, new Rng(config.Seed));

            // tmoji.bin
            var src = DataManager.GetPath(BiohazardVersion, "tmoji.bin");
            var dst = fileRepository.GetModPath("common/data/tmoji.bin");
            Directory.CreateDirectory(Path.GetDirectoryName(dst));
            DataManager.Export(src, dst);

            base.Generate(config, progress, fileRepository);

            ScaleEnemyAttacks(config, fileRepository);
        }

        protected override string[] TitleCardSoundFiles { get; } =
            new[] {
                "Common/Sound/core/core16.sap",
                "Common/Sound/core/core17.sap"
            };

        internal override string[] ChangePlayerCharacters(RandoConfig config, RandoLogger logger, GameData gameData, FileRepository fileRepository)
        {
            var actor = config.Player == 0 ? "leon" : "claire";
            var partner = config.Player == 0 ? "ada" : "sherry";

            var pldIndex = config.Player == 0 ? 0 : 1;
            var hurtSoundIndex = config.Player == 0 ? 0 : 1;
            var deathSoundIndex = config.Player == 0 ? 32 : 33;
            if (config.ChangePlayer)
            {
                var pldPath = GetSelectedPldPath(config, config.Player);
                actor = Path.GetFileName(pldPath);
            }

            ReplacePlayer(config, logger, fileRepository, pldIndex, hurtSoundIndex, deathSoundIndex, actor);
            ScalePlayerEMRs(config, logger, gameData, fileRepository);

            // Change partner
            var rng = new Rng(config.Seed + config.Player);
            var enabledPLDs = GetEnabledPartners(config);
            if (enabledPLDs.Length != 0)
            {
                partner = rng.NextOf(enabledPLDs);
                pldIndex = config.Player == 0 ? 0x0E : 0x0F;
                hurtSoundIndex = config.Player == 0 ? 14 : 15;
                deathSoundIndex = config.Player == 0 ? 37 : 38;
                ReplacePlayer(config, logger, fileRepository, pldIndex, hurtSoundIndex, deathSoundIndex, partner);
            }
            return new[] { actor, partner };
        }

        private void ScalePlayerEMRs(RandoConfig config, RandoLogger logger, GameData gameData, FileRepository fileRepository)
        {
            if (!config.ChangePlayer)
                return;

            var player = config.Player;
            var pldFileName = $"pl{player}/pld/pl{player:00}.pld";
            var originalPath = fileRepository.GetDataPath(pldFileName);
            var newPath = fileRepository.GetModPath(pldFileName);
            var originalFile = new PldFile(BiohazardVersion, originalPath);
            var newFile = new PldFile(BiohazardVersion, newPath);
            var scale = newFile.CalculateEmrScale(originalFile);
            if (scale == 1)
                return;

            foreach (var rdt in gameData.Rdts)
            {
                if (rdt.RdtId == new RdtId(2, 0x06) ||
                    rdt.RdtId == new RdtId(2, 0x07))
                {
                    continue;
                }
                ScaleEmrY(logger, rdt, EmrFlags.Player, scale);
            }
        }

        internal static void ScaleEmrY(RandoLogger logger, RandomizedRdt rdt, EmrFlags flags, double scale)
        {
            var emrIndex = rdt.ScaleEmrY(flags, scale);
            if (emrIndex != null)
            {
                logger.WriteLine($"  {rdt}: EMR [{flags}] Y offsets scaled by {scale}");
            }
        }

        internal override void RandomizeEnemySkins(RandoConfig config, RandoLogger logger, GameData gameData, FileRepository fileRepository)
        {
            logger.WriteHeading("Randomizing enemy skins:");

            var soundRegex = new Regex("enemy([0-9][0-9])_([0-9]+)(.ogg|.wav)", RegexOptions.IgnoreCase);
            var rng = new Rng(config.Seed);

            var pldBase0 = DataManager.GetPath(BiohazardVersion, "pld0");
            var pldBase1 = DataManager.GetPath(BiohazardVersion, "pld1");
            var pldDir0 = DataManager.GetDirectories(pldBase0).Select(x => Path.Combine(pldBase0, x)).ToArray();
            var pldDir1 = DataManager.GetDirectories(pldBase1).Select(x => Path.Combine(pldBase1, x)).ToArray();
            var pldBag = new EndlessBag<string>(rng, pldDir0.Concat(pldDir1));

            var enemySkins = GetEnemySkins()
                .Zip(config.EnabledEnemySkins, (skin, enabled) => (skin, enabled))
                .Where(s => s.enabled)
                .Select(s => s.skin)
                .Shuffle(rng);

            var keepOriginal = new HashSet<byte>();
            if (enemySkins.Any(x => x.IsOriginal))
            {
                keepOriginal = enemySkins
                    .SelectMany(x => x.EnemyIds)
                    .GroupBy(x => x)
                    .Select(x => rng.Next(0, x.Count() + 1) == 0 ? x.Key : (byte)0)
                    .ToHashSet();
            }

            var soundProcessActions = new List<Action>();
            var sapLock = new object();

            // Change bank 5 to bank 9 (This means we don't need to override enemy41.sap)
            SwapSoundBank(gameData, 5, 9);

            var replacedSapNumbers = new HashSet<byte>();
            var allReplacableEnemyIds = enemySkins
                .SelectMany(x => x.EnemyIds)
                .Distinct()
                .Shuffle(rng);
            foreach (var id in allReplacableEnemyIds)
            {
                // Check if we are to preserve the original enemy type
                if (keepOriginal.Contains(id))
                {
                    logger.WriteLine($"Setting EM{config.Player}{id:X2} to Original");
                    continue;
                }

                var skin = enemySkins
                    .Shuffle(rng)
                    .First(x => x.EnemyIds.Contains(id));

                // EMD/TIM
                var enemyDir = DataManager.GetPath(BiohazardVersion, Path.Combine("emd", skin.FileName));
                var srcEmdFileName = $"EM0{id:X2}.EMD";
                var dstEmdFileName = $"EM{config.Player}{id:X2}.EMD";
                var emdPath = $"pl{config.Player}/emd{config.Player}/{dstEmdFileName}";
                var origEmd = fileRepository.GetDataPath(emdPath);
                var srcEmd = Path.Combine(enemyDir, srcEmdFileName);
                var srcTim = Path.ChangeExtension(srcEmd, ".tim");
                var dstEmd = fileRepository.GetModPath(emdPath);
                var dstTim = Path.ChangeExtension(dstEmd, ".tim");

                var srcEmdData = DataManager.GetData(srcEmd)!;
                if (srcEmdData.Length == 0)
                {
                    // NPC overwrite
                    var pldFolder = pldBag.Next();
                    var actor = Path.GetFileName(pldFolder).ToActorString();
                    var pldPath = DataManager.GetFiles(pldFolder)
                        .First(x => x.EndsWith(".PLD", StringComparison.OrdinalIgnoreCase));
                    var pldFullPath = Path.Combine(pldFolder, pldPath);
                    var pldFile = new PldFile(BiohazardVersion, new MemoryStream(DataManager.GetData(pldFullPath)));
                    var emdFile = new EmdFile(BiohazardVersion, origEmd);

                    logger.WriteLine($"Setting EM{config.Player}{id:X2} to {actor}");
                    _enemyHelper.CreateZombie(id, pldFile, emdFile, dstEmd);
                    if (Path.GetFileNameWithoutExtension(pldPath).Equals("PL01", StringComparison.OrdinalIgnoreCase))
                    {
                        OverrideSoundBank(gameData, id, 45);
                    }
                }
                else
                {
                    logger.WriteLine($"Setting EM{config.Player}{id:X2} to {skin.Name}");
                    Directory.CreateDirectory(Path.GetDirectoryName(dstEmd));
                    DataManager.Export(srcEmd, dstEmd);
                    DataManager.Export(srcTim, dstTim);
                }

                // Sounds (shared, so only do it for Player 0)
                if (config.Player == 1)
                    continue;

                // Do not replace the same sap again
                var relevantSapNumber = ((Re2EnemyHelper)EnemyHelper).GetEnemySapNumber(id);
                foreach (var fileName in DataManager.GetFiles(enemyDir))
                {
                    var file = Path.Combine(enemyDir, fileName);
                    var match = soundRegex.Match(Path.GetFileName(file));
                    if (match.Success)
                    {
                        // Only process sound files that override our relevant sap
                        var enemySapNumber = byte.Parse(match.Groups[1].Value);
                        if (relevantSapNumber != 0 && enemySapNumber != relevantSapNumber)
                            continue;

                        if (replacedSapNumbers.Contains(enemySapNumber))
                            continue;

                        relevantSapNumber = enemySapNumber;
                        var enemySapFileName = $"enemy{enemySapNumber:00}.sap";
                        var sapIndex = int.Parse(match.Groups[2].Value);
                        var sapPath = $"common/sound/enemy/{enemySapFileName}";
                        var srcSapPath = fileRepository.GetDataPath(sapPath);
                        var dstSapPath = fileRepository.GetModPath(sapPath);
                        if (!File.Exists(dstSapPath))
                        {
                            Directory.CreateDirectory(Path.GetDirectoryName(dstSapPath));
                            File.Copy(srcSapPath, dstSapPath, true);
                        }

                        logger.WriteLine($"  Setting {enemySapFileName}#{sapIndex} to {file}");
                        soundProcessActions.Add(() =>
                        {
                            var waveformBuilder = new WaveformBuilder();
                            waveformBuilder.Append(file, new MemoryStream(DataManager.GetData(file)));
                            lock (sapLock)
                                waveformBuilder.SaveAt(dstSapPath, sapIndex);
                        });
                    }
                }
                if (relevantSapNumber != 0)
                    replacedSapNumbers.Add(relevantSapNumber);
            }

            // Do sound processing in bulk / parallel
            Parallel.ForEach(soundProcessActions, x => x());
        }

        private void OverrideSoundBank(GameData gameData, byte enemyType, byte bank)
        {
            foreach (var rdt in gameData.Rdts)
            {
                foreach (var opcode in rdt.AllOpcodes.OfType<SceEmSetOpcode>())
                {
                    if (opcode.Type == enemyType)
                    {
                        opcode.SoundBank = bank;
                    }
                }
            }
        }

        private void SwapSoundBank(GameData gameData, byte srcBank, byte targetBank)
        {
            foreach (var rdt in gameData.Rdts)
            {
                foreach (var opcode in rdt.AllOpcodes.OfType<SceEmSetOpcode>())
                {
                    if (opcode.SoundBank == srcBank)
                    {
                        opcode.SoundBank = targetBank;
                    }
                }
            }
        }

        internal override void RandomizeNPCs(RandoConfig config, NPCRandomiser npcRandomiser, VoiceRandomiser voiceRandomiser)
        {
            var pldBases = new[] { "pld0", "pld1" };
            foreach (var pldBase in pldBases)
            {
                var pldFolders = DataManager.GetDirectories(BiohazardVersion, pldBase);
                foreach (var pldFolder in pldFolders)
                {
                    var actor = Path.GetFileName(pldFolder);
                    var files = DataManager.GetFiles(BiohazardVersion, Path.Combine(pldBase, pldFolder));
                    foreach (var file in files)
                    {
                        if (file.EndsWith(".pld", StringComparison.OrdinalIgnoreCase))
                        {
                            var fullPath = DataManager.GetPath(BiohazardVersion, Path.Combine(pldBase, pldFolder, file));
                            npcRandomiser.AddNPC(0, fullPath, actor);
                        }
                    }
                }
            }
        }

        internal override string BGMPath => @"Common\Sound\BGM";

        private string[] GetEnabledPartners(RandoConfig config)
        {
            var enabledPLDs = GetAllPLDs().Intersect(GetEnabledNPCs(config)).ToArray();
            return enabledPLDs;
        }

        private string[] GetAllPLDs()
        {
            var pldFiles0 = DataManager
                .GetDirectories(BiohazardVersion, $"pld0")
                .Select(x => Path.GetFileName(x))
                .ToArray();
            var pldFiles1 = DataManager
                .GetDirectories(BiohazardVersion, $"pld1")
                .Select(x => Path.GetFileName(x))
                .ToArray();
            return pldFiles0.Concat(pldFiles1).OrderBy(x => x).ToArray();
        }

        private string[] GetEnabledNPCs(RandoConfig config)
        {
            var result = new List<string>();
            var allNPCs = GetNPCs();
            var enabledNPCs = config.EnabledNPCs;
            for (int i = 0; i < allNPCs.Length; i++)
            {
                if (enabledNPCs.Length > i && enabledNPCs[i])
                {
                    result.Add(allNPCs[i]);
                }
            }
            return result.ToArray();
        }

        public override EnemySkin[] GetEnemySkins()
        {
            var emdRegex = new Regex("em0([0-9a-f][0-9a-f]).emd", RegexOptions.IgnoreCase);
            var result = new List<EnemySkin>();
            result.Add(EnemySkin.Original);
            foreach (var enemyDir in DataManager.GetDirectories(BiohazardVersion, "emd"))
            {
                var enemyIds = new List<byte>();
                foreach (var file in DataManager.GetFiles(BiohazardVersion, $"emd/{enemyDir}"))
                {
                    var fileName = Path.GetFileName(file);
                    var match = emdRegex.Match(fileName);
                    if (match.Success)
                    {
                        var id = byte.Parse(match.Groups[1].Value, NumberStyles.HexNumber);
                        enemyIds.Add(id);
                    }
                }
                if (enemyIds.Count > 0)
                {
                    var fileName = Path.GetFileName(enemyDir);
                    var enemyNames = enemyIds
                        .Select(x => EnemyHelper.GetEnemyName(x).ToLower().ToActorString())
                        .ToArray();
                    result.Add(new EnemySkin(fileName, enemyNames, enemyIds.ToArray()));
                }
            }
            return result
                .OrderBy(x => x.IsOriginal ? 0 : 1)
                .ThenBy(x => x.IsNPC ? 0 : 1)
                .ToArray();
        }

        private void ReplacePlayer(RandoConfig config, RandoLogger logger, FileRepository fileRepository,
            int pldIndex, int hurtSoundIndex, int deathSoundIndex, string actor)
        {
            var originalPLDs = new[]
            {
                "leon", "claire", "leon", "claire", "leon", "claire", "leon", "claire", "leon", "claire", "leon",
                "chris", "hunk", "tofu", "ada", "sherry"
            };

            var originalPlayerActor = originalPLDs[pldIndex];
            var srcPldDir = GetPldDirectory(actor, out var basePldIndex);

            logger.WriteHeading($"Randomizing Player PL{pldIndex:X2}:");
            logger.WriteLine($"{originalPlayerActor} becomes {actor}");

            // Create target pld folder
            var targetPldDir = fileRepository.GetModPath($"pl{config.Player}/pld");
            Directory.CreateDirectory(targetPldDir);

            // Copy base PLD files
            var basePldFileName = $"PL{basePldIndex:X2}";
            foreach (var pldPath in Directory.GetFiles(fileRepository.GetDataPath($"pl{config.Player}/pld")))
            {
                var pldFileName = Path.GetFileName(pldPath);
                if (pldFileName.StartsWith(basePldFileName, StringComparison.OrdinalIgnoreCase))
                {
                    var pldFile = $"PL{pldIndex:X2}{pldFileName.Substring(4)}";
                    File.Copy(pldPath, Path.Combine(targetPldDir, pldFile), true);
                }
            }

            // Copy override PLD files
            var pldFiles = DataManager.GetFiles(srcPldDir);
            foreach (var pldPath in pldFiles)
            {
                var pldFile = Path.GetFileName(pldPath);
                if (pldFile.Length > 4 &&
                    (pldFile.EndsWith("pld", StringComparison.OrdinalIgnoreCase) ||
                     pldFile.EndsWith("plw", StringComparison.OrdinalIgnoreCase)))
                {
                    pldFile = $"PL{pldIndex:X2}{pldFile.Substring(4)}";
                    var targetFile = Path.Combine(targetPldDir, pldFile);
                    DataManager.Export(Path.Combine(srcPldDir, pldPath), targetFile);
                    if (pldFile.EndsWith("pld", StringComparison.OrdinalIgnoreCase))
                    {
                        FixPld(targetFile, pldIndex);
                    }
                }
            }

            // Replace other PLDs
            if (actor != originalPlayerActor)
            {
                if (pldIndex == 0 || pldIndex == 1)
                {
                    var numbers = new[] { 2, 4, 6 };
                    var src = Path.Combine(targetPldDir, $"pl{pldIndex:X2}.pld");
                    foreach (var n in numbers)
                    {
                        var dst = Path.Combine(targetPldDir, $"pl{pldIndex + n:X2}.pld");
                        File.Copy(src, dst, true);
                    }
                }

                var faceIndex = pldIndex >= 14 ? 1 : 0;
                ChangePlayerInventoryFace(config, fileRepository, faceIndex, actor);

                var hurtDirectory = Path.Combine("hurt", actor.StripActorSkin());
                var allHurtFiles = DataManager
                    .GetFiles(hurtDirectory)
                    .Where(x => x.EndsWith(".ogg", StringComparison.OrdinalIgnoreCase) || x.EndsWith(".wav", StringComparison.OrdinalIgnoreCase))
                    .ToArray();
                var hurtFiles = new string[4];
                foreach (var hurtFile in allHurtFiles)
                {
                    if (int.TryParse(Path.GetFileNameWithoutExtension(hurtFile), out var i))
                    {
                        if (i < hurtFiles.Length)
                        {
                            hurtFiles[i] = hurtFile;
                        }
                    }
                }
                if (hurtFiles.All(x => x != null))
                {
                    var corePath = fileRepository.GetModPath($"common/sound/core/core{hurtSoundIndex:00}.sap");
                    Directory.CreateDirectory(Path.GetDirectoryName(corePath));
                    for (int i = 0; i < hurtFiles.Length; i++)
                    {
                        var waveformBuilder = new WaveformBuilder();
                        var hurtFileData = DataManager.GetData(Path.Combine(hurtDirectory, hurtFiles[i]));
                        waveformBuilder.Append(hurtFiles[i], new MemoryStream(hurtFileData));
                        if (i == 0)
                            waveformBuilder.Save(corePath, 0x0F);
                        else
                            waveformBuilder.SaveAppend(corePath);
                    }
                    {
                        var coreDeathPath = fileRepository.GetModPath($"common/sound/core/core{deathSoundIndex:00}.sap");
                        var waveformBuilder = new WaveformBuilder();
                        var hurtFileData = DataManager.GetData(Path.Combine(hurtDirectory, hurtFiles[3]));
                        waveformBuilder.Append(hurtFiles[3], new MemoryStream(hurtFileData));
                        waveformBuilder.Save(coreDeathPath);
                    }
                }
            }
        }

        private void FixPld(string path, int pldIndex)
        {
            // Open the PLD and insert 160 empty positions at the start of mesh 0
            // this prevents the game from morphing any visible primitives
            var pldFileFixed = new PldFile(BioVersion.Biohazard2, path);

            // Ensure we have at least 19 parts, so everything works on Claire
            var mesh = pldFileFixed.GetMesh(0).ToBuilder();
            while (mesh.Count < 19)
            {
                mesh.Add();
            }
            if (pldIndex == 14)
            {
                mesh[0].InsertDummyPoints(160);
            }
            pldFileFixed.SetMesh(0, mesh.ToMesh());
            pldFileFixed.Save(path);
        }

        private void ScaleEnemyAttacks(RandoConfig config, FileRepository fileRepository)
        {
            if (!config.ChangePlayer)
                return;

            var enemiesToScale = new byte[] {
                Re2EnemyIds.ZombieCop,
                Re2EnemyIds.ZombieBrad,
                Re2EnemyIds.ZombieGuy1,
                Re2EnemyIds.ZombieGirl,
                Re2EnemyIds.ZombieTestSubject,
                Re2EnemyIds.ZombieScientist,
                Re2EnemyIds.ZombieNaked,
                Re2EnemyIds.ZombieGuy2,
                Re2EnemyIds.ZombieGuy3,
                Re2EnemyIds.ZombieRandom,
                Re2EnemyIds.ZombieDog,
                Re2EnemyIds.Crow,
                Re2EnemyIds.LickerRed,
                Re2EnemyIds.Alligator,
                Re2EnemyIds.GEmbryo,
                Re2EnemyIds.GAdult,
                Re2EnemyIds.Tyrant1,
                Re2EnemyIds.Tyrant2,
                Re2EnemyIds.ZombieArms,
                Re2EnemyIds.Ivy,
                Re2EnemyIds.Birkin1,
                Re2EnemyIds.Birkin2,
                Re2EnemyIds.Birkin4,
                Re2EnemyIds.Birkin5,
                0x36,
                0x37,
                Re2EnemyIds.IvyPurple,
                Re2EnemyIds.GiantMoth,
                Re2EnemyIds.MarvinBranagh
            };

            for (var i = 0; i < 2; i++)
            {
                var pldFileName = $"pl{i}/pld/pl{i:00}.pld";
                var originalPld = fileRepository.GetDataPath(pldFileName);
                var moddedPld = fileRepository.GetModPath(pldFileName);
                var originalPldFile = new PldFile(BioVersion.Biohazard2, originalPld);
                var moddedPldFile = new PldFile(BioVersion.Biohazard2, moddedPld);
                var targetScale = moddedPldFile.CalculateEmrScale(originalPldFile);
                if (targetScale == 1)
                    continue;

                foreach (var enemy in enemiesToScale)
                {
                    var fileName = $"pl{i}/emd{i}/em{i}{enemy:X2}.emd";
                    var target = fileRepository.GetModPath(fileName);
                    var source = File.Exists(target) ?
                        target :
                        fileRepository.GetDataPath(fileName);
                    ScaleEnemyAttacks(source, target, targetScale);
                }
            }
        }

        private void ScaleEnemyAttacks(string srcPath, string dstPath, double scale)
        {
            var emdFile = new EmdFile(BioVersion.Biohazard2, srcPath);
            emdFile.SetEmr(2, emdFile.GetEmr(2).Scale(scale));
            Directory.CreateDirectory(Path.GetDirectoryName(dstPath));
            emdFile.Save(dstPath);
        }

        private void ChangePlayerInventoryFace(RandoConfig config, FileRepository fileRepository, int index, string actor)
        {
            if (BgCreator == null)
                return;

            var srcPldDir = GetPldDirectory(actor, out _);
            var faceImageData = DataManager.GetData(Path.Combine(srcPldDir, "face.png"));
            if (faceImageData == null)
                return;

            var filename = Path.Combine("common", "data", $"st{config.Player}_jp.tim");
            var outputTimPath = fileRepository.GetModPath(filename);
            var inputTimData = File.Exists(outputTimPath)
                ? File.ReadAllBytes(outputTimPath)
                : DataManager.GetData(BioVersion.Biohazard2, Path.GetFileName(filename));
            Directory.CreateDirectory(Path.GetDirectoryName(outputTimPath!));

            var timCollection = new TimCollectionFile(inputTimData);
            if (timCollection.Tims.Count > 1)
            {
                var tim1 = timCollection.Tims[1];
                BgCreator.DrawImage(tim1, faceImageData, index * 44, 72);
                timCollection.Save(outputTimPath);
            }
        }

        private string GetPldDirectory(string actor, out int pldIndex)
        {
            var srcPldDir = DataManager.GetPath(BiohazardVersion, $"pld0\\{actor}");
            pldIndex = 0;
            if (!DataManager.GetFiles(srcPldDir).Any())
            {
                srcPldDir = DataManager.GetPath(BiohazardVersion, $"pld1\\{actor}");
                pldIndex = 1;
            }
            return srcPldDir;
        }

        protected override void SerialiseInventory(FileRepository fileRepository)
        {
            var inventoryAddress = new[]
            {
                AddressInventoryLeon,
                AddressInventoryClaire
            };
            var specialAddresses = new[]
            {
                new uint[] { 0x400000 + 0x00102248, 0x400000 + 0x00101C84 },
                new uint[] { 0x400000 + 0x0010223F },
            };

            var bw = new PatchWriter(ExePatch);
            for (int i = 0; i < inventoryAddress.Length; i++)
            {
                if (Inventories.Count <= i)
                    break;

                var inventory = Inventories[i];
                if (inventory == null)
                    continue;

                var offset = inventoryAddress[i];
                bw.Begin(offset);
                for (int j = 0; j < 11; j++)
                {
                    var entry = new RandomInventory.Entry();
                    if (j < inventory.Entries.Length)
                        entry = inventory.Entries[j];
                    else if (j == 10 && inventory.Special.HasValue)
                        entry = inventory.Special.Value;

                    bw.Write((byte)entry.Type);
                    bw.Write((byte)entry.Count);
                    bw.Write((byte)entry.Part);
                }
                bw.End();

                if (inventory.Special is RandomInventory.Entry specialEntry)
                {
                    foreach (var address in specialAddresses[i])
                    {
                        bw.Begin(address);
                        bw.Write(specialEntry.Type);
                        bw.End();
                    }

                    // Set weapon type to none when switching back from partner
                    bw.Begin(0x400000 + 0x1021FF);
                    bw.Write((byte)0xB1);
                    bw.Write((byte)0x80);
                    bw.Write((byte)0x90);
                    bw.End();
                }
            }
        }

        private void DisableDemo()
        {
            var pw = new PatchWriter(ExePatch);
            pw.Begin(0x503C45);
            pw.Write(0x90);
            pw.Write(0x90);
            pw.End();
        }

        private void FixClaireWeapons()
        {
            var offsets = new (int, byte)[]
            {
                (0x400000 + 0x13A386, 0xB8),
                (0x400000 + 0x13A387, 0xF7),
                (0x400000 + 0x13A38E, 0xB8),
                (0x400000 + 0x13A38F, 0xF7),
                (0x400000 + 0x13A396, 0xEF),
                (0x400000 + 0x13A397, 0xF8),
                (0x400000 + 0x13A39E, 0xEF),
                (0x400000 + 0x13A39F, 0xF8),
                (0x400000 + 0x13A598, 0x60),
                (0x400000 + 0x13A599, 0x64)
            };

            var bw = new BinaryWriter(ExePatch);
            foreach (var (o, b) in offsets)
            {
                bw.Write(o);
                bw.Write(1);
                bw.Write(b);
            }
        }

        private void FixWeaponHitScan(RandoConfig config)
        {
            var table = new short[]
            {
                0, 0, 0, 0,
                -200, 300, 0, 0,
                -200, 300, -2427, -2120,
                -200, 300, -2427, -2120,
                -200, 300, -2427, -2120,
                -200, 300, -2427, -2275, // (corrected for Claire)
                -200, 300, -2427, -2442, // (corrected for Claire)
                -500, 150, -1813, -1891, // (corrected for Claire)
                -500, 200, -1813, -1943, // (corrected for Claire)
                -200, 300, 0, 0,
                -200, 300, 0, 0,
                -200, 300, 0, 0,
                -200, 300, 0, 0,
                -400, 30, -1850, -1890,
                -600, 600, -1894, -1809,
                -400, 200, -1894, -1809,
                -200, 300, 0, 0,
                -200, 300, 0, 0,
                -200, 300, -2427, -2120,
                -200, 300, -2427, -2120,
            };

            if (config.ChangePlayer && config.SwapCharacters)
            {
                // Swap Leon and Claires values around
                for (var i = 0; i < table.Length; i += 4)
                {
                    (table[i + 3], table[i + 2]) = (table[i + 2], table[i + 3]);
                }
            }

            for (var i = 0; i < 2; i++)
            {
                var pldPath = GetSelectedPldPath(config, i);
                var csvData = DataManager.GetText(Path.Combine(pldPath, "weapons.csv"));
                if (csvData == null)
                    continue;

                var csv = csvData
                    .Split(['\n', '\r'], StringSplitOptions.RemoveEmptyEntries)
                    .Where(x => !string.IsNullOrWhiteSpace(x))
                    .Select(x => x.Trim().Split(','))
                    .ToArray();

                var k = 0;
                for (var j = 0; j < table.Length; j += 4)
                {
                    for (var l = 0; l < 4; l++)
                    {
                        if ((i == 0 && l == 3) || (i == 1 && l == 2))
                            continue;

                        table[j + l] = short.Parse(csv[k][l]);
                    }
                    k++;
                }
            }

            var pw = new PatchWriter(ExePatch);
            pw.Begin(0x53A358);
            var data = MemoryMarshal.Cast<short, byte>(new Span<short>(table));
            foreach (var d in data)
            {
                pw.Write(d);
            }
            pw.End();
        }

        private void DisableWaitForSherry()
        {
            // Disable (you must wait for Sherry)
            var bw = new PatchWriter(ExePatch);
            bw.Begin(0x400000 + 0xE9490);
            bw.Write((byte)0xEB);
            bw.End();
        }

        private void FixMothCrash()
        {
            var bw = new PatchWriter(ExePatch);
            bw.Begin(0x400000 + 0xAFFB6);
            bw.Write(new byte[] { 0xE9, 0x86, 0x00, 0x00, 0x00, 0x90 });
            bw.End();
        }

        private void AddCreditsRoom(FileRepository fileRepository, Rng rng)
        {
            var srcPath = DataManager.GetPath(BiohazardVersion, "credits");
            var dstPath = fileRepository.GetModPath("");

            CopyFolder(Path.Combine(srcPath, "common"), Path.Combine(dstPath, "common"));
            DataManager.Export(
                Path.Combine(srcPath, "footsteps.xml"),
                Path.Combine(dstPath, "footsteps.xml"));
            DataManager.Export(
                Path.Combine(srcPath, "item.xml"),
                Path.Combine(dstPath, "item.xml"));

            // HD
            var dstHiRes = Path.GetFullPath(Path.Combine(dstPath, "..", "hires"));
            if (Directory.Exists(dstHiRes))
            {
                var srcHiRes = Path.Combine(srcPath, "hires");
                CopyFolder(srcHiRes, dstHiRes);
            }

            // Music
            var musicSrcPath = Path.Combine(srcPath, "music");
            var musicFiles = DataManager
                .GetFiles(musicSrcPath)
                .Where(x => x.EndsWith(".ogg", StringComparison.OrdinalIgnoreCase))
                .ToArray();
            var musicDst = Path.Combine(dstPath, "pl0", "voice", "stage2");
            var sapIdStart = 304;
            Directory.CreateDirectory(musicDst);
            Parallel.For(0, musicFiles.Length, i =>
            {
                var musicFile = Path.Combine(musicSrcPath, musicFiles[i]);
                var sapId = sapIdStart + i;
                var sapDst = Path.Combine(musicDst, $"v{sapId:000}.sap");
                var waveBuilder = new WaveformBuilder();
                waveBuilder.Append(musicFile, new MemoryStream(DataManager.GetData(musicFile)));
                waveBuilder.Save(sapDst);
            });

            var rdtBuilder = ((Rdt2)Rdt.FromData(BiohazardVersion, DataManager.GetData(Path.Combine(srcPath, "room2170.rdt")))).ToBuilder();

            var credits = DataManager.GetText(Path.Combine(srcPath, "credits.txt"))!
                .Split(['\n', '\r'], StringSplitOptions.RemoveEmptyEntries)
                .Where(x => !string.IsNullOrEmpty(x))
                .ToArray();
            if (credits.Length != 0)
            {
                var msgBuilderEn = rdtBuilder.MSGEN.ToBuilder();
                var msgBuilderJa = rdtBuilder.MSGJA.ToBuilder();
                for (var i = 0; i < credits.Length; i++)
                {
                    var targetIndex = 11 + i;
                    var msgText = credits[i].Replace("\\n", "\n");
                    msgBuilderEn.Messages[targetIndex] = msgText.ToMsg(MsgLanguage.English, BioVersion.Biohazard2, false);
                    msgBuilderJa.Messages[targetIndex] =
                        string.Join("\n", msgText
                            .Split(new[] { '\n' })
                            .Select(x => x.Trim()))
                        .ToMsg(MsgLanguage.Japanese, BioVersion.Biohazard2, false);

                    var dd = msgBuilderEn.Messages[targetIndex].Data.ToList();
                    dd[1] = 0;
                    dd.RemoveAt(dd.Count - 1);
                    dd.RemoveAt(dd.Count - 1);
                    dd.Add(0xFA);
                    dd.Add(0x01);
                    dd.Add(0xFE);
                    dd.Add(0xA0);
                    msgBuilderEn.Messages[targetIndex] = new Msg(BioVersion.Biohazard2, MsgLanguage.English, dd.ToArray());

                    dd = msgBuilderJa.Messages[targetIndex].Data.ToList();
                    dd[1] = 0;
                    dd.RemoveAt(dd.Count - 1);
                    dd.RemoveAt(dd.Count - 1);
                    dd.Add(0xFA);
                    dd.Add(0x01);
                    dd.Add(0xFE);
                    dd.Add(0xA0);
                    msgBuilderJa.Messages[targetIndex] = new Msg(BioVersion.Biohazard2, MsgLanguage.Japanese, dd.ToArray());
                }
                rdtBuilder.MSGEN = msgBuilderEn.ToMsgList();
                rdtBuilder.MSGJA = msgBuilderJa.ToMsgList();
            }

            var dialogue = DataManager.GetText(Path.Combine(srcPath, "dialogue.txt"))!
                .Split(['\n', '\r'], StringSplitOptions.RemoveEmptyEntries)
                .Select(x => x.Trim())
                .Where(x => !string.IsNullOrEmpty(x))
                .ToArray();
            if (dialogue.Length != 0)
            {
                var msgBag = new EndlessBag<string>(rng, dialogue);
                var msgBuilderEn = rdtBuilder.MSGEN.ToBuilder();
                var msgBuilderJa = rdtBuilder.MSGJA.ToBuilder();
                for (var i = 39; i < msgBuilderEn.Messages.Count; i++)
                {
                    var msgText = msgBag.Next();
                    msgBuilderEn.Messages[i] = msgText.ToMsg(MsgLanguage.English, BioVersion.Biohazard2);
                    msgBuilderJa.Messages[i] = msgText.ToMsg(MsgLanguage.Japanese, BioVersion.Biohazard2);
                }
                rdtBuilder.MSGEN = msgBuilderEn.ToMsgList();
                rdtBuilder.MSGJA = msgBuilderJa.ToMsgList();
            }

            var dstRdt0 = Path.Combine(dstPath, "pl0", "rdt", "ROOM2170.RDT");
            var dstRdt1 = Path.Combine(dstPath, "pl1", "rdt", "ROOM2171.RDT");
            Directory.CreateDirectory(Path.GetDirectoryName(dstRdt0));
            Directory.CreateDirectory(Path.GetDirectoryName(dstRdt1));
            var rdt = rdtBuilder.ToRdt();
            rdt.Data.WriteToFile(dstRdt0);
            rdt.Data.WriteToFile(dstRdt1);

            // Redirect end of game
            for (var p = 0; p < 2; p++)
            {
                ModifyRdt(fileRepository, p, new RdtId(6, 0x00), 7, 51);
                ModifyRdt(fileRepository, p, new RdtId(6, 0x03), p == 0 ? 0x0A : 0x0B, 132);
            }
        }

        private void CopyFolder(string srcPath, string dstPath)
        {
            var files = DataManager.GetFiles(srcPath);
            foreach (var f in files)
            {
                var srcFile = Path.Combine(srcPath, f);
                var dstFile = Path.Combine(dstPath, f);
                var dstFolder = Path.GetDirectoryName(dstFile);
                Directory.CreateDirectory(dstFolder);
                if (dstFile.EndsWith(".sap.ogg", StringComparison.OrdinalIgnoreCase))
                {
                    dstFile = dstFile.Substring(0, dstFile.Length - 4);
                    var wavBuilder = new WaveformBuilder();
                    wavBuilder.Append(srcFile, new MemoryStream(DataManager.GetData(srcFile)));
                    wavBuilder.Save(dstFile);
                }
                else
                {
                    DataManager.Export(srcFile, dstFile);
                }
            }

            var directories = DataManager.GetDirectories(srcPath);
            foreach (var dir in directories)
            {
                CopyFolder(Path.Combine(srcPath, dir), Path.Combine(dstPath, dir));
            }
        }

        private static void ModifyRdt(FileRepository fileRepository, int p, RdtId rdtId, int procedureIndex, int keep)
        {
            var scdRedirect =
                "570017022DC60000" +
                "3B00013100001111222233334444000000000000000001170000002000000000" +
                "4700" +
                "0100";

            if (rdtId.Room == 0x03)
            {
                scdRedirect =
                    "6F0E" +
                    "02" +
                    scdRedirect;
            }

            var rdtPath = fileRepository.GetModPath($"pl{p}/rdt/ROOM{rdtId}{p}.RDT");
            var rdt = (Rdt2)Rdt.FromFile(BioVersion.Biohazard2, rdtPath);
            var rdtBuilder = rdt.ToBuilder();
            var scdBuilder = rdtBuilder.SCDMAIN.ToBuilder();
            var proc = scdBuilder.Procedures[procedureIndex];
            var pKeep = proc.Data.Slice(0, keep).ToArray();
            var pNew = StringToBytes(scdRedirect);
            var pReplace = pKeep.Concat(pNew).ToArray();
            scdBuilder.Procedures[procedureIndex] = new ScdProcedure(BioVersion.Biohazard2, pReplace);
            rdtBuilder.SCDMAIN = scdBuilder.ToProcedureList();
            rdt = rdtBuilder.ToRdt();
            rdt.Data.WriteToFile(rdtPath);
        }

        private static byte[] StringToBytes(string s)
        {
            var result = new byte[s.Length / 2];
            for (var i = 0; i < s.Length; i += 2)
            {
                var b = s.Substring(i, 2);
                result[i / 2] = Convert.ToByte(b, 16);
            }
            return result;
        }
    }
}
