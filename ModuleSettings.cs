using Blish_HUD.Input;
using Blish_HUD.Settings;
using felix.BlishEmotes.Strings;
using System;
using System.Collections.Generic;

namespace felix.BlishEmotes
{
    public class ModuleSettings
    {

        private Helper _helper;

        public ModuleSettings(SettingCollection settings, Helper helper)
        {
            this._helper = helper;
            this.RootSettings = settings;
            DefineGlobalSettings(settings);
            DefineEmotesKeybindSettings(settings);
            DefineRadialMenuSettings(settings);
        }
        public SettingCollection RootSettings { get; private set; }

        #region Global Settings
        private const string GLOBAL_SETTINGS = "global-settings";
        public SettingCollection GlobalSettings { get; private set; }

        public SettingEntry<bool> GlobalHideCornerIcon { get; private set; }
        public SettingEntry<KeyBinding> GlobalKeyBindToggleEmoteList { get; private set; }
        public SettingEntry<bool> GlobalUseRadialMenu { get; private set; }

        private void DefineGlobalSettings(SettingCollection settings)
        {
            this.GlobalSettings = settings.AddSubCollection(GLOBAL_SETTINGS);

            this.GlobalHideCornerIcon = this.GlobalSettings.DefineSetting(nameof(this.GlobalHideCornerIcon), false, () => Common.settings_global_hideCornerIcon);
            this.GlobalKeyBindToggleEmoteList = this.GlobalSettings.DefineSetting(nameof(this.GlobalKeyBindToggleEmoteList), new KeyBinding(), () => Common.settings_global_keybindToggleEmoteList);
            this.GlobalUseRadialMenu = this.GlobalSettings.DefineSetting(nameof(this.GlobalUseRadialMenu), false, () => Common.settings_global_useRadialMenu);
        }
        #endregion


        #region Emotes Shortcuts
        private const string EMOTES_SHORTCUT_SETTINGS = "emotes-shortcuts-settings";
        public SettingCollection EmotesShortcutsSettings { get; private set; }

        public Dictionary<Emote, SettingEntry<KeyBinding>> EmotesShortcutsKeybindsMap { get; private set; }

        private void DefineEmotesKeybindSettings(SettingCollection settings)
        {
            this.EmotesShortcutsSettings = settings.AddSubCollection(EMOTES_SHORTCUT_SETTINGS);

            this.EmotesShortcutsKeybindsMap = new Dictionary<Emote, SettingEntry<KeyBinding>>();
        }

        public event EventHandler<bool> OnEmotesLoaded;
        public void InitEmotesShortcuts(List<Emote> emotes)
        {
            this.EmotesShortcutsKeybindsMap.Clear();
            foreach (Emote emote in emotes)
            {
                this.EmotesShortcutsKeybindsMap.Add(emote, this.EmotesShortcutsSettings.DefineSetting(nameof(this.EmotesShortcutsKeybindsMap) + "_" + emote.Id, new KeyBinding(), () => _helper.EmotesResourceManager.GetString(emote.Id)));

                this.EmotesShortcutsKeybindsMap[emote].Value.Enabled = !emote.Locked;
                this.EmotesShortcutsKeybindsMap[emote].Value.Activated += delegate
                {
                    _helper.SendEmoteCommand(emote);
                };
            }
            OnEmotesLoaded?.Invoke(this, true);
        }
        #endregion

        #region RadialMenuSettings
        private const string RADIAL_MENU_SETTINGS = "radial-menu-settings";
        public SettingCollection RadialMenuSettings { get; private set; }

        public SettingEntry<bool> RadialSpawnAtCursor { get; private set; }

        public SettingEntry<KeyBinding> RadialToggleActionCameraKeyBind { get; private set; }

        public SettingEntry<float> RadialRadiusModifier { get; private set; }
        public SettingEntry<float> RadialIconSizeModifier { get; private set; }
        public SettingEntry<float> RadialIconOpacity { get; private set; }

        private void DefineRadialMenuSettings(SettingCollection settings)
        {
            this.RadialMenuSettings = settings.AddSubCollection(RADIAL_MENU_SETTINGS);

            this.RadialSpawnAtCursor = this.RadialMenuSettings.DefineSetting(nameof(this.RadialSpawnAtCursor), false, () => Common.settings_radial_spawnAtCursor);
            this.RadialToggleActionCameraKeyBind = this.RadialMenuSettings.DefineSetting(nameof(this.RadialToggleActionCameraKeyBind), new KeyBinding(), () => Common.settings_radial_actionCamKeybind);
            this.RadialRadiusModifier = this.RadialMenuSettings.DefineSetting(nameof(this.RadialRadiusModifier), 0.5f, () => Common.settings_radial_radiusModifier);
            this.RadialRadiusModifier.SetRange(0.2f, 1.0f);
            this.RadialIconSizeModifier = this.RadialMenuSettings.DefineSetting(nameof(this.RadialIconSizeModifier), 0.5f, () => Common.settings_radial_iconSizeModifier);
            this.RadialIconSizeModifier.SetRange(0.5f, 1.0f);
            this.RadialIconOpacity = this.RadialMenuSettings.DefineSetting(nameof(this.RadialIconOpacity), 0.5f, () => Common.settings_radial_iconOpacity);
            this.RadialIconOpacity.SetRange(0.5f, 1.0f);
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
