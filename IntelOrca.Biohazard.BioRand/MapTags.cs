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
    }
}
