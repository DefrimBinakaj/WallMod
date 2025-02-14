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

namespace WallMod.ViewModels;

/**
 * Viewmodel for the main application functionality
 */
public partial class MainWindowViewModel : ViewModelBase
{
    AppStorageHelper appStorageHelper;

    public IRelayCommand uploadClicked { get; }

    public IRelayCommand selectedDirectory { get; }

    public IRelayCommand filterClicked { get; }

    public ObservableCollection<Wallpaper> DisplayWallpaperList { get; set; }

    public IRelayCommand setWallpaperCommand { get; }

    public ObservableCollection<string> WallpaperStyleList { get; set; }

    public ObservableCollection<MonitorInfo> MonitorList { get; set; }

    public IRelayCommand detectMonitorsButton { get; }

    public IRelayCommand viewHistoryButton { get; }

    public ObservableCollection<Bitmap> MonitorThumbnailList { get; set; }

    HistoryHelper historyHelper;
    public ObservableCollection<string> WallpaperHistoryList { get; set; }
    public ObservableCollection<Wallpaper> HistoryWallpaperList { get; set; }

    public IRelayCommand settingsButton { get; }

    public IRelayCommand deleteHistoryButton { get; }

    public IRelayCommand openGithubButton { get; }


    public MainWindowViewModel()
    {
        appStorageHelper = new AppStorageHelper();
        appStorageHelper.InitAppStorage();

        // set default img and values
        CurrentWallpaperPreview = new Bitmap(AssetLoader.Open(new Uri("avares://Wallmod/Assets/placeholder-icon.png")));
        CurrentWallpaperName = "Name";
        currentWallpaperSize = "Resolution";

        AllowSaveHistory = true;
        StayRunningInBackground = false;

        uploadClicked = new RelayCommand(execImgUpload);

        selectedDirectory = new RelayCommand(execSelectDirec);

        filterClicked = new RelayCommand(filterExec);

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

        MonitorThumbnailList = new ObservableCollection<Bitmap>();

        historyHelper = new HistoryHelper();
        WallpaperHistoryList = new ObservableCollection<string>();
        HistoryWallpaperList = new ObservableCollection<Wallpaper>();

        settingsButton = new RelayCommand(navToSettings);

        deleteHistoryButton = new RelayCommand(DeleteHistory);

        openGithubButton = new RelayCommand(OpenGithub);

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

    //private bool showCpuMemoryUsage;
    //public bool ShowCpuMemoryUsage
    //{
    //    get => showCpuMemoryUsage;
    //    set
    //    {
    //        if (SetProperty(ref showCpuMemoryUsage, value))
    //        {
    //            // idk what im gonna do with this
    //        }
    //    }
    //}

    private bool allowSaveHistory;
    public bool AllowSaveHistory
    {
        get => allowSaveHistory;
        set => SetProperty(ref allowSaveHistory, value);
    }

    private bool stayRunningInBackground;
    public bool StayRunningInBackground
    {
        get => stayRunningInBackground;
        set => SetProperty(ref stayRunningInBackground, value);
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
                DisplayWallpaperList.Add(wp);
            }
        }
    }
    // ===============================



    // ===============================
    // select directory
    private void execSelectDirec()
    {
        selectDirec();
    }


    // erroring when it is spam-clicked
    private async void selectDirec()
    {
        Window window = new Window();
        Debug.WriteLine("direcbutton clicked");
        ImageHelper imgHelper = new ImageHelper();
        ObservableCollection<Wallpaper> directoryPath = await imgHelper.loadListFromDirectory(window, this);
        // directoryPath.OrderBy(entr => entr.Name, StringComparer.OrdinalIgnoreCase);
        DisplayWallpaperList.Clear();
        if (directoryPath != null && directoryPath.Count > 0)
        {
            foreach (var imgFile in directoryPath)
            {
                DisplayWallpaperList.Add(imgFile);
            }
        }
    }
    // ===============================



    // filter ...
    private void filterExec()
    {
        Debug.WriteLine("filter clicked");
    }




    // ===============================
    // image clicked in UI
    public async Task ImageTapped(Wallpaper wallpaper)
    {
        Debug.WriteLine(wallpaper.Name + " image tapped");
        LastSelectedWallpaper = wallpaper;
        CurrentWallpaperPreview = ImageHelper.GetBitmapFromPath(LastSelectedWallpaper.FilePath);
        CurrentWallpaperName = wallpaper.Name;
        CurrentWallpaperSize = currentWallpaperPreview.Size.Width.ToString() + " x " + currentWallpaperPreview.Size.Height.ToString();
        
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

    // currently not used for a valuable functionality, but maybe will be
    public async Task ImageDoubleTapped(Wallpaper wallpaper)
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
                historyHelper.AddToHistory(LastSelectedWallpaper.FilePath);
            }
        }
        else
        {
            string firstMonitorId = LastSelMonitor.MonitorIdPath;
            Debug.WriteLine("Using monitor ID: " + firstMonitorId);

            SetWallpaperHelper.SetWallpaper(LastSelectedWallpaper.FilePath, SelectedWallpaperStyle, firstMonitorId);

            if (allowSaveHistory)
            {
                historyHelper.AddToHistory(LastSelectedWallpaper.FilePath);
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
                historyHelper.AddToHistory(LastSelectedWallpaper.FilePath);
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
    private void ViewHistory()
    {
        HistoryWallpaperList.Clear();

        var history = historyHelper.LoadHistory();

        foreach (var filePath in history)
        {
            if (File.Exists(filePath))
            {
                var wallpaper = new Wallpaper
                {
                    FilePath = filePath,
                    Name = Path.GetFileNameWithoutExtension(filePath),
                    ImageThumbnailBitmap = ImageHelper.GetBitmapFromPath(filePath)
                };
                HistoryWallpaperList.Add(wallpaper);
            }
        }

        // switch to history view
        IsHistoryViewVisible = !IsHistoryViewVisible;
        IsImageGalleryViewVisible = !IsImageGalleryViewVisible;

        // repeated clicks doesnt infinitely increase memory
        GC.Collect();
        GC.WaitForPendingFinalizers();
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

    public void DeleteHistory()
    {
        HistoryWallpaperList.Clear();

        var history = historyHelper.LoadHistory();

        foreach (var filePath in history)
        {
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
            }
        }
    }

    // currently unused? put in app.axaml.cs iirc
    private void MinimizeToTray()
    {
        if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktopLifetime)
        {
            if (StayRunningInBackground)
            {
                desktopLifetime.MainWindow.Hide();
                Debug.WriteLine("App minimized to tray.");
            }
            else
            {
                desktopLifetime.Shutdown();
            }
        }
    }

    public void OpenGithub()
    {
        string url = "https://github.com/DefrimBinakaj";
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
    // ===============================


}
