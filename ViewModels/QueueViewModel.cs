using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WallMod.Models;

namespace WallMod.ViewModels;

public partial class QueueViewModel : ViewModelBase
{
    public ObservableCollection<Wallpaper> WallpaperQueue { get; set; } = new ObservableCollection<Wallpaper>();



    public QueueViewModel()
    {
        WallpaperQueue.Add(new Wallpaper { FilePath = "oy1", Name = "wp1" } );
        WallpaperQueue.Add(new Wallpaper { FilePath = "oy2", Name = "wp2" });
        WallpaperQueue.Add(new Wallpaper { FilePath = "oy3", Name = "wp3" });
    }


    private string minutesInput = "1";
    public string MinutesInput { get => minutesInput; set => SetProperty(ref minutesInput, value); }

    private string hoursInput = "0";
    public string HoursInput { get => hoursInput; set => SetProperty(ref hoursInput, value); }

    private string daysInput = "0";
    public string DaysInput { get => daysInput; set => SetProperty(ref daysInput, value); }

    private string weeksInput = "0";
    public string WeeksInput { get => weeksInput; set => SetProperty(ref weeksInput, value); }

    private string monthsInput = "0";
    public string MonthsInput { get => monthsInput; set => SetProperty(ref monthsInput, value); }

    public int TotalSeconds => 
        (((((int.Parse(MonthsInput) * 30 + int.Parse(WeeksInput) * 7) + 
        int.Parse(DaysInput)) * 24 + int.Parse(hoursInput)) * 60) + int.Parse(minutesInput)) * 60;


    [RelayCommand]
    public void MoveWallpaperUp(Wallpaper wp)
    {
        var i = WallpaperQueue.IndexOf(wp);
        if (i > 0) WallpaperQueue.Move(i, i - 1);
    }

    [RelayCommand]
    public void MoveWallpaperDown(Wallpaper wp)
    {
        var i = WallpaperQueue.IndexOf(wp);
        if (i >= 0 && i < WallpaperQueue.Count - 1) WallpaperQueue.Move(i, i + 1);
    }

    [RelayCommand]
    public void ClearQueue()
    {
        WallpaperQueue.Clear();
    }

}
