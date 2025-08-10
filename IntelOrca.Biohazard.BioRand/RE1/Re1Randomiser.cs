using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using IntelOrca.Biohazard.Extensions;
using IntelOrca.Biohazard.Model;
using IntelOrca.Biohazard.Room;

namespace IntelOrca.Biohazard.BioRand.RE1
{
    public class Re1Randomiser : BaseRandomiser
    {
        private readonly Re1EnemyHelper _enemyHelper = new Re1EnemyHelper();
        private Re1ItemHelper? _itemHelper;
        private object _playerFaceSync = new object();

        protected override BioVersion BiohazardVersion => BioVersion.Biohazard1;
        internal override IDoorHelper DoorHelper { get; } = new Re1DoorHelper();
        internal override IItemHelper ItemHelper
        {
            get
            {
                _itemHelper ??= new Re1ItemHelper(InstallConfig!.MaxInventorySize);
                return _itemHelper;
            }
        }
        internal override IEnemyHelper EnemyHelper => _enemyHelper;
        internal override INpcHelper NpcHelper { get; } = new Re1NpcHelper();

        public Re1Randomiser(ReInstallConfig installConfig, IBgCreator? bgCreator)
            : base(installConfig, bgCreator)
        {
        }

        public override string GetPlayerName(int player) => player == 0 ? "Chris" : "Jill";

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

        public override bool ValidateGamePath(string path)
        {
            var dataPath = FindDataPath(path);
            return Directory.Exists(Path.Combine(dataPath, "STAGE1"));
        }

        protected override string GetDataPath(string installPath) => FindDataPath(installPath);

        protected override RdtId[] GetRdtIds(string dataPath)
        {
            var rdtIds = new HashSet<RdtId>();
            for (int stage = 1; stage <= 7; stage++)
            {
                var files = Directory.GetFiles(Path.Combine(dataPath, @$"STAGE{stage}"));
                foreach (var file in files)
                {
                    // Check the file is an RDT file
                    var fileName = Path.GetFileName(file);
                    if (!fileName.StartsWith("ROOM", System.StringComparison.OrdinalIgnoreCase) ||
                        !fileName.EndsWith(".RDT", System.StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    if (RdtId.TryParse(fileName.Substring(4, 3), out var rdtId))
                    {
                        rdtIds.Add(rdtId);
                    }
                }
            }
            return rdtIds
                .OrderBy(x => x.Stage)
                .ThenBy(x => x.Room)
                .ToArray();
        }

        internal static RdtId[] MissingRooms { get; } = new[]
        {
            new RdtId(0, 0x10),
            new RdtId(0, 0x19),
            new RdtId(1, 0x00),
            new RdtId(1, 0x0C),
            new RdtId(1, 0x13),
            new RdtId(1, 0x14),
            new RdtId(1, 0x15),
            new RdtId(1, 0x16),
            new RdtId(1, 0x17),
            new RdtId(1, 0x18),
            new RdtId(1, 0x19),
            new RdtId(1, 0x1A),
            new RdtId(1, 0x1B),
            new RdtId(1, 0x1C)
        };

        protected override string GetRdtPath(string dataPath, RdtId rdtId, int player, bool mod)
        {
            if (!mod && MissingRooms.Contains(rdtId))
            {
                rdtId = new RdtId(rdtId.Stage + 5, rdtId.Room);
            }
            var path = Path.Combine(dataPath, @$"STAGE{rdtId.Stage + 1}\ROOM{rdtId}{player}.RDT");
            return path;
        }

        public override void Generate(RandoConfig config, IRandoProgress progress, FileRepository fileRepository)
        {
            var reConfig = InstallConfig;
            if (!reConfig.IsEnabled(BioVersion.Biohazard1))
            {
                throw new BioRandUserException("RE1 installation must be enabled to randomize RE1.");
            }
            if (config.RandomBgm && MusicAlbumSelected(config, "RE2"))
            {
                if (!reConfig.IsEnabled(BioVersion.Biohazard2))
                {
                    throw new BioRandUserException("RE2 installation must be enabled to use RE2 assets.");
                }
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
            // Chris / Jill
            var cts = new CancellationTokenSource();
            Parallel.Invoke(po,
                () =>
                {
                    try
                    {
                        GenerateRdts(config.WithPlayerScenario(0, 0), progress, fileRepository, cts.Token);
                    }
                    catch
                    {
                        cts.Cancel();
                        throw;
                    }
                },
                () =>
                {
                    try
                    {
                        GenerateRdts(config.WithPlayerScenario(1, 0), progress, fileRepository, cts.Token);
                    }
                    catch
                    {
                        cts.Cancel();
                        throw;
                    }
                });

            FixRoomSounds(fileRepository);
            DisableDemo();
            FixFlamethrowerCombine();
            FixWasteHeal();
            FixNeptuneDamage();
            if (reConfig.MaxInventorySize)
            {
                FixChrisInventorySize();
            }
            FixWeaponHitScan(config);
            FixYawnPoison(config);

            base.Generate(config, progress, fileRepository);
        }

        protected override string[] TitleCardSoundFiles { get; } =
            new[] {
                "sound/BIO01.WAV",
                "sound/EVIL01.WAV"
            };

        internal override string[] ChangePlayerCharacters(RandoConfig config, RandoLogger logger, GameData gameData, FileRepository fileRepository)
        {
            var actor = config.Player == 0 ? "chris" : "jill";
            var partner = config.Player == 0 ? "rebecca" : "barry";
            if (config.ChangePlayer)
            {
                var pldPath = GetSelectedPldPath(config, config.Player);
                actor = Path.GetFileName(pldPath);
                SwapPlayerCharacter(config, logger, gameData, config.Player, actor, fileRepository);
            }

            // Change partner
            if (config.Player == 0)
            {
                var rng = new Rng(config.Seed);
                var enabledPLDs = GetEnabledPartners(config);
                if (enabledPLDs.Length != 0)
                {
                    partner = rng.NextOf(enabledPLDs);
                    SwapPlayerCharacter(config, logger, gameData, 3, partner, fileRepository);
                }
            }

            return new[] { actor, partner };
        }

        private void ChangePlayerInventoryFace(FileRepository fileRepository, int pldIndex, string actor)
        {
            if (BgCreator == null)
                return;

            lock (_playerFaceSync)
            {
                var outputTimPath = fileRepository.GetModPath("data/statface.tim");
                var inputTimPath = File.Exists(outputTimPath) ?
                    outputTimPath :
                    fileRepository.GetDataPath("data/statface.tim");
                Directory.CreateDirectory(Path.GetDirectoryName(outputTimPath!));

                var timFile = new TimFile(inputTimPath);
                var pldPath = GetPldDirectory(actor, out _);
                var faceImageData = DataManager.GetData(Path.Combine(pldPath, "face.png"));
                if (faceImageData != null)
                {
                    var row = pldIndex / 2;
                    var col = pldIndex % 2;
                    BgCreator.DrawImage(timFile, faceImageData, col * 32, row * 32);
                }
                timFile.Save(outputTimPath);
            }
        }

        public override EnemySkin[] GetEnemySkins()
        {
            var emdRegex = new Regex("em10([0-9a-f][0-9a-f]).emd", RegexOptions.IgnoreCase);
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

        private void SwapPlayerCharacter(RandoConfig config, RandoLogger logger, GameData gameData, int pldIndex, string actor, FileRepository fileRepository)
        {
            var originalPlayerActor = pldIndex switch
            {
                0 => "chris",
                1 => "jill",
                2 => "barry",
                3 => "rebecca",
                _ => throw new NotImplementedException()
            };
            var srcPldDir = GetPldDirectory(actor, out _);

            if (originalPlayerActor != actor)
            {
                logger.WriteHeading(pldIndex < 2 ? "Randomizing Player:" : "Randomizing Partner:");
                logger.WriteLine($"{originalPlayerActor} becomes {actor}");
            }

            var targetEnemyDir = fileRepository.GetModPath("enemy");
            var targetPlayersDir = fileRepository.GetModPath("players");
            Directory.CreateDirectory(targetEnemyDir);
            Directory.CreateDirectory(targetPlayersDir);

            // Copy base EMW files
            var baseWeaponIndex = (config.SwapCharacters ? config.Player ^ 1 : config.Player) * 16;
            for (var i = 0; i < 16; i++)
            {
                var sourceFileName = $"players/w{baseWeaponIndex + i:X2}.emw";
                var targetFileName = $"players/w{(pldIndex * 16) + i:X2}.emw";
                var sourceFile = fileRepository.GetDataPath(sourceFileName);
                var targetLocation = fileRepository.GetModPath(targetFileName);
                if (File.Exists(sourceFile))
                {
                    File.Copy(sourceFile, targetLocation, true);
                }
            }

            // Copy override EMD files
            var pldFiles = DataManager.GetFiles(srcPldDir);
            foreach (var pldFile in pldFiles)
            {
                var pldFilePath = Path.Combine(srcPldDir, pldFile);
                if (Regex.IsMatch(pldFile, "char1[01].emd", RegexOptions.IgnoreCase))
                {
                    var targetFileName = $"CHAR1{pldIndex}.EMD";
                    DataManager.Export(pldFilePath, Path.Combine(targetEnemyDir, targetFileName));
                    continue;
                }

                var regex = Regex.Match(pldFile, "w([0-9a-f][0-9a-f]).emw", RegexOptions.IgnoreCase);
                if (regex.Success)
                {
                    var originalIndex = Convert.ToInt32(regex.Groups[1].Value, 16);
                    var weaponIndex = originalIndex % 16;
                    var targetWeaponIndex = (pldIndex * 16) + weaponIndex;
                    var targetFileName = $"W{targetWeaponIndex:X2}.EMW";
                    DataManager.Export(pldFilePath, Path.Combine(targetPlayersDir, targetFileName));
                }
            }

            // Replace hurt sounds
            if (actor != originalPlayerActor)
            {
                var baseHurtDir = Path.Combine("hurt", actor.StripActorSkin());
                var allHurtFiles = DataManager.GetFiles(baseHurtDir)
                    .Where(WaveformBuilder.IsSupportedExtension)
                    .Select(x => Path.Combine(baseHurtDir, x))
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
                    var hurtFileNames = new[]
                    {
                        new[] { "chris", "ch_ef" },
                        new[] { "jill", "jill_ef" },
                        new string[0],
                        new[] { "reb" }
                    };

                    var soundDir = fileRepository.GetModPath("sound");
                    Directory.CreateDirectory(soundDir);

                    for (int i = 0; i < hurtFiles.Length; i++)
                    {
                        var waveformBuilder = new WaveformBuilder();
                        waveformBuilder.Append(hurtFiles[i], new MemoryStream(DataManager.GetData(hurtFiles[i])));
                        var arr = hurtFileNames[pldIndex];
                        foreach (var hurtFileName in arr)
                        {
                            var soundPath = Path.Combine(soundDir, $"{hurtFileName}{i + 1:00}.WAV");
                            waveformBuilder.Save(soundPath);
                        }
                    }

                    if (pldIndex <= 1)
                    {
                        var nom = pldIndex == 0 ? "ch_nom.wav" : "ji_nom.wav";
                        var sime = pldIndex == 0 ? "ch_sime.wav" : "ji_sime.wav";
                        var waveformBuilder = new WaveformBuilder();
                        waveformBuilder.Append(hurtFiles[3], new MemoryStream(DataManager.GetData(hurtFiles[3])));
                        waveformBuilder.Save(Path.Combine(soundDir, nom));
                        waveformBuilder = new WaveformBuilder();
                        waveformBuilder.Append(hurtFiles[2], new MemoryStream(DataManager.GetData(hurtFiles[2])));
                        waveformBuilder.Save(Path.Combine(soundDir, sime));
                    }
                }
            }

            ChangePlayerInventoryFace(fileRepository, pldIndex, actor);

            if (pldIndex <= 1)
                FixPlayerAnimations(config, logger, gameData, fileRepository);
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
                        if (file.EndsWith(".emd", StringComparison.OrdinalIgnoreCase))
                        {
                            var fullPath = DataManager.GetPath(BiohazardVersion, Path.Combine(pldBase, pldFolder, file));
                            npcRandomiser.AddNPC(0, fullPath, actor);
                        }
                    }
                }
            }
        }

        internal override void RandomizeEnemySkins(RandoConfig config, RandoLogger logger, GameData gameData, FileRepository fileRepository)
        {
            logger.WriteHeading("Randomizing enemy skins:");

            var rng = new Rng(config.Seed);

            var pldDir0 = DataManager.GetDirectories(BiohazardVersion, "pld0");
            var pldDir1 = DataManager.GetDirectories(BiohazardVersion, "pld1");
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

            var allReplacableEnemyIds = enemySkins
                .SelectMany(x => x.EnemyIds)
                .Distinct()
                .Shuffle(rng);
            foreach (var id in allReplacableEnemyIds)
            {
                // Check if we are to preserve the original enemy type
                if (keepOriginal.Contains(id))
                {
                    logger.WriteLine($"Setting EM1{config.Player}{id:X2} to Original");
                    continue;
                }

                var skin = enemySkins
                    .Shuffle(rng)
                    .First(x => x.EnemyIds.Contains(id));

                // EMD/TIM
                var enemyDir = DataManager.GetPath(BiohazardVersion, Path.Combine("emd", skin.FileName));
                var srcEmdFileName = $"EM10{id:X2}.EMD";
                var dstEmdFileName = $"EM1{config.Player}{id:X2}.EMD";
                var emdPath = $"enemy/{dstEmdFileName}";
                var origEmd = fileRepository.GetDataPath(emdPath);
                var srcEmdData = DataManager.GetData(Path.Combine(enemyDir, srcEmdFileName))!;
                var dstEmd = fileRepository.GetModPath(emdPath);

                if (srcEmdData.Length == 0)
                {
                    // NPC overwrite
                    var pldFolder = pldBag.Next();
                    var actor = Path.GetFileName(pldFolder).ToActorString();
                    var pldPath = Directory.GetFiles(pldFolder)
                        .First(x => x.EndsWith(".emd", StringComparison.OrdinalIgnoreCase));
                    var pldFile = new EmdFile(BiohazardVersion, pldPath);
                    var emdFile = new EmdFile(BiohazardVersion, origEmd);

                    logger.WriteLine($"Setting EM1{config.Player}{id:X2} to {actor}");
                    _enemyHelper.CreateZombie(id, pldFile, emdFile, dstEmd);
                }
                else
                {
                    logger.WriteLine($"Setting EM1{config.Player}{id:X2} to {skin.Name}");
                    Directory.CreateDirectory(Path.GetDirectoryName(dstEmd));
                    File.WriteAllBytes(dstEmd, srcEmdData);
                }

                // Sounds (shared, so only do it for Player 0)
                if (config.Player == 1)
                    continue;

                foreach (var file in DataManager.GetFiles(enemyDir))
                {
                    if (!WaveformBuilder.IsSupportedExtension(file))
                        continue;

                    var filePath = Path.Combine(enemyDir, file);
                    var soundFileName = Path.ChangeExtension(file, ".wav").ToUpperInvariant();
                    var wavPath = soundFileName.Equals("VB00_10.WAV", StringComparison.OrdinalIgnoreCase) ?
                        $"voice/{soundFileName}" :
                        $"sound/{soundFileName}";
                    var dstPath = fileRepository.GetModPath(wavPath);
                    Directory.CreateDirectory(Path.GetDirectoryName(dstPath));

                    logger.WriteLine($"  Setting {soundFileName} to {filePath}");
                    soundProcessActions.Add(() =>
                    {
                        var waveformBuilder = new WaveformBuilder();
                        waveformBuilder.Append(file, new MemoryStream(DataManager.GetData(filePath)));
                        lock (sapLock)
                            waveformBuilder.Save(dstPath);
                    });
                }
            }

            // Do sound processing in bulk / parallel
            Parallel.ForEach(soundProcessActions, x => x());
        }

        protected override void SerialiseInventory(FileRepository fileRepository)
        {
            var doc = new XmlDocument();
            var root = doc.CreateElement("Init");
            var player = 1;
            foreach (var inventory in Inventories.Reverse<RandomInventory?>())
            {
                var playerNode = doc.CreateElement("Player");
                if (inventory != null)
                {
                    var maxItems = player == 0 ? 6 + 6 : 8;
                    var entries = inventory.Entries;
                    for (int i = 0; i < maxItems; i++)
                    {
                        var entry = entries.Length > i ? entries[i] : new RandomInventory.Entry();
                        var entryNode = doc.CreateElement("Entry");
                        entryNode.SetAttribute("id", entry.Type.ToString());
                        entryNode.SetAttribute("count", entry.Count.ToString());
                        playerNode.AppendChild(entryNode);
                    }
                }
                root.AppendChild(playerNode);
                player--;
            }
            doc.AppendChild(root);
            doc.Save(fileRepository.GetModPath("init.xml"));
        }

        internal override string BGMPath => "sound";

        internal static string FindDataPath(string installPath)
        {
            var originalDataPath = Path.Combine(installPath, "JPN");
            if (!Directory.Exists(originalDataPath))
            {
                originalDataPath = Path.Combine(installPath, "USA");
                if (!Directory.Exists(originalDataPath))
                {
                    originalDataPath = installPath;
                }
            }
            return originalDataPath;
        }

        private void FixPlayerAnimations(RandoConfig config, RandoLogger logger, GameData gameData, FileRepository fileRepository)
        {
            // Each room may store some player animations
            // We must correct the height of these for the custom character
            var pldPath = $"ENEMY/CHAR1{config.Player}.EMD";
            var oldPldPath = fileRepository.GetDataPath(pldPath);
            var newPldPath = fileRepository.GetModPath(pldPath);
            if (!fileRepository.Exists(newPldPath))
                return;

            var oldPldFile = new EmdFile(BioVersion.Biohazard1, oldPldPath);
            var newPldFile = new EmdFile(BioVersion.Biohazard1, newPldPath);
            var scale = newPldFile.CalculateEmrScale(oldPldFile);

            logger.WriteHeading($"Scaling EMRs in RDTs");
            foreach (var rdt in gameData.Rdts)
            {
                var rdt1 = (Rdt1)rdt.RdtFile;
                var builder = rdt1.ToBuilder();
                if (builder.EMR != null)
                {
                    logger.WriteLine($"Scaling EMR in {rdt.RdtId}");
                    builder.EMR = builder.EMR.Scale(scale);
                    rdt.RdtFile = builder.ToRdt();
                }
            }

            logger.WriteHeading($"Scaling EMRs in EMDs");
            var enemiesToScale = new byte[] {
                Re1EnemyIds.Zombie,
                Re1EnemyIds.ZombieNaked,
                Re1EnemyIds.Cerberus,
                Re1EnemyIds.WebSpinner,
                Re1EnemyIds.BlackTiger,
                Re1EnemyIds.Crow,
                Re1EnemyIds.Hunter,
                Re1EnemyIds.Wasp,
                Re1EnemyIds.Plant42,
                Re1EnemyIds.Chimera,
                Re1EnemyIds.Snake,
                Re1EnemyIds.Neptune,
                Re1EnemyIds.Tyrant1,
                Re1EnemyIds.Yawn1,
                Re1EnemyIds.Plant42Roots,
                Re1EnemyIds.Plant42Vines,
                Re1EnemyIds.Tyrant2,
                Re1EnemyIds.ZombieResearcher,
                Re1EnemyIds.Yawn2
            };
            foreach (var emId in enemiesToScale)
            {
                logger.WriteLine($"Scaling EMR in EM1{config.Player}{emId:X2}");
                var emdPath = $"ENEMY/EM1{config.Player}{emId:X2}.EMD";
                var oldEmdPath = fileRepository.GetDataPath(emdPath);
                var newEmdPath = fileRepository.GetModPath(emdPath);
                var emdFile = fileRepository.Exists(newEmdPath) ?
                    new EmdFile(BioVersion.Biohazard1, newEmdPath) :
                    new EmdFile(BioVersion.Biohazard1, oldEmdPath);

                emdFile.SetEmr(0, emdFile.GetEmr(0).Scale(scale));
                emdFile.Save(newEmdPath);
            }
        }

        private void FixRoomSounds(FileRepository fileRepository)
        {
            var bgmTable = DataManager.GetPath(BiohazardVersion, "bgm_tbl.xml");
            DataManager.Export(bgmTable, fileRepository.GetModPath("bgm_tbl.xml"));

            var xml = DataManager.GetText(BiohazardVersion, "sounds.xml");
            var doc = new XmlDocument();
            doc.LoadXml(xml);

            var roomNodes = doc.SelectNodes("Rooms/Room");
            foreach (XmlNode roomNode in roomNodes)
            {
                var idAttribute = roomNode.Attributes["id"];
                if (idAttribute == null)
                    continue;

                if (!RdtId.TryParse(idAttribute.Value, out var roomId))
                    continue;

                var enemyType = ((Re1EnemyHelper)EnemyHelper).GetEnemyInRdt(roomId);
                FixRoomSounds(fileRepository, roomId, enemyType, roomNode);
            }
        }

        private void FixRoomSounds(FileRepository fileRepository, RdtId rdtId, byte? enemyType, XmlNode roomNode)
        {
            var xmlPath = fileRepository.GetModPath($"tables/room_{rdtId}.xml");
            if (enemyType != null)
            {
                var template = GetTemplateXml(enemyType.Value);
                var entryNodes = roomNode.SelectNodes("Sound/Entry");
                for (int i = 0; i < 16; i++)
                {
                    entryNodes[i].InnerText = template[i] ?? "";
                }
            }

            var xml = roomNode.InnerXml;
            Directory.CreateDirectory(Path.GetDirectoryName(xmlPath));
            File.WriteAllText(xmlPath, xml);
        }

        private string[] GetTemplateXml(byte enemyType)
        {
            string[]? result = null;
            switch (enemyType)
            {
                case Re1EnemyIds.Zombie:
                    result = new[] { "z_taore", "z_ftL", "z_ftR", "z_kamu", "z_k02", "z_k01", "z_head", "z_haki", "z_sanj", "z_k03" };
                    break;
                case Re1EnemyIds.ZombieNaked:
                    result = new[] { "z_taore", "zep_ftL", "z_ftR", "ze_kamu", "z_nisi2", "z_nisi1", "ze_head", "ze_haki", "ze_sanj", "z_nisi3", "FL_walk", "FL_jump", "steam_b", "FL_ceil", "FL_fall", "FL_slash" };
                    break;
                case Re1EnemyIds.Cerberus:
                    result = new[] { "cer_foot", "cer_taoA", "cer_unar", "cer_bite", "cer_cryA", "cer_taoB", "cer_jkMX", "cer_kamu", "cer_cryB", "cer_runMX" };
                    break;
                case Re1EnemyIds.WebSpinner:
                    result = new[] { "kuasi_A", "kuasi_B", "kuasi_C", "sp_rakk", "sp_atck", "sp_bomb", "sp_fumu", "sp_Doku", "sp_sanj2" };
                    break;
                case Re1EnemyIds.BlackTiger:
                    result = new[] { "kuasi_A", "kuasi_B", "kuasi_C", "sp_rakk", "sp_atck", "sp_bomb", "sp_fumu", "sp_Doku", "poison" };
                    break;
                case Re1EnemyIds.Crow:
                    result = new[] { "RVcar1", "RVpat", "RVcar2", "RVwing1", "RVwing2", "RVfryed" };
                    break;
                case Re1EnemyIds.Hunter:
                    result = new[] { "HU_walkA", "HU_walkB", "HU_jump", "HU_att", "HU_land", "HU_smash", "HU_dam", "HU_Nout" };
                    break;
                case Re1EnemyIds.Wasp:
                    result = new[] { "bee4_ed", "hatinage", "bee_fumu" };
                    break;
                case Re1EnemyIds.Plant42:
                    break;
                case Re1EnemyIds.Chimera:
                    result = new[] { "FL_walk", "FL_jump", "steam_b", "FL_ceil", "FL_fall", "FL_slash", "FL_att", "FL_dam", "FL_out" };
                    break;
                case Re1EnemyIds.Snake:
                    result = new[] { "PY_mena", "PY_hit2", "PY_fall" };
                    break;
                case Re1EnemyIds.Neptune:
                    result = new[] { "nep_attB", "nep_attA", "nep_nomu", "nep_tura", "nep_twis", "nep_jump" };
                    break;
                case Re1EnemyIds.Tyrant1:
                    result = new[] { "TY_foot", "TY_kaze", "TY_slice", "TY_HIT", "TY_trust", "", "TY_taore", "TY_nage" };
                    break;
                case Re1EnemyIds.Yawn1:
                    break;
                case Re1EnemyIds.Plant42Roots:
                    break;
                case Re1EnemyIds.Plant42Vines:
                    break;
                case Re1EnemyIds.Tyrant2:
                    break;
                case Re1EnemyIds.ZombieResearcher:
                    result = new[] { "z_taore", "z_ftL", "z_ftR", "z_kamu", "z_mika02", "z_mika01", "z_head", "z_Hkick", "z_Ugoron", "z_mika03" };
                    break;
                case Re1EnemyIds.Yawn2:
                    break;
            }
            Array.Resize(ref result, 16);
            return result;
        }

        private void DisableDemo()
        {
            var pw = new PatchWriter(ExePatch);
            pw.Begin(0x48E031);
            pw.Write(0x90);
            pw.Write(0x90);
            pw.Write(0x90);
            pw.Write(0x90);
            pw.Write(0x90);
            pw.Write(0x90);
            pw.Write(0x90);
            pw.End();
        }

        private void FixFlamethrowerCombine()
        {
            var pw = new PatchWriter(ExePatch);

            // and bx, 0x7F -> nop
            pw.Begin(0x4483BD);
            pw.Write(0x90);
            pw.Write(0x90);
            pw.Write(0x90);
            pw.Write(0x90);
            pw.End();

            // and bx, 0x7F -> nop
            pw.Begin(0x44842D);
            pw.Write(0x90);
            pw.Write(0x90);
            pw.Write(0x90);
            pw.Write(0x90);
            pw.End();
        }

        private void FixWasteHeal()
        {
            // Allow using heal items when health is at max
            var pw = new PatchWriter(ExePatch);

            // jge 0447AA2h -> nop
            pw.Begin(0x447A39);
            pw.Write(0x90);
            pw.Write(0x90);
            pw.End();
        }

        private void FixNeptuneDamage()
        {
            var pw = new PatchWriter(ExePatch);

            // Neptune has no death routine, so replace it with Cerberus's
            // 0x4AA0EC -> 0x004596D0
            pw.Begin(0x4AA0EC);
            pw.Write32(0x004596D0);
            pw.End();

            // Give Neptune a damage value for each weapon
            const int numWeapons = 10;
            const int entrySize = 12;
            var damageValues = new short[] { 16, 14, 32, 40, 130, 20, 100, 200, 100, 900 };
            var enemyDataArrays = new uint[] { 0x4AF908U, 0x4B0268 };
            foreach (var enemyData in enemyDataArrays)
            {
                var neptuneData = enemyData + (Re1EnemyIds.Neptune * (numWeapons * entrySize)) + 0x06;
                for (var i = 0; i < numWeapons; i++)
                {
                    pw.Begin(neptuneData);
                    pw.Write16(damageValues[i]);
                    pw.End();
                    neptuneData += entrySize;
                }
            }
        }

        private void FixChrisInventorySize()
        {
            var pw = new PatchWriter(ExePatch);

            // Inventory instructions
            var addresses = new uint[]
            {
                0x40B461,
                0x40B476,
                0x40B483,
                0x414103,
                0x414022,
                0x4142CC
            };
            foreach (var addr in addresses)
            {
                pw.Begin(addr);
                pw.Write(0xB0);
                pw.Write(0x01);
                pw.Write(0x90);
                pw.Write(0x90);
                pw.Write(0x90);
                pw.End();
            }

            // Partner swap
            pw.Begin(0x0041B208);
            pw.Write(0xC7);
            pw.Write(0x05);
            pw.Write32(0x00AA8E48);
            pw.Write32(0x00C38814);
            pw.End();

            // Rebirth
            pw.Begin(0x100505A3);
            pw.Write(0xB8);
            pw.Write(0x01);
            pw.Write(0x00);
            pw.Write(0x00);
            pw.Write(0x00);
            pw.Write(0x90);
            pw.Write(0x90);
            pw.End();

            pw.Begin(0x1006F0C2 + 3);
            pw.Write(0x8);
            pw.End();
        }

        private void FixWeaponHitScan(RandoConfig config)
        {
            var table = new short[]
            {
                -2026, -1656, -2530, -2280, -2040, -1800,
                -1917, -1617, -2190, -1940, -2003, -1720
            };

            var numValues = table.Length / 2;
            if (config.ChangePlayer && config.SwapCharacters)
            {
                // Swap Chris and Jills values around
                for (var i = 0; i < numValues; i++)
                {
                    (table[i], table[i + numValues]) = (table[i + numValues], table[i]);
                }
            }

            for (var i = 0; i < 2; i++)
            {
                var pldPath = GetSelectedPldPath(config, i);
                var csvPath = Path.Combine(pldPath, "weapons.csv");
                if (!File.Exists(csvPath))
                    continue;

                var csv = File.ReadAllLines(csvPath)
                    .Where(x => !string.IsNullOrWhiteSpace(x))
                    .Select(x => x.Trim().Split(','))
                    .ToArray();

                var offset = i * numValues;
                for (var j = 0; j < numValues; j++)
                {
                    table[offset + j] = short.Parse(csv[j][0]);
                }
            }

            var pw = new PatchWriter(ExePatch);
            pw.Begin(0x4AAD98);
            var data = MemoryMarshal.Cast<short, byte>(new Span<short>(table));
            foreach (var d in data)
            {
                pw.Write(d);
            }
            pw.End();
        }

        private void FixYawnPoison(RandoConfig config)
        {
            const byte ST_POISON = 0x02;
            const byte ST_POISON_YAWN = 0x20;

            var pw = new PatchWriter(ExePatch);
            pw.Begin(0x45B8C0 + 6); // 80 0D 90 52 C3 00 20
            if (config.RandomDoors)
                pw.Write(ST_POISON);
            else
                pw.Write(ST_POISON_YAWN);
            pw.End();
        }
    }
}
