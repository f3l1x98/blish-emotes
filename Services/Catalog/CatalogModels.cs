using Newtonsoft.Json;
using System.Collections.Generic;

namespace felix.BlishEmotes.Services.Catalog
{
    /// <summary>
    /// Shape of the remote <c>catalog/manifest.json</c>. <see cref="CatalogManifest.SchemaVersion"/>
    /// versions the JSON format (a too-new value makes the client abort the sync and run from cache);
    /// <see cref="CatalogManifest.Version"/> is a monotonic content revision for humans/logs and never
    /// gates anything mechanical.
    /// </summary>
    public class CatalogManifest
    {
        [JsonProperty("schemaVersion", Required = Required.Always)] public int SchemaVersion { get; set; }
        [JsonProperty("version", Required = Required.Always)] public long Version { get; set; }
        [JsonProperty("files", Required = Required.Always)] public Dictionary<string, string> Files { get; set; }
    }

    /// <summary>Self-contained entry of <c>catalog/emotes.json</c>.</summary>
    public class CatalogEmote
    {
        [JsonProperty("id", Required = Required.Always)] public string Id { get; set; }
        [JsonProperty("command", Required = Required.Always)] public string Command { get; set; }
        [JsonProperty("alternativeCommands")] public List<string> AlternativeCommands { get; set; }
        [JsonProperty("names", Required = Required.Always)] public Dictionary<string, string> Names { get; set; }
        [JsonProperty("categories", Required = Required.Always)] public List<string> Categories { get; set; }
    }

    /// <summary>Category definition of <c>catalog/categories.json</c> (id + localized names; membership lives in the emotes).</summary>
    public class CatalogCategory
    {
        [JsonProperty("id", Required = Required.Always)] public string Id { get; set; }
        [JsonProperty("names", Required = Required.Always)] public Dictionary<string, string> Names { get; set; }
    }

    /// <summary>A parsed catalog: manifest, emotes and category definitions.</summary>
    public class CatalogData
    {
        public CatalogManifest Manifest { get; set; }
        public List<CatalogEmote> Emotes { get; set; }
        public List<CatalogCategory> Categories { get; set; }

        public static CatalogData FromJson(string manifestJson, string emotesJson, string categoriesJson)
        {
            return new CatalogData
            {
                Manifest = JsonConvert.DeserializeObject<CatalogManifest>(manifestJson),
                Emotes = JsonConvert.DeserializeObject<List<CatalogEmote>>(emotesJson),
                Categories = JsonConvert.DeserializeObject<List<CatalogCategory>>(categoriesJson),
            };
        }
    }

    /// <summary>A successful fetch: the parsed catalog plus the raw bytes to write into the local cache.</summary>
    public class FetchedCatalog
    {
        public CatalogData Data { get; set; }
        public byte[] ManifestJson { get; set; }
        /// <summary>File path (relative to the catalog root, e.g. <c>emotes.json</c> or <c>textures/barbecue.png</c>) to raw bytes.</summary>
        public Dictionary<string, byte[]> Files { get; set; } = new Dictionary<string, byte[]>();
    }

    /// <summary>
    /// A catalog update recorded when a sync swaps a new catalog into the cache, and consumed by
    /// the category merge when that update is applied (at the next launch, until live apply
    /// lands): which catalog version it was and which emote/category ids were new relative to
    /// the previous cache. Stored next to the cache; an orphaned record (its cache swap failed
    /// after it was written) is neutralized by the version check in <see cref="CatalogMerge"/>
    /// and cleared on the next apply.
    /// </summary>
    public class PendingCatalogUpdate
    {
        [JsonProperty("catalogVersion", Required = Required.Always)] public long CatalogVersion { get; set; }
        [JsonProperty("newEmoteIds", Required = Required.Always)] public List<string> NewEmoteIds { get; set; }
        [JsonProperty("newCategoryIds", Required = Required.Always)] public List<string> NewCategoryIds { get; set; }
    }

    /// <summary>The final state of a catalog sync, exposed to the UX.</summary>
    public enum CatalogSyncState
    {
        FirstFetch,
        Downloading,
        UpToDate,
        Updated,
        Failed,
        SchemaTooNew,
    }

    /// <summary>Progress report for a catalog sync that has to download files.</summary>
    public class CatalogSyncProgress
    {
        public CatalogSyncState State { get; set; }
        /// <summary>Files downloaded so far (only changed files are fetched).</summary>
        public int DownloadedFiles { get; set; }
        /// <summary>Total files referenced by the manifest.</summary>
        public int TotalFiles { get; set; }
    }

    /// <summary>Outcome of a catalog sync: state, remote version, and which emotes are new since the cache.</summary>
    public class CatalogSyncResult
    {
        public CatalogSyncState State { get; set; }
        /// <summary>Remote catalog version when known; otherwise the cached version.</summary>
        public long? Version { get; set; }
        /// <summary>Ids of emotes present in the fetched catalog but not in the previous cache.</summary>
        public List<string> NewEmoteIds { get; set; } = new List<string>();
        /// <summary>Ids of catalog categories present in the fetched catalog but not in the previous cache.</summary>
        public List<string> NewCategoryIds { get; set; } = new List<string>();
        /// <summary>Human-readable detail for <see cref="CatalogSyncState.Failed"/>.</summary>
        public string ErrorMessage { get; set; }
    }
}
