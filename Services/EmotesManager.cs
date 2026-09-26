using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Blish_HUD;
using Blish_HUD.Modules.Managers;
using felix.BlishEmotes.Services;
using felix.BlishEmotes.Services.Catalog;
using Microsoft.Xna.Framework.Graphics;
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

        // Cache mapping ids to objects. Swapped by reference (never mutated in place) so
        // concurrent readers on the game thread always iterate a consistent snapshot.
        private Dictionary<string, Emote> emotes;

        // SHA-256 (manifest form, see CatalogHash) of the texture file each live emote's Texture2D
        // was decoded from; lets a catalog apply reuse live emotes whose texture file did not change.
        private readonly Dictionary<string, string> _textureHashesById = new Dictionary<string, string>();

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
            var newEmoteIds = new HashSet<string>(newEmotes.Select(emote => emote.Id));
            foreach (var emote in emotes.Values)
            {
                // Texture will be placed into graveyard if no newEmote with same id has been found.
                // (simpler than Texture equality check and Textures dont get updated anyway, and if they do restart will fix it)
                if (emote.Texture != null && !newEmoteIds.Contains(emote.Id))
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
                // Reuse the live instance — and with it its texture — when the entry's data and
                // its texture file are unchanged, so a one-emote update re-decodes nothing else.
                if (emotes.TryGetValue(entry.Id, out Emote existing) && IsUnchanged(existing, entry, catalog))
                {
                    built.Add(entry.Id, existing);
                    continue;
                }

                var emote = new Emote()
                {
                    Id = entry.Id,
                    Command = entry.Command,
                    AlternativeCommands = entry.AlternativeCommands,
                    Names = entry.Names,
                    Categories = entry.Categories,
                    Locked = existing?.Locked ?? false,
                };
                emote.Texture = LoadCatalogTexture(emote.Id);
                emote.UpdateLabel(_currentCulture);
                built.Add(emote.Id, emote);
            }
            return built;
        }

        /// <summary>True when the live <paramref name="existing"/> emote matches the catalog
        /// entry's data and the texture file the live texture was decoded from — the entry may be
        /// reused as-is. The rules live in the unit-tested <see cref="CatalogEmoteDiff"/>.</summary>
        private bool IsUnchanged(Emote existing, CatalogEmote entry, CatalogData catalog)
        {
            if (!existing.EqualsCatalogEmote(entry))
            {
                return false;
            }

            string manifestHash = null;
            catalog.Manifest?.Files?.TryGetValue(CatalogPaths.TextureFile(entry.Id), out manifestHash);
            string loadedHash = _textureHashesById.TryGetValue(entry.Id, out string hash) ? hash : null;
            return string.Equals(loadedHash, manifestHash, StringComparison.OrdinalIgnoreCase);
        }

        private Texture2D LoadCatalogTexture(string emoteId)
        {
            try
            {
                // Read the bytes up front and decode from memory: no file handle is held during
                // the decode, so a background cache refresh can never race an open cache file.
                byte[] textureBytes = File.ReadAllBytes(CatalogManager.GetTextureFilePath(emoteId));
                // Record which file version this texture was built from, so a later catalog apply
                // can reuse the emote unless the manifest references a new texture file.
                _textureHashesById[emoteId] = CatalogHash.Sha256Hex(textureBytes);
                using (var stream = new MemoryStream(textureBytes))
                {
                    return TextureUtil.FromStreamPremultiplied(stream);
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"Failed to load cached texture for {emoteId}.");
                Logger.Error(ex.Message);
                // No hash recorded: the next catalog apply treats the texture as changed and retries.
                _textureHashesById.Remove(emoteId);
                return ContentsManager.GetTexture(@"textures/missing-texture.png");
            }
        }

        public void UpdateAll(List<Emote> newEmotes)
        {
            var newEmotesById = new Dictionary<string, Emote>(newEmotes.Count);
            foreach (var emote in newEmotes)
            {
                newEmotesById.Add(emote.Id, emote);
            }
            // Swap instead of modify inorder to prevent inconsistent state.
            emotes = newEmotesById;

            // Keep the texture-hash bookkeeping aligned with the live id set.
            foreach (string staleId in _textureHashesById.Keys.Where(id => !newEmotesById.ContainsKey(id)).ToList())
            {
                _textureHashesById.Remove(staleId);
            }
        }

        /// <summary>True when <paramref name="emote"/> is the currently live instance for its id
        /// — e.g. an unchanged catalog entry reused by <see cref="BuildCatalogEmotes"/>. Its
        /// texture is still rendered, so a failed live apply must not dispose it.</summary>
        internal bool IsLiveEmote(Emote emote)
        {
            return emotes.TryGetValue(emote.Id, out Emote live) && ReferenceEquals(live, emote);
        }

        public List<Emote> GetAll()
        {
            return new List<Emote>(emotes.Values);
        }

        public List<string> GetEmoteNamesByIds(List<string> ids)
        {
            if (ids == null || ids.Count == 0)
            {
                return new List<string>();
            }
            return ids.Select(id =>
            {
                emotes.TryGetValue(id, out var emote);
                return emote?.Label ?? id;
            }).ToList();
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
