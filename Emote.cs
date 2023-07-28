using Newtonsoft.Json;

namespace felix.BlishEmotes
{
    public class Emote
    {
        [JsonProperty("id", Required = Required.Always)] public string Id { get; set; }
        [JsonProperty("command", Required = Required.Always)] public string Command { get; set; }
        [JsonProperty("locked", DefaultValueHandling = DefaultValueHandling.Populate)] public bool Locked { get; set; } = false;
    }
}
