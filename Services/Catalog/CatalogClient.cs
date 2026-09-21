using felix.BlishEmotes.Exceptions;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace felix.BlishEmotes.Services.Catalog
{
    /// <summary>
    /// Fetches the catalog over HTTP(S) from a raw GitHub-style base URL (branch-qualified, per the
    /// transport decision). Naive full pull: manifest first, then every file in its <c>files</c> map,
    /// each hash-verified against the manifest, then parsed and validated before anything is handed
    /// back. The cheap conditional (ETag) sync is a later ticket.
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

        /// <summary>Fetches, hash-verifies and parses the catalog. Throws <see cref="CatalogException"/> on any failure.</summary>
        public async Task<FetchedCatalog> FetchAsync()
        {
            byte[] manifestJson = await GetAsync(CatalogPaths.ManifestFile).ConfigureAwait(false);
            var manifest = Deserialize<CatalogManifest>(manifestJson);
            if (manifest.SchemaVersion > CatalogValidator.SupportedSchemaVersion)
            {
                throw new CatalogException(
                    $"catalog schemaVersion {manifest.SchemaVersion} is newer than supported {CatalogValidator.SupportedSchemaVersion} - update the module");
            }

            var files = new Dictionary<string, byte[]>();
            foreach (var entry in manifest.Files)
            {
                string path = entry.Key;
                byte[] bytes = await GetAsync(path).ConfigureAwait(false);
                if (!CatalogHash.Verify(bytes, entry.Value))
                {
                    throw new CatalogException($"hash mismatch for {path}");
                }
                files[path] = bytes;
            }

            if (!files.ContainsKey(CatalogPaths.EmotesFile) || !files.ContainsKey(CatalogPaths.CategoriesFile))
            {
                throw new CatalogException("manifest does not list emotes.json/categories.json");
            }

            var data = new CatalogData
            {
                Manifest = manifest,
                Emotes = Deserialize<List<CatalogEmote>>(files[CatalogPaths.EmotesFile]),
                Categories = Deserialize<List<CatalogCategory>>(files[CatalogPaths.CategoriesFile]),
            };

            return new FetchedCatalog { Data = data, ManifestJson = manifestJson, Files = files };
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

        private static T Deserialize<T>(byte[] json)
        {
            try
            {
                return JsonConvert.DeserializeObject<T>(Encoding.UTF8.GetString(json));
            }
            catch (Exception e)
            {
                throw new CatalogException($"failed to parse {typeof(T)}", e);
            }
        }

        public void Dispose()
        {
            _httpClient.Dispose();
        }
    }
}
