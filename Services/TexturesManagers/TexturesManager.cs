using Blish_HUD;
using Blish_HUD.Modules.Managers;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace felix.BlishEmotes.Services.TexturesManagers
{
    public abstract class TexturesManager : ITexturesManager
    {
        public static string[] TextureExtensionMasks = new[] { "*.png", "*.jpg", "*.bmp", "*.gif", "*.tif", "*.dds" };
        private static readonly Logger Logger = Logger.GetLogger<TexturesManager>();

        public string ModuleDataTexturesDirectory { get; private set; }

        private bool _disposed = false;
        protected Dictionary<string, Texture2D> _textureCache { get; private set; }

        public TexturesManager(DirectoriesManager directoriesManager)
        {
            _textureCache = new Dictionary<string, Texture2D>();

            IReadOnlyList<string> registeredDirectories = directoriesManager.RegisteredDirectories;
            ModuleDataTexturesDirectory = Path.Combine(directoriesManager.GetFullDirectoryPath(registeredDirectories[0]), "textures");
        }

        public abstract void LoadTextures();

        protected void LoadTexturesFromDirectory(string[] textureExtensionMasks, string subdir = "")
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
