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
            
            hotkeyService.RegisterHotkey(settingsService.CurrentSettings.Hotkey);

            _mainWindow = new MainWindow
            {
                DataContext = _mainViewModel
            };

            // Intentionally not setting desktop.MainWindow = _mainWindow 
            // so the application starts minimized (hidden).
        }

        base.OnFrameworkInitializationCompleted();
    }

    private void OnOptionsClicked(object? sender, EventArgs e)
    {
        if (_mainWindow != null)
        {
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