using System;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;

namespace ScreenRecorder.Models
{
    public partial class SettingsModel : ObservableObject
    {
        [ObservableProperty]
        private string _hotkey = "Alt+S";

        [ObservableProperty]
        private int _fps = 30;

        [ObservableProperty]
        private int _quality = 80;

        [ObservableProperty]
        private string _outputPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");

        [ObservableProperty]
        private bool _isFullScreen = true;

        [ObservableProperty]
        private bool _startOnStartup = false;

        [ObservableProperty]
        private int _regionLeft = 0;

        [ObservableProperty]
        private int _regionTop = 0;

        [ObservableProperty]
        private int _regionRight = 1920;

        [ObservableProperty]
        private int _regionBottom = 1080;
    }
}