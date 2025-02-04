using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace WallMod.Helpers;

public class FileExporerHelper
{

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
                Process.Start("xdg-open", System.IO.Path.GetDirectoryName(filePath) ?? ".");
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine("Error opening file: " + ex.Message);
        }
    }

}
