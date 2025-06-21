using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace IntelOrca.Biohazard.BioRand
{
    internal class CutsceneRandomizer
    {
        public void Randomize(IClassicRandomizerGeneratedVariation context)
        {
            var cutscenes = GetCutscenes(context);
            context.ModBuilder.Npcs = RandomizeCharacterSwaps(cutscenes, context.Rng.NextFork());
            context.ModBuilder.Voices = RandomizeVoiceClips(context.DataManager, cutscenes, context.Rng.NextFork());
        }

        private ImmutableArray<AvailableCharacter> GetAvailableCharacters(IClassicRandomizerGeneratedVariation context)
        {
            var result = ImmutableArray.CreateBuilder<AvailableCharacter>();
            foreach (var ch in context.Variation.Map.Characters)
            {
                var actor = ch.Actor;
                var replacementPath = context.ModBuilder.Characters.GetValueOrDefault(ch.Id)?.Path;
                if (replacementPath != null)
                {
                    actor = Path.GetFileName(replacementPath);
                }
                result.Add(new AvailableCharacter()
                {
                    Id = ch.Id,
                    Actor = actor,
                    Playable = ch.Playable
                });
            }
            return result.ToImmutable();
        }

        private ImmutableArray<Cutscene> GetCutscenes(IClassicRandomizerGeneratedVariation context)
        {
            var voices = VoiceTargetRepository.Load(context.DataManager, BioVersion.Biohazard1);
            var availableCharacters = GetAvailableCharacters(context);
            var defaultCharacterPool = availableCharacters.Where(x => !x.Playable).ToImmutableArray();
            var result = ImmutableArray.CreateBuilder<Cutscene>();
            var rooms = context.Variation.Map.Rooms;
            foreach (var room in rooms.Values)
            {
                var rdt = room.Rdts?.FirstOrDefault() ?? default;
                foreach (var cutscene in room.Cutscenes ?? [])
                {
                    var actors = ImmutableArray.CreateBuilder<CutsceneActor>();
                    foreach (var actor in cutscene.Actors)
                    {
                        var character = context.Variation.Map.Characters.FirstOrDefault(x => x.Id == actor.Character);
                        var isPlayable = character?.Playable ?? false;
                        actors.Add(new CutsceneActor()
                        {
                            GlobalId = actor.GlobalId,
                            Name = actor.Name,
                            Character = actor.Character == null ? null : availableCharacters.First(x => x.Id == actor.Character),
                            Offsets = [.. Map.ParseLiteralArray(actor.Offsets)],
                            AllowedCharacters = isPlayable
                                ? [availableCharacters.First(x => x.Id == actor.Character)]
                                : actor.Include.Length != 0
                                    ? actor.Include.Select(x => availableCharacters.First(y => y.Id == x)).ToImmutableArray()
                                    : actor.Exclude.Length != 0
                                        ? defaultCharacterPool.Where(x => !actor.Exclude.Contains(x.Id)).ToImmutableArray()
                                        : defaultCharacterPool,
                            VoiceClips = voices.GetTargets(context.Variation.PlayerIndex, rdt, cutscene.Id, actor.Name)
                        });
                    }

                    if (actors.Count == 0)
                        continue;

                    result.Add(new Cutscene()
                    {
                        Name = cutscene.Name,
                        Rdts = [.. room.Rdts ?? []],
                        Id = cutscene.Id,
                        Actors = actors.ToImmutable()
                    });
                }
            }
            return result.ToImmutable();
        }

        private ImmutableArray<NpcReplacement> RandomizeCharacterSwaps(ImmutableArray<Cutscene> cutscenes, Rng rng)
        {
            FixGlobalCharacters(cutscenes, rng);

            // Randomize all characters
            foreach (var cutscene in cutscenes)
            {
                foreach (var actor in cutscene.Actors)
                {
                    actor.RandomizeCharacter(rng);
                }
            }

            // Create output
            var npcs = ImmutableArray.CreateBuilder<NpcReplacement>();
            foreach (var cutscene in cutscenes)
            {
                foreach (var actor in cutscene.Actors)
                {
                    foreach (var rdtId in cutscene.Rdts)
                    {
                        foreach (var offset in actor.Offsets)
                        {
                            npcs.Add(new NpcReplacement()
                            {
                                RdtId = rdtId,
                                Offset = offset,
                                Type = actor.Character?.Id ?? 0
                            });
                        }
                    }
                }
            }
            return npcs.ToImmutable();
        }

        private void FixGlobalCharacters(ImmutableArray<Cutscene> cutscenes, Rng rng)
        {
            var globalGroups = cutscenes
                .SelectMany(x => x.Actors)
                .Where(x => x.GlobalId != null)
                .GroupBy(x => x.GlobalId)
                .ToArray();

            foreach (var group in globalGroups)
            {
                var actors = group.ToArray();
                var allowedCharacters = actors[0].AllowedCharacters.ToHashSet();
                foreach (var a in actors)
                {
                    allowedCharacters.IntersectWith(a.AllowedCharacters);
                }

                var finalAllowedCharacters = ImmutableArray.Create(allowedCharacters
                    .Shuffle(rng)
                    .First());
                foreach (var a in actors)
                {
                    a.AllowedCharacters = finalAllowedCharacters;
                }
            }
        }

        private ImmutableDictionary<string, string> RandomizeVoiceClips(
            DataManager dataManager,
            ImmutableArray<Cutscene> cutscenes,
            Rng rng)
        {
            var voiceSourceRepo = new VoiceSourceRepository();
            voiceSourceRepo.Scan(dataManager);
            var voiceBag = new VoiceBag(voiceSourceRepo, rng);
            var npcs = ImmutableArray.CreateBuilder<NpcReplacement>();
            var voices = ImmutableDictionary.CreateBuilder<string, string>();
            var globalSelection = new Dictionary<string, AvailableCharacter>();
            foreach (var cutscene in cutscenes)
            {
                var participants = cutscene.Actors.Select(x => x.Name).ToArray();
                foreach (var actor in cutscene.Actors)
                {
                    foreach (var voiceClip in actor.VoiceClips)
                    {
                        var target = voiceClip;
                        var source = voiceBag.GetNext(actor.Name, participants, voiceClip.Kind);
                        if (source == null)
                            continue;

                        voices[target.Identifier] = source.Path;
                    }
                }
            }
            return voices.ToImmutable();
        }

        [DebuggerDisplay("{Name}")]
        public class Cutscene
        {
            public required string Name { get; set; }
            public ImmutableArray<RdtId> Rdts = [];
            public int Id { get; set; }
            public ImmutableArray<CutsceneActor> Actors { get; set; } = [];
        }

        [DebuggerDisplay("{Name} = {Character}")]
        public class CutsceneActor
        {
            public string? GlobalId { get; set; } = "";
            public string Name { get; set; } = "";
            public AvailableCharacter? Character { get; set; }
            public ImmutableArray<AvailableCharacter> AllowedCharacters { get; set; } = [];
            public ImmutableArray<int> Offsets { get; set; } = [];
            public ImmutableArray<VoiceTarget> VoiceClips { get; set; } = [];

            public void RandomizeCharacter(Rng rng)
            {
                if (Character == null)
                    return;

                Character = AllowedCharacters.Length == 1
                    ? AllowedCharacters[0]
                    : AllowedCharacters[rng.Next(0, AllowedCharacters.Length)];
                Name = Character.Actor;
            }
        }

        public record AvailableCharacter
        {
            public int Id { get; set; }
            public required string Actor { get; set; }
            public bool Playable { get; set; }
        }
    }

    internal class VoiceTargetRepository(ImmutableArray<VoiceTarget> targets)
    {
        public ImmutableArray<VoiceTarget> Targets => targets;

        public ImmutableArray<VoiceTarget> GetTargets(int player, RdtId rdtId, int cutscene, string actor)
        {
            return Targets
                .Where(x => (x.Player == null || x.Player == player) && x.Rdt == rdtId && x.Cutscene == cutscene && x.Actor == actor)
                .ToImmutableArray();
        }

        public static VoiceTargetRepository Load(DataManager dataManager, BioVersion version)
        {
            var targets = ImmutableArray.CreateBuilder<VoiceTarget>();
            var voices = dataManager.GetJson<Dictionary<string, VoiceSample>>(version, "voice.json");
            foreach (var kvp in voices)
            {
                var path = kvp.Key;
                var slices = kvp.Value.Actors ?? [kvp.Value];
                var time = 0.0;
                foreach (var slice in slices)
                {
                    targets.Add(new VoiceTarget()
                    {
                        Path = path,
                        Rdt = kvp.Value.Rdt == null ? default : RdtId.Parse(kvp.Value.Rdt),
                        Player = kvp.Value.Player,
                        Cutscene = kvp.Value.Cutscene,
                        Actor = slice.Actor ?? "",
                        Range = new AudioRange(time, slice.Split),
                        Kind = slice.Kind
                    });
                    time = slice.Split;
                }
            }
            return new VoiceTargetRepository(targets.ToImmutable());
        }

        public class VoiceSample : VoiceSampleSlice
        {
            public string? Rdt { get; set; }
            public int Cutscene { get; set; }
            public int? Player { get; set; }
            public bool Strict { get; set; }
            public VoiceSampleSlice[]? Actors { get; set; }
        }

        public class VoiceSampleSlice
        {
            public double Split { get; set; }
            public string? Actor { get; set; }
            public string? Kind { get; set; }
        }
    }

    internal record class VoiceTarget
    {
        public int? Player { get; set; }
        public RdtId Rdt { get; set; }
        public int Cutscene { get; set; }
        public required string Actor { get; set; }
        public required string Path { get; set; }
        public AudioRange Range { get; set; }
        public string? Kind { get; set; }

        public string Identifier
        {
            get
            {
                if (Range.IsDefault)
                    return Path;

                return $"{Path}({Range.Start},{Range.End})";
            }
        }
    }

    internal readonly struct AudioRange(double start, double end) : IComparable<AudioRange>, IEquatable<AudioRange>
    {
        public double Start => start;
        public double End => end;
        public double Length => end - start;
        public bool IsDefault => start == 0 && end == 0;

        public int CompareTo(AudioRange other)
        {
            if (start < other.Start) return -1;
            if (start > other.Start) return 1;
            if (end < other.End) return -1;
            if (end > other.End) return 1;
            return 0;
        }
        public bool Equals(AudioRange other) => start == other.Start && end == other.End;
        public override bool Equals(object? obj) => obj is AudioRange range && Equals(range);
        public override int GetHashCode() => (start.GetHashCode() * 3) ^ (end.GetHashCode() * 13);
        public static bool operator ==(AudioRange left, AudioRange right) => left.Equals(right);
        public static bool operator !=(AudioRange left, AudioRange right) => !(left == right);
    }

    internal class VoiceSourceRepository()
    {
        private ImmutableArray<VoiceSource> Sources { get; set; }

        public void Scan(DataManager dataManager)
        {
            Sources = dataManager
                .GetDirectories("voice")
                .SelectMany(x =>
                {
                    var actor = Path.GetFileName(x);
                    var files = Directory.GetFiles(x);
                    return files.Select(y => (Actor: actor, Path: y));
                })
                .AsParallel()
                .Select(x => CreateSource(x.Actor, x.Path))
                .Where(x => x != null)
                .Select(x => x!)
                .ToImmutableArray();
        }

        public ImmutableArray<VoiceSource> GetSourcesFor(string actor)
        {
            return Sources.Where(x => x.Actor == actor).ToImmutableArray();
        }

        private VoiceSource? CreateSource(string actor, string path)
        {
            if (!path.EndsWith(".wav", StringComparison.OrdinalIgnoreCase) &&
                !path.EndsWith(".ogg", StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            var fileName = Path.GetFileName(path);

            var sample = new VoiceSource()
            {
                Path = path,
                Actor = actor,
                Range = default,
                Kind = GetThingsFromFileName(fileName, '_').FirstOrDefault(),
                Condition = GetConditionsFromFileName(fileName)
            };
            return sample;
        }

        private static IVoiceCondition? GetConditionsFromFileName(string fileName)
        {
            IVoiceCondition? result = null;
            var conditions = GetThingsFromFileName(fileName, '-');
            foreach (var condition in conditions)
            {
                if (condition.StartsWith("no", StringComparison.OrdinalIgnoreCase))
                {
                    var actor = condition[2..];
                    var c = new NotVoiceCondition(new ContainsVoiceCondition(actor));
                    result = result != null ? new AndVoiceCondition(result, c) : c;
                }
                else
                {

                    var actor = condition;
                    var c = new ContainsVoiceCondition(actor);
                    result = result != null ? new OrVoiceCondition(result, c) : c;
                }
            }
            return result;
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
    }

    internal record class VoiceSource
    {
        public required string Path { get; set; }
        public required string Actor { get; set; }
        public AudioRange Range { get; set; }
        public required string Kind { get; set; }
        public IVoiceCondition? Condition { get; set; }
    }

    internal class VoiceBag(VoiceSourceRepository repo, Rng rng)
    {
        private Dictionary<string, ImmutableArray<VoiceSource>> _actorToVoiceMap = new();

        public VoiceSource? GetNext(string actor, string[] participants, string? kind)
        {
            var pool = GetPool(actor);
            var applicable = pool
                .Where(x => x.Kind == kind)
                .Where(x => x.Condition?.Evaluate(participants) ?? true)
                .Shuffle(rng);
            return applicable.FirstOrDefault();
        }

        private ImmutableArray<VoiceSource> GetPool(string actor)
        {
            if (!_actorToVoiceMap.TryGetValue(actor, out var pool))
            {
                pool = repo.GetSourcesFor(actor);
                _actorToVoiceMap[actor] = pool;
            }
            return pool;
        }
    }

    internal interface IVoiceCondition
    {
        bool Evaluate(string[] participants);
    }

    internal readonly struct ContainsVoiceCondition(string actor) : IVoiceCondition
    {
        public bool Evaluate(string[] participants) => participants.Contains(actor);
        public override string ToString() => actor;
    }

    internal readonly struct NotVoiceCondition(IVoiceCondition inner) : IVoiceCondition
    {
        public bool Evaluate(string[] participants) => !inner.Evaluate(participants);
        public override string ToString() => $"!{inner}";
    }

    internal readonly struct OrVoiceCondition(IVoiceCondition left, IVoiceCondition right) : IVoiceCondition
    {
        public bool Evaluate(string[] participants) => left.Evaluate(participants) || right.Evaluate(participants);
        public override string ToString() => $"({left} || {right})";
    }

    internal readonly struct AndVoiceCondition(IVoiceCondition left, IVoiceCondition right) : IVoiceCondition
    {
        public bool Evaluate(string[] participants) => left.Evaluate(participants) && right.Evaluate(participants);
        public override string ToString() => $"({left} && {right})";
    }
}
