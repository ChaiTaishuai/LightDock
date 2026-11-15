using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Media;

namespace AsterDock
{
    internal static class IconLoader
    {
        private static readonly ConcurrentDictionary<string, Task<ImageSource>> cache = new ConcurrentDictionary<string, Task<ImageSource>>(StringComparer.OrdinalIgnoreCase);
        private static readonly BlockingCollection<Action> queue = new BlockingCollection<Action>();
        static IconLoader()
        {
            Thread worker = new Thread(delegate() { foreach (Action job in queue.GetConsumingEnumerable()) job(); });
            worker.IsBackground = true; worker.SetApartmentState(ApartmentState.STA); worker.Start();
        }
        internal static Task<ImageSource> Load(string path)
        {
            return cache.GetOrAdd(path, delegate(string key) {
                TaskCompletionSource<ImageSource> result = new TaskCompletionSource<ImageSource>();
                queue.Add(delegate { try { result.SetResult(Native.Icon(key)); } catch (Exception) { result.SetResult(null); } });
                return result.Task;
            });
        }
    }
}
