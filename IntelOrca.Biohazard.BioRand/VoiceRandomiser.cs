using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;

namespace IntelOrca.Biohazard.BioRand
{
    internal class VoiceRandomiser
    {
        private static VoiceLengthCache _voiceLengthCache = VoiceLengthCache.Default;
        private static VoiceSample[]? _customSamplesCache;

        private readonly BioVersion _version;
        private readonly RandoLogger _logger;
        private readonly FileRepository _fileRepository;
        private readonly RandoConfig _config;
        private readonly string _originalDataPath;
        private readonly string _modPath;
        private readonly GameData _gameData;
        private readonly Map _map;
        private readonly Rng _rng;
        private readonly List<VoiceSample> _pool = new List<VoiceSample>();
        private readonly HashSet<VoiceSample> _randomized = new HashSet<VoiceSample>();
        private readonly INpcHelper _npcHelper;
        private readonly DataManager _dataManager;
        private readonly string[] _playerActors;
        private readonly string[] _originalPlayerActor;

        private VoiceSample[] _voiceSamples = new VoiceSample[0];
        private List<VoiceSample> _uniqueSamples = new List<VoiceSample>();

        private List<RoomVoices> _roomVoices = new List<RoomVoices>();
        private RdtId? _lastRdtId;

        private List<int> _newStageVoiceCount = new List<int>();
        private int _randomEventCutsceneCount = 256;

        public HashSet<string> SelectedActors { get; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        public VoiceRandomiser(
            BioVersion version,
            RandoLogger logger,
            FileRepository fileRepository,
            RandoConfig config,
            string originalDataPath,
            string modPath,
            GameData gameData,
            Map map,
            Rng random,
            INpcHelper npcHelper,
            DataManager dataManager,
            string[]? playerActors)
        {
            _version = version;
            _logger = logger;
            _fileRepository = fileRepository;
            _config = config;
            _originalDataPath = originalDataPath;
            _modPath = modPath;
            _gameData = gameData;
            _map = map;
            _rng = random;
            _npcHelper = npcHelper;
            _dataManager = dataManager;
            _voiceSamples = AddToSelectionInternal(version, fileRepository);
            _originalPlayerActor = _npcHelper.GetPlayerActors(config.Player);
            _playerActors = playerActors ?? _originalPlayerActor;
        }

        public void AddToSelection(BioVersion version, FileRepository fileRepository) => AddToSelectionInternal(version, fileRepository);

        private VoiceSample[] AddToSelectionInternal(BioVersion version, FileRepository fileRepository)
        {
            var voiceJsonPath = _dataManager.GetPath(version, "voice.json");
            var voiceJson = _dataManager.GetText(voiceJsonPath)!;
            var extraSamples = LoadVoiceInfoFromJson(voiceJson);
            // _uniqueSamples.AddRange(extraSamples);
            return extraSamples;
        }

        public void SetRoomVoiceMap(RdtId rdtId, int cutscene, string pc, Dictionary<string, string> actorToActorMap)
        {
            _roomVoices.Add(new RoomVoices(rdtId, cutscene, pc, actorToActorMap));
        }

        private VoiceSample[] AddCustom()
        {
            var samples = new List<VoiceSample>();
            foreach (var actor in _dataManager.GetDirectories("hurt"))
            {
                var hurtDirectory = $"hurt/{actor}";
                var sampleFiles = _dataManager.GetFiles(hurtDirectory);
                foreach (var sampleFile in sampleFiles)
                {
                    if (!sampleFile.EndsWith(".wav", StringComparison.OrdinalIgnoreCase) &&
                        !sampleFile.EndsWith(".ogg", StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    var sample = new VoiceSample();
                    sample.BasePath = hurtDirectory;
                    sample.Path = sampleFile;
                    sample.Actor = actor;
                    sample.End = GetVoiceLength($"{hurtDirectory}/{sample.Path}");
                    sample.Kind = Path.GetFileNameWithoutExtension(sampleFile) == "3" ? "death" : "hurt";
                    samples.Add(sample);
                }
            }

            var voiceLines = _dataManager
                .GetDirectories("voice")
                .SelectMany(x =>
                {
                    var actor = Path.GetFileName(x);
                    return _dataManager
                        .GetFiles(Path.Combine("voice", x))
                        .Select(x => Path.Combine("voice", actor, x))
                        .Select(y => (Actor: actor, SampleFiles: y));
                })
                .AsParallel()
                .Select(x => ProcessSample(x.Actor, x.SampleFiles))
                .Where(x => x != null)
                .ToArray() as VoiceSample[];

            samples.AddRange(voiceLines);
            return samples.ToArray();
        }

        private VoiceSample? ProcessSample(string actor, string sampleFile)
        {
            if (!sampleFile.EndsWith(".wav", StringComparison.OrdinalIgnoreCase) &&
                !sampleFile.EndsWith(".ogg", StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            using (_logger.Progress.BeginTask(_config.Player, $"Scanning '{sampleFile}'"))
            {
                var fileName = Path.GetFileName(sampleFile);
                var conditions = GetThingsFromFileName(fileName, '-');
                var conditionsb = new StringBuilder();
                foreach (var condition in conditions)
                {
                    if (condition.StartsWith("no", StringComparison.OrdinalIgnoreCase))
                    {
                        if (conditionsb.Length != 0)
                            conditionsb.Append(" && ");
                        conditionsb.Append('!');
                        conditionsb.Append(condition, 2, condition.Length - 2);
                    }
                    else
                    {
                        if (conditionsb.Length != 0)
                            conditionsb.Append(" || ");
                        conditionsb.Append('@');
                        conditionsb.Append(condition);
                    }
                }

                var sample = new VoiceSample();
                sample.BasePath = Path.GetDirectoryName(sampleFile);
                sample.Path = fileName;
                sample.Actor = actor;
                sample.End = GetVoiceLength(sampleFile);
                sample.Kind = GetThingsFromFileName(fileName, '_').FirstOrDefault();
                sample.Condition = conditionsb.Length == 0 ? null : conditionsb.ToString();
                return sample;
            }
        }

        private static string[] GetThingsFromFileName(string filename, char symbol)
        {
            var filenameEnd = filename.LastIndexOf('.');
            if (filenameEnd == -1)
                filenameEnd = filename.Length;

            var result = new List<string>();
            var start = -1;
            for (int i = 0; i < filenameEnd; i++)
            {
                var c = filename[i];
                if (c == symbol && start == -1)
                {
                    start = i + 1;
                }
                else if (c == '_' || c == '-')
                {
                    if (start != -1)
                    {
                        result.Add(filename.Substring(start, i - start));
                        i--;
                        start = -1;
                    }
                }
            }
            if (start != -1)
            {
                result.Add(filename.Substring(start, filenameEnd - start));
            }
            return result.ToArray();
        }

        public void Randomise()
        {
            ScanCustomSamples();
            PopulateRoomVoices();
            RandomizeRooms(_rng.NextFork());
        }

        public int[] AllocateConversation(Rng rng, RdtId rdtId, int count, string[] speakers, string[] actors, string? kind = null)
        {
            if (speakers.Length == 0)
            {
                var pc = _playerActors[0];
                var randomActor = GetRandomActor(_rng, pc, kind!);
                if (randomActor == null)
                {
                    return new int[0];
                }

                speakers = new[] { randomActor };
            }

            var stage = rdtId.Stage;
            while (_newStageVoiceCount.Count <= stage)
            {
                _newStageVoiceCount.Add(0);
            }

            var vId = 100 + _newStageVoiceCount[stage];
            _newStageVoiceCount[stage] += count;

            var cutscene = _randomEventCutsceneCount++;
            for (var i = 0; i < count; i++)
            {
                var voiceSample = new VoiceSample();
                voiceSample.Path = $"pl{_config.Player}/voice/stage{stage + 1}/v{vId + i:000}.sap";
                voiceSample.Player = _config.Player;
                voiceSample.Cutscene = cutscene;
                RandomizeVoice(
                    rng,
                    voiceSample,
                    "n/a",
                    _rng.NextOf(speakers),
                    kind,
                    actors);
            }
            return Enumerable.Range(vId, count).ToArray();
        }

        private void ScanCustomSamples()
        {
            if (_customSamplesCache == null)
            {
                using (_logger.Progress.BeginTask(_config.Player, "Scanning voices"))
                {
                    _customSamplesCache = AddCustom();
                    _voiceLengthCache.Save();
                }
            }
            _uniqueSamples.AddRange(_customSamplesCache);
        }

        private void PopulateRoomVoices()
        {
            foreach (var rdt in _gameData.Rdts)
            {
                if (_roomVoices.Any(x => x.RdtId == rdt.RdtId))
                {
                    continue;
                }

                var room = _map.GetRoom(rdt.RdtId);
                var npcs = room?.Npcs ?? new[] { new MapRoomNpcs() };
                npcs = npcs
                    .Where(x => (x.Player == null || x.Player == _config.Player) &&
                                (x.Scenario == null || x.Scenario == _config.Scenario) &&
                                (x.DoorRando == null || x.DoorRando == _config.RandomDoors))
                    .ToArray();

                foreach (var cutscene in npcs.GroupBy(x => x.Cutscene))
                {
                    var actorToNewActorMap = GetActors(rdt, npcs).ToDictionary(x => x);
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
                    SetRoomVoiceMap(rdt.RdtId, cutscene.Key, pc!, actorToNewActorMap);
                }
            }
        }

        private HashSet<string> GetActors(RandomizedRdt rdt, MapRoomNpcs[] npcs)
        {
            var actorToNewActorMap = new HashSet<string>();
            foreach (var npc in npcs)
            {
                if (npc.Player != null && npc.Player != _config.Player)
                    continue;
                if (npc.Scenario != null && npc.Scenario != _config.Scenario)
                    continue;
                if (npc.DoorRando != null && npc.DoorRando != _config.RandomDoors)
                    continue;

                foreach (var enemy in rdt.Enemies)
                {
                    if (!_npcHelper.IsNpc(enemy.Type))
                        continue;
                    if (npc.IncludeOffsets != null && !npc.IncludeOffsets.Contains(enemy.Offset))
                        continue;

                    var oldActor = _npcHelper.GetActor(enemy.Type);
                    if (oldActor == null)
                        continue;

                    actorToNewActorMap.Add(oldActor);
                }
            }
            return actorToNewActorMap;
        }

        private void RandomizeRooms(Rng rng)
        {
            _logger.WriteHeading("Randomizing Voices:");
            _uniqueSamples = _uniqueSamples.OrderBy(x => x.SortString).ToList();
            _pool.AddRange(_uniqueSamples.Shuffle(rng.NextFork()));

            var roomVoices = _roomVoices.GroupBy(x => x.RdtId);
            foreach (var g in roomVoices)
            {
                RandomizeRoom(rng.NextFork(), g.Key, g.ToArray());
            }
        }

        private void RandomizeRoom(Rng rng, RdtId rdtId, RoomVoices[] roomVoices)
        {
            foreach (var v in roomVoices)
            {
                RandomizeVoices(rdtId, rng, v.Cutscene, v.PlayerActor, v.ActorToActorMap);
            }
        }

        private void RandomizeVoices(RdtId rdtId, Rng rng, int cutscene, string pc, Dictionary<string, string> actorToNewActorMap)
        {
            var first = true;

            string? kindActor = null;
            var actors = actorToNewActorMap.Values.ToArray();
            foreach (var sample in _voiceSamples)
            {
                if (sample.Player == _config.Player &&
                    sample.Cutscene == cutscene &&
                    sample.IsPlayedIn(rdtId))
                {
                    if (first)
                    {
                        if (_lastRdtId != rdtId)
                        {
                            _logger.WriteLine($"{rdtId}:");
                            _lastRdtId = rdtId;
                        }
                        _logger.WriteLine($"  cutscene #{cutscene} contains {string.Join(", ", actorToNewActorMap.Values)}");
                        first = false;
                    }

                    var actor = sample.Actor!;
                    var kind = sample.Kind;
                    if (IsOmnipresentKind(kind))
                    {
                        kindActor ??= GetRandomActor(_rng, pc, kind!) ?? actor;
                        RandomizeVoice(rng, sample, actor, kindActor, sample.Kind, actors);
                    }
                    else if (actorToNewActorMap.TryGetValue(actor, out var newActor))
                    {
                        RandomizeVoice(rng, sample, actor, newActor, kind, actors);
                    }
                    else
                    {
                        RandomizeVoice(rng, sample, actor, actor, kind, actors);
                    }
                }
            }
        }

        private static bool IsOmnipresentKind(string? kind)
        {
            return kind == "radio" || kind == "narrator" || kind == "announcer" || kind == "jumpscare";
        }

        private string? GetRandomActor(Rng rng, string pc, string kind)
        {
            var actors = _uniqueSamples
                .Where(x => x.Kind == kind && x.Actor != pc)
                .Select(x => x.Actor)
                .Distinct()
                .Shuffle(rng);
            return actors.FirstOrDefault();
        }

        private void RandomizeVoice(Rng rng, VoiceSample voice, string actor, string newActor, string? kind, string[] actors)
        {
            if (_randomized.Contains(voice))
                return;

            actors = actors.Select(TrimActorGame).ToArray();
            var randomVoice = GetRandomVoice(rng, newActor, kind, actors, voice.Length, voice.MaxLength);
            if (randomVoice != null)
            {
                voice.Replacement = randomVoice;
                _randomized.Add(voice);

                var logKindSource = kind == null ? "" : $",{kind}";
                var logKindTarget = randomVoice.Source.Kind == null ? "" : $",{randomVoice.Source.Kind}";
                var logClip = randomVoice.IsClipped ? $" ({randomVoice.Start:0.00}-{randomVoice.End:0.00})" : "";
                _logger.WriteLine($"    {voice.PathWithSapIndex} [{actor}{logKindSource}] becomes {randomVoice.PathWithSapIndex} [{newActor}{logKindTarget}]{logClip}");
            }
        }

        private static string TrimActorGame(string actor)
        {
            var fsIndex = actor.IndexOf('.');
            if (fsIndex == -1)
                return actor;

            return actor.Substring(0, fsIndex);
        }

        private VoiceSampleReplacement? GetRandomVoice(Rng rng, string actor, string? kind, string[] actors, double originalLength, double maxLength, bool refillPool = true)
        {
            for (int i = 0; i < _pool.Count; i++)
            {
                var sample = _pool[i];
                var result = CheckSample(sample, actor, kind, actors, originalLength, maxLength);
                if (result == SampleCheckResult.Bad)
                    continue;

                _pool.RemoveAt(i);
                if (result == SampleCheckResult.Clip)
                    return new VoiceSampleReplacement(sample, sample.NameClipped!.Start, sample.NameClipped!.End);
                else
                    return new VoiceSampleReplacement(sample, sample.Start, sample.End);
            }

            if (!refillPool)
            {
                if (_config.ReduceSilences && originalLength != 0)
                {
                    // Allow any length sample
                    return GetRandomVoice(rng, actor, kind, actors, 0, maxLength, true);
                }
                else if (kind == "scream")
                {
                    // Try find a death sound instead
                    return GetRandomVoice(rng, actor, "death", actors, originalLength, maxLength, true);
                }
                else if (kind != null)
                {
                    // Fallback to any kind of voice
                    return GetRandomVoice(rng, actor, null, actors, originalLength, maxLength, true);
                }
                else
                {
                    return null;
                }
            }

            // Do not add lines that are already in the pool.
            // Otherwise we can end up with many duplicates in a row
            var poolHash = _pool.ToHashSet();
            var newItems = _uniqueSamples
                .Where(x => x.Actor == actor)
                .Where(x => !poolHash.Contains(x))
                .Shuffle(rng);
            _pool.AddRange(newItems);
            return GetRandomVoice(rng, actor, kind, actors, originalLength, maxLength, refillPool: false);
        }

        private SampleCheckResult CheckSample(VoiceSample sample, string actor, string? kind, string[] actors, double originalLength, double maxLength)
        {
            if (maxLength != 0 && sample.Length > maxLength)
                return SampleCheckResult.Bad;
            if (_config.ReduceSilences && originalLength != 0 && sample.Length < originalLength - 1) // Allow 1 second of silence
                return SampleCheckResult.Bad;
            if (!_config.AllowAnyVoice && sample.Actor != actor.StripActorSkin())
                return SampleCheckResult.Bad;
            if (sample.Kind != kind)
                return SampleCheckResult.Bad;
            if (!sample.CheckConditions(actors))
                return sample.NameClipped != null ? SampleCheckResult.Clip : SampleCheckResult.Bad;
            return SampleCheckResult.Good;
        }

        private enum SampleCheckResult { Bad, Good, Clip }

        public void SetVoices()
        {
            var groups = _randomized.GroupBy(x => (x.Path, x.SapIndex));
            foreach (var group in groups)
            {
                var sampleOrder = group.OrderBy(x => x.Start).ToArray();
                if (sampleOrder.All(x => x.Replacement == null))
                    continue;

                var firstSample = sampleOrder[0];
                var dstPath = GetVoicePath(_modPath, firstSample);
                using (_logger.Progress.BeginTask(_config.Player, $"Writing '{dstPath}'"))
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(dstPath)!);

                    if (sampleOrder.Length == 1 && firstSample.Replacement?.IsClipped == false && firstSample.SapIndex == null && firstSample.Replacement.Source.SapIndex == null)
                    {
                        var srcPath = GetVoicePath(firstSample.Replacement.Source);
                        CopySample(srcPath, dstPath);
                    }
                    else
                    {
                        var builder = new WaveformBuilder();
                        foreach (var sample in sampleOrder)
                        {
                            var replacement = sample.Replacement;
                            if (replacement != null)
                            {
                                var sliceSrcPath = GetVoicePath(replacement.Source);
                                using (_logger.Progress.BeginTask(_config.Player, $"Reading '{sliceSrcPath}'"))
                                {
                                    if (replacement.Source.SapIndex != null)
                                    {
                                        builder.Append(sliceSrcPath, replacement.Source.SapIndex.Value, replacement.Start, replacement.End);
                                    }
                                    else
                                    {
                                        var stream = new MemoryStream(_dataManager.GetData(sliceSrcPath));
                                        builder.Append(sliceSrcPath, stream, replacement.Start, replacement.End);
                                    }
                                }
                                builder.AppendSilence(sample.Length - replacement.Length);
                            }
                            else
                            {
                                builder.AppendSilence(sample.Length);
                            }
                        }

                        if (firstSample.SapIndex != null)
                        {
                            // Copy existing sap file first, then modify the embedded .wavs
                            if (!File.Exists(dstPath))
                            {
                                File.Copy(GetVoicePath(firstSample), dstPath, true);
                            }
                            builder.SaveAt(dstPath, firstSample.SapIndex.Value);
                        }
                        else
                        {
                            builder.Save(dstPath);
                        }
                    }
                }
            }
        }

        private void CopySample(string srcPath, string dstPath)
        {
            var srcExtension = Path.GetExtension(srcPath);
            var dstExtension = Path.GetExtension(dstPath);
            if (srcExtension.Equals(dstExtension, StringComparison.OrdinalIgnoreCase) &&
                _config.Game != 1) // RE 1 doesn't support ADPCM which the source voice might be (i.e. from RE 3)
            {
                _fileRepository.Copy(srcPath, dstPath);
            }
            else
            {
                var builder = new WaveformBuilder();
                builder.Append(srcPath, _fileRepository.GetStream(srcPath));
                builder.Save(dstPath);
            }
        }

        private static string GetVoicePath(VoiceSample sample)
        {
            return Path.Combine(sample.BasePath, sample.Path);
        }

        private static string GetVoicePath(string basePath, VoiceSample sample)
        {
            return Path.Combine(basePath, sample.Path);
        }

        private VoiceSample[] LoadVoiceInfoFromJson(string json)
        {
            var voiceList = JsonSerializer.Deserialize<Dictionary<string, VoiceSample>>(json, new JsonSerializerOptions()
            {
                ReadCommentHandling = JsonCommentHandling.Skip,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });

            var samples = new List<VoiceSample>();
            foreach (var kvp in voiceList!)
            {
                var sample = kvp.Value;
                sample.BasePath = _fileRepository.DataPath;
                sample.Path = kvp.Key;
                if (sample.Path.Contains("#"))
                {
                    var parts = sample.Path.Split('#');
                    sample.Path = parts[0];
                    sample.SapIndex = int.Parse(parts[1]);
                }

                var totalLength = GetVoiceLength(_fileRepository.GetDataPath(sample.Path));
                if (sample.Strict)
                    sample.MaxLength = totalLength;
                if (sample.Actors != null)
                {
                    var start = 0.0;
                    foreach (var sub in sample.Actors)
                    {
                        var limited = true;
                        var end = sub.Split;
                        if (end == 0)
                        {
                            end = totalLength;
                            limited = false;
                        }
                        var slice = sample.CreateSlice(sub, start, end);
                        slice.Cutscene = sample.Cutscene;
                        slice.Limited = limited;
                        start = sub.Split;
                        samples.Add(slice);
                    }
                }
                else
                {
                    if (sample.End == 0)
                    {
                        sample.End = totalLength;
                        if (sample.Start == 0)
                        {
                            sample.Vanilla = true;
                        }
                    }
                    samples.Add(sample);
                }
            }

            return samples.ToArray();
        }

        private static VoiceSample[] RemoveDuplicateVoices(VoiceSample[] samples, string originalDataPath)
        {
            var distinct = new List<VoiceSample>();
            foreach (var group in samples.GroupBy(x => GetVoiceSize(originalDataPath, x)))
            {
                var pathToKeep = group.First().Path;
                foreach (var item in group)
                {
                    if (item.Path == pathToKeep)
                    {
                        distinct.Add(item);
                    }
                }
            }
            return distinct.ToArray();
        }

        private static int GetVoiceSize(string basePath, VoiceSample sample)
        {
            var path = GetVoicePath(basePath, sample);
            return (int)new FileInfo(path).Length;
        }

        private double GetVoiceLength(string path)
        {
            return _voiceLengthCache.GetVoiceLength(path, path =>
            {
                var data = _dataManager.GetData(path);
                return data == null ? _fileRepository.GetStream(path) : new MemoryStream(data);
            });
        }

        private class RoomVoices
        {
            public RdtId RdtId { get; }
            public int Cutscene { get; }
            public string PlayerActor { get; }
            public Dictionary<string, string> ActorToActorMap { get; }

            public RoomVoices(RdtId rdtId, int cutscene, string pc, Dictionary<string, string> actorToActorMap)
            {
                RdtId = rdtId;
                Cutscene = cutscene;
                PlayerActor = pc;
                ActorToActorMap = actorToActorMap;
            }
        }

        [DebuggerDisplay("[{Actor}] {Sample}")]
        private class VoiceInfo
        {
            public VoiceSample Sample { get; set; }
            public string Actor { get; set; }
            public string Kind { get; set; }

            public VoiceInfo(VoiceSample sample, string actor, string kind)
            {
                Sample = sample;
                Actor = actor;
                Kind = kind;
            }
        }

        [DebuggerDisplay("Actor = {Actor} RdtId = {Rdt} Path = {Path}")]
        private class VoiceSample
        {
            private RdtId[]? _cachedRdtIds;

            public string? BasePath { get; set; }
            public string? Path { get; set; }
            public int? SapIndex { get; set; }
            public string? Actor { get; set; }
            public string? Kind { get; set; }
            public string? Rdt { get; set; }
            public string[]? Rdts { get; set; }
            public int Player { get; set; }
            public int Cutscene { get; set; }
            public bool Strict { get; set; }

            public double Start { get; set; }
            public double End { get; set; }
            public double Length => End - Start;

            public string? Condition { get; set; }

            public double MaxLength { get; set; }
            public bool Vanilla { get; set; }
            public bool Limited { get; set; }
            public VoiceSampleSplit[]? Actors { get; set; }
            public VoiceSampleNameClip? NameClipped { get; set; }

            public VoiceSampleReplacement? Replacement { get; set; }

            public string? PathWithSapIndex => SapIndex == null ? Path : $"{Path}#{SapIndex}";

            public string SortString => $"{BasePath}/{PathWithSapIndex}:{Start}-{End}";

            public VoiceSample CreateSlice(VoiceSampleSplit sub, double start, double end)
            {
                var nameClip = sub.NameClipped;
                if (nameClip != null)
                {
                    if (nameClip.End == 0)
                        nameClip.End = end;
                    if (nameClip.Start == 0)
                        nameClip.Start = start;
                }

                return new VoiceSample()
                {
                    BasePath = BasePath,
                    Path = Path,
                    Actor = sub.Actor,
                    Rdt = Rdt,
                    Rdts = Rdts,
                    Player = Player,
                    Start = start,
                    End = end,
                    MaxLength = end - start,
                    Condition = sub.Condition,
                    Kind = sub.Kind,
                    NameClipped = nameClip
                };
            }

            public bool CheckConditions(string[] otherActors)
            {
                if (Condition == null)
                    return true;

                var conditions = Condition.Replace("&&", "&").Split('&');
                foreach (var singleCondition in conditions)
                {
                    var orResult = false;
                    var orConditions = singleCondition.Replace("||", "|").Split('|');
                    foreach (var orCondition in orConditions)
                    {
                        var result = true;
                        var sc = orCondition.Trim();
                        if (sc.StartsWith("!"))
                        {
                            var cc = sc.Substring(1);
                            if (otherActors.Contains(cc))
                            {
                                result = false;
                            }
                        }
                        else if (sc.StartsWith("@"))
                        {
                            var cc = sc.Substring(1);
                            if (!otherActors.Contains(cc))
                            {
                                result = false;
                            }
                        }
                        if (result)
                        {
                            orResult = true;
                            break;
                        }
                    }
                    if (!orResult)
                        return false;
                }

                return true;
            }

            public bool IsPlayedIn(RdtId rdtId)
            {
                if (_cachedRdtIds == null)
                {
                    if (Rdts != null)
                    {
                        _cachedRdtIds = Rdts.Select(RdtId.Parse).ToArray();
                    }
                    else if (Rdt != null)
                    {
                        _cachedRdtIds = new[] { RdtId.Parse(Rdt) };
                    }
                    else
                    {
                        _cachedRdtIds = new RdtId[0];
                    }
                }
                return _cachedRdtIds.Contains(rdtId);
            }
        }

        private class VoiceSampleSplit
        {
            public string? Actor { get; set; }
            public double Split { get; set; }
            public string? Condition { get; set; }
            public string? Kind { get; set; }
            public VoiceSampleNameClip? NameClipped { get; set; }
        }

        private class VoiceSampleNameClip
        {
            public double Start { get; set; }
            public double End { get; set; }
        }

        private class VoiceSampleReplacement
        {
            public VoiceSample Source { get; }
            public double Start { get; }
            public double End { get; }

            public string PathWithSapIndex => Source.PathWithSapIndex!;
            public double Length => End - Start;
            public bool IsClipped => Start != Source.Start || End != Source.End || !Source.Vanilla;

            public VoiceSampleReplacement(VoiceSample source, double start, double end)
            {
                Source = source;
                Start = start;
                End = end;
                if (end == 0)
                    End = Source.End;
            }
        }
    }
}
