using Blish_HUD.Graphics.UI;
using Blish_HUD.Settings.UI.Views;
using felix.BlishEmotes.UI.Views;
using System;
using System.Threading.Tasks;

namespace felix.BlishEmotes.UI
{
    internal class SettingsWindowPresenter : Presenter<SettingsWindowView, ModuleSettings>
    {
        public SettingsWindowPresenter(SettingsWindowView view, ModuleSettings model) : base(view, model)
        {
        }
        protected override Task<bool> Load(IProgress<string> progress)
        {
            this.Model.OnEmotesLoaded += Model_EmotesSettingsLoaded;

            return base.Load(progress);
        }

        protected override void UpdateView()
        {
            this.View.GlobalSettingsViewContainer.Show(new SettingsView(this.Model.GlobalSettings));
            this.View.EmotesShortcutsSettingsViewContainer.Show(new SettingsView(this.Model.EmotesShortcutsSettings));
            this.View.RadialMenuSettingsViewContainer.Show(new SettingsView(this.Model.RadialMenuSettings));
        }

        private void Model_EmotesSettingsLoaded(object sender, bool e)
        {
            this.UpdateView();
        }

        protected override void Unload()
        {
            this.Model.OnEmotesLoaded -= Model_EmotesSettingsLoaded;
        }
    }
}
