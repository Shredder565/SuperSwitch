using System.Text.Json.Serialization;

namespace SuperSwitch.Common.Configuration
{
    [JsonConverter(typeof(JsonStringEnumConverter<ScalingFilter>))]
    public enum ScalingFilter
    {
        Bilinear,
        Nearest,
        Fsr,
        Area,
    }
}
