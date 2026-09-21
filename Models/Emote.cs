using Blish_HUD;
using felix.BlishEmotes.Services.Catalog;
using felix.BlishEmotes.Strings;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Newtonsoft.Json;
using System.Collections.Generic;
using System.Globalization;
using System.Resources;

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

        [JsonIgnore] public string TextureRef => $"{Id}.png";

        [JsonIgnore] private string _Label { get; set; } = "";

        [JsonIgnore] public override string Label
        {
            get => _Label;
        }

        /// <summary>
        /// Resolves the display name: fetched emotes use their catalog names (locale → en →
        /// title-cased id); embedded emotes use the resx, falling back to the title-cased id.
        /// </summary>
        public void UpdateLabel(ResourceManager embeddedNames, CultureInfo locale)
        {
            if (Names != null)
            {
                _Label = CatalogNames.Resolve(Names, locale, Id);
            }
            else
            {
                _Label = embeddedNames.GetString(Id) ?? CatalogNames.TitleCaseId(Id);
            }
        }
    }
}
