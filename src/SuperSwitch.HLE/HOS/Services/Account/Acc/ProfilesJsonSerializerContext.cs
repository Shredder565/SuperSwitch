using SuperSwitch.HLE.HOS.Services.Account.Acc.Types;
using System.Text.Json.Serialization;

namespace SuperSwitch.HLE.HOS.Services.Account.Acc
{
    [JsonSourceGenerationOptions(WriteIndented = true)]
    [JsonSerializable(typeof(ProfilesJson))]
    internal partial class ProfilesJsonSerializerContext : JsonSerializerContext
    {
    }
}
