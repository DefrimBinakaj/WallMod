using Avalonia;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Themes.Fluent;
using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WallMod.Helpers;
using WallMod.Models;
using WallMod.ViewModels;

namespace WallMod.State;

public class UniversalAppStore : ViewModelBase
{

    SettingsHistoryHelper settingsHistoryHelper = new SettingsHistoryHelper();


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
            if (currentSelectedDirectory != value)
            {
                currentSelectedDirectory = value;
                OnPropertyChanged(nameof(CurrentSelectedDirectory));
                CurrentSelectedDirecName = "/" + Path.GetFileName(Path.TrimEndingDirectorySeparator(value)); // change the display var
                settingsHistoryHelper.UpdateSetting("LastChosenDirectory", CurrentSelectedDirectory);
            }
        }
    }

    private string currentSelectedDirecName = "No Directory Selected";
    public string CurrentSelectedDirecName { get => currentSelectedDirecName; set { if (currentSelectedDirecName != value) { currentSelectedDirecName = value; OnPropertyChanged(nameof(CurrentSelectedDirecName)); } } }

    private double imgLoadProgress;
    public double ImgLoadProgress { get => imgLoadProgress; set { if (value != imgLoadProgress) { imgLoadProgress = value; OnPropertyChanged(nameof(ImgLoadProgress)); } } }


    // =======================================================
    // FILTERS
    // [ filters in vm must have applyAllFilters() ]
    private string filterSearchText = "";
    public string FilterSearchText
    {
        get => filterSearchText;
        set
        {
            if (SetProperty(ref filterSearchText, value))
            {
                settingsHistoryHelper.UpdateSetting("SearchFilter", FilterSearchText);
            }
        }
    }

    private bool showFolders = true;
    public bool ShowFolders
    {
        get => showFolders;
        set
        {
            if (showFolders != value)
            {
                showFolders = value;
                OnPropertyChanged(nameof(ShowFolders));
                settingsHistoryHelper.UpdateSetting("ShowFoldersFilter", ShowFolders.ToString());
            }
        }
    }

    private string currentSortChoice = "Name";
    public string CurrentSortChoice
    {
        get => currentSortChoice;
        set
        {
            if (SetProperty(ref currentSortChoice, value))
            {
                settingsHistoryHelper.UpdateSetting("ImgPropertySort", CurrentSortChoice);
            }
        }
    }

    private string currentAspectRatio = "All";
    public string CurrentAspectRatio
    {
        get => currentAspectRatio;
        set
        {
            if (SetProperty(ref currentAspectRatio, value))
            {
                settingsHistoryHelper.UpdateSetting("AspectRatioFilter", CurrentAspectRatio);
            }
        }
    }



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
    public bool SetBackgroundButtonEnabled
    {
        get => setBackgroundButtonEnabled;
        set => SetProperty(ref setBackgroundButtonEnabled, value);
    }



    // =======================================================
    // AUTOSET QUEUE
    public ObservableCollection<Wallpaper> WallpaperQueue { get; set; } = new ObservableCollection<Wallpaper>();

    // MAYBE?: Convenience helper (avoids duplicates)
    public void AddToQueue(Wallpaper wp)
    {
        if (WallpaperQueue.Any(w => w.FilePath == wp.FilePath) == false)
            WallpaperQueue.Add(wp);
    }



    // =======================================================
    // SETTINGS
    private bool mainGridVisibility = true;
    public bool MainGridVisibility { get => mainGridVisibility; set => SetProperty(ref mainGridVisibility, value); }
    
    private bool settingsViewVisibility = false;
    public bool SettingsViewVisibility { get => settingsViewVisibility; set => SetProperty(ref settingsViewVisibility, value); }


    private bool allowSaveHistory;
    public bool AllowSaveHistory { get => allowSaveHistory; set => SetProperty(ref allowSaveHistory, value); }

    private bool stayRunningInBackground;
    public bool StayRunningInBackground
    {
        get => stayRunningInBackground;
        set
        {
            if (SetProperty(ref stayRunningInBackground, value))
            {
                // save to json file
                settingsHistoryHelper.UpdateSetting("StayRunningInBackground", stayRunningInBackground.ToString()); 
            }
        }
    }

    private bool autoOpenLastDirectory;
    public bool AutoOpenLastDirectory
    {
        get => autoOpenLastDirectory;
        set
        {
            if (SetProperty(ref autoOpenLastDirectory, value))
            {
                // save to json file
                settingsHistoryHelper.UpdateSetting("AutoOpenLastChosenDirectoryOnAppStart", AutoOpenLastDirectory.ToString());
            }
        }
    }

    private bool rememberFilters;
    public bool RememberFilters
    {
        get => rememberFilters;
        set
        {
            if (SetProperty(ref rememberFilters, value))
            {
                // save to json file
                settingsHistoryHelper.UpdateSetting("RememberFilterSettings", RememberFilters.ToString());
            }
        }
    }

    private int cpuThreadsAllocated;
    public int CPUThreadsAllocated
    {
        get => cpuThreadsAllocated;
        set
        {
            if (cpuThreadsAllocated != value)
            {
                cpuThreadsAllocated = value;
                OnPropertyChanged();
                // save to json file
                settingsHistoryHelper.UpdateSetting("CPUThreadsAllocated", CPUThreadsAllocated.ToString());
            }
        }
    }
    public int MaxCPUThreads { get; } = Environment.ProcessorCount;


    // doesnt actually work like argb since it is being modified - decr rgb values in order to incr opacity
    private Color selectedBackgroundColour;
    public Color SelectedBackgroundColour
    {
        get => selectedBackgroundColour;
        set
        {
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
    public void changeFluentColour()
    {
        App currApp = (App)Application.Current;

        var newTheme = new FluentTheme();
        newTheme.Palettes[ThemeVariant.Light] = new ColorPaletteResources() { Accent = SelectedPrimaryAccentColour };
        newTheme.Palettes[ThemeVariant.Dark] = new ColorPaletteResources() { Accent = SelectedPrimaryAccentColour };
        currApp.Styles.Add(newTheme);
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
    private string appNameVersion = "v0.0.11";
    public string AppNameVersion
    {
        get => appNameVersion;
        set => SetProperty(ref appNameVersion, value);
    }
}
