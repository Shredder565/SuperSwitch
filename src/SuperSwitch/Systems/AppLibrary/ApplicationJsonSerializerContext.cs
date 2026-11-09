using System.Text.Json.Serialization;

namespace SuperSwitch.Ava.Systems.AppLibrary
{
    [JsonSourceGenerationOptions(WriteIndented = true)]
    [JsonSerializable(typeof(ApplicationMetadata))]
    internal partial class ApplicationJsonSerializerContext : JsonSerializerContext
    {
    }
}
