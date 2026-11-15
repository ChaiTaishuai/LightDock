using System;
using System.IO;
using System.Xml.Serialization;
using System.Windows;

namespace AsterDock
{
    internal static class Tests
    {
        private static int assertions;
        private static void Check(bool ok, string label) { assertions++; if (!ok) throw new Exception(label); }
        // The external runner kills only this test process after its ready marker.
        // A real guardian process must recover the real Shell, then --verify-recovery
        // compares it with this baseline. No user settings or desktop files are written.
        internal static int RunRecoveryFixture()
        {
            string root=AppDomain.CurrentDomain.BaseDirectory;
            var baseline=new System.Collections.Generic.List<ShellWindowRecord>();
            foreach(IntPtr bar in TaskbarVisibility.Windows()) baseline.Add(ShellRecovery.Capture(bar));
            foreach(IntPtr list in Native.DesktopLists()) { var r=ShellRecovery.Capture(list); r.OriginalRegion=WindowRegions.Read(list); baseline.Add(r); }
            ShellRecovery.Write(Path.Combine(root,"shell-crash-baseline.xml"),baseline);
            string ready=Path.Combine(root,"shell-crash-ready.txt"); if(File.Exists(ready)) File.Delete(ready);
            var entry=ShellDesktop.Read().Find(delegate(DesktopEntry e) { return File.Exists(e.Path) && e.Path.EndsWith(".lnk",StringComparison.OrdinalIgnoreCase); });
            if(entry==null) throw new InvalidOperationException("No desktop shortcut for recovery test");
            Application app=new Application(); Settings config=Settings.Defaults(); config.Items.Clear(); config.Items.Add(new DockItem("Recovery test",entry.Path));
            DockWindow window=new DockWindow(config);
            var timer=new System.Windows.Threading.DispatcherTimer { Interval=TimeSpan.FromMilliseconds(200) };
            int ticks=0;
            timer.Tick+=delegate {
                if(++ticks>150) { timer.Stop(); window.Close(); app.Shutdown(1); return; }
                if(window.DesktopItems.Busy || window.DesktopItems.HiddenCount!=1) return;
                window.SetState(DockState.Hidden);
                bool hidden=true; foreach(IntPtr bar in TaskbarVisibility.Windows()) hidden &= !Native.IsWindowVisible(bar);
                if(!hidden) return;
                File.WriteAllText(ready,System.Diagnostics.Process.GetCurrentProcess().Id.ToString()); timer.Stop();
            };
            window.Loaded+=delegate { timer.Start(); };
            return app.Run(window);
        }
        internal static int VerifyRecovery()
        {
            string root=AppDomain.CurrentDomain.BaseDirectory;
            try
            {
                var baseline=ShellRecovery.Read(Path.Combine(root,"shell-crash-baseline.xml"));
                Check(baseline.Count>0,"Recovery baseline exists");
                var current=TaskbarVisibility.Windows(); current.AddRange(Native.DesktopLists());
                foreach(var record in baseline)
                {
                    Check(ShellRecovery.Matches(record,current),"Same Shell window survives forced Dock termination");
                    IntPtr hwnd=new IntPtr(record.Handle);
                    Check(Native.IsWindowVisible(hwnd)==record.Visible,"Guardian restored original Shell visibility");
                    if(record.ClassName=="SysListView32") Check(WindowRegions.Equal(record.OriginalRegion,WindowRegions.Read(hwnd)),"Guardian restored original per-icon desktop region");
                }
                Check(!File.Exists(Path.Combine(Settings.Folder,"taskbar.recovery.xml")) && !File.Exists(Path.Combine(Settings.Folder,"desktop-items.recovery.xml")) && !File.Exists(Path.Combine(Settings.Folder,"visibility.recovery")),"Recovery journals cleared after successful restoration");
                File.WriteAllText(Path.Combine(root,"recovery-test-results.txt"),"PASS: "+assertions+" forced-exit recovery assertions\r\n"+DateTime.Now.ToString("O")); return 0;
            }
            catch(Exception ex) { File.WriteAllText(Path.Combine(root,"recovery-test-results.txt"),"FAIL: "+ex); return 1; }
        }
        // Manual compositor QA over a synthetic, high contrast background. No user settings saved.
        internal static int RunGlassPreview()
        {
            System.Windows.Application app = new System.Windows.Application();
            app.ShutdownMode = System.Windows.ShutdownMode.OnExplicitShutdown;
            System.Windows.Window background = new System.Windows.Window { Title = "Aster Dock · Glass backdrop", Width = 850, Height = 400, Left = 200, Top = 200, WindowStyle = System.Windows.WindowStyle.None };
            System.Windows.Controls.Grid grid = new System.Windows.Controls.Grid();
            for (int i = 0; i < 20; i++)
            {
                grid.ColumnDefinitions.Add(new System.Windows.Controls.ColumnDefinition());
                System.Windows.Controls.Border band = new System.Windows.Controls.Border { Background = new System.Windows.Media.SolidColorBrush(i % 2 == 0 ? System.Windows.Media.Color.FromRgb(35, 144, 160) : System.Windows.Media.Color.FromRgb(238, 170, 111)) };
                System.Windows.Controls.Grid.SetColumn(band, i); grid.Children.Add(band);
            }
            background.Content = grid; background.Show();
            Settings s = Settings.Defaults(); s.Movable = true; s.X = 300; s.Y = 320;
            // Keep the exact production tool-window style. Capture the larger backdrop
            // window to include the compositor outside the Dock's own screenshot bounds.
            DockWindow window = new DockWindow(s);
            System.Windows.Threading.DispatcherTimer timer = new System.Windows.Threading.DispatcherTimer();
            timer.Interval = TimeSpan.FromMinutes(5); timer.Tick += delegate { timer.Stop(); window.Close(); };
            window.Closed += delegate { timer.Stop(); background.Close(); app.Shutdown(); };
            window.Loaded += delegate { timer.Start(); };
            window.Show(); return app.Run();
        }
        internal static int RunWindowTest()
        {
            string report = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "window-test-results.txt");
            System.Windows.Application app = new System.Windows.Application();
            Settings s = Settings.Defaults();
            DockWindow window = new DockWindow(s);
            // Match production's tool-window style in the integration test.
            window.ShowInTaskbar = false;
            System.Windows.Threading.DispatcherTimer timer = new System.Windows.Threading.DispatcherTimer();
            int phase = 0; double width = 0; int result = 0;
            SettingsWindow panel = null;
            string diagnostics = DateTime.Now.ToString("O") + "\r\n";
            foreach (DisplayArea area in Displays.All()) diagnostics += area.Device + " " + area.BoundsPx.Width + "x" + area.BoundsPx.Height + " scale=" + area.Scale + " work=" + area.WorkPx + "\r\n";
            System.Collections.Generic.Dictionary<IntPtr, bool> before = new System.Collections.Generic.Dictionary<IntPtr, bool>();
            foreach (IntPtr list in Native.DesktopLists()) before[list] = Native.IsWindowVisible(list);
            System.Collections.Generic.Dictionary<IntPtr,bool> taskbars = new System.Collections.Generic.Dictionary<IntPtr,bool>();
            foreach(IntPtr bar in TaskbarVisibility.Windows()) taskbars[bar]=Native.IsWindowVisible(bar);
            System.Collections.Generic.Dictionary<IntPtr,byte[]> regions = new System.Collections.Generic.Dictionary<IntPtr,byte[]>();
            foreach(IntPtr list in Native.DesktopLists()) regions[list]=WindowRegions.Read(list);
            DesktopEntry borrowed=null;
            FileAttributes borrowedAttributes=0;
            int waitTicks=0;
            timer.Interval = TimeSpan.FromMilliseconds(400);
            timer.Tick += delegate {
                try
                {
                    if (phase == 0)
                    {
                        Check(window.IsVisible && window.ActualWidth > 100 && window.ActualHeight > 50, "Native window visible with nonzero geometry");
                        Check((Native.GetWindowLongPtr(window.Handle, -20).ToInt64() & 8) == 0, "Dock is not a topmost window");
                        Check(Native.GetWindowLongPtr(window.Handle, -8) != IntPtr.Zero, "Dock is owned at desktop level");
                        Check(Native.HasSystemCornerConfiguration(window.Handle), "System corners enabled without conflicting custom region or alpha layering");
                        Check(window.DesktopGestureAvailable, "Desktop observer installed");
                        Check(Displays.IsPerMonitorV2(window.Handle), "Dock HWND is Per-Monitor V2");
                        Check(Math.Abs(System.Windows.Media.VisualTreeHelper.GetDpi(window).DpiScaleX - Displays.WindowScale(window.Handle)) < .001, "WPF transform matches HWND DPI");
                        Rect native = Displays.WindowRect(window.Handle);
                        DisplayArea monitor = Displays.Primary;
                        Check(Math.Abs(native.Top-monitor.WorkPx.Top-8*monitor.Scale) <= 1 && Math.Abs(native.Left+(native.Width/2)-(monitor.WorkPx.Left+monitor.WorkPx.Width/2)) <= 2, "Native top-center alignment in physical pixels");
                        width = window.ActualWidth;
                        System.Windows.Media.Imaging.RenderTargetBitmap bitmap = new System.Windows.Media.Imaging.RenderTargetBitmap((int)window.ActualWidth, (int)window.ActualHeight, 96, 96, System.Windows.Media.PixelFormats.Pbgra32);
                        bitmap.Render(window);
                        System.Windows.Media.Imaging.PngBitmapEncoder encoder = new System.Windows.Media.Imaging.PngBitmapEncoder(); encoder.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(bitmap));
                        using (FileStream file = File.Create(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "dock-preview.png"))) encoder.Save(file);
                        window.ToggleHidden();
                    }
                    else if (phase == 1)
                    {
                        Check(window.CurrentState == DockState.Hidden && !window.IsVisible && !Native.IsWindowVisible(window.Handle), "Hidden state removes the whole Dock window");
                        foreach (IntPtr list in Native.DesktopLists()) Check(!Native.IsWindowVisible(list), "Desktop list hidden");
                        foreach(IntPtr bar in TaskbarVisibility.Windows()) Check(!Native.IsWindowVisible(bar),"Taskbar hidden with Dock");
                        window.TogglePeek();
                    }
                    else if (phase == 2)
                    {
                        Check(window.CurrentState == DockState.Peek && window.ActualWidth > 100, "Peek expands without restoring desktop icons");
                        foreach (IntPtr list in Native.DesktopLists()) Check(!Native.IsWindowVisible(list), "Peek keeps icons hidden");
                        foreach(IntPtr bar in TaskbarVisibility.Windows()) Check(!Native.IsWindowVisible(bar),"Peek keeps taskbar hidden");
                        window.ToggleHidden();
                    }
                    else if (phase == 3)
                    {
                        Check(window.CurrentState == DockState.Visible, "Double click transition restores normal state");
                        foreach (System.Collections.Generic.KeyValuePair<IntPtr, bool> pair in before) Check(Native.IsWindowVisible(pair.Key) == pair.Value, "Original icon visibility restored");
                        foreach(var pair in taskbars) Check(Native.IsWindowVisible(pair.Key)==pair.Value,"Original taskbar visibility restored");
                        s.Vertical = true;
                        // Rebuild without saving a test configuration to the user's settings file.
                        window.SetState(DockState.Visible);
                    }
                    else if (phase == 4)
                    {
                        Check(window.ActualHeight > window.ActualWidth, "Vertical window geometry");
                        Check(Native.HasSystemCornerConfiguration(window.Handle), "System corner configuration survives vertical resize");
                        s.Movable = true; s.HasRelativePosition = true; s.PositionMonitor = "DISCONNECTED-MONITOR"; s.PositionX = 1; s.PositionY = 1;
                        window.SetState(DockState.Visible);
                    }
                    else if (phase == 5)
                    {
                        Rect rect = Displays.WindowRect(window.Handle); Rect work = Displays.Primary.WorkPx;
                        Check(Math.Abs(rect.Right-work.Right) <= 2 && Math.Abs(rect.Bottom-work.Bottom) <= 2, "Missing monitor falls back to primary with retained relative anchor");
                        panel = new SettingsWindow(window, "Test"); panel.Show();
                    }
                    else if (phase == 6)
                    {
                        IntPtr panelHandle = new System.Windows.Interop.WindowInteropHelper(panel).Handle;
                        Check(Displays.IsPerMonitorV2(panelHandle), "Settings HWND is Per-Monitor V2");
                        Rect rect = Displays.WindowRect(panelHandle); Rect work = Displays.ForWindow(panelHandle).WorkPx;
                        // Standard resize borders may extend a few pixels beyond the visible frame.
                        work.Inflate(12*Displays.WindowScale(panelHandle),12*Displays.WindowScale(panelHandle));
                        Check(work.Contains(rect), "Settings window fits current work area");
                        panel.Close(); panel = null;
                        borrowed=ShellDesktop.Read().Find(delegate(DesktopEntry entry) { return File.Exists(entry.Path) && entry.Path.EndsWith(".lnk",StringComparison.OrdinalIgnoreCase); });
                        Check(borrowed!=null,"Existing desktop shortcut available for reversible projection test");
                        borrowedAttributes=File.GetAttributes(borrowed.Path);
                        s.Items.Clear(); s.Items.Add(new DockItem("Projection test",borrowed.Path));
                        window.DesktopItems.Update(s.Items);
                    }
                    else if(phase==7)
                    {
                        if(window.DesktopItems.Busy && ++waitTicks<25) return;
                        Check(window.DesktopItems.LastError==null,"Desktop projection completed: "+window.DesktopItems.LastError);
                        Check(window.DesktopItems.HiddenCount==1,"Only the added desktop shortcut is projected into Dock");
                        DesktopEntry after=ShellDesktop.Read().Find(delegate(DesktopEntry entry) { return entry.Path==borrowed.Path; });
                        Check(after!=null && after.Position==borrowed.Position,"Collected shortcut retains exact Shell position and identity");
                        Check(File.Exists(borrowed.Path) && File.GetAttributes(borrowed.Path)==borrowedAttributes,"Collected shortcut file and attributes unchanged");
                        foreach(IntPtr list in Native.DesktopLists())
                        {
                            WindowRegions.Rect hole=WindowRegions.ItemBounds(list,new System.Collections.Generic.List<int>{after.Index})[0];
                            WindowRegions.Rect rect; WindowRegions.GetWindowRect(list,out rect);
                            int x=rect.Left+(hole.Left+hole.Right)/2,y=rect.Top+(hole.Top+hole.Bottom)/2;
                            Check(!WindowRegions.ContainsScreenPoint(list,x,y),"Collected icon excluded from desktop paint and pointer hit region");
                            Check(DesktopGesture.IsEmptyDesktop(new DesktopGesture.Point{X=x,Y=y}),"Collected icon's old position behaves as empty desktop");
                            var others=ShellDesktop.Read().FindAll(delegate(DesktopEntry entry) { return entry.Path!=borrowed.Path; });
                            var indices=new System.Collections.Generic.List<int>(); foreach(var entry in others) indices.Add(entry.Index);
                            IntPtr baseline=WindowRegions.FromBytes(regions[list]);
                            try
                            {
                                bool intact=true;
                                foreach(var other in WindowRegions.ItemBounds(list,indices))
                                {
                                    int cx=(other.Left+other.Right)/2,cy=(other.Top+other.Bottom)/2;
                                    if(baseline==IntPtr.Zero || WindowRegions.PtInRegion(baseline,cx,cy))
                                        intact &= WindowRegions.ContainsScreenPoint(list,rect.Left+cx,rect.Top+cy);
                                }
                                Check(intact,"Uncollected desktop icon centers retain their original visibility");
                            }
                            finally { if(baseline!=IntPtr.Zero) WindowRegions.DeleteObject(baseline); }
                        }
                        s.Items.Clear(); window.DesktopItems.Update(s.Items); waitTicks=0;
                    }
                    else if(phase==8)
                    {
                        if(window.DesktopItems.Busy && ++waitTicks<25) return;
                        Check(window.DesktopItems.LastError==null && window.DesktopItems.HiddenCount==0,"Removing item restores desktop projection");
                        foreach(var pair in regions) Check(WindowRegions.Equal(WindowRegions.Read(pair.Key),pair.Value),"Original desktop region restored after removing item");
                        s.Items.Add(new DockItem("Projection test",borrowed.Path)); window.DesktopItems.Update(s.Items); waitTicks=0;
                    }
                    else if(phase==9)
                    {
                        if(window.DesktopItems.Busy && ++waitTicks<25) return;
                        Check(window.DesktopItems.HiddenCount==1,"Recollect before exit");
                        window.SetState(DockState.Hidden);
                    }
                    else
                    {
                        window.Cleanup();
                        foreach(var pair in regions) Check(WindowRegions.Equal(WindowRegions.Read(pair.Key),pair.Value),"Exit restores original desktop region");
                        foreach(var pair in taskbars) Check(Native.IsWindowVisible(pair.Key)==pair.Value,"Exit while hidden restores taskbar");
                        timer.Stop(); window.Close();
                        File.WriteAllText(report, "PASS: " + assertions + " window assertions\r\n" + diagnostics + "Desktop list windows tested: " + before.Count + "\r\nTaskbars tested: " + taskbars.Count + "\r\nNative blur request accepted: " + window.GlassAvailable); app.Shutdown();
                    }
                    phase++;
                }
                catch (Exception ex) { result = 1; timer.Stop(); if (panel != null) panel.Close(); window.Cleanup(); window.Close(); File.WriteAllText(report, "FAIL: " + diagnostics + ex); app.Shutdown(1); }
            };
            window.Loaded += delegate { timer.Start(); };
            app.Run(window);
            return result;
        }
        internal static int Run()
        {
            string report = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "test-results.txt");
            try
            {
                TestDisplayGeometry();
                var entries=ShellDesktop.Read();
                Check(entries.Count>0,"Shell desktop identities readable without file modification");
                var paths=DesktopIconProjection.Paths(new DockItem[] {new DockItem("A",@"C:\Desktop\A.lnk"),new DockItem("A",@"c:\desktop\a.lnk"),new DockItem("A",@"C:\Other\A.lnk"),new DockItem("App","explorer.exe")});
                Check(paths.Count==2,"Projection matches exact normalized paths, not duplicate labels or executable names");
                DesktopClickPair pair = new DesktopClickPair();
                Check(!pair.Click(10, 10, 100, true, 500, 4, 4) && pair.Click(12, 11, 200, true, 500, 4, 4), "Empty desktop double click");
                Check(!pair.Click(10, 10, 300, true, 500, 4, 4), "Triple click does not toggle twice");
                pair.Click(10, 10, 350, false, 500, 4, 4);
                Check(!pair.Click(10, 10, 400, true, 500, 4, 4), "Icon/application click resets desktop gesture");
                Check(!pair.Click(10, 10, 1000, true, 500, 4, 4), "Slow clicks ignored");
                Check(!pair.Click(30, 10, 1100, true, 500, 4, 4), "Spatially separated clicks ignored");
                System.Windows.Media.Imaging.BitmapSource explorerIcon = Native.Icon("explorer.exe") as System.Windows.Media.Imaging.BitmapSource;
                Check(explorerIcon != null && explorerIcon.PixelWidth >= 128, "Explorer high resolution shell icon");
                string shortcutPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "icon-test-" + Guid.NewGuid().ToString("N") + ".lnk");
                object shell = null, shortcut = null;
                try
                {
                    shell = Activator.CreateInstance(Type.GetTypeFromProgID("WScript.Shell"));
                    shortcut = shell.GetType().InvokeMember("CreateShortcut", System.Reflection.BindingFlags.InvokeMethod, null, shell, new object[] { shortcutPath });
                    shortcut.GetType().InvokeMember("TargetPath", System.Reflection.BindingFlags.SetProperty, null, shortcut, new object[] { Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "explorer.exe") });
                    shortcut.GetType().InvokeMember("Save", System.Reflection.BindingFlags.InvokeMethod, null, shortcut, null);
                    System.Windows.Media.Imaging.BitmapSource linkIcon = Native.Icon(shortcutPath) as System.Windows.Media.Imaging.BitmapSource;
                    Check(linkIcon != null && SamePixels(explorerIcon, linkIcon), "Shortcut icon matches target without arrow overlay");
                }
                finally
                {
                    if (shortcut != null) System.Runtime.InteropServices.Marshal.ReleaseComObject(shortcut);
                    if (shell != null) System.Runtime.InteropServices.Marshal.ReleaseComObject(shell);
                    if (File.Exists(shortcutPath)) File.Delete(shortcutPath);
                }
                Check(Layout.Magnify(0, 100, 1.6) == 1.6, "Center magnifies fully");
                Check(Layout.Magnify(100, 100, 1.6) == 1, "Outside radius stays normal");
                Check(Layout.Magnify(-50, 100, 1.6) == Layout.Magnify(50, 100, 1.6), "Falloff symmetric");
                Check(Layout.Magnify(25, 100, 1.6) > Layout.Magnify(50, 100, 1.6), "Smooth distance falloff");
                Check(Layout.KeepVisible(-2000, 0, 1920, 500) == 0, "Disconnected monitor clamps left");
                Check(Layout.KeepVisible(3000, 0, 1920, 500) == 1420, "Clamps right");
                Check(Layout.KeepVisible(-1000, -1920, 1920, 500) == -1000, "Negative monitor position retained");
                Check(Layout.KeepVisible(50, 0, 400, 600) == 0, "Oversized viewport remains accessible");
                Settings s = Settings.Defaults(); s.Radius = double.NaN; s.BackgroundOpacity = -10; s.IconSize = 100000; s.Magnification = double.PositiveInfinity;
                s.Items.Add(null); s.Items.Add(new DockItem("bad", "")); s.Normalize();
                Check(s.Radius == 24 && s.BackgroundOpacity == .05 && s.IconSize == 72 && s.Magnification == 1.55, "Invalid config bounded");
                Check(s.Items.Count == 5, "Invalid entries removed");
                s.Movable = true; s.Vertical = true; s.X = -1000; s.Y = 100;
                s.HasRelativePosition = true; s.PositionMonitor = "DISPLAY2"; s.PositionX = .75; s.PositionY = .25;
                XmlSerializer serializer = new XmlSerializer(typeof(Settings));
                using (MemoryStream stream = new MemoryStream())
                {
                    serializer.Serialize(stream, s); stream.Position = 0; Settings roundtrip = (Settings)serializer.Deserialize(stream);
                    Check(roundtrip.X == -1000 && roundtrip.Vertical && roundtrip.Movable && roundtrip.Items[0].Target == "explorer.exe", "Settings roundtrip");
                    Check(roundtrip.HasRelativePosition && roundtrip.PositionMonitor == "DISPLAY2" && roundtrip.PositionX == .75 && roundtrip.PositionY == .25, "Monitor anchor persists through XML");
                }
                // Instantiate and measure real WPF views without hiding any desktop icons.
                Settings clean = Settings.Defaults();
                DockWindow window = new DockWindow(clean);
                System.Windows.FrameworkElement dockContent = (System.Windows.FrameworkElement)window.Content;
                dockContent.Measure(new System.Windows.Size(1920,1080));
                Check(dockContent.DesiredSize.Width > 100 && dockContent.DesiredSize.Height > 50, "Dock measures");
                SettingsWindow panel = new SettingsWindow(window, "Ctrl + Alt + Space");
                System.Windows.FrameworkElement settingsContent = (System.Windows.FrameworkElement)panel.Content;
                settingsContent.Measure(new System.Windows.Size(480,720));
                Check(settingsContent.DesiredSize.Width > 0, "Settings view builds");
                window.Cleanup(); panel.Close(); window.Close();
                TestMeasuredLayouts();
                File.WriteAllText(report, "PASS: " + assertions + " assertions\r\n" + DateTime.Now.ToString("O")); return 0;
            }
            catch (Exception e) { File.WriteAllText(report, "FAIL: " + e); return 1; }
        }
        private static void TestDisplayGeometry()
        {
            // Resolution and scale are independent. Exercise the full matrix, including
            // a high scale on a small display and negative desktop origins.
            Size[] resolutions = { new Size(1920,1080), new Size(2560,1440), new Size(3840,2160) };
            double[] scales = { 1, 1.25, 1.5, 1.75, 2, 2.5, 3 };
            foreach (Size resolution in resolutions)
            foreach (double scale in scales)
            foreach (bool vertical in new bool[] { false, true })
            {
                Rect work = new Rect(-resolution.Width, -200, resolution.Width, resolution.Height-48*scale);
                Size limit = DisplayGeometry.Viewport(work,scale);
                // Max-size / max-magnification library must scroll inside the work area.
                double extent = 72*2+22;
                Size content = new Size(Math.Min(limit.Width,vertical ? extent : extent*100)+24, Math.Min(limit.Height,vertical ? extent*100 : extent)+20);
                Size pixels = new Size(content.Width*scale, content.Height*scale);
                Point centered = DisplayGeometry.Place(work,pixels,false,.5,0,scale);
                Check(work.Contains(new Rect(centered,pixels)), "Large library fits " + resolution + " @ " + scale + " vertical=" + vertical);
                Check(Math.Abs(centered.X+pixels.Width/2-(work.Left+work.Width/2)) < .001 && Math.Abs(centered.Y-work.Top-8*scale) < .001, "Center and top gap independent of global origin");
                Point anchored = DisplayGeometry.Place(work,pixels,true,.75,.25,scale);
                Point roundtrip = DisplayGeometry.Fractions(work,new Rect(anchored,pixels));
                Check(Math.Abs(roundtrip.X-.75) < .001 && Math.Abs(roundtrip.Y-.25) < .001, "Relative position survives scale / resolution");
                Point cursor = new Point(-100, 350), grab = new Point(.23,.61);
                Point origin = DisplayGeometry.Drag(cursor,pixels,grab);
                Check(Math.Abs(origin.X+pixels.Width*grab.X-cursor.X) < .001 && Math.Abs(origin.Y+pixels.Height*grab.Y-cursor.Y) < .001, "Physical drag anchor survives DPI change");
            }
            Settings invalid = new Settings { PositionX = double.PositiveInfinity, PositionY = -5, PositionMonitor = null };
            invalid.Normalize(); Check(invalid.PositionX == .5 && invalid.PositionY == 0 && invalid.PositionMonitor == "", "Invalid monitor anchors normalized");
        }
        private static void TestMeasuredLayouts()
        {
            foreach (bool vertical in new bool[] { false, true })
            {
                Settings config = Settings.Defaults(); config.Vertical = vertical; config.IconSize = 72; config.Magnification = 2;
                config.Items.Clear();
                for (int i = 0; i < 100; i++) config.Items.Add(new DockItem("Test", "explorer.exe"));
                DockWindow window = new DockWindow(config);
                try
                {
                    System.Windows.Controls.Border plate = (System.Windows.Controls.Border)window.Content;
                    System.Windows.Controls.ScrollViewer viewport = (System.Windows.Controls.ScrollViewer)((System.Windows.Controls.StackPanel)plate.Child).Children[0];
                    foreach (Size resolution in new Size[] { new Size(1920,1080),new Size(2560,1440),new Size(3840,2160) })
                    foreach (double scale in new double[] { 1,1.25,1.5,1.75,2,2.5,3 })
                    {
                        Rect work = new Rect(0,0,resolution.Width,resolution.Height-48*scale);
                        Size limit = DisplayGeometry.Viewport(work,scale);
                        viewport.MaxWidth = limit.Width; viewport.MaxHeight = limit.Height;
                        plate.Measure(new Size(work.Width/scale,work.Height/scale));
                        Check(plate.DesiredSize.Width*scale <= work.Width && plate.DesiredSize.Height*scale <= work.Height,
                            "WPF measured 100-icon library fits " + resolution + " @ " + scale + " vertical=" + vertical);
                    }
                }
                finally { window.Cleanup(); window.Close(); }
            }
        }
        private static bool SamePixels(System.Windows.Media.Imaging.BitmapSource a, System.Windows.Media.Imaging.BitmapSource b)
        {
            if (a.PixelWidth != b.PixelWidth || a.PixelHeight != b.PixelHeight || a.Format != b.Format) return false;
            int stride = (a.PixelWidth * a.Format.BitsPerPixel + 7) / 8;
            byte[] x = new byte[stride*a.PixelHeight], y = new byte[x.Length];
            a.CopyPixels(x, stride, 0); b.CopyPixels(y, stride, 0);
            for (int i = 0; i < x.Length; i++) if (x[i] != y[i]) return false;
            return true;
        }
    }
}
