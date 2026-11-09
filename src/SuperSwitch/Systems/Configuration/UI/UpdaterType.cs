using System.Text.Json.Serialization;

namespace SuperSwitch.Ava.Systems.Configuration.UI
{
    [JsonConverter(typeof(JsonStringEnumConverter<UpdaterType>))]
    public enum UpdaterType
    {
        Off,
        PromptAtStartup,
        CheckInBackground
    }
}
