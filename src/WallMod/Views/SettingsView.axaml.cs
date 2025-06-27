using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Microsoft.Extensions.DependencyInjection;
using System.Diagnostics;
using WallMod.Helpers;
using WallMod.State;
using WallMod.ViewModels;

namespace WallMod.Views;

public partial class SettingsView : UserControl
{

    private readonly UniversalAppStore uniVM = App.Services!.GetRequiredService<UniversalAppStore>();

    UpdateVersionHelper updateVersionHelper = new();

    public SettingsView()
    {
        InitializeComponent();
        DataContext = App.Services!.GetRequiredService<SettingsViewModel>();

        ExecVersionCheck();
    }

    private async void ExecVersionCheck()
    {
        var (fullyUpdated, _, _) = await updateVersionHelper.GetGithubVersionAndInstallLink();
        if (fullyUpdated == false)
        {
            uniVM.UpdateAvailableVisible = true;
        }
    }




}