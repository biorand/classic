using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using IntelOrca.Biohazard.BioRand.Routing;

namespace IntelOrca.Biohazard.BioRand
{
    internal class KeyRandomizer
    {
        public void Randomize(IClassicRandomizerGeneratedVariation context)
        {
            var map = context.Variation.Map;
            var rng = context.GetRng("key");
            var seed = rng.Next(0, int.MaxValue);
            var modBuilder = context.ModBuilder;

            var includeDocuments =
                context.Configuration.GetValueOrDefault("items/documents", false) &&
                context.Configuration.GetValueOrDefault("items/documents/keys", false);
            var includeHiddenItems = context.Configuration.GetValueOrDefault("items/hidden/keys", false);

            var graphBuilder = new GraphBuilder();
            var roomNodes = new Dictionary<string, Node>();
            var flagNodes = new Dictionary<string, Node>();
            var itemIdToKey = new Dictionary<int, Key>();
            var keyToItemId = new Dictionary<Key, int>();
            var itemNodeToGlobalId = new Dictionary<Node, int>();
            var globalIdToName = new Dictionary<int, string>();
            if (map.Items != null)
            {
                foreach (var k in map.Items)
                {
                    var itemId = k.Key;
                    var label = k.Value.Name;
                    if (k.Value.Kind is not string kind)
                        continue;

                    var kindParts = kind.Split('/');
                    if (kindParts.Length != 2 || kindParts[0] != "key")
                        continue;

                    var keyKind = (KeyKind)Enum.Parse(typeof(KeyKind), kindParts[1], true);
                    var group = k.Value.Group;
                    var keyNode = graphBuilder.Key(label, group, keyKind);
                    itemIdToKey[itemId] = keyNode;
                    keyToItemId[keyNode] = itemId;
                }
            }

            var beginEnd = map.BeginEndRooms.FirstOrDefault();
            if (beginEnd != null && map.Rooms != null)
            {
                var startNode = graphBuilder.Room("START");
                foreach (var kvp in map.Rooms)
                {
                    var roomKey = kvp.Key;
                    var room = kvp.Value;
                    var label = room.Name != null ? $"{roomKey}|{room.Name}" : $"{roomKey}";
                    roomNodes[roomKey] = graphBuilder.Room(label);
                }
                graphBuilder.Door(startNode, roomNodes[beginEnd.Start ?? ""]);

                foreach (var kvp in map.Rooms)
                {
                    var roomKey = kvp.Key;
                    var room = kvp.Value;
                    var source = roomNodes[roomKey];
                    if (room.Doors != null)
                    {
                        foreach (var edge in room.Doors)
                        {
                            if (edge.IgnoreInGraph || edge.Target == null)
                                continue;
                            if (edge.Kind == DoorKinds.Blocked || edge.Kind == DoorKinds.Locked)
                                continue;

                            var targetKeyId = edge.Target.Split(':');
                            var target = roomNodes[targetKeyId[0]];
                            var requirements = GetRequirements(edge);

                            // The graph library hates it when you have unblock[key] <--> [key]
                            var oppositeEdge = map.GetOtherSide(edge);
                            if (oppositeEdge?.Kind == DoorKinds.Unblock)
                            {
                                requirements = [];
                            }

                            _ = edge.Kind switch
                            {
                                DoorKinds.OneWay => graphBuilder.OneWay(source, target, requirements),
                                DoorKinds.NoReturn => graphBuilder.NoReturn(source, target, requirements),
                                DoorKinds.Unblock => graphBuilder.BlockedDoor(source, target, requirements),
                                DoorKinds.Unlock => graphBuilder.BlockedDoor(source, target, requirements),
                                _ => graphBuilder.Door(source, target, requirements)
                            };
                        }
                    }
                    if (room.Items != null)
                    {
                        foreach (var item in room.Items)
                        {
                            if (item.Optional == true)
                                continue;

                            if (item.Document == true && !includeDocuments)
                                continue;

                            if (item.Hidden == true && !includeHiddenItems)
                                continue;

                            var label = item.Name != null ? $"{roomKey}|{room.Name}/{item.Name}" : $"{roomKey}";
                            var group = item.Group;
                            var itemNode = graphBuilder.Item(label, group, source, GetRequirements(item));
                            if (item.GlobalId is short globalId)
                            {
                                itemNodeToGlobalId[itemNode] = globalId;
                                globalIdToName[globalId] = label;
                            }
                        }
                    }
                    if (room.Flags != null)
                    {
                        foreach (var item in room.Flags)
                        {
                            var flagKey = item.Name ?? throw new Exception("Flag has no name");
                            var flagNode = GetOrCreateFlag(flagKey);
                            graphBuilder.Door(source, flagNode, GetRequirements(item));
                        }
                    }
                }
            }

            var graph = graphBuilder.ToGraph();

            var deadendLimit = 1000;
            var route = graph.GenerateRoute(seed, new RouteFinderOptions()
            {
                DebugDeadendCallback = (o) =>
                {
                    deadendLimit--;
                    if (deadendLimit <= 0)
                    {
                        throw new RandomizerUserException("Failed to find route, possibly lack of items to place keys.");
                    }
                }
            });

            var globalIdToKey = itemNodeToGlobalId
                .Select(kvp => (kvp.Value, route.GetItemContents(kvp.Key)))
                .Where(x => x.Item2 != null)
                .ToDictionary(x => x.Item1, x => x.Item2!.Value);
            var locationNameToKey = globalIdToKey.ToDictionary(x => globalIdToName[x.Key], x => x.Value);
            var log = route.Log;

            var segmentTree = GetSegmentTree(map);
            foreach (var kvp in globalIdToKey)
            {
                var globalId = kvp.Key;
                var key = kvp.Value;

                var itemType = (byte)keyToItemId[key];
                var itemDefinition = map.GetItem(itemType);
                var amount = itemDefinition?.Amount ?? 1;
                if (itemDefinition?.Discard == true)
                {
                    var requireS = $"item({itemType})";
                    var segment = segmentTree.FindSegmentContaining(globalId);
                    if (segment == null)
                        continue;

                    var allEdges = segment.DescendantRooms
                        .SelectMany(x => x.AllEdges)
                        .Where(x => x.Requires2?.Contains(requireS) == true)
                        .ToList();
                    var lockIds = new HashSet<int>();
                    for (var i = 0; i < allEdges.Count; i++)
                    {
                        var edge = allEdges[i];
                        if (edge is MapRoomDoor door)
                        {
                            if (door.LockId is byte lockId)
                            {
                                if (!lockIds.Add(lockId))
                                {
                                    allEdges.RemoveAt(i);
                                    i--;
                                }
                            }
                        }
                    }
                    amount = allEdges.Count;
                }
                modBuilder.SetItem(kvp.Key, new Item(itemType, (ushort)amount));
            }

            Requirement[] GetRequirements(MapEdge e)
            {
                return e.Requirements
                    .Where(ShouldUseRequirement)
                    .Select(r =>
                        r.Kind switch
                        {
                            MapRequirementKind.Flag => new Requirement(GetOrCreateFlag(r.Value)),
                            MapRequirementKind.Item => new Requirement(itemIdToKey[int.Parse(r.Value)]),
                            MapRequirementKind.Room => new Requirement(roomNodes[r.Value]),
                            _ => throw new NotImplementedException()
                        })
                    .ToArray();
            }

            bool ShouldUseRequirement(MapRequirement r)
            {
                if (r.Kind == MapRequirementKind.Item)
                {
                    var itemId = int.Parse(r.Value);
                    var item = map.GetItem(itemId);
                    if (item != null)
                    {
                        return !item.Implicit;
                    }
                }
                return true;
            }

            Node GetOrCreateFlag(string name)
            {
                if (!flagNodes.TryGetValue(name, out var result))
                {
                    result = graphBuilder.Room(name);
                    flagNodes[name] = result;
                }
                return result;
            }
        }

        private static Segment GetSegmentTree(Map map)
        {
            var firstRoom = map.GetRoom(map.BeginEndRooms?.FirstOrDefault()?.Start ?? "");
            if (firstRoom == null)
                return new Segment([], []);

            var visited = new HashSet<MapRoom>();
            return VisitSegment(firstRoom);

            Segment VisitSegment(MapRoom room)
            {
                var rooms = ImmutableHashSet.CreateBuilder<MapRoom>();
                var children = ImmutableArray.CreateBuilder<Segment>();
                VisitRoom(room, rooms, children);
                return new Segment(rooms.ToImmutable(), children.ToImmutable());
            }

            void VisitRoom(MapRoom room, ImmutableHashSet<MapRoom>.Builder rooms, ImmutableArray<Segment>.Builder children)
            {
                if (visited.Add(room))
                {
                    rooms.Add(room);
                    foreach (var d in room.Doors ?? [])
                    {
                        if (d.Kind == DoorKinds.Blocked)
                            continue;

                        var targetRoom = map.GetRoom(d.TargetRoom ?? "");
                        if (targetRoom == null)
                            continue;

                        if (d.Kind == DoorKinds.NoReturn)
                            children.Add(VisitSegment(targetRoom));
                        else
                            VisitRoom(targetRoom, rooms, children);
                    }
                }
            }
        }

        private class Segment(ImmutableHashSet<MapRoom> rooms, ImmutableArray<Segment> children)
        {
            public ImmutableHashSet<MapRoom> Rooms => rooms;
            public ImmutableArray<Segment> Children => children;

            public Segment? FindSegmentContaining(int globalId)
            {
                if (rooms.SelectMany(x => x.Items ?? []).Any(x => x.GlobalId == globalId))
                {
                    return this;
                }
                return children
                    .Select(x => x.FindSegmentContaining(globalId))
                    .FirstOrDefault(x => x != null);
            }

            public IEnumerable<MapRoom> DescendantRooms
            {
                get
                {
                    foreach (var r in rooms)
                    {
                        yield return r;
                    }
                    foreach (var segment in children)
                    {
                        foreach (var r in segment.DescendantRooms)
                        {
                            yield return r;
                        }
                    }
                }
            }
        }
    }
}
