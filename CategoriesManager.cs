using Blish_HUD;
using felix.BlishEmotes.Exceptions;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace felix.BlishEmotes
{
    class CategoriesManager
    {
        public static readonly string NEW_CATEGORY_NAME = "New Category";
        private static readonly Logger Logger = Logger.GetLogger<CategoriesManager>();
        private PersistenceManager PersistenceManager;
        // Cache special Favourite category id
        public Guid FavouriteCategoryId { get; private set; }
        // Cache mapping ids to objects
        private Dictionary<Guid, Category> categories;

        public event EventHandler<List<Category>> CategoriesUpdated;

        public CategoriesManager(PersistenceManager persistenceManager)
        {
            PersistenceManager = persistenceManager;
            categories = new Dictionary<Guid, Category>();
        }

        public void Load()
        {
            try
            {
                var loadedCategories = PersistenceManager.LoadCategories();
                foreach (var category in loadedCategories)
                {
                    if (category.IsFavourite)
                    {
                        FavouriteCategoryId = category.Id;
                    }
                    categories.Add(category.Id, category);
                }
                // Ensure that Favourite category exists (in case user manually deleted it from file)
                if (FavouriteCategoryId == null)
                {
                    CreateCategory(Category.FAVOURITES_CATEGORY_NAME, null, null, true, true);
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

        public Category CreateCategory(string name, List<Emote> emotes = null, bool saveToFile = true)
        {
            return CreateCategory(name, emotes?.Select((emote) => emote.Id).ToList(), emotes, false, saveToFile);
        }
        private Category CreateCategory(string name, List<string> emoteIds = null, List<Emote> emotes = null, bool isFavourite = false, bool saveToFile = true)
        {
            emoteIds = emoteIds ?? new List<string>();
            emotes = emotes ?? new List<Emote>();

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

            // TODO ENSURE Emotes IS SET?!?!?!?
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

            categories.Remove(category.Id);
            // Recreate Dictionary in order to bypass optimization that would insert next item at the new empty space
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
            // Save using PersistenceManager
            PersistenceManager.SaveCategories(categories.Values.ToList());
        }

        private void SetupDefaultCategories()
        {
            Logger.Debug("SetupDefaultCategories");
            // Create Favourite category
            CreateCategory(Category.FAVOURITES_CATEGORY_NAME, null, null, true, false);
            CreateCategory("Greeting", new List<string>() { "beckon", "bow", "salute", "wave" }, null, false, false);
            CreateCategory("Reaction", new List<string>() { "cower", "cry", "facepalm", "hiss", "no", "sad", "shiver", "shiverplus", "shrug", "surprised", "thanks", "yes" }, null, false, false);
            CreateCategory("Fun", new List<string>() { "cheer", "laugh", "paper", "rock", "rockout", "scissors" }, null, false, false);
            CreateCategory("Pose", new List<string>() { "bless", "crossarms", "heroic", "kneel", "magicjuggle", "playdead", "point", "serve", "sit", "sleep", "stretch", "threaten" }, null, false, false);
            CreateCategory("Dance", new List<string>() { "dance", "geargrind", "shuffle", "step" }, null, false, false);
            CreateCategory("Miscellaneous", new List<string>() { "ponder", "possessed", "rank", "sipcoffee", "talk" }, null, false, false);
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
