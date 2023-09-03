﻿using Blish_HUD;
using Blish_HUD.Controls;
using Blish_HUD.Graphics.UI;
using Blish_HUD.Modules;
using Blish_HUD.Modules.Managers;
using Blish_HUD.Settings;
using felix.BlishEmotes;
using felix.BlishEmotes.Strings;
using felix.BlishEmotes.UI.Controls;
using felix.BlishEmotes.UI.Views;
using Microsoft.Xna.Framework;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.IO;
using System.Linq;
using System.Resources;
using System.Text.Json;
using System.Threading.Tasks;

namespace BlishEmotesList
{
    [Export(typeof(Blish_HUD.Modules.Module))]
    public class EmoteLisModule : Blish_HUD.Modules.Module
    {

        private static readonly Logger Logger = Logger.GetLogger<EmoteLisModule>();

        private Helper _helper;

        #region Service Managers
        internal SettingsManager SettingsManager => this.ModuleParameters.SettingsManager;
        internal ContentsManager ContentsManager => this.ModuleParameters.ContentsManager;
        internal DirectoriesManager DirectoriesManager => this.ModuleParameters.DirectoriesManager;
        internal Gw2ApiManager Gw2ApiManager => this.ModuleParameters.Gw2ApiManager;

        internal PersistenceManager PersistenceManager;
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

        private List<Emote> _emotes;

        private List<Emote> _radialEnabledEmotes => _emotes.Where(el => this.Settings.EmotesRadialEnabledMap.ContainsKey(el) ? this.Settings.EmotesRadialEnabledMap[el].Value : true).ToList();

        private List<Emote> _favouriteEmotes => _emotes.Where(el => el.IsFavourite).ToList();


        [ImportingConstructor]
        public EmoteLisModule([Import("ModuleParameters")] ModuleParameters moduleParameters) : base(moduleParameters)
        {
            _helper = new Helper();
        }

        protected override void DefineSettings(SettingCollection settings)
        {
            this.Settings = new ModuleSettings(settings, _helper);

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
                // Redraw UI due to switch between using categories and full list
                DrawUI();
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
            // Update radial menu emotes
            this.Settings.OnAnyEmotesRadialSettingsChanged += delegate
            {
                if (this._radialMenu != null)
                {
                    // Update radial menu emotes
                    this._radialMenu.Emotes = _radialEnabledEmotes;
                }
            };
        }

        protected override void Initialize()
        {
            Gw2ApiManager.SubtokenUpdated += OnApiSubTokenUpdated;

            // Init PersistenceManager
            try
            {
                PersistenceManager = new PersistenceManager(DirectoriesManager, ContentsManager);
            }
            catch (Exception e)
            {
                Logger.Fatal("Failed to init PersistenceManager!");
                Logger.Fatal(e.Message);
                Logger.Fatal(e.StackTrace);

                Unload();
                return;
            }

            // Init lists
            _emotes = new List<Emote>();

            // Init UI
            if (!this.Settings.GlobalHideCornerIcon.Value)
            {
                InitCornerIcon();
            }

            _settingsWindow = new TabbedWindow2(ContentsManager.GetTexture(@"textures\156006.png"), new Rectangle(35, 36, 900, 640), new Rectangle(95, 42, 783 + 38, 592))
            {
                Title = Common.settings_ui_title,
                Parent = GameService.Graphics.SpriteScreen,
                Location = new Point(100, 100),
                Emblem = this.ContentsManager.GetTexture(@"textures\102390.png"),
                Id = $"{this.Namespace}_SettingsWindow",
                SavesPosition = true,
            };

            // Settings
            _settingsWindow.Tabs.Add(new Tab(ContentsManager.GetTexture(@"textures\155052.png"), () => new GlobalSettingsView(this.Settings), Common.settings_ui_global_tab));
            // Emote Hotkey settings
            _settingsWindow.Tabs.Add(new Tab(ContentsManager.GetTexture(@"textures\156734+155150.png"), () => new EmoteHotkeySettingsView(this.Settings), Common.settings_ui_emoteHotkeys_tab));
        }

        protected override void OnModuleLoaded(EventArgs e)
        {
            DrawUI();

            // Base handler must be called
            base.OnModuleLoaded(e);
        }

        protected override async Task LoadAsync()
        {
            try
            {
                // load emotes
                _emotes = LoadEmotes();//LoadEmotesResource();
                // Update emotes with data from api
                await UpdateEmotesFromApi();

                this.Settings.InitEmotesSettings(_emotes);
                DrawUI();
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
                Icon = ContentsManager.GetTexture(@"textures/emotes_icon.png"),
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

        private void DrawUI()
        {
            _emoteListMenuStrip?.Dispose();

            _emoteListMenuStrip = new ContextMenuStrip();
            var menuItems = this.Settings.GlobalUseCategories.Value ? GetCategoryMenuItems() : GetEmotesMenuItems(_emotes);
            _emoteListMenuStrip.AddMenuItems(menuItems);

            _radialMenu?.Dispose();
            // Init radial menu
            _radialMenu = new RadialMenu(_helper, this.Settings, ContentsManager.GetTexture(@"textures/2107931.png"))
            {
                Parent = GameService.Graphics.SpriteScreen,
                Emotes = _radialEnabledEmotes,
            };

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

        private List<ContextMenuStripItem> GetCategoryMenuItems()
        {
            var items = new List<ContextMenuStripItem>();
            if (_favouriteEmotes.Count > 0)
            {
                var favouriteSubMenu = new ContextMenuStrip();
                favouriteSubMenu.AddMenuItems(GetEmotesMenuItems(_favouriteEmotes));
                var menuItem = new ContextMenuStripItem()
                {
                    Text = Common.emote_categoryFavourite,
                    Submenu = favouriteSubMenu,
                };
                items.Add(menuItem);
            }
            foreach (Category categoryEnum in Enum.GetValues(typeof(Category)))
            {
                var emotesForCategory = _emotes.Where(emote => emote.Category == categoryEnum).ToList();
                var categorySubMenu = new ContextMenuStrip();
                categorySubMenu.AddMenuItems(GetEmotesMenuItems(emotesForCategory));
                var menuItem = new ContextMenuStripItem()
                {
                    Text = categoryEnum.Label(),
                    Submenu = categorySubMenu,
                };
                items.Add(menuItem);
            }
            return items;
        }

        private ContextMenuStrip GetToggleFavContextMenu(Emote emote)
        {
            var toggleFavMenuItem = new ContextMenuStripItem()
            {
                Text = Common.emote_categoryFavourite,
                CanCheck = true,
                Checked = emote.IsFavourite,
            };
            toggleFavMenuItem.CheckedChanged += (sender, args) => {
                emote.IsFavourite = args.Checked;
                PersistenceManager.SaveEmotes(_emotes);
                DrawUI();
                Logger.Debug($"Toggled favourite for {emote.Id} to ${emote.IsFavourite}");
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
                    Text = _helper.EmotesResourceManager.GetString(emote.Id),
                    Enabled = !emote.Locked,
                    Submenu = GetToggleFavContextMenu(emote),
                };
                menuItem.Click += delegate
                {
                    _helper.SendEmoteCommand(emote);
                };
                items.Add(menuItem);
            }
            // Sort by text such that list is sorted no matter what locale
            items.Sort((x, y) => x.Text.CompareTo(y.Text));
            return items;
        }

        private async Task UpdateEmotesFromApi()
        {
            Logger.Debug("Update emotes from api");
            // load emote information from api
            var apiEmotes = await LoadEmotesFromApi();
            // Set locks
            foreach (var emote in _emotes)
            {
                // Mark emotes as unlocked
                emote.Locked = false;
                if (apiEmotes.UnlockableEmotesIds.Contains(emote.Id) && !apiEmotes.UnlockedEmotesIds.Contains(emote.Id))
                {
                    // Mark emotes as locked
                    emote.Locked = true;
                }
            }
        }

        private List<Emote> LoadEmotes()
        {
            List<Emote> emotes = PersistenceManager.LoadEmotes();
            foreach (var emote in emotes)
            {
                emote.Texture = ContentsManager.GetTexture(@"textures/emotes/" + emote.TextureRef, ContentsManager.GetTexture(@"textures/missing-texture.png"));
            }
            return emotes;
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
            } catch (Exception e)
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
            this.Settings.Unload();
            // Unload here
            _cornerIcon?.Dispose();
            _settingsWindow?.Dispose();
            _radialMenu?.Dispose();

            // All static members must be manually unset
        }

    }

}
