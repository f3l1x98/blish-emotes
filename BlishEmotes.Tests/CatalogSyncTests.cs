using felix.BlishEmotes.Services.Catalog;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BlishEmotes.Tests
{
    /// <summary>
    /// Cheap conditional sync: ETag 304, hash-diff re-pull, schema-too-new abort, failed sync keeps
    /// old cache and ETag, and outcome states are exposed. No pre-cache PNG decode-test: sha256
    /// verification covers transit corruption, and the load-time decode in
    /// EmotesManager.LoadCatalogTexture falls back to the missing-texture placeholder.
    /// </summary>
    public static class CatalogSyncTests
    {
        public static void Run()
        {
            Console.WriteLine("CatalogSync");

            TestRunner.Run("first fetch populates cache and reports FirstFetch", () =>
            {
                using (var server = new CatalogServer())
                using (var fixture = new CatalogCacheTests.Fixture())
                using (var client = new CatalogClient(server.BaseUrl))
                {
                    var cache = new CatalogCache(fixture.DataRoot);
                    var result = CatalogSync.RunAsync(client, cache, null).GetAwaiter().GetResult();

                    TestRunner.Equal(CatalogSyncState.FirstFetch, result.State);
                    TestRunner.Equal(64, result.NewEmoteIds.Count);
                    TestRunner.True(cache.HasCache());
                    TestRunner.Equal(64, cache.Load().Emotes.Count);
                    TestRunner.Equal(server.ManifestEtag, cache.LoadETag());
                }
            });

            TestRunner.Run("unchanged catalog returns 304 and pulls zero files", () =>
            {
                using (var server = new CatalogServer())
                using (var fixture = new CatalogCacheTests.Fixture())
                {
                    var cache = new CatalogCache(fixture.DataRoot);
                    var previous = SeedCache(server, cache);
                    string etag = cache.LoadETag();

                    using (var client = new CatalogClient(server.BaseUrl))
                    {
                        server.ResetRequestCounts();
                        var result = CatalogSync.RunAsync(client, cache, previous).GetAwaiter().GetResult();

                        TestRunner.Equal(CatalogSyncState.UpToDate, result.State);
                        TestRunner.Equal(1, server.RequestCount("manifest.json"));
                        TestRunner.Equal(0, server.TotalFileRequestCount());
                        TestRunner.Equal(etag, cache.LoadETag());
                        TestRunner.Empty(result.NewEmoteIds);
                    }
                }
            });

            TestRunner.Run("changed catalog re-pulls only changed files", () =>
            {
                using (var server = new CatalogServer())
                using (var fixture = new CatalogCacheTests.Fixture())
                {
                    var cache = new CatalogCache(fixture.DataRoot);
                    var previous = SeedCache(server, cache);

                    // Use a different existing texture's bytes to produce a changed but valid PNG.
                    byte[] newTexture = server.GetFile("textures/beckon.png");
                    server.SetManifestEtag("\"v2\"");
                    server.SetFile("manifest.json", BuildManifest(2, "textures/barbecue.png", newTexture));
                    server.SetFile("textures/barbecue.png", newTexture);
                    server.ResetRequestCounts();

                    using (var client = new CatalogClient(server.BaseUrl))
                    {
                        var result = CatalogSync.RunAsync(client, cache, previous).GetAwaiter().GetResult();

                        TestRunner.Equal(CatalogSyncState.Updated, result.State);
                        TestRunner.Equal(2, result.Version);
                        TestRunner.Equal(1, server.RequestCount("manifest.json"));
                        TestRunner.Equal(1, server.RequestCount("textures/barbecue.png"));
                        TestRunner.Equal(0, server.RequestCount("emotes.json"));
                        TestRunner.Equal(0, server.RequestCount("categories.json"));

                        var loaded = cache.Load();
                        TestRunner.Equal(2, loaded.Manifest.Version);
                        TestRunner.Equal(server.ManifestEtag, cache.LoadETag());
                        TestRunner.Equal(CatalogHash.Sha256Hex(newTexture),
                            CatalogHash.Sha256Hex(File.ReadAllBytes(cache.GetTextureFilePath("barbecue"))));
                    }
                }
            });

            TestRunner.Run("too-new schemaVersion aborts and keeps cache and ETag", () =>
            {
                using (var server = new CatalogServer())
                using (var fixture = new CatalogCacheTests.Fixture())
                {
                    var cache = new CatalogCache(fixture.DataRoot);
                    var previous = SeedCache(server, cache);
                    string etag = cache.LoadETag();

                    server.SetManifestEtag("\"v2\"");
                    server.SetFile("manifest.json", TooNewManifestBytes());

                    using (var client = new CatalogClient(server.BaseUrl))
                    {
                        var result = CatalogSync.RunAsync(client, cache, previous).GetAwaiter().GetResult();
                        TestRunner.Equal(CatalogSyncState.SchemaTooNew, result.State);
                        TestRunner.Equal(2, result.Version);

                        // Cache and ETag untouched.
                        TestRunner.Equal(etag, cache.LoadETag());
                        TestRunner.Equal(1, cache.Load().Manifest.Version);

                        // A second sync must re-fetch, not skip with a 304.
                        server.ResetRequestCounts();
                        var retry = CatalogSync.RunAsync(client, cache, previous).GetAwaiter().GetResult();
                        TestRunner.Equal(CatalogSyncState.SchemaTooNew, retry.State);
                        TestRunner.Equal(1, server.RequestCount("manifest.json"));
                    }
                }
            });

            TestRunner.Run("too-new schemaVersion with no cache reports SchemaTooNew", () =>
            {
                using (var server = new CatalogServer())
                using (var fixture = new CatalogCacheTests.Fixture())
                using (var client = new CatalogClient(server.BaseUrl))
                {
                    server.SetManifestEtag("\"v2\"");
                    server.SetFile("manifest.json", TooNewManifestBytes());

                    var cache = new CatalogCache(fixture.DataRoot);
                    var result = CatalogSync.RunAsync(client, cache, null).GetAwaiter().GetResult();
                    TestRunner.Equal(CatalogSyncState.SchemaTooNew, result.State);
                    TestRunner.False(cache.HasCache());
                    TestRunner.True(cache.LoadETag() == null);
                }
            });

            TestRunner.Run("hash-mismatched file keeps old cache and ETag and retries", () =>
            {
                using (var server = new CatalogServer())
                using (var fixture = new CatalogCacheTests.Fixture())
                {
                    var cache = new CatalogCache(fixture.DataRoot);
                    var previous = SeedCache(server, cache);
                    string etag = cache.LoadETag();

                    // Serve bytes that do not match the manifest hash: the sync must fail without
                    // touching the old cache or ETag, and a retry must genuinely re-fetch.
                    byte[] newTexture = server.GetFile("textures/beckon.png");
                    byte[] badBytes = new byte[] { 0x89, 0x50, 0x4E, 0x47 }; // not the bytes the manifest hashes
                    server.SetManifestEtag("\"v2\"");
                    server.SetFile("manifest.json", BuildManifest(2, "textures/barbecue.png", newTexture));
                    server.SetFile("textures/barbecue.png", newTexture);
                    server.Tamper("textures/barbecue.png", bytes => badBytes);

                    using (var client = new CatalogClient(server.BaseUrl))
                    {
                        var result = CatalogSync.RunAsync(client, cache, previous).GetAwaiter().GetResult();
                        TestRunner.Equal(CatalogSyncState.Failed, result.State);
                        TestRunner.NotNull(result.ErrorMessage);

                        // Old cache and ETag intact.
                        TestRunner.Equal(etag, cache.LoadETag());
                        TestRunner.Equal(1, cache.Load().Manifest.Version);

                        // Fix the catalog and retry: the old ETag must not 304 past the bad version.
                        server.Tamper("textures/barbecue.png", bytes => bytes);
                        server.SetManifestEtag("\"v3\"");
                        server.SetFile("manifest.json", BuildManifest(3, "textures/barbecue.png", newTexture));
                        server.ResetRequestCounts();

                        var retry = CatalogSync.RunAsync(client, cache, previous).GetAwaiter().GetResult();
                        TestRunner.Equal(CatalogSyncState.Updated, retry.State);
                        TestRunner.Equal(3, retry.Version);
                        TestRunner.Equal(1, server.RequestCount("manifest.json"));
                        TestRunner.Equal(1, server.RequestCount("textures/barbecue.png"));
                    }
                }
            });

            TestRunner.Run("offline sync fails and the cache stays loadable (offline freeze)", () =>
            {
                using (var server = new CatalogServer())
                using (var fixture = new CatalogCacheTests.Fixture())
                {
                    var cache = new CatalogCache(fixture.DataRoot);
                    var previous = SeedCache(server, cache);

                    // Network "blocked": server down.
                    server.Stop();
                    using (var client = new CatalogClient(server.BaseUrl))
                    {
                        var result = CatalogSync.RunAsync(client, cache, previous).GetAwaiter().GetResult();
                        TestRunner.Equal(CatalogSyncState.Failed, result.State);
                        TestRunner.NotNull(result.ErrorMessage);
                    }

                    // The cache still renders the same emotes.
                    TestRunner.True(cache.HasCache());
                    TestRunner.Equal(64, cache.Load().Emotes.Count);
                }
            });

            TestRunner.Run("structurally broken texture passes sync and lands in the cache", () =>
            {
                using (var server = new CatalogServer())
                using (var fixture = new CatalogCacheTests.Fixture())
                {
                    var cache = new CatalogCache(fixture.DataRoot);
                    var previous = SeedCache(server, cache);

                    // No pre-cache PNG decode-test (dropped): a hash-consistent but broken texture
                    // syncs successfully; the true decode-test is the load-time fallback in
                    // EmotesManager.LoadCatalogTexture (missing-texture placeholder).
                    byte[] brokenTexture = new byte[] { 0x89, 0x50, 0x4E, 0x47 };
                    server.SetManifestEtag("\"v2\"");
                    server.SetFile("manifest.json", BuildManifest(2, "textures/barbecue.png", brokenTexture));
                    server.SetFile("textures/barbecue.png", brokenTexture);

                    using (var client = new CatalogClient(server.BaseUrl))
                    {
                        var result = CatalogSync.RunAsync(client, cache, previous).GetAwaiter().GetResult();
                        TestRunner.Equal(CatalogSyncState.Updated, result.State);
                        TestRunner.Equal(brokenTexture.Length,
                            File.ReadAllBytes(cache.GetTextureFilePath("barbecue")).Length);
                    }
                }
            });

            TestRunner.Run("updated catalog reports new emote ids", () =>
            {
                using (var server = new CatalogServer())
                using (var fixture = new CatalogCacheTests.Fixture())
                {
                    var cache = new CatalogCache(fixture.DataRoot);
                    var previous = SeedCache(server, cache);

                    var newCatalog = AddEmote(server, cache, "tada", "/tada", "miscellaneous");
                    server.SetManifestEtag("\"v2\"");
                    server.SetFile("manifest.json", newCatalog.ManifestJson);
                    server.SetFile("emotes.json", newCatalog.EmotesJson);
                    server.SetFile("textures/tada.png", newCatalog.TextureBytes);
                    server.ResetRequestCounts();

                    using (var client = new CatalogClient(server.BaseUrl))
                    {
                        var result = CatalogSync.RunAsync(client, cache, previous).GetAwaiter().GetResult();

                        TestRunner.Equal(CatalogSyncState.Updated, result.State);
                        TestRunner.Equal(2, result.Version);
                        TestRunner.Equal(1, result.NewEmoteIds.Count);
                        TestRunner.True(result.NewEmoteIds.Contains("tada"));

                        var loaded = cache.Load();
                        TestRunner.Equal(65, loaded.Emotes.Count);
                        TestRunner.NotNull(loaded.Emotes.Find(e => e.Id == "tada"));
                    }
                }
            });
        }

        private static CatalogData SeedCache(CatalogServer server, CatalogCache cache)
        {
            using (var client = new CatalogClient(server.BaseUrl))
            {
                var result = CatalogSync.RunAsync(client, cache, null).GetAwaiter().GetResult();
                TestRunner.Equal(CatalogSyncState.FirstFetch, result.State);
            }
            return cache.Load();
        }

        private static void SetManifestEtag(this CatalogServer server, string etag)
        {
            server.ManifestEtag = etag;
        }

        private static byte[] BuildManifest(long version, string changedPath, byte[] changedBytes)
        {
            string baseManifestPath = Path.Combine(CatalogParsingTests.RepoPath("catalog"), "manifest.json");
            var manifest = JsonConvert.DeserializeObject<CatalogManifest>(File.ReadAllText(baseManifestPath, Encoding.UTF8));
            manifest.Version = version;
            manifest.Files[changedPath] = CatalogHash.Sha256Hex(changedBytes);
            return Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(manifest, Formatting.Indented));
        }

        private static byte[] TooNewManifestBytes()
        {
            var manifest = new CatalogManifest
            {
                SchemaVersion = CatalogValidator.SupportedSchemaVersion + 1,
                Version = 2,
                Files = new Dictionary<string, string>(),
            };
            return Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(manifest, Formatting.Indented));
        }

        private class EmoteCatalogOverride
        {
            public byte[] ManifestJson { get; set; }
            public byte[] EmotesJson { get; set; }
            public byte[] TextureBytes { get; set; }
        }

        private static EmoteCatalogOverride AddEmote(CatalogServer server, CatalogCache cache, string id, string command, string category)
        {
            string catalogDir = CatalogParsingTests.RepoPath("catalog");
            var manifest = JsonConvert.DeserializeObject<CatalogManifest>(File.ReadAllText(Path.Combine(catalogDir, "manifest.json"), Encoding.UTF8));
            var emotes = JsonConvert.DeserializeObject<List<CatalogEmote>>(File.ReadAllText(Path.Combine(catalogDir, "emotes.json"), Encoding.UTF8));
            byte[] textureBytes = File.ReadAllBytes(Path.Combine(catalogDir, "textures", "barbecue.png"));

            emotes.Add(new CatalogEmote
            {
                Id = id,
                Command = command,
                Names = new Dictionary<string, string> { { "en", "Tada" } },
                Categories = new List<string> { category },
            });
            manifest.Version = 2;
            manifest.Files[CatalogPaths.TextureFile(id)] = CatalogHash.Sha256Hex(textureBytes);
            manifest.Files["emotes.json"] = CatalogHash.Sha256Hex(Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(emotes, Formatting.Indented)));

            return new EmoteCatalogOverride
            {
                ManifestJson = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(manifest, Formatting.Indented)),
                EmotesJson = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(emotes, Formatting.Indented)),
                TextureBytes = textureBytes,
            };
        }
    }
}
