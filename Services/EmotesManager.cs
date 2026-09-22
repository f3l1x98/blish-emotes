using Blish_HUD;
using Blish_HUD.Modules.Managers;
using felix.BlishEmotes.Services;
using felix.BlishEmotes.Services.Catalog;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Gw2SharpLocale = Gw2Sharp.WebApi.Locale;

namespace felix.BlishEmotes
{
    class EmotesManager
    {
        private static readonly Logger Logger = Logger.GetLogger<EmotesManager>();
        public bool IsEmoteSynchronized { get; set; } = false;

        public bool IsEmoteTargeted { get; set; } = false;

        private ContentsManager ContentsManager;
        private ModuleSettings Settings;
        private CatalogManager CatalogManager;

        // Cache mapping ids to objects
        private Dictionary<string, Emote> emotes;

        // Replaced emote textures are held until module unload; immediate disposal would crash
        // open settings views or the radial menu which share the same Texture2D references.
        private List<Texture2D> _textureGraveyard = new List<Texture2D>();

        // Current overlay locale, kept in sync via UserLocaleChanged
        private CultureInfo _currentCulture;

        /// <summary>The current overlay locale, for resolving catalog names (e.g. auto-created category names).</summary>
        internal CultureInfo CurrentLocale => _currentCulture;

        public EmotesManager(ContentsManager contentsManager, ModuleSettings settings, CatalogManager catalogManager)
        {
            ContentsManager = contentsManager;
            Settings = settings;
            CatalogManager = catalogManager;
            emotes = new Dictionary<string, Emote>();
            _currentCulture = CurrentCulture();
            GameService.Overlay.UserLocaleChanged += OnLocaleChanged;
        }

        private void OnLocaleChanged(object sender, ValueEventArgs<CultureInfo> eventArgs)
        {
            _currentCulture = eventArgs.Value;
            UpdateEmoteLabels();
            Logger.Debug("Updated emote labels");
        }

        private static CultureInfo CurrentCulture()
        {
            try
            {
                switch (GameService.Overlay.UserLocale.Value)
                {
                    case Gw2SharpLocale.German: return new CultureInfo("de");
                    case Gw2SharpLocale.Spanish: return new CultureInfo("es");
                    case Gw2SharpLocale.French: return new CultureInfo("fr");
                    case Gw2SharpLocale.Korean: return new CultureInfo("ko");
                    case Gw2SharpLocale.Chinese: return new CultureInfo("zh");
                    default: return new CultureInfo("en");
                }
            }
            catch (Exception)
            {
                return CultureInfo.InvariantCulture;
            }
        }

        private void UpdateEmoteLabels()
        {
            foreach (var emote in emotes.Values)
            {
                emote.UpdateLabel(_currentCulture);
            }
        }

        /// <summary>Loads the emotes from a (cached) catalog; textures come from the cache directory.</summary>
        public void LoadFromCatalog(CatalogData catalog)
        {
            UpdateAll(BuildEmotes(catalog).Values.ToList());
        }

        /// <summary>
        /// Applies a fetched catalog live: build the new emote set from the cache, carry over the
        /// lock state, then swap it in. Must run on the game thread (textures need the GraphicsDevice).
        /// Replaced textures are sent to the graveyard and disposed only at module unload.
        /// </summary>
        public void ApplyCatalog(CatalogData catalog)
        {
            var previousEmotes = new Dictionary<string, Emote>(emotes);
            var built = BuildEmotes(catalog);
            foreach (var emote in built.Values)
            {
                if (previousEmotes.TryGetValue(emote.Id, out Emote previous) && previous.Locked)
                {
                    emote.Locked = true;
                }
            }
            GraveyardAndUpdateAll(built.Values.ToList());
        }

        /// <summary>Builds the emote list from a catalog without swapping the live set.</summary>
        internal List<Emote> BuildCatalogEmotes(CatalogData catalog)
        {
            return BuildEmotes(catalog).Values.ToList();
        }

        /// <summary>
        /// Swaps the live emote set for <paramref name="newEmotes"/>, moving replaced textures to
        /// the graveyard so they stay alive while settings views or the radial may still reference
        /// them. Shared textures that are still in use by the new set (e.g. the missing-texture
        /// fallback) are not moved.
        /// </summary>
        internal void GraveyardAndUpdateAll(List<Emote> newEmotes)
        {
            var newTextures = new HashSet<Texture2D>(newEmotes.Select(emote => emote.Texture));
            foreach (var emote in emotes.Values)
            {
                if (emote.Texture != null && !newTextures.Contains(emote.Texture))
                {
                    _textureGraveyard.Add(emote.Texture);
                }
            }
            UpdateAll(newEmotes);
        }

        private Dictionary<string, Emote> BuildEmotes(CatalogData catalog)
        {
            var built = new Dictionary<string, Emote>();
            foreach (var entry in catalog.Emotes)
            {
                var emote = new Emote()
                {
                    Id = entry.Id,
                    Command = entry.Command,
                    AlternativeCommands = entry.AlternativeCommands,
                    Names = entry.Names,
                    Categories = entry.Categories,
                };
                emote.Texture = LoadCatalogTexture(emote.Id);
                emote.UpdateLabel(_currentCulture);
                built.Add(emote.Id, emote);
            }
            return built;
        }

        private Texture2D LoadCatalogTexture(string emoteId)
        {
            try
            {
                // Read the bytes up front and decode from memory: no file handle is held during
                // the decode, so a background cache refresh can never race an open cache file.
                byte[] textureBytes = File.ReadAllBytes(CatalogManager.GetTextureFilePath(emoteId));
                using (var stream = new MemoryStream(textureBytes))
                {
                    return TextureUtil.FromStreamPremultiplied(stream);
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"Failed to load cached texture for {emoteId}.");
                Logger.Error(ex.Message);
                return ContentsManager.GetTexture(@"textures/missing-texture.png");
            }
        }

        public void UpdateAll(List<Emote> newEmotes)
        {
            emotes.Clear();
            foreach (var emote in newEmotes)
            {
                emotes.Add(emote.Id, emote);
            }
        }

        public List<Emote> GetAll()
        {
            return new List<Emote>(emotes.Values);
        }

        public List<Emote> GetRadial()
        {
            return emotes.Values.Where(el => this.Settings.EmotesRadialEnabledMap.ContainsKey(el) ? this.Settings.EmotesRadialEnabledMap[el].Value : true).ToList();
        }

        public void Unload()
        {
            GameService.Overlay.UserLocaleChanged -= OnLocaleChanged;
            // Dispose all Textures
            foreach (var emote in emotes.Values)
            {
                emote.Texture?.Dispose();
            }
            foreach (var texture in _textureGraveyard)
            {
                texture.Dispose();
            }
            _textureGraveyard.Clear();
        }

        public void SendEmoteCommand(Emote emote)
        {
            if (emote.Locked)
            {
                Logger.Debug("SendEmoteCommand: Emote locked.");
                return;
            }
            // Send emote command to chat if in game and map closed
            if (GameService.GameIntegration.Gw2Instance.IsInGame && !GameService.Gw2Mumble.UI.IsMapOpen)
            {
                string command = emote.Command;
                if (IsEmoteSynchronized)
                {
                    command += " *";
                }
                if (IsEmoteTargeted)
                {
                    command += " @";
                }
                Logger.Debug(command);
                GameService.GameIntegration.Chat.Send(command);
            }
        }
    }
}
