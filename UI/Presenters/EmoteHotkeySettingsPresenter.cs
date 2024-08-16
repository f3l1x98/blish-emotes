using Blish_HUD.Graphics.UI;
using felix.BlishEmotes.UI.Views;
using System;
using System.Threading.Tasks;

namespace felix.BlishEmotes.UI
{
    internal class EmoteHotkeySettingsPresenter : Presenter<EmoteHotkeySettingsView, ModuleSettings>
    {
        public EmoteHotkeySettingsPresenter(EmoteHotkeySettingsView view, ModuleSettings model) : base(view, model)
        {
        }
        protected override Task<bool> Load(IProgress<string> progress)
        {
            this.Model.OnEmotesLoaded += Model_EmotesSettingsLoaded;

            return base.Load(progress);
        }

        protected override void UpdateView()
        {
            this.View.BuildEmoteHotkeyPanel(this.Model.EmotesShortcutsSettings);
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
