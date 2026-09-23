using felix.BlishEmotes.Exceptions;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace felix.BlishEmotes.Services.Catalog
{
    /// <summary>
    /// The local copy of the fetched catalog files inside the module data directory
    /// (<c>&lt;data&gt;/catalog</c>): what the module runs from when offline, refreshed by a fetch,
    /// and only ever grown by policy. Writes are staged and validated before they swap the cache in
    /// (single generation), so a failed or corrupted fetch never replaces a working cache.
    /// </summary>
    public class CatalogCache
    {
        public const string CacheDirectoryName = "catalog";
        public const string StagingDirectoryName = "catalog-staging";
        public const string ETagFileName = "etag.txt";
        public const string PendingUpdateFileName = "pending-merge.json";

        /// <param name="dataRootDirectory">The module data directory the cache lives in (e.g. the <c>emotes</c> directory).</param>
        public CatalogCache(string dataRootDirectory)
        {
            DataRootDirectory = dataRootDirectory;
            CacheDirectory = Path.Combine(dataRootDirectory, CacheDirectoryName);
            StagingDirectory = Path.Combine(dataRootDirectory, StagingDirectoryName);
            ETagFilePath = Path.Combine(CacheDirectory, ETagFileName);
            PendingUpdateFilePath = Path.Combine(dataRootDirectory, PendingUpdateFileName);
        }

        public string DataRootDirectory { get; }
        public string CacheDirectory { get; }
        public string StagingDirectory { get; }
        public string ETagFilePath { get; }

        /// <summary>
        /// The pending-merge record lives in the data root (not inside the cache directory,
        /// which every swap deletes wholesale).
        /// </summary>
        public string PendingUpdateFilePath { get; }

        public bool HasCache()
        {
            return File.Exists(Path.Combine(CacheDirectory, CatalogPaths.EmotesFile));
        }

        /// <summary>Loads and validates the cached catalog. Throws <see cref="CatalogException"/> when missing, malformed or invalid.</summary>
        public CatalogData Load()
        {
            string manifestPath = Path.Combine(CacheDirectory, CatalogPaths.ManifestFile);
            string emotesPath = Path.Combine(CacheDirectory, CatalogPaths.EmotesFile);
            string categoriesPath = Path.Combine(CacheDirectory, CatalogPaths.CategoriesFile);
            try
            {
                var data = CatalogData.FromJson(
                    File.ReadAllText(manifestPath, Encoding.UTF8),
                    File.ReadAllText(emotesPath, Encoding.UTF8),
                    File.ReadAllText(categoriesPath, Encoding.UTF8));
                var errors = CatalogValidator.Validate(data);
                if (errors.Count > 0)
                {
                    throw new CatalogException("cached catalog is invalid: " + string.Join("; ", errors));
                }
                return data;
            }
            catch (CatalogException)
            {
                throw;
            }
            catch (Exception e)
            {
                throw new CatalogException("failed to load cached catalog", e);
            }
        }

        /// <summary>Tries to load and validate the cached catalog. Returns <c>null</c> when missing, malformed or invalid.</summary>
        public CatalogData TryLoad()
        {
            if (!HasCache())
            {
                return null;
            }
            try
            {
                return Load();
            } catch (CatalogException e)
            {
                return null;
            }
        }

        /// <summary>
        /// Writes a fetched catalog into the cache: stage all files, validate the staged copy, then
        /// swap it in. On any failure the existing cache (if any) is left untouched.
        /// </summary>
        public void Store(FetchedCatalog fetched)
        {
            if (fetched == null || fetched.Data == null)
            {
                throw new CatalogException("nothing to store");
            }
            try
            {
                WriteStaging(fetched);
                ValidateStaging();
                SwapStagedCache();
            }
            catch (CatalogException)
            {
                DiscardStaging();
                throw;
            }
            catch (Exception e)
            {
                DiscardStaging();
                throw new CatalogException("failed to store catalog in cache", e);
            }
        }

        public string GetTextureFilePath(string emoteId)
        {
            return GetFilePath(CatalogPaths.TextureFile(emoteId));
        }

        public string GetFilePath(string path)
        {
            return Path.Combine(CacheDirectory, path.Replace('/', Path.DirectorySeparatorChar));
        }

        /// <summary>Reads the stored ETag, or <c>null</c> when there is none.</summary>
        public string LoadETag()
        {
            if (!File.Exists(ETagFilePath))
            {
                return null;
            }
            try
            {
                return File.ReadAllText(ETagFilePath, Encoding.UTF8);
            }
            catch
            {
                return null;
            }
        }

        /// <summary>Persists the ETag.</summary>
        public void StoreETag(string etag)
        {
            if (etag == null)
            {
                if (File.Exists(ETagFilePath))
                {
                    File.Delete(ETagFilePath);
                }
                return;
            }
            File.WriteAllText(ETagFilePath, etag, Encoding.UTF8);
        }

        /// <summary>Removes the staging directory if it exists.</summary>
        public void DiscardStaging()
        {
            if (Directory.Exists(StagingDirectory))
            {
                Directory.Delete(StagingDirectory, true);
            }
        }

        /// <summary>Reads the pending-merge record, or <c>null</c> when there is none (or it is unreadable — it is rewritten by the next successful sync anyway).</summary>
        public PendingCatalogUpdate ReadPendingUpdateOrNull()
        {
            if (!File.Exists(PendingUpdateFilePath))
            {
                return null;
            }
            try
            {
                return JsonConvert.DeserializeObject<PendingCatalogUpdate>(
                    File.ReadAllText(PendingUpdateFilePath, Encoding.UTF8));
            }
            catch
            {
                return null;
            }
        }

        /// <summary>Records what a successful sync changed, for the merge that applies it.</summary>
        public void StorePendingUpdate(PendingCatalogUpdate pending)
        {
            File.WriteAllText(PendingUpdateFilePath, JsonConvert.SerializeObject(pending), Encoding.UTF8);
        }

        /// <summary>Removes the pending-merge record once it has been applied (or discarded as stale).</summary>
        public void ClearPendingUpdate()
        {
            if (File.Exists(PendingUpdateFilePath))
            {
                File.Delete(PendingUpdateFilePath);
            }
        }

        private void WriteStaging(FetchedCatalog fetched)
        {
            DiscardStaging();
            Directory.CreateDirectory(StagingDirectory);

            File.WriteAllBytes(Path.Combine(StagingDirectory, CatalogPaths.ManifestFile), fetched.ManifestJson);
            foreach (var entry in fetched.Files)
            {
                if (!CatalogPaths.IsSafeRelativePath(entry.Key))
                {
                    throw new CatalogException($"unsafe file path '{entry.Key}'");
                }
                string destination = Path.Combine(StagingDirectory, entry.Key.Replace('/', Path.DirectorySeparatorChar));
                Directory.CreateDirectory(Path.GetDirectoryName(destination));
                File.WriteAllBytes(destination, entry.Value);
            }
        }

        private void ValidateStaging()
        {
            var staged = CatalogData.FromJson(
                File.ReadAllText(Path.Combine(StagingDirectory, CatalogPaths.ManifestFile), Encoding.UTF8),
                File.ReadAllText(Path.Combine(StagingDirectory, CatalogPaths.EmotesFile), Encoding.UTF8),
                File.ReadAllText(Path.Combine(StagingDirectory, CatalogPaths.CategoriesFile), Encoding.UTF8));
            var errors = CatalogValidator.Validate(staged);
            if (errors.Count > 0)
            {
                throw new CatalogException("staged catalog is invalid: " + string.Join("; ", errors));
            }
        }

        private void SwapStagedCache()
        {
            if (Directory.Exists(CacheDirectory))
            {
                Directory.Delete(CacheDirectory, true);
            }
            Directory.Move(StagingDirectory, CacheDirectory);
        }
    }
}
