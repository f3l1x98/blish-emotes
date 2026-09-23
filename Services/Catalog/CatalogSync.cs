using felix.BlishEmotes.Exceptions;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace felix.BlishEmotes.Services.Catalog
{
    /// <summary>
    /// Blish-free orchestration of a cheap conditional catalog sync. Given a client and a cache it
    /// performs one conditional manifest GET, hash-diff, re-pull of changed files, staged validation,
    /// and cache swap. Failures leave the existing cache and ETag untouched and are reported as
    /// <see cref="CatalogSyncState.Failed"/>.
    /// </summary>
    public static class CatalogSync
    {
        /// <summary>
        /// Runs the sync and returns a detailed outcome.
        /// </summary>
        // TODO: I think passing previousCache is useless as it is always `cache.Load()` or null
        public static async Task<CatalogSyncResult> RunAsync(CatalogClient client, CatalogCache cache, IProgress<CatalogSyncProgress> progress = null)
        {
            // If previousCache is null -> Fresh install
            CatalogData currentCache = cache.TryLoad();
            string etag = currentCache == null ? null : cache.LoadETag();
            bool hadCache = currentCache != null;
            long? previousVersion = currentCache?.Manifest?.Version;

            try
            {
                var manifestResult = await client.GetManifestAsync(etag).ConfigureAwait(false);
                if (manifestResult.NotModified)
                {
                    return Result(CatalogSyncState.UpToDate, previousVersion, new List<string>(), new List<string>());
                }

                var remoteManifest = Deserialize<CatalogManifest>(manifestResult.ManifestJson);
                long? remoteVersion = remoteManifest?.Version;

                if (remoteManifest != null && remoteManifest.SchemaVersion > CatalogValidator.SupportedSchemaVersion)
                {
                    // Deliberately do not store the ETag: a future check must re-fetch the
                    // too-new manifest so the UX can keep reporting schema-too-new.
                    return Result(CatalogSyncState.SchemaTooNew, remoteVersion, new List<string>(), new List<string>());
                }

                if (remoteManifest == null || remoteManifest.Files == null)
                {
                    throw new CatalogException("manifest has no file map");
                }

                // TODO: I think I could just use currentCache.Manifest and compare the hashes stored in there
                //  Only con: would not discover if files have been tempered with
                List<string> changedPaths = DiffAgainstCache(cache, remoteManifest);
                int totalFiles = remoteManifest.Files.Count;
                progress?.Report(new CatalogSyncProgress { State = CatalogSyncState.Downloading, DownloadedFiles = 0, TotalFiles = totalFiles });

                var fetchedFiles = new Dictionary<string, byte[]>();
                for (int i = 0; i < changedPaths.Count; i++)
                {
                    string path = changedPaths[i];
                    string expectedHash = remoteManifest.Files[path];
                    byte[] bytes = await client.GetFileAsync(path, expectedHash).ConfigureAwait(false);
                    fetchedFiles[path] = bytes;
                    progress?.Report(new CatalogSyncProgress { State = CatalogSyncState.Downloading, DownloadedFiles = i + 1, TotalFiles = totalFiles });
                }

                var allFiles = new Dictionary<string, byte[]>(fetchedFiles);
                foreach (string path in remoteManifest.Files.Keys)
                {
                    if (allFiles.ContainsKey(path))
                    {
                        continue;
                    }
                    string cachePath = cache.GetFilePath(path);
                    if (!File.Exists(cachePath))
                    {
                        throw new CatalogException($"unchanged file missing from cache: {path}");
                    }
                    allFiles[path] = File.ReadAllBytes(cachePath);
                }

                var data = new CatalogData
                {
                    Manifest = remoteManifest,
                    Emotes = Deserialize<List<CatalogEmote>>(allFiles[CatalogPaths.EmotesFile]),
                    Categories = Deserialize<List<CatalogCategory>>(allFiles[CatalogPaths.CategoriesFile]),
                };
                var fetched = new FetchedCatalog
                {
                    Data = data,
                    ManifestJson = manifestResult.ManifestJson,
                    Files = allFiles,
                };

                var newEmoteIds = NewEmoteIds(currentCache, data);
                var newCategoryIds = NewCategoryIds(currentCache, data);

                // Record what this update changed before swapping the cache: the merge consumes
                // the record when the update is applied (live by the caller, or at next launch
                // if that apply never runs).
                // Written first on purpose — if the swap fails afterwards the orphaned record is
                // neutralized by the version check in the merge and cleared on the next apply.
                cache.StorePendingUpdate(new PendingCatalogUpdate
                {
                    CatalogVersion = remoteManifest.Version,
                    NewEmoteIds = newEmoteIds,
                    NewCategoryIds = newCategoryIds,
                });

                cache.Store(fetched);
                cache.StoreETag(manifestResult.ETag);

                var state = hadCache ? CatalogSyncState.Updated : CatalogSyncState.FirstFetch;
                return Result(state, remoteVersion, newEmoteIds, newCategoryIds);
            }
            catch (Exception e)
            {
                string message = e is CatalogException ? e.Message : e.ToString();
                return new CatalogSyncResult
                {
                    State = CatalogSyncState.Failed,
                    Version = previousVersion,
                    NewEmoteIds = new List<string>(),
                    NewCategoryIds = new List<string>(),
                    ErrorMessage = message,
                };
            }
        }

        private static CatalogSyncResult Result(CatalogSyncState state, long? version, List<string> newEmoteIds, List<string> newCategoryIds)
        {
            return new CatalogSyncResult
            {
                State = state,
                Version = version,
                NewEmoteIds = newEmoteIds ?? new List<string>(),
                NewCategoryIds = newCategoryIds ?? new List<string>(),
            };
        }

        private static List<string> EmoteIds(CatalogData data)
        {
            if (data?.Emotes == null)
            {
                return new List<string>();
            }
            return data.Emotes.Select(e => e.Id).ToList();
        }

        private static List<string> NewEmoteIds(CatalogData previous, CatalogData current)
        {
            var previousIds = new HashSet<string>(EmoteIds(previous));
            return EmoteIds(current).Where(id => !previousIds.Contains(id)).ToList();
        }

        private static List<string> CategoryIds(CatalogData data)
        {
            if (data?.Categories == null)
            {
                return new List<string>();
            }
            return data.Categories.Select(c => c.Id).ToList();
        }

        private static List<string> NewCategoryIds(CatalogData previous, CatalogData current)
        {
            var previousIds = new HashSet<string>(CategoryIds(previous));
            return CategoryIds(current).Where(id => !previousIds.Contains(id)).ToList();
        }

        private static List<string> DiffAgainstCache(CatalogCache cache, CatalogManifest manifest)
        {
            var changed = new List<string>();

            foreach (var entry in manifest.Files)
            {
                string path = entry.Key;
                string expectedHash = entry.Value;
                string cachePath = cache.GetFilePath(path);
                if (!File.Exists(cachePath))
                {
                    changed.Add(path);
                    continue;
                }
                byte[] cachedBytes = File.ReadAllBytes(cachePath);
                if (!CatalogHash.Verify(cachedBytes, expectedHash))
                {
                    changed.Add(path);
                }
            }

            return changed;
        }

        private static T Deserialize<T>(byte[] json)
        {
            try
            {
                return JsonConvert.DeserializeObject<T>(Encoding.UTF8.GetString(json));
            }
            catch (Exception e)
            {
                throw new CatalogException($"failed to parse {typeof(T)}", e);
            }
        }
    }
}
