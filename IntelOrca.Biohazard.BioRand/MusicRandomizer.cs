using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;

namespace IntelOrca.Biohazard.BioRand
{
    internal class MusicRandomizer
    {
        public void Randomize(IClassicRandomizerGeneratedVariation context)
        {
            var rng = context.Rng.NextFork();
            var modBuilder = context.ModBuilder;
            var config = context.Configuration;
            var fullList = GetFullList(context.DataManager);
            var allowList = fullList
                .Where(x => config.GetValueOrDefault($"music/game/{x.Game}", false))
                .GroupBy(x => x.Tag)
                .ToDictionary(x => x.Key, x => x.ToEndlessBag(rng));

            var bgmMap = context.DataManager.GetJson<Dictionary<string, ImmutableArray<string>>>(BioVersion.Biohazard1, "bgm.json");
            foreach (var kvp in bgmMap)
            {
                var tag = kvp.Key;
                var musicFiles = kvp.Value;
                if (!allowList.TryGetValue(tag, out var bag))
                    continue;

                foreach (var m in musicFiles)
                {
                    if (m.StartsWith("!"))
                        continue;
                    var mm = m.StartsWith("*") ? m[1..] : m;
                    modBuilder.SetMusic(mm, bag.Next());
                }
            }
        }

        private static ImmutableArray<MusicSourceFile> GetFullList(DataManager dataManager)
        {
            var files = ImmutableArray.CreateBuilder<MusicSourceFile>();
            foreach (var gameDir in dataManager.GetDirectories("bgm"))
            {
                var game = Path.GetFileName(gameDir);
                foreach (var tagDir in dataManager.GetDirectories($"bgm/{gameDir}"))
                {
                    var tag = Path.GetFileName(tagDir);

                    var bgmFile = Directory.GetFiles(tagDir, "*", SearchOption.AllDirectories);
                    foreach (var subFile in bgmFile)
                    {
                        if (!SupportedMusicExtension(subFile))
                            continue;

                        files.Add(new MusicSourceFile(subFile, game, tag));
                    }
                }

            }
            return files.ToImmutable();
        }

        private static readonly ImmutableArray<string> g_supportedExtensions = [".ogg", ".wav"];
        private static bool SupportedMusicExtension(string path)
        {
            return g_supportedExtensions.Any(x => path.EndsWith(".ogg", StringComparison.OrdinalIgnoreCase));
        }
    }
}
