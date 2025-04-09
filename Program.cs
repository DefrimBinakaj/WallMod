using System;
using System.Threading;
using Avalonia;
using Avalonia.Controls;
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
            bool createdNew;
            using (Mutex mutex = new Mutex(true, mutexName, out createdNew))
            {
                if (!createdNew)
                {
                    MainWindow mw = new MainWindow();
                    mw.BringWindowToFront(); //NOT IMPLM YET: bring window up when pinned taskbar logo is clicked, NOT tray icon
                    Console.WriteLine("Another instance is already running. Exiting.");
                    return;
                }
                BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
            }
        }

        // Avalonia configuration, don't remove; also used by visual designer.
        public static AppBuilder BuildAvaloniaApp()
            => AppBuilder.Configure<App>()
                .UsePlatformDetect()
                .WithInterFont()
                .LogToTrace();
    }
}
