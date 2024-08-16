using Blish_HUD;
using Blish_HUD.Controls;
using Blish_HUD.GameIntegration;
using Blish_HUD.Graphics.UI;
using Blish_HUD.Modules;
using Blish_HUD.Modules.Managers;
using Blish_HUD.Settings;
using felix.BlishEmotes;
using felix.BlishEmotes.Exceptions;
using felix.BlishEmotes.Services.TexturesManagers;
using felix.BlishEmotes.Strings;
using felix.BlishEmotes.UI.Controls;
using felix.BlishEmotes.UI.Views;
using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Threading.Tasks;

namespace BlishEmotesList
{
    [Export(typeof(Blish_HUD.Modules.Module))]
    public class EmotesModule : Blish_HUD.Modules.Module
    {
        internal static EmotesModule ModuleInstance;

        private static readonly Logger Logger = Logger.GetLogger<EmotesModule>();

        #region Service Managers
        internal SettingsManager SettingsManager => this.ModuleParameters.SettingsManager;
        internal ContentsManager ContentsManager => this.ModuleParameters.ContentsManager;
        internal DirectoriesManager DirectoriesManager => this.ModuleParameters.DirectoriesManager;
        internal Gw2ApiManager Gw2ApiManager => this.ModuleParameters.Gw2ApiManager;

        internal PersistenceManager PersistenceManager;
        internal TexturesManager GeneralTexturesManager;
        internal TexturesManager CategoryTexturesManager;
        internal TexturesManager EmoteTexturesManager;
        internal CategoriesManager CategoriesManager;
        internal EmotesManager EmotesManager;
        #endregion

        #region Controls
        private CornerIcon _cornerIcon;
        private ContextMenuStrip _emoteListMenuStrip;
        private TabbedWindow2 _settingsWindow;
        private RadialMenu _radialMenu;
        #endregion

        #region Settings
        public ModuleSettings Settings;
        #endregion


        [ImportingConstructor]
        public EmotesModule([Import("ModuleParameters")] ModuleParameters moduleParameters) : base(moduleParameters)
        {
            ModuleInstance = this;
        }

        protected override void DefineSettings(SettingCollection settings)
        {
            this.Settings = new ModuleSettings(settings);

            // Handlers
            this.Settings.GlobalHideCornerIcon.SettingChanged += (sender, args) =>
            {
                if (args.NewValue)
                {
                    _cornerIcon?.Dispose();
                }
                else
                {
                    InitCornerIcon();
                }
            };
            this.Settings.GlobalUseCategories.SettingChanged += delegate
            {
                // Redraw ContextMenu due to switch between using categories and full list
                DrawEmoteListContextMenu();
            };
            this.Settings.GlobalKeyBindToggleEmoteList.Value.Enabled = true;
            this.Settings.GlobalKeyBindToggleEmoteList.Value.Activated += delegate
            {
                if (!GameService.GameIntegration.Gw2Instance.IsInGame)
                {
                    Logger.Debug("Disabled outside game.");
                    return;
                }
                if (this.Settings.GlobalUseRadialMenu.Value)
                {
                    _radialMenu?.Show();
                }
                else
                {
                    ShowEmoteList(false);
                }
            };
            this.Settings.GlobalKeyBindToggleSynchronize.Value.Enabled = true;
            this.Settings.GlobalKeyBindToggleSynchronize.Value.Activated += delegate
            {
                EmotesManager.IsEmoteSynchronized = !EmotesManager.IsEmoteSynchronized;
                UpdateUI();
                Logger.Debug("Toggled IsEmoteSynchronized");
            };
            this.Settings.GlobalKeyBindToggleTargeting.Value.Enabled = true;
            this.Settings.GlobalKeyBindToggleTargeting.Value.Activated += delegate
            {
                EmotesManager.IsEmoteTargeted = !EmotesManager.IsEmoteTargeted;
                UpdateUI();
                Logger.Debug("Toggled IsEmoteTargeted");
            };
            // Update radial menu emotes
            this.Settings.OnAnyEmotesRadialSettingsChanged += delegate
            {
                UpdateRadialMenu();
            };
            this.Settings.EmoteShortcutActivated += OnEmoteSelected;
        }

        protected override void Initialize()
        {
            // SOTO Fix
            if (Program.OverlayVersion < new SemVer.Version(1, 1, 0))
            {
                try
                {
                    var tacoActive = typeof(TacOIntegration).GetProperty(nameof(TacOIntegration.TacOIsRunning)).GetSetMethod(true);
                    tacoActive?.Invoke(GameService.GameIntegration.TacO, new object[] { true });
                }
                catch { /* NOOP */ }
            }

            Gw2ApiManager.SubtokenUpdated += OnApiSubTokenUpdated;

            // Init custom Manager
            PersistenceManager = new PersistenceManager(DirectoriesManager);
            GeneralTexturesManager = new GeneralTexturesManager(ContentsManager, DirectoriesManager);
            GeneralTexturesManager.LoadTextures();
            CategoryTexturesManager = new CategoryTexturesManager(ContentsManager, DirectoriesManager);
            CategoryTexturesManager.LoadTextures();
            EmoteTexturesManager = new EmoteTexturesManager(DirectoriesManager);
            EmoteTexturesManager.LoadTextures();
            EmotesManager = new EmotesManager(ContentsManager, EmoteTexturesManager, Settings);
            CategoriesManager = new CategoriesManager(CategoryTexturesManager, PersistenceManager);

            CategoriesManager.CategoriesUpdated += delegate
            {
                UpdateUI();
            };

            // Init UI
            if (!this.Settings.GlobalHideCornerIcon.Value)
            {
                InitCornerIcon();
            }

            _settingsWindow = new TabbedWindow2((GeneralTexturesManager as GeneralTexturesManager).GetTexture(Textures.Background), new Rectangle(35, 36, 900, 640), new Rectangle(95, 42, 783 + 38, 592))
            {
                Title = Common.settings_ui_title,
                Parent = GameService.Graphics.SpriteScreen,
                Location = new Point(100, 100),
                Emblem = (GeneralTexturesManager as GeneralTexturesManager).GetTexture(Textures.SettingsIcon),
                Id = $"{this.Namespace}_SettingsWindow",
                SavesPosition = true,
            };

            // Settings
            _settingsWindow.Tabs.Add(new Tab((GeneralTexturesManager as GeneralTexturesManager).GetTexture(Textures.GlobalSettingsIcon), () => new GlobalSettingsView(this.Settings), Common.settings_ui_global_tab));
            // Category setting
            _settingsWindow.Tabs.Add(new Tab((GeneralTexturesManager as GeneralTexturesManager).GetTexture(Textures.CategorySettingsIcon), () => new CategorySettingsView(CategoriesManager, EmotesManager), Common.settings_ui_categories_tab));
            // Emote Hotkey settings
            _settingsWindow.Tabs.Add(new Tab((GeneralTexturesManager as GeneralTexturesManager).GetTexture(Textures.HotkeySettingsIcon), () => new EmoteHotkeySettingsView(this.Settings), Common.settings_ui_emoteHotkeys_tab));
        }

        protected override async Task LoadAsync()
        {
            try
            {
                // Load categories
                CategoriesManager.Load();
                // Load emotes
                EmotesManager.Load();
                // Update emotes with data from api
                await UpdateEmotesFromApi();
                // Update category Emotes
                CategoriesManager.ResolveEmoteIds(EmotesManager.GetAll());

                this.Settings.InitEmotesSettings(EmotesManager.GetAll());
                InitUI();
            }
            catch (Exception e)
            {
                Logger.Fatal("LoadAsync failed!");
                Logger.Error(e.ToString());
            }
        }

        public override IView GetSettingsView()
        {
            return new DummySettingsView(_settingsWindow.Show);
        }

        protected override void Update(GameTime gameTime)
        {
            // Hide radial menu
            if (_radialMenu.Visible && !this.Settings.GlobalKeyBindToggleEmoteList.Value.IsTriggering)
            {
                _radialMenu.Hide();
            }
        }

        private void InitCornerIcon()
        {
            _cornerIcon?.Dispose();
            _cornerIcon = new CornerIcon()
            {
                Icon = (GeneralTexturesManager as GeneralTexturesManager).GetTexture(Textures.ModuleIcon),
                BasicTooltipText = Common.cornerIcon_tooltip,
                Priority = -620003847,
            };

            _cornerIcon.Click += delegate
            {
                ShowEmoteList();
            };
            _cornerIcon.RightMouseButtonReleased += delegate
            {
                _settingsWindow.Show();
            };
        }

        private void InitUI()
        {
            DrawEmoteListContextMenu();

            // Init radial menu
            _radialMenu = new RadialMenu(this.Settings, (GeneralTexturesManager as GeneralTexturesManager).GetTexture(Textures.LockedTexture))
            {
                Parent = GameService.Graphics.SpriteScreen,
                Emotes = EmotesManager.GetRadial(),
                Categories = CategoriesManager.GetAll(),
            };
            _radialMenu.EmoteSelected += OnEmoteSelected;
        }

        private void OnEmoteSelected(object sender, Emote emote)
        {
            EmotesManager.SendEmoteCommand(emote);
        }

        private void UpdateUI()
        {
            DrawEmoteListContextMenu();
            UpdateRadialMenu();
        }

        private void UpdateRadialMenu()
        {
            if (_radialMenu != null)
            {
                _radialMenu.Categories = CategoriesManager?.GetAll() ?? new List<Category>();
                _radialMenu.Emotes = EmotesManager?.GetRadial() ?? new List<Emote>();
                _radialMenu.IsEmoteSynchronized = EmotesManager.IsEmoteSynchronized;
                _radialMenu.IsEmoteTargeted = EmotesManager.IsEmoteTargeted;
            }
        }

        private void DrawEmoteListContextMenu()
        {
            _emoteListMenuStrip?.Dispose();

            _emoteListMenuStrip = new ContextMenuStrip();
            var menuItems = this.Settings.GlobalUseCategories.Value ? GetCategoryMenuItems() : GetEmotesMenuItems(EmotesManager.GetAll());
            _emoteListMenuStrip.AddMenuItems(menuItems);
        }

        private async void OnApiSubTokenUpdated(object sender, ValueEventArgs<IEnumerable<Gw2Sharp.WebApi.V2.Models.TokenPermission>> e)
        {
            // Update emotes with data from api
            await UpdateEmotesFromApi();
        }

        private void ShowEmoteList(bool atCornerIcon = true)
        {
            if (atCornerIcon)
            {
                _emoteListMenuStrip?.Show(_cornerIcon);
            }
            else if (GameService.Input.Mouse.CursorIsVisible)
            {
                _emoteListMenuStrip?.Show(GameService.Input.Mouse.Position);
            }
            else
            {
                Logger.Debug("Emote list display conditions failed");
            }
        }

        private void AddEmoteModifierStatus(ref List<ContextMenuStripItem> items)
        {
            // If IsEmoteSynchronized insert at top
            if (EmotesManager.IsEmoteSynchronized)
            {
                items.Insert(0, new ContextMenuStripItem($"[ {Common.emote_synchronizeActive.ToUpper()} ]"));
            }
            // If IsEmoteTargeted insert at top
            if (EmotesManager.IsEmoteTargeted)
            {
                items.Insert(0, new ContextMenuStripItem($"[ {Common.emote_targetingActive.ToUpper()} ]"));
            }
        }

        private List<ContextMenuStripItem> GetCategoryMenuItems()
        {
            var items = new List<ContextMenuStripItem>();
            foreach (Category category in CategoriesManager.GetAll())
            {
                var categorySubMenu = new ContextMenuStrip();
                categorySubMenu.AddMenuItems(GetEmotesMenuItems(category.Emotes));
                var menuItem = new ContextMenuStripItem()
                {
                    Text = category.Name,
                    Submenu = categorySubMenu,
                };
                items.Add(menuItem);
            }
            AddEmoteModifierStatus(ref items);
            return items;
        }

        private ContextMenuStrip GetToggleFavContextMenu(Emote emote)
        {
            bool isFav = false;
            try
            {
                isFav = CategoriesManager.IsEmoteInCategory(CategoriesManager.FavouriteCategoryId, emote);
            }
            catch (NotFoundException) { }
            var toggleFavMenuItem = new ContextMenuStripItem()
            {
                Text = Common.emote_categoryFavourite,
                CanCheck = true,
                Checked = isFav,
            };
            toggleFavMenuItem.CheckedChanged += (sender, args) =>
            {
                CategoriesManager.ToggleEmoteFromCategory(CategoriesManager.FavouriteCategoryId, emote);
                DrawEmoteListContextMenu();
                Logger.Debug($"Toggled favourite for {emote.Id} to ${args.Checked}");
            };

            var toggleFavSubMenu = new ContextMenuStrip();
            toggleFavSubMenu.AddMenuItem(toggleFavMenuItem);
            return toggleFavSubMenu;
        }

        private List<ContextMenuStripItem> GetEmotesMenuItems(List<Emote> emotes)
        {
            var items = new List<ContextMenuStripItem>();
            foreach (var emote in emotes)
            {
                var menuItem = new ContextMenuStripItem()
                {
                    Text = emote.Label,
                    Enabled = !emote.Locked,
                    Submenu = GetToggleFavContextMenu(emote),
                };
                menuItem.Click += delegate
                {
                    EmotesManager.SendEmoteCommand(emote);
                };
                items.Add(menuItem);
            }
            // Sort by text such that list is sorted no matter what locale
            items.Sort((x, y) => x.Text.CompareTo(y.Text));
            AddEmoteModifierStatus(ref items);
            return items;
        }

        private async Task UpdateEmotesFromApi()
        {
            Logger.Debug("Update emotes from api");
            // load emote information from api
            var apiEmotes = await LoadEmotesFromApi();
            var emotes = EmotesManager.GetAll();
            // Set locks
            foreach (var emote in emotes)
            {
                // Mark emotes as unlocked
                emote.Locked = false;
                if (apiEmotes.UnlockableEmotesIds.Contains(emote.Id) && !apiEmotes.UnlockedEmotesIds.Contains(emote.Id))
                {
                    // Mark emotes as locked
                    emote.Locked = true;
                }
            }
            EmotesManager.UpdateAll(emotes);
        }

        struct ApiEmotesReturn
        {
            public List<string> UnlockableEmotesIds;
            public List<string> UnlockedEmotesIds;
        }

        private async Task<ApiEmotesReturn> LoadEmotesFromApi()
        {
            ApiEmotesReturn returnVal = new ApiEmotesReturn();
            try
            {
                if (Gw2ApiManager.HasPermissions(new[] { Gw2Sharp.WebApi.V2.Models.TokenPermission.Account, Gw2Sharp.WebApi.V2.Models.TokenPermission.Progression, Gw2Sharp.WebApi.V2.Models.TokenPermission.Unlocks }))
                {
                    Logger.Debug("Load emotes from API");
                    // load locked emotes
                    returnVal.UnlockableEmotesIds = new List<string>(await Gw2ApiManager.Gw2ApiClient.V2.Emotes.IdsAsync());
                    // load unlocked emotes
                    returnVal.UnlockedEmotesIds = new List<string>(await Gw2ApiManager.Gw2ApiClient.V2.Account.Emotes.GetAsync());
                }
                else
                {
                    returnVal.UnlockableEmotesIds = new List<string>();
                    returnVal.UnlockedEmotesIds = new List<string>();
                }
            }
            catch (Exception e)
            {
                Logger.Warn("Failed to fetch emotes from API");
                Logger.Debug(e.Message);
                returnVal.UnlockableEmotesIds = new List<string>();
                returnVal.UnlockedEmotesIds = new List<string>();
            }
            return returnVal;
        }

        /// <inheritdoc />
        protected override void Unload()
        {
            Gw2ApiManager.SubtokenUpdated -= OnApiSubTokenUpdated;
            this.Settings.EmoteShortcutActivated -= OnEmoteSelected;
            this.Settings.Unload();
            // Unload here
            _cornerIcon?.Dispose();
            _settingsWindow?.Dispose();
            _radialMenu.EmoteSelected -= OnEmoteSelected;
            _radialMenu?.Dispose();
            CategoriesManager.Unload();
            EmotesManager.Unload();
            GeneralTexturesManager.Dispose();
            CategoryTexturesManager.Dispose();
            EmoteTexturesManager.Dispose();

            // All static members must be manually unset
            ModuleInstance = null;
        }

    }

}
