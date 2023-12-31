﻿using Blish_HUD;
using Blish_HUD.Graphics.UI;
using felix.BlishEmotes.Exceptions;
using felix.BlishEmotes.UI.Views;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace felix.BlishEmotes.UI.Presenters
{
    class CategorySettingsPresenter : Presenter<CategorySettingsView, (CategoriesManager, EmotesManager)>
    {
        private static readonly Logger Logger = Logger.GetLogger<CategorySettingsPresenter>();

        public CategorySettingsPresenter(CategorySettingsView view, (CategoriesManager, EmotesManager) model) : base(view, model)
        {
        }

        protected override Task<bool> Load(IProgress<string> progress)
        {
            this.View.AddCategory += View_AddCategoryClicked;
            this.View.UpdateCategory += View_UpdateCategoryClicked;
            this.View.DeleteCategory += View_DeleteCategoryClicked;
            this.View.ReorderCategories += View_ReorderCategories;
            this.View.Categories = this.Model.Item1.GetAll();
            this.View.Emotes = this.Model.Item2.GetAll();

            return base.Load(progress);
        }

        public bool IsEmoteInCategory(Guid categoryId, Emote emote)
        {
            try
            {
                return this.Model.Item1.IsEmoteInCategory(categoryId, emote);
            }
            catch (NotFoundException)
            {
                return false;
            }
        }

        private void View_ReorderCategories(object sender, List<Category> e)
        {
            this.Model.Item1.ReorderCategories(e);
        }

        private void View_DeleteCategoryClicked(object sender, Category e)
        {
            this.Model.Item1.DeleteCategory(e);
            this.View.Categories = this.Model.Item1.GetAll();
            this.View.Rebuild();
        }

        private void View_UpdateCategoryClicked(object sender, Category e)
        {
            try
            {
                var updatedCategory = this.Model.Item1.UpdateCategory(e);
                this.View.Categories = this.Model.Item1.GetAll();
                this.View.Rebuild(updatedCategory);
            }
            catch (UniqueViolationException)
            {
                Logger.Error($"Failed to update category {e.Name} - Name already in use.");
            }
            catch (NotFoundException)
            {
                Logger.Error($"Failed to update category {e.Name} - Not found.");
            }
        }

        private void View_AddCategoryClicked(object sender, AddCategoryArgs e)
        {
            try
            {
                var newCategory = this.Model.Item1.CreateCategory(e.Name, e.TextureFileName, e.Emotes);
                this.View.Categories = this.Model.Item1.GetAll();
                this.View.Rebuild(newCategory);
            }
            catch (UniqueViolationException)
            {
                Logger.Error($"Failed to update category {e.Name} - Name already in use.");
            }
        }

        protected override void Unload()
        {
            this.View.AddCategory -= View_AddCategoryClicked;
            this.View.UpdateCategory -= View_UpdateCategoryClicked;
            this.View.DeleteCategory -= View_DeleteCategoryClicked;
            this.View.ReorderCategories -= View_ReorderCategories;
        }
    }
}
