using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Microsoft.Extensions.DependencyInjection;
using System;
using WallMod.Models;
using WallMod.State;
using WallMod.ViewModels;

namespace WallMod.Views;

public partial class AutoSetView : UserControl
{

    private readonly UniversalAppStore uniVM = App.Services!.GetRequiredService<UniversalAppStore>();

    private Wallpaper _draggedItem;
    private Point _dragStart;
    private bool _isDragging;

    public AutoSetView()
    {
        InitializeComponent();
        DataContext = App.Services!.GetRequiredService<AutoSetViewModel>();
    }

    public void ClearWallpaperQueue(object? sender, RoutedEventArgs e)
    {
        uniVM.WallpaperQueue.Clear();
    }


    private void Item_PointerPressed(object sender, PointerPressedEventArgs e)
    {
        if (sender is Control c && c.DataContext is Wallpaper wp)
        {
            _draggedItem = wp;
            _dragStart = e.GetPosition(null);
            _isDragging = false;
        }
    }

    private async void Item_PointerMoved(object sender, PointerEventArgs e)
    {
        if (_draggedItem == null || _isDragging) return;
        
        var currentPos = e.GetPosition(null);
        var distance = Math.Sqrt(Math.Pow(currentPos.X - _dragStart.X, 2) +
                                 Math.Pow(currentPos.Y - _dragStart.Y, 2));

        if (distance > 5)
        {
            _isDragging = true;
            var data = new DataObject();
            data.Set("Wallpaper", _draggedItem);
            await DragDrop.DoDragDrop(e, data, DragDropEffects.Move);
        }
    }

    private void Item_PointerReleased(object sender, PointerReleasedEventArgs e)
    {
        CleanupDrag();
    }

    private void ListBox_DragOver(object sender, DragEventArgs e)
    {
        e.DragEffects = DragDropEffects.Move;
        e.Handled = true;
        ShowInsertionLine(e);
    }

    private void ListBox_Drop(object? sender, DragEventArgs e)
    {
        if (_draggedItem == null || !e.Data.Contains("Wallpaper") || sender is not ListBox lb)
        {
            CleanupDrag();
            return;
        }

        var pos = e.GetPosition(lb);
        var targetIndex = FindInsertionIndex(pos, lb);
        var currentIndex = uniVM.WallpaperQueue.IndexOf(_draggedItem);

        // don't move if dropping at the same position or adjacent position that results in no change
        if (currentIndex >= 0 && targetIndex >= 0)
        {
            // adjust target index if we're moving from left to right
            // (since removing the item shifts indices)
            var adjustedTarget = targetIndex;
            if (currentIndex < targetIndex)
            {
                adjustedTarget--;
            }

            // only move if it actually changes position
            if (currentIndex != adjustedTarget)
            {
                uniVM.WallpaperQueue.Move(currentIndex, adjustedTarget);
            }
        }

        CleanupDrag();
    }

    // finds where to insert based on mouse position
    private int FindInsertionIndex(Point pos, ListBox lb)
    {
        // check each item to see if we're to the left of its center
        for (int i = 0; i < lb.ItemCount; i++)
        {
            if (lb.ContainerFromIndex(i) is Control container)
            {
                var itemBounds = container.Bounds;
                var itemCenterX = itemBounds.Left + (itemBounds.Width / 2);

                // if mouse is left of this item's center, insert here
                if (pos.X < itemCenterX)
                {
                    return i;
                }
            }
        }

        // if we're past all items, insert at the end
        return lb.ItemCount;
    }

    // positions the insertion indicator line (VERTICAL line, move by X)
    private void ShowInsertionLine(DragEventArgs e)
    {
        if (QueueListBox is not ListBox lb || DropIndicator is not Canvas canvas)
        {
            InsertLine.IsVisible = false;
            return;
        }

        var pos = e.GetPosition(lb);
        var insertIndex = FindInsertionIndex(pos, lb);
        double xPosition = 0;

        if (insertIndex < lb.ItemCount)
        {
            // position line at the left edge of the target item
            if (lb.ContainerFromIndex(insertIndex) is Control container)
            {
                var translation = container.TranslatePoint(new Point(0, 0), canvas);
                xPosition = translation?.X ?? 0;

                // adjust for the margin (3 pixels on each side based on your XAML)
                xPosition -= 3;
            }
        }
        else
        {
            // position line at the right edge of the last item
            if (lb.ItemCount > 0 && lb.ContainerFromIndex(lb.ItemCount - 1) is Control lastContainer)
            {
                var translation = lastContainer.TranslatePoint(new Point(lastContainer.Bounds.Width, 0), canvas);
                xPosition = translation?.X ?? canvas.Bounds.Width;

                // adjust for the margin
                xPosition += 3;
            }
        }

        // position the insertion line
        Canvas.SetLeft(InsertLine, xPosition); // center the 2px wide line
        Canvas.SetTop(InsertLine, 10); // vertically center in the 84px high area
        InsertLine.IsVisible = true;
    }


    private void CleanupDrag()
    {
        _draggedItem = null;
        _isDragging = false;
        if (InsertLine != null)
        {
            InsertLine.IsVisible = false;
        }
    }


}