using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Windows;

namespace AsterDock
{
    internal sealed class DisplayArea
    {
        internal IntPtr Handle;
        internal string Device;
        internal Rect BoundsPx, WorkPx;
        internal double Scale;
        internal bool Primary;
    }

    // All desktop coordinates in this class are PHYSICAL pixels. Never multiply a
    // global desktop origin by a monitor's DPI: adjacent monitors have different scales.
    internal static class Displays
    {
        [StructLayout(LayoutKind.Sequential)] private struct PixelRect { public int Left, Top, Right, Bottom; public Rect Rect { get { return new Rect(Left, Top, Right-Left, Bottom-Top); } } }
        [StructLayout(LayoutKind.Sequential)] private struct PixelPoint { public int X, Y; }
        [StructLayout(LayoutKind.Sequential, CharSet=CharSet.Unicode)] private struct MonitorInfo
        { public int Size; public PixelRect Bounds, Work; public uint Flags; [MarshalAs(UnmanagedType.ByValTStr, SizeConst=32)] public string Device; }
        private delegate bool MonitorCallback(IntPtr monitor, IntPtr dc, ref PixelRect bounds, IntPtr data);
        [DllImport("user32.dll")] private static extern bool EnumDisplayMonitors(IntPtr dc, IntPtr clip, MonitorCallback callback, IntPtr data);
        [DllImport("user32.dll", CharSet=CharSet.Unicode)] private static extern bool GetMonitorInfo(IntPtr monitor, ref MonitorInfo info);
        [DllImport("user32.dll")] private static extern IntPtr MonitorFromWindow(IntPtr window, uint flags);
        [DllImport("user32.dll")] private static extern IntPtr MonitorFromPoint(PixelPoint point, uint flags);
        [DllImport("shcore.dll")] private static extern int GetDpiForMonitor(IntPtr monitor, int type, out uint x, out uint y);
        [DllImport("user32.dll")] internal static extern uint GetDpiForWindow(IntPtr window);
        [DllImport("user32.dll")] private static extern uint GetDpiForSystem();
        [DllImport("user32.dll")] private static extern bool GetWindowRect(IntPtr window, out PixelRect rect);
        [DllImport("user32.dll")] private static extern bool GetCursorPos(out PixelPoint point);
        [DllImport("user32.dll")] internal static extern int GetSystemMetricsForDpi(int metric, uint dpi);
        [DllImport("user32.dll")] private static extern IntPtr GetWindowDpiAwarenessContext(IntPtr window);
        [DllImport("user32.dll")] private static extern bool AreDpiAwarenessContextsEqual(IntPtr a, IntPtr b);
        internal static bool IsPerMonitorV2(IntPtr window) { return AreDpiAwarenessContextsEqual(GetWindowDpiAwarenessContext(window), new IntPtr(-4)); }
        internal static double WindowScale(IntPtr window) { uint dpi = GetDpiForWindow(window); return dpi == 0 ? 1 : dpi / 96.0; }
        internal static double LegacySystemScale { get { return GetDpiForSystem() / 96.0; } }
        internal static Point Cursor { get { PixelPoint p; GetCursorPos(out p); return new Point(p.X, p.Y); } }
        internal static Rect WindowRect(IntPtr window) { PixelRect r; return GetWindowRect(window, out r) ? r.Rect : Rect.Empty; }
        private static DisplayArea Read(IntPtr monitor)
        {
            MonitorInfo info = new MonitorInfo { Size = Marshal.SizeOf(typeof(MonitorInfo)) };
            if (!GetMonitorInfo(monitor, ref info)) return null;
            uint x, y;
            double scale = GetDpiForMonitor(monitor, 0, out x, out y) >= 0 ? x / 96.0 : 1;
            return new DisplayArea { Handle = monitor, Device = info.Device, BoundsPx = info.Bounds.Rect, WorkPx = info.Work.Rect, Scale = scale, Primary = (info.Flags & 1) != 0 };
        }
        internal static List<DisplayArea> All()
        {
            List<DisplayArea> areas = new List<DisplayArea>();
            EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, delegate(IntPtr monitor, IntPtr dc, ref PixelRect r, IntPtr data) {
                DisplayArea area = Read(monitor); if (area != null) areas.Add(area); return true;
            }, IntPtr.Zero);
            if (areas.Count == 0) throw new InvalidOperationException(UiText.T("无法读取显示器工作区。"));
            return areas;
        }
        internal static DisplayArea Primary { get { List<DisplayArea> all = All(); return all.Find(delegate(DisplayArea a) { return a.Primary; }) ?? all[0]; } }
        internal static DisplayArea NamedOrPrimary(string device) { return All().Find(delegate(DisplayArea a) { return string.Equals(a.Device, device, StringComparison.OrdinalIgnoreCase); }) ?? Primary; }
        internal static DisplayArea AtPoint(Point p) { return Read(MonitorFromPoint(new PixelPoint { X = (int)Math.Round(p.X), Y = (int)Math.Round(p.Y) }, 2)) ?? Primary; }
        internal static DisplayArea ForWindow(IntPtr window) { return Read(MonitorFromWindow(window, 2)) ?? Primary; }
        internal static void Move(IntPtr window, Point position)
        {
            Rect old = WindowRect(window);
            int x = (int)Math.Round(position.X), y = (int)Math.Round(position.Y);
            if (!old.IsEmpty && old.Left == x && old.Top == y) return;
            Native.SetWindowPos(window, IntPtr.Zero, x, y, 0, 0, 0x15); // NOSIZE | NOZORDER | NOACTIVATE
        }
    }

    internal static class DisplayGeometry
    {
        internal static Point Place(Rect workPx, Size sizePx, bool movable, double xFraction, double yFraction, double scale)
        {
            double freeX = Math.Max(0, workPx.Width-sizePx.Width), freeY = Math.Max(0, workPx.Height-sizePx.Height);
            return new Point(workPx.Left + freeX * (movable ? xFraction : .5),
                workPx.Top + (movable ? freeY*yFraction : Math.Min(freeY, 8*scale)));
        }
        internal static Point Fractions(Rect workPx, Rect windowPx)
        {
            return new Point(Settings.Clamp((windowPx.Left-workPx.Left)/Math.Max(1, workPx.Width-windowPx.Width), 0, 1, .5),
                Settings.Clamp((windowPx.Top-workPx.Top)/Math.Max(1, workPx.Height-windowPx.Height), 0, 1, 0));
        }
        internal static Size Viewport(Rect workPx, double scale)
        { return new Size(Math.Max(1, workPx.Width/scale-40), Math.Max(1, workPx.Height/scale-40)); }
        internal static Point Drag(Point cursorPx, Size windowPx, Point grabFraction)
        { return new Point(cursorPx.X-windowPx.Width*grabFraction.X, cursorPx.Y-windowPx.Height*grabFraction.Y); }
    }
}
