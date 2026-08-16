using System;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using ScreenRecorder.ViewModels;
using ScreenRecorder.Views;
using ScreenRecorder.Services;

namespace ScreenRecorder;

public partial class App : Application
{
    private MainWindow? _mainWindow;
    private MainViewModel? _mainViewModel;

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.ShutdownMode = Avalonia.Controls.ShutdownMode.OnExplicitShutdown;

            var settingsService = new SettingsService();
            var hotkeyService = new GlobalHotkeyService();
            var recorderService = new RecorderService();

            _mainViewModel = new MainViewModel(settingsService, hotkeyService, recorderService);
            
            hotkeyService.RegisterHotkeys(
                settingsService.CurrentSettings.Hotkey,
                settingsService.CurrentSettings.ScreenshotHotkey);

            recorderService.RecordingStarted += OnRecordingStarted;
            recorderService.RecordingStopped += OnRecordingStopped;

            _mainWindow = new MainWindow
            {
                DataContext = _mainViewModel
            };

            // Intentionally not setting desktop.MainWindow = _mainWindow 
            // so the application starts minimized (hidden).
        }

        base.OnFrameworkInitializationCompleted();
    }

    private void OnRecordingStarted(object? sender, EventArgs e)
    {
        UpdateAppIcon("recording-logo.ico");
    }

    private void OnRecordingStopped(object? sender, EventArgs e)
    {
        UpdateAppIcon("avalonia-logo.ico");
    }

    private void UpdateAppIcon(string assetName)
    {
        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            try
            {
                using var stream = Avalonia.Platform.AssetLoader.Open(new Uri($"avares://ScreenRecorder/Assets/{assetName}"));
                var icon = new Avalonia.Controls.WindowIcon(stream);

                if (_mainWindow != null)
                {
                    _mainWindow.Icon = icon;
                }

                var trayIcons = Avalonia.Controls.TrayIcon.GetIcons(this);
                if (trayIcons != null && trayIcons.Count > 0)
                {
                    trayIcons[0].Icon = icon;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to update application icon to {assetName}: {ex.Message}");
            }
        });
    }

    private void OnOptionsClicked(object? sender, EventArgs e)
    {
        if (_mainWindow != null)
        {
            _mainWindow.Hide();
            _mainWindow.Show();
            _mainWindow.WindowState = Avalonia.Controls.WindowState.Normal;
            _mainWindow.Activate();
        }
    }

    private void OnExitClicked(object? sender, EventArgs e)
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            _mainViewModel?.SaveSettings();
            desktop.Shutdown();
        }
    }
}