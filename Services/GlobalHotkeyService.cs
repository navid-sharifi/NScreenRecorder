using System;
using System.Runtime.InteropServices;
using System.Threading;
using Avalonia.Threading;

namespace ScreenRecorder.Services
{
    public class GlobalHotkeyService
    {
        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern int GetMessage(out MSG lpMsg, IntPtr hWnd, uint wMsgFilterMin, uint wMsgFilterMax);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool PeekMessage(out MSG lpMsg, IntPtr hWnd, uint wMsgFilterMin, uint wMsgFilterMax, uint wRemoveMsg);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool PostThreadMessage(uint idThread, uint msg, IntPtr wParam, IntPtr lParam);

        [DllImport("kernel32.dll")]
        private static extern uint GetCurrentThreadId();

        [StructLayout(LayoutKind.Sequential)]
        private struct MSG
        {
            public IntPtr hwnd;
            public uint message;
            public IntPtr wParam;
            public IntPtr lParam;
            public uint time;
            public POINT pt;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct POINT
        {
            public int x;
            public int y;
        }

        private const uint WM_HOTKEY = 0x0312;
        private const uint WM_QUIT = 0x0012;
        private const uint WM_APP_REFRESH = 0x8000; // WM_APP: re-read the requested hotkeys
        private const uint PM_NOREMOVE = 0x0000;

        private const uint MOD_ALT = 0x0001;
        private const uint MOD_CONTROL = 0x0002;
        private const uint MOD_SHIFT = 0x0004;
        private const uint MOD_WIN = 0x0008;
        private const uint MOD_NOREPEAT = 0x4000;

        private const int RECORD_HOTKEY_ID = 9000;
        private const int SCREENSHOT_HOTKEY_ID = 9001;

        /// <summary>Raised when the recording hotkey (default Alt+S) is pressed.</summary>
        public event EventHandler? RecordHotkeyPressed;

        /// <summary>Raised when the screenshot hotkey (default Alt+D) is pressed.</summary>
        public event EventHandler? ScreenshotHotkeyPressed;

        private readonly object _sync = new();
        private readonly ManualResetEventSlim _threadReady = new(false);
        private Thread? _hotkeyThread;
        private uint _hotkeyThreadId;
        private string _recordHotkey = string.Empty;
        private string _screenshotHotkey = string.Empty;

        /// <summary>
        /// Registers (or re-registers) both global hotkeys. Safe to call repeatedly; the
        /// underlying message loop thread is created once and reused.
        /// </summary>
        public void RegisterHotkeys(string recordHotkey, string screenshotHotkey)
        {
            lock (_sync)
            {
                _recordHotkey = recordHotkey ?? string.Empty;
                _screenshotHotkey = screenshotHotkey ?? string.Empty;

                if (_hotkeyThread == null || !_hotkeyThread.IsAlive)
                {
                    _threadReady.Reset();
                    _hotkeyThread = new Thread(HotkeyLoop) { IsBackground = true, Name = "GlobalHotkeys" };
                    _hotkeyThread.Start();
                    return;
                }
            }

            // The hotkeys must be (un)registered on the thread owning the message loop.
            if (_threadReady.Wait(TimeSpan.FromSeconds(2)))
            {
                PostThreadMessage(_hotkeyThreadId, WM_APP_REFRESH, IntPtr.Zero, IntPtr.Zero);
            }
        }

        private void HotkeyLoop()
        {
            // Force the message queue into existence before anyone posts to this thread.
            PeekMessage(out _, IntPtr.Zero, 0, 0, PM_NOREMOVE);
            _hotkeyThreadId = GetCurrentThreadId();
            _threadReady.Set();

            ApplyRegistrations();

            while (true)
            {
                int ret = GetMessage(out MSG msg, IntPtr.Zero, 0, 0);
                if (ret == 0 || ret == -1)
                {
                    break;
                }

                if (msg.message == WM_APP_REFRESH)
                {
                    ApplyRegistrations();
                }
                else if (msg.message == WM_HOTKEY)
                {
                    int id = msg.wParam.ToInt32();
                    if (id == RECORD_HOTKEY_ID)
                    {
                        Dispatcher.UIThread.Post(() => RecordHotkeyPressed?.Invoke(this, EventArgs.Empty));
                    }
                    else if (id == SCREENSHOT_HOTKEY_ID)
                    {
                        Dispatcher.UIThread.Post(() => ScreenshotHotkeyPressed?.Invoke(this, EventArgs.Empty));
                    }
                }
            }

            UnregisterHotKey(IntPtr.Zero, RECORD_HOTKEY_ID);
            UnregisterHotKey(IntPtr.Zero, SCREENSHOT_HOTKEY_ID);
            _threadReady.Reset();
        }

        private void ApplyRegistrations()
        {
            string record, screenshot;
            lock (_sync)
            {
                record = _recordHotkey;
                screenshot = _screenshotHotkey;
            }

            UnregisterHotKey(IntPtr.Zero, RECORD_HOTKEY_ID);
            UnregisterHotKey(IntPtr.Zero, SCREENSHOT_HOTKEY_ID);

            bool hasRecord = TryParseHotkey(record, out uint recordModifiers, out uint recordKey);
            if (hasRecord)
            {
                RegisterHotKey(IntPtr.Zero, RECORD_HOTKEY_ID, recordModifiers | MOD_NOREPEAT, recordKey);
            }

            if (TryParseHotkey(screenshot, out uint shotModifiers, out uint shotKey))
            {
                // Ignore a screenshot hotkey that collides with the recording one.
                bool collides = hasRecord && shotModifiers == recordModifiers && shotKey == recordKey;
                if (!collides)
                {
                    RegisterHotKey(IntPtr.Zero, SCREENSHOT_HOTKEY_ID, shotModifiers | MOD_NOREPEAT, shotKey);
                }
            }
        }

        private static bool TryParseHotkey(string hotkeyString, out uint modifiers, out uint key)
        {
            modifiers = 0;
            key = 0;

            if (string.IsNullOrWhiteSpace(hotkeyString)) return false;

            var parts = hotkeyString.ToUpper().Split('+');
            foreach (var part in parts)
            {
                switch (part.Trim())
                {
                    case "ALT": modifiers |= MOD_ALT; break;
                    case "CTRL": modifiers |= MOD_CONTROL; break;
                    case "CONTROL": modifiers |= MOD_CONTROL; break;
                    case "SHIFT": modifiers |= MOD_SHIFT; break;
                    case "WIN": modifiers |= MOD_WIN; break;
                    default:
                        if (Enum.TryParse<ConsoleKey>(part.Trim(), true, out var consoleKey))
                        {
                            key = (uint)consoleKey;
                        }
                        else if (part.Trim().Length == 1)
                        {
                            key = (uint)part.Trim()[0];
                        }
                        break;
                }
            }

            return key != 0;
        }

        public void Unregister()
        {
            lock (_sync)
            {
                if (_hotkeyThread == null || !_hotkeyThread.IsAlive) return;
            }

            if (_threadReady.Wait(TimeSpan.FromSeconds(2)))
            {
                PostThreadMessage(_hotkeyThreadId, WM_QUIT, IntPtr.Zero, IntPtr.Zero);
            }
        }
    }
}
