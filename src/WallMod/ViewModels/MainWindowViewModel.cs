using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using CommunityToolkit.Mvvm.Input;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using WallMod.Helpers;
using WallMod.Helpers.History;
using WallMod.Models;
using WallMod.State;

namespace WallMod.ViewModels;

/**
 * Viewmodel for the main application functionality
 */
public partial class MainWindowViewModel : ViewModelBase
{

    private readonly UniversalAppStore uniVM;

    WallpaperHistoryHelper wallpaperHistoryHelper = new WallpaperHistoryHelper();
    SettingsHistoryHelper settingsHistoryHelper = new SettingsHistoryHelper();
    FavouritesHelper favouritesHelper = new FavouritesHelper();
    QueueHistoryHelper queueHistoryHelper = new QueueHistoryHelper();
    DirectoryHistoryHelper directoryHistoryHelper = new DirectoryHistoryHelper();
    private bool isRestoringQueue;


    public MainWindowViewModel(UniversalAppStore universalVM)
    {
        uniVM = universalVM;

        uniVM.PropertyChanged += (_, e) =>
        {
            // store changes reach the proxies here
            OnPropertyChanged(e.PropertyName);

            // hardcoded re-exec of selectDirec in order to change foldercount for all
            if (e.PropertyName == nameof(UniversalAppStore.FolderCountIncludesFolders)
                && CurrentSelectedDirecName != "Favourites"
                && Directory.Exists(CurrentSelectedDirectory))
            {
                selectDirec(CurrentSelectedDirectory);
            }
        };

        // set default img
        CurrentWallpaperPreview = new Bitmap(AssetLoader.Open(new Uri("avares://Wallmod/Assets/placeholder-icon.png")));

        // settings are already loaded by the store
        if (uniVM.AutoOpenLastDirectory)
        {
            string previousDir = settingsHistoryHelper.GetSettingEntry("LastChosenDirectory");
            if (Directory.Exists(previousDir))
            {
                selectDirec(previousDir);
            }
        }

        uniVM.WallpaperQueue.CollectionChanged += (s, e) =>
        {
            UpdateQueueCount();
            if (!isRestoringQueue)
            {
                queueHistoryHelper.SaveQueue(uniVM.WallpaperQueue);
                RefreshTileBadges();
            }
        };

        RestoreQueueFromHistory();
    }



    // UI variables ------------------------------------------------------------------------------

    // store proxies
    public bool MainGridVisibility { get => uniVM.MainGridVisibility; set => uniVM.MainGridVisibility = value; }
    public bool SettingsViewVisibility { get => uniVM.SettingsViewVisibility; set => uniVM.SettingsViewVisibility = value; }
    public bool StayRunningInBackground => uniVM.StayRunningInBackground; // used by App.axaml.cs
    public bool UpdateAvailableVisible => uniVM.UpdateAvailableVisible;
    public Color SelectedBackgroundColour => uniVM.SelectedBackgroundColour;
    public Color SelectedPrimaryAccentColour => uniVM.SelectedPrimaryAccentColour;
    public Color SelectedWallpaperCollectionColour => uniVM.SelectedWallpaperCollectionColour;
    public Color SelectedPreviewBackgroundColour => uniVM.SelectedPreviewBackgroundColour;



    // enlarge ============================================================
    // full-res image, shown once it's decoded (null = closed)

    private Bitmap? enlargedWallpaper;
    public Bitmap? EnlargedWallpaper { get => enlargedWallpaper; set { if (SetProperty(ref enlargedWallpaper, value)) OnPropertyChanged(nameof(MainGridOpacity)); } }

    public double MainGridOpacity => EnlargedWallpaper != null ? 0.01 : 1;

    public bool EnlargeRequested { get; private set; }

    public async Task OpenEnlarged()
    {
        if (EnlargeRequested || LastSelectedWallpaper?.FilePath is not string path || !File.Exists(path)) return;
        EnlargeRequested = true;

        try
        {
            var fullRes = await Task.Run(() => new Bitmap(path));
            if (EnlargeRequested && EnlargedWallpaper == null) EnlargedWallpaper = fullRes;
            else fullRes.Dispose(); // closed while loading
        }
        catch (Exception ex)
        {
            AppStorageHelper.LogCrash(ex);
        }
    }

    // frees the full-res image
    public void CloseEnlarged()
    {
        EnlargeRequested = false;
        var old = EnlargedWallpaper;
        EnlargedWallpaper = null;
        old?.Dispose();
    }



    // hotkeys ==================================================================

    // - and + hotkeys (for zooming image gallery)
    [RelayCommand] public void HotkeyPlus() => ThumbnailZoomLevel = Math.Min(ThumbnailZoomLevel + 2.5, 300); // 300 is max zoom value
    [RelayCommand] public void HotkeyMinus() => ThumbnailZoomLevel = Math.Max(ThumbnailZoomLevel - 2.5, 50); // 50 is min zoom value



    // preview button states ============================================================
    // accent while on, transparent while off

    private Color favouriteButtonColour = Colors.Transparent;
    public Color FavouriteButtonColour { get => favouriteButtonColour; set => SetProperty(ref favouriteButtonColour, value); }

    private Color mirrorButtonColour = Colors.Transparent;
    public Color MirrorButtonColour { get => mirrorButtonColour; set => SetProperty(ref mirrorButtonColour, value); }

    // queue button badge
    public void UpdateQueueCount()
    {
        QueueCount = LastSelectedWallpaper == null ? 0 : uniVM.WallpaperQueue.Count(w => w.FilePath == LastSelectedWallpaper.FilePath);
    }

    public void UpdateFavouriteButtonColour()
    {
        bool isFav = LastSelectedWallpaper != null && favouritesHelper.IsFavourite(LastSelectedWallpaper.FilePath);
        FavouriteButtonColour = isFav ? SelectedPrimaryAccentColour : Colors.Transparent;
    }

    public void UpdateMirrorButtonColour()
    {
        MirrorButtonColour = originalBeforeMirror != null ? SelectedPrimaryAccentColour : Colors.Transparent;
    }



    // mirror preview img ============================================================

    [RelayCommand]
    public async Task mirrorPreviewCommand()
    {
        if (LastSelectedWallpaper == null || LastSelectedWallpaper.IsDirectory == true) return;

        if (originalBeforeMirror != null)
        {
            // un-mirror
            LastSelectedWallpaper = originalBeforeMirror;
            originalBeforeMirror = null;
        }
        else
        {
            string? mirroredPath = ImageHelper.GetMirroredCopy(LastSelectedWallpaper.FilePath);
            if (mirroredPath == null) return;

            // same name/size/date/colour as the original
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

        CurrentWallpaperPreview = await GetPreview(LastSelectedWallpaper);
        UpdateMirrorButtonColour();
    }



    // autoset queue ============================================================

    // Autoset button
    [RelayCommand] public void autoSetNavCommand() => IsAutoSetVisible = !IsAutoSetVisible;

    // loads the saved queue on startup
    private async void RestoreQueueFromHistory()
    {
        try
        {
            var saved = await Task.Run(() => queueHistoryHelper.LoadQueue());

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



    // settings ============================================================

    [RelayCommand]
    public void settingsButton()
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