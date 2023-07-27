using Blish_HUD;
using Blish_HUD.Controls;
using Blish_HUD.Graphics.UI;
using Blish_HUD.Input;
using Blish_HUD.Modules;
using Blish_HUD.Modules.Managers;
using Blish_HUD.Settings;
using felix.BlishEmotesList;
using felix.BlishEmotes.Strings;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.IO;
using System.Resources;
using System.Runtime;
using System.Text.Json;
using System.Threading.Tasks;

namespace BlishEmotesList
{
    [Export(typeof(Blish_HUD.Modules.Module))]
    public class EmoteLisModule : Blish_HUD.Modules.Module
    {

        private static readonly Logger Logger = Logger.GetLogger<EmoteLisModule>();

        ResourceManager EmotesResourceManager = new ResourceManager("felix.BlishEmotesList.Strings.Emotes", typeof(Common).Assembly);

        #region Service Managers
        internal SettingsManager SettingsManager => this.ModuleParameters.SettingsManager;
        internal ContentsManager ContentsManager => this.ModuleParameters.ContentsManager;
        internal DirectoriesManager DirectoriesManager => this.ModuleParameters.DirectoriesManager;
        internal Gw2ApiManager Gw2ApiManager => this.ModuleParameters.Gw2ApiManager;
        #endregion

        #region Controls
        private CornerIcon _cornerIcon;
        private ContextMenuStrip _emoteListMenuStrip;
        #endregion

        #region Settings
        private SettingEntry<bool> _hideCornerIcon;
        private SettingEntry<KeyBinding> _keyBindToggleEmoteList;
        private SettingCollection _rootSettings;
        private SettingCollection _emotesKeybindSubCollection;
        private Dictionary<string, SettingEntry<KeyBinding>> _keyBindEmotes;
        #endregion

        private List<Emote> _emotes;
        private List<string> _unlockableEmotesIds;
        private List<string> _unlockedEmotesIds;


         [ImportingConstructor]
        public EmoteLisModule([Import("ModuleParameters")] ModuleParameters moduleParameters) : base(moduleParameters) { }

        protected override void DefineSettings(SettingCollection settings)
        {
            Logger.Debug("DefineSettings");
            _rootSettings = settings;
            // TODO SETTINGS IDEAS:
            // USE Strings.Common FOR i18n!!!!!!!!!!!
            // - Enabled/Disable CornerIcon
            // - Enabled/Disable CornerIcon Categories (-> categorize emotes)
            /* EXAMPLE CATEGORIES
    Dance
    Stunt
    Greetings
    Fun
    Poses
    Reactions
    Horror
    Miscellaneous
             */
            // - Emote Wheel
            // - Emote keybind -> IF THERE IS NO NATIVE SUPPORT ALREADY


            // TODO PERHAPS MOVE INTO OWN SEPARATE SETTINGS WINDOW -> two tabs, one for general settings, one for separate keybinds?!?!
            _hideCornerIcon = settings.DefineSetting(nameof(_hideCornerIcon), false, () => Common.settings_hideCornerIcon);
            _keyBindToggleEmoteList = settings.DefineSetting(nameof(_keyBindToggleEmoteList), new KeyBinding(), () => Common.settings_keybindToggleEmoteList);

            // Handlers
            _hideCornerIcon.SettingChanged += (sender, args) => {
                if (args.NewValue)
                {
                    _cornerIcon?.Hide();
                } else
                {
                    _cornerIcon?.Show();
                }
            };
            _keyBindToggleEmoteList.Value.Enabled = true;
            _keyBindToggleEmoteList.Value.Activated += delegate {
                ShowEmoteList(false);
            };
        }

        private void InitEmotesKeybindSettings()
        {

            // emote keybind subsection
            _emotesKeybindSubCollection = _rootSettings.AddSubCollection(nameof(_emotesKeybindSubCollection), true, () => Common.settings_emotesKeybindSubCollection);
            _emotesKeybindSubCollection.RenderInUi = true;
            // TODO emotes have to be loaded
            _keyBindEmotes = new Dictionary<string, SettingEntry<KeyBinding>>();
            foreach (var emote in _emotes)
            {
                var emoteSetting = _emotesKeybindSubCollection.DefineSetting("_keyBind" + emote.id, new KeyBinding(), () => EmotesResourceManager.GetString(emote.id));
                emoteSetting.Value.Enabled = true;
                emoteSetting.Value.Activated += delegate {
                    SendEmoteCommand(emote);
                };
                _keyBindEmotes.Add(emote.id, emoteSetting);
            }
        }

        protected override void Initialize()
        {
            Logger.Debug("Initialize");
            // TODO THE SAME HAS TO BE CALLED IF PERMISSIONS ARE UPDATED
            Gw2ApiManager.SubtokenUpdated += OnApiSubTokenUpdated;
            // Init lists
            _emotes = new List<Emote>();
            _unlockableEmotesIds = new List<string>();
            _unlockedEmotesIds = new List<string>();
        }

        protected override async Task LoadAsync()
        {
            Logger.Debug("LoadAsync");
            try
            {
                // load emotes
                _emotes = LoadEmotesResource();
                // load emote information from api
                await LoadEmotesFromApi();

                // Set emotes locked
                UpdateEmotesLock();

                InitEmotesKeybindSettings();


            } catch (Exception e)
            {
                Logger.Fatal("LoadAsync failed!");
                Logger.Error(e.ToString());
            }
        }

        protected override void OnModuleLoaded(EventArgs e)
        {
            Logger.Debug("OnModuleLoaded");
            _cornerIcon = new CornerIcon()
            {
                Icon = ContentsManager.GetTexture(@"textures/603447.png"),
                BasicTooltipText = Common.cornerIcon_tooltip,
            };

            _cornerIcon.Click += delegate
            {
                ShowEmoteList();
            };
            // Base handler must be called
            base.OnModuleLoaded(e);
        }

        // TODO SEE https://github.com/blish-hud/Pathing/blob/main/PathingModule.cs
        /*public override IView GetSettingsView()
        {
            return new SettingsHintView((_settingsWindow.Show, this.PackInitiator));
        }*/
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
            _emoteListMenuStrip.AddMenuItems(GetEmotesMenuItems());
            if (atCornerIcon)
            {
                _emoteListMenuStrip.Show(_cornerIcon);
            } else
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
                    Text = EmotesResourceManager.GetString(emote.id), // TODO TEST LOCALIZATION
                    Enabled = !emote.locked,
                };
                menuItem.Click += delegate {
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
                GameService.GameIntegration.Chat.Send(emote.command);
            }
        }

        private void UpdateEmotesLock()
        {
            foreach (var emote in _emotes)
            {
                // Mark emotes as unlocked
                emote.locked = false;
                if (_unlockableEmotesIds.Contains(emote.id) && !_unlockedEmotesIds.Contains(emote.id))
                {
                    // Mark emotes as locked
                    emote.locked = true;
                }
            }
        }

        private async Task LoadEmotesFromApi()
        {
            if (Gw2ApiManager.HasPermissions(new [] { Gw2Sharp.WebApi.V2.Models.TokenPermission.Progression, Gw2Sharp.WebApi.V2.Models.TokenPermission.Unlocks }))
            {
                // load locked emotes
                // TODO PERHAPS SIMPLY HARDCODE?!?!?
                _unlockableEmotesIds = new List<string>(await Gw2ApiManager.Gw2ApiClient.V2.Emotes.IdsAsync());
                // load unlocked emotes
                _unlockedEmotesIds = new List<string>(await Gw2ApiManager.Gw2ApiClient.V2.Account.Emotes.GetAsync());
            }
        }

        private List<Emote> LoadEmotesResource()
        {
            string fileContents;
            using (StreamReader reader = new StreamReader(ContentsManager.GetFileStream(@"json/emotes.json")))
            {
                fileContents = reader.ReadToEnd();
            }
            return JsonSerializer.Deserialize<List<Emote>>(fileContents);
        }

        /// <inheritdoc />
        protected override void Unload()
        {
            Gw2ApiManager.SubtokenUpdated -= OnApiSubTokenUpdated;
            _keyBindToggleEmoteList.Value.Enabled = false;
            // Unload here
            _cornerIcon?.Dispose();

            // All static members must be manually unset
        }

    }

}
