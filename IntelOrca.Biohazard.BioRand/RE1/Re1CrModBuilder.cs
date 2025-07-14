using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Xml;
using IntelOrca.Biohazard.Extensions;
using IntelOrca.Biohazard.Room;
using IntelOrca.Biohazard.Script;
using IntelOrca.Biohazard.Script.Opcodes;
using SixLabors.ImageSharp.PixelFormats;

namespace IntelOrca.Biohazard.BioRand.RE1
{
    internal class Re1CrModBuilder(DataManager biorandData, DataManager gameData) : ICrModBuilder
    {
        public ClassicRebirthMod Create(ClassicMod mod)
        {
            var session = new Session(biorandData, gameData, mod);
            return session.Create();
        }

        private class Session
        {
            private readonly DataManager _dataManager;
            private readonly DataManager _gameDataManager;
            private readonly ClassicMod _mod;
            private readonly ClassicRebirthModBuilder _crModBuilder;
            private readonly Map _map;
            private readonly DynamicTweaksBuilder _tb = new();

            private const int TWEAK_HITSCAN = 1;
            private const int TWEAK_INVENTORY_SIZE = 2;

            public Session(DataManager biorandData, DataManager gameData, ClassicMod mod)
            {
                _dataManager = biorandData;
                _gameDataManager = gameData;
                _mod = mod;
                _crModBuilder = new ClassicRebirthModBuilder(mod.Name);

                var player = _mod.General.GetValueOrDefault("player") as int? ?? 0;
                _map = _dataManager.GetJson<Map>(BioVersion.Biohazard1, "rdt.json")
                    .For(new MapFilter(false, (byte)player, 0));
            }

            public int Player => _mod.General.GetValueOrDefault("player") as int? ?? 0;
            public int InventorySize => _mod.General.GetValueOrDefault("inventorySize") as int? ?? 0;
            public bool RandomDoors => GetGeneralFlag("randomDoors");
            public bool RandomItems => GetGeneralFlag("randomItems");
            public bool RandomEnemies => GetGeneralFlag("randomEnemies");
            public bool CutscenesDisabled => GetGeneralFlag("cutscenesDisabled");
            public bool LockpickEnabled => GetGeneralFlag("lockpick");
            public bool InkEnabled => GetGeneralFlag("ink");
            public bool HelipadTyrantForced => GetGeneralFlag("forceTyrant");

            private bool GetGeneralFlag(string name)
            {
                return _mod.General.GetValueOrDefault(name)?.Equals(true) ?? false;
            }

            public ClassicRebirthMod Create()
            {
                _crModBuilder.Description = _mod.Description;
                if (_mod.Configuration is RandomizerConfiguration config)
                {
                    _crModBuilder.SetFile("config.json", config.ToJson(true));
                }

                var biorandModule = _dataManager.GetData("biorand.dll");
                if (biorandModule != null)
                {
                    _crModBuilder.Module = new ClassicRebirthModule("biorand.dll", biorandModule);
                }
                _crModBuilder.SetFile("log.md", _mod.GetDump(_map, Player == 0 ? "Chris" : "Jill"));
                _crModBuilder.SetFile($"generated.json", _mod.ToJson());

                Write();
                return _crModBuilder.Build();
            }

            public void Write()
            {
                WriteRdts();
                AddMiscXml();
                AddSoundXml();
                AddInventoryXml();
                AddProtagonistSkin();
                AddNpcSkins();
                AddEnemySkins();
                AddBackgroundTextures();
                AddVoices();
                AddMusic();
                AddTitleCall();
                AddDynamicTweaks();
            }

            private void WriteRdts()
            {
                var debugScripts = Environment.GetEnvironmentVariable("BIORAND_DEBUG_SCRIPTS") == "true";

                var gameData = GetGameData();
                if (debugScripts)
                {
                    DecompileGameData(gameData, Player, "scripts/");
                }
                ApplyRdtPatches(gameData);
                if (CutscenesDisabled)
                {
                    DisableCutscenes(gameData);
                }
                ApplyPostPatches(gameData);
                ApplyDoors(gameData);
                ApplyItems(gameData);
                ApplyEnemies(gameData);
                ApplyNpcs(gameData);
                foreach (var rrdt in gameData.Rdts)
                {
                    rrdt.Save();
                    _crModBuilder.SetFile(rrdt.OriginalPath!, rrdt.RdtFile.Data);
                }
                if (debugScripts)
                {
                    DecompileGameData(gameData, Player, "scripts_modded/");
                }
            }

            private void DecompileGameData(GameData gameData, int player, string prefix)
            {
                Parallel.ForEach(gameData.Rdts, rrdt =>
                {
                    rrdt.Decompile();
                    _crModBuilder.SetFile($"{prefix}{rrdt.RdtId}{player}.bio", rrdt.Script ?? "");
                    _crModBuilder.SetFile($"{prefix}{rrdt.RdtId}{player}.lst", rrdt.ScriptListing ?? "");
                    _crModBuilder.SetFile($"{prefix}{rrdt.RdtId}{player}.s", rrdt.ScriptDisassembly ?? "");
                });
            }

            public GameData GetGameData()
            {
                var result = new List<RandomizedRdt>();
                for (var i = 1; i <= 7; i++)
                {
                    var stageFolder = $"STAGE{i}";
                    var files = _gameDataManager.GetFiles(stageFolder);
                    foreach (var fileName in files)
                    {
                        var match = Regex.Match(fileName, @"^ROOM([0-9A-F]{3})(0|1).RDT$", RegexOptions.IgnoreCase);
                        if (match.Success)
                        {
                            var rdtId = RdtId.Parse(match.Groups[1].Value);
                            var rdtPlayer = int.Parse(match.Groups[2].Value);
                            if (rdtPlayer == Player)
                            {
                                var fileData = _gameDataManager.GetData($"{stageFolder}/{fileName}");
                                if (fileData == null || fileData.Length < 16)
                                    continue;

                                var rdt = Rdt.FromData(BioVersion.Biohazard1, fileData);
                                var rrdt = new RandomizedRdt(rdt, rdtId);
                                result.Add(rrdt);
                            }
                        }
                    }
                }

                foreach (var missingRoom in g_missingRooms)
                {
                    var mansion2 = new RdtId(missingRoom.Stage + 5, missingRoom.Room);
                    var rrdt2 = result.FirstOrDefault(x => x.RdtId == mansion2);
                    if (rrdt2 != null)
                    {
                        result.Add(new RandomizedRdt(rrdt2.RdtFile, missingRoom));
                    }
                }

                foreach (var rrdt in result)
                {
                    var rdtId = rrdt.RdtId;
                    rrdt.OriginalPath = $"STAGE{rdtId.Stage + 1}/ROOM{rdtId}{Player}.RDT";
                    rrdt.Load();
                }

                var gd = new GameData([.. result]);
                return gd;
            }


            private void ApplyRdtPatches(GameData gameData)
            {
                const byte PassCodeDoorLockId = 209;
                var player = Player;
                var randomDoors = RandomDoors;
                var randomItems = RandomItems;

                ConfigureOptions();
                EnableMoreJillItems();
                DisableDogWindows();
                DisableDogBoiler();
                AddDoor207();
                FixDoor104();
                FixDoorToWardrobe();
                FixPassCodeDoor();
                FixDrugStoreRoom();
                FixChrisPlant42();
                AllowRoughPassageDoorUnlock();
                ShotgunOnWallFix();
                DisableSerumDoorBlock();
                DisablePoisonChallenge();
                DisableBarryEvesdrop();
                ClearEnemies309();
                AllowPartnerItemBoxes();
                EnableFountainHeliportDoors();
                ForceHelipadTyrant();

                void ConfigureOptions()
                {
                    var rdt = gameData.GetRdt(RdtId.Parse("106"));
                    if (rdt == null)
                        return;

                    if (player == 0)
                    {
                        rdt.AdditionalOpcodes.AddRange(
                            ScdCondition.Parse("1:0").Generate(BioVersion.Biohazard1, [
                                Set(0, 123, (byte)(InkEnabled ? 0 : 1)), // Ink
                                Set(0, 124, (byte)(LockpickEnabled ? 0 : 1)) // Lockpick
                            ])
                        );
                    }
                    else
                    {
                        rdt.AdditionalOpcodes.AddRange(
                            ScdCondition.Parse("1:0").Generate(BioVersion.Biohazard1, [
                                Set(0, 123, (byte)(InkEnabled ? 0 : 1)), // Ink
                                Set(0, 124, (byte)(LockpickEnabled ? 0 : 1)), // Lockpick
                            ])
                        );
                        if (!LockpickEnabled)
                        {
                            rdt.Nop(0x31B02); // Disable Barry giving Jill the lockpick
                        }
                    }

                    static UnknownOpcode Set(byte group, byte index, byte value)
                    {
                        return new UnknownOpcode(0, 0x05, [group, index, value]);
                    }
                }

                void EnableMoreJillItems()
                {
                    // gameData.GetRdt(RdtId.Parse("106"))?.Patches.Add(new KeyValuePair<int, byte>(0x2FC02 + 1, 7));
                    // gameData.GetRdt(RdtId.Parse("106"))?.Patches.Add(new KeyValuePair<int, byte>(0x2FC02 + 2, 52));
                    // gameData.GetRdt(RdtId.Parse("106"))?.Patches.Add(new KeyValuePair<int, byte>(0x2FC02 + 3, 0));
                    // gameData.GetRdt(RdtId.Parse("106"))?.Nop(0x2FC06);
                    // gameData.GetRdt(RdtId.Parse("106"))?.Patches.Add(new KeyValuePair<int, byte>(0x2FFBC + 1, 7));
                    // gameData.GetRdt(RdtId.Parse("106"))?.Patches.Add(new KeyValuePair<int, byte>(0x2FFBC + 2, 52));
                    // gameData.GetRdt(RdtId.Parse("106"))?.Patches.Add(new KeyValuePair<int, byte>(0x2FFBC + 3, 0));
                    // gameData.GetRdt(RdtId.Parse("106"))?.Nop(0x2FFC0);
                    // gameData.GetRdt(RdtId.Parse("106"))?.Nop(0x31862);
                }

                void DisableDogWindows()
                {
                    var rdt108 = gameData.GetRdt(RdtId.Parse("108"));
                    rdt108?.Nop(0x19754, 0x197EE);
                }

                void DisableDogBoiler()
                {
                    var rdt114 = gameData.GetRdt(RdtId.Parse("114"));
                    rdt114?.Nop(0x24B80, 0x24C1C);
                }

                void AddDoor207()
                {
                    foreach (var rtdId in new[] { "207", "707" })
                    {
                        var rdt207 = gameData.GetRdt(RdtId.Parse(rtdId));
                        if (rdt207 != null)
                        {
                            rdt207.Nop(0x1C576);
                            rdt207.AdditionalOpcodes.Add(new DoorAotSeOpcode()
                            {
                                Opcode = 0x0C,
                                Id = 4,
                                X = 800,
                                Z = 10400,
                                W = 1900,
                                D = 1700,
                                Special = 0,
                                Re1UnkB = 0,
                                Animation = 0,
                                Re1UnkC = 2,
                                LockId = 21,
                                Target = new RdtId(255, 0x06),
                                NextX = 9180,
                                NextY = 0,
                                NextZ = 11280,
                                NextD = 2048,
                                LockType = 255,
                                Free = 129
                            });
                        }
                    }
                }

                void FixDoor104()
                {
                    var rdt104 = gameData.GetRdt(RdtId.Parse("104"));
                    if (rdt104 != null)
                    {
                        var door = rdt104.Doors.FirstOrDefault(x => x.Id == 2);
                        door.NextX = 12700;
                        door.NextY = -7200;
                        door.NextZ = 3300;
                    }
                }

                void FixDoorToWardrobe()
                {
                    var rdt112 = gameData.GetRdt(RdtId.Parse("112"));
                    var rdt612 = gameData.GetRdt(RdtId.Parse("612"));
                    rdt112?.Nop(0x17864, 0x17866);
                    rdt112?.Nop(0x17884, 0x17886);
                    rdt612?.Nop(0x17864, 0x17866);
                    rdt612?.Nop(0x17884, 0x17886);
                }

                void FixPassCodeDoor()
                {
                    for (var mansion = 0; mansion < 2; mansion++)
                    {
                        var mansionOffset = mansion == 0 ? 0 : 5;
                        var rdt = gameData.GetRdt(new RdtId(1 + mansionOffset, 0x01));
                        if (rdt == null)
                            return;

                        var door = rdt.Doors.FirstOrDefault(x => x.Id == 1) as DoorAotSeOpcode;
                        if (door == null)
                            return;

                        door.LockId = PassCodeDoorLockId;
                        door.NextX = 11200;
                        door.NextZ = 28000;
                        door.LockType = 255;
                        door.Free = 129;

                        if (!randomDoors && Player == 1)
                        {
                            rdt.AdditionalOpcodes.Add(new UnknownOpcode(0, 0x01, [0x0A]));
                            rdt.AdditionalOpcodes.Add(new UnknownOpcode(0, 0x04, [0x01, 0x25, 0x00]));
                            rdt.AdditionalOpcodes.Add(new UnknownOpcode(0, 0x05, [0x02, PassCodeDoorLockId, 0]));
                            rdt.AdditionalOpcodes.Add(new UnknownOpcode(0, 0x03, [0x00]));
                        }

                        rdt.Nop(0x41A34);

                        if (mansion == 1)
                        {
                            if (player == 0)
                            {
                                rdt.Nop(0x41A92);
                            }
                            else
                            {
                                rdt.Nop(0x41A5C, 0x41A68);
                            }
                        }
                    }
                }

                void FixDrugStoreRoom()
                {
                    if (player != 0)
                        return;

                    var rdt = gameData.GetRdt(RdtId.Parse("409"));
                    rdt?.Nop(0x166D4, 0x16742);
                    rdt?.Nop(0x168AA, 0x16918);
                }

                void FixChrisPlant42()
                {
                    if (player != 0)
                        return;

                    // Disable switch to Rebecca
                    var rdt40C = gameData.GetRdt(RdtId.Parse("40C"));
                    rdt40C?.Nop(0x66DC, 0x6712);

                    // Fix V-JOLT switch back
                    var rdt40F = gameData.GetRdt(RdtId.Parse("40F"));
                    rdt40F?.Nop(0x1F782);

                    // Force Rebecca cutscene
                    var rdt408 = gameData.GetRdt(RdtId.Parse("408"));
                    rdt408?.Nop(0x30B4E);
                    rdt408?.Nop(0x30B90);

                    // Force Rebecca in drug store room
                    var rdt409 = gameData.GetRdt(RdtId.Parse("409"));
                    rdt409?.Nop(0x16856);
                }

                void AllowRoughPassageDoorUnlock()
                {
                    for (var mansion = 0; mansion < 2; mansion++)
                    {
                        var mansionOffset = mansion == 0 ? 0 : 5;
                        var rdt = gameData.GetRdt(new RdtId(1 + mansionOffset, 0x14));
                        if (rdt == null)
                            return;

                        var doorId = player == 0 ? 1 : 5;
                        var door = (DoorAotSeOpcode)rdt.ConvertToDoor((byte)doorId, 0, 254, PassCodeDoorLockId);
                        door.Special = 2;
                        door.Re1UnkC = 1;
                        door.Target = new RdtId(0xFF, 0x01);
                        door.NextX = 15500;
                        door.NextZ = 25400;
                        door.NextD = 1024;

                        if (player == 1)
                        {
                            rdt.Nop(0x19F3A);
                            rdt.Nop(0x1A016);
                            rdt.Nop(0x1A01C);
                        }
                    }
                }

                void ShotgunOnWallFix()
                {
                    if (!randomItems)
                        return;

                    // Prevent placing shotgun
                    var rdt116 = gameData.GetRdt(RdtId.Parse("116"));
                    var rdt516 = gameData.GetRdt(RdtId.Parse("516"));
                    for (var i = 2; i < 2 + 8; i++)
                    {
                        rdt116?.Patches.Add(new KeyValuePair<int, byte>(0x1FE62 + i, 0));
                        rdt516?.Patches.Add(new KeyValuePair<int, byte>(0x1FE62 + i, 0));
                    }

                    // Lock both doors in sandwich room (since we can't put item back on wall)
                    foreach (var rdtId in new[] { "115", "515" })
                    {
                        var rdt115 = gameData.GetRdt(RdtId.Parse(rdtId));
                        if (rdt115 == null)
                            continue;

                        if (player == 1)
                        {
                            rdt115.Nop(0x2342);
                        }

                        // Fix locks (due to increased lock limit)
                        foreach (var opcode in rdt115.Opcodes)
                        {
                            if (opcode is UnknownOpcode unk && unk.Opcode == 5)
                            {
                                if (unk.Data[1] == 0)
                                    unk.Data[1] = 15;
                                else if (unk.Data[1] == 1 || unk.Data[1] == 2)
                                    unk.Data[1] = 16;
                            }
                        }
                    }

                    // Unlock doors when in hall or living room
                    var rdt109 = gameData.GetRdt(RdtId.Parse("109"));
                    var rdt609 = gameData.GetRdt(RdtId.Parse("609"));
                    rdt109?.AdditionalOpcodes.Add(new UnknownOpcode(0, 5, [2, 15, 0]));
                    rdt609?.AdditionalOpcodes.Add(new UnknownOpcode(0, 5, [2, 15, 0]));
                    rdt116?.AdditionalOpcodes.Add(new UnknownOpcode(0, 5, [2, 16, 0]));
                    rdt516?.AdditionalOpcodes.Add(new UnknownOpcode(0, 5, [2, 16, 0]));
                }

                void DisableSerumDoorBlock()
                {
                    var rdt = gameData.GetRdt(RdtId.Parse("20D"));
                    if (rdt == null)
                        return;

                    if (player == 0)
                    {
                        rdt.Nop(0x2914C);
                        rdt.Nop(0x29246);
                    }
                    else
                    {
                        rdt.Nop(0x291AE);
                    }
                }

                void DisablePoisonChallenge()
                {
                    var rdt = gameData.GetRdt(RdtId.Parse("20E"));
                    if (player == 0)
                    {
                        rdt?.Nop(0x10724);
                        rdt?.Nop(0x1073A, 0x10740);
                        rdt?.Nop(0x1075A, 0x107FC);
                        rdt?.Nop(0x107F2, 0x107FC);
                    }
                    else
                    {
                        rdt?.Nop(0x10724, 0x1072A);
                        rdt?.Nop(0x10744, 0x10780);
                        rdt?.Nop(0x1078A, 0x10794);
                    }
                }

                void DisableBarryEvesdrop()
                {
                    if (player != 1)
                        return;

                    var rdt = gameData.GetRdt(new RdtId(3, 0x05));
                    if (rdt == null)
                        return;

                    rdt.Nop(0x194A2);
                }

                void ClearEnemies309()
                {
                    if (player != 1)
                        return;

                    if (!RandomEnemies)
                        return;

                    var rdt = gameData.GetRdt(RdtId.Parse("309"));
                    rdt?.Nop(0x2D83C);
                    rdt?.Nop(0x2D852);
                }

                void AllowPartnerItemBoxes()
                {
                    // Remove partner check for these two item boxes
                    // This is so Rebecca can use the item boxes
                    // Important for Chris 8-inventory because the inventory
                    // is now shared for both him and Rebecca and player
                    // might need to make space for more items e.g. (V-JOLT)
                    var room = gameData.GetRdt(new RdtId(0, 0x00));
                    room?.Nop(0x10C92);

                    room = gameData.GetRdt(new RdtId(3, 0x03));
                    room?.Nop(0x1F920);
                }

                void EnableFountainHeliportDoors()
                {
                    var rdtFountain = gameData.GetRdt(RdtId.Parse("305"));
                    if (rdtFountain != null)
                    {
                        var door = (DoorAotSeOpcode)rdtFountain.Doors.First(x => x.Id == 0);
                        door.LockId = 2;
                        door.LockType = 255;
                        door.Special = 11;
                        door.Animation = 11;
                        door.NextX = 29130;
                        door.NextY = 0;
                        door.NextZ = 5700;
                        door.NextD = 2048;

                        // Remove message aot_reset
                        rdtFountain.Nop(0x3E9AE);
                    }

                    var rdtHeliport = gameData.GetRdt(RdtId.Parse("303"));
                    if (rdtHeliport != null)
                    {
                        var door = (DoorAotSeOpcode)rdtHeliport.ConvertToDoor(8, 11, null, null);
                        door.Target = RdtId.Parse("305");
                        door.LockId = 2;
                        door.LockType = 255;
                        door.Special = 11;
                        door.Animation = 11;
                        door.NextX = 3130;
                        door.NextY = 0;
                        door.NextZ = 16900;
                        door.NextD = 0;

                        rdtHeliport.Nop(0x111BE);
                        rdtHeliport.Nop(0x111C0);

                        // Set cut to 4 if last room is ?05
                        rdtHeliport.AdditionalOpcodes.AddRange([
                            new UnknownOpcode(0, 0x01, [ 0x0C ]),
                        new UnknownOpcode(0, 0x06, [ 0x03, 0x00, 0x05 ]),
                        new UnknownOpcode(0, 0x23, [ 0x01 ]),
                        new UnknownOpcode(0, 0x08, [ 0x02, 0x04, 0x00 ]),
                        new UnknownOpcode(0, 0x03, [ 0x00 ])
                        ]);
                    }
                }

                void ForceHelipadTyrant()
                {
                    if (HelipadTyrantForced)
                    {
                        var room = gameData.GetRdt(RdtId.Parse("303"));
                        room?.AdditionalOpcodes.Add(new UnknownOpcode(0, 5, [0, 43, 0]));
                    }
                }
            }

            private void DisableCutscenes(GameData gameData)
            {
                var rdt = gameData.GetRdt(RdtId.Parse("106"));
                if (rdt == null)
                    return;

                var player = Player;
                if (player == 0)
                {
                    rdt.AdditionalOpcodes.AddRange(
                        ScdCondition.Parse("1:0").Generate(BioVersion.Biohazard1, [
                            Set(1, 0, 0), // First cutscene
                        Set(1, 2, 0), // First zombie found
                        Set(1, 3, 0), // Second cutscene (Jill? Wesker?)
                        Set(1, 36, 0), // First Rebecca save room cutscene
                        Set(1, 69, 0), // Brad call cutscene in final lab room
                        Set(1, 72, 0), // Enrico cutscene
                        Set(1, 100, 0), // Prevent Plant 42 Rebecca switch
                        Set(1, 167, 0), // Init. dining room emblem state
                        Set(1, 171, 0), // Wesker cutscene after Plant 42
                        Set(0, 101, 0), // Jill in cell cutscene
                        Set(0, 127, 0), // Pick up radio
                        Set(0, 192, 0) // Rebecca not saved
                        ]));

                    // Disable hunter / rebecca scream
                    gameData.GetRdt(RdtId.Parse("60A"))?.Nop(0xF702);

                    // Disable rebecca in trouble
                    gameData.GetRdt(RdtId.Parse("601"))?.Nop(0x24CAA, 0x24E16);
                    gameData.GetRdt(RdtId.Parse("601"))?.Nop(0x24E1E, 0x24E8A);
                    gameData.GetRdt(RdtId.Parse("706"))?.Nop(0x3785C, 0x3796A);
                    gameData.GetRdt(RdtId.Parse("706"))?.Nop(0x37972, 0x379F6);

                    // Disable Jill in cell
                    gameData.GetRdt(RdtId.Parse("512"))?.Nop(0xAF4C, 0xAF8E);

                    // Disable Wesker cutscene
                    gameData.GetRdt(RdtId.Parse("514"))?.Nop(0xEFCE, 0xF048);

                    // Disable Wesker / Tyrant cutscene
                    var rdt513 = gameData.GetRdt(RdtId.Parse("513"));
                    if (rdt513 != null)
                    {
                        rdt513.Patches.Add(new KeyValuePair<int, byte>(0x1C4AA + 1, 0));
                        rdt513.Patches.Add(new KeyValuePair<int, byte>(0x1C4AA + 2, 55));
                        rdt513.Patches.Add(new KeyValuePair<int, byte>(0x1C6E8 + 1, 0));
                        rdt513.Patches.Add(new KeyValuePair<int, byte>(0x1C6E8 + 2, 55));
                        rdt513.Nop(0x1C484);
                        rdt513.Nop(0x1C6B0);
                        rdt513.AdditionalOpcodes.Add(new SceEmSetOpcode()
                        {
                            Opcode = 0x1B,
                            Type = 0x0C,
                            State = 0,
                            KillId = 112,
                            Re1Unk04 = 1,
                            Re1Unk05 = 2,
                            Re1Unk06 = 0,
                            Re1Unk07 = 0,
                            D = 3072,
                            Re1Unk0A = 0,
                            Re1Unk0B = 0,
                            X = 10700,
                            Y = 0,
                            Z = 7000,
                            Id = 1,
                            Re1Unk13 = 0,
                            Re1Unk14 = 0,
                            Re1Unk15 = 0
                        });
                        rdt513.AdditionalFrameOpcodes.AddRange(
                            ScdCondition.Parse("0:55 && 4:11").Generate(BioVersion.Biohazard1, [
                                Set(4, 11, 0),
                            new UnknownOpcode(0, 0x16, [0x00]),
                            new UnknownOpcode(0, 0x16, [0x01]),
                            new UnknownOpcode(0, 0x15, [0x02])]));
                    }
                }
                else
                {
                    rdt.AdditionalOpcodes.AddRange(
                        ScdCondition.Parse("1:0").Generate(BioVersion.Biohazard1, [
                            Set(0, 101, 0), // Chris in cell cutscene
                        Set(0, 127, 0), // Pick up radio
                        Set(1, 0, 0), // 106 first cutscene
                        Set(1, 2, 0), // 104 first zombie found
                        Set(1, 3, 0), // 106 Wesker search cutscene
                        Set(1, 5, 0), // 106/203 Wesker search complete
                        Set(1, 7, 0), // 106/203 Barry gift cutscene (also disables 115 sandwich rescue and 20A cutscene)
                        Set(1, 69, 0), // Brad call cutscene in final lab room
                        Set(1, 72, 0), // Enrico cutscene
                        Set(1, 86, 0), // 20E Yawn poison partner recovery
                        Set(1, 97, 0), // 20D Richard receives serum
                        Set(1, 103, 0), // 212 Forrest cutscene
                        Set(1, 161, 0), // 105 first dining room cutscene
                        Set(1, 170, 0), // Init. dining room emblem state
                        Set(1, 172, 0), // 104 visted
                        Set(1, 173, 0), // 105 zombie cutscene
                        Set(1, 175, 0), // Wesker cutscene after Plant 42
                        Set(1, 192, 0) // Barry not saved
                        ]));

                    // Disable Plant 42 Barry
                    gameData.GetRdt(RdtId.Parse("40C"))?.Nop(0x64C8);
                    gameData.GetRdt(RdtId.Parse("40C"))?.Nop(0x64D4);

                    // Disable Barry in Yawn 2 room
                    foreach (var rdtId in new[] { "20C", "70C" })
                    {
                        var rdt20C = gameData.GetRdt(RdtId.Parse(rdtId));
                        if (rdt20C != null)
                        {
                            rdt20C.Patches.Add(new KeyValuePair<int, byte>(0x96EA + 14, 0x2C));
                            rdt20C.Nop(0x9704);
                            rdt20C.Nop(0x970A);
                        }
                    }

                    // Disable Wesker cutscene
                    gameData.GetRdt(RdtId.Parse("514"))?.Nop(0xEFCE, 0xF0C6);

                    // Disable Chris in cell
                    gameData.GetRdt(RdtId.Parse("512"))?.Nop(0xAF4C, 0xAF74);

                    // Disable Wesker / Tyrant cutscene
                    var rdt513 = gameData.GetRdt(RdtId.Parse("513"));
                    if (rdt513 != null)
                    {
                        rdt513.Nop(0x1C484);
                        rdt513.Patches.Add(new KeyValuePair<int, byte>(0x1C4AA + 1, 0));
                        rdt513.Patches.Add(new KeyValuePair<int, byte>(0x1C4AA + 2, 55));
                        rdt513.Nop(0x1C724);
                        rdt513.Patches.Add(new KeyValuePair<int, byte>(0x1C7C8 + 1, 0));
                        rdt513.Patches.Add(new KeyValuePair<int, byte>(0x1C7C8 + 2, 55));
                        rdt513.AdditionalOpcodes.Add(new SceEmSetOpcode()
                        {
                            Opcode = 0x1B,
                            Type = 0x0C,
                            State = 0,
                            KillId = 112,
                            Re1Unk04 = 1,
                            Re1Unk05 = 2,
                            Re1Unk06 = 0,
                            Re1Unk07 = 0,
                            D = 3072,
                            Re1Unk0A = 0,
                            Re1Unk0B = 0,
                            X = 10700,
                            Y = 0,
                            Z = 7000,
                            Id = 1,
                            Re1Unk13 = 0,
                            Re1Unk14 = 0,
                            Re1Unk15 = 0
                        });
                        rdt513.AdditionalFrameOpcodes.AddRange(
                            ScdCondition.Parse("0:55 && 4:11").Generate(BioVersion.Biohazard1, [
                                Set(4, 11, 0),
                            new UnknownOpcode(0, 0x16, [0x00]),
                            new UnknownOpcode(0, 0x16, [0x01]),
                            new UnknownOpcode(0, 0x15, [0x02])]));
                    }
                }

                static UnknownOpcode Set(byte group, byte index, byte value)
                {
                    return new UnknownOpcode(0, 0x05, [group, index, value]);
                }
            }

            private void ApplyPostPatches(GameData gameData)
            {
                // For each changed item, patch any additional bytes
                foreach (var kvp in _map.Rooms)
                {
                    var rdts = kvp.Value.Rdts
                        .Select(gameData.GetRdt)
                        .Where(x => x != null)
                        .Select(x => x!)
                        .ToArray();
                    foreach (var item in kvp.Value.Items ?? [])
                    {
                        if (item.GlobalId is not short globalId)
                            continue;

                        if (_mod.Items.GetValueOrDefault(globalId) is Item newItem && item.TypeOffsets != null)
                        {
                            foreach (var o in item.TypeOffsets)
                            {
                                var typeOffset = Map.ParseLiteral(o);
                                foreach (var rdt in rdts)
                                {
                                    rdt.Patches.Add(new KeyValuePair<int, byte>(typeOffset, newItem.Type));
                                }
                            }
                        }
                    }
                }
            }

            private void ApplyDoors(GameData gameData)
            {
                foreach (var rrdt in gameData.Rdts)
                {
                    foreach (var doorOpcode in rrdt.Doors)
                    {
                        var doorIdentity = new RdtItemId(rrdt.RdtId, doorOpcode.Id);
                        if (_mod.Doors.TryGetValue(doorIdentity, out var doorLock))
                        {
                            if (doorLock == null)
                            {
                                doorOpcode.LockId = 0;
                                doorOpcode.LockType = 0;
                            }
                            else
                            {
                                doorOpcode.LockId = (byte)doorLock.Value.Id;
                                doorOpcode.LockType = (byte)doorLock.Value.KeyItemId;
                            }
                        }
                    }
                }
            }

            private void ApplyItems(GameData gameData)
            {
                foreach (var rrdt in gameData.Rdts)
                {
                    foreach (var itemOpcode in rrdt.Items)
                    {
                        if (_mod.Items.TryGetValue(itemOpcode.GlobalId, out var item))
                        {
                            itemOpcode.Type = item.Type;
                            itemOpcode.Amount = item.Amount;
                        }
                    }
                }
            }

            private void ApplyEnemies(GameData gameData)
            {
                if (!RandomEnemies)
                    return;

                // Clear all enemies
                foreach (var rdt in gameData.Rdts)
                {
                    var room = _map.Rooms.Values.FirstOrDefault(x => x.Rdts.Contains(rdt.RdtId));
                    if (room == null)
                        continue;

                    if (room.Enemies == null || room.Enemies.Length == 0)
                        continue;

                    var reservedIds = room.Enemies
                        .Where(x => x.Id != null)
                        .Select(x => x.Id!)
                        .ToArray();

                    var offsets = rdt.Enemies
                        .Where(x => CanRemoveEnemy(x.Type))
                        .Where(x => !reservedIds.Contains(x.Id))
                        .Select(x => x.Offset)
                        .ToArray();
                    foreach (var o in offsets)
                    {
                        rdt.Nop(o);
                    }
                }

                var allEffects = HarvestAllEffs(gameData);
                var groups = _mod.EnemyPlacements.GroupBy(x => x.RdtId);
                foreach (var g in groups)
                {
                    var rdt = gameData.GetRdt(g.Key);
                    if (rdt == null)
                        continue;

                    var requiredEsp = new HashSet<byte>();
                    var opcodes = new List<OpcodeBase>();
                    string? condition = null;
                    foreach (var ep in g)
                    {
                        if (ep.Create)
                        {
                            var opcode = CreateEnemyOpcode(ep);
                            opcodes.Add(opcode);
                            condition ??= ep.Condition;
                        }
                        else
                        {
                            foreach (var e in rdt.Enemies.Where(x => x.Id == ep.Id))
                            {
                                var oldType = e.Type;
                                var newType = (byte)ep.Type;

                                e.Type = newType;
                                if (newType == Re1EnemyIds.Snake ||
                                    newType == Re1EnemyIds.WebSpinner)
                                {
                                    e.State = 0;
                                }
                                else
                                {
                                    e.State = (byte)(e.State & 0x80);
                                }
                            }
                        }
                        foreach (var esp in ep.Esp)
                            requiredEsp.Add((byte)esp);
                    }
                    InsertConditions(rdt, opcodes, condition);
                    AddRequiredEsps(rdt, requiredEsp, allEffects);
                }

                bool CanRemoveEnemy(byte type)
                {
                    switch (type)
                    {
                        case Re1EnemyIds.Zombie:
                        case Re1EnemyIds.ZombieNaked:
                        case Re1EnemyIds.Cerberus:
                        case Re1EnemyIds.WebSpinner:
                        case Re1EnemyIds.BlackTiger:
                        case Re1EnemyIds.Crow:
                        case Re1EnemyIds.Hunter:
                        case Re1EnemyIds.Wasp:
                        case Re1EnemyIds.Chimera:
                        case Re1EnemyIds.Snake:
                        case Re1EnemyIds.Neptune:
                        case Re1EnemyIds.Tyrant1:
                        case Re1EnemyIds.Plant42Vines:
                        case Re1EnemyIds.ZombieResearcher:
                            return true;
                        default:
                            return false;
                    }
                }

                SceEmSetOpcode CreateEnemyOpcode(EnemyPlacement ep)
                {
                    return new SceEmSetOpcode()
                    {
                        Length = 22,
                        Opcode = (byte)OpcodeV1.SceEmSet,
                        Type = (byte)ep.Type,
                        State = (byte)ep.Pose,
                        KillId = (byte)ep.GlobalId,
                        Re1Unk04 = 1,
                        Re1Unk05 = 2,
                        Re1Unk06 = 0,
                        Re1Unk07 = 0,
                        D = (short)ep.D,
                        Re1Unk0A = 0,
                        Re1Unk0B = 0,
                        X = (short)ep.X,
                        Y = (short)ep.Y,
                        Z = (short)ep.Z,
                        Id = (byte)ep.Id,
                        Re1Unk13 = 0,
                        Re1Unk14 = 0,
                        Re1Unk15 = 0,
                    };
                }

                void InsertConditions(RandomizedRdt rdt, List<OpcodeBase> enemyOpcodes, string? condition)
                {
                    if (string.IsNullOrEmpty(condition))
                    {
                        rdt.AdditionalOpcodes.AddRange(enemyOpcodes);
                        return;
                    }

                    var scdCondition = ScdCondition.Parse(condition!);
                    var opcodes = scdCondition.Generate(BioVersion.Biohazard1, enemyOpcodes);
                    rdt.AdditionalOpcodes.AddRange(opcodes);
                }

                static Dictionary<byte, EmbeddedEffect> HarvestAllEffs(GameData gameData)
                {
                    var result = new Dictionary<byte, EmbeddedEffect>();
                    foreach (var rdt in gameData.Rdts)
                    {
                        var embeddedEffects = ((Rdt1)rdt.RdtFile).EmbeddedEffects;
                        for (var i = 0; i < embeddedEffects.Count; i++)
                        {
                            var ee = embeddedEffects[i];
                            if (ee.Id != 0xFF && !result.ContainsKey(ee.Id))
                            {
                                result[ee.Id] = ee;
                            }
                        }
                    }
                    return result;
                }

                static void AddRequiredEsps(RandomizedRdt rdt, HashSet<byte> espIds, Dictionary<byte, EmbeddedEffect> allEffects)
                {
                    if (espIds.Count == 0)
                        return;

                    var rdtFile = (Rdt1)rdt.RdtFile;
                    var embeddedEffects = rdtFile.EmbeddedEffects;
                    var missingIds = espIds.Except(embeddedEffects.Ids).ToArray();
                    if (missingIds.Length == 0)
                        return;

                    var existingEffects = embeddedEffects.Effects.ToList();
                    foreach (var id in missingIds)
                    {
                        existingEffects.Add(allEffects[id]);
                    }

                    var rdtBuilder = rdtFile.ToBuilder();
                    rdtBuilder.EmbeddedEffects = new EmbeddedEffectList(rdtFile.Version, existingEffects.ToArray());
                    rdt.RdtFile = rdtBuilder.ToRdt();
                }
            }

            private void ApplyNpcs(GameData gameData)
            {
                foreach (var npc in _mod.Npcs)
                {
                    var rdt = gameData.GetRdt(npc.RdtId);
                    if (rdt == null)
                        continue;

                    var em = rdt.Enemies.FirstOrDefault(x => x.Offset == npc.Offset);
                    if (em == null)
                        continue;

                    em.Type = (byte)npc.Type;
                }
            }

            private void AddInventoryXml()
            {
                var inventories = _mod.Inventory;

                using var ms = new MemoryStream();
                var doc = new XmlDocument();
                var root = doc.CreateElement("Init");

                var chrisRebecca = CreateEmptyInventory(12);
                var jill = CreateEmptyInventory(8);
                if (Player == 0)
                    chrisRebecca = inventories[0].WithSize(12);
                else
                    jill = inventories[0].WithSize(8);

                root.AppendChild(CreatePlayerNode(doc, jill));
                root.AppendChild(CreatePlayerNode(doc, chrisRebecca));

                doc.AppendChild(root);
                doc.Save(ms);
                _crModBuilder.SetFile("init.xml", ms.ToArray());

                static RandomInventory CreateEmptyInventory(int size)
                {
                    var entries = new List<RandomInventory.Entry>();
                    for (var i = 0; i < size; i++)
                    {
                        entries.Add(new RandomInventory.Entry());
                    }
                    entries[0] = new RandomInventory.Entry(Re1ItemIds.CombatKnife, 1);
                    return new RandomInventory([.. entries], null);
                }

                static XmlElement CreatePlayerNode(XmlDocument doc, RandomInventory main)
                {
                    var playerNode = doc.CreateElement("Player");
                    foreach (var entry in main.Entries)
                    {
                        var entryNode = doc.CreateElement("Entry");
                        entryNode.SetAttribute("id", entry.Type.ToString());
                        entryNode.SetAttribute("count", entry.Count.ToString());
                        playerNode.AppendChild(entryNode);
                    }
                    return playerNode;
                }
            }

            private void AddProtagonistSkin()
            {
                if (!_mod.Characters.TryGetValue(0, out var characterReplacement))
                    return;

                var characterPath = characterReplacement.Path;
                if (characterPath == null)
                    return;

                var characterName = Path.GetFileName(characterPath);
                var srcPlayer = 0;
                var emdData = _dataManager.GetData($"{characterPath}/char10.emd");
                if (emdData == null)
                {
                    srcPlayer = 1;
                    emdData = _dataManager.GetData($"{characterPath}/char11.emd");
                }

                var playerIndex = Player;
                _crModBuilder.SetFile($"ENEMY/CHAR1{playerIndex}.EMD", emdData);
                for (var i = 0; i < 12; i++)
                {
                    var emwData = _dataManager.GetData($"{characterPath}/W{srcPlayer}{i}.EMW");
                    if (emwData != null)
                    {
                        _crModBuilder.SetFile($"PLAYERS/W{playerIndex}{i}.EMW", emwData);
                    }
                }

                var hurtFiles = GetHurtFiles(characterName);
                var hurtFileNames = new string[][]
                {
                    ["chris", "ch_ef"],
                    ["jill", "jill_ef"],
                    [],
                    ["reb"]
                };

                var soundDir = "sound";
                for (int i = 0; i < hurtFiles.Length; i++)
                {
                    var targetData = GetHurtWaveData(hurtFiles[i]);
                    var arr = hurtFileNames[playerIndex];
                    foreach (var hurtFileName in arr)
                    {
                        var soundPath = $"{soundDir}/{hurtFileName}{i + 1:00}.wav";
                        _crModBuilder.SetFile(soundPath, targetData);
                    }
                }
                if (playerIndex <= 1)
                {
                    var nom = playerIndex == 0 ? "ch_nom.wav" : "ji_nom.wav";
                    var sime = playerIndex == 0 ? "ch_sime.wav" : "ji_sime.wav";
                    Convert($"{soundDir}/{nom}", hurtFiles[3]);
                    Convert($"{soundDir}/{sime}", hurtFiles[2]);
                }

                FixWeaponHitScan($"{characterPath}/weapons.csv", srcPlayer);
                FixInventoryFace($"{characterPath}/face.png", Player);

                void Convert(string target, string source)
                {
                    var targetData = GetHurtWaveData(source);
                    _crModBuilder.SetFile(target, targetData);
                }

                byte[]? GetHurtWaveData(string source)
                {
                    var data = _dataManager.GetData(source);
                    if (data == null)
                        return null;

                    var stream = new MemoryStream(data);

                    var waveformBuilder = new WaveformBuilder();
                    waveformBuilder.Append(source, stream);
                    return waveformBuilder.ToArray();
                }

                string[] GetHurtFiles(string character)
                {
                    var hurtDir = $"hurt/{character}";
                    var allHurtFiles = _dataManager.GetFiles(hurtDir)
                        .Where(x => x.EndsWith(".ogg", StringComparison.OrdinalIgnoreCase) || x.EndsWith(".wav", StringComparison.OrdinalIgnoreCase))
                        .ToArray();
                    var hurtFiles = new string[4];
                    foreach (var hurtFile in allHurtFiles)
                    {
                        if (int.TryParse(Path.GetFileNameWithoutExtension(hurtFile), out var i))
                        {
                            if (i < hurtFiles.Length)
                            {
                                hurtFiles[i] = $"{hurtDir}/{hurtFile}";
                            }
                        }
                    }
                    return hurtFiles;
                }
            }

            private void FixInventoryFace(string facePath, int pldIndex)
            {
                var facePng = _dataManager.GetData(facePath);
                if (facePng == null)
                    return;

                var timPath = "DATA/STATFACE.TIM";
                var timData = _gameDataManager.GetData(timPath);
                if (timData == null)
                    return;

                var face32 = PngToBgra32(facePng);
                var row = pldIndex / 2;
                var col = pldIndex % 2;

                var tim = new Tim(timData);
                var timBuilder = tim.ToBuilder();
                timBuilder.ImportPixels(col * 32, row * 32, 30, 30, face32, 0);
                _crModBuilder.SetFile(timPath, timBuilder.GetBytes());
            }

            private void FixWeaponHitScan(string csvPath, int srcPlayer)
            {
                var table = new short[]
                {
                    -2026, -1656, -2530, -2280, -2040, -1800,
                    -1917, -1617, -2190, -1940, -2003, -1720
                };
                var targetSpan = new Span<short>(table, Player * 6, 6);

                var csvText = _dataManager.GetText(csvPath);
                if (csvText != null)
                {
                    var csv = csvText.Split(['\n', '\r'], StringSplitOptions.RemoveEmptyEntries)
                        .Where(x => !string.IsNullOrWhiteSpace(x))
                        .Select(x => x.Trim().Split(','))
                        .ToArray();

                    for (var i = 0; i < targetSpan.Length; i++)
                    {
                        targetSpan[i] = short.Parse(csv[i][0]);
                    }
                }
                else
                {
                    var sourceSpan = new Span<short>(table, srcPlayer * 6, 6);
                    sourceSpan.CopyTo(targetSpan);
                }

                _tb.AddTweak(TWEAK_HITSCAN, MemoryMarshal.Cast<short, byte>(new Span<short>(table)));
            }

            private void AddNpcSkins()
            {
                var generator = new Re1CharacterGenerator(_dataManager, _gameDataManager, _crModBuilder);
                foreach (var kvp in _mod.Characters)
                {
                    if (kvp.Key < 2)
                        continue;

                    generator.Generate((byte)kvp.Key, kvp.Value);
                }
            }

            private void AddEnemySkins()
            {
                var skinPaths = _mod.EnemySkins;
                foreach (var skinPath in skinPaths)
                {
                    var files = _dataManager.GetFiles($"re1/emd/{skinPath}");
                    foreach (var f in files)
                    {
                        var destination = GetDestination(f);
                        if (destination == null)
                            continue;

                        var fileData = _dataManager.GetData($"re1/emd/{skinPath}/{f}");
                        if (fileData == null || fileData.Length == 0)
                            continue;

                        _crModBuilder.SetFile(destination, fileData);
                    }
                }

                string? GetDestination(string fileName)
                {
                    string[] voiceFileNamesForSoundFolder = [
                        "V_JOLT.WAV",
                        "v00d_02.wav",
                        "V00D_02S.WAV",
                        "V110_00.WAV",
                        "VB00_31.WAV",
                        "VB00_31A.WAV",
                        "VB00_31B.WAV",
                        "VB00_31C.WAV"
                    ];

                    if (fileName.EndsWith(".EMD", StringComparison.OrdinalIgnoreCase))
                    {
                        var match = Regex.Match(fileName, "EM10([0-9A-F][0-9A-F]).EMD", RegexOptions.IgnoreCase);
                        if (!match.Success)
                            return null;

                        var id = byte.Parse(match.Groups[1].Value, NumberStyles.HexNumber);
                        return $"ENEMY/EM1{Player}{id:X2}.EMD";
                    }
                    if (!fileName.EndsWith(".WAV", StringComparison.OrdinalIgnoreCase))
                        return null;
                    if (voiceFileNamesForSoundFolder.Contains(fileName, StringComparer.OrdinalIgnoreCase))
                        return $"SOUND/{fileName}";
                    if (fileName.StartsWith("VN_", StringComparison.OrdinalIgnoreCase))
                        return $"SOUND/{fileName}";
                    if (fileName.StartsWith("V", StringComparison.OrdinalIgnoreCase))
                        return $"VOICE/{fileName}";
                    return $"SOUND/{fileName}";
                }
            }

            private void AddBackgroundTextures()
            {
                var bgPng = _dataManager.GetData(BioVersion.Biohazard1, "bg.png");
                if (bgPng == null)
                    return;

                var bgPix = PngToPix(bgPng);
                _crModBuilder.SetFile("data/title.pix", bgPix);
                _crModBuilder.SetFile("type.png", bgPng);

                static byte[] PngToPix(byte[] png)
                {
                    using var img = SixLabors.ImageSharp.Image.Load<Rgba32>(png);
                    using var ms = new MemoryStream();
                    var bw = new BinaryWriter(ms);
                    img.ProcessPixelRows(accessor =>
                    {
                        for (var y = 0; y < accessor.Height; y++)
                        {
                            var row = accessor.GetRowSpan(y);
                            for (int i = 0; i < accessor.Width; i++)
                            {
                                var c = row[i];
                                var c4 = (ushort)(c.R / 8 | c.G / 8 << 5 | c.B / 8 << 10);
                                bw.Write(c4);
                            }
                        }
                    });
                    return ms.ToArray();
                }
            }

            private void AddVoices()
            {
                var voices = _mod.Voices;
                var groups = voices
                    .Select(x => (Key: VoiceTarget.Parse(x.Key), x.Value))
                    .Where(x => x.Key != null)
                    .Select(x => (Key: x.Key!.Value, x.Value))
                    .OrderBy(x => x.Key.Path)
                    .ThenBy(x => x.Key.Range)
                    .GroupBy(x => x.Key.Path);
                foreach (var g in groups)
                {
                    var destinationPath = g.Key;
                    var items = g.ToArray();
                    var wavBuilder = new WaveformBuilder();
                    var time = 0.0;
                    for (var i = 0; i < items.Length; i++)
                    {
                        var (k, sourcePath) = items[i];
                        var sourceStream = new MemoryStream(_dataManager.GetData(sourcePath));
                        var silenceDuration = k.Range.Start - time;
                        var duration = k.Range.End == 0 ? double.NaN : k.Range.Length;
                        wavBuilder.AppendSilence(silenceDuration);
                        wavBuilder.Append(sourcePath, sourceStream, 0, duration);
                        if (!double.IsNaN(duration))
                        {
                            var remaining = k.Range.End - wavBuilder.Duration;
                            wavBuilder.AppendSilence(remaining);
                        }
                        time = k.Range.End;
                    }
                    _crModBuilder.SetFile(destinationPath, wavBuilder.ToArray());
                }
            }

            private readonly struct VoiceTarget(string path, AudioRange range = default)
            {
                public string Path => path;
                public AudioRange Range => range;

                public static VoiceTarget? Parse(string s)
                {
                    var pattern = @"^(?<path>[^()]+)(?:\((?<start>\d+(?:\.\d+)?),(?<end>\d+(?:\.\d+)?)\))?$";
                    var match = Regex.Match(s, pattern);
                    if (!match.Success)
                        return null;

                    var path = match.Groups["path"].Value;
                    if (match.Groups["start"].Success && match.Groups["end"].Success)
                    {
                        var start = double.Parse(match.Groups["start"].Value);
                        var end = double.Parse(match.Groups["end"].Value);
                        var audioRange = new AudioRange(start, end);
                        return new VoiceTarget(path, audioRange);
                    }
                    return new VoiceTarget(path);
                }
            }

            private void AddMusic()
            {
                var bgmTable = _dataManager.GetData(BioVersion.Biohazard1, "bgm_tbl.xml");
                _crModBuilder.SetFile("bgm_tbl.xml", bgmTable);

                var encoder = new BgmBatchEncoder(_dataManager);
                encoder.Process(_mod, _crModBuilder);
            }

            private void AddTitleCall()
            {
                if (_mod.General.GetValueOrDefault("titleSound") is not string titleSound)
                    return;

                var stream = new MemoryStream(_dataManager.GetData(titleSound));
                var waveform = new WaveformBuilder();
                waveform.Append(titleSound, stream);
                var waveData = waveform.ToArray();

                _crModBuilder.SetFile("SOUND/BIO01.WAV", waveData);
                _crModBuilder.SetFile("SOUND/EVIL01.WAV", waveData);
            }

            private void AddDynamicTweaks()
            {
                _tb.AddTweak(TWEAK_INVENTORY_SIZE, [(byte)InventorySize]);
                _crModBuilder.SetFile("biorand.dat", _tb.Build());
            }

            private void AddMiscXml()
            {
                var miscTable = _dataManager.GetData(BioVersion.Biohazard1, "misc.xml");
                _crModBuilder.SetFile("misc.xml", miscTable);
            }

            private void AddSoundXml()
            {
                var xml = _dataManager.GetText(BioVersion.Biohazard1, "sounds.xml");
                var doc = new XmlDocument();
                doc.LoadXml(xml);

                var enemyPlacements = _mod.EnemyPlacements;
                var roomNodes = doc.SelectNodes("Rooms/Room");
                foreach (XmlNode roomNode in roomNodes)
                {
                    var idAttribute = roomNode.Attributes["id"];
                    if (idAttribute == null)
                        continue;

                    if (!RdtId.TryParse(idAttribute.Value, out var roomId))
                        continue;

                    var firstEnemy = enemyPlacements.FirstOrDefault(x => x.RdtId == roomId);
                    var firstEnemyType = (byte?)firstEnemy?.Type;
                    FixRoomSounds(roomId, firstEnemyType, roomNode);
                }

                void FixRoomSounds(RdtId rdtId, byte? enemyType, XmlNode roomNode)
                {
                    if (enemyType != null)
                    {
                        var template = GetTemplateXml(enemyType.Value);
                        var entryNodes = roomNode.SelectNodes("Sound/Entry");
                        for (int i = 0; i < 16; i++)
                        {
                            entryNodes[i].InnerText = template[i] ?? "";
                        }
                    }
                    _crModBuilder.SetFile($"tables/room_{rdtId}.xml", roomNode.InnerXml);
                }

                static string[] GetTemplateXml(byte enemyType)
                {
                    string[]? result = null;
                    switch (enemyType)
                    {
                        case Re1EnemyIds.Zombie:
                            result = ["z_taore", "z_ftL", "z_ftR", "z_kamu", "z_k02", "z_k01", "z_head", "z_haki", "z_sanj", "z_k03"];
                            break;
                        case Re1EnemyIds.ZombieNaked:
                            result = ["z_taore", "zep_ftL", "z_ftR", "ze_kamu", "z_nisi2", "z_nisi1", "ze_head", "ze_haki", "ze_sanj", "z_nisi3", "FL_walk", "FL_jump", "steam_b", "FL_ceil", "FL_fall", "FL_slash"];
                            break;
                        case Re1EnemyIds.Cerberus:
                            result = ["cer_foot", "cer_taoA", "cer_unar", "cer_bite", "cer_cryA", "cer_taoB", "cer_jkMX", "cer_kamu", "cer_cryB", "cer_runMX"];
                            break;
                        case Re1EnemyIds.WebSpinner:
                            result = ["kuasi_A", "kuasi_B", "kuasi_C", "sp_rakk", "sp_atck", "sp_bomb", "sp_fumu", "sp_Doku", "sp_sanj2"];
                            break;
                        case Re1EnemyIds.BlackTiger:
                            result = ["kuasi_A", "kuasi_B", "kuasi_C", "sp_rakk", "sp_atck", "sp_bomb", "sp_fumu", "sp_Doku", "poison"];
                            break;
                        case Re1EnemyIds.Crow:
                            result = ["RVcar1", "RVpat", "RVcar2", "RVwing1", "RVwing2", "RVfryed"];
                            break;
                        case Re1EnemyIds.Hunter:
                            result = ["HU_walkA", "HU_walkB", "HU_jump", "HU_att", "HU_land", "HU_smash", "HU_dam", "HU_Nout"];
                            break;
                        case Re1EnemyIds.Wasp:
                            result = ["bee4_ed", "hatinage", "bee_fumu"];
                            break;
                        case Re1EnemyIds.Plant42:
                            break;
                        case Re1EnemyIds.Chimera:
                            result = ["FL_walk", "FL_jump", "steam_b", "FL_ceil", "FL_fall", "FL_slash", "FL_att", "FL_dam", "FL_out"];
                            break;
                        case Re1EnemyIds.Snake:
                            result = ["PY_mena", "PY_hit2", "PY_fall"];
                            break;
                        case Re1EnemyIds.Neptune:
                            result = ["nep_attB", "nep_attA", "nep_nomu", "nep_tura", "nep_twis", "nep_jump"];
                            break;
                        case Re1EnemyIds.Tyrant1:
                            result = ["TY_foot", "TY_kaze", "TY_slice", "TY_HIT", "TY_trust", "", "TY_taore", "TY_nage"];
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
                            result = ["z_taore", "z_ftL", "z_ftR", "z_kamu", "z_mika02", "z_mika01", "z_head", "z_Hkick", "z_Ugoron", "z_mika03"];
                            break;
                        case Re1EnemyIds.Yawn2:
                            break;
                    }
                    Array.Resize(ref result, 16);
                    return result;
                }
            }

            private static byte[] PngToPix(byte[] png)
            {
                using var img = SixLabors.ImageSharp.Image.Load<Rgba32>(png);
                using var ms = new MemoryStream();
                var bw = new BinaryWriter(ms);
                img.ProcessPixelRows(accessor =>
                {
                    for (var y = 0; y < accessor.Height; y++)
                    {
                        var row = accessor.GetRowSpan(y);
                        for (int i = 0; i < accessor.Width; i++)
                        {
                            var c = row[i];
                            var c4 = (ushort)(c.R / 8 | c.G / 8 << 5 | c.B / 8 << 10);
                            bw.Write(c4);
                        }
                    }
                });
                return ms.ToArray();
            }

            private static uint[] PngToBgra32(byte[] png)
            {
                using var img = SixLabors.ImageSharp.Image.Load<Rgba32>(png);
                var result = new uint[img.Width * img.Height];
                img.ProcessPixelRows(accessor =>
                {
                    var dst = result;
                    var index = 0;
                    for (var y = 0; y < accessor.Height; y++)
                    {
                        var row = accessor.GetRowSpan(y);
                        for (int i = 0; i < accessor.Width; i++)
                        {
                            var c = row[i];
                            dst[index] = (uint)(c.B | (c.G << 8) | (c.R << 16) | (c.A << 24));
                            index++;
                        }
                    }
                });
                return result;
            }

            private static readonly RdtId[] g_missingRooms =
            [
                RdtId.Parse("110"),
                RdtId.Parse("119"),
                RdtId.Parse("200"),
                RdtId.Parse("20C"),
                RdtId.Parse("213"),
                RdtId.Parse("214"),
                RdtId.Parse("215"),
                RdtId.Parse("216"),
                RdtId.Parse("217"),
                RdtId.Parse("218"),
                RdtId.Parse("219"),
                RdtId.Parse("21A"),
                RdtId.Parse("21B"),
                RdtId.Parse("21C")
            ];
        }
    }

    internal class DynamicTweaksBuilder
    {
        private readonly MemoryStream _ms = new MemoryStream();
        private readonly BinaryWriter _bw;

        public DynamicTweaksBuilder()
        {
            _bw = new BinaryWriter(_ms);
        }

        public void AddTweak(int kind, ReadOnlySpan<byte> data)
        {
            var bw = new BinaryWriter(_ms);
            bw.Write(kind);
            bw.Write(data.Length);
            bw.Write(data);
        }

        public byte[] Build()
        {
            return _ms.ToArray();
        }
    }
}
