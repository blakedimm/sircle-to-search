using System;
using CircleSearch.Core.Math;

namespace CircleSearch.Core.Models;

public readonly record struct SelectionBounds(int X, int Y, int Width, int Height)
{
    public static readonly SelectionBounds Empty = new(0, 0, 0, 0);

    public int Right => X + Width;
    public int Bottom => Y + Height;
    public bool IsValid => Width > 4 && Height > 4;

    public static SelectionBounds FromPoints(ReadOnlySpan<Point2D> points)
    {
        if (points.IsEmpty)
            return Empty;

        double minX = points[0].X;
        double maxX = points[0].X;
        double minY = points[0].Y;
        double maxY = points[0].Y;

        for (int i = 1; i < points.Length; i++)
        {
            var p = points[i];
            if (p.X < minX) minX = p.X;
            if (p.X > maxX) maxX = p.X;
            if (p.Y < minY) minY = p.Y;
            if (p.Y > maxY) maxY = p.Y;
        }

        int x = (int)System.Math.Floor(minX);
        int y = (int)System.Math.Floor(minY);
        int w = (int)System.Math.Ceiling(maxX - minX);
        int h = (int)System.Math.Ceiling(maxY - minY);

        return new SelectionBounds(x, y, System.Math.Max(0, w), System.Math.Max(0, h));
    }
}