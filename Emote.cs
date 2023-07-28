using Newtonsoft.Json;

namespace felix.BlishEmotes
{
    public class Emote
    {
        [JsonProperty("id", Required = Required.Always)] public string id { get; set; }
        [JsonProperty("command", Required = Required.Always)] public string command { get; set; }
        [JsonProperty("locked", DefaultValueHandling = DefaultValueHandling.Populate)] public bool locked { get; set; } = false;
    }
}
