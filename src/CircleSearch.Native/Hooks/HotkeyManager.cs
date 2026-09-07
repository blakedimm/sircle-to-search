using System;
using System.Runtime.InteropServices;
using CircleSearch.Native.Win32;

namespace CircleSearch.Native.Hooks;

public sealed class HotkeyManager : IDisposable
{
    private const int HotkeyId = 9001;
    private const string WindowClassName = "CircleSearch_HotkeyReceiver";

    private IntPtr _hwnd = IntPtr.Zero;
    private WndProcDelegate? _wndProc;
    private bool _isRegistered;

    public event Action? HotkeyPressed;

    private delegate IntPtr WndProcDelegate(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    private struct WNDCLASSEX
    {
        public uint cbSize;
        public uint style;
        public IntPtr lpfnWndProc;
        public int cbClsExtra;
        public int cbWndExtra;
        public IntPtr hInstance;
        public IntPtr hIcon;
        public IntPtr hCursor;
        public IntPtr hbrBackground;
        public string? lpszMenuName;
        public string lpszClassName;
        public IntPtr hIconSm;
    }

    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
    private static extern ushort RegisterClassEx(ref WNDCLASSEX lpwcx);

    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
    private static extern IntPtr CreateWindowEx(
        uint dwExStyle, string lpClassName, string lpWindowName,
        uint dwStyle, int x, int y, int nWidth, int nHeight,
        IntPtr hWndParent, IntPtr hMenu, IntPtr hInstance, IntPtr lpParam);

    [DllImport("user32.dll")]
    private static extern bool DestroyWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern IntPtr DefWindowProc(IntPtr hWnd, uint uMsg, IntPtr wParam, IntPtr lParam);

    public bool Register(uint modifiers, uint vk)
    {
        Unregister();

        _wndProc = CustomWndProc;
        var hInstance = User32.GetModuleHandle(null);

        var wndClass = new WNDCLASSEX
        {
            cbSize = (uint)Marshal.SizeOf<WNDCLASSEX>(),
            lpfnWndProc = Marshal.GetFunctionPointerForDelegate(_wndProc),
            hInstance = hInstance,
            lpszClassName = WindowClassName + Guid.NewGuid().ToString("N")
        };

        RegisterClassEx(ref wndClass);

        // HWND_MESSAGE = -3 (окно только для сообщений, не отображается)
        _hwnd = CreateWindowEx(0, wndClass.lpszClassName, "Receiver", 0, 0, 0, 0, 0, new IntPtr(-3), IntPtr.Zero, hInstance, IntPtr.Zero);

        if (_hwnd == IntPtr.Zero)
            return false;

        _isRegistered = User32.RegisterHotKey(_hwnd, HotkeyId, modifiers | User32.MOD_NOREPEAT, vk);
        return _isRegistered;
    }

    public void Unregister()
    {
        if (_isRegistered && _hwnd != IntPtr.Zero)
        {
            User32.UnregisterHotKey(_hwnd, HotkeyId);
            _isRegistered = false;
        }

        if (_hwnd != IntPtr.Zero)
        {
            DestroyWindow(_hwnd);
            _hwnd = IntPtr.Zero;
        }
    }

    private IntPtr CustomWndProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam)
    {
        if (msg == User32.WM_HOTKEY && wParam.ToInt32() == HotkeyId)
        {
            HotkeyPressed?.Invoke();
            return IntPtr.Zero;
        }

        return DefWindowProc(hWnd, msg, wParam, lParam);
    }

    public void Dispose()
    {
        Unregister();
    }
}