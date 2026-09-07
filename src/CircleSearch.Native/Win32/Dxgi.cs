using System;
using System.Runtime.InteropServices;

namespace CircleSearch.Native.Win32;

public static class Dxgi
{
    public const uint DXGI_ERROR_WAIT_TIMEOUT = 0x887A0027;
    public const uint DXGI_ERROR_ACCESS_LOST = 0x887A0026;

    [StructLayout(LayoutKind.Sequential)]
    public struct DXGI_OUTDUPL_FRAME_INFO
    {
        public long TotalMetadataBufferSize;
        public uint LastPresentTime;
        public uint LastMouseUpdateTime;
        public uint AccumulatedFrames;
        public bool RectsCoalesced;
        public bool ProtectedContentMaskedOut;
        public uint PointerPosition;
        public uint TotalNumberOfRects;
    }
}