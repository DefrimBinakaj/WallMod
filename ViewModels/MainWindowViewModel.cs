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

namespace WallMod.ViewModels;

/**
 * Viewmodel for the main application functionality
 */
public partial class MainWindowViewModel : ViewModelBase
{
    AppStorageHelper appStorageHelper;

    // img upload ==========================================================
    public IRelayCommand uploadClicked { get; }

    public IRelayCommand selectedDirectory { get; }

    public IRelayCommand navigateToParentDirec {  get; }

    // filter ==============================================================
    public IRelayCommand filterClicked { get; }

    public IRelayCommand filterSearchCommand { get; }

    public IRelayCommand<string> filterGroupSelectedCommand { get; set; }

    public IRelayCommand<string> filterAspectRatioCommand {  get; set; }


    // wallpaper list ======================================================
    public ObservableCollection<Wallpaper> AllWallpapers { get; set; } // all wallpapers from a directory
    public ObservableCollection<Wallpaper> DisplayWallpaperList { get; set; } // current display of wallpapers after filtering


    // set background ======================================================
    public IRelayCommand setWallpaperCommand { get; }

    public ObservableCollection<string> WallpaperStyleList { get; set; }


    // monitors ============================================================
    public ObservableCollection<MonitorInfo> MonitorList { get; set; }

    public IRelayCommand detectMonitorsButton { get; }


    // history =============================================================
    public IRelayCommand viewHistoryButton { get; }

    public IRelayCommand<Wallpaper> deleteHistoryEntryButton {  get; }

    WallpaperHistoryHelper wallpaperHistoryHelper;
    public ObservableCollection<string> WallpaperHistoryList { get; set; }
    public ObservableCollection<Wallpaper> HistoryWallpaperList { get; set; }

    SettingsHistoryHelper settingsHistoryHelper;


    // settings ============================================================
    public IRelayCommand settingsButton { get; }
    public IRelayCommand deleteHistoryButton { get; }
    public IRelayCommand openGithubButton { get; }


    public MainWindowViewModel()
    {
        appStorageHelper = new AppStorageHelper();
        appStorageHelper.InitAppStorage();

        uploadClicked = new RelayCommand(execImgUpload);

        selectedDirectory = new RelayCommand(execSelectDirec);

        navigateToParentDirec = new RelayCommand(execNavigateToParentDirec);

        filterClicked = new RelayCommand(filterExec);

        filterSearchCommand = new RelayCommand(filterSearchExec);

        filterGroupSelectedCommand = new RelayCommand<string>(filterSelectExec);

        filterAspectRatioCommand = new RelayCommand<string>(filterAspectRatioExec);

        AllWallpapers = new ObservableCollection<Wallpaper>();

        DisplayWallpaperList = new ObservableCollection<Wallpaper>();

        setWallpaperCommand = new RelayCommand(SetWallpaper);

        WallpaperStyleList = new ObservableCollection<string>
        {
            "Fill",
            "Fit",
            "Stretch",
            "Tile",
            "Center",
            "Span"
        };
        SelectedWallpaperStyle = WallpaperStyleList[0];

        MonitorList = new ObservableCollection<MonitorInfo>();
        DetectMonitors();
        detectMonitorsButton = new RelayCommand(DetectMonitors);

        viewHistoryButton = new RelayCommand(ViewHistory);

        deleteHistoryEntryButton = new RelayCommand<Wallpaper>(DeleteSingleHistoryEntry);

        wallpaperHistoryHelper = new WallpaperHistoryHelper();
        WallpaperHistoryList = new ObservableCollection<string>();
        HistoryWallpaperList = new ObservableCollection<Wallpaper>();

        settingsHistoryHelper = new SettingsHistoryHelper();

        settingsButton = new RelayCommand(navToSettings);

        deleteHistoryButton = new RelayCommand(DeleteHistory);

        openGithubButton = new RelayCommand(OpenGithub);


        // set default img and values
        CurrentWallpaperPreview = new Bitmap(AssetLoader.Open(new Uri("avares://Wallmod/Assets/placeholder-icon.png")));
        CurrentWallpaperName = "Name";
        currentWallpaperSize = "Resolution";

        AllowSaveHistory = SavedAllowSaveHistory();
        StayRunningInBackground = SavedStayRunningInBackground();
        AutoOpenLastDirectory = SavedAutoOpenLastDirectory();
        RememberFilters = SavedRememberFilters();
        CPUThreadsAllocated = SavedCPUThreadsAllocated();

        if (AutoOpenLastDirectory == true)
        {
            execAutoOpenLastDir();
        }

        if (RememberFilters == true)
        {
            execRememberFilters();
        }

        SelectedBackgroundColour = SavedSelectedBackgroundColour();
        SelectedPrimaryAccentColour = SavedSelectedPrimaryAccentColour();
        SelectedWallpaperCollectionColour = SavedSelectedWallpaperCollectionColour();
        SelectedPreviewBackgroundColour = SavedSelectedPreviewBackgroundColour();
        changeFluentColour();

    }



    // UI variables ---------------------------------------

    private Wallpaper lastSelectedWallpaper;

    public Wallpaper LastSelectedWallpaper
    {
        get => lastSelectedWallpaper;
        set => SetProperty(ref lastSelectedWallpaper, value);
    }



    private MonitorInfo lastSelMonitor;

    public MonitorInfo LastSelMonitor
    {
        get => lastSelMonitor;
        set => SetProperty(ref lastSelMonitor, value);
    }


    private Bitmap currentWallpaperPreview;
    public Bitmap CurrentWallpaperPreview
    {
        get => currentWallpaperPreview;
        set
        {
            if (value != currentWallpaperPreview)
            {
                currentWallpaperPreview = value;
                OnPropertyChanged(nameof(CurrentWallpaperPreview));
            }
        }
    }

    private String currentWallpaperName;
    public String CurrentWallpaperName
    {
        get => currentWallpaperName;
        set
        {
            if (value != currentWallpaperName)
            {
                currentWallpaperName = value;
                OnPropertyChanged(nameof(CurrentWallpaperName));
            }
        }
    }

    private String currentWallpaperSize;
    public String CurrentWallpaperSize
    {
        get => currentWallpaperSize;
        set
        {
            if (value != currentWallpaperSize)
            {
                currentWallpaperSize = value;
                OnPropertyChanged(nameof(CurrentWallpaperSize));
            }
        }
    }


    private double imgLoadProgress;
    public double ImgLoadProgress
    {
        get => imgLoadProgress;
        set
        {
            if (value != imgLoadProgress)
            {
                imgLoadProgress = value;
                OnPropertyChanged(nameof(ImgLoadProgress));
            }
        }
    }

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
                CurrentSelectedDirecName = "/" + Path.GetFileName(value); // change the display var
                updateLastChosenDir();
            }
        }
    }

    private string currentSelectedDirecName = "No Directory Selected";
    public string CurrentSelectedDirecName
    {
        get => currentSelectedDirecName;
        set
        {
            if (currentSelectedDirecName != value)
            {
                currentSelectedDirecName = value;
                OnPropertyChanged(nameof(CurrentSelectedDirecName));
            }
        }
    }


    private string selectedWallpaperStyle = "Fill";
    public string SelectedWallpaperStyle
    {
        get => selectedWallpaperStyle;
        set => SetProperty(ref selectedWallpaperStyle, value);
    }


    private double thumbnailZoomLevel = 150;

    public double ThumbnailZoomLevel
    {
        get => thumbnailZoomLevel;
        set
        {
            if (thumbnailZoomLevel != value)
            {
                thumbnailZoomLevel = value;
                OnPropertyChanged();
            }
        }
    }


    private bool styleDropdownEnabled = false;
    public bool StyleDropdownEnabled
    {
        get => styleDropdownEnabled;
        set => SetProperty(ref styleDropdownEnabled, value);
    }



    // views ===================================================

    private bool isHistoryViewVisible = false;
    public bool IsHistoryViewVisible
    {
        get => isHistoryViewVisible;
        set => SetProperty(ref isHistoryViewVisible, value);
    }

    private bool isImageGalleryViewVisible = true;
    public bool IsImageGalleryViewVisible
    {
        get => isImageGalleryViewVisible;
        set => SetProperty(ref isImageGalleryViewVisible, value);
    }

    private bool mainGridVisibility = true;

    public bool MainGridVisibility
    {
        get => mainGridVisibility;
        set => SetProperty(ref mainGridVisibility, value);
    }

    private bool settingsViewVisibility = false;

    public bool SettingsViewVisibility
    {
        get => settingsViewVisibility;
        set => SetProperty(ref settingsViewVisibility, value);
    }



    // filter stuff ===================================================

    private bool isFilterOpen;
    public bool IsFilterOpen
    {
        get => isFilterOpen;
        set => SetProperty(ref isFilterOpen, value);
    }

    private string filterSearchText = "";
    public string FilterSearchText
    {
        get => filterSearchText;
        set
        {
            if (SetProperty(ref filterSearchText, value))
            {
                filterSearchExec(); // re-filter on every keystroke
                updateRememberFilterValue("SearchFilter", FilterSearchText);
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
                applyAllFilters();
                updateRememberFilterValue("ShowFoldersFilter", ShowFolders.ToString());
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
                updateRememberFilterValue("ImgPropertySort", CurrentSortChoice);
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
                updateRememberFilterValue("AspectRatioFilter", CurrentAspectRatio);
            }
        }
    }



    // settings ===================================================

    private bool allowSaveHistory;
    public bool AllowSaveHistory
    {
        get => allowSaveHistory;
        set
        {
            if (SetProperty(ref allowSaveHistory, value))
            {
                UpdateAllowSaveHistory(AllowSaveHistory); // save to json file
            }
        }
    }

    private bool stayRunningInBackground;
    public bool StayRunningInBackground
    {
        get => stayRunningInBackground;
        set
        {
            if (SetProperty(ref stayRunningInBackground, value))
            {
                UpdateStayRunningInBackground(StayRunningInBackground); // save to json file
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
                UpdateAutoOpenLastDirectory(AutoOpenLastDirectory); // save to json file
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
                UpdateRememberFilters(RememberFilters); // save to json file
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
                UpdateCPUThreadsAllocated(CPUThreadsAllocated); // save to json file
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
            UpdateSelectedBackgroundColour(value); // update it before processing
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
            UpdateSelectedPrimaryAccentColour(value);
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
            UpdateSelectedWallpaperCollectionColour(value);
            SetProperty(ref selectedWallpaperCollectionColour, convertUIColour(value));
        }
    }

    private Color selectedPreviewBackgroundColour;
    public Color SelectedPreviewBackgroundColour
    {
        get => selectedPreviewBackgroundColour;
        set
        {
            UpdateSelectedPreviewBackgroundColour(value);
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

    // ---------------------------------------------------





    // ===============================
    // image upload
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
    // ===============================



    // ===============================
    // select directory
    private void execSelectDirec()
    {
        selectDirec(null); // execute it without any folder name in particular
    }

    private void execNavigateToParentDirec()
    {
        Debug.WriteLine("CurrDirec = " + CurrentSelectedDirectory);
        if (CurrentSelectedDirectory != "No Directory Selected" || !string.IsNullOrEmpty(CurrentSelectedDirectory))
        {
            string parentDir = Path.GetDirectoryName(CurrentSelectedDirectory);
            if (!string.IsNullOrEmpty(parentDir) && Directory.Exists(parentDir))
            {
                selectDirec(parentDir);
            }
        }
    }

    // erroring when it is spam-clicked
    private async void selectDirec(string direcChoice)
    {
        Window window = new Window();
        Debug.WriteLine("direcbutton clicked");
        ImageHelper imgHelper = new ImageHelper();
        ObservableCollection<Wallpaper> directoryPath = await imgHelper.getWallpaperListFromDirec(window, this, direcChoice);

        AllWallpapers.Clear();
        if (directoryPath != null && directoryPath.Count > 0)
        {
            foreach (var imgFile in directoryPath)
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

    // ===============================



    // ===============================
    // filter stuff

    private void filterExec()
    {
        Debug.WriteLine("filter clicked");
        IsFilterOpen = false;
        IsFilterOpen = true;
    }

    private void filterSearchExec()
    {
        Debug.WriteLine("searchtext = " + FilterSearchText);
        applyAllFilters();
    }

    private void filterSelectExec(string selectedChoice)
    {
        Debug.WriteLine("select = " + selectedChoice);
        CurrentSortChoice = selectedChoice;
        applyAllFilters();
    }

    private void filterAspectRatioExec(string selectedChoice)
    {
        Debug.WriteLine("aspect ratio = " + selectedChoice);
        CurrentAspectRatio = selectedChoice;
        applyAllFilters();
    }

    private void applyAllFilters()
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


    // ===============================






    private readonly SemaphoreSlim imageTappedSemaphore = new SemaphoreSlim(1, 1);
    // ===============================
    // image clicked in UI
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
                    CurrentWallpaperSize = currentWallpaperPreview.Size.Width.ToString() + " x " + currentWallpaperPreview.Size.Height.ToString();
                }
                

                // set preview monitors to "unclicked"
                foreach (var mon in MonitorList)
                {
                    mon.FillColour = "Navy";
                }

                // disable set button
                MainWindow mw = new MainWindow();
                mw.SetBackgroundButton.IsEnabled = false;

                // disable dropdown
                StyleDropdownEnabled = false;

            }

        }
        finally
        {
            imageTappedSemaphore.Release();
        }

    }

    // currently not used for a valuable functionality, but maybe will be
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
    // ===============================


    // ===============================
    // set image as background/wallpaper

    // set all monitors to same wallpaper
    public void SetWallpaper()
    {
        // if its null, that means either no monitors selected, or all monitors selected
        if (LastSelMonitor == null)
        {
            Debug.WriteLine("set all monitors");
            foreach (var mon in MonitorList)
            {
                SetWallpaperHelper.SetWallpaper(LastSelectedWallpaper.FilePath, SelectedWallpaperStyle, mon.MonitorIdPath);
            }

            if (allowSaveHistory)
            {
                wallpaperHistoryHelper.AddToHistory(LastSelectedWallpaper.FilePath);
            }
        }
        else
        {
            string firstMonitorId = LastSelMonitor.MonitorIdPath;
            Debug.WriteLine("Using monitor ID: " + firstMonitorId);

            SetWallpaperHelper.SetWallpaper(LastSelectedWallpaper.FilePath, SelectedWallpaperStyle, firstMonitorId);

            if (allowSaveHistory)
            {
                wallpaperHistoryHelper.AddToHistory(LastSelectedWallpaper.FilePath);
            }
        }
    }

    // set background of a single monitor with specific area selected
    public void SetWallpaperWithCrop(string imagePath, string monitorId, int x, int y, int width, int height)
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
            if (allowSaveHistory)
            {
                wallpaperHistoryHelper.AddToHistory(LastSelectedWallpaper.FilePath);
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error setting cropped wallpaper: {ex.Message}");
        }
    }
    // ===============================







    // ===============================
    // redetect monitors
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
    // ===============================



    // ===============================
    // background set history
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

    public void DeleteSingleHistoryEntry(Wallpaper wallpaper)
    {
        if (wallpaper == null)
        {
            return;
        }
        wallpaperHistoryHelper.RemoveHistoryEntry(wallpaper.FilePath);
        HistoryWallpaperList.Remove(wallpaper);
    }
    // ===============================


    // ===============================
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

        MainWindow mw = new MainWindow();
        mw.SetBackgroundButton.IsEnabled = true;
    }

    public async Task AllMonitorsSelected()
    {
        LastSelMonitor = null;
        foreach (MonitorInfo mon in MonitorList)
        {
            mon.FillColour = "#8ccd00";
        }
    }
    // ===============================




    // ===============================
    // settings
    public void navToSettings()
    {
        SettingsViewVisibility = !SettingsViewVisibility;
        MainGridVisibility = !MainGridVisibility;
    }

    public bool SavedAllowSaveHistory()
    {
        string allowSaveHistoryStatus = settingsHistoryHelper.GetSettingEntry("AllowSaveHistory");
        return bool.Parse(allowSaveHistoryStatus);
    }
    public void UpdateAllowSaveHistory(bool allowsavehistbool)
    {
        settingsHistoryHelper.UpdateSetting("AllowSaveHistory", allowsavehistbool.ToString());
    }

    public void DeleteHistory()
    {
        HistoryWallpaperList.Clear();

        var history = wallpaperHistoryHelper.LoadHistoryJson();

        foreach (var filePath in history)
        {
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
            }
        }
    }


    public bool SavedStayRunningInBackground()
    {
        string allowSaveHistoryStatus = settingsHistoryHelper.GetSettingEntry("StayRunningInBackground");
        return bool.Parse(allowSaveHistoryStatus);
    }
    public void UpdateStayRunningInBackground(bool stayrunninginbg)
    {
        settingsHistoryHelper.UpdateSetting("StayRunningInBackground", stayrunninginbg.ToString());
    }



    public bool SavedAutoOpenLastDirectory()
    {
        string autoOpenLastDirectoryStatus = settingsHistoryHelper.GetSettingEntry("AutoOpenLastChosenDirectoryOnAppStart");
        return bool.Parse(autoOpenLastDirectoryStatus);
    }
    public void UpdateAutoOpenLastDirectory(bool autoopenlastdir)
    {
        settingsHistoryHelper.UpdateSetting("AutoOpenLastChosenDirectoryOnAppStart", autoopenlastdir.ToString());
    }
    public void execAutoOpenLastDir()
    {
        string previousDir = settingsHistoryHelper.GetSettingEntry("LastChosenDirectory");
        if (!string.IsNullOrEmpty(previousDir) && Directory.Exists(previousDir))
        {
            selectDirec(previousDir);
        }
    }
    public void updateLastChosenDir()
    {
        settingsHistoryHelper.UpdateSetting("LastChosenDirectory", CurrentSelectedDirectory);
    }



    public bool SavedRememberFilters()
    {
        string rememberFiltersStatus = settingsHistoryHelper.GetSettingEntry("RememberFilterSettings");
        return bool.Parse(rememberFiltersStatus);
    }
    public void UpdateRememberFilters(bool rememfilters)
    {
        settingsHistoryHelper.UpdateSetting("RememberFilterSettings", rememfilters.ToString());
    }
    public void updateRememberFilterValue(string filterType, string filterVal)
    {
        settingsHistoryHelper.UpdateSetting(filterType, filterVal);
    }
    public void execRememberFilters()
    {
        FilterSearchText = settingsHistoryHelper.GetSettingEntry("SearchFilter");
        ShowFolders = bool.Parse(settingsHistoryHelper.GetSettingEntry("ShowFoldersFilter"));
        CurrentSortChoice = settingsHistoryHelper.GetSettingEntry("ImgPropertySort");
        CurrentAspectRatio = settingsHistoryHelper.GetSettingEntry("AspectRatioFilter");

        applyAllFilters();
    }



    public int SavedCPUThreadsAllocated()
    {
        string cpuThreadsAllocatedStatus = settingsHistoryHelper.GetSettingEntry("CPUThreadsAllocated");
        return int.Parse(cpuThreadsAllocatedStatus);
    }
    public void UpdateCPUThreadsAllocated(int cputhreadsalloc)
    {
        settingsHistoryHelper.UpdateSetting("CPUThreadsAllocated", cputhreadsalloc.ToString());
    }




    public Color SavedSelectedBackgroundColour()
    {
        string savedSelectedBackgroundStatus = settingsHistoryHelper.GetSettingEntry("SelectedBackgroundColour");
        return Color.Parse(savedSelectedBackgroundStatus);
    }
    public void UpdateSelectedBackgroundColour(Color newBgColour)
    {
        settingsHistoryHelper.UpdateSetting("SelectedBackgroundColour", newBgColour.ToString());
    }

    public Color SavedSelectedPrimaryAccentColour()
    {
        string savedPrimAccentColStatus = settingsHistoryHelper.GetSettingEntry("SelectedPrimaryAccentColour");
        return Color.Parse(savedPrimAccentColStatus);
    }
    public void UpdateSelectedPrimaryAccentColour(Color newPrimAccentColour)
    {
        settingsHistoryHelper.UpdateSetting("SelectedPrimaryAccentColour", newPrimAccentColour.ToString());
    }

    public Color SavedSelectedWallpaperCollectionColour()
    {
        string savedWpCollecColStatus = settingsHistoryHelper.GetSettingEntry("SelectedWallpaperCollectionColour");
        return Color.Parse(savedWpCollecColStatus);
    }
    public void UpdateSelectedWallpaperCollectionColour(Color newWallpaperCollecColour)
    {
        settingsHistoryHelper.UpdateSetting("SelectedWallpaperCollectionColour", newWallpaperCollecColour.ToString());
    }

    public Color SavedSelectedPreviewBackgroundColour()
    {
        string savedPreviewBgColStatus = settingsHistoryHelper.GetSettingEntry("SelectedPreviewBackgroundColour");
        return Color.Parse(savedPreviewBgColStatus);
    }
    public void UpdateSelectedPreviewBackgroundColour(Color newPrevBgColour)
    {
        settingsHistoryHelper.UpdateSetting("SelectedPreviewBackgroundColour", newPrevBgColour.ToString());
    }

    public void OpenGithub()
    {
        string url = "https://github.com/DefrimBinakaj/WallMod";
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = url,
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to open GitHub repository: {ex.Message}");
        }

    }

    // open AppData/WallMod
    public void OpenStorageFiles()
    {
        FileExporerHelper fileExporerHelper = new FileExporerHelper();
        fileExporerHelper.OpenFolderInExplorer(appStorageHelper.appStorageDirectory);
    }


    // ===============================

}
