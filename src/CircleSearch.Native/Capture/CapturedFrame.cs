using System;

namespace CircleSearch.Native.Capture;

public sealed class CapturedFrame : IDisposable
{
    public byte[] Data { get; }
    public int Width { get; }
    public int Height { get; }
    public int Stride { get; }
    public int VirtualLeft { get; }
    public int VirtualTop { get; }

    public CapturedFrame(byte[] data, int width, int height, int stride, int virtualLeft, int virtualTop)
    {
        Data = data;
        Width = width;
        Height = height;
        Stride = stride;
        VirtualLeft = virtualLeft;
        VirtualTop = virtualTop;
    }

    public void Dispose()
    {
        // Память управляется GC, задел под нативные unmanaged буферы
    }
}