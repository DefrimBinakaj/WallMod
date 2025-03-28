using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WallMod.Helpers;

/**
 * Class for creating AppData json file where history/settings are saved after app is closed
 */
public class AppStorageHelper
{
    public string appStorageDirectory;

    public string appWallpaperHistoryFile;

    public string appSettingsHistoryFile;

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
    }
}
