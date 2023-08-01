using Blish_HUD.Graphics.UI;
using felix.BlishEmotes.UI.Views;
using System;
using System.Threading.Tasks;

namespace felix.BlishEmotes.UI.Presenters
{
    internal class DummySettingsPresenter : Presenter<DummySettingsView, Action>
    {
        public DummySettingsPresenter(DummySettingsView view, Action model) : base(view, model) { }

        protected override Task<bool> Load(IProgress<string> progress)
        {
            this.View.OpenSettingsClicked += View_OpenSettingsClicked;

            return base.Load(progress);
        }

        private void View_OpenSettingsClicked(object sender, EventArgs e)
        {
            this.Model.Invoke();
        }

        protected override void Unload()
        {
            this.View.OpenSettingsClicked -= View_OpenSettingsClicked;
        }
    }
}
