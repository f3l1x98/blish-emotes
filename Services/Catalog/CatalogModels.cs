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
}
