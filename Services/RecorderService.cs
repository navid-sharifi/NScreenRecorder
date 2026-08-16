using System;
using System.IO;
using ScreenRecorderLib;
using ScreenRecorder.Models;

namespace ScreenRecorder.Services
{
    public class RecorderService
    {
        private Recorder? _recorder;
        public bool IsRecording { get; private set; }
        public string? LastRecordedFilePath { get; private set; }

        public event EventHandler? RecordingStarted;
        public event EventHandler? RecordingStopped;
        public event EventHandler<string>? RecordingFailed;

        public void StartRecording(SettingsModel settings, bool isAudioOnly = false)
        {
            if (IsRecording) return;

            string extension = settings.VoiceFormat == 0 ? "mp4" : "m4a";
            string fileName = isAudioOnly 
                ? $"VoiceRecord_{DateTime.Now:yyyyMMdd_HHmmss}.{extension}"
                : $"Record_{DateTime.Now:yyyyMMdd_HHmmss}.mp4";
            string filePath = Path.Combine(settings.OutputPath, fileName);
            LastRecordedFilePath = filePath;

            if (!Directory.Exists(settings.OutputPath))
            {
                Directory.CreateDirectory(settings.OutputPath);
            }

            AudioBitrate bitrate = settings.VoiceQuality switch
            {
                0 => AudioBitrate.bitrate_96kbps,
                1 => AudioBitrate.bitrate_128kbps,
                2 => AudioBitrate.bitrate_160kbps,
                3 => AudioBitrate.bitrate_192kbps,
                _ => AudioBitrate.bitrate_192kbps
            };

            RecorderOptions options = new RecorderOptions
            {
                OutputOptions = new OutputOptions
                {
                    RecorderMode = RecorderMode.Video
                },
                VideoEncoderOptions = new VideoEncoderOptions
                {
                    Framerate = isAudioOnly ? 5 : settings.Fps,
                    Quality = isAudioOnly ? 10 : settings.Quality,
                    Encoder = new H264VideoEncoder
                    {
                        BitrateMode = H264BitrateControlMode.Quality,
                        EncoderProfile = H264Profile.Main
                    }
                },
                AudioOptions = new AudioOptions
                {
                    IsAudioEnabled = isAudioOnly ? true : (settings.RecordSystemSound || settings.RecordMicrophone),
                    IsOutputDeviceEnabled = isAudioOnly 
                        ? (settings.AudioSource == 0 || settings.AudioSource == 1) 
                        : settings.RecordSystemSound,
                    IsInputDeviceEnabled = isAudioOnly 
                        ? (settings.AudioSource == 0 || settings.AudioSource == 2) 
                        : settings.RecordMicrophone,
                    Bitrate = isAudioOnly ? bitrate : AudioBitrate.bitrate_128kbps
                },
                MouseOptions = new MouseOptions
                {
                    IsMousePointerEnabled = !isAudioOnly
                }
            };

            if (!settings.IsFullScreen)
            {
                int width = settings.RegionRight - settings.RegionLeft;
                int height = settings.RegionBottom - settings.RegionTop;
                if (width > 0 && height > 0)
                {
                    options.OutputOptions.SourceRect = new ScreenRect(settings.RegionLeft, settings.RegionTop, width, height);
                }
            }

            _recorder = Recorder.CreateRecorder(options);
            _recorder.OnRecordingComplete += Recorder_OnRecordingComplete;
            _recorder.OnRecordingFailed += Recorder_OnRecordingFailed;

            _recorder.Record(filePath);
            IsRecording = true;
            RecordingStarted?.Invoke(this, EventArgs.Empty);
        }

        public void StopRecording()
        {
            if (!IsRecording || _recorder == null) return;
            
            _recorder.Stop();
        }

        private void Recorder_OnRecordingFailed(object? sender, RecordingFailedEventArgs e)
        {
            IsRecording = false;
            RecordingFailed?.Invoke(this, e.Error);
            Cleanup();
        }

        private void Recorder_OnRecordingComplete(object? sender, RecordingCompleteEventArgs e)
        {
            IsRecording = false;
            RecordingStopped?.Invoke(this, EventArgs.Empty);
            Cleanup();
        }

        private void Cleanup()
        {
            if (_recorder != null)
            {
                _recorder.OnRecordingComplete -= Recorder_OnRecordingComplete;
                _recorder.OnRecordingFailed -= Recorder_OnRecordingFailed;
                _recorder.Dispose();
                _recorder = null;
            }
        }
    }
}