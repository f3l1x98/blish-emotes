using felix.BlishEmotes.Services.Catalog;
using felix.BlishEmotes.Exceptions;
using System;
using System.IO;
using System.Text;

namespace BlishEmotes.Tests
{
    /// <summary>
    /// Client contract against a local HTTP server serving the real repo catalog: conditional
    /// manifest GET (If-None-Match / 304) and hash-verified single-file GETs, plus the failure
    /// modes that must throw <see cref="CatalogException"/>.
    /// </summary>
    public static class CatalogClientTests
    {
        public static void Run()
        {
            Console.WriteLine("CatalogClient (local HTTP)");

            TestRunner.Run("manifest GET returns bytes and the server ETag", () =>
            {
                using (var server = new CatalogServer())
                using (var client = new CatalogClient(server.BaseUrl))
                {
                    var result = client.GetManifestAsync(null).GetAwaiter().GetResult();
                    TestRunner.False(result.NotModified);
                    TestRunner.True(result.ManifestJson.Length > 0);
                    TestRunner.Equal(server.ManifestEtag, result.ETag);
                }
            });

            TestRunner.Run("conditional manifest GET: matching ETag returns 304, stale returns 200", () =>
            {
                using (var server = new CatalogServer())
                using (var client = new CatalogClient(server.BaseUrl))
                {
                    var notModified = client.GetManifestAsync(server.ManifestEtag).GetAwaiter().GetResult();
                    TestRunner.True(notModified.NotModified);

                    var modified = client.GetManifestAsync("\"stale-etag\"").GetAwaiter().GetResult();
                    TestRunner.False(modified.NotModified);
                    TestRunner.True(modified.ManifestJson.Length > 0);
                }
            });

            TestRunner.Run("missing manifest fails with CatalogException", () =>
            {
                using (var server = new CatalogServer())
                using (var client = new CatalogClient(server.BaseUrl + "nope/"))
                {
                    TestRunner.NotNull(TestRunner.Throws<CatalogException>(
                        () => client.GetManifestAsync(null).GetAwaiter().GetResult()));
                }
            });

            TestRunner.Run("tampered file fails the hash check", () =>
            {
                using (var server = new CatalogServer())
                using (var client = new CatalogClient(server.BaseUrl))
                {
                    string emotesPath = Path.Combine(CatalogParsingTests.RepoPath("catalog"), "emotes.json");
                    string expectedHash = CatalogHash.Sha256Hex(File.ReadAllBytes(emotesPath));

                    server.Tamper("emotes.json", bytes => Encoding.UTF8.GetBytes(
                        Encoding.UTF8.GetString(bytes).Replace("\"Barbecue\"", "\"Barbecue!\"")));
                    var e = TestRunner.Throws<CatalogException>(
                        () => client.GetFileAsync("emotes.json", expectedHash).GetAwaiter().GetResult());
                    TestRunner.NotNull(e);
                    TestRunner.True(e.Message.Contains("hash mismatch"), e.Message);
                }
            });
        }
    }
}
