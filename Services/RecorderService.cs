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

        public void StartRecording(SettingsModel settings)
        {
            if (IsRecording) return;

            string fileName = $"Record_{DateTime.Now:yyyyMMdd_HHmmss}.mp4";
            string filePath = Path.Combine(settings.OutputPath, fileName);
            LastRecordedFilePath = filePath;

            if (!Directory.Exists(settings.OutputPath))
            {
                Directory.CreateDirectory(settings.OutputPath);
            }

            RecorderOptions options = new RecorderOptions
            {
                OutputOptions = new OutputOptions
                {
                    RecorderMode = RecorderMode.Video
                },
                VideoEncoderOptions = new VideoEncoderOptions
                {
                    Framerate = settings.Fps,
                    Quality = settings.Quality,
                    Encoder = new H264VideoEncoder
                    {
                        BitrateMode = H264BitrateControlMode.Quality,
                        EncoderProfile = H264Profile.Main
                    }
                },
                AudioOptions = new AudioOptions
                {
                    IsAudioEnabled = settings.RecordSystemSound || settings.RecordMicrophone,
                    IsOutputDeviceEnabled = settings.RecordSystemSound,
                    IsInputDeviceEnabled = settings.RecordMicrophone
                },
                MouseOptions = new MouseOptions
                {
                    IsMousePointerEnabled = true
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