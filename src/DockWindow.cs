using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using Forms = System.Windows.Forms;

namespace AsterDock
{
    internal enum DockState { Visible, Hidden, Peek }

    internal sealed partial class DockWindow : Window
    {
        internal readonly Settings Config;
        private readonly Border plate = new Border();
        private readonly StackPanel row = new StackPanel();
        private readonly List<FrameworkElement> tiles = new List<FrameworkElement>();
        private readonly DesktopVisibility desktop = new DesktopVisibility();
        private readonly TaskbarVisibility taskbar = new TaskbarVisibility();
        private readonly DesktopIconProjection desktopItems;
        private int maintenanceTick;
        private string shellWarning;
        private DesktopGesture desktopGesture;
        private readonly DispatcherTimer maintenance = new DispatcherTimer();
        private readonly DispatcherTimer clickTimer = new DispatcherTimer();
        private Action pendingClick;
        private Forms.NotifyIcon tray;
        private IntPtr hwnd;
        private DockState state;
        private SettingsWindow settings;
        private Point? dragStart;
        private DockItem draggedItem;
        private bool itemDragging;
        private bool dragged, cleaned;
        internal string HotkeyLabel = "快捷键不可用，请使用托盘菜单";
        internal DockState CurrentState { get { return state; } }
        internal IntPtr Handle { get { return hwnd; } }
        internal bool GlassAvailable { get; private set; }
        internal bool DesktopGestureAvailable { get { return desktopGesture != null && desktopGesture.Available; } }
        internal DesktopIconProjection DesktopItems { get { return desktopItems; } }
        private Point dragGrab;
        private ScrollViewer viewport;
        private bool displayRefreshQueued, placing;
        private Point? hover;
        private readonly Dictionary<FrameworkElement, double> scales = new Dictionary<FrameworkElement, double>();

        internal DockWindow(Settings config)
        {
            Config = config; Config.Normalize();
            desktopItems = new DesktopIconProjection(Dispatcher);
            Title = "Light Dock · 分组试验版"; WindowStyle = WindowStyle.None; ResizeMode = ResizeMode.NoResize;
            // Use a DWM glass surface, not a WS_EX_LAYERED bitmap: layered WPF windows
            // can accept an accent policy while silently suppressing its backdrop blur.
            AllowsTransparency = false; Background = Brushes.Transparent; ShowInTaskbar = false;
            System.Windows.Shell.WindowChrome.SetWindowChrome(this, new System.Windows.Shell.WindowChrome {
                CaptionHeight = 0, ResizeBorderThickness = new Thickness(0),
                GlassFrameThickness = new Thickness(-1), CornerRadius = new CornerRadius(0), UseAeroCaptionButtons = false
            });
            ShowActivated = false; Topmost = false; SizeToContent = SizeToContent.WidthAndHeight;
            FontFamily = new FontFamily("Microsoft YaHei UI");
            UseLayoutRounding = true; SnapsToDevicePixels = true;
            plate.Padding = new Thickness(12, 10, 12, 10); plate.Margin = new Thickness(0);
            // A single system clip shapes both tint and backdrop; no independently rounded border.
            plate.BorderThickness = new Thickness(0);
            plate.Child = row; Content = plate;
            plate.SizeChanged += delegate { Dispatcher.BeginInvoke(DispatcherPriority.ContextIdle, new Action(ApplyGlass)); };
            AllowDrop = true;
            DragOver += delegate(object sender, DragEventArgs e) {
                bool files = e.Data.GetDataPresent(DataFormats.FileDrop);
                e.Effects = files ? DragDropEffects.Link : DragDropEffects.None;
                if (files) HighlightExternalDrop(e.GetPosition(this));
                e.Handled = true;
            };
            DragLeave += delegate(object sender, DragEventArgs e) { if (!Inside(this,e.GetPosition(this))) ClearDropHighlight(); };
            Drop += delegate(object sender, DragEventArgs e) {
                string group = DropGroup(e.GetPosition(this)); ClearDropHighlight();
                if (e.Data.GetDataPresent(DataFormats.FileDrop)) AddPaths((string[])e.Data.GetData(DataFormats.FileDrop), group == "" ? null : group);
                e.Handled = true;
            };
            PreviewMouseLeftButtonDown += OnMouseDown;
            PreviewMouseLeftButtonUp += OnMouseUp;
            PreviewMouseMove += OnMouseMove;
            LostMouseCapture += delegate { if (dragged && !cleaned) FinishDrag(); if (itemDragging) { itemDragging = false; draggedItem = null; dragStart = null; Cursor=null; ClearDropHighlight(); } };
            MouseLeave += delegate { hover = null; StartAnimation(); };
            PreviewKeyDown += delegate(object sender, KeyEventArgs e) { if(e.Key == Key.Escape && itemDragging) { itemDragging=false; draggedItem=null; dragStart=null; Cursor=null; ReleaseMouseCapture(); ClearDropHighlight(); e.Handled=true; return; } if (e.Key == Key.Escape && state == DockState.Peek) { SetState(DockState.Hidden); e.Handled = true; } };
            ContextMenu = BuildMenu();
            SourceInitialized += delegate {
                hwnd = new WindowInteropHelper(this).Handle;
                Native.SetWindowLongPtr(hwnd, -20, new IntPtr(Native.GetWindowLongPtr(hwnd, -20).ToInt64() | 0x80));
                HwndSource.FromHwnd(hwnd).AddHook(WindowMessage);
                HwndSource.FromHwnd(hwnd).CompositionTarget.BackgroundColor = Colors.Transparent;
                desktopGesture = new DesktopGesture(Dispatcher, ToggleHidden);
                if (Native.RegisterHotKey(hwnd, 1, 0x4003, 0x20)) HotkeyLabel = "Ctrl + Alt + Space";
                else if (Native.RegisterHotKey(hwnd, 1, 0x4003, 0x44)) HotkeyLabel = "Ctrl + Alt + D（备用快捷键）";
            };
            Loaded += delegate { Place(); Native.ShowWindowAsync(hwnd, 4); Native.PlaceAtDesktop(hwnd); UpdateDesktop(); desktopItems.Update(Config.Items); Dispatcher.BeginInvoke(DispatcherPriority.ContextIdle, new Action(ApplyGlass)); };
            SizeChanged += delegate { QueueDisplayRefresh(); };
            maintenance.Interval = TimeSpan.FromMilliseconds(250);
            maintenance.Tick += delegate {
                UpdateDesktop();
                if (++maintenanceTick % 4 == 0 && IsLoaded)
                {
                    if (state == DockState.Visible) Native.PlaceAtDesktop(hwnd);
                    desktopItems.Update(Config.Items);
                    if (desktopItems.LastError != null) WarnShell(desktopItems.LastError);
                }
            };
            maintenance.Start();
            clickTimer.Interval = TimeSpan.FromMilliseconds(Native.GetDoubleClickTime() + 20);
            clickTimer.Tick += delegate { clickTimer.Stop(); Action action = pendingClick; pendingClick = null; if (action != null) action(); };
            CreateTray(); Render();
        }

        private IntPtr WindowMessage(IntPtr w, int msg, IntPtr wp, IntPtr lp, ref bool handled)
        {
            if (msg == 0x312 && wp.ToInt32() == 1) { TogglePeek(); handled = true; }
            // WPF owns WM_DPICHANGED and its suggested rectangle. Reflow only after
            // the framework has updated its device transform; do not apply DPI twice.
            if (msg == 0x7E || msg == 0x1A || msg == 0x31E) QueueDisplayRefresh();
            return IntPtr.Zero;
        }
        protected override void OnDpiChanged(DpiScale oldDpi, DpiScale newDpi)
        { base.OnDpiChanged(oldDpi, newDpi); QueueDisplayRefresh(); }
        private void QueueDisplayRefresh()
        {
            if (cleaned || displayRefreshQueued) return;
            displayRefreshQueued = true;
            Dispatcher.BeginInvoke(DispatcherPriority.ContextIdle, new Action(delegate {
                displayRefreshQueued = false;
                if (cleaned) return;
                UpdateViewport(); UpdateLayout(); Place(); ApplyGlass();
            }));
        }
        private void CreateTray()
        {
            tray = new Forms.NotifyIcon(); tray.Text = "Light Dock · 双击打开设置";
            tray.Icon = System.Drawing.Icon.ExtractAssociatedIcon(Process.GetCurrentProcess().MainModule.FileName) ?? System.Drawing.SystemIcons.Application;
            Forms.ContextMenuStrip menu = new Forms.ContextMenuStrip();
            menu.Items.Add("显示 Dock", null, delegate { SetState(DockState.Visible); });
            menu.Items.Add("隐藏 Dock、桌面图标与任务栏", null, delegate { SetState(DockState.Hidden); });
            menu.Items.Add("临时呼出 / 收回", null, delegate { TogglePeek(); });
            menu.Items.Add("设置…", null, delegate { OpenSettings(); });
            menu.Items.Add("退出并恢复桌面", null, delegate { Close(); });
            tray.ContextMenuStrip = menu; tray.DoubleClick += delegate { OpenSettings(); }; tray.Visible = true;
        }
        private ContextMenu BuildMenu()
        {
            ContextMenu menu = new ContextMenu();
            AddMenu(menu, "添加应用或文件…", PickFiles);
            AddMenu(menu, "添加文件夹…", delegate { using (Forms.FolderBrowserDialog picker = new Forms.FolderBrowserDialog()) { if (picker.ShowDialog() == Forms.DialogResult.OK) AddPaths(new string[] { picker.SelectedPath }); } });
            AddMenu(menu, "导入桌面快捷方式", ImportDesktop);
            AddMenu(menu, "新建分组…", delegate { EditGroup(null); });
            menu.Items.Add(new Separator());
            AddMenu(menu, "设置外观与布局…", OpenSettings);
            AddMenu(menu, "回到顶部居中", delegate { Config.Movable = false; Config.HasRelativePosition = false; Config.X = Config.Y = double.NaN; Apply(); });
            AddMenu(menu, "隐藏 / 恢复", ToggleHidden);
            menu.Items.Add(new Separator());
            AddMenu(menu, "退出并恢复桌面", Close);
            return menu;
        }
        private static void AddMenu(ContextMenu menu, string text, Action action)
        { MenuItem item = new MenuItem { Header = text }; item.Click += delegate { action(); }; menu.Items.Add(item); }

        internal void Apply() { Config.Normalize(); Render(); Place(); ApplyGlass(); Save(); UpdateDesktop(); if(IsLoaded) desktopItems.Update(Config.Items); }
        private void UpdateDesktop()
        {
            try { desktop.SetHidden(Config.HideDesktopIcons || state != DockState.Visible); }
            catch(Exception ex) { WarnShell(ex.Message); }
            try { taskbar.SetHidden(state != DockState.Visible); }
            catch(Exception ex) { WarnShell(ex.Message); }
        }
        private void WarnShell(string message)
        {
            if(shellWarning==message) return; shellWarning=message;
            if(tray!=null) tray.ShowBalloonTip(6000,"Light Dock · 桌面同步未完成",message,Forms.ToolTipIcon.Warning);
        }
        private void ApplyGlass()
        {
            if (hwnd == IntPtr.Zero || plate.ActualWidth <= 0 || plate.ActualHeight <= 0) return;
            PresentationSource source = PresentationSource.FromVisual(this);
            if (source == null) return;
            Rect bounds = new Rect(plate.TranslatePoint(new Point(), this), new Size(plate.ActualWidth, plate.ActualHeight));
            plate.Clip = null;
            bool systemCorners = Native.UseSystemCorners(hwnd);
            HwndSource.FromHwnd(hwnd).CompositionTarget.BackgroundColor = Colors.Transparent;
            GlassAvailable = systemCorners && Native.SetGlass(hwnd, Config.BlurBackground);
            if (!systemCorners)
            {
                // No rectangular blur fallback on systems without native rounded corners.
                Native.SetGlass(hwnd, false);
                plate.CornerRadius = new CornerRadius(8);
                Native.ClipGlass(hwnd, bounds, 8, source.CompositionTarget.TransformToDevice);
            }
        }
        internal void Save()
        { try { Config.Save(); } catch (Exception ex) { MessageBox.Show("无法保存设置：" + ex.Message, "Light Dock"); } }

        private void Render()
        {
            hover = null; StopAnimation(); tiles.Clear(); scales.Clear(); row.Children.Clear();
            plate.CornerRadius = new CornerRadius(0);
            plate.Background = new LinearGradientBrush(
                Color.FromArgb((byte)(255 * Math.Min(1, Config.BackgroundOpacity * 1.15)), 250, 252, 255),
                Color.FromArgb((byte)(255 * Config.BackgroundOpacity * .85), 229, 237, 246), 90);
            row.Orientation = Orientation.Vertical;
            // A bounded scroll viewport keeps large libraries reachable on small displays.
            StackPanel items = new StackPanel { Orientation = Orientation.Horizontal };
            PrepareGroups(items);
            double extent = Config.IconSize * Config.Magnification + 22;
            foreach (DockItem item in Config.Items)
            {
                Border tile = new Border { Width = extent, Height = extent, Background = Brushes.Transparent, CornerRadius = new CornerRadius(12), ToolTip = item.Name + "\n" + item.Target, Cursor = Cursors.Hand, Tag = item };
                Grid face = new Grid { Width = Config.IconSize, Height = Config.IconSize };
                TextBlock placeholder = new TextBlock { Text = item.Target == "ms-settings:" ? "⚙" : "◇", FontSize = Config.IconSize * .8, Foreground = new SolidColorBrush(Color.FromRgb(49, 67, 89)), TextAlignment = TextAlignment.Center };
                Image iconImage = new Image { Stretch = Stretch.Uniform, SnapsToDevicePixels = true };
                RenderOptions.SetBitmapScalingMode(iconImage, BitmapScalingMode.HighQuality);
                face.Children.Add(placeholder); face.Children.Add(iconImage);
                LoadIcon(item.Target, iconImage, placeholder);
                face.HorizontalAlignment = HorizontalAlignment.Center; face.VerticalAlignment = VerticalAlignment.Center;
                face.RenderTransformOrigin = new Point(.5, .5); face.RenderTransform = new ScaleTransform(1, 1);
                tile.Child = face; tiles.Add(tile); scales[tile] = 1;
                ContextMenu itemMenu = new ContextMenu(); DockItem captured = item;
                AddMenu(itemMenu, "打开 " + item.Name, delegate { Launch(captured); });
                AddMenu(itemMenu, "向前移动", delegate { MoveItem(captured, -1); });
                AddMenu(itemMenu, "向后移动", delegate { MoveItem(captured, 1); });
                AddMenu(itemMenu, "从 Dock 移除并恢复桌面图标", delegate { Config.Items.Remove(captured); Apply(); });
                AddMenu(itemMenu, "新建分组…", delegate { EditGroup(null); });
                AddMenu(itemMenu, "Dock 设置…", OpenSettings);
                tile.ContextMenu = itemMenu; GroupPanel(item.GroupId).Children.Add(tile);
            }
            FinishGroups(items, extent);
        }

        private void LoadIcon(string target, Image image, TextBlock placeholder)
        {
            IconLoader.Load(target).ContinueWith(delegate(System.Threading.Tasks.Task<ImageSource> task) {
                if (Dispatcher.HasShutdownStarted) return;
                Dispatcher.BeginInvoke(new Action(delegate {
                    if (cleaned || task.Result == null) return;
                    image.Source = task.Result; placeholder.Visibility = Visibility.Collapsed;
                }));
            });
        }

        private DisplayArea TargetDisplay()
        {
            if (dragged) return Displays.ForWindow(hwnd);
            if (!Config.Movable) return Displays.Primary;
            if (Config.HasRelativePosition) return Displays.NamedOrPrimary(Config.PositionMonitor);
            if (!double.IsNaN(Config.X) && !double.IsNaN(Config.Y))
                return Displays.AtPoint(new Point(Config.X*Displays.LegacySystemScale, Config.Y*Displays.LegacySystemScale));
            return Displays.Primary;
        }
        private void UpdateViewport()
        {
            ResizeGroups(false);
        }

        private void Place()
        {
            if (!IsLoaded || placing || hwnd == IntPtr.Zero) return;
            placing = true;
            try
            {
                DisplayArea area = TargetDisplay();
                Size size = new Size(ActualWidth*area.Scale, ActualHeight*area.Scale);
                if (dragged) { Displays.Move(hwnd, DisplayGeometry.Drag(Displays.Cursor, Displays.WindowRect(hwnd).Size, dragGrab)); return; }
                // Migrate old system-DPI coordinates once. New saves use the available
                // space fraction, so resolution and taskbar changes preserve the anchor.
                if (Config.Movable && !Config.HasRelativePosition && !double.IsNaN(Config.X) && !double.IsNaN(Config.Y))
                {
                    Rect legacy = new Rect(new Point(Config.X*Displays.LegacySystemScale, Config.Y*Displays.LegacySystemScale), size);
                    RememberPosition(area, legacy);
                }
                Displays.Move(hwnd, DisplayGeometry.Place(area.WorkPx, size,
                    Config.Movable && Config.HasRelativePosition, Config.PositionX, Config.PositionY, area.Scale));
            }
            finally { placing = false; }
        }
        private void RememberPosition(DisplayArea area, Rect rect)
        {
            Point fractions = DisplayGeometry.Fractions(area.WorkPx, rect);
            Config.HasRelativePosition = true; Config.PositionMonitor = area.Device;
            Config.PositionX = fractions.X; Config.PositionY = fractions.Y;
            Config.X = Config.Y = double.NaN;
        }
        private void OnMouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ClickCount >= 2)
            {
                // Collapse a double-click to one launch; desktop gestures own visibility.
                dragStart = null; ReleaseMouseCapture(); e.Handled = true; return;
            }
            if (IsToggleSource(e.OriginalSource as DependencyObject)) return;
            dragStart = Displays.Cursor; dragged = false; draggedItem = null; itemDragging = false;
            Point localPoint = e.GetPosition(this);
            foreach (FrameworkElement tile in tiles)
            {
                Point local = TranslatePoint(localPoint, tile);
                if (local.X >= 0 && local.X < tile.ActualWidth && local.Y >= 0 && local.Y < tile.ActualHeight && DropGroup(e.GetPosition(this)) == (((DockItem)tile.Tag).GroupId ?? ""))
                { draggedItem = (DockItem)tile.Tag; break; }
            }
            Rect rect = Displays.WindowRect(hwnd);
            dragGrab = new Point((dragStart.Value.X-rect.Left)/Math.Max(1,rect.Width), (dragStart.Value.Y-rect.Top)/Math.Max(1,rect.Height));
            if (Config.Movable || draggedItem != null) CaptureMouse();
        }
        private void OnMouseMove(object sender, MouseEventArgs e)
        {
            hover = e.GetPosition(this); StartAnimation();
            if (!dragStart.HasValue || e.LeftButton != MouseButtonState.Pressed) return;
            Point p = Displays.Cursor; Vector delta = p - dragStart.Value;
            uint dpi = Displays.GetDpiForWindow(hwnd);
            if (Math.Abs(delta.X) < Displays.GetSystemMetricsForDpi(68,dpi) && Math.Abs(delta.Y) < Displays.GetSystemMetricsForDpi(69,dpi) && !dragged) return;
            if (draggedItem != null)
            {
                itemDragging = true; clickTimer.Stop(); pendingClick = null; Cursor = Cursors.Hand; UpdateGroupDrag(e.GetPosition(this)); e.Handled = true; return;
            }
            if (!Config.Movable) return;
            dragged = true; clickTimer.Stop(); pendingClick = null;
            Displays.Move(hwnd, DisplayGeometry.Drag(p, Displays.WindowRect(hwnd).Size, dragGrab));
            QueueDisplayRefresh();
        }
        private void OnMouseUp(object sender, MouseButtonEventArgs e)
        {
            if (itemDragging)
            {
                DockItem item = draggedItem; Point screen = Displays.Cursor;
                itemDragging = false; draggedItem = null; dragStart = null; Cursor = null; ReleaseMouseCapture();
                if (DropIntoGroup(item, e.GetPosition(this))) { e.Handled = true; return; }
                ClearDropHighlight();
                string parent = null;
                try { if (Path.IsPathRooted(item.Target)) parent = Path.GetDirectoryName(Path.GetFullPath(item.Target)); } catch (Exception) { }
                bool desktopItem = string.Equals(parent, Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), StringComparison.OrdinalIgnoreCase) || string.Equals(parent, Environment.GetFolderPath(Environment.SpecialFolder.CommonDesktopDirectory), StringComparison.OrdinalIgnoreCase);
                if (desktopItem && DesktopGesture.IsDesktopBackground(new DesktopGesture.Point { X = (int)screen.X, Y = (int)screen.Y })) { Config.Items.Remove(item); Apply(); }
                e.Handled = true; return;
            }
            ReleaseMouseCapture();
            if (!dragStart.HasValue) return;
            dragStart = null;
            if (dragged) { FinishDrag(); return; }
            if (state == DockState.Hidden) return;
            Point p = e.GetPosition(this);
            foreach (FrameworkElement tile in tiles)
            {
                Point local = TranslatePoint(p, tile);
                if (local.X >= 0 && local.X < tile.ActualWidth && local.Y >= 0 && local.Y < tile.ActualHeight && DropGroup(e.GetPosition(this)) == (((DockItem)tile.Tag).GroupId ?? ""))
                { DockItem item = (DockItem)tile.Tag; QueueClick(delegate { Launch(item); }); e.Handled = true; return; }
            }
        }
        private void FinishDrag()
        {
            RememberPosition(Displays.ForWindow(hwnd), Displays.WindowRect(hwnd));
            dragged = false; dragStart = null; QueueDisplayRefresh(); Save();
        }
        private void QueueClick(Action action) { clickTimer.Stop(); pendingClick = action; clickTimer.Start(); }

        private bool animating;
        private void StartAnimation() { if (!animating) { CompositionTarget.Rendering += Animate; animating = true; } }
        private void StopAnimation() { if (animating) { CompositionTarget.Rendering -= Animate; animating = false; } }
        private void Animate(object sender, EventArgs e)
        {
            bool settled = true;
            foreach (FrameworkElement tile in tiles)
            {
                Point center = tile.TranslatePoint(new Point(tile.ActualWidth / 2, tile.ActualHeight / 2), this);
                double target = hover.HasValue && Math.Abs(hover.Value.Y-center.Y)<tile.ActualHeight/2 ? Layout.Magnify(hover.Value.X-center.X, Config.IconSize * 2.5, Config.Magnification) : 1;
                double next = scales[tile] + (target - scales[tile]) * .24;
                if (Math.Abs(next - target) < .002) next = target; else settled = false;
                scales[tile] = next;
                ScaleTransform transform = (ScaleTransform)((Border)tile).Child.RenderTransform;
                transform.ScaleX = transform.ScaleY = next;
            }
            if (settled) StopAnimation();
        }
        internal void ToggleHidden() { SetState(state == DockState.Visible ? DockState.Hidden : DockState.Visible); }
        internal void TogglePeek() { SetState(state == DockState.Peek ? DockState.Hidden : state == DockState.Hidden ? DockState.Peek : DockState.Hidden); }
        internal void SetState(DockState value)
        {
            clickTimer.Stop(); pendingClick = null;
            state = value;
            if (state == DockState.Hidden) { ClearDropHighlight(); StopAnimation(); Hide(); }
            else { Render(); Show(); Place(); Dispatcher.BeginInvoke(DispatcherPriority.ContextIdle, new Action(ApplyGlass)); }
            UpdateDesktop();
            if (state == DockState.Visible) Native.PlaceAtDesktop(hwnd);
            if (state == DockState.Peek) { Native.DetachDesktopOwner(hwnd); Activate(); Focus(); }
        }
        private void Launch(DockItem item)
        {
            try { Process.Start(new ProcessStartInfo(item.Target) { UseShellExecute = true }); if (state == DockState.Peek) SetState(DockState.Hidden); }
            catch (Exception ex) { MessageBox.Show("无法打开「" + item.Name + "」\n" + ex.Message, "Light Dock"); }
        }
        private void MoveItem(DockItem item, int delta)
        { int index = Config.Items.IndexOf(item); int next = index + delta; if (next < 0 || next >= Config.Items.Count) return; Config.Items.RemoveAt(index); Config.Items.Insert(next, item); Apply(); }
        private void PickFiles()
        {
            Microsoft.Win32.OpenFileDialog picker = new Microsoft.Win32.OpenFileDialog { Title = "添加应用、快捷方式或文件", Multiselect = true, Filter = "所有文件|*.*", CheckFileExists = true };
            if (picker.ShowDialog(this) == true) AddPaths(picker.FileNames);
        }
        private void AddPaths(string[] paths) { AddPaths(paths, null); }
        private void AddPaths(string[] paths, string group)
        {
            foreach (string path in paths)
            {
                if (!File.Exists(path) && !Directory.Exists(path)) continue;
                if (Config.Items.Exists(delegate(DockItem i) { return string.Equals(i.Target, path, StringComparison.OrdinalIgnoreCase); })) continue;
                Config.AddGroupedItem(new DockItem(Path.GetFileNameWithoutExtension(path), path), group);
            }
            Apply();
        }
        private void ImportDesktop()
        {
            foreach (string folder in new string[] { Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), Environment.GetFolderPath(Environment.SpecialFolder.CommonDesktopDirectory) })
                if (Directory.Exists(folder)) AddPaths(Directory.GetFiles(folder, "*.lnk"));
        }
        internal void OpenSettings()
        {
            if (settings != null) { settings.Activate(); return; }
            settings = new SettingsWindow(this, HotkeyLabel); settings.Closed += delegate { settings = null; }; settings.Show(); settings.Activate();
        }
        internal void Cleanup()
        {
            if (cleaned) return; cleaned = true;
            ClearDropHighlight(); maintenance.Stop(); clickTimer.Stop(); StopAnimation();
            if (desktopGesture != null) desktopGesture.Dispose();
            Native.UnregisterHotKey(hwnd, 1);
            foreach(Action restore in new Action[] { taskbar.Dispose,desktopItems.Dispose,desktop.Dispose })
                try { restore(); } catch(Exception ex) { ShellRecovery.Log(ex); }
            if (tray != null) { tray.Visible = false; tray.Dispose(); }
        }
        protected override void OnClosed(EventArgs e) { Cleanup(); base.OnClosed(e); }
    }
}
