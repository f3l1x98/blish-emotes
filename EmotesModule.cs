using Blish_HUD;
using Blish_HUD.Controls;
using Blish_HUD.GameIntegration;
using Blish_HUD.Graphics.UI;
using Blish_HUD.Modules;
using Blish_HUD.Modules.Managers;
using Blish_HUD.Settings;
using felix.BlishEmotes;
using felix.BlishEmotes.Services.Catalog;
using felix.BlishEmotes.Exceptions;
using felix.BlishEmotes.Services;
using felix.BlishEmotes.Strings;
using felix.BlishEmotes.UI.Controls;
using felix.BlishEmotes.UI.Views;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Resources;
using System.Text.Json;
using System.Threading;
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

        private readonly System.Threading.SemaphoreSlim _syncLock = new System.Threading.SemaphoreSlim(1, 1);


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
            _globalSettingsView = new GlobalSettingsView(this.Settings);
            _globalSettingsView.CheckNowClicked += async (sender, e) => await CheckNowAsync();
            _settingsWindow.Tabs.Add(new Tab(TexturesManager.GetTexture(Textures.GlobalSettingsIcon), () => _globalSettingsView, Common.settings_ui_global_tab));
            // Category setting
            _settingsWindow.Tabs.Add(new Tab(TexturesManager.GetTexture(Textures.CategorySettingsIcon), () => new CategorySettingsView(CategoriesManager, EmotesManager), Common.settings_ui_categories_tab));
            // Emote Hotkey settings
            _settingsWindow.Tabs.Add(new Tab(TexturesManager.GetTexture(Textures.HotkeySettingsIcon), () => new EmoteHotkeySettingsView(this.Settings), Common.settings_ui_emoteHotkeys_tab));
        }

        protected override async Task LoadAsync()
        {
            try
            {
                // Load categories
                CategoriesManager.Load();
                // Load emotes: cached catalog if we have one (offline-capable), embedded data otherwise
                var cachedCatalog = CatalogManager.LoadCachedCatalogOrNull();
                if (cachedCatalog != null)
                {
                    EmotesManager.LoadFromCatalog(cachedCatalog);
                    // Reconcile the persisted categories with the cached catalog: the one-time V2
                    // migration (stamp name-matched categories), and — when a fetched update was
                    // applied to the cache since the last launch — the merge that places new
                    // emotes into linked categories and auto-creates untombstoned ones.
                    var pendingUpdate = CatalogManager.ReadPendingUpdateOrNull();
                    CategoriesManager.ApplyCatalog(cachedCatalog, pendingUpdate, EmotesManager.CurrentLocale, EmotesManager.GetAll());
                    if (pendingUpdate != null)
                    {
                        CatalogManager.ClearPendingUpdate();
                    }
                }
                else
                {
                    EmotesManager.LoadEmbedded();
                }
                // Update emotes with data from api
                await UpdateEmotesFromApi();
                // Update category Emotes
                CategoriesManager.ResolveEmoteIds(EmotesManager.GetAll());

                this.Settings.InitEmotesSettings(EmotesManager.GetAll());
                InitUI();

                // Ticket 03: non-blocking cheap conditional sync. Updates are written to the cache
                // and take effect at the next launch; live apply is ticket 05.
                _ = RefreshCatalogAsync();
            }
            catch (Exception e)
            {
                Logger.Fatal("LoadAsync failed!");
                Logger.Error(e.ToString());
            }
        }

        private async Task RefreshCatalogAsync()
        {
            try
            {
                await _syncLock.WaitAsync().ConfigureAwait(false);
                Logger.Debug("Refreshing emote catalog in the background");
                var result = await CatalogManager.SyncAsync().ConfigureAwait(false);
                if (result.State == CatalogSyncState.FirstFetch || result.State == CatalogSyncState.Updated)
                {
                    QueueFirstRunMigration();
                }
            }
            catch (Exception e)
            {
                Logger.Error("Catalog refresh failed unexpectedly!");
                Logger.Error(e.ToString());
            }
            finally
            {
                _syncLock.Release();
            }
        }

        private async Task CheckNowAsync()
        {
            if (_globalSettingsView == null)
            {
                return;
            }

            _globalSettingsView.SetCheckNowEnabled(false);
            _globalSettingsView.SetSyncStatus(Common.settings_global_sync_checking);

            await _syncLock.WaitAsync().ConfigureAwait(false);
            CatalogSyncResult result;
            try
            {
                Logger.Debug("Manual catalog check started");
                result = await CatalogManager.SyncAsync().ConfigureAwait(false);
            }
            catch (Exception e)
            {
                Logger.Error("Manual catalog check failed unexpectedly!");
                Logger.Error(e.ToString());
                result = new CatalogSyncResult
                {
                    State = CatalogSyncState.Failed,
                    ErrorMessage = e.Message,
                };
            }

            // The sync ran in the background; apply and UI feedback must happen on the game thread.
            GameService.Graphics.QueueMainThreadRender(device =>
            {
                try
                {
                    if (result.State == CatalogSyncState.Updated || result.State == CatalogSyncState.FirstFetch)
                    {
                        ApplyCatalogLive();
                    }
                    UpdateCheckNowStatus(result);
                }
                catch (Exception e)
                {
                    Logger.Error("Live catalog apply failed unexpectedly!");
                    Logger.Error(e.ToString());
                    _globalSettingsView.SetSyncStatus(string.Format(Common.settings_global_sync_failed, e.Message));
                }
                finally
                {
                    _globalSettingsView.SetCheckNowEnabled(true);
                    _syncLock.Release();
                }
            });
        }

        private void ApplyCatalogLive()
        {
            var cachedCatalog = CatalogManager.LoadCachedCatalogOrNull();
            if (cachedCatalog == null)
            {
                return;
            }

            var pendingUpdate = CatalogManager.ReadPendingUpdateOrNull();
            var newEmotes = EmotesManager.BuildCatalogEmotes(cachedCatalog);
            try
            {
                var result = CategoriesManager.ApplyCatalog(cachedCatalog, pendingUpdate, EmotesManager.CurrentLocale, newEmotes, fireCategoriesUpdated: false);

                // A first-run migration stamps categories but does not place new emotes. Apply the
                // same pending update a second time against the now-migrated state to place them.
                if (result.Outcome == CatalogMergeOutcome.Migrated && pendingUpdate != null)
                {
                    result = CategoriesManager.ApplyCatalog(cachedCatalog, pendingUpdate, EmotesManager.CurrentLocale, newEmotes, fireCategoriesUpdated: false);
                }

                if (result.Outcome != CatalogMergeOutcome.Noop && pendingUpdate != null && result.Outcome != CatalogMergeOutcome.Migrated)
                {
                    CatalogManager.ClearPendingUpdate();
                }

                // Swap the emote collection only after the category merge succeeded.
                EmotesManager.GraveyardAndUpdateAll(newEmotes);
                CategoriesManager.NotifyCategoriesUpdated();
                this.Settings.InitEmotesSettings(EmotesManager.GetAll());

                // Refresh lock states for newly arrived emotes within seconds.
                _ = UpdateEmotesFromApi();

                Logger.Info("Live catalog apply completed.");
            }
            catch (Exception)
            {
                // The category merge failed before swapping anything live, or the emote swap
                // failed. New textures were never exposed to the UI, so dispose them immediately.
                foreach (var emote in newEmotes)
                {
                    emote.Texture?.Dispose();
                }
                throw;
            }
        }

        private void UpdateCheckNowStatus(CatalogSyncResult result)
        {
            switch (result.State)
            {
                case CatalogSyncState.UpToDate:
                    _globalSettingsView.SetSyncStatus(Common.settings_global_sync_upToDate);
                    break;
                case CatalogSyncState.Updated:
                case CatalogSyncState.FirstFetch:
                    _globalSettingsView.SetSyncStatus(string.Format(Common.settings_global_sync_updated, result.NewEmoteIds.Count));
                    break;
                case CatalogSyncState.SchemaTooNew:
                    _globalSettingsView.SetSyncStatus(Common.settings_global_sync_schemaTooNew);
                    break;
                case CatalogSyncState.Failed:
                    _globalSettingsView.SetSyncStatus(string.Format(Common.settings_global_sync_failed, result.ErrorMessage));
                    break;
                default:
                    _globalSettingsView.SetSyncStatus(string.Empty);
                    break;
            }
        }

        /// <summary>
        /// A user upgrading from 1.x migrates on the first 2.0 run — the moment a catalog first
        /// becomes available (a fetch just succeeded). Stamping catalogIds onto name-matched
        /// categories changes nothing rendered, so it is safe to apply immediately; the fetched
        /// emotes and any new categories themselves take effect on the next launch (live apply is
        /// ticket 05). No-op when the state is already migrated.
        /// </summary>
        private void QueueFirstRunMigration()
        {
            var cachedCatalog = CatalogManager.LoadCachedCatalogOrNull();
            if (cachedCatalog == null)
            {
                return;
            }
            var pendingUpdate = CatalogManager.ReadPendingUpdateOrNull();
            GameService.Graphics.QueueMainThreadRender(device =>
            {
                try
                {
                    if (PersistenceManager.MergedCatalogVersion == null)
                    {
                        var migrationResult = CategoriesManager.ApplyCatalog(cachedCatalog, pendingUpdate, EmotesManager.CurrentLocale, EmotesManager.GetAll());
                        // The migration only stamps categories; keep the pending update so a
                        // live apply (or the next launch) can place new emotes.
                        if (pendingUpdate != null && migrationResult.Outcome != CatalogMergeOutcome.Migrated)
                        {
                            CatalogManager.ClearPendingUpdate();
                        }
                    }
                    // Already migrated: the update stays recorded and is applied at the next
                    // launch or by a manual Check now.
                }
                catch (Exception e)
                {
                    Logger.Error("Catalog migration failed - retrying at the next launch.");
                    Logger.Error(e.ToString());
                }
            });
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
            TexturesManager.Dispose();

            // All static members must be manually unset
            ModuleInstance = null;
        }

    }

}
