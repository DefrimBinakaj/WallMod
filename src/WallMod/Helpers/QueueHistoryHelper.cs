using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using WallMod.Models;

namespace WallMod.Helpers;

public class QueueHistoryHelper
{
    private class QueueEntry
    {
        public string FilePath { get; set; } = "";
        public int? CropX { get; set; }
        public int? CropY { get; set; }
        public int? CropWidth { get; set; }
        public int? CropHeight { get; set; }
        public string? CropMonitorId { get; set; }
    }

    private readonly string queueFile;

    public QueueHistoryHelper()
    {
        AppStorageHelper appStorageHelper = new AppStorageHelper();
        appStorageHelper.InitAppStorage();
        queueFile = appStorageHelper.appQueueHistoryFile
                    ?? Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                        "WallMod", "WallModQueueHistory.json");
    }

    public void SaveQueue(IEnumerable<Wallpaper> queue)
    {
        try
        {
            var entries = queue
                .Where(wp => !string.IsNullOrEmpty(wp.FilePath))
                .Select(wp => new QueueEntry
                {
                    FilePath = wp.FilePath,
                    CropX = wp.CropX,
                    CropY = wp.CropY,
                    CropWidth = wp.CropWidth,
                    CropHeight = wp.CropHeight,
                    CropMonitorId = wp.CropMonitorId,
                })
                .ToList();

            File.WriteAllText(queueFile, JsonSerializer.Serialize(entries));
        }
        catch (Exception ex)
        {
            AppStorageHelper.LogCrash(ex);
        }
    }

    // heavy (thumbnail + colour classification per image) - call from a background thread
    public List<Wallpaper> LoadQueue()
    {
        var result = new List<Wallpaper>();
        try
        {
            if (!File.Exists(queueFile)) return result;

            var entries = JsonSerializer.Deserialize<List<QueueEntry>>(File.ReadAllText(queueFile));
            if (entries == null) return result;

            ImageHelper imageHelper = new ImageHelper();
            foreach (var entry in entries)
            {
                if (!File.Exists(entry.FilePath)) continue; // image moved/deleted since last run - self-heals

                Wallpaper wp = imageHelper.getWallpaperObjectFromPath(entry.FilePath);
                wp.CropX = entry.CropX;
                wp.CropY = entry.CropY;
                wp.CropWidth = entry.CropWidth;
                wp.CropHeight = entry.CropHeight;
                wp.CropMonitorId = entry.CropMonitorId;

                // cropped items display the crop region, matching what add-to-queue produced
                if (wp.CropX is int cx && wp.CropY is int cy &&
                    wp.CropWidth is int cw && wp.CropHeight is int ch && cw > 0 && ch > 0)
                {
                    var croppedThumb = ImageHelper.GetCroppedThumbnail(entry.FilePath, cx, cy, cw, ch);
                    if (croppedThumb != null)
                    {
                        wp.ImageThumbnailBitmap = croppedThumb;
                    }
                }

                result.Add(wp);
            }
        }
        catch (Exception ex)
        {
            AppStorageHelper.LogCrash(ex);
        }
        return result;
    }
}
