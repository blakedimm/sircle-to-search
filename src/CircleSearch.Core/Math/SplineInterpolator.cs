using System;
using System.Collections.Generic;

namespace CircleSearch.Core.Math;

public static class SplineInterpolator
{
    private const double Alpha = 0.5; // Centripetal parameter: prevents loops and cusp self-intersections

    public static List<Point2D> CreateSmoothPath(IReadOnlyList<Point2D> points, int subdivisionsPerSegment = 8, bool isClosed = false)
    {
        var smoothed = new List<Point2D>();
        if (points == null || points.Count < 2)
            return smoothed;

        if (points.Count == 2)
        {
            smoothed.AddRange(points);
            return smoothed;
        }

        int count = points.Count;
        int segments = isClosed ? count : count - 1;

        for (int i = 0; i < segments; i++)
        {
            Point2D p0 = isClosed ? points[(i - 1 + count) % count] : (i > 0 ? points[i - 1] : points[0]);
            Point2D p1 = points[i];
            Point2D p2 = isClosed ? points[(i + 1) % count] : points[i + 1];
            Point2D p3 = isClosed ? points[(i + 2) % count] : (i + 2 < count ? points[i + 2] : p2);

            for (int step = 0; step < subdivisionsPerSegment; step++)
            {
                double t = step / (double)subdivisionsPerSegment;
                smoothed.Add(EvaluateCatmullRom(p0, p1, p2, p3, t));
            }
        }

        if (!isClosed)
        {
            smoothed.Add(points[^1]);
        }

        return smoothed;
    }

    private static Point2D EvaluateCatmullRom(Point2D p0, Point2D p1, Point2D p2, Point2D p3, double t)
    {
        double t0 = 0.0;
        double t1 = GetT(t0, p0, p1);
        double t2 = GetT(t1, p1, p2);
        double t3 = GetT(t2, p2, p3);

        if (System.Math.Abs(t2 - t1) < 1e-6)
            return p1;

        double currentT = t1 + t * (t2 - t1);

        Point2D a1 = Remap(t0, t1, p0, p1, currentT);
        Point2D a2 = Remap(t1, t2, p1, p2, currentT);
        Point2D a3 = Remap(t2, t3, p2, p3, currentT);

        Point2D b1 = Remap(t0, t2, a1, a2, currentT);
        Point2D b2 = Remap(t1, t3, a2, a3, currentT);

        return Remap(t1, t2, b1, b2, currentT);
    }

    private static double GetT(double tPrev, Point2D pA, Point2D pB)
    {
        double distSq = pA.DistanceSquaredTo(pB);
        return tPrev + System.Math.Pow(distSq, Alpha * 0.5);
    }

    private static Point2D Remap(double tA, double tB, Point2D pA, Point2D pB, double t)
    {
        if (System.Math.Abs(tB - tA) < 1e-6)
            return pA;

        double factor = (t - tA) / (tB - tA);
        return pA + (pB - pA) * factor;
    }
}