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
        if (Math.Abs(currentPos.X - _dragStart.X) > 5 || 
            Math.Abs(currentPos.Y - _dragStart.Y) > 5)
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

    private void ListBox_DragLeave(object sender, RoutedEventArgs e)
    {
        InsertLine.IsVisible = false;
    }

    private void ListBox_Drop(object? sender, DragEventArgs e)
    {
        if (_draggedItem != null && 
            e.Data.Contains("Wallpaper") && 
            sender is ListBox lb)
        {
            var pos = e.GetPosition(lb);
            var newIndex = FindInsertionIndex(pos, lb);
            var oldIndex = uniVM.WallpaperQueue.IndexOf(_draggedItem);
            
            if (oldIndex >= 0 && newIndex >= 0 && oldIndex != newIndex)
            {
                uniVM.WallpaperQueue.Move(oldIndex, newIndex);
            }
        }
        CleanupDrag();
    }

    // finds where to insert based on mouse position
    private int FindInsertionIndex(Point pos, ListBox lb)
    {
        for (int i = 0; i < lb.ItemCount; i++)
        {
            if (lb.ContainerFromIndex(i) is Control c)
            {
                if (pos.Y < c.Bounds.Top + c.Bounds.Height / 2)
                {
                    return i;
                }
            }
        }
        return lb.ItemCount - 1;
    }

    // positions the insertion indicator line
    private void ShowInsertionLine(DragEventArgs e)
    {
        if (QueueListBox is not ListBox lb || DropIndicator is not Canvas canvas) return;
        
        var pos = e.GetPosition(lb);
        double yPos = 0;
        bool found = false;

        for (int i = 0; i < lb.ItemCount; i++)
        {
            if (lb.ContainerFromIndex(i) is Control c)
            {
                if (pos.Y < c.Bounds.Top + c.Bounds.Height / 2)
                {
                    var pt = c.TranslatePoint(new Point(0, 0), canvas);
                    yPos = pt?.Y ?? 0;
                    found = true;
                    break;
                }
            }
        }

        if (!found && lb.ItemCount > 0)
        {
            if (lb.ContainerFromIndex(lb.ItemCount - 1) is Control last)
            {
                var pt = last.TranslatePoint(new Point(0, last.Bounds.Height), canvas);
                yPos = pt?.Y ?? canvas.Bounds.Height;
            }
        }

        InsertLine.Width = canvas.Bounds.Width * 0.9;
        Canvas.SetLeft(InsertLine, canvas.Bounds.Width * 0.05);
        Canvas.SetTop(InsertLine, yPos);
        InsertLine.IsVisible = true;
    }

    private void CleanupDrag()
    {
        _draggedItem = null;
        _isDragging = false;
        InsertLine.IsVisible = false;
    }


}