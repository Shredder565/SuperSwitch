using System.Text.Json.Serialization;

namespace SuperSwitch.Ava.Common.Models.Amiibo
{
    [JsonSerializable(typeof(AmiiboJson))]
    public partial class AmiiboJsonSerializerContext : JsonSerializerContext;
}
