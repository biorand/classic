using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Threading;
using IntelOrca.Biohazard.BioRand.Events;
using IntelOrca.Biohazard.BioRand.RE1;
using IntelOrca.Biohazard.BioRand.RE2;
using IntelOrca.Biohazard.BioRand.RE3;

namespace IntelOrca.Biohazard.BioRand
{
    public abstract class BaseRandomiser
    {
        protected ReInstallConfig InstallConfig { get; }
        protected IBgCreator? BgCreator { get; }

        protected MemoryStream ExePatch { get; } = new MemoryStream();
        internal List<RandomInventory?> Inventories { get; } = new List<RandomInventory?>();

        protected abstract BioVersion BiohazardVersion { get; }
        internal abstract IDoorHelper DoorHelper { get; }
        internal abstract IItemHelper ItemHelper { get; }
        internal abstract IEnemyHelper EnemyHelper { get; }
        internal abstract INpcHelper NpcHelper { get; }

        public abstract bool ValidateGamePath(string path);

        protected abstract string GetDataPath(string installPath);
        protected abstract RdtId[] GetRdtIds(string dataPath);
        protected abstract string GetRdtPath(string dataPath, RdtId rdtId, int player, bool mod);

        public BaseRandomiser(ReInstallConfig installConfig, IBgCreator? bgCreator)
        {
            InstallConfig = installConfig;
            BgCreator = bgCreator;
        }

        internal DataManager DataManager
        {
            get
            {
                var dataPath = Environment.GetEnvironmentVariable("BIORAND_DATA");
                if (string.IsNullOrEmpty(dataPath))
                {
                    var assemblyDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
                    var basePath = assemblyDir;
#if DEBUG
                    basePath = Path.GetFullPath(Path.Combine(assemblyDir, "..\\..\\..\\..\\IntelOrca.Biohazard.BioRand"));
#endif
                    dataPath = Path.Combine(basePath, "data");
                }

                var dataPaths = new List<string>();
                if (InstallConfig.EnableCustomContent)
                {
                    var userDataPath = Path.Combine(GetSettingsDirectory(), "data");
                    dataPaths.Add(userDataPath);
                }
                dataPaths.Add(dataPath);
                return new DataManager(dataPaths.ToArray());
            }
        }

        private static string GetSettingsDirectory()
        {
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            return Path.Combine(appData, "biorand");
        }

        private void SetInventory(int player, RandomInventory? inventory)
        {
            if (inventory == null)
                return;

            lock (Inventories)
            {
                while (Inventories.Count <= player)
                {
                    Inventories.Add(null);
                }
                Inventories[player] = inventory;
            }
        }

        public int DoIntegrityCheck(string installPath)
        {
            var result = 0;
            var dataPath = GetDataPath(installPath);
            var rdtIds = GetRdtIds(dataPath);
            var numPlayers = BiohazardVersion == BioVersion.Biohazard3 ? 1 : 2;
            for (int player = 0; player < numPlayers; player++)
            {
                var actualChecksums = GetRdtChecksums(dataPath, player, rdtIds);
                var expectedChecksums = GetRdtChecksums(player);
                if (expectedChecksums == null)
                    continue;

                foreach (var kvp in expectedChecksums)
                {
                    if (actualChecksums.TryGetValue(kvp.Key, out var actualChecksum))
                    {
                        if (kvp.Value != actualChecksum)
                        {
                            result = 1;
                        }
                    }
                    else
                    {
                        return 2;
                    }
                }
            }

            if (Directory.Exists(Path.Combine(dataPath, "bio1dc")))
                return 3;
            if (Directory.Exists(Path.Combine(dataPath, "..", "bio1dc")))
                return 3;

            return result;
        }

        private Dictionary<RdtId, ulong>? GetRdtChecksums(int player)
        {
            var path = DataManager.GetPath(BiohazardVersion, "checksum.json");
            if (File.Exists(path))
            {
                var checksumJson = File.ReadAllBytes(path);
                var checksumsForEachPlayer = JsonSerializer.Deserialize<Dictionary<string, ulong>[]>(checksumJson)!;
                var checksums = checksumsForEachPlayer[player];
                return checksums.ToDictionary(x => RdtId.Parse(x.Key), x => x.Value);
            }
            return null;
        }

        private Dictionary<RdtId, ulong> GetRdtChecksums(string dataPath, int player, RdtId[] rdtIds)
        {
            var fileRepo = new FileRepository(dataPath);
            if (this is Re3Randomiser re3randomizer)
                re3randomizer.AddArchives(dataPath, fileRepo);
            var result = new Dictionary<RdtId, ulong>();
            foreach (var rdtId in rdtIds)
            {
                var rdtPath = GetRdtPath(dataPath, rdtId, player, false);
                try
                {
                    if (fileRepo.Exists(rdtPath))
                    {
                        result[rdtId] = fileRepo.GetBytes(rdtPath).CalculateFnv1a();
                    }
                }
                catch
                {
                }
            }
            return result;
        }

        protected virtual RandomizedRdt ReadRdt(FileRepository fileRepository, RdtId rdtId, string path, string modPath)
        {
            var rdtBytes = fileRepository.GetBytes(path);
            var rdt = GameDataReader.ReadRdt(BiohazardVersion, rdtId, rdtBytes, path, modPath);
            return rdt;
        }

        private GameData ReadGameData(FileRepository fileRepository, int player, RdtId[] rdtIds, bool mod)
        {
            var rdts = new List<RandomizedRdt>();
            foreach (var rdtId in rdtIds)
            {
                var rdtPath = GetRdtPath(fileRepository.DataPath, rdtId, player, false);
                var modRdtPath = GetRdtPath(fileRepository.ModPath, rdtId, player, true);
                try
                {
                    var srcPath = mod ? modRdtPath : rdtPath;
                    var rdt = ReadRdt(fileRepository, rdtId, srcPath, modRdtPath);
                    rdts.Add(rdt);
                }
                catch
                {
                }
            }
            return new GameData(rdts.ToArray());
        }

        public void Generate(RandoConfig config, IRandoProgress progress)
        {
            config = config.Clone();
            if (config.Version < RandoConfig.LatestVersion)
            {
                throw new BioRandVersionException($"This seed was generated with an older version of the randomizer and cannot be played.");
            }
            else if (config.Version != RandoConfig.LatestVersion)
            {
                throw new BioRandVersionException($"This seed was generated with a newer version of the randomizer and cannot be played.");
            }

            var reConfig = InstallConfig;
            var installPath = reConfig.GetInstallPath(BiohazardVersion);
            if (BiohazardVersion == BioVersion.BiohazardCv)
            {
                installPath = Path.GetDirectoryName(installPath);
            }

            var originalDataPath = GetDataPath(installPath);
            var modPath = Path.Combine(installPath, @"mod_biorand");
            var fileRepo = new FileRepository(originalDataPath, modPath);
            if (reConfig!.IsEnabled(BioVersion.Biohazard3))
            {
                var re3randomizer = new Re3Randomiser(reConfig, null);
                var dataPath = re3randomizer.GetDataPath(reConfig.GetInstallPath(BioVersion.Biohazard3));
                re3randomizer.AddArchives(dataPath, fileRepo);
            }

            using (progress.BeginTask(null, $"Clearing '{modPath}'"))
            {
                if (Directory.Exists(modPath))
                {
                    try
                    {
                        Directory.Delete(modPath, true);
                    }
                    catch
                    {
                    }
                }
                Directory.CreateDirectory(modPath);
            }

            using (progress.BeginTask(config.Player, $"Generating seed"))
            {
                PreGenerate(config);
                Generate(config, progress, fileRepo);
            }

            using (progress.BeginTask(null, $"Writing manifest"))
            {
                File.WriteAllText(Path.Combine(modPath, "manifest.txt"), "[MOD]\nName = BioRand: A Resident Evil Randomizer\nModule = biorand.dll\n");
                File.WriteAllText(Path.Combine(modPath, "description.txt"), $"{Program.CurrentVersionInfo}\r\nSeed: {config}\r\n");
            }
        }

        private void PreGenerate(RandoConfig config)
        {
            // Randomize player characters
            if (config.ChangePlayer)
            {
                var rng = new Rng(config.Seed);
                if (config.Player0 == 0)
                {
                    var length = GetPlayerCharacters(0).Length;
                    config.Player0 = (byte)(1 + rng.Next(0, length));
                }
                if (config.Player1 == 0)
                {
                    var length = GetPlayerCharacters(1).Length;
                    config.Player1 = (byte)(1 + rng.Next(0, length));
                }
            }
        }

        public virtual void Generate(RandoConfig config, IRandoProgress progress, FileRepository fileRepository)
        {
            var reConfig = InstallConfig;
            if (config.RandomItems && config.RandomInventory && !config.ShuffleItems)
            {
                SerialiseInventory(fileRepository);
            }

            if (config.RandomBgm)
            {
                GenerateBGM(config, reConfig, progress, fileRepository);
            }

            using (progress.BeginTask(null, $"Creating backgrounds"))
                CreateBackgrounds(config, fileRepository);
            if (reConfig.RandomizeTitleVoice)
                using (progress.BeginTask(null, $"Copying title card sounds"))
                    CreateTitleCardSounds(config, fileRepository);
            using (progress.BeginTask(null, $"Copying biorand.dll"))
                CreateDataModule(fileRepository);
        }

        public void GenerateRdts(RandoConfig config, IRandoProgress progress, FileRepository fileRepository, CancellationToken ct = default)
        {
            var baseSeed = config.Seed + config.Player;
            var randomDoors = new Rng(baseSeed + 1);
            var randomItems = new Rng(baseSeed + 2);
            var randomEnemies = new Rng(baseSeed + 3);
            var randomNpcs = new Rng(baseSeed + 4);
            var randomVoices = new Rng(baseSeed + 5);
            var randomCutscenes = new Rng(baseSeed + 6);

            // In RE1, room sounds are not in the RDT, so enemies must be same for both players
            if (BiohazardVersion == BioVersion.Biohazard1)
                randomEnemies = new Rng(config.Seed + 3);

            using var logger = new RandoLogger(progress, fileRepository.GetModPath($"log_pl{config.Player}.txt"));
            logger.WriteHeading(Program.CurrentVersionInfo);
            logger.WriteLine($"Seed: {config}");
            logger.WriteLine($"Player: {config.Player} {GetPlayerName(config.Player)}");
            logger.WriteLine($"Scenario: {GetScenarioName(config.Scenario)}");

            try
            {
                var rdtIds = GetRdtIds(fileRepository.DataPath);
                var gameData = ReadGameData(fileRepository, config.Player, rdtIds, mod: false);

#if DEBUG
                using (progress.BeginTask(config.Player, "Dumping original RDT disassemblies"))
                {
                    DumpScripts(config, gameData, fileRepository.GetModPath($"scripts_pl{config.Player}"));
                }
#endif

                var map = GetMapFromJson();
                var graph = null as PlayGraph;
                ItemRandomiser? itemRandomiser = null;
                if (config.RandomDoors || config.RandomItems)
                {
                    var dgmlPath = fileRepository.GetModPath($"graph_pl{config.Player}.dgml");
                    var doorRando = new DoorRandomiser(logger, config, gameData, map, randomDoors, DoorHelper, ItemHelper);
                    itemRandomiser = new ItemRandomiser(logger, config, gameData, map, randomItems, ItemHelper);
                    using (progress.BeginTask(config.Player, "Randomizing doors"))
                    {
                        graph = config.RandomDoors ?
                            doorRando.CreateRandomGraph() :
                            doorRando.CreateOriginalGraph();
                    }

                    var sss = JsonSerializer.Serialize(map, new JsonSerializerOptions()
                    {
                        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
                        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                        WriteIndented = true
                    });
                    File.WriteAllText(@"M:\git\rer\IntelOrca.Biohazard.BioRand\data\re2\rdt.a1.corrected.json", sss);
                    try
                    {
                        using (progress.BeginTask(config.Player, "Randomizing items"))
                        {
                            itemRandomiser.RandomiseItems(graph);
                        }
                        if (config.RandomInventory && !config.ShuffleItems)
                        {
                            SetInventory(config.Player, itemRandomiser.RandomizeStartInventory());
                        }
                    }
                    finally
                    {
                        graph.GenerateDgml(dgmlPath, ItemHelper);
                    }
                }

                string[]? playerActors;
                using (progress.BeginTask(config.Player, $"Changing player character"))
                    playerActors = ChangePlayerCharacters(config, logger, gameData, fileRepository);

                VoiceRandomiser? voiceRandomiser = null;
                if (config.RandomNPCs || config.RandomCutscenes || config.RandomEvents)
                {
                    voiceRandomiser = new VoiceRandomiser(
                        BiohazardVersion,
                        logger,
                        fileRepository,
                        config,
                        fileRepository.DataPath,
                        fileRepository.ModPath,
                        gameData,
                        map,
                        randomVoices,
                        NpcHelper,
                        DataManager,
                        playerActors);
                }

                NPCRandomiser? npcRandomiser = null;
                if (config.RandomNPCs)
                {
                    using (progress.BeginTask(config.Player, "Randomizing NPCs"))
                    {
                        npcRandomiser = new NPCRandomiser(
                            BiohazardVersion,
                            logger,
                            fileRepository,
                            config,
                            fileRepository.ModPath,
                            gameData,
                            map,
                            randomNpcs,
                            NpcHelper,
                            DataManager,
                            playerActors,
                            voiceRandomiser!);
                        var selectedActors = GetSelectedActors(config);
                        if (selectedActors.Length == 0)
                        {
                            throw new BioRandUserException("No NPCs selected.");
                        }
                        npcRandomiser.SelectedActors.AddRange(selectedActors);
                        RandomizeNPCs(config, npcRandomiser, voiceRandomiser!);
                        npcRandomiser.Randomise();
                    }
                }

                if (config.RandomCutscenes)
                {
                    voiceRandomiser!.Randomise();
                }

                EnemyRandomiser? enemyRandomiser = null;
                if (config.RandomEnemies)
                {
                    using (progress.BeginTask(config.Player, "Randomizing enemies"))
                    {
                        enemyRandomiser = new EnemyRandomiser(BiohazardVersion, logger, config, gameData, map, randomEnemies, EnemyHelper, DataManager);
                        enemyRandomiser.Randomise(graph, ct);
                    }
                }

#if DEBUG
                if (config.RandomEvents)
#else
                if (config.RandomCutscenes && config.RandomEvents)
#endif
                {
                    if (BiohazardVersion != BioVersion.Biohazard2)
                    {
                        throw new BioRandUserException("Random events are only supported for RE 2.");
                    }

                    var cutscene = new CutsceneRandomiser(
                        logger,
                        DataManager,
                        config,
                        gameData,
                        map,
                        randomCutscenes,
                        itemRandomiser,
                        enemyRandomiser,
                        npcRandomiser,
                        config.RandomCutscenes ? voiceRandomiser : null);
                    cutscene.Randomise(graph);
                }

                if (enemyRandomiser != null)
                {
                    using (progress.BeginTask(config.Player, "Replacing enemies"))
                    {
                        enemyRandomiser.Apply();
                    }
                }

                if (config.RandomEnemySkins)
                {
                    using (progress.BeginTask(config.Player, "Randomizing enemy skins"))
                    {
                        RandomizeEnemySkins(config, logger, gameData, fileRepository);
                    }
                }

                if (config.RandomCutscenes)
                {
                    voiceRandomiser!.SetVoices();
                }

                // Copy EMD to EMD08
                {
                    var emd = fileRepository.GetModPath("ROOM/EMD");
                    var emd08 = fileRepository.GetModPath("ROOM/EMD08");
                    if (Directory.Exists(emd))
                    {
                        fileRepository.CopyDirectory(emd, emd08);
                    }
                }

                PostGenerate(config, progress, fileRepository, gameData);

                using (progress.BeginTask(config.Player, "Writing RDT files"))
                {
                    foreach (var rdt in gameData.Rdts)
                    {
                        rdt.Save();
                    }
                }

#if DEBUG
                using (progress.BeginTask(config.Player, "Dumping modified RDT disassemblies"))
                {
                    var moddedGameData = ReadGameData(fileRepository, config.Player, rdtIds, mod: true);
                    DumpScripts(config, moddedGameData, fileRepository.GetModPath($"scripts_modded_pl{config.Player}"));
                }
#endif
            }
            catch (Exception ex)
            {
                logger.WriteException(ex);
                throw;
            }
        }

        protected virtual void PostGenerate(RandoConfig config, IRandoProgress progress, FileRepository fileRepository, GameData gameData)
        {
        }

        protected virtual void GenerateBGM(RandoConfig config, ReInstallConfig reConfig, IRandoProgress progress, FileRepository fileRepository)
        {
            using var logger = new RandoLogger(progress, fileRepository.GetModPath("log_bgm.txt"));
            logger.WriteHeading(Program.CurrentVersionInfo);
            logger.WriteLine($"Seed: {config}");

            var bgmDirectory = fileRepository.GetModPath(BGMPath);
            var bgmRandomizer = new BgmRandomiser(logger, config, fileRepository, bgmDirectory, GetBgmJson(), BiohazardVersion != BioVersion.Biohazard2, new Rng(config.Seed), DataManager);
            var enabledBgms = GetSelectedAlbums(config);
            if (enabledBgms.Length == 0)
            {
                throw new BioRandUserException("No music albums selected.");
            }
            if (enabledBgms.Contains("RE1", StringComparer.OrdinalIgnoreCase))
            {
                var r = new Re1Randomiser(reConfig, BgCreator);
                r.AddMusicSelection(bgmRandomizer, reConfig, 1.0);
            }
            if (enabledBgms.Contains("RE2", StringComparer.OrdinalIgnoreCase))
            {
                var r = new Re2Randomiser(reConfig, BgCreator);
                r.AddMusicSelection(bgmRandomizer, reConfig, 1.0);
            }
            if (enabledBgms.Contains("RE3", StringComparer.OrdinalIgnoreCase))
            {
                var r = new Re3Randomiser(reConfig, BgCreator);
                r.AddMusicSelection(bgmRandomizer, reConfig, 0.75);
            }

            if (BiohazardVersion == BioVersion.Biohazard1)
                bgmRandomizer.ImportVolume = 0.75f;
            if (BiohazardVersion == BioVersion.Biohazard3)
                bgmRandomizer.ImportVolume = 0.5f;
            bgmRandomizer.AddCutomMusicToSelection(enabledBgms);

            using (progress.BeginTask(null, "Randomizing BGM"))
            {
                bgmRandomizer.Randomise();
            }
        }

        protected virtual string[] TitleCardSoundFiles { get; } = new string[0];

        internal virtual string[]? ChangePlayerCharacters(RandoConfig config, RandoLogger logger, GameData gameData, FileRepository fileRepository)
        {
            return null;
        }

        internal virtual void RandomizeEnemySkins(RandoConfig config, RandoLogger logger, GameData gameData, FileRepository fileRepository)
        {
        }

        internal virtual void RandomizeNPCs(RandoConfig config, NPCRandomiser npcRandomiser, VoiceRandomiser voiceRandomiser)
        {
        }

        public abstract string GetPlayerName(int player);
        private string GetScenarioName(int scenario) => scenario == 0 ? "A" : "B";

        private Map GetMapFromJson()
        {
            var jsonMap = GetJsonMap();
            var map = JsonSerializer.Deserialize<Map>(jsonMap, new JsonSerializerOptions()
            {
                ReadCommentHandling = JsonCommentHandling.Skip,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            })!;
            return map;
        }

        private string GetJsonMap()
        {
            var path = DataManager.GetPath(BiohazardVersion, "rdt.json");
            return File.ReadAllText(path);
        }

        internal string GetBgmJson() => DataManager.GetText(BiohazardVersion, "bgm.json");

        internal abstract string BGMPath { get; }

        private void DumpRdtList(GameData gameData, string path)
        {
            var sb = new StringBuilder();
            foreach (var rdt in gameData.Rdts)
            {
                sb.AppendLine($"{rdt.RdtId} ():");
                Script.AstPrinter.Print(sb, rdt.Ast!);
            }
            File.WriteAllText(path, sb.ToString());
        }

        private void DumpRdt(StringBuilder sb, RandomizedRdt rdt)
        {
            foreach (var door in rdt.Doors)
            {
                sb.AppendLine($"    Door #{door.Id}: {new RdtId(door.NextStage, door.NextRoom)} (0x{door.Offset:X2})");
            }
            foreach (var item in rdt.Items)
            {
                sb.AppendLine($"    Item #{item.Id}: {item.Type} x{item.Amount} (0x{item.Offset:X2})");
            }
            foreach (var enemy in rdt.Enemies)
            {
                sb.AppendLine($"    Enemy #{enemy.Id}: {enemy.Type} (0x{enemy.Offset:X2})");
            }
        }

        private void DumpScripts(RandoConfig config, GameData gameData, string scriptPath)
        {
            Directory.CreateDirectory(scriptPath);
            foreach (var rdt in gameData.Rdts)
            {
                File.WriteAllText(Path.Combine(scriptPath, $"{rdt.RdtId}.bio"), rdt.Script);
                File.WriteAllText(Path.Combine(scriptPath, $"{rdt.RdtId}.s"), rdt.ScriptDisassembly);
                File.WriteAllText(Path.Combine(scriptPath, $"{rdt.RdtId}.lst"), rdt.ScriptListing);
            }

            // var player = config.Player;
            // var options = new JsonSerializerOptions()
            // {
            //     ReadCommentHandling = JsonCommentHandling.Skip
            // };
            // var old = JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(@"M:\git\rer\IntelOrca.Biohazard\data\voice.json"), options)!;
            // var sb = new StringBuilder();
            // var done = new HashSet<string>();
            // foreach (var rdt in gameData.Rdts)
            // {
            //     foreach (var sound in rdt.Sounds)
            //     {
            //         var stage = rdt.RdtId.Stage;
            //         var id = sound.Id;
            //         var path = $"pl{player}/voice/stage{stage + 1}/v{id:000}.sap";
            //         if (done.Add(path) && old.TryGetValue(path, out var value))
            //         {
            //             var actor = value;
            //             sb.AppendLine($"\"{path}\": {{ \"rdt\": \"{rdt.RdtId}\", \"player\": {player}, \"actor\": \"{actor}\" }},");
            //         }
            //     }
            // }
        }

        protected virtual void SerialiseInventory(FileRepository fileRepository)
        {
        }

        public virtual string[] GetPlayerCharacters(int index)
        {
            var result = new HashSet<string>();
            var pldFiles = DataManager
                .GetDirectories(BiohazardVersion, $"pld{index}")
                .ToArray();
            foreach (var pldPath in pldFiles)
            {
                var actor = Path.GetFileName(pldPath);
                result.Add(actor.ToActorString());
            }
            return result.OrderBy(x => x).ToArray();
        }

        protected string GetSelectedActor(RandoConfig config)
        {
            return GetSelectedActor(config, config.Player);
        }

        protected string GetSelectedActor(RandoConfig config, int player)
        {
            if (!config.ChangePlayer)
                return GetPlayerName(player).ToLower();

            var pldPath = GetSelectedPldPath(config, player);
            return Path.GetFileName(pldPath).ToLower();
        }

        protected virtual string GetSelectedPldPath(RandoConfig config, int player)
        {
            var pldIndex = GetSelectedPldIndex(config, player);
            var pldDirectoryIndex = config.SwapCharacters ? player ^ 1 : player;
            var pldPath = DataManager.GetDirectories(BiohazardVersion, $"pld{pldDirectoryIndex}")
                .OrderBy(x => Path.GetFileName(x).ToActorString())
                .Skip(pldIndex)
                .FirstOrDefault();
            return pldPath;
        }

        protected int GetSelectedPldIndex(RandoConfig config, int player)
        {
            var player0 = config.SwapCharacters ? config.Player1 : config.Player0;
            var player1 = config.SwapCharacters ? config.Player0 : config.Player1;
            var pldIndex = (player == 0 ? player0 : player1) - 1;
            return pldIndex;
        }

        public virtual string[] GetWeaponNames() => ItemHelper.GetWeaponNames();

        public virtual EnemySkin[] GetEnemySkins()
        {
            return new EnemySkin[0];
        }

        public virtual string[] GetNPCs()
        {
            var actors = new HashSet<string>();
            actors.AddRange(GetDefaultNPCs());
            for (int i = 0; i < 2; i++)
            {
                var plds = DataManager
                    .GetDirectories(BiohazardVersion, $"pld{i}")
                    .Select(Path.GetFileName)
                    .ToArray();
                actors.AddRange(plds);
            }
            return actors
                .OrderBy(x => x)
                .ToArray();
        }

        protected virtual string[] GetDefaultNPCs()
        {
            return new[] { "chris", "jill", "barry", "rebecca", "wesker", "enrico", "richard" };
        }

        protected bool MusicAlbumSelected(RandoConfig config, string album)
        {
            var albumIndex = Array.FindIndex(GetMusicAlbums(), x => x.Equals(album, StringComparison.OrdinalIgnoreCase));
            if (albumIndex > -1 && albumIndex < config.EnabledBGMs.Length)
            {
                return config.EnabledBGMs[albumIndex];
            }
            return false;
        }

        public virtual string[] GetMusicAlbums()
        {
            var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "re1",
                "re2",
                "re3"
            };
            result.AddRange(DataManager
                .GetDirectories("bgm")
                .Select(Path.GetFileName));
            return result
                .Select(x => x.ToUpper())
                .OrderBy(x => x)
                .ToArray();
        }

        private string[] GetSelectedActors(RandoConfig config)
        {
            var enabledNpcs = config.EnabledNPCs;
            var npcs = GetNPCs();
            for (int i = 0; i < npcs.Length; i++)
            {
                if (enabledNpcs.Length <= i || !enabledNpcs[i])
                {
                    npcs[i] = "";
                }
            }
            return npcs
                .Where(x => !string.IsNullOrEmpty(x))
                .ToArray();
        }

        private string[] GetSelectedAlbums(RandoConfig config)
        {
            var enabledBgms = config.EnabledBGMs;
            var albums = GetMusicAlbums();
            for (int i = 0; i < albums.Length; i++)
            {
                if (enabledBgms.Length <= i || !enabledBgms[i])
                {
                    albums[i] = "";
                }
            }
            return albums
                .Where(x => !string.IsNullOrEmpty(x))
                .ToArray();
        }

        public SelectableEnemy[] GetEnemies()
        {
            return EnemyHelper.GetSelectableEnemies();
        }

        private void CreateBackgrounds(RandoConfig config, FileRepository fileRepository)
        {
            try
            {
                if (BiohazardVersion == BioVersion.Biohazard1)
                {
                    var src = DataManager.GetData(BiohazardVersion, "bg.png");
                    CreateBackgroundRaw(config, src, fileRepository.GetModPath("data/title.pix"));
                    CreateBackgroundPng(config, src, fileRepository.GetModPath("type.png"));
                }
                else if (BiohazardVersion == BioVersion.Biohazard2)
                {
                    var src = DataManager.GetData(BiohazardVersion, "bg.png");
                    CreateBackgroundPng(config, src, fileRepository.GetModPath("common/data/title_bg.png"));
                    CreateBackgroundPng(config, src, fileRepository.GetModPath("common/data/type00.png"));
                }
                else if (BiohazardVersion == BioVersion.Biohazard3)
                {
                    var src = DataManager.GetData(BiohazardVersion, "bg.png");
                    CreateBackgroundRaw(config, src, fileRepository.GetModPath("data/etc/type00.pix"));
                    CreateBackgroundTim(config, src, fileRepository.GetModPath("data_j/etc2/titlej.dat"));
                    CreateBackgroundTim(config, src, fileRepository.GetModPath("data_j/etc2/titletgs.dat"));
                }
            }
            catch
            {
            }
        }

        private void CreateBackgroundRaw(RandoConfig config, byte[] pngBackground, string outputFilename)
        {
            if (BgCreator == null)
                return;

            Directory.CreateDirectory(Path.GetDirectoryName(outputFilename));
            var pixels = BgCreator.CreateARGB(config, pngBackground);
            using var fs = new FileStream(outputFilename, FileMode.Create);
            var bw = new BinaryWriter(fs);

            for (int i = 0; i < 320 * 240; i++)
            {
                var c = pixels[i];
                var r = (byte)((c >> 16) & 0xFF);
                var g = (byte)((c >> 8) & 0xFF);
                var b = (byte)((c >> 0) & 0xFF);
                var c4 = (ushort)((r / 8) | ((g / 8) << 5) | ((b / 8) << 10));
                bw.Write(c4);
            }
        }

        private void CreateBackgroundPng(RandoConfig config, byte[] pngBackground, string outputFilename)
        {
            if (BgCreator == null)
                return;

            Directory.CreateDirectory(Path.GetDirectoryName(outputFilename));
            var titleBg = BgCreator.CreatePNG(config, pngBackground);
            File.WriteAllBytes(outputFilename, titleBg);
        }

        private void CreateBackgroundTim(RandoConfig config, byte[] pngBackground, string outputFilename)
        {
            if (BgCreator == null)
                return;

            Directory.CreateDirectory(Path.GetDirectoryName(outputFilename));
            var pixels = BgCreator.CreateARGB(config, pngBackground);
            using var fs = new FileStream(outputFilename, FileMode.Create);
            var bw = new BinaryWriter(fs);

            bw.Write(16);
            bw.Write(2);
            bw.Write(153612);
            bw.Write((ushort)0);
            bw.Write((ushort)0);
            bw.Write((ushort)320);
            bw.Write((ushort)240);
            for (int i = 0; i < 320 * 240; i++)
            {
                var c = pixels[i];
                var r = (byte)((c >> 16) & 0xFF);
                var g = (byte)((c >> 8) & 0xFF);
                var b = (byte)((c >> 0) & 0xFF);
                var c4 = (ushort)((r / 8) | ((g / 8) << 5) | ((b / 8) << 10));
                bw.Write(c4);
            }
        }

        private void CreateTitleCardSounds(RandoConfig config, FileRepository fileRepository)
        {
            var titleSounds = DataManager
                .GetFiles("title")
                .Where(x => x.EndsWith(".ogg", StringComparison.OrdinalIgnoreCase))
                .Where(x => !Path.GetFileName(x).Equals("template.ogg", StringComparison.OrdinalIgnoreCase))
                .ToArray();
            if (titleSounds.Length != 0)
            {
                var rng = new Rng(config.Seed);
                var titleSound = rng.NextOf(titleSounds);
                ReplaceTitleCardSound(fileRepository, titleSound);
            }
        }

        protected virtual void ReplaceTitleCardSound(FileRepository fileRepository, string sourcePath)
        {
            var builder = new WaveformBuilder();
            builder.Append(sourcePath);

            var targetTitleCardSounds = TitleCardSoundFiles;
            foreach (var filename in targetTitleCardSounds)
            {
                var dst = fileRepository.GetModPath(filename);
                Directory.CreateDirectory(Path.GetDirectoryName(dst));
                if (targetTitleCardSounds.Length != 0)
                {
                    builder.Save(dst);
                }
            }
        }

        private void CreateDataModule(FileRepository fileRepository)
        {
            var datPath = fileRepository.GetModPath("biorand.dat");
            File.WriteAllBytes(datPath, ExePatch.ToArray());
            var biorandModuleFilename = "biorand.dll";
            try
            {
                var src = DataManager.GetPath(biorandModuleFilename);
                var dst = fileRepository.GetModPath(biorandModuleFilename);
                File.Copy(src, dst, true);
            }
            catch
            {
            }
        }
    }
}
