using Avalonia.Controls;
using Avalonia;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using WallMod.Models;
using Avalonia.Platform;

namespace WallMod.Helpers
{
    /**
     * class used for handling monitor detection/info
     */
    public class MonitorHelper
    {
        // used to get monitors on the pc and also scale them down for use in ui
        public static IEnumerable<MonitorInfo> GetMonitors(Window mainWindow)
        {
            if (mainWindow == null)
            {
                Debug.WriteLine("MainWindow is null.");
                return new List<MonitorInfo>();
            }

            var monitors = new List<MonitorInfo>();

            // if non-windows, just do fallback
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                // do the linux/mac approach
                monitors.AddRange(getAgnosticMonitors(mainWindow));
            }
            else
            {
                // do windows approach
                try
                {
                    var dw = (IDesktopWallpaper)new DesktopWallpaper();
                    uint count = dw.GetMonitorDevicePathCount();

                    var screens = mainWindow.Screens;
                    int fallbackCounter = 0;

                    foreach (var screen in screens.All)
                    {
                        // attempt to find a real idesktopwallpaper monitor id by coordinate matching
                        string matchedId = findWindowsMonitorId(dw, count, screen);

                        // if no match found, assign a synthetic fallback id
                        if (string.IsNullOrEmpty(matchedId))
                        {
                            matchedId = "WIN_MON_" + fallbackCounter;
                            fallbackCounter++;
                        }

                        monitors.Add(new MonitorInfo
                        {
                            MonitorIdPath = matchedId,
                            Bounds = screen.Bounds,
                            WorkingArea = screen.WorkingArea,
                            IsPrimary = screen.IsPrimary ? "main" : "",
                            CurrWallpaper = new Wallpaper(),
                            FillColour = "Navy",
                            StrokeColour = screen.IsPrimary ? "Gold" : "White",
                        });

                        Debug.WriteLine("windows monitor => id=" + matchedId +
                                        " bounds=" + screen.Bounds +
                                        " isprimary=" + screen.IsPrimary);
                    }
                }
                catch (Exception ex)
                {
                    // if something fails, fallback to linux/mac approach
                    Debug.WriteLine("windows com logic failed: " + ex.Message);
                    monitors.AddRange(getAgnosticMonitors(mainWindow));
                }
            }

            if (monitors.Count == 0)
                return monitors;

            // do the ui scaling for bounding rectangles
            scaleUIBounds(monitors);

            return monitors;
        }

        // tries to match an avalonia screen to a real idesktopwallpaper monitor id
        private static string findWindowsMonitorId(IDesktopWallpaper dw, uint count, Screen screen)
        {
            // coordinate tolerance
            const int TOL = 5;

            for (uint i = 0; i < count; i++)
            {
                string testId = dw.GetMonitorDevicePathAt(i);

                try
                {
                    Rect r = dw.GetMonitorRECT(testId);

                    bool leftClose = Math.Abs(r.Left - screen.Bounds.X) <= TOL;
                    bool topClose = Math.Abs(r.Top - screen.Bounds.Y) <= TOL;

                    if (leftClose && topClose)
                    {
                        // found a match
                        return testId;
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine("getmonitorrect failed for " + testId + ": " + ex.Message);
                }
            }

            return "";
        }

        // for non-windows systems, just assign synthetic ids like "linux_mon_0"
        private static IEnumerable<MonitorInfo> getAgnosticMonitors(Window mainWindow)
        {
            var list = new List<MonitorInfo>();
            var screens = mainWindow.Screens;
            int index = 0;

            foreach (var screen in screens.All)
            {
                string syntheticId = "LINUX_MON_" + index;
                index++;

                var info = new MonitorInfo
                {
                    MonitorIdPath = syntheticId,
                    Bounds = screen.Bounds,
                    WorkingArea = screen.WorkingArea,
                    IsPrimary = screen.IsPrimary ? "main" : "",
                    CurrWallpaper = new Wallpaper(),
                    FillColour = "Navy",
                    StrokeColour = screen.IsPrimary ? "Gold" : "White",
                };

                list.Add(info);

                Debug.WriteLine("linux/mac monitor => id=" + syntheticId +
                                " bounds=" + screen.Bounds +
                                " isprimary=" + screen.IsPrimary);
            }

            return list;
        }

        // scales each monitor's bounds so they fit in a small ui region (e.g. 240x100)
        private static void scaleUIBounds(List<MonitorInfo> monitors)
        {
            double minX = monitors.Min(m => m.Bounds.X);
            double minY = monitors.Min(m => m.Bounds.Y);
            double maxX = monitors.Max(m => m.Bounds.X + m.Bounds.Width);
            double maxY = monitors.Max(m => m.Bounds.Y + m.Bounds.Height);

            double totalWidth = maxX - minX;
            double totalHeight = maxY - minY;

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
                int uiY = (int)(offsetY * scaleFactor) + 5;
                int uiW = (int)(mon.Bounds.Width * scaleFactor);
                int uiH = (int)(mon.Bounds.Height * scaleFactor);

                mon.UIBounds = new PixelRect(uiX, uiY, uiW, uiH);

                Debug.WriteLine("uibounds for " + mon.MonitorIdPath + " = " + mon.UIBounds);
            }
        }
    }
}
