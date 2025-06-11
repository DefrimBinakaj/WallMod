using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.VisualTree;
using Microsoft.Extensions.DependencyInjection;
using System;
using WallMod.Helpers;
using WallMod.Models;
using WallMod.State;
using WallMod.ViewModels;

namespace WallMod.Views;

public partial class HistoryView : UserControl
{

    private readonly UniversalAppStore uniVM = App.Services!.GetRequiredService<UniversalAppStore>();

    WallpaperHistoryHelper wallpaperHistoryHelper = new WallpaperHistoryHelper();

    public HistoryView()
    {
        InitializeComponent();
        DataContext = App.Services!.GetRequiredService<HistoryViewModel>();
    }



    private async void OnImageTapped(object? sender, PointerPressedEventArgs e)
    {
        if (sender is Control control && control.DataContext is Wallpaper wallpaper)
        {
            // get actual window that owns this view
            var mainWindow = this.GetVisualRoot() as MainWindow;
            if (mainWindow?.DataContext is not MainWindowViewModel mainVM) return;

            // replicate MainWindow code behind history image tap
            mainWindow.ResetRectangle();
            mainWindow.SetBackgroundButton.IsEnabled = false;
            await mainVM.ImageTapped(wallpaper);
        }
    }


    public void HistoryEntryDeleteClicked(object? sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.DataContext is Wallpaper wallpaper)
        {
            if (wallpaper == null)
            {
                return;
            }
            wallpaperHistoryHelper.RemoveHistoryEntry(wallpaper.FilePath);
            uniVM.HistoryWallpaperList.Remove(wallpaper);
        }
    }

}