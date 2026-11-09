using System.Text.Json.Serialization;

namespace SuperSwitch.Ava.Systems.Configuration
{
    [JsonSourceGenerationOptions(WriteIndented = true)]
    [JsonSerializable(typeof(ConfigurationFileFormat))]
    internal partial class ConfigurationJsonSerializerContext : JsonSerializerContext;
}
