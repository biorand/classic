using System.Collections.Generic;
using IntelOrca.Biohazard.Room;
using IntelOrca.Biohazard.Script.Opcodes;

namespace IntelOrca.Biohazard.BioRand.RE2
{
    internal class Re2DoorHelper : IDoorHelper
    {
        public byte[] GetReservedLockIds() => new byte[] { 1, 2 };

        public void Begin(RandoConfig config, GameData gameData, Map map)
        {
            Nop(0x10B, 0x1756, 0x175E);
            Nop(0x10C, 0x1AB4);
            Nop(0x20B, 0x3762, 0x3754);
            Nop(0x20D, 0x113C);
            Nop(0x20F, 0x182A, 0x182A);
            Nop(0x103, 0x36FE);
            Nop(0x108, 0x1B0A);
            Nop(0x118, 0x1EF6);
            Nop(0x208, 0x14EC, 0x14F4);
            Nop(0x210, 0x135E, 0x12E8);
            Nop(0x211, 0x177E);
            Nop(0x408, 0x2974);
            Nop(0x615, 0x2FD8, 0x3562);
            Nop(0x60B, 0x1432, 0x1414);

            FixDarkRoomLocker(config, gameData);
            FixClaireElevator(config, gameData);

            void Nop(ushort rdtId, int address0, int address1 = -1)
            {
                var rdt = gameData.GetRdt(RdtId.FromInteger(rdtId));
                if (config.Player == 0)
                    rdt?.Nop(address0);
                else
                    rdt?.Nop(address1 == -1 ? address0 : address1);
            }
        }

        public void End(RandoConfig config, GameData gameData, Map map)
        {
            PreventWrongScenario(config, gameData);

            // See https://github.com/biorand/classic/issues/265
            var rdt = gameData.GetRdt(new RdtId(0, 0x0D));
            if (rdt == null || rdt.Version != BioVersion.Biohazard2 || config.Player != 1)
                return;

            rdt.Nop(0x0894);
            rdt.Nop(0x08BA);
        }

        private void FixDarkRoomLocker(RandoConfig config, GameData gameData)
        {
            var rdt = gameData.GetRdt(new RdtId(1, 0x08));
            if (rdt != null)
            {
                if (config.Player == 0)
                {
                    // Allow Leon to use Claire's locker and disable outfit change
                    rdt.Nop(0x14A2, 0x14AA);
                    rdt.Nop(0x14E6);
                    rdt.Nop(0x1758, 0x1762);
                    rdt.Nop(0x178E, 0x1798);
                    rdt.Nop(0x1940, 0x197E);
                    rdt.Nop(0x198A, 0x1990);
                    rdt.Nop(0x1996, 0x19B8);
                }
                else
                {
                    // Disable outfit change
                    rdt.Nop(0x1948, 0x1986);
                    rdt.Nop(0x1992, 0x1998);
                    rdt.Nop(0x199E, 0x19C0);
                }
            }
        }

        /// <summary>
        /// Change flag check to item pickup rather than partner status.
        /// </summary>
        /// <param name="config"></param>
        /// <param name="gameData"></param>
        private void FixClaireElevator(RandoConfig config, GameData gameData)
        {
            if (config.RandomDoors || config.Player == 0)
                return;

            var rdt = gameData.GetRdt(new RdtId(5, 0x01));
            if (rdt == null)
                return;

            rdt.Patches.Add(new KeyValuePair<int, byte>(0x056A + 1, 0x22));
            rdt.Patches.Add(new KeyValuePair<int, byte>(0x056A + 2, 0x1D));
        }

        private void PreventWrongScenario(RandoConfig config, GameData gameData)
        {
            RandomizedRdt? rdt = null;
            var alert = "RANDOMIZED FOR WRONG SCENARIO COMBINATION!";
            if (config.Scenario == 1)
            {
                // 100 cannot be start
                rdt = gameData.GetRdt(new RdtId(0, 0x00));
            }
            else
            {
                // 104 cannot be start
                rdt = gameData.GetRdt(new RdtId(0, 0x04));
            }
            if (rdt != null)
            {
                ShowRoomAlert(rdt, alert);
            }
        }

        private void ShowRoomAlert(RandomizedRdt rdt, string alert)
        {
            var rdtBuilder = ((Rdt2)rdt.RdtFile).ToBuilder();
            var enbuilder = rdtBuilder.MSGEN.ToBuilder();
            var jabuilder = rdtBuilder.MSGJA.ToBuilder();
            enbuilder.Messages.Add(alert.ToMsg(MsgLanguage.English, BioVersion.Biohazard2));
            jabuilder.Messages.Add(alert.ToMsg(MsgLanguage.Japanese, BioVersion.Biohazard2));
            rdtBuilder.MSGEN = enbuilder.ToMsgList();
            rdtBuilder.MSGJA = jabuilder.ToMsgList();
            rdt.RdtFile = rdtBuilder.ToRdt();

            var msgId = (byte)(enbuilder.Messages.Count - 1);
            // rdt.AdditionalFrameOpcodes.Add(new UnknownOpcode(0, 0x06, new byte[] { 0x00, 0x0E, 0x00 }));
            // rdt.AdditionalFrameOpcodes.Add(new UnknownOpcode(0, 0x23, new byte[] { 0x00, 0x1B, 0x00, 0x00, 0x00 }));
            rdt.AdditionalFrameOpcodes.Add(new UnknownOpcode(0, 0x2B, new byte[] { 0x00, msgId, 0x00, 0xFF, 0xFF }));
            // rdt.AdditionalFrameOpcodes.Add(new UnknownOpcode(0, 0x08, new byte[] { 0x00 }));
        }
    }
}
