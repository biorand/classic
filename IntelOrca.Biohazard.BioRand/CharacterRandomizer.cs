using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;

namespace IntelOrca.Biohazard.BioRand
{
    internal class CharacterRandomizer
    {
        public void Randomize(IClassicRandomizerGeneratedVariation context)
        {
            var rng = context.Rng.NextFork();
            var characters = ImmutableDictionary.CreateBuilder<int, CharacterReplacement>();
            RandomizeProtagonist();
            RandomizeNpcs();
            context.ModBuilder.Characters = characters.ToImmutable();

            void RandomizeProtagonist()
            {
                var enabledCharacters = new EndlessBag<string>(rng, GetEnabledCharacters("protagonist"));
                foreach (var ch in context.Variation.Map.Characters)
                {
                    if (!ch.Playable)
                        continue;

                    characters.Add(ch.Id, new CharacterReplacement(enabledCharacters.Next(), 0));
                }
            }

            void RandomizeNpcs()
            {
                var weaponIds = context.Variation.Map.Items
                    .Where(x => x.Value.Kind.StartsWith("weapon/"))
                    .Select(x => x.Key)
                    .ToArray();

                var enabledCharacters = new EndlessBag<string>(rng, GetEnabledCharacters("npc"));
                foreach (var ch in context.Variation.Map.Characters)
                {
                    if (ch.Playable)
                        continue;

                    var weapon = ch.Weapon && weaponIds.Length != 0
                        ? rng.NextOf(weaponIds)
                        : 0;
                    characters.Add(ch.Id, new CharacterReplacement(enabledCharacters.Next(), weapon));
                }
            }

            string[] GetEnabledCharacters(string prefix)
            {
                return GetCharacters()
                    .Where(x => context.Configuration.GetValueOrDefault($"{prefix}/character/{Path.GetFileName(x)}", true))
                    .Shuffle(rng);
            }

            List<string> GetCharacters()
            {
                var result = new List<string>();
                var dataManager = context.DataManager;
                foreach (var basePath in dataManager.BasePaths)
                {
                    foreach (var pl in new[] { "pld0", "pld1" })
                    {
                        var pldDir = Path.Combine(basePath, "re1", pl);
                        foreach (var characterPath in dataManager.GetDirectories(pldDir))
                        {
                            result.Add(characterPath);
                        }
                    }
                }
                return result;
            }
        }
    }
}
