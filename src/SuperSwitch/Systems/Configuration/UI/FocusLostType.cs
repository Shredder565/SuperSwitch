using System.Text.Json.Serialization;

namespace SuperSwitch.Ava.Systems.Configuration.UI
{
    [JsonConverter(typeof(JsonStringEnumConverter<FocusLostType>))]
    public enum FocusLostType
    {
        DoNothing,
        BlockInput,
        MuteAudio,
        BlockInputAndMuteAudio,
        PauseEmulation
    }
}
