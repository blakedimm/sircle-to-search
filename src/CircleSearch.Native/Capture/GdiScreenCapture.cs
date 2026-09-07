using System;
using System.Runtime.InteropServices;
using CircleSearch.Native.Win32;

namespace CircleSearch.Native.Capture;

public sealed class GdiScreenCapture : IScreenCapture
{
    public CapturedFrame CaptureVirtualScreen()
    {
        int left = User32.GetSystemMetrics(User32.SM_XVIRTUALSCREEN);
        int top = User32.GetSystemMetrics(User32.SM_YVIRTUALSCREEN);
        int width = User32.GetSystemMetrics(User32.SM_CXVIRTUALSCREEN);
        int height = User32.GetSystemMetrics(User32.SM_CYVIRTUALSCREEN);

        int stride = width * 4;
        byte[] buffer = new byte[stride * height];

        IntPtr desktopHwnd = User32.GetDesktopWindow();
        IntPtr hdcSrc = User32.GetDC(desktopHwnd);
        IntPtr hdcDest = User32.CreateCompatibleDC(hdcSrc);
        IntPtr hBitmap = User32.CreateCompatibleBitmap(hdcSrc, width, height);
        IntPtr hOldBmp = User32.SelectObject(hdcDest, hBitmap);

        try
        {
            User32.BitBlt(hdcDest, 0, 0, width, height, hdcSrc, left, top, User32.SRCCOPY);

            User32.BITMAPINFO bmi = new()
            {
                bmiHeader = new User32.BITMAPINFOHEADER
                {
                    biSize = (uint)Marshal.SizeOf<User32.BITMAPINFOHEADER>(),
                    biWidth = width,
                    biHeight = -height, // Top-down
                    biPlanes = 1,
                    biBitCount = 32,
                    biCompression = 0 // BI_RGB
                }
            };

            User32.GetDIBits(hdcDest, hBitmap, 0, (uint)height, buffer, ref bmi, User32.DIB_RGB_COLORS);
        }
        finally
        {
            User32.SelectObject(hdcDest, hOldBmp);
            User32.DeleteObject(hBitmap);
            User32.DeleteDC(hdcDest);
            User32.ReleaseDC(desktopHwnd, hdcSrc);
        }

        return new CapturedFrame(buffer, width, height, stride, left, top);
    }

    public void Dispose() { }
}