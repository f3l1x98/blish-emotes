using Blish_HUD.Modules.Managers;
using Microsoft.Xna.Framework.Graphics;
using System.Collections.Generic;

namespace felix.BlishEmotes.Services.TexturesManagers
{
    public class CategoryTexturesManager : TexturesManager
    {

        public CategoryTexturesManager(DirectoriesManager directoriesManager) : base(directoriesManager)
        {
        }

        protected override void LoadTextures(in Dictionary<string, Texture2D> textureCache)
        {
            LoadDynamicTextures(TextureExtensionMasks);
        }
    }
}
