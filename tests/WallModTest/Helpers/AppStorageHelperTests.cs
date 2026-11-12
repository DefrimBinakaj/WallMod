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
        Assert.True(Directory.Exists(AppStorageHelper.appStorageDirectory));
        Assert.EndsWith("WallModWallpaperHistory.json", AppStorageHelper.appWallpaperHistoryFile);
        Assert.EndsWith("WallModSettingsHistory.json", AppStorageHelper.appSettingsHistoryFile);
        Assert.EndsWith("CrashLog.txt", AppStorageHelper.crashLogFile);
    }

    [Fact]
    public void LogCrash_WritesExceptionLine()
    {
        // redirect the crash log to a temp file so we don't touch real AppData
        string originalCrashLogFile = AppStorageHelper.crashLogFile;
        string tempFile = Path.Combine(Path.GetTempPath(), $"crash-{Guid.NewGuid()}.txt");
        AppStorageHelper.crashLogFile = tempFile;

        try
        {
            AppStorageHelper.LogCrash(new InvalidOperationException("unit-test crash"));

            Assert.Contains("unit-test crash", File.ReadAllText(tempFile), StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            // always put the real path back, so the other test still sees CrashLog.txt
            AppStorageHelper.crashLogFile = originalCrashLogFile;
            File.Delete(tempFile);
        }
    }

}
