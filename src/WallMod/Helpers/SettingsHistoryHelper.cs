using Avalonia.Media;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace WallMod.Helpers;

public class SettingsHistoryHelper
{

    AppStorageHelper appStorageHelper;
    public SettingsHistoryHelper()
    {
        appStorageHelper = new AppStorageHelper();
        appStorageHelper.InitAppStorage();
        initSettingsFileDict();
    }

    // create the keys for all settings in the file
    public void initSettingsFileDict()
    {

        Dictionary<string, string> defaultSettings = new Dictionary<string, string>
        {
            { "AllowSaveHistory", "True" },
            { "AutoOpenLastChosenDirectoryOnAppStart", "False" },
            { "LastChosenDirectory", "" },
            { "RememberFilterSettings", "False" },
            { "AppBackgroundColour", "purpley" },
            { "StayRunningInBackground", "False" },
            { "CPUThreadsAllocated", Math.Round(Environment.ProcessorCount * 0.85).ToString() },
            // filters (used in same dict to avoid simul I/O operations)
            { "SearchFilter", "" },
            { "ShowFoldersFilter", "True" },
            { "ImgPropertySort", "Name" },
            { "AspectRatioFilter", "All" },
            // colours
            { "SelectedBackgroundColour", Color.FromArgb(110, 50, 0, 190).ToString() },
            { "SelectedPrimaryAccentColour", Color.FromArgb(255, 64, 224, 208).ToString() },
            { "SelectedWallpaperCollectionColour", Color.FromArgb(255, 0, 0, 0).ToString() },
            { "SelectedPreviewBackgroundColour", Color.FromArgb(200, 97, 97, 97).ToString() },
        };

        Dictionary<string, string> currSettings;

        if (File.Exists(appStorageHelper.appSettingsHistoryFile))
        {
            string json = File.ReadAllText(appStorageHelper.appSettingsHistoryFile);
            currSettings = JsonSerializer.Deserialize<Dictionary<string, string>>(json) ?? new Dictionary<string, string>();
        }
        else
        {
            currSettings = new Dictionary<string, string>();
        }

        // account for changes in structure by add any missing keys from defaultsettings
        foreach (var keyval in defaultSettings)
        {
            currSettings.TryAdd(keyval.Key, keyval.Value);
        }


        string newJson = JsonSerializer.Serialize(currSettings, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(appStorageHelper.appSettingsHistoryFile, newJson);

    }

    public Dictionary<string, string> LoadSettingsJson()
    {
        if (!File.Exists(appStorageHelper.appSettingsHistoryFile))
            return null;

        // read + deserialize JSON to string list
        string json = File.ReadAllText(appStorageHelper.appSettingsHistoryFile);
        return JsonSerializer.Deserialize<Dictionary<string, string>>(json) ?? new Dictionary<string, string>();
    }


    public void UpdateSetting(string settingType, string desiredValue)
    {
        Dictionary<string,string> historyDict = LoadSettingsJson();

        // update (if it exists in the dict)
        if (historyDict.ContainsKey(settingType))
        {
            historyDict[settingType] = desiredValue;
        }
        else
        {
            Debug.WriteLine("!!!!!!!! SETTING DOES NOT EXIST");
        }

        // save updated history
        string json = JsonSerializer.Serialize(historyDict);
        File.WriteAllText(appStorageHelper.appSettingsHistoryFile, json);
    }


    public string GetSettingEntry(string settingType)
    {
        Dictionary<string, string> historyDict = LoadSettingsJson();

        string valueRequested = "";

        if (historyDict.ContainsKey(settingType))
        {
            valueRequested = historyDict[settingType];
        }
        else
        {
            Debug.WriteLine("!!!!!!!! SETTING DOES NOT EXIST");
        }

        return valueRequested;

    }

}
