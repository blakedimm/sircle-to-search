using System;
using System.Collections.Generic;

namespace CircleSearch.Core.Math;

public static class PathSimplifier
{
    public static List<Point2D> Simplify(IReadOnlyList<Point2D> points, double epsilon = 2.5)
    {
        if (points == null || points.Count < 3)
            return points != null ? new List<Point2D>(points) : new List<Point2D>();

        var result = new List<Point2D>();
        var keepMask = new bool[points.Count];
        keepMask[0] = true;
        keepMask[points.Count - 1] = true;

        RdpRecursive(points, 0, points.Count - 1, epsilon, keepMask);

        for (int i = 0; i < points.Count; i++)
        {
            if (keepMask[i])
            {
                result.Add(points[i]);
            }
        }

        return result;
    }

    private static void RdpRecursive(IReadOnlyList<Point2D> points, int start, int end, double epsilon, bool[] keepMask)
    {
        if (end <= start + 1)
            return;

        double maxDistance = 0;
        int maxIndex = start;

        Point2D lineStart = points[start];
        Point2D lineEnd = points[end];

        for (int i = start + 1; i < end; i++)
        {
            double dist = PerpendicularDistance(points[i], lineStart, lineEnd);
            if (dist > maxDistance)
            {
                maxDistance = dist;
                maxIndex = i;
            }
        }

        if (maxDistance > epsilon)
        {
            keepMask[maxIndex] = true;
            RdpRecursive(points, start, maxIndex, epsilon, keepMask);
            RdpRecursive(points, maxIndex, end, epsilon, keepMask);
        }
    }

    private static double PerpendicularDistance(Point2D pt, Point2D lineStart, Point2D lineEnd)
    {
        double dx = lineEnd.X - lineStart.X;
        double dy = lineEnd.Y - lineStart.Y;
        double lengthSquared = dx * dx + dy * dy;

        if (lengthSquared < 0.0001)
            return pt.DistanceTo(lineStart);

        double t = ((pt.X - lineStart.X) * dx + (pt.Y - lineStart.Y) * dy) / lengthSquared;
        t = System.Math.Clamp(t, 0.0, 1.0);

        Point2D projection = new(lineStart.X + t * dx, lineStart.Y + t * dy);
        return pt.DistanceTo(projection);
    }
}