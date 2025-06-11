using Avalonia.Controls;
using Avalonia.Media.Imaging;
using Avalonia.Platform.Storage;
using SkiaSharp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using WallMod.Helpers;

namespace WallModTest.Helpers;

public class ImageHelperTests
{

    [Fact]
    public void RgbToHue_RedDominant_ReturnsCorrectHue()
    {
        // Arrange
        int r = 255, g = 50, b = 50;

        // Act
        var hue = ImageHelper.RgbToHue(r, g, b);

        // Assert (red should be near 0°)
        Assert.InRange(hue, -15, 15);
    }

    [Fact]
    public void RgbToHue_GreenDominant_ReturnsCorrectHue()
    {
        // Arrange
        int r = 50, g = 255, b = 50;

        // Act
        var hue = ImageHelper.RgbToHue(r, g, b);

        // Assert (green should be ~120°)
        Assert.InRange(hue, 105, 135);
    }

}
