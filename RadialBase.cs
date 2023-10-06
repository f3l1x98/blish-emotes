using Microsoft.Xna.Framework.Graphics;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace felix.BlishEmotes
{
    public abstract class RadialBase
    {
        [JsonIgnore] public Texture2D Texture { get; set; }
        [JsonIgnore] public bool Locked { get; set; } = false;
        // TODO add Label and use BuildsManager.s_moduleInstance.LanguageChanged -= ModuleInstance_LanguageChanged; to update Label for Emote
    }
}
