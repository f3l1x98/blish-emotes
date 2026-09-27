using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Threading.Tasks;
using Blish_HUD;
using Blish_HUD.Controls;
using Blish_HUD.GameIntegration;
using Blish_HUD.Graphics.UI;
using Blish_HUD.Modules;
using Blish_HUD.Modules.Managers;
using Blish_HUD.Settings;
using felix.BlishEmotes;
using felix.BlishEmotes.Exceptions;
using felix.BlishEmotes.Services;
using felix.BlishEmotes.Services.Catalog;
using felix.BlishEmotes.Strings;
using felix.BlishEmotes.UI.Controls;
using felix.BlishEmotes.UI.Views;
using Gw2Sharp.WebApi.V2.Models;
using Microsoft.Xna.Framework;
using Color = Microsoft.Xna.Framework.Color;
using Emote = felix.BlishEmotes.Emote;
using Rectangle = Microsoft.Xna.Framework.Rectangle;
using Version = SemVer.Version;

namespace BlishEmotesList
{
    [Export(typeof(Module))]
    public class EmotesModule : Module
    {
        internal static EmotesModule ModuleInstance;

        private static readonly Logger Logger = Logger.GetLogger<EmotesModule>();

        #region Service Managers
        internal SettingsManager SettingsManager => this.ModuleParameters.SettingsManager;
        internal ContentsManager ContentsManager => this.ModuleParameters.ContentsManager;
        internal DirectoriesManager DirectoriesManager => this.ModuleParameters.DirectoriesManager;
        internal Gw2ApiManager Gw2ApiManager => this.ModuleParameters.Gw2ApiManager;

        internal PersistenceManager PersistenceManager;
        internal TexturesManager TexturesManager;
        internal CategoriesManager CategoriesManager;
        internal EmotesManager EmotesManager;
        internal CatalogManager CatalogManager;
        #endregion

        #region Controls
        private CornerIcon _cornerIcon;
        private ContextMenuStrip _emoteListMenuStrip;
        private TabbedWindow2 _settingsWindow;
        private RadialMenu _radialMenu;
        private GlobalSettingsView _globalSettingsView;
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
            if (Program.OverlayVersion < new Version(1, 1, 0))
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
            TexturesManager = new TexturesManager(ContentsManager, DirectoriesManager);
            CatalogManager = new CatalogManager(DirectoriesManager);
            EmotesManager = new EmotesManager(ContentsManager, Settings, CatalogManager);
            CategoriesManager = new CategoriesManager(TexturesManager, PersistenceManager);

            CategoriesManager.CategoriesUpdated += delegate
            {
                UpdateUI();
            };

            // Init UI
            if (!this.Settings.GlobalHideCornerIcon.Value)
            {
                InitCornerIcon();
            }

            _settingsWindow = new TabbedWindow2(TexturesManager.GetTexture(Textures.Background), new Rectangle(35, 36, 900, 640), new Rectangle(95, 42, 783 + 38, 592))
            {
                Title = Common.settings_ui_title,
                Parent = GameService.Graphics.SpriteScreen,
                Location = new Point(100, 100),
                Emblem = TexturesManager.GetTexture(Textures.SettingsIcon),
                Id = $"{this.Namespace}_SettingsWindow",
                SavesPosition = true,
            };

            // Settings
            _globalSettingsView = new GlobalSettingsView(this.Settings, CatalogManager, EmotesManager);
            _settingsWindow.Tabs.Add(new Tab(TexturesManager.GetTexture(Textures.GlobalSettingsIcon), () => _globalSettingsView, Common.settings_ui_global_tab));
            // Category setting
            _settingsWindow.Tabs.Add(new Tab(TexturesManager.GetTexture(Textures.CategorySettingsIcon), () => new CategorySettingsView(CategoriesManager, EmotesManager), Common.settings_ui_categories_tab));
            // Emote Hotkey settings
            _settingsWindow.Tabs.Add(new Tab(TexturesManager.GetTexture(Textures.HotkeySettingsIcon), () => new EmoteHotkeySettingsView(this.Settings), Common.settings_ui_emoteHotkeys_tab));

            CatalogManager.ProgressChanged += OnCatalogProgressChanged;
            CatalogManager.LastSyncResultUpdated += OnSyncResultUpdated;
        }

        protected override async Task LoadAsync()
        {
            try
            {
                // Load categories (these are user-owned, so they load even before a catalog exists).
                CategoriesManager.Load();

                // Load emotes from the cache if we have one. A fresh install has no cache and no
                // embedded fallback — the module stays visible-but-empty until the first fetch.
                var cachedCatalog = CatalogManager.LoadCachedCatalogOrNull();
                if (cachedCatalog != null)
                {
                    EmotesManager.LoadFromCatalog(cachedCatalog);
                    var pendingUpdate = CatalogManager.ReadPendingUpdateOrNull();
                    var mergeResult = CategoriesManager.ApplyCatalog(cachedCatalog, pendingUpdate, EmotesManager.CurrentLocale, EmotesManager.GetAll());
                    if (mergeResult.Outcome != CatalogMergeOutcome.Noop && pendingUpdate != null)
                    {
                        CatalogManager.ClearPendingUpdate();
                    }

                    CatalogManager.InitLastSyncedVersion(cachedCatalog);
                }

                // Update emotes with data from api
                await UpdateEmotesFromApi();
                // Update category Emotes
                CategoriesManager.ResolveEmoteIds(EmotesManager.GetAll());

                this.Settings.InitEmotesSettings(EmotesManager.GetAll());
                InitUI();

                // Initialise the settings UI to the current (empty/offline) state.
                UpdateCatalogUx();

                if (this.Settings.GlobalCheckCatalogOnStartup.Value)
                {
                    _ = CatalogManager.SyncAsync();
                }
            }
            catch (Exception e)
            {
                Logger.Fatal("LoadAsync failed!");
                Logger.Error(e.ToString());
            }
        }

        // Both handlers run on the game thread: CatalogManager raises its events through the
        // overlay's synchronization context, so UI state may be touched directly.
        private void OnCatalogProgressChanged(object sender, CatalogSyncProgress progress)
        {
            SetCornerIconLoading(Common.settings_catalog_sync_downloading);
            // TODO this gets called every time (every time a file has been downloaded) even though it only checks if the state has changed
            //  -> Only call if status has changed
            UpdateRadialEmptyState();
        }

        /// <summary>
        /// Applies a finished sync: an updated catalog is applied live (no restart needed), the
        /// corner icon loading state is cleared, and the empty-state/context-menu UX is refreshed.
        /// </summary>
        private void OnSyncResultUpdated(object sender, CatalogSyncResult result)
        {
            try
            {
                SetCornerIconLoading(null);
                if (result.State == CatalogSyncState.Updated || result.State == CatalogSyncState.FirstFetch)
                {
                    ApplyCatalogLive();
                    ShowCatalogToast(result);
                }
                UpdateCatalogUx();
            }
            catch (Exception e)
            {
                Logger.Error("Live catalog apply failed unexpectedly!");
                Logger.Error(e.ToString());
            }
        }

        private void ApplyCatalogLive()
        {
            var cachedCatalog = CatalogManager.LoadCachedCatalogOrNull();
            if (cachedCatalog == null)
            {
                return;
            }

            var pendingUpdate = CatalogManager.ReadPendingUpdateOrNull();
            var catalogEmotes = EmotesManager.BuildCatalogEmotes(cachedCatalog);
            try
            {
                var result = CategoriesManager.ApplyCatalog(cachedCatalog, pendingUpdate, EmotesManager.CurrentLocale, catalogEmotes, fireCategoriesUpdated: false);

                if (result.Outcome != CatalogMergeOutcome.Noop && pendingUpdate != null)
                {
                    CatalogManager.ClearPendingUpdate();
                }

                // Swap the emote collection only after the category merge succeeded.
                EmotesManager.GraveyardAndUpdateAll(catalogEmotes);
                CategoriesManager.NotifyCategoriesUpdated();
                this.Settings.InitEmotesSettings(EmotesManager.GetAll());

                // Refresh lock states for newly arrived emotes within seconds.
                _ = UpdateEmotesFromApi();

                Logger.Info("Live catalog apply completed.");
            }
            catch (Exception)
            {
                // The category merge failed before swapping anything live, or the emote swap
                // failed. Dispose only textures that were never exposed to the UI: unchanged
                // entries are reused live instances whose textures are still rendered.
                foreach (var emote in catalogEmotes)
                {
                    if (!EmotesManager.IsLiveEmote(emote))
                    {
                        emote.Texture?.Dispose();
                    }
                }
                throw;
            }
        }

        private void SetCornerIconLoading(string message)
        {
            // The corner icon is disposed while GlobalHideCornerIcon is set.
            if (_cornerIcon != null)
            {
                _cornerIcon.LoadingMessage = message;
            }
        }

        private void UpdateCatalogUx()
        {
            UpdateRadialEmptyState();
            // TODO: This is unnecessary during the first call inside LoadAsync
            //  (already called inside InitUI shortly before and there is most certainly nothing new yet)
            //  -> HOWEVER prob required during other calls to this method
            DrawEmoteListContextMenu();
        }

        private void UpdateRadialEmptyState()
        {
            if (_radialMenu == null)
            {
                return;
            }

            if (EmotesManager.GetAll().Count == 0)
            {
                var state = CatalogManager.LastSyncResult.State;
                bool hasCache = CatalogManager.HasCache();
                if (state == CatalogSyncState.Downloading && !hasCache)
                {
                    _radialMenu.EmptyStateText = Common.radial_gettingEmotes;
                    _radialMenu.EmptyStateColor = Color.Gray;
                }
                else if (!hasCache && state == CatalogSyncState.Failed)
                {
                    _radialMenu.EmptyStateText = Common.radial_noEmotesYet;
                    _radialMenu.EmptyStateColor = Color.Orange;
                }
                else
                {
                    _radialMenu.EmptyStateText = "No Emotes found!";
                    _radialMenu.EmptyStateColor = Color.Red;
                }
            }
        }

        private void ShowCatalogToast(CatalogSyncResult result)
        {
            if (result.NewEmoteIds == null || result.NewEmoteIds.Count == 0)
            {
                return;
            }

            string message = result.State == CatalogSyncState.FirstFetch 
                ? Common.settings_catalog_sync_toast_firstFetch 
                : string.Format(Common.settings_catalog_sync_toast, result.NewEmoteIds.Count, result.NewCategoryIds.Count);
            ScreenNotification.ShowNotification(message);
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
                Icon = TexturesManager.GetTexture(Textures.ModuleIcon),
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
            _radialMenu = new RadialMenu(this.Settings, TexturesManager.GetTexture(Textures.LockedTexture))
            {
                Parent = GameService.Graphics.SpriteScreen,
                Emotes = EmotesManager.GetRadial(),
                Categories = CategoriesManager.GetAll(),
            };
            _radialMenu.EmoteSelected += OnEmoteSelected;
            UpdateRadialEmptyState();
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
            UpdateRadialEmptyState();
        }

        private void DrawEmoteListContextMenu()
        {
            _emoteListMenuStrip?.Dispose();

            _emoteListMenuStrip = new ContextMenuStrip();
            var allEmotes = EmotesManager.GetAll();
            if (allEmotes.Count == 0)
            {
                BuildEmptyEmoteListMenu(_emoteListMenuStrip);
            }
            else
            {
                var menuItems = this.Settings.GlobalUseCategories.Value ? GetCategoryMenuItems() : GetEmotesMenuItems(allEmotes);
                _emoteListMenuStrip.AddMenuItems(menuItems);
            }
        }

        private void BuildEmptyEmoteListMenu(ContextMenuStrip menu)
        {
            var state = CatalogManager.LastSyncResult.State;
            bool hasCache = CatalogManager.HasCache();
            if (state == CatalogSyncState.Downloading && !hasCache)
            {
                menu.AddMenuItem(new ContextMenuStripItem(Common.emoteList_gettingEmotes) { Enabled = false });
            }
            else if (!hasCache && state == CatalogSyncState.Failed)
            {
                menu.AddMenuItem(new ContextMenuStripItem(Common.emoteList_noEmotesYet) { Enabled = false });
                menu.AddMenuItem(new ContextMenuStripItem(Common.emoteList_noEmotesRetry) { Enabled = false });
                var openSettings = new ContextMenuStripItem(Common.emoteList_openSettings);
                openSettings.Click += delegate { _settingsWindow.Show(); };
                menu.AddMenuItem(openSettings);
            }
            else
            {
                menu.AddMenuItem(new ContextMenuStripItem(Common.emoteList_noEmotesYet) { Enabled = false });
            }
        }

        private void AddEmoteModifierStatus(ref List<ContextMenuStripItem> items)
        {
            var lastResult = CatalogManager.LastSyncResult;
            if (lastResult.State == CatalogSyncState.Failed && CatalogManager.HasCache() && lastResult.Version.HasValue)
            {
                items.Insert(0, new ContextMenuStripItem($"[ {string.Format(Common.cornerIcon_catalogOffline, lastResult.Version.Value).ToUpper()} ]"));
            }
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
            toggleFavMenuItem.CheckedChanged += (sender, args) => {
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
                if (Gw2ApiManager.HasPermissions(new[] { TokenPermission.Account, TokenPermission.Progression, TokenPermission.Unlocks }))
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

        private async void OnApiSubTokenUpdated(object sender, ValueEventArgs<IEnumerable<TokenPermission>> e)
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
            TexturesManager.Dispose();
            
            CatalogManager.ProgressChanged -= OnCatalogProgressChanged;
            CatalogManager.LastSyncResultUpdated -= OnSyncResultUpdated;

            // All static members must be manually unset
            ModuleInstance = null;
        }

    }

}
