using System;
using System.Threading;
using Avalonia;
using Avalonia.Controls;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using WallMod.State;
using WallMod.ViewModels;
using WallMod.Views;

namespace WallMod
{
    internal sealed class Program
    {

        // mutex used for single-instance mechanism (ensure only one instance of app running at all times)
        private const string mutexName = "WallMod_SingleInstance_Mutex";

        // Initialization code. Don't use any Avalonia, third-party APIs or any
        // SynchronizationContext-reliant code before AppMain is called: things aren't initialized
        // yet and stuff might break.
        [STAThread]
        public static void Main(string[] args)
        {
            // logic required for single-instance mechanism mutexing
            using var mutex = new Mutex(true, mutexName, out bool isFirst);
            if (!isFirst) return;                 // second instance ─> exit

            // ---------- IoC container -------------------------------------------------
            var host = Host.CreateDefaultBuilder(args)
                           .ConfigureServices(services =>
                           {
                               // shared state
                               services.AddSingleton<UniversalAppStore>();

                               // view-models
                               services.AddTransient<MainWindowViewModel>();
                               services.AddTransient<QueueViewModel>();
                               services.AddTransient<HistoryViewModel>();
                               services.AddTransient<SettingsViewModel>();

                               // views / windows
                               services.AddTransient<MainWindow>();
                               services.AddTransient<QueueView>();
                               services.AddTransient<HistoryView>();
                               services.AddTransient<SettingsView>();
                           })
                           .Build();

            App.Services = host.Services; // expose globally for App.xaml.cs

            // ---------- start Avalonia -----------------------------------------------
            BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
        }

        // Avalonia configuration, don't remove; also used by visual designer.
        public static AppBuilder BuildAvaloniaApp()
            => AppBuilder.Configure<App>()
                .UsePlatformDetect()
                .WithInterFont()
                .LogToTrace();
    }
}
