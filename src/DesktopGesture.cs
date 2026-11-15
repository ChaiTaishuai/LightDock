using System;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows.Threading;

namespace AsterDock
{
    internal sealed class DesktopClickPair
    {
        private bool pending; private int x, y; private uint time;
        internal bool Click(int px, int py, uint now, bool empty, uint interval, int dx, int dy)
        {
            if (!empty) { pending = false; return false; }
            bool pair = pending && unchecked(now - time) <= interval && Math.Abs(px-x) <= dx && Math.Abs(py-y) <= dy;
            pending = !pair; x = px; y = py; time = now; return pair;
        }
    }
    // Observe only: all mouse events are always forwarded, including interactive wallpapers.
    internal sealed class DesktopGesture : IDisposable
    {
        [StructLayout(LayoutKind.Sequential)] internal struct Point { public int X, Y; }
        [StructLayout(LayoutKind.Sequential)] private struct MouseData { public Point Point; public uint Data, Flags, Time; public UIntPtr Extra; }
        [StructLayout(LayoutKind.Sequential)] private struct Message { public IntPtr Window; public uint Id; public UIntPtr W; public IntPtr L; public uint Time; public Point Point; public uint Private; }
        private delegate IntPtr HookProc(int code, IntPtr message, IntPtr data);
        [DllImport("user32.dll")] private static extern IntPtr SetWindowsHookEx(int kind, HookProc proc, IntPtr module, uint thread);
        [DllImport("user32.dll")] private static extern bool UnhookWindowsHookEx(IntPtr hook);
        [DllImport("user32.dll")] private static extern IntPtr CallNextHookEx(IntPtr hook, int code, IntPtr message, IntPtr data);
        [DllImport("user32.dll")] private static extern int GetMessage(out Message message, IntPtr window, uint min, uint max);
        [DllImport("user32.dll")] private static extern bool PeekMessage(out Message message, IntPtr window, uint min, uint max, uint flags);
        [DllImport("user32.dll")] private static extern bool PostThreadMessage(uint thread, uint message, IntPtr w, IntPtr l);
        [DllImport("kernel32.dll")] private static extern uint GetCurrentThreadId();
        [DllImport("kernel32.dll", CharSet=CharSet.Unicode)] private static extern IntPtr GetModuleHandle(string module);
        [DllImport("user32.dll")] private static extern IntPtr WindowFromPoint(Point point);
        [DllImport("user32.dll")] private static extern IntPtr GetAncestor(IntPtr window, uint flags);
        [DllImport("user32.dll", CharSet=CharSet.Unicode)] private static extern int GetClassName(IntPtr window, StringBuilder name, int count);
        [DllImport("user32.dll")] private static extern bool ScreenToClient(IntPtr window, ref Point point);
        [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr window, out uint process);
        [DllImport("kernel32.dll")] private static extern IntPtr OpenProcess(uint access, bool inherit, uint process);
        [DllImport("kernel32.dll")] private static extern bool CloseHandle(IntPtr handle);
        [DllImport("kernel32.dll")] private static extern IntPtr VirtualAllocEx(IntPtr process, IntPtr address, UIntPtr size, uint allocation, uint protect);
        [DllImport("kernel32.dll")] private static extern bool VirtualFreeEx(IntPtr process, IntPtr address, UIntPtr size, uint type);
        [DllImport("kernel32.dll")] private static extern bool WriteProcessMemory(IntPtr process, IntPtr address, byte[] data, UIntPtr size, out UIntPtr written);
        [DllImport("user32.dll")] private static extern IntPtr SendMessageTimeout(IntPtr window, uint message, IntPtr w, IntPtr l, uint flags, uint timeout, out IntPtr result);
        private readonly Dispatcher dispatcher;
        private readonly Action toggle;
        private readonly DesktopClickPair clicks = new DesktopClickPair();
        private readonly Thread thread;
        private readonly HookProc callback;
        private volatile bool disposed;
        private uint threadId;
        private IntPtr hook;
        private Point down;
        private bool pressed, moved;
        private int dragThresholdX = 4, dragThresholdY = 4;
        internal bool Available { get; private set; }
        internal DesktopGesture(Dispatcher dispatcher, Action toggle)
        {
            this.dispatcher = dispatcher; this.toggle = toggle; callback = OnMouse;
            thread = new Thread(Run) { IsBackground = true, Name = "Aster desktop mouse observer" };
            thread.Start();
        }
        private void Run()
        {
            threadId = GetCurrentThreadId(); Message message;
            PeekMessage(out message, IntPtr.Zero, 0, 0, 0);
            if (disposed) return;
            hook = SetWindowsHookEx(14, callback, GetModuleHandle(null), 0);
            Available = hook != IntPtr.Zero;
            try { if (Available) while (!disposed && GetMessage(out message, IntPtr.Zero, 0, 0) > 0) { } }
            finally { if (hook != IntPtr.Zero) UnhookWindowsHookEx(hook); Available = false; }
        }
        private IntPtr OnMouse(int code, IntPtr message, IntPtr data)
        {
            if (code >= 0 && !disposed)
            {
                MouseData mouse = (MouseData)Marshal.PtrToStructure(data, typeof(MouseData));
                int kind = message.ToInt32();
                if (kind == 0x201)
                {
                    down = mouse.Point; pressed = true; moved = false;
                    uint dpi = (uint)Math.Round(96*Displays.AtPoint(new System.Windows.Point(down.X,down.Y)).Scale);
                    dragThresholdX = Displays.GetSystemMetricsForDpi(68,dpi); dragThresholdY = Displays.GetSystemMetricsForDpi(69,dpi);
                }
                if (kind == 0x200 && pressed && (Math.Abs(mouse.Point.X-down.X) > dragThresholdX || Math.Abs(mouse.Point.Y-down.Y) > dragThresholdY)) moved = true;
                if (kind == 0x202)
                {
                    bool click = pressed && !moved && (mouse.Flags & 1) == 0;
                    pressed = false;
                    IntPtr root = GetAncestor(WindowFromPoint(mouse.Point), 2);
                    StringBuilder name = new StringBuilder(128); GetClassName(root, name, name.Capacity);
                    bool desktopRoot = name.ToString() == "Progman" || name.ToString() == "WorkerW";
                    // No shell calls, remote memory, or waiting inside the low-level hook.
                    dispatcher.BeginInvoke(new Action(delegate {
                        if (disposed) return;
                        bool empty = click && desktopRoot && IsEmptyDesktop(mouse.Point);
                        uint dpi = (uint)Math.Round(96*Displays.AtPoint(new System.Windows.Point(mouse.Point.X,mouse.Point.Y)).Scale);
                        if (clicks.Click(mouse.Point.X, mouse.Point.Y, mouse.Time, empty, Native.GetDoubleClickTime(), Displays.GetSystemMetricsForDpi(36,dpi)/2, Displays.GetSystemMetricsForDpi(37,dpi)/2)) toggle();
                    }));
                }
            }
            return CallNextHookEx(hook, code, message, data);
        }
        internal static bool IsEmptyDesktop(Point screen)
        {
            foreach (IntPtr list in Native.DesktopLists())
            {
                if (!Native.IsWindowVisible(list)) continue;
                if (!WindowRegions.ContainsScreenPoint(list,screen.X,screen.Y)) continue;
                Point point = screen;
                if (!ScreenToClient(list, ref point)) return false;
                uint id; GetWindowThreadProcessId(list, out id);
                // LVM_HITTEST carries a pointer: supply data in Explorer's address space.
                // No code injection; only a temporary LVHITTESTINFO buffer, freed below.
                IntPtr process = OpenProcess(0x28, false, id);
                if (process == IntPtr.Zero) return false;
                IntPtr memory = IntPtr.Zero;
                try
                {
                    byte[] buffer = new byte[24]; Array.Copy(BitConverter.GetBytes(point.X), 0, buffer, 0, 4); Array.Copy(BitConverter.GetBytes(point.Y), 0, buffer, 4, 4);
                    memory = VirtualAllocEx(process, IntPtr.Zero, (UIntPtr)buffer.Length, 0x3000, 4);
                    UIntPtr written;
                    if (memory == IntPtr.Zero || !WriteProcessMemory(process, memory, buffer, (UIntPtr)buffer.Length, out written)) return false;
                    IntPtr hit;
                    if (SendMessageTimeout(list, 0x1012, IntPtr.Zero, memory, 0x22, 100, out hit) == IntPtr.Zero) return false;
                    if (hit.ToInt64() != -1 && hit.ToInt64() != 0xFFFFFFFFL) return false;
                }
                finally { if (memory != IntPtr.Zero) VirtualFreeEx(process, memory, UIntPtr.Zero, 0x8000); CloseHandle(process); }
            }
            return true;
        }
        internal static bool IsDesktopBackground(Point screen)
        {
            IntPtr root = GetAncestor(WindowFromPoint(screen), 2);
            StringBuilder name = new StringBuilder(128); GetClassName(root, name, name.Capacity);
            return (name.ToString() == "Progman" || name.ToString() == "WorkerW") && IsEmptyDesktop(screen);
        }
        public void Dispose() { disposed = true; if (threadId != 0) PostThreadMessage(threadId, 0x12, IntPtr.Zero, IntPtr.Zero); }
    }
}
