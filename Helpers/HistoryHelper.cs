using Avalonia.Controls;
using Microsoft.Win32;
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
using WallMod.ViewModels;

namespace WallMod.Helpers;

/**
 * Class for loading/viewing/adding image history
 */
public class HistoryHelper
{
    AppStorageHelper appStorageHelper;

    public HistoryHelper()
    {
        appStorageHelper = new AppStorageHelper();
        appStorageHelper.InitAppStorage();
    }


    public void AddToHistory(string wallpaperPath)
    {
        // load existing history
        var history = LoadHistoryJson();

        // remove existing entry to avoid duplicates
        history.Remove(wallpaperPath);

        // insert in front
        history.Insert(0, wallpaperPath);

        // limit history to certain amount (eg.20) items [OPTIONAL]
        // if (history.Count > 20)
        //     history.RemoveAt(history.Count - 1);

        // save updated history
        string json = JsonSerializer.Serialize(history);
        File.WriteAllText(appStorageHelper.appStorageFilePath, json);

    }

    public List<string> LoadHistoryJson()
    {
        if (!File.Exists(appStorageHelper.appStorageFilePath))
            return new List<string>();

        // read + deserialize JSON to string list
        string json = File.ReadAllText(appStorageHelper.appStorageFilePath);
        return JsonSerializer.Deserialize<List<string>>(json) ?? new List<string>();
    }

    public async Task<ObservableCollection<Wallpaper>> GetHistoryWallpapers()
    {
        List<string> historyList = LoadHistoryJson();
        ImageHelper imgHelper = new ImageHelper();
        var viewModel = new MainWindowViewModel();

        ObservableCollection<Wallpaper> wpList = new ObservableCollection<Wallpaper>();


        // same as imagehelper multiprocessing
        // hardcoded amount of processors used to retrieve all images in a directory
        var currPCProcessorCount = Environment.ProcessorCount;
        Debug.WriteLine("processors used: " + currPCProcessorCount);
        var semaphore = new System.Threading.SemaphoreSlim(currPCProcessorCount);
        var tasks = new List<Task>();

        foreach (var filePath in historyList)
        {
            await semaphore.WaitAsync();

            var task = Task.Run(async () =>
            {
                try
                {
                    if (File.Exists(filePath))
                    {
                        var currWp = imgHelper.getWallpaperObjectFromPath(filePath);
                        // add wallpaper to collec on main thread
                        await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
                        {
                            wpList.Add(currWp);
                        });
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

        return wpList;

    }


    // NOT CURRENTLY USED [OPTIONAL]
    // get windows wallpaper history (NOT used in app; used in windows settings)
    public void ViewWindowsHistory()
    {
        string registryPath = @"Software\Microsoft\Windows\CurrentVersion\Explorer\Wallpapers";
        using (var wallpkey = Registry.CurrentUser.OpenSubKey(registryPath))
        {
            if (wallpkey != null)
            {
                for (int i = 0; i < 5; i++)
                {
                    string valueName = $"BackgroundHistoryPath{i}";
                    var path = wallpkey.GetValue(valueName) as string;

                    if (!string.IsNullOrEmpty(path))
                    {
                        // WallpaperHistoryList.Add(path);
                        Debug.WriteLine(path);
                    }
                }
            }
        }

    }
}
