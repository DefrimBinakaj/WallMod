using Metsys.Bson;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WallMod.Helpers;

namespace WallModTest.Helpers;

public class FileExplorerHelperTests
{
    private readonly FileExporerHelper _helper = new();

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("Z:\\this\\file\\doesnotexist.png")]
    public void OpenFileInExplorer_InvalidInput_DoesNotThrow(string path)
    {
        var ex = Record.Exception(() => _helper.OpenFileInExplorer(path));
        Assert.Null(ex);
    }


    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("Z:\\this\\folder\\doesnotexist")]
    public void OpenFolderInExplorer_InvalidInput_DoesNotThrow(string path)
    {
        var ex = Record.Exception(() => _helper.OpenFolderInExplorer(path));
        Assert.Null(ex);
    }
}
