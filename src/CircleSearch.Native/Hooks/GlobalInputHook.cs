using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using CircleSearch.Core.Math;
using CircleSearch.Native.Win32;

namespace CircleSearch.Native.Hooks;

public sealed class GlobalInputHook : IDisposable
{
    private IntPtr _hookId = IntPtr.Zero;
    private User32.LowLevelMouseProc? _mouseProc;

    public event Action<Point2D>? MouseDown;
    public event Action<Point2D>? MouseMove;
    public event Action<Point2D>? MouseUp;
    public event Action? RightMouseDown;

    public void Install()
    {
        if (_hookId != IntPtr.Zero) return;

        _mouseProc = HookCallback;
        using var curProcess = Process.GetCurrentProcess();
        using var curModule = curProcess.MainModule;

        IntPtr modHandle = User32.GetModuleHandle(curModule?.ModuleName);
        _hookId = User32.SetWindowsHookEx(User32.WH_MOUSE_LL, _mouseProc, modHandle, 0);
    }

    public void Uninstall()
    {
        if (_hookId != IntPtr.Zero)
        {
            User32.UnhookWindowsHookEx(_hookId);
            _hookId = IntPtr.Zero;
            _mouseProc = null;
        }
    }

    private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0)
        {
            var hookStruct = Marshal.PtrToStructure<User32.MSLLHOOKSTRUCT>(lParam);
            Point2D pt = new(hookStruct.pt.x, hookStruct.pt.y);

            int msg = wParam.ToInt32();
            switch (msg)
            {
                case User32.WM_LBUTTONDOWN:
                    MouseDown?.Invoke(pt);
                    break;
                case User32.WM_MOUSEMOVE:
                    MouseMove?.Invoke(pt);
                    break;
                case User32.WM_LBUTTONUP:
                    MouseUp?.Invoke(pt);
                    break;
                case User32.WM_RBUTTONDOWN:
                    RightMouseDown?.Invoke();
                    break;
            }
        }

        return User32.CallNextHookEx(_hookId, nCode, wParam, lParam);
    }

    public void Dispose()
    {
        Uninstall();
    }
}