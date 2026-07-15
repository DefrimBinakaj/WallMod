using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
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

        uniVM.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(uniVM.SelectedPrimaryAccentColour))
                OnPropertyChanged(nameof(SelectedPrimaryAccentColour));
        };

        UpdateLoopButtonColour();
    }



    private int timeValueInput = 5; // default to 5
    public int TimeValueInput { get => timeValueInput; set { SetProperty(ref timeValueInput, value); OnPropertyChanged(nameof(TotalSeconds)); } }

    private int selectedTimeUnitIndex = 1; // Default to minutes
    public int SelectedTimeUnitIndex { get => selectedTimeUnitIndex; set { SetProperty(ref selectedTimeUnitIndex, value); OnPropertyChanged(nameof(TotalSeconds)); } }

    // calc total seconds based on selected value and unit
    public int TotalSeconds
    {
        get
        {
            int multiplier = selectedTimeUnitIndex switch
            {
                0 => 1,           // seconds
                1 => 60,          // minutes
                2 => 3600,        // hours
                3 => 86400,       // days
                4 => 604800,      // weeks
                5 => 2592000,     // months (30 days)
                _ => 60           // default to minutes
            };

            return Math.Max(1, TimeValueInput * multiplier);
        }
    }


    // countdown display for the next autoset change (display only - Task.Delay stays the authority)
    private DispatcherTimer? countdownTimer;
    private DateTime nextChangeAt;

    private string timeRemainingDisplay = "";
    public string TimeRemainingDisplay { get => timeRemainingDisplay; set => SetProperty(ref timeRemainingDisplay, value); }
    
    private void StartCountdown()
    {
        nextChangeAt = DateTime.Now.AddSeconds(TotalSeconds);

        if (countdownTimer == null)
        {
            countdownTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            countdownTimer.Tick += (s, e) => UpdateCountdownDisplay();
        }

        UpdateCountdownDisplay(); // show immediately, don't wait for first tick
        countdownTimer.Start();
    }

    private void StopCountdown()
    {
        countdownTimer?.Stop();
        TimeRemainingDisplay = "";
    }

    private void UpdateCountdownDisplay()
    {
        var remaining = nextChangeAt - DateTime.Now;
        if (remaining < TimeSpan.Zero) remaining = TimeSpan.Zero;
        TimeRemainingDisplay = "Next in " + FormatRemaining(remaining);
    }

    // adaptive: your intervals range from seconds to months
    private static string FormatRemaining(TimeSpan t)
    {
        if (t.TotalDays >= 1) return $"{(int)t.TotalDays}d {t.Hours}h";
        if (t.TotalHours >= 1) return $"{(int)t.TotalHours}h {t.Minutes}m";
        if (t.TotalMinutes >= 1) return $"{(int)t.TotalMinutes}m {t.Seconds}s";
        return $"{t.Seconds}s";
    }



    // themes
    public Color SelectedBackgroundColour { get => uniVM.SelectedBackgroundColour; set { if (uniVM.SelectedBackgroundColour != value) { uniVM.SelectedBackgroundColour = value; OnPropertyChanged(); } } }
    public Color SelectedPrimaryAccentColour { get => uniVM.SelectedPrimaryAccentColour; set { if (uniVM.SelectedPrimaryAccentColour != value) { uniVM.SelectedPrimaryAccentColour = value; OnPropertyChanged(); } } }
    public Color SelectedWallpaperCollectionColour { get => uniVM.SelectedWallpaperCollectionColour; set { if (uniVM.SelectedWallpaperCollectionColour != value) { uniVM.SelectedWallpaperCollectionColour = value; OnPropertyChanged(); } } }
    public Color SelectedPreviewBackgroundColour { get => uniVM.SelectedPreviewBackgroundColour; set { if (uniVM.SelectedPreviewBackgroundColour != value) { uniVM.SelectedPreviewBackgroundColour = value; OnPropertyChanged(); } } }

    // queue layout switching 

    private bool customQueueViewVisible;
    public bool CustomQueueViewVisible { get => customQueueViewVisible; set => SetProperty(ref customQueueViewVisible, value); }

    private bool randomQueueViewVisible;
    public bool RandomQueueViewVisible { get => randomQueueViewVisible; set => SetProperty(ref randomQueueViewVisible, value); }


    private bool customAutoSetDisableButtonEnabled = false;
    public bool CustomAutoSetDisableButtonEnabled { get => customAutoSetDisableButtonEnabled; set => SetProperty(ref customAutoSetDisableButtonEnabled, value); }

    private bool customAutoSetEnableButtonEnabled = true;
    public bool CustomAutoSetEnableButtonEnabled { get => customAutoSetEnableButtonEnabled; set => SetProperty(ref customAutoSetEnableButtonEnabled, value); }


    private bool randomAutoSetDisableButtonEnabled = false;
    public bool RandomAutoSetDisableButtonEnabled { get => randomAutoSetDisableButtonEnabled; set => SetProperty(ref randomAutoSetDisableButtonEnabled, value); }

    private bool randomAutoSetEnableButtonEnabled = true;
    public bool RandomAutoSetEnableButtonEnabled { get => randomAutoSetEnableButtonEnabled; set => SetProperty(ref randomAutoSetEnableButtonEnabled, value); }


    private string randDirecName;
    public string RandDirecName { get => randDirecName; set => SetProperty(ref randDirecName, value); }

    private string randWallpapersRemaining;
    public string RandWallpapersRemaining { get => randWallpapersRemaining; set => SetProperty(ref randWallpapersRemaining, value); }

    private bool randomizeEachMonitor;
    public bool RandomizeEachMonitor { get => randomizeEachMonitor; set => SetProperty(ref randomizeEachMonitor, value); }

    private Color loopButtonColour = Colors.Transparent;
    public Color LoopButtonColour { get => loopButtonColour; set => SetProperty(ref loopButtonColour, value); }

    [RelayCommand] public void toggleLoopQueueButton() => ToggleLoopQueue();
    public void ToggleLoopQueue()
    {
        uniVM.LoopQueue = !uniVM.LoopQueue;
        Debug.WriteLine("loop queue = " + uniVM.LoopQueue);
        UpdateLoopButtonColour();
    }

    public void UpdateLoopButtonColour()
    {
        LoopButtonColour = uniVM.LoopQueue
            ? SelectedPrimaryAccentColour
            : Colors.Transparent;
    }


    [RelayCommand] // in axaml, itll be referenced as queueChoiceCommand
    private void queueChoice(string selectedChoice)
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

        // clear previous choice
        RandDirImageCollection.Clear();
        RandDirecName = "";
        RandWallpapersRemaining = "";


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
            return;
        }

        if (!folderOpenPick.Any()) return; // if cancel is clicked, don't crash
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
                        RandWallpapersRemaining = "Wallpapers Remaining = " + RandDirImageCollection.Count.ToString();
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



    // enable for custom queue
    [RelayCommand] public async Task ExecEnableCustomAutoSet() => await enableCustomAutoSet();
    private async Task enableCustomAutoSet()
    {
        if (WallpaperQueue.Count > 0)
        {
            CustomAutoSetDisableButtonEnabled = true;
            CustomAutoSetEnableButtonEnabled = false;

            // make sure you disable random to avoid concurrent autosetting
            disableRandomAutoSet();

            await ExecCustomAutoSet();
        }
    }
    // disable for custom queue
    [RelayCommand] public void ExecDisableCustomAutoSet() => disableCustomAutoSet();
    private async void disableCustomAutoSet()
    {
        cancelToken.Cancel();
        cancelToken = new CancellationTokenSource();
        CustomAutoSetDisableButtonEnabled = false;
        CustomAutoSetEnableButtonEnabled = true;
        // do not clear the custom queue
        // WallpaperQueue.Clear();
        StopCountdown();
    }



    // enable for rand queue
    [RelayCommand] public async Task ExecEnableRandomAutoSet() => await enableRandomAutoSet();
    private async Task enableRandomAutoSet()
    {
        if (RandDirImageCollection.Count > 0)
        {
            RandomAutoSetDisableButtonEnabled = true;
            RandomAutoSetEnableButtonEnabled = false;

            // make sure you disable custom to avoid concurrent autosetting
            disableCustomAutoSet();

            await ExecRandomAutoSet();
        }
    }
    // disable for rand queue
    [RelayCommand] public void ExecDisableRandomAutoSet() => disableRandomAutoSet();
    private async void disableRandomAutoSet()
    {
        cancelToken.Cancel();
        cancelToken = new CancellationTokenSource();
        RandomAutoSetDisableButtonEnabled = false;
        RandomAutoSetEnableButtonEnabled = true;
        // do not clear random queue
        // RandDirImageCollection.Clear();
        // RandDirecName = "";
        // RandWallpapersRemaining = "";
        StopCountdown();
    }



    private async Task ExecCustomAutoSet()
    {
        while (!cancelToken.IsCancellationRequested)
        {
            if (WallpaperQueue.Count == 0)
            {
                disableCustomAutoSet();
                return;
            }
            else if (WallpaperQueue.Count > 0 && TotalSeconds >= 1)
            {
                Debug.WriteLine("current time interval = " + TotalSeconds.ToString() + "seconds");
                Debug.WriteLine("current image that is set = " + WallpaperQueue.First().Name);

                var wp = WallpaperQueue.First();

                // cropped item -> apply only to the monitor the crop was drawn for, if it's still connected
                bool setCropped = false;
                if (wp.CropX is int cx && wp.CropY is int cy &&
                    wp.CropWidth is int cw && wp.CropHeight is int ch &&
                    cw > 0 && ch > 0 && wp.CropMonitorId is string cropMonId)
                {
                    var targetMon = uniVM.MonitorList.FirstOrDefault(m => m.MonitorIdPath == cropMonId);
                    if (targetMon != null)
                    {
                        SetWallpaperHelper.SetWallpaperCropped(wp.FilePath, "Fill", targetMon.MonitorIdPath, cx, cy, cw, ch);
                        setCropped = true;
                    }
                }

                // uncropped item, or the crop's monitor is gone -> full image on all monitors as before
                if (!setCropped)
                {
                    foreach (var mon in uniVM.MonitorList)
                    {
                        SetWallpaperHelper.SetWallpaper(wp.FilePath, "Fill", mon.MonitorIdPath);
                    }
                }

                WallpaperQueue.RemoveAt(0);

                if (uniVM.LoopQueue)
                {
                    WallpaperQueue.Add(wp); // finished image (crop and all) goes to the back
                }
            }

            StartCountdown();
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

    [RelayCommand] public void clearWallpaperQueueButton() => ClearWallpaperQueue();
    public void ClearWallpaperQueue()
    {
        uniVM.WallpaperQueue.Clear();
        disableCustomAutoSet();
    }

    [RelayCommand] public void skipCustomAutoSetImageButton() => SkipCustomAutoSetImage();
    private void SkipCustomAutoSetImage()
    {
        Debug.WriteLine("Skipping one custom queued wallpaper");
        cancelToken.Cancel();
        cancelToken = new CancellationTokenSource();
        enableCustomAutoSet();
    }


    private async Task ExecRandomAutoSet()
    {
        while (!cancelToken.IsCancellationRequested)
        {
            if (RandDirImageCollection.Count == 0)
            {
                disableRandomAutoSet();
                return;
            }
            else if (RandDirImageCollection.Count > 0 && TotalSeconds >= 1)
            {
                // choose between one rand image for all monitors, or diff rand for each monitor
                if (RandomizeEachMonitor == true)
                {
                    // make different rand image for each monitor
                    foreach (var mon in uniVM.MonitorList)
                    {
                        var randInit = new Random();
                        var randWallpaperChoice = RandDirImageCollection.OrderBy(rnd => randInit.Next()).First();
                        SetWallpaperHelper.SetWallpaper(randWallpaperChoice.FilePath, "Fill", mon.MonitorIdPath);
                        RandDirImageCollection.Remove(randWallpaperChoice);
                        RandWallpapersRemaining = "Wallpapers Remaining = " + RandDirImageCollection.Count.ToString();
                    }
                }
                else if (RandomizeEachMonitor == false)
                {
                    // use same image for all monitors
                    var randInit = new Random();
                    var randWallpaperChoice = RandDirImageCollection.OrderBy(rnd => randInit.Next()).First();
                    foreach (var mon in uniVM.MonitorList)
                    {
                        SetWallpaperHelper.SetWallpaper(randWallpaperChoice.FilePath, "Fill", mon.MonitorIdPath);
                    }
                    RandDirImageCollection.Remove(randWallpaperChoice);
                    RandWallpapersRemaining = "Wallpapers Remaining = " + RandDirImageCollection.Count.ToString();
                }
            }

            StartCountdown();
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
    [RelayCommand] public void skipRandomAutoSetImageButton() => SkipRandomAutoSetImage();
    private void SkipRandomAutoSetImage()
    {
        Debug.WriteLine("Skipping one random queued wallpaper");
        cancelToken.Cancel();
        cancelToken = new CancellationTokenSource();
        enableRandomAutoSet();
    }

}
