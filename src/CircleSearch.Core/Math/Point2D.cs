using System;

namespace CircleSearch.Core.Math;

public readonly record struct Point2D(double X, double Y)
{
    public static readonly Point2D Zero = new(0, 0);

    public double DistanceTo(Point2D other)
    {
        double dx = X - other.X;
        double dy = Y - other.Y;
        return System.Math.Sqrt(dx * dx + dy * dy);
    }

    public double DistanceSquaredTo(Point2D other)
    {
        double dx = X - other.X;
        double dy = Y - other.Y;
        return dx * dx + dy * dy;
    }

    public static Point2D operator +(Point2D a, Point2D b) => new(a.X + b.X, a.Y + b.Y);
    public static Point2D operator -(Point2D a, Point2D b) => new(a.X - b.X, a.Y - b.Y);
    public static Point2D operator *(Point2D p, double scalar) => new(p.X * scalar, p.Y * scalar);
}