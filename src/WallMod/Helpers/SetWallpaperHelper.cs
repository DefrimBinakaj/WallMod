using Avalonia;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Win32;
using SkiaSharp;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using WallMod.Models;
using WallMod.State;


// generally used LLM for most of this (since OS background setting is jank), will change eventually lol
//=======

namespace WallMod.Helpers
{
    // Windows per-monitor wallpaper COM interface. Methods must stay in vtable order, so the unused ones before GetPosition stay
    [ComImport]
    [Guid("B92B56A9-8B55-4E14-9A89-0199BBB6F93B")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    public interface IDesktopWallpaper
    {
        void SetWallpaper([MarshalAs(UnmanagedType.LPWStr)] string monitorID, [MarshalAs(UnmanagedType.LPWStr)] string wallpaper);
        [return: MarshalAs(UnmanagedType.LPWStr)] string GetWallpaper([MarshalAs(UnmanagedType.LPWStr)] string monitorID);
        [return: MarshalAs(UnmanagedType.LPWStr)] string GetMonitorDevicePathAt(uint monitorIndex);
        uint GetMonitorDevicePathCount();
        Rect GetMonitorRECT([MarshalAs(UnmanagedType.LPWStr)] string monitorID);
        void SetBackgroundColor(uint color);
        uint GetBackgroundColor();
        void SetPosition(DesktopWallpaperPosition position);
        DesktopWallpaperPosition GetPosition();
    }

    [ComImport]
    [Guid("C2CF3110-460E-4fc1-B9D0-8A1C0C9CC4BD")]
    public class DesktopWallpaper { }

    public enum DesktopWallpaperPosition { Center, Tile, Stretch, Fit, Fill, Span }

    [StructLayout(LayoutKind.Sequential)]
    public struct Rect { public int Left, Top, Right, Bottom; }

    // Sets one monitor's wallpaper and never changes what any other monitor shows.
    //  - KDE, XFCE: real per-monitor wallpapers, style applied natively by the desktop
    //  - Windows: per-monitor images, but the style is desktop-wide, so we draw the style into an image of the monitor's exact pixel size
    //  - GNOME, Cinnamon, MATE, feh: only ONE image for all monitors, so we rebuild exactly what every monitor shows right now
    //    into one desktop-sized image, replace only this monitor's part, and show it spanned
    public static class SetWallpaperHelper
    {
        private static readonly string Dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "WallMod", "current");

        // a single-image desktop's big image: its size, and each monitor's rect in it.
        // Native = image pixels per physical pixel of that monitor (only not 1 on GNOME with mixed display scaling)
        private sealed record Layout(int Width, int Height, Dictionary<string, (SKRectI Rect, float Native)> Monitors);

        public static void SetWallpaper(string imagePath, string wallpaperStyle, string monitorId)
        {
            using var src = Decode(imagePath);
            Apply(src, imagePath, wallpaperStyle, monitorId);
        }

        public static void SetWallpaperCropped(string imagePath, string wallpaperStyle, string monitorId, int x, int y, int width, int height)
        {
            using var src = Decode(imagePath);
            using var crop = Subset(src, new SKRectI(x, y, x + width, y + height));
            Apply(crop, null, wallpaperStyle, monitorId);
        }

        private static void Apply(SKBitmap src, string? file, string style, string monitorId)
        {
            var monitors = App.Services.GetRequiredService<UniversalAppStore>().MonitorList.ToArray();
            var mon = monitors.FirstOrDefault(m => m.MonitorIdPath == monitorId) ?? throw new ArgumentException("Unknown monitor: " + monitorId);

            int left = monitors.Min(m => m.Bounds.X), top = monitors.Min(m => m.Bounds.Y);
            var desktop = new PixelRect(left, top, monitors.Max(m => m.Bounds.Right) - left, monitors.Max(m => m.Bounds.Bottom) - top);

            // Span = this monitor's piece of the image filling the whole desktop, cut out at full resolution and then just filled
            using var slice = style == "Span" ? SpanSlice(src, mon.Bounds, desktop) : null;
            if (slice != null) { file = null; style = "Fill"; }
            var img = slice ?? src;

            if (OperatingSystem.IsWindows()) SetWindows(img, style, mon, monitors, desktop);
            else if (OperatingSystem.IsLinux()) SetLinux(img, file, style, mon, monitors, desktop);
            else if (OperatingSystem.IsMacOS()) SetMac(img, style, mon);
            else throw new PlatformNotSupportedException("Setting wallpapers is only supported on Windows, Linux and macOS");
        }

        #region Windows

        private static void SetWindows(SKBitmap img, string style, MonitorInfo mon, MonitorInfo[] monitors, PixelRect desktop)
        {
            var dw = (IDesktopWallpaper)new DesktopWallpaper();
            uint bg = dw.GetBackgroundColor(); // 0x00BBGGRR, the colour windows shows around Fit/Center images
            var background = new SKColor((byte)bg, (byte)(bg >> 8), (byte)(bg >> 16));

            // Span is the one windows mode with a single image for all monitors. Leaving it would change the other monitors,
            // so first give each of them its own exact copy of the part of the spanned image it's showing
            if (dw.GetPosition() == DesktopWallpaperPosition.Span)
            {
                using var current = TryLoad(dw.GetWallpaper(mon.MonitorIdPath))
                    ?? TryLoad(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Microsoft", "Windows", "Themes", "TranscodedWallpaper"));
                dw.SetPosition(DesktopWallpaperPosition.Fill);
                if (current != null)
                    foreach (var other in monitors.Where(m => m != mon))
                    {
                        using var part = SpanSlice(current, other.Bounds, desktop);
                        dw.SetWallpaper(other.MonitorIdPath, SaveRendered(part, "Fill", other, background));
                    }
            }

            // exactly the monitor's pixel size, so it shows 1:1 under any (desktop-wide) mode, which we leave alone
            dw.SetWallpaper(mon.MonitorIdPath, SaveRendered(img, style, mon, background));
        }

        // size = the image's pixel size, the monitor's bounds unless given (macOS bounds are points, not pixels)
        private static string SaveRendered(SKBitmap img, string style, MonitorInfo mon, SKColor background, PixelSize? size = null)
        {
            var s = size ?? mon.Bounds.Size;
            using var output = new SKBitmap(s.Width, s.Height);
            using (var c = new SKCanvas(output))
            {
                c.Clear(background);
                Draw(c, img, style, new SKRectI(0, 0, output.Width, output.Height));
            }
            string path = NextSlot(mon.MonitorIdPath, ".png");
            Save(output, path);
            return path;
        }

        #endregion

        #region macOS

        // each screen is set on its own (NSWorkspace setDesktopImageURL:forScreen:), so other screens are never touched.
        // Like Windows, the style is drawn into an image of the screen's exact pixel size and handed over as Fill, so it shows 1:1.
        // Only the screen's current Space (virtual desktop) changes: macOS has no API for the others
        private static void SetMac(SKBitmap img, string style, MonitorInfo mon)
        {
            if (!mon.MonitorIdPath.StartsWith(MonitorHelper.MacIdPrefix) || !uint.TryParse(mon.MonitorIdPath[MonitorHelper.MacIdPrefix.Length..], out uint displayId))
                throw new ArgumentException("Not a macOS display: " + mon.MonitorIdPath);

            IntPtr screen = MacScreen(displayId);

            // avalonia gives macOS screens in points; the image needs real pixels (2x on retina)
            double scale = SendDouble(screen, Sel("backingScaleFactor"));
            var pixels = new PixelSize((int)Math.Round(mon.Bounds.Width * scale), (int)Math.Round(mon.Bounds.Height * scale));

            // black around Fit/Center: macOS no longer reports the user's own fill colour
            string path = SaveRendered(img, style, mon, SKColors.Black, pixels);

            // Fill = scale proportionally (NSImageScaleProportionallyUpOrDown = 3) + allow clipping; a no-op for an exact-size image
            IntPtr appKit = NativeLibrary.Load(AppKit);
            IntPtr Key(string name) => Marshal.ReadIntPtr(NativeLibrary.GetExport(appKit, name)); // AppKit's NSString constants
            IntPtr nsNumber = Class("NSNumber");
            IntPtr options = Send(Class("NSMutableDictionary"), Sel("dictionary"));
            SendVoid(options, Sel("setObject:forKey:"), Send(nsNumber, Sel("numberWithUnsignedInteger:"), (nuint)3), Key("NSWorkspaceDesktopImageScalingKey"));
            SendVoid(options, Sel("setObject:forKey:"), SendBool(nsNumber, Sel("numberWithBool:"), true), Key("NSWorkspaceDesktopImageAllowClippingKey"));

            IntPtr url = Send(Class("NSURL"), Sel("fileURLWithPath:"), MacString(path));
            IntPtr workspace = Send(Class("NSWorkspace"), Sel("sharedWorkspace"));
            if (!SendSetDesktopImage(workspace, Sel("setDesktopImageURL:forScreen:options:error:"), url, screen, options, IntPtr.Zero))
                throw new InvalidOperationException("macOS refused the wallpaper for display " + displayId);
        }

        // the NSScreen whose NSScreenNumber is this display id
        private static IntPtr MacScreen(uint displayId)
        {
            IntPtr screens = Send(Class("NSScreen"), Sel("screens"));
            IntPtr numberKey = MacString("NSScreenNumber");
            for (nuint i = 0, count = SendNUInt(screens, Sel("count")); i < count; i++)
            {
                IntPtr screen = Send(screens, Sel("objectAtIndex:"), i);
                IntPtr number = Send(Send(screen, Sel("deviceDescription")), Sel("objectForKey:"), numberKey);
                if (number != IntPtr.Zero && SendUInt(number, Sel("unsignedIntValue")) == displayId) return screen;
            }
            throw new InvalidOperationException("Display is no longer connected: " + displayId);
        }

        private static IntPtr MacString(string s)
        {
            IntPtr utf8 = Marshal.StringToCoTaskMemUTF8(s);
            try { return Send(Class("NSString"), Sel("stringWithUTF8String:"), utf8); }
            finally { Marshal.FreeCoTaskMem(utf8); }
        }

        // tells macOS WallMod is doing user-requested work, so it never gets napped (timers throttled) while hidden.
        // NSActivityUserInitiatedAllowingIdleSystemSleep: no App Nap, but the Mac can still go to sleep. Kept for the app's lifetime
        public static void MacBeginActivity()
        {
            IntPtr processInfo = Send(Class("NSProcessInfo"), Sel("processInfo"));
            IntPtr activity = SendActivity(processInfo, Sel("beginActivityWithOptions:reason:"), 0x00EFFFFFUL, MacString("Changing wallpapers on a schedule"));
            Send(activity, Sel("retain")); // the activity lasts as long as this object does
        }

        // Objective-C runtime (Apple's C API for calling AppKit). objc_msgSend needs one exact signature per argument/return shape
        private const string ObjC = "/usr/lib/libobjc.A.dylib";
        private const string AppKit = "/System/Library/Frameworks/AppKit.framework/AppKit";

        [DllImport(ObjC, EntryPoint = "objc_getClass")] private static extern IntPtr Class(string name);
        [DllImport(ObjC, EntryPoint = "sel_registerName")] private static extern IntPtr Sel(string name);
        [DllImport(ObjC, EntryPoint = "objc_msgSend")] private static extern IntPtr Send(IntPtr receiver, IntPtr selector);
        [DllImport(ObjC, EntryPoint = "objc_msgSend")] private static extern IntPtr Send(IntPtr receiver, IntPtr selector, IntPtr arg);
        [DllImport(ObjC, EntryPoint = "objc_msgSend")] private static extern IntPtr Send(IntPtr receiver, IntPtr selector, nuint arg);
        [DllImport(ObjC, EntryPoint = "objc_msgSend")] private static extern IntPtr SendBool(IntPtr receiver, IntPtr selector, [MarshalAs(UnmanagedType.I1)] bool arg);
        [DllImport(ObjC, EntryPoint = "objc_msgSend")] private static extern void SendVoid(IntPtr receiver, IntPtr selector, IntPtr arg1, IntPtr arg2);
        [DllImport(ObjC, EntryPoint = "objc_msgSend")] private static extern nuint SendNUInt(IntPtr receiver, IntPtr selector);
        [DllImport(ObjC, EntryPoint = "objc_msgSend")] private static extern uint SendUInt(IntPtr receiver, IntPtr selector);
        [DllImport(ObjC, EntryPoint = "objc_msgSend")] private static extern double SendDouble(IntPtr receiver, IntPtr selector);
        [DllImport(ObjC, EntryPoint = "objc_msgSend")] private static extern IntPtr SendActivity(IntPtr receiver, IntPtr selector, ulong options, IntPtr reason);
        [return: MarshalAs(UnmanagedType.I1)]
        private static extern bool SendSetDesktopImage(IntPtr receiver, IntPtr selector, IntPtr url, IntPtr screen, IntPtr options, IntPtr error);

        #endregion

        #region Linux

        private static void SetLinux(SKBitmap img, string? file, string style, MonitorInfo mon, MonitorInfo[] monitors, PixelRect desktop)
        {
            string de = (Environment.GetEnvironmentVariable("XDG_CURRENT_DESKTOP") ?? Environment.GetEnvironmentVariable("DESKTOP_SESSION") ?? "").ToLowerInvariant();

            if (de.Contains("kde") || de.Contains("plasma"))
                SetKde(img, file, style, mon, monitors);
            else if (de.Contains("xfce"))
                SetXfce(img, file, style, mon);
            else if (de.Contains("cinnamon"))
                SetSpanned("org.cinnamon.desktop.background", "picture-uri", null, img, style, mon, AvaloniaLayout(monitors, desktop));
            else if (de.Contains("mate"))
                SetSpanned("org.mate.background", "picture-filename", null, img, style, mon, AvaloniaLayout(monitors, desktop));
            else if (de.Contains("gnome") || de.Contains("unity") || de.Contains("budgie") || de.Contains("pantheon"))
                SetSpanned("org.gnome.desktop.background", "picture-uri", "picture-uri-dark", img, style, mon, MutterLayout(monitors) ?? AvaloniaLayout(monitors, desktop));
            else
                SetFeh(img, style, mon, AvaloniaLayout(monitors, desktop), desktop);
        }

        // KDE: a real wallpaper + fill mode per screen (X11 and Wayland). Plasma numbers screens its own way, so match by position
        private static void SetKde(SKBitmap img, string? file, string style, MonitorInfo mon, MonitorInfo[] monitors)
        {
            int rank = monitors.OrderBy(m => m.Bounds.X).ThenBy(m => m.Bounds.Y).ToList().IndexOf(mon);
            int fillMode = style switch { "Stretch" => 0, "Fit" => 1, "Tile" => 3, "Center" => 6, _ => 2 };
            string uri = ToUri(SaveSource(img, file, mon.MonitorIdPath));
            string script = $$"""
                var ds = desktops().filter(function (d) { return d.screen >= 0; });
                var screens = [];
                ds.forEach(function (d) { if (screens.indexOf(d.screen) < 0) screens.push(d.screen); });
                screens.sort(function (a, b) { var ga = screenGeometry(a), gb = screenGeometry(b); return ga.x - gb.x || ga.y - gb.y; });
                ds.forEach(function (d) {
                    if (d.screen !== screens[{{rank}}]) return;
                    d.wallpaperPlugin = 'org.kde.image';
                    d.currentConfigGroup = ['Wallpaper', 'org.kde.image', 'General'];
                    d.writeConfig('Image', '{{uri}}');
                    d.writeConfig('FillMode', {{fillMode}});
                });
                """;
            Run("dbus-send", "--session", "--print-reply", "--dest=org.kde.plasmashell", "/PlasmaShell", "org.kde.PlasmaShell.evaluateScript", "string:" + script);
        }

        // XFCE: a real wallpaper + style per monitor, stored under the monitor's xrandr name
        private static void SetXfce(SKBitmap img, string? file, string style, MonitorInfo mon)
        {
            string name = XrandrMonitors().Where(s => s.Rect == mon.Bounds).Select(s => s.Name).FirstOrDefault()
                ?? throw new InvalidOperationException("Couldn't find this monitor in xrandr");
            string imageStyle = style switch { "Center" => "1", "Tile" => "2", "Stretch" => "3", "Fit" => "4", _ => "5" };
            string path = SaveSource(img, file, mon.MonitorIdPath);

            string all;
            try { all = Run("xfconf-query", "-c", "xfce4-desktop", "-l"); } catch { all = ""; }
            var props = all.Split('\n').Select(p => p.Trim())
                .Where(p => Regex.IsMatch(p, $@"^/backdrop/screen\d+/monitor{Regex.Escape(name)}/workspace\d+/last-image$")).ToList();
            if (props.Count == 0) props.Add($"/backdrop/screen0/monitor{name}/workspace0/last-image");

            foreach (var p in props)
            {
                Run("xfconf-query", "-c", "xfce4-desktop", "-p", p.Replace("/last-image", "/image-style"), "-n", "-t", "int", "-s", imageStyle);
                Run("xfconf-query", "-c", "xfce4-desktop", "-p", p, "-n", "-t", "string", "-s", path);
            }
        }

        // GNOME / Cinnamon / MATE: one image for all monitors. Rebuild every monitor exactly as the desktop draws it now
        // (its image, picture-options and background colours), replace only this monitor's part, then show it spanned
        private static void SetSpanned(string schema, string key, string? darkKey, SKBitmap img, string style, MonitorInfo mon, Layout layout)
        {
            string options = GGet(schema, "picture-options") ?? "zoom";
            SKColor primary = ParseColor(GGet(schema, "primary-color")), secondary = ParseColor(GGet(schema, "secondary-color"));
            string shading = GGet(schema, "color-shading-type") ?? "solid";
            string? light = GGet(schema, key), dark = darkKey == null ? null : GGet(schema, darkKey);
            var whole = new SKRectI(0, 0, layout.Width, layout.Height);

            void Background(SKCanvas c, SKRectI r)
            {
                var end = shading == "horizontal" ? new SKPoint(r.Right, r.Top) : new SKPoint(r.Left, r.Bottom);
                using var paint = new SKPaint { Color = primary };
                if (shading != "solid")
                    paint.Shader = SKShader.CreateLinearGradient(new SKPoint(r.Left, r.Top), end, new[] { primary, secondary }, SKShaderTileMode.Clamp);
                c.DrawRect(r, paint);
            }

            string ComposeFrom(string tag, string? current) => Compose(tag, layout, c =>
            {
                foreach (var (r, _) in layout.Monitors.Values) Background(c, r);
                using var cur = TryLoad(ToLocalPath(current));
                if (cur == null || options == "none") return;

                if (options == "spanned")
                    Draw(c, cur, "Stretch", whole); // our own previous image is exactly this size, so it's copied 1:1
                else
                    foreach (var (r, native) in layout.Monitors.Values)
                        Draw(c, cur, options switch { "scaled" => "Fit", "stretched" => "Stretch", "centered" => "Center", "wallpaper" => "Tile", _ => "Fill" }, r, native, whole);
            }, Background, img, style, mon.MonitorIdPath);

            string lightFile = ComposeFrom("desktop", light);
            GSet(schema, key, key == "picture-filename" ? lightFile : ToUri(lightFile)); // MATE stores a path, the others a uri
            if (darkKey != null && dark != null)
                GSet(schema, darkKey, ToUri(dark == light ? lightFile : ComposeFrom("desktopdark", dark)));
            GSet(schema, "picture-options", "spanned");
        }

        // plain X11 window managers: feh's own record of what it last showed (~/.fehbg) is what the monitors show now
        private static void SetFeh(SKBitmap img, string style, MonitorInfo mon, Layout layout, PixelRect desktop)
        {
            var whole = new SKRectI(0, 0, layout.Width, layout.Height);
            string fehbg = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".fehbg");
            string last = File.Exists(fehbg) ? File.ReadAllText(fehbg) : ""; // e.g. feh --no-fehbg --bg-fill '/a.jpg' '/b.jpg'
            string mode = Regex.Match(last, @"--bg-(\w+)").Groups[1].Value switch { "center" => "Center", "max" => "Fit", "scale" => "Stretch", "tile" => "Tile", _ => "Fill" };
            var files = Regex.Matches(last, @"'((?:[^']|'\\'')*)'").Select(m => m.Groups[1].Value.Replace(@"'\''", "'")).ToList();

            string path = Compose("desktop", layout, c =>
            {
                if (files.Count == 0) return;
                if (last.Contains("--no-xinerama"))
                {
                    using var cur = TryLoad(files[0]);
                    if (cur != null) Draw(c, cur, mode, whole);
                    return;
                }
                // one image per screen, in xinerama order, reused when there are fewer images than screens
                var screens = XrandrMonitors();
                for (int i = 0; i < screens.Count; i++)
                {
                    using var cur = TryLoad(files[i % files.Count]);
                    var r = screens[i].Rect;
                    if (cur != null) Draw(c, cur, mode, SKRectI.Create(r.X - desktop.X, r.Y - desktop.Y, r.Width, r.Height));
                }
            }, (c, r) => { using var black = new SKPaint { Color = SKColors.Black }; c.DrawRect(r, black); }, img, style, mon.MonitorIdPath);

            Run("feh", "--bg-fill", "--no-xinerama", path);
        }

        private static string Compose(string tag, Layout layout, Action<SKCanvas> paintCurrent, Action<SKCanvas, SKRectI> paintBackground, SKBitmap img, string style, string monitorId)
        {
            var (rect, native) = layout.Monitors[monitorId];
            using var full = new SKBitmap(layout.Width, layout.Height);
            using (var c = new SKCanvas(full))
            {
                c.Clear(SKColors.Black);
                paintCurrent(c);
                paintBackground(c, rect);
                Draw(c, img, style, rect, native);
            }
            string path = NextSlot(tag, ".png");
            Save(full, path);
            return path;
        }

        private static Layout AvaloniaLayout(MonitorInfo[] monitors, PixelRect desktop) =>
            new(desktop.Width, desktop.Height, monitors.ToDictionary(m => m.MonitorIdPath,
                m => (SKRectI.Create(m.Bounds.X - desktop.X, m.Bounds.Y - desktop.Y, m.Bounds.Width, m.Bounds.Height), 1f)));

        // GNOME (esp. Wayland with scaling) can report scaled-down monitor sizes to the app, which would make the image soft.
        // Ask mutter for the real layout instead: each monitor's logical rect, its scale, and its true pixel size
        private static Layout? MutterLayout(MonitorInfo[] monitors)
        {
            string state;
            try
            {
                state = Run("gdbus", "call", "--session", "--dest", "org.gnome.Mutter.DisplayConfig", "--object-path", "/org/gnome/Mutter/DisplayConfig",
                            "--method", "org.gnome.Mutter.DisplayConfig.GetCurrentState");
            }
            catch { return null; }

            const string Str = @"'(?:[^'\\]|\\.)*'";
            // physical size of each connector's current mode
            var heads = Regex.Matches(state, $@"\(\(({Str}), {Str}, {Str}, {Str}\), \[").ToList();
            var modeSize = new Dictionary<string, (int W, int H)>();
            foreach (Match m in Regex.Matches(state, $@"\({Str}, (\d+), (\d+), [^,]+, [^,]+, \[[^\]]*\], \{{[^}}]*'is-current': <true>"))
                if (heads.LastOrDefault(h => h.Index < m.Index) is Match head)
                    modeSize.TryAdd(head.Groups[1].Value, (int.Parse(m.Groups[1].Value), int.Parse(m.Groups[2].Value)));

            bool logicalLayout = !state.Contains("'layout-mode': <uint32 2>"); // 2 = physical layout (X11)
            var logical = new List<(int X, int Y, int W, int H, double Scale)>();
            foreach (Match m in Regex.Matches(state, $@"\((-?\d+), (-?\d+), ([\d.]+), (?:uint32 )?(\d+), (?:true|false), \[\(({Str})"))
            {
                if (!modeSize.TryGetValue(m.Groups[5].Value, out var px)) return null;
                if (int.Parse(m.Groups[4].Value) % 2 == 1) px = (px.H, px.W); // rotated 90 / 270
                double scale = logicalLayout ? double.Parse(m.Groups[3].Value, CultureInfo.InvariantCulture) : 1;
                logical.Add((int.Parse(m.Groups[1].Value), int.Parse(m.Groups[2].Value), (int)Math.Round(px.W / scale), (int)Math.Round(px.H / scale), scale));
            }
            if (logical.Count != monitors.Length) return null;

            int minX = logical.Min(l => l.X), minY = logical.Min(l => l.Y);
            double width = logical.Max(l => l.X + l.W) - minX, height = logical.Max(l => l.Y + l.H) - minY;
            // pixels per logical pixel = the sharpest monitor's scale, so no monitor is ever upscaled (capped at the 16384px GPU texture limit)
            double k = Math.Min(logical.Max(l => l.Scale), 16384 / Math.Max(width, height));

            var byPosition = logical.OrderBy(l => l.X).ThenBy(l => l.Y).ToList();
            var rects = monitors.OrderBy(m => m.Bounds.X).ThenBy(m => m.Bounds.Y).Select((m, i) => (m.MonitorIdPath, L: byPosition[i]))
                .ToDictionary(p => p.MonitorIdPath, p => (
                    SKRectI.Round(SKRect.Create((float)((p.L.X - minX) * k), (float)((p.L.Y - minY) * k), (float)(p.L.W * k), (float)(p.L.H * k))),
                    (float)(k / p.L.Scale)));
            return new Layout((int)Math.Round(width * k), (int)Math.Round(height * k), rects);
        }

        // active monitors in xinerama order, e.g. " 0: +*DP-1 2560/597x1440/336+0+0  DP-1"
        private static List<(string Name, PixelRect Rect)> XrandrMonitors() =>
            Run("xrandr", "--listactivemonitors").Split('\n')
                .Select(l => Regex.Match(l, @"(\d+)/\d+x(\d+)/\d+\+(-?\d+)\+(-?\d+)\s+(\S+)\s*$"))
                .Where(m => m.Success)
                .Select(m => (m.Groups[5].Value, new PixelRect(int.Parse(m.Groups[3].Value), int.Parse(m.Groups[4].Value), int.Parse(m.Groups[1].Value), int.Parse(m.Groups[2].Value))))
                .ToList();

        private static string? GGet(string schema, string key)
        {
            try
            {
                string v = Run("gsettings", "get", schema, key).Trim();
                return v.Length >= 2 && v[0] is '\'' or '"' ? v[1..^1].Replace("\\'", "'").Replace("\\\"", "\"").Replace("\\\\", "\\") : v;
            }
            catch { return null; }
        }

        private static void GSet(string schema, string key, string value) => Run("gsettings", "set", schema, key, value);

        private static SKColor ParseColor(string? v)
        {
            if (v is { Length: 13 }) v = "#" + v.Substring(1, 2) + v.Substring(5, 2) + v.Substring(9, 2); // old 16-bit #rrrrggggbbbb
            return SKColor.TryParse(v ?? "", out var c) ? c : SKColors.Black;
        }

        private static string? ToLocalPath(string? v) =>
            string.IsNullOrEmpty(v) ? null : v.StartsWith("file://") ? new Uri(v).LocalPath : v;

        #endregion

        #region Drawing & files

        // draws img into area the way style lays it out. native = output pixels per image pixel for Center/Tile (their natural size).
        // Tiles start at the area's top-left, or are centred in tileFrame (how GNOME tiles)
        private static void Draw(SKCanvas c, SKBitmap img, string style, SKRectI area, float native = 1, SKRectI? tileFrame = null)
        {
            float sx = native, sy = native;
            if (style is "Fill" or "Fit" or "Stretch")
            {
                sx = (float)area.Width / img.Width;
                sy = (float)area.Height / img.Height;
                if (style != "Stretch") sx = sy = style == "Fit" ? Math.Min(sx, sy) : Math.Max(sx, sy);
            }
            int w = Math.Max(1, (int)Math.Round(img.Width * sx)), h = Math.Max(1, (int)Math.Round(img.Height * sy));
            using var resized = w == img.Width && h == img.Height ? null
                : img.Resize(new SKImageInfo(w, h), SKFilterQuality.High) ?? throw new InvalidOperationException("Couldn't resize the image");
            var bmp = resized ?? img;

            c.Save();
            c.ClipRect(area);
            if (style == "Tile")
            {
                var f = tileFrame ?? area;
                int ox = tileFrame == null ? f.Left : f.Left + (f.Width - w) / 2, oy = tileFrame == null ? f.Top : f.Top + (f.Height - h) / 2;
                for (int y = oy - (int)Math.Ceiling((oy - area.Top) / (double)h) * h; y < area.Bottom; y += h)
                    for (int x = ox - (int)Math.Ceiling((ox - area.Left) / (double)w) * w; x < area.Right; x += w)
                        c.DrawBitmap(bmp, x, y);
            }
            else
            {
                c.DrawBitmap(bmp, area.Left + (area.Width - w) / 2, area.Top + (area.Height - h) / 2);
            }
            c.Restore();
        }

        // the part of src a monitor shows when src fills the whole desktop, cut out at the image's own resolution
        private static SKBitmap SpanSlice(SKBitmap src, PixelRect mon, PixelRect desktop)
        {
            float s = Math.Max((float)desktop.Width / src.Width, (float)desktop.Height / src.Height);
            float left = (src.Width - desktop.Width / s) / 2, top = (src.Height - desktop.Height / s) / 2;
            return Subset(src, SKRectI.Round(SKRect.Create(left + (mon.X - desktop.X) / s, top + (mon.Y - desktop.Y) / s, mon.Width / s, mon.Height / s)));
        }

        private static SKBitmap Subset(SKBitmap src, SKRectI r)
        {
            var part = new SKBitmap();
            if (src.ExtractSubset(part, SKRectI.Intersect(r, new SKRectI(0, 0, src.Width, src.Height)))) return part;
            part.Dispose();
            throw new ArgumentException("Area is outside the image");
        }

        // for desktops that lay the image out themselves: a copy of the original file when it's untouched, else the cut-out piece
        private static string SaveSource(SKBitmap img, string? file, string tag)
        {
            string path = NextSlot(tag, file != null ? Path.GetExtension(file) : ".png");
            if (file != null) File.Copy(file, path, true);
            else Save(img, path);
            return path;
        }

        // two files per target, written alternately: the path changes on every set so the desktop always reloads it,
        // but only two ever exist. In AppData since the desktop keeps pointing at it after a reboot
        private static string NextSlot(string tag, string extension)
        {
            Directory.CreateDirectory(Dir);
            tag = string.Concat(tag.Where(char.IsLetterOrDigit));
            DateTime Age(string slot) => Directory.GetFiles(Dir, $"{tag}_{slot}.*").Select(File.GetLastWriteTimeUtc).DefaultIfEmpty(DateTime.MinValue).Max();
            string slot = Age("a") <= Age("b") ? "a" : "b";
            foreach (var old in Directory.GetFiles(Dir, $"{tag}_{slot}.*")) File.Delete(old);
            return Path.Combine(Dir, $"{tag}_{slot}{extension}");
        }

        private static void Save(SKBitmap image, string path)
        {
            using var stream = File.Create(path);
            image.Encode(stream, SKEncodedImageFormat.Png, 100);
        }

        // decode straight to the normal 32-bit format, so grayscale/indexed images resize and draw like any other
        private static SKBitmap Decode(string path)
        {
            using var codec = SKCodec.Create(path) ?? throw new InvalidDataException("Can't read image: " + path);
            return SKBitmap.Decode(codec, codec.Info.WithColorType(SKImageInfo.PlatformColorType).WithAlphaType(SKAlphaType.Premul))
                ?? throw new InvalidDataException("Can't decode image: " + path);
        }

        // the user's current wallpaper, whatever it is; null if there isn't one
        private static SKBitmap? TryLoad(string? path)
        {
            if (string.IsNullOrEmpty(path) || !File.Exists(path)) return null;
            if (path.EndsWith(".xml", StringComparison.OrdinalIgnoreCase) && (path = XmlImage(path)) == null) return null;
            try { return Decode(path); } catch { }
            if (!OperatingSystem.IsLinux()) return null;
            // formats Skia can't read (jxl, svg, ...): convert with gdk-pixbuf, the same loader the desktop itself uses
            try
            {
                string png = Path.Combine(Path.GetTempPath(), "wallmod_current.png");
                Run("gdk-pixbuf-thumbnailer", "-s", "16384", path, png);
                return Decode(png);
            }
            catch { return null; }
        }

        // GNOME slideshow / day-night .xml wallpapers: the first still image, in its biggest size
        private static string? XmlImage(string path)
        {
            try
            {
                var doc = XDocument.Load(path);
                var file = doc.Descendants("static").Elements("file").FirstOrDefault() ?? doc.Descendants("file").FirstOrDefault();
                var best = file?.Elements("size").OrderByDescending(s => (int?)s.Attribute("width") ?? 0).FirstOrDefault() ?? file;
                return best?.Value.Trim();
            }
            catch { return null; }
        }

        private static string ToUri(string path) => new Uri(path).AbsoluteUri.Replace("'", "%27");

        // runs the program directly (no shell), so paths with spaces/quotes can't break anything; throws if it fails
        private static string Run(string fileName, params string[] args)
        {
            var info = new ProcessStartInfo(fileName) { RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false, CreateNoWindow = true };
            foreach (var a in args) info.ArgumentList.Add(a);

            using var process = Process.Start(info) ?? throw new Exception("Failed to start " + fileName);
            var error = process.StandardError.ReadToEndAsync();
            string output = process.StandardOutput.ReadToEnd();
            process.WaitForExit();

            if (process.ExitCode != 0)
                throw new Exception($"{fileName} failed (exit {process.ExitCode}): {error.Result}");
            return output;
        }

        #endregion
    }

}
