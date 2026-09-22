using Newtonsoft.Json;
using System;
using System.Collections.Generic;

namespace felix.BlishEmotes.Services.Persistence
{
    /// <summary>
    /// Persisted shape of one user category in <c>categories.json</c> — the wire counterpart of
    /// the runtime <c>Category</c> minus runtime state (resolved emotes, texture). Kept Blish-free
    /// so the V2 file format and the catalog merge are unit-testable.
    /// </summary>
    public class PersistedCategory
    {
        [JsonProperty("id", Required = Required.Always)] public Guid Id { get; set; }
        [JsonProperty("name", Required = Required.Always)] public string Name { get; set; }
        [JsonProperty("emoteIds", Required = Required.Always)] public List<string> EmoteIds { get; set; }
        [JsonProperty("isFavourite", DefaultValueHandling = DefaultValueHandling.Populate)] public bool IsFavourite { get; set; } = false;
        [JsonProperty("textureFileName")] public string TextureFileName { get; set; }

        /// <summary>
        /// Id of the catalog category this user category is linked to (a "linked category" in
        /// CONTEXT.md terms): it receives newly fetched emotes tagged with that catalog category.
        /// Null = purely user-owned; omitted from the JSON when null.
        /// </summary>
        [JsonProperty("catalogId", NullValueHandling = NullValueHandling.Ignore)] public string CatalogId { get; set; }
    }

    /// <summary>
    /// The V2 <c>categories.json</c> document: the user's categories (the only category state the
    /// module renders) plus the catalog-link bookkeeping —
    /// <see cref="DeletedCatalogCategoryIds"/> (tombstones: linked categories the user deleted,
    /// which auto-creation must never resurrect) and <see cref="MergedCatalogVersion"/> (the
    /// catalog version the state has been reconciled against; null = the one-time V2 migration
    /// has not run yet).
    /// </summary>
    public class UserCategoriesDocument
    {
        public List<PersistedCategory> Categories { get; set; } = new List<PersistedCategory>();
        public List<string> DeletedCatalogCategoryIds { get; set; } = new List<string>();
        public long? MergedCatalogVersion { get; set; }
    }
}
