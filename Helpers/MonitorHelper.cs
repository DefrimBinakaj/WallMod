using Avalonia.Controls;
using Avalonia;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WallMod.Models;
using System.Runtime.InteropServices;
using System.Threading;

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

        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            return monitors;


        var dw = (IDesktopWallpaper)new DesktopWallpaper();
        uint count = dw.GetMonitorDevicePathCount();


        var screens = mainWindow.Screens;

        foreach (var screen in screens.All)
        {

            // gen current id and attach to the correct monitor object
            string currMonId = "";
            for (uint i = 0; i < dw.GetMonitorDevicePathCount(); i++)
            {
                string currMonitorId = dw.GetMonitorDevicePathAt(i);
                if (dw.GetMonitorRECT(currMonitorId).Left == screen.Bounds.X && dw.GetMonitorRECT(currMonitorId).Top == screen.Bounds.Y)
                {
                    currMonId = currMonitorId;
                }
            }


            monitors.Add(new MonitorInfo
            {
                MonitorIdPath = currMonId,
                Bounds = screen.Bounds, // Bounds = [ positionX, positionY, width, height ]
                WorkingArea = screen.WorkingArea,
                IsPrimary = screen.IsPrimary ? "main" : "", // for main monitor, set to "main" - for others, set to empty string
                CurrWallpaper = new Wallpaper(),
                FillColour = "Navy",
            });
            Debug.WriteLine(
                " -- id = " + currMonId +
                " -- bounds = " + screen.Bounds + 
                " -- workingarea = " + screen.WorkingArea + 
                " -- isprimary = " + screen.IsPrimary);

            // adjust bounds and workingarea to fit in the UI
            double positionScaleFactor = 0.03;
            double sizeScaleFactor = 0.03;
            monitors.Last().UIBounds = new PixelRect(
                Convert.ToInt32(screen.Bounds.X * positionScaleFactor) + 80, 
                Convert.ToInt32(screen.Bounds.Y * positionScaleFactor) + 70, 
                Convert.ToInt32(screen.Bounds.Width * sizeScaleFactor), 
                Convert.ToInt32(screen.Bounds.Height * sizeScaleFactor));
            Debug.WriteLine("UIBounds = " + monitors.Last().UIBounds);

            if (monitors.Last().IsPrimary == "main")
            {
                monitors.Last().StrokeColour = "Gold";
            }
            else
            {
                monitors.Last().StrokeColour = "White";
            }

        }

        return monitors;
    }



}
