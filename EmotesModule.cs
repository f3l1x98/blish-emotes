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
        private DateTime _lastCatalogCheck = DateTime.MinValue;
        private long? _syncStartVersion;


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

            CatalogManager.ProgressChanged += OnCatalogProgressChanged;
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
                    CategoriesManager.ApplyCatalog(cachedCatalog, pendingUpdate, EmotesManager.CurrentLocale, EmotesManager.GetAll());
                    if (pendingUpdate != null)
                    {
                        CatalogManager.ClearPendingUpdate();
                    }
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
                    _ = RefreshCatalogAsync();
                }
            }
            catch (Exception e)
            {
                Logger.Fatal("LoadAsync failed!");
                Logger.Error(e.ToString());
            }
        }

        private void OnCatalogProgressChanged(object sender, CatalogSyncProgress progress)
        {
            GameService.Graphics.QueueMainThreadRender(device =>
            {
                _cornerIcon.LoadingMessage = Common.settings_global_sync_downloading.Substring(0, Common.settings_global_sync_downloading.IndexOf("…") + 1);
                _globalSettingsView?.UpdateCatalogStatus(BuildCatalogStatus(CatalogManager.LastSyncResult, _syncStartVersion, progress));
                UpdateRadialEmptyState();
            });
        }

        private async Task RefreshCatalogAsync()
        {
            if (!await _syncLock.WaitAsync(0).ConfigureAwait(false))
            {
                // A manual Check now is already running.
                return;
            }
            try
            {
                Logger.Debug("Refreshing emote catalog in the background");
                _syncStartVersion = CatalogManager.LastSyncResult.Version;
                var result = await CatalogManager.SyncAsync().ConfigureAwait(false);
                GameService.Graphics.QueueMainThreadRender(device =>
                {
                    _cornerIcon.LoadingMessage = null;
                    _lastCatalogCheck = DateTime.UtcNow;
                    UpdateCatalogUx();
                });
                if (result.State == CatalogSyncState.FirstFetch || result.State == CatalogSyncState.Updated)
                {
                    QueueFirstRunMigration();
                }
            }
            catch (Exception e)
            {
                Logger.Error("Catalog refresh failed unexpectedly!");
                Logger.Error(e.ToString());
                GameService.Graphics.QueueMainThreadRender(device =>
                {
                    _cornerIcon.LoadingMessage = null;
                    _lastCatalogCheck = DateTime.UtcNow;
                    UpdateCatalogUx();
                });
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

            await _syncLock.WaitAsync().ConfigureAwait(false);
            _syncStartVersion = CatalogManager.LastSyncResult.Version;
            GameService.Graphics.QueueMainThreadRender(device =>
            {
                _globalSettingsView.UpdateCatalogStatus(BuildCatalogStatus(new CatalogSyncResult { State = CatalogSyncState.Downloading, Version = _syncStartVersion }, _syncStartVersion, CatalogManager.CurrentProgress));
                _cornerIcon.LoadingMessage = Common.settings_global_sync_downloading.Substring(0, Common.settings_global_sync_downloading.IndexOf("…") + 1);
            });

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
                    _cornerIcon.LoadingMessage = null;
                    _lastCatalogCheck = DateTime.UtcNow;
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
                finally
                {
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

        private CatalogStatus BuildCatalogStatus(CatalogSyncResult result, long? previousVersion, CatalogSyncProgress progress = null)
        {
            bool hasCache = CatalogManager.HasCache();
            bool isDownloading = result.State == CatalogSyncState.Downloading;
            var status = new CatalogStatus
            {
                State = result.State,
                Version = result.Version,
                PreviousVersion = previousVersion,
                NewEmoteCount = result.NewEmoteIds?.Count ?? 0,
                NewEmoteNames = GetNewEmoteNames(result.NewEmoteIds),
                CheckOnStartup = this.Settings.GlobalCheckCatalogOnStartup.Value,
                CanCheck = !isDownloading,
                CheckButtonText = (!hasCache && result.State == CatalogSyncState.Failed) ? Common.settings_global_catalog_retryNow : Common.settings_global_checkNow,
            };

            if (isDownloading)
            {
                status.StatusLine = hasCache
                    ? string.Format(Common.settings_global_sync_downloading, progress?.DownloadedFiles ?? 0, progress?.TotalFiles ?? 0)
                    : "downloading… (first run)";
                status.Feedback = Common.settings_global_sync_downloading;
                status.FeedbackSubtext = hasCache ? null : Common.settings_global_sync_downloading_sub;
                status.FeedbackColor = Color.Gray;
                status.ShowProgress = true;
                if (progress != null)
                {
                    status.DownloadedFiles = progress.DownloadedFiles;
                    status.TotalFiles = progress.TotalFiles;
                }
                return status;
            }

            status.ShowProgress = false;

            switch (result.State)
            {
                case CatalogSyncState.UpToDate:
                    if (hasCache)
                    {
                        status.StatusLine = string.Format(Common.settings_global_sync_statusLine, result.Version, FormatLastChecked());
                        status.Feedback = string.Format(Common.settings_global_sync_upToDate_detail, result.Version);
                        status.FeedbackColor = Color.Gray;
                    }
                    else
                    {
                        status.StatusLine = "no catalog yet";
                        status.Feedback = Common.settings_global_sync_noCatalog_detail;
                        status.FeedbackColor = Color.Gray;
                    }
                    break;
                case CatalogSyncState.Updated:
                    status.StatusLine = string.Format(Common.settings_global_sync_statusLine, result.Version, FormatLastChecked());
                    status.Feedback = string.Format(Common.settings_global_sync_updated_detail, previousVersion, result.Version, result.NewEmoteIds.Count, status.NewEmoteNames);
                    status.FeedbackColor = Color.Green;
                    break;
                case CatalogSyncState.FirstFetch:
                    status.StatusLine = string.Format(Common.settings_global_sync_statusLine, result.Version, FormatLastChecked());
                    status.Feedback = string.Format(Common.settings_global_sync_firstFetch_detail, result.NewEmoteIds.Count);
                    status.FeedbackColor = Color.Green;
                    break;
                case CatalogSyncState.Failed:
                    if (hasCache)
                    {
                        status.StatusLine = $"v{result.Version} · cached · last check failed";
                        status.Feedback = string.Format(Common.settings_global_sync_failed_detail, result.Version);
                        status.FeedbackColor = Color.Red;
                    }
                    else
                    {
                        status.StatusLine = "no catalog yet";
                        status.Feedback = Common.settings_global_sync_firstFail_detail;
                        status.FeedbackSubtext = Common.settings_global_sync_firstFail_sub;
                        status.FeedbackColor = Color.Red;
                    }
                    break;
                case CatalogSyncState.SchemaTooNew:
                    if (hasCache)
                    {
                        status.StatusLine = $"v{result.Version} · cached · catalog too new";
                        status.Feedback = Common.settings_global_sync_schemaTooNew_detail;
                        status.FeedbackSubtext = string.Format(Common.settings_global_sync_schemaTooNew_sub, result.Version);
                        status.FeedbackColor = Color.Orange;
                    }
                    else
                    {
                        status.StatusLine = "no catalog yet";
                        status.Feedback = Common.settings_global_sync_schemaTooNew_detail;
                        status.FeedbackSubtext = Common.settings_global_sync_schemaTooNew_noCache_sub;
                        status.FeedbackColor = Color.Orange;
                    }
                    break;
            }

            return status;
        }

        private void UpdateCatalogUx()
        {
            var result = CatalogManager.LastSyncResult;
            var status = BuildCatalogStatus(result, _syncStartVersion);
            _globalSettingsView?.UpdateCatalogStatus(status);
            UpdateRadialEmptyState();
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

        private string FormatLastChecked()
        {
            if (_lastCatalogCheck == DateTime.MinValue)
            {
                return "never";
            }
            var elapsed = DateTime.UtcNow - _lastCatalogCheck;
            if (elapsed.TotalSeconds < 60)
            {
                return "just now";
            }
            if (elapsed.TotalMinutes < 60)
            {
                return $"{(int)elapsed.TotalMinutes}m ago";
            }
            return $"{(int)elapsed.TotalHours}h ago";
        }

        private string GetNewEmoteNames(List<string> ids)
        {
            if (ids == null || ids.Count == 0)
            {
                return string.Empty;
            }
            var labels = ids.Select(id => EmotesManager.GetAll().FirstOrDefault(e => e.Id == id)?.Label ?? id).ToList();
            return string.Join(", ", labels);
        }

        private void ShowCatalogToast(CatalogSyncResult result)
        {
            if (result.NewEmoteIds == null || result.NewEmoteIds.Count == 0)
            {
                return;
            }

            string names = GetNewEmoteNames(result.NewEmoteIds);
            string message = result.State == CatalogSyncState.FirstFetch
                ? string.Format(Common.settings_global_sync_firstFetch_toast, names)
                : string.Format(Common.settings_global_sync_updated_toast, names);
            ScreenNotification.ShowNotification(message, ScreenNotification.NotificationType.Green);
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
