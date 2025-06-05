using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WallMod.Helpers;
using WallMod.Models;
using WallMod.State;

namespace WallMod.ViewModels;

public partial class HistoryViewModel : ObservableObject
{

    private readonly UniversalAppStore uniVM = App.Services!.GetRequiredService<UniversalAppStore>();

    WallpaperHistoryHelper wallpaperHistoryHelper = new WallpaperHistoryHelper();

    // create WallpaperQueue replica which is the same as universal value
    public ObservableCollection<Wallpaper> HistoryWallpaperList => uniVM.HistoryWallpaperList;

    public bool IsHistoryViewVisible { get => uniVM.IsHistoryViewVisible; set { if (uniVM.IsHistoryViewVisible != value) { uniVM.IsHistoryViewVisible = value; OnPropertyChanged(); } } }
    public bool IsImageGalleryViewVisible { get => uniVM.IsImageGalleryViewVisible; set { if (uniVM.IsImageGalleryViewVisible != value) { uniVM.IsImageGalleryViewVisible = value; OnPropertyChanged(); } } }


    public HistoryViewModel(UniversalAppStore universalVM)
    {
        uniVM = universalVM;
    }



    // go back to gallery
    [RelayCommand] public void navBackToGalleryButton() => NavBackToGallery();

    private async void NavBackToGallery()
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

}
