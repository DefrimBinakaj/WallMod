using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace WallMod.Helpers;

/**
 * Class for loading/viewing/adding image history
 */
public class HistoryHelper
{
    AppStorageHelper appStorageHelper;

    public HistoryHelper()
    {
        appStorageHelper = new AppStorageHelper();
        appStorageHelper.InitAppStorage();
    }


    public void AddToHistory(string wallpaperPath)
    {
        // load existing history
        var history = LoadHistory();

        // remove existing entry to avoid duplicates
        history.Remove(wallpaperPath);

        // insert in front
        history.Insert(0, wallpaperPath);

        // limit history to certain amount (eg.20) items [OPTIONAL]
        // if (history.Count > 20)
        //     history.RemoveAt(history.Count - 1);

        // save updated history
        string json = JsonSerializer.Serialize(history);
        File.WriteAllText(appStorageHelper.appStorageFilePath, json);

    }

    public List<string> LoadHistory()
    {
        if (!File.Exists(appStorageHelper.appStorageFilePath))
            return new List<string>();

        // read + deserialize JSON to string list
        string json = File.ReadAllText(appStorageHelper.appStorageFilePath);
        return JsonSerializer.Deserialize<List<string>>(json) ?? new List<string>();
    }


    // NOT CURRENTLY USED [OPTIONAL]
    // get windows wallpaper history (NOT used in app; used in windows settings)
    public void ViewWindowsHistory()
    {
        string registryPath = @"Software\Microsoft\Windows\CurrentVersion\Explorer\Wallpapers";
        using (var wallpkey = Registry.CurrentUser.OpenSubKey(registryPath))
        {
            if (wallpkey != null)
            {
                for (int i = 0; i < 5; i++)
                {
                    string valueName = $"BackgroundHistoryPath{i}";
                    var path = wallpkey.GetValue(valueName) as string;

                    if (!string.IsNullOrEmpty(path))
                    {
                        // WallpaperHistoryList.Add(path);
                        Debug.WriteLine(path);
                    }
                }
            }
        }

    }
}
