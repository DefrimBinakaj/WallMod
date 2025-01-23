using Avalonia;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace WallMod.Models;

/**
 * Model for monitor object
 */
public class MonitorInfo: INotifyPropertyChanged
{
    public string MonitorIdPath { get; set; }
    public PixelRect Bounds { get; set; }
    public PixelRect WorkingArea { get; set; }
    public string IsPrimary { get; set; }
    public Wallpaper CurrWallpaper { get; set; }
    public PixelRect UIBounds { get; set; }

    private string fillColour;
    public string FillColour
    {
        get => fillColour;
        set
        {
            if (value != fillColour)
            {
                fillColour = value;
                OnPropertyChanged();
            }
        }
    }

    public event PropertyChangedEventHandler PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string propName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propName));
    }
}
