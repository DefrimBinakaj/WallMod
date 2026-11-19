using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using WallMod.Helpers;
using WallMod.Helpers.History;
using WallMod.Models;

namespace WallMod.ViewModels;

public partial class MainWindowViewModel
{

    // cancels the running gallery load whenever a new one starts
    private CancellationTokenSource? loadCts;

    // ms between tiles appearing while a folder loads
    private const int FrameMs = 1;

    // every wallpaper in the current folder
    public List<Wallpaper> AllWallpapers { get; set; } = new List<Wallpaper>();

    // what the gallery shows (after filters)
    private ObservableCollection<Wallpaper> displayWallpaperList = new();
    public ObservableCollection<Wallpaper> DisplayWallpaperList { get => displayWallpaperList; set => SetProperty(ref displayWallpaperList, value); }

    // count beside the folder name
    public int GalleryCount => uniVM.FolderCountIncludesFolders ? DisplayWallpaperList.Count : DisplayWallpaperList.Count(w => w.IsDirectory != true);

    // list of history wallpapers
    public ObservableCollection<Wallpaper> HistoryWallpaperList => uniVM.HistoryWallpaperList;

    // list of recent directories (directory history)
    public ObservableCollection<DirectoryHistoryRecord> DirectoryHistoryList { get; } = new();

    // the original wallpaper while its mirrored copy is shown
    private Wallpaper? originalBeforeMirror;

    private Wallpaper lastSelectedWallpaper;
    public Wallpaper LastSelectedWallpaper { get => lastSelectedWallpaper; set => SetProperty(ref lastSelectedWallpaper, value); }

    private double thumbnailZoomLevel = 150;
    public double ThumbnailZoomLevel { get => thumbnailZoomLevel; set => SetProperty(ref thumbnailZoomLevel, value); }

    private bool isFilterOpen;
    public bool IsFilterOpen { get => isFilterOpen; set => SetProperty(ref isFilterOpen, value); }

    private bool isDirectoryHistoryOpen;
    public bool IsDirectoryHistoryOpen { get => isDirectoryHistoryOpen; set => SetProperty(ref isDirectoryHistoryOpen, value); }

    // store proxies
    public double ImgLoadProgress { get => uniVM.ImgLoadProgress; set => uniVM.ImgLoadProgress = value; }
    public string CurrentSelectedDirectory { get => uniVM.CurrentSelectedDirectory; set => uniVM.CurrentSelectedDirectory = value; }
    public string CurrentSelectedDirecName { get => uniVM.CurrentSelectedDirecName; set => uniVM.CurrentSelectedDirecName = value; }
    public bool IsHistoryViewVisible { get => uniVM.IsHistoryViewVisible; set => uniVM.IsHistoryViewVisible = value; }
    public bool IsImageGalleryViewVisible { get => uniVM.IsImageGalleryViewVisible; set => uniVM.IsImageGalleryViewVisible = value; }



    // filter ==============================================================
    // filters (each one re-runs applyAllFilters)

    public string FilterSearchText { get => uniVM.FilterSearchText; set { if (uniVM.FilterSearchText == value) return; uniVM.FilterSearchText = value; applyAllFilters(); } }
    public bool ShowFolders { get => uniVM.ShowFolders; set { if (uniVM.ShowFolders == value) return; uniVM.ShowFolders = value; applyAllFilters(); } }
    public string CurrentSortChoice { get => uniVM.CurrentSortChoice; set { if (uniVM.CurrentSortChoice == value) return; uniVM.CurrentSortChoice = value; applyAllFilters(); } }
    public string CurrentAspectRatio { get => uniVM.CurrentAspectRatio; set { if (uniVM.CurrentAspectRatio == value) return; uniVM.CurrentAspectRatio = value; applyAllFilters(); } }

    private string colourFilter = "All";
    public string ColourFilter { get => colourFilter; set { if (SetProperty(ref colourFilter, value)) applyAllFilters(); } }

    private bool onlyFavourites;
    public bool OnlyFavourites { get => onlyFavourites; set { if (SetProperty(ref onlyFavourites, value)) applyAllFilters(); } }

    private bool onlyHighRes;
    public bool OnlyHighRes { get => onlyHighRes; set { if (SetProperty(ref onlyHighRes, value)) applyAllFilters(); } }

    // at least as big as the biggest monitor
    private bool IsHighRes(Wallpaper wp) =>
        uniVM.MonitorList.Count == 0
        || (wp.ImageWidth >= uniVM.MonitorList.Max(m => m.Bounds.Width) && wp.ImageHeight >= uniVM.MonitorList.Max(m => m.Bounds.Height));

    // accent funnel when any filter isn't default
    public Color FilterIconColour =>
        CurrentSortChoice != "Name" || !ShowFolders || !string.IsNullOrEmpty(FilterSearchText) || CurrentAspectRatio != "All" || ColourFilter != "All" || OnlyFavourites || OnlyHighRes
            ? SelectedPrimaryAccentColour : Colors.White;

    [RelayCommand]
    public void filterClicked()
    {
        IsFilterOpen = false;
        IsFilterOpen = true;
    }

    // folders only get filtered by search and show folders
    private bool PassesFilters(Wallpaper wp) =>
        (ShowFolders || wp.IsDirectory == false)
        && (string.IsNullOrEmpty(FilterSearchText) || (wp.Name != null && wp.Name.Contains(FilterSearchText, StringComparison.OrdinalIgnoreCase)))
        && (CurrentAspectRatio == "All" || wp.IsDirectory == true || ImageHelper.GetAspectRatio(wp.ImageWidth, wp.ImageHeight) == CurrentAspectRatio)
        && (ColourFilter == "All" || wp.IsDirectory == true || ImageHelper.GetColourName(wp.ColourCategory) == ColourFilter)
        && (!OnlyFavourites || wp.IsDirectory == true || wp.IsFavourite)
        && (!OnlyHighRes || wp.IsDirectory == true || IsHighRes(wp));

    public void applyAllFilters()
    {
        OnPropertyChanged(nameof(FilterIconColour));
        var result = AllWallpapers.Where(PassesFilters).ToList();

        // folders always go first
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
            case "Width": // widest first, then by resolution
                folderList = folderList.OrderBy(wp => wp.Name, StringComparer.OrdinalIgnoreCase);
                imageList = imageList.OrderByDescending(wp => Math.Round((double)(wp.ImageWidth ?? 0) / Math.Max(1, wp.ImageHeight ?? 1), 2))
                .ThenByDescending(wp => (wp.ImageWidth ?? 0) * (wp.ImageHeight ?? 0));
                break;
        }

        galleryFill = FillGallery(folderList.Concat(imageList).ToList());
    }

    // the refill running right now
    private Task galleryFill = Task.CompletedTask;
    private int galleryFillVersion;

    // adds tiles ~8ms at a time so the window doesn't freeze
    private async Task FillGallery(List<Wallpaper> ordered)
    {
        int version = ++galleryFillVersion;
        var display = new ObservableCollection<Wallpaper>();
        display.CollectionChanged += (_, _) => OnPropertyChanged(nameof(GalleryCount)); // count follows the tiles as they're added
        DisplayWallpaperList = display;
        OnPropertyChanged(nameof(GalleryCount));
        var frame = Stopwatch.StartNew();

        foreach (var wp in ordered)
        {
            display.Add(wp);
            if (frame.ElapsedMilliseconds < 8) continue;

            await Dispatcher.Yield();
            if (version != galleryFillVersion) return;
            frame.Restart();
        }
    }



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
            AllWallpapers.Add(await Task.Run(() => imgHelper.getWallpaperObjectFromPath(selectedFilePath)));
        }

        RefreshTileBadges();
        applyAllFilters();
    }



    // select directory ==========================================================

    [RelayCommand] public void selectedDirectory() => selectDirec(null); // null = folder picker

    [RelayCommand]
    public void navigateToParentDirec()
    {
        string currentDir = Path.TrimEndingDirectorySeparator(CurrentSelectedDirectory);

        // from favourites, go back to the folder it was opened from
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
            if (folderChoice == null) return; // picker cancelled

            var token = StartNewLoad();
            uniVM.CurrentSelectedDirectory = " Loading...";

            ImageHelper imgHelper = new ImageHelper();
            var direcContents = await Task.Run(() => imgHelper.getDirecContents(folderChoice));
            if (token.IsCancellationRequested) return;

            if (direcContents == null) // no access to folder
            {
                AllWallpapers = new List<Wallpaper>();
                applyAllFilters();
                return;
            }

            var (folderTiles, imgFiles) = direcContents.Value;
            AllWallpapers = folderTiles;
            applyAllFilters();
            uniVM.CurrentSelectedDirectory = folderChoice;
            directoryHistoryHelper.AddToRecent(folderChoice);

            await StreamImages(imgHelper, imgFiles, token);
        }
        catch (OperationCanceledException) { } // moved to another folder
        catch (Exception ex)
        {
            AppStorageHelper.LogCrash(ex);
        }
    }

    // images decode in the background and show one every FrameMs
    private async Task StreamImages(ImageHelper imgHelper, List<FileInfo> imgFiles, CancellationToken token)
    {
        // Name/Date can load already sorted, other sorts re-sort at the end
        string loadOrder = CurrentSortChoice == "Date" ? "Date" : "Name";
        var imgFilePaths = (loadOrder == "Date"
                ? imgFiles.OrderByDescending(file => file.LastWriteTime)
                : imgFiles.OrderBy(file => file.Name, StringComparer.OrdinalIgnoreCase))
            .Select(file => file.FullName);

        int processedFiles = 0;
        uniVM.ImgLoadProgress = imgFiles.Count == 0 ? 100 : 0;

        var revealClock = Stopwatch.StartNew();
        long nextRevealAt = 0;

        RefreshTileBadges();

        await foreach (var wallpaper in imgHelper.LoadWallpapersInOrder(imgFilePaths, token))
        {
            if (PassesFilters(wallpaper))
            {
                long now = revealClock.ElapsedMilliseconds;
                if (now - nextRevealAt > 30) nextRevealAt = now; // catch up after a slow image
                if (nextRevealAt > now) await Task.Delay((int)(nextRevealAt - now), token);
                nextRevealAt += FrameMs;
            }

            if (token.IsCancellationRequested) return;

            SetTileBadges(wallpaper);
            AllWallpapers.Add(wallpaper);
            if (PassesFilters(wallpaper)) // filters may have changed while waiting
            {
                while (!galleryFill.IsCompleted) await galleryFill; // let a refill finish first so the order stays right
                DisplayWallpaperList.Add(wallpaper);
            }

            processedFiles++;
            uniVM.ImgLoadProgress = (double)processedFiles / imgFiles.Count * 100;
        }

        if (CurrentSortChoice != loadOrder) applyAllFilters();
    }

    // cancels the old load so it can't overwrite the new one
    private CancellationToken StartNewLoad()
    {
        loadCts?.Cancel();
        loadCts = new CancellationTokenSource();
        return loadCts.Token;
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
            UpdateMirrorButtonColour();

            CurrentWallpaperPreview = await GetPreview(wallpaper);
            CurrentWallpaperName = wallpaper.Name;
            if (CurrentWallpaperPreview != null)
            {
                // real size (the preview may be decoded smaller)
                CurrentWallpaperSize = wallpaper.ImageWidth + " x " + wallpaper.ImageHeight;

                string ratio = ImageHelper.GetAspectRatio(wallpaper.ImageWidth, wallpaper.ImageHeight);
                if (ratio == "none") ratio = $"{(double?)wallpaper.ImageWidth / wallpaper.ImageHeight:0.00}:1";
                CurrentWallpaperDetails = $"{ratio}  ·  {new FileInfo(wallpaper.FilePath).Length / 1048576.0:0.0} MB";
            }

            PrefetchNeighbours(wallpaper);
        }
        finally
        {
            imageTappedSemaphore.Release();
        }

        UpdateQueueCount();
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



    // gallery tile corner badges (favourite / queued) ============================================================

    // rebuilt by RefreshTileBadges
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



    // favourites / history / recent directories (left rail) =============================================

    [RelayCommand]
    public async Task viewFavouritesButton()
    {
        // clicking favourites again goes back to the folder
        if (CurrentSelectedDirecName == "Favourites")
        {
            navigateToParentDirec();
            return;
        }

        var token = StartNewLoad();
        var favWallpapers = await favouritesHelper.GetFavouriteWallpapers();
        if (token.IsCancellationRequested) return;

        AllWallpapers = favWallpapers.ToList();
        CurrentSelectedDirecName = "Favourites";
        RefreshTileBadges();
        applyAllFilters();
    }

    [RelayCommand]
    public async Task viewHistoryButton()
    {
        if (!IsHistoryViewVisible)
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

    [RelayCommand]
    public void directoryHistoryClicked()
    {
        // folders deleted since are skipped
        DirectoryHistoryList.Clear();
        ImageHelper imgHelper = new ImageHelper();
        foreach (var direc in directoryHistoryHelper.LoadRecentJson().Where(direc => direc != CurrentSelectedDirectory && Directory.Exists(direc)))
        {
            DirectoryHistoryList.Add(new DirectoryHistoryRecord(imgHelper.CountFolderItems(direc), direc));
        }

        // same reopen as filterClicked
        IsDirectoryHistoryOpen = false;
        IsDirectoryHistoryOpen = true;
    }

    // recent folder rows pass their path to openDirectoryHistoryCommand
    [RelayCommand]
    public void openDirectoryHistory(string direc)
    {
        IsDirectoryHistoryOpen = false;
        selectDirec(direc);
    }

}