namespace IntelOrca.Biohazard.BioRand
{
    internal static class DoorKinds
    {
        /// <summary>
        /// Door is dynamically locked, lock id should be preserved.
        /// </summary>
        public const string Dynamic = "dynamic";

        /// <summary>
        /// Door is permanently or temporarily blocked. Treated as a wall.
        /// </summary>
        public const string Blocked = "blocked";

        /// <summary>
        /// Door can be unblocked to make a two way passage.
        /// Door may have requirements (puzzle doors).
        /// </summary>
        public const string Unblock = "unblock";

        /// <summary>
        /// Door is locked, but can be unlocked (without key) from the other side.
        /// </summary>
        public const string Locked = "locked";

        /// <summary>
        /// Door can be unlocked without a key.
        /// </summary>
        public const string Unlock = "unlock";

        /// <summary>
        /// Door is one way, but there is some other way to return.
        /// </summary>
        public const string OneWay = "oneway";

        /// <summary>
        /// Door is one way, and there is no way to return again.
        /// </summary>
        public const string NoReturn = "noreturn";
    }
}
