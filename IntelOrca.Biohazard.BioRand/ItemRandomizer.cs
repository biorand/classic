using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using SixLabors.ImageSharp;

namespace IntelOrca.Biohazard.BioRand
{
    internal class ItemRandomizer
    {
        public void Randomize(IClassicRandomizerGeneratedVariation context)
        {
            var config = context.Configuration;
            var map = context.Variation.Map;
            var rng = context.Rng.NextFork();
            var modBuilder = context.ModBuilder;

            var documentPriority = config.GetValueOrDefault("items/documents", false)
                ? config.GetValueOrDefault("items/documents/keys", false)
                    ? ItemPriority.Normal
                    : ItemPriority.Low
                : ItemPriority.Disabled;
            var hiddenPriority = config.GetValueOrDefault("items/hidden/keys", false)
                ? ItemPriority.Normal
                : ItemPriority.Low;

            var itemSlots = new ItemSlotCollection(modBuilder, map, documentPriority, hiddenPriority, rng);

            // Inventory weapons
            var inventoryWeapons = modBuilder.Inventory[0].Entries
                .Where(x => x.Type != 0)
                .Select(x => new KeyValuePair<int, MapItemDefinition>(x.Type, map.Items[x.Type]))
                .Where(x => x.Value.Kind.StartsWith("weapon/"))
                .Select(x => new WeaponInfo(x.Key, x.Value, config))
                .ToArray();

            // Weapons
            var weapons = map.Items
                .Where(x => x.Value.Kind.StartsWith("weapon/"))
                .Select(x => new WeaponInfo(x.Key, x.Value, config))
                .Shuffle(rng);
            var wpgroup = new HashSet<string>(inventoryWeapons.Select(x => x.Group));
            var wpplaced = new List<WeaponInfo>(inventoryWeapons);
            foreach (var wp in weapons)
            {
                if (!wp.Enabled)
                    continue;

                // Do not place weapons of the same group
                if (!wpgroup.Add(wp.Group))
                    continue;

                if (itemSlots.DequeueNormalFirst() is not int globalId)
                    break;

                var amount = rng.Next(wp.MinInitial, wp.MaxInitial + 1);
                modBuilder.SetItem(globalId, new Item((byte)wp.Type, (ushort)amount));
                wpplaced.Add(wp);
            }

            // Everything else
            var weights = map.Items
                .Select(x => (x.Key, x.Value, config.GetValueOrDefault($"items/distribution/{x.Value.Kind}", 0.0)))
                .Where(x => x.Item3 != 0)
                .ToArray();

            // Disable ammo that can't be used
            for (var i = 0; i < weights.Length; i++)
            {
                var itemType = weights[i].Item1;
                var definition = weights[i].Item2;
                if (definition.Kind.StartsWith("ammo/"))
                {
                    if (!wpplaced.Any(x => x.Enabled && x.SupportsAmmo(itemType)))
                    {
                        weights[i].Item3 = 0;
                    }
                }
            }

            var totalWeight = weights.Sum(x => x.Item3);
            var totalItems = itemSlots.Count;
            var itemCounts = weights
                .Select(x => (x.Item1, x.Item2, (int)Math.Ceiling(x.Item3 / totalWeight * totalItems)))
                .OrderBy(x => x.Item3)
                .ToArray();

            foreach (var (type, definition, count) in itemCounts)
            {
                var minStack = Math.Max(1, config.GetValueOrDefault($"items/stack/min/{definition.Kind}", 1));
                var maxStack = Math.Min(definition.Max, config.GetValueOrDefault($"items/stack/max/{definition.Kind}", definition.Max));
                for (var i = 0; i < count; i++)
                {
                    if (itemSlots.DequeueAny() is not int globalId)
                        break;

                    var amount = rng.Next(minStack, maxStack + 1);
                    modBuilder.SetItem(globalId, new Item((byte)type, (ushort)amount));
                }
            }

            // Set all remaining items to empty
            while (itemSlots.DequeueAny() is int globalId)
            {
                modBuilder.SetItem(globalId, new Item(0, 0));
            }
        }

        public class ItemSlotCollection
        {
            private readonly Rng _rng;
            private readonly Queue<MapRoomItem> _normalPriority = [];
            private readonly Queue<MapRoomItem> _lowPriority = [];

            public int Count => _normalPriority.Count + _lowPriority.Count;

            public ItemSlotCollection(ModBuilder modBuilder, Map map, ItemPriority documents, ItemPriority hidden, Rng rng)
            {
                _rng = rng;

                var assigned = modBuilder.AssignedItemGlobalIds.ToHashSet();
                var allItems = map.Rooms.Values
                    .SelectMany(x => x.Items)
                    .Where(x => x.GlobalId != null)
                    .DistinctBy(x => x.GlobalId)
                    .Shuffle(rng);

                foreach (var item in allItems)
                {
                    if (assigned.Contains(item.GlobalId ?? 0))
                        continue;

                    if (item.Document == true && documents == ItemPriority.Disabled)
                        continue;
                    if (item.Hidden == true && hidden == ItemPriority.Disabled)
                        continue;

                    if (item.Optional == true)
                        _lowPriority.Enqueue(item);
                    else if (item.Document == true && documents == ItemPriority.Low)
                        _lowPriority.Enqueue(item);
                    else if (item.Hidden == true && hidden == ItemPriority.Low)
                        _lowPriority.Enqueue(item);
                    else
                        _normalPriority.Enqueue(item);
                }
            }

            public int? DequeueNormalFirst()
            {
                if (_normalPriority.Count != 0)
                    return _normalPriority.Dequeue().GlobalId;
                else if (_lowPriority.Count != 0)
                    return _lowPriority.Dequeue().GlobalId;
                else
                    return null;
            }

            public int? DequeueAny()
            {
                var count = Count;
                if (count == 0)
                    return null;

                var index = _rng.Next(0, count);
                if (index >= _normalPriority.Count)
                    return _lowPriority.Dequeue().GlobalId;
                else
                    return _normalPriority.Dequeue().GlobalId;
            }
        }

        public enum ItemPriority
        {
            Disabled,
            Low,
            Normal
        }

        public sealed class WeaponInfo(int type, MapItemDefinition definition, RandomizerConfiguration config)
        {
            public int Type => type;
            public MapItemDefinition Definition => definition;
            public string Group { get; } = definition.Kind.Split('/').Skip(1).First();
            public bool Enabled { get; } = config.GetValueOrDefault($"items/weapon/enabled/{definition.Kind}", false);
            public int MinInitial { get; } = config.GetValueOrDefault($"items/weapon/initial/min/{definition.Kind}", 0);
            public int MaxInitial { get; } = config.GetValueOrDefault($"items/weapon/initial/max/{definition.Kind}", 0);
            public bool SupportsAmmo(int itemId) => definition.Ammo != null && definition.Ammo.Contains(itemId);
        }
    }
}
