using Blish_HUD;
using Blish_HUD.Controls;
using Blish_HUD.Modules.Managers;
using BlishEmotesList;
using felix.BlishEmotes.Services;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime;
using System.Text;
using System.Threading.Tasks;

namespace felix.BlishEmotes.UI.Controls
{
    public class SelectionOption
    {
        public Texture2D Texture { get; protected set; }
        public string TextureRef { get; protected set; }

        public SelectionOption(Texture2D texture, string textureRef)
        {
            Texture = texture;
            TextureRef = textureRef;
        }
    }

    public class IconSelection : Control
    {
        private static readonly Logger Logger = Logger.GetLogger<IconSelection>();

        public event EventHandler<SelectionOption> Selected;

        private List<SelectionOption> _options;
        public List<SelectionOption> Options
        {
            get => _options;
            set
            {
                _options = value;
                UpdateSizeAndLocation();
            }
        }

        private Control _attachedToControl;
        public Control AttachedToControl
        {
            get => _attachedToControl;
            set
            {
                _attachedToControl = value;
                UpdateSizeAndLocation();
            }
        }

        private int _columns = 10;
        public int Columns
        { 
            get => _columns;
            set
            {
                _columns = value;
                UpdateSizeAndLocation();
            }
        }

        private int _iconSize = 30;
        public int IconSize
        {
            get => _iconSize;
            set
            {
                _iconSize = value;
                UpdateSizeAndLocation();
            }
        }

        private int _gridGap = 5;
        public int GridGap
        {
            get => _gridGap;
            set
            {
                _gridGap = value;
                UpdateSizeAndLocation();
            }
        }

        public Texture2D Background { get; set; }

        private class SelectionEntry : SelectionOption
        {
            public int X { get; set; }
            public int Y { get; set; }
            public bool Hovered { get; set; }

            public SelectionEntry(Texture2D texture, string textureRef) : base(texture, textureRef)
            {
            }
        }

        private List<SelectionEntry> _selections;

        public IconSelection(Container parent, Control attachedToControl)
        {
            Parent = parent;
            AttachedToControl = attachedToControl;
            _selections = new List<SelectionEntry>();
            ZIndex = 997;
            Visible = false;
            Background = EmotesModule.ModuleInstance.TexturesManager.GetTexture(Textures.Background);

            UpdateSizeAndLocation();

            Shown += HandleShown;
            Hidden += HandleHidden;
        }

        private void UpdateSizeAndLocation()
        {
            int requiredRows = ((Options?.Count ?? 0) / Columns) + 1;
            Point contentSize = new Point(Columns * (IconSize + GridGap), requiredRows * (IconSize + GridGap));
            Point padding = new Point((int)Padding.Left + (int)Padding.Right, (int)Padding.Top + (int)Padding.Bottom);
            Size = contentSize + padding;

            Point attachedLoc = AttachedToControl == null ? Parent.Location : LocalizedLocation(AttachedToControl, Parent);
            Point attachedSize = AttachedToControl?.Size ?? new Point(0, 0);
            int centerXOffset = Size.X / 2 - attachedSize.X / 2;
            Location = (attachedLoc + new Point(0, attachedSize.Y + 2)) - new Point(centerXOffset, 0);
        }

        private Point LocalizedLocation(Control control, Container localizedTo)
        {
            Point localized = control.Location;
            Container nextParent = control.Parent;
            while(nextParent != localizedTo)
            {
                if (nextParent == null)
                {
                    Logger.Warn("control is not a descendant of localizedTo");
                    return localized;
                }
                localized += nextParent.Location;

                nextParent = nextParent.Parent;
            }
            return localized;
        }

        private void HandleShown(object sender, EventArgs e)
        {
            Input.Mouse.LeftMouseButtonPressed += OnLeftMouseButtonPressed;
        }

        private void HandleHidden(object sender, EventArgs e)
        {
            Input.Mouse.LeftMouseButtonPressed -= OnLeftMouseButtonPressed;
        }

        private void OnLeftMouseButtonPressed(object sender, EventArgs args)
        {
            var hovered = _selections.SingleOrDefault((item) => item.Hovered);
            if (hovered != null)
            {
                Selected.Invoke(this, hovered);
            }
            Hide();
        }

        protected override void Paint(SpriteBatch spriteBatch, Rectangle bounds)
        {
            spriteBatch.DrawOnCtrl(
                 this,
                 Background,
                 bounds,
                 new Rectangle(40, 30, bounds.Width, bounds.Height),
                 Color.White);

            var newEntries = CreateSelectionEntries();
            _selections.Clear();
            _selections.AddRange(newEntries);

            PaintSelectionEntries(spriteBatch);
        }

        private List<SelectionEntry> CreateSelectionEntries()
        {
            List<SelectionEntry> newEntries = new List<SelectionEntry>();
            for (int i = 0; i < Options.Count; i++)
            {
                var emote = Options[i];

                int col = i % Columns;
                int row = i / Columns;
                int x = col * (IconSize + GridGap) + (int)Padding.Left;
                int y = row * (IconSize + GridGap) + (int)Padding.Top;

                newEntries.Add(new SelectionEntry(emote.Texture, emote.TextureRef)
                {
                    X = x,
                    Y = y,
                    Hovered = false,
                });
            }
            return newEntries;
        }

        private void PaintSelectionEntries(SpriteBatch spriteBatch)
        {
            foreach (var entry in _selections)
            {
                var entryBounds = new Rectangle(entry.X, entry.Y, IconSize, IconSize);
                entry.Hovered = entryBounds.Contains(RelativeMousePosition);

                if (entry.Hovered)
                {
                    spriteBatch.DrawOnCtrl(this, ContentService.Textures.Pixel, entryBounds, null, Color.Gray);
                }
                spriteBatch.DrawOnCtrl(this, entry.Texture, entryBounds, null, Color.White);
            }
        }

        protected override void DisposeControl()
        {
            base.DisposeControl();

            Shown -= HandleShown;
            Hidden -= HandleHidden;
        }
    }
}
