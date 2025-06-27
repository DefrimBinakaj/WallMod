using Avalonia;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Themes.Fluent;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WallMod.Helpers;
using WallMod.State;
using WallMod.Converters;

namespace WallMod.ViewModels;

public partial class SettingsViewModel : ObservableObject
{

    private readonly UniversalAppStore uniVM = App.Services!.GetRequiredService<UniversalAppStore>();

    AppStorageHelper appStorageHelper = new AppStorageHelper();
    SettingsHistoryHelper settingsHistoryHelper = new SettingsHistoryHelper();
    WallpaperHistoryHelper wallpaperHistoryHelper = new WallpaperHistoryHelper();
    UpdateVersionHelper updateVersionHelper = new UpdateVersionHelper();

    [RelayCommand] public void backButton() => navBack();
    [RelayCommand] public void deleteHistoryButton() => DeleteHistory();
    [RelayCommand] public void openGithubButton() => OpenGithub();
    [RelayCommand] public void updateAppButton() => UpdateApp();

    public SettingsViewModel(UniversalAppStore universalVM)
    {
        uniVM = universalVM;

        uniVM.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(uniVM.UpdateAvailableVisible))
                OnPropertyChanged(nameof(UpdateAvailableVisible));
        };
    }


    public bool AllowSaveHistory { get => uniVM.AllowSaveHistory; set { if (uniVM.AllowSaveHistory != value) { uniVM.AllowSaveHistory = value; OnPropertyChanged(); } } }
    public bool StayRunningInBackground { get => uniVM.StayRunningInBackground; set { if (uniVM.StayRunningInBackground != value) { uniVM.StayRunningInBackground = value; OnPropertyChanged(); } } }
    public bool AutoOpenLastDirectory { get => uniVM.AutoOpenLastDirectory; set { if (uniVM.AutoOpenLastDirectory != value) { uniVM.AutoOpenLastDirectory = value; OnPropertyChanged(); } } }
    public bool RememberFilters { get => uniVM.RememberFilters; set { if (uniVM.RememberFilters != value) { uniVM.RememberFilters = value; OnPropertyChanged(); } } }
    public int CPUThreadsAllocated { get => uniVM.CPUThreadsAllocated; set { if (uniVM.CPUThreadsAllocated != value) { uniVM.CPUThreadsAllocated = value; OnPropertyChanged(); } } }
    public int MaxCPUThreads { get; } = Environment.ProcessorCount;
    public Color SelectedBackgroundColour { get => uniVM.SelectedBackgroundColour; set { if (uniVM.SelectedBackgroundColour != value) { uniVM.SelectedBackgroundColour = value; OnPropertyChanged(); } } }
    public Color SelectedPrimaryAccentColour { get => uniVM.SelectedPrimaryAccentColour; set { if (uniVM.SelectedPrimaryAccentColour != value) { uniVM.SelectedPrimaryAccentColour = value; OnPropertyChanged(); } } }
    public Color SelectedWallpaperCollectionColour { get => uniVM.SelectedWallpaperCollectionColour; set { if (uniVM.SelectedWallpaperCollectionColour != value) { uniVM.SelectedWallpaperCollectionColour = value; OnPropertyChanged(); } } }
    public Color SelectedPreviewBackgroundColour { get => uniVM.SelectedPreviewBackgroundColour; set { if (uniVM.SelectedPreviewBackgroundColour != value) { uniVM.SelectedPreviewBackgroundColour = value; OnPropertyChanged(); } } }
    public string AppNameVersion { get => uniVM.AppNameVersion; set { if (uniVM.AppNameVersion != value) { uniVM.AppNameVersion = value; OnPropertyChanged(); } } }
    public bool UpdateAvailableVisible { get => uniVM.UpdateAvailableVisible; set { if (uniVM.UpdateAvailableVisible != value) { uniVM.UpdateAvailableVisible = value; OnPropertyChanged(); } } }




    public void navBack()
    {
        uniVM.SettingsViewVisibility = !uniVM.SettingsViewVisibility;
        uniVM.MainGridVisibility = !uniVM.MainGridVisibility;
    }


    public void DeleteHistory()
    {
        uniVM.HistoryWallpaperList.Clear();

        var history = wallpaperHistoryHelper.LoadHistoryJson();

        foreach (var filePath in history)
        {
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
            }
        }
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


    public async void UpdateApp()
    {
        var versionList = updateVersionHelper.GetGithubVersionAndInstallLink();
        updateVersionHelper.ExecuteAppUpdate(versionList.Result.Item2, versionList.Result.Item3);
    }



    // open AppData/WallMod
    public void OpenStorageFiles()
    {
        FileExporerHelper fileExporerHelper = new FileExporerHelper();
        fileExporerHelper.OpenFolderInExplorer(appStorageHelper.appStorageDirectory);
    }




}
