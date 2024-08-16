using Blish_HUD;
using Blish_HUD.Modules.Managers;
using Microsoft.Xna.Framework.Graphics;
using System.ComponentModel;

namespace felix.BlishEmotes.Services.TexturesManagers
{
    public enum Textures
    {
        [Description("emotes_icon.png")]
        ModuleIcon = 0,
        [Description("156006.png")]
        Background = 1,
        [Description("102390.png")]
        SettingsIcon = 2,
        [Description("155052.png")]
        GlobalSettingsIcon = 3,
        [Description("156909.png")]
        CategorySettingsIcon = 4,
        [Description("156734+155150.png")]
        HotkeySettingsIcon = 5,

        [Description("missing-texture.png")]
        MissingTexture = 6,
        [Description("2107931.png")]
        LockedTexture = 7,
    }

    public static class TexturesExtensions
    {
        public static string ToFileName(this Textures val)
        {
            DescriptionAttribute[] attributes = (DescriptionAttribute[])val
               .GetType()
               .GetField(val.ToString())
               .GetCustomAttributes(typeof(DescriptionAttribute), false);
            return attributes.Length > 0 ? attributes[0].Description : string.Empty;
        }
    }

    public class GeneralTexturesManager : TexturesManager
    {
        private static readonly Logger Logger = Logger.GetLogger<GeneralTexturesManager>();
        private ContentsManager ContentsManager;

        public GeneralTexturesManager(ContentsManager contentsManager, DirectoriesManager directoriesManager) : base(directoriesManager)
        {
            this.ContentsManager = contentsManager;
        }

        public override void LoadTextures()
        {
            if (_textureCache.Count != 0)
            {
                Logger.Info("Skipping LoadTextures due to already loaded.");
                return;
            }
            // TODO ContentsManager was null.
            // REASON: base() calls this and base() constructor is executed before constructor of this class
            _textureCache.Add(Textures.ModuleIcon.ToString(), ContentsManager.GetTexture(@"textures\emotes_icon.png"));
            _textureCache.Add(Textures.Background.ToString(), ContentsManager.GetTexture(@"textures\156006.png"));
            _textureCache.Add(Textures.SettingsIcon.ToString(), ContentsManager.GetTexture(@"textures\102390.png"));
            _textureCache.Add(Textures.GlobalSettingsIcon.ToString(), ContentsManager.GetTexture(@"textures\155052.png"));
            _textureCache.Add(Textures.CategorySettingsIcon.ToString(), ContentsManager.GetTexture(@"textures\156909.png"));
            _textureCache.Add(Textures.HotkeySettingsIcon.ToString(), ContentsManager.GetTexture(@"textures\156734+155150.png"));
            _textureCache.Add(Textures.MissingTexture.ToString(), ContentsManager.GetTexture(@"textures\missing-texture.png"));
            _textureCache.Add(Textures.LockedTexture.ToString(), ContentsManager.GetTexture(@"textures\2107931.png"));
        }
        public void UpdateTexture(Textures textureRef, Texture2D newTexture)
        {
            UpdateTexture(textureRef.ToString(), newTexture);
        }

        public Texture2D GetTexture(Textures textureRef)
        {
            return GetTexture(textureRef.ToString());
        }
    }
}
