using Blish_HUD;
using Blish_HUD.Controls;
using Blish_HUD.Graphics.UI;
using felix.BlishEmotes.Strings;
using felix.BlishEmotes.UI.Presenters;
using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.Globalization;
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
        private Helper helper;
        // https://github.com/Tharylia/Blish-HUD-Modules/blob/main/Estreya.BlishHUD.EventTable/UI/Views/AreaSettingsView.cs
        private Panel CategoryListPanel;
        private Menu CategoryListMenu;
        private StandardButton AddCategoryButton;
        private Panel CategoryEditPanel;

        public List<Category> Categories { get; set; }
        public event EventHandler<AddCategoryArgs> AddCategory;
        public event EventHandler<Category> UpdateCategory;
        public event EventHandler<Category> DeleteCategory;

        public List<Emote> Emotes { get; set; }


        private const int _labelWidth = 200;
        private const int _controlWidth = 150;
        private const int _height = 20;

        public CategorySettingsView(CategoriesManager categoriesManager, EmotesManager emotesManager, Helper helper) : base()
        {
            this.WithPresenter(new CategorySettingsPresenter(this, (categoriesManager, emotesManager)));
            this.helper = helper;
        }

        private FlowPanel CreateRowPanel(Container parent)
        {
            return new FlowPanel()
            {
                CanCollapse = false,
                CanScroll = false,
                Parent = parent,
                WidthSizingMode = SizingMode.AutoSize,
                HeightSizingMode = SizingMode.AutoSize,
                FlowDirection = ControlFlowDirection.SingleLeftToRight,
                OuterControlPadding = new Vector2(5, 5),
            };
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
            var addBtnHeight = 30;
            CategoryListPanel = new Panel()
            {
                Parent = buildPanel,
                ShowBorder = true,
                CanScroll = true,
                HeightSizingMode = SizingMode.Standard,
                WidthSizingMode = SizingMode.Standard,
                Location = new Point(0, 0),
                Size = new Point(200, buildPanel.ContentRegion.Height - 10 - addBtnHeight), // - padding - add btn height
            };
            CategoryListMenu = new Menu
            {
                Parent = CategoryListPanel,
                WidthSizingMode = SizingMode.Fill,
            };
            BuildCategoryMenuItems();

            AddCategoryButton = new StandardButton()
            {
                Parent = buildPanel,
                Text = Common.category_add,
                Size = new Point(200, addBtnHeight),
                Location = new Point(0, CategoryListPanel.Size.Y + 10),
            };
            AddCategoryButton.Click += delegate
            {
                // TODO IF A CATEGORY HAS BEEN DELETED PREVIOUSLY THIS INSERTS IT AT THE SAME POS INSTEAD OF AT THE END
                // From StackOverflow: As you can see, the implementation uses a technique that tracks a list of free positions in the entries array, and when a new value is added, the free entries are used first.
                AddCategory?.Invoke(this, new AddCategoryArgs() { Name = CategoriesManager.NEW_CATEGORY_NAME });
            };

            // Init right panel
            CategoryEditPanel = new Panel()
            {
                Parent = buildPanel,
                ShowBorder = true,
                CanScroll = false,
                HeightSizingMode = SizingMode.Fill,
                WidthSizingMode = SizingMode.Fill,
                Location = new Point(CategoryListPanel.Size.X + 20, 0),
            };
        }

        public void Rebuild(Category category = null)
        {
            CategoryEditPanel?.ClearChildren();
            CategoryListMenu?.ClearChildren();

            BuildCategoryMenuItems();
            if (category != null)
            {
                BuildEditPanel(CategoryEditPanel, category);
            }
        }

        private void BuildCategoryMenuItems()
        {
            foreach (var category in Categories)
            {
                MenuItem menuItem = new MenuItem(category.Name)
                {
                    Parent = CategoryListMenu,
                    WidthSizingMode = SizingMode.Fill,
                    HeightSizingMode = SizingMode.AutoSize,
                    Menu = BuildCategoryRightClickMenu(category),
                };
                menuItem.Click += (s, e) => {
                    BuildEditPanel(CategoryEditPanel, category);
                };
            }
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
            Panel settingsPanel = new Panel()
            {
                Parent = parent,
                CanCollapse = false,
                CanScroll = false,
                Size = parent.ContentRegion.Size,
            };

            // Header containing Name of category + save button
            Panel header = new Panel()
            {
                Parent = settingsPanel,
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
                category.Name = categoryName.Text;
                UpdateCategory?.Invoke(this, category);
            };

            // Emotes
            FlowPanel emotesPanel = new FlowPanel()
            {
                Parent = settingsPanel,
                CanCollapse = false,
                CanScroll = false,
                FlowDirection = ControlFlowDirection.TopToBottom,
                HeightSizingMode = SizingMode.Fill,
                WidthSizingMode = SizingMode.Fill,
                Location = new Point(0, 80),
            };
            foreach (var emote in Emotes)
            {
                var emoteInCategoryRow = CreateRowPanel(emotesPanel);
                emoteInCategoryRow.OuterControlPadding = new Vector2(20, 5);
                Label emoteInCategoryLabel = new Label()
                {
                    Parent = emoteInCategoryRow,
                    Text = helper.EmotesResourceManager.GetString(emote.Id),
                    Size = new Point(100, _height),
                    Location = new Point(0, 0),
                };
                Checkbox emoteInCategoryCheckbox = new Checkbox()
                {
                    Parent = emoteInCategoryRow,
                    Checked = (this.Presenter as CategorySettingsPresenter).IsEmoteInCategory(category.Id, emote),
                    Size = new Point(_controlWidth, _height),
                    Location = new Point(_labelWidth + 5, 0),
                };
                emoteInCategoryCheckbox.CheckedChanged += (s, args) => {
                    if (args.Checked)
                    {
                        category.AddEmote(emote);
                    }
                    else
                    {
                        category.RemoveEmote(emote);
                    }
                };
            }
        }

        protected override void Unload()
        {
            this.CategoryListPanel?.Dispose();
            this.AddCategoryButton?.Dispose();
            this.CategoryEditPanel?.Dispose();
            this.CategoryListMenu?.Dispose();
        }
    }
}
