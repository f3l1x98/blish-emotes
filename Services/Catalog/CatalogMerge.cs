using felix.BlishEmotes.Services.Persistence;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

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
    /// The catalog merge into the user-persisted categories (per the wayfinder decision "Catalog
    /// merge into user-persisted categories"): the user's categories remain the only rendered
    /// state; catalog categories are placement hints. Blish-free and side-effect-free so every
    /// rule below is unit-tested:
    ///
    /// <list type="bullet">
    /// <item>Migration (state never reconciled, user has categories): each category whose name
    /// matches a catalog category's en name is stamped with that catalogId — "if you touched the
    /// name, it's yours". Nothing is placed, nothing created, nothing pruned.</item>
    /// <item>Seeding (state never reconciled, nothing but Favourites — fresh install or a
    /// corrupted file): every untombstoned catalog category is auto-created as a linked category
    /// and every emote placed per its tags.</item>
    /// <item>Update (a fetched catalog update is applied): each new emote lands in every linked
    /// category matching its placement tags — no untouched-vs-modified distinction; a catalog
    /// category new in the update, or tagged by a new emote, is auto-created unless tombstoned;
    /// tombstoned tags land uncategorized. Nothing is ever pruned.</item>
    /// </list>
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
                    return Migrate(document, catalog);
                }
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

        private static CatalogMergeResult Migrate(UserCategoriesDocument document, CatalogData catalog)
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
                // No match: the user renamed or deleted the old default — "if you touched the
                // name, it's yours"; deleted defaults are simply absent and stay that way.
            }
            document.MergedCatalogVersion = catalog.Manifest.Version;
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
            var tombstones = new HashSet<string>(document.DeletedCatalogCategoryIds ?? new List<string>());
            var linkedCatalogIds = new HashSet<string>(
                document.Categories.Where(category => category.CatalogId != null).Select(category => category.CatalogId));

            // Auto-create linked categories for catalog categories the user doesn't have:
            // every category new in this update, plus every placement tag of a new emote that no
            // linked category matches. Tombstoned ones are never resurrected; the user's unique
            // category names are respected (a conflict skips creation and the emotes land
            // uncategorized). New categories append at the end of the user's order.
            var tagsOfNewEmotes = new HashSet<string>();
            var emotesById = catalog.Emotes.ToDictionary(emote => emote.Id);
            foreach (string emoteId in pending.NewEmoteIds ?? new List<string>())
            {
                CatalogEmote emote;
                if (emotesById.TryGetValue(emoteId, out emote) && emote.Categories != null)
                {
                    foreach (string tag in emote.Categories)
                    {
                        tagsOfNewEmotes.Add(tag);
                    }
                }
            }
            foreach (var catalogCategory in catalog.Categories ?? new List<CatalogCategory>())
            {
                string catalogCategoryId = catalogCategory.Id;
                if (linkedCatalogIds.Contains(catalogCategoryId) || tombstones.Contains(catalogCategoryId))
                {
                    continue;
                }
                bool newInUpdate = pending.NewCategoryIds != null && pending.NewCategoryIds.Contains(catalogCategoryId);
                if (!newInUpdate && !tagsOfNewEmotes.Contains(catalogCategoryId))
                {
                    continue;
                }
                string name = CatalogNames.Resolve(catalogCategory.Names, locale, catalogCategoryId);
                if (document.Categories.Any(category => string.Equals(category.Name, name, StringComparison.Ordinal)))
                {
                    result.SkippedNameConflicts.Add(name);
                    continue;
                }
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
            var linkedByCatalogId = document.Categories
                .Where(category => category.CatalogId != null)
                .ToLookup(category => category.CatalogId);
            foreach (string emoteId in pending.NewEmoteIds ?? new List<string>())
            {
                CatalogEmote emote;
                if (!emotesById.TryGetValue(emoteId, out emote) || emote.Categories == null)
                {
                    continue;
                }
                foreach (string tag in emote.Categories)
                {
                    if (tombstones.Contains(tag))
                    {
                        // Tombstoned: the emote lands uncategorized (flat list), placeable by hand.
                        continue;
                    }
                    foreach (var target in linkedByCatalogId[tag])
                    {
                        if (!target.EmoteIds.Contains(emoteId))
                        {
                            target.EmoteIds.Add(emoteId);
                            result.PlacedEmoteCount++;
                        }
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
