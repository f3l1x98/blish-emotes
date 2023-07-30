using Blish_HUD;
using felix.BlishEmotes.Strings;
using Microsoft.Xna.Framework.Graphics;
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
        [JsonIgnore] public Texture2D Texture { get; set; }
    }

    public static class CategoryExtensions
    {
        private static readonly Logger Logger = Logger.GetLogger<Emote>();
        public static string Label(this Category category)
        {
            switch (category)
            {
                case Category.Greeting:
                    return Common.emote_categoryGreeting;
                case Category.Reaction:
                    return Common.emote_categoryReaction;
                case Category.Fun:
                    return Common.emote_categoryFun;
                case Category.Pose:
                    return Common.emote_categoryPose;
                case Category.Dance:
                    return Common.emote_categoryDance;
                case Category.Miscellaneous:
                    return Common.emote_categoryMiscellaneous;
                default:
                    Logger.Fatal("Missing category handling - Tried to retrieve label for " + category.ToString());
                    throw new System.Exception("Missing category handling");
            }
        }
    }
}
