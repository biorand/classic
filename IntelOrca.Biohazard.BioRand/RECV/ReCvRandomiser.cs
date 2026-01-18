using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using IntelOrca.Biohazard.Extensions;
using IntelOrca.Biohazard.Room;
using IntelOrca.Biohazard.Script.Opcodes;
using Ps2IsoTools.UDF;
using Ps2IsoTools.UDF.Files;

namespace IntelOrca.Biohazard.BioRand.RECV
{
    public class ReCvRandomiser : BaseRandomiser
    {
        private AfsFile? _advAfs;
        // private AfsFile? _multspq1Afs;
        private AfsFile? _rdxAfs;
        private AfsFile? _systemAfs;
        private RandomizedRdt[] _rdts = new RandomizedRdt[205];
        private byte[] _elf = new byte[0];

        protected override BioVersion BiohazardVersion => BioVersion.BiohazardCv;
        internal override IDoorHelper DoorHelper { get; } = new ReCvDoorHelper();
        internal override IItemHelper ItemHelper { get; } = new ReCvItemHelper();
        internal override IEnemyHelper EnemyHelper => new ReCvEnemyHelper();
        internal override INpcHelper NpcHelper { get; }
        internal override string BGMPath => throw new NotImplementedException();

#pragma warning disable 8618
        public ReCvRandomiser(ReInstallConfig installConfig, IBgCreator? bgCreator)
            : base(installConfig, bgCreator)
        {
        }
#pragma warning restore 8618

        protected override RdtId[] GetRdtIds(string dataPath)
        {
            return Enumerable.Range(0, _rdxFileNames.Length)
                .Select(GetRdtId)
                .ToArray();
        }

        private int GetRdxFileIndex(RdtId id)
        {
            for (var i = 0; i < _rdxFileNames.Length; i++)
            {
                var rdtId = GetRdtId(i);
                if (rdtId == id)
                    return i;
            }
            return -1;
        }

        protected override string GetDataPath(string installPath)
        {
            return Path.Combine(installPath, "data");
        }

        protected override string GetRdtPath(string dataPath, RdtId rdtId, int player, bool mod)
        {
            var rdtPath = $"R{rdtId}.RDT";
            return mod ?
                Path.Combine(dataPath, "rdx_lnk_modded", rdtPath) :
                Path.Combine(dataPath, "..", "mod_biorand", "rdx_lnk", rdtPath);
        }

        public override string GetPlayerName(int player) =>
            player switch
            {
                0 => "Claire",
                1 => "Chris",
                _ => throw new NotImplementedException(),
            };

        public override string[] GetPlayerCharacters(int index) => base.GetPlayerCharacters(0);
        protected override string GetSelectedPldPath(RandoConfig config, int player) => base.GetSelectedPldPath(config, 0);

        internal FileRepository CreateRepository(string installPath, string? modPath = null)
        {
            var repo = new FileRepository(installPath, null);
            AddArchives(installPath, repo);
            return repo;
        }

        internal void AddArchives(string installPath, FileRepository fileRepo)
        {
            var dataPath = GetDataPath(installPath);
            foreach (var path in Directory.GetFiles(dataPath, "rofs*.dat"))
            {
                fileRepo.AddRE3Archive(path);
            }
        }

        public override bool ValidateGamePath(string path)
        {
            return File.Exists(Path.Combine(path, "rofs1.dat"));
        }

        protected override RandomizedRdt ReadRdt(FileRepository fileRepository, RdtId rdtId, string path, string modPath)
        {
            var result = base.ReadRdt(fileRepository, rdtId, path, modPath);
            var fileIndex = GetRdxFileIndex(rdtId);
            if (_rdts[fileIndex] == null)
                _rdts[fileIndex] = result;
            return result;
        }

        public override void Generate(RandoConfig config, IRandoProgress progress, FileRepository fileRepository)
        {
            config.IncludeDocuments = false;
            config.SwapCharacters = false;
            config.RandomNPCs = false;
            config.RandomCutscenes = false;
            config.RandomEvents = false;
            config.RandomBgm = false;

            var isoDirectory = Path.GetDirectoryName(fileRepository.DataPath);
            var input = Path.Combine(isoDirectory, "recvx.iso");
            var output = Path.Combine(isoDirectory, "recvx_biorand.iso");

            UdfEditor? udfEditor = null;
            try
            {
                FileIdentifier? advAfsFileId;
                FileIdentifier? multspq1AfsFileId;
                FileIdentifier? rdxAfsFileId;
                FileIdentifier? elfFileId;
                FileIdentifier? systemAfsFileId;
                using (progress.BeginTask(null, "Reading ISO file"))
                {
                    udfEditor = new Ps2IsoTools.UDF.UdfEditor(input, output);
                    advAfsFileId = udfEditor.GetFileByName("ADV.AFS");
                    if (advAfsFileId == null)
                        throw new BioRandUserException("ADV.AFS not found in ISO");

                    multspq1AfsFileId = udfEditor.GetFileByName("MULTSPQ1.AFS");
                    if (multspq1AfsFileId == null)
                        throw new BioRandUserException("MULTSPQ1.AFS not found in ISO");

                    rdxAfsFileId = udfEditor.GetFileByName("RDX_LNK.AFS");
                    if (rdxAfsFileId == null)
                        throw new BioRandUserException("RDX_LNK.AFS not found in ISO");

                    elfFileId = udfEditor.GetFileByName("SLUS_201.84");
                    if (elfFileId == null)
                        throw new BioRandUserException("SLUS_201.84 not found in ISO");

                    systemAfsFileId = udfEditor.GetFileByName("SYSTEM.AFS");
                    if (systemAfsFileId == null)
                        throw new BioRandUserException("SYSTEM.AFS not found in ISO");

                    _advAfs = ReadAfs(udfEditor, advAfsFileId);
                    // _multspq1Afs = ReadAfs(udfEditor, multspq1AfsFileId);
                    _rdxAfs = ReadAfs(udfEditor, rdxAfsFileId);
                    _elf = ReadFile(udfEditor, elfFileId);
                    _systemAfs = ReadAfs(udfEditor, systemAfsFileId);
                }

                // ExtractAfs(_multspq1Afs, @"F:\games\recv\extracted\multspq1.afs");
                using (progress.BeginTask(null, $"Creating backgrounds"))
                    ReplaceBackground(config);

                // Extract room files
                var rdxPath = Path.Combine(fileRepository.ModPath, "rdx_lnk");
                Directory.CreateDirectory(rdxPath);
                Parallel.For(0, _rdxAfs.Count, i =>
                {
                    var rdtId = GetRdtId(i);
                    var prs = new PrsFile(_rdxAfs.GetFileData(i));
                    prs.Uncompressed.WriteToFile(Path.Combine(rdxPath, $"R{rdtId}.RDT"));

#if EXTRACT_TEXTURES
                    try
                    {
                        var d = Path.Combine(fileRepository.ModPath, "rdx_texture", $"R{rdtId}");
                        Directory.CreateDirectory(d);
                        var rdt = new RdtCv(prs.Uncompressed);
                        var gIndex = 0;
                        foreach (var g in rdt.Textures.Groups)
                        {
                            var eIndex = 0;
                            foreach (var e in g.Entries)
                            {
                                if (e.Kind == CvTextureEntryKind.TIM2)
                                {
                                    if (BgCreator == null)
                                    {
                                        var fileName = $"R{rdtId}_{gIndex:00}_{eIndex:00}.bmp";
                                        var path = Path.Combine(d, fileName);
                                        var bmp = e.Tim2.Picture0.ToBmp();
                                        bmp.Data.WriteToFile(path);
                                    }
                                    else
                                    {
                                        var fileName = $"R{rdtId}_{gIndex:00}_{eIndex:00}.png";
                                        var path = Path.Combine(d, fileName);
                                        BgCreator.SaveImage(path, e.Tim2.Picture0);
                                    }
                                }
                                eIndex++;
                            }
                            gIndex++;
                        }
                    }
                    catch
                    {
                    }
#endif
                });

                GenerateRdts(config, progress, fileRepository);

                if (config.RandomDoors)
                    DisableNosferatuPoison();
                if (InstallConfig.DoorSkip)
                    SetDoorSkip();
                FixRifleStacking();
                SetItemQuantityPickup(config, new Rng(config.Seed));
                HackItemPickup();

                // base.Generate(config, progress, fileRepository);

                using (progress.BeginTask(null, "Compressing room files"))
                {
                    _rdxAfs = WriteRdxAfs(fileRepository);
                }

                using (progress.BeginTask(null, "Creating ISO file"))
                {
                    udfEditor.ReplaceFileStream(advAfsFileId, new MemoryStream(_advAfs!.Data.ToArray()));
                    udfEditor.ReplaceFileStream(rdxAfsFileId, new MemoryStream(_rdxAfs!.Data.ToArray()));
                    udfEditor.ReplaceFileStream(elfFileId, new MemoryStream(_elf));
                    udfEditor.ReplaceFileStream(systemAfsFileId, new MemoryStream(_systemAfs!.Data.ToArray()));
                    udfEditor.Rebuild(output);
                }
            }
            finally
            {
                udfEditor?.Dispose();
            }
        }

        internal override string[]? ChangePlayerCharacters(RandoConfig config, RandoLogger logger, GameData gameData, FileRepository fileRepository)
        {
            var actor = "claire.cv";
            if (config.ChangePlayer)
            {
                // Change Claire
                var pldPath = GetSelectedPldPath(config, 0);
                if (pldPath != null)
                {
                    actor = Path.GetFileName(pldPath);
                    SwapPlayerCharacter(config, logger, fileRepository, 10, actor);
                }

                // Change Chris
                pldPath = GetSelectedPldPath(config, 1);
                if (pldPath != null)
                {
                    actor = Path.GetFileName(pldPath);
                    SwapPlayerCharacter(config, logger, fileRepository, 11, actor);
                }
            }
            return new[] { actor };
        }

        private void SwapPlayerCharacter(RandoConfig config, RandoLogger logger, FileRepository fileRepository, int pldIndex, string actor)
        {
            var originalPlayerActor = pldIndex == 10 ? "claire.cv" : "chris.cv";
            var srcPldDir = DataManager.GetPath(BiohazardVersion, $"pld0\\{actor}");

            logger.WriteHeading($"Randomizing Player PL{pldIndex:X2}:");
            logger.WriteLine($"{originalPlayerActor} becomes {actor}");

            var srcPldFile = Path.Combine(srcPldDir, $"PL00.PLD");

            var afsBuilder = _systemAfs!.ToBuilder();
            afsBuilder.Replace(pldIndex, DataManager.GetData(srcPldFile)!);
            _systemAfs = afsBuilder.ToAfsFile();
        }

        protected override void PostGenerate(RandoConfig config, IRandoProgress progress, FileRepository fileRepository, GameData gameData)
        {
            using (progress.BeginTask(null, "Randomizing portraits"))
            {
                RandomizePortraits(gameData);
            }

            TestEdits(gameData);

            if (!config.RandomItems || !config.RandomInventory)
                return;

            var rrdt = gameData.GetRdt(RdtId.Parse("1000"));
            if (rrdt == null)
                return;

            var inventory = Inventories[0];
            if (inventory == null)
                return;

            // if
            var ifIndex = rrdt.AdditionalOpcodes.Count;
            var ifStatement = new UnknownOpcode(0, 0x01, new byte[] { 0x00 });
            rrdt.AdditionalOpcodes.Add(ifStatement);

            // ck
            rrdt.AdditionalOpcodes.Add(new UnknownOpcode(0, 0x04, new byte[] { 0x01, 0x01, 0x00, 0x00, 0x01 }));

            // inventory stuff
            if (inventory.Special is RandomInventory.Entry entry)
            {
                SetInventoryItem(-1, entry.Type, entry.Count);
            }
            for (var i = 0; i < inventory.Entries.Length; i++)
            {
                var e = inventory.Entries[i];
                if (e.Part != 2)
                {
                    SetInventoryItem(i, e.Type, e.Count);
                }
            }

            // endif
            rrdt.AdditionalOpcodes.Add(new UnknownOpcode(0, 0x03, new byte[] { 0x00 }));

            var skipSize = 0;
            for (var i = ifIndex + 1; i < rrdt.AdditionalOpcodes.Count; i++)
            {
                skipSize += rrdt.AdditionalOpcodes[i].Length;
            }
            ifStatement.Data[0] = (byte)skipSize;

            void SetInventoryItem(int index, byte type, byte quantity)
            {
                if (index == -1)
                {
                    SetSpecialSlotItem(type);
                }
                else if (index == 0)
                {
                    SetFirstInventoryItem(type);
                }
                else if (type != 0)
                {
                    rrdt!.AdditionalOpcodes.Add(new UnknownOpcode(0, 0xC7, new byte[] { 0x00, type, 0x00 }));
                }
                if (type != 0)
                {
                    rrdt.AdditionalOpcodes.Add(new UnknownOpcode(0, 0xC9, new byte[] { type, quantity, 0x00 }));
                }
            }
        }

        private unsafe void TestEdits(GameData gameData)
        {
#if DEBUG1
            // QuickDoor(RdtId.Parse("10B"), 0);

            var srcRdt = (RdtCv)gameData.GetRdt(RdtId.Parse("10C0"))!.RdtFile;
            var srcModels = srcRdt.Models.Pages[1..5].ToArray();
            var srcMotion = srcRdt.Motions;
            var srcTexture = srcRdt.Textures.Groups[1..4].ToArray();
            var srcEnemies = srcRdt.Enemies.ToArray();

            var rrdt = gameData.GetRdt(RdtId.Parse("1010"))!;
            rrdt.PostModifications.Add(() =>
            {
                var rdtBuilder = ((RdtCv)rrdt.RdtFile).ToBuilder();

                var scdb = new ScdProcedureList.Builder(BioVersion.BiohazardCv);
                scdb.Procedures.Add(new ScdProcedure(BioVersion.BiohazardCv, new byte[] { 0x00, 0x00 }));
                scdb.Procedures.Add(new ScdProcedure(BioVersion.BiohazardCv, new byte[] { 0x00, 0x00 }));
                rdtBuilder.Script = scdb.ToProcedureList();

                rdtBuilder.Motions = srcMotion;

                var modelListBuilder = rdtBuilder.Models.ToBuilder();
                modelListBuilder.Pages.RemoveAt(1);
                modelListBuilder.Pages.Insert(1, srcModels[0]);
                modelListBuilder.Pages.Insert(1, srcModels[2]);
                rdtBuilder.Models = modelListBuilder.ToCvModelList();

                var textureListBuilder = rdtBuilder.Textures.ToBuilder();
                textureListBuilder.Groups.RemoveAt(1);
                textureListBuilder.Groups.Insert(1, srcTexture[0]);
                // textureListBuilder.Groups.Insert(1, srcTexture[0]);
                rdtBuilder.Textures = textureListBuilder.ToTextureList();

                rdtBuilder.Enemies.Clear();
                for (var i = 0; i < 2; i++)
                {
                    rdtBuilder.Enemies.Add(new RdtCv.Enemy()
                    {
                        Header = 1,
                        Type = ReCvEnemyIds.Zombie,
                        Effect = 0,
                        Variant = 0,
                        Index = (short)i,
                        Position = new RdtCv.VectorF(8, 0, 24),
                        Rotation = new RdtCv.Vector32()
                    });
                }

                rrdt.RdtFile = rdtBuilder.ToRdt();
            });
#endif
        }

        private void QuickDoor(RdtId destination, int exitId)
        {
            var rdt = (RdtCv)_rdts[1].RdtFile;
            var rdtBuilder = rdt.ToBuilder();
            var aot = rdtBuilder.Aots[4];
            aot.Stage = (byte)destination.Stage;
            aot.Room = (byte)destination.Room;
            aot.ExitId = (byte)exitId;
            rdtBuilder.Aots[4] = aot;
            rdt = rdtBuilder.ToRdt();
            _rdts[1].RdtFile = rdt;
        }

        private ScdProcedureList Replace(ScdProcedureList src, byte[] data)
        {
            var data2 = new byte[src.Data.Length];
            Array.Copy(data, data2, data.Length);
            return new ScdProcedureList(BiohazardVersion, data2);
        }

        private AfsFile ReadAfs(UdfEditor editor, FileIdentifier fileId)
        {
            var data = ReadFile(editor, fileId);
            return new AfsFile(data);
        }

        private byte[] ReadFile(UdfEditor editor, FileIdentifier fileId)
        {
            var stream = editor.GetFileStream(fileId);
            var data = new byte[stream.Length];
            stream.Read(data, 0, data.Length);
            return data;
        }

        private AfsFile WriteRdxAfs(FileRepository fileRepository)
        {
            if (_rdxAfs == null)
                throw new Exception();

            var builder = _rdxAfs.ToBuilder();
            var parellelOptions = new ParallelOptions()
            {
                MaxDegreeOfParallelism = 32
            };

            Parallel.For(0, _rdts.Length, parellelOptions, i =>
            {
                var rrdt = _rdts[i];
                if (rrdt != null)
                {
                    var prs = CachePrs(fileRepository, rrdt.RdtId, rrdt.RdtFile.Data);
                    builder.Replace(i, prs.Data);
                }
            });
            return builder.ToAfsFile();
        }

        private PrsFile CachePrs(FileRepository fileRepository, RdtId rtdId, ReadOnlyMemory<byte> uncompressed)
        {
            var cacheDir = Path.Combine(Path.GetDirectoryName(fileRepository.DataPath), "prs_cache");
            var cacheFile = Path.Combine(cacheDir, rtdId + "_" + uncompressed.CalculateFnv1a().ToString("X8"));
            if (File.Exists(cacheFile))
            {
                try
                {
                    return new PrsFile(File.ReadAllBytes(cacheFile));
                }
                catch
                {
                }
            }

            var compressedData = PrsFile.Compress(uncompressed);
            try
            {
                if (Directory.Exists(cacheDir))
                {
                    Directory.CreateDirectory(cacheDir);
                    compressedData.Data.WriteToFile(cacheFile);
                }
            }
            catch
            {
            }
            return compressedData;
        }

        private void DisableNosferatuPoison()
        {
            var ms = new MemoryStream(_elf);
            var bw = new BinaryWriter(ms);
            ms.Position = ConvertAddress(0x1E6DC4);
            bw.Write(0);
        }

        private void SetDoorSkip()
        {
            var ms = new MemoryStream(_elf);
            var bw = new BinaryWriter(ms);
            ms.Position = ConvertAddress(0x133D4C);
            bw.Write(0);
            ms.Position = ConvertAddress(0x133D54);
            bw.Write(0);
        }

        private void FixRifleStacking()
        {
            var ms = new MemoryStream(_elf);
            var bw = new BinaryWriter(ms);
            ms.Position = ConvertAddress(0x35B1F4);
            bw.Write((byte)0x02);
            bw.Write((byte)0x0E);
            ms.Position = ConvertAddress(0x35B200);
            bw.Write((byte)0x02);
            bw.Write((byte)0x16);
        }

        private void HackItemPickup()
        {
            var ms = new MemoryStream(_elf);
            var bw = new BinaryWriter(ms);
            ms.Position = ConvertAddress(0x266E30);
            bw.Write((byte)0x06);
        }

        private void SetSpecialSlotItem(byte item)
        {
            var ms = new MemoryStream(_elf);
            var bw = new BinaryWriter(ms);
            ms.Position = ConvertAddress(0x3340B0);
            bw.Write((uint)(0x00FF0000 | item));

            _elf[ConvertAddress(0x2A6CE8)] = item;
        }

        private void SetFirstInventoryItem(byte item)
        {
            _elf[ConvertAddress(0x2A6CF0)] = item;
        }

        private void SetItemQuantityPickup(RandoConfig config, Rng rng)
        {
            var ms = new MemoryStream(_elf);
            var bw = new BinaryWriter(ms);

            var table = ConvertAddress(0x0035BCC0);
            for (var i = 0; i < 256; i++)
            {
                var itemType = (byte)i;
                var itemAttributes = ItemHelper.GetItemAttributes(itemType);
                if ((itemAttributes & ItemAttribute.InkRibbon) != 0 ||
                    (itemAttributes & ItemAttribute.Ammo) != 0 ||
                    itemType == ReCvItemIds.RocketLauncher)
                {
                    var quantity = GetQuantity(itemType);

                    var offset = table + (i * 4 * 4);
                    for (var j = 0; j < 4; j++)
                    {
                        ms.Position = offset + (j * 4);
                        bw.Write(quantity);
                    }
                }
            }

            int GetQuantity(byte type)
            {
                if (type == ItemHelper.GetItemId(CommonItemKind.InkRibbon))
                {
                    return (byte)rng.Next(1, 3);
                }

                var maxForType = ItemHelper.GetMaxAmmoForAmmoType(type);
                var multiplier = config.AmmoQuantity / 8.0;
                var max = (int)Math.Round(multiplier * maxForType);
                var min = Math.Max(1, max / 2);
                return rng.Next(min, max + 1);
            }
        }

        private static int ConvertAddress(int address)
        {
            return address - 0xFFF80;
        }

        private static RdtId GetRdtId(int fileIndex)
        {
            var fileName = _rdxFileNames[fileIndex];
            var stage = fileName[3] - '0';
            var room = int.Parse(fileName.Substring(4, 2));
            var variant = fileName[6] - '0';
            return new RdtId(stage, room, variant);
        }

        private static void ExtractAfs(AfsFile afs, string target)
        {
            Directory.CreateDirectory(target);
            for (var i = 0; i < afs.Count; i++)
            {
                var data = afs.GetFileData(i);
                var path = Path.Combine(target, i.ToString());
                data.WriteToFile(path);
            }
        }

        private void ReplaceBackground(RandoConfig config)
        {
            if (BgCreator == null)
                return;

            var src = DataManager.GetData(BiohazardVersion, "bg.png");
            var argb = BgCreator.CreateARGB(config, src);

            var afs = _advAfs!;
            var data = afs.GetFileData(2).ToArray();
            var dataTim = new Memory<byte>(data, 0x460, 0x80960 - 0x460);

            var bgTim = new Tim2(dataTim);
            var bgTimBuilder = bgTim.ToBuilder();

            var pic0 = bgTimBuilder.Pictures[0];
            var pic0b = pic0.ToBuilder();
            pic0b.Import(argb);
            bgTimBuilder.Pictures[0] = pic0b.ToPicture();

            var bgTimNew = bgTimBuilder.ToTim2();
            bgTimNew.Data.CopyTo(dataTim);

            var afsBuilder = _advAfs!.ToBuilder();
            afsBuilder.Replace(2, data);
            _advAfs = afsBuilder.ToAfsFile();
        }

        private void RandomizePortraits(GameData gameData)
        {
            var files = DataManager.GetFiles("portrait");
            foreach (var file in files)
            {
                var fileName = Path.GetFileName(file);
                var pattern = @"^R([0-9A-Za-z]{4})_(\d{2})_(\d{2})\.png$";
                var match = Regex.Match(fileName, pattern);
                if (!match.Success)
                    continue;

                if (!RdtId.TryParse(match.Groups[1].Value, out var rdtId) ||
                    !int.TryParse(match.Groups[2].Value, out var groupNum) ||
                    !int.TryParse(match.Groups[3].Value, out var entryNum))
                {
                    continue;
                }

                ReplaceRdtTexture(gameData, rdtId, groupNum, entryNum, $"portrait/{file}");
            }
        }

        private void ReplaceRdtTexture(GameData gameData, RdtId rdtId, int groupNum, int entryNum, string path)
        {
            if (BgCreator == null)
                return;

            var rdt = gameData.GetRdt(rdtId);
            if (rdt == null)
                return;

            var data = DataManager.GetData(path);
            if (data == null)
                return;

            var argb = BgCreator.LoadImage(new MemoryStream(data));
            var rdtBuilder = ((RdtCv)rdt.RdtFile).ToBuilder();
            var tim2Builder = rdtBuilder.Textures.Groups[groupNum].Entries[entryNum].Tim2.ToBuilder();
            var pic0 = tim2Builder.Pictures[0].ToBuilder();
            pic0.Import(argb);
            tim2Builder.Pictures[0] = pic0.ToPicture();
            var tim2 = tim2Builder.ToTim2();
            rdtBuilder.Textures = rdtBuilder.Textures.WithNewTexture(groupNum, entryNum, tim2);
            rdt.RdtFile = rdtBuilder.ToRdt();
        }

        private static string[] _rdxFileNames = new[]
        {
            "RM_0000.RDX",
            "RM_0010.RDX",
            "RM_0020.RDX",
            "RM_0021.RDX",
            "RM_0030.RDX",
            "RM_0031.RDX",
            "RM_0040.RDX",
            "RM_0050.RDX",
            "RM_0060.RDX",
            "RM_0070.RDX",
            "RM_0080.RDX",
            "RM_0090.RDX",
            "RM_0100.RDX",
            "RM_0110.RDX",
            "RM_0120.RDX",
            "RM_0130.RDX",
            "RM_0140.RDX",
            "RM_0150.RDX",
            "RM_0160.RDX",
            "RM_1000.RDX",
            "RM_1001.RDX",
            "RM_1002.RDX",
            "RM_1020.RDX",
            "RM_1021.RDX",
            "RM_1030.RDX",
            "RM_1040.RDX",
            "RM_1050.RDX",
            "RM_1060.RDX",
            "RM_1070.RDX",
            "RM_1080.RDX",
            "RM_1090.RDX",
            "RM_1100.RDX",
            "RM_1110.RDX",
            "RM_1120.RDX",
            "RM_1121.RDX",
            "RM_1122.RDX",
            "RM_1130.RDX",
            "RM_1140.RDX",
            "RM_2000.RDX",
            "RM_2010.RDX",
            "RM_2011.RDX",
            "RM_2020.RDX",
            "RM_2030.RDX",
            "RM_2031.RDX",
            "RM_2040.RDX",
            "RM_2050.RDX",
            "RM_2060.RDX",
            "RM_2070.RDX",
            "RM_3000.RDX",
            "RM_3010.RDX",
            "RM_3011.RDX",
            "RM_3020.RDX",
            "RM_3030.RDX",
            "RM_3040.RDX",
            "RM_3050.RDX",
            "RM_3060.RDX",
            "RM_3070.RDX",
            "RM_3080.RDX",
            "RM_3090.RDX",
            "RM_3091.RDX",
            "RM_3100.RDX",
            "RM_3110.RDX",
            "RM_3120.RDX",
            "RM_3130.RDX",
            "RM_3140.RDX",
            "RM_3150.RDX",
            "RM_3160.RDX",
            "RM_3170.RDX",
            "RM_3180.RDX",
            "RM_3190.RDX",
            "RM_3200.RDX",
            "RM_3210.RDX",
            "RM_3220.RDX",
            "RM_3230.RDX",
            "RM_3240.RDX",
            "RM_4000.RDX",
            "RM_4001.RDX",
            "RM_4010.RDX",
            "RM_4011.RDX",
            "RM_4012.RDX",
            "RM_4020.RDX",
            "RM_4030.RDX",
            "RM_4040.RDX",
            "RM_4050.RDX",
            "RM_5000.RDX",
            "RM_5001.RDX",
            "RM_5010.RDX",
            "RM_5500.RDX",
            "RM_5510.RDX",
            "RM_5520.RDX",
            "RM_5530.RDX",
            "RM_5540.RDX",
            "RM_5550.RDX",
            "RM_5580.RDX",
            "RM_5590.RDX",
            "RM_5600.RDX",
            "RM_5610.RDX",
            "RM_5620.RDX",
            "RM_5630.RDX",
            "RM_5640.RDX",
            "RM_5650.RDX",
            "RM_5660.RDX",
            "RM_5670.RDX",
            "RM_5680.RDX",
            "RM_5690.RDX",
            "RM_5700.RDX",
            "RM_5710.RDX",
            "RM_5800.RDX",
            "RM_5810.RDX",
            "RM_5820.RDX",
            "RM_5830.RDX",
            "RM_5840.RDX",
            "RM_6000.RDX",
            "RM_6010.RDX",
            "RM_6020.RDX",
            "RM_6030.RDX",
            "RM_6040.RDX",
            "RM_6050.RDX",
            "RM_6051.RDX",
            "RM_6060.RDX",
            "RM_6070.RDX",
            "RM_6080.RDX",
            "RM_6090.RDX",
            "RM_6100.RDX",
            "RM_7000.RDX",
            "RM_7010.RDX",
            "RM_7020.RDX",
            "RM_7030.RDX",
            "RM_7031.RDX",
            "RM_7040.RDX",
            "RM_7050.RDX",
            "RM_7060.RDX",
            "RM_7070.RDX",
            "RM_7071.RDX",
            "RM_7080.RDX",
            "RM_7081.RDX",
            "RM_7090.RDX",
            "RM_7100.RDX",
            "RM_7110.RDX",
            "RM_7120.RDX",
            "RM_7130.RDX",
            "RM_7140.RDX",
            "RM_7150.RDX",
            "RM_7160.RDX",
            "RM_7170.RDX",
            "RM_7180.RDX",
            "RM_7181.RDX",
            "RM_7190.RDX",
            "RM_7191.RDX",
            "RM_7200.RDX",
            "RM_7210.RDX",
            "RM_7220.RDX",
            "RM_7221.RDX",
            "RM_7230.RDX",
            "RM_7231.RDX",
            "RM_7240.RDX",
            "RM_7250.RDX",
            "RM_8000.RDX",
            "RM_8001.RDX",
            "RM_8010.RDX",
            "RM_8020.RDX",
            "RM_8030.RDX",
            "RM_8040.RDX",
            "RM_8050.RDX",
            "RM_9000.RDX",
            "RM_9010.RDX",
            "RM_9020.RDX",
            "RM_9030.RDX",
            "RM_9040.RDX",
            "RM_9050.RDX",
            "RM_9060.RDX",
            "RM_9070.RDX",
            "RM_9080.RDX",
            "RM_9090.RDX",
            "RM_9091.RDX",
            "RM_9100.RDX",
            "RM_9110.RDX",
            "RM_9120.RDX",
            "RM_9130.RDX",
            "RM_9140.RDX",
            "RM_9150.RDX",
            "RM_9160.RDX",
            "RM_9170.RDX",
            "RM_9180.RDX",
            "RM_9190.RDX",
            "RM_9200.RDX",
            "RM_9210.RDX",
            "RM_9220.RDX",
            "RM_9230.RDX",
            "RM_9240.RDX",
            "RM_9250.RDX",
            "RM_9260.RDX",
            "RM_9270.RDX",
            "RM_9280.RDX",
            "RM_9290.RDX",
            "RM_9300.RDX",
            "RM_9301.RDX",
            "RM_9302.RDX",
            "RM_9310.RDX",
            "RM_9320.RDX",
            "RM_9321.RDX",
            "RM_9340.RDX",
            "RM_9350.RDX",
            "RM_9360.RDX",
            "RM_9370.RDX",
        };
    }
}
