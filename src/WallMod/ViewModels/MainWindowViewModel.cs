using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Platform.Storage;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using WallMod.Helpers;
using WallMod.Models;
using WallMod.State;

namespace WallMod.ViewModels;

/**
 * Viewmodel for the main application functionality
 */
public partial class MainWindowViewModel : ViewModelBase
{

    private readonly UniversalAppStore uniVM = App.Services!.GetRequiredService<UniversalAppStore>();

    WallpaperHistoryHelper wallpaperHistoryHelper = new WallpaperHistoryHelper();
    SettingsHistoryHelper settingsHistoryHelper = new SettingsHistoryHelper();
    FavouritesHelper favouritesHelper = new FavouritesHelper();
    QueueHistoryHelper queueHistoryHelper = new QueueHistoryHelper();
    private bool isRestoringQueue;

    // cancels the running gallery load whenever a new one starts
    private CancellationTokenSource? loadCts;

    // gallery fill pacing: milliseconds per tile (0 = no pacing, show tiles the instant they decode)
    private const int FrameMs = 1;

    // all wallpapers from a directory (never bound in XAML, so a plain list)
    public List<Wallpaper> AllWallpapers { get; set; } = new List<Wallpaper>();

    // current display of wallpapers after filtering; replaced wholesale by applyAllFilters
    private ObservableCollection<Wallpaper> displayWallpaperList = new ObservableCollection<Wallpaper>();
    public ObservableCollection<Wallpaper> DisplayWallpaperList { get => displayWallpaperList; set => SetProperty(ref displayWallpaperList, value); }

    // set of styles available (eg. fill, fit, tile, etc)
    public ObservableCollection<string> WallpaperStyleList { get; set; }

    // list of all current monitors of pc
    public ObservableCollection<MonitorInfo> MonitorList => uniVM.MonitorList;

    // list of history wallpapers
    public ObservableCollection<Wallpaper> HistoryWallpaperList => uniVM.HistoryWallpaperList;

    // holds the untouched gallery object while the mirrored copy is selected (null = not mirrored)
    private Wallpaper? originalBeforeMirror;


    public MainWindowViewModel(UniversalAppStore universalVM)
    {

        uniVM = universalVM;

        uniVM.PropertyChanged += (_, e) =>
        {
            // mirror every store change onto the same-named proxy here (theme colours, view visibility, progress, ...)
            OnPropertyChanged(e.PropertyName);

            // hardcoded re-exec of selectDirec in order to change foldercount for all
            if (e.PropertyName == nameof(UniversalAppStore.FolderCountIncludesFolders)
                && CurrentSelectedDirecName != "Favourites"
                && Directory.Exists(CurrentSelectedDirectory))
            {
                selectDirec(CurrentSelectedDirectory);
            }
        };

        WallpaperStyleList = new ObservableCollection<string> { "Fill", "Fit", "Stretch", "Tile", "Center", "Span" };
        SelectedWallpaperStyle = WallpaperStyleList[0];

        // set default img and values
        CurrentWallpaperPreview = new Bitmap(AssetLoader.Open(new Uri("avares://Wallmod/Assets/placeholder-icon.png")));
        CurrentWallpaperName = "Name";
        currentWallpaperSize = "Resolution";

        bool boolSetting(string key) => bool.Parse(settingsHistoryHelper.GetSettingEntry(key));
        Color colourSetting(string key) => Color.Parse(settingsHistoryHelper.GetSettingEntry(key));

        uniVM.AllowSaveHistory = boolSetting("AllowSaveHistory");
        uniVM.StayRunningInBackground = boolSetting("StayRunningInBackground");
        uniVM.FolderCountIncludesFolders = boolSetting("FolderCount");
        uniVM.AutoOpenLastDirectory = boolSetting("AutoOpenLastChosenDirectoryOnAppStart");
        uniVM.RememberFilters = boolSetting("RememberFilterSettings");
        uniVM.RememberThumbnailZoomLevel = boolSetting("RememberThumbnailZoomLevel");
        uniVM.CPUThreadsAllocated = int.Parse(settingsHistoryHelper.GetSettingEntry("CPUThreadsAllocated"));

        if (uniVM.AutoOpenLastDirectory == true)
        {
            string previousDir = settingsHistoryHelper.GetSettingEntry("LastChosenDirectory");
            if (Directory.Exists(previousDir))
            {
                selectDirec(previousDir);
            }
        }

        if (uniVM.RememberFilters == true)
        {
            uniVM.FilterSearchText = settingsHistoryHelper.GetSettingEntry("SearchFilter");
            uniVM.ShowFolders = boolSetting("ShowFoldersFilter");
            uniVM.CurrentSortChoice = settingsHistoryHelper.GetSettingEntry("ImgPropertySort");
            uniVM.CurrentAspectRatio = settingsHistoryHelper.GetSettingEntry("AspectRatioFilter");
            applyAllFilters();
        }

        uniVM.SelectedBackgroundColour = colourSetting("SelectedBackgroundColour");
        uniVM.SelectedPrimaryAccentColour = colourSetting("SelectedPrimaryAccentColour");
        uniVM.SelectedWallpaperCollectionColour = colourSetting("SelectedWallpaperCollectionColour");
        uniVM.SelectedPreviewBackgroundColour = colourSetting("SelectedPreviewBackgroundColour");
        uniVM.changeFluentColour();

        uniVM.WallpaperQueue.CollectionChanged += (s, e) =>
        {
            UpdateAutoSetButtonColour();
            if (!isRestoringQueue)
            {
                queueHistoryHelper.SaveQueue(uniVM.WallpaperQueue); // add/remove/reorder/clear all persist
                RefreshTileBadges();
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
    public Bitmap CurrentWallpaperPreview { get => currentWallpaperPreview; set => SetProperty(ref currentWallpaperPreview, value); }

    private String currentWallpaperName;
    public String CurrentWallpaperName { get => currentWallpaperName; set => SetProperty(ref currentWallpaperName, value); }

    private String currentWallpaperSize;
    public String CurrentWallpaperSize { get => currentWallpaperSize; set => SetProperty(ref currentWallpaperSize, value); }

    public double ImgLoadProgress { get => uniVM.ImgLoadProgress; set { if (uniVM.ImgLoadProgress != value) { uniVM.ImgLoadProgress = value; OnPropertyChanged(nameof(uniVM.ImgLoadProgress)); } } }

    public string CurrentSelectedDirectory { get => uniVM.CurrentSelectedDirectory; set { if (uniVM.CurrentSelectedDirectory != value) { uniVM.CurrentSelectedDirectory = value; OnPropertyChanged(nameof(uniVM.CurrentSelectedDirectory)); } } }
    public string CurrentSelectedDirecName { get => uniVM.CurrentSelectedDirecName; set { if (uniVM.CurrentSelectedDirecName != value) { uniVM.CurrentSelectedDirecName = value; OnPropertyChanged(nameof(uniVM.CurrentSelectedDirecName)); } } }

    private string selectedWallpaperStyle = "Fill";
    public string SelectedWallpaperStyle { get => selectedWallpaperStyle; set => SetProperty(ref selectedWallpaperStyle, value); }

    private double thumbnailZoomLevel = 150;
    public double ThumbnailZoomLevel { get => thumbnailZoomLevel; set => SetProperty(ref thumbnailZoomLevel, value); }

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

    private Color mirrorButtonColour = Colors.Transparent;
    public Color MirrorButtonColour { get => mirrorButtonColour; set => SetProperty(ref mirrorButtonColour, value); }


    public bool MainGridVisibility { get => uniVM.MainGridVisibility; set { if (uniVM.MainGridVisibility != value) { uniVM.MainGridVisibility = value; OnPropertyChanged(); } } }
    public bool SettingsViewVisibility { get => uniVM.SettingsViewVisibility; set { if (uniVM.SettingsViewVisibility != value) { uniVM.SettingsViewVisibility = value; OnPropertyChanged(); } } }
    public bool SetBackgroundButtonEnabled { get => uniVM.SetBackgroundButtonEnabled; set { if (uniVM.SetBackgroundButtonEnabled != value) { uniVM.SetBackgroundButtonEnabled = value; OnPropertyChanged(); } } }


    private bool isFilterOpen;
    public bool IsFilterOpen { get => isFilterOpen; set => SetProperty(ref isFilterOpen, value); }

    // filter setters re-run applyAllFilters whenever they actually change
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

    [RelayCommand]
    public async Task uploadClicked()
    {
        var result = await new Window().StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            FileTypeFilter = new List<FilePickerFileType> { FilePickerFileTypes.ImageAll },
            AllowMultiple = true
        });

        ImageHelper imgHelper = new ImageHelper();
        foreach (var img in result)
        {
            string selectedFilePath = img.Path.LocalPath;
            AllWallpapers.Add(await Task.Run(() => imgHelper.getWallpaperObjectFromPath(selectedFilePath))); // decode off the UI thread
        }

        RefreshTileBadges();
        applyAllFilters();
    }


    // select directory ==========================================================

    [RelayCommand] public void selectedDirectory() => selectDirec(null); // no folder in particular -> picker

    [RelayCommand]
    public void navigateToParentDirec()
    {
        string currentDir = Path.TrimEndingDirectorySeparator(CurrentSelectedDirectory);

        // back from favourites -> reopen the folder favourites was opened from; otherwise -> parent folder
        string? parentDir = CurrentSelectedDirecName == "Favourites" ? currentDir : Path.GetDirectoryName(currentDir);
        if (!string.IsNullOrEmpty(parentDir) && Directory.Exists(parentDir))
        {
            selectDirec(parentDir);
        }
    }

    public async void selectDirec(string? direcChoice)
    {
        try
        {
            string? folderChoice = direcChoice ?? await FileExplorerHelper.PickFolder("Select an Image Directory");
            if (folderChoice == null) return; // picker cancelled - leave the current gallery (and any running load) alone

            var token = StartNewLoad();
            uniVM.CurrentSelectedDirectory = " Loading...";

            ImageHelper imgHelper = new ImageHelper();
            var direcContents = await Task.Run(() => imgHelper.getDirecContents(folderChoice));
            if (token.IsCancellationRequested) return; // user already moved on

            if (direcContents == null) // no access to folder
            {
                AllWallpapers = new List<Wallpaper>();
                applyAllFilters();
                return;
            }

            // folder tiles show straight away; images stream in after them
            var (folderTiles, imgFiles) = direcContents.Value;
            AllWallpapers = folderTiles;
            applyAllFilters();
            uniVM.CurrentSelectedDirectory = folderChoice;

            // Name/Date order is known from the file list, so images can arrive already sorted;
            // Size/Colour need every image decoded first, so those re-sort once at the end
            string loadOrder = CurrentSortChoice == "Date" ? "Date" : "Name";
            var imgFilePaths = (loadOrder == "Date"
                    ? imgFiles.OrderByDescending(file => file.LastWriteTime)
                    : imgFiles.OrderBy(file => file.Name, StringComparer.OrdinalIgnoreCase))
                .Select(file => file.FullName);

            int totalFiles = imgFiles.Count;
            int processedFiles = 0;
            uniVM.ImgLoadProgress = totalFiles == 0 ? 100 : 0;

            // decoding runs flat out in the background; tiles are *shown* one every FrameMs so they stream in smoothly.
            // the clock keeps that real rate even though Windows' timer only wakes every ~15ms
            var revealClock = Stopwatch.StartNew();
            long nextRevealAt = 0;

            RefreshTileBadges(); // favourites file read once per load, not once per tile

            await foreach (var wallpaper in imgHelper.LoadWallpapersInOrder(imgFilePaths, token))
            {
                // wait for this tile's turn (hidden tiles skip the wait)
                if (PassesFilters(wallpaper))
                {
                    long now = revealClock.ElapsedMilliseconds;
                    if (now - nextRevealAt > 30) nextRevealAt = now; // coming out of a stall (eg. one slow image): stream the backlog, don't dump it
                    if (nextRevealAt > now) await Task.Delay((int)(nextRevealAt - now), token);
                    nextRevealAt += FrameMs;
                }

                if (token.IsCancellationRequested) return; // another folder / favourites owns the gallery now

                SetTileBadges(wallpaper);
                AllWallpapers.Add(wallpaper);
                if (PassesFilters(wallpaper)) DisplayWallpaperList.Add(wallpaper); // re-checked: filters may have changed during the wait

                processedFiles++;
                uniVM.ImgLoadProgress = (double)processedFiles / totalFiles * 100;
            }

            // tiles streamed in loadOrder; Size/Colour (or a sort picked mid-load) need the full set -> sort once now
            if (CurrentSortChoice != loadOrder) applyAllFilters();
        }
        catch (OperationCanceledException) { } // user moved to another folder mid-load
        catch (Exception ex)
        {
            AppStorageHelper.LogCrash(ex);
        }
    }

    // cancels any load still running, so a slow old folder can't finish late and overwrite the newer view
    private CancellationToken StartNewLoad()
    {
        loadCts?.Cancel();
        loadCts = new CancellationTokenSource();
        return loadCts.Token;
    }

    private static async Task<string?> PickFolder()
    {
        var folderOpenPick = await new Window().StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = "Select an Image Directory",
            AllowMultiple = false
        });

        return folderOpenPick.Count > 0 ? folderOpenPick[0].Path.LocalPath : null;
    }



    // filter ==============================================================

    [RelayCommand] // in axaml, itll be referenced as filterClicked
    public void filterClicked()
    {
        IsFilterOpen = false;
        IsFilterOpen = true;
    }

    // in axaml: filterGroupSelectedCommand / filterAspectRatioCommand (the setters re-filter)
    [RelayCommand] public void filterGroupSelected(string selectedChoice) => CurrentSortChoice = selectedChoice;
    [RelayCommand] public void filterAspectRatio(string selectedChoice) => CurrentAspectRatio = selectedChoice;

    // search / show-folders / aspect-ratio filters for one tile (folders always pass the aspect filter)
    private bool PassesFilters(Wallpaper wp) =>
        (ShowFolders || wp.IsDirectory == false)
        && (string.IsNullOrEmpty(FilterSearchText) || (wp.Name != null && wp.Name.StartsWith(FilterSearchText, StringComparison.OrdinalIgnoreCase)))
        && (CurrentAspectRatio == "All" || wp.IsDirectory == true || ImageHelper.GetAspectRatio(wp.ImageWidth, wp.ImageHeight) == CurrentAspectRatio);

    public void applyAllFilters()
    {
        var result = AllWallpapers.Where(PassesFilters).ToList();

        // name/date/size/colour sort; folders always listed before images
        var folderList = result.Where(wp => wp.IsDirectory == true);
        var imageList = result.Where(wp => wp.IsDirectory == false);
        switch (CurrentSortChoice)
        {
            case "Name":
                folderList = folderList.OrderBy(wp => wp.Name, StringComparer.OrdinalIgnoreCase);
                imageList = imageList.OrderBy(wp => wp.Name, StringComparer.OrdinalIgnoreCase);
                break;
            case "Date":
                folderList = folderList.OrderByDescending(wp => wp.Date);
                imageList = imageList.OrderByDescending(wp => wp.Date);
                break;
            case "Size":
                folderList = folderList.OrderByDescending(wp => wp.FolderItemCount);
                imageList = imageList.OrderByDescending(wp => (wp.ImageWidth ?? 0) * (wp.ImageHeight ?? 0));
                break;
            case "Colour":
                folderList = folderList.OrderBy(wp => wp.Name, StringComparer.OrdinalIgnoreCase);
                imageList = imageList.OrderBy(wp => wp.ColourCategory);
                break;
        }

        // swap the whole list in at once: one reset for the ListBox instead of a Clear() plus an Add() per tile
        DisplayWallpaperList = new ObservableCollection<Wallpaper>(folderList.Concat(imageList));
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
            if (wallpaper.IsDirectory == true || string.IsNullOrWhiteSpace(wallpaper.FilePath)) return;

            LastSelectedWallpaper = wallpaper;
            originalBeforeMirror = null;
            UpdateMirrorButtonColour(); // selecting from the gallery always exits mirror mode

            CurrentWallpaperPreview = ImageHelper.GetBitmapFromPath(wallpaper.FilePath);
            CurrentWallpaperName = wallpaper.Name;
            if (CurrentWallpaperPreview != null)
            {
                CurrentWallpaperSize = CurrentWallpaperPreview.Size.Width + " x " + CurrentWallpaperPreview.Size.Height;
            }
        }
        finally
        {
            imageTappedSemaphore.Release();
        }

        UpdateAutoSetButtonColour();
        UpdateFavouriteButtonColour();
    }


    public void ImageDoubleTapped(Wallpaper wallpaper)
    {
        if (wallpaper.IsDirectory == true)
        {
            selectDirec(wallpaper.FilePath);
        }
        else if (LastSelMonitor?.CurrWallpaper != null)
        {
            LastSelMonitor.CurrWallpaper.ImageThumbnailBitmap = CurrentWallpaperPreview;
        }
    }


    public void UpdateAutoSetButtonColour()
    {
        bool inQueue = LastSelectedWallpaper != null
                       && uniVM.WallpaperQueue.Any(a => a.FilePath == LastSelectedWallpaper.FilePath);

        AutoSetButtonColour = inQueue ? SelectedPrimaryAccentColour : Colors.Transparent;
    }
    public void UpdateFavouriteButtonColour()
    {
        bool isFav = LastSelectedWallpaper != null
                     && favouritesHelper.IsFavourite(LastSelectedWallpaper.FilePath);

        FavouriteButtonColour = isFav ? SelectedPrimaryAccentColour : Colors.Transparent;
    }
    public void UpdateMirrorButtonColour()
    {
        MirrorButtonColour = originalBeforeMirror != null ? SelectedPrimaryAccentColour : Colors.Transparent;
    }


    // gallery tile corner badges (favourite / queued) ============================================================

    // path lookups, rebuilt by RefreshTileBadges; tiles streaming in mid-load read these per tile
    private HashSet<string> favouritePaths = new HashSet<string>();
    private HashSet<string> queuedPaths = new HashSet<string>();

    private void SetTileBadges(Wallpaper wp)
    {
        wp.IsFavourite = favouritePaths.Contains(wp.FilePath);
        wp.IsQueued = queuedPaths.Contains(wp.FilePath);
    }

    public void RefreshTileBadges()
    {
        favouritePaths = favouritesHelper.GetFavouritePaths().ToHashSet();
        queuedPaths = uniVM.WallpaperQueue.Select(w => w.FilePath).ToHashSet();
        foreach (var wp in AllWallpapers) SetTileBadges(wp);
    }


    // image setting to background ============================================================

    // no monitor selected = every monitor
    public void SetWallpaperWithoutCrop()
    {
        if (LastSelMonitor == null)
        {
            foreach (var mon in MonitorList)
            {
                SetWallpaperHelper.SetWallpaper(LastSelectedWallpaper.FilePath, SelectedWallpaperStyle, mon.MonitorIdPath);
            }
        }
        else
        {
            SetWallpaperHelper.SetWallpaper(LastSelectedWallpaper.FilePath, SelectedWallpaperStyle, LastSelMonitor.MonitorIdPath);
        }

        if (uniVM.AllowSaveHistory)
        {
            wallpaperHistoryHelper.AddToHistory(LastSelectedWallpaper.FilePath);
        }
    }

    // set background of a single monitor with specific area selected
    public void SetWallpaperWithCrop()
    {
        if (LastSelectedWallpaper == null || pendingCropX is not int x || pendingCropY is not int y
           || pendingCropW is not int w || pendingCropH is not int h || pendingCropMonitorId is not string monitorId) return;

        try
        {
            SetWallpaperHelper.SetWallpaperCropped(LastSelectedWallpaper.FilePath, SelectedWallpaperStyle, monitorId, x, y, w, h);

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
    [RelayCommand]
    public void openImageInExplorerCommand()
    {
        if (LastSelectedWallpaper == null || CurrentWallpaperPreview == null) return;
        new FileExplorerHelper().OpenFileInExplorer(LastSelectedWallpaper.FilePath);
    }



    // mirror preview img ============================================================

    [RelayCommand]
    public void mirrorPreviewCommand()
    {
        if (LastSelectedWallpaper == null || LastSelectedWallpaper.IsDirectory == true) return;

        if (originalBeforeMirror != null)
        {
            // currently mirrored -> restore the original gallery object
            LastSelectedWallpaper = originalBeforeMirror;
            originalBeforeMirror = null;
        }
        else
        {
            string? mirroredPath = ImageHelper.GetMirroredCopy(LastSelectedWallpaper.FilePath);
            if (mirroredPath == null) return;

            // build the mirrored stand-in; same dims/name/date/colour so UI text stays identical
            var mirroredThumbnail = new ImageHelper().getWallpaperObjectFromPath(mirroredPath).ImageThumbnailBitmap;

            originalBeforeMirror = LastSelectedWallpaper;
            LastSelectedWallpaper = new Wallpaper
            {
                FilePath = mirroredPath,
                Name = originalBeforeMirror.Name,
                Date = originalBeforeMirror.Date,
                IsDirectory = false,
                ImageWidth = originalBeforeMirror.ImageWidth,
                ImageHeight = originalBeforeMirror.ImageHeight,
                ImageThumbnailBitmap = mirroredThumbnail ?? originalBeforeMirror.ImageThumbnailBitmap,
                ColourCategory = originalBeforeMirror.ColourCategory,
            };
        }

        CurrentWallpaperPreview = ImageHelper.GetBitmapFromPath(LastSelectedWallpaper.FilePath);
        UpdateMirrorButtonColour();
    }



    // auto set wallpaper / "Wallpaper Queue" ============================================================

    [RelayCommand]
    public void addWallpaperToAutoSetCommand()
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
            queueItem.MonitorBadge = MonitorHelper.BuildMiniMonitorBadge(uniVM.MonitorList, null, "#8ccd00");
        }

        uniVM.WallpaperQueue.Add(queueItem); // CollectionChanged handler updates the button colour + saves
        Debug.WriteLine("autoset added: " + queueItem.Name);
    }

    [RelayCommand] public void autoSetNavCommand() => IsAutoSetVisible = !IsAutoSetVisible;


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
            RefreshTileBadges();
            ClearTempImages();
        }
    }



    // favourite ============================================================

    [RelayCommand]
    public void addWallpaperToFavouritesCommand()
    {
        if (LastSelectedWallpaper == null) return;

        favouritesHelper.ToggleFavourite(LastSelectedWallpaper.FilePath);
        UpdateFavouriteButtonColour();
        RefreshTileBadges();
    }

    [RelayCommand]
    public async Task viewFavouritesButton()
    {
        // clicking favourites again goes back to the folder
        if (CurrentSelectedDirecName == "Favourites")
        {
            navigateToParentDirec();
            return;
        }

        var token = StartNewLoad(); // stops a directory load from landing on top of favourites
        var favWallpapers = await favouritesHelper.GetFavouriteWallpapers();
        if (token.IsCancellationRequested) return;

        AllWallpapers = favWallpapers.ToList();
        CurrentSelectedDirecName = "Favourites";
        RefreshTileBadges();

        // filter/sort the master into the display list
        applyAllFilters();
    }



    // monitors ============================================================

    // selected monitor tile = accent at ~55% so the number on it stays readable
    private string SelectedMonitorFill => Color.FromArgb(140, SelectedPrimaryAccentColour.R, SelectedPrimaryAccentColour.G, SelectedPrimaryAccentColour.B).ToString();

    // monitor rect tapped in UI - used for selecting monitor to set background of
    public void MonitorTapped(MonitorInfo monitor)
    {
        LastSelMonitor = monitor;
        foreach (MonitorInfo mon in MonitorList)
        {
            mon.FillColour = mon == monitor ? SelectedMonitorFill : MonitorHelper.IdleFill;
        }
        StyleDropdownEnabled = false;
        SetBackgroundButtonEnabled = true;
    }

    public void AllMonitorsSelected()
    {
        LastSelMonitor = null;
        foreach (MonitorInfo mon in MonitorList)
        {
            mon.FillColour = SelectedMonitorFill;
        }
    }



    // history ============================================================

    [RelayCommand]
    public async Task viewHistoryButton()
    {
        if (IsHistoryViewVisible == false)
        {
            HistoryWallpaperList.Clear();
            foreach (var item in await wallpaperHistoryHelper.GetHistoryWallpapers())
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



    // clear temp imgs ============================================================
    // queue / favourites / history may still point at mirrored copies, so those are kept
    private void ClearTempImages()
    {
        try
        {
            var referenced = uniVM.WallpaperQueue.Select(w => w.FilePath)
                             .Concat(favouritesHelper.GetFavouritePaths())
                             .Concat(wallpaperHistoryHelper.LoadHistoryJson());
            ImageHelper.ClearTempImageFiles(referenced);
        }
        catch (Exception ex)
        {
            AppStorageHelper.LogCrash(ex);
        }
    }

}