using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Blish_HUD;
using Blish_HUD.Modules.Managers;
using felix.BlishEmotes.Services.Catalog;

namespace felix.BlishEmotes.Services
{
    /// <summary>
    /// Module-facing catalog service: loads the local cache (what the module runs from offline) and
    /// refreshes it with a cheap conditional sync. A sync failure or a catalog that fails parsing
    /// or invariants never replaces current data and never updates the stored
    /// ETag, so the next check genuinely retries instead of 304-ing past a bad version.
    /// </summary>
    public class CatalogManager
    {
        private static readonly Logger Logger = Logger.GetLogger<CatalogManager>();

        /// <summary>
        /// Transport per the wayfinder "Transport choice for catalog delivery" decision:
        /// raw.githubusercontent.com, branch-qualified. The feature branch is used during
        /// development so the catalog can be fetched before it lands on master; the ship ticket
        /// flips this to master for the 2.0.0 release.
        /// TODO(10-ship-2-0-0): serve from "master".
        /// </summary>
        private const string CatalogUrl = "https://raw.githubusercontent.com/f3l1x98/blish-emotes/feature/remote-emotes-catalog/catalog/";

        private readonly CatalogCache _cache;

        /// <summary>The outcome of the most recent sync, including transient <see cref="CatalogSyncState.Downloading"/> state.</summary>
        public CatalogSyncResult LastSyncResult { get; private set; } = new CatalogSyncResult { State = CatalogSyncState.UpToDate };
        public event EventHandler<CatalogSyncResult> LastSyncResultUpdated;
        public DateTime LastCatalogCheck { get; private set; } = DateTime.MinValue;
        /// <summary> Lock object for preventing multiple syncs. </summary>
        private readonly object _syncLock = new object();
        /// <summary> Currently running sync Task. Null if no task running. </summary>
        private Task<CatalogSyncResult> _syncTask;

        /// <summary>The most recent download progress report, or null when no sync is active.</summary>
        public CatalogSyncProgress CurrentProgress { get; private set; }

        /// <summary>Fired whenever a file is downloaded during a sync.</summary>
        public event EventHandler<CatalogSyncProgress> ProgressChanged;

        public CatalogManager(DirectoriesManager directoriesManager)
        {
            IReadOnlyList<string> registeredDirectories = directoriesManager.RegisteredDirectories;
            string dataDirectory = directoriesManager.GetFullDirectoryPath(registeredDirectories[0]);
            _cache = new CatalogCache(dataDirectory);
        }

        /// <summary>True when a usable cache directory exists on disk.</summary>
        public bool HasCache()
        {
            return _cache.HasCache();
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
                Logger.Warn("Failed to load cached catalog - no usable local catalog.");
                Logger.Warn(e.Message);
                return null;
            }
        }

        /// <summary>
        /// Cheap conditional sync: one conditional GET on the manifest, hash-diff against the cache,
        /// re-pull only changed files, stage and validate before swapping. Returns a detailed outcome
        /// for the UX; the caller applies a swapped catalog live on the game thread.
        /// Guarded against multiple callers. Subsequent callers receive the same running Task.
        /// </summary>
        public Task<CatalogSyncResult> SyncAsync()
        {
            lock (_syncLock)
            {
                if (_syncTask != null)
                {
                    return _syncTask;
                }

                _syncTask = GuardedSyncAsync();

                _syncTask.ContinueWith(_ =>
                {
                    lock (_syncLock)
                    {
                        _syncTask = null;
                    }
                });
                return _syncTask;
            }
        }
        
        private async Task<CatalogSyncResult> GuardedSyncAsync()
        {
            try
            {
                Logger.Debug("Refreshing emote catalog in the background");
                CatalogData previousCache = LoadCachedCatalogOrNull();
                // TODO: it is called LastSyncRESULT so shouldnt it only have the results?
                LastSyncResult = new CatalogSyncResult
                {
                    State = CatalogSyncState.Downloading,
                    Version = previousCache?.Manifest?.Version,
                    PreviousVersion = previousCache?.Manifest?.Version,
                };
                LastSyncResultUpdated?.Invoke(this, LastSyncResult);

                var progress = new Progress<CatalogSyncProgress>();
                progress.ProgressChanged += (sender, p) =>
                {
                    CurrentProgress = p;
                    ProgressChanged?.Invoke(this, p);
                };

                using (var client = new CatalogClient(CatalogUrl))
                {
                    LastSyncResult = await CatalogSync.RunAsync(client, _cache, progress).ConfigureAwait(false);
                }
            }
            catch (Exception e)
            {
                Logger.Error("Catalog sync failed unexpectedly!");
                Logger.Error(e.ToString());
                // A throw before CatalogSync's own catch leaves LastSyncResult at Downloading;
                // synthesize a Failed result so the status below cannot get stuck on it.
                LastSyncResult = new CatalogSyncResult
                {
                    State = CatalogSyncState.Failed,
                    Version = LastSyncResult.Version,
                    PreviousVersion = LastSyncResult.Version,
                    ErrorMessage = e.Message,
                };
            }
            finally
            {
                LastCatalogCheck = DateTime.UtcNow;
                LastSyncResultUpdated?.Invoke(this, LastSyncResult);
                LogResult(LastSyncResult);
            }
            return LastSyncResult;
        }

        /// <summary>Cache path of an emote's texture (<c>catalog/textures/{id}.png</c>).</summary>
        public string GetTextureFilePath(string emoteId)
        {
            return _cache.GetTextureFilePath(emoteId);
        }

        /// <summary>
        /// The update a successful sync recorded for the merge to apply (new emotes/categories since
        /// the previous cache), or null. Consumed — and then <see cref="ClearPendingUpdate"/> —
        /// when the update is applied to the user's categories.
        /// </summary>
        public PendingCatalogUpdate ReadPendingUpdateOrNull()
        {
            return _cache.ReadPendingUpdateOrNull();
        }

        /// <summary>Removes the recorded pending update once applied (or discarded as stale/orphaned).</summary>
        public void ClearPendingUpdate()
        {
            _cache.ClearPendingUpdate();
        }

        private void LogResult(CatalogSyncResult result)
        {
            switch (result.State)
            {
                case CatalogSyncState.UpToDate:
                    Logger.Info($"Emote catalog up to date (version {result.Version}).");
                    break;
                case CatalogSyncState.FirstFetch:
                    Logger.Info($"Fetched emote catalog for the first time (version {result.Version}, {result.NewEmoteIds.Count} new emotes) and cached it.");
                    break;
                case CatalogSyncState.Updated:
                    Logger.Info($"Updated emote catalog to version {result.Version} ({result.NewEmoteIds.Count} new emotes); changes take effect on next launch.");
                    break;
                case CatalogSyncState.SchemaTooNew:
                    Logger.Warn($"Emote catalog version {result.Version} uses a schema newer than this module supports - running from cache.");
                    break;
                case CatalogSyncState.Failed:
                    Logger.Warn($"Emote catalog sync failed: {result.ErrorMessage}");
                    break;
            }
        }
    }
}
