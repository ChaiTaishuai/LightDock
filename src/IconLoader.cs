using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.IO;

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
            // Shell namespace icons are dynamic; never retain their first image.
            if (ShellDrop.IsRecycleBin(path)) return LoadFresh(path);
            if (IsImage(path))
            {
                TaskCompletionSource<ImageSource> image = new TaskCompletionSource<ImageSource>();
                queue.Add(delegate {
                    try {
                        BitmapImage bitmap = new BitmapImage(); bitmap.BeginInit();
                        bitmap.CacheOption = BitmapCacheOption.OnLoad; bitmap.DecodePixelWidth = 256;
                        bitmap.UriSource = new Uri(Path.GetFullPath(path)); bitmap.EndInit(); bitmap.Freeze();
                        image.SetResult(bitmap);
                    } catch (Exception) { try { image.SetResult(Native.Icon(path)); } catch (Exception) { image.SetResult(null); } }
                });
                return image.Task;
            }
            return cache.GetOrAdd(path, delegate(string key) {
                TaskCompletionSource<ImageSource> result = new TaskCompletionSource<ImageSource>();
                queue.Add(delegate { try { result.SetResult(Native.Icon(key)); } catch (Exception) { result.SetResult(null); } });
                return result.Task;
            });
        }
        private static Task<ImageSource> LoadFresh(string path)
        {
            var result = new TaskCompletionSource<ImageSource>();
            queue.Add(delegate { try { result.SetResult(Native.Icon(path)); } catch(Exception) { result.SetResult(null); } });
            return result.Task;
        }
        private static bool IsImage(string path)
        {
            string ext = Path.GetExtension(path).ToLowerInvariant();
            return ext == ".png" || ext == ".jpg" || ext == ".jpeg" || ext == ".bmp" || ext == ".gif" || ext == ".tif" || ext == ".tiff" || ext == ".wdp";
        }
    }
}
