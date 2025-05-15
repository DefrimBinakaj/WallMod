using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WallMod.Models;
using WallMod.ViewModels;

namespace WallMod.State;

public class UniversalAppStore : ViewModelBase
{

    // =======================================================
    // GALLERY
    private bool isImageGalleryViewVisible = true;
    public bool IsImageGalleryViewVisible { get => isImageGalleryViewVisible; set => SetProperty(ref isImageGalleryViewVisible, value); }

    // =======================================================


    // =======================================================
    // HISTORY
    public ObservableCollection<Wallpaper> HistoryWallpaperList { get; set; } = new ObservableCollection<Wallpaper>();

    private bool isHistoryViewVisible = false;
    public bool IsHistoryViewVisible { get => isHistoryViewVisible; set => SetProperty(ref isHistoryViewVisible, value); }

    // =======================================================


    // =======================================================
    // QUEUE
    public ObservableCollection<Wallpaper> WallpaperQueue { get; set; } = new ObservableCollection<Wallpaper>();

    // MAYBE?: Convenience helper (avoids duplicates)
    public void AddToQueue(Wallpaper wp)
    {
        if (WallpaperQueue.Any(w => w.FilePath == wp.FilePath) == false)
            WallpaperQueue.Add(wp);
    }
    // =======================================================
}
