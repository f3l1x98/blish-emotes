using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace felix.BlishEmotesList
{
    class Emote
    {
        [JsonProperty("id", Required = Required.Always)] public string id { get; set; }
        [JsonProperty("command", Required = Required.Always)] public string command { get; set; }
        [JsonProperty("locked", DefaultValueHandling = DefaultValueHandling.Populate)] public bool locked { get; set; } = false;
    }
}
