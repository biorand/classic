using System;
using System.IO;
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
        public bool HardMode => mod.GetGeneralValue<bool>("hard");
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
            rdt106.AdditionalOpcodes.AddRange(
                ScdCondition.Parse("1:0").Generate(BioVersion.Biohazard1, [
                    Set(0, 122, (byte)(InkEnabled ? 0 : 1)),
                    Set(0, 123, (byte)(HardMode ? 0 : 1)),
                    Set(0, 124, (byte)(LockpickEnabled ? 0 : 1))
                ])
            );

            if (Player == 1 && !LockpickEnabled)
            {
                // Disable Barry giving Jill the lockpick
                rdt106.Nop(0x31B02);
            }

            static UnknownOpcode Set(byte group, byte index, byte value)
            {
                return new UnknownOpcode(0, 0x05, [group, index, value]);
            }
        }

        [Patch(BothMansions = true)]
        public void FixMapsAsItems(
            RandomizedRdt rdt107,
            RandomizedRdt rdt20B)
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
        }

        [Patch]
        public void FixMapsAsItems(
            RandomizedRdt rdt300,
            RandomizedRdt rdt30F,
            RandomizedRdt rdt406)
        {
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

        [Patch]
        public void RemoveBlood211(RandomizedRdt rdt211)
        {
            if (RandomEnemies)
            {
                // Certain enemies like dogs and bees crash game if they attack player
                // when player does pickup/bend down animation
                rdt211.Nop(0x10BB2, 0x10C0A);
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
            if (!RandomDoors)
                return;

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

        [Patch]
        public void FixDoorPlant42(
            RandomizedRdt rdt408,
            RandomizedRdt rdt40A,
            RandomizedRdt rdt40C)
        {
            // Keep bookcase open
            rdt40A.Nop(0x393BC);

            // Change aot_set to door_aot_set
            var door = (DoorAotSeOpcode)rdt40C.ConvertToDoor(1, 5, null, null);
            door.Special = 0;
            door.NextStage = 0xFF;
            door.NextRoom = 0x0A;
            door.NextX = 14300;
            door.NextY = 0;
            door.NextZ = 5200;
            door.NextD = 2048;

            if (RandomDoors)
            {
                // Don't block the double doors in nest room
                rdt408.Nop(0x30ACC, 0x30ACE);
                rdt408.Nop(0x30AEC, 0x30AEE);

                // Do not switch to mansion 2
                if (Player == 0)
                {
                    rdt40C.Nop(0x676C);
                }
                else
                {
                    rdt40C.Nop(0x66AC);
                }

                // Block door until plant 42 is dead
                if (Player == 0)
                {
                    rdt40C.Nop(0x67A2, 0x67A8);
                    rdt40C.Patch(0x67AE + 1, 3);
                    rdt40C.Patch(0x67AE + 2, 96);
                    rdt40C.Patch(0x67AE + 3, 1);
                    rdt40C.Nop(0x67B2);
                    rdt40C.Nop(0x67C0);
                    rdt40C.Nop(0x67C4);
                }
                else
                {
                    rdt40C.Nop(0x66E2, 0x66E8);
                    rdt40C.Patch(0x66EE + 1, 3);
                    rdt40C.Patch(0x66EE + 2, 96);
                    rdt40C.Patch(0x66EE + 3, 1);
                    rdt40C.Nop(0x66F2);
                    rdt40C.Nop(0x6700);
                    rdt40C.Nop(0x6704);

                    // Plant 42 crashes room if you kill it then leave then come back in room
                    // Not sure why, and it doesn't happen if you save, restart game, load just
                    // before going back in room. Some kind of runtime memory issue?
                    rdt40C.Nop(0x65FA, 0x6676);
                }
            }
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
        public void DiableJillSandwich(RandomizedRdt rdt115)
        {
            if (RandomDoors)
            {
                rdt115.Nop(0x22F4, 0x231E);
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
        public void FixPlayerChangeGlitch305(RandomizedRdt rdt305)
        {
            rdt305.Nop(0x3EEEC);
            rdt305.Nop(0x3EEF8);
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

                // Disable waterfall (for now)
                rdt302.Nop(0x18E40, 0x18E58);
                rdt302.Nop(0x18EB4, 0x18ED6);
                rdt302.Nop(0x18F18, 0x18F2A);
                rdt302.Nop(0x19008, 0x191E2);
            }
        }

        [Patch]
        public void FixDoor500(RandomizedRdt rdt500)
        {
            // Prevent leaving due to Tyrant 1 being killed
            if (Player == 0)
            {
                rdt500.Nop(0xCF42, 0xCF54);
            }
            else
            {
                rdt500.Nop(0xCF70, 0xCF82);
            }
        }

        [Patch]
        public void FixCutscene502(RandomizedRdt rdt502)
        {
            if (Player == 1)
            {
                rdt502.AdditionalOpcodes.AddRange([
                    CreateFromString("0118"),
                    CreateFromString("04003700"),
                    CreateFromString("0401C001"),
                    CreateFromString("20000000000C0000E01500001C0C"),
                    CreateFromString("0300")]);
            }
        }

        [Patch(Player = 1)]
        public void AddVariableHelper308(RandomizedRdt rdt308)
        {
            // Set FG_ROOM[0] if Barry conditions are met
            // This allows us to only put enemies in based on !FG_ROOM[0]
            rdt308.AdditionalOpcodes.Add(new UnknownOpcode(0, 0x01, [0x0E]));
            rdt308.AdditionalOpcodes.Add(new UnknownOpcode(0, 0x04, [0x01, 0x5D, 0x01]));
            rdt308.AdditionalOpcodes.Add(new UnknownOpcode(0, 0x04, [0x01, 0x25, 0x00]));
            rdt308.AdditionalOpcodes.Add(new UnknownOpcode(0, 0x05, [0x04, 0x00, 0x00]));
            rdt308.AdditionalOpcodes.Add(new UnknownOpcode(0, 0x03, [0x00]));
            rdt308.AdditionalOpcodes.Add(new UnknownOpcode(0, 0x01, [0x12]));
            rdt308.AdditionalOpcodes.Add(new UnknownOpcode(0, 0x04, [0x01, 0x58, 0x00]));
            rdt308.AdditionalOpcodes.Add(new UnknownOpcode(0, 0x04, [0x01, 0x48, 0x01]));
            rdt308.AdditionalOpcodes.Add(new UnknownOpcode(0, 0x04, [0x01, 0x5D, 0x00]));
            rdt308.AdditionalOpcodes.Add(new UnknownOpcode(0, 0x05, [0x04, 0x00, 0x00]));
            rdt308.AdditionalOpcodes.Add(new UnknownOpcode(0, 0x03, [0x00]));
        }

        [Patch]
        public void FixBookcase216(RandomizedRdt rdt406)
        {
            // Keep bookcase moved when going through door
            if (RandomDoors && mod.Doors.TryGetValue(RdtItemId.Parse("216:2"), out var dtl))
            {
                if (dtl.Target?.Room is RdtId targetRdt)
                {
                    ForBothMansions(targetRdt, rdt =>
                    {
                        rdt.AdditionalOpcodes.Add(new UnknownOpcode(0, 0x05, [0x00, 79, 0x00]));
                    });
                }
            }
        }

        [Patch]
        public void FixBookcase406(RandomizedRdt rdt406)
        {
            // Keep bookcases moved when going through door
            if (RandomDoors && mod.Doors.TryGetValue(RdtItemId.Parse("406:2"), out var dtl))
            {
                if (dtl.Target?.Room is RdtId targetRdt)
                {
                    ForBothMansions(targetRdt, rdt =>
                    {
                        rdt.AdditionalOpcodes.Add(new UnknownOpcode(0, 0x05, [0x00, 69, 0x00]));
                    });
                }
            }
        }

        [Patch]
        public void FixMoDiscReader507(RandomizedRdt rdt507)
        {
            var aot = rdt507.AllOpcodes.OfType<AotSetOpcode>().FirstOrDefault(x => x.Id == 13);
            var item = (ItemAotSetOpcode)rdt507.Items.FirstOrDefault(x => x.Id == 14);
            item.X = aot.X;
            item.Y = aot.Z;
            item.W = (short)aot.W;
            item.H = (short)aot.D;

            string[] ss = [
                $"011E",                     // if
                $"04007201",                 // ck(FG_SCENARIO, 114, 1)
                $"120C070109000D000100",     //     aot_reset(12, SCE_USEITEM, 1, 9, 13, 1);
                $"120D0281860045000000",     //     aot_reset(13, SCE_MESSAGE, 129, 134, 69, 0);
                $"130E0D00",                 //     aot_delete(14, SCE_DOCUMENT, 0);
                $"0230",                     // else
                $"120C0081000000000000",     //     aot_reset(12, SCE_NONE, 129, 0, 0, 0);
                $"0114",                     //     if
                $"04076200",                 //     ck(FG_ITEM, 98, 0)
                $"120D0081000000000000",     //         aot_reset(13, SCE_NONE, 129, 0, 0, 0);
                $"130E0D81",                 //         aot_delete(14, SCE_DOCUMENT, 129);
                $"0210",                     //     else
                $"120D0281870045000000",     //         aot_reset(13, SCE_MESSAGE, 129, 135, 69, 0);
                $"130E0D00",                 //         aot_delete(14, SCE_DOCUMENT, 0);

                $"010C",                     // if
                $"1028",                     // testitem(ITEM_MO_DISK)
                $"05007200",                 //     set(FG_SCENARIO, 114, 0)
                $"240E0D00",                 //     aot_on(14, SCE_DOCUMENT, 0);
                $"0300",                     // endif
            ];
            rdt507.AdditionalFrameOpcodes.AddRange(CreateFromString(ss));
            rdt507.Nop(0x19A64, 0x19A9C);
        }

        [Patch]
        public void FixMoDiscReader509(RandomizedRdt rdt509)
        {
            var aot = rdt509.AllOpcodes.OfType<AotSetOpcode>().FirstOrDefault(x => x.Id == 4);
            var item = (ItemAotSetOpcode)rdt509.Items.FirstOrDefault(x => x.Id == 1);
            item.X = aot.X;
            item.Y = aot.Z;
            item.W = (short)aot.W;
            item.H = (short)aot.D;

            string[] ss = [
                $"011E",                     // if
                $"04007001",                 // ck(FG_SCENARIO, 112, 1)
                $"1203070109000D000100",     //     aot_reset(3, SCE_USEITEM, 1, 9, 13, 1);
                $"12040981090000000000",     //     aot_reset(4, SCE_EVENT, 129, 9, event_00, 0);
                $"13010D00",                 //     aot_delete(1, SCE_DOCUMENT, 0);
                $"0230",                     // else
                $"12030081000000000000",     //     aot_reset(3, SCE_NONE, 129, 0, 0, 0);
                $"0114",                     //     if
                $"04075200",                 //     ck(FG_ITEM, 82, 0)
                $"12040081000000000000",     //         aot_reset(4, SCE_NONE, 129, 0, 0, 0);
                $"13010D81",                 //         aot_delete(1, SCE_DOCUMENT, 129);
                $"0210",                     //     else
                $"12040281810045000000",     //         aot_reset(4, SCE_MESSAGE, 129, 129, 69, 0);
                $"13010D00",                 //         aot_delete(1, SCE_DOCUMENT, 0);

                $"010C",                     // if
                $"1028",                     // testitem(ITEM_MO_DISK)
                $"05007000",                 //     set(FG_SCENARIO, 112, 0)
                $"14000901",                 //     evt_exec(0, 9, event_01)
                $"0300",                     // endif
            ];
            rdt509.AdditionalFrameOpcodes.AddRange(CreateFromString(ss));
            if (Player == 0)
                rdt509.Nop(0x1D122, 0x1D15A);
            else
                rdt509.Nop(0x1D13A, 0x1D172);
        }

        [Patch]
        public void FixMoDiscReader510(RandomizedRdt rdt510)
        {
            var aot = rdt510.AllOpcodes.OfType<AotSetOpcode>().FirstOrDefault(x => x.Id == 4);
            var item = (ItemAotSetOpcode)rdt510.Items.FirstOrDefault(x => x.Id == 2);
            item.X = aot.X;
            item.Y = aot.Z;
            item.W = (short)aot.W;
            item.H = (short)aot.D;

            string[] ss = [
                $"011E",                     // if
                $"04007101",                 // ck(FG_SCENARIO, 113, 1)
                $"1203070109000D000100",     //     aot_reset(3, SCE_USEITEM, 1, 9, 13, 1);
                $"12040281800045000000",     //     aot_reset(4, SCE_MESSAGE, 129, 128, 69, 0);
                $"13020D00",                 //     aot_delete(2, SCE_DOCUMENT, 0);
                $"0230",                     // else
                $"12030081000000000000",     //     aot_reset(3, SCE_NONE, 129, 0, 0, 0);
                $"0114",                     //     if
                $"04075A00",                 //     ck(FG_ITEM, 90, 0)
                $"12040081000000000000",     //         aot_reset(4, SCE_NONE, 129, 0, 0, 0);
                $"13020D81",                 //         aot_delete(2, SCE_DOCUMENT, 129);
                $"0210",                     //     else
                $"12040281810045000000",     //         aot_reset(4, SCE_MESSAGE, 129, 129, 69, 0);
                $"13020D00",                 //         aot_delete(2, SCE_DOCUMENT, 0);

                $"010C",                     // if
                $"1028",                     // testitem(ITEM_MO_DISK)
                $"05007100",                 //     set(FG_SCENARIO, 113, 0)
                $"14000900",                 //     evt_exec(0, 9, event_00)
                $"0300",                     // endif
            ];
            rdt510.AdditionalFrameOpcodes.AddRange(CreateFromString(ss));
            if (Player == 0)
                rdt510.Nop(0x25412, 0x2544A);
            else
                rdt510.Nop(0x253FA, 0x25432);
        }

        [Patch]
        public void FixMoDiscDoor508(RandomizedRdt rdt508)
        {
            // FG_ITEM[82] -> FG_SCENARIO[112] (509)
            // FG_ITEM[98] -> FG_SCENARIO[114] (507)
            // FG_ITEM[90] -> FG_SCENARIO[113] (510)

            PatchSet(0x039C, 0, 112, 1);
            PatchSet(0x03A2, 0, 114, 1);
            PatchSet(0x03A8, 0, 113, 1);
            if (Player == 0)
            {
                PatchSet(0x067C, 0, 112, 0);
                PatchSet(0x06AE, 0, 114, 0);
                PatchSet(0x06E0, 0, 113, 0);
            }
            else
            {
                PatchSet(0x0664, 0, 112, 0);
                PatchSet(0x0696, 0, 114, 0);
                PatchSet(0x06C8, 0, 113, 0);
            }

            void PatchSet(int offset, byte group, byte id, byte value)
            {
                rdt508.Patch(offset + 1, group);
                rdt508.Patch(offset + 2, id);
                rdt508.Patch(offset + 3, value);
            }
        }

        [Patch(BothMansions = true, Player = 1)]
        public void EnableItemForJill106(RandomizedRdt rdt106)
        {
            rdt106.Nop(0x2FC00, 0x2FC06);
            rdt106.Nop(0x2FC24, 0x2FC26);

            rdt106.Nop(0x2FFBC);
            rdt106.Nop(0x2FFC4);

            rdt106.Nop(0x31860, 0x31862);
            rdt106.Nop(0x31870, 0x31872);
        }

        [Patch(BothMansions = true, Player = 1)]
        public void EnableItemForJill113(RandomizedRdt rdt113)
        {
            rdt113.Nop(0x1CAD8, 0x1CADA);
            rdt113.Nop(0x1CB7C);
            rdt113.Nop(0x1CB98, 0x1CB9A);
            rdt113.Nop(0x1CBBA);
        }

        [Patch(BothMansions = true, Player = 1)]
        public void EnableItemForJill11B(RandomizedRdt rdt11B)
        {
            rdt11B.Nop(0x4A7E, 0x4A80);
            rdt11B.Nop(0x4A9E, 0x4AA0);
            rdt11B.Nop(0x4B4C);
        }

        [Patch(BothMansions = true, Player = 1)]
        public void EnableItemForJill211(RandomizedRdt rdt211)
        {
            rdt211.Nop(0x10C1C, 0x10C1E);
            rdt211.Nop(0x10C42);
        }

        [Patch(BothMansions = true, Player = 1)]
        public void EnableItemForJill21C(RandomizedRdt rdt21C)
        {
            rdt21C.AdditionalOpcodes.Add(
                CreateItemFromString("180A3421781E4006E8033D0100FF222424FA60220000E4810086"));
        }

        [Patch(Player = 1)]
        public void EnableItemForJill401(RandomizedRdt rdt401)
        {
            rdt401.AdditionalOpcodes.AddRange([
                CreateItemFromString("1803FC08BC34E803E8033D0101FF360B60FA7837000064810000"),
                CreateFromString("3B0100089600"),
                CreateFromString("13030000"),
                CreateFromString("0118"),
                CreateFromString("04076400"),
                CreateFromString("0D05FC08BC34E803E8030981090000000000"),
                CreateFromString("0300")]);
        }

        [Patch(Player = 1)]
        public void EnableItemForJill40F(RandomizedRdt rdt40F)
        {
            rdt40F.Nop(0x1F6FE);
        }

        private static ItemAotSetOpcode CreateItemFromString(string hex)
        {
            using var ms = new MemoryStream();
            CreateFromString(hex).Write(new BinaryWriter(ms));
            ms.Position = 0;
            return ItemAotSetOpcode.Read(new BinaryReader(ms), 0);
        }

        private static UnknownOpcode[] CreateFromString(string[] hex)
        {
            return hex.Select(CreateFromString).ToArray();
        }

        private static UnknownOpcode CreateFromString(string hex)
        {
            var data = new byte[(hex.Length - 2) / 2];
            for (var i = 0; i < data.Length; i++)
            {
                data[i] = FromHexChars(hex[(i + 1) * 2], hex[(i + 1) * 2 + 1]);
            }
            return new UnknownOpcode(0, FromHexChars(hex[0], hex[1]), data);

            static byte FromHexChars(char a, char b)
            {
                return (byte)((FromHexChar(a) << 4) + FromHexChar(b));
            }

            static byte FromHexChar(char c)
            {
                if (c >= '0' && c <= '9')
                    return (byte)(c - '0');
                if (c >= 'A' && c <= 'F')
                    return (byte)(c - 'A' + 10);
                if (c >= 'a' && c <= 'f')
                    return (byte)(c - 'a' + 10);
                return 0;
            }
        }

        private void ForBothMansions(RdtId rdtId, Action<RandomizedRdt> action)
        {
            if (rdtId.Stage == 5 || rdtId.Stage == 6)
            {
                var rdt = gameData.GetRdt(new RdtId(rdtId.Stage - 5, rdtId.Room));
                if (rdt != null)
                    action(rdt);
            }
            {
                var rdt = gameData.GetRdt(new RdtId(rdtId.Stage, rdtId.Room));
                if (rdt != null)
                    action(rdt);
            }
            if (rdtId.Stage == 0 || rdtId.Stage == 1)
            {
                var rdt = gameData.GetRdt(new RdtId(rdtId.Stage + 5, rdtId.Room));
                if (rdt != null)
                    action(rdt);
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
