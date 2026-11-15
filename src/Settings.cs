using System;
using System.Collections.Generic;
using System.IO;
using System.Xml.Serialization;

namespace AsterDock
{
    public class DockItem
    {
        public string Name { get; set; }
        public string Target { get; set; }
        public DockItem() { }
        public DockItem(string name, string target) { Name = name; Target = target; }
    }

    public class Settings
    {
        public double Radius = 24;
        public double BackgroundOpacity = .26;
        public bool BlurBackground = true;
        public int AppearanceVersion;
        public double IconSize = 44;
        public double Magnification = 1.55;
        public bool Movable;
        public bool Vertical;
        public bool HideDesktopIcons;
        public double X = double.NaN;
        public double Y = double.NaN;
        public bool HasRelativePosition;
        public string PositionMonitor = "";
        public double PositionX = .5;
        public double PositionY;
        public List<DockItem> Items = new List<DockItem>();
        public static string Folder { get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "AsterDock"); } }
        public static string FilePath { get { return Path.Combine(Folder, "settings.xml"); } }
        public static Settings Defaults()
        {
            Settings s = new Settings();
            s.AppearanceVersion = 1;
            s.Items.Add(new DockItem("文件资源管理器", "explorer.exe"));
            s.Items.Add(new DockItem("桌面", Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory)));
            s.Items.Add(new DockItem("文档", Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments)));
            s.Items.Add(new DockItem("记事本", "notepad.exe"));
            s.Items.Add(new DockItem("系统设置", "ms-settings:"));
            return s;
        }
        public void Normalize()
        {
            Radius = Clamp(Radius, 0, 60, 24);
            BackgroundOpacity = Clamp(BackgroundOpacity, .05, 1, .26);
            IconSize = Clamp(IconSize, 28, 72, 44);
            Magnification = Clamp(Magnification, 1, 2, 1.55);
            if (double.IsInfinity(X)) X = double.NaN;
            if (double.IsInfinity(Y)) Y = double.NaN;
            PositionX = Clamp(PositionX, 0, 1, .5);
            PositionY = Clamp(PositionY, 0, 1, 0);
            if (PositionMonitor == null) PositionMonitor = "";
            if (Items == null) Items = new List<DockItem>();
            Items.RemoveAll(delegate(DockItem i) { return i == null || string.IsNullOrWhiteSpace(i.Target); });
            foreach (DockItem i in Items) if (string.IsNullOrWhiteSpace(i.Name)) i.Name = Path.GetFileName(i.Target);
        }
        public static double Clamp(double v, double min, double max, double fallback)
        { return double.IsNaN(v) || double.IsInfinity(v) ? fallback : Math.Max(min, Math.Min(max, v)); }
        public static Settings Load()
        {
            try
            {
                if (!File.Exists(FilePath)) return Defaults();
                using (FileStream stream = File.OpenRead(FilePath))
                {
                    Settings s = (Settings)new XmlSerializer(typeof(Settings)).Deserialize(stream);
                    if (s.AppearanceVersion < 1) { s.BackgroundOpacity = Math.Min(s.BackgroundOpacity, .26); s.BlurBackground = true; s.AppearanceVersion = 1; }
                    s.Normalize(); return s;
                }
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show("配置无法读取，将使用默认设置。原文件将保留为 settings.invalid.xml。\n" + ex.Message, "Aster Dock");
                if (File.Exists(FilePath)) File.Copy(FilePath, Path.Combine(Folder, "settings.invalid.xml"), true);
                return Defaults();
            }
        }
        public void Save()
        {
            Normalize(); Directory.CreateDirectory(Folder);
            string temp = FilePath + ".tmp";
            using (FileStream stream = File.Create(temp)) new XmlSerializer(typeof(Settings)).Serialize(stream, this);
            if (File.Exists(FilePath)) File.Replace(temp, FilePath, FilePath + ".bak");
            else File.Move(temp, FilePath);
        }
    }

    // Pure geometry used both by placement and the test runner.
    internal static class Layout
    {
        public static double Magnify(double distance, double radius, double maximum)
        { double t = Math.Max(0, 1 - Math.Abs(distance) / radius); return 1 + (maximum - 1) * t * t * (3 - 2 * t); }
        public static double KeepVisible(double position, double start, double available, double size)
        { return Math.Max(start, Math.Min(start + Math.Max(0, available - size), position)); }
    }
}
