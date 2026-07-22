using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace WallMod.Helpers;

public class UIHistoryHelper
{
    
    AppStorageHelper appStorageHelper;
    public UIHistoryHelper()
    {
        appStorageHelper = new AppStorageHelper();
        appStorageHelper.InitAppStorage();
    }

    public UIStateDTO Load()
    {
        try
        {
            if (File.Exists(appStorageHelper.appUIStateHistoryFile))
            {
                var state = JsonSerializer.Deserialize<UIStateDTO>(File.ReadAllText(appStorageHelper.appUIStateHistoryFile));
                if (state != null) return state;
            }
        }
        catch (Exception ex) { AppStorageHelper.LogCrash(ex); }
        return new UIStateDTO();
    }

    public void Save(UIStateDTO state)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(appStorageHelper.appUIStateHistoryFile)!);
            File.WriteAllText(appStorageHelper.appUIStateHistoryFile, JsonSerializer.Serialize(state));
        }
        catch (Exception ex) { AppStorageHelper.LogCrash(ex); }
    }

}

public class UIStateDTO
{
    public double WindowWidth { get; set; } = 1500;
    public double WindowHeight { get; set; } = 750;
    public int? WindowLeft { get; set; } = null;     // null = never saved -> let Avalonia center
    public int? WindowTop { get; set; } = null;
    public bool IsMaximized { get; set; } = false;
    public double LeftColumnStars { get; set; } = 2;
    public double RightColumnStars { get; set; } = 1;
    public double ThumbnailZoomLevel { get; set; } = 150;
}
