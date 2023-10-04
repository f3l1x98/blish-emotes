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
    }
}
