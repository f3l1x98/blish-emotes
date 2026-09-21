using Blish_HUD;
using Blish_HUD.Modules.Managers;
using felix.BlishEmotes.Services.Catalog;
using felix.BlishEmotes.Exceptions;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace felix.BlishEmotes.Services
{
    /// <summary>
    /// Module-facing catalog service: loads the local cache (what the module runs from offline)
    /// and refreshes it with a background full-pull fetch. A fetch failure or a catalog that fails
    /// parsing/sanity-checking never replaces current data — the module just stays on the cache
    /// (or the embedded fallback) and retries on the next launch.
    /// </summary>
    class CatalogManager
    {
        private static readonly Logger Logger = Logger.GetLogger<CatalogManager>();

        /// <summary>
        /// Transport per the wayfinder "Transport choice for catalog delivery" decision:
        /// raw.githubusercontent.com, branch-qualified. The feature branch is used during
        /// development so the catalog can be fetched before it lands on master; the ship ticket
        /// flips this to master for the 2.0.0 release.
        /// TODO(10-ship-2.0.0): serve from "master".
        /// </summary>
        private const string CatalogUrl = "https://raw.githubusercontent.com/f3l1x98/blish-emotes/feature/remote-emotes-catalog/catalog/";

        private readonly CatalogCache _cache;

        public CatalogManager(DirectoriesManager directoriesManager)
        {
            IReadOnlyList<string> registeredDirectories = directoriesManager.RegisteredDirectories;
            string dataDirectory = directoriesManager.GetFullDirectoryPath(registeredDirectories[0]);
            _cache = new CatalogCache(dataDirectory);
        }

        /// <summary>The cached catalog, or null when there is no usable cache (fresh install / corrupted).</summary>
        public CatalogData LoadCachedCatalogOrNull()
        {
            try
            {
                if (!_cache.HasCache())
                {
                    return null;
                }
                var cached = _cache.Load();
                Logger.Info($"Loaded emote catalog from cache (version {cached.Manifest.Version}, {cached.Emotes.Count} emotes)");
                return cached;
            }
            catch (Exception e)
            {
                Logger.Warn("Failed to load cached catalog - falling back to embedded emote data.");
                Logger.Warn(e.Message);
                return null;
            }
        }

        /// <summary>
        /// Fetches the catalog and swaps it into the cache. Returns the fetched catalog, or null on
        /// any failure (network, hash mismatch, parse failure, invalid data) — callers keep their
        /// current data in that case.
        /// </summary>
        public async Task<CatalogData> FetchAsync()
        {
            try
            {
                using (var client = new CatalogClient(CatalogUrl))
                {
                    var fetched = await client.FetchAsync().ConfigureAwait(false);
                    _cache.Store(fetched);
                    Logger.Info($"Fetched emote catalog {fetched.Data.Manifest.Version} ({fetched.Data.Emotes.Count} emotes) and updated the cache");
                    return fetched.Data;
                }
            }
            catch (CatalogException e)
            {
                Logger.Warn($"Catalog fetch failed - staying on current emote data. {e.Message}");
                return null;
            }
            catch (Exception e)
            {
                Logger.Warn("Catalog fetch failed - staying on current emote data.");
                Logger.Warn(e.ToString());
                return null;
            }
        }

        /// <summary>Cache path of an emote's texture (<c>catalog/textures/&lt;id&gt;.png</c>).</summary>
        public string GetTextureFilePath(string emoteId)
        {
            return _cache.GetTextureFilePath(emoteId);
        }
    }
}
