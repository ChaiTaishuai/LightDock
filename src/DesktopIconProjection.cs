using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows.Threading;

namespace AsterDock
{
    internal static class WindowRegions
    {
        [StructLayout(LayoutKind.Sequential)] internal struct Rect { public int Left,Top,Right,Bottom; }
        [DllImport("gdi32.dll")] internal static extern IntPtr CreateRectRgn(int left,int top,int right,int bottom);
        [DllImport("gdi32.dll")] internal static extern int CombineRgn(IntPtr result,IntPtr first,IntPtr second,int mode);
        [DllImport("gdi32.dll")] internal static extern bool DeleteObject(IntPtr value);
        [DllImport("gdi32.dll")] internal static extern bool PtInRegion(IntPtr region,int x,int y);
        [DllImport("gdi32.dll")] private static extern uint GetRegionData(IntPtr region,uint size,byte[] data);
        [DllImport("gdi32.dll")] private static extern IntPtr ExtCreateRegion(IntPtr transform,uint size,byte[] data);
        [DllImport("user32.dll")] private static extern int GetWindowRgn(IntPtr window,IntPtr region);
        [DllImport("user32.dll")] private static extern int SetWindowRgn(IntPtr window,IntPtr region,bool redraw);
        [DllImport("user32.dll")] internal static extern bool GetWindowRect(IntPtr window,out Rect rect);
        [DllImport("user32.dll")] private static extern bool ClientToScreen(IntPtr window,ref System.Drawing.Point point);
        [DllImport("user32.dll")] private static extern IntPtr SendMessageTimeout(IntPtr window,uint message,IntPtr wp,IntPtr lp,uint flags,uint timeout,out IntPtr result);
        [DllImport("kernel32.dll")] private static extern IntPtr OpenProcess(uint access,bool inherit,uint process);
        [DllImport("kernel32.dll")] private static extern bool CloseHandle(IntPtr handle);
        [DllImport("kernel32.dll")] private static extern IntPtr VirtualAllocEx(IntPtr process,IntPtr address,UIntPtr size,uint allocation,uint protect);
        [DllImport("kernel32.dll")] private static extern bool VirtualFreeEx(IntPtr process,IntPtr address,UIntPtr size,uint type);
        [DllImport("kernel32.dll")] private static extern bool WriteProcessMemory(IntPtr process,IntPtr address,byte[] buffer,UIntPtr size,out UIntPtr written);
        [DllImport("kernel32.dll")] private static extern bool ReadProcessMemory(IntPtr process,IntPtr address,byte[] buffer,UIntPtr size,out UIntPtr read);
        internal static byte[] Read(IntPtr window)
        {
            IntPtr region = CreateRectRgn(0,0,0,0);
            try { return GetWindowRgn(window,region) == 0 ? null : Bytes(region); }
            finally { DeleteObject(region); }
        }
        internal static byte[] Bytes(IntPtr region)
        {
            uint size = GetRegionData(region,0,null);
            if (size == 0) throw new InvalidOperationException("无法读取桌面显示区域。");
            byte[] data = new byte[size];
            if (GetRegionData(region,size,data) == 0) throw new InvalidOperationException("无法读取桌面显示区域。");
            return data;
        }
        internal static IntPtr FromBytes(byte[] data) { return data == null ? IntPtr.Zero : ExtCreateRegion(IntPtr.Zero,(uint)data.Length,data); }
        internal static bool Equal(byte[] a,byte[] b)
        { if (a == null || b == null) return a == b; if (a.Length != b.Length) return false; for (int i=0;i<a.Length;i++) if (a[i]!=b[i]) return false; return true; }
        internal static bool ContainsScreenPoint(IntPtr window,int x,int y)
        {
            byte[] data=Read(window); if(data==null) return true;
            Rect rect; if(!GetWindowRect(window,out rect)) return false;
            IntPtr region=FromBytes(data);
            try { return PtInRegion(region,x-rect.Left,y-rect.Top); }
            finally { DeleteObject(region); }
        }
        internal static void Apply(IntPtr window,byte[] data)
        {
            IntPtr region = FromBytes(data);
            if (data != null && region == IntPtr.Zero) throw new InvalidOperationException("无法建立桌面显示区域。");
            if (SetWindowRgn(window,region,true) == 0) { if (region != IntPtr.Zero) DeleteObject(region); throw new InvalidOperationException("无法更新桌面显示区域。"); }
            // On success USER owns the region.
        }
        internal static byte[] Exclude(IntPtr window,byte[] original,List<Rect> holes)
        {
            Rect bounds; if (!GetWindowRect(window,out bounds)) throw new InvalidOperationException("桌面窗口已失效。");
            IntPtr region = original == null ? CreateRectRgn(0,0,bounds.Right-bounds.Left,bounds.Bottom-bounds.Top) : FromBytes(original);
            if (region == IntPtr.Zero) throw new InvalidOperationException("无法建立桌面显示区域。");
            try
            {
                foreach (Rect hole in holes)
                {
                    IntPtr cut = CreateRectRgn(hole.Left,hole.Top,hole.Right,hole.Bottom);
                    try { if (CombineRgn(region,region,cut,4)==0) throw new InvalidOperationException("无法收纳桌面图标。"); }
                    finally { DeleteObject(cut); }
                }
                return Bytes(region);
            }
            finally { DeleteObject(region); }
        }
        internal static List<Rect> ItemBounds(IntPtr list,List<int> indices)
        {
            List<Rect> result = new List<Rect>();
            if (indices.Count == 0) return result;
            uint pid; ShellRecovery.GetWindowThreadProcessId(list,out pid);
            IntPtr process = OpenProcess(0x38,false,pid), memory = IntPtr.Zero;
            if (process == IntPtr.Zero) throw new InvalidOperationException("无法读取桌面图标边界。");
            try
            {
                memory = VirtualAllocEx(process,IntPtr.Zero,(UIntPtr)16,0x3000,4);
                if (memory == IntPtr.Zero) throw new InvalidOperationException("无法读取桌面图标边界。");
                Rect window; GetWindowRect(list,out window);
                System.Drawing.Point client = new System.Drawing.Point(); ClientToScreen(list,ref client);
                foreach (int index in indices)
                {
                    byte[] buffer = new byte[16]; UIntPtr count; IntPtr answer;
                    // LVIR_BOUNDS includes both image and label. A read-only query; no
                    // ListView item deletion or position writes into Explorer's model.
                    if (!WriteProcessMemory(process,memory,buffer,(UIntPtr)16,out count) || count.ToUInt64()!=16 ||
                        SendMessageTimeout(list,0x100E,new IntPtr(index),memory,0x22,150,out answer)==IntPtr.Zero || answer==IntPtr.Zero ||
                        !ReadProcessMemory(process,memory,buffer,(UIntPtr)16,out count) || count.ToUInt64()!=16)
                        throw new InvalidOperationException("桌面正在刷新，暂缓收纳图标。");
                    int dx=client.X-window.Left,dy=client.Y-window.Top;
                    result.Add(new Rect { Left=BitConverter.ToInt32(buffer,0)+dx,Top=BitConverter.ToInt32(buffer,4)+dy,Right=BitConverter.ToInt32(buffer,8)+dx,Bottom=BitConverter.ToInt32(buffer,12)+dy });
                }
                return result;
            }
            finally { if(memory!=IntPtr.Zero) VirtualFreeEx(process,memory,UIntPtr.Zero,0x8000); CloseHandle(process); }
        }
    }

    internal sealed class DesktopIconProjection : IDisposable
    {
        private readonly Dispatcher dispatcher;
        private readonly List<ShellWindowRecord> records = new List<ShellWindowRecord>();
        private bool disposed,busy;
        private int revision;
        private HashSet<string> targets = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private static string FilePath { get { return Path.Combine(Settings.Folder,"desktop-items.recovery.xml"); } }
        internal int HiddenCount { get; private set; }
        internal string LastError { get; private set; }
        internal bool Busy { get { return busy; } }
        internal DesktopIconProjection(Dispatcher dispatcher) { this.dispatcher=dispatcher; }
        internal static HashSet<string> Paths(IEnumerable<DockItem> items)
        {
            HashSet<string> paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach(DockItem item in items)
                if(item!=null && !string.IsNullOrEmpty(item.Target) && Path.IsPathRooted(item.Target))
                    try { paths.Add(Path.GetFullPath(item.Target).TrimEnd(Path.DirectorySeparatorChar)); } catch(ArgumentException) { } catch(NotSupportedException) { }
            return paths;
        }
        internal void Update(IEnumerable<DockItem> items)
        {
            if(disposed) return;
            HashSet<string> next = Paths(items);
            if(!targets.SetEquals(next)) { targets=next; revision++; }
            Refresh();
        }
        private sealed class Snapshot
        { internal IntPtr Window; internal ShellWindowRecord Identity; internal List<WindowRegions.Rect> Holes; }
        private static List<Snapshot> Capture(HashSet<string> paths)
        {
            List<Snapshot> snapshots = new List<Snapshot>();
            if(paths.Count==0) return snapshots;
            List<DesktopEntry> entries=ShellDesktop.Read();
            List<int> indices=new List<int>();
            foreach(DesktopEntry entry in entries) if(paths.Contains(entry.Path.TrimEnd(Path.DirectorySeparatorChar))) indices.Add(entry.Index);
            foreach(IntPtr list in Native.DesktopLists()) snapshots.Add(new Snapshot { Window=list,Identity=ShellRecovery.Capture(list),Holes=WindowRegions.ItemBounds(list,indices) });
            return snapshots;
        }
        private void Refresh()
        {
            if(disposed || busy) return;
            busy=true; int version=revision; HashSet<string> paths=new HashSet<string>(targets,StringComparer.OrdinalIgnoreCase);
            Task.Factory.StartNew(delegate { return Capture(paths); }).ContinueWith(delegate(Task<List<Snapshot>> task) {
                if(dispatcher.HasShutdownStarted) { var ignored=task.Exception; return; }
                dispatcher.BeginInvoke(new Action(delegate {
                    busy=false;
                    if(disposed) { var ignored=task.Exception; return; }
                    if(version!=revision) { var ignored=task.Exception; Refresh(); return; }
                    try { if(task.IsFaulted) throw task.Exception.GetBaseException(); Apply(task.Result); LastError=null; }
                    catch(Exception ex) { if(LastError!=ex.Message) ShellRecovery.Log(ex); LastError=ex.Message; }
                }));
            });
        }
        private void Apply(List<Snapshot> snapshots)
        {
            List<IntPtr> current=Native.DesktopLists();
            int count=0;
            foreach(Snapshot snapshot in snapshots)
            {
                if(!ShellRecovery.Matches(snapshot.Identity,current)) continue;
                ShellWindowRecord record=records.Find(delegate(ShellWindowRecord r) { return r.Handle==snapshot.Window.ToInt64() && ShellRecovery.Matches(r,current); });
                if(snapshot.Holes.Count==0) continue;
                byte[] now=WindowRegions.Read(snapshot.Window);
                if(record==null)
                { record=snapshot.Identity; record.OriginalRegion=now; records.Add(record); }
                else if(!WindowRegions.Equal(now,record.AppliedRegion) && !WindowRegions.Equal(now,record.PreviousRegion))
                    record.OriginalRegion=now; // respect another desktop manager's new region
                byte[] mask=WindowRegions.Exclude(snapshot.Window,record.OriginalRegion,snapshot.Holes);
                if(!WindowRegions.Equal(now,mask))
                {
                    record.PreviousRegion=now; record.AppliedRegion=mask;
                    ShellRecovery.Write(FilePath,records); // durable before any visual mutation
                    WindowRegions.Apply(snapshot.Window,mask);
                }
                count+=snapshot.Holes.Count;
            }
            bool removed=false;
            foreach(ShellWindowRecord record in records.ToArray())
            {
                Snapshot wanted=snapshots.Find(delegate(Snapshot s) { return s.Window.ToInt64()==record.Handle && s.Holes.Count>0 && s.Identity.ProcessStarted==record.ProcessStarted; });
                if(wanted!=null) continue;
                Restore(record,current); records.Remove(record); removed=true;
            }
            if(records.Count==0) { if(File.Exists(FilePath)) File.Delete(FilePath); }
            else if(removed) ShellRecovery.Write(FilePath,records);
            HiddenCount=count;
        }
        private static void Restore(ShellWindowRecord record,List<IntPtr> current)
        {
            if(!ShellRecovery.Matches(record,current)) return;
            IntPtr hwnd=new IntPtr(record.Handle); byte[] now=WindowRegions.Read(hwnd);
            // Do not overwrite a region installed by another app after ours.
            if(WindowRegions.Equal(now,record.AppliedRegion) || WindowRegions.Equal(now,record.PreviousRegion)) WindowRegions.Apply(hwnd,record.OriginalRegion);
        }
        internal static void Recover()
        {
            List<ShellWindowRecord> records=ShellRecovery.Read(FilePath),pending=new List<ShellWindowRecord>();
            List<IntPtr> current=Native.DesktopLists();
            foreach(ShellWindowRecord record in records) try { Restore(record,current); } catch(Exception ex) { pending.Add(record); ShellRecovery.Log(ex); }
            if(pending.Count>0) ShellRecovery.Write(FilePath,pending); else if(File.Exists(FilePath)) File.Delete(FilePath);
        }
        public void Dispose()
        {
            if(disposed) return; disposed=true;
            if(records.Count>0) Recover();
            records.Clear(); HiddenCount=0;
        }
    }
}
