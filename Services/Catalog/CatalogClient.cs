using felix.BlishEmotes.Exceptions;
using System;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;

namespace felix.BlishEmotes.Services.Catalog
{
    /// <summary>
    /// Fetches catalog files over HTTP(S) from a raw GitHub-style base URL (branch-qualified, per the
    /// transport decision): a conditional GET on the manifest (<c>If-None-Match</c> / <c>304</c>) plus
    /// hash-verified single-file GETs. Orchestration - schema gate, hash-diff, staging, swap - lives
    /// in <see cref="CatalogSync"/>.
    /// </summary>
    public class CatalogClient : IDisposable
    {
        private readonly HttpClient _httpClient;
        private readonly string _baseUrl;

        public CatalogClient(string baseUrl)
        {
            _baseUrl = baseUrl != null && baseUrl.EndsWith("/", StringComparison.Ordinal) ? baseUrl : baseUrl + "/";
            _httpClient = new HttpClient
            {
                Timeout = TimeSpan.FromMinutes(2),
            };
        }

        /// <summary>Result of a conditional manifest GET.</summary>
        public class ManifestFetchResult
        {
            /// <summary>True when the server returned <c>304 Not Modified</c>.</summary>
            public bool NotModified { get; set; }
            /// <summary>The manifest bytes for a <c>200 OK</c> response.</summary>
            public byte[] ManifestJson { get; set; }
            /// <summary>ETag returned by the server, to store for the next conditional request.</summary>
            public string ETag { get; set; }
        }

        /// <summary>
        /// Performs a conditional GET on the manifest. Returns <see cref="ManifestFetchResult.NotModified"/>
        /// when the server replies <c>304</c>; otherwise returns the manifest bytes and the new ETag.
        /// </summary>
        public async Task<ManifestFetchResult> GetManifestAsync(string etag)
        {
            string url = _baseUrl + CatalogPaths.ManifestFile;
            using (var request = new HttpRequestMessage(HttpMethod.Get, url))
            {
                if (!string.IsNullOrEmpty(etag))
                {
                    request.Headers.TryAddWithoutValidation("If-None-Match", etag);
                }

                using (var response = await _httpClient.SendAsync(request).ConfigureAwait(false))
                {
                    if ((int)response.StatusCode == 304)
                    {
                        return new ManifestFetchResult { NotModified = true, ETag = etag };
                    }

                    if (!response.IsSuccessStatusCode)
                    {
                        throw new CatalogException($"failed to fetch manifest: {(int)response.StatusCode} {response.ReasonPhrase}");
                    }

                    byte[] manifestJson = await response.Content.ReadAsByteArrayAsync().ConfigureAwait(false);
                    string responseEtag = null;
                    if (response.Headers.TryGetValues("ETag", out var etagValues))
                    {
                        responseEtag = etagValues.FirstOrDefault();
                    }

                    return new ManifestFetchResult { NotModified = false, ManifestJson = manifestJson, ETag = responseEtag };
                }
            }
        }

        /// <summary>Fetches a single catalog file and verifies it against the manifest hash.</summary>
        public async Task<byte[]> GetFileAsync(string path, string expectedHash)
        {
            byte[] bytes = await GetAsync(path).ConfigureAwait(false);
            if (!CatalogHash.Verify(bytes, expectedHash))
            {
                throw new CatalogException($"hash mismatch for {path}");
            }
            return bytes;
        }

        private async Task<byte[]> GetAsync(string path)
        {
            if (!CatalogPaths.IsSafeRelativePath(path))
            {
                throw new CatalogException($"unsafe file path '{path}'");
            }
            try
            {
                using (var response = await _httpClient.GetAsync(_baseUrl + path).ConfigureAwait(false))
                {
                    if (!response.IsSuccessStatusCode)
                    {
                        throw new CatalogException($"failed to fetch {path}: {(int)response.StatusCode} {response.ReasonPhrase}");
                    }
                    return await response.Content.ReadAsByteArrayAsync().ConfigureAwait(false);
                }
            }
            catch (CatalogException)
            {
                throw;
            }
            catch (Exception e)
            {
                throw new CatalogException($"failed to fetch {path}", e);
            }
        }

        public void Dispose()
        {
            _httpClient.Dispose();
        }
    }
}
