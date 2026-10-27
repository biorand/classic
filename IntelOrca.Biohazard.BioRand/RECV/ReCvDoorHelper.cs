using System.Collections.Generic;
using IntelOrca.Biohazard.Script.Opcodes;

namespace IntelOrca.Biohazard.BioRand.RECV
{
    internal class ReCvDoorHelper : IDoorHelper
    {
        public byte[] GetReservedLockIds() => new byte[0];

        public void Begin(RandoConfig config, GameData gameData, Map map)
        {
            // Do not lose lighter when giving Rodrigo medicine
            Nop(gameData, RdtId.Parse("1000"), 0x18CD7A);
            Nop(gameData, RdtId.Parse("1000"), 0x18DB74);
            // Do not put Rodrigo's gift into special slot
            Nop(gameData, RdtId.Parse("1000"), 0x18CD74);
            Nop(gameData, RdtId.Parse("1000"), 0x18DB7C);

            if (!config.RandomDoors)
            {
                // Force version of 102 and 103 where you can get briefcase and use medal
                Nop(gameData, RdtId.Parse("1010"), 0x3EF2C); // Force RDT1021 to load
                Nop(gameData, RdtId.Parse("1010"), 0x3EF38, 0x3EF4C); // Force RDT1021 to load
                Nop(gameData, RdtId.Parse("1010"), 0x3EF50, 0x3EF5A); // Force RDT1021 to load
                Nop(gameData, RdtId.Parse("1050"), 0x1DF2AA, 0x1DF2BE); // Force RDT1031 to load
                Nop(gameData, RdtId.Parse("1050"), 0x1DF2C2, 0x1DF2CC); // Force RDT1031 to load
            }

            // Force window cutscene on item interaction
            Nop(gameData, RdtId.Parse("1070"), 0x1819AE);

            if (config.RandomDoors)
            {
                SetFlag(gameData, RdtId.Parse("20E0"), 1, 19, 0);
                SetFlag(gameData, RdtId.Parse("5040"), 1, 19, 1);
            }

            // Delete Steve/Alfred cutscene
            Nop(gameData, RdtId.Parse("3050"), 0x15F288, 0x15F2DA);
            Nop(gameData, RdtId.Parse("3050"), 0x15EEDC, 0x15EEF6);

            if (!config.RandomDoors)
            {
                // Softlock can occur if you enter 305 via ladder without picking up silver key
                Patch(gameData, RdtId.Parse("3060"), 0x70A10 + 6, 0x00);

            }

            if (!config.RandomDoors)
            {
                // Change condition for going into 4011 so that it happens straight after Alfred cutscene
                Patch(gameData, RdtId.Parse("4080"), 0x9F86C + 2, 0xC5);
                Patch(gameData, RdtId.Parse("40F0"), 0x7241C + 2, 0xC5);
            }
            else
            {
                SetFlag(gameData, RdtId.Parse("4080"), 1, 211, 0);
            }

            // Force Steve to appear at airport
            Nop(gameData, RdtId.Parse("5000"), 0x187778, 0x18777A);
            Nop(gameData, RdtId.Parse("5000"), 0x187784, 0x18779C);

            if (config.RandomDoors)
            {
                SetFlag(gameData, RdtId.Parse("6000"), 1, 35, 0);
                SetFlag(gameData, RdtId.Parse("6000"), 1, 261, 0);
                // SetFlag(gameData, RdtId.Parse("6000"), 1, 264, 0);
            }

            if (config.RandomDoors)
            {
                // Prevent change to Chris
                Nop(gameData, RdtId.Parse("70A0"), 0x1F3140);
            }

            // Give the Sq. Valve Handle instead of the Socket, so the Octa
            // Valve Handle can never be lost by combining it with the Socket
            // while it is still needed elsewhere
            Patch(gameData, RdtId.Parse("A100"), 0x53A0A + 2, ReCvItemIds.SqValveHandle);
            Patch(gameData, RdtId.Parse("A100"), 0x54668 + 1, ReCvItemIds.SqValveHandle);
            Patch(gameData, RdtId.Parse("A100"), 0x5467A + 2, ReCvItemIds.SqValveHandle);

            if (config.RandomDoors)
            {
                // Using the security card in A1C0 ends Claire's game, so only allow it
                // once the gold dragonfly door in A1E0 has been opened (FG_EVENT 448).
                // Replaces the room flag clears in the init script, which are redundant
                // as room flags are reset on every room change.
                Patch(gameData, RdtId.Parse("A1C0"), 0x1BA80A, new byte[] {
                    0x01, 0x0E,                         // if
                    0x04, 0x01, 0xC0, 0x01, 0x00, 0x01, //     ck FG_EVENT, 448, 0, 1
                    0x05, 0x01, 0xB9, 0x01, 0x00, 0x00, //     set FG_EVENT, 441, 0, 0
                    0x03, 0x00,                         // endif
                    0x05, 0x04, 0x02, 0x00, 0x00, 0x01, // set FG_ROOM, 2, 0, 1
                    0x05, 0x04, 0x03, 0x00, 0x00, 0x01, // set FG_ROOM, 3, 0, 1
                    0x05, 0x04, 0x04, 0x00, 0x00, 0x01, // set FG_ROOM, 4, 0, 1
                    0x05, 0x04, 0x05, 0x00, 0x00, 0x01, // set FG_ROOM, 5, 0, 1
                    0xF4, 0xF4                          // nop
                });
            }

            // Do not put Rodrigo's gift into special slot
            Nop(gameData, RdtId.Parse("8170"), 0x14D26E);
            Nop(gameData, RdtId.Parse("8170"), 0x14EB68);

            if (config.RandomDoors)
            {
                SetFlag(gameData, RdtId.Parse("8080"), 1, 309, 1);
                SetFlag(gameData, RdtId.Parse("8080"), 1, 41, 0);
                SetFlag(gameData, RdtId.Parse("80A0"), 1, 354, 0);
            }
        }

        public void End(RandoConfig config, GameData gameData, Map map)
        {
        }

        private void SetFlag(GameData gameData, RdtId rtdId, byte kind, int index, byte value)
        {
            var rrdt = gameData.GetRdt(rtdId);
            if (rrdt == null)
                return;

            rrdt.AdditionalOpcodes.Add(new UnknownOpcode(0, 0x05, new byte[] { kind, (byte)(index & 0xFF), (byte)((index >> 8) & 0xFF), 0x00, value }));
        }

        private void Nop(GameData gameData, RdtId rtdId, int offset)
        {
            var rrdt = gameData.GetRdt(rtdId);
            if (rrdt == null)
                return;

            rrdt.Nop(offset);
        }

        private void Nop(GameData gameData, RdtId rtdId, int beginOffset, int endOffset)
        {
            var rrdt = gameData.GetRdt(rtdId);
            if (rrdt == null)
                return;

            rrdt.Nop(beginOffset, endOffset);
        }

        public void Patch(GameData gameData, RdtId rtdId, int offset, byte value)
        {
            var rrdt = gameData.GetRdt(rtdId);
            if (rrdt == null)
                return;

            rrdt.Patches.Add(new KeyValuePair<int, byte>(offset, value));
        }

        public void Patch(GameData gameData, RdtId rtdId, int offset, byte[] values)
        {
            for (int i = 0; i < values.Length; i++)
            {
                Patch(gameData, rtdId, offset + i, values[i]);
            }
        }
    }
}
