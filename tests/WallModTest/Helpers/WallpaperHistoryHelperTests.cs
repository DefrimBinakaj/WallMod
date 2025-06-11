using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WallMod.Helpers;

namespace WallModTest.Helpers;

public class WallpaperHistoryHelperTests
{
    [Fact]
    public void AddToHistory_UpdatesEntries()
    {
        var helper = new WallpaperHistoryHelper();
        var testPath = @"C:\test.jpg";

        helper.AddToHistory(testPath);
        var history = helper.LoadHistoryJson();

        Assert.Contains(testPath, history);
    }

    [Fact]
    public void RemoveFromHistory_UpdatesEntries()
    {
        var helper = new WallpaperHistoryHelper();
        var testPath = @"C:\test.jpg";
        helper.AddToHistory(testPath);

        helper.RemoveHistoryEntry(testPath);
        var history = helper.LoadHistoryJson();

        Assert.DoesNotContain(testPath, history);
    }
}
