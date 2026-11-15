using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Xml.Serialization;

namespace AsterDock
{
    public sealed class ShellWindowRecord
    {
        public long Handle, ProcessStarted;
        public uint Process;
        public string ClassName;
        public bool Visible;
        public byte[] OriginalRegion, AppliedRegion, PreviousRegion;
    }
    internal static class ShellRecovery
    {
        [DllImport("user32.dll", CharSet=CharSet.Unicode)] private static extern int GetClassName(IntPtr window,StringBuilder text,int count);
        [DllImport("user32.dll")] internal static extern uint GetWindowThreadProcessId(IntPtr window,out uint process);
        internal static string ClassOf(IntPtr window) { StringBuilder text = new StringBuilder(256); GetClassName(window,text,text.Capacity); return text.ToString(); }
        internal static ShellWindowRecord Capture(IntPtr window)
        {
            uint pid; GetWindowThreadProcessId(window,out pid);
            using (Process process = Process.GetProcessById((int)pid))
                return new ShellWindowRecord { Handle = window.ToInt64(), Process = pid, ProcessStarted = process.StartTime.ToUniversalTime().Ticks, ClassName = ClassOf(window), Visible = Native.IsWindowVisible(window) };
        }
        internal static bool Matches(ShellWindowRecord record, List<IntPtr> allowed)
        {
            IntPtr hwnd = new IntPtr(record.Handle);
            if (!allowed.Contains(hwnd)) return false;
            try { ShellWindowRecord now = Capture(hwnd); return now.Process == record.Process && now.ProcessStarted == record.ProcessStarted && now.ClassName == record.ClassName; }
            catch (ArgumentException) { return false; }
        }
        internal static void Write(string file,List<ShellWindowRecord> records)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(file));
            string temp = file + ".tmp";
            using (FileStream stream = new FileStream(temp,FileMode.Create,FileAccess.Write,FileShare.None))
            { new XmlSerializer(typeof(List<ShellWindowRecord>)).Serialize(stream,records); stream.Flush(true); }
            if (File.Exists(file)) File.Replace(temp,file,null); else File.Move(temp,file);
        }
        internal static List<ShellWindowRecord> Read(string file)
        {
            if (!File.Exists(file)) return new List<ShellWindowRecord>();
            using (FileStream stream = File.OpenRead(file)) return (List<ShellWindowRecord>)new XmlSerializer(typeof(List<ShellWindowRecord>)).Deserialize(stream);
        }
        internal static void RecoverAll()
        {
            // Each recovery is independent: a damaged journal must not block the others.
            foreach (Action action in new Action[] { TaskbarVisibility.Recover, DesktopIconProjection.Recover, DesktopVisibility.Recover })
                try { action(); } catch (Exception ex) { Log(ex); }
        }
        internal static void Log(Exception ex)
        { try { Directory.CreateDirectory(Settings.Folder); File.AppendAllText(Path.Combine(Settings.Folder,"shell-recovery.log"),DateTime.Now.ToString("O")+" "+ex+Environment.NewLine); } catch (IOException) { } catch (UnauthorizedAccessException) { } }
    }
    internal sealed class TaskbarVisibility : IDisposable
    {
        private readonly List<ShellWindowRecord> original = new List<ShellWindowRecord>();
        private static string FilePath { get { return Path.Combine(Settings.Folder,"taskbar.recovery.xml"); } }
        internal static List<IntPtr> Windows()
        {
            List<IntPtr> result = new List<IntPtr>();
            Native.EnumWindows(delegate(IntPtr w,IntPtr p) { string name = ShellRecovery.ClassOf(w); if (name == "Shell_TrayWnd" || name == "Shell_SecondaryTrayWnd") result.Add(w); return true; },IntPtr.Zero);
            return result;
        }
        internal void SetHidden(bool hide)
        {
            if (!hide) { Dispose(); return; }
            List<IntPtr> current = Windows();
            foreach (IntPtr window in current)
            {
                ShellWindowRecord record = original.Find(delegate(ShellWindowRecord r) { return r.Handle == window.ToInt64() && ShellRecovery.Matches(r,current); });
                if (record == null)
                {
                    record = ShellRecovery.Capture(window); original.Add(record);
                    try { ShellRecovery.Write(FilePath,original); } catch { original.Remove(record); throw; }
                }
                if (Native.IsWindowVisible(window)) Native.ShowWindow(window,0);
            }
        }
        internal static void Recover()
        { Restore(ShellRecovery.Read(FilePath)); }
        private static void Restore(List<ShellWindowRecord> records)
        {
            List<IntPtr> current = Windows();
            List<ShellWindowRecord> pending = new List<ShellWindowRecord>();
            foreach (ShellWindowRecord record in records)
            {
                if (!ShellRecovery.Matches(record,current)) continue;
                IntPtr hwnd = new IntPtr(record.Handle);
                Native.ShowWindow(hwnd,record.Visible ? 8 : 0); // restore without activation
                if (Native.IsWindowVisible(hwnd) != record.Visible) pending.Add(record);
            }
            if (pending.Count > 0) ShellRecovery.Write(FilePath,pending);
            else if (File.Exists(FilePath)) File.Delete(FilePath);
        }
        public void Dispose() { if (original.Count == 0) return; Restore(original); original.Clear(); }
    }
}
