using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using IntelOrca.Biohazard.BioRand.RE2;
using IntelOrca.Biohazard.Model;
using IntelOrca.Biohazard.Script.Opcodes;

namespace IntelOrca.Biohazard.BioRand
{
    internal class NPCRandomiser
    {
        private static readonly object _sync = new object();

        private readonly BioVersion _version;
        private readonly RandoLogger _logger;
        private readonly FileRepository _fileRepository;
        private readonly RandoConfig _config;
        private readonly string _modPath;
        private readonly GameData _gameData;
        private readonly Map _map;
        private readonly Rng _rng;
        private readonly INpcHelper _npcHelper;
        private readonly DataManager _dataManager;
        private readonly string[] _playerActors;
        private readonly string[] _originalPlayerActor;

        private Dictionary<byte, string> _extraNpcMap = new Dictionary<byte, string>();
        private Dictionary<byte, (short, short)> _npcHeight = new Dictionary<byte, (short, short)>();
        private List<ExternalCharacter> _emds = new List<ExternalCharacter>();

        private VoiceRandomiser? _voiceRandomiser;

        public string PlayerActor => _playerActors[0];
        public HashSet<string> SelectedActors { get; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        public NPCRandomiser(
            BioVersion version,
            RandoLogger logger,
            FileRepository fileRepository,
            RandoConfig config,
            string modPath,
            GameData gameData,
            Map map,
            Rng random,
            INpcHelper npcHelper,
            DataManager dataManager,
            string[]? playerActors,
            VoiceRandomiser voiceRandomiser)
        {
            _version = version;
            _logger = logger;
            _fileRepository = fileRepository;
            _config = config;
            _modPath = modPath;
            _gameData = gameData;
            _map = map;
            _rng = random;
            _npcHelper = npcHelper;
            _dataManager = dataManager;
            _originalPlayerActor = _npcHelper.GetPlayerActors(config.Player);
            _playerActors = playerActors ?? _originalPlayerActor;
            _voiceRandomiser = voiceRandomiser;

            FixRooms();
        }

        private void FixRooms()
        {
            // For RE 2, room 216 and 301 need the partner removed to prevent cutscene softlock
            DetachPartner(new RdtId(1, 0x16));
            DetachPartner(new RdtId(2, 0x01));
            DetachPartner(new RdtId(2, 0x07));
            DetachPartner(new RdtId(2, 0x0B));
        }

        private void DetachPartner(RdtId rdtId)
        {
            var rdt = _gameData.GetRdt(rdtId);
            if (rdt != null && rdt.Version == BioVersion.Biohazard2)
            {
                rdt.AdditionalOpcodes.Add(new UnknownOpcode(0, 0x22, new byte[] { 0x01, 0x03, 0x00 }));
            }
        }

        public void AddNPC(byte emId, string emPath, string actor)
        {
            _emds.Add(new ExternalCharacter(emId, emPath, actor));
        }

        public void Randomise()
        {
            RandomizeExternalNPCs(_rng.NextFork());
            RandomizeRooms(_rng.NextFork());
        }

        public byte GetRandomNpc(RandomizedRdt rdt, Rng rng)
        {
            var defaultIncludeTypes = _npcHelper.GetDefaultIncludeTypes(rdt)
                .Shuffle(rng)
                .DistinctBy(x => _npcHelper.GetActor(x))
                .ToArray();
            if (rng.Next(0, 8) != 0)
            {
                // Make it rare for player to also be an NPC
                var newDefaultIncludeTypes = defaultIncludeTypes
                    .Where(x => GetActor(x) != _playerActors[0])
                    .ToArray();
                if (newDefaultIncludeTypes.Length != 0)
                {
                    defaultIncludeTypes = newDefaultIncludeTypes;
                }
            }
            return defaultIncludeTypes[0];
        }

        private void RandomizeExternalNPCs(Rng rng)
        {
            if (_config.Game == 1)
            {
                lock (_sync)
                {
                    // RE 1 shared EMD files, so must have a shared RNG
                    var sharedRng = new Rng(_config.Seed);
                    RandomizeExternalNPC(sharedRng);
                }
            }
            else
            {
                RandomizeExternalNPC(rng);
            }
        }

        private void RandomizeExternalNPC(Rng rng)
        {
            if (_emds.Count == 0)
                return;

            _logger.WriteHeading("Adding additional NPCs:");
            var externals = _emds
                .Where(x => SelectedActors.Contains(x.Actor))
                .ToEndlessBag(rng);

            var actorMap = new Dictionary<string, ExternalCharacter>();
            for (var i = 0; i < 256; i++)
            {
                var enemyType = (byte)i;
                if (!_npcHelper.IsSpareSlot(enemyType))
                    continue;

                ExternalCharacter newActor;
                var originalActor = _npcHelper.GetActor(enemyType);
                if (originalActor != null && _config.Game == 1)
                {
                    if (!actorMap.TryGetValue(originalActor, out newActor))
                    {
                        newActor = externals.Next();
                        actorMap[originalActor] = newActor;
                    }
                }
                else
                {
                    newActor = externals.Next();
                }
                SetEm(enemyType, newActor, rng);
            }
        }

        void SetEm(byte id, ExternalCharacter ec, Rng rng)
        {
            var emdFileName = _config.Game switch
            {
                1 => Path.Combine("enemy", $"em1{id:X3}.emd"),
                2 => Path.Combine($"pl{_config.Player}", $"emd{_config.Player}", $"em{_config.Player}{id:X2}.emd"),
                3 => Path.Combine($"ROOM", $"EMD", $"EM{id:X2}.EMD"),
                _ => null,
            };
            if (emdFileName == null)
                return;

            var originalEmdPath = _fileRepository.GetDataPath(emdFileName);
            var targetEmdPath = _fileRepository.GetModPath(emdFileName);
            if (!_fileRepository.Exists(originalEmdPath))
                return;

            _npcHelper.CreateEmdFile(id, ec.EmPath, originalEmdPath, targetEmdPath, _dataManager, _fileRepository, rng);
            _extraNpcMap[id] = ec.Actor;
            _logger.WriteLine($"Enemy 0x{id:X2} becomes {ec.Actor}");
        }

        private void RandomizeRooms(Rng rng)
        {
            _logger.WriteHeading("Randomizing Characters:");
            foreach (var rdt in _gameData.Rdts)
            {
                RandomizeRoom(rng.NextFork(), rdt);
            }
        }

        private void RandomizeRoom(Rng rng, RandomizedRdt rdt)
        {
            var npcRng = rng.NextFork();

            var defaultIncludeTypes = _npcHelper.GetDefaultIncludeTypes(rdt)
                .Shuffle(rng)
                .DistinctBy(x => _npcHelper.GetActor(x))
                .ToArray();
            if (rng.Next(0, 8) != 0)
            {
                // Make it rare for player to also be an NPC
                var newDefaultIncludeTypes = defaultIncludeTypes
                    .Where(x => GetActor(x) != _playerActors[0])
                    .ToArray();
                if (newDefaultIncludeTypes.Length != 0)
                {
                    defaultIncludeTypes = newDefaultIncludeTypes;
                }
            }

            var room = _map.GetRoom(rdt.RdtId);
            var npcs = room?.Npcs;
            if (npcs == null)
                npcs = new[] { new MapRoomNpcs() };
            npcs = npcs
                .Where(x => (x.Player == null || x.Player == _config.Player) &&
                            (x.Scenario == null || x.Scenario == _config.Scenario) &&
                            (x.DoorRando == null || x.DoorRando == _config.RandomDoors))
                .ToArray();

            var offsetToTypeMap = new Dictionary<int, byte>();
            var idToTypeMap = new Dictionary<byte, byte>();
            foreach (var cutscene in npcs.GroupBy(x => x.Cutscene))
            {
                var actorToNewActorMap = RandomizeCharacters(rdt, npcRng, defaultIncludeTypes, cutscene.ToArray(), offsetToTypeMap, idToTypeMap);
                var pc = cutscene.First().PlayerActor ?? _originalPlayerActor[0]!;
                if (pc == _originalPlayerActor[0] || _config.RandomDoors)
                {
                    actorToNewActorMap[pc] = _playerActors[0]!;
                    pc = _playerActors[0];
                }
                else
                {
                    actorToNewActorMap[pc] = _playerActors[1];
                }

                _voiceRandomiser?.SetRoomVoiceMap(rdt.RdtId, cutscene.Key, pc!, actorToNewActorMap);
            }

            foreach (var enemy in rdt.Enemies)
            {
                if (offsetToTypeMap.TryGetValue(enemy.Offset, out var newType))
                {
                    var npc = npcs.FirstOrDefault(x => x.IncludeOffsets == null || x.IncludeOffsets.Contains(enemy.Offset));
                    if (npc == null || npc.EmrScale != false)
                    {
                        var scale = GetEmrScale(enemy.Type, newType);
                        if (scale != 1)
                        {
                            ScaleEMRs(rdt, enemy.Id, scale);
                        }
                    }
                    enemy.Type = newType;
                }
            }
        }

        private double GetEmrScale(byte oldType, byte newType)
        {
            // Currently only supported in RE 2
            if (_config.Game != 2)
                return 1;

            var (oldTypeOrig, _) = GetNpcHeight(oldType);
            var (_, newTypeNew) = GetNpcHeight(newType);
            var scale = Extensions2.GetScale(oldTypeOrig, newTypeNew);
            return scale;
        }

        private (short, short) GetNpcHeight(byte type)
        {
            if (!_npcHeight.TryGetValue(type, out var height))
            {
                var player = _config.Player;
                var emdFileName = $"pl{player}/emd{player}/em{player}{type:X2}.emd";
                var originalEmdPath = _fileRepository.GetDataPath(emdFileName);
                var moddedEmdPath = _fileRepository.GetModPath(emdFileName);
                var originalEmdFile = new EmdFile(BioVersion.Biohazard2, originalEmdPath);
                var originalHeight = originalEmdFile.GetHeight();
                var moddedHeight = originalHeight;
                if (File.Exists(moddedEmdPath))
                {
                    var moddedEmdFile = new EmdFile(BioVersion.Biohazard2, moddedEmdPath);
                    moddedHeight = moddedEmdFile.GetHeight();
                }
                height = (originalHeight, moddedHeight);
                _npcHeight[type] = height;
            }
            return height;
        }

        private void ScaleEMRs(RandomizedRdt rdt, byte id, double scale)
        {
            EmrFlags flags = 0;
            switch (id)
            {
                case 0:
                    flags = EmrFlags.Entity0;
                    break;
                case 1:
                    flags = EmrFlags.Entity1;
                    break;
                case 255:
                    flags = EmrFlags.Partner;
                    break;
                default:
                    break;
            }
            if (flags != 0)
            {
                Re2Randomiser.ScaleEmrY(_logger, rdt, flags, scale);
            }
        }

        private Dictionary<string, string> RandomizeCharacters(RandomizedRdt rdt, Rng rng, byte[] defaultIncludeTypes, MapRoomNpcs[] npcs, Dictionary<int, byte> offsetToTypeMap, Dictionary<byte, byte> idToTypeMap)
        {
            var actorToNewActorMap = new Dictionary<string, string>();
            foreach (var npc in npcs)
            {
                if (npc.Player != null && npc.Player != _config.Player)
                    continue;
                if (npc.Scenario != null && npc.Scenario != _config.Scenario)
                    continue;
                if (npc.DoorRando != null && npc.DoorRando != _config.RandomDoors)
                    continue;

                var supportedNpcs = npc.IncludeTypes?.Select(x => (byte)x).ToArray() ?? defaultIncludeTypes;
                if (npc.ExcludeTypes != null)
                {
                    supportedNpcs = supportedNpcs.Except(npc.ExcludeTypes.Select(x => (byte)x)).ToArray();
                }
                if (supportedNpcs.Length == 0)
                {
                    continue;
                }

                var selectedNpcs = supportedNpcs
                    .Where(x => SelectedActors.Contains(GetActor(x) ?? ""))
                    .ToArray();
                if (selectedNpcs.Length != 0)
                {
                    supportedNpcs = selectedNpcs;
                }
                supportedNpcs = supportedNpcs.Distinct().ToArray();

                foreach (var enemyGroup in rdt.Enemies.GroupBy(x => x.Type))
                {
                    if (!_npcHelper.IsNpc(enemyGroup.Key))
                        continue;

                    supportedNpcs = supportedNpcs.Shuffle(rng);
                    {
                        // Avoid using the same actor again, unless we run out
                        var noDuplicatesSupportedNpcs = supportedNpcs
                            .Where(x => !actorToNewActorMap.Values.Contains(GetActor(x)))
                            .ToArray();
                        if (noDuplicatesSupportedNpcs.Length != 0)
                        {
                            supportedNpcs = noDuplicatesSupportedNpcs;
                        }
                    }

                    // HACK A specific enemy ID must always be Sherry, or never be Sherry otherwise there will be an EMR conflict
                    foreach (var enemyId in enemyGroup.Select(x => x.Id).Distinct())
                    {
                        if (idToTypeMap.TryGetValue(enemyId, out var alreadyType))
                        {
                            var actor = GetActor(alreadyType);
                            if (actor.IsSherryActor())
                            {
                                supportedNpcs = supportedNpcs.Where(x => GetActor(x).IsSherryActor()).ToArray();
                            }
                            else
                            {
                                supportedNpcs = supportedNpcs.Where(x => !GetActor(x).IsSherryActor()).ToArray();
                            }
                        }
                    }
                    if (supportedNpcs.Length == 0)
                    {
                        continue;
                    }

                    foreach (var enemy in enemyGroup)
                    {
                        if (npc.IncludeOffsets != null && !npc.IncludeOffsets.Contains(enemy.Offset))
                            continue;

                        var oldActor = GetActor(enemy.Type, originalOnly: true)!;
                        if (oldActor == null)
                            continue;

                        string newActor;
                        if (offsetToTypeMap.TryGetValue(enemy.Offset, out var newEnemyType))
                        {
                            newActor = GetActor(newEnemyType)!;
                        }
                        else
                        {
                            if (npc.Use != null)
                            {
                                var parts = npc.Use.Split(';');
                                var rdtId = RdtId.Parse(parts[0]);
                                var rdtOffset = int.Parse(parts[1].Substring(2), System.Globalization.NumberStyles.HexNumber);
                                var altRdt = _gameData.GetRdt(rdtId);
                                if (altRdt != null)
                                {
                                    var altNpc = altRdt.Enemies.FirstOrDefault(x => x.Offset == rdtOffset);
                                    if (altNpc != null)
                                    {
                                        newEnemyType = altNpc.Type;
                                    }
                                }
                            }
                            else
                            {
#if ALWAYS_SWAP_NPC
                                var newEnemyTypeIndex = Array.FindIndex(supportedNpcs, x => GetActor(x) != oldActor);
                                newEnemyType = newEnemyTypeIndex == -1 ? supportedNpcs[0] : supportedNpcs[newEnemyTypeIndex];
#else
                                newEnemyType = supportedNpcs[0];
#endif
                            }
                            newActor = GetActor(newEnemyType)!;
                            _logger.WriteLine($"{rdt.RdtId}:{enemy.Id} (0x{enemy.Offset:X}) [{_npcHelper.GetNpcName(enemy.Type)}] becomes [{_npcHelper.GetNpcName(newEnemyType)} ({newActor})]");
                            offsetToTypeMap[enemy.Offset] = newEnemyType;
                            idToTypeMap[enemy.Id] = newEnemyType;
                        }
                        actorToNewActorMap[oldActor] = newActor;
                    }
                }
            }
            return actorToNewActorMap;
        }

        public string? GetActor(byte enemyType, bool originalOnly = false)
        {
            if (!originalOnly && _extraNpcMap.TryGetValue(enemyType, out var actor))
                return actor;
            return _npcHelper.GetActor(enemyType);
        }

        [DebuggerDisplay("{Actor}")]
        private readonly struct ExternalCharacter
        {
            public byte EmId { get; }
            public string EmPath { get; }
            public string Actor { get; }

            public ExternalCharacter(byte emId, string emPath, string actor)
            {
                EmId = emId;
                EmPath = emPath;
                Actor = actor;
            }
        }
    }
}
