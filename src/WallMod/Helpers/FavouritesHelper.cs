using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using WallMod.Models;
using WallMod.State;

namespace WallMod.Helpers;

/**
 * Class for loading favourite images in the main image gallery
 */
public class FavouritesHelper
{
    private readonly UniversalAppStore uniVM = App.Services!.GetRequiredService<UniversalAppStore>();

    AppStorageHelper appStorageHelper;

    public FavouritesHelper()
    {
        appStorageHelper = new AppStorageHelper();
        appStorageHelper.InitAppStorage();
    }

    public async Task<ObservableCollection<Wallpaper>> GetFavouriteWallpapers()
    {
        List<string> favList = GetFavouritePaths();
        ImageHelper imgHelper = new ImageHelper();

        // used to preserve order
        Wallpaper[] resultArray = new Wallpaper[favList.Count];

        // same multiprocessing as history / imagehelper
        int allocCPUThreads = uniVM.CPUThreadsAllocated;
        Debug.WriteLine("processors used: " + allocCPUThreads);
        var semaphore = new System.Threading.SemaphoreSlim(allocCPUThreads);
        var tasks = new List<Task>();

        for (int i = 0; i < favList.Count; i++)
        {
            string filePath = favList[i];
            int index = i;
            await semaphore.WaitAsync();
            var task = Task.Run(() =>
            {
                try
                {
                    if (File.Exists(filePath))
                    {
                        var currWp = imgHelper.getWallpaperObjectFromPath(filePath);
                        resultArray[index] = currWp;
                    }
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

        // add them in original order (skip any that failed / no longer exist)
        var wpList = new ObservableCollection<Wallpaper>();
        await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
        {
            for (int i = 0; i < resultArray.Length; i++)
            {
                var w = resultArray[i];
                if (w != null)
                {
                    wpList.Add(w);
                }
            }
        });

        return wpList;
    }

    // get all favourite paths
    public List<string> GetFavouritePaths()
    {
        try
        {
            if (!File.Exists(appStorageHelper.appFavouritesFile)) return new List<string>();
            var json = File.ReadAllText(appStorageHelper.appFavouritesFile);
            return JsonSerializer.Deserialize<List<string>>(json) ?? new List<string>();
        }
        catch (Exception ex)
        {
            AppStorageHelper.LogCrash(ex);
            return new List<string>();
        }
    }

    // save favourite paths
    private void SaveFavourites(List<string> paths)
    {
        try
        {
            var json = JsonSerializer.Serialize(paths);
            File.WriteAllText(appStorageHelper.appFavouritesFile, json);
        }
        catch (Exception ex)
        {
            AppStorageHelper.LogCrash(ex);
        }
    }

    public bool IsFavourite(string filePath) => GetFavouritePaths().Contains(filePath);

    // add or remove a favourite, returns the new state (true = now favourited)
    public bool ToggleFavourite(string filePath)
    {
        var paths = GetFavouritePaths();
        if (paths.Contains(filePath))
        {
            paths.Remove(filePath);
            SaveFavourites(paths);
            return false;
        }
        else
        {
            paths.Add(filePath);
            SaveFavourites(paths);
            return true;
        }
    }
}
