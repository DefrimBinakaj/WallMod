using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WallMod.Models;

namespace WallMod.State;

public class UniversalAppStore
{

    // =======================================================
    // SELECTION


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
