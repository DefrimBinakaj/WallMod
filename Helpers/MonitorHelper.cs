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
                Rect currMonitorRECT = dw.GetMonitorRECT(currMonitorId);
                if (currMonitorRECT.Left == screen.Bounds.X && currMonitorRECT.Top == screen.Bounds.Y)
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
                StrokeColour = screen.IsPrimary ? "Gold" : "White",
            });
            Debug.WriteLine(
                " -- id = " + currMonId +
                " -- bounds = " + screen.Bounds + 
                " -- workingarea = " + screen.WorkingArea + 
                " -- isprimary = " + screen.IsPrimary);


        }



        // UIBounds manipulation code --------------------------------
        double minX = monitors.Min(m => m.Bounds.X);
        double minY = monitors.Min(m => m.Bounds.Y);
        double maxX = monitors.Max(m => m.Bounds.X + m.Bounds.Width);
        double maxY = monitors.Max(m => m.Bounds.Y + m.Bounds.Height);

        double totalWidth = maxX - minX;
        double totalHeight = maxY - minY;

        // target width and height for UI (made slightly less tall than axaml)
        double targetWidth = 240;
        double targetHeight = 100;

        double scaleX = targetWidth / totalWidth;
        double scaleY = targetHeight / totalHeight;
        double scaleFactor = Math.Min(scaleX, scaleY);


        foreach (var mon in monitors)
        {
            double offsetX = mon.Bounds.X - minX;
            double offsetY = mon.Bounds.Y - minY;

            int uiX = (int)(offsetX * scaleFactor);
            int uiY = (int)(offsetY * scaleFactor);
            int uiW = (int)(mon.Bounds.Width * scaleFactor);
            int uiH = (int)(mon.Bounds.Height * scaleFactor);

            // set final UIBounds
            mon.UIBounds = new PixelRect(uiX, uiY + 5, uiW, uiH);

            Debug.WriteLine("UIBounds for " + mon.MonitorIdPath + " = " + mon.UIBounds);
        }


        

        return monitors;
    }



}
