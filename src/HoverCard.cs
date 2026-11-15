using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
namespace AsterDock
{
    internal sealed partial class DockWindow
    {
        private DispatcherTimer hoverDelay;
        private FrameworkElement hoverTarget;
        private DockItem hoverItem;
        private HoverCard hoverCard;
        private void AttachHoverCard(FrameworkElement tile,DockItem item)
        {
            tile.MouseEnter+=delegate {
                HideHoverCard();
                if(cleaned || Mouse.LeftButton==MouseButtonState.Pressed || Mouse.RightButton==MouseButtonState.Pressed) return;
                hoverTarget=tile; hoverItem=item;
                if(hoverDelay==null) {
                    hoverDelay=new DispatcherTimer();
                    hoverDelay.Tick+=delegate {
                        hoverDelay.Stop();
                        if(cleaned || hoverTarget==null || !hoverTarget.IsMouseOver || !IsVisible || itemDragging || dragged) return;
                        if(hoverCard==null) hoverCard=new HoverCard(this);
                        hoverCard.Display(hoverTarget,hoverItem);
                    };
                }
                hoverDelay.Interval=TimeSpan.FromSeconds(Config.HoverDelaySeconds);
                hoverDelay.Start();
            };
            tile.MouseLeave+=delegate { if(hoverTarget==tile) HideHoverCard(); };
            tile.ContextMenuOpening+=delegate { HideHoverCard(); };
        }
        private void HideHoverCard()
        {
            if(hoverDelay!=null) hoverDelay.Stop();
            hoverTarget=null; hoverItem=null;
            if(hoverCard!=null) hoverCard.Hide();
        }
        private void DisposeHoverCard() { HideHoverCard(); if(hoverCard!=null) { hoverCard.Close(); hoverCard=null; } }
    }

    internal sealed class HoverCard : Window
    {
        private readonly TextBlock name=new TextBlock { FontSize=15,FontWeight=FontWeights.SemiBold,Foreground=new SolidColorBrush(Color.FromRgb(42,52,65)),TextTrimming=TextTrimming.CharacterEllipsis };
        private readonly TextBlock path=new TextBlock { FontSize=11.5,Foreground=new SolidColorBrush(Color.FromRgb(91,103,117)),Margin=new Thickness(0,5,0,0),TextWrapping=TextWrapping.NoWrap };
        private readonly Border surface=new Border();
        private readonly StackPanel text=new StackPanel();
        internal IntPtr Handle { get; private set; }
        internal bool GlassAvailable { get; private set; }
        internal HoverCard(Window owner)
        {
            Owner=owner; ShowActivated=false; ShowInTaskbar=false; Focusable=false; IsHitTestVisible=false;
            WindowStyle=WindowStyle.None; ResizeMode=ResizeMode.NoResize; SizeToContent=SizeToContent.WidthAndHeight;
            Background=Brushes.Transparent; FontFamily=new FontFamily("Microsoft YaHei UI"); UseLayoutRounding=true;
            System.Windows.Shell.WindowChrome.SetWindowChrome(this,new System.Windows.Shell.WindowChrome {
                CaptionHeight=0,ResizeBorderThickness=new Thickness(0),GlassFrameThickness=new Thickness(-1)
            });
            text.Children.Add(name);text.Children.Add(path);
            surface.Padding=new Thickness(16,12,16,13); surface.BorderThickness=new Thickness(1);
            surface.BorderBrush=new SolidColorBrush(Color.FromArgb(150,255,255,255));surface.Child=text;Content=surface;
            SourceInitialized+=delegate {
                Handle=new WindowInteropHelper(this).Handle;
                Native.SetWindowLongPtr(Handle,-20,new IntPtr(Native.GetWindowLongPtr(Handle,-20).ToInt64() | 0x080000A0)); // no activation, tool window, pointer transparent
                HwndSource.FromHwnd(Handle).CompositionTarget.BackgroundColor=Colors.Transparent;
                Native.UseSystemCorners(Handle); GlassAvailable=Native.SetGlass(Handle,true);
                surface.Background=new LinearGradientBrush(Color.FromArgb(GlassAvailable?(byte)200:(byte)255,250,252,255),Color.FromArgb(GlassAvailable?(byte)185:(byte)255,235,242,249),90);
            };
        }
        internal static string DisplayPath(string target)
        {
            if(ShellDrop.IsRecycleBin(target)) return "Windows 回收站";
            if(target=="ms-settings:") return "Windows 设置";
            return target ?? "";
        }
        internal static string Shorten(string value,double width,Func<string,double> measure)
        {
            if(measure(value)<=width) return value;
            int keep=value.Length;
            while(keep>0) {
                keep--; int left=keep/3,right=keep-left;
                string candidate=value.Substring(0,left)+"…"+value.Substring(value.Length-right);
                if(measure(candidate)<=width)return candidate;
            }
            return "…";
        }
        internal static Point Place(Rect anchor,Size size,Rect work,double scale)
        {
            double y=anchor.Bottom+8*scale;
            if(y+size.Height>work.Bottom) y=anchor.Top-size.Height-8*scale;
            return new Point(Math.Max(work.Left,Math.Min(anchor.Left+(anchor.Width-size.Width)/2,work.Right-size.Width)),Math.Max(work.Top,Math.Min(y,work.Bottom-size.Height)));
        }
        internal void Display(FrameworkElement tile,DockItem item)
        {
            Point top=tile.PointToScreen(new Point()),bottom=tile.PointToScreen(new Point(tile.ActualWidth,tile.ActualHeight));
            Rect anchor=new Rect(top,bottom); var monitor=Displays.AtPoint(top);
            double max=Math.Max(40,Math.Min(320,monitor.WorkPx.Width/monitor.Scale-16));
            name.MaxWidth=path.MaxWidth=max-34; name.Text=item.Name;
            path.Text=Shorten(DisplayPath(item.Target),max-34,delegate(string value) {
                var probe=new TextBlock {Text=value,FontFamily=FontFamily,FontSize=11.5};probe.Measure(new Size(double.PositiveInfinity,double.PositiveInfinity));return probe.DesiredSize.Width;
            });
            // Initialize and move the hidden HWND onto the target monitor before measuring its DPI.
            new WindowInteropHelper(this).EnsureHandle();
            Displays.Move(Handle,top); UpdateLayout();
            surface.Measure(new Size(max,double.PositiveInfinity));
            double scale=Displays.WindowScale(Handle);
            Displays.Move(Handle,Place(anchor,new Size(surface.DesiredSize.Width*scale,surface.DesiredSize.Height*scale),monitor.WorkPx,scale));
            Show(); UpdateLayout();
            Displays.Move(Handle,Place(anchor,Displays.WindowRect(Handle).Size,monitor.WorkPx,Displays.WindowScale(Handle)));
            text.BeginAnimation(OpacityProperty,new DoubleAnimation(0,1,TimeSpan.FromMilliseconds(120)));
        }
    }
}
