using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using System.Runtime.Serialization;

namespace felix.BlishEmotes
{
    public enum Category
    {
        [EnumMember(Value = "greeting")]
        Greeting,
        [EnumMember(Value = "reaction")]
        Reaction,
        [EnumMember(Value = "fun")]
        Fun,
        [EnumMember(Value = "pose")]
        Pose,
        [EnumMember(Value = "dance")]
        Dance,
        [EnumMember(Value = "miscellaneous")]
        Miscellaneous,
    }
    public class Emote
    {
        [JsonProperty("id", Required = Required.Always)] public string Id { get; set; }
        [JsonProperty("command", Required = Required.Always)] public string Command { get; set; }
        [JsonProperty("locked", DefaultValueHandling = DefaultValueHandling.Populate)] public bool Locked { get; set; } = false;
        [JsonConverter(typeof(StringEnumConverter)), JsonProperty("category", Required = Required.Always)] public Category Category { get; set; }
    }
}
