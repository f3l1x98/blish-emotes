﻿using Blish_HUD;
using felix.BlishEmotes.Exceptions;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace felix.BlishEmotes
{
    class CategoriesManager
    {
        private static readonly Logger Logger = Logger.GetLogger<CategoriesManager>();
        private PersistenceManager PersistenceManager;
        // Cache special Favourite category id
        public Guid FavouriteCategoryId { get; private set; }
        // Cache mapping ids to objects
        private Dictionary<Guid, Category> categories;

        public CategoriesManager(PersistenceManager persistenceManager)
        {
            PersistenceManager = persistenceManager;
            categories = new Dictionary<Guid, Category>();

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
        }

        public Category CreateCategory(string name, List<Emote> emotes = null, bool saveToFile = true)
        {
            return CreateCategory(name, emotes.Select((emote) => emote.Id).ToList(), emotes, false, saveToFile);
        }
        private Category CreateCategory(string name, List<string> emoteIds = null, List<Emote> emotes = null, bool isFavourite = false, bool saveToFile = true)
        {
            emoteIds = emoteIds ?? new List<string>();
            emotes = emotes ?? new List<Emote>();
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
            return newCategory.Clone();
        }

        public Category UpdateCategory(Category category, bool saveToFile = true)
        {
            Category current;
            categories.TryGetValue(category.Id, out current);
            if (current == null)
            {
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
            return category.Clone();
        }

        public Category GetById(Guid id)
        {
            Category category;
            categories.TryGetValue(id, out category);
            if (category == null)
            {
                throw new NotFoundException($"No category found for id {id}");
            }
            return category.Clone();
        }

        public List<Category> GetAll()
        {
            return new List<Category>(categories.Values.Select((category) => category.Clone()));
        }

        public void ToggleFavouriteEmote(Emote emote, bool saveToFile = true)
        {
            if (FavouriteCategoryId == null)
            {
                Logger.Error("FavouriteCategoryId is not set!");
                return;
            }

            if (categories[FavouriteCategoryId].EmoteIds.Contains(emote.Id))
            {
                categories[FavouriteCategoryId].RemoveEmote(emote.Id);
            }
            else
            {
                categories[FavouriteCategoryId].AddEmote(emote);
            }

            if (saveToFile)
            {
                PersistenceManager.SaveCategories(categories.Values.ToList());
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
                throw new UniqueViolationException($"Name must be unique - {name} already in use.");
            }
        }
    }
}
