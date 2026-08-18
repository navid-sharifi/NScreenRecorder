#if WINDOWS
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media.Imaging;
using Avalonia.Platform.Storage;
using ScreenRecorder.Services;
using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Versioning;

namespace ScreenRecorder.Views
{
    /// <summary>
    /// Full-window screenshot preview with crop, clipboard copy and "save as" support.
    /// The GDI bitmap stays the source of truth so every export keeps the captured quality.
    /// </summary>
    [SupportedOSPlatform("windows")]
    public partial class ScreenshotWindow : Window
    {
        private readonly ScreenshotService _screenshotService = new();
        private readonly string _defaultFolder;

        private readonly System.Drawing.Bitmap _original;
        private System.Drawing.Bitmap _current;
        private WriteableBitmap? _preview;

        private Point _dragOrigin;
        private bool _isDragging;
        private System.Drawing.Rectangle? _selection;

        /// <summary>Only used by the XAML runtime loader / previewer.</summary>
        public ScreenshotWindow() : this(new System.Drawing.Bitmap(1, 1), string.Empty)
        {
        }

        public ScreenshotWindow(System.Drawing.Bitmap capture, string defaultFolder)
        {
            InitializeComponent();

            _original = capture;
            _current = capture;
            _defaultFolder = defaultFolder;

            SizeChanged += (_, _) => ClearSelection();
            UpdatePreview();
        }

        private void UpdatePreview()
        {
            var previousPreview = _preview;
            _preview = _screenshotService.ToAvaloniaBitmap(_current);
            PreviewImage.Source = _preview;
            previousPreview?.Dispose();

            ClearSelection();
            ResetButton.IsEnabled = !ReferenceEquals(_current, _original);
            Title = $"Screenshot - {_current.Width} x {_current.Height}";
            InfoText.Text = $"{_current.Width} x {_current.Height} px  -  Drag over the image to select an area, then use \"Crop to Selection\". " +
                            "Ctrl+C copies, Ctrl+S saves, Esc closes.";
        }

        #region Crop selection

        /// <summary>
        /// Bounds of the rendered image in ImageHost coordinates. The image is drawn with
        /// Stretch="Uniform", so it is scaled to fit and centred inside the host cell.
        /// </summary>
        private Rect GetRenderedImageBounds()
        {
            var host = ImageHost.Bounds.Size;
            if (host.Width <= 0 || host.Height <= 0 || _current.Width == 0 || _current.Height == 0)
            {
                return default;
            }

            var scale = Math.Min(host.Width / _current.Width, host.Height / _current.Height);
            var width = _current.Width * scale;
            var height = _current.Height * scale;

            return new Rect((host.Width - width) / 2, (host.Height - height) / 2, width, height);
        }

        private static Point ClampToRect(Point point, Rect rect) => new(
            Math.Clamp(point.X, rect.X, rect.Right),
            Math.Clamp(point.Y, rect.Y, rect.Bottom));

        private void OnPointerPressed(object? sender, PointerPressedEventArgs e)
        {
            if (!e.GetCurrentPoint(ImageHost).Properties.IsLeftButtonPressed) return;

            var imageBounds = GetRenderedImageBounds();
            if (imageBounds.Width <= 0) return;

            _dragOrigin = ClampToRect(e.GetPosition(ImageHost), imageBounds);
            _isDragging = true;
            _selection = null;
            CropButton.IsEnabled = false;

            SelectionRectangle.IsVisible = true;
            SelectionRectangle.Width = 0;
            SelectionRectangle.Height = 0;
            Canvas.SetLeft(SelectionRectangle, _dragOrigin.X);
            Canvas.SetTop(SelectionRectangle, _dragOrigin.Y);

            e.Pointer.Capture(OverlayCanvas);
        }

        private void OnPointerMoved(object? sender, PointerEventArgs e)
        {
            if (!_isDragging) return;

            var imageBounds = GetRenderedImageBounds();
            var current = ClampToRect(e.GetPosition(ImageHost), imageBounds);

            Canvas.SetLeft(SelectionRectangle, Math.Min(_dragOrigin.X, current.X));
            Canvas.SetTop(SelectionRectangle, Math.Min(_dragOrigin.Y, current.Y));
            SelectionRectangle.Width = Math.Abs(current.X - _dragOrigin.X);
            SelectionRectangle.Height = Math.Abs(current.Y - _dragOrigin.Y);
        }

        private void OnPointerReleased(object? sender, PointerReleasedEventArgs e)
        {
            if (!_isDragging) return;
            _isDragging = false;
            e.Pointer.Capture(null);

            var imageBounds = GetRenderedImageBounds();
            if (imageBounds.Width <= 0)
            {
                ClearSelection();
                return;
            }

            var left = Canvas.GetLeft(SelectionRectangle);
            var top = Canvas.GetTop(SelectionRectangle);
            var scale = imageBounds.Width / _current.Width;

            var region = new System.Drawing.Rectangle(
                (int)Math.Round((left - imageBounds.X) / scale),
                (int)Math.Round((top - imageBounds.Y) / scale),
                (int)Math.Round(SelectionRectangle.Width / scale),
                (int)Math.Round(SelectionRectangle.Height / scale));

            if (region.Width < 4 || region.Height < 4)
            {
                ClearSelection();
                return;
            }

            _selection = region;
            CropButton.IsEnabled = true;
            SetStatus($"Selection: {region.Width} x {region.Height} px");
        }

        private void ClearSelection()
        {
            _isDragging = false;
            _selection = null;
            SelectionRectangle.IsVisible = false;
            SelectionRectangle.Width = 0;
            SelectionRectangle.Height = 0;
            CropButton.IsEnabled = false;
        }

        #endregion

        #region Commands

        private void OnCropClick(object? sender, RoutedEventArgs e)
        {
            if (_selection is not { } region) return;

            try
            {
                var cropped = _screenshotService.Crop(_current, region);
                ReplaceCurrent(cropped);
                SetStatus($"Cropped to {cropped.Width} x {cropped.Height} px");
            }
            catch (Exception ex)
            {
                SetStatus($"Crop failed: {ex.Message}", isError: true);
            }
        }

        private void OnResetClick(object? sender, RoutedEventArgs e)
        {
            if (ReferenceEquals(_current, _original)) return;

            ReplaceCurrent(_original);
            SetStatus("Restored the original capture");
        }

        private void OnCopyClick(object? sender, RoutedEventArgs e) => CopyToClipboard();

        private async void OnSaveAsClick(object? sender, RoutedEventArgs e) => await SaveAsAsync();

        private void OnCloseClick(object? sender, RoutedEventArgs e) => Close();

        private void CopyToClipboard()
        {
            try
            {
                var owner = TryGetPlatformHandle()?.Handle ?? IntPtr.Zero;
                _screenshotService.CopyToClipboard(_current, owner);
                SetStatus("Copied to clipboard");
            }
            catch (Exception ex)
            {
                SetStatus($"Copy failed: {ex.Message}", isError: true);
            }
        }

        private async System.Threading.Tasks.Task SaveAsAsync()
        {
            var options = new FilePickerSaveOptions
            {
                Title = "Save Screenshot",
                SuggestedFileName = ScreenshotService.BuildDefaultFileName(),
                DefaultExtension = "png",
                ShowOverwritePrompt = true,
                FileTypeChoices = new List<FilePickerFileType>
                {
                    new("PNG Image") { Patterns = new[] { "*.png" }, MimeTypes = new[] { "image/png" } },
                    new("JPEG Image") { Patterns = new[] { "*.jpg", "*.jpeg" }, MimeTypes = new[] { "image/jpeg" } },
                    new("Bitmap Image") { Patterns = new[] { "*.bmp" }, MimeTypes = new[] { "image/bmp" } }
                }
            };

            if (!string.IsNullOrWhiteSpace(_defaultFolder) && Directory.Exists(_defaultFolder))
            {
                options.SuggestedStartLocation = await StorageProvider.TryGetFolderFromPathAsync(_defaultFolder);
            }

            var file = await StorageProvider.SaveFilePickerAsync(options);
            var path = file?.TryGetLocalPath();
            if (string.IsNullOrEmpty(path)) return;

            try
            {
                _screenshotService.Save(_current, path);
                SetStatus($"Saved to {path}");
            }
            catch (Exception ex)
            {
                SetStatus($"Save failed: {ex.Message}", isError: true);
            }
        }

        private void ReplaceCurrent(System.Drawing.Bitmap bitmap)
        {
            var previous = _current;
            _current = bitmap;

            if (!ReferenceEquals(previous, _original) && !ReferenceEquals(previous, bitmap))
            {
                previous.Dispose();
            }

            UpdatePreview();
        }

        private void SetStatus(string message, bool isError = false)
        {
            StatusText.Text = message;
            StatusText.Foreground = isError
                ? Avalonia.Media.Brushes.IndianRed
                : new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse("#4EC9B0"));
        }

        #endregion

        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                if (_selection != null || _isDragging)
                {
                    ClearSelection();
                }
                else
                {
                    Close();
                }
                e.Handled = true;
            }
            else if (e.Key == Key.C && e.KeyModifiers == KeyModifiers.Control)
            {
                CopyToClipboard();
                e.Handled = true;
            }
            else if (e.Key == Key.S && e.KeyModifiers == KeyModifiers.Control)
            {
                _ = SaveAsAsync();
                e.Handled = true;
            }
            else if (e.Key == Key.Enter && _selection != null)
            {
                OnCropClick(this, new RoutedEventArgs());
                e.Handled = true;
            }

            base.OnKeyDown(e);
        }

        protected override void OnClosed(EventArgs e)
        {
            base.OnClosed(e);

            PreviewImage.Source = null;
            _preview?.Dispose();
            _preview = null;

            if (!ReferenceEquals(_current, _original))
            {
                _current.Dispose();
            }
            _original.Dispose();
        }
    }
}
#else
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media.Imaging;
using Avalonia.Platform.Storage;
using ScreenRecorder.Services;
using System;
using System.Collections.Generic;
using System.IO;

namespace ScreenRecorder.Views
{
    public partial class ScreenshotWindow : Window
    {
        private readonly ScreenshotService _screenshotService = new();
        private readonly string _defaultFolder;

        private readonly Bitmap _original;
        private Bitmap _current;
        private WriteableBitmap? _preview;

        private Point _dragOrigin;
        private bool _isDragging;
        private System.Drawing.Rectangle? _selection;

        public ScreenshotWindow() : this(new WriteableBitmap(new PixelSize(1, 1), new Vector(96, 96), Avalonia.Platform.PixelFormat.Bgra8888, Avalonia.Platform.AlphaFormat.Opaque), string.Empty)
        {
        }

        public ScreenshotWindow(Bitmap capture, string defaultFolder)
        {
            InitializeComponent();

            _original = capture;
            _current = capture;
            _defaultFolder = defaultFolder;

            SizeChanged += (_, _) => ClearSelection();
            UpdatePreview();
        }

        private void UpdatePreview()
        {
            var previousPreview = _preview;
            _preview = _screenshotService.ToAvaloniaBitmap(_current);
            PreviewImage.Source = _preview;
            previousPreview?.Dispose();

            ClearSelection();
            ResetButton.IsEnabled = !ReferenceEquals(_current, _original);
            int width = (int)_current.Size.Width;
            int height = (int)_current.Size.Height;
            Title = $"Screenshot - {width} x {height}";
            InfoText.Text = $"{width} x {height} px  -  Drag over the image to select an area, then use \"Crop to Selection\". " +
                            "Ctrl+C copies, Ctrl+S saves, Esc closes.";
        }

        #region Crop selection

        private Rect GetRenderedImageBounds()
        {
            var host = ImageHost.Bounds.Size;
            int currentWidth = (int)_current.Size.Width;
            int currentHeight = (int)_current.Size.Height;
            if (host.Width <= 0 || host.Height <= 0 || currentWidth == 0 || currentHeight == 0)
            {
                return default;
            }

            var scale = Math.Min(host.Width / currentWidth, host.Height / currentHeight);
            var width = currentWidth * scale;
            var height = currentHeight * scale;

            return new Rect((host.Width - width) / 2, (host.Height - height) / 2, width, height);
        }

        private static Point ClampToRect(Point point, Rect rect) => new(
            Math.Clamp(point.X, rect.X, rect.Right),
            Math.Clamp(point.Y, rect.Y, rect.Bottom));

        private void OnPointerPressed(object? sender, PointerPressedEventArgs e)
        {
            if (!e.GetCurrentPoint(ImageHost).Properties.IsLeftButtonPressed) return;

            var imageBounds = GetRenderedImageBounds();
            if (imageBounds.Width <= 0) return;

            _dragOrigin = ClampToRect(e.GetPosition(ImageHost), imageBounds);
            _isDragging = true;
            _selection = null;
            CropButton.IsEnabled = false;

            SelectionRectangle.IsVisible = true;
            SelectionRectangle.Width = 0;
            SelectionRectangle.Height = 0;
            Canvas.SetLeft(SelectionRectangle, _dragOrigin.X);
            Canvas.SetTop(SelectionRectangle, _dragOrigin.Y);

            e.Pointer.Capture(OverlayCanvas);
        }

        private void OnPointerMoved(object? sender, PointerEventArgs e)
        {
            if (!_isDragging) return;

            var imageBounds = GetRenderedImageBounds();
            var current = ClampToRect(e.GetPosition(ImageHost), imageBounds);

            Canvas.SetLeft(SelectionRectangle, Math.Min(_dragOrigin.X, current.X));
            Canvas.SetTop(SelectionRectangle, Math.Min(_dragOrigin.Y, current.Y));
            SelectionRectangle.Width = Math.Abs(current.X - _dragOrigin.X);
            SelectionRectangle.Height = Math.Abs(current.Y - _dragOrigin.Y);
        }

        private void OnPointerReleased(object? sender, PointerReleasedEventArgs e)
        {
            if (!_isDragging) return;
            _isDragging = false;
            e.Pointer.Capture(null);

            var imageBounds = GetRenderedImageBounds();
            if (imageBounds.Width <= 0)
            {
                ClearSelection();
                return;
            }

            var left = Canvas.GetLeft(SelectionRectangle);
            var top = Canvas.GetTop(SelectionRectangle);
            var scale = imageBounds.Width / _current.Size.Width;

            var region = new System.Drawing.Rectangle(
                (int)Math.Round((left - imageBounds.X) / scale),
                (int)Math.Round((top - imageBounds.Y) / scale),
                (int)Math.Round(SelectionRectangle.Width / scale),
                (int)Math.Round(SelectionRectangle.Height / scale));

            if (region.Width < 4 || region.Height < 4)
            {
                ClearSelection();
                return;
            }

            _selection = region;
            CropButton.IsEnabled = true;
            SetStatus($"Selection: {region.Width} x {region.Height} px");
        }

        private void ClearSelection()
        {
            _isDragging = false;
            _selection = null;
            SelectionRectangle.IsVisible = false;
            SelectionRectangle.Width = 0;
            SelectionRectangle.Height = 0;
            CropButton.IsEnabled = false;
        }

        #endregion

        #region Commands

        private void OnCropClick(object? sender, RoutedEventArgs e)
        {
            if (_selection is not { } region) return;

            try
            {
                var cropped = _screenshotService.Crop(_current, region);
                ReplaceCurrent(cropped);
                SetStatus($"Cropped to {(int)cropped.Size.Width} x {(int)cropped.Size.Height} px");
            }
            catch (Exception ex)
            {
                SetStatus($"Crop failed: {ex.Message}", isError: true);
            }
        }

        private void OnResetClick(object? sender, RoutedEventArgs e)
        {
            if (ReferenceEquals(_current, _original)) return;

            ReplaceCurrent(_original);
            SetStatus("Restored the original capture");
        }

        private void OnCopyClick(object? sender, RoutedEventArgs e) => CopyToClipboard();

        private async void OnSaveAsClick(object? sender, RoutedEventArgs e) => await SaveAsAsync();

        private void OnCloseClick(object? sender, RoutedEventArgs e) => Close();

        private void CopyToClipboard()
        {
            try
            {
                var owner = TryGetPlatformHandle()?.Handle ?? IntPtr.Zero;
                _screenshotService.CopyToClipboard(_current, owner);
                SetStatus("Copied to clipboard");
            }
            catch (Exception ex)
            {
                SetStatus($"Copy failed: {ex.Message}", isError: true);
            }
        }

        private async System.Threading.Tasks.Task SaveAsAsync()
        {
            var options = new FilePickerSaveOptions
            {
                Title = "Save Screenshot",
                SuggestedFileName = ScreenshotService.BuildDefaultFileName(),
                DefaultExtension = "png",
                ShowOverwritePrompt = true,
                FileTypeChoices = new List<FilePickerFileType>
                {
                    new("PNG Image") { Patterns = new[] { "*.png" }, MimeTypes = new[] { "image/png" } },
                    new("JPEG Image") { Patterns = new[] { "*.jpg", "*.jpeg" }, MimeTypes = new[] { "image/jpeg" } },
                    new("Bitmap Image") { Patterns = new[] { "*.bmp" }, MimeTypes = new[] { "image/bmp" } }
                }
            };

            if (!string.IsNullOrWhiteSpace(_defaultFolder) && Directory.Exists(_defaultFolder))
            {
                options.SuggestedStartLocation = await StorageProvider.TryGetFolderFromPathAsync(_defaultFolder);
            }

            var file = await StorageProvider.SaveFilePickerAsync(options);
            var path = file?.TryGetLocalPath();
            if (string.IsNullOrEmpty(path)) return;

            try
            {
                _screenshotService.Save(_current, path);
                SetStatus($"Saved to {path}");
            }
            catch (Exception ex)
            {
                SetStatus($"Save failed: {ex.Message}", isError: true);
            }
        }

        private void ReplaceCurrent(Bitmap bitmap)
        {
            var previous = _current;
            _current = bitmap;

            if (!ReferenceEquals(previous, _original) && !ReferenceEquals(previous, bitmap))
            {
                previous.Dispose();
            }

            UpdatePreview();
        }

        private void SetStatus(string message, bool isError = false)
        {
            StatusText.Text = message;
            StatusText.Foreground = isError
                ? Avalonia.Media.Brushes.IndianRed
                : new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse("#4EC9B0"));
        }

        #endregion

        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                if (_selection != null || _isDragging)
                {
                    ClearSelection();
                }
                else
                {
                    Close();
                }
                e.Handled = true;
            }
            else if (e.Key == Key.C && e.KeyModifiers == KeyModifiers.Control)
            {
                CopyToClipboard();
                e.Handled = true;
            }
            else if (e.Key == Key.S && e.KeyModifiers == KeyModifiers.Control)
            {
                _ = SaveAsAsync();
                e.Handled = true;
            }
            else if (e.Key == Key.Enter && _selection != null)
            {
                OnCropClick(this, new RoutedEventArgs());
                e.Handled = true;
            }

            base.OnKeyDown(e);
        }

        protected override void OnClosed(EventArgs e)
        {
            base.OnClosed(e);

            PreviewImage.Source = null;
            _preview?.Dispose();
            _preview = null;

            if (!ReferenceEquals(_current, _original))
            {
                _current.Dispose();
            }
            _original.Dispose();
        }
    }
}
#endif