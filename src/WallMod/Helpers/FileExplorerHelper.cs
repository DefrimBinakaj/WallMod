using Avalonia.Controls;
using Avalonia.Platform.Storage;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace WallMod.Helpers;

/**
 * Class for opening image in file explorer
 */
public class FileExplorerHelper
{

    // folder picker shared by the gallery + random autoset; null = cancelled
    public static async Task<string?> PickFolder(string title)
    {
        var folderOpenPick = await new Window().StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = title,
            AllowMultiple = false
        });

        return folderOpenPick.Count > 0 ? folderOpenPick[0].Path.LocalPath : null;
    }

    public void OpenFileInExplorer(string filePath)
    {
        if (string.IsNullOrEmpty(filePath) || !System.IO.File.Exists(filePath))
        {
            Debug.WriteLine("File not found: " + filePath);
            return;
        }

        try
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                Process.Start("explorer", $"/select,\"{filePath}\"");
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                Process.Start("open", "-R " + filePath);
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                Process.Start("xdg-open", Path.GetDirectoryName(filePath) ?? ".");
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine("Error opening file: " + ex.Message);
        }
    }

    public void OpenFolderInExplorer(string folderPath)
    {
        if (string.IsNullOrEmpty(folderPath) || !Directory.Exists(folderPath))
        {
            Debug.WriteLine("Folder not found: " + folderPath);
            return;
        }

        try
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                Process.Start("explorer", $"\"{folderPath}\"");
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                Process.Start("open", $"\"{folderPath}\"");
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                Process.Start("xdg-open", folderPath);
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine("Error opening folder: " + ex.Message);
        }
    }
}

