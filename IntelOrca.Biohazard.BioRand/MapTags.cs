namespace IntelOrca.Biohazard.BioRand
{
    internal static class MapTags
    {
        public const string Box = "box";
        public const string Safe = "safe";
        public const string Save = "save";
        public const string Prologue = "prologue";
        public const string SegmentEnd = "segment-end";
        public const string Begin = "begin";
        public const string End = "end";

        /// <summary>
        /// Door must be connected out from this room. i.e. this door
        /// can't be used to first access this room.
        /// </summary>
        public const string ConnectOut = "connect-out";

        /// <summary>
        /// Door must connect back to a room with shared ancestor.
        /// Used after a one way door, and we need to ensure that the player can return
        /// without to pick up items.
        /// </summary>
        public const string ConnectBack = "connect-back";

        /// <summary>
        /// Tells the lock rando to prioritise a lock for this door.
        /// </summary>
        public const string LockPriority = "lock-priority";
    }
}
