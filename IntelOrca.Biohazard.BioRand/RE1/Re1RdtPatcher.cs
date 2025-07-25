using System;
using System.Linq;
using System.Reflection;
using IntelOrca.Biohazard.Script.Opcodes;

namespace IntelOrca.Biohazard.BioRand.RE1
{
    /// <summary>
    /// Patches many things within in the RE 1 RDT scripts.
    /// </summary>
    /// <param name="gameData"></param>
    /// <param name="mod"></param>
    internal class Re1RdtPatcher(GameData gameData, ClassicMod mod)
    {
        private const byte PassCodeDoorLockId = 128 | 81;

        public int Player => mod.GetGeneralValue<int>("player");
        public bool RandomEnemies => mod.GetGeneralValue<bool>("randomEnemies");
        public bool RandomItems => mod.GetGeneralValue<bool>("randomItems");
        public bool RandomDoors => mod.GetGeneralValue<bool>("randomDoors");
        public bool InkEnabled => mod.GetGeneralValue<bool>("ink");
        public bool LockpickEnabled => mod.GetGeneralValue<bool>("lockpick");
        public bool HelipadTyrantForced => mod.GetGeneralValue<bool>("forceTyrant");

        public void Patch()
        {
            var methods = GetType().GetMethods();
            foreach (var m in methods)
            {
                var patchAttribute = m.GetCustomAttribute<PatchAttribute>();
                if (patchAttribute is null)
                    continue;

                if (patchAttribute.Player != -1 && patchAttribute.Player != Player)
                    continue;

                var parameters = m.GetParameters();
                var arguments = new object?[parameters.Length];

                var bothMansions = patchAttribute.BothMansions;
                for (var mansion = 0; mansion < (bothMansions ? 2 : 1); mansion++)
                {
                    for (var i = 0; i < parameters.Length; i++)
                    {
                        var p = parameters[i];
                        if (p.Name.StartsWith("rdt"))
                        {
                            var rdtId = RdtId.Parse(p.Name[3..]);
                            if ((rdtId.Stage == 0 || rdtId.Stage == 1) && mansion == 1)
                                rdtId = new RdtId(rdtId.Stage + 5, rdtId.Room, rdtId.Variant);
                            arguments[i] = gameData.GetRdt(rdtId);
                        }
                    }
                    m.Invoke(this, arguments);
                }
            }
        }

        [Patch]
        public void ConfigureOptions(RandomizedRdt rdt106)
        {
            if (Player == 0)
            {
                rdt106.AdditionalOpcodes.AddRange(
                    ScdCondition.Parse("1:0").Generate(BioVersion.Biohazard1, [
                        Set(0, 123, (byte)(InkEnabled ? 0 : 1)), // Ink
                        Set(0, 124, (byte)(LockpickEnabled ? 0 : 1)) // Lockpick
                    ])
                );
            }
            else
            {
                rdt106.AdditionalOpcodes.AddRange(
                    ScdCondition.Parse("1:0").Generate(BioVersion.Biohazard1, [
                        Set(0, 123, (byte)(InkEnabled ? 0 : 1)), // Ink
                        Set(0, 124, (byte)(LockpickEnabled ? 0 : 1)), // Lockpick
                    ])
                );
                if (!LockpickEnabled)
                {
                    rdt106.Nop(0x31B02); // Disable Barry giving Jill the lockpick
                }
            }

            static UnknownOpcode Set(byte group, byte index, byte value)
            {
                return new UnknownOpcode(0, 0x05, [group, index, value]);
            }
        }

#if false
        [Patch]
        public void EnableMoreJillItems(RandomizedRdt rdt106)
        {
            rdt106.Patch(0x2FC02 + 1, 7);
            rdt106.Patch(0x2FC02 + 2, 52);
            rdt106.Patch(0x2FC02 + 3, 0);
            rdt106.Nop(0x2FC06);
            rdt106.Patch(0x2FFBC + 1, 7);
            rdt106.Patch(0x2FFBC + 2, 52);
            rdt106.Patch(0x2FFBC + 3, 0);
            rdt106.Nop(0x2FFC0);
            rdt106.Nop(0x31862);
        }
#endif

        [Patch(BothMansions = true)]
        public void FixMapsAsItems(
            RandomizedRdt rdt107,
            RandomizedRdt rdt20B,
            RandomizedRdt rdt300,
            RandomizedRdt rdt30F,
            RandomizedRdt rdt406)
        {
            if (mod.Items.ContainsKey(145))
            {
                // 107 mansion 1F map
                // Set item aot coordinates
                SetItemAot(rdt107, 0x347DE, 0x347FE);

                // Change enabled/disabled of the item aot instead of the event aot
                rdt107.Patch(0x349C4 + 1, 0x02);
                rdt107.Patch(0x349C4 + 2, 0x04);
                rdt107.Patch(0x349CA + 1, 0x02);
                rdt107.Patch(0x349CA + 2, 0x04);
            }

            if (mod.Items.TryGetValue(17, out var item))
            {
                // 20B mansion 2F map
                rdt20B.AdditionalOpcodes.Add(new ItemAotSetOpcode()
                {
                    Opcode = 0x18,
                    Id = 4,
                    X = 5050,
                    Y = 1400,
                    W = 500,
                    H = 1800,
                    Type = item.Type,
                    Amount = item.Amount,
                    Re1Unk0C = 1,
                    Re1Unk14 = 0,
                    Re1Unk15 = 0,
                    GlobalId = 17,
                    Re1Unk17 = 129,
                    TakeAnimation = 0,
                    Re1Unk19 = 0
                });
                rdt20B.Nop(0xCD68);
                rdt20B.Nop(0xCE52);
            }

            if (mod.Items.ContainsKey(79))
            {
                // 300 courtyard map
                rdt300.Nop(0x15E66, 0x15E78);
            }

            if (mod.Items.ContainsKey(118))
            {
                // 30F caves map
                SetItemAot(rdt30F, 0x3589E, 0x35A18);
                rdt30F.Nop(0x35B04);
            }

            if (mod.Items.ContainsKey(135))
            {
                // 406 guardhouse map
                SetItemAot(rdt406, 0x24F88, 0x24F26);
            }
        }

        [Patch(BothMansions = true)]
        public void FixDocuments(RandomizedRdt rdt20A)
        {
            if (mod.Items.ContainsKey(16))
            {
                // 20A fish tank document on desk
                if (Player == 0)
                {
                    SetItemAot(rdt20A, 0x179EC, 0x17948, clearSource: false);
                    SetItemAot(rdt20A, 0x17A18, 0x17948, clearSource: true);
                }
                else
                {
                    SetItemAot(rdt20A, 0x179EC, 0x17948, clearSource: false);
                    SetItemAot(rdt20A, 0x17A1E, 0x17948, clearSource: false);
                    SetItemAot(rdt20A, 0x17A4A, 0x17948, clearSource: true);
                }
            }
        }

        [Patch]
        public void DisableDogWindows(RandomizedRdt rdt108)
        {
            rdt108.Nop(0x19754, 0x197EE);
        }

        [Patch]
        public void DisableDogBoiler(RandomizedRdt rdt114)
        {
            rdt114.Nop(0x24B80, 0x24C1C);
        }

        [Patch]
        public void DisableHunterAmbushes(RandomizedRdt rdt603, RandomizedRdt rdt609)
        {
            if (RandomEnemies)
            {
                rdt603.Nop(0x1ABA2, 0x1ABD4);
                if (Player == 0)
                    rdt609.Nop(0x1CB8E, 0x1CBC0);
                else
                    rdt609.Nop(0x1CB52, 0x1CB84);
            }
        }

        [Patch(BothMansions = true)]
        public void AddDoor207(RandomizedRdt rdt207)
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

        [Patch]
        public void FixDoor104(RandomizedRdt rdt104)
        {
            var door = rdt104.Doors.FirstOrDefault(x => x.Id == 2);
            door.NextX = 12700;
            door.NextY = -7200;
            door.NextZ = 3300;
        }

        [Patch]
        public void FixDoor106(RandomizedRdt rdt106)
        {
            if (Player == 0)
            {
                rdt106.Nop(0x2FBD4, 0x2FBD6);
                rdt106.Nop(0x2FBF4, 0x2FBF6);

                var frontDoor = (DoorAotSeOpcode)rdt106.Doors.First(x => x.Id == 3);
                frontDoor.Animation = 3;
                frontDoor.Special = 6;
            }
            else
            {
                rdt106.Nop(0x2FBD4);
                rdt106.AdditionalOpcodes.Add(new DoorAotSeOpcode()
                {
                    Opcode = 0x0C,
                    Id = 3,
                    X = 14600,
                    Z = 800,
                    W = 5200,
                    D = 2200,
                    Special = 6,
                    Re1UnkB = 0,
                    Animation = 3,
                    Re1UnkC = 1,
                    LockId = 0,
                    Target = new RdtId(255, 0x06),
                    NextX = 0,
                    NextY = 0,
                    NextZ = 0,
                    NextD = 0,
                    LockType = 0,
                    Free = 129
                });
            }
        }

        [Patch(BothMansions = true)]
        public void FixDoorToWardrobe(RandomizedRdt rdt112)
        {
            rdt112.Nop(0x17864, 0x17866);
            rdt112.Nop(0x17884, 0x17886);
        }

        [Patch]
        public void FixPassCodeDoor(RandomizedRdt rdt201)
        {
            var door = rdt201.Doors.FirstOrDefault(x => x.Id == 1) as DoorAotSeOpcode;
            if (door == null)
                return;

            door.LockId = PassCodeDoorLockId;
            door.NextX = 11200;
            door.NextZ = 28000;
            door.LockType = 255;
            door.Free = 129;

            if (!RandomDoors && Player == 1)
            {
                rdt201.AdditionalOpcodes.Add(new UnknownOpcode(0, 0x01, [0x0A]));
                rdt201.AdditionalOpcodes.Add(new UnknownOpcode(0, 0x04, [0x01, 0x25, 0x00]));
                rdt201.AdditionalOpcodes.Add(new UnknownOpcode(0, 0x05, [0x02, PassCodeDoorLockId, 0]));
                rdt201.AdditionalOpcodes.Add(new UnknownOpcode(0, 0x03, [0x00]));
            }

            rdt201.Nop(0x41A34);

            if (rdt201.RdtId.Stage == 1)
            {
                if (Player == 0)
                {
                    rdt201.Nop(0x41A92);
                }
                else
                {
                    rdt201.Nop(0x41A5C, 0x41A68);
                }
            }
        }

        [Patch(Player = 0)]
        public void FixDrugStoreRoom(RandomizedRdt rdt409)
        {
            rdt409.Nop(0x166D4, 0x16742);
            rdt409.Nop(0x168AA, 0x16918);
        }

        [Patch(Player = 0)]
        public void FixChrisPlant42(
            RandomizedRdt rdt408,
            RandomizedRdt rdt409,
            RandomizedRdt rdt40C,
            RandomizedRdt rdt40F)
        {
            // Disable switch to Rebecca
            rdt40C.Nop(0x66DC, 0x6712);

            // Fix V-JOLT switch back
            rdt40F.Nop(0x1F782);

            // Force Rebecca cutscene
            rdt408.Nop(0x30B4E);
            rdt408.Nop(0x30B90);

            // Force Rebecca in drug store room
            rdt409.Nop(0x16856);
        }

        [Patch(BothMansions = true)]
        public void AllowRoughPassageDoorUnlock(RandomizedRdt rdt214)
        {
            var doorId = Player == 0 ? 1 : 5;
            var door = (DoorAotSeOpcode)rdt214.ConvertToDoor((byte)doorId, 0, 254, PassCodeDoorLockId);
            door.Special = 2;
            door.Re1UnkC = 1;
            door.Target = new RdtId(0xFF, 0x01);
            door.NextX = 15500;
            door.NextZ = 25400;
            door.NextD = 1024;

            if (Player == 1)
            {
                rdt214.Nop(0x19F3A);
                rdt214.Nop(0x1A016);
                rdt214.Nop(0x1A01C);
            }
        }

        [Patch(BothMansions = true)]
        public void ShotgunOnWallFix(RandomizedRdt rdt109, RandomizedRdt rdt115, RandomizedRdt rdt116)
        {
            if (RandomItems)
            {
                // Prevent overwriting item quantity
                rdt116.Nop(0x1FE16);

                // Prevent placing shotgun
                for (var i = 2; i < 2 + 8; i++)
                {
                    rdt116?.Patch(0x1FE62 + i, 0);
                }

                // Lock both doors in sandwich room (since we can't put item back on wall)
                if (Player == 1)
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

                // Unlock doors when in hall or living room
                rdt109?.AdditionalOpcodes.Add(new UnknownOpcode(0, 5, [2, 15, 0]));
                rdt116?.AdditionalOpcodes.Add(new UnknownOpcode(0, 5, [2, 16, 0]));
            }
        }

        [Patch]
        public void DisableSerumDoorBlock(RandomizedRdt rdt20D)
        {
            if (Player == 0)
            {
                rdt20D.Nop(0x2914C);
                rdt20D.Nop(0x29246);
            }
            else
            {
                rdt20D.Nop(0x291AE);
            }
        }

        [Patch]
        public void DisablePoisonChallenge(RandomizedRdt rdt20E)
        {
            if (Player == 0)
            {
                rdt20E.Nop(0x10724);
                rdt20E.Nop(0x1073A, 0x10740);
                rdt20E.Nop(0x1075A, 0x107FC);
                rdt20E.Nop(0x107F2, 0x107FC);
            }
            else
            {
                rdt20E.Nop(0x10724, 0x1072A);
                rdt20E.Nop(0x10744, 0x10780);
                rdt20E.Nop(0x1078A, 0x10794);
            }
        }

        [Patch(Player = 1)]
        public void DisableBarryEvesdrop(RandomizedRdt rdt405)
        {
            rdt405.Nop(0x194A2);
        }

        [Patch(Player = 0)]
        public void ChangeEnemies101(RandomizedRdt rdt101)
        {
            // This is a really silly hack to get round that ids 0 and 1 are
            // reserved for hunter/Rebecca in 601 and therefore get left
            if (RandomEnemies)
            {
                foreach (var em in rdt101.Enemies)
                {
                    em.Id += 2;
                }
            }
        }

        [Patch(Player = 1)]
        public void ClearEnemies309(RandomizedRdt rdt309)
        {
            if (RandomEnemies)
            {
                rdt309.Nop(0x2D83C);
                rdt309.Nop(0x2D852);
            }
        }

        [Patch(Player = 0)]
        public void ClearEnemies601(RandomizedRdt rdt601)
        {
            if (RandomEnemies)
            {
                rdt601.Nop(0x24D0A);
                rdt601.Nop(0x24DB6);
                rdt601.Nop(0x24DCC);
                rdt601.Nop(0x24DEA);
                rdt601.Nop(0x24E00);
            }
        }

        [Patch(Player = 0)]
        public void ClearEnemies706(RandomizedRdt rdt706)
        {
            if (RandomEnemies)
            {
                rdt706.Nop(0x3792A);
                rdt706.Nop(0x37954);
            }
        }

#if false
        public void AllowPartnerItemBoxes()
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
#endif

        [Patch]
        public void EnableFountainHeliportDoors(RandomizedRdt rdt303, RandomizedRdt rdt305)
        {
            var door = (DoorAotSeOpcode)rdt305.Doors.First(x => x.Id == 0);
            door.LockId = 2;
            door.LockType = 255;
            door.Special = 11;
            door.Animation = 11;
            door.NextX = 29130;
            door.NextY = 0;
            door.NextZ = 5700;
            door.NextD = 2048;

            // Remove message aot_reset
            rdt305.Nop(0x3E9AE);

            door = (DoorAotSeOpcode)rdt303.ConvertToDoor(8, 11, null, null);
            door.Target = RdtId.Parse("305");
            door.LockId = 2;
            door.LockType = 255;
            door.Special = 11;
            door.Animation = 11;
            door.NextX = 3130;
            door.NextY = 0;
            door.NextZ = 16900;
            door.NextD = 0;

            rdt303.Nop(0x111BE);
            rdt303.Nop(0x111C0);

            // Set cut to 4 if last room is ?05
            rdt303.AdditionalOpcodes.AddRange([
                new UnknownOpcode(0, 0x01, [ 0x0C ]),
                new UnknownOpcode(0, 0x06, [ 0x03, 0x00, 0x05 ]),
                new UnknownOpcode(0, 0x23, [ 0x01 ]),
                new UnknownOpcode(0, 0x08, [ 0x02, 0x04, 0x00 ]),
                new UnknownOpcode(0, 0x03, [ 0x00 ])
            ]);
        }

        [Patch]
        public void ForceHelipadTyrant(RandomizedRdt rdt303)
        {
            if (HelipadTyrantForced)
            {
                rdt303.AdditionalOpcodes.Add(new UnknownOpcode(0, 5, [0, 43, 0]));
            }
        }

        [Patch]
        public void ForceLift302(RandomizedRdt rdt300, RandomizedRdt rdt301, RandomizedRdt rdt302)
        {
            if (RandomDoors)
            {
                // Set lifts to raised state
                rdt301.AdditionalOpcodes.Add(new UnknownOpcode(0, 5, [0, 48, 1]));
                rdt300.AdditionalOpcodes.Add(new UnknownOpcode(0, 5, [0, 49, 0]));

                // Set lifts to lowered state
                rdt302.AdditionalOpcodes.Add(new UnknownOpcode(0, 5, [0, 48, 0]));
                rdt302.AdditionalOpcodes.Add(new UnknownOpcode(0, 5, [0, 49, 1]));

                // Remove these odd change player opcodes
                rdt302.Nop(0x192C4);
                rdt302.Nop(0x192D0);
                rdt302.Nop(0x192DC);
                rdt302.Nop(0x1944A);
                rdt302.Nop(0x19450);
            }
        }

        private static void SetItemAot(RandomizedRdt rdt, int targetOffset, int sourceOffset, bool clearSource = true)
        {
            var targetAot = rdt.Opcodes.OfType<ItemAotSetOpcode>().FirstOrDefault(x => x.Offset == targetOffset);
            var sourceAot = rdt.Opcodes.OfType<AotSetOpcode>().FirstOrDefault(x => x.Offset == sourceOffset);
            if (targetAot == null || sourceAot == null)
                return;

            targetAot.X = sourceAot.X;
            targetAot.Y = sourceAot.Z;
            targetAot.W = (short)sourceAot.W;
            targetAot.H = (short)sourceAot.D;
            if (clearSource)
            {
                sourceAot.X = 0;
                sourceAot.Z = 0;
                sourceAot.W = 0;
                sourceAot.D = 0;
            }
        }

        [AttributeUsage(AttributeTargets.Method)]
        public class PatchAttribute : Attribute
        {
            public bool BothMansions { get; set; }
            public int Player { get; set; } = -1;
        }
    }
}
