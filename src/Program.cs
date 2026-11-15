using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Windows;

namespace AsterDock
{
    internal static class Program
    {
        internal const string MutexName = "Local\\AsterDock.Desktop.Singleton";
        [STAThread]
        public static int Main(string[] args)
        {
            Trace("Start, argument count=" + args.Length);
            if (args.Length > 0 && args[0] == "--self-test") return Tests.Run();
            if (args.Length > 0 && args[0] == "--verify-recovery") return Tests.VerifyRecovery();
            if (args.Length > 1 && args[0] == "--guard")
            {
                try { Process.GetProcessById(int.Parse(args[1])).WaitForExit(); } catch (ArgumentException) { }
                using (Mutex recovery = new Mutex(false, MutexName))
                {
                    bool owned = false;
                    try { try { owned = recovery.WaitOne(5000); } catch (AbandonedMutexException) { owned = true; }
                        if (owned) ShellRecovery.RecoverAll(); }
                    finally { if (owned) recovery.ReleaseMutex(); }
                }
                return 0;
            }
            using (Mutex mutex = new Mutex(false, MutexName))
            {
                bool owned;
                try { owned = mutex.WaitOne(0); } catch (AbandonedMutexException) { owned = true; }
                if (!owned) {
                    if (args.Length > 0 && (args[0] == "--recycle-test" || args[0] == "--recycle-data-test" || args[0] == "--groups-test" || args[0] == "--window-test" || args[0] == "--glass-preview" || args[0] == "--recovery-fixture")) return 2;
                    MessageBox.Show("请先退出正在运行的稳定版或试验版 Dock，再启动分组试验版。两个版本不能同时接管桌面。", "Light Dock 分组试验版");
                    return 2;

                }
                try
                {
                    ShellRecovery.RecoverAll();
                    if (args.Length > 0 && args[0] == "--repair-desktop") {
                        foreach(IntPtr list in Native.DesktopLists()) { WindowRegions.Apply(list,null); Native.ShowWindow(list,5); }
                        return 0;
                    }
                    ProcessStartInfo guard = new ProcessStartInfo(Process.GetCurrentProcess().MainModule.FileName, "--guard " + Process.GetCurrentProcess().Id);
                    guard.UseShellExecute = false; guard.CreateNoWindow = true; guard.WindowStyle = ProcessWindowStyle.Hidden;
                    Process.Start(guard);
                    if (args.Length > 0 && args[0] == "--recycle-refresh-test") return RecycleRefreshTests.Run();
                    if (args.Length > 0 && args[0] == "--projection-test") return ProjectionTests.Run();
                    if (args.Length > 0 && args[0] == "--groups-test") return GroupTests.Run();
                    if (args.Length > 0 && args[0] == "--recycle-data-test") return RecycleTests.Run();
                    if (args.Length > 0 && args[0] == "--recycle-test") return Tests.RunWindowTest(true);
                    if (args.Length > 0 && args[0] == "--window-test") return Tests.RunWindowTest();
                    if (args.Length > 0 && args[0] == "--recovery-fixture") return Tests.RunRecoveryFixture();
                    if (args.Length > 0 && args[0] == "--glass-preview") return Tests.RunGlassPreview();
                    Trace("Creating application");
                    Application app = new Application(); app.ShutdownMode = ShutdownMode.OnMainWindowClose;
                    app.DispatcherUnhandledException += delegate(object sender, System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e) {
                        MessageBox.Show("Light Dock 遇到错误，将退出并恢复桌面图标。\n" + e.Exception.Message, "Light Dock");
                        e.Handled = true; app.Shutdown(1);
                    };
                    DockWindow window = new DockWindow(Settings.Load());
                    Trace("Window constructed");
                    EventWaitHandle openSettings = new EventWaitHandle(false, EventResetMode.AutoReset, "Local\\AsterDock.OpenSettings");
                    System.Windows.Threading.DispatcherTimer signals = new System.Windows.Threading.DispatcherTimer();
                    signals.Interval = TimeSpan.FromMilliseconds(300);
                    signals.Tick += delegate { if (openSettings.WaitOne(0)) window.OpenSettings(); }; signals.Start();
                    if (args.Length > 0 && args[0] == "--settings") window.Loaded += delegate { window.OpenSettings(); };
                    app.Exit += delegate { window.Cleanup(); };
                    int result = app.Run(window); signals.Stop(); openSettings.Dispose(); Trace("Application exited: " + result); return result;
                }
                catch (Exception ex) { ShellRecovery.RecoverAll(); MessageBox.Show(ex.ToString(), "Light Dock 启动失败"); return 1; }
                finally { mutex.ReleaseMutex(); }
            }
        }
        private static void Trace(string message)
        {
            try { File.AppendAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "startup.log"), DateTime.Now.ToString("O") + " " + message + Environment.NewLine); } catch (IOException) { } catch (UnauthorizedAccessException) { }
        }
    }
}
