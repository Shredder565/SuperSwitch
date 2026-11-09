using System.Text.Json.Serialization;

namespace SuperSwitch.Common.Configuration
{
    [JsonConverter(typeof(JsonStringEnumConverter<GraphicsBackend>))]
    public enum GraphicsBackend
    {
        Vulkan,
        OpenGl,
    }
}
