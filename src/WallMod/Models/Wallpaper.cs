using Avalonia.Media.Imaging;
using ColorThiefDotNet;
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
    public int? ImageWidth { get; set; }
    public int? ImageHeight { get; set; }
    public Bitmap? ImageThumbnailBitmap { get; set; }
    public string? Name { get; set; }
    public DateTime? Date { get; set; }
    public double? ColourCategory { get; set; }

    public bool? IsDirectory { get; set; }


    // crop metadata (original-image pixels) + the monitor it was drawn for; all null = full image
    public int? CropX { get; set; }
    public int? CropY { get; set; }
    public int? CropWidth { get; set; }
    public int? CropHeight { get; set; }
    public string? CropMonitorId { get; set; }
    
    // tiny monitor-layout badge for the queue (accent = target monitor); built at runtime, never persisted
    public List<MonitorInfo>? MonitorBadge { get; set; }
}
