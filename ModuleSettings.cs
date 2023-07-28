using Blish_HUD.Input;
using Blish_HUD.Settings;
using felix.BlishEmotes.Strings;
using System.Collections.Generic;
using System.Resources;

namespace felix.BlishEmotes
{
    public class ModuleSettings
    {

        private ResourceManager _emotesResourceManager;

        public ModuleSettings(SettingCollection settings, ResourceManager emotesResourceManager)
        {
            this.RootSettings = settings;
            DefineGlobalSettings(settings);
            DefineEmotesKeybindSettings(settings);
            _emotesResourceManager = emotesResourceManager;
        }
        public SettingCollection RootSettings { get; private set; }

        #region Global Settings
        private const string GLOBAL_SETTINGS = "global-settings";
        public SettingCollection GlobalSettings { get; private set; }

        public SettingEntry<bool> GlobalHideCornerIcon { get; private set; }
        public SettingEntry<KeyBinding> GlobalKeyBindToggleEmoteList { get; private set; }

        private void DefineGlobalSettings(SettingCollection settings)
        {
            this.GlobalSettings = settings.AddSubCollection(GLOBAL_SETTINGS);
            this.GlobalSettings.RenderInUi = true;

            this.GlobalHideCornerIcon = this.GlobalSettings.DefineSetting(nameof(this.GlobalHideCornerIcon), false, () => Common.settings_global_hideCornerIcon);
            this.GlobalKeyBindToggleEmoteList = this.GlobalSettings.DefineSetting(nameof(this.GlobalKeyBindToggleEmoteList), new KeyBinding(), () => Common.settings_global_keybindToggleEmoteList);
        }
        #endregion


        #region Emotes Shortcuts
        private const string EMOTES_SHORTCUT_SETTINGS = "emotes-shortcuts-settings";
        public SettingCollection EmotesShortcutsSettings { get; private set; }

        public Dictionary<Emote, SettingEntry<KeyBinding>> EmotesShortcutsKeybindsMap { get; private set; }

        private void DefineEmotesKeybindSettings(SettingCollection settings)
        {
            this.EmotesShortcutsSettings = settings.AddSubCollection(EMOTES_SHORTCUT_SETTINGS);
            this.EmotesShortcutsSettings.RenderInUi = true;

            this.EmotesShortcutsKeybindsMap = new Dictionary<Emote, SettingEntry<KeyBinding>>();
        }

        public bool EmotesLoaded { get; private set; } = false;
        public void InitEmotesShortcuts(List<Emote> emotes)
        {
            this.EmotesShortcutsKeybindsMap.Clear();
            foreach (Emote emote in emotes)
            {
                this.EmotesShortcutsKeybindsMap.Add(emote, this.EmotesShortcutsSettings.DefineSetting(nameof(this.EmotesShortcutsKeybindsMap) + "_" + emote.Id, new KeyBinding(), () => _emotesResourceManager.GetString(emote.Id)));

                this.EmotesShortcutsKeybindsMap[emote].Value.Enabled = !emote.Locked;
            }
            EmotesLoaded = true;
        }
        #endregion

        public void Unload()
        {
            this.GlobalKeyBindToggleEmoteList.Value.Enabled = false;
            foreach (var entry in this.EmotesShortcutsKeybindsMap)
            {
                entry.Value.Value.Enabled = false;
            }
        }
    }
}
