using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml;
using IntelOrca.Biohazard.BioRand.RE1;
using IntelOrca.Biohazard.Room;
using IntelOrca.Biohazard.Script.Opcodes;
using SixLabors.ImageSharp.PixelFormats;

namespace IntelOrca.Biohazard.BioRand
{
    internal class Re1ClassicRandomizerController : IClassicRandomizerController
    {
        public GameData GetGameData(IClassicRandomizerContext context, int player)
        {
            var result = new List<RandomizedRdt>();
            for (var i = 1; i <= 7; i++)
            {
                var files = context.GameDataManager.GetFiles($"JPN/STAGE{i}");
                foreach (var path in files)
                {
                    var fileName = Path.GetFileName(path);
                    var match = Regex.Match(fileName, @"^ROOM([0-9A-F]{3})(0|1).RDT$", RegexOptions.IgnoreCase);
                    if (match.Success)
                    {
                        var rdtId = RdtId.Parse(match.Groups[1].Value);
                        var rdtPlayer = int.Parse(match.Groups[2].Value);
                        if (rdtPlayer == player)
                        {
                            var fileData = context.GameDataManager.GetData(path);
                            if (fileData.Length < 16)
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
                result.Add(new RandomizedRdt(rrdt2.RdtFile, missingRoom));
            }

            foreach (var rrdt in result)
            {
                var rdtId = rrdt.RdtId;
                rrdt.OriginalPath = $"STAGE{rdtId.Stage + 1}/ROOM{rdtId}0.RDT";
                rrdt.Load();
            }

            var gd = new GameData([.. result]);
            ApplyRdtPatches(context, gd, player);
            if (context.Configuration.GetValueOrDefault("cutscenes/disable", false))
            {
                DisableCutscenes(context, gd, player);
            }
            return gd;
        }

        private void ApplyRdtPatches(IClassicRandomizerContext context, GameData gameData, int player)
        {
            const byte PassCodeDoorLockId = 209;
            var randomDoors = context.Configuration.GetValueOrDefault("doors/random", false);
            var randomItems = context.Configuration.GetValueOrDefault("items/random", false);

            FixPassCodeDoor();
            AllowRoughPassageDoorUnlock();
            ShotgunOnWallFix();
            DisableBarryEvesdrop();
            AllowPartnerItemBoxes();

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

                    if (!randomDoors && player == 1)
                    {
                        rdt.AdditionalOpcodes.Add(new UnknownOpcode(0, 0x01, new byte[] { 0x0A }));
                        rdt.AdditionalOpcodes.Add(new UnknownOpcode(0, 0x04, new byte[] { 0x01, 0x25, 0x00 }));
                        rdt.AdditionalOpcodes.Add(new UnknownOpcode(0, 0x05, new byte[] { 0x02, PassCodeDoorLockId - 192, 0 }));
                        rdt.AdditionalOpcodes.Add(new UnknownOpcode(0, 0x03, new byte[] { 0x00 }));
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

                var rdt = gameData.GetRdt(new RdtId(0, 0x16));
                if (rdt == null)
                    return;

                rdt.Nop(0x1FE16);
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

        }

        private void DisableCutscenes(IClassicRandomizerContext context, GameData gameData, int player)
        {
            if (player == 0)
            {
                Set("106", 1, 0, 0); // First cutscene
                Set("106", 1, 2, 0); // First zombie found
                Set("106", 1, 3, 0); // Second cutscene (Jill? Wesker?)
                Set("106", 1, 36, 0); // First Rebecca save room cutscene
                Set("106", 1, 167, 0); // Init. dining room emblem state
            }

            void Set(string rdtId, byte group, byte index, byte value)
            {
                var rdt = gameData.GetRdt(RdtId.Parse(rdtId))!;
                rdt?.AdditionalOpcodes.Add(new UnknownOpcode(0, 0x05, [group, index, value]));
            }
        }

        public void WritePatches(IClassicRandomizerContext context, PatchWriter pw)
        {
            var randomDoors = context.Configuration.GetValueOrDefault("doors/random", false);

            DisableDemo(pw);
            FixFlamethrowerCombine(pw);
            FixWasteHeal(pw);
            FixNeptuneDamage(pw);
            FixChrisInventorySize(pw);
            FixYawnPoison(pw, randomDoors);
        }

        private static void DisableDemo(PatchWriter pw)
        {
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

        private static void FixFlamethrowerCombine(PatchWriter pw)
        {
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

        private static void FixWasteHeal(PatchWriter pw)
        {
            // Allow using heal items when health is at max
            // jge 0447AA2h -> nop
            pw.Begin(0x447A39);
            pw.Write(0x90);
            pw.Write(0x90);
            pw.End();
        }

        private static void FixNeptuneDamage(PatchWriter pw)
        {
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

        private static void FixChrisInventorySize(PatchWriter pw)
        {
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

        private static void FixYawnPoison(PatchWriter pw, bool doorRandomizer)
        {
            const byte ST_POISON = 0x02;
            const byte ST_POISON_YAWN = 0x20;

            pw.Begin(0x45B8C0 + 6); // 80 0D 90 52 C3 00 20
            if (doorRandomizer)
                pw.Write(ST_POISON);
            else
                pw.Write(ST_POISON_YAWN);
            pw.End();
        }

        public void WriteExtra(IClassicRandomizerContext context)
        {
            AddInventoryXml(context);
            AddBackgroundTextures(context);
        }

        private void AddInventoryXml(IClassicRandomizerContext context)
        {
            using var ms = new MemoryStream();
            var doc = new XmlDocument();
            var root = doc.CreateElement("Init");

            var inventories = context.ModBuilder.Inventory;
            var chris = inventories.Length > 0 ? inventories[0] : CreateEmptyInventory(6);
            var jill = inventories.Length > 1 ? inventories[1] : CreateEmptyInventory(8);
            var rebecca = inventories.Length > 2 ? inventories[2] : CreateEmptyInventory(6);

            root.AppendChild(CreatePlayerNode(doc, jill, new RandomInventory()));
            root.AppendChild(CreatePlayerNode(doc, chris, rebecca));

            doc.AppendChild(root);
            doc.Save(ms);
            context.CrModBuilder.SetFile("init.xml", ms.ToArray());
        }

        private static RandomInventory CreateEmptyInventory(int size)
        {
            var entries = new List<RandomInventory.Entry>();
            for (var i = 0; i < size; i++)
            {
                entries.Add(new RandomInventory.Entry());
            }
            entries[0] = new RandomInventory.Entry(Re1ItemIds.CombatKnife, 1);
            return new RandomInventory([.. entries], null);
        }

        private static XmlElement CreatePlayerNode(XmlDocument doc, RandomInventory main, RandomInventory partner)
        {
            var playerNode = doc.CreateElement("Player");
            foreach (var inv in new[] { main, partner })
            {
                foreach (var entry in inv.Entries)
                {
                    var entryNode = doc.CreateElement("Entry");
                    entryNode.SetAttribute("id", entry.Type.ToString());
                    entryNode.SetAttribute("count", entry.Count.ToString());
                    playerNode.AppendChild(entryNode);
                }
            }
            return playerNode;
        }

        private void AddBackgroundTextures(IClassicRandomizerContext context)
        {
            var bgPng = context.DataManager.GetData(BioVersion.Biohazard1, "bg.png");
            var bgPix = PngToPix(bgPng);
            context.CrModBuilder.SetFile("data/title.pix", bgPix);
            context.CrModBuilder.SetFile("type.png", bgPng);
        }

        private byte[] PngToPix(byte[] png)
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
                        var c4 = (ushort)((c.R / 8) | ((c.G / 8) << 5) | ((c.B / 8) << 10));
                        bw.Write(c4);
                    }
                }
            });
            return ms.ToArray();
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
