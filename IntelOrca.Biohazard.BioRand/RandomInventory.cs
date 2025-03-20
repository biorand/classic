using System.Linq;

namespace IntelOrca.Biohazard.BioRand
{
    internal class RandomInventory
    {
        public Entry[] Entries { get; } = [];
        public Entry? Special { get; } = null;

        public RandomInventory()
        {
        }

        public RandomInventory(Entry[] entries, Entry? special)
        {
            Entries = entries;
            Special = special;
        }

        public RandomInventory WithSize(int max)
        {
            var entries = Entries.Take(max).ToList();
            while (entries.Count < max)
                entries.Add(new Entry());
            return new RandomInventory([.. entries], Special);
        }

        public struct Entry
        {
            public byte Type { get; }
            public byte Count { get; }
            public byte Part { get; }

            public Entry(byte type, byte count)
            {
                Type = type;
                Count = count;
                Part = 0;
            }

            public Entry(byte type, byte count, byte part)
            {
                Type = type;
                Count = count;
                Part = part;
            }
        }
    }
}
