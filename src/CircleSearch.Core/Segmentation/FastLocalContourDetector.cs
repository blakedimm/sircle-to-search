using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using CircleSearch.Core.Math;
using CircleSearch.Core.Models;

namespace CircleSearch.Core.Segmentation;

public sealed class FastLocalContourDetector : IContourDetector
{
    public ValueTask<ContourResult> DetectContourAsync(
        ReadOnlyMemory<byte> bgraBuffer,
        int stride,
        int screenWidth,
        int screenHeight,
        Point2D clickPoint,
        double tolerance = 38.0,
        int roiRadius = 180)
    {
        int cx = (int)System.Math.Round(clickPoint.X);
        int cy = (int)System.Math.Round(clickPoint.Y);

        if (cx < 0 || cy < 0 || cx >= screenWidth || cy >= screenHeight)
            return ValueTask.FromResult(ContourResult.Failed("Click point out of bounds."));

        // 1. Ограничиваем локальную область поиска (ROI)
        int minX = System.Math.Max(0, cx - roiRadius);
        int maxX = System.Math.Min(screenWidth - 1, cx + roiRadius);
        int minY = System.Math.Max(0, cy - roiRadius);
        int maxY = System.Math.Min(screenHeight - 1, cy + roiRadius);

        int roiW = maxX - minX + 1;
        int roiH = maxY - minY + 1;

        var span = bgraBuffer.Span;

        // Базовый цвет в точке клика (BGRA формат)
        int clickOffset = cy * stride + cx * 4;
        byte seedB = span[clickOffset];
        byte seedG = span[clickOffset + 1];
        byte seedR = span[clickOffset + 2];

        bool[,] mask = new bool[roiW, roiH];
        bool[,] visited = new bool[roiW, roiH];

        // 2. Локальный BFS (Flood-fill) по цветовой разнице
        var queue = new Queue<(int rx, int ry)>();
        int startRx = cx - minX;
        int startRy = cy - minY;

        queue.Enqueue((startRx, startRy));
        visited[startRx, startRy] = true;
        mask[startRx, startRy] = true;

        Span<(int dx, int dy)> directions = stackalloc (int, int)[]
        {
            (-1, 0), (1, 0), (0, -1), (0, 1)
        };

        int solidPixels = 0;

        while (queue.Count > 0)
        {
            var (rx, ry) = queue.Dequeue();
            solidPixels++;

            foreach (var (dx, dy) in directions)
            {
                int nx = rx + dx;
                int ny = ry + dy;

                if (nx < 0 || ny < 0 || nx >= roiW || ny >= roiH || visited[nx, ny])
                    continue;

                visited[nx, ny] = true;

                int globalX = minX + nx;
                int globalY = minY + ny;
                int pixelOffset = globalY * stride + globalX * 4;

                byte b = span[pixelOffset];
                byte g = span[pixelOffset + 1];
                byte r = span[pixelOffset + 2];

                double diff = ColorDistance.Calculate(seedR, seedG, seedB, r, g, b);
                if (diff <= tolerance)
                {
                    mask[nx, ny] = true;
                    queue.Enqueue((nx, ny));
                }
            }
        }

        if (solidPixels < 16)
            return ValueTask.FromResult(ContourResult.Failed("Segmented object too small."));

        // 3. Извлечение контура через Marching Squares
        var rawContour = MarchingSquares.TraceContour(mask, minX, minY);
        if (rawContour.Count < 4)
            return ValueTask.FromResult(ContourResult.Failed("Contour extraction failed."));

        // 4. Сглаживание и упрощение
        var simplified = PathSimplifier.Simplify(rawContour, epsilon: 2.0);
        var smooth = SplineInterpolator.CreateSmoothPath(simplified, subdivisionsPerSegment: 4, isClosed: true);

        var bounds = SelectionBounds.FromPoints(smooth.ToArray());
        return ValueTask.FromResult(new ContourResult(smooth, bounds, true));
    }
}