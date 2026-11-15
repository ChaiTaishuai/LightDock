using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
namespace AsterDock
{
    internal static class HoverCardTests
    {
        internal static int Run()
        {
            string report=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"hover-card-test-results.txt");
            var app=new Application(); var tile=new Border {Width=80,Height=80,Background=Brushes.SteelBlue};
            var owner=new Window {Content=tile,Width=120,Height=120,ShowActivated=false,ShowInTaskbar=false};
            HoverCard card=null;int result=0,phase=0,checks=0;
            Action<bool,string> check=delegate(bool ok,string label){checks++;if(!ok)throw new Exception(label);};
            var timer=new DispatcherTimer {Interval=TimeSpan.FromMilliseconds(250)};
            timer.Tick+=delegate {
                try {
                    if(phase==0) {
                        foreach(double scale in new double[]{1,1.25,1.5,2,3}) foreach(int width in new int[]{1920,2560,3840}) {
                            var work=new Rect(-width,-200,width,1080);var size=new Size(320*scale,80*scale);
                            foreach(var anchor in new Rect[]{new Rect(work.Left,work.Top,80,80),new Rect(work.Right-80,work.Bottom-80,80,80)})
                                check(work.Contains(new Rect(HoverCard.Place(anchor,size,work,scale),size)),"Edge placement fits "+scale);
                        }
                        check(HoverCard.DisplayPath(ShellDrop.RecycleBin)=="Windows 回收站","Friendly recycle bin path");
                        string shortPath=HoverCard.Shorten(@"D:\工作资料\非常长的文件夹名称\项目资料",20,s=>s.Length);
                        check(shortPath.Length<=20 && shortPath.Contains("…") && shortPath.EndsWith("项目资料"),"Middle truncation retains tail");
                        card=new HoverCard(owner);card.Display(tile,new DockItem("项目资料 · Light Dock",@"D:\工作文件\非常长的文件夹名称\设计与素材\项目资料"));
                    } else {
                        check(card.IsVisible,"Card visible");check(Native.HasSystemCornerConfiguration(card.Handle),"Native rounded clipping without layered rectangle");
                        check(Displays.IsPerMonitorV2(card.Handle),"Card PMv2 aware");
                        check((Native.GetWindowLongPtr(card.Handle,-20).ToInt64() & 0x08000000)!=0,"Card cannot activate");
                        check(Displays.ForWindow(card.Handle).WorkPx.Contains(Displays.WindowRect(card.Handle)),"Actual card inside work area");
                        var bitmap=new RenderTargetBitmap((int)Math.Ceiling(card.ActualWidth),(int)Math.Ceiling(card.ActualHeight),96,96,PixelFormats.Pbgra32); bitmap.Render(card);
                        var png=new PngBitmapEncoder();png.Frames.Add(BitmapFrame.Create(bitmap));using(var file=File.Create(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"hover-card-preview.png")))png.Save(file);
                        card.Hide();check(!card.IsVisible,"Hide removes card");card.Close();timer.Stop();owner.Close();
                        File.WriteAllText(report,"PASS: "+checks+" checks; native blur="+card.GlassAvailable);return;
                    }
                    phase++;
                }catch(Exception e){result=1;timer.Stop();if(card!=null)card.Close();owner.Close();File.WriteAllText(report,"FAIL: "+e);}
            };
            owner.Loaded+=delegate {timer.Start();};app.Run(owner);return result;
        }
    }
}
