using Avalonia.Controls;
using Avalonia;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WallMod.Models;

namespace WallMod.Helpers;

/**
 * Class used for handling monitor detection/info
 */
public class MonitorHelper
{

    // used to get monitors on the pc and also scale them down for use in UI
    public static IEnumerable<MonitorInfo> GetMonitors(Window mainWindow)
    {
        if (mainWindow == null)
        {
            Debug.WriteLine("MainWindow is null.");
            return new List<MonitorInfo>();
        }

        var monitors = new List<MonitorInfo>();

        var screens = mainWindow.Screens;

        foreach (var screen in screens.All)
        {
            monitors.Add(new MonitorInfo
            {
                Bounds = screen.Bounds, // Bounds = [ positionX, positionY, width, height ]
                WorkingArea = screen.WorkingArea,
                IsPrimary = screen.IsPrimary ? "main" : "", // for main monitor, set to "main" - for others, set to empty string
                CurrWallpaper = new Wallpaper(),
                FillColour = "Navy",
            });
            Debug.WriteLine("monitor#" + screen + " -- bounds = " + screen.Bounds + " -- workingarea = " + screen.WorkingArea + " -- isprimary = " + screen.IsPrimary);

            // adjust bounds and workingarea to fit in the UI
            double scaleFactor = 0.035;
            monitors.Last().UIBounds = new PixelRect(Convert.ToInt32(screen.Bounds.X * scaleFactor), Convert.ToInt32(screen.Bounds.Y * scaleFactor), Convert.ToInt32(screen.Bounds.Width * scaleFactor), Convert.ToInt32(screen.Bounds.Height * scaleFactor));

            Debug.WriteLine("UIBounds = " + monitors.Last().UIBounds);

        }

        return monitors;
    }
}
