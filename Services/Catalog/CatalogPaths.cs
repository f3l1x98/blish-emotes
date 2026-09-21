using System;
using System.IO;

namespace felix.BlishEmotes.Services.Catalog
{
    /// <summary>Relative file paths used inside the catalog (manifest <c>files</c> map, cache layout).</summary>
    public static class CatalogPaths
    {
        public const string ManifestFile = "manifest.json";
        public const string EmotesFile = "emotes.json";
        public const string CategoriesFile = "categories.json";
        public const string TexturesDirectory = "textures";

        public static string TextureFile(string emoteId)
        {
            return $"{TexturesDirectory}/{emoteId}.png";
        }

        /// <summary>
        /// True if <paramref name="path"/> is a safe relative catalog path: non-empty, not rooted,
        /// no <c>..</c> segments — anything a hostile manifest could use to escape the cache directory
        /// is rejected before it is fetched or written.
        /// </summary>
        public static bool IsSafeRelativePath(string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                return false;
            }
            if (path.IndexOfAny(Path.GetInvalidPathChars()) >= 0)
            {
                return false;
            }
            if (Path.IsPathRooted(path))
            {
                return false;
            }
            string normalized = path.Replace('\\', '/');
            if (normalized.Contains("//") || normalized.StartsWith("/", StringComparison.Ordinal))
            {
                return false;
            }
            foreach (string segment in normalized.Split('/'))
            {
                if (segment == ".." || segment.Length == 0)
                {
                    return false;
                }
                // Windows-drives-away segments: "C:" is rooted on Windows (invisible on Unix),
                // and surrounding whitespace is stripped by the Win32 path APIs.
                if (segment.Length == 2 && segment[1] == ':' && char.IsLetter(segment[0]))
                {
                    return false;
                }
                if (segment != segment.Trim())
                {
                    return false;
                }
            }
            return true;
        }
    }
}
