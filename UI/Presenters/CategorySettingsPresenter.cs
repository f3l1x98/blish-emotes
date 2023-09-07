using Blish_HUD.Graphics.UI;
using felix.BlishEmotes.UI.Views;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace felix.BlishEmotes.UI.Presenters
{
    class CategorySettingsPresenter : Presenter<CategorySettingsView, (CategoriesManager, EmotesManager)>
    {

        public CategorySettingsPresenter(CategorySettingsView view, (CategoriesManager, EmotesManager) model) : base(view, model)
        {
        }
        protected override Task<bool> Load(IProgress<string> progress)
        {
            this.View.AddCategory += View_AddCategoryClicked;
            this.View.UpdateCategory += View_UpdateCategoryClicked;
            this.View.DeleteCategory += View_DeleteCategoryClicked;
            this.View.Categories = this.Model.Item1.GetAll();

            return base.Load(progress);
        }

        private void View_DeleteCategoryClicked(object sender, Category e)
        {
            this.Model.Item1.DeleteCategory(e);
            this.View.Categories = this.Model.Item1.GetAll();
            // TODO TRIGGER VIEW REBUILD?!?!?
            this.UpdateView();
        }

        private void View_UpdateCategoryClicked(object sender, Category e)
        {
            this.Model.Item1.UpdateCategory(e);
            this.View.Categories = this.Model.Item1.GetAll();
            // TODO TRIGGER VIEW REBUILD?!?!?
            this.UpdateView();
        }

        private void View_AddCategoryClicked(object sender, AddCategoryArgs e)
        {
            this.Model.Item1.CreateCategory(e.Name, e.Emotes);
            this.View.Categories = this.Model.Item1.GetAll();
            // TODO TRIGGER VIEW REBUILD?!?!?
            // THIS MIGHT BREAK AUTOMATICALLY DISPLAYING EDIT
            this.UpdateView();
        }

        protected override void Unload()
        {
            this.View.AddCategory -= View_AddCategoryClicked;
            this.View.UpdateCategory -= View_UpdateCategoryClicked;
            this.View.DeleteCategory -= View_DeleteCategoryClicked;
        }
    }
}
