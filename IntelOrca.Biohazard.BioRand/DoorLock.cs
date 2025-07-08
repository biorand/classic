using System.Diagnostics;

namespace IntelOrca.Biohazard.BioRand
{
    [DebuggerDisplay("Id = {Id} Key = {KeyItemId}")]
    public readonly struct DoorLock(int id, int keyItemId)
    {
        public DoorLock() : this(0, 0)
        {
        }

        public int Id { get; init; } = id;
        public int KeyItemId { get; init; } = keyItemId;
    }
}
