using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media.Imaging;
using Microsoft.Win32;

namespace Screenshoter
{
    public static class ScreenshotHelper
    {
        // Захват всего виртуального экрана средствами GDI (вызвать перед показом окна)
        public static BitmapSource CaptureVirtualScreen(out double left, out double top,
                                                        out double width, out double height)
        {
            double dpi = GetDpiScale();
            left = SystemParameters.VirtualScreenLeft;
            top = SystemParameters.VirtualScreenTop;
            width = SystemParameters.VirtualScreenWidth;
            height = SystemParameters.VirtualScreenHeight;

            int pxW = (int)Math.Round(width * dpi);
            int pxH = (int)Math.Round(height * dpi);
            int pxL = (int)Math.Round(left * dpi);
            int pxT = (int)Math.Round(top * dpi);

            using var bmp = new System.Drawing.Bitmap(pxW, pxH);
            using (var g = System.Drawing.Graphics.FromImage(bmp))
                g.CopyFromScreen(pxL, pxT, 0, 0, new System.Drawing.Size(pxW, pxH));

            var hbmp = bmp.GetHbitmap();
            try
            {
                var src = System.Windows.Interop.Imaging.CreateBitmapSourceFromHBitmap(
                    hbmp, IntPtr.Zero, Int32Rect.Empty,
                    BitmapSizeOptions.FromEmptyOptions());
                src.Freeze();
                return src;
            }
            finally { DeleteObject(hbmp); }
        }

        // Обрезка кадра по выделению в DIP -> перевод в пиксели источника
        public static BitmapSource Crop(BitmapSource full, Rect selDip,
                                        double windowDipW, double windowDipH)
        {
            double sx = full.PixelWidth / windowDipW;
            double sy = full.PixelHeight / windowDipH;

            int x = (int)Math.Round(selDip.X * sx);
            int y = (int)Math.Round(selDip.Y * sy);
            int w = (int)Math.Round(selDip.Width * sx);
            int h = (int)Math.Round(selDip.Height * sy);

            x = Math.Max(0, x); y = Math.Max(0, y);
            w = Math.Min(w, full.PixelWidth - x);
            h = Math.Min(h, full.PixelHeight - y);
            if (w <= 0 || h <= 0) return full;

            var cropped = new CroppedBitmap(full, new Int32Rect(x, y, w, h));
            cropped.Freeze();
            return cropped;
        }

        // Асинхронное копирование: UI не блокируется, буфер ставится в STA-потоке
        public static Task CopyToClipboardAsync(BitmapSource image)
        {
            var tcs = new TaskCompletionSource<bool>();
            var thread = new System.Threading.Thread(() =>
            {
                try
                {
                    Clipboard.SetImage(image);   // требует STA
                    tcs.SetResult(true);
                }
                catch (Exception ex) { tcs.SetException(ex); }
            });
            thread.SetApartmentState(System.Threading.ApartmentState.STA);
            thread.IsBackground = true;
            thread.Start();
            return tcs.Task;
        }

        // Асинхронное сохранение PNG
        public static async Task SaveAsync(BitmapSource image)
        {
            var dlg = new SaveFileDialog
            {
                Filter = "PNG Image|*.png",
                FileName = $"Screenshot_{DateTime.Now:yyyyMMdd_HHmmss}.png"
            };
            if (dlg.ShowDialog() != true) return;
            string path = dlg.FileName;

            await Task.Run(() =>
            {
                var encoder = new PngBitmapEncoder();
                encoder.Frames.Add(BitmapFrame.Create(image));
                using var fs = new FileStream(path, FileMode.Create, FileAccess.Write);
                encoder.Save(fs);
            });
        }

        private static double GetDpiScale()
        {
            var wnd = Application.Current?.MainWindow;
            if (wnd != null)
            {
                var src = System.Windows.PresentationSource.FromVisual(wnd);
                if (src?.CompositionTarget != null)
                    return src.CompositionTarget.TransformToDevice.M11;
            }
            return 1.0;
        }

        [System.Runtime.InteropServices.DllImport("gdi32.dll")]
        private static extern bool DeleteObject(IntPtr hObject);
    }
}
