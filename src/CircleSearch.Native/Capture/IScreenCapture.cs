using System;

namespace CircleSearch.Native.Capture;

public interface IScreenCapture : IDisposable
{
    CapturedFrame CaptureVirtualScreen();
}