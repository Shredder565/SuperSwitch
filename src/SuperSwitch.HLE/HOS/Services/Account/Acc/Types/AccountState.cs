using System.Text.Json.Serialization;

namespace SuperSwitch.HLE.HOS.Services.Account.Acc
{
    [JsonConverter(typeof(JsonStringEnumConverter<AccountState>))]
    public enum AccountState
    {
        Closed,
        Open,
    }
}
