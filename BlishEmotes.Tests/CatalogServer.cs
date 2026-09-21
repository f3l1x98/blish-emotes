using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Threading;

namespace BlishEmotes.Tests
{
    /// <summary>A throwaway HTTP server serving a catalog directory with ETag support and request logging.</summary>
    internal sealed class CatalogServer : IDisposable
    {
        private readonly HttpListener _listener;
        private readonly Thread _thread;
        private readonly string _catalogDir;
        private readonly Dictionary<string, Func<byte[], byte[]>> _tampering = new Dictionary<string, Func<byte[], byte[]>>();
        private readonly Dictionary<string, byte[]> _overrides = new Dictionary<string, byte[]>();
        private readonly Dictionary<string, int> _requestCounts = new Dictionary<string, int>();
        private volatile bool _running = true;

        public string BaseUrl { get; }
        public string ManifestEtag { get; set; } = "\"repo-manifest\"";

        internal CatalogServer(string manifestEtag = null)
        {
            _catalogDir = CatalogParsingTests.RepoPath("catalog");
            if (manifestEtag != null)
            {
                ManifestEtag = manifestEtag;
            }

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

        internal void SetFile(string relativePath, byte[] bytes)
        {
            _overrides[relativePath] = bytes;
        }

        internal byte[] GetFile(string relativePath)
        {
            string fullPath = Path.Combine(_catalogDir, relativePath.Replace('/', Path.DirectorySeparatorChar));
            return _overrides.ContainsKey(relativePath)
                ? _overrides[relativePath]
                : File.ReadAllBytes(fullPath);
        }

        internal int RequestCount(string relativePath)
        {
            _requestCounts.TryGetValue(relativePath, out int count);
            return count;
        }

        internal int TotalFileRequestCount()
        {
            int total = 0;
            foreach (var entry in _requestCounts)
            {
                if (entry.Key != "manifest.json")
                {
                    total += entry.Value;
                }
            }
            return total;
        }

        internal void ResetRequestCounts()
        {
            _requestCounts.Clear();
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
                    return;
                }
                Handle(context);
            }
        }

        private void Handle(HttpListenerContext context)
        {
            try
            {
                string relative = context.Request.Url.AbsolutePath.TrimStart('/');
                string file = relative.StartsWith("catalog/", StringComparison.Ordinal)
                    ? relative.Substring("catalog/".Length)
                    : null;

                if (file == null)
                {
                    context.Response.StatusCode = 404;
                    context.Response.Close();
                    return;
                }

                RecordRequest(file);

                if (file == "manifest.json")
                {
                    string ifNoneMatch = context.Request.Headers["If-None-Match"];
                    if (ifNoneMatch == ManifestEtag)
                    {
                        context.Response.StatusCode = 304;
                        context.Response.AddHeader("ETag", ManifestEtag);
                        context.Response.Close();
                        return;
                    }
                }

                byte[] bytes;
                if (_overrides.TryGetValue(file, out bytes))
                {
                    // Handled below.
                }
                else
                {
                    string fullPath = Path.Combine(_catalogDir, file.Replace('/', Path.DirectorySeparatorChar));
                    if (!File.Exists(fullPath))
                    {
                        context.Response.StatusCode = 404;
                        context.Response.Close();
                        return;
                    }
                    bytes = File.ReadAllBytes(fullPath);
                }

                Func<byte[], byte[]> transform;
                if (_tampering.TryGetValue(file, out transform))
                {
                    bytes = transform(bytes);
                }

                context.Response.StatusCode = 200;
                if (file == "manifest.json")
                {
                    context.Response.AddHeader("ETag", ManifestEtag);
                }
                context.Response.ContentLength64 = bytes.Length;
                context.Response.OutputStream.Write(bytes, 0, bytes.Length);
                context.Response.Close();
            }
            catch (Exception)
            {
                // Client went away / listener stopping.
            }
        }

        private void RecordRequest(string file)
        {
            if (!_requestCounts.ContainsKey(file))
            {
                _requestCounts[file] = 0;
            }
            _requestCounts[file]++;
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
