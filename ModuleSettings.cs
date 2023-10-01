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
        public SettingEntry<KeyBinding> GlobalKeyBindToggleSynchronize { get; private set; }
        public SettingEntry<KeyBinding> GlobalKeyBindToggleTargeting { get; private set; }
        public SettingEntry<bool> GlobalUseCategories { get; private set; }
        public SettingEntry<bool> GlobalUseRadialMenu { get; private set; }

        private void DefineGlobalSettings(SettingCollection settings)
        {
            this.GlobalSettings = settings.AddSubCollection(GLOBAL_SETTINGS);

            this.GlobalHideCornerIcon = this.GlobalSettings.DefineSetting(nameof(this.GlobalHideCornerIcon), false, () => Common.settings_global_hideCornerIcon);
            this.GlobalKeyBindToggleEmoteList = this.GlobalSettings.DefineSetting(nameof(this.GlobalKeyBindToggleEmoteList), new KeyBinding(), () => Common.settings_global_keybindToggleEmoteList);
            this.GlobalUseCategories = this.GlobalSettings.DefineSetting(nameof(this.GlobalUseCategories), false, () => Common.settings_global_useCategories);
            this.GlobalUseRadialMenu = this.GlobalSettings.DefineSetting(nameof(this.GlobalUseRadialMenu), false, () => Common.settings_global_useRadialMenu);
            this.GlobalKeyBindToggleSynchronize = this.GlobalSettings.DefineSetting(nameof(this.GlobalKeyBindToggleSynchronize), new KeyBinding(), () => Common.settings_global_keybindToggleSynchronize);
            this.GlobalKeyBindToggleTargeting = this.GlobalSettings.DefineSetting(nameof(this.GlobalKeyBindToggleTargeting), new KeyBinding(), () => Common.settings_global_keybindToggleTargeting);
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
        }
        #endregion


        #region RadialMenuSettings
        private const string RADIAL_MENU_SETTINGS = "radial-menu-settings";
        public SettingCollection RadialMenuSettings { get; private set; }

        public SettingEntry<bool> RadialSpawnAtCursor { get; private set; }

        public SettingEntry<KeyBinding> RadialToggleActionCameraKeyBind { get; private set; }

        public SettingEntry<float> RadialRadiusModifier { get; private set; }
        public SettingEntry<float> RadialInnerRadiusPercentage { get; private set; }
        public SettingEntry<float> RadialIconSizeModifier { get; private set; }
        public SettingEntry<float> RadialIconOpacity { get; private set; }

        private void DefineRadialMenuSettings(SettingCollection settings)
        {
            this.RadialMenuSettings = settings.AddSubCollection(RADIAL_MENU_SETTINGS);

            this.RadialSpawnAtCursor = this.RadialMenuSettings.DefineSetting(nameof(this.RadialSpawnAtCursor), false, () => Common.settings_radial_spawnAtCursor);
            this.RadialToggleActionCameraKeyBind = this.RadialMenuSettings.DefineSetting(nameof(this.RadialToggleActionCameraKeyBind), new KeyBinding(), () => Common.settings_radial_actionCamKeybind);
            this.RadialRadiusModifier = this.RadialMenuSettings.DefineSetting(nameof(this.RadialRadiusModifier), 0.35f, () => Common.settings_radial_radiusModifier);
            this.RadialRadiusModifier.SetRange(0.25f, 0.5f);
            this.RadialInnerRadiusPercentage = this.RadialMenuSettings.DefineSetting(nameof(this.RadialInnerRadiusPercentage), 0.25f, () => Common.settings_radial_innerRadiusPercentage, () => Common.settings_radial_innerRadiusPercentage_description);
            this.RadialInnerRadiusPercentage.SetRange(0.0f, 0.5f);
            this.RadialIconSizeModifier = this.RadialMenuSettings.DefineSetting(nameof(this.RadialIconSizeModifier), 0.5f, () => Common.settings_radial_iconSizeModifier);
            this.RadialIconSizeModifier.SetRange(0.25f, 0.75f);
            this.RadialIconOpacity = this.RadialMenuSettings.DefineSetting(nameof(this.RadialIconOpacity), 0.5f, () => Common.settings_radial_iconOpacity);
            this.RadialIconOpacity.SetRange(0.5f, 0.75f);

            DefineEmotesRadialSettings(this.RadialMenuSettings);
        }


        private const string EMOTES_RADIAL_SETTINGS = "emotes-radial-settings";
        public SettingCollection EmotesRadialSettings { get; private set; }
        public Dictionary<Emote, SettingEntry<bool>> EmotesRadialEnabledMap { get; private set; }

        private void DefineEmotesRadialSettings(SettingCollection settings)
        {
            this.EmotesRadialSettings = settings.AddSubCollection(EMOTES_RADIAL_SETTINGS);

            this.EmotesRadialEnabledMap = new Dictionary<Emote, SettingEntry<bool>>();
        }

        public event EventHandler OnAnyEmotesRadialSettingsChanged;

        public void InitEmotesRadialEnabled(List<Emote> emotes)
        {
            this.EmotesRadialEnabledMap.Clear();
            foreach (Emote emote in emotes)
            {
                var newSetting = this.EmotesRadialSettings.DefineSetting(nameof(this.EmotesRadialEnabledMap) + "_" + emote.Id, true, () => _helper.EmotesResourceManager.GetString(emote.Id));
                this.EmotesRadialEnabledMap.Add(emote, newSetting);
                newSetting.SettingChanged += delegate
                {
                    OnAnyEmotesRadialSettingsChanged?.Invoke(this, null);
                };
            }
        }

        #endregion


        public event EventHandler<bool> OnEmotesLoaded;
        public void InitEmotesSettings(List<Emote> emotes)
        {
            this.InitEmotesShortcuts(emotes);
            this.InitEmotesRadialEnabled(emotes);
            OnEmotesLoaded?.Invoke(this, true);
        }

        public void Unload()
        {
            this.GlobalKeyBindToggleEmoteList.Value.Enabled = false;
            this.GlobalKeyBindToggleSynchronize.Value.Enabled = false;
            this.GlobalKeyBindToggleTargeting.Value.Enabled = false;
            foreach (var entry in this.EmotesShortcutsKeybindsMap)
            {
                entry.Value.Value.Enabled = false;
            }
            this.EmotesShortcutsKeybindsMap.Clear();
            this.EmotesRadialEnabledMap.Clear();
        }
    }
}
