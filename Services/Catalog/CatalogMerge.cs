using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using felix.BlishEmotes.Services.Persistence;

namespace felix.BlishEmotes.Services.Catalog
{
    /// <summary>What <see cref="CatalogMerge.Apply"/> did to the user's category state.</summary>
    public enum CatalogMergeOutcome
    {
        /// <summary>Nothing to do: already reconciled against this catalog (or a stale/orphaned pending update).</summary>
        Noop,
        /// <summary>One-time V2 migration: name-matched categories were stamped with their catalogId.</summary>
        Migrated,
        /// <summary>Empty state (only Favourites) met a catalog: default categories were created and every emote placed.</summary>
        Seeded,
        /// <summary>An applied catalog update: new emotes were placed, new categories auto-created.</summary>
        Updated,
    }

    /// <summary>Detail of a merge for logging/UX. The document mutations themselves are the result.</summary>
    public class CatalogMergeResult
    {
        public CatalogMergeOutcome Outcome { get; set; }
        /// <summary>User categories stamped with a catalogId by the migration.</summary>
        public List<string> MigratedCategoryNames { get; } = new List<string>();
        /// <summary>Linked categories auto-created by this merge, in creation order.</summary>
        public List<string> CreatedCategoryNames { get; } = new List<string>();
        /// <summary>How many emote memberships were added.</summary>
        public int PlacedEmoteCount { get; set; }
        /// <summary>Auto-creations skipped because a user category already owns the name (its emotes land uncategorized).</summary>
        public List<string> SkippedNameConflicts { get; } = new List<string>();
    }

    /// <summary>
    /// Merges the Catalog with the user persisted categories. User persisted categories trump catalog.
    /// Idempotent: re-running against the same state is a <see cref="CatalogMergeOutcome.Noop"/>.
    /// </summary>
    public static class CatalogMerge
    {
        public static CatalogMergeResult Apply(UserCategoriesDocument document, CatalogData catalog, PendingCatalogUpdate pending, CultureInfo locale)
        {
            if (catalog == null || catalog.Manifest == null || catalog.Emotes == null)
            {
                return new CatalogMergeResult { Outcome = CatalogMergeOutcome.Noop };
            }

            if (document.MergedCatalogVersion == null)
            {
                if (document.Categories.Any(category => !category.IsFavourite))
                {
                    return Migrate(document, catalog, locale);
                }
                // Seed if no categories exists, or only the favourites category (as this one cannot be changed by user)
                return Seed(document, catalog, locale);
            }

            // A pending update applies only when it describes exactly the catalog being applied
            // (an orphaned record from a sync whose cache swap failed describes a different one)
            // and is newer than what the state was last reconciled against.
            if (pending != null
                && pending.CatalogVersion == catalog.Manifest.Version
                && pending.CatalogVersion > document.MergedCatalogVersion)
            {
                return Update(document, catalog, pending, locale);
            }

            return new CatalogMergeResult { Outcome = CatalogMergeOutcome.Noop };
        }

        /// <summary>
        /// Migrates the current user categories to the catalog. Name-matched categories are stamped
        /// with their catalogId; emotes that are new to *this user* (absent from every non-Favourites
        /// category) are placed into every linked category.
        /// </summary>
        private static CatalogMergeResult Migrate(UserCategoriesDocument document, CatalogData catalog, CultureInfo locale)
        {
            var result = new CatalogMergeResult { Outcome = CatalogMergeOutcome.Migrated };
            foreach (var catalogCategory in catalog.Categories ?? new List<CatalogCategory>())
            {
                string enName = EnName(catalogCategory);
                if (enName == null)
                {
                    continue;
                }
                // Unique names on both sides make the match unambiguous; Favourites never merges
                // and an already-stamped category is never re-stamped.
                var match = document.Categories.FirstOrDefault(category =>
                    !category.IsFavourite
                    && category.CatalogId == null
                    && string.Equals(category.Name, enName, StringComparison.Ordinal));
                if (match != null)
                {
                    match.CatalogId = catalogCategory.Id;
                    result.MigratedCategoryNames.Add(match.Name);
                }
                // No match: the user renamed or deleted the old default -> ignore
            }

            var knownEmoteIds = new HashSet<string>(
                document.Categories
                    .Where(category => !category.IsFavourite)
                    .SelectMany(category => category.EmoteIds ?? new List<string>()));
            var syntheticPending = new PendingCatalogUpdate
            {
                CatalogVersion = catalog.Manifest.Version,
                NewEmoteIds = catalog.Emotes
                    .Where(emote => !knownEmoteIds.Contains(emote.Id))
                    .Select(emote => emote.Id)
                    .ToList(),
                NewCategoryIds = new List<string>(),
            };

            // Place emotes into categories using update
            var placement = Update(document, catalog, syntheticPending, locale);
            result.CreatedCategoryNames.AddRange(placement.CreatedCategoryNames);
            result.SkippedNameConflicts.AddRange(placement.SkippedNameConflicts);
            result.PlacedEmoteCount += placement.PlacedEmoteCount;

            return result;
        }

        private static CatalogMergeResult Seed(UserCategoriesDocument document, CatalogData catalog, CultureInfo locale)
        {
            // Everything in this catalog is new to the empty state.
            var everything = new PendingCatalogUpdate
            {
                CatalogVersion = catalog.Manifest.Version,
                NewEmoteIds = catalog.Emotes.Select(emote => emote.Id).ToList(),
                NewCategoryIds = (catalog.Categories ?? new List<CatalogCategory>()).Select(category => category.Id).ToList(),
            };
            var result = Update(document, catalog, everything, locale);
            result.Outcome = CatalogMergeOutcome.Seeded;
            return result;
        }

        private static CatalogMergeResult Update(UserCategoriesDocument document, CatalogData catalog, PendingCatalogUpdate pending, CultureInfo locale)
        {
            var result = new CatalogMergeResult { Outcome = CatalogMergeOutcome.Updated };
            var userDeletedCategoryIds = new HashSet<string>(document.DeletedCatalogCategoryIds ?? new List<string>());
            var linkedCatalogIds = new HashSet<string>(
                document.Categories.Where(category => category.CatalogId != null).Select(category => category.CatalogId));

            // Collect categories of new emotes
            var categoriesOfNewEmotes = new HashSet<string>();
            var emotesById = catalog.Emotes.ToDictionary(emote => emote.Id);
            foreach (string emoteId in pending.NewEmoteIds ?? new List<string>())
            {
                if (emotesById.TryGetValue(emoteId, out var emote) && emote.Categories != null)
                {
                    foreach (string categoryId in emote.Categories)
                    {
                        categoriesOfNewEmotes.Add(categoryId);
                    }
                }
            }
            foreach (var catalogCategory in catalog.Categories ?? new List<CatalogCategory>())
            {
                string catalogCategoryId = catalogCategory.Id;
                if (linkedCatalogIds.Contains(catalogCategoryId) || userDeletedCategoryIds.Contains(catalogCategoryId))
                {
                    continue;
                }
                bool newInUpdate = pending.NewCategoryIds != null && pending.NewCategoryIds.Contains(catalogCategoryId);
                // Not yet linked with user category but also not used in new emotes -> skip (unused category)
                if (!newInUpdate && !categoriesOfNewEmotes.Contains(catalogCategoryId))
                {
                    continue;
                }
                string name = CatalogNames.Resolve(catalogCategory.Names, locale, catalogCategoryId);
                // Not yet linked but there is a user category with same name -> skip (name conflict)
                if (document.Categories.Any(category => string.Equals(category.Name, name, StringComparison.Ordinal)))
                {
                    result.SkippedNameConflicts.Add(name);
                    continue;
                }
                // Create new user category
                document.Categories.Add(new PersistedCategory
                {
                    Id = Guid.NewGuid(),
                    Name = name,
                    CatalogId = catalogCategoryId,
                    EmoteIds = new List<string>(),
                });
                linkedCatalogIds.Add(catalogCategoryId);
                result.CreatedCategoryNames.Add(name);
            }

            // Place each new emote into every linked category matching its placement tags.
            // Favourites is never linked, so it is never merged. Nothing is pruned.
            var linkedUserCategories = document.Categories
                .Where(category => category.CatalogId != null)
                .ToDictionary(category => category.CatalogId);
            foreach (string emoteId in pending.NewEmoteIds ?? new List<string>())
            {
                if (!emotesById.TryGetValue(emoteId, out var emote) || emote.Categories == null)
                {
                    continue;
                }
                foreach (string categoryId in emote.Categories)
                {
                    if (userDeletedCategoryIds.Contains(categoryId))
                    {
                        // Tombstoned: the emote lands uncategorized (flat list), placeable by hand.
                        continue;
                    }
                    
                    if (linkedUserCategories.TryGetValue(categoryId, out var target) && !target.EmoteIds.Contains(emoteId))
                    {
                        target.EmoteIds.Add(emoteId);
                        result.PlacedEmoteCount++;
                    }
                }
            }

            document.MergedCatalogVersion = pending.CatalogVersion;
            return result;
        }

        private static string EnName(CatalogCategory category)
        {
            string name;
            if (category.Names != null
                && category.Names.TryGetValue(CatalogNames.FallbackLocale, out name)
                && !string.IsNullOrEmpty(name))
            {
                return name;
            }
            return null;
        }
    }
}
