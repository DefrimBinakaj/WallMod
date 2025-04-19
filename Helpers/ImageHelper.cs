using Avalonia.Controls;
using Avalonia.Media.Imaging;
using Avalonia.Platform.Storage;
using Avalonia.Platform;
using SkiaSharp;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using WallMod.ViewModels;
using WallMod.Models;
using DataJuggler.PixelDatabase;
using ColorThiefDotNet;

namespace WallMod.Helpers;

/**
 * Class used for handling selecting/loading/converting/compressing/handling images
 */
public class ImageHelper
{
    // -----------------------------------------------------------------------------------------------
    // funcs for converting resizedSkiaImg to bitmap

    public static Bitmap GetBitmapFromPath(string filePath)
    {
        if (File.Exists(filePath))
        {
            return new Bitmap(filePath);
        }
        else
        {
            Debug.WriteLine("!!!!! " + filePath + " doesnt exist");
            return null;
        }
    }

    public static Bitmap LoadFromResource(Uri resourceUri)
    {
        return new Bitmap(AssetLoader.Open(resourceUri));
    }

    public static async Task<Bitmap?> LoadFromWeb(Uri url)
    {
        using var httpClient = new HttpClient();
        try
        {
            var response = await httpClient.GetAsync(url);
            response.EnsureSuccessStatusCode();
            var data = await response.Content.ReadAsByteArrayAsync();
            return new Bitmap(new MemoryStream(data));
        }
        catch (HttpRequestException ex)
        {
            Console.WriteLine("!!!!! error downloading resizedSkiaImg " + url + " -- " + ex.Message);
            return null;
        }
    }

    // -----------------------------------------------------------------------------------------------





    // -----------------------------------------------------------------------------------------------
    // funcs for converting path to wallpaper object

    public Wallpaper getWallpaperObjectFromPath(string imgFilePath)
    {
        var imgResult = GetThumbNailFromPath(imgFilePath);

        // perform colour classification using thumbnail
        PixelDatabase colourDB = PixelDatabaseLoader.LoadPixelDatabase(imgResult.Item2, null);
        var imgClass = ImageClassifier.Classify(colourDB);

        return new Wallpaper
        {
            FilePath = imgFilePath,
            ImageBitmap = null, // WE DO NOT NEED THIS IN ORDER TO SET BACKGROUND - uses filepath
            ImageWidth = imgResult.Item3,
            ImageHeight = imgResult.Item4,
            ImageThumbnailBitmap = imgResult.Item1,
            ColourCategory = RgbToHue(imgClass.AverageRed, imgClass.AverageGreen, imgClass.AverageBlue),
            Name = Path.GetFileName(imgFilePath),
            Date = File.GetLastWriteTime(imgFilePath),
            IsDirectory = false,
        };
    }

    private static double RgbToHue(double r, double g, double b)
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

    // thumbnail size hardcoded in arg
    public (Bitmap, string, int, int) GetThumbNailFromPath(string origImgPath, int thumbnailWidth = 250, int thumbnailHeight = 200)
    {

        using (var inputStream = File.OpenRead(origImgPath))
        {
            using (var skBitmap = SKBitmap.Decode(inputStream))
            {
                if (skBitmap == null)
                {
                    Debug.WriteLine("!!!!! failed to load img at " + origImgPath);
                    return (null, null, 0, 0);
                }

                int width = skBitmap.Width;
                int height = skBitmap.Height;

                // this line MUST have this exact structure in order to ensure height scaling in UI
                float aspectRatio = (float)width / height;

                int targetWidth = thumbnailWidth;
                int targetHeight = (int) (thumbnailWidth / aspectRatio);

                if (targetHeight > thumbnailHeight)
                {
                    targetHeight = thumbnailHeight;
                    targetWidth = (int) (thumbnailHeight * aspectRatio);
                }

                using (var resizedBitmap = skBitmap.Resize(new SKImageInfo(targetWidth, targetHeight), SKFilterQuality.None))
                {
                    if (resizedBitmap == null)
                    {
                        Debug.WriteLine("!!!!! failed to resize image at " + origImgPath);
                        return (null, null, 0, 0);
                    }

                    using (var image = SKImage.FromBitmap(resizedBitmap))
                    {
                        using (var data = image.Encode(SKEncodedImageFormat.Jpeg, 60))
                        {
                            byte[] bytes = data.ToArray();

                            using (var memStream = new MemoryStream(data.ToArray()))
                            {
                                var avaloniaBitmap = new Bitmap(memStream);

                                // 2) also write these bytes to a temp file
                                string tempFile = Path.Combine(
                                    Path.GetTempPath(),
                                    "thumb_" + Guid.NewGuid().ToString("N") + ".jpg");

                                File.WriteAllBytes(tempFile, bytes);

                                // return both the Avalonia Bitmap & the temp file path
                                return (avaloniaBitmap, tempFile, width, height);
                            }
                        }
                    }
                }
            }
        }

    }


    // -----------------------------------------------------------------------------------------------







    // func for returning a list of wallpaper objects
    public async Task<ObservableCollection<Wallpaper?>> getWallpaperListFromDirec(Window window, MainWindowViewModel mvm, string folderSpecified)
    {

        List<string> SupportedExtensions = new List<string> { ".jpg", ".jpeg", ".png", ".bmp" };

        var imgCollec = new ObservableCollection<Wallpaper>();

        string folderChoice = ""; // init
        if (folderSpecified != null)
        {
            folderChoice = folderSpecified;
        }
        else if (folderSpecified == null)
        {
            // open folder picker
            var folderOpenPick = await window.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
            {
                Title = "Select an Image Directory",
                AllowMultiple = false
            });

            if (folderOpenPick == null || !folderOpenPick.Any())
            {
                return null;
            }
            folderChoice = folderOpenPick.Last().Path.LocalPath;
        }
        

        mvm.CurrentSelectedDirectory = " Loading...";


        // populate directories (if no access to folder, quit func)
        try
        {
            string selectedDirPath = folderChoice;
            foreach (var subDir in Directory.GetDirectories(selectedDirPath))
            {
                // Create a 'Wallpaper' object for the directory
                var folderWallpaper = new Wallpaper
                {
                    FilePath = subDir,
                    Name = Path.GetFileName(subDir),
                    IsDirectory = true,
                    // Use your own folder icon path or resource
                    ImageThumbnailBitmap = new Bitmap(AssetLoader.Open(new Uri("avares://Wallmod/Assets/folderimg.png"))),
                    Date = Directory.GetLastWriteTime(subDir)
                };
                imgCollec.Add(folderWallpaper);
            }
        }
        catch (UnauthorizedAccessException)
        {
            Debug.WriteLine($"!!!  Access denied to {folderChoice} - Skipping loading func");
            return null;
        }




        // use Last() since folderChoice is technically a list, but AllowMultiple restricts to one element
        // enumerate files in the folder
        var files = Directory.EnumerateFiles(folderChoice)
                             .Where(file => SupportedExtensions.Contains(Path.GetExtension(file).ToLower()));

        int totalFiles = files.Count();
        int processedFiles = 0;

        // IMPORTANT !!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!
        // hardcoded amount of processors used to retrieve all images in a directory
        int allocCPUThreads = mvm.CPUThreadsAllocated;
        Debug.WriteLine("processors used: " + allocCPUThreads);
        var semaphore = new System.Threading.SemaphoreSlim(allocCPUThreads);
        var tasks = new List<Task>();

        foreach (var filePath in files)
        {
            await semaphore.WaitAsync();

            var task = Task.Run(async () =>
            {
                try
                {
                    var wallpaper = getWallpaperObjectFromPath(filePath);

                    // add wallpaper to collec on main thread
                    await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
                    {
                        imgCollec.Add(wallpaper);
                        processedFiles++;
                        mvm.ImgLoadProgress = (double)processedFiles / totalFiles * 100;
                    });
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"!!!!! failed to load image at {filePath}: {ex.Message}");
                }
                finally
                {
                    semaphore.Release();
                }
            });

            tasks.Add(task);
        }

        await Task.WhenAll(tasks);

        mvm.CurrentSelectedDirectory = folderChoice;


        return imgCollec;

    }


    // kinda useless? idk
    // func for choosing one file with upload button
    public async Task<Wallpaper> chooseOneWallpaperUpload(Window window)
    {
        var result = await window.StorageProvider.OpenFilePickerAsync(new Avalonia.Platform.Storage.FilePickerOpenOptions
        {
            Title = window.Title,
            FileTypeFilter = new List<FilePickerFileType> { FilePickerFileTypes.ImageAll },
            AllowMultiple = false
        });

        if (result == null || !result.Any())
        {
            return null;
        }

        try
        {
            string selectedFilePath = result.Last().Path.LocalPath;
            Debug.WriteLine("selected file path: " + selectedFilePath);
            return getWallpaperObjectFromPath(selectedFilePath);
        }
        catch (Exception ex)
        {
            Debug.WriteLine("!!!!! error uploading file - " + ex.Message);
            return null;
        }


    }




    // func for choosing a file with upload button
    public async Task<ObservableCollection<Wallpaper>> chooseMultipleWallpaperUpload(Window window)
    {

        var wallpaperCollec = new ObservableCollection<Wallpaper>();

        var result = await window.StorageProvider.OpenFilePickerAsync(new Avalonia.Platform.Storage.FilePickerOpenOptions
        {
            Title = window.Title,
            FileTypeFilter = new List<FilePickerFileType> { FilePickerFileTypes.ImageAll },
            AllowMultiple = true
        });

        if (result == null || !result.Any())
        {
            return null;
        }

        foreach (var img in result)
        {
            try
            {
                string selectedFilePath = img.Path.LocalPath;
                Debug.WriteLine("selected file path: " + selectedFilePath);
                wallpaperCollec.Add(getWallpaperObjectFromPath(selectedFilePath));
            }
            catch (Exception ex)
            {
                Debug.WriteLine("!!!!! error uploading file - " + ex.Message);
            }
        }

        return wallpaperCollec;

    }



    // get aspect ratio of an image with room for error
    public string GetAspectRatio(int? width, int? height)
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
        else if ( Math.Abs(aspectVal - 16.00 / 9.00) < 0.075)
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



}
