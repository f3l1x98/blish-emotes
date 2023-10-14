using Blish_HUD;
using Blish_HUD.Controls;
using Blish_HUD.Input;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using System.Linq;

namespace felix.BlishEmotes.UI.Controls
{
    class ReorderableMenu : Menu
    {
        public event EventHandler<List<ReorderableMenuItem>> Reordered;

        private ReorderableMenuItem DraggedChild => Children.FirstOrDefault(child => child is ReorderableMenuItem entry && entry.Dragging) as ReorderableMenuItem;
        private List<ReorderableMenuItem> ReorderableChildren => Children.Where(child => child is ReorderableMenuItem).Cast<ReorderableMenuItem>().ToList();

        protected override void OnChildAdded(ChildChangedEventArgs e)
        {
            if (e.ChangedChild is ReorderableMenuItem)
            {
                e.ChangedChild.LeftMouseButtonPressed += this.ChangedChild_LeftMouseButtonPressed;
                e.ChangedChild.LeftMouseButtonReleased += this.ChangedChild_LeftMouseButtonReleased;

                base.OnChildAdded(e);
            }
            else
            {
                e.Cancel = true;
            }
        }

        protected override void OnChildRemoved(ChildChangedEventArgs e)
        {
            e.ChangedChild.LeftMouseButtonPressed -= this.ChangedChild_LeftMouseButtonPressed;
            e.ChangedChild.LeftMouseButtonReleased -= this.ChangedChild_LeftMouseButtonReleased;

            base.OnChildRemoved(e);
        }

        private void ChangedChild_LeftMouseButtonReleased(object sender, MouseEventArgs e)
        {
            var draggedEntry = DraggedChild;
            if (draggedEntry == null)
            {
                return;
            }

            if (sender is ReorderableMenuItem entry)
            {
                // Check if we have to move one last time
                if (sender != draggedEntry)
                {
                    // move
                    int draggedOnIndex = this.Children.IndexOf(entry);
                    if (draggedOnIndex >= 0)
                    {
                        DragItem(draggedEntry, draggedOnIndex);

                        Reordered?.Invoke(this, this.ReorderableChildren);
                    }
                }
                draggedEntry.Dragging = false;
            }
        }

        private void ChangedChild_LeftMouseButtonPressed(object sender, MouseEventArgs e)
        {
            if (DraggedChild != null)
            {
                // Ignore because already dragging a child
                return;
            }
            if (sender is ReorderableMenuItem entry)
            {
                if (entry.CanDrag)
                {
                    entry.Dragging = true;

                    // Subscribe to global LefMouseButtonReleased in order to catch cancel
                    GameService.Input.Mouse.LeftMouseButtonReleased += Game_OnLeftMouseButtonReleased;
                }
            }
        }

        private int GetNewIndex(ReorderableMenuItem draggedOnEntry)
        {
            int draggedOnIndex = this.Children.ToList().IndexOf(draggedOnEntry);

            // Entry not found -> append at the end
            if (draggedOnIndex == -1)
            {
                return this.Children.Count - 1;
            }

            return draggedOnIndex;
        }

        private int GetCurrentDragOverIndex()
        {
            List<Control> currentHoveredEntries = this.Children.Where(child =>
            {
                if (child is ReorderableMenuItem)
                {
                    int x = this.RelativeMousePosition.X + this.HorizontalScrollOffset;
                    int y = this.RelativeMousePosition.Y + this.VerticalScrollOffset;

                    return x >= child.Left && x < child.Right && y >= child.Top && y < child.Bottom;
                }
                return false;
            }).ToList();

            if (currentHoveredEntries.Count == 0)
            {
                return -1;
            }

            return GetNewIndex(currentHoveredEntries.First() as ReorderableMenuItem);
        }

        private void DragItem(ReorderableMenuItem draggingEntry, int newIndex)
        {
            int currentIndex = this.Children.IndexOf(draggingEntry);
            if (newIndex != currentIndex)
            {
                // move dragged child
                if (this.Children.Remove(draggingEntry))
                {
                    // If dragged further down -> decrement due to remove
                    if (newIndex > currentIndex)
                    {
                        newIndex--;
                    }
                }
                this.Children.Insert(newIndex, draggingEntry);
                Invalidate();
            }
        }

        public override void UpdateContainer(GameTime gameTime)
        {
            base.UpdateContainer(gameTime);

            var draggingEntry = DraggedChild;
            if (draggingEntry == null)
            {
                return;
            }

            // Check if placeholder already exists -> move him
            int draggedOnIndex = this.GetCurrentDragOverIndex();
            if (draggedOnIndex == -1)
            {
                // Mouse probably outside list
                return;
            }

            DragItem(draggingEntry, draggedOnIndex);
        }

        private void Game_OnLeftMouseButtonReleased(object sender, MouseEventArgs args)
        {
            if (sender is MouseHandler mh)
            {
                var draggedEntry = DraggedChild;
                if (draggedEntry == null)
                {
                    // nothing dragging right now -> ignore
                    return;
                }

                // Only handle non ReorderableMenuItem (because they are handled separately)
                if (mh.ActiveControl is ReorderableMenuItem)
                {
                    // Unsubscribe
                    GameService.Input.Mouse.LeftMouseButtonReleased -= Game_OnLeftMouseButtonReleased;
                    return;
                }

                // Dragging, but released outside dragging area -> stop dragging
                draggedEntry.Dragging = false;

                Reordered?.Invoke(this, this.ReorderableChildren);

                // Unsubscribe
                GameService.Input.Mouse.LeftMouseButtonReleased -= Game_OnLeftMouseButtonReleased;
            }
        }

        protected override void DisposeControl()
        {
            GameService.Input.Mouse.LeftMouseButtonReleased -= Game_OnLeftMouseButtonReleased;
            base.DisposeControl();
        }
    }
}
