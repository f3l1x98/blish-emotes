using Blish_HUD.Controls;
using Blish_HUD.Graphics.UI;
using Microsoft.Xna.Framework;

namespace felix.BlishEmotes.UI.Views
{
    internal class SettingsWindowView : View
    {
        public ViewContainer GlobalSettingsViewContainer { get; private set; }
        public ViewContainer EmotesShortcutsSettingsViewContainer { get; private set; }

        public SettingsWindowView(ModuleSettings settings) : base()
        {
            this.WithPresenter(new SettingsWindowPresenter(this, settings));
        }
        protected override void Build(Container buildPanel)
        {
            EmotesShortcutsSettingsViewContainer = new ViewContainer()
            {
                Parent = buildPanel,
                Width = 350,
                Location = new Point(0, 0),
                HeightSizingMode = SizingMode.AutoSize,
            };

            GlobalSettingsViewContainer = new ViewContainer()
            {
                Parent = buildPanel,
                Location = new Point(350, 0),
                Width = 350,
                HeightSizingMode = SizingMode.AutoSize,
            };
        }
    }
}
