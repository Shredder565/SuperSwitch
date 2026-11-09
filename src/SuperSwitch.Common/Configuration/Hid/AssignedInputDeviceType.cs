using System.Text.Json.Serialization;

namespace SuperSwitch.Common.Configuration.Hid
{
    [JsonConverter(typeof(JsonStringEnumConverter<AssignedInputDeviceType>))]
    public enum AssignedInputDeviceType
    {
        Keyboard,
        Controller,
    }
}
