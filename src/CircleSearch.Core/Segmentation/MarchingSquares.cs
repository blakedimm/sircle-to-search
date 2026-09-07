using System.Collections.Generic;
using CircleSearch.Core.Math;

namespace CircleSearch.Core.Segmentation;

public static class MarchingSquares
{
    // Трассировка внешнего контура бинарной маски
    public static List<Point2D> TraceContour(bool[,] mask, int offsetX, int offsetY)
    {
        int width = mask.GetLength(0);
        int height = mask.GetLength(1);
        var path = new List<Point2D>();

        // 1. Ищем стартовую граничную точку (сверху вниз, слева направо)
        int startX = -1, startY = -1;
        for (int y = 0; y < height && startX == -1; y++)
        {
            for (int x = 0; x < width; x++)
            {
                if (mask[x, y])
                {
                    startX = x;
                    startY = y;
                    break;
                }
            }
        }

        if (startX == -1)
            return path;

        // Направления шагов: 0 = Up, 1 = Right, 2 = Down, 3 = Left
        int curX = startX;
        int curY = startY;
        int prevStep = 3; // пришли слева
        int safetyLimit = width * height * 2;
        int steps = 0;

        do
        {
            path.Add(new Point2D(curX + offsetX, curY + offsetY));

            int cellValue = GetCellState(mask, curX, curY, width, height);
            int nextStep = ResolveNextStep(cellValue, prevStep);

            switch (nextStep)
            {
                case 0: curY--; break; // Up
                case 1: curX++; break; // Right
                case 2: curY++; break; // Down
                case 3: curX--; break; // Left
            }

            prevStep = nextStep;
            steps++;

        } while ((curX != startX || curY != startY) && steps < safetyLimit);

        return path;
    }

    private static int GetCellState(bool[,] mask, int x, int y, int w, int h)
    {
        int state = 0;
        if (IsSolid(mask, x - 1, y - 1, w, h)) state |= 1; // Top-Left
        if (IsSolid(mask, x,     y - 1, w, h)) state |= 2; // Top-Right
        if (IsSolid(mask, x,     y,     w, h)) state |= 4; // Bottom-Right
        if (IsSolid(mask, x - 1, y,     w, h)) state |= 8; // Bottom-Left
        return state;
    }

    private static bool IsSolid(bool[,] mask, int x, int y, int w, int h)
    {
        if (x < 0 || y < 0 || x >= w || y >= h) return false;
        return mask[x, y];
    }

    private static int ResolveNextStep(int state, int previousStep)
    {
        return state switch
        {
            1 => 0, // Up
            2 => 1, // Right
            3 => 1, // Right
            4 => 2, // Down
            5 => (previousStep == 0) ? 0 : 2, // Седловая точка
            6 => 2, // Down
            7 => 2, // Down
            8 => 3, // Left
            9 => 0, // Up
            10 => (previousStep == 1) ? 1 : 3, // Седловая точка
            11 => 1, // Right
            12 => 3, // Left
            13 => 0, // Up
            14 => 3, // Left
            _ => 1  // Fallback Right
        };
    }
}