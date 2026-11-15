using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Interop;
using System.Windows.Threading;

namespace AsterDock
{
    internal sealed class SettingsWindow : Window
    {
        private readonly DockWindow dock;
        private readonly StackPanel body = new StackPanel();
        private bool fitQueued;
        private static SolidColorBrush Ink = new SolidColorBrush(Color.FromRgb(227, 235, 248));
        private static SolidColorBrush Muted = new SolidColorBrush(Color.FromRgb(149, 169, 195));
        internal SettingsWindow(DockWindow owner, string hotkey)
        {
            dock = owner;
            Title = "Light Dock · 设置"; Width = 480; Height = 720; MinHeight = 500;
            WindowStartupLocation = WindowStartupLocation.CenterScreen; Background = new SolidColorBrush(Color.FromRgb(16, 22, 35));
            Foreground = Ink; FontFamily = new FontFamily("Microsoft YaHei UI"); FontSize = 13; Topmost = true;
            UseLayoutRounding = true;
            Loaded += delegate { FitToDisplay(Displays.ForWindow(dock.Handle), true); };
            SourceInitialized += delegate { HwndSource.FromHwnd(new WindowInteropHelper(this).Handle).AddHook(DisplayMessage); };
            body.Margin = new Thickness(28, 24, 28, 24);
            Content = new ScrollViewer { Content = body, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
            body.Children.Add(new TextBlock { Text = "LIGHT / DOCK", Foreground = new SolidColorBrush(Color.FromRgb(133, 200, 255)), FontSize = 12 });
            body.Children.Add(new TextBlock { Text = "让桌面，轻一点。", FontSize = 27, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 10, 0, 8) });
            Note("所有更改即时生效，自动保存。将常用应用留在触手可及的位置。");
            Section("外观");
            Note("浅色毛玻璃 · 建议不透明度 20%–35%，可透出壁纸的色彩与轮廓。");
            Check("模糊背后的桌面（毛玻璃）", dock.Config.BlurBackground, delegate(bool v) { dock.Config.BlurBackground = v; });
            Slider("背板不透明度（越低越通透）", .05, 1, dock.Config.BackgroundOpacity, delegate(double v) { dock.Config.BackgroundOpacity = v; }, delegate(double v) { return (v * 100).ToString("0") + "%"; });
            Note("圆角：跟随 Windows 11 系统。背板与毛玻璃统一裁剪，避免边缘残影。");
            Slider("图标大小（随系统缩放）", 28, 72, dock.Config.IconSize, delegate(double v) { dock.Config.IconSize = v; }, delegate(double v) { return v.ToString("0"); });
            Slider("悬停放大", 1, 2, dock.Config.Magnification, delegate(double v) { dock.Config.Magnification = v; }, delegate(double v) { return v.ToString("0.00") + " ×"; });
            Section("布局");
            Check("自由拖拽（关闭后回到顶部居中）", dock.Config.Movable, delegate(bool v) { dock.Config.Movable = v; });
            Note("分组试验版采用横向布局，一级菜单最多 12 个图标。");
            Slider("二级菜单最大可见行数", 2, 8, dock.Config.VisibleRows, delegate(double v) { dock.Config.VisibleRows = (int)Math.Round(v); }, delegate(double v) { return Math.Round(v).ToString() + " 行"; });
            Note("开启自由拖拽后，按住图标之间的背板移动 Dock。将来自桌面的图标拖到桌面空白处，可将其移出 Dock 并恢复原位置。图标过多时，滚动鼠标滚轮浏览。");
            Section("桌面与呼出");
            Check("运行期间始终隐藏桌面原生图标", dock.Config.HideDesktopIcons, delegate(bool v) { dock.Config.HideDesktopIcons = v; });
            Note("双击桌面空白处 → 同时隐藏 / 恢复 Dock、桌面图标与任务栏\n双击图标、任务栏或应用窗口不会触发\n" + hotkey + " → 呼出 / 收回\n临时呼出只显示 Dock，任务栏继续隐藏；Esc 或打开应用后收回\n新增图标：从桌面拖入文件、文件夹或快捷方式");
            Note("加入 Dock 的桌面图标会暂时从原位置隐藏。原文件和位置保持不变；从 Dock 移除或退出程序后原位恢复。");
            WrapPanel actions = new WrapPanel();
            ActionButton(actions, "隐藏 / 恢复", dock.ToggleHidden);
            ActionButton(actions, "临时呼出 / 收回", dock.TogglePeek);
            ActionButton(actions, "退出并恢复桌面", delegate { dock.Close(); });
            body.Children.Add(actions);
            Section("壁纸兼容");
            Note("Dock 位于桌面层，普通应用窗口可以覆盖它。动态壁纸兼容性取决于壁纸程序的窗口层级。");
            Button close = new Button { Content = "完成", HorizontalAlignment = HorizontalAlignment.Right, Padding = new Thickness(24, 8, 24, 8), Margin = new Thickness(0, 20, 0, 0), Background = new SolidColorBrush(Color.FromRgb(136, 204, 255)), Foreground = Brushes.Black };
            close.Click += delegate { Close(); }; body.Children.Add(close);
        }
        private IntPtr DisplayMessage(IntPtr hwnd, int msg, IntPtr wp, IntPtr lp, ref bool handled)
        { if (msg == 0x7E || msg == 0x1A) QueueFit(); return IntPtr.Zero; }
        protected override void OnDpiChanged(DpiScale oldDpi, DpiScale newDpi)
        { base.OnDpiChanged(oldDpi, newDpi); QueueFit(); }
        private void QueueFit()
        {
            if (fitQueued) return; fitQueued = true;
            Dispatcher.BeginInvoke(DispatcherPriority.ContextIdle, new Action(delegate {
                fitQueued = false;
                if (IsVisible) FitToDisplay(Displays.ForWindow(new WindowInteropHelper(this).Handle), false);
            }));
        }
        private void FitToDisplay(DisplayArea area, bool center)
        {
            if (WindowState != WindowState.Normal) return;
            double maxWidth = Math.Max(1, area.WorkPx.Width/area.Scale-24);
            double maxHeight = Math.Max(1, area.WorkPx.Height/area.Scale-24);
            MinWidth = Math.Min(360, maxWidth); MinHeight = Math.Min(500, maxHeight);
            Width = Math.Min(Width, maxWidth); Height = Math.Min(Height, maxHeight);
            IntPtr hwnd = new WindowInteropHelper(this).Handle;
            Rect old = Displays.WindowRect(hwnd);
            Size size = new Size(Width*area.Scale, Height*area.Scale);
            Point position = center ? DisplayGeometry.Place(area.WorkPx,size,true,.5,.5,area.Scale) :
                new Point(Layout.KeepVisible(old.Left,area.WorkPx.Left,area.WorkPx.Width,size.Width), Layout.KeepVisible(old.Top,area.WorkPx.Top,area.WorkPx.Height,size.Height));
            Displays.Move(hwnd, position);
        }
        private void Section(string text) { body.Children.Add(new TextBlock { Text = text, FontSize = 16, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 24, 0, 12) }); }
        private void ActionButton(Panel parent, string label, Action action)
        { Button button = new Button { Content = label, Margin = new Thickness(0, 8, 8, 0), Padding = new Thickness(8, 5, 8, 5) }; button.Click += delegate { action(); }; parent.Children.Add(button); }
        private void Note(string text) { body.Children.Add(new TextBlock { Text = text, TextWrapping = TextWrapping.Wrap, Foreground = Muted, LineHeight = 22, Margin = new Thickness(0, 0, 0, 4) }); }
        private void Check(string label, bool value, Action<bool> update)
        {
            CheckBox check = new CheckBox { Content = label, IsChecked = value, Foreground = Ink, Margin = new Thickness(0, 0, 0, 12) };
            check.Click += delegate { update(check.IsChecked == true); dock.Apply(); }; body.Children.Add(check);
        }
        private void Slider(string label, double min, double max, double value, Action<double> update, Func<double, string> format)
        {
            DockPanel labels = new DockPanel();
            TextBlock number = new TextBlock { Text = format(value), Foreground = Muted, HorizontalAlignment = HorizontalAlignment.Right };
            DockPanel.SetDock(number, Dock.Right); labels.Children.Add(number); labels.Children.Add(new TextBlock { Text = label }); body.Children.Add(labels);
            Slider slider = new Slider { IsSnapToTickEnabled = label == "二级菜单最大可见行数", TickFrequency = 1, Minimum = min, Maximum = max, Value = value, Margin = new Thickness(0, 8, 0, 15) };
            slider.ValueChanged += delegate { update(slider.Value); number.Text = format(slider.Value); dock.Apply(); }; body.Children.Add(slider);
        }
    }
}
