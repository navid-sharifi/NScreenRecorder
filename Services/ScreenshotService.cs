#if WINDOWS
using System;
using System.Drawing;
using GdiBitmap = System.Drawing.Bitmap;
using GdiPixelFormat = System.Drawing.Imaging.PixelFormat;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Threading;
using Avalonia;
using Avalonia.Media.Imaging;
using Avalonia.Platform;

namespace ScreenRecorder.Services
{
    /// <summary>
    /// Captures the screen at full native resolution and provides the helpers the
    /// screenshot editor needs (Avalonia preview conversion, clipboard, save to disk).
    /// </summary>
    [SupportedOSPlatform("windows")]
    public class ScreenshotService
    {
        #region Native

        [DllImport("user32.dll")]
        private static extern int GetSystemMetrics(int nIndex);

        [DllImport("gdi32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern IntPtr CreateDC(string lpszDriver, string? lpszDevice, string? lpszOutput, IntPtr lpInitData);

        [DllImport("gdi32.dll", SetLastError = true)]
        private static extern IntPtr CreateCompatibleDC(IntPtr hdc);

        [DllImport("gdi32.dll", SetLastError = true)]
        private static extern IntPtr CreateCompatibleBitmap(IntPtr hdc, int nWidth, int nHeight);

        [DllImport("gdi32.dll", SetLastError = true)]
        private static extern IntPtr SelectObject(IntPtr hdc, IntPtr hgdiobj);

        [DllImport("gdi32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool BitBlt(IntPtr hdcDest, int xDest, int yDest, int wDest, int hDest,
                                          IntPtr hdcSrc, int xSrc, int ySrc, uint rop);

        [DllImport("gdi32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool DeleteObject(IntPtr hObject);

        [DllImport("gdi32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool DeleteDC(IntPtr hdc);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool OpenClipboard(IntPtr hWndNewOwner);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool EmptyClipboard();

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool CloseClipboard();

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr SetClipboardData(uint uFormat, IntPtr hMem);

        [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern uint RegisterClipboardFormat(string lpszFormat);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr GlobalAlloc(uint uFlags, UIntPtr dwBytes);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr GlobalLock(IntPtr hMem);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GlobalUnlock(IntPtr hMem);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr GlobalFree(IntPtr hMem);

        private const int SM_XVIRTUALSCREEN = 76;
        private const int SM_YVIRTUALSCREEN = 77;
        private const int SM_CXVIRTUALSCREEN = 78;
        private const int SM_CYVIRTUALSCREEN = 79;

        private const uint SRCCOPY = 0x00CC0020;
        private const uint CAPTUREBLT = 0x40000000;

        private const uint CF_DIB = 8;
        private const uint GMEM_MOVEABLE = 0x0002;

        #endregion

        /// <summary>
        /// Captures every monitor (the whole virtual desktop) in physical pixels, so the
        /// result keeps the native resolution regardless of DPI scaling.
        /// </summary>
        public GdiBitmap CaptureVirtualScreen()
        {
            int left = GetSystemMetrics(SM_XVIRTUALSCREEN);
            int top = GetSystemMetrics(SM_YVIRTUALSCREEN);
            int width = GetSystemMetrics(SM_CXVIRTUALSCREEN);
            int height = GetSystemMetrics(SM_CYVIRTUALSCREEN);

            if (width <= 0 || height <= 0)
            {
                throw new InvalidOperationException("Unable to determine the screen size.");
            }

            IntPtr screenDc = CreateDC("DISPLAY", null, null, IntPtr.Zero);
            if (screenDc == IntPtr.Zero)
            {
                throw new InvalidOperationException("Unable to open a device context for the display.");
            }

            IntPtr memoryDc = IntPtr.Zero;
            IntPtr hBitmap = IntPtr.Zero;
            IntPtr previousBitmap = IntPtr.Zero;

            try
            {
                memoryDc = CreateCompatibleDC(screenDc);
                hBitmap = CreateCompatibleBitmap(screenDc, width, height);
                if (memoryDc == IntPtr.Zero || hBitmap == IntPtr.Zero)
                {
                    throw new InvalidOperationException("Unable to allocate the capture buffer.");
                }

                previousBitmap = SelectObject(memoryDc, hBitmap);

                // CAPTUREBLT also picks up layered windows (tooltips, menus, overlays).
                if (!BitBlt(memoryDc, 0, 0, width, height, screenDc, left, top, SRCCOPY | CAPTUREBLT))
                {
                    throw new InvalidOperationException($"Screen capture failed (error {Marshal.GetLastWin32Error()}).");
                }

                return Image.FromHbitmap(hBitmap);
            }
            finally
            {
                if (previousBitmap != IntPtr.Zero) SelectObject(memoryDc, previousBitmap);
                if (hBitmap != IntPtr.Zero) DeleteObject(hBitmap);
                if (memoryDc != IntPtr.Zero) DeleteDC(memoryDc);
                DeleteDC(screenDc);
            }
        }

        /// <summary>Returns the given region of the bitmap as a new independent bitmap.</summary>
        public GdiBitmap Crop(GdiBitmap source, Rectangle region)
        {
            var bounds = new Rectangle(0, 0, source.Width, source.Height);
            region.Intersect(bounds);

            if (region.Width <= 0 || region.Height <= 0)
            {
                throw new ArgumentException("The crop region is outside of the image.", nameof(region));
            }

            return source.Clone(region, GdiPixelFormat.Format32bppRgb);
        }

        /// <summary>Converts a GDI bitmap into a bitmap Avalonia can render.</summary>
        public WriteableBitmap ToAvaloniaBitmap(GdiBitmap source)
        {
            var target = new WriteableBitmap(
                new PixelSize(source.Width, source.Height),
                new Vector(96, 96),
                Avalonia.Platform.PixelFormat.Bgra8888,
                AlphaFormat.Opaque);

            var data = source.LockBits(
                new Rectangle(0, 0, source.Width, source.Height),
                ImageLockMode.ReadOnly,
                GdiPixelFormat.Format32bppRgb);

            try
            {
                using var frame = target.Lock();
                int rowBytes = Math.Min(Math.Abs(data.Stride), frame.RowBytes);
                var row = new byte[rowBytes];

                for (int y = 0; y < source.Height; y++)
                {
                    Marshal.Copy(data.Scan0 + (y * data.Stride), row, 0, rowBytes);
                    Marshal.Copy(row, 0, frame.Address + (y * frame.RowBytes), rowBytes);
                }
            }
            finally
            {
                source.UnlockBits(data);
            }

            return target;
        }

        /// <summary>
        /// Puts the image on the Windows clipboard as both CF_DIB and PNG so it can be
        /// pasted into classic apps as well as browsers and chat clients.
        /// </summary>
        /// <param name="ownerWindow">
        /// Handle of the window that owns the clipboard. It must be a real window: with a
        /// NULL owner, EmptyClipboard resets the owner and every SetClipboardData fails.
        /// </param>
        public void CopyToClipboard(GdiBitmap source, IntPtr ownerWindow)
        {
            if (ownerWindow == IntPtr.Zero)
            {
                throw new ArgumentException("A clipboard owner window is required.", nameof(ownerWindow));
            }

            byte[] dib = BuildPackedDib(source);

            byte[] png;
            using (var stream = new MemoryStream())
            {
                source.Save(stream, ImageFormat.Png);
                png = stream.ToArray();
            }

            // The clipboard is a shared resource; another app may hold it for a moment.
            bool opened = false;
            for (int attempt = 0; attempt < 10 && !opened; attempt++)
            {
                opened = OpenClipboard(ownerWindow);
                if (!opened) Thread.Sleep(50);
            }

            if (!opened)
            {
                throw new InvalidOperationException("Another application is holding the clipboard open.");
            }

            try
            {
                EmptyClipboard();

                if (!PlaceOnClipboard(CF_DIB, dib))
                {
                    throw new InvalidOperationException($"The clipboard rejected the image (error {Marshal.GetLastWin32Error()}).");
                }

                uint pngFormat = RegisterClipboardFormat("PNG");
                if (pngFormat != 0)
                {
                    PlaceOnClipboard(pngFormat, png);
                }
            }
            finally
            {
                CloseClipboard();
            }
        }

        /// <summary>Saves the image, picking the encoder from the file extension.</summary>
        public void Save(GdiBitmap source, string path)
        {
            var directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            switch (Path.GetExtension(path).ToLowerInvariant())
            {
                case ".jpg":
                case ".jpeg":
                    var jpegEncoder = ImageCodecInfo.GetImageEncoders()
                        .FirstOrDefault(codec => codec.FormatID == ImageFormat.Jpeg.Guid);

                    if (jpegEncoder == null)
                    {
                        source.Save(path, ImageFormat.Jpeg);
                        break;
                    }

                    using (var parameters = new EncoderParameters(1))
                    {
                        parameters.Param[0] = new EncoderParameter(System.Drawing.Imaging.Encoder.Quality, 95L);
                        source.Save(path, jpegEncoder, parameters);
                    }
                    break;

                case ".bmp":
                    source.Save(path, ImageFormat.Bmp);
                    break;

                default:
                    source.Save(path, ImageFormat.Png);
                    break;
            }
        }

        /// <summary>Suggests a timestamped file name for a new screenshot.</summary>
        public static string BuildDefaultFileName() => $"Screenshot_{DateTime.Now:yyyyMMdd_HHmmss}.png";

        private static bool PlaceOnClipboard(uint format, byte[] bytes)
        {
            IntPtr handle = GlobalAlloc(GMEM_MOVEABLE, (UIntPtr)(uint)bytes.Length);
            if (handle == IntPtr.Zero) return false;

            IntPtr target = GlobalLock(handle);
            if (target == IntPtr.Zero)
            {
                GlobalFree(handle);
                return false;
            }

            try
            {
                Marshal.Copy(bytes, 0, target, bytes.Length);
            }
            finally
            {
                GlobalUnlock(handle);
            }

            // On success the clipboard owns the memory; on failure we still do.
            if (SetClipboardData(format, handle) == IntPtr.Zero)
            {
                GlobalFree(handle);
                return false;
            }

            return true;
        }

        /// <summary>
        /// Builds a packed DIB (BITMAPINFOHEADER + bottom-up 32bpp pixels) for CF_DIB.
        /// Alpha is forced to opaque because GDI screen captures leave it at zero.
        /// </summary>
        private static byte[] BuildPackedDib(GdiBitmap source)
        {
            int width = source.Width;
            int height = source.Height;
            int stride = width * 4;
            const int headerSize = 40;

            var buffer = new byte[headerSize + (stride * height)];

            BitConverter.GetBytes(headerSize).CopyTo(buffer, 0);      // biSize
            BitConverter.GetBytes(width).CopyTo(buffer, 4);           // biWidth
            BitConverter.GetBytes(height).CopyTo(buffer, 8);          // biHeight (positive = bottom-up)
            BitConverter.GetBytes((short)1).CopyTo(buffer, 12);       // biPlanes
            BitConverter.GetBytes((short)32).CopyTo(buffer, 14);      // biBitCount
            BitConverter.GetBytes(0).CopyTo(buffer, 16);              // biCompression = BI_RGB
            BitConverter.GetBytes(stride * height).CopyTo(buffer, 20); // biSizeImage

            var data = source.LockBits(
                new Rectangle(0, 0, width, height),
                ImageLockMode.ReadOnly,
                GdiPixelFormat.Format32bppRgb);

            try
            {
                var row = new byte[stride];
                for (int y = 0; y < height; y++)
                {
                    Marshal.Copy(data.Scan0 + (y * data.Stride), row, 0, stride);

                    for (int x = 3; x < stride; x += 4)
                    {
                        row[x] = 255;
                    }

                    // DIB rows are stored bottom-up.
                    int offset = headerSize + ((height - 1 - y) * stride);
                    Buffer.BlockCopy(row, 0, buffer, offset, stride);
                }
            }
            finally
            {
                source.UnlockBits(data);
            }

            return buffer;
        }
    }
}
#else
using System;
using System.IO;
using Avalonia;
using Avalonia.Input.Platform;
using Avalonia.Media.Imaging;
using Avalonia.Platform;

namespace ScreenRecorder.Services
{
    public class ScreenshotService
    {
        public Bitmap CaptureVirtualScreen()
        {
            string tempFile = Path.Combine(Path.GetTempPath(), $"screenshot_{Guid.NewGuid()}.png");
            try
            {
                if (System.OperatingSystem.IsMacOS())
                {
                    using var process = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = "/usr/sbin/screencapture",
                        Arguments = $"-x \"{tempFile}\"",
                        UseShellExecute = false,
                        CreateNoWindow = true
                    });
                    process?.WaitForExit();
                }

                if (File.Exists(tempFile))
                {
                    var bitmap = new Bitmap(tempFile);
                    try { File.Delete(tempFile); } catch {}
                    return bitmap;
                }
                
                return CreateBlankBitmap(800, 600);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Screen capture failed: {ex.Message}", ex);
            }
        }

        private Bitmap CreateBlankBitmap(int width, int height)
        {
            var target = new WriteableBitmap(
                new PixelSize(width, height),
                new Vector(96, 96),
                PixelFormat.Bgra8888,
                AlphaFormat.Opaque);
            return target;
        }

        public Bitmap Crop(Bitmap source, System.Drawing.Rectangle region)
        {
            int width = Math.Min(region.Width, (int)source.Size.Width - region.X);
            int height = Math.Min(region.Height, (int)source.Size.Height - region.Y);

            if (width <= 0 || height <= 0)
            {
                throw new ArgumentException("The crop region is outside of the image.");
            }

            var cropped = new RenderTargetBitmap(new PixelSize(width, height), new Vector(96, 96));
            using (var ctx = cropped.CreateDrawingContext())
            {
                ctx.DrawImage(source, 
                    new Rect(region.X, region.Y, width, height), 
                    new Rect(0, 0, width, height));
            }

            using (var ms = new MemoryStream())
            {
                cropped.Save(ms);
                ms.Position = 0;
                return new Bitmap(ms);
            }
        }

        public WriteableBitmap ToAvaloniaBitmap(Bitmap source)
        {
            var rt = new RenderTargetBitmap(new PixelSize((int)source.Size.Width, (int)source.Size.Height), new Vector(96, 96));
            using (var ctx = rt.CreateDrawingContext())
            {
                ctx.DrawImage(source, new Rect(0, 0, source.Size.Width, source.Size.Height), new Rect(0, 0, source.Size.Width, source.Size.Height));
            }
            using (var ms = new MemoryStream())
            {
                rt.Save(ms);
                ms.Position = 0;
                return WriteableBitmap.Decode(ms);
            }
        }

        public void CopyToClipboard(Bitmap source, IntPtr ownerWindow)
        {
            Avalonia.Threading.Dispatcher.UIThread.Post(async () =>
            {
                try
                {
                    if (Application.Current?.ApplicationLifetime is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop)
                    {
                        var mainWindow = desktop.MainWindow;
                        if (mainWindow != null)
                        {
                            var clipboard = mainWindow.Clipboard;
                            if (clipboard != null)
                            {
                                await clipboard.SetBitmapAsync(source);
                            }
                        }
                    }
                }
                catch {}
            });
        }

        public void Save(Bitmap source, string path)
        {
            var directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }
            source.Save(path);
        }

        public static string BuildDefaultFileName() => $"Screenshot_{DateTime.Now:yyyyMMdd_HHmmss}.png";
    }
}
#endif