using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Microsoft.Extensions.DependencyInjection;
using WallMod.Models;
using WallMod.State;
using WallMod.ViewModels;

namespace WallMod.Views;

public partial class AutoSetView : UserControl
{

    private readonly UniversalAppStore uniVM = App.Services!.GetRequiredService<UniversalAppStore>();

    public AutoSetView()
    {
        InitializeComponent();
        DataContext = App.Services!.GetRequiredService<AutoSetViewModel>();
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