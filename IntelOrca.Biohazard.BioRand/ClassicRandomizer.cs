using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using IntelOrca.Biohazard.BioRand.Routing;

namespace IntelOrca.Biohazard.BioRand
{
    public class ClassicRandomizer : IRandomizer
    {
        public RandomizerConfigurationDefinition ConfigurationDefinition => throw new System.NotImplementedException();
        public RandomizerConfiguration DefaultConfiguration => throw new System.NotImplementedException();

        public string BuildVersion => throw new System.NotImplementedException();

        public RandomizerOutput Randomize(RandomizerInput input)
        {
            var dataManager = new DataManager(new[] {
                @"M:\git\biorand-classic\IntelOrca.Biohazard.BioRand\data"
            });
            var map = dataManager.GetJson<Map>(BioVersion.Biohazard1, "rdt.json")
                .For(new MapFilter(false, 0, 0));
            var keyRandomizer = new KeyRandomizer();
            keyRandomizer.RandomiseItems(input.Seed, map);
            return new RandomizerOutput(
                ImmutableArray<RandomizerOutputAsset>.Empty,
                "",
                new Dictionary<string, string>());
        }
    }

    public class KeyRandomizer
    {
        public void RandomiseItems(int seed, Map map)
        {
            var graphBuilder = new GraphBuilder();
            var routingNodes = new Dictionary<string, Node>();
            var itemIdToKey = new Dictionary<int, Key>();
            var itemNodeToGlobalId = new Dictionary<Node, int>();
            var globalIdToName = new Dictionary<int, string>();
            if (map.Keys != null)
            {
                foreach (var k in map.Keys)
                {
                    var itemId = k.Key;
                    var label = k.Value.Name;
                    var kind = (KeyKind)Enum.Parse(typeof(KeyKind), k.Value.Kind, true);
                    itemIdToKey[itemId] = graphBuilder.Key(label, 1, kind);
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
                    routingNodes[roomKey] = graphBuilder.Room(label);
                }
                graphBuilder.Door(startNode, routingNodes[beginEnd.Start ?? ""]);

                foreach (var kvp in map.Rooms)
                {
                    var roomKey = kvp.Key;
                    var room = kvp.Value;
                    var source = routingNodes[roomKey];
                    if (room.Doors != null)
                    {
                        foreach (var edge in room.Doors)
                        {
                            if (edge.Target == null)
                                return;

                            var targetKeyId = edge.Target.Split(':');
                            var target = routingNodes[targetKeyId[0]];
                            graphBuilder.Door(source, target, GetRequirements(edge));
                        }
                    }
                    if (room.Items != null)
                    {
                        foreach (var item in room.Items)
                        {
                            var label = item.Name != null ? $"{roomKey}|{room.Name}/{item.Name}" : $"{roomKey}";
                            var itemNode = graphBuilder.Item(label, 1, source, GetRequirements(item));
                            if (item.GlobalId is short globalId)
                            {
                                itemNodeToGlobalId[itemNode] = globalId;
                                globalIdToName[globalId] = label;
                            }
                        }
                    }
                }
            }

            var graph = graphBuilder.ToGraph();
            var route = graph.GenerateRoute(seed, new RouteFinderOptions());
            var itemToKey = itemNodeToGlobalId
                .Select(kvp => (kvp.Value, route.GetItemContents(kvp.Key)))
                .Where(x => x.Item2 != null)
                .ToDictionary(x => x.Item1, x => x.Item2);
            var ggg = itemToKey.ToDictionary(x => globalIdToName[x.Key], x => x.Value);
            var s = route.Log;

            Requirement[] GetRequirements(MapEdge e)
            {
                return e.Requirements.Select(r =>
                    r.Kind switch
                    {
                        MapRequirementKind.Item => new Requirement(itemIdToKey[int.Parse(r.Value)]),
                        MapRequirementKind.Room => new Requirement(routingNodes[r.Value]),
                        _ => throw new NotImplementedException()
                    }).ToArray();
            }
        }
    }
}
