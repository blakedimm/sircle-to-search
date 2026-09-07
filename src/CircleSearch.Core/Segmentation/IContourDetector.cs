using System;
using System.Threading.Tasks;
using CircleSearch.Core.Math;
using CircleSearch.Core.Models;

namespace CircleSearch.Core.Segmentation;

public interface IContourDetector
{
    ValueTask<ContourResult> DetectContourAsync(
        ReadOnlyMemory<byte> bgraBuffer,
        int stride,
        int screenWidth,
        int screenHeight,
        Point2D clickPoint,
        double tolerance = 38.0,
        int roiRadius = 180
    );
}