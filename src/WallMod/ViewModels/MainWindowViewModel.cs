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
    FavouritesHelper favouritesHelper = new FavouritesHelper();
    QueueHistoryHelper queueHistoryHelper = new QueueHistoryHelper();
    private bool isRestoringQueue;

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


        // BANDAID FIX for gallery / history visibility bug
        uniVM.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(UniversalAppStore.IsHistoryViewVisible))
                OnPropertyChanged(nameof(IsHistoryViewVisible));
            if (e.PropertyName == nameof(UniversalAppStore.IsImageGalleryViewVisible))
                OnPropertyChanged(nameof(IsImageGalleryViewVisible));


            // hardcoded re-exec of selectDirec in order to change foldercount for all
            if (e.PropertyName == nameof(UniversalAppStore.FolderCountIncludesFolders))
            {
                if (CurrentSelectedDirecName != "Favourites"
                    && !string.IsNullOrEmpty(CurrentSelectedDirectory)
                    && Directory.Exists(CurrentSelectedDirectory))
                {
                    selectDirec(CurrentSelectedDirectory);
                }
            }
        };


        // set default img and values
        CurrentWallpaperPreview = new Bitmap(AssetLoader.Open(new Uri("avares://Wallmod/Assets/placeholder-icon.png")));
        CurrentWallpaperName = "Name";
        currentWallpaperSize = "Resolution";


        uniVM.AllowSaveHistory = bool.Parse(settingsHistoryHelper.GetSettingEntry("AllowSaveHistory"));
        uniVM.StayRunningInBackground = bool.Parse(settingsHistoryHelper.GetSettingEntry("StayRunningInBackground"));
        uniVM.FolderCountIncludesFolders = bool.Parse(settingsHistoryHelper.GetSettingEntry("FolderCount"));
        uniVM.AutoOpenLastDirectory = bool.Parse(settingsHistoryHelper.GetSettingEntry("AutoOpenLastChosenDirectoryOnAppStart"));
        uniVM.RememberFilters = bool.Parse(settingsHistoryHelper.GetSettingEntry("RememberFilterSettings"));
        uniVM.RememberThumbnailZoomLevel = bool.Parse(settingsHistoryHelper.GetSettingEntry("RememberThumbnailZoomLevel"));
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

        uniVM.WallpaperQueue.CollectionChanged += (s, e) =>
        {
            UpdateAutoSetButtonColour();
            if (!isRestoringQueue)
            {
                queueHistoryHelper.SaveQueue(uniVM.WallpaperQueue); // add/remove/reorder/clear all persist
            }
        };

        RestoreQueueFromHistory();
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

    private Color autoSetButtonColour = Colors.Transparent;
    public Color AutoSetButtonColour { get => autoSetButtonColour; set => SetProperty(ref autoSetButtonColour, value); }

    private Color favouriteButtonColour = Colors.Transparent;
    public Color FavouriteButtonColour { get => favouriteButtonColour; set => SetProperty(ref favouriteButtonColour, value); }

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

    public bool StayRunningInBackground { get => uniVM.StayRunningInBackground; set { if (uniVM.StayRunningInBackground != value) { uniVM.StayRunningInBackground = value; OnPropertyChanged(); } } }
    public Color SelectedBackgroundColour { get => uniVM.SelectedBackgroundColour; set { if (uniVM.SelectedBackgroundColour != value) { uniVM.SelectedBackgroundColour = value; OnPropertyChanged(); } } }
    public Color SelectedPrimaryAccentColour { get => uniVM.SelectedPrimaryAccentColour; set { if (uniVM.SelectedPrimaryAccentColour != value) { uniVM.SelectedPrimaryAccentColour = value; OnPropertyChanged(); } } }
    public Color SelectedWallpaperCollectionColour { get => uniVM.SelectedWallpaperCollectionColour; set { if (uniVM.SelectedWallpaperCollectionColour != value) { uniVM.SelectedWallpaperCollectionColour = value; OnPropertyChanged(); } } }
    public Color SelectedPreviewBackgroundColour { get => uniVM.SelectedPreviewBackgroundColour; set { if (uniVM.SelectedPreviewBackgroundColour != value) { uniVM.SelectedPreviewBackgroundColour = value; OnPropertyChanged(); } } }
    public string AppNameVersion { get => uniVM.AppNameVersion; set { if (uniVM.AppNameVersion != value) { uniVM.AppNameVersion = value; OnPropertyChanged(); } } }
    public bool UpdateAvailableVisible { get => uniVM.UpdateAvailableVisible; set { if (uniVM.UpdateAvailableVisible != value) { uniVM.UpdateAvailableVisible = value; OnPropertyChanged(); } } }


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
    private async void execNavigateToParentDirec()
    {
        // route back to prev dir if the user clicks back from favs
        if (CurrentSelectedDirecName == "Favourites" && !string.IsNullOrEmpty(CurrentSelectedDirectory))
        {
            selectDirec(Path.TrimEndingDirectorySeparator(CurrentSelectedDirectory));
        }
        // route to parent if the user is on any direc that isn't fav/null
        else if (CurrentSelectedDirectory != "No Directory Selected" || !string.IsNullOrEmpty(CurrentSelectedDirectory))
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

    [RelayCommand] // in axaml, itll be referenced as filterClicked
    public void filterClicked()
    {
        IsFilterOpen = false;
        IsFilterOpen = true;
    }

    [RelayCommand] // in axaml, itll be referenced as filterSearchCommand
    public void filterSearch()
    {
        applyAllFilters();
    }

    [RelayCommand] // in axaml, itll be referenced as filterGroupSelectedCommand
    public void filterGroupSelected(string selectedChoice)
    {
        CurrentSortChoice = selectedChoice;
        applyAllFilters();
    }

    [RelayCommand] // in axaml, itll be referenced as filterAspectRatioCommand
    public void filterAspectRatio(string selectedChoice)
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
                folderList = folderList.OrderByDescending(wp => wp.FolderItemCount);
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







    // hotkeys ==================================================================
    
    // - and + hotkeys (for zooming image gallery)
    [RelayCommand] public void HotkeyPlusClicked() => HotkeyPlus();
    public void HotkeyPlus()
    {
        ThumbnailZoomLevel = Math.Min(ThumbnailZoomLevel + 2.5, 300); // 300 is max zoom value
    }

    [RelayCommand] public void HotkeyMinusClicked() => HotkeyMinus();
    public void HotkeyMinus()
    {
        ThumbnailZoomLevel = Math.Max(ThumbnailZoomLevel - 2.5, 50); // 50 is min zoom value
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

            }

        }
        finally
        {
            imageTappedSemaphore.Release();
        }

        UpdateAutoSetButtonColour();
        UpdateFavouriteButtonColour();
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


    public void UpdateAutoSetButtonColour()
    {
        bool inQueue = LastSelectedWallpaper != null
                       && uniVM.WallpaperQueue.Any(a => a.FilePath == LastSelectedWallpaper.FilePath);

        AutoSetButtonColour = inQueue
            ? SelectedPrimaryAccentColour
            : Colors.Transparent;
    }
    public void UpdateFavouriteButtonColour()
    {
        bool isFav = LastSelectedWallpaper != null
                       && favouritesHelper.IsFavourite(LastSelectedWallpaper.FilePath);

        FavouriteButtonColour = isFav
            ? SelectedPrimaryAccentColour
            : Colors.Transparent;
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
            SetWallpaperHelper.SetWallpaperCropped(imagePath, SelectedWallpaperStyle, monitorId, x, y, width, height);

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

    // crop currently outlined by the DragRect, in original-image pixels (null = none)
    private int? pendingCropX, pendingCropY, pendingCropW, pendingCropH;
    private string? pendingCropMonitorId;

    public void SetPendingCrop(int x, int y, int w, int h, string monitorId)
    {
        pendingCropX = x; pendingCropY = y; pendingCropW = w; pendingCropH = h;
        pendingCropMonitorId = monitorId;
    }
    public void ClearPendingCrop()
    {
        pendingCropX = pendingCropY = pendingCropW = pendingCropH = null;
        pendingCropMonitorId = null;
    }



    // opens image in explorer
    [RelayCommand] public void openImageInExplorerCommand() => OpenImageInExplorer();
    private void OpenImageInExplorer()
    {
        if (LastSelectedWallpaper == null || CurrentWallpaperPreview == null) return;
        FileExporerHelper fileExporerHelper = new FileExporerHelper();
        fileExporerHelper.OpenFileInExplorer(LastSelectedWallpaper.FilePath);
    }


    // auto set wallpaper / "Wallpaper Queue" ============================================================
    [RelayCommand] public void addWallpaperToAutoSetCommand() => AddWallpaperToAutoSet();
    public void AddWallpaperToAutoSet()
    {
        if (LastSelectedWallpaper == null) return;

        Wallpaper queueItem = LastSelectedWallpaper;

        // a crop is active -> queue a copy carrying the crop, so the gallery object stays untouched
        if (pendingCropX is int cx && pendingCropY is int cy &&
            pendingCropW is int cw && pendingCropH is int ch && cw > 0 && ch > 0
            && pendingCropMonitorId is string cropMonId)
        {
            queueItem = new Wallpaper
            {
                FilePath = LastSelectedWallpaper.FilePath,
                Name = LastSelectedWallpaper.Name,
                Date = LastSelectedWallpaper.Date,
                IsDirectory = false,
                ImageWidth = LastSelectedWallpaper.ImageWidth,
                ImageHeight = LastSelectedWallpaper.ImageHeight,
                // queue shows what will actually be set; falls back to the full thumb if the crop render fails
                ImageThumbnailBitmap = ImageHelper.GetCroppedThumbnail(LastSelectedWallpaper.FilePath, cx, cy, cw, ch)
                                       ?? LastSelectedWallpaper.ImageThumbnailBitmap,
                ColourCategory = LastSelectedWallpaper.ColourCategory,
                CropX = cx,
                CropY = cy,
                CropWidth = cw,
                CropHeight = ch,
                CropMonitorId = cropMonId,
                MonitorBadge = MonitorHelper.BuildMiniMonitorBadge(uniVM.MonitorList, cropMonId, "#8ccd00"),
            };
        }
        else
        {
            // uncropped -> autoset applies it to every monitor, so the badge shows all lit
            queueItem.MonitorBadge = MonitorHelper.BuildMiniMonitorBadge(
                uniVM.MonitorList, null, "#8ccd00");
        }

        uniVM.WallpaperQueue.Add(queueItem);
        Debug.WriteLine("autoset added: " + queueItem.Name);
        UpdateAutoSetButtonColour();
    }
    [RelayCommand] public void autoSetNavCommand() => AutoSetMenuNav();
    public void AutoSetMenuNav()
    {
        // switch preview for autoset
        // IsPreviewVisible = !IsPreviewVisible;
        IsAutoSetVisible = !IsAutoSetVisible;
    }


    private async void RestoreQueueFromHistory()
    {
        try
        {
            var saved = await Task.Run(() => queueHistoryHelper.LoadQueue()); // thumbnails off the UI thread

            isRestoringQueue = true;
            foreach (var wp in saved)
            {
                var badge = MonitorHelper.BuildMiniMonitorBadge(uniVM.MonitorList, wp.CropMonitorId, "#8ccd00");
                if (badge.Count > 0)
                {
                    wp.MonitorBadge = badge;
                }
                uniVM.WallpaperQueue.Add(wp);
            }
        }
        catch (Exception ex)
        {
            AppStorageHelper.LogCrash(ex);
        }
        finally
        {
            isRestoringQueue = false;
        }
    }


    // favourite ============================================================
    [RelayCommand] public void addWallpaperToFavouritesCommand() => addWallpaperToFavourites();
    public void addWallpaperToFavourites()
    {
        if (LastSelectedWallpaper != null)
        {
            favouritesHelper.ToggleFavourite(lastSelectedWallpaper.FilePath);
            Debug.WriteLine("fav added: " + lastSelectedWallpaper.Name);
            UpdateFavouriteButtonColour();
        }
    }
    [RelayCommand]
    public async Task viewFavouritesButton() => await LoadFavourites();
    private async Task LoadFavourites()
    {

        if (CurrentSelectedDirecName == "Favourites")
        {
            execNavigateToParentDirec();
            return;
        }

        var favWallpapers = await favouritesHelper.GetFavouriteWallpapers();

        AllWallpapers.Clear();
        foreach (var wp in favWallpapers)
        {
            AllWallpapers.Add(wp);
        }

        CurrentSelectedDirecName = "Favourites";

        // filter/sort the master into the display list
        applyAllFilters();
    }

    // monitors ============================================================


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
