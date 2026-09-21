using System.Collections.Generic;
using System.Linq;

namespace felix.BlishEmotes.Services.Catalog
{
    /// <summary>
    /// Sanity-checks a fetched (or cached) catalog before it is allowed to replace what the module
    /// renders. Mirrors the invariants of <c>scripts/validate_catalog.py</c>: a non-empty error list
    /// means the caller must stay on its fallback data.
    /// </summary>
    public static class CatalogValidator
    {
        /// <summary>Manifest JSON format version this module understands; a newer one aborts the sync.</summary>
        public const int SupportedSchemaVersion = 1;

        public static List<string> Validate(CatalogData catalog)
        {
            var errors = new List<string>();

            if (catalog == null)
            {
                errors.Add("catalog is null");
                return errors;
            }

            ValidateManifest(catalog.Manifest, errors);
            if (errors.Count > 0)
            {
                // Manifest unusable (too new / malformed) — the rest cannot be judged against it.
                return errors;
            }

            var categoryIds = new HashSet<string>();
            if (catalog.Categories != null)
            {
                foreach (var category in catalog.Categories)
                {
                    if (category == null || string.IsNullOrEmpty(category.Id))
                    {
                        errors.Add("category without id");
                        continue;
                    }
                    if (!categoryIds.Add(category.Id))
                    {
                        errors.Add($"duplicate category id: {category.Id}");
                    }
                    if (category.Names == null || !HasName(category.Names))
                    {
                        errors.Add($"category {category.Id}: no 'en' name");
                    }
                }
            }

            if (catalog.Emotes == null || catalog.Emotes.Count == 0)
            {
                errors.Add("emotes.json: no emotes");
                return errors;
            }

            var seenIds = new HashSet<string>();
            foreach (var emote in catalog.Emotes)
            {
                if (emote == null || string.IsNullOrEmpty(emote.Id))
                {
                    errors.Add("emote without id");
                    continue;
                }
                if (!seenIds.Add(emote.Id))
                {
                    errors.Add($"duplicate id: {emote.Id}");
                }
                if (string.IsNullOrEmpty(emote.Command) || !emote.Command.StartsWith("/"))
                {
                    errors.Add($"{emote.Id}: command '{emote.Command}' does not start with '/'");
                }
                if (emote.AlternativeCommands != null)
                {
                    foreach (var command in emote.AlternativeCommands)
                    {
                        if (string.IsNullOrEmpty(command) || !command.StartsWith("/"))
                        {
                            errors.Add($"{emote.Id}: alternative command '{command}' does not start with '/'");
                        }
                        if (command == emote.Command)
                        {
                            errors.Add($"{emote.Id}: alternative command '{command}' duplicates primary");
                        }
                    }
                }
                if (emote.Names == null || !HasName(emote.Names))
                {
                    errors.Add($"{emote.Id}: no 'en' name");
                }
                if (emote.Categories == null || emote.Categories.Count == 0)
                {
                    errors.Add($"{emote.Id}: in no category");
                }
                else
                {
                    foreach (var categoryId in emote.Categories)
                    {
                        if (!categoryIds.Contains(categoryId))
                        {
                            errors.Add($"{emote.Id}: references unknown category '{categoryId}'");
                        }
                    }
                }
                // The module renders one texture per emote; the manifest must know it.
                if (!catalog.Manifest.Files.ContainsKey(CatalogPaths.TextureFile(emote.Id)))
                {
                    errors.Add($"{emote.Id}: missing texture {CatalogPaths.TextureFile(emote.Id)}");
                }
            }

            return errors;
        }

        private static void ValidateManifest(CatalogManifest manifest, List<string> errors)
        {
            if (manifest == null)
            {
                errors.Add("manifest.json: missing or malformed");
                return;
            }
            if (manifest.SchemaVersion > SupportedSchemaVersion)
            {
                errors.Add(
                    $"manifest.json: schemaVersion {manifest.SchemaVersion} is newer than supported {SupportedSchemaVersion} - update the module");
                return;
            }
            if (manifest.Files == null || manifest.Files.Count == 0)
            {
                errors.Add("manifest.json: empty files map");
                return;
            }
            foreach (var path in manifest.Files.Keys)
            {
                if (!CatalogPaths.IsSafeRelativePath(path))
                {
                    errors.Add($"manifest.json: unsafe file path '{path}'");
                }
            }
        }

        private static bool HasName(IDictionary<string, string> names)
        {
            return names.TryGetValue(CatalogNames.FallbackLocale, out string name)
                && !string.IsNullOrEmpty(name);
        }
    }
}
