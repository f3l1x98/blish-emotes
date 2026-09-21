using System.Collections.Generic;
using System.Globalization;

namespace felix.BlishEmotes.Services.Catalog
{
    /// <summary>
    /// Resolves an emote's display name from the catalog's per-locale <c>names</c> map:
    /// exact locale → <c>en</c> → title-cased id (the fallback chain settled by the
    /// catalog-schema prototype).
    /// </summary>
    public static class CatalogNames
    {
        public const string FallbackLocale = "en";

        public static string Resolve(IDictionary<string, string> names, CultureInfo locale, string emoteId)
        {
            if (names != null)
            {
                string key = !string.IsNullOrEmpty(locale?.TwoLetterISOLanguageName)
                    ? locale.TwoLetterISOLanguageName
                    : FallbackLocale;
                if (names.TryGetValue(key, out string byLocale) && !string.IsNullOrEmpty(byLocale))
                {
                    return byLocale;
                }
                if (names.TryGetValue(FallbackLocale, out string byFallback) && !string.IsNullOrEmpty(byFallback))
                {
                    return byFallback;
                }
            }
            return TitleCaseId(emoteId);
        }

        /// <summary>Fallback label for an emote without a usable name: the id, title-cased (<c>thumbsdown</c> → <c>Thumbsdown</c>).</summary>
        public static string TitleCaseId(string emoteId)
        {
            if (string.IsNullOrEmpty(emoteId))
            {
                return string.Empty;
            }
            return CultureInfo.InvariantCulture.TextInfo.ToTitleCase(emoteId.ToLowerInvariant());
        }
    }
}
