using Microsoft.Win32;
using SkiaSharp;
using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;

namespace WallMod.Helpers
{
    // =========================================================
    // IDesktopWallpaper for Windows
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

        /// <summary>
        /// Sets the wallpaper on the current OS.
        /// For Linux, the monitorId is used (if provided) to update only that monitor (e.g. in KDE).
        /// </summary>
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
                SetWallpaperLinux(imagePath, monitorId);
            }
            else
            {
                throw new NotSupportedException("OS not supported for setting a background");
            }
        }

        #region Windows & macOS

        private static void SetWallpaperWindows(string imagePath, string wallpaperStyle, string monitorId)
        {
            if (!File.Exists(imagePath))
                throw new FileNotFoundException($"Image file not found: {imagePath}");

            // Update registry values to set the style
            SetRegistryValues(wallpaperStyle);

            try
            {
                var dw = (IDesktopWallpaper)new DesktopWallpaper();
                // Potentially also ensure large images are resized
                string finalPath = EnsureImageUnderLimit(imagePath);
                dw.SetWallpaper(monitorId, finalPath);
                Debug.WriteLine($"SetWallpaper successful: monitor={monitorId}, file='{imagePath}'");
            }
            catch (Exception ex)
            {
                Debug.WriteLine("SetWallpaperWindows via DesktopWallpaper failed: " + ex.Message);
                // fallback using SystemParametersInfo if needed:
                // SystemParametersInfo(20, 0, finalPath, 0x01 | 0x02);
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

        private static void SetWallpaperMacOS(string imagePath)
        {
            var script = $"osascript -e 'tell application \"Finder\" to set desktop picture to POSIX file \"{imagePath}\"'";
            var psi = new ProcessStartInfo("bash", $"-c \"{script}\"")
            {
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };
            using var proc = Process.Start(psi);
            proc.WaitForExit();
        }

        /// <summary>
        /// Ensures images bigger than ~ 9 million pixels are resized to avoid Windows wallpaper API issues.
        /// </summary>
        private static string EnsureImageUnderLimit(string originalPath, int maxWidth = 3840, int maxHeight = 3840)
        {
            try
            {
                using var inputStream = File.OpenRead(originalPath);
                using var codec = SKCodec.Create(inputStream);
                if (codec == null)
                {
                    Debug.WriteLine("Could not read image header for " + originalPath);
                    return originalPath;
                }

                var info = codec.Info;
                int width = info.Width;
                int height = info.Height;

                // Check size: if under ~ 10 million pixels, skip
                if (width * height < 9000000)
                    return originalPath;

                float scale = Math.Min((float)maxWidth / width, (float)maxHeight / height);
                int newWidth = (int)(width * scale);
                int newHeight = (int)(height * scale);

                inputStream.Seek(0, SeekOrigin.Begin);
                using var originalBitmap = SKBitmap.Decode(inputStream);
                if (originalBitmap == null)
                {
                    Debug.WriteLine("Failed to decode " + originalPath);
                    return originalPath;
                }

                using var resizedBitmap = originalBitmap.Resize(new SKImageInfo(newWidth, newHeight), SKFilterQuality.High);
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

                Debug.WriteLine($"Resized image from {width}x{height} to {newWidth}x{newHeight}, saved at {tempPath}");
                return tempPath;
            }
            catch (Exception ex)
            {
                Debug.WriteLine("EnsureImageUnderLimit error: " + ex.Message);
                return originalPath;
            }
        }

        #endregion

        #region Linux

        private static void SetWallpaperLinux(string imagePath, string monitorId)
        {
            // Many DEs won't actually refresh if you reuse the same file path;
            // create a fresh temporary file each time to guarantee a forced update.
            string uniqueCopy = CreateUniqueTempCopy(imagePath);

            var desktopSession = (Environment.GetEnvironmentVariable("XDG_CURRENT_DESKTOP") ??
                                  Environment.GetEnvironmentVariable("GDMSESSION") ??
                                  Environment.GetEnvironmentVariable("DESKTOP_SESSION") ??
                                  "").ToLower();

            bool isWayland = !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("WAYLAND_DISPLAY"));
            // Create a file:// style URI for gsettings calls
            string imageUri = "file://" + uniqueCopy;

            try
            {
                // KDE Plasma (X11 and Wayland)
                if (desktopSession.Contains("kde") || desktopSession.Contains("plasma"))
                {
                    if (isWayland)
                    {
                        // KDE (Wayland) - use connector names like "HDMI-A-1"
                        // If monitorId is empty, apply to all outputs
                        string cmd = string.IsNullOrEmpty(monitorId)
                            ? $"plasma-apply-wallpaperimage '{uniqueCopy}'"
                            : $"plasma-apply-wallpaperimage --output {monitorId} '{uniqueCopy}'";
                        RunBash(cmd);
                    }
                    else
                    {
                        // KDE (X11) - use QDbus approach with a bit of JavaScript
                        int screenIndex = ParseScreenIndex(monitorId);
                        string script = GenerateKdeScript(screenIndex, imageUri);
                        RunBash($"qdbus org.kde.plasmashell /PlasmaShell org.kde.PlasmaShell.evaluateScript \"{script}\"");
                    }
                }
                // GNOME-based (GNOME, Ubuntu/Unity, Cinnamon, Budgie, etc.)
                else if (desktopSession.Contains("gnome") || desktopSession.Contains("ubuntu") ||
                         desktopSession.Contains("unity") || desktopSession.Contains("cinnamon") ||
                         desktopSession.Contains("budgie"))
                {
                    // Force picture-uri, picture-uri-dark, and set picture-options
                    RunBash($"gsettings set org.gnome.desktop.background picture-uri '{imageUri}'");
                    RunBash($"gsettings set org.gnome.desktop.background picture-uri-dark '{imageUri}'");
                    // Options: none, wall, centered, scaled, stretched, zoom, spanned
                    RunBash("gsettings set org.gnome.desktop.background picture-options 'zoom'");
                }
                // MATE
                else if (desktopSession.Contains("mate"))
                {
                    RunBash($"gsettings set org.mate.background picture-filename '{uniqueCopy}'");
                }
                // Elementary / Pantheon
                else if (desktopSession.Contains("pantheon"))
                {
                    RunBash($"gsettings set org.pantheon.desktop.gala.background picture-uri '{imageUri}'");
                }
                // Fallback for other environments or unknown
                else
                {
                    // feh sets the wallpaper immediately on X11
                    // Add --no-fehbg if you don’t want ~/.fehbg overwritten
                    RunBash($"feh --bg-scale '{uniqueCopy}'");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Linux wallpaper error: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Some KDE X11 flows allow passing a monitor index after "LINUX_MON_",
        /// e.g. "LINUX_MON_1" => screenIndex=1.
        /// If none present, returns -1 (apply to all).
        /// </summary>
        private static int ParseScreenIndex(string monitorId)
        {
            if (!string.IsNullOrEmpty(monitorId) && monitorId.StartsWith("LINUX_MON_"))
            {
                if (int.TryParse(monitorId.Substring("LINUX_MON_".Length), out int index))
                    return index;
            }
            return -1; // Apply to all monitors
        }

        /// <summary>
        /// For KDE’s dbus-based set wallpaper. We optionally break once we set on the desired screen
        /// if a screen index is provided.
        /// </summary>
        private static string GenerateKdeScript(int screenIndex, string imageUri)
        {
            string targetScreen = screenIndex >= 0
                ? $"if (d.screen === {screenIndex}) {{"
                : "// apply to all screens";

            return $@"
                var allDesktops = desktops();
                for (var i = 0; i < allDesktops.length; i++) {{
                    var d = allDesktops[i];
                    {targetScreen}
                        d.wallpaperPlugin = 'org.kde.image';
                        d.currentConfigGroup = ['Wallpaper', 'org.kde.image', 'General'];
                        d.writeConfig('Image', '{imageUri}');
                        {(screenIndex >= 0 ? "break;" : "")}
                    {(screenIndex >= 0 ? "}" : "")}
                }}".Replace("\r", "").Replace("\n", "");
        }

        /// <summary>
        /// Creates a unique temp copy of the provided file so that consecutive sets
        /// with the same file are detected as actual changes by the DE.
        /// </summary>
        private static string CreateUniqueTempCopy(string originalPath)
        {
            if (!File.Exists(originalPath))
                throw new FileNotFoundException($"Image file not found: {originalPath}");

            string extension = Path.GetExtension(originalPath);
            string fileName = "wallmod_temp_" + Guid.NewGuid().ToString("N") + extension;
            string tempPath = Path.Combine(Path.GetTempPath(), fileName);

            File.Copy(originalPath, tempPath, overwrite: true);
            return tempPath;
        }

        private static void RunBash(string script)
        {
            try
            {
                using var process = new Process();
                process.StartInfo = new ProcessStartInfo
                {
                    FileName = "bash",
                    Arguments = $"-c \"{script.Replace("\"", "\\\"")}\"",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                process.Start();
                string output = process.StandardOutput.ReadToEnd();
                string error = process.StandardError.ReadToEnd();
                process.WaitForExit();

                if (process.ExitCode != 0)
                {
                    throw new Exception($"Command failed (exit={process.ExitCode}): {error}");
                }

                Debug.WriteLine($"Bash output: {output}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Bash error: {ex.Message}");
                throw;
            }
        }

        #endregion
    }
}
