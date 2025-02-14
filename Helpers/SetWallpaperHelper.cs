using Microsoft.Win32;
using SkiaSharp;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;


/*
 * Class used for performing the action of setting an image as the desktop background
 * 
 * NOTE: currently a huge mess of copied bs
 */

namespace WallMod.Helpers
{
    // =========================================================
    // IDesktopWallpaper init
    // https://stackoverflow.com/questions/41516979/c-sharp-how-do-you-get-an-instance-of-a-com-interface/41713718#41713718
    // =========================================================

    [ComImport]
    [Guid("B92B56A9-8B55-4E14-9A89-0199BBB6F93B")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    public interface IDesktopWallpaper
    {
        void SetWallpaper(
            [MarshalAs(UnmanagedType.LPWStr)] string monitorID,
            [MarshalAs(UnmanagedType.LPWStr)] string wallpaper);

        [return: MarshalAs(UnmanagedType.LPWStr)]
        string GetWallpaper(
            [MarshalAs(UnmanagedType.LPWStr)] string monitorID);

        [return: MarshalAs(UnmanagedType.LPWStr)]
        string GetMonitorDevicePathAt(uint monitorIndex);

        [return: MarshalAs(UnmanagedType.U4)]
        uint GetMonitorDevicePathCount();

        [return: MarshalAs(UnmanagedType.Struct)]
        Rect GetMonitorRECT([MarshalAs(UnmanagedType.LPWStr)] string monitorID);

        void SetBackgroundColor([MarshalAs(UnmanagedType.U4)] uint color);
        [return: MarshalAs(UnmanagedType.U4)]
        uint GetBackgroundColor();

        void SetPosition([MarshalAs(UnmanagedType.I4)] DesktopWallpaperPosition position);
        [return: MarshalAs(UnmanagedType.I4)]
        DesktopWallpaperPosition GetPosition();

        void SetSlideshow(IntPtr items);
        IntPtr GetSlideshow();
        void SetSlideshowOptions(DesktopSlideshowDirection options, uint slideshowTick);
        void GetSlideshowOptions(out DesktopSlideshowDirection options, out uint slideshowTick);
        void AdvanceSlideshow([MarshalAs(UnmanagedType.LPWStr)] string monitorID, [MarshalAs(UnmanagedType.I4)] DesktopSlideshowDirection direction);
        DesktopSlideshowDirection GetStatus();
        bool Enable();
    }

    // =========================================================
    // Windows
    // =========================================================

    // windows-OS necessary stuff (random as hell)
    [ComImport]
    [Guid("C2CF3110-460E-4fc1-B9D0-8A1C0C9CC4BD")]
    public class DesktopWallpaper
    {
    }

    public enum DesktopSlideshowDirection
    {
        Forward = 0,
        Backward = 1
    }

    public enum DesktopWallpaperPosition
    {
        Center = 0,
        Tile = 1,
        Stretch = 2,
        Fit = 3,
        Fill = 4,
        Span = 5
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct Rect
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }


    public class SetWallpaperHelper
    {
        public enum WallpaperStyle
        {
            Fill,
            Fit,
            Stretch,
            Tile,
            Center,
            Span // Windows 8+ only
        }


        // platform branching
        public static void SetWallpaper(string imagePath, string wallpaperStyle, string monitorId)
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                SetWallpaperWindows(imagePath, wallpaperStyle, monitorId);
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                SetWallpaperMacOS(imagePath);
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                SetWallpaperLinux(imagePath);
            }
            else
            {
                throw new NotSupportedException("OS not supported for setting a background");
            }
        }

        // gpt mess
        private static void SetWallpaperWindows(string imagePath, string wallpaperStyle, string monitorId)
        {
            // 1) Validate
            if (!File.Exists(imagePath))
                throw new FileNotFoundException($"Image file not found: {imagePath}");

            // 2) Set registry style as you did before (Fill, Fit, etc.)
            SetRegistryValues(wallpaperStyle);

            // 3) Use IDesktopWallpaper
            try
            {
                var dw = (IDesktopWallpaper)new DesktopWallpaper();

                // If monitorId is empty, Windows typically applies to all. 
                // If monitorId is something like "\\.\DISPLAY2", it sets that monitor only.
                dw.SetWallpaper(monitorId, EnsureImageUnderLimit(imagePath));

                Debug.WriteLine($"SetWallpaper successful: monitor={monitorId}, file='{imagePath}'");
            }
            catch (Exception ex)
            {
                Debug.WriteLine("SetWallpaperWindows via DesktopWallpaper failed: " + ex.Message);
            }
        }



        // func for pictures that will not set due to size
        public static string EnsureImageUnderLimit(string originalPath, int maxWidth = 5000, int maxHeight = 5000)
        {
            try
            {
                using var inputStream = File.OpenRead(originalPath);
                using var codec = SKCodec.Create(inputStream);
                if (codec == null)
                {
                    Debug.WriteLine("Could not read image header for " + originalPath);
                    return originalPath; // fallback
                }

                // img dimensions
                var info = codec.Info;
                int width = info.Width;
                int height = info.Height;

                // if image is within limit, just return the original path
                if (width * height < 20000000)
                    return originalPath;

                // compute a scale factor so that neither dimension exceeds the limit
                float scale = Math.Min((float)maxWidth / width, (float)maxHeight / height);
                int newWidth = (int)(width * scale);
                int newHeight = (int)(height * scale);

                // rewind stream to decode fully
                inputStream.Seek(0, SeekOrigin.Begin);
                using var originalBitmap = SKBitmap.Decode(inputStream);
                if (originalBitmap == null)
                {
                    Debug.WriteLine("Failed to decode " + originalPath);
                    return originalPath;
                }

                using var resizedBitmap = originalBitmap
                    .Resize(new SKImageInfo(newWidth, newHeight), SKFilterQuality.Medium);

                if (resizedBitmap == null)
                {
                    Debug.WriteLine("Failed to resize " + originalPath);
                    return originalPath;
                }

                string tempPath = Path.Combine(Path.GetTempPath(), "wallmod_resized_" + Path.GetFileName(originalPath));
                using var image = SKImage.FromBitmap(resizedBitmap);
                using var data = image.Encode(SKEncodedImageFormat.Jpeg, 85);

                using (var output = File.OpenWrite(tempPath))
                {
                    data.SaveTo(output);
                }

                Debug.WriteLine($"resized large image from {width}x{height} to {newWidth}x{newHeight}, saved at {tempPath}");
                return tempPath;
            }
            catch (Exception ex)
            {
                Debug.WriteLine("EnsureImageUnderLimit error: " + ex.Message);
                return originalPath; // fallback if anything goes wrong
            }
        }


        private static void SetRegistryValues(string wallpaperStyle)
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"Control Panel\Desktop", writable: true);
            if (key == null)
                throw new Exception("Failed to open HKCU\\Control Panel\\Desktop");

            switch (wallpaperStyle.ToLower())
            {
                case "fill":
                    key.SetValue("WallpaperStyle", "10");
                    key.SetValue("TileWallpaper", "0");
                    break;
                case "fit":
                    key.SetValue("WallpaperStyle", "6");
                    key.SetValue("TileWallpaper", "0");
                    break;
                case "stretch":
                    key.SetValue("WallpaperStyle", "2");
                    key.SetValue("TileWallpaper", "0");
                    break;
                case "tile":
                    key.SetValue("WallpaperStyle", "0");
                    key.SetValue("TileWallpaper", "1");
                    break;
                case "center":
                    key.SetValue("WallpaperStyle", "0");
                    key.SetValue("TileWallpaper", "0");
                    break;
                case "span":
                    key.SetValue("WallpaperStyle", "22");
                    key.SetValue("TileWallpaper", "0");
                    break;
                default:
                    key.SetValue("WallpaperStyle", "10");
                    key.SetValue("TileWallpaper", "0");
                    break;
            }
        }

        // idek if this works
        // =========================================================
        // Mac
        // =========================================================
        private static void SetWallpaperMacOS(string imagePath)
        {
            var script = $"osascript -e 'tell application \"Finder\" to set desktop picture to POSIX file \"{imagePath}\"'";
            var psi = new System.Diagnostics.ProcessStartInfo("bash", $"-c \"{script}\"")
            {
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };
            using var proc = System.Diagnostics.Process.Start(psi);
            proc.WaitForExit();
        }

        // idek if this works
        // =========================================================
        // Linux
        // =========================================================
        private static void SetWallpaperLinux(string imagePath)
        {
            var desktopSession = Environment.GetEnvironmentVariable("XDG_CURRENT_DESKTOP")?.ToLower() ?? "";
            if (desktopSession.Contains("gnome"))
            {
                var script = $"gsettings set org.gnome.desktop.background picture-uri 'file://{imagePath}'";
                RunBash(script);
            }
            else if (desktopSession.Contains("kde"))
            {
                var script = $"qdbus org.kde.plasmashell /PlasmaShell org.kde.PlasmaShell.evaluateScript " +
                             $"\"var allDesktops = desktops();for (i=0;i<allDesktops.length;i++){{" +
                             $"d = allDesktops[i];" +
                             $"d.wallpaperPlugin = \\\"org.kde.image\\\";" +
                             $"d.currentConfigGroup = Array(\\\"Wallpaper\\\",\\\"org.kde.image\\\",\\\"General\\\");" +
                             $"d.writeConfig(\\\"Image\\\", \\\"file://{imagePath}\\\")}}\"";
                RunBash(script);
            }
            else
            {
                throw new NotSupportedException("Unsupported Linux desktop environment for setting wallpaper.");
            }
        }

        private static void RunBash(string script)
        {
            var psi = new System.Diagnostics.ProcessStartInfo("bash", $"-c \"{script}\"")
            {
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };
            using var proc = System.Diagnostics.Process.Start(psi);
            proc.WaitForExit();
        }
    }
}
