using System.Text.Json.Serialization;

namespace IntelOrca.Biohazard.BioRand
{
    public sealed class CharacterReplacement
    {
        public string Path { get; init; } = "";

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public int Weapon { get; init; }
    }
}
