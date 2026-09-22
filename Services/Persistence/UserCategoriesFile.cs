using felix.BlishEmotes.Exceptions;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;

namespace felix.BlishEmotes.Services.Persistence
{
    /// <summary>
    /// Reads and writes the user-persisted <c>categories.json</c>.
    ///
    /// V2 shape: <c>{ "version": "V2", "data": [categories…], "deletedCatalogCategoryIds": […],
    /// "mergedCatalogVersion": N }</c>. The layout is downgrade-safe by construction:
    /// <c>data</c> remains a plain category array (1.x deserializes it directly) and every added
    /// field is unknown to the 1.x loader, which ignores both the wrapper version string and
    /// unknown members — a 1.x module loads a V2 file fine and merely drops the link
    /// bookkeeping when it next saves (a later 2.0 re-migration re-derives it).
    /// </summary>
    public static class UserCategoriesFile
    {
        public const string VersionV1 = "V1";
        public const string VersionV2 = "V2";

        private class Wrapper
        {
            [JsonProperty("version", Required = Required.Always)] public string Version { get; set; }
            [JsonProperty("data", Required = Required.Always)] public List<PersistedCategory> Data { get; set; }
            [JsonProperty("deletedCatalogCategoryIds")] public List<string> DeletedCatalogCategoryIds { get; set; }
            [JsonProperty("mergedCatalogVersion", NullValueHandling = NullValueHandling.Ignore)] public long? MergedCatalogVersion { get; set; }
        }

        /// <summary>
        /// Reads a V1 or V2 file. Throws <see cref="FileNotFoundException"/> when absent and
        /// <see cref="CatalogException"/> when malformed or of an unknown version.
        /// </summary>
        public static UserCategoriesDocument Read(string path)
        {
            string content = File.ReadAllText(path);
            Wrapper wrapper;
            try
            {
                wrapper = JsonConvert.DeserializeObject<Wrapper>(content);
            }
            catch (Exception e)
            {
                throw new CatalogException($"failed to parse {path}", e);
            }
            if (wrapper == null || wrapper.Data == null)
            {
                throw new CatalogException($"{path} has no category data");
            }
            if (wrapper.Version != VersionV1 && wrapper.Version != VersionV2)
            {
                // A future format must not be half-read and rewritten by this version.
                throw new CatalogException($"unsupported categories.json version '{wrapper.Version}'");
            }
            return new UserCategoriesDocument
            {
                Categories = wrapper.Data,
                DeletedCatalogCategoryIds = wrapper.DeletedCatalogCategoryIds ?? new List<string>(),
                MergedCatalogVersion = wrapper.MergedCatalogVersion,
            };
        }

        /// <summary>Writes the document in the V2 shape.</summary>
        public static void Write(string path, UserCategoriesDocument document)
        {
            var wrapper = new Wrapper
            {
                Version = VersionV2,
                Data = document.Categories,
                DeletedCatalogCategoryIds = document.DeletedCatalogCategoryIds,
                MergedCatalogVersion = document.MergedCatalogVersion,
            };
            File.WriteAllText(path, JsonConvert.SerializeObject(wrapper));
        }
    }
}
