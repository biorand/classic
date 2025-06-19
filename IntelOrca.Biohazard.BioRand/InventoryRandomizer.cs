using System;
using System.Collections.Generic;
using System.Linq;
using SixLabors.ImageSharp;

namespace IntelOrca.Biohazard.BioRand
{
    internal class InventoryRandomizer
    {
        public void Randomize(IClassicRandomizerGeneratedVariation context)
        {
            var config = context.Configuration;
            var rng = context.Rng.NextFork();

            var inventorySizeMin = config.GetValueOrDefault("inventory/min", 0);
            var inventorySizeMax = config.GetValueOrDefault("inventory/max", 8);
            var inventorySize = rng.Next(inventorySizeMin, inventorySizeMax + 1);
            var inventoryState = new InventoryBuilder(inventorySize);

            var knife = GetRandomEnabled("inventory/weapon/knife");
            if (knife)
            {
                var kvp = context.Variation.Map.Items.FirstOrDefault(x => x.Value.Kind == "weapon/knife");
                if (kvp.Key != 0)
                {
                    inventoryState.Add(new Item((byte)kvp.Key, (byte)kvp.Value.Max));
                }
            }

            var primary = GetRandomWeapon("inventory/primary");
            if (primary != null)
            {
                inventoryState.Add(primary);
            }

            var secondary = GetRandomWeapon("inventory/secondary", exclude: primary);
            if (secondary != null)
            {
                inventoryState.Add(secondary);
            }

            var itemPool = new ItemPool(rng);
            itemPool.AddGroup(context, "health/");
            itemPool.AddGroup(context, "ink");
            while (!inventoryState.Full && itemPool.TakeStack(context) is Item stack)
            {
                inventoryState.Add(stack);
            }

            context.ModBuilder.Inventory = [inventoryState.Build()];

            bool GetRandomEnabled(string configKey)
            {
                var configValue = config.GetValueOrDefault(configKey, "Always");
                if (configValue == "Always")
                    return true;
                if (configValue == "Never")
                    return false;
                return rng.NextOf(false, true);
            }

            WeaponSwag? GetRandomWeapon(string prefix, WeaponSwag? exclude = null)
            {
                var pool = new List<WeaponSwag?>();
                if (config.GetValueOrDefault($"{prefix}/none", false))
                {
                    pool.Add(null);
                }
                foreach (var kvp in context.Variation.Map.Items)
                {
                    var definition = kvp.Value;
                    if (!definition.Kind.StartsWith("weapon/"))
                        continue;

                    var swag = new WeaponSwag(kvp.Key, kvp.Value);
                    if (swag.Group == exclude?.Group)
                        continue;

                    var isEnabled = config.GetValueOrDefault($"{prefix}/{definition.Kind}", false);
                    if (isEnabled)
                    {
                        pool.Add(swag);
                    }
                }

                var chosen = pool.Shuffle(rng).FirstOrDefault();
                if (chosen != null)
                {
                    var ammoMin = config.GetValueOrDefault($"{prefix}/ammo/min", 0.0) * chosen.Definition.Max;
                    var ammoMax = config.GetValueOrDefault($"{prefix}/ammo/max", 0.0) * chosen.Definition.Max;
                    var ammoTotal = (int)Math.Round(rng.NextDouble(ammoMin, ammoMax));
                    chosen.WeaponAmount = Math.Min(ammoTotal, chosen.Definition.Max);
                    if (chosen.Definition.Ammo is int[] ammo && ammo.Length != 0)
                    {
                        chosen.ExtraAmount = ammoTotal - chosen.WeaponAmount;
                        chosen.ExtraType = rng.NextOf(ammo);
                        chosen.ExtraMaxStack = context.Variation.Map.Items[chosen.ExtraType].Max;
                    }
                }
                return chosen;
            }
        }

        private class InventoryBuilder
        {
            private List<RandomInventory.Entry> _entries = [];

            public int Capacity { get; }
            public bool Full => _entries.Count >= Capacity;

            public InventoryBuilder(int capacity)
            {
                Capacity = capacity;
            }

            public RandomInventory Build()
            {
                while (_entries.Count < Capacity)
                {
                    _entries.Add(new RandomInventory.Entry());
                }
                return new RandomInventory([.. _entries], null);
            }

            public void Add(Item item)
            {
                _entries.Add(new RandomInventory.Entry(item.Type, (byte)item.Amount));
            }

            public void Add(WeaponSwag swag)
            {
                Add(new Item((byte)swag.WeaponType, (ushort)swag.WeaponAmount));

                var extra = swag.ExtraAmount;
                while (extra > 0)
                {
                    var take = Math.Min(extra, swag.ExtraMaxStack);
                    Add(new Item((byte)swag.ExtraType, (ushort)take));
                    extra -= take;
                }
            }
        }

        private class WeaponSwag(int itemId, MapItemDefinition definition)
        {
            public MapItemDefinition Definition => definition;
            public int WeaponType => itemId;
            public int WeaponAmount { get; set; }
            public int ExtraType { get; set; }
            public int ExtraAmount { get; set; }
            public int ExtraMaxStack { get; set; }

            public string Group => definition.Kind.Split('/').Skip(1).First();
        }

        private class ItemPool(Rng rng)
        {
            private List<List<Item>> _groups = [];
            private int _next = -1;

            public void AddGroup(IClassicRandomizerGeneratedVariation context, string prefix)
            {
                var g = GetRandomItems(context, prefix);
                if (g.Count != 0)
                    _groups.Add(g);
            }

            public Item? TakeStack(IClassicRandomizerGeneratedVariation context)
            {
                if (_next == -1 || _next >= _groups.Count)
                {
                    _groups = [.. _groups.Shuffle(rng)];
                    _next = 0;
                }
                if (_groups.Count == 0)
                {
                    return null;
                }

                var group = _groups[_next];
                var itemIndex = rng.Next(0, group.Count);
                var item = group[itemIndex];
                var definition = context.Variation.Map.Items[item.Type];
                var take = Math.Min(item.Amount, definition.Max);
                var remaining = item.Amount - take;
                if (remaining > 0)
                {
                    group[itemIndex] = new Item(item.Type, (ushort)(item.Amount - take));
                }
                else
                {
                    group.RemoveAt(itemIndex);
                    if (group.Count == 0)
                    {
                        _groups.RemoveAt(_next);
                    }
                }
                _next++;
                return new Item(item.Type, (ushort)take);
            }

            private List<Item> GetRandomItems(IClassicRandomizerGeneratedVariation context, string prefix)
            {
                var items = new List<Item>();
                var config = context.Configuration;
                foreach (var kvp in context.Variation.Map.Items)
                {
                    var itemId = kvp.Key;
                    var definition = kvp.Value;
                    if (!definition.Kind.StartsWith(prefix))
                        continue;

                    var min = config.GetValueOrDefault($"inventory/{definition.Kind}/min", 0);
                    var max = config.GetValueOrDefault($"inventory/{definition.Kind}/max", 0);
                    var amount = rng.Next(min, max + 1);
                    if (amount > 0)
                    {
                        items.Add(new Item((byte)itemId, (ushort)amount));
                    }
                }
                return items;
            }
        }

    }
}
