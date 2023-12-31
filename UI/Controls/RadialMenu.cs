﻿using Blish_HUD;
using Blish_HUD.Controls;
using felix.BlishEmotes.Strings;
using felix.BlishEmotes.UI.Views;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MonoGame.Extended;
using SemVer;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Resources;
using System.Threading.Tasks;
using static Blish_HUD.ArcDps.ArcDpsEnums;

namespace felix.BlishEmotes.UI.Controls
{
    // Based on https://github.com/manlaan/BlishHud-Mounts/blob/main/Controls/DrawRadial.cs by bennieboj
    internal class RadialContainer<T> where T : RadialBase
    {
        public double StartAngle { get; set; }
        public double EndAngle { get; set; }
        public T Value { get; set; }
        public Texture2D Texture { get; set; }
        public string Text { get; set; }
        public int X { get; set; }
        public int Y { get; set; }

        public bool Selected { get; set; }
        public bool Locked { get; set; }
    }

    internal class RadialMenu : Container
    {
        private static readonly Logger Logger = Logger.GetLogger<RadialMenu>();

        public event EventHandler<Emote> EmoteSelected;

        private Helper _helper;
        private ModuleSettings _settings;
        public List<Emote> Emotes { private get; set; }
        public List<Category> Categories { private get; set; }
        public bool IsEmoteSynchronized { private get; set; }
        public bool IsEmoteTargeted { private get; set; }
        private Texture2D _lockedTexture;

        private List<RadialContainer<Emote>> _radialEmotes = new List<RadialContainer<Emote>>();
        private RadialContainer<Emote> SelectedEmote => _radialEmotes.SingleOrDefault(m => m.Selected);


        private List<RadialContainer<Category>> _radialCategories = new List<RadialContainer<Category>>();
        private RadialContainer<Category> SelectedCategory => _radialCategories.SingleOrDefault(m => m.Selected);

        private bool _isActionCamToggled;

        private double _startAngle = Math.PI * Math.Floor(0.75 * 360) / 180.0; // start at 270deg (aka -90deg, aka at the top)
        private int _disabledRadius = 100;
        private int _categoryRadius = 0;
        private int _radius = 0;
        private int _iconSize = 0;
        private int _maxRadialDiameter = 0;

        private Label _noEmotesLabel;
        private Label _selectedEmoteLabel;
        private Label _synchronizeToggleActiveLabel;
        private Label _targetToggleActiveLabel;
        private Point RadialSpawnPoint = default;

        private float _debugLineThickness = 2;

        public RadialMenu(ModuleSettings settings, Texture2D LockedTexture)
        {
            this._helper = new Helper();
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

            _synchronizeToggleActiveLabel = new Label()
            {
                Parent = this,
                Visible = false,
                Location = new Point(0, 0),
                Size = new Point(200, 30),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Middle,
                Font = GameService.Content.DefaultFont14,
                Text = Common.emote_synchronizeActive,
                BackgroundColor = Color.Black * 0.3f
            };

            _targetToggleActiveLabel = new Label()
            {
                Parent = this,
                Visible = false,
                Location = new Point(0, 0),
                Size = new Point(200, 30),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Middle,
                Font = GameService.Content.DefaultFont14,
                Text = Common.emote_targetingActive,
                BackgroundColor = Color.Black * 0.3f
            };
        }

        protected override CaptureType CapturesInput()
        {
            return CaptureType.Mouse;
        }


        public override void PaintBeforeChildren(SpriteBatch spriteBatch, Rectangle bounds)
        {
            _selectedEmoteLabel.Text = "";
            if (Emotes.Count == 0)
            {
                _noEmotesLabel.Show();
                return;
            }
            else
            {
                _noEmotesLabel.Hide();

                if (IsEmoteSynchronized)
                {
                    _synchronizeToggleActiveLabel.Show();
                }
                else
                {
                    _synchronizeToggleActiveLabel.Hide();
                }

                if (IsEmoteTargeted)
                {
                    _targetToggleActiveLabel.Show();
                }
                else
                {
                    _targetToggleActiveLabel.Hide();
                }

                var emotes = Emotes;
                var emotesInnerRadius = _disabledRadius;
                if (_settings.GlobalUseCategories.Value && Categories.Count > 0)
                {
                    PaintEmoteCategories(spriteBatch, Categories);

                    emotes = SelectedCategory != null ? SelectedCategory.Value.Emotes : new List<Emote>();
                    emotesInnerRadius = _categoryRadius;
                }

                PaintEmotes(spriteBatch, emotesInnerRadius, emotes);
            }

            base.PaintBeforeChildren(spriteBatch, bounds);
        }

        private void PaintEmoteCategories(SpriteBatch spriteBatch, List<Category> categories)
        {
            if (Helper.IsDebugEnabled())
            {
                // Draw inner circle where emote selection is disabled
                spriteBatch.DrawCircle(RadialSpawnPoint.ToVector2(), _disabledRadius, 50, Color.Red, _debugLineThickness);
            }
            // Create RadialEmote wrapper for each category
            var newList = CreateRadialContainerList<Category>(_categoryRadius, (category) => SelectedCategory?.Value == category, categories);
            _radialCategories.Clear();
            _radialCategories.AddRange(newList);

            DrawRadialContainerItems(spriteBatch, _disabledRadius, _categoryRadius, _radialCategories);
        }

        private void PaintEmotes(SpriteBatch spriteBatch, int innerRadius, List<Emote> emotes)
        {
            if (Helper.IsDebugEnabled())
            {
                // Draw categories circle where emote selection is disabled
                spriteBatch.DrawCircle(RadialSpawnPoint.ToVector2(), innerRadius, 50, Color.Red, _debugLineThickness);
            }
            // Create RadialEmote wrapper for each emote
            var newList = CreateRadialContainerList<Emote>(_radius, (emote) => false, emotes);
            _radialEmotes.Clear();
            _radialEmotes.AddRange(newList);

            DrawRadialContainerItems(spriteBatch, innerRadius, int.MaxValue, _radialEmotes);
        }

        private void DrawRadialContainerItems<T>(SpriteBatch spriteBatch, int innerRadius, int outerRadius, List<RadialContainer<T>> radialContainerItems) where T : RadialBase
        {
            // Calc angle between mouse pos and radial center
            var mousePos = Input.Mouse.Position;
            var diff = mousePos - RadialSpawnPoint;
            var angle = Math.Atan2(diff.Y, diff.X);
            // Handle multiple of 2PI
            while (angle < _startAngle)
            {
                angle += Math.PI * 2;
            }
            var length = new Vector2(diff.Y, diff.X).Length();

            foreach (var item in radialContainerItems)
            {
                DrawDebugSectionSeparators(spriteBatch, innerRadius, Math.Min(_radius, outerRadius), item);

                // Only mark as selected if far enough from center away (in order to be able to close radial without selecting emote)
                if (length >= innerRadius && length <= outerRadius)
                {
                    item.Selected = item.StartAngle <= angle && item.EndAngle > angle;
                    if (item.Selected)
                    {
                        _selectedEmoteLabel.Text = item.Text;
                    }
                }
                else if (length < innerRadius)
                {
                    item.Selected = false;
                }

                // Draw emote texture
                spriteBatch.DrawOnCtrl(this, item.Texture, new Rectangle(item.X, item.Y, _iconSize, _iconSize), null, item.Locked ? Color.White * 0.25f : Color.White * (item.Selected ? 1f : _settings.RadialIconOpacity.Value));
                // Draw locked texture
                if (item.Locked)
                {
                    spriteBatch.DrawOnCtrl(this, _lockedTexture, new Rectangle(item.X, item.Y, _iconSize, _iconSize), null, Color.White * (item.Selected ? 1f : _settings.RadialIconOpacity.Value));
                }
            }
        }

        private List<RadialContainer<T>> CreateRadialContainerList<T> (int outerRadius, Func<T, bool> IsSelected, List<T> items) where T : RadialBase
        {
            // Create RadialEmote wrapper for each emote
            double currentAngle = _startAngle;
            double sweepAngle = Math.PI * 2 / items.Count; // Divide 360deg or 2PIrad between categories
            List<RadialContainer<T>> newList = new List<RadialContainer<T>>();
            foreach (var item in items)
            {
                var midAngle = currentAngle + sweepAngle / 2;
                var endAngle = currentAngle + sweepAngle;

                // Add _radius because Point(_radius) is center of radial and coordinates are top left based
                int offset = _iconSize / 2;
                int x = (int)Math.Round(_radius + (outerRadius - offset) * Math.Cos(midAngle));
                int y = (int)Math.Round(_radius + (outerRadius - offset) * Math.Sin(midAngle));

                newList.Add(new RadialContainer<T>()
                {
                    Value = item,
                    StartAngle = currentAngle,
                    EndAngle = endAngle,
                    X = x,
                    Y = y,
                    Text = item.Label,
                    Texture = item.Texture,
                    Selected = IsSelected(item),
                    Locked = item.Locked,
                });

                currentAngle = endAngle;
            }
            return newList;
        }

        private void DrawDebugSectionSeparators<T>(SpriteBatch spriteBatch, int innerRadius, int outerRadius, RadialContainer<T> item) where T : RadialBase
        {
            if (Helper.IsDebugEnabled())
            {
                // Draw debug lines separating the sections between emotes
                float xDebugInner = (float)Math.Round(innerRadius * Math.Cos(item.StartAngle)) + RadialSpawnPoint.X;
                float yDebugInner = (float)Math.Round(innerRadius * Math.Sin(item.StartAngle)) + RadialSpawnPoint.Y;
                var debugRadiusOuter = outerRadius;
                float xDebugOuter = (float)Math.Round(debugRadiusOuter * Math.Cos(item.StartAngle)) + RadialSpawnPoint.X;
                float yDebugOuter = (float)Math.Round(debugRadiusOuter * Math.Sin(item.StartAngle)) + RadialSpawnPoint.Y;
                spriteBatch.DrawLine(new Vector2(xDebugInner, yDebugInner), new Vector2(xDebugOuter, yDebugOuter), Color.Red, _debugLineThickness);
            }
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
            _categoryRadius = _disabledRadius + (int)((_radius - _disabledRadius) * 0.5);
            _disabledRadius = (int)(_radius * _settings.RadialInnerRadiusPercentage.Value);
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
            _synchronizeToggleActiveLabel.Location = new Point(_selectedEmoteLabel.Location.X, _selectedEmoteLabel.Location.Y + _selectedEmoteLabel.Size.Y);
            _targetToggleActiveLabel.Location = new Point(_synchronizeToggleActiveLabel.Location.X, _synchronizeToggleActiveLabel.Location.Y + _synchronizeToggleActiveLabel.Size.Y);
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
                Logger.Debug("Sending command for " + selected.Value.Id);
                EmoteSelected.Invoke(this, selected.Value);
            }
        }

        protected override void DisposeControl()
        {
            base.DisposeControl();
        }
    }
}
