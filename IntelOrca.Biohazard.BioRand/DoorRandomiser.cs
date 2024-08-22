using System;
using System.Collections.Generic;
using System.Linq;
using IntelOrca.Biohazard.Script.Opcodes;

namespace IntelOrca.Biohazard.BioRand
{
    internal class DoorRandomiser
    {
        private static bool g_debugLogging = false;

        private readonly RandoLogger _logger;
        private RandoConfig _config;
        private GameData _gameData;
        private Map _map = new Map();
        private Dictionary<RdtId, PlayNode> _nodeMap = new Dictionary<RdtId, PlayNode>();
        private List<PlayNode> _allNodes = new List<PlayNode>();
        private Rng _rng;
        private IDoorHelper _doorHelper;
        private IItemHelper _itemHelper;

        private int _keyItemSpotsLeft;
        private readonly HashSet<ushort> _keyItemRequiredSet = new HashSet<ushort>();
        private int _numUnconnectedEdges;
        private int _numKeyEdges;
        private int _numUnlockedEdges;
        private PlayEdge? _keyRichEdge;
        private int _keyRichEdgeScore;
        private bool _boxRoomReached;
        private Queue<byte> _lockIds = new Queue<byte>();
        private List<PlayNode> _nodesLeft = new List<PlayNode>();

        private static readonly ConnectConstraint[] g_strictConstraints = new ConnectConstraint[]
        {
            new LockConstraint(),
            new FixedLinkConstraint(),
            new LoopbackConstraint(),
            new LeafConstraint(),
            new KeyConstraint(),
            new BoxConstraint()
        };

        private static readonly ConnectConstraint[] g_looseConstraints = new ConnectConstraint[]
        {
            new LockConstraint(),
            new FixedLinkConstraint(),
            new LoopbackConstraint(),
            new LeafConstraint(),
            new KeyConstraint()
        };

        private static readonly ConnectConstraint[] g_endConstraints = new ConnectConstraint[]
        {
            new LockConstraint(),
            new FixedLinkConstraint()
        };

        public DoorRandomiser(RandoLogger logger, RandoConfig config, GameData gameData, Map map, Rng random, IDoorHelper doorHelper, IItemHelper itemHelper)
        {
            _logger = logger;
            _config = config;
            _gameData = gameData;
            _map = map;
            _rng = random;
            _doorHelper = doorHelper;
            _itemHelper = itemHelper;
        }

        public PlayGraph CreateOriginalGraph()
        {
            _logger.WriteHeading("Creating Original Room Graph:");
            _doorHelper.Begin(_config, _gameData, _map);

            // Create all nodes
            foreach (var kvp in _map.Rooms!)
            {
                GetOrCreateNode(RdtId.Parse(kvp.Key));
            }

            var graph = new PlayGraph();
            var beginEnd = GetBeginEnd();
            graph.Start = GetOrCreateNode(RdtId.Parse(beginEnd.Start!));
            graph.End = GetOrCreateNode(RdtId.Parse(beginEnd.End!));

            var queue = new Queue<PlayNode>();
            queue.Enqueue(graph.Start);
            graph.Start.Visited = true;
            while (!graph.End.Visited && queue.Count != 0)
            {
                var node = queue.Dequeue();
                _logger.WriteLine($"Visited {node}");
                if (g_debugLogging)
                {
                    foreach (var edge in node.Edges)
                    {
                        var edgeNode = edge.Node;
                        if (edgeNode != null)
                        {
                            if (!edgeNode.Visited)
                            {
                                edgeNode.Visited = true;
                                edgeNode.Depth = node.Depth + 1;
                                queue.Enqueue(edgeNode);
                            }
                        }
                    }
                }
            }

            _doorHelper.End(_config, _gameData, _map);
            return graph;
        }

        public PlayGraph CreateRandomGraph()
        {
            _logger.WriteHeading("Creating Random Room Graph:");
            _doorHelper.Begin(_config, _gameData, _map);
            _allNodes = CreateNodes();
            _lockIds = Enumerable
                .Range(0, 125)
                .Select(x => (byte)x)
                .Except(_doorHelper.GetReservedLockIds())
                .Select(x => (byte)(x + 128))
                .ToQueue();

            // Create start and end
            var graph = new PlayGraph();
            var beginEnd = GetBeginEnd();
            graph.Start = GetOrCreateNode(RdtId.Parse(beginEnd.Start!));
            graph.End = GetOrCreateNode(RdtId.Parse(beginEnd.End!));

            var numAreas = _config.AreaCount + 1;

            _nodesLeft.AddRange(_allNodes.Where(x => x.Category != DoorRandoCategory.Exclude));

            var beginNode = graph.Start;
            beginNode.Visited = true;
            var pool = new List<PlayNode>() { beginNode };
            AddStickyNodeGroup(beginNode, pool);

            // Remove the begin (+ required nodes)
            _nodesLeft.Remove(graph.End);
            foreach (var node in pool)
            {
                _nodesLeft.Remove(node);
            }

            if (numAreas == 1)
            {
                _nodesLeft.RemoveAll(x => x.Category == DoorRandoCategory.Segment);
                var areaSuperNodes = GetAreaSuperNodes(numAreas);
                pool.AddRange(areaSuperNodes[0]);
                _logger.WriteLine($"Creating single segment from {beginNode} to {graph.End}:");
                CreateArea(beginNode, graph.End, pool);
                beginNode = graph.End;
            }
            else
            {
                var bridgeSuperNodes = GetBridgeSuperNodes();
                var areaSuperNodes = GetAreaSuperNodes(numAreas);
                for (int i = 0; i < numAreas; i++)
                {
                    pool.AddRange(areaSuperNodes[i]);
                    var bridgeNode = i == numAreas - 1 ? graph.End : bridgeSuperNodes[i][0];
                    _logger.WriteLine($"Creating segment from {beginNode} to {bridgeNode}:");
                    CreateArea(beginNode, bridgeNode, pool);
                    beginNode = bridgeNode;
                    if (i < numAreas - 1)
                    {
                        pool.AddRange(bridgeSuperNodes[i]);
                    }
                }
            }
            FinishOffEndNodes(graph.End);

            FinalChecks(graph);
            _doorHelper.End(_config, _gameData, _map);
            UpdateLinkedRooms();
            return graph;
        }

        private MapStartEnd GetBeginEnd()
        {
            foreach (var startEnd in _map.BeginEndRooms!)
            {
                if (startEnd.Player != null && startEnd.Player != _config.Player)
                    continue;
                if (startEnd.Scenario != null && startEnd.Scenario != _config.Scenario)
                    continue;
                if (startEnd.DoorRando != null && startEnd.DoorRando != _config.RandomDoors)
                    continue;

                return startEnd;
            }
            throw new Exception("No begin / end room set.");
        }

        private List<PlayNode> CreateNodes()
        {
            var nodes = new List<PlayNode>();
            foreach (var kvp in _map.Rooms!)
            {
                var node = GetOrCreateNode(RdtId.Parse(kvp.Key));
                foreach (var edge in node.Edges)
                {
                    edge.Node = null;
                    edge.NoReturn = false;
                }
                nodes.Add(node);
            }
            return nodes;
        }

        private void UpdateLinkedRooms()
        {
            foreach (var node in _allNodes)
            {
                if (node.LinkedRdtId == null)
                    continue;

                var rdt = _gameData.GetRdt(node.RdtId)!;
                var linkedRdt = _gameData.GetRdt(node.LinkedRdtId.Value)!;

                var rdtDoors = rdt.Doors.ToArray();
                var linkedRdtDoors = linkedRdt.Doors.ToArray();
                for (int i = 0; i < linkedRdtDoors.Length; i++)
                {
                    var dst = linkedRdtDoors[i];
                    var src = rdtDoors.FirstOrDefault(x => x.Id == dst.Id);
                    if (src != null)
                    {
                        dst.Target = src.Target;
                        dst.NextX = src.NextX;
                        dst.NextY = src.NextY;
                        dst.NextZ = src.NextZ;
                        dst.NextD = src.NextD;
                        dst.LockId = src.LockId;
                        dst.LockType = src.LockType;
                    }
                    else
                    {
                        _logger.WriteLine($"Unable to synchronise door {dst.Id} from {node.RdtId} to {node.LinkedRdtId}");
                    }
                }
                _logger.WriteLine($"Synchronising doors from {node.RdtId} to {node.LinkedRdtId}");
            }
        }

        private void CreateArea(PlayNode begin, PlayNode end, List<PlayNode> pool)
        {
            _boxRoomReached = false;
            _keyItemSpotsLeft = 0;
            _keyItemRequiredSet.Clear();

            var nonLinearBridgeNode = end.Edges.Count > 2;
            if (nonLinearBridgeNode)
            {
                pool.Add(end);
            }

            PlayEdge[] unfinishedEdges;
            do
            {
                CalculateEdgeCounts(pool);
                unfinishedEdges = GetUnfinishedEdges(pool);

                if (g_debugLogging)
                    _logger.WriteLine($"        Edges left: {_numUnconnectedEdges} (key = {_numKeyEdges}, unlocked = {_numUnlockedEdges})");
            } while (ConnectUpRandomNode(unfinishedEdges, pool));
            if (!end.Visited && !ConnectUpNode(end, unfinishedEdges))
            {
                _logger.WriteLine($"    Failed to connect to end node {end.RdtId}");
                throw new Exception("Unable to connect end node");
            }

            // Lock other side of begin -> begin + 1 door
            foreach (var edge in begin.Edges)
            {
                if (edge.NoReturn && edge.Node != null)
                {
                    foreach (var oppositeEdge in edge.Node.Edges)
                    {
                        if (oppositeEdge.Node == begin)
                        {
                            oppositeEdge.Lock = LockKind.Always;
                        }
                    }
                }
            }

            // Set end -> end + 1 door to no return
            foreach (var edge in end.Edges)
            {
                if (edge.Node == null)
                {
                    if (nonLinearBridgeNode)
                    {
                        if (edge.IsBridgeEdge)
                        {
                            edge.NoReturn = true;
                        }
                        else if (edge.Randomize)
                        {
                            // This was an unconnected edge that isn't the 'bridge edge', so
                            // ensure it never gets connected.
                            edge.Lock = LockKind.Always;
                        }
                    }
                    else
                    {
                        edge.NoReturn = true;
                    }
                    edge.IsBridgeEdge = false;
                }
            }

            pool.RemoveAll(x => x.Visited);
        }

        private void FinalChecks(PlayGraph graph)
        {
            foreach (var node in _allNodes)
            {
                if (!node.Visited)
                    continue;

                // NOP out instructions
                var rdt = _gameData.GetRdt(node.RdtId)!;
                foreach (var offset in node.DoorRandoNop)
                {
                    rdt.Nop(offset);
                    if (g_debugLogging)
                        _logger.WriteLine($"{rdt.RdtId} (0x{offset:X2}) opcode removed");
                }

                // Lock any unconnected doors and loopback to itself to be extra safe
                foreach (var edge in node.Edges)
                {
                    if (edge.Node == null)
                    {
                        _logger.WriteLine($"{node.RdtId}:{edge.DoorId} -> null");

                        // Connect door back to itself
                        LockDoor(edge);

                        // Remove requirement of keys
                        edge.Requires = new byte[0];
                    }
                }
            }
            if (g_debugLogging)
            {
                foreach (var node in _allNodes)
                {
                    if (!node.Visited)
                    {
                        _logger.WriteLine($"{node.RdtId} not used");
                    }
                }
            }

            if (!graph.End!.Visited)
            {
                throw new Exception("End not reached");
            }
        }

        private List<PlayNode>[] GetBridgeSuperNodes()
        {
            var bridgeSuperNodes = new List<List<PlayNode>>();
            var bridgeNodes = _nodesLeft.Where(x =>
                x.Category == DoorRandoCategory.Bridge ||
                x.Category == DoorRandoCategory.Segment).Shuffle(_rng);
            foreach (var bridgeNode in bridgeNodes)
            {
                var bridgeSuperNode = new List<PlayNode>();
                AddStickyNodeGroup(bridgeNode, bridgeSuperNode);
                _nodesLeft.RemoveMany(bridgeSuperNode);
                bridgeSuperNodes.Add(bridgeSuperNode);
            }
            return bridgeSuperNodes.Shuffle(_rng).ToArray();
        }

        private PlayNode[][] GetAreaSuperNodes(int count)
        {
            var superNodes = Enumerable.Range(0, count).Select(x => new List<PlayNode>()).ToArray();
            var superNodeIndex = _rng.Next(0, count);

            var boxNodes = _nodesLeft
                .Where(x => x.Category == DoorRandoCategory.Box)
                .Shuffle(_rng)
                .ToList();
            if (_config.PrioritiseCutscenes)
            {
                boxNodes = boxNodes
                    .OrderBy(x => x.HasCutscene ? 0 : 1)
                    .ToList();
            }
            if (_config.AreaSize < 7)
            {
                var minNodes = _config.AreaCount + 1;
                var numNodes = minNodes + ((boxNodes.Count - minNodes) * _config.AreaSize / 7);
                boxNodes.RemoveRange(numNodes, boxNodes.Count - numNodes);
            }
            while (boxNodes.Count > 0)
            {
                var node = boxNodes[0];
                var superNode = superNodes[superNodeIndex];
                AddStickyNodeGroup(node, superNode);
                _nodesLeft.RemoveMany(superNode);
                boxNodes.RemoveMany(superNode);
                superNodeIndex = (superNodeIndex + 1) % superNodes.Length;
            }

            // Divide up nodes of same edge count equally
            _nodesLeft = _nodesLeft
                .Shuffle(_rng)
                .ToList();
            if (_config.PrioritiseCutscenes)
            {
                _nodesLeft = _nodesLeft
                    .OrderBy(x => x.HasCutscene ? 0 : 1)
                    .ToList();
            }
            if (_config.AreaSize < 7)
            {
                var numNodes = (_nodesLeft.Count * (_config.AreaSize + 1)) / 8;
                _nodesLeft.RemoveRange(numNodes, _nodesLeft.Count - numNodes);
            }
            foreach (var subg in _nodesLeft.GroupBy(x => x.Edges.Count))
            {
                var subgNodes = subg.Intersect(_nodesLeft).Shuffle(_rng).ToList();
                while (subgNodes.Count > 0)
                {
                    var node = subgNodes[0];
                    var superNode = superNodes[superNodeIndex];
                    AddStickyNodeGroup(node, superNode);
                    _nodesLeft.RemoveMany(superNode);
                    subgNodes.RemoveMany(superNode);
                    superNodeIndex = (superNodeIndex + 1) % superNodes.Length;
                }
            }

            return superNodes.Select(x => x.ToArray()).ToArray();
        }

        private void AddStickyNodeGroup(PlayNode node, List<PlayNode> list)
        {
            if (!list.Contains(node))
                list.Add(node);

            // Find all nodes this room requires
            foreach (var reqRoom in node.RequiresRoom)
            {
                if (!list.Contains(reqRoom))
                {
                    list.Add(reqRoom);
                    AddStickyNodeGroup(reqRoom, list);
                }
            }

            foreach (var edge in node.Edges)
            {
                if (!edge.Randomize)
                {
                    var stickyNode = GetOrCreateNode(edge.OriginalTargetRdt);
                    if (!list.Contains(stickyNode))
                    {
                        list.Add(stickyNode);
                        AddStickyNodeGroup(stickyNode, list);
                    }
                }
                foreach (var reqRoom in edge.RequiresRoom)
                {
                    if (!list.Contains(reqRoom))
                    {
                        list.Add(reqRoom);
                        AddStickyNodeGroup(reqRoom, list);
                    }
                }
            }

            // Find all other nodes that require this node
            foreach (var node2 in _nodesLeft)
            {
                foreach (var reqRoom in node2.RequiresRoom)
                {
                    if (reqRoom == node)
                    {
                        if (!list.Contains(node2))
                        {
                            list.Add(node2);
                            AddStickyNodeGroup(node2, list);
                        }
                    }
                }
                foreach (var edge in node2.Edges)
                {
                    foreach (var reqRoom in edge.RequiresRoom)
                    {
                        if (reqRoom == node)
                        {
                            if (!list.Contains(node2))
                            {
                                list.Add(node2);
                                AddStickyNodeGroup(node2, list);
                            }
                        }
                    }
                }
            }
        }

        private bool ConnectUpRandomNode(IEnumerable<PlayEdge> unfinishedEdges, IEnumerable<PlayNode> availableExitNodes)
        {
            // Connect key doors before non-key doors
            unfinishedEdges = unfinishedEdges
                .Shuffle(_rng)
                .OrderBy(x => x.Requires.Length != 0 ? 1 : 0)
                .ToArray();

            if (ConnectUpRandomNode(g_strictConstraints, unfinishedEdges, availableExitNodes))
                return true;

            return ConnectUpRandomNode(g_looseConstraints, unfinishedEdges, availableExitNodes);
        }

        private bool ConnectUpRandomNode(IEnumerable<ConnectConstraint> constraints, IEnumerable<PlayEdge> unfinishedEdges, IEnumerable<PlayNode> availableExitNodes)
        {
            foreach (var entrance in unfinishedEdges)
            {
                var exit = GetRandomRoom(constraints, entrance, availableExitNodes);
                if (exit != null)
                {
                    ConnectEdges(entrance, exit);
                    return true;
                }
            }
            return false;
        }

        private bool ConnectUpNode(PlayNode endNode, IEnumerable<PlayEdge> unfinishedEdges)
        {
            // Order by most key rich edge first
            unfinishedEdges = unfinishedEdges
                .Shuffle(_rng)
                .OrderByDescending(x => x.Parent.DoorRandoAllRequiredItems.Length + x.Requires.Length + x.RequiresRoom.Length)
                .ToArray();

            foreach (var exit in endNode.Edges.Shuffle(_rng))
            {
                foreach (var entrance in unfinishedEdges)
                {
                    if (ValidateConnection(g_endConstraints, entrance, exit))
                    {
                        ConnectEdges(entrance, exit);
                        return true;
                    }
                }
            }
            return false;
        }

        private PlayEdge[] GetUnfinishedEdges(IEnumerable<PlayNode> nodes)
        {
            var unfinishedEdges = nodes
                .Where(x => x.Visited)
                .SelectMany(x => x.Edges)
                .Where(x => x.Node == null && IsAccessible(x))
                .ToArray();
            return unfinishedEdges;
        }

        private void ConnectEdges(PlayEdge entrance, PlayEdge exit)
        {
            var exitNode = exit.Parent;
            var loopback = exitNode.Visited;
            if (!loopback)
            {
                exitNode.Visited = true;
                if (entrance.Requires.Length != 0)
                {
                    exitNode.DoorRandoAllRequiredItems = entrance.Parent.DoorRandoAllRequiredItems
                        .Union(entrance.Requires)
                        .ToArray();
                }
                else
                {
                    exitNode.DoorRandoAllRequiredItems = entrance.Parent.DoorRandoAllRequiredItems;
                }
                exitNode.Depth = entrance.Parent.Depth + 1;

                if (exitNode.Category == DoorRandoCategory.Box)
                {
                    _boxRoomReached = true;
                }

                _keyItemSpotsLeft += exitNode.Items.Count(x => x.Priority == ItemPriority.Normal && (x.Requires?.Length ?? 0) == 0);
                AddToKeyItemRequiredCount(exitNode.Edges.SelectMany(x => x.Requires));
                AddToKeyItemRequiredCount(exitNode.Requires);
            }

            ConnectDoor(entrance, exit, loopback);
        }

        private void AddToKeyItemRequiredCount(IEnumerable<byte> keys)
        {
            foreach (var key in keys)
            {
                _keyItemRequiredSet.Add(key);
            }
        }

        private void ConnectDoor(PlayEdge aEdge, PlayEdge bEdge, bool isLocked = false)
        {
            var a = aEdge.Parent;
            var b = bEdge.Parent;
            aEdge.Node = b;
            bEdge.Node = a;

            var isAlwaysLocked = false;
            if (aEdge.Requires.Length != 0 || (aEdge.Lock == LockKind.Unblock && bEdge.Lock != LockKind.Gate))
            {
                // If the door is locked or temporarily blocked, lock from the other side
                // This is a safety measure for loopbacks
                isLocked = true;
            }
            if (aEdge.NoUnlock)
            {
                isLocked = false;
            }
            else if (!aEdge.Randomize || !bEdge.Randomize)
            {
                isLocked = false;
            }
            else if (aEdge == bEdge)
            {
                isLocked = true;
                isAlwaysLocked = true;
            }

            byte lockId = 0;
            if (isLocked)
            {
                lockId = GetNextLockId();
                if (lockId == 0)
                {
                    isLocked = false;
                }
            }

            // Do not rewrite last room comparisons if edge is the same
            // this breaks rooms like 401 (South Office) when a door is unconnected
            var noCompareRewrite = aEdge == bEdge;
            if (a.Category != DoorRandoCategory.Static)
            {
                var aDoorId = aEdge.DoorId!.Value;
                var aRdt = _gameData.GetRdt(a.RdtId)!;
                aRdt.SetDoorTarget(aEdge, b.RdtId, bEdge.Entrance!.Value, noCompareRewrite);
                aRdt.RemoveDoorUnlock(aDoorId);
                if (aEdge.Lock == LockKind.Side)
                {
                    aEdge.Lock = LockKind.None;
                    aRdt.RemoveDoorLock(aDoorId);
                }
                if (isAlwaysLocked)
                {
                    aEdge.LockId = lockId;
                    aRdt.SetDoorLock(aDoorId, lockId);
                }
                else if (isLocked)
                {
                    aEdge.LockId = lockId;
                    aRdt.EnsureDoorUnlock(aDoorId, lockId);
                }
            }

            if (aEdge != bEdge && b.Category != DoorRandoCategory.Static && bEdge.Lock != LockKind.Always)
            {
                var bDoorId = bEdge.DoorId!.Value;
                var bRdt = _gameData.GetRdt(b.RdtId)!;
                if (aEdge.Entrance == null)
                {
                    bRdt.SetDoorTarget(bEdge, b.RdtId, bEdge.Entrance!.Value, noCompareRewrite);
                    bRdt.SetDoorLock(bDoorId, 255);
                }
                else
                {
                    bRdt.SetDoorTarget(bEdge, a.RdtId, aEdge.Entrance.Value, noCompareRewrite);
                    if (isLocked)
                    {
                        bEdge.Lock = LockKind.Side;
                        bEdge.LockId = lockId;
                        bRdt.SetDoorLock(bDoorId, lockId);
                    }
                    else if (b.Category != DoorRandoCategory.Static)
                    {
                        if (bEdge.Lock == LockKind.Side)
                            bEdge.Lock = LockKind.None;
                        bRdt.RemoveDoorLock(bDoorId);
                    }
                }
            }

            _logger.WriteLine($"    Connected {GetEdgeString(a, aEdge)} to {GetEdgeString(b, bEdge)}");
        }

        private byte GetNextLockId()
        {
            if (_lockIds.Count == 0)
                return 0;

            return _lockIds.Dequeue();
        }

        private void LockDoor(PlayEdge edge)
        {
            if (edge.DoorId == null)
                return;

            if (edge.Entrance != null)
            {
                ConnectDoor(edge, edge);
                return;
            }

            var lockId = GetNextLockId();
            if (lockId != 0)
            {
                var doorId = edge.DoorId.Value;
                var rdt = _gameData.GetRdt(edge.Parent.RdtId)!;
                rdt.SetDoorLock(doorId, lockId);
            }
        }

        private string GetEdgeString(PlayNode node, PlayEdge edge)
        {
            var rs = edge.GetRequiresString(_itemHelper);
            var s = $"{node.RdtId}:{edge.DoorId}";
            if (string.IsNullOrEmpty(rs))
                return s;
            else
                return $"{s} {rs}";
        }

        private static bool ValidateEntranceNodeForCounting(PlayEdge edge)
        {
            // Already connected
            if (edge.Node != null)
                return false;

            // Do not connect a blocked edge up
            if (edge.Lock == LockKind.Always)
                return false;

            // Do not connect a gate edge up, other side should be done first
            if (edge.Lock == LockKind.Gate)
                return false;

            // Do not connect this node up yet until we have visited all required rooms
            if (!edge.RequiresRoom.All(x => x.Visited))
                return false;

            // Edges that can't be randomized are not really free
            if (!edge.Randomize)
                return false;

            return true;
        }

        private static int GetFreeUnlockedEdgesForRoom(PlayNode node, PlayEdge excludeEdge)
        {
            return node.Edges.Count(x => x != excludeEdge && x.Node == null && x.Randomize && x.Requires.Length == 0 && IsAccessible(x));
        }

        private static bool IsAccessible(PlayEdge edge)
        {
            return edge.Lock == LockKind.None || edge.Lock == LockKind.Side || edge.Lock == LockKind.Unblock;
        }

        private PlayEdge? GetRandomRoom(IEnumerable<ConnectConstraint> constraints, PlayEdge entrance, IEnumerable<PlayNode> availableExitNodes)
        {
            var pool = new List<PlayEdge>();
            foreach (var exitNode in availableExitNodes)
            {
                foreach (var exitEdge in exitNode.Edges)
                {
                    if (ValidateConnection(constraints, entrance, exitEdge))
                    {
                        pool.Add(exitEdge);
                    }
                }
            }

            if (pool.Count == 0)
                return null;

            var index = _rng.Next(0, pool.Count);
            return pool[index];
        }

        private bool ValidateConnection(IEnumerable<ConnectConstraint> constraints, PlayEdge entrance, PlayEdge exit)
        {
            // Same node
            if (entrance.Parent == exit.Parent)
                return false;

            // Already connected
            if (entrance.Node != null || exit.Node != null)
                return false;

            foreach (var constraint in constraints)
            {
                if (!constraint.Validate(this, entrance, exit))
                {
                    return false;
                }
            }
            return true;
        }

        private void CalculateEdgeCounts(IEnumerable<PlayNode> nodes)
        {
            _numUnconnectedEdges = 0;
            _numUnlockedEdges = 0;
            _numKeyEdges = 0;
            _keyRichEdge = null;
            _keyRichEdgeScore = 0;

            foreach (var node in nodes)
            {
                if (!node.Visited)
                    continue;

                foreach (var edge in node.Edges)
                {
                    if (edge.Node == null && edge.Lock != LockKind.Always && edge.Randomize)
                    {
                        _numUnconnectedEdges++;
                        if (ValidateEntranceNodeForCounting(edge))
                        {
                            if (edge.Requires.Length != 0)
                                _numKeyEdges++;
                            else
                                _numUnlockedEdges++;

                            var keyRichScore = edge.Parent.DoorRandoAllRequiredItems.Length + edge.Requires.Length + edge.RequiresRoom.Length;
                            if (keyRichScore > _keyRichEdgeScore)
                            {
                                _keyRichEdge = edge;
                                _keyRichEdgeScore = keyRichScore;
                            }
                        }
                    }
                }
            }
        }

        private RandomizedRdt GetIdealRdt(RdtId rtdId)
        {
            var rdt = _gameData.GetRdt(rtdId)!;
            if (rdt.Version == BioVersion.Biohazard1)
            {
                // The mansion 2 RDTs are better for door positions
                if (rtdId.Stage <= 1)
                {
                    return _gameData.GetRdt(new RdtId(rtdId.Stage + 5, rtdId.Room))!;
                }
            }
            return rdt;
        }

        private static bool IsSameRdtId(RandomizedRdt rdt, RdtId id, RdtId other)
        {
            if (rdt.Version == BioVersion.Biohazard1)
            {
                if (id.Stage == 255)
                {
                    return new RdtId(rdt.RdtId.Stage, id.Room) == other;
                }
                else if (id.Room == other.Room)
                {
                    var stageA = id.Stage;
                    var stageB = other.Stage;
                    if (stageA >= 5) stageA -= 5;
                    if (stageB >= 5) stageB -= 5;
                    return stageA == stageB;
                }
            }
            return id == other;
        }

        private PlayNode GetOrCreateNode(RdtId rdtId)
        {
            var node = FindNode(rdtId);
            if (node != null)
                return node;

            var rdt = _gameData.GetRdt(rdtId)!;
            // var items = rdt.EnumerateOpcodes<IItemAotSetOpcode>(_config)
            //     .DistinctBy(x => x.Id)
            //     .Where(x => _config.IncludeDocuments || (_itemHelper.GetItemAttributes((byte)x.Type) & ItemAttribute.Document) == 0)
            //     .Select(x => new ItemPoolEntry()
            //     {
            //         RdtId = rdt.RdtId,
            //         Id = x.Id,
            //         Type = x.Type,
            //         Amount = x.Amount,
            //         GlobalId = x.GlobalId,
            //         AllowDocuments = true
            //     })
            //     .ToArray();
            var items = new ItemPoolEntry[0];

            // RE1 Jill does not have ink ribbons
            if (rdt.Version == BioVersion.Biohazard1 && _config.Player == 1)
            {
                var inkRibbonType = _itemHelper.GetItemId(CommonItemKind.InkRibbon);
                items = items.Where(x => x.Type != inkRibbonType).ToArray();
            }

            node = new PlayNode(rdtId);
            node.Items = items;
            _nodeMap.Add(rdtId, node);

            var mapRoom = _map.GetRoom(rdtId);
            if (mapRoom == null)
                throw new Exception("No JSON definition for room");

            node.Requires = mapRoom.Requires?.Select(x => (byte)x).ToArray() ?? Array.Empty<byte>();
            if (mapRoom.Items != null)
            {
                node.RequiresRoom = mapRoom.Items
                    .Where(x => x.Player == null || x.Player == _config.Player)
                    .Where(x => x.Scenario == null || x.Scenario == _config.Scenario)
                    .Where(x => x.DoorRando == null || x.DoorRando == _config.RandomDoors)
                    .SelectMany(x => x.RequiresRoom ?? Array.Empty<string>())
                    .Select(x => GetOrCreateNode(RdtId.Parse(x)))
                    .ToArray();
            }

            if (mapRoom.Doors != null)
            {
                foreach (var door in mapRoom.Doors)
                {
                    if (door.Player != null && door.Player != _config.Player)
                        continue;
                    if (door.Scenario != null && door.Scenario != _config.Scenario)
                        continue;
                    if (door.DoorRando != null && door.DoorRando != _config.RandomDoors)
                        continue;

                    DoorEntrance? entrance = null;
                    RandomizedRdt targetRdt;
                    IDoorAotSetOpcode? targetExit;
                    var target = RdtDoorTarget.Parse(door.Target!);
                    if (target.Id != null)
                    {
                        targetRdt = GetIdealRdt(target.Rdt);
                        targetExit = targetRdt.Doors.First(x => x.Id == target.Id);
                    }
                    else
                    {
                        targetRdt = GetIdealRdt(target.Rdt);
                        targetExit = targetRdt.Doors.FirstOrDefault(x => IsSameRdtId(targetRdt, x.Target, rdtId));
                    }
                    var doorId = door.Id ?? rdt.Doors.FirstOrDefault(x => x.Target == target.Rdt)?.Id;
                    if (door.EntranceId != null)
                    {
                        entrance = new DoorEntrance(door.EntranceId.Value);
                    }
                    else if (door.Entrance != null)
                    {
                        entrance = new DoorEntrance()
                        {
                            X = (short)door.Entrance.X,
                            Y = (short)door.Entrance.Y,
                            Z = (short)door.Entrance.Z,
                            D = (short)door.Entrance.D,
                            Floor = (byte)door.Entrance.Floor,
                            Camera = (byte)door.Entrance.Cut
                        };
                    }
                    else if (targetExit != null)
                    {
                        entrance = DoorEntrance.FromOpcode(targetExit);
                        if (entrance != null && door.Cut != null)
                        {
                            entrance = entrance.Value.WithCamera(door.Cut.Value);
                        }
                    }

                    if (_config.RandomDoors && door.Create)
                    {
                        var key = ParseLiteral(door.Lock);
                        var lockId = door.LockId == null ? (byte?)null : (byte?)door.LockId;
                        var doorOpcode = rdt.ConvertToDoor((byte)door.Id!, (byte)door.Texture, key == null ? (byte?)null : (byte?)key.Value, lockId);
                        if (door.Special.HasValue)
                        {
                            ((DoorAotSeOpcode)doorOpcode).Special = (byte)door.Special.Value;
                        }
                        door.Lock = null;
                    }

                    var edgeNode = GetOrCreateNode(target.Rdt);
                    var edge = new PlayEdge(node, edgeNode, door.NoReturn, door.Requires?.Select(x => (byte)x).ToArray(), doorId, entrance, door);
                    edge.Randomize = door.Randomize ?? true;
                    edge.NoUnlock = door.NoUnlock;
                    edge.IsBridgeEdge = door.IsBridgeEdge;
                    if (door.Lock != null)
                    {
                        edge.Lock = (LockKind)Enum.Parse(typeof(LockKind), door.Lock, true);
                    }
                    if (door.RequiresRoom != null)
                    {
                        edge.RequiresRoom = door.RequiresRoom.Select(x => GetOrCreateNode(RdtId.Parse(x))).ToArray();
                    }
                    node.Edges.Add(edge);
                }
            }

            if (mapRoom.Items != null)
            {
                foreach (var correctedItem in mapRoom.Items)
                {
                    if (correctedItem.Player != null && correctedItem.Player != _config.Player)
                        continue;
                    if (correctedItem.Scenario != null && correctedItem.Scenario != _config.Scenario)
                        continue;
                    if (correctedItem.DoorRando != null && correctedItem.DoorRando != _config.RandomDoors)
                        continue;
                    if (!_config.IncludeDocuments && (correctedItem.Type != null && (_itemHelper.GetItemAttributes(correctedItem.Type.Value) & ItemAttribute.Document) != 0))
                        continue;

                    // HACK 255 is used for item get commands
                    if (correctedItem.Id == 255)
                    {
                        items = items.Concat(new[] {
                                new ItemPoolEntry() {
                                    RdtId = rdtId,
                                    Raw = correctedItem,
                                    Id = correctedItem.Id,
                                    Type = (ushort)(correctedItem.Type ?? 0),
                                    Amount = correctedItem.Amount ?? 1,
                                    GlobalId = (ushort?)correctedItem.GlobalId,
                                    Requires = correctedItem.Requires?.Select(x => (byte)x).ToArray(),
                                    Priority = ParsePriority(correctedItem.Priority),
                                    AllowDocuments = correctedItem.AllowDocuments ?? true
                                }
                            }).ToArray();
                        continue;
                    }

                    var idx = Array.FindIndex(items, x => x.Id == correctedItem.Id);
                    if (idx != -1)
                    {
                        if (correctedItem.Link == null)
                        {
                            items[idx].Type = (ushort)(correctedItem.Type ?? items[idx].Type);
                            items[idx].Amount = correctedItem.Amount ?? items[idx].Amount;
                        }
                        else
                        {
                            items[idx].Type = 0;

                            var rdtItemId = RdtItemId.Parse(correctedItem.Link);
                            node.LinkedItems.Add(correctedItem.Id, rdtItemId);
                        }
                        items[idx].Requires = correctedItem.Requires?.Select(x => (byte)x).ToArray();
                        items[idx].Priority = ParsePriority(correctedItem.Priority);
                        items[idx].AllowDocuments = correctedItem.AllowDocuments ?? true;
                    }
                    else
                    {
                        items = items.Concat(new[] {
                                new ItemPoolEntry() {
                                    RdtId = rdtId,
                                    Raw = correctedItem,
                                    Id = correctedItem.Id,
                                    Type = (ushort)(correctedItem.Type ?? 0),
                                    Amount = correctedItem.Amount ?? 1,
                                    GlobalId = (ushort?)correctedItem.GlobalId,
                                    Requires = correctedItem.Requires?.Select(x => (byte)x).ToArray(),
                                    Priority = ParsePriority(correctedItem.Priority),
                                    AllowDocuments = correctedItem.AllowDocuments ?? true
                                }
                            }).ToArray();
                    }
                }

                // Remove any items that have no type (removed fixed items)
                node.Items = items.Where(x => x.Type != 0).ToArray();
            }

            mapRoom.Items = items
                .Select(x => new MapRoomItem()
                {
                    Nop = x.Raw?.Nop,
                    Offsets = x.Raw?.Offsets,
                    Id = x.Id,
                    GlobalId = (short)(x.GlobalId ?? -1),
                    Type = (byte)x.Type,
                    Amount = (byte)x.Amount,
                    Priority = x.Raw?.Priority,
                    Requires = x.Raw?.Requires,
                    RequiresRoom = x.Raw?.RequiresRoom,
                    AllowDocuments = x.Raw?.AllowDocuments,
                    DoorRando = x.Raw?.DoorRando,
                    Player = x.Raw?.Player,
                    Scenario = x.Raw?.Scenario
                })
                .ToArray();

            if (mapRoom.DoorRando != null)
            {
                foreach (var spec in mapRoom.DoorRando)
                {
                    if (spec.Player != null && spec.Player != _config.Player)
                        continue;
                    if (spec.Scenario != null && spec.Scenario != _config.Scenario)
                        continue;

                    if (spec.Category != null)
                    {
                        node.Category = (DoorRandoCategory)Enum.Parse(typeof(DoorRandoCategory), spec.Category, true);
                    }

                    node.DoorRandoNop = Map.ParseNopArray(spec.Nop, rdt);
                    node.HasCutscene = spec.Cutscene;
                }
            }

            if (mapRoom.LinkedRoom != null)
            {
                node.LinkedRdtId = RdtId.Parse(mapRoom.LinkedRoom);
            }

            return node;
        }

        private static int? ParseLiteral(string? s)
        {
            if (s == null)
                return null;

            if (s.StartsWith("0x"))
            {
                return int.Parse(s.Substring(2), System.Globalization.NumberStyles.HexNumber);
            }
            return int.Parse(s);
        }

        private void FinishOffEndNodes(PlayNode endNode)
        {
            foreach (var edge in endNode.Edges)
            {
                edge.NoReturn = false;
            }

            var stack = new Stack<PlayNode>();
            stack.Push(endNode);
            while (stack.Count != 0)
            {
                var node = stack.Pop();
                foreach (var edge in node.Edges)
                {
                    if (edge.Node == null && !edge.Randomize)
                    {
                        edge.Node = GetOrCreateNode(edge.OriginalTargetRdt);
                        if (!edge.Node.Visited)
                        {
                            edge.Node.Visited = true;
                            edge.Node.Depth = node.Depth + 1;
                            stack.Push(edge.Node);
                            _allNodes.Add(edge.Node);
                        }
                    }
                }
            }
        }

        private static ItemPriority ParsePriority(string? s)
        {
            if (s == null)
                return ItemPriority.Normal;
            return (ItemPriority)Enum.Parse(typeof(ItemPriority), s, true);
        }

        public PlayNode? FindNode(RdtId rdtId)
        {
            if (_nodeMap.TryGetValue(rdtId, out var node))
                return node;
            return null;
        }

        private abstract class ConnectConstraint
        {
            public virtual bool Validate(DoorRandomiser dr, PlayEdge entrance, PlayEdge exit)
            {
                return true;
            }
        }

        private class LockConstraint : ConnectConstraint
        {
            public override bool Validate(DoorRandomiser dr, PlayEdge entrance, PlayEdge exit)
            {
                // Do not connect a blocked edge up
                if (entrance.Lock == LockKind.Always)
                    return false;

                // Do not connect a gate edge up, other side should be done first (e.g. bottom of ladder in main hall)
                if (entrance.Lock == LockKind.Gate)
                    return false;

                // Do not connect this node up yet until we have visited all required rooms (e.g. armory)
                if (!entrance.RequiresRoom.All(x => x.Visited))
                    return false;

                // Ignore rest of checks if this is a fixed edge
                if (!entrance.Randomize || !exit.Randomize)
                    return true;

                // Exit node must have an entrance
                if (exit.Entrance == null)
                    return false;

                // For now, ignore any rooms with one way door setups
                // if (exit.Parent.Edges.Any(x => x.Entrance == null && x.Randomize))
                //     return false;

                // Don't connect to a door with a key requirement on the other side
                if (exit.Requires.Length != 0)
                    return false;

                // Don't connect to a door with a room requirement on the other side
                if (exit.RequiresRoom.Length != 0)
                    return false;

                // Don't connect to a door with a no unlock on the other side (in case we need to lock the door)
                if (exit.NoUnlock)
                    return false;

                // Don't connect to a door that is blocked / one way
                if (exit.Lock == LockKind.Always || exit.Lock == LockKind.Unblock)
                    return false;

                return true;
            }
        }

        private class FixedLinkConstraint : ConnectConstraint
        {
            public override bool Validate(DoorRandomiser dr, PlayEdge entrance, PlayEdge exit)
            {
                // HACK RE CV, if door uses offsets, we can't assume we can change
                //      the variant
                if (dr._config.Game == 4)
                {
                    var exitVariant = exit.Parent.RdtId.Variant ?? 0;
                    if (entrance.Raw.Offsets != null && exitVariant != 0)
                    {
                        return false;
                    }

                    var entranceVariant = entrance.Parent.RdtId.Variant ?? 0;
                    if (exit.Raw.Offsets != null && entranceVariant != 0)
                    {
                        return false;
                    }
                }

                if (entrance.IsBridgeEdge)
                    return false;
                if (!entrance.Randomize || !exit.Randomize)
                    return entrance.OriginalTargetRdt == exit.Parent.RdtId && exit.OriginalTargetRdt == entrance.Parent.RdtId;
                return true;
            }
        }

        private class LoopbackConstraint : ConnectConstraint
        {
            public override bool Validate(DoorRandomiser dr, PlayEdge entrance, PlayEdge exit)
            {
                // Check if this is a loopback node
                if (!exit.Parent.Visited)
                    return true;

                if (!entrance.Randomize || !exit.Randomize)
                    return true;

                // Do not waste the edge requiring the most keys on a loopback
                if (entrance == dr._keyRichEdge)
                    return false;

                // Do not connect back to a room that already connects to this room
                if (exit.Parent.Edges.Any(x => x.Node == entrance.Parent))
                    return false;

                // Do not loopback until all unvisited single edge rooms are visited
                // if (!dr._nodes.Any(x => !x.Visited && GetFreeEdgesForRoom(x) == 1))
                //     return false;

                // Check if this is a compatible loopback route (i.e. has the same tokens, no different ones)
                // var outliers = node.DoorRandoRouteTokens.UnionExcept(exitNode.DoorRandoRouteTokens).Count();
                var outliers = exit.Parent.DoorRandoAllRequiredItems.Except(entrance.Parent.DoorRandoAllRequiredItems).Count();
                if (outliers != 0)
                    return false;

                // Seen room before, check if we have another spare unconnected edge
                var remainingEdges = dr._numUnlockedEdges + dr._numKeyEdges - 1;
                return remainingEdges > 1;
            }
        }

        private class LeafConstraint : ConnectConstraint
        {
            public override bool Validate(DoorRandomiser dr, PlayEdge entrance, PlayEdge exit)
            {
                if (!entrance.Randomize || !exit.Randomize)
                    return true;

                // Check if this is a leaf node
                var extraEdges = exit.Parent.Edges.Count(x => x != exit && ValidateEntranceNodeForCounting(x));
                if (extraEdges != 0)
                    return true;

                // Do not connect to leaf node if this is our last edge (reserved for bridge node)
                var remainingEdges = dr._numUnlockedEdges + dr._numKeyEdges - 1;
                if (remainingEdges == 0)
                    return false;

                // Do not waste the edge requiring the most keys on a leaf node
                if (entrance == dr._keyRichEdge)
                    return false;

                return true;
            }
        }

        private class KeyConstraint : ConnectConstraint
        {
            public override bool Validate(DoorRandomiser dr, PlayEdge entrance, PlayEdge exit)
            {
                var numRequiredKeys = dr._keyItemRequiredSet.Count;
                var numKeySlots = dr._keyItemSpotsLeft;
                var unlockedEdges = dr._numUnlockedEdges;
                if (entrance.Requires.Length == 0)
                {
                    unlockedEdges--;
                }
                else if (numKeySlots < numRequiredKeys)
                {
                    // Do not extend past a key door until we our rich in item pickups
                    return false;
                }

                // Still a key free edge left
                if (unlockedEdges > 0)
                    return true;

                var extraEdges = exit.Parent.Edges.Where(x => x != exit && IsAccessible(x)).ToArray();
                var extraItems = exit.Parent.Items.Count(x => x.Priority == ItemPriority.Normal && (x.Requires?.Length ?? 0) == 0);
                if (extraEdges.Length != 0)
                {
                    var minKeyReq = extraEdges.Min(x => x.Requires.Length);

                    // Still a key free edge left
                    if (minKeyReq == 0)
                        return true;
                }

                // Make sure we have more items to pick up than required key items
                var totalKeyReq = extraEdges.Sum(x => x.Requires.Length) + exit.Requires.Length + exit.Parent.Items.Sum(x => x.Requires?.Length ?? 0);
                if (numKeySlots + extraItems >= numRequiredKeys + totalKeyReq)
                    return true;

                return false;
            }
        }

        private class BoxConstraint : ConnectConstraint
        {
            public override bool Validate(DoorRandomiser dr, PlayEdge entrance, PlayEdge exit)
            {
                if (dr._boxRoomReached)
                    return true;

                if (!entrance.Randomize || !exit.Randomize)
                    return true;

                // Do not extend graph via required key edges until a box room is accessible
                if (entrance.Parent.Category != DoorRandoCategory.Bridge &&
                    entrance.Parent.Category != DoorRandoCategory.Segment &&
                    entrance.Requires.Length != 0)
                {
                    return false;
                }

                if (exit.Parent.Category == DoorRandoCategory.Box)
                    return true;

                // Make sure we still have a free unlocked edge for the box room
                var extraUnlockedEdges = GetFreeUnlockedEdgesForRoom(exit.Parent, exit);
                if (dr._numUnlockedEdges <= 1 && extraUnlockedEdges == 0)
                    return false;

                return false;
            }
        }
    }
}
