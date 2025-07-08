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

        public string Game { get; set; } = "";
        public string Name { get; set; } = "";
        public string Description { get; set; } = "";
        public ImmutableDictionary<string, object?> General { get; set; } = ImmutableDictionary.Create<string, object?>();
        public ImmutableArray<RandomInventory> Inventory { get; set; } = [];
        public ImmutableArray<int> AssignedItemGlobalIds => [.. _itemMap.Keys];
        public ImmutableArray<EnemyPlacement> EnemyPlacements => [.. _enemyPlacements];
        public ImmutableDictionary<string, MusicSourceFile> Music => _music.ToImmutableDictionary();
        public ImmutableDictionary<int, CharacterReplacement> Characters { get; set; } = ImmutableDictionary<int, CharacterReplacement>.Empty;
        public ImmutableArray<string> EnemySkins { get; set; } = [];
        public ImmutableArray<NpcReplacement> Npcs { get; set; } = [];
        public ImmutableDictionary<string, string> Voices { get; set; } = ImmutableDictionary<string, string>.Empty;
        public int? Seed { get; set; }
        public RandomizerConfiguration? Configuration { get; set; }

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

        public void SetMusic(string path, MusicSourceFile music)
        {
            _music[path] = music;
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

        public ClassicMod Build()
        {
            return new ClassicMod()
            {
                Game = Game,
                Name = Name,
                Description = Description,
                General = General,
                Inventory = Inventory,
                Doors = _doorLock.ToImmutableDictionary(),
                Items = _itemMap.ToImmutableDictionary(),
                EnemyPlacements = EnemyPlacements,
                Music = Music,
                Characters = Characters,
                EnemySkins = EnemySkins,
                Npcs = Npcs,
                Voices = Voices,
                Seed = Seed,
                Configuration = Configuration
            };
        }
    }

    public sealed class ClassicMod
    {
        public string Game { get; init; } = "";
        public string Name { get; init; } = "";
        public string Description { get; init; } = "";
        public ImmutableDictionary<string, object?> General { get; set; } = ImmutableDictionary.Create<string, object?>();
        public ImmutableArray<RandomInventory> Inventory { get; init; } = [];
        public ImmutableDictionary<RdtItemId, DoorLock?> Doors { get; init; } = ImmutableDictionary.Create<RdtItemId, DoorLock?>();
        public ImmutableDictionary<int, Item> Items { get; init; } = ImmutableDictionary.Create<int, Item>();
        public ImmutableArray<EnemyPlacement> EnemyPlacements { get; init; } = [];
        public ImmutableDictionary<string, MusicSourceFile> Music { get; init; } = ImmutableDictionary<string, MusicSourceFile>.Empty;
        public ImmutableDictionary<int, CharacterReplacement> Characters { get; init; } = ImmutableDictionary<int, CharacterReplacement>.Empty;
        public ImmutableArray<string> EnemySkins { get; init; } = [];
        public ImmutableArray<NpcReplacement> Npcs { get; init; } = [];
        public ImmutableDictionary<string, string> Voices { get; init; } = ImmutableDictionary<string, string>.Empty;
        public int? Seed { get; init; }
        public RandomizerConfiguration? Configuration { get; init; }

        public static ClassicMod FromJson(string json)
        {
            var result = JsonSerializer.Deserialize<ClassicMod>(json, JsonOptions)!;
            result.General = result.General.ToImmutableDictionary(
                kvp => kvp.Key,
                kvp => kvp.Value is JsonElement el
                    ? el.ValueKind switch
                    {
                        JsonValueKind.String => el.GetString(),
                        JsonValueKind.Number => el.TryGetInt32(out int i) ? (object)i : (object)el.GetDouble(),
                        JsonValueKind.True => true,
                        JsonValueKind.False => false,
                        JsonValueKind.Null => null,
                        _ => el.ToString()
                    }
                    : kvp.Value
            );
            return result;
        }

        public string ToJson()
        {
            return JsonSerializer.Serialize(this, JsonOptions);
        }

        private static JsonSerializerOptions JsonOptions { get; } = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = true,
            Converters = {
                new RdtIdConverter(),
                new RdtItemIdConverter(),
                new RandomizerConfigurationJsonConverter2() },
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        };

        internal string GetDump(Map map, string playerName)
        {
            var mdb = new MarkdownBuilder();

            mdb.Heading(1, "Inventory");
            DumpInventory(playerName, Inventory[0]);

            mdb.Heading(1, "Items");

            var placedItems = Items
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
            foreach (var g in EnemyPlacements.GroupBy(x => typeToEntryMap[x.Type]).OrderByDescending(x => x.Count()))
            {
                var enemyName = g.Key.Name;
                var total = g.Count();
                var rooms = g.GroupBy(x => x.RdtId).Count();
                var avg = ((double)total / rooms).ToString("0.0");
                mdb.TableRow(enemyName, total, rooms, avg);
            }

            mdb.Heading(3, "List");
            mdb.Table("Global ID", "RDT", "Room", "ID", "Enemy");
            foreach (var enemy in EnemyPlacements.OrderBy(x => x.RdtId).ThenBy(x => x.Id))
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

        private readonly struct PlacedItem(int globalId, Item item, MapItemDefinition? definition)
        {
            public int GlobalId => globalId;
            public Item Item => item;
            public MapItemDefinition? Definition => definition;
            public string Group => definition?.Kind.Split('/').First() ?? "";
        }

    }

    internal class RandomizerConfigurationJsonConverter2 : RandomizerConfigurationJsonConverter
    {
        public override RandomizerConfiguration? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            var d = JsonSerializer.Deserialize(ref reader, typeof(Dictionary<string, object>)) as Dictionary<string, object>;
            return RandomizerConfiguration.FromDictionary(d!);
        }
    }
}
