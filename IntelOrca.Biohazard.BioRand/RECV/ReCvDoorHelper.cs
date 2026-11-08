using System.Collections.Generic;
using IntelOrca.Biohazard.Room;
using IntelOrca.Biohazard.Script.Opcodes;

namespace IntelOrca.Biohazard.BioRand.RECV
{
    internal class ReCvDoorHelper : IDoorHelper
    {
        // In room scripts, a flag value of 0 means the engine's bit is set and 1 means it is clear.
        // Unused by rooms 1020, 1021, 1030 and 1031.
        private const byte CutsceneSkippedRoomFlag = 30;
        // Value 0 once a door transition has started
        private const int DoorTransitionCutFlag = 31;
        // Not used by any room script
        private const int GraveyardReturnEventFlag = 102;
        private const int SteveReturnEventFlag = 103;
        // Value 0 while a cutscene has control of the game
        private const int CinematicCutFlag = 29;
        // Opens the screen that gives VAR_ITEM_ID to the player
        private const byte ItemGetScreenCutFlag = 27;
        // The item get screen gives the item without asking, if there is room for it
        private const byte ItemWithoutAskingCutFlag = 17;
        // Value 0 once the item get screen has given the item
        private const int ItemTakenCutFlag = 20;
        // Unused by room 1031. Value 0 once the room has offered Steve's item since being entered.
        private const byte HandoverOfferedRoomFlag = 30;
        // Not used by any room script
        private const int SteveItemTakenEventFlag = 101;

        // The rooms that can be played, and the variants that only have their first visit cutscenes
        private static readonly RdtId GraveyardRoom = RdtId.Parse("1021");
        private static readonly RdtId GraveyardCutsceneRoom = RdtId.Parse("1020");
        private static readonly RdtId SteveRoom = RdtId.Parse("1031");
        private static readonly RdtId SteveCutsceneRoom = RdtId.Parse("1030");

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

            AddFirstVisitCutscenes(gameData);

            // Once the training facility has been visited (FG_EVENT 189), the door from 2000
            // loads 2021, whose door to the palace (2070) ignores the ID card lock (FG_EVENT 4).
            // Only 2020 has the card reader, so go there until the door has been unlocked.
            AddRoomReload(gameData, RdtId.Parse("2021"), RdtId.Parse("2020"), null, Check(0x01, 4, 1));

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
            if (config.RandomDoors)
            {
                // The player can still be in a cutscene room after its cutscene, so its doors
                // must lead to the same rooms as the working room's.
                MirrorDoorTargets(gameData, GraveyardRoom, GraveyardCutsceneRoom);
                MirrorDoorTargets(gameData, SteveRoom, SteveCutsceneRoom);
            }
        }

        /// <summary>
        /// Gives the doors of another variant of a room the targets the door randomiser set
        /// for this variant. Only doors that are native door AOTs in both variants are copied,
        /// as an AOT with the same index can be something else in the other variant.
        /// Door locks are not copied, as setting them has no effect on RE:CV rooms.
        /// </summary>
        private static void MirrorDoorTargets(GameData gameData, RdtId sourceId, RdtId targetId)
        {
            var source = gameData.GetRdt(sourceId);
            var target = gameData.GetRdt(targetId);
            if (source == null || target == null)
                return;

            foreach (var (edge, doorTarget, destination) in source.CvDoorTargets)
            {
                var doorId = edge.DoorId!.Value;
                // Offsets are script addresses in the source room, so do not apply them to the target room
                if (edge.Raw.Offsets != null && edge.Raw.Offsets.Length != 0)
                    continue;
                if (!IsNativeDoor(target, doorId))
                    continue;
                if (edge.Raw.AltId is int altId && !IsNativeDoor(target, altId))
                    continue;

                target.SetDoorTarget(edge, doorTarget, destination);
            }
        }

        private static bool IsNativeDoor(RandomizedRdt rdt, int aotIndex)
        {
            var aots = ((RdtCv)rdt.RdtFile).Aots;
            return aotIndex < aots.Length && aots[aotIndex].Kind == 0;
        }

        /// <summary>
        /// The briefcase can only be taken in 1021 and the medal only used in 1031, but
        /// the graveyard and Steve cutscenes are only in 1020 and 1030. Every door leads
        /// to 1021 and 1031, which pass through to the cutscene room on the first visit.
        /// The cutscene room then returns to the working room as soon as the cutscene ends.
        /// </summary>
        private void AddFirstVisitCutscenes(GameData gameData)
        {
            var eventFlag = (byte)0x01;
            var roomFlag = (byte)0x04;
            var cutFlag = (byte)0x0A;
            var specialFlag = (byte)0x06;

            AddRoomReload(gameData, GraveyardRoom, GraveyardCutsceneRoom, null, Check(eventFlag, 104, 1));
            AddRoomReload(gameData, SteveRoom, SteveCutsceneRoom, null, Check(eventFlag, 107, 1));

            // Cutscene over: FG_ROOM 6 is only clear from its start until it ends or is skipped
            AddRoomReload(gameData, GraveyardCutsceneRoom, GraveyardRoom, GraveyardReturnEventFlag,
                Check(eventFlag, 104, 0), Check(roomFlag, 6, 1), Check(cutFlag, CinematicCutFlag, 1));

            // Cutscene skipped, leave once the skip events have tidied up
            AddRoomReload(gameData, GraveyardCutsceneRoom, GraveyardRoom, GraveyardReturnEventFlag,
                Check(roomFlag, CutsceneSkippedRoomFlag, 0));
            Patch(gameData, GraveyardCutsceneRoom, 0x1EF24C, new byte[] { 0x05, roomFlag, CutsceneSkippedRoomFlag, 0x00, 0x00, 0x00 });
            Patch(gameData, GraveyardCutsceneRoom, 0x1EF258, new byte[] { 0xF4, 0xF4, 0xF4, 0xF4 }); // bgm_on_ex

            // Both the normal and the skip events finish by setting FG_SPECIAL 27
            AddRoomReload(gameData, SteveCutsceneRoom, SteveRoom, SteveReturnEventFlag,
                Check(eventFlag, 107, 0), Check(specialFlag, 27, 1), Check(cutFlag, CinematicCutFlag, 1));

            // Put the player back where the cutscene left them, using the cutscene's own placement
            AddReturnPlacement(gameData, GraveyardRoom, GraveyardReturnEventFlag,
                new UnknownOpcode(0, 0x65, new byte[] { 0x00, 0x00, 0x00 }), // load_work
                new UnknownOpcode(0, 0x69, new byte[] { 0x0C, 0x00, 0x00, 0x00, 0x00 }), // common_ctr_base_pos_set
                new UnknownOpcode(0, 0x64, new byte[] { 0x07, 0xB0, 0x36, 0x00, 0x00, 0x28, 0x23 }), // player_ctr_pos_set
                new UnknownOpcode(0, 0x69, new byte[] { 0x0D, 0x00, 0x00, 0x00, 0x00 }), // common_ctr_base_ang_set
                new UnknownOpcode(0, 0x69, new byte[] { 0x0B, 0x00, 0x5A, 0x00, 0x00 }), // common_ctr_pos_set2
                new UnknownOpcode(0, 0x64, new byte[] { 0x80 })); // player_ctr_motion_init
            AddReturnPlacement(gameData, SteveRoom, SteveReturnEventFlag,
                new UnknownOpcode(0, 0x65, new byte[] { 0x00, 0x00, 0x00 }), // load_work
                new UnknownOpcode(0, 0x69, new byte[] { 0x0C, 0x01, 0x00, 0x00, 0x00 }), // common_ctr_base_pos_set
                new UnknownOpcode(0, 0x64, new byte[] { 0x07, 0x00, 0x05, 0x00, 0x00, 0xF6, 0x2C }), // player_ctr_pos_set
                new UnknownOpcode(0, 0x69, new byte[] { 0x0D, 0x00, 0x00, 0x00, 0x00 }), // common_ctr_base_ang_set
                new UnknownOpcode(0, 0x69, new byte[] { 0x0B, 0x00, 0x5A, 0x00, 0x00 }), // common_ctr_pos_set2
                new UnknownOpcode(0, 0x69, new byte[] { 0x17, 0x01, 0x00 }), // common_ctr_flag_set
                new UnknownOpcode(0, 0x64, new byte[] { 0x80 })); // player_ctr_motion_init

            // These rooms mark the map as visited, which changes the door logic in 1010 and 1050
            Nop(gameData, GraveyardCutsceneRoom, 0x1EE352);
            Nop(gameData, SteveCutsceneRoom, 0x27B5A2);

            // Steve must not change the player's weapon
            Nop(gameData, SteveCutsceneRoom, 0x27B910);
            Nop(gameData, SteveCutsceneRoom, 0x27B92C, 0x27B932);

            // What Steve hands over is a randomised item, which 1031 gives instead so it can be retried
            Nop(gameData, SteveCutsceneRoom, 0x27B940, 0x27B950);
            Nop(gameData, SteveCutsceneRoom, 0x27CA10, 0x27CA20);
            AddSteveHandover(gameData);
        }

        /// <summary>
        /// Without a free slot the game refuses the item and forgets about it, so offer it
        /// again each time the player enters the room until it has been taken.
        /// </summary>
        private void AddSteveHandover(GameData gameData)
        {
            var rrdt = gameData.GetRdt(SteveRoom);
            if (rrdt == null)
                return;

            var cutFlag = (byte)0x0A;
            var type = ReCvItemIds.Handgun;
            var isHandoverItem = new UnknownOpcode(0, 0x06, new byte[] { 0x08, 0x00, type }); // cmpb VAR_ITEM_ID
            var setHandoverItem = new UnknownOpcode(0, 0x08, new byte[] { 0x08, type, 0x00 }); // setb VAR_ITEM_ID
            rrdt.AddCvItemOpcode(isHandoverItem);
            rrdt.AddCvItemOpcode(setHandoverItem);

            // The room's own script clears FG_CUT_BG 20 later on in this frame
            AddEarlyFrameBlock(rrdt, new List<UnknownOpcode>
            {
                Check(0x01, SteveItemTakenEventFlag, 1),
                Check(cutFlag, ItemTakenCutFlag, 0),
                isHandoverItem,
                SetEvent(SteveItemTakenEventFlag, 0)
            });
            AddEarlyFrameBlock(rrdt, new List<UnknownOpcode>
            {
                Check(0x01, 107, 0),
                Check(0x01, SteveItemTakenEventFlag, 1),
                Check(0x04, HandoverOfferedRoomFlag, 1),
                Check(cutFlag, DoorTransitionCutFlag, 1),
                Check(cutFlag, CinematicCutFlag, 1),
                new UnknownOpcode(0, 0x05, new byte[] { 0x04, HandoverOfferedRoomFlag, 0x00, 0x00, 0x00 }),
                new UnknownOpcode(0, 0x05, new byte[] { cutFlag, ItemGetScreenCutFlag, 0x00, 0x00, 0x00 }),
                setHandoverItem,
                new UnknownOpcode(0, 0x05, new byte[] { cutFlag, ItemWithoutAskingCutFlag, 0x00, 0x00, 0x00 })
            });
        }

        private static UnknownOpcode Check(byte kind, int index, byte value)
        {
            return new UnknownOpcode(0, 0x04, new byte[] { kind, (byte)(index & 0xFF), (byte)((index >> 8) & 0xFF), 0x00, value });
        }

        /// <summary>
        /// When the conditions are met, leaves the room for another variant of it,
        /// using the load screen rather than a door animation.
        /// </summary>
        private void AddRoomReload(GameData gameData, RdtId rdtId, RdtId target, int? returnEventFlag, params UnknownOpcode[] conditions)
        {
            var rrdt = gameData.GetRdt(rdtId);
            if (rrdt == null)
                return;

            var variant = (byte)(target.Variant ?? 0);
            var body = new List<UnknownOpcode>(conditions) { Check(0x0A, DoorTransitionCutFlag, 1) };
            if (returnEventFlag != null)
                body.Add(SetEvent(returnEventFlag.Value, 0));
            body.Add(new UnknownOpcode(0, 0x92, new byte[] { 0x00 })); // bg_se_off_2
            body.Add(new UnknownOpcode(0, 0x93, new byte[] { 0x00 })); // bgm_off_2
            body.Add(new UnknownOpcode(0, 0xB6, new byte[] { variant })); // room_sound_case
            body.Add(new UnknownOpcode(0, 0x37, new byte[] { variant })); // room_case_no
            body.Add(new UnknownOpcode(0, 0x33, new byte[] { 0x00, 0x00, 0x00, (byte)target.Stage, (byte)target.Room, 0x00, 0xFF })); // set_door_call
            AddEarlyFrameBlock(rrdt, body);
        }

        /// <summary>
        /// Runs the given opcodes once, the first time the room runs after the flag was set by a room reload.
        /// </summary>
        private void AddReturnPlacement(GameData gameData, RdtId rdtId, int returnEventFlag, params UnknownOpcode[] placement)
        {
            var rrdt = gameData.GetRdt(rdtId);
            if (rrdt == null)
                return;

            var body = new List<UnknownOpcode>
            {
                Check(0x01, returnEventFlag, 0),
                SetEvent(returnEventFlag, 1)
            };
            body.AddRange(placement);
            AddEarlyFrameBlock(rrdt, body);
        }

        // At the top of the frame script, as the end of it is not always reached
        private static void AddEarlyFrameBlock(RandomizedRdt rrdt, List<UnknownOpcode> body)
        {
            body.Add(new UnknownOpcode(0, 0x03, new byte[] { 0x00 })); // endif

            var size = 0;
            foreach (var opcode in body)
                size += opcode.Length;

            rrdt.AddEarlyFrameOpcode(new UnknownOpcode(0, 0x01, new byte[] { (byte)size }));
            foreach (var opcode in body)
                rrdt.AddEarlyFrameOpcode(opcode);
        }

        private static UnknownOpcode SetEvent(int index, byte value)
        {
            return new UnknownOpcode(0, 0x05, new byte[] { 0x01, (byte)(index & 0xFF), (byte)((index >> 8) & 0xFF), 0x00, value });
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
