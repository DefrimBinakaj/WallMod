using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WallMod.Helpers;

/**
 * Class for creating AppData json/txt files where history/settings/logs are saved after app is closed
 */
public static class AppStorageHelper
{
    public static readonly string appStorageDirectory = Directory.CreateDirectory(
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "WallMod")).FullName;

    // AppData/WallMod/***.json
    public static readonly string appWallpaperHistoryFile = Path.Combine(appStorageDirectory, "WallModWallpaperHistory.json");
    public static readonly string appSettingsHistoryFile = Path.Combine(appStorageDirectory, "WallModSettingsHistory.json");
    public static readonly string appFavouritesFile = Path.Combine(appStorageDirectory, "WallModFavourites.json");
    public static readonly string appQueueHistoryFile = Path.Combine(appStorageDirectory, "WallModQueueHistory.json");
    public static readonly string appUIStateHistoryFile = Path.Combine(appStorageDirectory, "WallModUIStateHistory.json");
    public static string crashLogFile = Path.Combine(appStorageDirectory, "CrashLog.txt"); // not readonly so tests can redirect it

    // add error logs to crashlog file
    public static void LogCrash(Exception ex)
    {
        try
        {
            File.AppendAllText(crashLogFile, DateTime.Now + " ---->> " + ex + "\n");
        }
        catch
        {
            Debug.WriteLine("error logging crash!");
        }
    }
}
