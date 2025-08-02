using System.Diagnostics;

namespace IntelOrca.Biohazard.BioRand
{
    [DebuggerDisplay("Id = {Id} Key = {Type}")]
    public readonly struct DoorLock(int id, int type)
    {
        public DoorLock() : this(0, 0)
        {
        }

        public int Id { get; init; } = id;
        public int Type { get; init; } = type;
    }
}
