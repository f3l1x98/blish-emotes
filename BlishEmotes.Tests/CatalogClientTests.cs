using felix.BlishEmotes.Services.Catalog;
using felix.BlishEmotes.Exceptions;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;
using System.Threading;

namespace BlishEmotes.Tests
{
    /// <summary>
    /// Full-pull fetch against a local HTTP server serving the real repo catalog: manifest →
    /// hash-verified files → parse → validate. Also covers the failure modes that must leave the
    /// module on its fallback data.
    /// </summary>
    public static class CatalogClientTests
    {
        public static void Run()
        {
            Console.WriteLine("CatalogClient (local HTTP)");

            TestRunner.Run("full pull fetches and validates the real catalog", () =>
            {
                using (var server = new CatalogServer())
                using (var client = new CatalogClient(server.BaseUrl))
                {
                    var fetched = client.FetchAsync().GetAwaiter().GetResult();
                    TestRunner.Equal(64, fetched.Data.Emotes.Count);
                    TestRunner.True(fetched.Files.Count >= 66);
                    TestRunner.True(fetched.ManifestJson.Length > 0);
                }
            });

            TestRunner.Run("missing manifest fails with CatalogException", () =>
            {
                using (var server = new CatalogServer())
                using (var client = new CatalogClient(server.BaseUrl + "nope/"))
                {
                    TestRunner.NotNull(TestRunner.Throws<CatalogException>(
                        () => client.FetchAsync().GetAwaiter().GetResult()));
                }
            });

            TestRunner.Run("tampered file fails the hash check", () =>
            {
                using (var server = new CatalogServer())
                using (var client = new CatalogClient(server.BaseUrl))
                {
                    server.Tamper("emotes.json", bytes => Encoding.UTF8.GetBytes(
                        Encoding.UTF8.GetString(bytes).Replace("\"Barbecue\"", "\"Barbecue!\"")));
                    var e = TestRunner.Throws<CatalogException>(() => client.FetchAsync().GetAwaiter().GetResult());
                    TestRunner.NotNull(e);
                    TestRunner.True(e.Message.Contains("hash mismatch"), e.Message);
                }
            });

            TestRunner.Run("fetch failure leaves a previously cached catalog loadable (offline freeze)", () =>
            {
                using (var server = new CatalogServer())
                using (var fixture = new CatalogCacheTests.Fixture())
                {
                    var fetched = new CatalogClient(server.BaseUrl).FetchAsync().GetAwaiter().GetResult();
                    var cache = new CatalogCache(fixture.DataRoot);
                    cache.Store(fetched);

                    // Network "blocked": server down.
                    server.Stop();
                    using (var client = new CatalogClient(server.BaseUrl))
                    {
                        TestRunner.NotNull(TestRunner.Throws<CatalogException>(
                            () => client.FetchAsync().GetAwaiter().GetResult()));
                    }

                    // The cache still renders the same emotes.
                    TestRunner.True(cache.HasCache());
                    TestRunner.Equal(64, cache.Load().Emotes.Count);
                }
            });
        }

        /// <summary>A throwaway HTTP server serving the repo's catalog/ directory.</summary>
        private class CatalogServer : IDisposable
        {
            private readonly HttpListener _listener;
            private readonly Thread _thread;
            private readonly string _catalogDir;
            private readonly Dictionary<string, Func<byte[], byte[]>> _tampering =
                new Dictionary<string, Func<byte[], byte[]>>();
            private volatile bool _running = true;

            public string BaseUrl { get; }

            internal CatalogServer()
            {
                _catalogDir = CatalogParsingTests.RepoPath("catalog");
                int port = 18340 + new Random().Next(200);
                BaseUrl = $"http://localhost:{port}/catalog/";
                _listener = new HttpListener();
                _listener.Prefixes.Add($"http://localhost:{port}/");
                _listener.Start();
                _thread = new Thread(Serve) { IsBackground = true };
                _thread.Start();
            }

            internal void Tamper(string file, Func<byte[], byte[]> transform)
            {
                _tampering[file] = transform;
            }

            private void Serve()
            {
                while (_running)
                {
                    HttpListenerContext context;
                    try
                    {
                        context = _listener.GetContext();
                    }
                    catch (Exception)
                    {
                        return; // Listener stopped.
                    }
                    Handle(context);
                }
            }

            private void Handle(HttpListenerContext context)
            {
                try
                {
                    string relative = context.Request.Url.AbsolutePath.TrimStart('/'); // "catalog/<file>"
                    string file = relative.StartsWith("catalog/", StringComparison.Ordinal)
                        ? relative.Substring("catalog/".Length)
                        : null;
                    string fullPath = file != null
                        ? Path.Combine(_catalogDir, file.Replace('/', Path.DirectorySeparatorChar))
                        : null;
                    if (fullPath == null || !File.Exists(fullPath))
                    {
                        context.Response.StatusCode = 404;
                    }
                    else
                    {
                        byte[] bytes = File.ReadAllBytes(fullPath);
                        Func<byte[], byte[]> transform;
                        if (_tampering.TryGetValue(file, out transform))
                        {
                            bytes = transform(bytes);
                        }
                        context.Response.StatusCode = 200;
                        context.Response.ContentLength64 = bytes.Length;
                        context.Response.OutputStream.Write(bytes, 0, bytes.Length);
                    }
                    context.Response.Close();
                }
                catch (Exception)
                {
                    // Client went away / listener stopping.
                }
            }

            internal void Stop()
            {
                _running = false;
                try { _listener.Stop(); } catch (Exception) { }
                try { _listener.Close(); } catch (Exception) { }
            }

            public void Dispose()
            {
                Stop();
            }
        }
    }
}
