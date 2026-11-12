using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using ColorThiefDotNet;
using DataJuggler.PixelDatabase;
using Microsoft.Extensions.DependencyInjection;
using SkiaSharp;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using WallMod.Models;
using WallMod.State;
using WallMod.ViewModels;

namespace WallMod.Helpers;

/**
 * Class used for handling selecting/loading/converting/compressing/handling images
 */
public class ImageHelper
{

    private readonly UniversalAppStore uniVM = App.Services!.GetRequiredService<UniversalAppStore>();

    public static HashSet<string> SupportedExtensions = new(StringComparer.OrdinalIgnoreCase) { ".jpg", ".jpeg", ".png", ".bmp" };

    private const int MaxCachedImages = 10000; // ~10-20 KB each
    private const int ThumbnailWidth = 250, ThumbnailHeight = 200;

    // decoded once and shared by every tile
    private static readonly Lazy<Bitmap> folderIcon = new(() => new Bitmap(AssetLoader.Open(new Uri("avares://Wallmod/Assets/folderimg.png"))));

    private static readonly string MirrorDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "WallMod", "mirrored");

    // everything one decode of an image produces; Thumbnail is null if the file couldn't be decoded
    private sealed record ImageData(byte[]? Thumbnail, int Width, int Height, double Hue);
    private static readonly ImageData unreadableImage = new(null, 0, 0, 0);
    private static readonly ConcurrentDictionary<string, ImageData> imageCache = new();

    private int allocCPUThreads => Math.Max(1, uniVM.CPUThreadsAllocated);


    // ===== wallpaper objects ======================================================================

    // complete wallpaper for one file (gallery, favourites, history, queue, uploads, random autoset, mirror)
    public Wallpaper getWallpaperObjectFromPath(string imgFilePath)
    {
        var file = new FileInfo(imgFilePath);
        var imgData = GetImageData(file);

        return new Wallpaper
        {
            FilePath = imgFilePath,
            ImageBitmap = null, // WE DO NOT NEED THIS IN ORDER TO SET BACKGROUND - uses filepath
            ImageWidth = imgData.Width,
            ImageHeight = imgData.Height,
            ImageThumbnailBitmap = ToBitmap(imgData.Thumbnail),
            ColourCategory = imgData.Hue,
            Name = file.Name,
            Date = file.LastWriteTime,
            IsDirectory = false,
        };
    }

    // folder tiles (ready to show) + the supported image files in a folder; null = folder can't be opened
    public (List<Wallpaper> folderTiles, List<FileInfo> imgFiles)? getDirecContents(string folderChoice)
    {
        try
        {
            var folderTiles = Directory.GetDirectories(folderChoice).Select(subDir => new Wallpaper
            {
                FilePath = subDir,
                Name = Path.GetFileName(subDir),
                IsDirectory = true,
                ImageThumbnailBitmap = folderIcon.Value,
                Date = Directory.GetLastWriteTime(subDir),
                FolderItemCount = CountFolderItems(subDir)
            }).ToList();

            var imgFiles = new DirectoryInfo(folderChoice).EnumerateFiles()
                                                          .Where(file => SupportedExtensions.Contains(file.Extension))
                                                          .ToList();
            return (folderTiles, imgFiles);
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or DirectoryNotFoundException)
        {
            Debug.WriteLine($"!!!  Access denied to {folderChoice} - Skipping loading func");
            return null;
        }
    }

    // decodes in parallel (allocCPUThreads at a time) but hands wallpapers back in the given order,
    // each one as soon as it and everything before it is ready -> the gallery fills left to right, line by line
    public async IAsyncEnumerable<Wallpaper> LoadWallpapersInOrder(IEnumerable<string> imgFilePaths, [EnumeratorCancellation] CancellationToken token = default)
    {
        var semaphore = new SemaphoreSlim(allocCPUThreads);

        // starts every load now; the semaphore keeps only allocCPUThreads decoding at once
        var tasks = imgFilePaths.Select(filePath => Task.Run(async () =>
        {
            await semaphore.WaitAsync(token);
            try { return getWallpaperObjectFromPath(filePath); }
            finally { semaphore.Release(); }
        }, token)).ToList();

        foreach (var task in tasks)
        {
            yield return await task;
        }
    }

    // queue tile showing only the cropped region (crop in original-image pixels)
    public static Bitmap? GetCroppedThumbnail(string filePath, int x, int y, int w, int h)
    {
        var (resizedBitmap, _, _) = GetThumbNailFromPath(filePath, new SKRectI(x, y, x + w, y + h));
        using (resizedBitmap) return resizedBitmap == null ? null : ToBitmap(EncodeJpeg(resizedBitmap));
    }

    // full-resolution image for the preview pane
    public static Bitmap GetBitmapFromPath(string filePath)
    {
        if (File.Exists(filePath)) return new Bitmap(filePath);
        else
        {
            Debug.WriteLine("!!!!! " + filePath + " doesnt exist");
            return null;
        }
    }


    // ===== decoding + cache =======================================================================

    private static string CacheKey(FileInfo file) => file.FullName + "|" + file.LastWriteTimeUtc.Ticks + "|" + file.Length;

    // cached decode: thumbnail jpeg + full size + average colour
    private static ImageData GetImageData(FileInfo file)
    {
        if (!file.Exists) return unreadableImage;

        string key = CacheKey(file);
        if (imageCache.TryGetValue(key, out var cached)) return cached;

        var (resizedBitmap, width, height) = GetThumbNailFromPath(file.FullName);
        if (resizedBitmap == null) return unreadableImage; // not cached, so eg. a locked file is retried next time

        using (resizedBitmap)
        {
            if (imageCache.Count >= MaxCachedImages) imageCache.Clear();
            return imageCache[key] = new ImageData(EncodeJpeg(resizedBitmap), width, height, AverageHue(resizedBitmap));
        }
    }

    // the one decode pipeline (gallery + cropped queue thumbnails):
    // decodes only as much of the file as a thumbnail needs (jpegs decode straight at 1/2, 1/4 or 1/8 size),
    // cuts out cropRect (original-image pixels, null = whole image) and fits it into the thumbnail box.
    // returns the thumbnail (caller disposes; null if unreadable) + the image's full size
    private static (SKBitmap? resizedBitmap, int width, int height) GetThumbNailFromPath(string origImgPath, SKRectI? cropRect = null)
    {
        try
        {
            using var codec = SKCodec.Create(origImgPath);
            if (codec == null)
            {
                Debug.WriteLine("!!!!! failed to load img at " + origImgPath);
                return (null, 0, 0);
            }

            int width = codec.Info.Width, height = codec.Info.Height;
            var area = cropRect ?? new SKRectI(0, 0, width, height);
            area.Intersect(new SKRectI(0, 0, width, height));
            if (area.Width <= 0 || area.Height <= 0) return (null, width, height);

            var (targetWidth, targetHeight) = ScaleToThumbnail(area.Width, area.Height);
            var decodeSize = codec.GetScaledDimensions(Math.Min(1f, Math.Max((float)targetWidth / area.Width, (float)targetHeight / area.Height)));
            var decodeInfo = new SKImageInfo(decodeSize.Width, decodeSize.Height, SKColorType.Bgra8888, SKAlphaType.Premul);

            using var skBitmap = new SKBitmap(decodeInfo);
            var result = codec.GetPixels(decodeInfo, skBitmap.GetPixels());
            if (result != SKCodecResult.Success && result != SKCodecResult.IncompleteInput) return (null, width, height);

            // the area, converted into the (possibly smaller) decoded image's pixels
            float sx = (float)decodeSize.Width / width, sy = (float)decodeSize.Height / height;
            var decodedArea = new SKRectI((int)(area.Left * sx), (int)(area.Top * sy),
                                          (int)Math.Ceiling(area.Right * sx), (int)Math.Ceiling(area.Bottom * sy));
            decodedArea.Intersect(new SKRectI(0, 0, decodeSize.Width, decodeSize.Height));

            using var cropped = new SKBitmap();
            if (!skBitmap.ExtractSubset(cropped, decodedArea)) return (null, width, height);

            // Medium = bilinear + mipmaps (None/nearest aliased badly at these ratios)
            var resizedBitmap = cropped.Resize(new SKImageInfo(targetWidth, targetHeight, SKColorType.Bgra8888, SKAlphaType.Premul), SKFilterQuality.Medium);
            return (resizedBitmap, width, height);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"!!!!! failed to load image at {origImgPath}: {ex.Message}");
            return (null, 0, 0);
        }
    }

    // fit inside the thumbnail box keeping aspect ratio
    // (the aspectRatio line MUST have this exact structure in order to ensure height scaling in UI)
    private static (int, int) ScaleToThumbnail(int width, int height)
    {
        float aspectRatio = (float)width / height;

        int targetWidth = ThumbnailWidth;
        int targetHeight = (int)(ThumbnailWidth / aspectRatio);

        if (targetHeight > ThumbnailHeight)
        {
            targetHeight = ThumbnailHeight;
            targetWidth = (int)(ThumbnailHeight * aspectRatio);
        }

        return (Math.Max(1, targetWidth), Math.Max(1, targetHeight));
    }

    // same maths as PixelDatabase's ImageClassifier.Classify: whole-number average of every pixel's R, G, B
    private static double AverageHue(SKBitmap resizedBitmap)
    {
        ReadOnlySpan<byte> pixels = resizedBitmap.GetPixelSpan(); // B, G, R, A per pixel
        long r = 0, g = 0, b = 0;
        for (int i = 0; i < pixels.Length; i += 4)
        {
            b += pixels[i];
            g += pixels[i + 1];
            r += pixels[i + 2];
        }

        long count = pixels.Length / 4;
        return RgbToHue(r / count, g / count, b / count);
    }

    private static byte[] EncodeJpeg(SKBitmap resizedBitmap)
    {
        using var image = SKImage.FromBitmap(resizedBitmap);
        using var data = image.Encode(SKEncodedImageFormat.Jpeg, 85);
        return data.ToArray();
    }

    private static Bitmap? ToBitmap(byte[]? jpegBytes)
    {
        if (jpegBytes == null) return null;
        using var memStream = new MemoryStream(jpegBytes);
        return new Bitmap(memStream);
    }

    private int CountFolderItems(string subDir)
    {
        try
        {
            int folderImgCount = Directory.EnumerateFiles(subDir).Count(f => SupportedExtensions.Contains(Path.GetExtension(f)));
            if (uniVM.FolderCountIncludesFolders) folderImgCount += Directory.EnumerateDirectories(subDir).Count();
            return folderImgCount;
        }
        catch (UnauthorizedAccessException)
        {
            return 0; // no access to the subfolder itself
        }
    }



    // ===== utilities ==============================================================================


    // get aspect ratio of an image with room for error
    public static string GetAspectRatio(int? width, int? height)
    {
        if (width == 0 || height == 0 || width == null || height == null)
        {
            return "none";
        }

        double aspectVal = (double)width / (double)height;

        // use a different error percentage for each aspect ratio
        if (Math.Abs(aspectVal - 4.00 / 3.00) < 0.07)
        {
            return "4:3";
        }
        else if (aspectVal <= 1.00)
        {
            return "Vertical";
        }
        else if (Math.Abs(aspectVal - 16.00 / 9.00) < 0.075)
        {
            return "16:9";
        }
        else if (Math.Abs(aspectVal - 16.00 / 10.00) < 0.075)
        {
            return "16:10";
        }

        else if (Math.Abs(aspectVal - 21.00 / 9.00) < 0.25)
        {
            return "21:9";
        }
        else if (Math.Abs(aspectVal - 32.00 / 9.00) < 0.55)
        {
            return "32:9";
        }
        else
        {
            return "none";
        }

    }

    public static double RgbToHue(double r, double g, double b)
    {
        double rr = r / 255.0;
        double gg = g / 255.0;
        double bb = b / 255.0;

        double max = Math.Max(rr, Math.Max(gg, bb));
        double min = Math.Min(rr, Math.Min(gg, bb));
        double delta = max - min;

        if (delta < 0.00001)
            return 0.0;

        double hue;
        if (Math.Abs(max - rr) < 0.00001)
        {
            hue = (gg - bb) / delta;
        }
        else if (Math.Abs(max - gg) < 0.00001)
        {
            hue = 2.0 + (bb - rr) / delta;
        }
        else
        {
            hue = 4.0 + (rr - gg) / delta;
        }

        hue *= 60.0;
        if (hue < 0) hue += 360.0;

        // force any hue >= 345 down by 360 to unify the red colour region
        if (hue >= 345)
            hue -= 360;

        return hue;
    }

    // gen horizontally mirrored copy of image
    // re-flipping the same image reuses the file and two same-named files from different folders can't collide
    public static string? GetMirroredCopy(string filePath)
    {
        try
        {
            if (!File.Exists(filePath)) return null;
            Directory.CreateDirectory(MirrorDir);

            string pathHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(filePath)))[..8];
            string target = Path.Combine(MirrorDir, Path.GetFileNameWithoutExtension(filePath) + "_" + pathHash + "_mirrored.png");
            if (File.Exists(target)) return target; // already generated this session or earlier

            using var src = SKBitmap.Decode(filePath);
            if (src == null) return null;

            using var dst = new SKBitmap(src.Width, src.Height);
            using (var canvas = new SKCanvas(dst))
            {
                canvas.Scale(-1, 1);
                canvas.DrawBitmap(src, -src.Width, 0);
            }

            using var image = SKImage.FromBitmap(dst);
            using var data = image.Encode(SKEncodedImageFormat.Png, 100);
            using var stream = File.Create(target);
            data.SaveTo(stream);
            return target;
        }
        catch (Exception ex)
        {
            AppStorageHelper.LogCrash(ex);
            return null;
        }
    }

    // deletes mirrored copies nothing references anymore + leftover temp jpgs (thumb_* are from older versions)
    public static void ClearTempImageFiles(IEnumerable<string> referencedPaths)
    {
        try
        {
            var keep = new HashSet<string>(referencedPaths, StringComparer.OrdinalIgnoreCase);
            string tempDir = Path.GetTempPath();
            var patterns = new[] { "thumb_*.jpg", "wallmod_resized_*.jpg" };

            var mirroredFiles = Directory.Exists(MirrorDir)
                ? Directory.EnumerateFiles(MirrorDir, "*_mirrored.png").Where(file => !keep.Contains(file))
                : Enumerable.Empty<string>();
            var tempFiles = patterns.SelectMany(pattern => Directory.EnumerateFiles(tempDir, pattern));

            foreach (var file in mirroredFiles.Concat(tempFiles).ToList())
            {
                try { File.Delete(file); }
                catch (Exception ex) { Debug.WriteLine("temp clear skip: " + ex.Message); }
            }
        }
        catch (Exception ex) { AppStorageHelper.LogCrash(ex); }
    }





}
