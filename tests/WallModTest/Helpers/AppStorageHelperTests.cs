using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WallMod.Helpers;

namespace WallModTest.Helpers;

public class AppStorageHelperTests
{
    [Fact]
    public void InitAppStorage_CreatesDirectoryAndSetsPaths()
    {
        var helper = new AppStorageHelper();

        helper.InitAppStorage();

        // directory is created
        Assert.True(Directory.Exists(helper.appStorageDirectory));

        // paths end with the expected filenames
        Assert.EndsWith("WallModWallpaperHistory.json", helper.appWallpaperHistoryFile);
        Assert.EndsWith("WallModSettingsHistory.json", helper.appSettingsHistoryFile);
        Assert.EndsWith("CrashLog.txt", AppStorageHelper.crashLogFile);
    }

    [Fact]
    public void LogCrash_WritesExceptionLine()
    {
        // redirect the crash log to a temp file so we don't touch real AppData
        string tempFile = Path.Combine(Path.GetTempPath(), $"crash-{Guid.NewGuid()}.txt");
        AppStorageHelper.crashLogFile = tempFile;

        var ex = new InvalidOperationException("unit-test crash");

        AppStorageHelper.LogCrash(ex);

        Assert.Contains("unit-test crash", File.ReadAllText(tempFile), StringComparison.OrdinalIgnoreCase);

        File.Delete(tempFile);
    }

}
