using Blish_HUD;
using Blish_HUD.Controls;
using Blish_HUD.Graphics.UI;
using felix.BlishEmotes.Strings;
using felix.BlishEmotes.UI.Presenters;
using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace felix.BlishEmotes.UI.Views
{
    public class AddCategoryArgs
    {
        public string Name {  get; set; }
        public List<Emote> Emotes { get; set; }
    }

    class CategorySettingsView : View
    {
        // https://github.com/Tharylia/Blish-HUD-Modules/blob/main/Estreya.BlishHUD.EventTable/UI/Views/AreaSettingsView.cs
        //private FlowPanel CategoryFlowPanel;
        private Panel CategoryListPanel;
        private StandardButton AddCategoryButton;
        private Panel CategoryEditPanel;

        public List<Category> Categories { get; set; }
        public event EventHandler<AddCategoryArgs> AddCategory;
        public event EventHandler<Category> UpdateCategory;
        public event EventHandler<Category> DeleteCategory;

        public CategorySettingsView(CategoriesManager categoriesManager, EmotesManager emotesManager) : base()
        {
            this.WithPresenter(new CategorySettingsPresenter(this, (categoriesManager, emotesManager)));
        }

        // IDEA:
        // - Left side has column containing cards for each category
        //  - Cards can be reorder (via drag and drop?!?!?) to reorder categories in list display (and radial, but imo not as important)
        //  - + Btn at the bottom to add new category
        //  - Select category to display emote selection in right container
        //  - Scrollable
        // - Right container
        //  - Empty if no category selected
        //  - Contains selection of all emotes in order to toggle whether part of category or not
        // - Favourite category
        //  - Cannot be deleted
        //  - Cannot be reordered?!?!?!?
        protected override void Build(Container buildPanel)
        {
            // Init left panel
            /*CategoryFlowPanel = new FlowPanel()
            {
                Parent = buildPanel,
                FlowDirection = ControlFlowDirection.TopToBottom,
                /*HeightSizingMode = SizingMode.Fill,
                WidthSizingMode = SizingMode.Fill,/
                Size = new Point(200, 400),
                CanScroll = true,
            };*/
            CategoryListPanel = new Panel()
            {
                Parent = buildPanel,
                ShowBorder = true,
                CanScroll = true,
                HeightSizingMode = SizingMode.Standard,
                WidthSizingMode = SizingMode.Standard,
                //Location = new Point(bounds.X, bounds.Y),
                //Size = new Point(Panel.MenuStandard.Size.X - 75, bounds.Height - StandardButton.STANDARD_CONTROL_HEIGHT),
                Location = new Point(0, 0),
                Size = new Point(200, buildPanel.ContentRegion.Height - 10 - 30), // TODO - padding - add btn height
            };
            Menu categoryListMenu = new Menu
            {
                Parent = CategoryListPanel,
                WidthSizingMode = SizingMode.Fill
            };
            foreach (var category in Categories)
            {
                MenuItem menuItem = new MenuItem(category.Name)
                {
                    Parent = categoryListMenu,
                    WidthSizingMode = SizingMode.Fill,
                    HeightSizingMode = SizingMode.AutoSize,
                    Menu = BuildCategoryRightClickMenu(category),
                };
                menuItem.Click += (s, e) => {
                    // TODO build edit panel for this and display it
                    BuildEditPanel(CategoryEditPanel, category);
                };
            }

            AddCategoryButton = new StandardButton()
            {
                Parent = buildPanel,
                Text = Common.category_add,
                Size = new Point(200, 30),
                Location = new Point(0, CategoryListPanel.Size.Y + 10),
            };
            AddCategoryButton.Click += delegate
            {
                // TODO HOW TO ADD NEW -> FIRST CREATE MENU ITEM OR NOT?!?!?
                AddCategory?.Invoke(this, new AddCategoryArgs() { Name = "New Category" });
                // TODO REFETCH CATEGORIES
                //BuildEditPanel(CategoryEditPanel, Categories);
            };

            // Init right panel
            CategoryEditPanel = new Panel()
            {
                Parent = buildPanel,
                ShowBorder = true,
                CanScroll = false,
                HeightSizingMode = SizingMode.Fill,
                WidthSizingMode = SizingMode.Fill,
                //Location = new Point(bounds.X, bounds.Y),
                //Size = new Point(Panel.MenuStandard.Size.X - 75, bounds.Height - StandardButton.STANDARD_CONTROL_HEIGHT),
                Location = new Point(CategoryListPanel.Size.X + 20, 0),
                //Size = new Point(200, 400),
            };
        }

        private ContextMenuStrip BuildCategoryRightClickMenu(Category category)
        {
            var rightClickMenu = new ContextMenuStrip();
            var deleteItem = new ContextMenuStripItem()
            {
                Text = Common.settings_ui_delete,
                Enabled = !category.IsFavourite,
            };
            deleteItem.Click += delegate
            {
                DeleteCategory?.Invoke(this, category);
            };
            rightClickMenu.AddMenuItem(deleteItem);

            return rightClickMenu;
        }

        private void BuildEditPanel(Panel parent, Category category)
        {
            parent.ClearChildren();
            // TODO
            FlowPanel settingsPanel = new FlowPanel()
            {
                Parent = parent,
                CanCollapse = false,
                CanScroll = true,
                FlowDirection = ControlFlowDirection.TopToBottom,
                Size = parent.Size,
                //HeightSizingMode = SizingMode.Fill,
                //WidthSizingMode = SizingMode.Fill,
            };

            // Header containing Name of category + save button
            Panel header = new Panel()
            {
                Parent = settingsPanel,
                //WidthSizingMode = SizingMode.Fill,
                Width = settingsPanel.Width,
                Height = 60,
                CanScroll = false,
                ShowBorder = false,
            };
            TextBox categoryName = new TextBox()
            {
                Parent = header,
                Width = 200,
                Height = 40,
                Text = category.Name,
                Location = new Point(header.Size.X / 2 - 100, 10),
                Font = GameService.Content.DefaultFont18,
            };
            StandardButton saveButton = new StandardButton()
            {
                Parent = header,
                Text = Common.settings_ui_save,
                Size = new Point(80, 40),
                Location = new Point(header.Size.X - 100, 10),
            };
            saveButton.Click += delegate
            {
                // TODO UPDATE
                category.Name = categoryName.Text;
                UpdateCategory?.Invoke(this, category);
            };

            //Label test = new Label() { Parent = settingsPanel, Text = category.Name, };
        }

        protected override void Unload()
        {
            this.CategoryListPanel?.Dispose();
            this.AddCategoryButton?.Dispose();
            this.CategoryEditPanel?.Dispose();
        }
    }
}
