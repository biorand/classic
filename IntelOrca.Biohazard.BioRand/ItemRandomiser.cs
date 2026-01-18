using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using IntelOrca.Biohazard.BioRand.RE1;
using IntelOrca.Biohazard.BioRand.RE2;
using IntelOrca.Biohazard.BioRand.RE3;
using IntelOrca.Biohazard.BioRand.RECV;
using IntelOrca.Biohazard.Script.Opcodes;

namespace IntelOrca.Biohazard.BioRand
{
    internal class ItemRandomiser
    {
        private static bool g_debugLogging = false;

        private readonly RandoLogger _logger;
        private readonly RandoConfig _config;
        private readonly GameData _gameData;
        private readonly Map _map;
        private readonly Rng _rng;

        private PlayNode[] _nodes = new PlayNode[0];
        private List<ItemPoolEntry> _currentPool = new List<ItemPoolEntry>();
        private List<ItemPoolEntry> _shufflePool = new List<ItemPoolEntry>();
        private List<ItemPoolEntry> _definedPool = new List<ItemPoolEntry>();
        private HashSet<KeyRequirement> _requiredItems = new HashSet<KeyRequirement>();
        private HashSet<byte> _startKeyItems = new HashSet<byte>();
        private HashSet<byte> _haveItems = new HashSet<byte>();
        private HashSet<PlayNode> _visitedRooms = new HashSet<PlayNode>();
        private HashSet<RdtItemId> _visitedItems = new HashSet<RdtItemId>();

        private HashSet<ushort> _globalIdVisited = new HashSet<ushort>();
        private Dictionary<ushort, Item> _globalIdToRandomItem = new Dictionary<ushort, Item>();

        private byte? _specialItem;
        private List<RandomInventory.Entry> _startingWeapons = new List<RandomInventory.Entry>();
        private List<byte> _availableGunpowder = new List<byte>();

        public IItemHelper ItemHelper { get; }

        public ItemRandomiser(RandoLogger logger, RandoConfig config, GameData gameData, Map map, Rng random, IItemHelper itemHelper)
        {
            _logger = logger;
            _config = config;
            _gameData = gameData;
            _map = map;
            _rng = random;
            ItemHelper = itemHelper;
        }

        public void RandomiseItems(PlayGraph graph)
        {
            if (graph.Start == null || graph.End == null)
                throw new ArgumentException("No start or end node", nameof(graph));

            _nodes = graph.GetAllNodes();

            _logger.WriteHeading("Randomizing Items:");
            _logger.WriteLine("Placing key items:");

            var checkpoint = graph.Start;
            RandomizeSpecialItem();
            ClearItems();
            _visitedRooms.Clear();

            var loopLimit = 5000;
            while (!_visitedRooms.Contains(graph.End) || !JustOptionalItemsLeft())
            {
                loopLimit--;
                if (loopLimit <= 0)
                    throw new Exception("Item randomization failed to terminate");

                PlaceKeyItem(_config.RandomDoors || _config.AlternativeRoutes);
                var newCheckpoint = Search(checkpoint);
                if (newCheckpoint != checkpoint && _requiredItems.Count == 0)
                {
                    _logger.WriteLine("    ------------ checkpoint ------------");
                    if (_config.RandomDoors || _config.Segmented)
                    {
                        _shufflePool.AddRange(_currentPool.Where(x => x.Priority != ItemPriority.Fixed));
                        _currentPool.Clear();
                    }
                    if (_config.RandomDoors)
                    {
                        ClearItems();
                        foreach (var item in newCheckpoint.DoorRandoAllRequiredItems)
                        {
                            _haveItems.Add(item);
                        }
                    }
                    else if (_config.Game == 1)
                    {
                        // HACK RE 1 battery needs placing in the lab
                        _haveItems.Remove(Re1ItemIds.Battery);
                    }
                    else if (_config.Game == 4)
                    {
                        // HACK RE CV items needed in multiple segments
                        _haveItems.Remove(ReCvItemIds.EaglePlate);
                        _haveItems.Remove(ReCvItemIds.OctaValveHandle);
                        _haveItems.Remove(ReCvItemIds.MusicBoxPlate);
                    }
                    checkpoint = newCheckpoint;
                }
            }
            _shufflePool.AddRange(_currentPool.Where(x => x.Priority != ItemPriority.Fixed));
            if (_shufflePool.DistinctBy(x => x.RdtItemId).Count() != _shufflePool.Count())
                throw new Exception();

            if (!_config.RandomDoors && _config.ShuffleItems)
            {
                ShuffleRemainingPool();
            }
            else
            {
                RandomiseRemainingPool();
            }

            SetLinkedItems();
            SetItems();
            PatchRE1Stuff();
            PatchDesk();
        }

        private void RandomizeSpecialItem()
        {
            // Only RE 2 has a special inventory item
            var gameHasSpecialItem = _config.Game == 2 || _config.Game == 4;
            if (gameHasSpecialItem && _config.RandomInventory && !_config.ShuffleItems)
            {
                if (_rng.Next(0, 4) == 0)
                {
                    if (_config.Game == 2)
                    {
                        _specialItem = (byte)_rng.NextOf(new[] {
                            Re2ItemIds.GVirus,
                            Re2ItemIds.Lighter,
                            Re2ItemIds.Lockpick,
                        });
                        _startKeyItems.Add(_specialItem.Value);
                        if (_specialItem.Value == Re2ItemIds.Lockpick)
                        {
                            _startKeyItems.Add(Re2ItemIds.SmallKey);
                            if (_config.Player == 0)
                            {
                                // Remove small key check for sewer door
                                var rdt = _gameData.GetRdt(new RdtId(3, 0x01));
                                if (rdt != null)
                                {
                                    // Fix messages and small key check to be lockpick related
                                    var msg = rdt.Opcodes.FirstOrDefault(x => x.Offset == 0x2786) as UnknownOpcode;
                                    if (msg != null)
                                    {
                                        msg.Data[1]--;
                                    }
                                    rdt.Nop(0x276C);
                                    rdt.Nop(0x27AA);
                                    rdt.Nop(0x27A2, 0x27B4);
                                }
                            }
                        }
                    }
                    else
                    {
                        _specialItem = (byte)_rng.NextOf(new[] {
                            ReCvItemIds.FamilyPicture,
                            ReCvItemIds.InkRibbon,
                            ReCvItemIds.Lighter,
                            ReCvItemIds.Lockpick,
                        });
                        _startKeyItems.Add(_specialItem.Value);
                        foreach (var item in ItemHelper.GetInitialKeyItems(_config))
                        {
                            _startKeyItems.Add(item);
                        }
                    }
                }
                else
                {
                    // Reserve for weapon
                    _specialItem = null;
                }

                if (_specialItem != Re2ItemIds.Lockpick)
                {
                    if (_config.Player == 1)
                    {
                        // Add small key check for sewer door
                        var rdt = _gameData.GetRdt(new RdtId(3, 0x01));
                        if (rdt != null)
                        {
                            // Fix messages and small key check to be lockpick related
                            var msg = rdt.Opcodes.FirstOrDefault(x => x.Offset == 0x2760) as UnknownOpcode;
                            if (msg != null)
                            {
                                msg.Data[1]++;
                            }

                            rdt.Nop(0x273A, 0x273E);
                            rdt.Nop(0x274E, 0x2757);
                            rdt.Nop(0x277C, 0x2780);
                            rdt.Nop(0x278E, 0x2799);
                        }
                    }
                }
            }
            else
            {
                _specialItem = 0;
                foreach (var item in ItemHelper.GetInitialKeyItems(_config))
                {
                    _startKeyItems.Add(item);
                }
            }
        }

        public RandomInventory? RandomizeStartInventory()
        {
            var size = ItemHelper.GetInventorySize(_config);
            if (size == null)
                return null;

            var entries = new List<RandomInventory.Entry>();
            var specialEntry = (RandomInventory.Entry?)null;
            int remaining;
            for (int i = 0; i < size.Length; i++)
            {
                _logger.WriteHeading($"Randomizing Inventory {i}:");

                // Special item
                remaining = size[i];
                if (i == 0)
                {
                    if (_specialItem.HasValue)
                    {
                        AddToInventory(_specialItem.Value, 1);
                    }
                    else
                    {
                        var possibleItems = _startingWeapons
                            .Select(x => x.Type)
                            .Where(x => ItemHelper.GetItemSize(x) <= 1)
                            .ToArray();
                        if (possibleItems.Length == 0 || _rng.NextProbability(25))
                        {
                            _specialItem = ItemHelper.GetItemId(CommonItemKind.Knife);
                        }
                        else
                        {
                            _specialItem = _rng.NextOf(possibleItems);
                        }
                    }
                }

                // RE 3 doesn't need the knife as it is in the box by default,
                // but if no weapon is given, the knife is required - otherwise the game crashes.
                // This is the case for RE 2 as well
                if (_config.Game != 3 || _startingWeapons.Count == 0)
                {
                    // Always give the player a knife
                    AddToInventoryCommon(CommonItemKind.Knife, 1);
                }

                // RE CV, we always need the lighter for first cutscene
                if (_config.Game == 4)
                {
                    AddToInventory(ReCvItemIds.Lighter, 1);
                }

                if (remaining >= 3 || _rng.NextProbability(75))
                {
                    // Weapons
                    foreach (var weapon in _startingWeapons)
                    {
                        var weaponType = weapon.Type;
                        var ammoType = ItemHelper
                            .GetAmmoTypeForWeapon(weaponType)
                            .Shuffle(_rng)
                            .FirstOrDefault();

                        var amount = weapon.Count;
                        var extra = (byte)0;
                        if (_rng.NextProbability(75))
                        {
                            amount = ItemHelper.GetMaxAmmoForAmmoType(weaponType);
                            var max = (int)Math.Max(1, ItemHelper.GetMaxAmmoForAmmoType(ammoType) * (_config.AmmoQuantity / 8.0));
                            extra = (byte)_rng.Next(0, Math.Min(max * 2, 100));
                        }
                        AddToInventory(weaponType, amount);
                        if (remaining >= 2)
                        {
                            if (extra != 0 && ammoType != 0)
                            {
                                AddToInventory(ammoType, extra);
                            }
                        }
                        if (remaining <= 1)
                            break;
                    }
                }

                // If we still don't have a weapon in slot 1, just make it a knife,
                // otherwise the game will crash
                if (entries.Count == 0)
                {
                    AddToInventoryCommon(CommonItemKind.Knife, 1);
                }

                if (_config.Game == 3 && _config.RatioGunpowder != 0 && _availableGunpowder.Count != 0)
                {
                    // If gunpowder is enabled, give the player the reloading tool
                    AddToInventory(Re3ItemIds.ReloadingTool, 255);
                }

                // Health items
                if (_config.RatioHealth != 0)
                {
                    if (_config.Game == 3 && _rng.NextProbability(50))
                    {
                        AddToInventory(Re3ItemIds.FirstAidSprayBox, (byte)_rng.Next(1, 4));
                    }
                    else
                    {
                        var healthItems = new[]
                        {
                            CommonItemKind.FirstAid,
                            CommonItemKind.HerbG,
                            CommonItemKind.HerbGG,
                            CommonItemKind.HerbGGG,
                            CommonItemKind.HerbGR,
                            CommonItemKind.HerbGB,
                            CommonItemKind.HerbGGB,
                            CommonItemKind.HerbGRB,
                            CommonItemKind.HerbR,
                            CommonItemKind.HerbB
                        };
                        healthItems = healthItems.Shuffle(_rng);
                        for (int j = 0; j < 4; j++)
                        {
                            if (_rng.NextProbability(50))
                            {
                                AddToInventoryCommon(healthItems[j], 1);
                            }
                        }
                    }
                }

                // Maybe an ink ribbon or two
                if (_config.RatioInkRibbons != 0)
                {
                    if (ItemHelper.HasInkRibbons(_config) && _rng.NextProbability(50))
                        AddToInventoryCommon(CommonItemKind.InkRibbon, (byte)_rng.Next(1, 3));
                }

                if (_config.Game == 3 && _config.RatioGunpowder != 0 && _availableGunpowder.Count != 0)
                {
                    // If gunpowder is enabled, give the player some gunpowder
                    for (int j = 0; j < 4; j++)
                    {
                        var gunpowder = _availableGunpowder[_rng.Next(0, _availableGunpowder.Count)];
                        AddToInventory(gunpowder, 1);
                    }
                }

                while (remaining > 0)
                {
                    entries.Add(new RandomInventory.Entry());
                    remaining--;
                }
            }
            return new RandomInventory(entries.ToArray(), specialEntry);

            void AddToInventoryCommon(CommonItemKind commonType, byte count)
            {
                var type = ItemHelper.GetItemId(commonType);
                AddToInventory(type, count);
            }

            void AddToInventory(byte type, byte count)
            {
                if (specialEntry == null && _specialItem == type)
                {
                    specialEntry = new RandomInventory.Entry(type, count, 0);
                    _logger.WriteLine($"Adding {ItemHelper.GetItemName(type)} x{count} as special item");
                }
                else
                {
                    var size = ItemHelper.GetItemSize(type);
                    if (remaining >= size)
                    {
                        if (size == 2)
                        {
                            // Double items must be placed at the top of the inventory
                            entries.Insert(0, new RandomInventory.Entry(type, count, 1));
                            entries.Insert(1, new RandomInventory.Entry(type, count, 2));
                        }
                        else
                        {
                            entries.Add(new RandomInventory.Entry(type, count, 0));
                        }
                        _logger.WriteLine($"Adding {ItemHelper.GetItemName(type)} x{count}");
                        remaining -= size;
                    }
                }
            }
        }

        public Item GetRandomGift(Rng rng, int generosity)
        {
            var interestedTypes = ItemAttribute.Ammo | ItemAttribute.Heal | ItemAttribute.InkRibbon;
            var groups = _definedPool
                .GroupBy(x => ItemHelper.GetItemAttributes((byte)x.Type))
                .Where(x => (x.Key & interestedTypes) != 0)
                .ToArray();
            if (groups.Length != 0)
            {
                var randomGroup = rng.NextOf(groups);
                switch (randomGroup.Key)
                {
                    case ItemAttribute.Ammo:
                        {
                            var item = _rng.NextOf(randomGroup.ToArray());
                            var maxAmount = ItemHelper.GetMaxAmmoForAmmoType((byte)item.Type);
                            var amount = (byte)_rng.Next(maxAmount / 2, maxAmount);
                            if (generosity == 0)
                            {
                                amount /= 2;
                            }
                            return new Item((byte)item.Type, amount);
                        }
                    case ItemAttribute.Heal:
                        {
                            if (generosity == 0)
                            {
                                return new Item(rng.NextOf(
                                    ItemHelper.GetItemId(CommonItemKind.HerbG),
                                    ItemHelper.GetItemId(CommonItemKind.HerbGG),
                                    ItemHelper.GetItemId(CommonItemKind.HerbGGB),
                                    ItemHelper.GetItemId(CommonItemKind.HerbR),
                                    ItemHelper.GetItemId(CommonItemKind.HerbB),
                                    ItemHelper.GetItemId(CommonItemKind.HerbGB),
                                    ItemHelper.GetItemId(CommonItemKind.FirstAid),
                                    ItemHelper.GetItemId(CommonItemKind.HerbGRB)), 1);
                            }
                            else
                            {
                                return new Item(rng.NextOf(
                                    ItemHelper.GetItemId(CommonItemKind.FirstAid),
                                    ItemHelper.GetItemId(CommonItemKind.HerbGRB)), 1);
                            }
                        }
                    case ItemAttribute.InkRibbon:
                        {
                            var inkType = ItemHelper.GetItemId(CommonItemKind.InkRibbon);
                            var count = (byte)rng.Next(3, 6);
                            if (generosity == 0)
                                count = (byte)(count / 2);
                            return new Item(inkType, count);
                        }
                }
            }
            var fasType = ItemHelper.GetItemId(CommonItemKind.FirstAid);
            return new Item(fasType, 1);
        }

        private bool JustOptionalItemsLeft()
        {
            if (_requiredItems.Count == 0)
                return true;

            return _requiredItems.All(x => !x.IsDoor && (x.Item == null || ItemHelper.IsOptionalItem(_config, (byte)x.Item.Value.Type)));
        }

        private void ClearItems()
        {
            // Leon starts with a lighter
            _haveItems.Clear();
            _haveItems.AddRange(_startKeyItems);
        }

        private PlayNode Search(PlayNode start)
        {
            // Repeat the search until the room count no longer increases
            // This fixes #509 where the order of rooms checked would cause a half search to occur
            // before and after a key item placement. Caused by a room requirement condition.
            var result = SearchInternal(start);
            var visited1 = _visitedRooms.Count;
            var visited2 = visited1;
            do
            {
                visited1 = visited2;
                result = SearchInternal(start);
                visited2 = _visitedRooms.Count;
            } while (visited2 > visited1);
            return result;
        }

        private PlayNode SearchInternal(PlayNode start)
        {
            var checkpoint = start;
            var seen = new HashSet<RdtId>();
            var walkedNodes = new List<PlayNode>();
            var stack = new Stack<PlayNode>();
            stack.Push(start);
            while (stack.Count != 0)
            {
                var node = stack.Pop();
                walkedNodes.Add(node);
                seen.Add(node.RdtId);

                if (_visitedRooms.Add(node))
                {
                    // Add any required keys for the room (ones that don't guard an item, e.g. Film A, Film B, etc.)
                    if (node.Requires.Length != 0)
                        _requiredItems.Add(new KeyRequirement(node.Requires));

                    // First time we have visited room, add room items to pool
                    foreach (var item in node.Items)
                    {
                        // Don't add items we require a key for
                        if (item.Requires != null && item.Requires.Length != 0)
                        {
                            _requiredItems.Add(new KeyRequirement(item.Requires, item));
                        }
                        else
                        {
                            AddItemToPool(item);
                        }
                    }

                    if (g_debugLogging && node.Items.Length != 0)
                    {
                        _logger.WriteLine($"    Room {node.RdtId} contains:");
                        foreach (var item in node.Items)
                        {
                            _logger.WriteLine($"        {item}");
                        }
                    }
                }

                foreach (var edge in node.Edges)
                {
                    if (edge.Node == null)
                        continue;
                    if (edge.Lock != LockKind.None && edge.Lock != LockKind.Unblock)
                        continue;
                    if (!edge.RequiresRoom.All(x => _visitedRooms.Contains(x)))
                        continue;

                    var requiredItems = edge.Requires == null ? new byte[0] : edge.Requires.Except(_haveItems).ToArray()!;
                    var justOptionalLeft = requiredItems.All(x => ItemHelper.IsOptionalItem(_config, (byte)x));
                    if (requiredItems.Length == 0 || justOptionalLeft)
                    {
                        if (seen.Contains(edge.Node.RdtId))
                            continue;

                        if (edge.NoReturn)
                        {
                            if (!_visitedRooms.Contains(edge.Node))
                            {
                                checkpoint = edge.Node;
                                if (g_debugLogging)
                                {
                                    _logger.WriteLine($"        {node} -> {edge.Node} (checkpoint)");
                                }
                            }
                        }
                        else
                        {
                            if (g_debugLogging)
                            {
                                _logger.WriteLine($"        {node} -> {edge.Node}");
                            }
                            stack.Push(edge.Node);
                        }
                    }
                    else
                    {
                        if (g_debugLogging)
                        {
                            var items = requiredItems!.Select(x => ItemHelper.GetItemName((byte)x)).ToArray();
                            _logger.WriteLine($"        Key requirement [{string.Join(", ", items)}] for {node} -> {edge.Node}");
                        }
                        _requiredItems.Add(new KeyRequirement(requiredItems, null, isDoor: true));
                    }
                }
            }
            return checkpoint;
        }

        private bool HasAllRequiredItems(byte[]? items)
        {
            if (items != null && items.Length != 0)
            {
                foreach (var r in items)
                {
                    if (!_haveItems.Contains(r))
                    {
                        return false;
                    }
                }
            }
            return true;
        }

        private int? FindNewKeyItemLocation(int type, bool includeLowPriority, bool includeKeyItems)
        {
            var randomOrder = Enumerable.Range(0, _currentPool.Count).Shuffle(_rng).ToArray();
            var bestI = (int?)null;
            foreach (var i in randomOrder)
            {
                var item = _currentPool[i];
                if (item.Priority == ItemPriority.Fixed)
                    continue;

                if (!includeLowPriority && GetTruePriority(item.Priority) == ItemPriority.Low)
                    continue;

                if (!HasAllRequiredItems(item.Requires))
                    continue;

                if (!includeKeyItems && (ItemHelper.GetItemAttributes((byte)item.Type) & ItemAttribute.Key) != 0)
                    continue;

                if (item.Type == type)
                    bestI = i;
                else
                    return i;
            }
            return bestI;
        }

        private static string GetNth(int i)
        {
            while (i >= 20)
                i /= 10;
            if (i == 1)
                return $"{i}st";
            if (i == 2)
                return $"{i}nd";
            if (i == 3)
                return $"{i}rd";
            return $"{i}th";
        }

        private void PlaceKeyItem(bool alternativeRoutes)
        {
            var keyItemPlaceOrder = GetKeyItemPlaceOrder();
            if (keyItemPlaceOrder.Length == 0)
                return;

            var checkList = GetKeyItemPlaceOrder();
            foreach (var req in checkList)
            {
                if (PlaceKeyItem(req, alternativeRoutes))
                {
                    var quantity = ItemHelper.GetItemQuantity(_config, (byte)req);
                    for (int i = 1; i < quantity; i++)
                    {
                        if (!PlaceKeyItem(req, true))
                        {
                            throw new Exception($"Unable to place {GetNth(i + 1)} {ItemHelper.GetItemName((byte)req)}");
                        }
                    }
                    UpdateRequiredItemList();
                    return;
                }
            }

            if (!alternativeRoutes)
            {
                // Failed, so try with alternative routes
                // This is a hack to get round the blue card
                PlaceKeyItem(true);
                return;
            }

            _logger.WriteLine("    Unable to place the following key items:");
            foreach (var item in checkList)
            {
                _logger.WriteLine($"        {ItemHelper.GetItemName((byte)item)}");
            }

            if (keyItemPlaceOrder.Any(x => !ItemHelper.IsOptionalItem(_config, (byte)x)))
                throw new Exception("Unable to find key item to swap");

            _requiredItems.Clear();
        }

        private byte[] GetKeyItemPlaceOrder()
        {
            UpdateRequiredItemList();
            return _requiredItems
                .Shuffle(_rng)
                .OrderBy(x => x.IsDoor ? 0 : 1)
                .ThenBy(x => x.Keys.Length)
                .SelectMany(x => x.Keys)
                .Where(x => !_haveItems.Contains(x))
                .ToArray();
        }

        private void UpdateRequiredItemList()
        {
            var newItemsForPool = _requiredItems
                .Where(x => x.Item != null && x.Keys.All(x => _haveItems.Contains(x)))
                .Select(x => x.Item)
                .ToArray();
            foreach (var rq in newItemsForPool)
            {
                AddItemToPool(rq!.Value);
            }

            _requiredItems.RemoveWhere(x => x.Keys.All(x => _haveItems.Contains(x)));
        }

        private bool PlaceKeyItem(byte req, bool alternativeRoute)
        {
            // HACK for RE 2, Leon has lighter from start, Claire has lockpick instead of small keys
            //      this means it won't exist anywhere in the game when adding it in
            var noOriginalItemLocation = false;
            if (_config.Game == 2 && ((_config.Player == 0 && req == Re2ItemIds.Lighter) || (_config.Player == 1 && req == Re2ItemIds.SmallKey)))
            {
                noOriginalItemLocation = true;
            }

            // HACK CV remove when we can
            if (_config.Game == 4)
            {
                noOriginalItemLocation = true;
            }

            // Get a new location for the key item
            var index = FindNewKeyItemLocation(req, includeLowPriority: false, includeKeyItems: !noOriginalItemLocation);
            if (index == null)
            {
                if (_config.RandomDoors)
                {
                    throw new BioRandUserException("This seed has a door configuration where not enough item " +
                        "pickups exist for all the required key items. Try another seed, or increasing your segment size.");
                }
                else
                {
                    index = FindNewKeyItemLocation(req, includeLowPriority: true, includeKeyItems: !noOriginalItemLocation);
                    if (index == null)
                    {
                        throw new Exception($"Unable to find location for {ItemHelper.GetItemName(req)}.");
                    }
                }
            }

            var itemEntry = _currentPool[index.Value];
            var itemParentNode = _nodes.First(x => x.RdtId == itemEntry.RdtId);

            if (!_config.RandomDoors)
            {
                if (noOriginalItemLocation)
                {
                    itemEntry.Amount = 1;
                }
                else
                {
                    // Find original location of key item
                    var futureItem = false;
                    var originalIndex = _currentPool.FindIndex(x => x.Type == req);
                    if (originalIndex == -1 && alternativeRoute)
                    {
                        var t = FindKeyInLaterArea(req);
                        if (t != null)
                        {
                            var (futureNode, itemIndex) = t.Value;
                            if (g_debugLogging)
                            {
                                _logger.WriteLine($"        Found key as future item ({futureNode.Items[itemIndex]})");
                            }

                            // Change room item to the item we are going to replace
                            var id = futureNode.Items[itemIndex].Id;
                            var kc = futureNode.Items[itemIndex].Amount;
                            futureNode.Items[itemIndex].Type = itemEntry.Type;
                            futureNode.Items[itemIndex].Amount = itemEntry.Amount;
                            itemEntry.Amount = kc;
                            futureItem = true;

                            // Change if in required items pool
                            foreach (var ri in _requiredItems.ToArray())
                            {
                                if (ri.Item.HasValue)
                                {
                                    var reqItem = ri.Item.Value;
                                    if (reqItem.RdtId == futureNode.RdtId && reqItem.Id == id)
                                    {
                                        reqItem.Type = itemEntry.Type;
                                        reqItem.Amount = itemEntry.Amount;
                                        _requiredItems.Remove(ri);
                                        _requiredItems.Add(ri.WithItem(reqItem));
                                    }
                                }
                            }
                        }
                    }
                    if (!futureItem)
                    {
                        if (originalIndex == -1)
                        {
                            // Check original key was in a previous checkpoint area (edge case for Weapon Box Key)
                            var shuffleIndex = _shufflePool.FindIndex(x => x.Type == req);
                            if (shuffleIndex != -1)
                            {
                                var item = _shufflePool[shuffleIndex];
                                _shufflePool.RemoveAt(shuffleIndex);
                                _currentPool.Add(item);
                                originalIndex = _currentPool.Count - 1;
                            }
                        }
                        if (originalIndex == -1)
                        {
                            return false;
                        }

                        // Check original key can actually be moved
                        var originalItemEntry = _currentPool[originalIndex];
                        if (originalItemEntry.Priority == ItemPriority.Fixed)
                        {
                            index = originalIndex;
                            itemEntry = originalItemEntry;
                        }

                        // Change original key item to the item we are going to replace
                        var keyCount = originalItemEntry.Amount;
                        originalItemEntry.Type = itemEntry.Type;
                        originalItemEntry.Amount = itemEntry.Amount;
                        _currentPool[originalIndex] = originalItemEntry;
                        itemEntry.Amount = keyCount;
                    }
                }
            }
            else
            {
                itemEntry.Amount = GetTotalKeyRequirementCount(itemParentNode, req);
            }
            itemEntry.Type = req;

            // Remove new key item location from pool
            _haveItems.Add(req);
            _currentPool.RemoveAt(index.Value);
            SetItem(itemEntry);
            itemParentNode.PlacedKeyItems.Add(itemEntry);

            _logger.WriteLine($"    Placing key item ({ItemHelper.GetItemName((byte)itemEntry.Type)} x{itemEntry.Amount}) in {itemEntry.RdtId}:{itemEntry.Id}");
            return true;
        }

        private (PlayNode, int)? FindKeyInLaterArea(byte req)
        {
            foreach (var node in _nodes)
            {
                for (int i = 0; i < node.Items.Length; i++)
                {
                    var item = node.Items[i];
                    if (item.Type == req && item.Priority != ItemPriority.Fixed && !_visitedItems.Contains(item.RdtItemId))
                    {
                        return (node, i);
                    }
                }
            }
            return null;
        }

        private void AddItemToPool(ItemPoolEntry item)
        {
            // CVX currently, eventually add to all games
            if (_config.Game == 4 && !_globalIdVisited.Add(item.GlobalId!.Value))
            {
                return;
            }

            if (_visitedItems.Add(item.RdtItemId))
            {
                _currentPool.Add(item);
                if (g_debugLogging)
                    _logger.WriteLine($"    Add {item.ToString(ItemHelper)} to current pool");
            }

            if (_currentPool.DistinctBy(x => x.RdtItemId).Count() != _currentPool.Count())
                throw new Exception();
        }

        private void SetItem(ItemPoolEntry entry)
        {
            _definedPool.Add(entry);
            if (_config.Game == 4)
            {
                _globalIdToRandomItem.Add(entry.GlobalId!.Value, new Item((byte)entry.Type, entry.Amount));
            }
        }

        private void RandomiseRemainingPool()
        {
            _logger.WriteLine("Randomizing non-key items:");
            if (_config.RatioAmmo == 0 && _config.RatioHealth == 0 && _config.RatioInkRibbons == 0)
            {
                throw new Exception("No item ratios have been set.");
            }

            // Shuffle the pool, keep low priority at the end
            var shuffled = _shufflePool
                .Where(x => x.Priority == ItemPriority.Normal)
                .Shuffle(_rng)
                .Concat(_shufflePool.Where(x => GetTruePriority(x.Priority) == ItemPriority.Low))
                .ToQueue();
            _shufflePool.Clear();

            // Weapons first
            _startingWeapons.Clear();

            var availableWeapons = new List<byte>();
            var weaponPool = ItemHelper
                .GetWeapons(_rng, _config)
                .ToList();
            for (var i = weaponPool.Count - 1; i >= 0; i--)
            {
                if (!_config.EnabledWeapons[i])
                {
                    weaponPool.RemoveAt(i);
                }
            }

            if (_config.RandomInventory && !_config.ShuffleItems)
            {
                RandomizeInventory(weaponPool, availableWeapons);
            }
            else
            {
                availableWeapons.AddRange(ItemHelper.GetDefaultWeapons(_config));
            }

            while (weaponPool.Count != 0)
            {
                var weapon = weaponPool[weaponPool.Count - 1];
                weaponPool.RemoveAt(weaponPool.Count - 1);
                availableWeapons.Add(weapon);

                // Spawn weapon
                var amount = GetRandomAmount(weapon, true);
                SpawnItem(shuffled, weapon, amount);
            }

            var ammoTypes = new HashSet<byte>();
            var gunpowderTypes = new HashSet<byte>();
            foreach (var weapon in availableWeapons)
            {
                // Spawn upgrade
                var upgradeType = ItemHelper.GetWeaponUpgrade(weapon, _rng, _config);
                if (upgradeType != null && _rng.NextProbability(50))
                {
                    SpawnItem(shuffled, upgradeType.Value, 1);
                }

                // Add supported ammo types
                var weaponAmmoTypes = ItemHelper.GetAmmoTypeForWeapon(weapon);
                foreach (var ammoType in weaponAmmoTypes)
                {
                    ammoTypes.Add(ammoType);
                }

                // Add gunpowder types
                var weaponGunpowderTypes = ItemHelper.GetWeaponGunpowder(weapon);
                foreach (var gunpowderType in weaponGunpowderTypes)
                {
                    gunpowderTypes.Add(gunpowderType);
                }
            }
            _availableGunpowder.AddRange(gunpowderTypes);

            // Now everything else
            var gunpowderTable = _rng.CreateProbabilityTable<byte>();
            foreach (var gunpowderType in gunpowderTypes)
            {
                var probability = ItemHelper.GetItemProbability(gunpowderType);
                gunpowderTable.Add(gunpowderType, probability);
            }

            var ammoTable = _rng.CreateProbabilityTable<byte>();
            foreach (var ammoType in ammoTypes)
            {
                var probability = ItemHelper.GetItemProbability(ammoType);
                ammoTable.Add(ammoType, probability);
            }

            var healthTable = _rng.CreateProbabilityTable<byte>();
            healthTable.Add(ItemHelper.GetItemId(CommonItemKind.HerbG), 0.5);
            healthTable.Add(ItemHelper.GetItemId(CommonItemKind.HerbR), 0.15);
            healthTable.Add(ItemHelper.GetItemId(CommonItemKind.HerbB), 0.25);
            healthTable.Add(ItemHelper.GetItemId(CommonItemKind.FirstAid), 0.1);

            var inkTable = _rng.CreateProbabilityTable<byte>();
            inkTable.Add(ItemHelper.GetItemId(CommonItemKind.InkRibbon), 1);

            var totalRatio = (double)(_config.RatioGunpowder + _config.RatioAmmo + _config.RatioHealth + _config.RatioInkRibbons);
            var numGunpowder = (int)Math.Ceiling((_config.RatioGunpowder / totalRatio) * shuffled.Count);
            var numAmmo = (int)Math.Ceiling((_config.RatioAmmo / totalRatio) * shuffled.Count);
            var numHealth = (int)Math.Ceiling((_config.RatioHealth / totalRatio) * shuffled.Count);
            var numInk = (int)Math.Ceiling((_config.RatioInkRibbons / totalRatio) * shuffled.Count);

            var proportions = new List<(int, Rng.Table<byte>)>();
            if (ItemHelper.HasGunPowder(_config))
            {
                proportions.Add((numGunpowder, gunpowderTable));
            }
            proportions.Add((numAmmo, ammoTable));
            proportions.Add((numHealth, healthTable));
            if (ItemHelper.HasInkRibbons(_config))
            {
                proportions.Add((numInk, inkTable));
            }
            proportions = proportions
                .Where(x => x.Item1 != 0 && !x.Item2.IsEmpty)
                .OrderBy(x => x.Item1)
                .ToList();
            if (proportions.Count > 0)
            {
                var lastP = proportions[proportions.Count - 1];
                lastP.Item1 = int.MaxValue;
                proportions[proportions.Count - 1] = lastP;

                foreach (var p in proportions)
                {
                    SpawnItems(shuffled, p.Item1, p.Item2);
                }
            }
        }

        private void RandomizeInventory(List<byte> weaponPool, List<byte> availableWeapons)
        {
            var weaponKinds = new[] { (WeaponKind)_config.Weapon0, (WeaponKind)_config.Weapon1 };
            for (int i = 0; i < weaponKinds.Length; i++)
            {
                var weaponKind = weaponKinds[i];
                if (weaponKind == WeaponKind.Random)
                    weaponKind = _rng.NextOf(WeaponKind.None, WeaponKind.Sidearm, WeaponKind.Primary, WeaponKind.Powerful);
                if (weaponKind == WeaponKind.None)
                    continue;

                var itemIndex = weaponPool.FindLastIndex(x => weaponKind == ItemHelper.GetWeaponKind(x));
                if (itemIndex != -1)
                {
                    var weapon = weaponPool[itemIndex];
                    weaponPool.RemoveAt(itemIndex);
                    availableWeapons.Add(weapon);

                    // Add to inventory
                    var amount = GetRandomAmount(weapon, true);
                    _startingWeapons.Add(new RandomInventory.Entry(weapon, amount));
                }
            }
        }

        private void SpawnItems(Queue<ItemPoolEntry> pool, int count, Rng.Table<byte> probabilityTable)
        {
            for (int i = 0; i < count; i++)
            {
                var itemType = probabilityTable.Next();
                if (!SpawnItem(pool, itemType, GetRandomAmount(itemType, false)))
                {
                    break;
                }
            }
        }

        private bool SpawnItem(Queue<ItemPoolEntry> pool, byte itemType, byte amount)
        {
            if (pool.Count != 0)
            {
                var oldEntry = pool.Dequeue();
                var newEntry = oldEntry;
                newEntry.Type = itemType;
                newEntry.Amount = amount;
                _logger.WriteLine($"    Replaced {oldEntry.ToString(ItemHelper)} with {newEntry.ToString(ItemHelper)}");
                if (_definedPool.Any(x => x.RdtItemId == newEntry.RdtItemId))
                    throw new Exception();
                SetItem(newEntry);
                return true;
            }
            else
            {
                return false;
            }
        }

        private byte GetRandomAmount(byte type, bool fullQuantity)
        {
            if (type == ItemHelper.GetItemId(CommonItemKind.InkRibbon))
            {
                return (byte)_rng.Next(1, 3);
            }

            var multiplier = fullQuantity ? 1 : (_config.AmmoQuantity / 8.0);
            var max = ItemHelper.GetMaxAmmoForAmmoType(type);
            return (byte)_rng.Next(1, (int)(max * multiplier) + 1);
        }

        private void ShuffleRemainingPool()
        {
            _logger.WriteLine("Shuffling non-key items:");

            if (_config.RandomInventory && !_config.ShuffleItems)
            {
                var weaponPool = ItemHelper
                    .GetWeapons(_rng, _config)
                    .Shuffle(_rng)
                    .ToList();
                RandomizeInventory(weaponPool, new List<byte>());
            }

            var shufflePool = _shufflePool
                .Where(x => GetTruePriority(x.Priority) != ItemPriority.Low)
                .ToArray();
            var shuffled = shufflePool.Shuffle(_rng).ToQueue();
            for (int i = 0; i < shufflePool.Length; i++)
            {
                var entry = shufflePool[i];
                var shuffleEntry = shuffled.Dequeue();
                if (!entry.AllowDocuments)
                {
                    var iteration = 0;
                    while ((ItemHelper.GetItemAttributes((byte)shuffleEntry.Type) & ItemAttribute.Document) != 0)
                    {
                        // We can't replace this item with a document,
                        // put item back on the end and take next one
                        shuffled.Enqueue(shuffleEntry);
                        shuffleEntry = shuffled.Dequeue();
                        iteration++;
                        if (iteration >= 1000)
                        {
                            throw new BioRandUserException("Item could not be replaced with a non-document item.");
                        }
                    }
                }
                entry.Type = shuffleEntry.Type;
                entry.Amount = shuffleEntry.Amount;
                _logger.WriteLine($"    Swapped {shufflePool[i].ToString(ItemHelper)} with {shuffleEntry.ToString(ItemHelper)}");
                SetItem(entry);
            }
            _shufflePool.Clear();
        }

        private void SetLinkedItems()
        {
            _logger.WriteLine("Setting up linked items:");
            foreach (var node in _nodes)
            {
                foreach (var linkedItem in node.LinkedItems)
                {
                    LinkItem(linkedItem.Value, new RdtItemId(node.RdtId, linkedItem.Key));
                }
                if (node.LinkedRdtId != null)
                {
                    var linkedRdtId = node.LinkedRdtId.Value;
                    var linkedRdt = _gameData.GetRdt(linkedRdtId)!;
                    var ids = linkedRdt.Items.Select(x => x.Id).Distinct().ToArray();
                    foreach (var id in ids)
                    {
                        LinkItem(new RdtItemId(node.RdtId, id), new RdtItemId(linkedRdtId, id));
                    }
                }
            }
        }

        private void LinkItem(RdtItemId source, RdtItemId target)
        {
            var sourceItemIndex = _definedPool.FindIndex(x => x.RdtItemId == source);
            if (sourceItemIndex != -1)
            {
                var sourceItem = _definedPool[sourceItemIndex];
                var targetItem = sourceItem;
                targetItem.RdtItemId = target;
                SetItem(targetItem);
                _logger.WriteLine($"    {sourceItem.ToString(ItemHelper)} placed at {target}");
            }
        }

        private void SetItems()
        {
            if (_config.Game == 4)
            {
                foreach (var rdt in _gameData.Rdts)
                {
                    var room = _map.GetRoom(rdt.RdtId);
                    if (room?.Items != null)
                    {
                        foreach (var jItem in room.Items)
                        {
                            if (jItem.GlobalId is short globalId)
                            {
                                if (_globalIdToRandomItem.TryGetValue((ushort)jItem.GlobalId, out var item))
                                {
                                    rdt.SetItem(jItem.Id, item, jItem);
                                }
                            }
                            else
                            {
                                throw new InvalidDataException($"No global ID for {rdt.RdtId}:{jItem.Id}");
                            }
                        }
                    }
                }
            }
            else
            {
                foreach (var entry in _definedPool)
                {
                    var rdt = _gameData.GetRdt(entry.RdtId)!;

                    // HACK: 255 is used for give commands
                    if (entry.Id == 255)
                    {
                        foreach (var itemGet in rdt.ItemGets)
                        {
                            itemGet.Type = (byte)entry.Type;
                            itemGet.Amount = (byte)entry.Amount;
                        }
                    }
                    else
                    {
                        if (rdt.Version == BioVersion.Biohazard1)
                        {
                            var originalType = rdt.Items.FirstOrDefault(x => x.Id == entry.Id)?.Type;
                            if (originalType != null)
                            {
                                foreach (var opcode in rdt.Opcodes)
                                {
                                    if (opcode is TestPickupOpcode testPickup && testPickup.Type == (byte)originalType)
                                    {
                                        testPickup.Type = (byte)entry.Type;
                                    }
                                }
                            }
                        }

                        rdt.SetItem(entry.Id, new Item((byte)entry.Type, entry.Amount), entry.Raw);
                    }
                }
            }
        }

        private void PatchRE1Stuff()
        {
            foreach (var stage in new[] { 0, 5 })
            {
                var rdt = _gameData.GetRdt(new RdtId(stage, 0x0D));
                if (rdt == null || rdt.Version != BioVersion.Biohazard1)
                    return;

                var type = (byte)rdt.Items.First(x => x.Id == 4).Type;
                rdt.Patches.Add(new KeyValuePair<int, byte>(0x26E7, type));

                type = (byte)rdt.Items.First(x => x.Id == 131).Type;
                rdt.Patches.Add(new KeyValuePair<int, byte>(0x270F, type));
            }
        }

        private void PatchDesk()
        {
            var rdt = _gameData.GetRdt(new RdtId(0, 0x15));
            if (rdt == null || rdt.Version != BioVersion.Biohazard2)
                return;

            // Only take 5 inspections for the item rather than 50
            if (_config.Player == 0)
                rdt.Patches.Add(new KeyValuePair<int, byte>(0x1A20, 5));
            else
                rdt.Patches.Add(new KeyValuePair<int, byte>(0x1C7A, 5));
        }

        private byte GetTotalKeyRequirementCount(PlayNode node, byte keyType)
        {
            if (ItemHelper.IsItemInfinite((byte)keyType))
                return 0;

            if (!ItemHelper.IsRe2ItemIdsDiscardable((byte)keyType))
                return 1;

            byte total = 0;
            var visited = new HashSet<PlayNode>();
            var stack = new Stack<PlayNode>();
            stack.Push(node);
            visited.Add(node);
            while (stack.Count != 0)
            {
                var n = stack.Pop();
                if (n.Requires.Contains(keyType))
                    total++;

                foreach (var item in n.Items)
                {
                    if (item.Requires?.Contains(keyType) == true)
                        total++;
                }

                foreach (var edge in n.Edges)
                {
                    if (edge.Requires.Contains(keyType))
                        total++;
                    if (edge.Node != null && edge.Lock != LockKind.Always && visited.Add(edge.Node))
                    {
                        stack.Push(edge.Node);
                    }
                }
            }
            return total;
        }

        private ItemPriority GetTruePriority(ItemPriority priority)
        {
            if (!_config.HiddenKeyItems && priority == ItemPriority.Hidden)
                return ItemPriority.Low;
            return priority;
        }

        private class KeyRequirement : IEquatable<KeyRequirement>
        {
            public byte[] Keys { get; }
            public ItemPoolEntry? Item { get; }
            public bool IsDoor { get; }

            public KeyRequirement(IEnumerable<byte> keys)
                : this(keys, null, false)
            {
            }

            public KeyRequirement(IEnumerable<byte> keys, ItemPoolEntry? item, bool isDoor = false)
            {
                Keys = keys.OrderBy(x => x).ToArray();
                if (Keys.Length == 0)
                    throw new ArgumentException(nameof(keys));
                Item = item;
                IsDoor = isDoor;
            }

            public override int GetHashCode()
            {
                return string.Join(",", Keys).GetHashCode();
            }

            public override bool Equals(object obj)
            {
                return Equals(obj as KeyRequirement);
            }

            public bool Equals(KeyRequirement? other)
            {
                if (other == null)
                    return false;
                if (!Keys.SequenceEqual(other.Keys))
                    return false;
                if (!Item.Equals(other.Item))
                    return false;
                if (!Keys.SequenceEqual(other.Keys))
                    return false;
                return IsDoor == other.IsDoor;
            }

            public override string ToString()
            {
                return $"{string.Join(",", Keys.Select(x => x))}";
            }

            public KeyRequirement WithItem(ItemPoolEntry? item)
            {
                return new KeyRequirement(Keys, item, IsDoor);
            }
        }
    }
}
