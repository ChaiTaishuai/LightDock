using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
namespace AsterDock
{
    internal static class ShellDrop
    {
        internal const string RecycleBin = "::{645FF040-5081-101B-9F08-00AA002F954E}";
        internal static bool IsRecycleBin(string path) { return string.Equals(path,RecycleBin,StringComparison.OrdinalIgnoreCase); }
        [DllImport("shell32.dll")] private static extern IntPtr ILCombine(IntPtr parent,IntPtr child);
        [DllImport("shell32.dll")] private static extern int SHGetNameFromIDList(IntPtr pidl,uint kind,out IntPtr name);
        internal static bool Accepts(IDataObject data) { return data.GetDataPresent(DataFormats.FileDrop) || data.GetDataPresent("Shell IDList Array"); }
        internal static string[] Read(IDataObject data)
        {
            var result=new List<string>();
            try {
                var files=data.GetData(DataFormats.FileDrop) as string[];
                if(files!=null) result.AddRange(files);
                if(data.GetDataPresent("Shell IDList Array")) {
                    var stream=data.GetData("Shell IDList Array") as MemoryStream;
                    if(stream!=null) result.AddRange(Parse(stream.ToArray()));
                }
            } catch(COMException) { } catch(IOException) { }

            return result.ToArray();
        }
        private static bool Valid(byte[] data,int offset,int header)
        {
            if(offset<header || offset>data.Length-2) return false;
            while(offset<=data.Length-2) { int size=BitConverter.ToUInt16(data,offset);if(size==0)return true;if(size<2 || size>data.Length-offset)return false;offset+=size; }
            return false;
        }
        internal static string[] Parse(byte[] data)
        {
            var result=new List<string>(); if(data.Length<12)return result.ToArray();
            uint count=BitConverter.ToUInt32(data,0);if(count>4096 || 4L+4L*(count+1)>data.Length)return result.ToArray();
            int header=checked(4+4*((int)count+1)),parent=BitConverter.ToInt32(data,4);
            if(!Valid(data,parent,header))return result.ToArray();
            IntPtr memory=Marshal.AllocCoTaskMem(data.Length);
            try {
                Marshal.Copy(data,0,memory,data.Length);
                for(int i=0;i<count;i++) {
                    int offset=BitConverter.ToInt32(data,8+i*4);if(!Valid(data,offset,header))continue;
                    IntPtr full=ILCombine(IntPtr.Add(memory,parent),IntPtr.Add(memory,offset)),name=IntPtr.Zero;
                    try { if(full!=IntPtr.Zero && SHGetNameFromIDList(full,0x80028000,out name)>=0 && IsRecycleBin(Marshal.PtrToStringUni(name)))result.Add(RecycleBin); }
                    finally { if(name!=IntPtr.Zero)Marshal.FreeCoTaskMem(name);if(full!=IntPtr.Zero)Marshal.FreeCoTaskMem(full); }
                }
            } finally { Marshal.FreeCoTaskMem(memory); }
            return result.ToArray();
        }
    }
}
