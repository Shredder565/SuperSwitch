using System.Text.Json.Serialization;

namespace SuperSwitch.Common.Configuration
{
    [JsonConverter(typeof(JsonStringEnumConverter<GraphicsDebugLevel>))]
    public enum GraphicsDebugLevel
    {
        None,
        Error,
        Slowdowns,
        All,
    }
}
