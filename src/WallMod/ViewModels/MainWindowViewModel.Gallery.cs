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

    // gallery fill pacing: milliseconds per tile (0 = no pacing, show tiles the instant they decode)
    private const int FrameMs = 1;

    // all wallpapers from a directory (never bound in XAML, so a plain list)
    public List<Wallpaper> AllWallpapers { get; set; } = new List<Wallpaper>();

    // current display of wallpapers after filtering; replaced wholesale by applyAllFilters
    private ObservableCollection<Wallpaper> displayWallpaperList = new();
    public ObservableCollection<Wallpaper> DisplayWallpaperList { get => displayWallpaperList; set => SetProperty(ref displayWallpaperList, value); }

    // number next to the folder name; follows the Folder count setting like the per-folder badges
    public int GalleryCount => uniVM.FolderCountIncludesFolders ? DisplayWallpaperList.Count : DisplayWallpaperList.Count(w => w.IsDirectory != true);

    // list of history wallpapers
    public ObservableCollection<Wallpaper> HistoryWallpaperList => uniVM.HistoryWallpaperList;

    // list of recent directories (directory history)
    public ObservableCollection<DirectoryHistoryRecord> DirectoryHistoryList { get; } = new();

    // holds the untouched gallery object while the mirrored copy is selected (null = not mirrored)
    private Wallpaper? originalBeforeMirror;

    private Wallpaper lastSelectedWallpaper;
    public Wallpaper LastSelectedWallpaper { get => lastSelectedWallpaper; set => SetProperty(ref lastSelectedWallpaper, value); }

    private double thumbnailZoomLevel = 150;
    public double ThumbnailZoomLevel { get => thumbnailZoomLevel; set => SetProperty(ref thumbnailZoomLevel, value); }

    private bool isFilterOpen;
    public bool IsFilterOpen { get => isFilterOpen; set => SetProperty(ref isFilterOpen, value); }

    private bool isDirectoryHistoryOpen;
    public bool IsDirectoryHistoryOpen { get => isDirectoryHistoryOpen; set => SetProperty(ref isDirectoryHistoryOpen, value); }

    // store proxies: the store raises the change, the constructor passes it on
    public double ImgLoadProgress { get => uniVM.ImgLoadProgress; set => uniVM.ImgLoadProgress = value; }
    public string CurrentSelectedDirectory { get => uniVM.CurrentSelectedDirectory; set => uniVM.CurrentSelectedDirectory = value; }
    public string CurrentSelectedDirecName { get => uniVM.CurrentSelectedDirecName; set => uniVM.CurrentSelectedDirecName = value; }
    public bool IsHistoryViewVisible { get => uniVM.IsHistoryViewVisible; set => uniVM.IsHistoryViewVisible = value; }
    public bool IsImageGalleryViewVisible { get => uniVM.IsImageGalleryViewVisible; set => uniVM.IsImageGalleryViewVisible = value; }



    // filter ==============================================================
    // every filter re-runs applyAllFilters when it changes. the first four are saved settings, so they live in the store

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

    // at least as big as the largest monitor -> never upscaled anywhere
    private bool IsHighRes(Wallpaper wp) =>
        uniVM.MonitorList.Count == 0
        || (wp.ImageWidth >= uniVM.MonitorList.Max(m => m.Bounds.Width) && wp.ImageHeight >= uniVM.MonitorList.Max(m => m.Bounds.Height));

    // accent funnel while anything in the filter popup is off its default (sort = Name, show folders = on)
    public Color FilterIconColour =>
        CurrentSortChoice != "Name" || !ShowFolders || !string.IsNullOrEmpty(FilterSearchText) || CurrentAspectRatio != "All" || ColourFilter != "All" || OnlyFavourites || OnlyHighRes
            ? SelectedPrimaryAccentColour : Colors.White;

    [RelayCommand]
    public void filterClicked()
    {
        IsFilterOpen = false;
        IsFilterOpen = true;
    }

    // search / show-folders / aspect-ratio / colour filters for one tile (folders always pass the aspect + colour filters)
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
            case "Width": // widest (x divided by y); tiebreak by sorting by resolution afterwards
                folderList = folderList.OrderBy(wp => wp.Name, StringComparer.OrdinalIgnoreCase);
                imageList = imageList.OrderByDescending(wp => Math.Round((double)(wp.ImageWidth ?? 0) / Math.Max(1, wp.ImageHeight ?? 1), 2))
                .ThenByDescending(wp => (wp.ImageWidth ?? 0) * (wp.ImageHeight ?? 0));
                break;
        }

        galleryFill = FillGallery(folderList.Concat(imageList).ToList());
    }

    // the gallery refill running right now (tiles streaming in wait for it, so their order stays right)
    private Task galleryFill = Task.CompletedTask;
    private int galleryFillVersion;

    // every Add builds a tile on the UI thread, so do ~8ms of them per frame, then let input + drawing run.
    // building them all in one go is what froze the window; a newer call stops an older fill
    private async Task FillGallery(List<Wallpaper> ordered)
    {
        int version = ++galleryFillVersion;
        var display = new ObservableCollection<Wallpaper>();
        display.CollectionChanged += (_, _) => OnPropertyChanged(nameof(GalleryCount)); // tiles are added after the list is set
        DisplayWallpaperList = display;
        OnPropertyChanged(nameof(GalleryCount));
        var frame = Stopwatch.StartNew();

        foreach (var wp in ordered)
        {
            display.Add(wp);
            if (frame.ElapsedMilliseconds < 8) continue;

            await Dispatcher.Yield(); // input + rendering run first
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

    // folder tiles show straight away, then the images stream in
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

            var (folderTiles, imgFiles) = direcContents.Value;
            AllWallpapers = folderTiles;
            applyAllFilters();
            uniVM.CurrentSelectedDirectory = folderChoice;
            directoryHistoryHelper.AddToRecent(folderChoice);

            await StreamImages(imgHelper, imgFiles, token);
        }
        catch (OperationCanceledException) { } // user moved to another folder mid-load
        catch (Exception ex)
        {
            AppStorageHelper.LogCrash(ex);
        }
    }

    // decoding runs flat out in the background; tiles are *shown* one every FrameMs so they stream in smoothly
    private async Task StreamImages(ImageHelper imgHelper, List<FileInfo> imgFiles, CancellationToken token)
    {
        // Name/Date order is known from the file list, so images can arrive already sorted;
        // Size/Colour need every image decoded first, so those re-sort once at the end
        string loadOrder = CurrentSortChoice == "Date" ? "Date" : "Name";
        var imgFilePaths = (loadOrder == "Date"
                ? imgFiles.OrderByDescending(file => file.LastWriteTime)
                : imgFiles.OrderBy(file => file.Name, StringComparer.OrdinalIgnoreCase))
            .Select(file => file.FullName);

        int processedFiles = 0;
        uniVM.ImgLoadProgress = imgFiles.Count == 0 ? 100 : 0;

        // the clock keeps the real rate even though Windows' timer only wakes every ~15ms
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
            if (PassesFilters(wallpaper)) // re-checked: filters may have changed during the wait
            {
                while (!galleryFill.IsCompleted) await galleryFill; // a refill is mid-way: land after it, not inside it
                DisplayWallpaperList.Add(wallpaper);
            }

            processedFiles++;
            uniVM.ImgLoadProgress = (double)processedFiles / imgFiles.Count * 100;
        }

        // tiles streamed in loadOrder; Size/Colour (or a sort picked mid-load) need the full set -> sort once now
        if (CurrentSortChoice != loadOrder) applyAllFilters();
    }

    // cancels any load still running, so a slow old folder can't finish late and overwrite the newer view
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
            UpdateMirrorButtonColour(); // selecting from the gallery always exits mirror mode

            CurrentWallpaperPreview = await GetPreview(wallpaper);
            CurrentWallpaperName = wallpaper.Name;
            if (CurrentWallpaperPreview != null)
            {
                // preview may be decoded smaller, so the real size comes from the wallpaper itself
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

        var token = StartNewLoad(); // stops a directory load from landing on top of favourites
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
        // refresh on every open; folders deleted/moved since are skipped
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

    // each recent-directory row binds the generated openDirectoryHistoryCommand with its path
    [RelayCommand]
    public void openDirectoryHistory(string direc)
    {
        IsDirectoryHistoryOpen = false;
        selectDirec(direc);
    }

}