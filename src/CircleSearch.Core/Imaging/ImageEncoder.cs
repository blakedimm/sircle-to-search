using System;
using System.Buffers.Binary;
using System.IO;
using System.IO.Compression;

namespace CircleSearch.Core.Imaging;

public static class ImageEncoder
{
    public static byte[] EncodeToJpegStream(byte[] bgraData, int width, int height, int quality = 90)
    {
        return EncodeToPngStream(bgraData, width, height);
    }

    public static byte[] EncodeToPngStream(byte[] bgraData, int width, int height)
    {
        if (bgraData == null || bgraData.Length == 0 || width <= 0 || height <= 0)
            return Array.Empty<byte>();

        using var ms = new MemoryStream();

        // 1. PNG Signature
        ms.Write([137, 80, 78, 71, 13, 10, 26, 10]);

        // 2. IHDR Chunk
        byte[] ihdr = new byte[13];
        BinaryPrimitives.WriteInt32BigEndian(ihdr.AsSpan(0, 4), width);
        BinaryPrimitives.WriteInt32BigEndian(ihdr.AsSpan(4, 4), height);
        ihdr[8] = 8;  // bit depth: 8 bits per channel
        ihdr[9] = 6;  // color type: RGBA
        ihdr[10] = 0; // compression
        ihdr[11] = 0; // filter method
        ihdr[12] = 0; // interlace: none
        WriteChunk(ms, "IHDR"u8, ihdr);

        // 3. IDAT Chunk: Raw RGBA сканлайны с filter byte 0
        int rawStride = width * 4;
        byte[] rawScanlines = new byte[(rawStride + 1) * height];

        for (int y = 0; y < height; y++)
        {
            int dstOffset = y * (rawStride + 1);
            rawScanlines[dstOffset] = 0; // Filter: None

            int srcOffset = y * rawStride;
            for (int x = 0; x < width; x++)
            {
                int pSrc = srcOffset + x * 4;
                int pDst = dstOffset + 1 + x * 4;

                // BGRA -> RGBA
                rawScanlines[pDst] = bgraData[pSrc + 2];     // R
                rawScanlines[pDst + 1] = bgraData[pSrc + 1]; // G
                rawScanlines[pDst + 2] = bgraData[pSrc];     // B
                rawScanlines[pDst + 3] = bgraData[pSrc + 3]; // A
            }
        }

        using var compressedStream = new MemoryStream();
        compressedStream.WriteByte(0x78);
        compressedStream.WriteByte(0x9C);

        using (var deflate = new DeflateStream(compressedStream, CompressionLevel.Fastest, leaveOpen: true))
        {
            deflate.Write(rawScanlines, 0, rawScanlines.Length);
        }

        uint adler = ComputeAdler32(rawScanlines);
        byte[] adlerBytes = new byte[4];
        BinaryPrimitives.WriteUInt32BigEndian(adlerBytes, adler);
        compressedStream.Write(adlerBytes, 0, 4);

        WriteChunk(ms, "IDAT"u8, compressedStream.ToArray());

        // 4. IEND Chunk
        WriteChunk(ms, "IEND"u8, ReadOnlySpan<byte>.Empty);

        return ms.ToArray();
    }

    private static void WriteChunk(Stream stream, ReadOnlySpan<byte> type, ReadOnlySpan<byte> data)
    {
        Span<byte> lengthBytes = stackalloc byte[4];
        BinaryPrimitives.WriteInt32BigEndian(lengthBytes, data.Length);
        stream.Write(lengthBytes);

        stream.Write(type);
        if (!data.IsEmpty)
        {
            stream.Write(data);
        }

        uint crc = CalculateCrc32(type, data);
        Span<byte> crcBytes = stackalloc byte[4];
        BinaryPrimitives.WriteUInt32BigEndian(crcBytes, crc);
        stream.Write(crcBytes);
    }

    private static uint ComputeAdler32(ReadOnlySpan<byte> data)
    {
        uint a = 1, b = 0;
        const uint mod = 65521;
        foreach (byte byteVal in data)
        {
            a = (a + byteVal) % mod;
            b = (b + a) % mod;
        }
        return (b << 16) | a;
    }

    private static uint CalculateCrc32(ReadOnlySpan<byte> type, ReadOnlySpan<byte> data)
    {
        uint crc = 0xFFFFFFFF;
        UpdateCrc(ref crc, type);
        UpdateCrc(ref crc, data);
        return ~crc;
    }

    private static void UpdateCrc(ref uint crc, ReadOnlySpan<byte> buffer)
    {
        foreach (byte b in buffer)
        {
            crc ^= b;
            for (int i = 0; i < 8; i++)
            {
                crc = (crc & 1) != 0 ? (crc >> 1) ^ 0xEDB88320 : (crc >> 1);
            }
        }
    }
}