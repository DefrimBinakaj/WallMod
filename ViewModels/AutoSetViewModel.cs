using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WallMod.Models;
using WallMod.State;

namespace WallMod.ViewModels;

public partial class AutoSetViewModel : ObservableObject
{
    
    private readonly UniversalAppStore uniVM = App.Services!.GetRequiredService<UniversalAppStore>();

    // create WallpaperQueue replica which is the same as universal value
    public ObservableCollection<Wallpaper> WallpaperQueue => uniVM.WallpaperQueue;


    public AutoSetViewModel(UniversalAppStore universalVM)
    {
        uniVM = universalVM;
    }


    private int? minutesInput = 1;
    public int? MinutesInput { get => minutesInput; set => SetProperty(ref minutesInput, value); }

    private int? hoursInput = 0;
    public int? HoursInput { get => hoursInput; set => SetProperty(ref hoursInput, value); }

    private int? daysInput = 0;
    public int? DaysInput { get => daysInput; set => SetProperty(ref daysInput, value); }

    private int? weeksInput = 0;
    public int? WeeksInput { get => weeksInput; set => SetProperty(ref weeksInput, value); }

    private int? monthsInput = 0;
    public int? MonthsInput { get => monthsInput; set => SetProperty(ref monthsInput, value); }

    // must change this to account for potential null vals
    public int? TotalSeconds => 
        (((((MonthsInput * 30 + WeeksInput * 7) + DaysInput) * 24 + hoursInput) * 60) + minutesInput) * 60;



    // queue layout switching 

    private bool customQueueViewVisible;
    public bool CustomQueueViewVisible { get => customQueueViewVisible; set => SetProperty(ref customQueueViewVisible, value); }

    private bool randomQueueViewVisible;
    public bool RandomQueueViewVisible { get => randomQueueViewVisible; set => SetProperty(ref randomQueueViewVisible, value); }

    [RelayCommand] public void queueChoiceCommand(string choice) => queueChoiceExec(choice);

    private void queueChoiceExec(string selectedChoice)
    {
        if (selectedChoice == "Custom")
        {
            CustomQueueViewVisible = true;
            RandomQueueViewVisible = false;
        }
        else if (selectedChoice == "Random")
        {
            CustomQueueViewVisible = false;
            RandomQueueViewVisible = true;
        }
    }

}
