using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Data.Core;
using Avalonia.Data.Core.Plugins;
using Avalonia.Markup.Xaml;
using Avalonia.Platform;
using System;
using System.Diagnostics;
using WallMod.ViewModels;
using WallMod.Views;

namespace WallMod;

public partial class App : Application
{

    private TrayIcon? trayIcon;

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            // Line below is needed to remove Avalonia data validation.
            // Without this line you will get duplicate validations from both Avalonia and CT
            BindingPlugins.DataValidators.RemoveAt(0);

            var mainWindow = new MainWindow
            {
                DataContext = new MainWindowViewModel()
            };

            desktop.MainWindow = mainWindow;

            // init system tray icon
            InitializeTrayIcon(mainWindow);

            // minimize to tray if desired
            mainWindow.Closing += (s, e) =>
            {
                if (((MainWindowViewModel)mainWindow.DataContext).StayRunningInBackground)
                {
                    e.Cancel = true; // prevent actual closing
                    mainWindow.Hide(); // minimize to system tray
                    Debug.WriteLine("app minimized to tray");
                }
            };
        }

        base.OnFrameworkInitializationCompleted();
    }


    private void InitializeTrayIcon(Window mainWindow)
    {
        trayIcon = new TrayIcon
        {
            Icon = new WindowIcon(AssetLoader.Open(new Uri("avares://Wallmod/Assets/wallmodicon.ico"))),
            ToolTipText = "WallpaperMotor",
            IsVisible = true
        };


        trayIcon.Clicked += (_, _) =>
        {
            mainWindow.Show();
            mainWindow.WindowState = WindowState.Normal;
            Debug.WriteLine("Restored from tray.");
        };

        // create tray menu
        var menu = new NativeMenu();

        // "Show" menu item
        var showItem = new NativeMenuItem("Show");
        showItem.Click += (_, _) =>
        {
            mainWindow.Show();
            mainWindow.WindowState = WindowState.Normal;
        };

        // "Exit" menu item
        var exitItem = new NativeMenuItem("Exit");
        exitItem.Click += (_, _) =>
        {
            Environment.Exit(0);
        };

        menu.Items.Add(showItem);
        menu.Items.Add(exitItem);
        trayIcon.Menu = menu;
    }


}
