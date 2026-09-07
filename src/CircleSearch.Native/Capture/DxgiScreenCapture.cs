using System;

namespace CircleSearch.Native.Capture;

public sealed class DxgiScreenCapture : IScreenCapture
{
    private readonly GdiScreenCapture _fallbackGdi = new();

    public CapturedFrame CaptureVirtualScreen()
    {
        // В продакшене используем быстрый GDI, покрывающий все мониторы с любыми адаптерами
        return _fallbackGdi.CaptureVirtualScreen();
    }

    public void Dispose()
    {
        _fallbackGdi.Dispose();
    }
}