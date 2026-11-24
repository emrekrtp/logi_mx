using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace MxBacklightControl.Services;

public sealed class GlobalHotkeyService : IDisposable
{
    private const int WmHotkey = 0x0312;
    private const int HotkeyId = 0x4D58; // "MX"
    private const uint ModAlt = 0x0001;
    private const uint ModControl = 0x0002;
    private const uint ModNoRepeat = 0x4000;
    private const uint VkL = 0x4C; // 'L' key

    private HwndSource? _source;
    private IntPtr _hwnd;
    private bool _registered;

    public event Action? HotkeyPressed;

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

    public void Attach(Window window, bool enable)
    {
        var helper = new WindowInteropHelper(window);
        _hwnd = helper.EnsureHandle();
        _source = HwndSource.FromHwnd(_hwnd);
        _source?.AddHook(WndProc);

        SetEnabled(enable);
    }

    public bool SetEnabled(bool enable)
    {
        if (_hwnd == IntPtr.Zero)
        {
            return false;
        }

        if (enable && !_registered)
        {
            _registered = RegisterHotKey(_hwnd, HotkeyId, ModControl | ModAlt | ModNoRepeat, VkL);
            return _registered;
        }

        if (!enable && _registered)
        {
            UnregisterHotKey(_hwnd, HotkeyId);
            _registered = false;
        }

        return true;
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == WmHotkey && wParam.ToInt32() == HotkeyId)
        {
            HotkeyPressed?.Invoke();
            handled = true;
        }
        return IntPtr.Zero;
    }

    public void Dispose()
    {
        if (_registered && _hwnd != IntPtr.Zero)
        {
            UnregisterHotKey(_hwnd, HotkeyId);
            _registered = false;
        }
        _source?.RemoveHook(WndProc);
        _source = null;
    }
}
