using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace AsterDock
{
    internal static class Native
    {
        internal delegate bool EnumProc(IntPtr window, IntPtr parameter);
        [DllImport("user32.dll")] internal static extern bool EnumWindows(EnumProc callback, IntPtr parameter);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] internal static extern IntPtr FindWindowEx(IntPtr parent, IntPtr after, string name, string title);
        [DllImport("user32.dll")] internal static extern bool IsWindowVisible(IntPtr window);
        [DllImport("user32.dll")] internal static extern bool IsWindow(IntPtr window);
        [DllImport("user32.dll")] internal static extern bool ShowWindowAsync(IntPtr window, int command);
        [DllImport("user32.dll")] internal static extern bool ShowWindow(IntPtr window, int command);
        [DllImport("user32.dll")] internal static extern bool SetWindowPos(IntPtr window, IntPtr after, int x, int y, int w, int h, uint flags);
        [DllImport("user32.dll", CharSet=CharSet.Unicode)] private static extern IntPtr FindWindow(string name, string title);
        [DllImport("user32.dll")] private static extern IntPtr GetWindow(IntPtr window, uint command);
        [DllImport("user32.dll")] private static extern IntPtr GetAncestor(IntPtr window, uint flags);
        [DllImport("user32.dll", EntryPoint="GetWindowLongPtrW")] internal static extern IntPtr GetWindowLongPtr(IntPtr window, int index);
        [DllImport("user32.dll", EntryPoint="SetWindowLongPtrW")] internal static extern IntPtr SetWindowLongPtr(IntPtr window, int index, IntPtr value);
        [DllImport("user32.dll")] internal static extern bool RegisterHotKey(IntPtr window, int id, uint modifiers, uint key);
        [DllImport("user32.dll")] internal static extern bool UnregisterHotKey(IntPtr window, int id);
        [DllImport("user32.dll")] internal static extern bool DestroyIcon(IntPtr icon);
        [DllImport("user32.dll")] internal static extern uint GetDoubleClickTime();
        [DllImport("dwmapi.dll")] private static extern int DwmSetWindowAttribute(IntPtr window, int attribute, ref int value, int size);
        [DllImport("dwmapi.dll")] private static extern int DwmGetWindowAttribute(IntPtr window, int attribute, out int value, int size);
        [DllImport("dwmapi.dll")] private static extern int DwmEnableBlurBehindWindow(IntPtr window, ref BlurRegion blur);
        [StructLayout(LayoutKind.Sequential)] private struct BlurRegion { public uint Flags; public int Enable; public IntPtr Region; public int Transition; }
        internal static bool UseSystemCorners(IntPtr window)
        {
            SetWindowRgn(window, IntPtr.Zero, true);
            // Enable alpha in the client surface without defining a custom window region.
            BlurRegion alpha = new BlurRegion { Flags = 1, Enable = 1 };
            DwmEnableBlurBehindWindow(window, ref alpha);
            int round = 2;
            return DwmSetWindowAttribute(window, 33, ref round, 4) >= 0;
        }
        internal static bool HasSystemCornerConfiguration(IntPtr window)
        {
            IntPtr region = CreateRoundRectRgn(0, 0, 1, 1, 0, 0);
            try
            {
                int preference;
                return GetWindowRgn(window, region) == 0 &&
                    DwmGetWindowAttribute(window, 33, out preference, 4) >= 0 && preference == 2 &&
                    (GetWindowLongPtr(window, -20).ToInt64() & 0x80000) == 0;
            }
            finally { DeleteObject(region); }
        }
        [DllImport("user32.dll")] private static extern bool SetWindowCompositionAttribute(IntPtr window, ref CompositionData data);
        [DllImport("user32.dll")] private static extern int SetWindowRgn(IntPtr window, IntPtr region, bool redraw);
        [DllImport("gdi32.dll")] private static extern IntPtr CreateRoundRectRgn(int left, int top, int right, int bottom, int ellipseWidth, int ellipseHeight);
        [DllImport("gdi32.dll")] private static extern bool DeleteObject(IntPtr value);
        [DllImport("user32.dll")] private static extern int GetWindowRgn(IntPtr window, IntPtr region);
        [DllImport("gdi32.dll")] private static extern bool PtInRegion(IntPtr region, int x, int y);
        internal static bool HasRoundedClip(IntPtr window, int centerX, int centerY)
        {
            IntPtr region = CreateRoundRectRgn(0, 0, 1, 1, 0, 0);
            try { return GetWindowRgn(window, region) > 0 && !PtInRegion(region, 0, 0) && PtInRegion(region, centerX, centerY); }
            finally { DeleteObject(region); }
        }
        [StructLayout(LayoutKind.Sequential)] private struct AccentPolicy { public int State, Flags, Color, Animation; }
        [StructLayout(LayoutKind.Sequential)] private struct CompositionData { public int Attribute; public IntPtr Data; public int Size; }

        // Accent blur preserves live content behind the DWM glass window. This compatibility
        // API is optional: unsupported systems retain the translucent WPF tint instead.
        internal static bool SetGlass(IntPtr window, bool enabled)
        {
            // Use light blur instead of Acrylic's dense luminosity layer so wallpaper shapes remain visible.
            // The WPF gradient supplies the adjustable milk-white wash.
            AccentPolicy policy = new AccentPolicy { State = enabled ? 3 : 0 };
            IntPtr memory = Marshal.AllocHGlobal(Marshal.SizeOf(typeof(AccentPolicy)));
            try
            {
                Marshal.StructureToPtr(policy, memory, false);
                CompositionData data = new CompositionData { Attribute = 19, Data = memory, Size = Marshal.SizeOf(typeof(AccentPolicy)) };
                return SetWindowCompositionAttribute(window, ref data);
            }
            catch (EntryPointNotFoundException) { return false; }
            finally { Marshal.FreeHGlobal(memory); }
        }
        internal static void ClipGlass(IntPtr window, Rect bounds, double radius, Matrix pixels)
        {
            Point origin = pixels.Transform(bounds.TopLeft), end = pixels.Transform(bounds.BottomRight);
            double diameter = Math.Min(radius * 2, Math.Min(bounds.Width, bounds.Height));
            IntPtr region = CreateRoundRectRgn((int)Math.Round(origin.X), (int)Math.Round(origin.Y),
                (int)Math.Ceiling(end.X), (int)Math.Ceiling(end.Y),
                (int)Math.Round(diameter * pixels.M11), (int)Math.Round(diameter * pixels.M22));
            // DWM consumes a copy of the region. Constrain its backdrop independently of
            // HWND painting: SetWindowRgn alone does not describe the blur footprint.
            if (region != IntPtr.Zero)
            {
                BlurRegion blur = new BlurRegion { Flags = 3, Enable = 1, Region = region };
                DwmEnableBlurBehindWindow(window, ref blur);
            }
            // Windows owns the region only after successful SetWindowRgn.
            if (region != IntPtr.Zero && SetWindowRgn(window, region, true) == 0) DeleteObject(region);
        }
        [DllImport("shell32.dll", CharSet=CharSet.Unicode)] private static extern IntPtr SHGetFileInfo(string path, uint attributes, out ShellInfo info, uint size, uint flags);
        [StructLayout(LayoutKind.Sequential, CharSet=CharSet.Unicode)] private struct ShellInfo
        {
            public IntPtr Icon; public int Index; public uint Attributes;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst=260)] public string DisplayName;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst=80)] public string TypeName;
        }
        internal static List<IntPtr> DesktopLists()
        {
            List<IntPtr> result = new List<IntPtr>();
            EnumWindows(delegate(IntPtr w, IntPtr p) {
                IntPtr view = FindWindowEx(w, IntPtr.Zero, "SHELLDLL_DefView", null);
                if (view != IntPtr.Zero) {
                    IntPtr list = FindWindowEx(view, IntPtr.Zero, "SysListView32", null);
                    if (list != IntPtr.Zero) result.Add(list);
                }
                return true;
            }, IntPtr.Zero);
            return result;
        }
        internal static ImageSource Icon(string path)
        {
            ShellInfo info;
            if (!System.IO.Path.IsPathRooted(path) && path.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                path = System.IO.Path.Combine(Environment.GetFolderPath(path.Equals("explorer.exe", StringComparison.OrdinalIgnoreCase) ? Environment.SpecialFolder.Windows : Environment.SpecialFolder.System), path);
            // Ask only for the base system image index: no SHGFI_LINKOVERLAY/ADDOVERLAYS.
            if (SHGetFileInfo(path, 0, out info, (uint)Marshal.SizeOf(typeof(ShellInfo)), 0x4000) == IntPtr.Zero) return null;
            Guid iid = typeof(IImageList).GUID;
            IImageList list = null; IntPtr icon = IntPtr.Zero;
            try
            {
                if (SHGetImageList(4, ref iid, out list) < 0) return null; // SHIL_JUMBO: 256 px
                list.GetIcon(info.Index, 1, out icon); // ILD_TRANSPARENT, overlay mask = 0
                BitmapSource image = Imaging.CreateBitmapSourceFromHIcon(icon, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
                image = TrimPaddedIcon(image); image.Freeze(); return image;
            }
            catch (COMException) { return null; }
            finally { if (icon != IntPtr.Zero) DestroyIcon(icon); if (list != null) Marshal.ReleaseComObject(list); }
        }
        [DllImport("shell32.dll")] private static extern int SHGetImageList(int size, ref Guid iid, [MarshalAs(UnmanagedType.Interface)] out IImageList list);
        [ComImport, Guid("46EB5926-582E-4017-9FDF-E8998DAA0950"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IImageList
        {
            void Add(); void ReplaceIcon(); void SetOverlayImage(); void Replace(); void AddMasked(); void Draw(); void Remove();
            void GetIcon(int index, int flags, out IntPtr icon);
        }
        private static BitmapSource TrimPaddedIcon(BitmapSource source)
        {
            // Some legacy apps put a 32px icon in the corner of a 256px canvas.
            FormatConvertedBitmap rgba = new FormatConvertedBitmap(source, PixelFormats.Bgra32, null, 0);
            int w = rgba.PixelWidth, h = rgba.PixelHeight, stride = w * 4;
            byte[] data = new byte[stride * h]; rgba.CopyPixels(data, stride, 0);
            int left = w, top = h, right = -1, bottom = -1;
            for (int y = 0; y < h; y++) for (int x = 0; x < w; x++)
                if (data[y * stride + x * 4 + 3] > 8) { left = Math.Min(left, x); top = Math.Min(top, y); right = Math.Max(right, x); bottom = Math.Max(bottom, y); }
            if (right >= left && Math.Max(right-left+1, bottom-top+1) < w / 2)
            {
                int size = Math.Min(w, Math.Max(right-left+1, bottom-top+1) + 4);
                return new CroppedBitmap(rgba, new Int32Rect(Math.Max(0, Math.Min(w-size, left-2)), Math.Max(0, Math.Min(h-size, top-2)), size, size));
            }
            return source;
        }
        internal static void PlaceAtDesktop(IntPtr hwnd)
        {
            IntPtr progman = FindWindow("Progman", null);
            if (progman == IntPtr.Zero) return;
            if (GetWindowLongPtr(hwnd, -8) != progman) SetWindowLongPtr(hwnd, -8, progman);
            List<IntPtr> lists = DesktopLists();
            IntPtr desktop = lists.Count > 0 ? GetAncestor(lists[0], 2) : progman;
            IntPtr previous = GetWindow(desktop, 3); // GW_HWNDPREV: directly above desktop
            if (previous != IntPtr.Zero && previous != hwnd && (GetWindowLongPtr(previous, -20).ToInt64() & 8) == 0)
                SetWindowPos(hwnd, previous, 0, 0, 0, 0, 0x13);
        }
        internal static void DetachDesktopOwner(IntPtr hwnd) { SetWindowLongPtr(hwnd, -8, IntPtr.Zero); }
    }

    // Only hides the icon list, never its DefView/WorkerW parent or the wallpaper.
    // Captures the original visibility for every new Explorer window.
    internal sealed class DesktopVisibility : IDisposable
    {
        private readonly Dictionary<IntPtr, bool> original = new Dictionary<IntPtr, bool>();
        private bool active;
        internal void SetHidden(bool hide)
        {
            if (!hide) { Restore(); return; }
            active = true;
            foreach (IntPtr list in Native.DesktopLists())
            {
                if (!original.ContainsKey(list))
                {
                    original[list] = Native.IsWindowVisible(list);
                    // Persist recovery before hiding, so a killed UI can be recovered by the guardian.
                    WriteRecovery();
                }
                if (Native.IsWindowVisible(list)) Native.ShowWindow(list, 0);
            }
        }
        private void WriteRecovery()
        {
            System.IO.Directory.CreateDirectory(Settings.Folder);
            List<string> lines = new List<string>();
            foreach (KeyValuePair<IntPtr, bool> pair in original)
                if (pair.Value) lines.Add(pair.Key.ToInt64().ToString());
            System.IO.File.WriteAllLines(System.IO.Path.Combine(Settings.Folder, "visibility.recovery"), lines.ToArray());
        }
        internal static void Recover()
        {
            string file = System.IO.Path.Combine(Settings.Folder, "visibility.recovery");
            if (!System.IO.File.Exists(file)) return;
            List<IntPtr> current = Native.DesktopLists();
            foreach (string line in System.IO.File.ReadAllLines(file))
            {
                long h;
                if (long.TryParse(line, out h) && current.Contains(new IntPtr(h))) Native.ShowWindow(new IntPtr(h), 5);
            }
            System.IO.File.Delete(file);
        }
        private void Restore()
        {
            if (!active) return;
            List<IntPtr> current = Native.DesktopLists();
            foreach (KeyValuePair<IntPtr, bool> pair in original)
                if (pair.Value && current.Contains(pair.Key)) Native.ShowWindow(pair.Key, 5);
            original.Clear(); active = false;
            string file = System.IO.Path.Combine(Settings.Folder, "visibility.recovery");
            if (System.IO.File.Exists(file)) System.IO.File.Delete(file);
        }
        public void Dispose() { Restore(); }
    }
}
