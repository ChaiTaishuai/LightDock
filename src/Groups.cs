using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Serialization;

namespace AsterDock
{
    public class DockGroup
    {
        public string Id;
        public string Name;
    }
    public partial class Settings
    {
        public int VisibleRows = 4;
        public List<DockGroup> Groups = new List<DockGroup>();
        [XmlIgnore] public int Columns { get { return Math.Max(1, Items.Count(i => string.IsNullOrEmpty(i.GroupId))); } }
        public void NormalizeGroups()
        {
            Vertical = false;
            VisibleRows = Math.Max(2, Math.Min(8, VisibleRows));
            if (Groups == null) Groups = new List<DockGroup>();
            Groups.RemoveAll(g => g == null || string.IsNullOrEmpty(g.Id) || g.Id == "_");
            var ids = new HashSet<string>(); Groups.RemoveAll(g => !ids.Add(g.Id));
            foreach(var g in Groups) if(string.IsNullOrWhiteSpace(g.Name)) g.Name = "新分组";
            int primary = 0;
            foreach(var item in Items)
            {
                if(string.IsNullOrEmpty(item.GroupId)) { item.GroupId = ++primary > 12 ? "_" : ""; }
                else if(item.GroupId != "_" && !ids.Contains(item.GroupId)) item.GroupId = "_";
            }
        }
        public void AddGroupedItem(DockItem item)
        { item.GroupId = Items.Count(i => string.IsNullOrEmpty(i.GroupId)) < 12 ? "" : "_"; Items.Add(item); }
        public void AddGroupedItem(DockItem item, string destination)
        {
            if (destination == "_" || (destination != null && Groups.Exists(g => g.Id == destination)))
            { item.GroupId = destination; Items.Add(item); }
            else AddGroupedItem(item);
        }
        public bool Transfer(DockItem item, string group, DockItem before)
        {
            if(!Items.Contains(item) || item == before) return false;
            group = group ?? "";
            if(group == "" && !string.IsNullOrEmpty(item.GroupId) && Items.Count(i => string.IsNullOrEmpty(i.GroupId)) >= 12) return false;
            if(group != "" && group != "_" && !Groups.Exists(g => g.Id == group)) return false;
            Items.Remove(item); item.GroupId = group;
            int index = before == null ? -1 : Items.IndexOf(before);
            if(index < 0) Items.Add(item); else Items.Insert(index, item);
            return true;
        }
        public void DeleteGroup(string id)
        { foreach(var item in Items) if(item.GroupId == id) item.GroupId = "_"; Groups.RemoveAll(g => g.Id == id); }
    }
}
