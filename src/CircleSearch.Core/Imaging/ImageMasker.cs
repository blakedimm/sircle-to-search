using System;
using System.Collections.Generic;
using CircleSearch.Core.Math;
using CircleSearch.Core.Models;

namespace CircleSearch.Core.Imaging;

public static class ImageMasker
{
    public static byte[] ExtractAndMask(
        ReadOnlySpan<byte> sourceBgra,
        int sourceStride,
        SelectionBounds bounds,
        IReadOnlyList<Point2D>? polygon = null)
    {
        if (!bounds.IsValid)
            return Array.Empty<byte>();

        int targetStride = bounds.Width * 4;
        byte[] output = new byte[targetStride * bounds.Height];

        bool usePolygonMask = polygon != null && polygon.Count >= 3;

        for (int y = 0; y < bounds.Height; y++)
        {
            int srcY = bounds.Y + y;
            int srcRowOffset = srcY * sourceStride + bounds.X * 4;
            int dstRowOffset = y * targetStride;

            for (int x = 0; x < bounds.Width; x++)
            {
                int srcPixel = srcRowOffset + x * 4;
                int dstPixel = dstRowOffset + x * 4;

                int globalX = bounds.X + x;
                int globalY = bounds.Y + y;

                bool isInside = !usePolygonMask || IsPointInPolygon(globalX, globalY, polygon!);

                if (isInside)
                {
                    output[dstPixel]     = sourceBgra[srcPixel];     // B
                    output[dstPixel + 1] = sourceBgra[srcPixel + 1]; // G
                    output[dstPixel + 2] = sourceBgra[srcPixel + 2]; // R
                    output[dstPixel + 3] = 255;                      // A
                }
                else
                {
                    // Пиксели вне контура зануляются
                    output[dstPixel]     = 0;
                    output[dstPixel + 1] = 0;
                    output[dstPixel + 2] = 0;
                    output[dstPixel + 3] = 0;
                }
            }
        }

        return output;
    }

    private static bool IsPointInPolygon(double x, double y, IReadOnlyList<Point2D> polygon)
    {
        bool inside = false;
        int j = polygon.Count - 1;

        for (int i = 0; i < polygon.Count; i++)
        {
            var pi = polygon[i];
            var pj = polygon[j];

            if (((pi.Y > y) != (pj.Y > y)) &&
                (x < (pj.X - pi.X) * (y - pi.Y) / (pj.Y - pi.Y) + pi.X))
            {
                inside = !inside;
            }
            j = i;
        }

        return inside;
    }
}