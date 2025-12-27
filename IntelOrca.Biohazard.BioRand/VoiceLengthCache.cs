using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using NVorbis;

namespace IntelOrca.Biohazard.BioRand
{
    internal sealed class VoiceLengthCache
    {
        public static VoiceLengthCache Default => new();

        private const int CurrentVersion = 2;
        private ConcurrentDictionary<string, (long, double)> _voiceLengthCache = new(StringComparer.OrdinalIgnoreCase);

        private VoiceLengthCache()
        {
            Read();
        }

        public void Read()
        {
            _voiceLengthCache.Clear();
            try
            {
                var cachePath = GetCachePath();
                if (!File.Exists(cachePath))
                    return;

                using var fs = new FileStream(cachePath, FileMode.Open, FileAccess.Read);
                var br = new BinaryReader(fs, Encoding.UTF8);

                var version = br.ReadInt32();
                if (version != CurrentVersion)
                    return;

                br.ReadInt32();
                br.ReadInt32();
                var count = br.ReadInt32();
                var kvps = new KeyValuePair<string, (long, double)>[count];
                for (var i = 0; i < count; i++)
                {
                    var path = br.ReadString();
                    var size = br.ReadInt64();
                    var length = br.ReadDouble();
                    kvps[i] = new KeyValuePair<string, (long, double)>(path, (size, length));
                }
                _voiceLengthCache = new ConcurrentDictionary<string, (long, double)>(kvps);
            }
            catch (Exception)
            {
            }
        }

        public void Save()
        {
            try
            {
                var cachePath = GetCachePath();
                using var fs = new FileStream(cachePath, FileMode.Create, FileAccess.Write);
                var bw = new BinaryWriter(fs, Encoding.UTF8);

                var pairs = _voiceLengthCache.OrderBy(x => x.Key).ToArray();

                bw.Write((int)CurrentVersion);
                bw.Write((int)0);
                bw.Write((int)0);
                bw.Write(pairs.Length);
                foreach (var kvp in pairs)
                {
                    bw.Write(kvp.Key);
                    bw.Write(kvp.Value.Item1);
                    bw.Write(kvp.Value.Item2);
                }
            }
            catch (Exception)
            {
            }
        }

        private static string GetCachePath()
        {
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            var biorandPath = Path.Combine(appData, "biorand");
            var cachePath = Path.Combine(biorandPath, "voice.cache");
            return cachePath;
        }

        public double GetVoiceLength(string path, Func<string, Stream> getStream)
        {
            var stream = getStream(path);
            var streamSize = stream.Length;

            if (_voiceLengthCache.TryGetValue(path, out var result))
            {
                if (result.Item1 == streamSize)
                {
                    return result.Item2;
                }
            }

            var length = GetVoiceLengthInner(path, getStream);
            _voiceLengthCache.TryAdd(path, (streamSize, length));
            return length;
        }

        private static double GetVoiceLengthInner(string path, Func<string, Stream> getStream)
        {
            Stream? fs = null;
            try
            {
                fs = getStream(path);
                if (path.EndsWith(".sap", StringComparison.OrdinalIgnoreCase))
                {
                    fs.Position = 8;
                    var br = new BinaryReader(fs);
                    var magic = br.ReadUInt32();
                    fs.Position -= 4;
                    if (magic == 0x5367674F) // OGG
                    {
                        using var vorbis = new VorbisReader(new SlicedStream(fs, 8, fs.Length - 8), closeOnDispose: false);
                        return vorbis.TotalTime.TotalSeconds;
                    }
                    else
                    {
                        var decoder = new MSADPCMDecoder();
                        return decoder.GetLength(fs);
                    }
                }
                else if (path.EndsWith(".ogg"))
                {
                    using var vorbis = new VorbisReader(fs);
                    return vorbis.TotalTime.TotalSeconds;
                }
                else
                {
                    var decoder = new MSADPCMDecoder();
                    return decoder.GetLength(fs);
                }
            }
            catch (Exception ex)
            {
                throw new BioRandUserException($"Unable to process '{path}'. {ex.Message}");
            }
            finally
            {
                fs?.Dispose();
            }
        }
    }
}
