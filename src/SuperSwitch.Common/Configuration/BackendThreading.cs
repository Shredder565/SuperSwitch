using System.Text.Json.Serialization;

namespace SuperSwitch.Common.Configuration
{
    [JsonConverter(typeof(JsonStringEnumConverter<BackendThreading>))]
    public enum BackendThreading
    {
        Auto,
        Off,
        On,
    }
}
