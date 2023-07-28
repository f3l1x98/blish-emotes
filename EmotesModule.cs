using Blish_HUD;
using Blish_HUD.Controls;
using Blish_HUD.Graphics.UI;
using Blish_HUD.Modules;
using Blish_HUD.Modules.Managers;
using Blish_HUD.Settings;
using felix.BlishEmotes;
using felix.BlishEmotes.Strings;
using felix.BlishEmotes.UI.Views;
using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.IO;
using System.Resources;
using System.Text.Json;
using System.Threading.Tasks;

namespace BlishEmotesList
{
    [Export(typeof(Blish_HUD.Modules.Module))]
    public class EmoteLisModule : Blish_HUD.Modules.Module
    {

        private static readonly Logger Logger = Logger.GetLogger<EmoteLisModule>();

        public ResourceManager EmotesResourceManager = new ResourceManager("felix.BlishEmotes.Strings.Emotes", typeof(Common).Assembly);

        #region Service Managers
        internal SettingsManager SettingsManager => this.ModuleParameters.SettingsManager;
        internal ContentsManager ContentsManager => this.ModuleParameters.ContentsManager;
        internal DirectoriesManager DirectoriesManager => this.ModuleParameters.DirectoriesManager;
        internal Gw2ApiManager Gw2ApiManager => this.ModuleParameters.Gw2ApiManager;
        #endregion

        #region Controls
        private CornerIcon _cornerIcon;
        private ContextMenuStrip _emoteListMenuStrip;
        private TabbedWindow2 _settingsWindow;
        #endregion

        #region Settings
        public ModuleSettings Settings;
        #endregion

        private List<Emote> _emotes;
        private List<string> _unlockableEmotesIds;
        private List<string> _unlockedEmotesIds;


        [ImportingConstructor]
        public EmoteLisModule([Import("ModuleParameters")] ModuleParameters moduleParameters) : base(moduleParameters) { }

        protected override void DefineSettings(SettingCollection settings)
        {
            this.Settings = new ModuleSettings(settings, EmotesResourceManager);

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
            this.Settings.GlobalKeyBindToggleEmoteList.Value.Enabled = true;
            this.Settings.GlobalKeyBindToggleEmoteList.Value.Activated += delegate
            {
                ShowEmoteList(false);
            };

            foreach (var entry in this.Settings.EmotesShortcutsKeybindsMap)
            {
                entry.Value.Value.Activated += delegate
                {
                    SendEmoteCommand(entry.Key);
                };
            }
        }

        protected override void Initialize()
        {
            // TODO THE SAME HAS TO BE CALLED IF PERMISSIONS ARE UPDATED
            Gw2ApiManager.SubtokenUpdated += OnApiSubTokenUpdated;
            // Init lists
            _emotes = new List<Emote>();
            _unlockableEmotesIds = new List<string>();
            _unlockedEmotesIds = new List<string>();

            // Init UI
            if (!this.Settings.GlobalHideCornerIcon.Value)
            {
                InitCornerIcon();
            }

            _settingsWindow = new TabbedWindow2(ContentsManager.GetTexture(@"textures\156006.png"), new Rectangle(35, 36, 920, 760), new Rectangle(90, 15, 783 + 38, 750))
            {
                Title = Common.settings_ui_title,
                Parent = GameService.Graphics.SpriteScreen,
                Location = new Point(100, 100),
                Emblem = this.ContentsManager.GetTexture(@"textures\102390.png"),
                Id = $"{this.Namespace}_SettingsWindow",
                SavesPosition = true,
            };

            _settingsWindow.Tabs.Add(new Tab(ContentsManager.GetTexture(@"textures\102391.png"), () => new SettingsWindowView(this.Settings), Common.settings_ui_global_tab));
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
        }

        protected override async Task LoadAsync()
        {
            try
            {
                // load emotes
                _emotes = LoadEmotesResource();
                // load emote information from api
                await LoadEmotesFromApi();

                // Set emotes locked
                UpdateEmotesLock();

                this.Settings.InitEmotesShortcuts(_emotes);
            }
            catch (Exception e)
            {
                Logger.Fatal("LoadAsync failed!");
                Logger.Error(e.ToString());
            }
        }

        public override IView GetSettingsView()
        {
            return new SettingsHintView(_settingsWindow.Show);
        }

        private async void OnApiSubTokenUpdated(object sender, ValueEventArgs<IEnumerable<Gw2Sharp.WebApi.V2.Models.TokenPermission>> e)
        {
            // load emote information from api
            await LoadEmotesFromApi();
            // update emotes lock
            UpdateEmotesLock();
        }

        private void ShowEmoteList(bool atCornerIcon = true)
        {
            _emoteListMenuStrip?.Dispose();
            _emoteListMenuStrip = new ContextMenuStrip();
            var menuItems = GetEmotesMenuItems();
            // Sort by text such that list is sorted no matter what locale
            menuItems.Sort((x, y) => x.Text.CompareTo(y.Text));
            _emoteListMenuStrip.AddMenuItems(menuItems);
            if (atCornerIcon)
            {
                _emoteListMenuStrip.Show(_cornerIcon);
            }
            else
            {
                _emoteListMenuStrip.Show(GameService.Input.Mouse.Position);
            }
        }

        private List<ContextMenuStripItem> GetEmotesMenuItems()
        {
            var items = new List<ContextMenuStripItem>();
            foreach (var emote in _emotes)
            {
                var menuItem = new ContextMenuStripItem()
                {
                    Text = EmotesResourceManager.GetString(emote.Id),
                    Enabled = !emote.Locked,
                };
                menuItem.Click += delegate
                {
                    SendEmoteCommand(emote);
                };
                items.Add(menuItem);
            }
            return items;
        }

        private void SendEmoteCommand(Emote emote)
        {
            // Send emote command to chat if in game and map closed
            if (GameService.GameIntegration.Gw2Instance.IsInGame && !GameService.Gw2Mumble.UI.IsMapOpen)
            {
                GameService.GameIntegration.Chat.Send(emote.Command);
            }
        }

        private void UpdateEmotesLock()
        {
            foreach (var emote in _emotes)
            {
                // Mark emotes as unlocked
                emote.Locked = false;
                if (_unlockableEmotesIds.Contains(emote.Id) && !_unlockedEmotesIds.Contains(emote.Id))
                {
                    // Mark emotes as locked
                    emote.Locked = true;
                }
            }
        }

        private List<Emote> LoadEmotesResource()
        {
            string fileContents;
            using (StreamReader reader = new StreamReader(ContentsManager.GetFileStream(@"json/emotes.json")))
            {
                fileContents = reader.ReadToEnd();
            }
            return JsonSerializer.Deserialize<List<Emote>>(fileContents, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });
        }

        private async Task LoadEmotesFromApi()
        {
            if (Gw2ApiManager.HasPermissions(new[] { Gw2Sharp.WebApi.V2.Models.TokenPermission.Account, Gw2Sharp.WebApi.V2.Models.TokenPermission.Progression, Gw2Sharp.WebApi.V2.Models.TokenPermission.Unlocks }))
            {
                // load locked emotes
                _unlockableEmotesIds = new List<string>(await Gw2ApiManager.Gw2ApiClient.V2.Emotes.IdsAsync());
                // load unlocked emotes
                _unlockedEmotesIds = new List<string>(await Gw2ApiManager.Gw2ApiClient.V2.Account.Emotes.GetAsync());
            }
            else
            {
                _unlockableEmotesIds.Clear();
                _unlockedEmotesIds.Clear();
            }
        }

        /// <inheritdoc />
        protected override void Unload()
        {
            Gw2ApiManager.SubtokenUpdated -= OnApiSubTokenUpdated;
            this.Settings.Unload();
            // Unload here
            _cornerIcon?.Dispose();
            _settingsWindow?.Dispose();

            // All static members must be manually unset
        }

    }

}
