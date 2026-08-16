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

        private const int WM_HOTKEY = 0x0312;
        private const int HOTKEY_ID = 9000;

        public event EventHandler? HotkeyPressed;
        private Thread? _hotkeyThread;
        private bool _isRunning;
        private uint _currentModifiers;
        private uint _currentKey;

        public void RegisterHotkey(string hotkeyString)
        {
            Unregister();
            
            uint modifiers = 0;
            uint key = 0;

            var parts = hotkeyString.ToUpper().Split('+');
            foreach (var part in parts)
            {
                switch (part.Trim())
                {
                    case "ALT": modifiers |= 0x0001; break;
                    case "CTRL": modifiers |= 0x0002; break;
                    case "SHIFT": modifiers |= 0x0004; break;
                    case "WIN": modifiers |= 0x0008; break;
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

            if (key == 0) return;

            _currentModifiers = modifiers;
            _currentKey = key;

            _isRunning = true;
            _hotkeyThread = new Thread(HotkeyLoop) { IsBackground = true };
            _hotkeyThread.Start();
        }

        private void HotkeyLoop()
        {
            if (!RegisterHotKey(IntPtr.Zero, HOTKEY_ID, _currentModifiers, _currentKey))
            {
                return;
            }

            while (_isRunning)
            {
                int ret = GetMessage(out MSG msg, IntPtr.Zero, 0, 0);
                if (ret == 0 || ret == -1)
                {
                    break;
                }

                if (msg.message == WM_HOTKEY && msg.wParam.ToInt32() == HOTKEY_ID)
                {
                    Dispatcher.UIThread.Post(() => HotkeyPressed?.Invoke(this, EventArgs.Empty));
                }
            }

            UnregisterHotKey(IntPtr.Zero, HOTKEY_ID);
        }

        public void Unregister()
        {
            _isRunning = false;
            UnregisterHotKey(IntPtr.Zero, HOTKEY_ID);
        }
    }
}