using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ScreenRecorder.Models;
using ScreenRecorder.Services;
using ScreenRecorder.Views;

namespace ScreenRecorder.ViewModels;

public partial class MainViewModel : ViewModelBase
{
    private readonly SettingsService _settingsService;
    private readonly GlobalHotkeyService _hotkeyService;
    private readonly RecorderService _recorderService;
    private readonly ScreenshotService _screenshotService = new();

    [ObservableProperty]
    private SettingsModel _settings;

    [ObservableProperty]
    private string _statusMessage = "Ready";

    public ObservableCollection<int> AvailableFps { get; }

    public MainViewModel(SettingsService settingsService, GlobalHotkeyService hotkeyService, RecorderService recorderService)
    {
        _settingsService = settingsService;
        _hotkeyService = hotkeyService;
        _recorderService = recorderService;

        AvailableFps = new ObservableCollection<int>(Enumerable.Range(5, 56));
        _settings = _settingsService.CurrentSettings;
        _settings.PropertyChanged += OnSettingsPropertyChanged;

        _hotkeyService.RecordHotkeyPressed += OnRecordHotkeyPressed;
        _hotkeyService.ScreenshotHotkeyPressed += OnScreenshotHotkeyPressed;
        _recorderService.RecordingStarted += (s, e) => StatusMessage = "Recording Started...";
        _recorderService.RecordingStopped += (s, e) =>
        {
            StatusMessage = "Recording Stopped.";
            var filePath = _recorderService.LastRecordedFilePath;
            if (!string.IsNullOrEmpty(filePath))
            {
                Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                {
                    var notification = new NotificationWindow(filePath);
                    notification.Show();
                });
            }
        };
        _recorderService.RecordingFailed += (s, err) => StatusMessage = $"Recording Failed: {err}";

        // Synchronize registry startup setting on startup
        ApplyStartupSetting();
    }

    public MainViewModel()
    {
        _settingsService = new SettingsService();
        _hotkeyService = new GlobalHotkeyService();
        _recorderService = new RecorderService();
        AvailableFps = new ObservableCollection<int>(Enumerable.Range(5, 56));
        _settings = _settingsService.CurrentSettings;
        _settings.PropertyChanged += OnSettingsPropertyChanged;
    }

    private void OnSettingsPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        SaveSettings();
    }

    private void ApplyStartupSetting()
    {
        if (System.OperatingSystem.IsWindows())
        {
            try
            {
                using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run", true);
                if (key != null)
                {
                    if (Settings.StartOnStartup)
                    {
                        string? exePath = System.Environment.ProcessPath;
                        if (!string.IsNullOrEmpty(exePath))
                        {
                            key.SetValue("ScreenRecorder", exePath);
                        }
                    }
                    else
                    {
                        key.DeleteValue("ScreenRecorder", false);
                    }
                }
            }
            catch (System.Exception ex)
            {
                StatusMessage = $"Startup registry update failed: {ex.Message}";
            }
        }
    }

    private void OnRecordHotkeyPressed(object? sender, System.EventArgs e)
    {
        ToggleRecording();
    }

    private void OnScreenshotHotkeyPressed(object? sender, System.EventArgs e)
    {
        CaptureScreenshot();
    }

    /// <summary>
    /// Grabs the screen without showing anything, so callers can hide their own window
    /// first and open the editor afterwards.
    /// </summary>
    public System.Drawing.Bitmap? CaptureScreenshotImage()
    {
        try
        {
            return _screenshotService.CaptureVirtualScreen();
        }
        catch (System.Exception ex)
        {
            StatusMessage = $"Screenshot failed: {ex.Message}";
            return null;
        }
    }

    public void OpenScreenshotEditor(System.Drawing.Bitmap capture)
    {
        var window = new ScreenshotWindow(capture, Settings.OutputPath);
        window.Show();
        window.Activate();
        StatusMessage = "Screenshot captured.";
    }

    [RelayCommand]
    public void CaptureScreenshot()
    {
        var capture = CaptureScreenshotImage();
        if (capture != null)
        {
            OpenScreenshotEditor(capture);
        }
    }

    [RelayCommand]
    public void ToggleRecording()
    {
        if (_recorderService.IsRecording)
        {
            _recorderService.StopRecording();
        }
        else
        {
            _recorderService.StartRecording(Settings);
        }
    }

    [RelayCommand]
    public void SaveSettings()
    {
        _settingsService.Save();
        _hotkeyService.RegisterHotkeys(Settings.Hotkey, Settings.ScreenshotHotkey);
        ApplyStartupSetting();
    }
}