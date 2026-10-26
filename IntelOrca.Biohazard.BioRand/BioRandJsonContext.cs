using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using IntelOrca.Biohazard.BioRand.Events;

namespace IntelOrca.Biohazard.BioRand
{
    [JsonSourceGenerationOptions(
        PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
        ReadCommentHandling = JsonCommentHandling.Skip,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingDefault,
        WriteIndented = true)]
    [JsonSerializable(typeof(Dictionary<string, ulong>[]))]
    [JsonSerializable(typeof(Map))]
    [JsonSerializable(typeof(Dictionary<string, string[]>))]
    [JsonSerializable(typeof(EnemyRandomiser.EnemyPosition[]))]
    [JsonSerializable(typeof(Dictionary<string, CutsceneRoomInfo>))]
    [JsonSerializable(typeof(Dictionary<string, VoiceRandomiser.VoiceSample>))]
    internal partial class BioRandJsonContext : JsonSerializerContext
    {
    }

    internal static class BioRandJson
    {
        public static readonly JsonSerializerOptions Options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            ReadCommentHandling = JsonCommentHandling.Skip,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingDefault,
            WriteIndented = true,
            TypeInfoResolver = BioRandJsonContext.Default
        };

        public static T Deserialize<T>(string? json)
        {
            if (json == null)
                throw new ArgumentNullException(nameof(json));

            var typeInfo = (JsonTypeInfo<T>)Options.GetTypeInfo(typeof(T));
            return (T)JsonSerializer.Deserialize(json, typeInfo)!;
        }

        public static T Deserialize<T>(ReadOnlySpan<byte> json)
        {
            var typeInfo = (JsonTypeInfo<T>)Options.GetTypeInfo(typeof(T));
            return (T)JsonSerializer.Deserialize(json, typeInfo)!;
        }

        public static string Serialize<T>(T value)
        {
            var typeInfo = (JsonTypeInfo<T>)Options.GetTypeInfo(typeof(T));
            return JsonSerializer.Serialize(value, typeInfo);
        }
    }
}
