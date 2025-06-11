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
public class AppStorageHelper
{
    public string appStorageDirectory;

    public string appWallpaperHistoryFile;

    public string appSettingsHistoryFile;

    public static string crashLogFile;

    public void InitAppStorage()
    {
        // AppData/WallMod
        appStorageDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "WallMod"
        );
        Directory.CreateDirectory(appStorageDirectory);

        // AppData/WallMod/WallModWallpaperHistory.json
        appWallpaperHistoryFile = Path.Combine(appStorageDirectory, "WallModWallpaperHistory.json");

        // AppData/WallMod/WallModSettingsHistory.json
        appSettingsHistoryFile = Path.Combine(appStorageDirectory, "WallModSettingsHistory.json");

        // AppData/WallMod/WallModCrashLog.txt
        crashLogFile = Path.Combine(appStorageDirectory, "CrashLog.txt");

    }


    // add error logs to crashlog file
    public static void LogCrash(Exception ex)
    {
        try
        {
            string currLogOutput = DateTime.Now + " ---->> " + ex.ToString() + "\n";
            File.AppendAllText(crashLogFile, currLogOutput);
        }
        catch
        {
            Debug.WriteLine("error logging crash!");
        }
    }

    // NOT USED YET: get all contents from crashlog file
    public static string GetCrashLog()
    {
        if (!File.Exists(crashLogFile))
        {
            return "";
        }
        return File.ReadAllText(crashLogFile);
    }

}
