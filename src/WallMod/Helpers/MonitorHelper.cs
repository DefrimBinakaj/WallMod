using Avalonia.Controls;
using Avalonia;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using WallMod.Models;
using Avalonia.Platform;

namespace WallMod.Helpers;

/**
 * class used for handling monitor detection/info
 */
public class MonitorHelper
{

    // preview-canvas monitor colours (selected monitors use the accent colour instead of IdleFill)
    public const string IdleFill = "#2E2A3A";
    public const string PrimaryStroke = "#E0B84D";
    public const string SecondaryStroke = "#B3FFFFFF";

    // monitor canvas size in the preview panel (must match the ItemsPanel size in MainWindow.axaml)
    public static readonly double CanvasWidth = 180;
    public static readonly double CanvasHeight = 60;

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
                        IsPrimary = screen.IsPrimary ? true : false,
                        CurrWallpaper = new Wallpaper(),
                        FillColour = IdleFill,
                        StrokeColour = screen.IsPrimary ? PrimaryStroke : SecondaryStroke,
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

        for (int i = 0; i < monitors.Count; i++)
            monitors[i].DisplayNumber = i + 1;

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
                IsPrimary = screen.IsPrimary ? true : false,
                CurrWallpaper = new Wallpaper(),
                FillColour = IdleFill,
                StrokeColour = screen.IsPrimary ? PrimaryStroke : SecondaryStroke,
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
        const int gap = 2; // px shaved off each side -> 4px between touching monitors

        double minX = monitors.Min(m => m.Bounds.X);
        double minY = monitors.Min(m => m.Bounds.Y);
        double maxX = monitors.Max(m => m.Bounds.X + m.Bounds.Width);
        double maxY = monitors.Max(m => m.Bounds.Y + m.Bounds.Height);

        double totalWidth = maxX - minX;
        double totalHeight = maxY - minY;

        double scaleFactor = Math.Min(CanvasWidth / totalWidth, CanvasHeight / totalHeight);

        // leftover space split evenly -> layout sits in the middle of the canvas
        double centreX = (CanvasWidth - totalWidth * scaleFactor) / 2;
        double centreY = (CanvasHeight - totalHeight * scaleFactor) / 2;

        foreach (var mon in monitors)
        {
            int uiX = (int)(centreX + (mon.Bounds.X - minX) * scaleFactor) + gap;
            int uiY = (int)(centreY + (mon.Bounds.Y - minY) * scaleFactor) + gap;
            int uiW = Math.Max(4, (int)(mon.Bounds.Width * scaleFactor) - gap * 2);
            int uiH = Math.Max(4, (int)(mon.Bounds.Height * scaleFactor) - gap * 2);

            mon.UIBounds = new PixelRect(uiX, uiY, uiW, uiH);

            Debug.WriteLine("uibounds for " + mon.MonitorIdPath + " = " + mon.UIBounds);
        }
    }

    // builds a miniature copy of the monitor layout for a queue-item badge;
    // the target monitor is filled with the accent colour, the rest other colour
    public static List<MonitorInfo> BuildMiniMonitorBadge(IEnumerable<MonitorInfo> monitors,
                                                          string? targetMonitorId,
                                                          string accentColour,
                                                          double targetWidth = 30,
                                                          double targetHeight = 14)
    {
        var monList = monitors.ToList();
        var result = new List<MonitorInfo>();
        if (monList.Count == 0) return result;

        double minX = monList.Min(m => m.Bounds.X);
        double minY = monList.Min(m => m.Bounds.Y);
        double maxX = monList.Max(m => m.Bounds.X + m.Bounds.Width);
        double maxY = monList.Max(m => m.Bounds.Y + m.Bounds.Height);

        double scaleFactor = Math.Min(targetWidth / (maxX - minX), targetHeight / (maxY - minY));

        foreach (var mon in monList)
        {
            result.Add(new MonitorInfo
            {
                MonitorIdPath = mon.MonitorIdPath,
                Bounds = mon.Bounds,
                WorkingArea = mon.WorkingArea,
                IsPrimary = mon.IsPrimary,
                CurrWallpaper = new Wallpaper(),
                StrokeColour = "White",
                FillColour = (targetMonitorId == null || mon.MonitorIdPath == targetMonitorId) ? accentColour : "Navy",
                UIBounds = new PixelRect(
                    (int)((mon.Bounds.X - minX) * scaleFactor),
                    (int)((mon.Bounds.Y - minY) * scaleFactor),
                    Math.Max(3, (int)(mon.Bounds.Width * scaleFactor)),
                    Math.Max(2, (int)(mon.Bounds.Height * scaleFactor))),
            });
        }
        return result;
    }
}
