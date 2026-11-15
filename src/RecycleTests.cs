using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
namespace AsterDock
{
    internal static class RecycleTests
    {
        [DllImport("shell32.dll", CharSet=CharSet.Unicode)] private static extern int SHParseDisplayName(string name,IntPtr context,out IntPtr pidl,uint flags,out uint attributes);
        [DllImport("shell32.dll")] private static extern uint ILGetSize(IntPtr pidl);
        private static int checks;
        private static void Check(bool value,string label) { checks++; if(!value) throw new Exception(label); }
        internal static int Run()
        {
            string report=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"recycle-data-test-results.txt");
            try {
                IntPtr pidl; uint attributes;
                Check(SHParseDisplayName(ShellDrop.RecycleBin,IntPtr.Zero,out pidl,0,out attributes)>=0,"Resolve actual Shell recycle bin");
                byte[] data;
                try {
                    int size=(int)ILGetSize(pidl); data=new byte[14+size];
                    Buffer.BlockCopy(BitConverter.GetBytes(1),0,data,0,4);
                    Buffer.BlockCopy(BitConverter.GetBytes(12),0,data,4,4);
                    Buffer.BlockCopy(BitConverter.GetBytes(14),0,data,8,4);
                    Marshal.Copy(pidl,data,14,size);
                } finally { Marshal.FreeCoTaskMem(pidl); }
                var drop=new DataObject(); drop.SetData("Shell IDList Array",new MemoryStream(data));
                Check(ShellDrop.Accepts(drop),"Accept Shell drag format without FileDrop");
                var targets=ShellDrop.Read(drop);
                Check(targets.Length==1 && targets[0]==ShellDrop.RecycleBin,"Decode real Shell PIDL from desktop parent");
                Check(ShellDrop.Parse(new byte[12]).Length==0,"Reject invalid PIDL offsets");
                Check(ShellDrop.Parse(new byte[]{1,2}).Length==0,"Reject truncated data");
                var icon=Native.Icon(ShellDrop.RecycleBin);
                Check(icon!=null && icon.IsFrozen,"Resolve recycle bin system icon");
                var s=Settings.Defaults(); s.Items.Clear(); s.Groups.Add(new DockGroup {Id="g",Name="Test"});
                var bin=new DockItem("回收站",ShellDrop.RecycleBin); var other=new DockItem("Other","explorer.exe");
                s.AddGroupedItem(other); s.AddGroupedItem(bin,"g"); s.Normalize();
                Check(bin.GroupId=="g","Recycle bin stays in secondary group after normalization");
                Check(s.Transfer(bin,"",other) && s.Items[0]==bin,"Insert bin before primary icon");
                Check(s.Transfer(bin,"_",null) && bin.GroupId=="_","Move bin to ungrouped secondary area");
                Check(s.Transfer(bin,"g",null),"Move bin into named group");
                var serializer=new System.Xml.Serialization.XmlSerializer(typeof(Settings));
                using(var stream=new MemoryStream()) { serializer.Serialize(stream,s); stream.Position=0; var restored=(Settings)serializer.Deserialize(stream); restored.Normalize(); Check(restored.Items.Exists(i=>i.Target==ShellDrop.RecycleBin && i.GroupId=="g"),"Persist bin identity and group"); }
                File.WriteAllText(report,"PASS: "+checks+" recycle bin data assertions"); return 0;
            } catch(Exception e) { File.WriteAllText(report,"FAIL: "+e); return 1; }
        }
    }
}
