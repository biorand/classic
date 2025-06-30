using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace IntelOrca.Biohazard.BioRand
{
    public class ModBuilder
    {
        private readonly Dictionary<RdtItemId, DoorLock?> _doorLock = new();
        private readonly Dictionary<int, Item> _itemMap = new();
        private readonly List<EnemyPlacement> _enemyPlacements = new();
        private readonly Dictionary<string, MusicSourceFile> _music = new(StringComparer.OrdinalIgnoreCase);

        public string Name { get; set; } = "";
        public string Description { get; set; } = "";
        public ImmutableDictionary<string, object> General { get; set; } = ImmutableDictionary.Create<string, object>();
        public ImmutableArray<RandomInventory> Inventory { get; set; } = [];
        public ImmutableArray<int> AssignedItemGlobalIds => [.. _itemMap.Keys];
        public ImmutableArray<EnemyPlacement> EnemyPlacements => [.. _enemyPlacements];
        public ImmutableDictionary<string, MusicSourceFile> Music => _music.ToImmutableDictionary();
        public ImmutableDictionary<int, CharacterReplacement> Characters { get; set; } = ImmutableDictionary<int, CharacterReplacement>.Empty;
        public ImmutableArray<string> EnemySkins { get; set; } = [];
        public ImmutableArray<NpcReplacement> Npcs { get; set; } = [];
        public ImmutableDictionary<string, string> Voices { get; set; } = ImmutableDictionary<string, string>.Empty;

        public void SetDoorTarget(RdtItemId doorIdentity, RdtItemId target)
        {
        }

        public void SetDoorLock(RdtItemId doorIdentity, DoorLock? doorLock)
        {
            _doorLock.Add(doorIdentity, doorLock);
        }

        public Item? GetItem(int globalId)
        {
            return _itemMap.TryGetValue(globalId, out var item) ? item : null;
        }

        public void SetItem(int globalId, Item item)
        {
            _itemMap.Add(globalId, item);
        }

        public void AddEnemy(EnemyPlacement placement)
        {
            _enemyPlacements.Add(placement);
        }

        public void ApplyToRdt(RandomizedRdt rrdt)
        {
            foreach (var doorOpcode in rrdt.Doors)
            {
                var doorIdentity = new RdtItemId(rrdt.RdtId, doorOpcode.Id);
                if (_doorLock.TryGetValue(doorIdentity, out var doorLock))
                {
                    if (doorLock == null)
                    {
                        doorOpcode.LockId = 0;
                        doorOpcode.LockType = 0;
                    }
                    else
                    {
                        doorOpcode.LockId = (byte)doorLock.Value.Id;
                        doorOpcode.LockType = (byte)doorLock.Value.KeyItemId;
                    }
                }
            }
            foreach (var itemOpcode in rrdt.Items)
            {
                if (_itemMap.TryGetValue(itemOpcode.GlobalId, out var item))
                {
                    itemOpcode.Type = item.Type;
                    itemOpcode.Amount = item.Amount;
                }
            }
        }

        internal string GetDump(IClassicRandomizerGeneratedVariation context)
        {
            var map = context.Variation.Map;
            var mdb = new MarkdownBuilder();

            mdb.Heading(1, "Inventory");
            DumpInventory(context.Variation.PlayerName, Inventory[0]);

            mdb.Heading(1, "Items");

            var placedItems = _itemMap
                .Select(x => new PlacedItem(x.Key, x.Value, map.GetItem(x.Value.Type)))
                .GroupBy(x => x.Group);

            DumpGroup("key", "Keys");
            DumpGroup("weapon", "Weapons");
            DumpGroup("ammo", "Ammo");
            DumpGroup("health", "Health");
            DumpGroup("ink", "Ink");

            mdb.Heading(1, "Enemies");
            mdb.Heading(3, "Summary");
            mdb.Table("Enemy", "Total", "Rooms", "Avg. per room");
            var typeToEntryMap = map.Enemies
                .SelectMany(x => x.Entries)
                .SelectMany(x => x.Id.Select(y => (x, y)))
                .ToDictionary(x => x.Item2, x => x.Item1);
            foreach (var g in _enemyPlacements.GroupBy(x => typeToEntryMap[x.Type]).OrderByDescending(x => x.Count()))
            {
                var enemyName = g.Key.Name;
                var total = g.Count();
                var rooms = g.GroupBy(x => x.RdtId).Count();
                var avg = ((double)total / rooms).ToString("0.0");
                mdb.TableRow(enemyName, total, rooms, avg);
            }

            mdb.Heading(3, "List");
            mdb.Table("Global ID", "RDT", "Room", "ID", "Enemy");
            foreach (var enemy in _enemyPlacements.OrderBy(x => x.RdtId).ThenBy(x => x.Id))
            {
                var entry = map.Enemies.SelectMany(x => x.Entries).FirstOrDefault(x => x.Id.Contains(enemy.Type));
                var enemyName = entry?.Name ?? $"{enemy.Type}";
                var roomName = map.GetRoomsContaining(enemy.RdtId).FirstOrDefault()?.Name ?? "";
                mdb.TableRow(enemy.GlobalId, enemy.RdtId, roomName, enemy.Id, enemyName);
            }

            mdb.Heading(1, "Doors");
            mdb.Table("RDT", "ID", "ROOM", "DOOR", "TARGET", "LOCK", "REQUIRES");
            foreach (var r in map.Rooms)
            {
                foreach (var d in r.Value.Doors ?? [])
                {
                    var rdt = r.Value.Rdts.FirstOrDefault();
                    var requires = string.Join(", ", (d.Requires2 ?? []).Select(GetRequiresString));
                    if (requires == "")
                    {
                        if (d.Kind == "locked")
                        {
                            requires = "(locked)";
                        }
                        else if (d.Kind == "unlock")
                        {
                            requires = "(unlock)";
                        }
                    }
                    mdb.TableRow(rdt, (object?)d.Id ?? "", r.Value.Name ?? "", d.Name ?? "", d.Target ?? "", d.LockId ?? 0, requires);
                }
            }


            return mdb.Build();

            void DumpInventory(string playerName, RandomInventory inventory)
            {
                mdb.Heading(2, playerName);
                mdb.Table("Item", "Amount");
                foreach (var entry in inventory.Entries)
                {
                    if (entry.Part != 0)
                        continue;

                    var itemName = map.GetItem(entry.Type)?.Name ?? $"{entry.Type}";
                    mdb.TableRow(itemName, entry.Count);
                }
            }

            void DumpGroup(string group, string heading)
            {
                var g = placedItems.FirstOrDefault(x => x.Key == group);
                if (g == null)
                    return;

                var filtered = g
                    .Select(x => x)
                    .OrderBy(x => x.Item.Type)
                    .ThenBy(x => x.GlobalId)
                    .ToArray();

                mdb.Heading(2, heading);
                mdb.Table("ID", "Item", "Amount", "RDT", "Room", "Location");
                foreach (var i in filtered)
                {
                    var (rdtId, room, location) = GetItemSlotName(map, i.GlobalId);
                    mdb.TableRow(i.GlobalId, i.Definition?.Name ?? "", i.Item.Amount, rdtId, room, location);
                }
            }

            string GetRequiresString(string s)
            {
                var req = MapRequirement.Parse(s);
                if (req.Kind == MapRequirementKind.Item)
                {
                    if (int.TryParse(req.Value, out var itemId))
                    {
                        var item = map.GetItem(itemId);
                        return item?.Name ?? s;
                    }
                }
                return s;
            }
        }

        private static (RdtId rdtId, string room, string location) GetItemSlotName(Map map, int globalId)
        {
            foreach (var kvp in map.Rooms)
            {
                var room = kvp.Value;
                if (room.Items == null)
                    continue;

                var rdt = room.Rdts?.FirstOrDefault() ?? new RdtId();
                foreach (var item in room.Items)
                {
                    if (item.GlobalId == globalId)
                    {
                        return (rdt, room.Name ?? "", item.Name ?? "");
                    }
                }
            }
            return (default, "", "");
        }

        public void SetMusic(string path, MusicSourceFile music)
        {
            _music[path] = music;
        }

        private readonly struct PlacedItem(int globalId, Item item, MapItemDefinition? definition)
        {
            public int GlobalId => globalId;
            public Item Item => item;
            public MapItemDefinition? Definition => definition;
            public string Group => definition?.Kind.Split('/').First() ?? "";
        }

        public static ModBuilder FromJson(string json)
        {
            return new ModBuilder();
        }

        public string ToJson()
        {
            var options = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                WriteIndented = true,
                Converters = { new RdtIdConverter() },
                Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            };
            return JsonSerializer.Serialize(new
            {
                DoorsLocks = _doorLock.ToDictionary(x => x.Key.ToString(), x => new
                {
                    x.Value?.Id,
                    x.Value?.KeyItemId
                }),
                EnemyPlacements = _enemyPlacements,
                Npcs,
                Items = _itemMap,
                Characters,
                Voices,
                Music = _music
            }, options);
        }
    }
}
