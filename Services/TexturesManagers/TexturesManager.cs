using Blish_HUD;
using Blish_HUD.Modules.Managers;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;

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

    public abstract class TexturesManager : ITexturesManager
    {
        public static string[] TextureExtensionMasks = new[] { "*.png", "*.jpg", "*.bmp", "*.gif", "*.tif", "*.dds" };
        private static readonly Logger Logger = Logger.GetLogger<TexturesManager>();

        public string ModuleDataTexturesDirectory { get; private set; }

        private bool _disposed = false;
        private Dictionary<string, Texture2D> _textureCache;

        public TexturesManager(DirectoriesManager directoriesManager)
        {
            _textureCache = new Dictionary<string, Texture2D>();

            IReadOnlyList<string> registeredDirectories = directoriesManager.RegisteredDirectories;
            ModuleDataTexturesDirectory = Path.Combine(directoriesManager.GetFullDirectoryPath(registeredDirectories[0]), "textures");

            LoadTextures(_textureCache);
        }

        protected abstract void LoadTextures(in Dictionary<string, Texture2D> textureCache);

        /*public TexturesManager(ContentsManager contentsManager, DirectoriesManager directoriesManager)
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

            // Load dynamic textures
            IReadOnlyList<string> registeredDirectories = directoriesManager.RegisteredDirectories;
            ModuleDataTexturesDirectory = Path.Combine(directoriesManager.GetFullDirectoryPath(registeredDirectories[0]), "textures");

            this.LoadCategoriesTextures(directoriesManager);

            this.LoadEmotesTextures(directoriesManager);
        }

        private void LoadCategoriesTextures(DirectoriesManager directoriesManager)
        {
            LoadDynamicTextures(directoriesManager, TextureExtensionMasks);
        }

        private void LoadEmotesTextures(DirectoriesManager directoriesManager)
        {
            // TODO if category with same name as emote exists, emote texture will override category texture and be used for both
            LoadDynamicTextures(directoriesManager, new[] { "*.png" }, "emotes");
        }*/

        protected void LoadDynamicTextures(string[] textureExtensionMasks, string subdir = "")
        {
            string directory = Path.Combine(ModuleDataTexturesDirectory, subdir);
            if (!Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }
            var imageFiles = textureExtensionMasks.SelectMany((extension) => Directory.EnumerateFiles(directory, extension));
            foreach (var file in imageFiles)
            {
                var fileStream = File.OpenRead(file);
                string cacheKey = Path.GetFileNameWithoutExtension(file);
                _textureCache.Add(cacheKey, TextureUtil.FromStreamPremultiplied(fileStream));
            }
        }

        public void UpdateTexture(Textures textureRef, Texture2D newTexture)
        {
            UpdateTexture(textureRef.ToString(), newTexture);
        }
        public void UpdateTexture(string textureRef, Texture2D newTexture)
        {
            AssertAlive();
            if (!_textureCache.ContainsKey(textureRef))
            {
                Logger.Error($"Failed to update texture - No texture found for {textureRef}");
                return;
            }
            _textureCache[textureRef]?.Dispose();
            _textureCache[textureRef] = newTexture;
        }

        public Texture2D GetTexture(Textures textureRef)
        {
            return GetTexture(textureRef.ToString());
        }
        public Texture2D GetTexture(string textureRef)
        {
            AssertAlive();
            if (!_textureCache.ContainsKey(textureRef))
            {
                return null;
            }
            return _textureCache[textureRef];
        }

        private void AssertAlive()
        {
            if (_disposed) throw new ObjectDisposedException("TexturesManager was disposed!");
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
