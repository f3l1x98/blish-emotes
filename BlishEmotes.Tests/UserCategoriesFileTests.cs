using felix.BlishEmotes.Exceptions;
using felix.BlishEmotes.Services.Persistence;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace BlishEmotes.Tests
{
    /// <summary>
    /// The V2 categories.json file format: V1 (1.3.8) files load, V2 round-trips, and — the
    /// downgrade-safety contract — a 1.x loader can read a V2 file because data stays a plain
    /// category array and every added field is unknown-to-and-ignored-by it.
    /// </summary>
    public static class UserCategoriesFileTests
    {
        public static void Run()
        {
            Console.WriteLine("UserCategoriesFile");

            TestRunner.Run("missing file throws FileNotFoundException", () =>
            {
                using (var fixture = new CatalogCacheTests.Fixture())
                {
                    string path = Path.Combine(fixture.DataRoot, "categories.json");
                    TestRunner.NotNull(TestRunner.Throws<FileNotFoundException>(() => UserCategoriesFile.Read(path)));
                }
            });

            TestRunner.Run("a 1.3.8 (V1) file loads with empty link bookkeeping", () =>
            {
                using (var fixture = new CatalogCacheTests.Fixture())
                {
                    string path = Path.Combine(fixture.DataRoot, "categories.json");
                    File.WriteAllText(path, JsonConvert.SerializeObject(new
                    {
                        version = "V1",
                        data = new object[]
                        {
                            new { id = "d4f1e0aa-1111-2222-3332-444455556666", name = "Favourites", emoteIds = new[] { "beckon" }, isFavourite = true, textureFileName = "favourites.png" },
                            new { id = "d4f1e0aa-1111-2222-3332-444455556667", name = "Greeting", emoteIds = new[] { "beckon", "wave" } },
                        },
                    }));

                    var document = UserCategoriesFile.Read(path);
                    TestRunner.Equal(2, document.Categories.Count);
                    TestRunner.Equal("Greeting", document.Categories[1].Name);
                    TestRunner.True(document.Categories[1].EmoteIds.Contains("wave"));
                    TestRunner.True(document.Categories[1].CatalogId == null);
                    TestRunner.Empty(document.DeletedCatalogCategoryIds);
                    TestRunner.True(document.MergedCatalogVersion == null);
                }
            });

            TestRunner.Run("V2 document round-trips", () =>
            {
                using (var fixture = new CatalogCacheTests.Fixture())
                {
                    string path = Path.Combine(fixture.DataRoot, "categories.json");
                    var document = new UserCategoriesDocument
                    {
                        Categories = new List<PersistedCategory>
                        {
                            new PersistedCategory { Id = Guid.NewGuid(), Name = "Favourites", EmoteIds = new List<string> { "beckon" }, IsFavourite = true },
                            new PersistedCategory { Id = Guid.NewGuid(), Name = "Greeting", EmoteIds = new List<string>(), CatalogId = "greeting" },
                        },
                        DeletedCatalogCategoryIds = new List<string> { "dance" },
                        MergedCatalogVersion = 7,
                    };
                    UserCategoriesFile.Write(path, document);

                    var loaded = UserCategoriesFile.Read(path);
                    TestRunner.Equal(2, loaded.Categories.Count);
                    TestRunner.Equal("greeting", loaded.Categories[1].CatalogId);
                    TestRunner.True(loaded.Categories[0].IsFavourite);
                    TestRunner.Equal(1, loaded.DeletedCatalogCategoryIds.Count);
                    TestRunner.Equal(7, loaded.MergedCatalogVersion);
                }
            });

            TestRunner.Run("unlinked categories and unmigrated state omit their fields", () =>
            {
                using (var fixture = new CatalogCacheTests.Fixture())
                {
                    string path = Path.Combine(fixture.DataRoot, "categories.json");
                    UserCategoriesFile.Write(path, new UserCategoriesDocument
                    {
                        Categories = new List<PersistedCategory>
                        {
                            new PersistedCategory { Id = Guid.NewGuid(), Name = "My Emotes", EmoteIds = new List<string>() },
                        },
                    });

                    string json = File.ReadAllText(path);
                    TestRunner.False(json.Contains("catalogId"));
                    TestRunner.False(json.Contains("mergedCatalogVersion"));

                    var loaded = UserCategoriesFile.Read(path);
                    TestRunner.True(loaded.Categories[0].CatalogId == null);
                    TestRunner.True(loaded.MergedCatalogVersion == null);
                }
            });

            TestRunner.Run("downgrade-safe: a 1.x loader reads a V2 file and ignores unknown fields", () =>
            {
                using (var fixture = new CatalogCacheTests.Fixture())
                {
                    string path = Path.Combine(fixture.DataRoot, "categories.json");
                    var document = new UserCategoriesDocument
                    {
                        Categories = new List<PersistedCategory>
                        {
                            new PersistedCategory { Id = Guid.NewGuid(), Name = "Greeting", EmoteIds = new List<string> { "wave" }, CatalogId = "greeting" },
                        },
                        DeletedCatalogCategoryIds = new List<string> { "dance" },
                        MergedCatalogVersion = 7,
                    };
                    UserCategoriesFile.Write(path, document);

                    // The 1.x shape: wrapper with version + data only, category without catalogId.
                    // Newtonsoft's default MissingMemberHandling.Ignore is what makes the unknown
                    // V2 fields harmless to it.
                    var legacy = JsonConvert.DeserializeObject<LegacyWrapper>(File.ReadAllText(path));
                    TestRunner.Equal("V2", legacy.Version); // read, but never checked by 1.x
                    TestRunner.Equal(1, legacy.Data.Count);
                    TestRunner.Equal("Greeting", legacy.Data[0].Name);
                    TestRunner.True(legacy.Data[0].EmoteIds.Contains("wave"));
                    TestRunner.True(legacy.Data[0].IsFavourite == false);
                }
            });

            TestRunner.Run("malformed file and unknown future version are rejected", () =>
            {
                using (var fixture = new CatalogCacheTests.Fixture())
                {
                    string path = Path.Combine(fixture.DataRoot, "categories.json");
                    File.WriteAllText(path, "{ not json");
                    TestRunner.NotNull(TestRunner.Throws<CatalogException>(() => UserCategoriesFile.Read(path)));

                    File.WriteAllText(path, JsonConvert.SerializeObject(new { version = "V3", data = new object[0] }));
                    TestRunner.NotNull(TestRunner.Throws<CatalogException>(() => UserCategoriesFile.Read(path)));
                }
            });

            TestRunner.Run("a V1 file rewritten by a downgraded 1.x module loads again", () =>
            {
                using (var fixture = new CatalogCacheTests.Fixture())
                {
                    string path = Path.Combine(fixture.DataRoot, "categories.json");
                    // 1.x rewrote the V2 file as V1: link bookkeeping gone, categories intact.
                    File.WriteAllText(path, JsonConvert.SerializeObject(new
                    {
                        version = "V1",
                        data = new object[]
                        {
                            new { id = "d4f1e0aa-1111-2222-3332-444455556666", name = "Greeting", emoteIds = new[] { "wave" } },
                        },
                    }));
                    var document = UserCategoriesFile.Read(path);
                    TestRunner.Equal(1, document.Categories.Count);
                    TestRunner.True(document.MergedCatalogVersion == null); // re-migrates on the next 2.0 run
                }
            });
        }

        /// <summary>The persisted wrapper as the 1.x module defines it (version + data, nothing else).</summary>
        private class LegacyWrapper
        {
            [JsonProperty("version")] public string Version { get; set; }
            [JsonProperty("data", Required = Required.Always)] public List<LegacyCategory> Data { get; set; }
        }

        private class LegacyCategory
        {
            [JsonProperty("id")] public Guid Id { get; set; }
            [JsonProperty("name")] public string Name { get; set; }
            [JsonProperty("emoteIds")] public List<string> EmoteIds { get; set; }
            [JsonProperty("isFavourite")] public bool IsFavourite { get; set; }
            [JsonProperty("textureFileName")] public string TextureFileName { get; set; }
        }
    }
}
