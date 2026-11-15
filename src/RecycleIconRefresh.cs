using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
namespace AsterDock
{
    internal sealed partial class DockWindow
    {
        private sealed class RecycleIconBinding
        {
            internal Image Image;
            internal TextBlock Placeholder;
            internal bool Busy, Pending;
        }
        private readonly List<RecycleIconBinding> recycleIcons = new List<RecycleIconBinding>();
        private void RefreshRecycleIcons()
        {
            foreach(var binding in recycleIcons) RefreshRecycleIcon(binding);
        }
        private void RefreshRecycleIcon(RecycleIconBinding binding)
        {
            if(cleaned) return;
            if(binding.Busy) { binding.Pending=true; return; }
            binding.Busy=true;
            IconLoader.Load(ShellDrop.RecycleBin).ContinueWith(delegate(Task<ImageSource> task) {
                if(Dispatcher.HasShutdownStarted) return;
                Dispatcher.BeginInvoke(new Action(delegate {
                    binding.Busy=false;
                    // A rebuild, move, removal or exit invalidates the old view.
                    if(cleaned || !recycleIcons.Contains(binding) || task.Status!=TaskStatus.RanToCompletion || task.Result==null) return;
                    if(binding.Pending) { binding.Pending=false; RefreshRecycleIcon(binding); }
                    binding.Image.Source=task.Result;
                    binding.Placeholder.Visibility=Visibility.Collapsed;
                }));
            });
        }
    }
}
