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

    public string appStorageFilePath;

    public void InitAppStorage()
    {
        // AppData/WallMod
        appStorageDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "WallMod"
        );
        Directory.CreateDirectory(appStorageDirectory);

        // dir:
        // AppData/WallMod/wallpaperHistory.json
        appStorageFilePath = Path.Combine(appStorageDirectory, "WallModStorageSheet.json");
    }
}
