using Blish_HUD.Controls;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace felix.BlishEmotes.UI.Controls
{
    class ReorderableMenuItem : MenuItem
    {
        private bool _canDrag = true;
        private bool _dragging = false;

        public bool CanDrag
        {
            get => this._canDrag;
            internal set => this.SetProperty(ref this._canDrag, value, true);
        }

        public bool Dragging
        {
            get => this._dragging;
            internal set => this.SetProperty(ref this._dragging, value, true);
        }

        public ReorderableMenuItem() : base()
        {
        }

        public ReorderableMenuItem(string text) : base(text)
        {
        }
    }
}
