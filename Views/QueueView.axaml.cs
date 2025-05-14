using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Microsoft.Extensions.DependencyInjection;
using WallMod.Models;
using WallMod.State;
using WallMod.ViewModels;

namespace WallMod.Views;

public partial class QueueView : UserControl
{

    private readonly UniversalAppStore uniVM = App.Services!.GetRequiredService<UniversalAppStore>();

    public QueueView()
    {
        InitializeComponent();
        DataContext = App.Services!.GetRequiredService<QueueViewModel>();
    }


    // wallpaper queue management functions
    public void MoveWallpaperUpClicked(object? sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.DataContext is Wallpaper wallpaper)
        {
            var i = uniVM.WallpaperQueue.IndexOf(wallpaper);
            if (i > 0) uniVM.WallpaperQueue.Move(i, i - 1);
        }
    }
    public void MoveWallpaperDownClicked(object? sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.DataContext is Wallpaper wallpaper)
        {
            var i = uniVM.WallpaperQueue.IndexOf(wallpaper);
            if (i >= 0 && i < uniVM.WallpaperQueue.Count - 1) uniVM.WallpaperQueue.Move(i, i + 1);
        }
    }
    public void ClearWallpaperQueue(object? sender, RoutedEventArgs e)
    {
        uniVM.WallpaperQueue.Clear();
    }


    


}