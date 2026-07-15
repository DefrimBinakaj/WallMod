using Microsoft.Win32;
using SkiaSharp;
using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

// generally used LLM for most of this (since OS background setting is jank), will change soon
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

        // crop an image then set it; moved out of MainWindowViewModel.SetWallpaperWithCrop
        // so the autoset queue can apply crops too
        public static void SetWallpaperCropped(string imagePath, string wallpaperStyle, string monitorId,
                                               int x, int y, int width, int height)
        {
            using var skiaImage = SKBitmap.Decode(imagePath);

            var cropRect = new SKRectI(x, y, x + width, y + height);
            cropRect.Intersect(new SKRectI(0, 0, skiaImage.Width, skiaImage.Height));

            using var croppedImage = new SKBitmap(cropRect.Width, cropRect.Height);
            using (var canvas = new SKCanvas(croppedImage))
            {
                canvas.DrawBitmap(skiaImage, cropRect, new SKRect(0, 0, cropRect.Width, cropRect.Height));
            }

            string tempPath = Path.Combine(Path.GetTempPath(), "croppedWallpaper.png");
            // File.Create, not OpenWrite: OpenWrite doesn't truncate, so a smaller PNG written over
            // a larger one leaves trailing bytes; autoset rewrites this file constantly
            using (var stream = File.Create(tempPath))
            {
                croppedImage.Encode(stream, SKEncodedImageFormat.Png, 100);
            }

            SetWallpaper(tempPath, wallpaperStyle, monitorId);
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
            RunBash("osascript", "-e",
                $"tell application \"Finder\" to set desktop picture to POSIX file \"{imagePath}\"");
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
            // CHANGED: rotating two-slot copy instead of a new GUID copy every time.
            // Path still changes between consecutive sets (forces DE refresh),
            // but disk usage is capped at 2 files per monitor instead of growing forever.
            string uniqueCopy = CreateRotatingCopy(imagePath, monitorId);

            var desktopSession = (Environment.GetEnvironmentVariable("XDG_CURRENT_DESKTOP") ??
                                  Environment.GetEnvironmentVariable("GDMSESSION") ??
                                  Environment.GetEnvironmentVariable("DESKTOP_SESSION") ??
                                  "").ToLower();

            bool isWayland = !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("WAYLAND_DISPLAY"));
            string imageUri = "file://" + uniqueCopy;

            try
            {
                // KDE Plasma (X11 and Wayland) - branch structure UNCHANGED
                if (desktopSession.Contains("kde") || desktopSession.Contains("plasma"))
                {
                    if (isWayland)
                    {
                        if (string.IsNullOrEmpty(monitorId))
                            RunBash("plasma-apply-wallpaperimage", uniqueCopy);
                        else
                            RunBash("plasma-apply-wallpaperimage", "--output", monitorId, uniqueCopy);
                    }
                    else
                    {
                        int screenIndex = ParseScreenIndex(monitorId);
                        string script = GenerateKdeScript(screenIndex, imageUri);
                        RunBash("qdbus", "org.kde.plasmashell", "/PlasmaShell",
                            "org.kde.PlasmaShell.evaluateScript", script);
                    }
                }
                // GNOME-based (GNOME, Ubuntu/Unity, Cinnamon, Budgie) - UNCHANGED commands
                else if (desktopSession.Contains("gnome") || desktopSession.Contains("ubuntu") ||
                         desktopSession.Contains("unity") || desktopSession.Contains("cinnamon") ||
                         desktopSession.Contains("budgie"))
                {
                    RunBash("gsettings", "set", "org.gnome.desktop.background", "picture-uri", imageUri);
                    RunBash("gsettings", "set", "org.gnome.desktop.background", "picture-uri-dark", imageUri);
                    RunBash("gsettings", "set", "org.gnome.desktop.background", "picture-options", "zoom");
                }
                // MATE - UNCHANGED
                else if (desktopSession.Contains("mate"))
                {
                    RunBash("gsettings", "set", "org.mate.background", "picture-filename", uniqueCopy);
                }
                // Elementary / Pantheon - UNCHANGED
                else if (desktopSession.Contains("pantheon"))
                {
                    RunBash("gsettings", "set", "org.pantheon.desktop.gala.background", "picture-uri", imageUri);
                }
                // Fallback - UNCHANGED
                else
                {
                    RunBash("feh", "--bg-scale", uniqueCopy);
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
        /// alternates between two fixed filenames per monitor target. The returned path always differs from the
        /// previous set (same forced-refresh effect as the old GUID copies), but
        /// only two files ever exist per monitor. Stored in AppData so the file
        /// referenced by the DE survives reboots (temp may be wiped on boot).
        /// </summary>
        private static string CreateRotatingCopy(string originalPath, string monitorId)
        {
            if (!File.Exists(originalPath))
                throw new FileNotFoundException($"Image file not found: {originalPath}");

            string dir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "WallMod", "current");
            Directory.CreateDirectory(dir);

            // sanitize monitor id into a filename-safe tag
            var tag = new StringBuilder("wall");
            foreach (char c in monitorId ?? "all")
                tag.Append(char.IsLetterOrDigit(c) ? c : '_');

            string extension = Path.GetExtension(originalPath);
            string slotA = Path.Combine(dir, tag + "_a" + extension);
            string slotB = Path.Combine(dir, tag + "_b" + extension);

            // overwrite whichever slot is older (missing files report year 1601, so they get picked first)
            string target = File.GetLastWriteTimeUtc(slotA) <= File.GetLastWriteTimeUtc(slotB)
                ? slotA
                : slotB;

            File.Copy(originalPath, target, overwrite: true);
            return target;
        }

        /// <summary>
        /// runs the program directly with ArgumentList.
        /// Same commands, same arguments - but no shell layer, so filenames with
        /// quotes/spaces/$ can never break the command, and stderr is drained
        /// concurrently so large error output can't deadlock the process.
        /// </summary>
        private static void RunBash(string fileName, params string[] args)
        {
            using var process = new Process();
            process.StartInfo = new ProcessStartInfo
            {
                FileName = fileName,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            foreach (var a in args)
                process.StartInfo.ArgumentList.Add(a);

            if (!process.Start())
                throw new Exception("Failed to start " + fileName);

            var errTask = process.StandardError.ReadToEndAsync();
            string output = process.StandardOutput.ReadToEnd();
            process.WaitForExit();

            if (process.ExitCode != 0)
            {
                throw new Exception($"Command failed (exit={process.ExitCode}): {errTask.Result}");
            }

            Debug.WriteLine($"{fileName} output: {output}");
        }

        #endregion
    }
}
