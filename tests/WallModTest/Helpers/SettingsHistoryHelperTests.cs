using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using WallMod.Helpers;

namespace WallModTest.Helpers;

public class SettingsHistoryHelperTests
{
    private readonly string _tempAppData;
    private readonly SettingsHistoryHelper _helper;
    private readonly string _settingsFile;

    public SettingsHistoryHelperTests()
    {
        _tempAppData = Path.Combine(Path.GetTempPath(), "WallModTests_" + Guid.NewGuid());
        Directory.CreateDirectory(_tempAppData);
        Environment.SetEnvironmentVariable("LOCALAPPDATA", _tempAppData);

        _helper = new SettingsHistoryHelper();

        var storageField = typeof(SettingsHistoryHelper)
            .GetField("appStorageHelper", BindingFlags.NonPublic | BindingFlags.Instance)!;
        var storageObj = (AppStorageHelper)storageField.GetValue(_helper)!;
        _settingsFile = storageObj.appSettingsHistoryFile;
    }

    [Fact]
    public void Constructor_CreatesSettingsFileWithDefaults()
    {
        Assert.True(File.Exists(_settingsFile));

        Dictionary<string, string> dict = _helper.LoadSettingsJson();
        string[] requiredKeys = new[]
        {
                "AllowSaveHistory", "AutoOpenLastChosenDirectoryOnAppStart",
                "CPUThreadsAllocated", "AspectRatioFilter", "AppBackgroundColour"
            };

        foreach (var key in requiredKeys)
            Assert.True(dict.ContainsKey(key), $"Missing key {key}");
    }

    [Fact]
    public void UpdateSetting_ChangesValueOnDisk()
    {
        _helper.UpdateSetting("AllowSaveHistory", "False");

        var reloaded = _helper.LoadSettingsJson();
        Assert.Equal("False", reloaded["AllowSaveHistory"]);
    }

    [Fact]
    public void GetSettingEntry_ReturnsCorrectValue()
    {
        _helper.UpdateSetting("ShowFoldersFilter", "False");

        string val = _helper.GetSettingEntry("ShowFoldersFilter");
        Assert.Equal("False", val);
    }

    public void Dispose()
    {
        Environment.SetEnvironmentVariable("LOCALAPPDATA", null);
        if (Directory.Exists(_tempAppData))
            Directory.Delete(_tempAppData, true);
    }
}
