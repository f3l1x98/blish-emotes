using Blish_HUD.Controls;
using Blish_HUD.Graphics.UI;
using Blish_HUD.Input;
using Blish_HUD.Settings;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace felix.BlishEmotes.UI.Views
{
    internal class EmoteHotkeySettingsView : View
    {
        private FlowPanel FlowPanel;
        private LoadingSpinner Spinner;

        public EmoteHotkeySettingsView(ModuleSettings settings) : base()
        {
            this.WithPresenter(new EmoteHotkeySettingsPresenter(this, settings));
        }
        protected override void Build(Container buildPanel)
        {
            // Init root panel
            FlowPanel = new FlowPanel()
            {
                Parent = buildPanel,
                FlowDirection = ControlFlowDirection.TopToBottom,
                HeightSizingMode = SizingMode.Fill,
                WidthSizingMode = SizingMode.Fill,
                CanScroll = false,
            };
            // Init spinner while emotes are loading
            Spinner = new LoadingSpinner()
            {
                Parent = buildPanel,
                Location = new Point(buildPanel.Width / 2, buildPanel.Height / 2),
                Visible = true,
            };
        }

        public void BuildEmoteHotkeyPanel(SettingCollection EmotesShortcutsSettings)
        {
            // Clear current children
            FlowPanel?.ClearChildren();

            // Hide spinner
            if (EmotesShortcutsSettings.Count() > 0)
            {
                Spinner.Hide();
            }

            const int labelWidth = 100;
            const int keyAssignerWidth = 100;
            const int height = 20;
            const int padding = 5;

            // Sort SettingEntries by DisplayName
            List<SettingEntry> sorted = new List<SettingEntry>(EmotesShortcutsSettings.Entries);
            sorted.Sort((a, b) => a.DisplayName.CompareTo(b.DisplayName));
            // Create KeybindingAssigner control for each entry
            foreach (SettingEntry<KeyBinding> entry in sorted)
            {
                var containerPanel = new FlowPanel()
                {
                    Parent = FlowPanel,
                    FlowDirection = ControlFlowDirection.SingleLeftToRight,
                    WidthSizingMode = SizingMode.AutoSize,
                    HeightSizingMode = SizingMode.AutoSize,
                    OuterControlPadding = new Vector2(padding, padding),
                };
                var keyAssignerLabel = new Label()
                {
                    Parent = containerPanel,
                    Text = entry.DisplayName,
                    Size = new Point(labelWidth, height),
                };
                var keyAssigner = new KeybindingAssigner(entry.Value)
                {
                    Parent = containerPanel,
                    NameWidth = 0,
                    Size = new Point(keyAssignerWidth, height),
                    Location = new Point(labelWidth + padding, 0),
                };
            }
        }

        protected override void Unload()
        {
            this.FlowPanel?.Dispose();
            this.Spinner?.Dispose();
        }
    }
}
