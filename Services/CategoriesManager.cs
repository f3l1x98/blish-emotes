using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Blish_HUD;
using felix.BlishEmotes.Exceptions;
using felix.BlishEmotes.Services;
using felix.BlishEmotes.Services.Catalog;
using felix.BlishEmotes.Services.Persistence;
using Microsoft.Xna.Framework.Graphics;

namespace felix.BlishEmotes
{
    class CategoriesManager
    {
        public static readonly string NEW_CATEGORY_NAME = "New Category";
        private static readonly Logger Logger = Logger.GetLogger<CategoriesManager>();

        public event EventHandler<List<Category>> CategoriesUpdated;
        // Cache special Favourite category id
        public Guid FavouriteCategoryId { get; private set; }

        private TexturesManager TexturesManager;
        private PersistenceManager PersistenceManager;
        // Cache mapping ids to objects
        private Dictionary<Guid, Category> categories;

        public CategoriesManager(TexturesManager texturesManager, PersistenceManager persistenceManager)
        {
            TexturesManager = texturesManager;
            PersistenceManager = persistenceManager;
            categories = new Dictionary<Guid, Category>();
        }

        public void Load()
        {
            try
            {
                var document = PersistenceManager.LoadDocument();
                foreach (var persisted in document.Categories)
                {
                    var category = FromPersisted(persisted);
                    if (category.IsFavourite)
                    {
                        FavouriteCategoryId = category.Id;
                    }
                    categories.Add(category.Id, category);
                }
                // Ensure that Favourite category exists (in case user manually deleted it from file)
                if (FavouriteCategoryId == null)
                {
                    CreateFavouriteCategory();
                }
            }
            catch (FileNotFoundException)
            {
                // Fresh install: only Favourites exists until the first fetch creates linked
                // categories from the catalog.
                CreateFavouriteCategory();
                PersistenceManager.SaveCategories(categories.Values.ToList());
            }
            catch (Exception ex)
            {
                Logger.Error("Failed to load categories.");
                Logger.Error(ex.Message);
                Logger.Debug(ex.StackTrace);
            }
        }

        private Category FromPersisted(PersistedCategory persisted)
        {
            string textureFileName = persisted.TextureFileName ?? Textures.CategorySettingsIcon.ToString();
            return new Category()
            {
                Id = persisted.Id,
                Name = persisted.Name,
                EmoteIds = persisted.EmoteIds,
                IsFavourite = persisted.IsFavourite,
                TextureFileName = textureFileName,
                CatalogId = persisted.CatalogId,
                Texture = GetTexture(textureFileName),
            };
        }
        private Texture2D GetTexture(string textureFileName)
        {
            return TexturesManager.GetTexture(GetTexturesManagerKey(textureFileName));
        }
        private string GetTexturesManagerKey(Category category)
        {
            return GetTexturesManagerKey(category.TextureFileName);
        }
        private string GetTexturesManagerKey(string textureFileName)
        {
            return Path.GetFileNameWithoutExtension(textureFileName);
        }

        public void ReorderCategories(List<Category> newOrder, bool saveToFile = true)
        {
            if (newOrder.Count != categories.Count)
            {
                Logger.Error("Reordered category list length does not match current category list length.");
                return;
            }

            categories.Clear();
            foreach (var category in newOrder)
            {
                categories.Add(category.Id, category);
            }

            if (saveToFile)
            {
                PersistenceManager.SaveCategories(categories.Values.ToList());
            }
        }

        public Category CreateCategory(string name, string textureFileName, List<Emote> emotes = null, bool saveToFile = true)
        {
            return CreateCategory(name, textureFileName, emotes?.Select((emote) => emote.Id).ToList(), emotes, false, saveToFile);
        }
        private Category CreateCategory(string name, string textureFileName = null, List<string> emoteIds = null, List<Emote> emotes = null, bool isFavourite = false, bool saveToFile = true)
        {
            emoteIds = emoteIds ?? new List<string>();
            emotes = emotes ?? new List<Emote>();
            textureFileName = textureFileName ?? Textures.CategorySettingsIcon.ToString();

            if (name == NEW_CATEGORY_NAME)
            {
                // Append number to allow creating new categories rapidly
                int next = GetNextNewCategoryNumber();
                if (next > 0)
                {
                    name = $"{name} {next}";
                }
            }

            AssertUniqueName(name);

            var newCategory = new Category()
            {
                Id = Guid.NewGuid(),
                Name = name,
                IsFavourite = isFavourite,
                EmoteIds = emoteIds,
                Emotes = emotes,
                TextureFileName = textureFileName,
                Texture = GetTexture(textureFileName),
            };
            categories.Add(newCategory.Id, newCategory);
            if (isFavourite)
            {
                FavouriteCategoryId = newCategory.Id;
            }

            if (saveToFile)
            {
                PersistenceManager.SaveCategories(categories.Values.ToList());
            }

            Logger.Debug($"Created category {newCategory.Id}-{newCategory.Name}");
            CategoriesUpdated?.Invoke(this, GetAll());
            return newCategory.Clone();
        }

        public Category UpdateCategory(Category category, bool saveToFile = true)
        {
            Category current;
            categories.TryGetValue(category.Id, out current);
            if (current == null)
            {
                Logger.Debug($"No category found for id {category.Id}");
                throw new NotFoundException($"No category found for id {category.Id}");
            }
            if (current.Name != category.Name)
            {
                // Name update -> unique check
                AssertUniqueName(category.Name);
            }

            // This works, because the new TextureFileName is the absolute fileName of the new texture
            if (current.TextureFileName != category.TextureFileName)
            {
                if (File.Exists(category.TextureFileName))
                {
                    // Copy to emotes dir
                    string newTextureFileName = $"{category.Name}{Path.GetExtension(category.TextureFileName)}";
                    string absoluteFilePath = Path.Combine(TexturesManager.ModuleDataTexturesDirectory, newTextureFileName);
                    if (File.Exists(absoluteFilePath))
                    {
                        File.Delete(absoluteFilePath);
                    }
                    File.Copy(category.TextureFileName, absoluteFilePath);

                    // Update category and texture cache
                    category.TextureFileName = newTextureFileName;
                    var cacheKey = GetTexturesManagerKey(category);
                    TexturesManager.UpdateTexture(cacheKey, category.Texture);
                }
                else
                {
                    Logger.Error($"Unable to update texture - {category.TextureFileName} not found!");
                }
            }

            categories[category.Id] = category;
            if (saveToFile)
            {
                PersistenceManager.SaveCategories(categories.Values.ToList());
            }

            Logger.Debug($"Updated category {category.Id}-{category.Name}");
            CategoriesUpdated?.Invoke(this, GetAll());
            return category.Clone();
        }

        public bool DeleteCategory(Category category, bool saveToFile = true)
        {
            Category current;
            categories.TryGetValue(category.Id, out current);
            if (current == null)
            {
                Logger.Debug($"Tried deleting non-existing category with id {category.Id}");
                // If it does not exist -> just claim delete was successful xD
                return true;
            }
            // Prevent deleting favourite category
            if (current.IsFavourite)
            {
                Logger.Debug("Tried to delete favourite category -> abort.");
                return false;
            }

            if (category.TextureFileName != Textures.CategorySettingsIcon.ToString())
            {
                // Delete file
                var fileName = Path.Combine(TexturesManager.ModuleDataTexturesDirectory, category.TextureFileName);
                File.Delete(fileName);
            }
            categories.Remove(category.Id);
            // Recreate Dictionary in order to bypass optimization that would insert next item at the now empty space
            categories = new Dictionary<Guid, Category>(categories);
            if (current.CatalogId != null)
            {
                // The user deleted a linked category: tombstone its catalog category so a later
                // catalog update never auto-creates it again (emotes tagged with it land uncategorized).
                PersistenceManager.RecordDeletedCatalogCategory(current.CatalogId);
            }
            if (saveToFile)
            {
                PersistenceManager.SaveCategories(categories.Values.ToList());
            }

            Logger.Debug($"Deleted category {category.Id}-{category.Name}");
            CategoriesUpdated?.Invoke(this, GetAll());
            return true;
        }

        public Category GetById(Guid id)
        {
            Category category;
            categories.TryGetValue(id, out category);
            if (category == null)
            {
                Logger.Debug($"No category found for id {id}");
                throw new NotFoundException($"No category found for id {id}");
            }
            return category.Clone();
        }

        public List<Category> GetAll()
        {
            return new List<Category>(categories.Values.Select((category) => category.Clone()));
        }

        public bool IsEmoteInCategory(Guid categoryId, Emote emote)
        {
            Category category;
            categories.TryGetValue(categoryId, out category);
            if (category == null)
            {
                Logger.Debug($"No category found for id {categoryId}");
                throw new NotFoundException($"No category found for id {categoryId}");
            }
            return category.EmoteIds.Contains(emote.Id);
        }

        public void ToggleEmoteFromCategory(Guid categoryId, Emote emote, bool saveToFile = true)
        {
            if (categoryId == null)
            {
                Logger.Error("categoryId is not set!");
                return;
            }

            try
            {
                if (IsEmoteInCategory(categoryId, emote))
                {
                    categories[categoryId].RemoveEmote(emote);
                }
                else
                {
                    categories[categoryId].AddEmote(emote);
                }

                if (saveToFile)
                {
                    PersistenceManager.SaveCategories(categories.Values.ToList());
                }
            }
            catch (NotFoundException)
            {
                Logger.Warn($"Failed to toggle emote {emote.Id} - Category not found!");
            }
        }

        /// <summary>
        /// Sets <c>category.Emotes</c> based on <c>category.EmoteIds</c> and passed emotes list for all categories.
        /// </summary>
        public void ResolveEmoteIds(List<Emote> emotes)
        {
            foreach (var category in categories.Values)
            {
                category.Emotes = emotes.Where((emote) => category.EmoteIds.Contains(emote.Id)).ToList();
            }
        }

        /// <summary>
        /// Reconciles the user's categories with a (cached) catalog — the one-time V2 migration
        /// (stamp name-matched categories with their catalogId) and, when a fetched update was
        /// applied to the cache since the last launch, the merge (place new emotes into linked
        /// categories, auto-create untombstoned catalog categories the user doesn't have, prune
        /// nothing). The decisions live in the Blish-free, unit-tested <see cref="CatalogMerge"/>;
        /// this applies its result to the live state, persists once, and refreshes the UI.
        /// Must run on the game thread.
        /// </summary>
        /// <param name="fireCategoriesUpdated">When false the event is not fired, allowing the caller
        /// to swap other dependent state (the emote collection) before notifying consumers.</param>
        public CatalogMergeResult ApplyCatalog(CatalogData catalog, PendingCatalogUpdate pendingUpdate, CultureInfo locale, List<Emote> emotes, bool fireCategoriesUpdated = true)
        {
            // Snapshot the live state; the merge works on the persisted shape only.
            var document = new UserCategoriesDocument
            {
                Categories = categories.Values.Select(PersistenceManager.ToPersisted).ToList(),
                DeletedCatalogCategoryIds = new List<string>(PersistenceManager.DeletedCatalogCategoryIds),
                MergedCatalogVersion = PersistenceManager.MergedCatalogVersion,
            };

            var result = CatalogMerge.Apply(document, catalog, pendingUpdate, locale);
            if (result.Outcome == CatalogMergeOutcome.Noop)
            {
                return result;
            }

            // Build a complete new category dictionary from the merged document, then swap it in
            // place. Any failure before this point leaves the previous <c>categories</c> untouched.
            var newCategories = new Dictionary<Guid, Category>();
            foreach (var persisted in document.Categories)
            {
                var category = FromPersisted(persisted);
                newCategories.Add(category.Id, category);
                if (category.IsFavourite)
                {
                    FavouriteCategoryId = category.Id;
                }
            }
            categories = newCategories;

            PersistenceManager.MarkMerged(document.MergedCatalogVersion ?? catalog.Manifest.Version);
            PersistenceManager.SaveCategories(categories.Values.ToList());
            ResolveEmoteIds(emotes);

            Logger.Info($"Catalog merge ({result.Outcome}): linked {result.MigratedCategoryNames.Count} categories by name, "
                + $"created {result.CreatedCategoryNames.Count} ({string.Join(", ", result.CreatedCategoryNames)}), "
                + $"placed {result.PlacedEmoteCount} emotes"
                + (result.SkippedNameConflicts.Count > 0 ? $", skipped name conflicts: {string.Join(", ", result.SkippedNameConflicts)}" : ""));
            if (fireCategoriesUpdated)
            {
                CategoriesUpdated?.Invoke(this, GetAll());
            }
            return result;
        }

        /// <summary>Fires the <see cref="CategoriesUpdated"/> event with the current category list.
        /// Used by live catalog applies after the caller has finished swapping dependent state.</summary>
        public void NotifyCategoriesUpdated()
        {
            CategoriesUpdated?.Invoke(this, GetAll());
        }

        public void Unload()
        {
            // Save using PersistenceManager
            PersistenceManager.SaveCategories(categories.Values.ToList());
        }

        private void CreateFavouriteCategory()
        {
            CreateCategory(Category.FAVOURITES_CATEGORY_NAME, null, null, null, true, false);
        }

        private void AssertUniqueName(string name)
        {
            bool nameInUse = categories.Values.Any((category) => category.Name == name);
            if (nameInUse)
            {
                Logger.Debug($"Name must be unique - {name} already in use.");
                throw new UniqueViolationException($"Name must be unique - {name} already in use.");
            }
        }

        private int GetNextNewCategoryNumber()
        {
            // Get all that start with NEW_CATEGORY_NAME -> remove and try to parse remaining number -> return max
            var newCategoryNumbers = categories.Values.Where((category) => category.Name.StartsWith(NEW_CATEGORY_NAME)).Select(category =>
            {
                var numberStr = category.Name.Replace(NEW_CATEGORY_NAME, "").Trim();
                if (numberStr.Length == 0)
                {
                    // First one did not have a number appended -> treat as 0
                    numberStr = "0";
                }

                int parsed;
                if (!int.TryParse(numberStr, out parsed))
                {
                    // Parsing failed -> ignore/treat as 0
                    parsed = 0;
                }
                return parsed;
            }).ToList();
            return newCategoryNumbers.Count == 0 ? 0 : newCategoryNumbers.Max() + 1;
        }
    }
}
