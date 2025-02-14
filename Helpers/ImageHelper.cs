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

        return new Wallpaper
        {
            FilePath = imgFilePath,
            ImageBitmap = null, // WE DO NOT NEED THIS IN ORDER TO SET BACKGROUND - uses filepath
            ImageWidth = imgResult.Item2,
            ImageHeight = imgResult.Item3,
            ImageThumbnailBitmap = imgResult.Item1,
            Category = "RandCategory",
            Name = Path.GetFileName(imgFilePath)
        };
    }


    // thumbnail size hardcoded in arg
    public (Bitmap, int, int) GetThumbNailFromPath(string origImgPath, int thumbnailWidth = 350, int thumbnailHeight = 250)
    {

        using (var inputStream = File.OpenRead(origImgPath))
        {
            using (var skBitmap = SKBitmap.Decode(inputStream))
            {
                if (skBitmap == null)
                {
                    Debug.WriteLine("!!!!! failed to load img at " + origImgPath);
                    return (null, 0, 0);
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

                using (var resizedBitmap = skBitmap.Resize(new SKImageInfo(targetWidth, targetHeight), SKFilterQuality.Low))
                {
                    if (resizedBitmap == null)
                    {
                        Debug.WriteLine("!!!!! failed to resize image at " + origImgPath);
                        return (null, 0, 0);
                    }

                    using (var image = SKImage.FromBitmap(resizedBitmap))
                    {
                        using (var data = image.Encode(SKEncodedImageFormat.Jpeg, 60))
                        {
                            using (var memStream = new MemoryStream(data.ToArray()))
                            {
                                return (new Bitmap(memStream), width, height);
                            }
                        }
                    }
                }
            }
        }

    }


    // -----------------------------------------------------------------------------------------------







    // func for returning a list of wallpaper objects
    public async Task<ObservableCollection<Wallpaper?>> loadListFromDirectory(Window window, MainWindowViewModel mvm)
    {

        List<string> SupportedExtensions = new List<string> { ".jpg", ".jpeg", ".png", ".bmp", ".gif" };

        var imgCollec = new ObservableCollection<Wallpaper>();

        // open folder picker
        var folderChoice = await window.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = "Select an Image Directory",
            AllowMultiple = false
        });

        if (folderChoice == null || !folderChoice.Any())
        {
            return null;
        }

        mvm.CurrentSelectedDirectory = "Loading...";

        // use Last() since folderChoice is technically a list, but AllowMultiple restricts to one element
        // enumerate files in the folder
        var files = Directory.EnumerateFiles(folderChoice.Last().Path.LocalPath)
                             .Where(file => SupportedExtensions.Contains(Path.GetExtension(file).ToLower()));

        int totalFiles = files.Count();
        int processedFiles = 0;

        // IMPORTANT !!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!
        // hardcoded amount of processors used to retrieve all images in a directory
        var currPCProcessorCount = Environment.ProcessorCount;
        Debug.WriteLine("processors used: " + currPCProcessorCount);
        var semaphore = new System.Threading.SemaphoreSlim(currPCProcessorCount);
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

        mvm.CurrentSelectedDirectory = "/" + Path.GetFileName(folderChoice.Last().Path.LocalPath);

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
}
