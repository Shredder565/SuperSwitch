using SuperSwitch.Common.Utilities;

namespace SuperSwitch.Ava.Systems.Configuration
{
    internal static class ConfigurationFileFormatSettings
    {
        public static readonly ConfigurationJsonSerializerContext SerializerContext = new(JsonHelper.GetDefaultSerializerOptions());
    }
}
