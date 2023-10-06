using Blish_HUD;
using felix.BlishEmotes.Strings;
using Gw2Sharp.WebApi.V2.Models;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using System.Runtime.Serialization;

namespace felix.BlishEmotes
{
    public class Emote : RadialBase
    {
        [JsonProperty("id", Required = Required.Always)] public string Id { get; set; }
        [JsonProperty("command", Required = Required.Always)] public string Command { get; set; }

        [JsonIgnore] public string TextureRef => $"{Id}.png";
    }
}
