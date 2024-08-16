using Blish_HUD;
using Blish_HUD.Controls;
using Blish_HUD.Graphics.UI;
using Blish_HUD.Settings;
using felix.BlishEmotes.Strings;
using Microsoft.Xna.Framework;
using System.Collections.Generic;
using System.Linq;

namespace felix.BlishEmotes.UI.Views
{
    internal class GlobalSettingsView : View
    {
        private ModuleSettings _settings;

        private FlowPanel _globalPanel;
        private FlowPanel _radialPanel;
        private FlowPanel _emotesRadialPanel;
        private LoadingSpinner _emotesRadialSpinner;

        private const int _labelWidth = 200;
        private const int _controlWidth = 150;
        private const int _height = 20;
        private const int _padding = 5;

        public GlobalSettingsView(ModuleSettings settings) : base()
        {
            this._settings = settings;

            _settings.OnEmotesLoaded += delegate
            {
                BuildEmotesRadialEnabledPanel();
            };
        }

        private FlowPanel CreatePanel(Container parent, Point location, int width)
        {
            return new FlowPanel()
            {
                CanCollapse = false,
                CanScroll = false,
                Parent = parent,
                Location = location,
                Width = width,
                HeightSizingMode = SizingMode.AutoSize,
                FlowDirection = ControlFlowDirection.SingleTopToBottom,
            };
        }

        private FlowPanel CreateRowPanel(Container parent)
        {
            return new FlowPanel()
            {
                CanCollapse = false,
                CanScroll = false,
                Parent = parent,
                WidthSizingMode = SizingMode.AutoSize,
                HeightSizingMode = SizingMode.AutoSize,
                FlowDirection = ControlFlowDirection.SingleLeftToRight,
                OuterControlPadding = new Vector2(_padding, _padding),
            };
        }

        protected override void Build(Container buildPanel)
        {
            // Construct panel for _settings.GlobalSettings
            _globalPanel = CreatePanel(buildPanel, new Point(0, 0), 450);
            BuildGlobalPanel();


            // Construct panel for _settings.RadialMenuSettings
            _radialPanel = CreatePanel(buildPanel, new Point(400, 0), 450);
            BuildRadialPanel();


            // Construct panel for _settings.EmotesRadialSettings
            var emotesRadialPanelLabel = new Label()
            {
                Parent = buildPanel,
                Text = Common.settings_radial_emotesEnabled,
                Location = new Point(0, 160),
                Size = new Point(170, 40),
                Font = GameService.Content.DefaultFont18,
            };
            // Init spinner while emotes are loading
            _emotesRadialSpinner = new LoadingSpinner()
            {
                Parent = buildPanel,
                Location = new Point(170, 160),
                Size = new Point(40, 40),
                Visible = true,
            };
            _emotesRadialPanel = CreatePanel(buildPanel, new Point(0, 200), 0);
            _emotesRadialPanel.HeightSizingMode = SizingMode.Fill;
            _emotesRadialPanel.WidthSizingMode = SizingMode.Fill;
            _emotesRadialPanel.FlowDirection = ControlFlowDirection.TopToBottom;
            BuildEmotesRadialEnabledPanel();
        }

        private void BuildGlobalPanel()
        {
            // GlobalHideCornerIcon
            var _hideCornerIconRow = CreateRowPanel(_globalPanel);
            Label _hideCornerIconLabel = new Label()
            {
                Parent = _hideCornerIconRow,
                Text = Common.settings_global_hideCornerIcon,
                Size = new Point(_labelWidth, _height),
                Location = new Point(0, 0),
            };
            Checkbox _hideCornerIconCheckbox = new Checkbox()
            {
                Parent = _hideCornerIconRow,
                Checked = this._settings.GlobalHideCornerIcon.Value,
                Size = new Point(_controlWidth, _height),
                Location = new Point(_labelWidth + _padding, 0),
            };
            _hideCornerIconCheckbox.CheckedChanged += delegate
            {
                this._settings.GlobalHideCornerIcon.Value = _hideCornerIconCheckbox.Checked;
            };

            // GlobalKeyBindToggleEmoteList
            var _toggleEmoteListKeybindRow = CreateRowPanel(_globalPanel);
            Label _toggleEmoteListKeybindLabel = new Label()
            {
                Parent = _toggleEmoteListKeybindRow,
                Text = Common.settings_global_keybindToggleEmoteList,
                Size = new Point(_labelWidth, _height),
                Location = new Point(0, 0),
            };
            KeybindingAssigner _toggleEmoteListKeybindAssigner = new KeybindingAssigner(this._settings.GlobalKeyBindToggleEmoteList.Value)
            {
                Parent = _toggleEmoteListKeybindRow,
                NameWidth = 0,
                Padding = Thickness.Zero,
                Size = new Point(_controlWidth, _height),
                Location = new Point(_labelWidth - 2, 0), // -2 due to hardcoded padding between Name and keybind
            };

            // GlobalKeyBindToggleSynchronize
            var _toggleSynchronizeKeybindRow = CreateRowPanel(_globalPanel);
            Label _toggleSynchronizeKeybindLabel = new Label()
            {
                Parent = _toggleSynchronizeKeybindRow,
                Text = Common.settings_global_keybindToggleSynchronize,
                Size = new Point(_labelWidth, _height),
                Location = new Point(0, 0),
            };
            KeybindingAssigner _toggleSynchronizeKeybindAssigner = new KeybindingAssigner(this._settings.GlobalKeyBindToggleSynchronize.Value)
            {
                Parent = _toggleSynchronizeKeybindRow,
                NameWidth = 0,
                Padding = Thickness.Zero,
                Size = new Point(_controlWidth, _height),
                Location = new Point(_labelWidth - 2, 0), // -2 due to hardcoded padding between Name and keybind
            };

            // GlobalKeyBindToggleTargeting
            var _toggleTargetingKeybindRow = CreateRowPanel(_globalPanel);
            Label _toggleTargetingKeybindLabel = new Label()
            {
                Parent = _toggleTargetingKeybindRow,
                Text = Common.settings_global_keybindToggleTargeting,
                Size = new Point(_labelWidth, _height),
                Location = new Point(0, 0),
            };
            KeybindingAssigner _toggleTargetingKeybindAssigner = new KeybindingAssigner(this._settings.GlobalKeyBindToggleTargeting.Value)
            {
                Parent = _toggleTargetingKeybindRow,
                NameWidth = 0,
                Padding = Thickness.Zero,
                Size = new Point(_controlWidth, _height),
                Location = new Point(_labelWidth - 2, 0), // -2 due to hardcoded padding between Name and keybind
            };

            // GlobalUseRadialMenu
            var _useRadialRow = CreateRowPanel(_globalPanel);
            Label _useRadialLabel = new Label()
            {
                Parent = _useRadialRow,
                Text = Common.settings_global_useRadialMenu,
                Size = new Point(_labelWidth, _height),
                Location = new Point(0, 0),
            };
            Dropdown _useRadialDropdown = new Dropdown()
            {
                Parent = _useRadialRow,
                Size = new Point(_controlWidth, _height),
                Location = new Point(_labelWidth + _padding, 0),
            };
            _useRadialDropdown.Items.Add("List menu");
            _useRadialDropdown.Items.Add("Radial menu");
            _useRadialDropdown.SelectedItem = this._settings.GlobalUseRadialMenu.Value ? "Radial menu" : "List menu";
            _useRadialDropdown.ValueChanged += delegate
            {
                this._settings.GlobalUseRadialMenu.Value = _useRadialDropdown.SelectedItem.Equals("Radial menu");
            };

            // GlobalUseCategories
            var _useCategoriesRow = CreateRowPanel(_globalPanel);
            Label _useCategoriesLabel = new Label()
            {
                Parent = _useCategoriesRow,
                Text = Common.settings_global_useCategories,
                Size = new Point(_labelWidth, _height),
                Location = new Point(0, 0),
            };
            Checkbox _useCategoriesCheckbox = new Checkbox()
            {
                Parent = _useCategoriesRow,
                Checked = this._settings.GlobalUseCategories.Value,
                Size = new Point(_controlWidth, _height),
                Location = new Point(_labelWidth + _padding, 0),
            };
            _useCategoriesCheckbox.CheckedChanged += delegate
            {
                this._settings.GlobalUseCategories.Value = _useCategoriesCheckbox.Checked;
            };
        }

        private void BuildRadialPanel()
        {
            // RadialSpawnAtCursor
            var _spawnAtCursorRow = CreateRowPanel(_radialPanel);
            Label _spawnAtCursorLabel = new Label()
            {
                Parent = _spawnAtCursorRow,
                Text = Common.settings_radial_spawnAtCursor,
                Size = new Point(_labelWidth, _height),
                Location = new Point(0, 0),
            };
            Checkbox _spawnAtCursorCheckbox = new Checkbox()
            {
                Parent = _spawnAtCursorRow,
                Checked = this._settings.RadialSpawnAtCursor.Value,
                Size = new Point(_controlWidth, _height),
                Location = new Point(_labelWidth + _padding, 0),
            };
            _spawnAtCursorCheckbox.CheckedChanged += delegate
            {
                this._settings.RadialSpawnAtCursor.Value = _spawnAtCursorCheckbox.Checked;
            };

            // RadialToggleActionCameraKeyBind
            var _actionCamKeybindRow = CreateRowPanel(_radialPanel);
            Label _actionCamKeybindLabel = new Label()
            {
                Parent = _actionCamKeybindRow,
                Text = Common.settings_radial_actionCamKeybind,
                Size = new Point(_labelWidth, _height),
                Location = new Point(0, 0),
            };
            KeybindingAssigner _actionCamKeybindAssigner = new KeybindingAssigner(this._settings.RadialToggleActionCameraKeyBind.Value)
            {
                Parent = _actionCamKeybindRow,
                NameWidth = 0,
                Padding = Thickness.Zero,
                Size = new Point(_controlWidth, _height),
                Location = new Point(_labelWidth - 2, 0), // -2 due to hardcoded padding between Name and keybind
            };

            // RadialRadiusModifier
            var _radiusModifierRow = CreateRowPanel(_radialPanel);
            Label _radiusModifierLabel = new Label()
            {
                Parent = _radiusModifierRow,
                Text = Common.settings_radial_radiusModifier,
                Size = new Point(_labelWidth, _height),
                Location = new Point(0, 0),
            };
            TrackBar _radiusModifierTrackBar = new TrackBar()
            {
                Parent = _radiusModifierRow,
                Value = this._settings.RadialRadiusModifier.Value * 100.0f,
                MinValue = 25,
                MaxValue = 50,
                Size = new Point(_controlWidth, _height),
                Location = new Point(_labelWidth + _padding, 0),
            };
            _radiusModifierTrackBar.ValueChanged += delegate { this._settings.RadialRadiusModifier.Value = _radiusModifierTrackBar.Value / 100.0f; };

            // RadialInnerRadiusPercentage
            var _innerRadiusPercentageRow = CreateRowPanel(_radialPanel);
            Label _innerRadiusPercentageLabel = new Label()
            {
                Parent = _innerRadiusPercentageRow,
                Text = Common.settings_radial_innerRadiusPercentage,
                Size = new Point(_labelWidth, _height),
                Location = new Point(0, 0),
                BasicTooltipText = Common.settings_radial_innerRadiusPercentage_description,
            };
            TrackBar _innerRadiusPercentageTrackBar = new TrackBar()
            {
                Parent = _innerRadiusPercentageRow,
                Value = this._settings.RadialInnerRadiusPercentage.Value * 100.0f,
                MinValue = 0,
                MaxValue = 50,
                Size = new Point(_controlWidth, _height),
                Location = new Point(_labelWidth + _padding, 0),
                BasicTooltipText = Common.settings_radial_innerRadiusPercentage_description,
            };
            _innerRadiusPercentageTrackBar.ValueChanged += delegate { this._settings.RadialInnerRadiusPercentage.Value = _innerRadiusPercentageTrackBar.Value / 100.0f; };

            // RadialIconSizeModifier
            var _iconSizeModifierRow = CreateRowPanel(_radialPanel);
            Label _iconSizeModifierLabel = new Label()
            {
                Parent = _iconSizeModifierRow,
                Text = Common.settings_radial_iconSizeModifier,
                Size = new Point(_labelWidth, _height),
                Location = new Point(0, 0),
            };
            TrackBar _iconSizeModifierTrackBar = new TrackBar()
            {
                Parent = _iconSizeModifierRow,
                Value = this._settings.RadialIconSizeModifier.Value * 100.0f,
                MinValue = 25,
                MaxValue = 75,
                Size = new Point(_controlWidth, _height),
                Location = new Point(_labelWidth + _padding, 0),
            };
            _iconSizeModifierTrackBar.ValueChanged += delegate { this._settings.RadialIconSizeModifier.Value = _iconSizeModifierTrackBar.Value / 100.0f; };

            // RadialIconOpacity
            var _iconOpacityRow = CreateRowPanel(_radialPanel);
            Label _iconOpacityLabel = new Label()
            {
                Parent = _iconOpacityRow,
                Text = Common.settings_radial_iconOpacity,
                Size = new Point(_labelWidth, _height),
                Location = new Point(0, 0),
            };
            TrackBar _iconOpacityTrackBar = new TrackBar()
            {
                Parent = _iconOpacityRow,
                Value = this._settings.RadialIconOpacity.Value * 100.0f,
                MinValue = 50,
                MaxValue = 75,
                Size = new Point(_controlWidth, _height),
                Location = new Point(_labelWidth + _padding, 0),
            };
            _iconOpacityTrackBar.ValueChanged += delegate { this._settings.RadialIconOpacity.Value = _iconOpacityTrackBar.Value / 100.0f; };
        }

        public void BuildEmotesRadialEnabledPanel()
        {
            // Clear current children
            _emotesRadialPanel?.ClearChildren();

            // Dispose spinner
            if (this._settings.EmotesRadialEnabledMap.Count() > 0)
            {
                _emotesRadialSpinner?.Dispose();
            }

            // Sort by DisplayName
            List<KeyValuePair<Emote, SettingEntry<bool>>> sorted = this._settings.EmotesRadialEnabledMap.ToList();
            sorted.Sort((a, b) => a.Value.DisplayName.CompareTo(b.Value.DisplayName));
            // Create Checkbox control for each entry
            foreach (var entry in sorted)
            {
                var _emoteRadialEnabledRow = CreateRowPanel(_emotesRadialPanel);
                _emoteRadialEnabledRow.OuterControlPadding = new Vector2(10, _padding);
                Label _emoteRadialEnableLabel = new Label()
                {
                    Parent = _emoteRadialEnabledRow,
                    Text = entry.Value.DisplayName,
                    Size = new Point(100, _height),
                    Location = new Point(0, 0),
                };
                Checkbox _emoteRadialEnableCheckbox = new Checkbox()
                {
                    Parent = _emoteRadialEnabledRow,
                    Checked = entry.Value.Value,
                    Size = new Point(_controlWidth, _height),
                    Location = new Point(_labelWidth + _padding, 0),
                };
                _emoteRadialEnableCheckbox.CheckedChanged += delegate
                {
                    entry.Value.Value = _emoteRadialEnableCheckbox.Checked;
                };
            }
        }

        protected override void Unload()
        {
            this._globalPanel?.Dispose();
            this._radialPanel?.Dispose();
            this._emotesRadialPanel?.Dispose();
            this._emotesRadialSpinner?.Dispose();
        }
    }
}
