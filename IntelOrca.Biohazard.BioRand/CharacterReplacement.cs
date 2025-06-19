using System.Text.Json.Serialization;

namespace IntelOrca.Biohazard.BioRand
{
    internal class CharacterReplacement(int id, string character, int weapon)
    {
        public int Id => id;
        public string Path => character;

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public int Weapon => weapon;
    }
}
