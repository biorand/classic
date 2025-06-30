using System.Diagnostics;

namespace IntelOrca.Biohazard.BioRand
{
    [DebuggerDisplay("Id = {Id} Key = {KeyItemId}")]
    public readonly struct DoorLock(int id, int keyItemId)
    {
        public int Id => id;
        public int KeyItemId => keyItemId;
    }
}
