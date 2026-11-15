using System;
using System.Diagnostics;
using Microsoft.Win32;
namespace AsterDock
{
    internal static class StartupRegistration
    {
        private const string KeyPath=@"Software\Microsoft\Windows\CurrentVersion\Run";
        private const string Name="LightDock";
        internal static string Command { get { return "\""+Process.GetCurrentProcess().MainModule.FileName+"\" --autostart"; } }
        internal static bool Enabled {
            get { using(var key=Registry.CurrentUser.OpenSubKey(KeyPath,false)) return key!=null && string.Equals(key.GetValue(Name) as string,Command,StringComparison.OrdinalIgnoreCase); }
        }
        internal static void Set(bool enabled)
        {
            using(var key=Registry.CurrentUser.CreateSubKey(KeyPath)) {
                if(key==null) throw new InvalidOperationException(UiText.T("无法打开当前用户的启动设置。"));
                if(enabled) key.SetValue(Name,Command,RegistryValueKind.String);
                else if(string.Equals(key.GetValue(Name) as string,Command,StringComparison.OrdinalIgnoreCase)) key.DeleteValue(Name,false);
            }
        }
    }
}
