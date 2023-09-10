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
        //private static readonly Logger Logger = Logger.GetLogger<ReorderableMenu>();

        public event EventHandler<List<ReorderableMenuItem>> Reordered;

        private ReorderableMenuItem DraggedChild => Children.FirstOrDefault(child => child is ReorderableMenuItem entry && entry.Dragging) as ReorderableMenuItem;
        private List<ReorderableMenuItem> ReorderableChildren => Children.Where(child => child is ReorderableMenuItem).Cast<ReorderableMenuItem>().ToList();

        protected override void OnChildAdded(ChildChangedEventArgs e)
        {
            if (e.ChangedChild is ReorderableMenuItem)
            {
                // TODO THIS MIGHT CAUSE ISSUE WITH Click EVENT -> BOTH FIRE
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

        // TODO ALLOW INSERT AT BOTTOM IF DRAGGED BELOW LAST ELEMENT -> will not react because LeftMouse not released above an ReorderableMenuItem -> perhaps add this handler also to menu?!?!?! (unsure if menu or menu parent)
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
                    }
                }
                draggedEntry.Dragging = false;

                Reordered?.Invoke(this, this.ReorderableChildren);
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


            // TODO SOMETIMES BREAKS (probably when mouse leaves parent AND THEN LMOUSE IS RELEASED -> event not caught -> still thinking that element is being dragged)
            //GameService.Input.Mouse.LeftMouseButtonReleased

            // Check if placeholder already exists -> move him
            int draggedOnIndex = this.GetCurrentDragOverIndex();
            if (draggedOnIndex == -1)
            {
                // Mouse probably outside list
                return;
            }

            DragItem(draggingEntry, draggedOnIndex);
        }
    }
}
