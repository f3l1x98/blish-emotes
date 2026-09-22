using Blish_HUD.Modules.Managers;
using Blish_HUD;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using felix.BlishEmotes.Services.Persistence;

namespace felix.BlishEmotes
{
    /// <summary>
    /// Owns <c>categories.json</c>: loads and saves the user's categories (V2 format per
    /// <see cref="UserCategoriesFile"/> — downgrade-safe against the 1.x loader) and carries the
    /// document-level catalog-link bookkeeping (tombstones, merged catalog version) across saves.
    /// </summary>
    class PersistenceManager
    {
        private static readonly Logger Logger = Logger.GetLogger<PersistenceManager>();

        private string _baseDirectoryPath;
        private string _categoriesFile => Path.Combine(_baseDirectoryPath, "categories.json");

        /// <summary>Catalog categories whose linked user category was deleted by the user — auto-creation must never resurrect them (tombstones).</summary>
        private List<string> _deletedCatalogCategoryIds = new List<string>();

        /// <summary>Catalog version the persisted categories have been reconciled against; null until the one-time V2 migration has run.</summary>
        private long? _mergedCatalogVersion;

        public PersistenceManager(DirectoriesManager directoriesManager)
        {
            IReadOnlyList<string> registeredDirectories = directoriesManager.RegisteredDirectories;
            _baseDirectoryPath = directoriesManager.GetFullDirectoryPath(registeredDirectories[0]);
        }

        /// <summary>Recorded tombstones; updated when the user deletes a linked category.</summary>
        public IReadOnlyList<string> DeletedCatalogCategoryIds => _deletedCatalogCategoryIds;

        /// <summary>The catalog version the loaded categories were last reconciled against (null = not yet migrated).</summary>
        public long? MergedCatalogVersion => _mergedCatalogVersion;

        /// <summary>Records a tombstone so the catalog merge never resurrects the deleted catalog category.</summary>
        public void RecordDeletedCatalogCategory(string catalogId)
        {
            if (string.IsNullOrEmpty(catalogId) || _deletedCatalogCategoryIds.Contains(catalogId))
            {
                return;
            }
            _deletedCatalogCategoryIds.Add(catalogId);
        }

        /// <summary>Advances the reconciled-against catalog version (called after a successful merge).</summary>
        public void MarkMerged(long catalogVersion)
        {
            _mergedCatalogVersion = catalogVersion;
        }

        /// <summary>
        /// Loads the persisted document. Throws <see cref="FileNotFoundException"/> when the file
        /// is absent (the caller then seeds default categories); returns an empty document when it
        /// is unreadable (the caller recovers with an empty state, which a later catalog merge
        /// re-seeds). Doc-level state is kept for subsequent saves.
        /// </summary>
        public UserCategoriesDocument LoadDocument()
        {
            try
            {
                var document = UserCategoriesFile.Read(_categoriesFile);
                _deletedCatalogCategoryIds = document.DeletedCatalogCategoryIds;
                _mergedCatalogVersion = document.MergedCatalogVersion;
                Logger.Debug($"Successfully loaded categories from {_categoriesFile}");
                return document;
            }
            catch (FileNotFoundException e)
            {
                Logger.Debug("Category file not found.");
                // Re-throw because CategoriesManager will now have to setup default categories
                throw e;
            }
            catch (Exception e)
            {
                Logger.Error("Failed to load categories - recovering with an empty state.");
                Logger.Error(e.Message);
                Logger.Debug(e.StackTrace);
                _deletedCatalogCategoryIds = new List<string>();
                _mergedCatalogVersion = null;
                return new UserCategoriesDocument();
            }
        }

        public void SaveCategories(List<Category> categories)
        {
            try
            {
                var document = new UserCategoriesDocument
                {
                    Categories = categories.Select(ToPersisted).ToList(),
                    DeletedCatalogCategoryIds = new List<string>(_deletedCatalogCategoryIds),
                    MergedCatalogVersion = _mergedCatalogVersion,
                };
                UserCategoriesFile.Write(_categoriesFile, document);
                Logger.Debug($"Successfully saved categories to {_categoriesFile}");
            }
            catch (Exception e)
            {
                Logger.Error($"Failed to write {_categoriesFile}");
                Logger.Error(e.Message);
                Logger.Debug(e.StackTrace);
            }
        }

        /// <summary>Snapshot of a live category in the persisted shape (emote ids are copied — the merge mutates documents built from it).</summary>
        internal static PersistedCategory ToPersisted(Category category)
        {
            return new PersistedCategory
            {
                Id = category.Id,
                Name = category.Name,
                EmoteIds = new List<string>(category.EmoteIds),
                IsFavourite = category.IsFavourite,
                TextureFileName = category.TextureFileName,
                CatalogId = category.CatalogId,
            };
        }
    }
}
