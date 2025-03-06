using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace IntelOrca.Biohazard.BioRand
{
    public class Map
    {
        public MapStartEnd[]? BeginEndRooms { get; set; }
        public Dictionary<int, MapItemDefinition> Items { get; set; } = [];
        public MapEnemyGroup[] Enemies { get; set; } = [];
        public Dictionary<string, MapRoom> Rooms { get; set; } = [];

        internal MapItemDefinition? GetItem(int type)
        {
            Items.TryGetValue(type, out var result);
            return result;
        }

        internal MapRoom? GetRoom(RdtId id)
        {
            if (Rooms == null)
                return null;
            Rooms.TryGetValue(id.ToString(), out var value);
            return value;
        }

        internal static int[] ParseNopArray(JsonElement[]? nopArray, RandomizedRdt rdt)
        {
            var nop = new List<int>();
            if (nopArray != null)
            {
                foreach (var entry in nopArray)
                {
                    if (entry.ValueKind == System.Text.Json.JsonValueKind.String)
                    {
                        var s = entry.GetString()!;
                        if (s.Contains('-'))
                        {
                            var parts = s.Split('-');
                            var lower = ParseLiteral(parts[0]);
                            var upper = ParseLiteral(parts[1]);
                            foreach (var op in rdt.Opcodes)
                            {
                                if (op.Offset >= lower && op.Offset <= upper)
                                {
                                    nop.Add(op.Offset);
                                }
                            }
                        }
                        else
                        {
                            nop.Add(ParseLiteral(s));
                        }
                    }
                    else
                    {
                        nop.Add(entry.GetInt32());
                    }
                }
            }
            return nop.ToArray();
        }

        public static int ParseLiteral(string s)
        {
            if (s.StartsWith("0x"))
            {
                return int.Parse(s.Substring(2), System.Globalization.NumberStyles.HexNumber);
            }
            return int.Parse(s);
        }

        public Map For(MapFilter filter)
        {
            return new Map()
            {
                BeginEndRooms = BeginEndRooms
                    .Where(x => x.IsIncludedInFilter(filter))
                    .ToArray(),
                Items = Items,
                Enemies = Enemies,
                Rooms = Rooms
                    .Where(x => x.Value.IsIncludedInFilter(filter))
                    .ToDictionary(x => x.Key, x => x.Value.For(filter))
            };
        }
    }

    public class MapStartEnd : MapFilterable
    {
        public string? Start { get; set; }
        public string? End { get; set; }
    }

    public class MapItemDefinition
    {
        public string Name { get; set; } = "";
        public string Kind { get; set; } = "";
        public int[]? Ammo { get; set; }
        public int Max { get; set; }
        public int Group { get; set; }
        public int? Amount { get; set; }
        public bool Discard { get; set; }
    }

    public class MapEnemyGroup
    {
        public string Name { get; set; } = "";
        public string Background { get; set; } = "";
        public string Foreground { get; set; } = "";
        public MapEnemyGroupEntry[] Entries { get; set; } = [];
    }

    public class MapEnemyGroupEntry
    {
        public string Key { get; set; } = "";
        public string Name { get; set; } = "";
        public int[] Id { get; set; } = [];
    }

    public class MapRoom : MapFilterable
    {
        public string? Name { get; set; }
        public string? LinkedRoom { get; set; }
        public string[]? Tags { get; set; }
        public RdtId[]? Rdts { get; set; }
        public int[]? Requires { get; set; }
        public MapRoomDoor[]? Doors { get; set; }
        public MapRoomItem[]? Items { get; set; }
        public MapEdge[]? Flags { get; set; }
        public MapRoomEnemies[]? Enemies { get; set; }
        public MapRoomNpcs[]? Npcs { get; set; }
        public new DoorRandoSpec[]? DoorRando { get; set; }

        public IEnumerable<MapEdge> AllEdges
        {
            get
            {
                if (Doors != null)
                {
                    foreach (var door in Doors)
                    {
                        yield return door;
                    }
                }
                if (Flags != null)
                {
                    foreach (var flag in Flags)
                    {
                        yield return flag;
                    }
                }
                if (Items != null)
                {
                    foreach (var item in Items)
                    {
                        yield return item;
                    }
                }
            }
        }

        public MapRoom For(MapFilter filter)
        {
            return new MapRoom()
            {
                Name = Name,
                Tags = Tags,
                Rdts = Rdts,
                Doors = Doors?.Where(x => x.IsIncludedInFilter(filter)).ToArray() ?? [],
                Items = Items?.Where(x => x.IsIncludedInFilter(filter)).ToArray() ?? [],
                Flags = Flags?.Where(x => x.IsIncludedInFilter(filter)).ToArray() ?? [],
                DoorRando = DoorRando,
                Enemies = Enemies,
                Npcs = Npcs
            };
        }

        public bool HasTag(string tag)
        {
            if (Tags == null)
                return false;
            return Tags.Contains(tag);
        }
    }

    public class MapRoomDoor : MapEdge
    {
        public string? Condition { get; set; }
        public bool Create { get; set; }
        public int Texture { get; set; }
        public int? Special { get; set; }
        public int? Id { get; set; }
        public int? AltId { get; set; }
        public JsonElement[]? Offsets { get; set; }
        public byte? Cut { get; set; }
        public MapRoomDoorEntrance? Entrance { get; set; }
        public int? EntranceId { get; set; }
        public string? Target { get; set; }
        public bool? Randomize { get; set; }
        public string? Lock { get; set; }
        public byte? LockId { get; set; }
        public bool NoReturn { get; set; }
        public bool NoUnlock { get; set; }
        public bool IsBridgeEdge { get; set; }
        public int[]? Requires { get; set; }
        public string[]? RequiresRoom { get; set; }
        public string? Kind { get; set; }
    }

    public class MapRoomDoorEntrance
    {
        public int X { get; set; }
        public int Y { get; set; }
        public int Z { get; set; }
        public int D { get; set; }
        public int Floor { get; set; }
        public int Cut { get; set; }
    }

    public class MapRoomItem : MapEdge
    {
        public JsonElement[]? Nop { get; set; }
        public JsonElement[]? Offsets { get; set; }
        public byte Id { get; set; }
        public byte? ItemId { get; set; }
        public short? GlobalId { get; set; }
        public byte? Type { get; set; }
        public byte? Amount { get; set; }
        public string? Link { get; set; }
        public string? Priority { get; set; }
        public int[]? Requires { get; set; }
        public string[]? RequiresRoom { get; set; }
        public bool? AllowDocuments { get; set; }

        public bool? Document { get; set; }
        public bool? Hidden { get; set; }
        public bool? Optional { get; set; }
        public int Group { get; set; }
        public string[]? TypeOffsets { get; set; }
    }

    public class MapRoomEnemies : MapFilterable
    {
        public JsonElement[]? Nop { get; set; }
        public int[]? ExcludeOffsets { get; set; }
        public int[]? ExcludeTypes { get; set; }
        public int[]? IncludeTypes { get; set; }
        public bool KeepState { get; set; }
        public bool KeepAi { get; set; }
        public bool KeepPositions { get; set; }
        public bool IgnoreRatio { get; set; }
        public short? Y { get; set; }
        public int? MaxDifficulty { get; set; }
        public bool? Restricted { get; set; }
        public string? Condition { get; set; }

        // Filters
        public bool? RandomPlacements { get; set; }
    }

    public class MapRoomNpcs
    {
        public int[]? IncludeOffsets { get; set; }
        public int[]? IncludeTypes { get; set; }
        public int[]? ExcludeTypes { get; set; }
        public int? Player { get; set; }
        public int? Scenario { get; set; }
        public bool? DoorRando { get; set; }
        public int Cutscene { get; set; }
        public string? PlayerActor { get; set; }
        public bool? EmrScale { get; set; }
        public string? Use { get; set; }
    }

    public class DoorRandoSpec
    {
        public string? Category { get; set; }
        public JsonElement[]? Nop { get; set; }
        public bool Cutscene { get; set; }
        public int? Player { get; set; }
        public int? Scenario { get; set; }
    }

    public class MapEdge : MapFilterable
    {
        public string? Name { get; set; }
        public string[]? Requires2 { get; set; }
        public MapRequirement[] Requirements => Requires2?.Select(MapRequirement.Parse).ToArray() ?? new MapRequirement[0];
    }

    public readonly struct MapRequirement
    {
        public MapRequirementKind Kind { get; }
        public string Value { get; }

        public MapRequirement(MapRequirementKind kind, string value)
        {
            Kind = kind;
            Value = value;
        }

        public static MapRequirement Parse(string input)
        {
            if (TryParse(input, out var result))
                return result;
            throw new ArgumentException("Invalid requirement syntax", nameof(input));
        }

        public static bool TryParse(string input, out MapRequirement result)
        {
            var m = Regex.Match(input, @"^([a-z]+)\(([^()]*)\)$");
            if (m.Success)
            {
                if (Enum.TryParse<MapRequirementKind>(m.Groups[1].Value, true, out var kind))
                {
                    result = new MapRequirement(kind, m.Groups[2].Value);
                    return true;
                }
            }
            result = default;
            return false;
        }
    }

    public enum MapRequirementKind
    {
        None,
        Flag,
        Item,
        Room,
    }

    public abstract class MapFilterable
    {
        public bool? DoorRando { get; set; }
        public int? Player { get; set; }
        public int? Scenario { get; set; }

        public bool IsIncludedInFilter(MapFilter filter)
        {
            if (DoorRando != null && DoorRando != filter.DoorRando)
                return false;
            if (Player != null && Player != filter.Player)
                return false;
            if (Scenario != null && Scenario != filter.Scenario)
                return false;
            return true;
        }
    }

    public readonly struct MapFilter
    {
        public bool DoorRando { get; }
        public byte Player { get; }
        public byte Scenario { get; }

        public MapFilter(bool doorRando, byte player, byte scenario)
        {
            DoorRando = doorRando;
            Player = player;
            Scenario = scenario;
        }
    }

    internal class RdtIdConverter : JsonConverter<RdtId>
    {
        public override RdtId Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            return RdtId.Parse(reader.GetString() ?? "");
        }

        public override void Write(Utf8JsonWriter writer, RdtId value, JsonSerializerOptions options)
        {
            writer.WriteStringValue(value.ToString());
        }
    }
}
