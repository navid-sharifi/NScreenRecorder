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
        private string _screenshotHotkey = "Alt+D";

        [ObservableProperty]
        private string _voiceRecordHotkey = "Alt+V";

        [ObservableProperty]
        private int _audioSource = 0; // 0 = Device and Microphone, 1 = Device Only, 2 = Microphone Only

        [ObservableProperty]
        private int _voiceFormat = 1; // 0 = MP4, 1 = M4A

        [ObservableProperty]
        private int _voiceQuality = 3; // 0=96kbps, 1=128kbps, 2=160kbps, 3=192kbps, 4=256kbps, 5=320kbps

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
        private bool _recordSystemSound = true;

        [ObservableProperty]
        private bool _recordMicrophone = true;

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