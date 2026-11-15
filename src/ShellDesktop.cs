using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.InteropServices;

namespace AsterDock
{
    internal sealed class DesktopEntry
    {
        internal string Path;
        internal int Index;
        internal System.Drawing.Point Position;
    }

    // Read the Shell's item identities, not display labels (which can be duplicated).
    // This never moves, deletes, renames or changes attributes of a desktop file.
    internal static class ShellDesktop
    {
        [ComImport, Guid("6D5140C1-7436-11CE-8034-00AA006009FA"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface Services { void QueryService(ref Guid service, ref Guid iid, [MarshalAs(UnmanagedType.Interface)] out object result); }
        [ComImport, Guid("000214E2-0000-0000-C000-000000000046"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface Browser
        {
            void GetWindow(out IntPtr window); void ContextSensitiveHelp();
            void InsertMenusSB(); void SetMenuSB(); void RemoveMenusSB(); void SetStatusTextSB();
            void EnableModelessSB(); void TranslateAcceleratorSB(); void BrowseObject();
            void GetViewStateStream(); void GetControlWindow(); void SendControlMsg();
            void QueryActiveShellView([MarshalAs(UnmanagedType.Interface)] out object view);
        }
        [ComImport, Guid("CDE725B0-CCC9-4519-917E-325D72FAB4CE"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface FolderView
        {
            void GetCurrentViewMode(out uint mode); void SetCurrentViewMode(uint mode);
            void GetFolder(ref Guid iid, [MarshalAs(UnmanagedType.Interface)] out object folder);
            void Item(int index, out IntPtr item); void ItemCount(uint flags, out int count);
            void Items(); void GetSelectionMarkedItem(); void GetFocusedItem();
            void GetItemPosition(IntPtr item, out System.Drawing.Point position);
        }
        [ComImport, Guid("000214E6-0000-0000-C000-000000000046"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface Folder
        {
            void ParseDisplayName(); void EnumObjects(); void BindToObject(); void BindToStorage();
            void CompareIDs(); void CreateViewObject(); void GetAttributesOf(); void GetUIObjectOf();
            void GetDisplayNameOf(IntPtr item, uint flags, IntPtr name);
        }
        [DllImport("shlwapi.dll", CharSet=CharSet.Unicode)] private static extern int StrRetToStrW(IntPtr strret, IntPtr item, out IntPtr text);
        internal static List<DesktopEntry> Read()
        {
            object windows = null, dispatch = null, browser = null, view = null, folder = null;
            try
            {
                windows = Activator.CreateInstance(Type.GetTypeFromCLSID(new Guid("9BA05972-F6A8-11CF-A442-00A0C90A8F39")));
                object[] args = { 0, null, 8, 0, 1 }; // desktop, NEEDDISPATCH
                dispatch = windows.GetType().InvokeMember("FindWindowSW", BindingFlags.InvokeMethod, null, windows, args);
                if (dispatch == null) throw new InvalidOperationException("桌面 Shell 尚未就绪。");
                Guid service = new Guid("4C96BE40-915C-11CF-99D3-00AA004AE837"), iid = typeof(Browser).GUID;
                ((Services)dispatch).QueryService(ref service,ref iid,out browser);
                ((Browser)browser).QueryActiveShellView(out view);
                FolderView fv = (FolderView)view;
                iid = typeof(Folder).GUID; fv.GetFolder(ref iid,out folder);
                int count; fv.ItemCount(2,out count);
                List<DesktopEntry> result = new List<DesktopEntry>();
                for (int index = 0; index < count; index++)
                {
                    IntPtr item = IntPtr.Zero, strret = Marshal.AllocCoTaskMem(272), text = IntPtr.Zero;
                    try
                    {
                        fv.Item(index,out item);
                        ((Folder)folder).GetDisplayNameOf(item,0x8000,strret); // FORPARSING
                        Marshal.ThrowExceptionForHR(StrRetToStrW(strret,item,out text));
                        System.Drawing.Point position; fv.GetItemPosition(item,out position);
                        result.Add(new DesktopEntry { Index = index, Path = Marshal.PtrToStringUni(text), Position = position });
                    }
                    finally { if (item != IntPtr.Zero) Marshal.FreeCoTaskMem(item); if (text != IntPtr.Zero) Marshal.FreeCoTaskMem(text); Marshal.FreeCoTaskMem(strret); }
                }
                return result;
            }
            finally
            {
                foreach (object obj in new object[] { folder,view,browser,dispatch,windows })
                    if (obj != null && Marshal.IsComObject(obj)) Marshal.ReleaseComObject(obj);
            }
        }
    }
}
