using System;
using System.Runtime.InteropServices;
using System.Windows.Threading;
namespace AsterDock
{
    internal sealed partial class DockWindow
    {
        [StructLayout(LayoutKind.Sequential)] private struct NotifyEntry { internal IntPtr Pidl; [MarshalAs(UnmanagedType.Bool)] internal bool Recursive; }
        [DllImport("shell32.dll", CharSet=CharSet.Unicode)] private static extern int SHParseDisplayName(string name,IntPtr context,out IntPtr pidl,uint flags,out uint attributes);
        [DllImport("shell32.dll")] private static extern uint SHChangeNotifyRegister(IntPtr hwnd,int sources,int events,uint message,int count,ref NotifyEntry entry);
        [DllImport("shell32.dll")] private static extern bool SHChangeNotifyDeregister(uint id);
        [DllImport("user32.dll",CharSet=CharSet.Unicode)] private static extern uint RegisterWindowMessage(string name);
        private const int RecycleMessage=0x8000+30;
        private uint recycleRegistration,imageRegistration,taskbarCreated;
        private IntPtr rootPidl;
        private DispatcherTimer recycleDebounce;
        private bool RecycleNotificationsAvailable { get { return recycleRegistration!=0 && imageRegistration!=0; } }
        private void StartRecycleNotifications()
        {
            StopRecycleNotifications();
            taskbarCreated=RegisterWindowMessage("TaskbarCreated");
            recycleDebounce=new DispatcherTimer { Interval=TimeSpan.FromMilliseconds(400) };
            recycleDebounce.Tick+=delegate { recycleDebounce.Stop(); RefreshRecycleIcons(); };
            IntPtr bin=IntPtr.Zero; uint attributes;
            try {
                if(SHParseDisplayName(ShellDrop.RecycleBin,IntPtr.Zero,out bin,0,out attributes)<0) return;
                rootPidl=Marshal.AllocCoTaskMem(2); Marshal.WriteInt16(rootPidl,0);
                var entry=new NotifyEntry { Pidl=bin,Recursive=true };
                recycleRegistration=SHChangeNotifyRegister(hwnd,3,0x7fffffff,RecycleMessage,1,ref entry);
                entry.Pidl=rootPidl;
                imageRegistration=SHChangeNotifyRegister(hwnd,3,0x8000,RecycleMessage,1,ref entry);
            } catch(Exception ex) { ShellRecovery.Log(ex); }
            finally { if(bin!=IntPtr.Zero) Marshal.FreeCoTaskMem(bin); }
        }
        private bool HandleRecycleMessage(int msg)
        {
            if(msg==RecycleMessage) { if(recycleDebounce!=null) { recycleDebounce.Stop(); recycleDebounce.Start(); } return true; }
            if(taskbarCreated!=0 && (uint)msg==taskbarCreated) { StartRecycleNotifications(); RefreshRecycleIcons(); }
            return false;
        }
        private void StopRecycleNotifications()
        {
            if(recycleDebounce!=null) { recycleDebounce.Stop(); recycleDebounce=null; }
            if(recycleRegistration!=0) SHChangeNotifyDeregister(recycleRegistration);
            if(imageRegistration!=0) SHChangeNotifyDeregister(imageRegistration);
            recycleRegistration=imageRegistration=0;
            if(rootPidl!=IntPtr.Zero) { Marshal.FreeCoTaskMem(rootPidl); rootPidl=IntPtr.Zero; }
        }
    }
}
