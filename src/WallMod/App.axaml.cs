using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Data.Core;
using Avalonia.Data.Core.Plugins;
using Avalonia.Markup.Xaml;
using Avalonia.Platform;
using Avalonia.Threading;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Diagnostics;
using System.Drawing;
using System.Threading.Tasks;
using WallMod.Helpers;
using WallMod.ViewModels;
using WallMod.Views;

namespace WallMod;

public partial class App : Application
{

    private TrayIcon? trayIcon;

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
        SetupGlobalExceptionHandling();
    }

    public static IServiceProvider Services { get; set; } = default!;

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desk)
        {

            // show splash screen
            var splash = new SplashScreen();
            desk.MainWindow = splash;
            splash.Show();


            Dispatcher.UIThread.Post(async () =>
            {
                await Task.Yield();

                var mainWindow = Services.GetRequiredService<MainWindow>();
                desk.MainWindow = mainWindow;
                InitializeTrayIcon(mainWindow);
                mainWindow.Closing += (_, e) =>
                {
                    // only the window's own close button hides to tray; quitting the app (Cmd+Q on macOS) or the OS shutting down still quits
                    if (e.CloseReason == WindowCloseReason.WindowClosing && ((MainWindowViewModel)mainWindow.DataContext!).StayRunningInBackground)
                    {
                        e.Cancel = true;
                        mainWindow.Hide();
                        Debug.WriteLine("App minimised to tray");
                    }
                };

                // macOS compat: clicking the dock icon brings the hidden window back
                if (this.TryGetFeature<IActivatableLifetime>() is { } activatable)
                    activatable.Activated += (_, e) =>
                    {
                        if (e.Kind == ActivationKind.Reopen) { mainWindow.Show(); mainWindow.WindowState = WindowState.Normal; }
                    };
                // macOS compat: stop App Nap from throttling autoset's timer while WallMod sits hidden in the background
                if (OperatingSystem.IsMacOS()) SetWallpaperHelper.MacBeginActivity();


                mainWindow.Show();
                splash.Close();
            }, DispatcherPriority.Background);
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



    private void SetupGlobalExceptionHandling()
    {
        AppDomain.CurrentDomain.UnhandledException += (sender, e) =>
        {
            if (e.ExceptionObject is Exception ex)
            {
                AppStorageHelper.LogCrash(ex);
            }
        };

        TaskScheduler.UnobservedTaskException += (sender, e) =>
        {
            AppStorageHelper.LogCrash(e.Exception);
            e.SetObserved();
        };
    }

}
