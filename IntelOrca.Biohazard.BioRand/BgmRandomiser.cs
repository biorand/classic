using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace IntelOrca.Biohazard.BioRand
{
    internal class BgmRandomiser
    {
        // Name prefix:
        //   ! = use, but don't set
        //   * = set, but don't use

        private const char CharUseNoSet = '!';
        private const char CharSetNoUse = '*';
        private const char CharVolume = '@';

        public const string TagBasement = "basement";
        public const string TagCalm = "calm";
        public const string TagClown = "clown";
        public const string TagCreepy = "creepy";
        public const string TagCountdown = "countdown";
        public const string TagDanger = "danger";
        public const string TagInstrument = "instrument";
        public const string TagOutside = "outside";
        public const string TagResults = "results";
        public const string TagSafe = "safe";
        public const string TagInstrumentBad = "instrument_bad";
        public const string TagInstrumentProgress = "instrument_progress";
        public const string TagInstrumentGood = "instrument_good";

        private readonly RandoLogger _logger;
        private readonly RandoConfig _config;
        private readonly FileRepository _fileRepo;
        private readonly BgmList _srcBgmList = new BgmList();
        private readonly string _bgmJson;
        private readonly string _bgmDirectory;
        private readonly bool _isWav;
        private readonly Rng _rng;
        private readonly DataManager _dataManager;
        private readonly Dictionary<string, string> _trackSetList = new Dictionary<string, string>();

        public float ImportVolume { get; set; } = 1.0f;

        public BgmRandomiser(
            RandoLogger logger,
            RandoConfig config,
            FileRepository fileRepo,
            string bgmDirectory,
            string bgmJson,
            bool isWav,
            Rng rng,
            DataManager dataManager)
        {
            _logger = logger;
            _config = config;
            _fileRepo = fileRepo;
            _bgmJson = bgmJson;
            _bgmDirectory = bgmDirectory;
            _isWav = isWav;
            _rng = rng;
            _dataManager = dataManager;
        }

        public void AddCutomMusicToSelection(string[] albums)
        {
            var bgmList = new BgmList();
            foreach (var album in albums)
            {
                var dir = _dataManager.GetPath("bgm", album);
                AddCustomMusicToSelection(bgmList, dir);
            }
            _srcBgmList.Union(bgmList);
        }

        private void AddCustomMusicToSelection(BgmList bgmList, string directory)
        {
            var tags = _dataManager.GetDirectories(directory);
            foreach (var tag in tags)
            {
                var files = _dataManager
                    .GetFiles(Path.Combine(directory, tag))
                    .Where(x => WaveformBuilder.IsSupportedExtension(x))
                    .ToArray();
                foreach (var file in files)
                {
                    var fullPath = Path.Combine(directory, tag, file);
                    bgmList.Add(tag, fullPath);
                }
            }
        }

        public void Randomise()
        {
            _logger.WriteHeading("Shuffling BGM:");
            _trackSetList.Clear();
            var bgmList = GetBgmList(_bgmJson);

            // Ensure these are randomized, but otherwise give custom priority
            Shuffle(bgmList, TagBasement, TagCreepy);
            Shuffle(bgmList, TagCountdown, TagCreepy);
            Shuffle(bgmList, TagSafe, TagCalm);
            Shuffle(bgmList, TagResults, TagCalm);

            // Shuffle tracks in each tag
            foreach (var tag in bgmList.Tags)
            {
                // Countdown only really works in a normal run
                if (tag == TagCountdown && _config.RandomDoors)
                    continue;

                Shuffle(bgmList, tag, tag);
            }

            // Clown music
            Shuffle(bgmList, TagInstrumentBad, TagClown, true);
            if (_rng.Next(0, 4) == 0)
            {
                Shuffle(bgmList, TagBasement, TagClown, true);
            }

            // Copy/convert all tracks
            var groups = _trackSetList.GroupBy(x => x.Value);
#if SINGLE_THREADED
            foreach (var g in groups)
            {
                var src = g.Key;
                var dst = g.Select(x => x.Key).ToArray();
                SetMusicTrack(src, dst);
            }
#else
            Parallel.ForEach(groups, g =>
            {
                var src = g.Key;
                var dst = g.Select(x => x.Key).ToArray();
                SetMusicTrack(src, dst);
            });
#endif
        }

        private void Shuffle(BgmList bgmList, string dstTag, string srcTag, bool overlay = false)
        {
            var dstList = bgmList
                .GetList(dstTag)
                .ToArray();
            var srcList = _srcBgmList
                .GetList(srcTag)
                .ToEndlessBag(_rng);

            if (srcList.Count == 0)
                return;

            if (overlay)
            {
                dstList = dstList
                    .Shuffle(_rng)
                    .Take(srcList.Count)
                    .ToArray();
            }

            var extension = _isWav ? ".wav" : ".sap";
            var dstDir = _bgmDirectory;
            Directory.CreateDirectory(dstDir);
            for (int i = 0; i < dstList.Length; i++)
            {
                var dstName = dstList[i];
                if (dstName.StartsWith("!"))
                    continue;
                if (dstName.StartsWith("*"))
                    dstName = dstName.Substring(1);

                var src = srcList.Next();
                if (Path.GetFileNameWithoutExtension(src) == dstName)
                {
                    src = srcList.Next();
                    if (Path.GetFileNameWithoutExtension(src) == dstName)
                    {
                        continue;
                    }
                }

                var dst = Path.Combine(dstDir, dstName + extension);
                PrepareMusicTrack(src, dst);

                _logger.WriteLine($"Setting {dstName} to {src}");
            }
        }

        private void PrepareMusicTrack(string src, string dst)
        {
            _trackSetList[dst] = src;
        }

        private void SetMusicTrack(string src, string[] dst)
        {
            if (dst.Length == 0)
                return;

            using (_logger.Progress.BeginTask(null, $"Processing BGM, '{src}'"))
            {
                var srcExtension = Path.GetExtension(src);
                var dstExtension = Path.GetExtension(dst[0]);
                if (ImportVolume == 1 && string.Equals(srcExtension, dstExtension, StringComparison.OrdinalIgnoreCase))
                {
                    for (int i = 0; i < dst.Length; i++)
                    {
                        using (_logger.Progress.BeginTask(null, $"Copying '{src}'"))
                        {
                            _fileRepo.Copy(src, dst[i]);
                        }
                    }
                }
                else
                {
                    using (_logger.Progress.BeginTask(null, $"Converting '{src}'"))
                    {
                        var volumeSep = src.IndexOf(CharVolume);
                        var volume = ImportVolume;
                        if (volumeSep != -1)
                        {
                            volume *= float.Parse(src.Substring(volumeSep + 1));
                            src = src.Substring(0, volumeSep);
                        }

                        using var stream = new MemoryStream(_dataManager.GetData(src));
                        var builder = new WaveformBuilder(volume: volume);
                        if (_config.Game == 1)
                        {
                            // RE1 can't handle very large .wav files, limit tracks to 3 minutes
                            builder.Append(src, stream, 0, 2.5 * 60);
                        }
                        else
                        {
                            builder.Append(src, stream);
                        }
                        builder.Save(dst[0]);
                    }
                    using (_logger.Progress.BeginTask(null, $"Copying '{dst[0]}'"))
                    {
                        for (int i = 1; i < dst.Length; i++)
                        {
                            _dataManager.Export(dst[0], dst[i]);
                        }
                    }
                }
            }
        }

        private BgmList GetBgmList(string bgmJson)
        {
            var dict = JsonSerializer.Deserialize<Dictionary<string, string[]>>(bgmJson, new JsonSerializerOptions()
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                ReadCommentHandling = JsonCommentHandling.Skip
            });
            if (dict == null)
                throw new Exception();
            return new BgmList(dict);
        }

        private class BgmList
        {
            private Dictionary<string, List<string>> _samples = new Dictionary<string, List<string>>();

            public string[] Tags => _samples.Keys.ToArray();

            public BgmList()
            {
            }

            public BgmList(Dictionary<string, string[]> samples)
            {
                _samples = samples
                    .ToDictionary(x => x.Key, x => x.Value.ToList());
            }

            public void Add(string tag, string path)
            {
                var list = GetList(tag);
                list.Add(path);
            }

            public void Union(BgmList other)
            {
                foreach (var kvp in other._samples)
                {
                    var list = GetList(kvp.Key);
                    foreach (var item in kvp.Value)
                    {
                        if (!string.IsNullOrEmpty(item))
                        {
                            list.Add(item);
                        }
                    }
                }
            }

            public List<string> GetList(string tag)
            {
                if (_samples.TryGetValue(tag, out var list))
                {
                    return list;
                }

                list = new List<string>();
                _samples.Add(tag, list);
                return list;
            }

            public void MakeFullPath(string bgmSubDirectory, string extension)
            {
                foreach (var kvp in _samples)
                {
                    var list = kvp.Value;
                    for (int i = 0; i < list.Count; i++)
                    {
                        var name = list[i];
                        if (name.Length > 0)
                        {
                            if (name[0] == CharSetNoUse)
                            {
                                list[i] = "";
                            }
                            else
                            {
                                if (name[0] == CharUseNoSet)
                                    name = name.Substring(1);
                                list[i] = Path.Combine(bgmSubDirectory, name) + extension;
                            }
                        }
                    }
                }
            }

            public void SetVolume(double volume)
            {
                if (volume == 1)
                    return;

                foreach (var kvp in _samples)
                {
                    var list = kvp.Value;
                    for (int i = 0; i < list.Count; i++)
                    {
                        var name = list[i];
                        if (name.Length > 0)
                        {
                            list[i] = name + CharVolume + volume.ToString("0.00");
                        }
                    }
                }
            }
        }
    }
}
