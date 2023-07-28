﻿using Blish_HUD.Controls;
using Blish_HUD.Graphics.UI;
using Microsoft.Xna.Framework;
using System;

namespace felix.BlishEmotes.UI.Views
{
    internal class SettingsHintView : View
    {
        private Action OpenSettings;
        private StandardButton _bttnOpenSettings;

        public SettingsHintView(Action OpenSettings)
        {
            this.OpenSettings = OpenSettings;
        }

        protected override void Build(Container buildPanel)
        {
            _bttnOpenSettings = new StandardButton()
            {
                Text = "Open Settings",
                Width = 192,
                Parent = buildPanel,
            };

            _bttnOpenSettings.Location = new Point(Math.Max(buildPanel.Width / 2 - _bttnOpenSettings.Width / 2, 20), Math.Max(buildPanel.Height / 2 - _bttnOpenSettings.Height, 20) - _bttnOpenSettings.Height - 10);

            _bttnOpenSettings.Click += _bttnOpenSettings_Click;
        }

        private void _bttnOpenSettings_Click(object sender, Blish_HUD.Input.MouseEventArgs e)
        {
            this.OpenSettings();
        }

        protected override void Unload()
        {
            if (_bttnOpenSettings != null)
                _bttnOpenSettings.Click -= _bttnOpenSettings_Click;
        }
    }

}
