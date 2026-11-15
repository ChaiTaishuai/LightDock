using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using System.Windows.Markup;
using System.Windows.Controls.Primitives;

namespace AsterDock
{
    internal sealed partial class DockWindow
    {
        private bool expanded;
        private Button expander;
        private Border expansion;
        private ScrollViewer secondaryScroll;
        private StackPanel secondaryContent;
        private readonly Dictionary<string,WrapPanel> groupPanels = new Dictionary<string,WrapPanel>();
        private readonly Dictionary<string,Border> groupBoxes = new Dictionary<string,Border>();
        private DateTime triangleHover;
        private DispatcherTimer dragScroll;
        private double groupExtent;
        private bool resizingGroups;
        private void PrepareGroups(StackPanel unused)
        {
            groupPanels.Clear(); groupBoxes.Clear();
            groupExtent = Config.IconSize * Config.Magnification + 22;
            secondaryContent = new StackPanel();
            MakeGroup("", null);
            MakeGroup("_", "未分组");
            foreach(var group in Config.Groups) MakeGroup(group.Id, group.Name);
        }
        private void MakeGroup(string id,string name)
        {
            WrapPanel panel = new WrapPanel { Width = Config.Columns * groupExtent, MinHeight = groupExtent, Orientation = Orientation.Horizontal };
            groupPanels[id] = panel;
            StackPanel content = new StackPanel();
            if(name != null)
            {
                TextBlock label = new TextBlock { Text = name, FontSize = 12, Height = 28, Padding = new Thickness(7,5,0,0), Foreground = Brushes.SlateGray, TextTrimming = TextTrimming.CharacterEllipsis };
                content.Children.Add(label);
            }
            content.Children.Add(panel);
            Border box = new Border { Child = content, Background = Brushes.Transparent, CornerRadius = new CornerRadius(6) };
            groupBoxes[id] = box;
            if(id != "") secondaryContent.Children.Add(box);
            ContextMenu menu = new ContextMenu();
            AddMenu(menu,"新建分组…",delegate { EditGroup(null); });
            if(id != "" && id != "_")
            {
                AddMenu(menu,"重命名分组…",delegate { EditGroup(Config.Groups.Find(g => g.Id == id)); });
                AddMenu(menu,"删除分组（图标移至未分组）",delegate { Config.DeleteGroup(id); Apply(); });
            }
            box.ContextMenu = menu;
        }
        private WrapPanel GroupPanel(string id) { return groupPanels[id ?? ""]; }
        private void FinishGroups(StackPanel unused,double extent)
        {
            Grid top = new Grid();
            top.ColumnDefinitions.Add(new ColumnDefinition()); top.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(28) });
            top.Children.Add(groupBoxes[""]);
            expander = new Button { Content = expanded ? "▴" : "▾", Width = 26, Height = 30, FontSize = 17, Background = Brushes.Transparent, BorderThickness = new Thickness(0), ToolTip = "展开 / 收起分组", Focusable = false };
            Grid.SetColumn(expander,1); top.Children.Add(expander);
            expander.Click += delegate { expanded = !expanded; ResizeGroups(true); };
            secondaryScroll = new ScrollViewer { Content = secondaryContent, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled, CanContentScroll = false };
            secondaryScroll.Resources.Add(typeof(ScrollBar), FrostedScrollBarStyle());
            expansion = new Border { Child = secondaryScroll, Height = 0, ClipToBounds = true };
            StackPanel all = new StackPanel { Width = Config.Columns*extent + 28 };
            all.Children.Add(top); all.Children.Add(expansion);
            viewport = new ScrollViewer { Content = all, HorizontalScrollBarVisibility = ScrollBarVisibility.Auto, VerticalScrollBarVisibility = ScrollBarVisibility.Disabled };
            row.Children.Add(viewport); ResizeGroups(false);
        }
        private static Style FrostedScrollBarStyle()
        {
            // The translucent track and thumb let the Dock's existing glass show through.
            // Track and repeat buttons retain standard WPF scrolling and accessibility.
            const string xaml = @"<Style xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation'
 xmlns:x='http://schemas.microsoft.com/winfx/2006/xaml' TargetType='{x:Type ScrollBar}'>
 <Setter Property='Width' Value='10'/><Setter Property='MinWidth' Value='10'/>
 <Setter Property='Template'><Setter.Value><ControlTemplate TargetType='{x:Type ScrollBar}'>
  <Grid Background='Transparent'>
   <Border Width='5' Margin='0,4' CornerRadius='3' Background='#20FFFFFF' BorderBrush='#25FFFFFF' BorderThickness='0.5'/>
   <Track x:Name='PART_Track' IsDirectionReversed='True' Focusable='False'>
    <Track.DecreaseRepeatButton><RepeatButton Command='{x:Static ScrollBar.PageUpCommand}' Opacity='0' Focusable='False'/></Track.DecreaseRepeatButton>
    <Track.IncreaseRepeatButton><RepeatButton Command='{x:Static ScrollBar.PageDownCommand}' Opacity='0' Focusable='False'/></Track.IncreaseRepeatButton>
    <Track.Thumb><Thumb MinHeight='24' Focusable='False'><Thumb.Template><ControlTemplate TargetType='{x:Type Thumb}'>
      <Border x:Name='Grip' Width='5' Margin='0,2' CornerRadius='3' Background='#88F7FAFF' BorderBrush='#80FFFFFF' BorderThickness='0.5'/>
      <ControlTemplate.Triggers><Trigger Property='IsMouseOver' Value='True'><Setter TargetName='Grip' Property='Background' Value='#C0FFFFFF'/><Setter TargetName='Grip' Property='Width' Value='7'/></Trigger>
       <Trigger Property='IsDragging' Value='True'><Setter TargetName='Grip' Property='Background' Value='#E0FFFFFF'/><Setter TargetName='Grip' Property='Width' Value='7'/></Trigger></ControlTemplate.Triggers>
     </ControlTemplate></Thumb.Template></Thumb></Track.Thumb>
   </Track>
  </Grid>
 </ControlTemplate></Setter.Value></Setter></Style>";
            return (Style)XamlReader.Parse(xaml);
        }
        private void ResizeGroups(bool animate)
        {
            if(viewport == null || expansion == null || resizingGroups) return;
            resizingGroups = true;
            try
            {
                var display = TargetDisplay(); var available = DisplayGeometry.Viewport(display.WorkPx,display.Scale);
                viewport.MaxWidth = available.Width;
                int remaining = Config.VisibleRows;
                double desired = 0;
                foreach(string key in new string[] { "_" }.Concat(Config.Groups.Select(g => g.Id)))
                {
                    int rows = Math.Max(1,(groupPanels[key].Children.Count+Config.Columns-1)/Config.Columns);
                    desired += 28 + Math.Min(remaining,rows)*groupExtent;
                    remaining -= Math.Min(remaining,rows);
                    if(remaining == 0) break;
                }
                desired = Math.Min(desired, Math.Max(0,available.Height-groupExtent-20));
                secondaryScroll.MaxHeight = desired;
                double target = expanded ? desired : 0;
                expander.Content = expanded ? "▴" : "▾";
                if(!animate && expansion.HasAnimatedProperties) return;
                double start = expansion.ActualHeight;
                expansion.BeginAnimation(HeightProperty,null); expansion.Height = target;
                if(animate)
                {
                    var animation = new DoubleAnimation(start,target,TimeSpan.FromMilliseconds(240)) { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseInOut }, FillBehavior = FillBehavior.Stop };
                    animation.Completed += delegate { expansion.BeginAnimation(HeightProperty,null); QueueDisplayRefresh(); };
                    expansion.BeginAnimation(HeightProperty,animation);
                }
            }
            finally { resizingGroups=false; }
        }
        private bool IsToggleSource(DependencyObject source)
        {
            while(source != null)
            {
                if(source == expander || source is System.Windows.Controls.Primitives.ScrollBar || source is System.Windows.Controls.Primitives.Thumb) return true;
                source = source is Visual ? VisualTreeHelper.GetParent(source) : LogicalTreeHelper.GetParent(source);
            }
            return false;
        }
        private bool Inside(FrameworkElement element,Point point)
        {
            if(element == null || !element.IsVisible) return false;
            Point local = TranslatePoint(point,element);
            return local.X>=0 && local.Y>=0 && local.X<element.ActualWidth && local.Y<element.ActualHeight;
        }
        private string DropGroup(Point point)
        {
            if(!Inside(viewport,point)) return null;
            if(Inside(groupBoxes[""],point)) return "";
            if(expanded && Inside(expansion,point)) foreach(var pair in groupBoxes) if(pair.Key!="" && Inside(pair.Value,point)) return pair.Key;
            return null;
        }
        private void UpdateGroupDrag(Point point)
        {
            if(dragScroll == null)
            {
                dragScroll = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(80) };
                dragScroll.Tick += delegate { if(itemDragging) UpdateGroupDrag(Mouse.GetPosition(this)); else dragScroll.Stop(); };
            }
            dragScroll.Start();
            if(!expanded && Inside(expander,point))
            {
                if(triangleHover == DateTime.MinValue) triangleHover=DateTime.UtcNow;
                if((DateTime.UtcNow-triangleHover).TotalMilliseconds>500) { expanded=true; ResizeGroups(true); }
            }
            else triangleHover=DateTime.MinValue;
            string group=DropGroup(point);
            foreach(var pair in groupBoxes) pair.Value.Background = pair.Key == group ? new SolidColorBrush(Color.FromArgb(45,90,155,225)) : Brushes.Transparent;
            if(expanded && Inside(expansion,point))
            {
                Point local=TranslatePoint(point,expansion);
                if(local.Y<24) secondaryScroll.ScrollToVerticalOffset(secondaryScroll.VerticalOffset-12);
                else if(local.Y>expansion.ActualHeight-24) secondaryScroll.ScrollToVerticalOffset(secondaryScroll.VerticalOffset+12);
            }
        }
        private void ClearDropHighlight()
        { if(dragScroll!=null) dragScroll.Stop(); triangleHover=DateTime.MinValue; foreach(var box in groupBoxes.Values) box.Background=Brushes.Transparent; }
        private void HighlightExternalDrop(Point point)
        {
            string group = DropGroup(point);
            foreach(var pair in groupBoxes) pair.Value.Background = group != null && pair.Key == group ? new SolidColorBrush(Color.FromArgb(45,90,155,225)) : Brushes.Transparent;
            if(expanded && Inside(expansion,point))
            {
                Point local=TranslatePoint(point,expansion);
                if(local.Y<24) secondaryScroll.ScrollToVerticalOffset(secondaryScroll.VerticalOffset-12);
                else if(local.Y>expansion.ActualHeight-24) secondaryScroll.ScrollToVerticalOffset(secondaryScroll.VerticalOffset+12);
            }
        }
        private bool DropIntoGroup(DockItem item,Point point)
        {
            string group=DropGroup(point);
            if(group==null) return false;
            DockItem before=null;
            foreach(var tile in tiles) if(Inside(tile,point)) { before=(DockItem)tile.Tag; break; }
            ClearDropHighlight();
            if(Config.Transfer(item,group,before)) Apply();
            return true; // a full primary row rejects the transfer without removing the item
        }
        private void EditGroup(DockGroup group)
        {
            Window dialog=new Window { Title=group==null ? "新建分组" : "重命名分组", Width=340,Height=155,ResizeMode=ResizeMode.NoResize,WindowStartupLocation=WindowStartupLocation.CenterScreen };
            StackPanel body=new StackPanel { Margin=new Thickness(18) };
            TextBox name=new TextBox { Text=group==null ? "" : group.Name, MaxLength=40,Margin=new Thickness(0,0,0,12) };
            Button ok=new Button { Content="确定",IsDefault=true,Padding=new Thickness(12,5,12,5) };
            ok.Click+=delegate { if(string.IsNullOrWhiteSpace(name.Text)) { name.Focus(); return; } dialog.DialogResult=true; };
            body.Children.Add(name); body.Children.Add(ok); dialog.Content=body; dialog.Loaded+=delegate { name.Focus(); name.SelectAll(); };
            if(dialog.ShowDialog()!=true) return;
            if(group==null) Config.Groups.Add(new DockGroup { Id=Guid.NewGuid().ToString("N"),Name=name.Text.Trim() }); else group.Name=name.Text.Trim();
            expanded=true; Apply();
        }
        internal void TestExpand(bool value) { expanded=value; ResizeGroups(true); }
        internal double ExpansionHeight { get { return expansion.ActualHeight; } }
        internal double SecondaryExtent { get { return secondaryScroll.ExtentHeight; } }
        internal double SecondaryViewport { get { return secondaryScroll.ViewportHeight; } }
    }
}
