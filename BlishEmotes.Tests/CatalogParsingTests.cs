using felix.BlishEmotes.Services.Catalog;
using Newtonsoft.Json;
using System;
using System.IO;
using System.Text;

namespace BlishEmotes.Tests
{
    /// <summary>Parses and validates the real catalog committed in this repo — the artifact the module fetches.</summary>
    public static class CatalogParsingTests
    {
        public static void Run()
        {
            Console.WriteLine("CatalogParsing (real catalog/)");
            string catalogDir = RepoPath("catalog");

            TestRunner.Run("manifest.json parses with expected shape", () =>
            {
                var manifest = ParseManifest(catalogDir);
                TestRunner.Equal(CatalogValidator.SupportedSchemaVersion, manifest.SchemaVersion);
                TestRunner.True(manifest.Version >= 1);
                TestRunner.True(manifest.Files.Count >= 64 + 2, "66 files: 2 json + 64 textures");
            });

            TestRunner.Run("all 64 emotes parse and the catalog validates", () =>
            {
                var manifest = ParseManifest(catalogDir);
                var data = CatalogData.FromJson(
                    File.ReadAllText(Path.Combine(catalogDir, "manifest.json"), Encoding.UTF8),
                    File.ReadAllText(Path.Combine(catalogDir, "emotes.json"), Encoding.UTF8),
                    File.ReadAllText(Path.Combine(catalogDir, "categories.json"), Encoding.UTF8));
                TestRunner.Equal(64, data.Emotes.Count);
                TestRunner.Empty(CatalogValidator.Validate(data));
                TestRunner.True(manifest.Files.ContainsKey("textures/barbecue.png"));
            });

            TestRunner.Run("manifest hashes match the real files", () =>
            {
                var manifest = ParseManifest(catalogDir);
                foreach (var entry in manifest.Files)
                {
                    string fullPath = Path.Combine(catalogDir, entry.Key.Replace('/', Path.DirectorySeparatorChar));
                    TestRunner.True(File.Exists(fullPath), $"file {entry.Key} exists");
                    TestRunner.True(CatalogHash.Sha256Hex(File.ReadAllBytes(fullPath)).Equals(entry.Value, StringComparison.OrdinalIgnoreCase),
                        $"hash of {entry.Key}");
                }
            });

            TestRunner.Run("localized names resolve through the fallback chain", () =>
            {
                var data = ParseData(catalogDir);
                var barbecue = data.Emotes.Find(e => e.Id == "barbecue");
                TestRunner.Equal("Grillen", CatalogNames.Resolve(barbecue.Names, new System.Globalization.CultureInfo("de"), barbecue.Id));
                TestRunner.Equal("Barbecue", CatalogNames.Resolve(barbecue.Names, new System.Globalization.CultureInfo("fr"), barbecue.Id));
                var bless = data.Emotes.Find(e => e.Id == "bless");
                TestRunner.Equal("Bless", CatalogNames.Resolve(bless.Names, new System.Globalization.CultureInfo("de"), bless.Id));
            });

            TestRunner.Run("alternativeCommands present where expected", () =>
            {
                var data = ParseData(catalogDir);
                TestRunner.Equal(2, data.Emotes.Find(e => e.Id == "barbecue").AlternativeCommands.Count);
                TestRunner.Equal(1, data.Emotes.Find(e => e.Id == "boogie").AlternativeCommands.Count);
                TestRunner.True(data.Emotes.Find(e => e.Id == "beckon").AlternativeCommands == null);
            });

            TestRunner.Run("malformed json fails to parse", () =>
            {
                TestRunner.NotNull(TestRunner.Throws<Exception>(() =>
                    CatalogData.FromJson("{ not json", "[]", "[]")));
            });
        }

        private static CatalogManifest ParseManifest(string catalogDir)
        {
            return JsonConvert.DeserializeObject<CatalogManifest>(
                File.ReadAllText(Path.Combine(catalogDir, "manifest.json"), Encoding.UTF8));
        }

        private static CatalogData ParseData(string catalogDir)
        {
            return CatalogData.FromJson(
                File.ReadAllText(Path.Combine(catalogDir, "manifest.json"), Encoding.UTF8),
                File.ReadAllText(Path.Combine(catalogDir, "emotes.json"), Encoding.UTF8),
                File.ReadAllText(Path.Combine(catalogDir, "categories.json"), Encoding.UTF8));
        }

        internal static string RepoPath(string relative)
        {
            var dir = new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory);
            while (dir != null && !Directory.Exists(Path.Combine(dir.FullName, "catalog")))
            {
                dir = dir.Parent;
            }
            if (dir == null)
            {
                throw new InvalidOperationException("repo root with catalog/ not found");
            }
            return Path.Combine(dir.FullName, relative);
        }
    }
}
