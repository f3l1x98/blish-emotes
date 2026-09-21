using felix.BlishEmotes.Services.Catalog;
using felix.BlishEmotes.Exceptions;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace BlishEmotes.Tests
{
    /// <summary>Cache behavior: staged writes, validation before swap, offline reload, failure isolation.</summary>
    public static class CatalogCacheTests
    {
        public static void Run()
        {
            Console.WriteLine("CatalogCache");

            TestRunner.Run("fresh data directory has no cache", () =>
            {
                using (var fixture = new Fixture())
                {
                    TestRunner.False(new CatalogCache(fixture.DataRoot).HasCache());
                }
            });

            TestRunner.Run("store writes a loadable, valid cache", () =>
            {
                using (var fixture = new Fixture())
                {
                    var cache = new CatalogCache(fixture.DataRoot);
                    cache.Store(FetchedFromRealCatalog(fixture));
                    TestRunner.True(cache.HasCache());
                    var loaded = cache.Load();
                    TestRunner.Equal(64, loaded.Emotes.Count);
                    TestRunner.True(File.Exists(cache.GetTextureFilePath("barbecue")));
                    // Staging directory is gone after the swap.
                    TestRunner.False(Directory.Exists(cache.StagingDirectory));
                }
            });

            TestRunner.Run("second load runs fully from cache (offline)", () =>
            {
                using (var fixture = new Fixture())
                {
                    var cache = new CatalogCache(fixture.DataRoot);
                    cache.Store(FetchedFromRealCatalog(fixture));
                    var first = cache.Load();
                    var second = cache.Load();
                    TestRunner.Equal(first.Emotes.Count, second.Emotes.Count);
                    TestRunner.Equal(first.Manifest.Version, second.Manifest.Version);
                }
            });

            TestRunner.Run("invalid fetched catalog is not stored and leaves cache untouched", () =>
            {
                using (var fixture = new Fixture())
                {
                    var cache = new CatalogCache(fixture.DataRoot);
                    var good = FetchedFromRealCatalog(fixture);
                    cache.Store(good);

                    // Corrupt the next fetch: emote with a command missing the leading slash.
                    var bad = FetchedFromRealCatalog(fixture);
                    bad.Files["emotes.json"] = Encoding.UTF8.GetBytes(
                        Encoding.UTF8.GetString(bad.Files["emotes.json"]).Replace("\"/barbecue\"", "\"barbecue\""));
                    TestRunner.NotNull(TestRunner.Throws<CatalogException>(() => cache.Store(bad)));

                    // Existing cache still intact and valid.
                    TestRunner.True(cache.HasCache());
                    TestRunner.Equal(64, cache.Load().Emotes.Count);
                    TestRunner.False(Directory.Exists(cache.StagingDirectory));
                }
            });

            TestRunner.Run("garbage manifest in fetch is not stored", () =>
            {
                using (var fixture = new Fixture())
                {
                    var cache = new CatalogCache(fixture.DataRoot);
                    var fetched = FetchedFromRealCatalog(fixture);
                    fetched.ManifestJson = Encoding.UTF8.GetBytes("{}"); // staged manifest is garbage
                    TestRunner.NotNull(TestRunner.Throws<CatalogException>(() => cache.Store(fetched)));
                    TestRunner.False(cache.HasCache());
                }
            });

            TestRunner.Run("load on missing cache throws CatalogException", () =>
            {
                using (var fixture = new Fixture())
                {
                    TestRunner.NotNull(TestRunner.Throws<CatalogException>(() => new CatalogCache(fixture.DataRoot).Load()));
                }
            });

            TestRunner.Run("load on corrupted cache throws CatalogException", () =>
            {
                using (var fixture = new Fixture())
                {
                    var cache = new CatalogCache(fixture.DataRoot);
                    cache.Store(FetchedFromRealCatalog(fixture));
                    File.WriteAllText(Path.Combine(cache.CacheDirectory, "emotes.json"), "not json");
                    TestRunner.NotNull(TestRunner.Throws<CatalogException>(() => cache.Load()));
                }
            });

            TestRunner.Run("unsafe file path in fetch is refused", () =>
            {
                using (var fixture = new Fixture())
                {
                    var cache = new CatalogCache(fixture.DataRoot);
                    var fetched = FetchedFromRealCatalog(fixture);
                    fetched.Files["../evil.json"] = new byte[] { 1 };
                    TestRunner.NotNull(TestRunner.Throws<CatalogException>(() => cache.Store(fetched)));
                    TestRunner.False(Directory.Exists(cache.CacheDirectory));
                }
            });
        }

        /// <summary>Builds a FetchedCatalog from the real repo catalog, as a successful fetch would deliver it.</summary>
        internal static FetchedCatalog FetchedFromRealCatalog(Fixture fixture)
        {
            string catalogDir = CatalogParsingTests.RepoPath("catalog");
            var files = new Dictionary<string, byte[]>();
            foreach (string relative in Directory.EnumerateFiles(catalogDir, "*", SearchOption.AllDirectories))
            {
                string key = relative.Substring(catalogDir.Length + 1).Replace(Path.DirectorySeparatorChar, '/');
                if (key == "manifest.json")
                {
                    continue;
                }
                files[key] = File.ReadAllBytes(relative);
            }
            var data = CatalogData.FromJson(
                File.ReadAllText(Path.Combine(catalogDir, "manifest.json"), Encoding.UTF8),
                Encoding.UTF8.GetString(files["emotes.json"]),
                Encoding.UTF8.GetString(files["categories.json"]));
            return new FetchedCatalog
            {
                Data = data,
                ManifestJson = File.ReadAllBytes(Path.Combine(catalogDir, "manifest.json")),
                Files = files,
            };
        }

        internal class Fixture : IDisposable
        {
            public readonly string DataRoot;

            public Fixture()
            {
                DataRoot = Path.Combine(Path.GetTempPath(), "blish-emotes-tests-" + Guid.NewGuid().ToString("N"));
                Directory.CreateDirectory(DataRoot);
            }

            public void Dispose()
            {
                try
                {
                    Directory.Delete(DataRoot, true);
                }
                catch
                {
                    // Best effort cleanup of temp dirs.
                }
            }
        }
    }
}
