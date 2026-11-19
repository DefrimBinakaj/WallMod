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

    // styles for all monitors / one monitor (Custom Area = drag rect)
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

    // badge on the queue button
    private int queueCount;
    public int QueueCount { get => queueCount; set { if (SetProperty(ref queueCount, value)) OnPropertyChanged(nameof(QueueButtonTip)); } }
    public string QueueButtonTip => QueueCount > 0 ? $"Add To Queue (Q) · {QueueCount} already in queue" : "Add To Queue (Q)";

    // "Added to queue · #N" note beside the Autoset button
    private string queueAddedText = "";
    public string QueueAddedText { get => queueAddedText; set => SetProperty(ref queueAddedText, value); }

    private bool queueAddedShown;
    public bool QueueAddedShown { get => queueAddedShown; set => SetProperty(ref queueAddedShown, value); }

    public bool SetBackgroundButtonEnabled { get => uniVM.SetBackgroundButtonEnabled; set => uniVM.SetBackgroundButtonEnabled = value; }

    // All = every monitor, One = one monitor
    private bool allMonitorsMode;
    public bool AllMonitorsMode
    {
        get => allMonitorsMode;
        set
        {
            if (!SetProperty(ref allMonitorsMode, value)) return;
            if (!value) lastAllMonitorsStyle = SelectedWallpaperStyle ?? "Fill"; // remember it for next time

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
    // decoded at the pane's pixel width, not the full file

    // set by the view when the pane resizes
    private int previewPixelWidth = 1280;
    public int PreviewPixelWidth { get => previewPixelWidth; set { if (SetProperty(ref previewPixelWidth, value)) SharpenPreviewIfSoft(); } }

    // never wider than the image itself
    private int PreviewDecodeWidth(Wallpaper wp) => Math.Min(wp.ImageWidth ?? PreviewPixelWidth, PreviewPixelWidth);

    // recent previews, so going back is instant
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

            if (previewCache.Count >= MaxCachedPreviews) previewCache.Clear();
            return previewCache[wp.FilePath] = bitmap;
        });
    }

    // decode the neighbours ahead of time
    private void PrefetchNeighbours(Wallpaper wp)
    {
        int i = DisplayWallpaperList.IndexOf(wp);
        foreach (int n in new[] { i + 1, i - 1 })
        {
            if (i >= 0 && n >= 0 && n < DisplayWallpaperList.Count && DisplayWallpaperList[n] is { IsDirectory: false } neighbour)
                _ = GetPreview(neighbour);
        }
    }

    // pane got wider: decode again once resizing stops
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

    // crop drawn with the DragRect, in image pixels (null = none)
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
            AppStorageHelper.LogCrash(ex);
        }
    }

    // set background of a single monitor with specific area selected
    public void SetWallpaperWithCrop()
    {
        if (LastSelectedWallpaper == null || pendingCrop == null) return;
        var crop = pendingCrop.Value;

        try
        {
            // the crop already matches the monitor's shape
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

        // a crop is drawn: queue a copy that carries it
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
            // no crop: autoset uses every monitor
            queueItem.MonitorBadge = MonitorHelper.BuildMiniMonitorBadge(uniVM.MonitorList, null, "#8ccd00");
        }

        uniVM.WallpaperQueue.Add(queueItem);
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

    // selected monitor = accent at ~55%
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

    // no monitor selected
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