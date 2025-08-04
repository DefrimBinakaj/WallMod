using Avalonia.Controls;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Platform.Storage;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using WallMod.Helpers;
using WallMod.Models;
using WallMod.State;

namespace WallMod.ViewModels;

public partial class AutoSetViewModel : ObservableObject
{
    
    private readonly UniversalAppStore uniVM = App.Services!.GetRequiredService<UniversalAppStore>();
    ImageHelper imageHelper = new ImageHelper();

    // create WallpaperQueue replica which is the same as universal value
    public ObservableCollection<Wallpaper> WallpaperQueue => uniVM.WallpaperQueue;

    public CancellationTokenSource cancelToken = new CancellationTokenSource();

    // collection of images in directory chosen for rand autoset
    public ObservableCollection<Wallpaper> RandDirImageCollection = new();

    public AutoSetViewModel(UniversalAppStore universalVM)
    {
        uniVM = universalVM;
    }


    private int? minutesInput = 1;
    public int? MinutesInput { get => minutesInput; set => SetProperty(ref minutesInput, value); }

    private int? hoursInput = 0;
    public int? HoursInput { get => hoursInput; set => SetProperty(ref hoursInput, value); }

    private int? daysInput = 0;
    public int? DaysInput { get => daysInput; set => SetProperty(ref daysInput, value); }

    private int? weeksInput = 0;
    public int? WeeksInput { get => weeksInput; set => SetProperty(ref weeksInput, value); }

    private int? monthsInput = 0;
    public int? MonthsInput { get => monthsInput; set => SetProperty(ref monthsInput, value); }

    // fallback = 60 seconds
    public int TotalSeconds => 
        (((((MonthsInput * 30 + WeeksInput * 7) + DaysInput) * 24 + hoursInput) * 60) + minutesInput) * 60 ?? 60;



    // queue layout switching 

    private bool customQueueViewVisible;
    public bool CustomQueueViewVisible { get => customQueueViewVisible; set => SetProperty(ref customQueueViewVisible, value); }

    private bool randomQueueViewVisible;
    public bool RandomQueueViewVisible { get => randomQueueViewVisible; set => SetProperty(ref randomQueueViewVisible, value); }

    private bool autoSetDisableButtonEnabled = false;
    public bool AutoSetDisableButtonEnabled { get => autoSetDisableButtonEnabled; set => SetProperty(ref autoSetDisableButtonEnabled, value); }

    private bool autoSetEnableButtonEnabled = true;
    public bool AutoSetEnableButtonEnabled { get => autoSetEnableButtonEnabled; set => SetProperty(ref autoSetEnableButtonEnabled, value); }


    private string randDirecName;
    public string RandDirecName { get => randDirecName; set => SetProperty(ref randDirecName, value); }

    private string randWallpapersRemaining;
    public string RandWallpapersRemaining { get => randWallpapersRemaining; set => SetProperty(ref randWallpapersRemaining, value); }

    private bool chooseRandForEachMonitor;
    public bool ChooseRandForEachMonitor { get => chooseRandForEachMonitor; set => SetProperty(ref chooseRandForEachMonitor, value); }

    [RelayCommand] public void queueChoiceCommand(string choice) => queueChoiceExec(choice);
    private void queueChoiceExec(string selectedChoice)
    {
        if (selectedChoice == "Custom")
        {
            CustomQueueViewVisible = true;
            RandomQueueViewVisible = false;
        }
        else if (selectedChoice == "Random")
        {
            CustomQueueViewVisible = false;
            RandomQueueViewVisible = true;
        }
    }


    [RelayCommand] public void execBrowseDirectory() => execBrowseDirec();
    private void execBrowseDirec()
    {
        browseDirec();
    }
    private async void browseDirec()
    {
        Window window = new Window();
        ImageHelper imgHelper = new ImageHelper();

        // open folder picker
        var folderOpenPick = await window.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = "Select a directory to randomly autoset images",
            AllowMultiple = false
        });

        if (folderOpenPick == null || !folderOpenPick.Any())
        {
            RandDirecName = "";
        }

        RandDirecName = folderOpenPick.Last().Path.LocalPath;


        List<string> SupportedExtensions = new List<string> { ".jpg", ".jpeg", ".png", ".bmp" };
        var files = Directory.EnumerateFiles(RandDirecName)
                             .Where(file => SupportedExtensions.Contains(Path.GetExtension(file).ToLower()));
        int totalFiles = files.Count();

        // IMPORTANT !!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!
        // hardcoded amount of processors used to retrieve all images in a directory
        int allocCPUThreads = uniVM.CPUThreadsAllocated;
        var semaphore = new System.Threading.SemaphoreSlim(allocCPUThreads);
        var tasks = new List<Task>();

        foreach (var filePath in files)
        {
            await semaphore.WaitAsync();

            var task = Task.Run(async () =>
            {
                try
                {
                    var wallpaper = imageHelper.getWallpaperObjectFromPath(filePath);

                    // add wallpaper to collec on main thread
                    await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
                    {
                        RandDirImageCollection.Add(wallpaper);
                    });
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"!!!!! failed to load image at {filePath}: {ex.Message}");
                }
                finally
                {
                    semaphore.Release();
                }
            });

            tasks.Add(task);
        }

        await Task.WhenAll(tasks);
    }


    [RelayCommand] public void randMonitorChoiceCommand(string choice) => randMonitorChoiceExec(choice);
    private void randMonitorChoiceExec(string selectedChoice)
    {
        if (selectedChoice == "SingularRand")
        {
            ChooseRandForEachMonitor = false;
        }
        else if (selectedChoice == "RandEach")
        {
            ChooseRandForEachMonitor = true;
        }
    }




    [RelayCommand] public void ExecEnableAutoSet() => enableAutoSet();
    private async void enableAutoSet()
    {
        if (CustomQueueViewVisible == true)
        {
            if (WallpaperQueue.Count > 0)
            {
                AutoSetDisableButtonEnabled = true;
                AutoSetEnableButtonEnabled = false;
                await ExecAutoSetQueue();
            }
        }
        else if (RandomQueueViewVisible == true)
        {
            if (RandDirImageCollection.Count > 0)
            {
                AutoSetDisableButtonEnabled = true;
                AutoSetEnableButtonEnabled = false;
                await ExecAutoSetRand();
            }
        }
    }

    [RelayCommand] public void ExecDisableAutoSet() => disableAutoSet();
    private async void disableAutoSet()
    {
        cancelToken.Cancel();
        cancelToken = new CancellationTokenSource();
        AutoSetDisableButtonEnabled = false;
        AutoSetEnableButtonEnabled = true;
        // do not clear the custom queue
        // WallpaperQueue.Clear();
        RandDirImageCollection.Clear();
        RandDirecName = "";
        RandWallpapersRemaining = "";
    }

    private async Task ExecAutoSetQueue()
    {
        while (!cancelToken.IsCancellationRequested)
        {
            if (WallpaperQueue.Count == 0)
            {
                disableAutoSet();
                return;
            }
            else if (WallpaperQueue.Count > 0 && TotalSeconds >= 60)
            {
                Debug.WriteLine("current time interval = " + TotalSeconds.ToString() + "seconds");
                Debug.WriteLine("current image that is set = " + WallpaperQueue.First().Name);
                foreach (var mon in uniVM.MonitorList)
                {
                    SetWallpaperHelper.SetWallpaper(WallpaperQueue.First().FilePath, "Fill", mon.MonitorIdPath);
                }
                WallpaperQueue.RemoveAt(0);
            }

            try
            {
                await Task.Delay(TotalSeconds * 1000, cancelToken.Token);
            }
            catch (TaskCanceledException)
            {
                return;
            }
            
        }
    }

    private async Task ExecAutoSetRand()
    {
        while (!cancelToken.IsCancellationRequested)
        {
            if (RandDirImageCollection.Count == 0)
            {
                disableAutoSet();
                return;
            }
            else if (RandDirImageCollection.Count > 0 && TotalSeconds >= 60)
            {
                // choose between one rand image for all monitors, or diff rand for each monitor
                if (ChooseRandForEachMonitor == true)
                {
                    // make different rand image for each monitor
                    foreach (var mon in uniVM.MonitorList)
                    {
                        var randInit = new Random();
                        var randWallpaperChoice = RandDirImageCollection.OrderBy(rnd => randInit.Next()).First();
                        SetWallpaperHelper.SetWallpaper(randWallpaperChoice.FilePath, "Fill", mon.MonitorIdPath);
                        RandDirImageCollection.Remove(randWallpaperChoice);
                    }
                }
                else if (ChooseRandForEachMonitor == false)
                {
                    // use same image for all monitors
                    var randInit = new Random();
                    var randWallpaperChoice = RandDirImageCollection.OrderBy(rnd => randInit.Next()).First();
                    foreach (var mon in uniVM.MonitorList)
                    {
                        SetWallpaperHelper.SetWallpaper(randWallpaperChoice.FilePath, "Fill", mon.MonitorIdPath);
                    }
                    RandDirImageCollection.Remove(randWallpaperChoice);
                }
                RandWallpapersRemaining = "Wallpapers Remaining = " + RandDirImageCollection.Count.ToString();
            }

            try
            {
                await Task.Delay(TotalSeconds * 1000, cancelToken.Token);
            }
            catch (TaskCanceledException)
            {
                return;
            }
        }
    }

}
