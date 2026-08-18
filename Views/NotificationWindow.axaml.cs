using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using System;
using System.Diagnostics;
using System.IO;

namespace ScreenRecorder.Views
{
    public partial class NotificationWindow : Window
    {
        private readonly string _filePath;

        public NotificationWindow()
        {
            InitializeComponent();
            _filePath = string.Empty;
        }

        public NotificationWindow(string filePath)
        {
            InitializeComponent();
            _filePath = filePath;
            
            // Auto close after 5 seconds
            DispatcherTimer.RunOnce(() => Close(), TimeSpan.FromSeconds(5));
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }

        protected override void OnOpened(EventArgs e)
        {
            base.OnOpened(e);

            var screens = Screens;
            var primaryScreen = screens?.Primary;
            if (primaryScreen != null)
            {
                var workingArea = primaryScreen.WorkingArea;
                var scaling = RenderScaling;
                var widthInPhys = (int)(Width * scaling);
                var heightInPhys = (int)(Height * scaling);

                // Position bottom-right of the working area (with a 20px padding)
                var x = workingArea.Right - widthInPhys - 20;
                var y = workingArea.Bottom - heightInPhys - 20;
                Position = new PixelPoint(x, y);
            }
        }

        private void OnNotificationClick(object? sender, PointerPressedEventArgs e)
        {
            var point = e.GetCurrentPoint(this);
            if (point.Properties.IsLeftButtonPressed)
            {
                OpenAndFocusFolder();
                Close();
            }
        }

        private void OnCloseClick(object? sender, RoutedEventArgs e)
        {
            Close();
            e.Handled = true;
        }

        private void OpenAndFocusFolder()
        {
            if (string.IsNullOrEmpty(_filePath) || !File.Exists(_filePath))
            {
                return;
            }

            try
            {
                if (System.OperatingSystem.IsWindows())
                {
                    Process.Start("explorer.exe", $"/select,\"{_filePath}\"");
                }
                else if (System.OperatingSystem.IsMacOS())
                {
                    Process.Start("open", $"-R \"{_filePath}\"");
                }
                else
                {
                    var directory = Path.GetDirectoryName(_filePath);
                    if (!string.IsNullOrEmpty(directory))
                    {
                        Process.Start(new ProcessStartInfo
                        {
                            FileName = "xdg-open",
                            Arguments = $"\"{directory}\"",
                            UseShellExecute = true
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to open folder: {ex.Message}");
            }
        }
    }
}
