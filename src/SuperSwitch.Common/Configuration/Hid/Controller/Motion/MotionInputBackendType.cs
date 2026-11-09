using System.Text.Json.Serialization;

namespace SuperSwitch.Common.Configuration.Hid.Controller.Motion
{
    [JsonConverter(typeof(JsonStringEnumConverter<MotionInputBackendType>))]
    public enum MotionInputBackendType : byte
    {
        Invalid,
        GamepadDriver,
        CemuHook,
    }
}
