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
    public required string MonitorIdPath { get; set; }
    public PixelRect Bounds { get; set; }
    public PixelRect WorkingArea { get; set; }
    public bool IsPrimary { get; set; }
    public Wallpaper? CurrWallpaper { get; set; }
    public PixelRect UIBounds { get; set; }
    // 1-based, matches the 1-4 hotkeys (MonitorList order)
    public int DisplayNumber { get; set; }
    public string Description => $"Monitor {DisplayNumber}{(IsPrimary ? " (Primary)" : "")}  ·  {Bounds.Width} x {Bounds.Height}";


    private string? fillColour;
    public string? FillColour { 
        get => fillColour; 
        set { if (value != fillColour) { fillColour = value; OnPropertyChanged(); } } 
    }

    private string? strokeColour;
    public string? StrokeColour { 
        get => strokeColour; 
        set { if (value != strokeColour) { strokeColour = value; OnPropertyChanged(); } } 
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? propName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propName));
    }
}
