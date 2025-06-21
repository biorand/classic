using System.Text.Json.Serialization;

namespace IntelOrca.Biohazard.BioRand
{
    internal class CharacterReplacement(string character, int weapon)
    {
        public string Path => character;

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public int Weapon => weapon;
    }
}
