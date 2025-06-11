using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Controls;
using Avalonia.Media.Imaging;
using Avalonia.Media;
using Avalonia.Platform;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Threading.Tasks;
using System;
using WallMod.Helpers;
using WallMod.Models;
using WallMod.Views;
using System.IO;
using Avalonia;
using System.Linq;
using System.Collections.Generic;
using System.Globalization;
using DataJuggler.PixelDatabase;
using Avalonia.Themes.Fluent;
using Avalonia.Styling;
using System.Threading;
using WallMod.State;
using Microsoft.Extensions.DependencyInjection;

namespace WallMod.ViewModels;

/**
 * Viewmodel for the main application functionality
 */
public partial class MainWindowViewModel : ViewModelBase
{

    private readonly UniversalAppStore uniVM = App.Services!.GetRequiredService<UniversalAppStore>();

    AppStorageHelper appStorageHelper = new AppStorageHelper();
    WallpaperHistoryHelper wallpaperHistoryHelper = new WallpaperHistoryHelper();
    SettingsHistoryHelper settingsHistoryHelper = new SettingsHistoryHelper();

    // all wallpapers from a directory
    public ObservableCollection<Wallpaper> AllWallpapers { get; set; } = new ObservableCollection<Wallpaper>();

    // current display of wallpapers after filtering
    public ObservableCollection<Wallpaper> DisplayWallpaperList { get; set; } = new ObservableCollection<Wallpaper>();

    // set of styles available (eg. fill, fit, tile, etc)
    public ObservableCollection<string> WallpaperStyleList { get; set; }

    // list of all current monitors of pc
    public ObservableCollection<MonitorInfo> MonitorList => uniVM.MonitorList;

    // list of history wallpapers
    public ObservableCollection<Wallpaper> HistoryWallpaperList => uniVM.HistoryWallpaperList;



    public MainWindowViewModel(UniversalAppStore universalVM)
    {

        uniVM = universalVM;

        // in order to instantly refresh theme changes in settings
        uniVM.PropertyChanged += (_, e) => OnPropertyChanged(e.PropertyName);

        appStorageHelper.InitAppStorage();

        WallpaperStyleList = new ObservableCollection<string>
        {
            "Fill", "Fit", "Stretch", "Tile", "Center", "Span"
        };
        SelectedWallpaperStyle = WallpaperStyleList[0];

        DetectMonitors();


        // BANDAID FIX for gallery / history visibility bug
        uniVM.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(UniversalAppStore.IsHistoryViewVisible))
                OnPropertyChanged(nameof(IsHistoryViewVisible));
            if (e.PropertyName == nameof(UniversalAppStore.IsImageGalleryViewVisible))
                OnPropertyChanged(nameof(IsImageGalleryViewVisible));
        };


        // set default img and values
        CurrentWallpaperPreview = new Bitmap(AssetLoader.Open(new Uri("avares://Wallmod/Assets/placeholder-icon.png")));
        CurrentWallpaperName = "Name";
        currentWallpaperSize = "Resolution";


        uniVM.AllowSaveHistory = bool.Parse(settingsHistoryHelper.GetSettingEntry("AllowSaveHistory"));
        uniVM.StayRunningInBackground = bool.Parse(settingsHistoryHelper.GetSettingEntry("StayRunningInBackground"));
        uniVM.AutoOpenLastDirectory = bool.Parse(settingsHistoryHelper.GetSettingEntry("AutoOpenLastChosenDirectoryOnAppStart"));
        uniVM.RememberFilters = bool.Parse(settingsHistoryHelper.GetSettingEntry("RememberFilterSettings"));
        uniVM.CPUThreadsAllocated = int.Parse(settingsHistoryHelper.GetSettingEntry("CPUThreadsAllocated"));

        if (uniVM.AutoOpenLastDirectory == true)
        {
            string previousDir = settingsHistoryHelper.GetSettingEntry("LastChosenDirectory");
            if (!string.IsNullOrEmpty(previousDir) && Directory.Exists(previousDir))
            {
                selectDirec(previousDir);
            }
        }

        if (uniVM.RememberFilters == true)
        {
            uniVM.FilterSearchText = settingsHistoryHelper.GetSettingEntry("SearchFilter");
            uniVM.ShowFolders = bool.Parse(settingsHistoryHelper.GetSettingEntry("ShowFoldersFilter"));
            uniVM.CurrentSortChoice = settingsHistoryHelper.GetSettingEntry("ImgPropertySort");
            uniVM.CurrentAspectRatio = settingsHistoryHelper.GetSettingEntry("AspectRatioFilter");
            applyAllFilters();
        }

        uniVM.SelectedBackgroundColour = Color.Parse(settingsHistoryHelper.GetSettingEntry("SelectedBackgroundColour"));
        uniVM.SelectedPrimaryAccentColour = Color.Parse(settingsHistoryHelper.GetSettingEntry("SelectedPrimaryAccentColour"));
        uniVM.SelectedWallpaperCollectionColour = Color.Parse(settingsHistoryHelper.GetSettingEntry("SelectedWallpaperCollectionColour"));
        uniVM.SelectedPreviewBackgroundColour = Color.Parse(settingsHistoryHelper.GetSettingEntry("SelectedPreviewBackgroundColour"));
        uniVM.changeFluentColour();


    }



    // UI variables ------------------------------------------------------------------------------

    private Wallpaper lastSelectedWallpaper;
    public Wallpaper LastSelectedWallpaper { get => lastSelectedWallpaper; set => SetProperty(ref lastSelectedWallpaper, value); }

    private MonitorInfo lastSelMonitor;
    public MonitorInfo LastSelMonitor { get => lastSelMonitor; set => SetProperty(ref lastSelMonitor, value); }

    private Bitmap currentWallpaperPreview;
    public Bitmap CurrentWallpaperPreview { get => currentWallpaperPreview; set { if (value != currentWallpaperPreview) { currentWallpaperPreview = value; OnPropertyChanged(nameof(CurrentWallpaperPreview)); } } }

    private String currentWallpaperName;
    public String CurrentWallpaperName { get => currentWallpaperName; set { if (value != currentWallpaperName) { currentWallpaperName = value; OnPropertyChanged(nameof(CurrentWallpaperName)); } } }

    private String currentWallpaperSize;
    public String CurrentWallpaperSize { get => currentWallpaperSize; set { if (value != currentWallpaperSize) { currentWallpaperSize = value; OnPropertyChanged(nameof(CurrentWallpaperSize)); } } }

    public double ImgLoadProgress { get => uniVM.ImgLoadProgress; set { if (uniVM.ImgLoadProgress != value) { uniVM.ImgLoadProgress = value; OnPropertyChanged(nameof(uniVM.ImgLoadProgress)); } } }
    
    public string CurrentSelectedDirectory { get => uniVM.CurrentSelectedDirectory; set { if (uniVM.CurrentSelectedDirectory != value) { uniVM.CurrentSelectedDirectory = value; OnPropertyChanged(nameof(uniVM.CurrentSelectedDirectory)); } } }
    public string CurrentSelectedDirecName { get => uniVM.CurrentSelectedDirecName; set { if (uniVM.CurrentSelectedDirecName != value) { uniVM.CurrentSelectedDirecName = value; OnPropertyChanged(nameof(uniVM.CurrentSelectedDirecName)); } } }

    private string selectedWallpaperStyle = "Fill";
    public string SelectedWallpaperStyle { get => selectedWallpaperStyle; set => SetProperty(ref selectedWallpaperStyle, value); }

    private double thumbnailZoomLevel = 150;
    public double ThumbnailZoomLevel { get => thumbnailZoomLevel; set { if (thumbnailZoomLevel != value) { thumbnailZoomLevel = value; OnPropertyChanged(); } } }

    private bool styleDropdownEnabled = false;
    public bool StyleDropdownEnabled { get => styleDropdownEnabled; set => SetProperty(ref styleDropdownEnabled, value); }


    public bool IsHistoryViewVisible { get => uniVM.IsHistoryViewVisible; set { if (uniVM.IsHistoryViewVisible != value) { uniVM.IsHistoryViewVisible = value; OnPropertyChanged(); } } }
    public bool IsImageGalleryViewVisible { get => uniVM.IsImageGalleryViewVisible; set { if (uniVM.IsImageGalleryViewVisible != value) { uniVM.IsImageGalleryViewVisible = value; OnPropertyChanged(); } } }

    private bool isPreviewVisible = true;
    public bool IsPreviewVisible { get => isPreviewVisible; set => SetProperty(ref isPreviewVisible, value); }

    private bool isAutoSetVisible = false;
    public bool IsAutoSetVisible { get => isAutoSetVisible; set => SetProperty(ref isAutoSetVisible, value); }

    public bool MainGridVisibility { get => uniVM.MainGridVisibility; set { if (uniVM.MainGridVisibility != value) { uniVM.MainGridVisibility = value; OnPropertyChanged(); } } }
    public bool SettingsViewVisibility { get => uniVM.SettingsViewVisibility; set { if (uniVM.SettingsViewVisibility != value) { uniVM.SettingsViewVisibility = value; OnPropertyChanged(); } } }
    public bool SetBackgroundButtonEnabled { get => uniVM.SetBackgroundButtonEnabled; set { if (uniVM.SetBackgroundButtonEnabled != value) { uniVM.SetBackgroundButtonEnabled = value; OnPropertyChanged(); } } }


    private bool isFilterOpen;
    public bool IsFilterOpen { get => isFilterOpen; set => SetProperty(ref isFilterOpen, value); }

    public string FilterSearchText
    {
        get => uniVM.FilterSearchText;
        set
        {
            if (uniVM.FilterSearchText != value)
            {
                uniVM.FilterSearchText = value;
                OnPropertyChanged();
                applyAllFilters(); // re-filter on every keystroke
            }
        }
    }
    public bool ShowFolders
    {
        get => uniVM.ShowFolders;
        set
        {
            if (uniVM.ShowFolders != value)
            {
                uniVM.ShowFolders = value;
                OnPropertyChanged();
                applyAllFilters();
            }
        }
    }
    public string CurrentSortChoice
    {
        get => uniVM.CurrentSortChoice;
        set
        {
            if (uniVM.CurrentSortChoice != value)
            {
                uniVM.CurrentSortChoice = value;
                OnPropertyChanged();
                applyAllFilters();
            }
        }
    }
    public string CurrentAspectRatio
    {
        get => uniVM.CurrentAspectRatio;
        set
        {
            if (uniVM.CurrentAspectRatio != value)
            {
                uniVM.CurrentAspectRatio = value;
                OnPropertyChanged();
                applyAllFilters();
            }
        }
    }

    public bool AllowSaveHistory { get => uniVM.AllowSaveHistory; set { if (uniVM.AllowSaveHistory != value) { uniVM.AllowSaveHistory = value; OnPropertyChanged(); } } }
    public bool StayRunningInBackground { get => uniVM.StayRunningInBackground; set { if (uniVM.StayRunningInBackground != value) { uniVM.StayRunningInBackground = value; OnPropertyChanged(); } } }
    public bool AutoOpenLastDirectory { get => uniVM.AutoOpenLastDirectory; set { if (uniVM.AutoOpenLastDirectory != value) { uniVM.AutoOpenLastDirectory = value; OnPropertyChanged(); } } }
    public bool RememberFilters { get => uniVM.RememberFilters; set { if (uniVM.RememberFilters != value) { uniVM.RememberFilters = value; OnPropertyChanged(); } } }
    public int CPUThreadsAllocated { get => uniVM.CPUThreadsAllocated; set { if (uniVM.CPUThreadsAllocated != value) { uniVM.CPUThreadsAllocated = value; OnPropertyChanged(); } } }
    public int MaxCPUThreads { get; } = Environment.ProcessorCount;
    public Color SelectedBackgroundColour { get => uniVM.SelectedBackgroundColour; set { if (uniVM.SelectedBackgroundColour != value) { uniVM.SelectedBackgroundColour = value; OnPropertyChanged(); } } }
    public Color SelectedPrimaryAccentColour { get => uniVM.SelectedPrimaryAccentColour; set { if (uniVM.SelectedPrimaryAccentColour != value) { uniVM.SelectedPrimaryAccentColour = value; OnPropertyChanged(); } } }
    public Color SelectedWallpaperCollectionColour { get => uniVM.SelectedWallpaperCollectionColour; set { if (uniVM.SelectedWallpaperCollectionColour != value) { uniVM.SelectedWallpaperCollectionColour = value; OnPropertyChanged(); } } }
    public Color SelectedPreviewBackgroundColour { get => uniVM.SelectedPreviewBackgroundColour; set { if (uniVM.SelectedPreviewBackgroundColour != value) { uniVM.SelectedPreviewBackgroundColour = value; OnPropertyChanged(); } } }
    public string AppNameVersion { get => uniVM.AppNameVersion; set { if (uniVM.AppNameVersion != value) { uniVM.AppNameVersion = value; OnPropertyChanged(); } } }


    // ------------------------------------------------------------------------------
    // funcs


    // img upload ==========================================================

    [RelayCommand] public void uploadClicked() => execImgUpload();
    private void execImgUpload()
    {
        multipleImgUpload();
    }
    private async void multipleImgUpload()
    {
        Window window = new Window();
        Debug.WriteLine("button clicked");
        ImageHelper imgHelper = new ImageHelper();
        ObservableCollection<Wallpaper> newFileList = await imgHelper.chooseMultipleWallpaperUpload(window);

        if (newFileList != null && newFileList.Count > 0)
        {
            foreach (Wallpaper wp in newFileList)
            {
                AllWallpapers.Add(wp);
                DisplayWallpaperList.Add(wp);
            }
        }

        applyAllFilters();
    }


    // select directory
    [RelayCommand] public void selectedDirectory() => execSelectDirec();
    private void execSelectDirec()
    {
        selectDirec(null); // execute it without any folder name in particular
    }

    [RelayCommand] public void navigateToParentDirec() => execNavigateToParentDirec();
    private void execNavigateToParentDirec()
    {
        if (CurrentSelectedDirectory != "No Directory Selected" || !string.IsNullOrEmpty(CurrentSelectedDirectory))
        {
            string parentDir = Path.GetDirectoryName(Path.TrimEndingDirectorySeparator(CurrentSelectedDirectory));
            if (!string.IsNullOrEmpty(parentDir) && Directory.Exists(parentDir))
            {
                selectDirec(parentDir);
            }
        }
    }

    public async void selectDirec(string direcChoice)
    {
        Window window = new Window();
        ImageHelper imgHelper = new ImageHelper();
        ObservableCollection<Wallpaper> directoryPathImageCollec = await imgHelper.getWallpaperListFromDirec(window, direcChoice);

        AllWallpapers.Clear();
        if (directoryPathImageCollec != null && directoryPathImageCollec.Count > 0)
        {
            foreach (var imgFile in directoryPathImageCollec)
            {
                AllWallpapers.Add(imgFile);
            }
        }

        DisplayWallpaperList.Clear();
        foreach (var wp in AllWallpapers)
        {
            DisplayWallpaperList.Add(wp);
        }

        applyAllFilters();

    }



    // filter ==============================================================

    [RelayCommand] public void filterClicked() => filterExec();
    private void filterExec()
    {
        IsFilterOpen = false;
        IsFilterOpen = true;
    }

    [RelayCommand] public void filterSearchCommand() => filterSearchExec();
    public void filterSearchExec()
    {
        applyAllFilters();
    }

    [RelayCommand] public void filterGroupSelectedCommand(string choice) => filterSelectExec(choice);
    private void filterSelectExec(string selectedChoice)
    {
        CurrentSortChoice = selectedChoice;
        applyAllFilters();
    }

    [RelayCommand] public void filterAspectRatioCommand(string choice) => filterAspectRatioExec(choice);
    private void filterAspectRatioExec(string selectedChoice)
    {
        CurrentAspectRatio = selectedChoice;
        applyAllFilters();
    }

    public void applyAllFilters()
    {
        ImageHelper imageHelper = new ImageHelper();
        var result = AllWallpapers.AsEnumerable();

        // aspect ratio filter
        if (CurrentAspectRatio != "All")
        {
            //  ensure folders are included nonetheless
            result = result.Where(wp =>
                (wp.IsDirectory ?? false) || 
                imageHelper.GetAspectRatio(wp.ImageWidth, wp.ImageHeight) == CurrentAspectRatio
            );

        }

        // search filter
        if (!string.IsNullOrEmpty(FilterSearchText))
        {
            result = result.Where(wp =>
                wp.Name != null &&
                wp.Name.StartsWith(FilterSearchText, StringComparison.OrdinalIgnoreCase)
            );
        }


        // showfolder filter
        if (ShowFolders == false)
        {
            result = result.Where(wp => wp.IsDirectory == false);
        }

        var folderList = result.Where(wp => wp.IsDirectory == true);
        var imageList = result.Where(wp => wp.IsDirectory == false);
        // name/date/size filter
        switch (CurrentSortChoice)
        {
            case "Name":
                folderList = folderList.OrderBy(wp => wp.Name, StringComparer.OrdinalIgnoreCase);
                imageList = imageList.OrderBy(wp => wp.Name, StringComparer.OrdinalIgnoreCase);
                result = folderList.Concat(imageList); // combine
                break;
            case "Date":
                folderList = folderList.OrderByDescending(wp => wp.Date);
                imageList = imageList.OrderByDescending(wp => wp.Date);
                result = folderList.Concat(imageList); // combine
                break;
            case "Size":
                folderList = folderList.OrderBy(wp => wp.Name, StringComparer.OrdinalIgnoreCase);
                imageList = imageList.OrderByDescending(wp => (wp.ImageWidth ?? 0) * (wp.ImageHeight ?? 0));
                result = folderList.Concat(imageList); // combine
                break;
            case "Colour":
                folderList = folderList.OrderBy(wp => wp.Name, StringComparer.OrdinalIgnoreCase);
                // imageList = imageList.OrderBy(wp => wp.ColourCategory.Color.ToHsl().H );
                imageList = imageList.OrderBy(wp => wp.ColourCategory);
                result = folderList.Concat(imageList); // combine
                break;
        }


        // update DisplayWallpaperList
        DisplayWallpaperList.Clear();
        foreach (var wp in result)
        {
            DisplayWallpaperList.Add(wp);
        }
    }








    // image clicking ============================================================

    private readonly SemaphoreSlim imageTappedSemaphore = new SemaphoreSlim(1, 1);

    public async Task ImageTapped(Wallpaper wallpaper)
    {
        await imageTappedSemaphore.WaitAsync();

        try
        {

            if (wallpaper.IsDirectory == true)
            {
                Debug.WriteLine("folder tapped");
            }
            else
            {
                if (string.IsNullOrWhiteSpace(wallpaper.FilePath))
                {
                    Debug.WriteLine("ERROR - img FilePath does not exist");
                    return;
                }
                Debug.WriteLine(wallpaper.Name + " image tapped");
                LastSelectedWallpaper = wallpaper;
                CurrentWallpaperPreview = ImageHelper.GetBitmapFromPath(LastSelectedWallpaper.FilePath);
                CurrentWallpaperName = wallpaper.Name;
                if (CurrentWallpaperPreview != null)
                {
                    CurrentWallpaperSize = CurrentWallpaperPreview.Size.Width.ToString() + " x " + CurrentWallpaperPreview.Size.Height.ToString();
                }
                

                // set preview monitors to "unclicked"
                foreach (var mon in MonitorList)
                {
                    mon.FillColour = "Navy";
                }

                // disable set button
                // DO NOT init mainwindow since it bugs out gallery
                SetBackgroundButtonEnabled = false;

                // disable dropdown
                StyleDropdownEnabled = false;

            }

        }
        finally
        {
            imageTappedSemaphore.Release();
        }

    }


    public async Task ImageDoubleTapped(Wallpaper wallpaper)
    {

        if (wallpaper.IsDirectory == true)
        {
            // DisplayWallpaperList.Clear();
            selectDirec(wallpaper.FilePath);
        }
        else
        {
            Debug.WriteLine(wallpaper.Name + " image double tapped");

            if (MonitorList != null)
            {
                for (int i = 0; i < MonitorList.Count; i++)
                {
                    MonitorInfo monitor = MonitorList[i];
                    Debug.WriteLine("mmm" + monitor + monitor.CurrWallpaper + monitor.IsPrimary);
                    if (monitor == LastSelMonitor)
                    {
                        Debug.WriteLine("YES!!");
                        monitor.CurrWallpaper.ImageThumbnailBitmap = CurrentWallpaperPreview;

                        // replace in collec
                        MonitorList[i] = monitor;
                    }
                }
            }
        }

        

    }


    // image setting to background ============================================================

    // set all monitors to same wallpaper
    public async Task SetWallpaperWithoutCrop()
    {
        // if its null, that means either no monitors selected, or all monitors selected
        if (LastSelMonitor == null)
        {
            Debug.WriteLine("set all monitors");
            foreach (var mon in MonitorList)
            {
                SetWallpaperHelper.SetWallpaper(LastSelectedWallpaper.FilePath, SelectedWallpaperStyle, mon.MonitorIdPath);
            }

            if (uniVM.AllowSaveHistory)
            {
                wallpaperHistoryHelper.AddToHistory(LastSelectedWallpaper.FilePath);
            }
        }
        else
        {
            string firstMonitorId = LastSelMonitor.MonitorIdPath;
            Debug.WriteLine("Using monitor ID: " + firstMonitorId);

            SetWallpaperHelper.SetWallpaper(LastSelectedWallpaper.FilePath, SelectedWallpaperStyle, firstMonitorId);

            if (uniVM.AllowSaveHistory)
            {
                wallpaperHistoryHelper.AddToHistory(LastSelectedWallpaper.FilePath);
            }
        }
    }

    // set background of a single monitor with specific area selected
    public async Task SetWallpaperWithCrop(string imagePath, string monitorId, int x, int y, int width, int height)
    {
        try
        {
            // load og img
            using var skiaImage = SkiaSharp.SKBitmap.Decode(imagePath);

            // get rect for area cropping
            var cropRect = new SkiaSharp.SKRectI(x, y, x + width, y + height);

            // check crop bounds
            cropRect.Intersect(new SkiaSharp.SKRectI(0, 0, skiaImage.Width, skiaImage.Height));

            // crop img
            using var croppedImage = new SkiaSharp.SKBitmap(cropRect.Width, cropRect.Height);
            using (var canvas = new SkiaSharp.SKCanvas(croppedImage))
            {
                canvas.DrawBitmap(skiaImage, cropRect, new SkiaSharp.SKRect(0, 0, cropRect.Width, cropRect.Height));
            }

            // save cropped img to temp file
            string tempPath = Path.Combine(Path.GetTempPath(), "croppedWallpaper.png");
            using (var stream = File.OpenWrite(tempPath))
            {
                croppedImage.Encode(stream, SkiaSharp.SKEncodedImageFormat.Png, 100);
            }

            // set background
            SetWallpaperHelper.SetWallpaper(tempPath, SelectedWallpaperStyle, monitorId);

            // save
            if (uniVM.AllowSaveHistory)
            {
                wallpaperHistoryHelper.AddToHistory(LastSelectedWallpaper.FilePath);
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error setting cropped wallpaper: {ex.Message}");
        }
    }



    // auto set wallpaper / "Wallpaper Queue" ============================================================
    [RelayCommand] public void addWallpaperToAutoSetCommand() => AddWallpaperToAutoSet();
    public void AddWallpaperToAutoSet()
    {
        Debug.WriteLine("AUTOSET");
        // TEMP: should i prevent the same wallpaper from being queued again?
        if (LastSelectedWallpaper != null)
        {
            uniVM.WallpaperQueue.Add(LastSelectedWallpaper);
            Debug.WriteLine("added " + lastSelectedWallpaper.Name);
        }
    }

    [RelayCommand] public void autoSetNavCommand() => AutoSetMenuNav();
    public void AutoSetMenuNav()
    {
        // switch preview for autoset
        IsPreviewVisible = !IsPreviewVisible;
        IsAutoSetVisible = !IsAutoSetVisible;

        // repeated clicks doesnt infinitely increase memory
        // GC.Collect();
        // GC.WaitForPendingFinalizers();
    }



    // monitors ============================================================

    // redetect monitors
    [RelayCommand] public void detectMonitorsButton() => DetectMonitors();
    private void DetectMonitors()
    {
        Window window = new Window();
        try
        {
            var monitors = MonitorHelper.GetMonitors(window);
            MonitorList.Clear();
            foreach (var mon in monitors)
            {
                MonitorList.Add(mon);
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine("ERROR --" + ex);
        }
    }

    // monitor rect tapped in UI - used for selecting monitor to set background of
    public async Task MonitorTapped(MonitorInfo monitor)
    {
        LastSelMonitor = monitor;
        monitor.FillColour = "#8ccd00";
        foreach (MonitorInfo mon in MonitorList)
        {
            if (mon != LastSelMonitor)
            {
                mon.FillColour = "Navy";
            }
        }
        StyleDropdownEnabled = false;

        SetBackgroundButtonEnabled = true;
    }

    public async Task AllMonitorsSelected()
    {
        LastSelMonitor = null;
        foreach (MonitorInfo mon in MonitorList)
        {
            mon.FillColour = "#8ccd00";
        }
    }


    // history ============================================================

    [RelayCommand] public void viewHistoryButton() => ViewHistory();
    private async void ViewHistory()
    {
        if (IsHistoryViewVisible == false)
        {
            HistoryWallpaperList.Clear();

            var historyList = await wallpaperHistoryHelper.GetHistoryWallpapers();
            foreach (var item in historyList)
            {
                HistoryWallpaperList.Add(item);
            }
        }

        // switch view
        IsHistoryViewVisible = !IsHistoryViewVisible;
        IsImageGalleryViewVisible = !IsImageGalleryViewVisible;

        // repeated clicks doesnt infinitely increase memory
        GC.Collect();
        GC.WaitForPendingFinalizers();

    }


    // settings ============================================================

    [RelayCommand] public void settingsButton() => navToSettings();
    public void navToSettings()
    {
        SettingsViewVisibility = !SettingsViewVisibility;
        MainGridVisibility = !MainGridVisibility;
    }


}
