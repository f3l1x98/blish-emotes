using Blish_HUD;
using Blish_HUD.Controls;
using Blish_HUD.Graphics.UI;
using Blish_HUD.Settings.UI.Views;
using felix.BlishEmotes.Strings;
using Microsoft.Xna.Framework;
using System;
using System.Threading.Tasks;

namespace felix.BlishEmotes.UI.Views
{
    internal class GlobalSettingsView : View
    {
        private ModuleSettings _settings;

        private FlowPanel _globalPanel;
        private FlowPanel _radialPanel;

        private const int _labelWidth = 200;
        private const int _controlWidth = 150;
        private const int _height = 20;
        private const int _padding = 5;

        public GlobalSettingsView(ModuleSettings settings) : base()
        {
            this._settings = settings;
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
            _hideCornerIconCheckbox.CheckedChanged += delegate {
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

            // GlobalUseRadialMenu
            // -> PERHAPS TRY DROPDOWN (not necessary, but for the looks?!?)
            var _useRadialRow = CreateRowPanel(_globalPanel);
            Label _useRadialLabel = new Label()
            {
                Parent = _useRadialRow,
                Text = Common.settings_global_useRadialMenu,
                Size = new Point(_labelWidth, _height),
                Location = new Point(0, 0),
            };
            // TODO THIS IS NOT DRAWN
            Dropdown _useRadialDropdown = new Dropdown()
            {
                Parent = _useRadialRow,
                Size = new Point(_controlWidth, _height),
                Location = new Point(_labelWidth + _padding, 0),
            };
            _useRadialDropdown.Items.Add("List menu");
            _useRadialDropdown.Items.Add("Radial menu");
            _useRadialDropdown.SelectedItem = this._settings.GlobalUseRadialMenu.Value ? "Radial menu" : "List menu";
            _useRadialDropdown.ValueChanged += delegate {
                this._settings.GlobalUseRadialMenu.Value = _useRadialDropdown.SelectedItem.Equals("Radial menu");
            };

            // GlobalUseCategories
            // -> PERHAPS ONLY DISPLAY IF !GlobalUseRadialMenu
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
            _useCategoriesCheckbox.CheckedChanged += delegate {
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
            _spawnAtCursorCheckbox.CheckedChanged += delegate {
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
                MinValue = 50,
                MaxValue = 100,
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
                MaxValue = 100,
                Size = new Point(_controlWidth, _height),
                Location = new Point(_labelWidth + _padding, 0),
            };
            _iconOpacityTrackBar.ValueChanged += delegate { this._settings.RadialIconOpacity.Value = _iconOpacityTrackBar.Value / 100.0f; };
        }

        protected override void Unload()
        {
            this._globalPanel?.Dispose();
            this._radialPanel?.Dispose();
        }
    }
}
