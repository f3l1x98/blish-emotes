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
    // Based on https://github.com/manlaan/BlishHud-Mounts/blob/main/Controls/DrawRadial.cs by bennieboj
    internal class RadialEmote
    {
        public double StartAngle { get; set; }
        public double EndAngle { get; set; }
        public Emote Emote { get; set; }
        public Texture2D Texture { get; set; }
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
        public List<Emote> Emotes { private get; set; }
        private Texture2D _lockedTexture;

        private List<RadialEmote> _radialEmotes = new List<RadialEmote>();
        private RadialEmote SelectedEmote => _radialEmotes.SingleOrDefault(m => m.Selected);

        private bool _isActionCamToggled;

        private int _innerRadius = 100;
        private int _radius = 0;
        private int _iconSize = 0;
        private int _maxRadialDiameter = 0;

        private Label _noEmotesLabel;
        private Label _selectedEmoteLabel;
        private Point RadialSpawnPoint = default;

        private float _debugLineThickness = 2;

        public RadialMenu(Helper helper, ModuleSettings settings, Texture2D LockedTexture)
        {
            this._helper = helper;
            this._settings = settings;
            this.Emotes = new List<Emote>();
            this._lockedTexture = LockedTexture;
            Visible = false;
            Padding = Blish_HUD.Controls.Thickness.Zero;
            Shown += async (sender, e) => await HandleShown(sender, e);
            Hidden += async (sender, e) => await HandleHidden(sender, e);

            _noEmotesLabel = new Label()
            {
                Parent = this,
                Location = new Point(0, 0),
                Size = new Point(800, 500),
                Font = GameService.Content.DefaultFont32,
                Text = "No Emotes found!",
                TextColor = Color.Red,
            };

            _selectedEmoteLabel = new Label()
            {
                Parent = this,
                Location = new Point(0, 0),
                Size = new Point(200, 50),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Middle,
                Font = GameService.Content.DefaultFont32,
                Text = "",
                BackgroundColor = Color.Black * 0.5f
            };
        }

        protected override CaptureType CapturesInput()
        {
            return CaptureType.Mouse;
        }


        public override void PaintBeforeChildren(SpriteBatch spriteBatch, Rectangle bounds)
        {
            _radialEmotes.Clear();
            _selectedEmoteLabel.Text = "";
            if (Emotes.Count == 0)
            {
                _noEmotesLabel.Show();
                return;
            }
            else
            {
                _noEmotesLabel.Hide();
            }

            // Create RadialEmote wrapper for each emote
            double startAngle = Math.PI * Math.Floor(0.75 * 360) / 180.0; // start at 270deg (aka -90deg, aka at the top)
            if (Helper.IsDebugEnabled())
            {
                // Draw inner circle where emote selection is disabled
                spriteBatch.DrawCircle(RadialSpawnPoint.ToVector2(), _innerRadius, 50, Color.Red, _debugLineThickness);
            }
            double currentAngle = startAngle;
            double sweepAngle = Math.PI * 2 / Emotes.Count; // Divide 360deg or 2PIrad between emotes
            foreach (var emote in Emotes)
            {
                var midAngle = currentAngle + sweepAngle / 2;
                var endAngle = currentAngle + sweepAngle;

                int x = (int)Math.Round(_radius + _radius * Math.Cos(midAngle));
                int y = (int)Math.Round(_radius + _radius * Math.Sin(midAngle));

                if (Helper.IsDebugEnabled())
                {
                    // Draw debug lines separating the sections between emotes
                    float xDebugInner = (float)Math.Round(_innerRadius * Math.Cos(currentAngle)) + RadialSpawnPoint.X;
                    float yDebugInner = (float)Math.Round(_innerRadius * Math.Sin(currentAngle)) + RadialSpawnPoint.Y;
                    var debugRadiusOuter = 250;
                    float xDebugOuter = (float)Math.Round(2 * debugRadiusOuter * Math.Cos(currentAngle)) + RadialSpawnPoint.X;
                    float yDebugOuter = (float)Math.Round(2 * debugRadiusOuter * Math.Sin(currentAngle)) + RadialSpawnPoint.Y;
                    spriteBatch.DrawLine(new Vector2(xDebugInner, yDebugInner), new Vector2(xDebugOuter, yDebugOuter), Color.Red, _debugLineThickness);
                }

                _radialEmotes.Add(new RadialEmote()
                {
                    Emote = emote,
                    StartAngle = currentAngle,
                    EndAngle = endAngle,
                    X = x,
                    Y = y,
                    Text = _helper.EmotesResourceManager.GetString(emote.Id),
                    Texture = emote.Texture,
                });

                currentAngle = endAngle;
            }

            // Calc angle between mouse pos and radial center
            var mousePos = Input.Mouse.Position;
            var diff = mousePos - RadialSpawnPoint;
            var angle = Math.Atan2(diff.Y, diff.X);
            // Handle multiple of 2PI
            while (angle < startAngle)
            {
                angle += Math.PI * 2;
            }

            var length = new Vector2(diff.Y, diff.X).Length();

            // Draw each RadialEmote and mark selected
            foreach (var radialEmote in _radialEmotes)
            {
                // Only mark as selected if far enough from center away (in order to be able to close radial without selecting emote)
                if (length >= _innerRadius)
                {
                    // Mark as selected
                    radialEmote.Selected = radialEmote.StartAngle <= angle && radialEmote.EndAngle > angle;
                    if (radialEmote.Selected)
                    {
                        _selectedEmoteLabel.Text = radialEmote.Text;
                    }
                }

                // Draw emote texture
                spriteBatch.DrawOnCtrl(this, radialEmote.Texture, new Rectangle(radialEmote.X, radialEmote.Y, _iconSize, _iconSize), null, radialEmote.Emote.Locked ? Color.White * 0.25f : Color.White * (radialEmote.Selected ? 1f : _settings.RadialIconOpacity.Value));
                // Draw locked texture
                if (radialEmote.Emote.Locked)
                {
                    spriteBatch.DrawOnCtrl(this, _lockedTexture, new Rectangle(radialEmote.X, radialEmote.Y, _iconSize, _iconSize), null, Color.White * (radialEmote.Selected ? 1f : _settings.RadialIconOpacity.Value));
                }
            }

            base.PaintBeforeChildren(spriteBatch, bounds);
        }


        private async Task HandleShown(object sender, EventArgs e)
        {
            Logger.Debug("HandleShown entered");
            if (!GameService.Input.Mouse.CursorIsVisible && !_settings.RadialToggleActionCameraKeyBind.IsNull)
            {
                _isActionCamToggled = true;
                await _helper.TriggerKeybind(_settings.RadialToggleActionCameraKeyBind);
                Logger.Debug("HandleShown turned off action cam");
            }

            // Calc max radial menu radius/size
            _maxRadialDiameter = Math.Min(GameService.Graphics.SpriteScreen.Width, GameService.Graphics.SpriteScreen.Height);
            _iconSize = (int)(_maxRadialDiameter / 8 * _settings.RadialIconSizeModifier.Value);
            _radius = (int)((_maxRadialDiameter * (3.0 / 4.0) - _iconSize / 2) * _settings.RadialRadiusModifier.Value);
            _innerRadius = (int)(_radius * _settings.RadialInnerRadiusPercentage.Value);
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

            // Set Location of selected emote label to roughly center of radial menu
            _selectedEmoteLabel.Location = new Point(RadialSpawnPoint.X - this.Location.X - _selectedEmoteLabel.Size.X / 2, RadialSpawnPoint.Y - this.Location.Y - _selectedEmoteLabel.Size.Y / 2 - 20);
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
            // send emote command
            var selected = SelectedEmote;
            if (selected != null)
            {
                Logger.Debug("Sending command for " + selected.Emote.Id);
                _helper.SendEmoteCommand(selected.Emote);
            }
        }
    }
}
