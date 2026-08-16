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

        _hotkeyService.HotkeyPressed += OnHotkeyPressed;
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
    }

    public MainViewModel()
    {
        _settingsService = new SettingsService();
        _hotkeyService = new GlobalHotkeyService();
        _recorderService = new RecorderService();
        AvailableFps = new ObservableCollection<int>(Enumerable.Range(5, 56));
        _settings = _settingsService.CurrentSettings;
    }

    private void OnHotkeyPressed(object? sender, System.EventArgs e)
    {
        ToggleRecording();
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
        _hotkeyService.RegisterHotkey(Settings.Hotkey);
    }
}