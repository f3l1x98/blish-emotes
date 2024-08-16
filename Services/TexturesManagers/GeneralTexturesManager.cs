using Blish_HUD.Modules.Managers;
using Microsoft.Xna.Framework.Graphics;
using System.Collections.Generic;

namespace felix.BlishEmotes.Services.TexturesManagers
{
    public class GeneralTexturesManager : TexturesManager
    {
        private ContentsManager ContentsManager;

        public GeneralTexturesManager(ContentsManager contentsManager, DirectoriesManager directoriesManager) : base(directoriesManager)
        {
            this.ContentsManager = contentsManager;
        }

        protected override void LoadTextures(in Dictionary<string, Texture2D> textureCache)
        {
            textureCache.Add(Textures.ModuleIcon.ToString(), ContentsManager.GetTexture(@"textures\emotes_icon.png"));
            textureCache.Add(Textures.Background.ToString(), ContentsManager.GetTexture(@"textures\156006.png"));
            textureCache.Add(Textures.SettingsIcon.ToString(), ContentsManager.GetTexture(@"textures\102390.png"));
            textureCache.Add(Textures.GlobalSettingsIcon.ToString(), ContentsManager.GetTexture(@"textures\155052.png"));
            textureCache.Add(Textures.CategorySettingsIcon.ToString(), ContentsManager.GetTexture(@"textures\156909.png"));
            textureCache.Add(Textures.HotkeySettingsIcon.ToString(), ContentsManager.GetTexture(@"textures\156734+155150.png"));
            textureCache.Add(Textures.MissingTexture.ToString(), ContentsManager.GetTexture(@"textures\missing-texture.png"));
            textureCache.Add(Textures.LockedTexture.ToString(), ContentsManager.GetTexture(@"textures\2107931.png"));
        }
    }
}
