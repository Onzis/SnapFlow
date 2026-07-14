using System;
using System.Runtime.InteropServices;
using System.Windows.Interop;

namespace Screenshoter
{
    // Регистрация глобальной горячей клавиши через невидимое окно-приёмник WM_HOTKEY.
    public sealed class HotkeyManager : IDisposable
    {
        [Flags]
        public enum Mod : uint { Alt = 0x1, Control = 0x2, Shift = 0x4, Win = 0x8 }

        private const int WM_HOTKEY = 0x0312;
        private const int HOTKEY_ID = 0xB00B;
        private static readonly IntPtr HWND_MESSAGE = new IntPtr(-3);

        private readonly HwndSource _source;
        private readonly IntPtr _handle;
        public event Action? HotkeyPressed;

        public HotkeyManager(Mod modifiers, uint virtualKey)
        {
            var parameters = new HwndSourceParameters("ScreenshoterHotkeyHost")
            {
                Width = 0,
                Height = 0,
                WindowStyle = 0,
                ParentWindow = HWND_MESSAGE
            };
            _source = new HwndSource(parameters);
            _handle = _source.Handle;
            _source.AddHook(WndProc);

            if (!RegisterHotKey(_handle, HOTKEY_ID, (uint)modifiers, virtualKey))
            {
                int err = Marshal.GetLastWin32Error();
                _source.RemoveHook(WndProc);
                _source.Dispose();
                throw new InvalidOperationException(
                    $"Не удалось зарегистрировать горячую клавишу (код {err}). Возможно, она уже занята.");
            }
        }

        private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (msg == WM_HOTKEY && wParam.ToInt32() == HOTKEY_ID)
            {
                HotkeyPressed?.Invoke();
                handled = true;
            }
            return IntPtr.Zero;
        }

        public void Dispose()
        {
            UnregisterHotKey(_handle, HOTKEY_ID);
            _source.RemoveHook(WndProc);
            _source.Dispose();
        }

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

        [DllImport("user32.dll")]
        private static extern bool UnregisterHotKey(IntPtr hWnd, int id);
    }
}
