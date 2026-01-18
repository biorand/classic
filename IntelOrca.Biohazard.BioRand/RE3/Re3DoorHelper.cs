using System;
using System.IO;
using System.Linq;
using IntelOrca.Biohazard.Script;
using IntelOrca.Biohazard.Script.Opcodes;

namespace IntelOrca.Biohazard.BioRand.RE3
{
    internal class Re3DoorHelper : IDoorHelper
    {
        public byte[] GetReservedLockIds()
        {
            return new byte[] { 0, 1, 3, 4, 9, 10, 12, 13, 14, 16, 17, 19, 20, 21, 25, 26, 27, 29, 32, 33, 34, 35 };
        }

        public void Begin(RandoConfig config, GameData gameData, Map map)
        {
            if (!config.RandomDoors)
                return;

            // For 30B, change boss door to id 30 so we can separate it from the normal door
            var rdt = gameData.GetRdt(new RdtId(2, 0x0B));
            if (rdt != null)
            {
                var bossDoor = rdt.Doors.Skip(1).First();
                bossDoor.Id = 30;
            }
        }

        public void End(RandoConfig config, GameData gameData, Map map)
        {
            UnblockRpdDoor();
            FixSalesOffice();
            FixLockpickDoor();
            FixStagla();
            FixTrain();
            UnblockWasteDisposalDoor();
            FixTowerOutdoor();
            FixNemesisFlag();
            FixWarehouseAlley();
            FixHydrantAlley();
            if (config.RandomDoors)
            {
                FixConditionalCuts();
                FixRestuarantFront();
                FixRestuarant();
                FixPressOffice();
                FixTrainCrashExit();
                FixRain();
            }
            else
            {
                FixBarricadeAlley();
            }

            void FixConditionalCuts()
            {
                // Sales office alley
                AddCutCorrectionFor(new RdtId(0, 0x0B), 99, 0, 4);
                // Press street
                AddCutCorrectionFor(new RdtId(1, 0x07), 143, 0, 10);
                // Piano room
                AddCutCorrectionFor(new RdtId(2, 0x01), 112, 3, 10);
                // Laboratory
                AddCutCorrectionFor(new RdtId(3, 0x0A), 91, 3, 11);
                // SynthesisRoom
                AddCutCorrectionFor(new RdtId(3, 0x0B), 179, 1, 11);
                AddCutCorrectionFor(new RdtId(3, 0x0B), 180, 11, 16);
                // CommsRoom
                AddCutCorrectionFor(new RdtId(4, 0x0A), 199, 11, 3);
            }

            void UnblockRpdDoor()
            {
                var rdt = gameData.GetRdt(new RdtId(0, 0x11));
                if (rdt == null)
                    return;

                if (config.RandomDoors)
                    rdt.Nop(0x2CAE, 0x2CF8);
                else if (config.RandomItems)
                    rdt.Nop(0x2CB6, 0x2CD2);
            }

            void FixSalesOffice()
            {
                var rdt = gameData.GetRdt(new RdtId(0, 0x1B));
                if (rdt == null)
                    return;

                // Allow remote to be used after the door has been unlocked
                rdt.Nop(0x4328);
                rdt.Nop(0x49B0);
                rdt.Nop(0x4B5A);
            }

            void FixLockpickDoor()
            {
                var rdt1 = gameData.GetRdt(new RdtId(0, 0x0A));
                var rdt2 = gameData.GetRdt(new RdtId(0, 0x24));
                if (rdt1 != null && rdt2 != null)
                {
                    var lockpickDoor1 = rdt1.Doors.First(x => x.Id == 2);
                    var lockpickDoor2 = rdt2.Doors.First(x => x.Id == 2);
                    CopyDoorTo(lockpickDoor1, lockpickDoor2);
                }
            }

            void FixStagla()
            {
                var rdt = gameData.GetRdt(new RdtId(1, 0x0E));
                if (rdt != null)
                {
                    rdt.Nop(0x2BB6);
                }
            }

            void FixBarricadeAlley()
            {
                if (!config.RandomItems)
                    return;

                // Force barricade alley door to go to 2nd hydrant alley
                var rdt = gameData.GetRdt(new RdtId(0, 0x08));
                if (rdt != null)
                {
                    var door = rdt.Doors.FirstOrDefault(x => x.Id == 5);
                    if (door != null)
                    {
                        door.Target = new RdtId(0, 0x23);
                    }
                }

                // Force hydrant alley save to go to 2nd hydrant alley
                rdt = gameData.GetRdt(new RdtId(0, 0x0C));
                if (rdt != null)
                {
                    rdt.Nop(0x42A, 0x452);
                }

                // Force RPD street 1 to go to 2nd hydrant alley
                rdt = gameData.GetRdt(new RdtId(0, 0x0A));
                if (rdt != null)
                {
                    var door = rdt.Doors.FirstOrDefault(x => x.Id == 0);
                    if (door != null)
                    {
                        door.Target = new RdtId(0, 0x23);
                    }
                }

                // Make the doors of the hydrant alley go to day or night rooms
                var dayRdt = gameData.GetRdt(new RdtId(0, 0x09));
                rdt = gameData.GetRdt(new RdtId(0, 0x23));
                if (dayRdt != null && rdt != null)
                {
                    var doors = rdt.Doors.ToArray();
                    var door122 = (DoorAotSeOpcode)rdt.Doors.First(x => x.Target == new RdtId(0, 0x22));
                    var door124 = (DoorAotSeOpcode)rdt.Doors.First(x => x.Target == new RdtId(0, 0x24));

                    var door108 = (DoorAotSeOpcode)dayRdt.Doors.First(x => x.Target == new RdtId(0, 0x08));
                    var door10A = (DoorAotSeOpcode)dayRdt.Doors.First(x => x.Target == new RdtId(0, 0x0A));

                    // if (bits[3][9] == 1)
                    rdt.AdditionalOpcodes.Add(new UnknownOpcode(0, 0x06, new byte[] { 0x00, 0x48, 0x00 }));
                    rdt.AdditionalOpcodes.Add(new UnknownOpcode(0, 0x4C, new byte[] { 0x03, 0x09, 0x01 }));
                    rdt.AdditionalOpcodes.Add(door122);
                    rdt.AdditionalOpcodes.Add(door124);
                    // else
                    rdt.AdditionalOpcodes.Add(new UnknownOpcode(0, 0x07, new byte[] { 0x00, 0x44, 0x00 }));
                    rdt.AdditionalOpcodes.Add(door108);
                    rdt.AdditionalOpcodes.Add(door10A);

                    // Remove original door opcodes
                    rdt.Nop(door122.Offset);
                    rdt.Nop(door124.Offset);
                }

                // Parasite alley needs to go to day or night RPD street
                rdt = gameData.GetRdt(new RdtId(1, 0x1A));
                if (rdt != null)
                {
                    var door124 = (DoorAotSeOpcode)rdt.Doors.First(x => x.Id == 0);
                    var door10A = CopyDoor(door124, new RdtId(0, 0x0A));

                    // if (bits[3][9] == 1)
                    rdt.AdditionalOpcodes.Add(new UnknownOpcode(0, 0x06, new byte[] { 0x00, 0x28, 0x00 }));
                    rdt.AdditionalOpcodes.Add(new UnknownOpcode(0, 0x4C, new byte[] { 0x03, 0x09, 0x01 }));
                    rdt.AdditionalOpcodes.Add(door124);
                    // else
                    rdt.AdditionalOpcodes.Add(new UnknownOpcode(0, 0x07, new byte[] { 0x00, 0x24, 0x00 }));
                    rdt.AdditionalOpcodes.Add(door10A);

                    // Remove original door
                    rdt.Nop(door124.Offset);
                }
            }

            void FixHydrantAlley()
            {
                var rdt = gameData.GetRdt(new RdtId(0, 0x23));
                if (rdt != null)
                {
                    // if (bits[3][28] == 1)
                    rdt.AdditionalOpcodes.Add(new UnknownOpcode(0, 0x06, new byte[] { 0x00, 0x5B, 0x00 }));
                    rdt.AdditionalOpcodes.Add(new UnknownOpcode(0, 0x4C, new byte[] { 0x03, 0x1C, 0x01 }));

                    // cut_replace([0..8], [11..19]);
                    for (int i = 0; i < 9; i++)
                        rdt.AdditionalOpcodes.Add(new UnknownOpcode(0, 0x53, new byte[] { (byte)i, (byte)(i + 11) }));

                    foreach (var cut in new[] { 0, 4, 5, 7 })
                    {
                        // if (cut == 0)
                        rdt.AdditionalOpcodes.Add(new UnknownOpcode(0, 0x06, new byte[] { 0x00, 0x0A, 0x00 }));
                        rdt.AdditionalOpcodes.Add(new UnknownOpcode(0, 0x4E, new byte[] { 0x00, 0x1A, 0x00, (byte)cut, 0x00 }));
                        // cut_chg(11);
                        rdt.AdditionalOpcodes.Add(new UnknownOpcode(0, 0x50, new byte[] { (byte)(cut + 11) }));
                        // endif
                        rdt.AdditionalOpcodes.Add(new UnknownOpcode(0, 0x08, new byte[] { 0x00 }));
                    }

                    // cut_auto(1);
                    rdt.AdditionalOpcodes.Add(new UnknownOpcode(0, 0x52, new byte[] { 1 }));
                    // endif
                    rdt.AdditionalOpcodes.Add(new UnknownOpcode(0, 0x08, new byte[] { 0x00 }));

                    // NOP out old cut_replace
                    rdt.Nop(0x2DC8);
                    rdt.Nop(0x2DCB);
                    rdt.Nop(0x2DCE);
                    rdt.Nop(0x2DD1);
                    rdt.Nop(0x2DD4);
                    rdt.Nop(0x2DD7);
                    rdt.Nop(0x2DDA);
                    rdt.Nop(0x2DDD);
                    rdt.Nop(0x2DE0);

                    // NOP out Nemesis encounter (seems to break when we have the cut_replace at the top)
                    rdt.Nop(0x2E18);
                    rdt.Nop(0x2E30);
                    rdt.Nop(0x2E34);
                }
            }

            void FixRestuarantFront()
            {
                var rdt = gameData.GetRdt(new RdtId(1, 0x05));
                if (rdt != null)
                {
                    CopyDoorTo(rdt, 3, 2);
                }
            }

            void FixRestuarant()
            {
                var rdt = gameData.GetRdt(new RdtId(1, 0x11));
                if (rdt != null)
                {
                    CopyDoorTo(rdt, 23, 5);
                }
            }

            void FixPressOffice()
            {
                var rdt = gameData.GetRdt(new RdtId(1, 0x0F));
                if (rdt != null)
                {
                    CopyDoorTo(rdt, 1, 0);
                    CopyDoorTo(rdt, 2, 0);
                    CopyDoorTo(rdt, 16, 0);

                    var door = rdt.Doors.First(x => x.Id == 2);
                    door.LockId = 0;
                    door.LockType = 0;
                }
            }

            void FixTrainCrashExit()
            {
                var rdt = gameData.GetRdt(new RdtId(1, 0x15));
                if (rdt != null)
                {
                    CopyDoorTo(rdt, 5, 4);
                }
            }

            void FixRain()
            {
                // Rain often crashes for me
                var rainRooms = new[]
                {
                    0x00, 0x0C, 0x0D, 0x0E, 0x0F, 0x11, 0x17
                };
                foreach (var rainRoom in rainRooms)
                {
                    var rdt = gameData.GetRdt(new RdtId(3, rainRoom));
                    if (rdt != null)
                    {
                        var rainOpcodes = rdt.Opcodes
                            .Where(x => x.Opcode == (byte)OpcodeV3.RainSet)
                            .ToArray();
                        foreach (var opcode in rainOpcodes)
                        {
                            rdt.Nop(opcode.Offset);
                        }
                    }
                }
            }

            void AddCutCorrectionFor(RdtId rdtId, byte flag3, byte originalCut, byte newCut)
            {
                var rdt = gameData.GetRdt(rdtId);
                if (rdt != null)
                {
                    AddCutCorrection(rdt, flag3, originalCut, newCut);
                }
            }

            void AddCutCorrection(RandomizedRdt rdt, byte flag3, byte originalCut, byte newCut)
            {
                // if (bits[3][flag3] == 1 && cut == originalCut)
                rdt.AdditionalOpcodes.Add(new UnknownOpcode(0, 0x06, new byte[] { 0x00, 0x10, 0x00 }));
                rdt.AdditionalOpcodes.Add(new UnknownOpcode(0, 0x4C, new byte[] { 0x03, flag3, 0x01 }));
                rdt.AdditionalOpcodes.Add(new UnknownOpcode(0, 0x4E, new byte[] { 0x00, 0x1A, 0x00, originalCut, 0x00 }));

                // cut_chg(newCut);
                rdt.AdditionalOpcodes.Add(new UnknownOpcode(0, 0x50, new byte[] { newCut }));

                // cut_auto(1);
                rdt.AdditionalOpcodes.Add(new UnknownOpcode(0, 0x52, new byte[] { 1 }));

                // endif
                rdt.AdditionalOpcodes.Add(new UnknownOpcode(0, 0x08, new byte[] { 0x00 }));
            }

            void FixTrain()
            {
                var rdt = gameData.GetRdt(new RdtId(1, 0x0C));
                if (rdt == null)
                    return;

                if (!config.RandomItems)
                    return;

                var elseIndex = Array.FindIndex(rdt.Opcodes, x => x.Offset == 0x4EE4);
                if (elseIndex != -1)
                {
                    rdt.Opcodes[elseIndex] = new UnknownOpcode(0x4EE4, (byte)OpcodeV3.EndIf, new byte[] { 0x00, 0x00 });
                }
            }

            void UnblockWasteDisposalDoor()
            {
                var rdt = gameData.GetRdt(new RdtId(4, 0x09));
                if (rdt == null)
                    return;

                if (!config.RandomItems)
                    return;

                // 0: door
                rdt.Nop(0x599A);
                rdt.Nop(0x2876);

                // 2: card reader
                // rdt.Nop(0x2782);
                // rdt.Nop(0x286C);

                // 3
                rdt.Nop(0x58EC);
                rdt.Nop(0x2862);

                // 4
                rdt.Nop(0x5900);
                rdt.Nop(0x2858);
            }

            void FixTowerOutdoor()
            {
                var rdt = gameData.GetRdt(new RdtId(2, 0x0B));
                if (rdt == null)
                    return;

                if (!config.RandomItems)
                    return;

                // Prevent Nemesis event from happening if clock puzzle is complete

                // if (bits[3][158] == 1)
                rdt.AdditionalOpcodes.Add(new UnknownOpcode(0, 0x06, new byte[] { 0x00, 0x0A, 0x00 }));
                rdt.AdditionalOpcodes.Add(new UnknownOpcode(0, 0x4C, new byte[] { 3, 158, 1 }));
                // bits[3][138] = 1
                rdt.AdditionalOpcodes.Add(new UnknownOpcode(0, 0x4D, new byte[] { 3, 138, 1 }));
                // endif
                rdt.AdditionalOpcodes.Add(new UnknownOpcode(0, 0x08, new byte[] { 0x00 }));

                if (!config.RandomDoors)
                    return;

                // Fix camera angle if Nemesis event has happened
                AddCutCorrection(rdt, 162, 0, 21);
            }

            void FixNemesisFlag()
            {
                foreach (var rdt in gameData.Rdts)
                {
                    // Turn off some kind of Nemesis attack
                    // bits[1][0] = 0
                    rdt.AdditionalOpcodes.Add(new UnknownOpcode(0, 0x4D, new byte[] { 0x01, 0x00, 0x00 }));
                }
            }

            void FixWarehouseAlley()
            {
                var rdt = gameData.GetRdt(new RdtId(0, 0x1D));
                if (rdt == null)
                    return;

                rdt.Nop(0x0AA0);
            }
        }

        private static void CopyDoorTo(RandomizedRdt rdt, int dstId, int srcId)
        {
            var src = rdt.Doors.First(x => x.Id == srcId);
            foreach (var dst in rdt.Doors.Where(x => x.Id == dstId))
            {
                CopyDoorTo(dst, src);
            }
            foreach (var dst in rdt.Resets.Where(x => x.Id == dstId && x.SCE == 1))
            {
                dst.NextX = src.NextX;
                dst.NextY = src.NextY;
                dst.NextZ = src.NextZ;
            }
        }

        private static void CopyDoorTo(IDoorAotSetOpcode dst, IDoorAotSetOpcode src)
        {
            dst.Target = src.Target;
            dst.NextX = src.NextX;
            dst.NextY = src.NextY;
            dst.NextZ = src.NextZ;
            dst.NextD = src.NextD;
            dst.NextFloor = src.NextFloor;
            dst.NextCamera = src.NextCamera;
            dst.LockId = src.LockId;
            dst.LockType = src.LockType;
        }

        private static OpcodeBase CopyDoor(DoorAotSeOpcode src, RdtId newTarget)
        {
            var ms = new MemoryStream();
            var bw = new BinaryWriter(ms);
            src.Write(bw);
            ms.Position = 0;
            var result = DoorAotSeOpcode.Read(new BinaryReader(ms), 0);
            result.Target = newTarget;
            return result;
        }
    }
}
