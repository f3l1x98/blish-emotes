using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using felix.BlishEmotes.Services.Catalog;
using Newtonsoft.Json;

namespace felix.BlishEmotes
{
    public class Emote : RadialBase
    {
        [JsonProperty("id", Required = Required.Always)] public string Id { get; set; }
        [JsonProperty("command", Required = Required.Always)] public string Command { get; set; }

        /// <summary>Wiki alias commands (e.g. barbecue: <c>/bbq</c>, <c>/grill</c>); catalog data, the primary <see cref="Command"/> is what gets sent.</summary>
        [JsonProperty("alternativeCommands")] public List<string> AlternativeCommands { get; set; }

        /// <summary>Per-locale names from the catalog (present on fetched emotes; embedded emotes use the resx instead).</summary>
        [JsonProperty("names")] public Dictionary<string, string> Names { get; set; }

        /// <summary>Catalog placement tags (category ids); parsed now, consumed by the catalog merge later.</summary>
        [JsonProperty("categories")] public List<string> Categories { get; set; }

        [JsonIgnore] private string _Label { get; set; } = "";

        [JsonIgnore] public override string Label
        {
            get => _Label;
        }

        /// <summary>
        /// Resolves the display name from the catalog (locale → en → title-cased id).
        /// </summary>
        public void UpdateLabel(CultureInfo locale)
        {
            _Label = Names != null
                ? CatalogNames.Resolve(Names, locale, Id)
                : CatalogNames.TitleCaseId(Id);
        }

        public bool EqualsCatalogEmote(CatalogEmote catalogEmote)
        {
            return this.Id == catalogEmote.Id
                   && this.Command == catalogEmote.Command
                   && ListEquals(this.AlternativeCommands, catalogEmote.AlternativeCommands)
                   && ListEquals(this.Categories, catalogEmote.Categories)
                   && NamesEquals(this.Names, catalogEmote.Names);
        }

        private static bool ListEquals(List<string> a, List<string> b)
        {
            if (ReferenceEquals(a, b))
            {
                return true;
            }
            if (a == null || b == null || a.Count != b.Count)
            {
                return false;
            }
            return a.SequenceEqual(b);
        }

        private static bool NamesEquals(Dictionary<string, string> a, Dictionary<string, string> b)
        {
            if (ReferenceEquals(a, b))
            {
                return true;
            }
            if (a == null || b == null || a.Count != b.Count)
            {
                return false;
            }
            foreach (var pair in a)
            {
                if (!b.TryGetValue(pair.Key, out string value) || pair.Value != value)
                {
                    return false;
                }
            }
            return true;
        }
    }
}
