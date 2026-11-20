using Avalonia;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Themes.Fluent;
using System;
using System.Collections.ObjectModel;
using System.IO;
using WallMod.Helpers.History;
using WallMod.Models;
using WallMod.ViewModels;

namespace WallMod.State;

public class UniversalAppStore : ViewModelBase
{

    SettingsHistoryHelper settingsHistoryHelper = new SettingsHistoryHelper();


    // loads the saved settings (fields set directly so nothing gets saved back)
    public UniversalAppStore()
    {
        bool boolSetting(string key) => bool.Parse(settingsHistoryHelper.GetSettingEntry(key));
        Color colourSetting(string key) => Color.Parse(settingsHistoryHelper.GetSettingEntry(key));

        allowSaveHistory = boolSetting("AllowSaveHistory");
        stayRunningInBackground = boolSetting("StayRunningInBackground");
        folderCountIncludesFolders = boolSetting("FolderCount");
        autoOpenLastDirectory = boolSetting("AutoOpenLastChosenDirectoryOnAppStart");
        rememberFilters = boolSetting("RememberFilterSettings");
        rememberThumbnailZoomLevel = boolSetting("RememberThumbnailZoomLevel");
        cpuThreadsAllocated = int.Parse(settingsHistoryHelper.GetSettingEntry("CPUThreadsAllocated"));

        if (rememberFilters)
        {
            filterSearchText = settingsHistoryHelper.GetSettingEntry("SearchFilter");
            showFolders = boolSetting("ShowFoldersFilter");
            currentSortChoice = settingsHistoryHelper.GetSettingEntry("ImgPropertySort");
            currentAspectRatio = settingsHistoryHelper.GetSettingEntry("AspectRatioFilter");
        }

        selectedBackgroundColour = convertBackgroundColour(colourSetting("SelectedBackgroundColour"));
        selectedPrimaryAccentColour = convertUIColour(colourSetting("SelectedPrimaryAccentColour"));
        selectedWallpaperCollectionColour = convertUIColour(colourSetting("SelectedWallpaperCollectionColour"));
        selectedPreviewBackgroundColour = convertUIColour(colourSetting("SelectedPreviewBackgroundColour"));
        changeFluentColour();
    }


    // =======================================================
    // GALLERY
    private bool isImageGalleryViewVisible = true;
    public bool IsImageGalleryViewVisible { get => isImageGalleryViewVisible; set => SetProperty(ref isImageGalleryViewVisible, value); }

    private string currentSelectedDirectory = "No Directory Selected";
    public string CurrentSelectedDirectory
    {
        get => currentSelectedDirectory;
        set
        {
            if (!SetProperty(ref currentSelectedDirectory, value)) return;
            CurrentSelectedDirecName = "/" + Path.GetFileName(Path.TrimEndingDirectorySeparator(value)); // change the display var
            settingsHistoryHelper.UpdateSetting("LastChosenDirectory", value);
        }
    }

    private string currentSelectedDirecName = "No Directory Selected";
    public string CurrentSelectedDirecName { get => currentSelectedDirecName; set => SetProperty(ref currentSelectedDirecName, value); }

    private double imgLoadProgress;
    public double ImgLoadProgress { get => imgLoadProgress; set => SetProperty(ref imgLoadProgress, value); }


    // =======================================================
    // FILTERS
    // [ filters in vm must have applyAllFilters() ]
    private string filterSearchText = "";
    public string FilterSearchText { get => filterSearchText; set { if (SetProperty(ref filterSearchText, value)) settingsHistoryHelper.UpdateSetting("SearchFilter", value); } }

    private bool showFolders = true;
    public bool ShowFolders { get => showFolders; set { if (SetProperty(ref showFolders, value)) settingsHistoryHelper.UpdateSetting("ShowFoldersFilter", value.ToString()); } }

    private string currentSortChoice = "Name";
    public string CurrentSortChoice { get => currentSortChoice; set { if (SetProperty(ref currentSortChoice, value)) settingsHistoryHelper.UpdateSetting("ImgPropertySort", value); } }

    private string currentAspectRatio = "All";
    public string CurrentAspectRatio { get => currentAspectRatio; set { if (SetProperty(ref currentAspectRatio, value)) settingsHistoryHelper.UpdateSetting("AspectRatioFilter", value); } }


    // =======================================================
    // HISTORY
    public ObservableCollection<Wallpaper> HistoryWallpaperList { get; set; } = new ObservableCollection<Wallpaper>();

    private bool isHistoryViewVisible = false;
    public bool IsHistoryViewVisible { get => isHistoryViewVisible; set => SetProperty(ref isHistoryViewVisible, value); }


    // =======================================================
    // PREVIEW

    // list of all current monitors of pc
    public ObservableCollection<MonitorInfo> MonitorList { get; set; } = new ObservableCollection<MonitorInfo>();

    private bool setBackgroundButtonEnabled = false;
    public bool SetBackgroundButtonEnabled { get => setBackgroundButtonEnabled; set => SetProperty(ref setBackgroundButtonEnabled, value); }


    // =======================================================
    // AUTOSET QUEUE
    public ObservableCollection<Wallpaper> WallpaperQueue { get; set; } = new ObservableCollection<Wallpaper>();

    private bool loopCustomQueue = false;
    public bool LoopCustomQueue { get => loopCustomQueue; set => SetProperty(ref loopCustomQueue, value); }

    private bool loopRandomQueue = false;
    public bool LoopRandomQueue { get => loopRandomQueue; set => SetProperty(ref loopRandomQueue, value); }


    // =======================================================
    // SETTINGS
    private bool mainGridVisibility = true;
    public bool MainGridVisibility { get => mainGridVisibility; set => SetProperty(ref mainGridVisibility, value); }

    private bool settingsViewVisibility = false;
    public bool SettingsViewVisibility { get => settingsViewVisibility; set => SetProperty(ref settingsViewVisibility, value); }

    private bool allowSaveHistory;
    public bool AllowSaveHistory { get => allowSaveHistory; set { if (SetProperty(ref allowSaveHistory, value)) settingsHistoryHelper.UpdateSetting("AllowSaveHistory", value.ToString()); } }

    private bool stayRunningInBackground;
    public bool StayRunningInBackground { get => stayRunningInBackground; set { if (SetProperty(ref stayRunningInBackground, value)) settingsHistoryHelper.UpdateSetting("StayRunningInBackground", value.ToString()); } }

    private bool folderCountIncludesFolders;
    public bool FolderCountIncludesFolders { get => folderCountIncludesFolders; set { if (SetProperty(ref folderCountIncludesFolders, value)) settingsHistoryHelper.UpdateSetting("FolderCount", value.ToString()); } }

    private bool autoOpenLastDirectory;
    public bool AutoOpenLastDirectory { get => autoOpenLastDirectory; set { if (SetProperty(ref autoOpenLastDirectory, value)) settingsHistoryHelper.UpdateSetting("AutoOpenLastChosenDirectoryOnAppStart", value.ToString()); } }

    private bool rememberFilters;
    public bool RememberFilters { get => rememberFilters; set { if (SetProperty(ref rememberFilters, value)) settingsHistoryHelper.UpdateSetting("RememberFilterSettings", value.ToString()); } }

    private bool rememberThumbnailZoomLevel;
    public bool RememberThumbnailZoomLevel { get => rememberThumbnailZoomLevel; set { if (SetProperty(ref rememberThumbnailZoomLevel, value)) settingsHistoryHelper.UpdateSetting("RememberThumbnailZoomLevel", value.ToString()); } }

    private int cpuThreadsAllocated;
    public int CPUThreadsAllocated { get => cpuThreadsAllocated; set { if (SetProperty(ref cpuThreadsAllocated, value)) settingsHistoryHelper.UpdateSetting("CPUThreadsAllocated", value.ToString()); } }
    public static int MaxCPUThreads { get; } = Math.Max(1, Environment.ProcessorCount - 2); // leave 2 threads for UI & rendering


    // doesnt actually work like argb since it is being modified - decr rgb values in order to incr opacity
    private Color selectedBackgroundColour;
    public Color SelectedBackgroundColour
    {
        get => selectedBackgroundColour;
        set
        {
            if (value == selectedBackgroundColour) return;
            // update it before processing
            settingsHistoryHelper.UpdateSetting("SelectedBackgroundColour", value.ToString());
            SetProperty(ref selectedBackgroundColour, convertBackgroundColour(value));
        }
    }

    // func to convert colour for avalonia alpha and tint
    public Color convertBackgroundColour(Color inputColour)
    {
        // gpt code
        const double minAlpha = 50;   // least tint opacity
        const double maxAlpha = 255;  // most tint opacity
        double factor = 1 - (inputColour.A / 255.0); // invert the chosen alpha (0 -> 1, 255 -> 0) to account for transparency conflict
        byte newAlpha = (byte)(minAlpha + (maxAlpha - minAlpha) * factor); // formula used to adapt colorpicker to alphacolour UI
        var newColor = Color.FromArgb(newAlpha, inputColour.R, inputColour.G, inputColour.B);
        return newColor;
    }

    // func to convert colour for binding to UI buttons and grid etc
    private Color convertUIColour(Color inputColour)
    {
        // gpt code
        const byte minAlpha = 50;   // minimum opacity
        const byte maxAlpha = 255;  // maximum opacity
                                    // Clamp the alpha value between minAlpha and maxAlpha
        byte newAlpha = (byte)Math.Clamp(inputColour.A, minAlpha, maxAlpha);
        var newColor = Color.FromArgb(newAlpha, inputColour.R, inputColour.G, inputColour.B);
        return newColor;
    }


    private Color selectedPrimaryAccentColour;
    public Color SelectedPrimaryAccentColour
    {
        get => selectedPrimaryAccentColour;
        set
        {
            settingsHistoryHelper.UpdateSetting("SelectedPrimaryAccentColour", value.ToString());
            SetProperty(ref selectedPrimaryAccentColour, convertUIColour(value));
            changeFluentColour();
        }
    }
    // function to change fluent-based UI colours (eg. sliders / tabs / etc )
    // https://github.com/AvaloniaUI/Avalonia/discussions/12042
    private FluentTheme? newFluentTheme;
    public void changeFluentColour()
    {
        App currApp = (App)Application.Current;

        if (newFluentTheme == null)
        {
            newFluentTheme = new FluentTheme();
            currApp.Styles.Add(newFluentTheme);
        }

        newFluentTheme.Palettes[ThemeVariant.Light] = new ColorPaletteResources() { Accent = SelectedPrimaryAccentColour };
        newFluentTheme.Palettes[ThemeVariant.Dark] = new ColorPaletteResources() { Accent = SelectedPrimaryAccentColour };
    }


    private Color selectedWallpaperCollectionColour;
    public Color SelectedWallpaperCollectionColour
    {
        get => selectedWallpaperCollectionColour;
        set
        {
            settingsHistoryHelper.UpdateSetting("SelectedWallpaperCollectionColour", value.ToString());
            SetProperty(ref selectedWallpaperCollectionColour, convertUIColour(value));
        }
    }

    private Color selectedPreviewBackgroundColour;
    public Color SelectedPreviewBackgroundColour
    {
        get => selectedPreviewBackgroundColour;
        set
        {
            settingsHistoryHelper.UpdateSetting("SelectedPreviewBackgroundColour", value.ToString());
            SetProperty(ref selectedPreviewBackgroundColour, convertUIColour(value));
        }
    }


    // version ===================================================
    private string appNameVersion = "v0.3.2";
    public string AppNameVersion { get => appNameVersion; set => SetProperty(ref appNameVersion, value); }

    private bool updateAvailableVisible = false;
    public bool UpdateAvailableVisible { get => updateAvailableVisible; set => SetProperty(ref updateAvailableVisible, value); }

}