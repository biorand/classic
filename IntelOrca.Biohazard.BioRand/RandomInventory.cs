using System.Linq;

namespace IntelOrca.Biohazard.BioRand
{
    public class RandomInventory
    {
        public Entry[] Entries { get; init; } = [];
        public Entry? Special { get; init; } = null;

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
            public byte Type { get; init; }
            public byte Count { get; init; }
            public byte Part { get; init; }

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
