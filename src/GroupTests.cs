using System;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using System.Xml.Serialization;

namespace AsterDock
{
    internal static class GroupTests
    {
        static int checks;
        static void Check(bool value,string text) { checks++; if(!value) throw new Exception(text); }
        internal static int Run()
        {
            string report=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"group-test-results.txt");
            DockWindow window=null;
            try
            {
                Settings config=new Settings();
                for(int i=0;i<43;i++) config.AddGroupedItem(new DockItem("测试 "+i,"explorer.exe"));
                config.Normalize();
                Check(config.Columns==12 && config.Items.Count(i=>i.GroupId=="_")==31,"Automatic overflow at twelve");
                var group=new DockGroup { Id="work",Name="工作" }; config.Groups.Add(group);
                var direct=new Settings(); direct.Groups.Add(new DockGroup { Id="work",Name="工作" });
                direct.AddGroupedItem(new DockItem("指定组","explorer.exe"),"work");
                direct.AddGroupedItem(new DockItem("未分组","explorer.exe"),"_");
                direct.AddGroupedItem(new DockItem("默认","explorer.exe"),null);
                Check(direct.Items[0].GroupId=="work" && direct.Items[1].GroupId=="_" && direct.Items[2].GroupId=="", "External drop uses chosen group despite free primary capacity");
                var item=config.Items[12]; Check(config.Transfer(item,"work",null),"Transfer from ungrouped to named group");
                Check(!config.Transfer(item,"",null),"Full primary rejects transfer without loss");
                for(int i=0;i<7;i++) config.Transfer(config.Items.First(x=>x.GroupId==""),"_",null);
                Check(config.Columns==5,"Five primary icons imply five secondary columns");
                config.DeleteGroup("work"); Check(item.GroupId=="_","Deleting group preserves item in ungrouped");
                using(var stream=new MemoryStream())
                {
                    var serializer=new XmlSerializer(typeof(Settings)); serializer.Serialize(stream,config);stream.Position=0;
                    var copy=(Settings)serializer.Deserialize(stream);copy.Normalize();
                    Check(copy.Columns==5 && copy.Items.Count==43,"Group assignments roundtrip");
                }
                foreach(var original in config.Items.Where(i=>i.GroupId=="").ToArray()) config.Transfer(original,"_",null);
                Check(config.Columns==1,"Empty primary uses one column");
                config.VisibleRows=99;config.Normalize();Check(config.VisibleRows==8,"Maximum rows clamped");
                config.VisibleRows=0;config.Normalize();Check(config.VisibleRows==2,"Minimum rows clamped");
                config.VisibleRows=4;
                for(int columns=0;columns<=12;columns++) for(int rows=2;rows<=8;rows++)
                {
                    var matrix=new Settings { VisibleRows=rows };
                    for(int i=0;i<columns;i++) matrix.Items.Add(new DockItem("primary","explorer.exe") { GroupId="" });
                    for(int i=0;i<37;i++) matrix.Items.Add(new DockItem("secondary","explorer.exe") { GroupId="_" });
                    matrix.Normalize();
                    Check(matrix.Columns==Math.Max(1,columns) && matrix.VisibleRows==rows && matrix.Items.Count==columns+37,"Column and row matrix preserves all items");
                }
                foreach(var original in config.Items.Take(5).ToArray()) config.Transfer(original,"",null);
                config.Groups.Add(new DockGroup { Id="work",Name="工作" });
                foreach(var original in config.Items.Where(i=>i.GroupId=="_").Take(9).ToArray()) config.Transfer(original,"work",null);
                Application app=new Application(); window=new DockWindow(config);
                int phase=0,result=0;double closed=0;
                var timer=new DispatcherTimer { Interval=TimeSpan.FromMilliseconds(450) };
                timer.Tick+=delegate {
                    try
                    {
                        if(phase==0) { Check(window.ExpansionHeight==0,"Starts collapsed");closed=window.ActualHeight;window.TestExpand(true); }
                        else if(phase==1)
                        {
                            Check(window.ActualHeight>closed && window.ExpansionHeight>0,"Animated expansion increases real window height");
                            Check(window.SecondaryExtent>window.SecondaryViewport,"Overflow scrolls inside secondary menu");
                            Check(Native.HasSystemCornerConfiguration(window.Handle),"Native rounded glass preserved");
                            var bmp=new RenderTargetBitmap((int)Math.Ceiling(window.ActualWidth),(int)Math.Ceiling(window.ActualHeight),96,96,PixelFormats.Pbgra32);bmp.Render(window);
                            var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(bmp));using(var file=File.Create(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"groups-preview.png"))) encoder.Save(file);
                            window.TestExpand(false);
                        }
                        else
                        {
                            Check(Math.Abs(window.ActualHeight-closed)<2,"Collapse restores original height");
                            timer.Stop(); window.Close();
                            File.WriteAllText(report,"PASS: "+checks+" group assertions\r\n"+DateTime.Now.ToString("O"));app.Shutdown();
                        }
                        phase++;
                    }
                    catch(Exception ex) { result=1;timer.Stop();window.Close();File.WriteAllText(report,"FAIL: "+ex);app.Shutdown(1); }
                };
                window.Loaded+=delegate { timer.Start(); };app.Run(window);return result;
            }
            catch(Exception ex) { if(window!=null) window.Cleanup();File.WriteAllText(report,"FAIL: "+ex);return 1; }
        }
    }
}
