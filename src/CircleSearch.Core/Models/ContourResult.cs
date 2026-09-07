using System.Collections.Generic;
using CircleSearch.Core.Math;

namespace CircleSearch.Core.Models;

public sealed record ContourResult(
    IReadOnlyList<Point2D> ContourPoints,
    SelectionBounds BoundingBox,
    bool Success,
    string? ErrorMessage = null
)
{
    public static ContourResult Failed(string reason) =>
        new([], SelectionBounds.Empty, false, reason);
}