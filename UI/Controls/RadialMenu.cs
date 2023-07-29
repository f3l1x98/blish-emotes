using Blish_HUD;
using Blish_HUD.Controls;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MonoGame.Extended;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace felix.BlishEmotes.UI.Controls
{
    // Heavily "inspired" by https://github.com/manlaan/BlishHud-Mounts/blob/main/Controls/DrawRadial.cs
    internal class RadialEmote
    {
        public double StartAngle { get; set; }
        public double EndAngle { get; set; }
        public Emote Emote { get; set; }
        // TODO FOR LATER
        public Texture2D Texture { get; set; }
        // FOR NOW
        public string Text { get; set; }
        public int X { get; set; }
        public int Y { get; set; }

        public bool Selected { get; set; }
    }

    internal class RadialMenu : Container
    {
        private static readonly Logger Logger = Logger.GetLogger<RadialMenu>();

        private Helper _helper;
        private ModuleSettings _settings;
        private List<Emote> _emotes;

        private List<RadialEmote> _radialEmotes = new List<RadialEmote>();
        private RadialEmote SelectedEmote => _radialEmotes.Single(m => m.Selected);

        private bool _isActionCamToggled;

        private int _radius = 0;
        private int _iconSize = 0;
        private int _maxRadialDiameter = 0;

        private Label _noEmotesLabel;
        private Point RadialSpawnPoint = default;
        private float _debugLineThickness = 2;

        public RadialMenu(Helper helper, ModuleSettings settings, List<Emote> emotes)
        {
            this._helper = helper;
            this._settings = settings;
            this._emotes = emotes;
            Visible = false;
            Padding = Blish_HUD.Controls.Thickness.Zero;
            Shown += async (sender, e) => await HandleShown(sender, e);
            Hidden += async (sender, e) => await HandleHidden(sender, e);

            _noEmotesLabel = new Label()
            {
                Parent = this,
                Location = new Point(0, 0),
                Font = GameService.Content.DefaultFont32,
                Text = "No Emotes found!",
                TextColor = Color.Red,
            };
        }

        protected override CaptureType CapturesInput()
        {
            return CaptureType.Mouse;
        }


        public override void PaintBeforeChildren(SpriteBatch spriteBatch, Rectangle bounds)
        {
            _radialEmotes.Clear();
            if (_emotes.Count == 0)
            {
                _noEmotesLabel.Show();
                return;
            }
            else
            {
                _noEmotesLabel.Hide();
            }

            // TODO DEBUG HELPER LIKE RADIAL MOUNT
            float r = (float)(_iconSize * Math.Sqrt(2) / 2);
            if (Helper.IsDebugEnabled())
            {
                // Draw debug lines 
                var spawnPointVec = RadialSpawnPoint.ToVector2();
                var rectpos = spawnPointVec - new Vector2(_iconSize / 2, _iconSize / 2);
                spriteBatch.DrawRectangle(rectpos, new Size2(_iconSize, _iconSize), Color.Red, _debugLineThickness);
                spriteBatch.DrawCircle(spawnPointVec, 1, 50, Color.Red, _debugLineThickness);
                spriteBatch.DrawCircle(spawnPointVec, r, 50, Color.Red, _debugLineThickness);
            }
            double currentAngle = 0.0;
            double sweepAngle = Math.PI * 2 / _emotes.Count; // Divide 360deg or 2PIrad between emotes

            // Create RadialEmote wrapper for each emote
            foreach (var emote in _emotes)
            {
                var angleMid = currentAngle + sweepAngle / 2;
                var angleEnd = currentAngle + sweepAngle;

                int x = (int)Math.Round(_radius + _radius * Math.Cos(angleMid));
                int y = (int)Math.Round(_radius + _radius * Math.Sin(angleMid));

                if (Helper.IsDebugEnabled())
                {
                    // Draw debug lines separating the sections between emotes
                    float xDebugInner = (float)Math.Round(r * Math.Cos(currentAngle)) + RadialSpawnPoint.X;
                    float yDebugInner = (float)Math.Round(r * Math.Sin(currentAngle)) + RadialSpawnPoint.Y;
                    var debugRadiusOuter = 250;
                    float xDebugOuter = (float)Math.Round(2 * debugRadiusOuter * Math.Cos(currentAngle)) + RadialSpawnPoint.X;
                    float yDebugOuter = (float)Math.Round(2 * debugRadiusOuter * Math.Sin(currentAngle)) + RadialSpawnPoint.Y;
                    spriteBatch.DrawLine(new Vector2(xDebugInner, yDebugInner), new Vector2(xDebugOuter, yDebugOuter), Color.Red, _debugLineThickness);
                }

                _radialEmotes.Add(new RadialEmote()
                {
                    Emote = emote,
                    StartAngle = currentAngle,
                    EndAngle = angleEnd,
                    X = x,
                    Y = y,
                    Text = _helper.EmotesResourceManager.GetString(emote.Id),
                    //Texture = _textureCache.GetMountImgFile(mount),
                });

                currentAngle = angleEnd;
            }

            // Calc angle between mouse pos and radial center
            var mousePos = Input.Mouse.Position;
            var diff = mousePos - RadialSpawnPoint;
            var angle = Math.Atan2(diff.Y, diff.X);
            // Ensure positive angle
            while (angle < 0.0)
            {
                angle += Math.PI * 2;
            }

            // Draw each RadialEmote and mark selected
            foreach (var radialEmote in _radialEmotes)
            {
                // Mark as selected
                radialEmote.Selected = radialEmote.StartAngle <= angle && radialEmote.EndAngle > angle;

                // TODO SWITCH TO DrawOnCtrl TO DRAW TEXTURE 
                //spriteBatch.DrawOnCtrl(this, radialEmote.Texture, new Rectangle(radialEmote.X, radialEmote.Y, _iconSize, _iconSize), null, radialEmote.Emote.Locked ? Color.Gray : Color.White * (radialEmote.Selected ? 1f : _settings.RadialIconOpacity.Value));
                spriteBatch.DrawStringOnCtrl(this, radialEmote.Text.Substring(0, 1), GameService.Content.DefaultFont32, new Rectangle(radialEmote.X, radialEmote.Y, _iconSize, _iconSize), radialEmote.Emote.Locked ? Color.Red : Color.White * (radialEmote.Selected ? 1f : _settings.RadialIconOpacity.Value), false, HorizontalAlignment.Center, VerticalAlignment.Middle);

            }

            base.PaintBeforeChildren(spriteBatch, bounds);
        }


        private async Task HandleShown(object sender, EventArgs e)
        {
            Logger.Debug("HandleShown entered");
            if (!GameService.Input.Mouse.CursorIsVisible && !_settings.RadialToggleActionCameraKeyBind.IsNull) // IsNull check is wrong -> check if PrimaryKey None
            {
                _isActionCamToggled = true;
                await _helper.TriggerKeybind(_settings.RadialToggleActionCameraKeyBind);
                Logger.Debug("HandleShown turned off action cam");
            }

            // Calc max radial menu radius/size
            _maxRadialDiameter = Math.Min(GameService.Graphics.SpriteScreen.Width, GameService.Graphics.SpriteScreen.Height);
            _iconSize = (int)(_maxRadialDiameter / 4 * _settings.RadialIconSizeModifier.Value);
            // TODO perhaps adjust radius due to more sections than mounts
            _radius = (int)((_maxRadialDiameter / 2 - _iconSize / 2) * _settings.RadialRadiusModifier.Value);
            Size = new Point(_maxRadialDiameter, _maxRadialDiameter);

            // Set spawn point
            if (_settings.RadialSpawnAtCursor.Value)
            {
                RadialSpawnPoint = Input.Mouse.Position;
            }
            else
            {
                // Move cursor to center of screen
                Blish_HUD.Controls.Intern.Mouse.SetPosition(GameService.Graphics.WindowWidth / 2, GameService.Graphics.WindowHeight / 2, true);
                RadialSpawnPoint = new Point(GameService.Graphics.SpriteScreen.Width / 2, GameService.Graphics.SpriteScreen.Height / 2);
            }

            // Set location of this control using calculated spawn point (Location is upper left corner not center, aka RadialSpawnPoint)
            Location = new Point(RadialSpawnPoint.X - _radius - _iconSize / 2, RadialSpawnPoint.Y - _radius - _iconSize / 2);
        }

        private async Task HandleHidden(object sender, EventArgs e)
        {
            Logger.Debug("HandleHidden entered");
            if (_isActionCamToggled)
            {
                await _helper.TriggerKeybind(_settings.RadialToggleActionCameraKeyBind);
                _isActionCamToggled = false;
                Logger.Debug("HandleHidden turned back on action cam");
            }
            // TODO send emote
            var selected = SelectedEmote;
            if (selected != null)
            {
                //_helper.SendEmoteCommand(selected.Emote);
                Logger.Debug("SENDING COMMAND FOR: " + selected.Emote.Id);
            }
        }
    }
}
