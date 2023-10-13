using Blish_HUD.Modules.Managers;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace felix.BlishEmotes.Services
{

    public enum Textures {
        ModuleIcon = 0, // emotes_icon.png
        Background = 1, // 156006.png
        SettingsIcon = 2, // 102390.png
        GlobalSettingsIcon = 3, // 155052.png
        CategorySettingsIcon = 4, // 156909.png
        HotkeySettingsIcon = 5, //156734+155150.png

        MissingTexture = 6, // missing-texture.png
        LockedTexture = 7, // 2107931.png
    }

    public class TexturesManager : IDisposable
    {
        private bool _disposed = false;
        private Dictionary<string, Texture2D> _textureCache;

        public TexturesManager(ContentsManager contentsManager)
        {
            _textureCache = new Dictionary<string, Texture2D>
            {
                { Textures.ModuleIcon.ToString(), contentsManager.GetTexture(@"textures\emotes_icon.png") },
                { Textures.Background.ToString(), contentsManager.GetTexture(@"textures\156006.png") },
                { Textures.SettingsIcon.ToString(), contentsManager.GetTexture(@"textures\102390.png") },
                { Textures.GlobalSettingsIcon.ToString(), contentsManager.GetTexture(@"textures\155052.png") },
                { Textures.CategorySettingsIcon.ToString(), contentsManager.GetTexture(@"textures\156909.png") },
                { Textures.HotkeySettingsIcon.ToString(), contentsManager.GetTexture(@"textures\156734+155150.png") },
                { Textures.MissingTexture.ToString(), contentsManager.GetTexture(@"textures\missing-texture.png") },
                { Textures.LockedTexture.ToString(), contentsManager.GetTexture(@"textures\2107931.png") }
            };

            // Load categories textures
            // TODO load all images using DirectoryManager
        }

        private void AssertAlive()
        {
            if (_disposed) throw new ObjectDisposedException("TexturesManager was disposed!");
        }

        public Texture2D GetTexture(Textures textureRef)
        {
            return GetTexture(textureRef.ToString());
        }
        public Texture2D GetTexture(string textureRef)
        {
            AssertAlive();
            return _textureCache[textureRef];
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                _disposed = true;

                foreach (var texture in _textureCache.Values)
                {
                    texture.Dispose();
                }
                _textureCache.Clear();
            }
        }
    }
}
