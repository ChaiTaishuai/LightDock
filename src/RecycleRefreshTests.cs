using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
namespace AsterDock
{
    internal static class RecycleRefreshTests
    {
        private static Image Find(DependencyObject parent,string target)
        {
            var border=parent as Border;
            if(border!=null && border.Tag is DockItem && ((DockItem)border.Tag).Target==target)
                return (Image)((Grid)border.Child).Children[1];
            for(int i=0;i<VisualTreeHelper.GetChildrenCount(parent);i++) { var found=Find(VisualTreeHelper.GetChild(parent,i),target); if(found!=null)return found; }
            return null;
        }
        internal static int Run()
        {
            string report=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"recycle-refresh-test-results.txt");
            var app=new Application(); var config=Settings.Defaults(); config.Items.Clear();
            var bin=new DockItem("回收站",ShellDrop.RecycleBin);
            config.Items.Add(bin); config.Items.Add(new DockItem("Explorer","explorer.exe"));
            var window=new DockWindow(config); var timer=new DispatcherTimer {Interval=TimeSpan.FromMilliseconds(150)};
            int phase=0,ticks=0,result=0; ImageSource previous=null,ordinary=null;
            timer.Tick+=delegate {
                try {
                    if(++ticks>120) throw new Exception("Timed out waiting for live recycle icon update, phase="+phase);
                    var image=Find(window,ShellDrop.RecycleBin); var regular=Find(window,"explorer.exe");
                    if(image==null || image.Source==null || regular==null || regular.Source==null)return;
                    if(phase==0) {previous=image.Source;ordinary=regular.Source;phase=1;}
                    else if(phase==1 && !ReferenceEquals(previous,image.Source)) {
                        if(!ReferenceEquals(ordinary,regular.Source))throw new Exception("Ordinary icon unexpectedly refreshed");
                        config.Transfer(bin,"_",null); window.SetState(DockState.Visible); window.TestExpand(true); phase=2;
                    } else if(phase==2) {previous=image.Source; window.TestExpand(false);phase=3;}
                    else if(phase==3 && !ReferenceEquals(previous,image.Source)) {
                        window.TestExpand(true);
                        if(bin.GroupId!="_")throw new Exception("Group changed during refresh");
                        timer.Stop(); window.Close();
                        File.WriteAllText(report,"PASS: primary refresh, ordinary cache retained, secondary refresh while collapsed, group retained, clean exit. No recycle-bin contents modified.");
                    }
                } catch(Exception ex) {result=1;timer.Stop();window.Close();File.WriteAllText(report,"FAIL: "+ex);}
            };
            window.Loaded+=delegate {timer.Start();}; app.Run(window);return result;
        }
    }
}
