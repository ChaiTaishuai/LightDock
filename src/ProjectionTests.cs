using System;
using System.Collections.Generic;
using System.IO;
using System.Windows;
using System.Windows.Threading;
namespace AsterDock
{
    internal static class ProjectionTests
    {
        internal static int Run()
        {
            string report=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"projection-test-results.txt");
            var app=new Application { ShutdownMode=ShutdownMode.OnExplicitShutdown };
            var projection=new DesktopIconProjection(app.Dispatcher);
            var baseline=new Dictionary<IntPtr,byte[]>();
            var entries=ShellDesktop.Read(); var items=new List<DockItem>();
            foreach(var e in entries) items.Add(new DockItem("Test",e.Path));
            foreach(var list in Native.DesktopLists()) baseline[list]=WindowRegions.Read(list);
            int phase=0,ticks=0,checks=0,result=0;
            Action<bool,string> check=delegate(bool ok,string name) { checks++; if(!ok) throw new Exception(name); };
            var timer=new DispatcherTimer { Interval=TimeSpan.FromMilliseconds(150) };
            timer.Tick+=delegate {
                try {
                    if(++ticks>100) throw new Exception("Projection timed out");
                    if(projection.Busy) return;
                    check(projection.LastError==null,"Projection query failed");
                    if(phase==0) {
                        check(items.Count>1,"Multiple desktop entries available");
                        projection.Update(items); phase++;
                    } else if(phase==1) {
                        check(projection.HiddenCount==items.Count,"Collect entire desktop, including recycle bin");
                        foreach(var pair in baseline) {
                            // Simulate a live region changed by Explorer: old recovery
                            // skipped it or adopted it as the original desktop region.
                            var altered=WindowRegions.Exclude(pair.Key,WindowRegions.Read(pair.Key),new List<WindowRegions.Rect>{new WindowRegions.Rect{Left=0,Top=0,Right=7,Bottom=7}});
                            WindowRegions.Apply(pair.Key,altered);
                        }
                        items.RemoveAt(0); projection.Update(items);
                        foreach(var pair in baseline) check(WindowRegions.Equal(WindowRegions.Read(pair.Key),pair.Value),"Removal synchronously restores baseline before async work");
                        phase++;
                    } else if(phase==2) {
                        check(projection.HiddenCount==items.Count,"Remaining icons recollected");
                        foreach(var pair in baseline) {
                            var bounds=WindowRegions.ItemBounds(pair.Key,new List<int>{entries[0].Index})[0];
                            WindowRegions.Rect rect; WindowRegions.GetWindowRect(pair.Key,out rect);
                            check(WindowRegions.ContainsScreenPoint(pair.Key,rect.Left+(bounds.Left+bounds.Right)/2,rect.Top+(bounds.Top+bounds.Bottom)/2),"Removed icon is visible again");
                            WindowRegions.Apply(pair.Key,WindowRegions.Exclude(pair.Key,WindowRegions.Read(pair.Key),new List<WindowRegions.Rect>{new WindowRegions.Rect{Left=0,Top=0,Right=9,Bottom=9}}));
                        }
                        projection.Dispose();
                        foreach(var pair in baseline) check(WindowRegions.Equal(WindowRegions.Read(pair.Key),pair.Value),"Exit restores baseline even when live mask changed");
                        var after=ShellDesktop.Read();
                        check(after.Count==entries.Count,"All Shell entries retained");
                        foreach(var entry in entries) check(after.Exists(e=>e.Path==entry.Path && e.Position==entry.Position),"Original item and position retained: "+entry.Path);
                        check(!File.Exists(Path.Combine(Settings.Folder,"desktop-items.recovery.xml")),"Recovery journal cleared after successful restore");
                        timer.Stop(); File.WriteAllText(report,"PASS: "+checks+" checks; desktop entries="+entries.Count); app.Shutdown();
                    }
                } catch(Exception e) {
                    result=1; timer.Stop();
                    try { projection.Dispose(); } finally { foreach(var pair in baseline) WindowRegions.Apply(pair.Key,pair.Value); }
                    File.WriteAllText(report,"FAIL: "+e); app.Shutdown(1);
                }
            };
            timer.Start(); app.Run(); return result;
        }
    }
}
