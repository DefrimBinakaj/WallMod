using Avalonia.Media;
using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using WallMod.Helpers;
using WallMod.Models;

namespace WallMod.ViewModels;

public partial class MainWindowViewModel
{

    // set of styles available (eg. fill, fit, tile, etc)
    // styles are drawn into a monitor-sized image per monitor (same result on Windows + Linux) ; Custom Area = drag rect
    private static readonly string[] AllMonitorStyles = { "Fill", "Fit", "Stretch", "Tile", "Center", "Span" };
    private static readonly string[] OneMonitorStyles = { "Custom Area", "Fill", "Fit", "Stretch", "Tile", "Center" };
    private string lastAllMonitorsStyle = "Fill";

    private string[] wallpaperStyleList = OneMonitorStyles;
    public string[] WallpaperStyleList { get => wallpaperStyleList; set => SetProperty(ref wallpaperStyleList, value); }

    private string selectedWallpaperStyle = "Custom Area";
    public string SelectedWallpaperStyle { get => selectedWallpaperStyle; set => SetProperty(ref selectedWallpaperStyle, value); }

    // list of all current monitors of pc
    public ObservableCollection<MonitorInfo> MonitorList => uniVM.MonitorList;

    private MonitorInfo lastSelMonitor;
    public MonitorInfo LastSelMonitor { get => lastSelMonitor; set { if (SetProperty(ref lastSelMonitor, value)) OnPropertyChanged(nameof(SetButtonText)); } }

    private Bitmap currentWallpaperPreview;
    public Bitmap CurrentWallpaperPreview { get => currentWallpaperPreview; set => SetProperty(ref currentWallpaperPreview, value); }

    private string currentWallpaperName = "Name";
    public string CurrentWallpaperName { get => currentWallpaperName; set => SetProperty(ref currentWallpaperName, value); }

    private string currentWallpaperSize = "Resolution";
    public string CurrentWallpaperSize { get => currentWallpaperSize; set => SetProperty(ref currentWallpaperSize, value); }

    // resolution hover: aspect ratio + file size
    private string? currentWallpaperDetails;
    public string? CurrentWallpaperDetails { get => currentWallpaperDetails; set => SetProperty(ref currentWallpaperDetails, value); }

    private bool isAutoSetVisible;
    public bool IsAutoSetVisible { get => isAutoSetVisible; set => SetProperty(ref isAutoSetVisible, value); }

    // how many times the selected image is already in the queue (badge on the queue button)
    private int queueCount;
    public int QueueCount { get => queueCount; set { if (SetProperty(ref queueCount, value)) OnPropertyChanged(nameof(QueueButtonTip)); } }
    public string QueueButtonTip => QueueCount > 0 ? $"Add To Queue (Q) · {QueueCount} already in queue" : "Add To Queue (Q)";

    // "Added to queue · #N" beside the Autoset button for a few secs (text stays put while it fades out)
    private string queueAddedText = "";
    public string QueueAddedText { get => queueAddedText; set => SetProperty(ref queueAddedText, value); }

    private bool queueAddedShown;
    public bool QueueAddedShown { get => queueAddedShown; set => SetProperty(ref queueAddedShown, value); }

    public bool SetBackgroundButtonEnabled { get => uniVM.SetBackgroundButtonEnabled; set => uniVM.SetBackgroundButtonEnabled = value; }

    // All toggle on = set every monitor ; off = one monitor
    private bool allMonitorsMode;
    public bool AllMonitorsMode
    {
        get => allMonitorsMode;
        set
        {
            if (!SetProperty(ref allMonitorsMode, value)) return;
            if (!value) lastAllMonitorsStyle = SelectedWallpaperStyle ?? "Fill"; // remember the All style for next time

            WallpaperStyleList = value ? AllMonitorStyles : OneMonitorStyles;
            SelectedWallpaperStyle = value ? lastAllMonitorsStyle : "Custom Area";
            OnPropertyChanged(nameof(SetButtonText));
        }
    }

    // set button says what it'll do
    public string SetButtonText =>
        AllMonitorsMode ? "Set on all monitors"
        : LastSelMonitor != null ? $"Set on Monitor {LastSelMonitor.DisplayNumber}"
        : "Pick a monitor";



    // preview ==========================================================
    // decoded at the pane's real pixel width, not the file's (enlarge loads the real file itself):
    // jpegs decode straight at 1/2, 1/4 or 1/8 size, so a 4k image is ~4x less work in a typical pane

    // preview pane width in real pixels (set by the view on splitter / window / dpi changes)
    private int previewPixelWidth = 1280;
    public int PreviewPixelWidth { get => previewPixelWidth; set { if (SetProperty(ref previewPixelWidth, value)) SharpenPreviewIfSoft(); } }

    // capped at the image's own width so small images aren't upscaled
    private int PreviewDecodeWidth(Wallpaper wp) => Math.Min(wp.ImageWidth ?? PreviewPixelWidth, PreviewPixelWidth);

    // recent previews by path: going back to an image, or arrowing onto a prefetched one, is instant
    private readonly ConcurrentDictionary<string, Bitmap> previewCache = new();
    private const int MaxCachedPreviews = 24;

    private Task<Bitmap?> GetPreview(Wallpaper wp)
    {
        int width = PreviewDecodeWidth(wp);
        if (previewCache.TryGetValue(wp.FilePath, out var cached) && cached.PixelSize.Width >= width) return Task.FromResult<Bitmap?>(cached);

        return Task.Run<Bitmap?>(() =>
        {
            var bitmap = ImageHelper.GetBitmapFromPath(wp.FilePath, width);
            if (bitmap == null) return null;

            if (previewCache.Count >= MaxCachedPreviews) previewCache.Clear(); // same simple policy as the thumbnail cache
            return previewCache[wp.FilePath] = bitmap;
        });
    }

    // left / right neighbours decode in the background while you look at this one
    private void PrefetchNeighbours(Wallpaper wp)
    {
        int i = DisplayWallpaperList.IndexOf(wp);
        foreach (int n in new[] { i + 1, i - 1 })
        {
            if (i >= 0 && n >= 0 && n < DisplayWallpaperList.Count && DisplayWallpaperList[n] is { IsDirectory: false } neighbour)
                _ = GetPreview(neighbour);
        }
    }

    // pane got wider than the current decode -> re-decode once the splitter / window settles
    private int sharpenVersion;
    private async void SharpenPreviewIfSoft()
    {
        int version = ++sharpenVersion;
        await Task.Delay(250);
        if (version != sharpenVersion || LastSelectedWallpaper is not { IsDirectory: false } wp || CurrentWallpaperPreview == null) return;
        if (CurrentWallpaperPreview.PixelSize.Width >= PreviewDecodeWidth(wp)) return;

        var sharper = await GetPreview(wp);
        if (version == sharpenVersion && wp == LastSelectedWallpaper && sharper != null) CurrentWallpaperPreview = sharper;
    }



    // image setting to background ============================================================

    // crop currently outlined by the DragRect, in original-image pixels (null = none)
    private (int X, int Y, int W, int H, string MonitorId)? pendingCrop;
    public void SetPendingCrop(int x, int y, int w, int h, string monitorId) => pendingCrop = (x, y, w, h, monitorId);
    public void ClearPendingCrop() => pendingCrop = null;

    // no monitor selected = every monitor
    public void SetWallpaperWithoutCrop()
    {
        if (LastSelectedWallpaper == null) return;

        try
        {
            foreach (var mon in LastSelMonitor != null ? new[] { LastSelMonitor } : MonitorList.ToArray())
                SetWallpaperHelper.SetWallpaper(LastSelectedWallpaper.FilePath, SelectedWallpaperStyle, mon.MonitorIdPath);

            if (uniVM.AllowSaveHistory) wallpaperHistoryHelper.AddToHistory(LastSelectedWallpaper.FilePath);
        }
        catch (Exception ex)
        {
            AppStorageHelper.LogCrash(ex); // eg. a linux desktop command failed ; logged instead of crashing the app
        }
    }

    // set background of a single monitor with specific area selected
    public void SetWallpaperWithCrop()
    {
        if (LastSelectedWallpaper == null || pendingCrop == null) return;
        var crop = pendingCrop.Value;

        try
        {
            // crop already matches the monitor's aspect, so Fill is exact (Custom Area isn't an OS style)
            SetWallpaperHelper.SetWallpaperCropped(LastSelectedWallpaper.FilePath, "Fill", crop.MonitorId, crop.X, crop.Y, crop.W, crop.H);

            if (uniVM.AllowSaveHistory) wallpaperHistoryHelper.AddToHistory(LastSelectedWallpaper.FilePath);
        }
        catch (Exception ex)
        {
            AppStorageHelper.LogCrash(ex);
        }
    }

    // opens image in explorer
    [RelayCommand]
    public void openImageInExplorerCommand()
    {
        if (LastSelectedWallpaper == null || CurrentWallpaperPreview == null) return;
        new FileExplorerHelper().OpenFileInExplorer(LastSelectedWallpaper.FilePath);
    }



    // auto set wallpaper / "Wallpaper Queue" ============================================================

    [RelayCommand]
    public void addWallpaperToAutoSetCommand()
    {
        if (LastSelectedWallpaper == null) return;

        Wallpaper queueItem = LastSelectedWallpaper;

        // a crop is active (with a real size) -> queue a copy carrying the crop, so the gallery object stays untouched
        if (pendingCrop is { W: > 0, H: > 0 } crop)
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
                ImageThumbnailBitmap = ImageHelper.GetCroppedThumbnail(LastSelectedWallpaper.FilePath, crop.X, crop.Y, crop.W, crop.H)
                                       ?? LastSelectedWallpaper.ImageThumbnailBitmap,
                ColourCategory = LastSelectedWallpaper.ColourCategory,
                CropX = crop.X,
                CropY = crop.Y,
                CropWidth = crop.W,
                CropHeight = crop.H,
                CropMonitorId = crop.MonitorId,
                MonitorBadge = MonitorHelper.BuildMiniMonitorBadge(uniVM.MonitorList, crop.MonitorId, "#8ccd00"),
            };
        }
        else
        {
            // uncropped -> autoset applies it to every monitor, so the badge shows all lit
            queueItem.MonitorBadge = MonitorHelper.BuildMiniMonitorBadge(uniVM.MonitorList, null, "#8ccd00");
        }

        uniVM.WallpaperQueue.Add(queueItem); // CollectionChanged handler updates the badge + saves
        ShowQueueAdded(uniVM.WallpaperQueue.Count);
    }

    // a newer add restarts the clock
    private int queueAddedVersion;
    private async void ShowQueueAdded(int position)
    {
        QueueAddedText = $"Added to queue · #{position}";
        QueueAddedShown = true;

        int version = ++queueAddedVersion;
        await Task.Delay(3000);
        if (version == queueAddedVersion) QueueAddedShown = false;
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
        AllMonitorsMode = false;
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

    // nothing selected: every monitor idle and the set button off until one is picked
    public void UnselectAllMonitors()
    {
        LastSelMonitor = null;
        foreach (MonitorInfo mon in MonitorList)
        {
            mon.FillColour = MonitorHelper.IdleFill;
        }
        SetBackgroundButtonEnabled = false;
    }

}