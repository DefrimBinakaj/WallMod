using Avalonia.Media.Imaging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WallMod.Models;

/*
 * Model for wallpaper object
 */
public class Wallpaper
{
    public string FilePath { get; set; }
    public Bitmap? ImageBitmap { get; set; }
    public Bitmap? ImageThumbnailBitmap { get; set; }
    public string? Name { get; set; }
    public string? Category { get; set; }
}
