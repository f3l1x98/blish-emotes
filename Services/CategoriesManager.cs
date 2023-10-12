using Blish_HUD;
using Blish_HUD.Modules.Managers;
using felix.BlishEmotes.Exceptions;
using Gw2Sharp.WebApi.V2.Models;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace felix.BlishEmotes
{
    class CategoriesManager
    {
        public static readonly string NEW_CATEGORY_NAME = "New Category";
        public const string DEFAULT_TEXTURE_FILE_NAME = "156909.png";
        private Texture2D defaultTexture;
        private static readonly Logger Logger = Logger.GetLogger<CategoriesManager>();
        private ContentsManager ContentsManager;
        private PersistenceManager PersistenceManager;
        // Cache special Favourite category id
        public Guid FavouriteCategoryId { get; private set; }
        // Cache mapping ids to objects
        private Dictionary<Guid, Category> categories;

        public event EventHandler<List<Category>> CategoriesUpdated;

        public CategoriesManager(ContentsManager contentsManager, PersistenceManager persistenceManager)
        {
            ContentsManager = contentsManager;
            PersistenceManager = persistenceManager;
            categories = new Dictionary<Guid, Category>();
            defaultTexture = ContentsManager.GetTexture(@"textures/" + DEFAULT_TEXTURE_FILE_NAME, ContentsManager.GetTexture(@"textures/missing-texture.png"));
        }

        public void Load()
        {
            try
            {
                var loadedCategories = PersistenceManager.LoadCategories();
                foreach (var category in loadedCategories)
                {
                    category.Texture = GetTexture(category);
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
                SetupDefaultCategories();
            }
            catch (Exception ex)
            {
                Logger.Error("Failed to load categories.");
                Logger.Error(ex.Message);
                Logger.Debug(ex.StackTrace);
            }
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
        private Category CreateCategory(string name, string textureFileName = DEFAULT_TEXTURE_FILE_NAME, List<string> emoteIds = null, List<Emote> emotes = null, bool isFavourite = false, bool saveToFile = true)
        {
            emoteIds = emoteIds ?? new List<string>();
            emotes = emotes ?? new List<Emote>();
            textureFileName = textureFileName ?? DEFAULT_TEXTURE_FILE_NAME;

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

            if (current.TextureFileName != category.TextureFileName)
            {
                if (current.Texture != defaultTexture)
                {
                    current.Texture?.Dispose();
                }
                // TODO set category.Texture here?!?!?
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

            if (category.Texture != defaultTexture)
            {
                category.Texture?.Dispose();
            }
            categories.Remove(category.Id);
            // Recreate Dictionary in order to bypass optimization that would insert next item at the now empty space
            categories = new Dictionary<Guid, Category>(categories);
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

        public void ResolveEmoteIds(List<Emote> emotes)
        {
            foreach (var category in categories.Values)
            {
                category.Emotes = emotes.Where((emote) => category.EmoteIds.Contains(emote.Id)).ToList();
            }
        }

        public void Unload()
        {
            // Dispose all Textures
            foreach (var category in categories.Values)
            {
                if (category.Texture != defaultTexture)
                {
                    category.Texture?.Dispose();
                }
            }
            defaultTexture?.Dispose();

            // Save using PersistenceManager
            PersistenceManager.SaveCategories(categories.Values.ToList());
        }

        private Texture2D GetTexture(Category category)
        {
            return GetTexture(category.TextureFileName);
        }
        private Texture2D GetTexture(string textureFileName)
        {
            if (textureFileName == DEFAULT_TEXTURE_FILE_NAME)
            {
                return defaultTexture;
            }
            return ContentsManager.GetTexture(@"textures/" + textureFileName, ContentsManager.GetTexture(@"textures/missing-texture.png"));
        }

        private void CreateFavouriteCategory()
        {
            CreateCategory(Category.FAVOURITES_CATEGORY_NAME, null, null, null, true, false);
        }

        private void SetupDefaultCategories()
        {
            Logger.Debug("SetupDefaultCategories");
            // Create default categories
            CreateFavouriteCategory();
            CreateCategory("Greeting", null, new List<string>() { "beckon", "bow", "salute", "wave" }, null, false, false);
            CreateCategory("Reaction", null, new List<string>() { "cower", "cry", "facepalm", "hiss", "no", "sad", "shiver", "shiverplus", "shrug", "surprised", "thanks", "yes" }, null, false, false);
            CreateCategory("Fun", null, new List<string>() { "cheer", "laugh", "paper", "rock", "rockout", "scissors" }, null, false, false);
            CreateCategory("Pose", null, new List<string>() { "bless", "crossarms", "heroic", "kneel", "magicjuggle", "playdead", "point", "serve", "sit", "sleep", "stretch", "threaten" }, null, false, false);
            CreateCategory("Dance", null, new List<string>() { "dance", "geargrind", "shuffle", "step" }, null, false, false);
            CreateCategory("Miscellaneous", null, new List<string>() { "ponder", "possessed", "rank", "readbook", "sipcoffee", "talk" }, null, false, false);
            PersistenceManager.SaveCategories(categories.Values.ToList());
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
