using Blish_HUD.Controls;
using Blish_HUD.Graphics.UI;
using Blish_HUD.Settings.UI.Views;
using Microsoft.Xna.Framework;

namespace felix.BlishEmotes.UI.Views
{
    internal class SettingsWindowView : View
    {
        private ModuleSettings _settings;

        private ViewContainer _globalSettingsViewContainer;
        private ViewContainer _emotesShortcutsSettingsViewContainer;

        private SettingsView _globalSettingsView;
        private SettingsView _emotesShortcutsSettingsView;

        public SettingsWindowView(ModuleSettings settings) : base()
        {
            _settings = settings;
        }
        protected override void Build(Container buildPanel)
        {
            _emotesShortcutsSettingsViewContainer = new ViewContainer()
            {
                Parent = buildPanel,
                Width = 350,
                Location = new Point(0, 0),
                HeightSizingMode = SizingMode.AutoSize,
            };

            _globalSettingsViewContainer = new ViewContainer()
            {
                Parent = buildPanel,
                Location = new Point(350, 0),
                Width = 350,
                HeightSizingMode = SizingMode.AutoSize,
            };

            DoUpdate();
        }

        public void DoUpdate()
        {
            if (_globalSettingsView != null)
            {
                _globalSettingsView.DoUnload();
            }
            if (_emotesShortcutsSettingsView != null)
            {
                _globalSettingsView.DoUnload();
            }
            _globalSettingsView = new SettingsView(_settings.GlobalSettings);
            _emotesShortcutsSettingsView = new SettingsView(_settings.EmotesShortcutsSettings);
            _globalSettingsViewContainer.Show(_globalSettingsView);
            _emotesShortcutsSettingsViewContainer.Show(_emotesShortcutsSettingsView);
        }
    }
}
