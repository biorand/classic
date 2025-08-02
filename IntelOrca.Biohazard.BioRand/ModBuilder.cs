using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace IntelOrca.Biohazard.BioRand
{
    public class ModBuilder
    {
        private readonly Dictionary<RdtItemId, DoorTargetLock> _doors = new();
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

        public ModBuilder SetGeneral(string key, object? value)
        {
            General = General.SetItem(key, value);
            return this;
        }

        public void SetDoorTarget(RdtItemId doorIdentity, DoorTarget target)
        {
            _doors.TryGetValue(doorIdentity, out var info);
            _doors[doorIdentity] = info with { Target = target };
        }

        public void SetDoorLock(RdtItemId doorIdentity, DoorLock? doorLock)
        {
            _doors.TryGetValue(doorIdentity, out var info);
            _doors[doorIdentity] = info with { Lock = doorLock };
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

        public void SetMusic(string path, MusicSourceFile music)
        {
            _music[path] = music;
        }

        public static ModBuilder FromJson(string json)
        {
            return new ModBuilder();
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
                Doors = _doors.ToImmutableDictionary(),
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
        public ImmutableDictionary<RdtItemId, DoorTargetLock> Doors { get; init; } = ImmutableDictionary.Create<RdtItemId, DoorTargetLock>();
        public ImmutableDictionary<int, Item> Items { get; init; } = ImmutableDictionary.Create<int, Item>();
        public ImmutableArray<EnemyPlacement> EnemyPlacements { get; init; } = [];
        public ImmutableDictionary<string, MusicSourceFile> Music { get; init; } = ImmutableDictionary<string, MusicSourceFile>.Empty;
        public ImmutableDictionary<int, CharacterReplacement> Characters { get; init; } = ImmutableDictionary<int, CharacterReplacement>.Empty;
        public ImmutableArray<string> EnemySkins { get; init; } = [];
        public ImmutableArray<NpcReplacement> Npcs { get; init; } = [];
        public ImmutableDictionary<string, string> Voices { get; init; } = ImmutableDictionary<string, string>.Empty;
        public int? Seed { get; init; }
        public RandomizerConfiguration? Configuration { get; init; }

        public T? GetGeneralValue<T>(string name, T? defaultValue = default)
        {
            if (General.TryGetValue(name, out var value) && value is T typedValue)
            {
                return typedValue;
            }
            return defaultValue;
        }

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
    }

    internal class RandomizerConfigurationJsonConverter2 : RandomizerConfigurationJsonConverter
    {
        public override RandomizerConfiguration? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            var d = JsonSerializer.Deserialize(ref reader, typeof(Dictionary<string, object>)) as Dictionary<string, object>;
            return RandomizerConfiguration.FromDictionary(d!);
        }
    }

    public readonly struct DoorTargetLock
    {
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public DoorTarget? Target { get; init; }
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public DoorLock? Lock { get; init; }
    }

    public readonly struct DoorTarget
    {
        public RdtId Room { get; init; }
        public int X { get; init; }
        public int Y { get; init; }
        public int Z { get; init; }
        public int D { get; init; }
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public int? Cut { get; init; }
    }
}
