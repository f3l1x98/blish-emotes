using Microsoft.Xna.Framework.Graphics;
using System;

namespace felix.BlishEmotes.Services.TexturesManagers
{
    internal interface ITexturesManager : IDisposable
    {
        string ModuleDataTexturesDirectory { get; }

        void UpdateTexture(string textureRef, Texture2D newTexture);

        Texture2D GetTexture(string textureRef);
    }
}
