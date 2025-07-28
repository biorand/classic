using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

namespace IntelOrca.Biohazard.BioRand
{
    internal class ModLogBuilder
    {
        private readonly Map _map;
        private readonly ClassicMod _mod;
        private readonly string _playerName;

        public ModLogBuilder(Map map, ClassicMod mod, string playerName)
        {
            _map = map;
            _mod = mod;
            _playerName = playerName;
        }

        public string Build()
        {
            var mdb = new MarkdownBuilder();

            mdb.Heading(1, "Inventory");
            DumpInventory(_playerName, _mod.Inventory[0]);

            mdb.Heading(1, "Items");

            var placedItems = _mod.Items
                .Select(x => new PlacedItem(x.Key, x.Value, _map.GetItem(x.Value.Type)))
                .GroupBy(x => x.Group);

            DumpGroup("key", "Keys");
            DumpGroup("weapon", "Weapons");
            DumpGroup("ammo", "Ammo");
            DumpGroup("health", "Health");
            DumpGroup("ink", "Ink");

            mdb.Heading(1, "Enemies");
            mdb.Heading(3, "Summary");
            mdb.Table("Enemy", "Total", "Rooms", "Avg. per rdt");
            var typeToEntryMap = _map.Enemies
                .SelectMany(x => x.Entries)
                .SelectMany(x => x.Id.Select(y => (x, y)))
                .ToDictionary(x => x.Item2, x => x.Item1);
            foreach (var g in _mod.EnemyPlacements.GroupBy(x => typeToEntryMap[x.Type]).OrderByDescending(x => x.Count()))
            {
                var enemyName = g.Key.Name;
                var total = g.Count();
                var rooms = g.GroupBy(x => x.RdtId).Count();
                var avg = ((double)total / rooms).ToString("0.0");
                mdb.TableRow(enemyName, total, rooms, avg);
            }

            mdb.Heading(3, "List");
            mdb.Table("Global ID", "RDT", "Room", "ID", "Enemy");
            foreach (var enemy in _mod.EnemyPlacements.OrderBy(x => x.RdtId).ThenBy(x => x.Id))
            {
                var entry = _map.Enemies.SelectMany(x => x.Entries).FirstOrDefault(x => x.Id.Contains(enemy.Type));
                var enemyName = entry?.Name ?? $"{enemy.Type}";
                var roomName = _map.GetRoomsContaining(enemy.RdtId).FirstOrDefault()?.Name ?? "";
                mdb.TableRow(enemy.GlobalId, enemy.RdtId, roomName, enemy.Id, enemyName);
            }

            mdb.Heading(1, "Doors");
            mdb.Table("RDT", "ID", "ROOM", "DOOR", "TARGET", "LOCK", "REQUIRES");
            foreach (var r in _map.Rooms)
            {
                foreach (var d in r.Value.Doors ?? [])
                {
                    var rdt = r.Value.Rdts.FirstOrDefault();
                    var requires = string.Join(", ", (d.Requires2 ?? []).Select(GetRequiresString));
                    if (requires == "")
                    {
                        if (d.Kind == DoorKinds.Locked)
                        {
                            requires = "(locked)";
                        }
                        else if (d.Kind == DoorKinds.Unlock)
                        {
                            requires = "(unlock)";
                        }
                    }
                    mdb.TableRow(rdt, (object?)d.Id ?? "", r.Value.Name ?? "", d.Name ?? "", d.Target ?? "", d.LockId ?? 0, requires);
                }
            }

            mdb.Heading(1, "Map");

            var firstRoom = _map.Rooms.Values.FirstOrDefault(x => x.HasTag("begin"));
            var segmentRoots = new List<RoomTreeNode>();
            var root = GetRoomTree(null, firstRoom, [], segmentRoots);
            if (root != null)
                segmentRoots.Add(root);
            segmentRoots.Reverse();
            for (var i = 0; i < segmentRoots.Count; i++)
            {
                mdb.Heading(3, $"Segment {i + 1}");
                segmentRoots[i].Visit(0, (indent, node) => mdb.AppendLine($"{new string(' ', indent * 2)}{node.Icon} **{node.RdtId}** | {node.Name}  "));
                mdb.AppendLine();
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

                    var itemName = _map.GetItem(entry.Type)?.Name ?? $"{entry.Type}";
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
                    var (rdtId, room, location) = GetItemSlotName(_map, i.GlobalId);
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
                        var item = _map.GetItem(itemId);
                        return item?.Name ?? s;
                    }
                }
                return s;
            }
        }

        private RoomTreeNode? GetRoomTree(MapRoom? parent, MapRoom room, HashSet<MapRoom> visited, List<RoomTreeNode> segmentRoots)
        {
            if (!visited.Add(room))
                return new RoomTreeNode(room, [], isLoopback: true);

            var roomRdt = room.Rdts.FirstOrDefault();
            var children = ImmutableArray.CreateBuilder<RoomTreeNode>();
            foreach (var door in room.Doors ?? [])
            {
                MapRoom? targetRoom = null;
                var isUnlock = false;
                if (door.Id is int id)
                {
                    var doorRdt = new RdtItemId(roomRdt, (byte)id);
                    if (_mod.Doors.TryGetValue(doorRdt, out var dtl))
                    {
                        if (dtl.Lock?.Type == 255)
                            continue;
                        if (dtl.Lock?.Type == 254)
                            isUnlock = true;

                        if (dtl.Target != null)
                            targetRoom = _map.GetRoomsContaining(dtl.Target.Value.Room).FirstOrDefault();
                    }
                }

                // Fallback
                if (targetRoom == null)
                {
                    if (door.Kind == "locked")
                        continue;
                    if (door.Kind == "unlocked")
                        isUnlock = true;

                    targetRoom = _map.GetRoom(door.TargetRoom ?? "");
                }

                if (targetRoom == null || targetRoom == room || targetRoom == parent)
                    continue;

                var node = isUnlock
                    ? new RoomTreeNode(targetRoom, [], isLoopback: true, isUnlock: true)
                    : GetRoomTree(room, targetRoom, visited, segmentRoots);
                if (node == null)
                    continue;

                if (door.HasTag(MapTags.SegmentEnd))
                    segmentRoots.Add(node);
                else
                    children.Add(node);
            }
            children.Sort();
            return new RoomTreeNode(room, children.ToImmutable(), isLoopback: false);
        }

        private static (RdtId rdtId, string room, string location) GetItemSlotName(Map map, int globalId)
        {
            foreach (var kvp in map.Rooms)
            {
                var room = kvp.Value;
                if (room.Items == null)
                    continue;

                var rdt = room.Rdts.FirstOrDefault();
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

        private class RoomTreeNode(MapRoom room, ImmutableArray<RoomTreeNode> children, bool isLoopback = false, bool isUnlock = false) : IComparable<RoomTreeNode>
        {
            public MapRoom Room => room;
            public ImmutableArray<RoomTreeNode> Children => children;

            public RdtId? RdtId => room.Rdts.FirstOrDefault();
            public string Name => room.Name ?? "";
            public bool IsLoopback => isLoopback;
            public bool IsSegmentEnd => room.Doors?.Any(x => x.HasTag(MapTags.SegmentEnd)) == true;
            public string Icon => true switch
            {
                _ when isUnlock => "🔓",
                _ when isLoopback => "↩️",
                _ when room.HasTag(MapTags.Begin) => "🏠",
                _ when room.HasTag(MapTags.End) => "🏁",
                _ when room.HasTag(MapTags.Safe) => "🛏️",
                _ when room.HasTag(MapTags.Box) => "🧳",
                _ when room.HasTag(MapTags.Save) => "💾",
                _ when IsSegmentEnd => "🎯",
                _ => "🚪"
            };

            public int MaxDepth => 1 + (Children.IsEmpty ? 0 : Children.Max(x => x.MaxDepth));

            public int CompareTo(RoomTreeNode other)
            {
                if (isLoopback && !other.IsLoopback)
                    return 1;
                if (!isLoopback && other.IsLoopback)
                    return -1;
                return MaxDepth - other.MaxDepth;
            }

            public void Visit(int level, Action<int, RoomTreeNode> cb)
            {
                cb(level, this);
                foreach (var child in Children)
                {
                    child.Visit(level + 1, cb);
                }
            }
        }
    }
}
