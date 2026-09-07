using System;
using System.Runtime.CompilerServices;

namespace CircleSearch.Core.Segmentation;

public static class ColorDistance
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static double Calculate(byte r1, byte g1, byte b1, byte r2, byte g2, byte b2)
    {
        long rMean = (r1 + r2) / 2;
        long deltaR = r1 - r2;
        long deltaG = g1 - g2;
        long deltaB = b1 - b2;

        long weightR = 2 + (rMean >> 8);
        long weightG = 4;
        long weightB = 2 + ((255 - rMean) >> 8);

        return System.Math.Sqrt(weightR * deltaR * deltaR +
                                weightG * deltaG * deltaG +
                                weightB * deltaB * deltaB);
    }
}