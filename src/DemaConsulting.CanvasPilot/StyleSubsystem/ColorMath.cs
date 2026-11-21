// Copyright (c) DEMA Consulting
// 
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
// 
// The above copyright notice and this permission notice shall be included in all
// copies or substantial portions of the Software.
// 
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
// SOFTWARE.

namespace DemaConsulting.CanvasPilot.StyleSubsystem;

/// <summary>
///     The color arithmetic of the style check: sRGB to CIELAB (D65), the CIE76 color difference, and the distance of a
///     color from the straight line between two other colors.
/// </summary>
/// <remarks>
///     CIE76 is the Euclidean distance in CIELAB. It was chosen over CIEDE2000 because the check asks only whether a color
///     is near one of a few palette colors, it needs no special cases, and it gives the same answer on every machine; its
///     habit of over-stating differences between saturated blues only makes the check slightly stricter. The functions are
///     pure and thread-safe.
/// </remarks>
internal static class ColorMath
{
    /// <summary>
    ///     The reference white of D65 on the scale where Y is 1.
    /// </summary>
    private const double WhiteX = 0.95047;

    /// <summary>
    ///     The reference white of D65, the Z component.
    /// </summary>
    private const double WhiteZ = 1.08883;

    /// <summary>
    ///     The threshold of the CIELAB function, <c>(6/29)^3</c>.
    /// </summary>
    private const double Epsilon = 216.0 / 24389.0;

    /// <summary>
    ///     The slope of the linear part of the CIELAB function, <c>(29/6)^2 / 3</c>.
    /// </summary>
    private const double Slope = 841.0 / 108.0;

    /// <summary>
    ///     The distance in sRGB (0 to 255 per channel) beyond which a color cannot be within a few units of color difference
    ///     of a line, so that the more costly conversion is not needed. A unit of color difference is at least about a third
    ///     of a unit of a channel anywhere in the gamut, so this leaves a wide margin.
    /// </summary>
    private const double NearRgbDistance = 60;

    /// <summary>
    ///     Converts a color of sRGB (0 to 255 per channel, which may have fractions) to CIELAB with the white of D65.
    /// </summary>
    /// <param name="red">The red channel, 0 to 255.</param>
    /// <param name="green">The green channel, 0 to 255.</param>
    /// <param name="blue">The blue channel, 0 to 255.</param>
    /// <returns>The lightness and the two color axes.</returns>
    public static Lab ToLab(double red, double green, double blue)
    {
        // Undo the transfer curve of sRGB, then take the three tristimulus values with the matrix of sRGB for D65
        var r = Linearize(red);
        var g = Linearize(green);
        var b = Linearize(blue);
        var x = ((0.4124564 * r) + (0.3575761 * g) + (0.1804375 * b)) / WhiteX;
        var y = (0.2126729 * r) + (0.7151522 * g) + (0.0721750 * b);
        var z = ((0.0193339 * r) + (0.1191920 * g) + (0.9503041 * b)) / WhiteZ;

        // The CIELAB axes from the three values
        var fx = F(x);
        var fy = F(y);
        var fz = F(z);
        return new Lab((116 * fy) - 16, 500 * (fx - fy), 200 * (fy - fz));
    }

    /// <summary>
    ///     Measures the CIE76 difference of two colors: the Euclidean distance in CIELAB.
    /// </summary>
    /// <param name="first">One color.</param>
    /// <param name="second">The other color.</param>
    /// <returns>The difference; 0 for the same color, and about 2.3 for the smallest difference that the eye notices.</returns>
    public static double DeltaE76(Lab first, Lab second)
    {
        var l = first.L - second.L;
        var a = first.A - second.A;
        var b = first.B - second.B;
        return Math.Sqrt((l * l) + (a * a) + (b * b));
    }

    /// <summary>
    ///     Measures how far a color is, in color difference, from the nearest point of the straight line (in sRGB, the space
    ///     in which an edge is blended) between two colors.
    /// </summary>
    /// <param name="red">The red channel of the color, 0 to 255.</param>
    /// <param name="green">The green channel of the color, 0 to 255.</param>
    /// <param name="blue">The blue channel of the color, 0 to 255.</param>
    /// <param name="from">One end of the line.</param>
    /// <param name="to">The other end of the line.</param>
    /// <returns>
    ///     The CIE76 difference between the color and the nearest point of the line; <see cref="double.PositiveInfinity" />
    ///     when the color is so far from the line in sRGB that it cannot be near it.
    /// </returns>
    public static double BlendDistance(double red, double green, double blue, Rgb from, Rgb to)
    {
        // Find the point of the segment that is nearest to the color, in sRGB
        var dr = to.R - from.R;
        var dg = to.G - from.G;
        var db = to.B - from.B;
        var lengthSquared = (dr * dr) + (dg * dg) + (db * db);
        if (lengthSquared <= 0)
        {
            return double.PositiveInfinity;
        }

        var t = Math.Clamp((((red - from.R) * dr) + ((green - from.G) * dg) + ((blue - from.B) * db)) / lengthSquared, 0, 1);
        var pr = from.R + (t * dr);
        var pg = from.G + (t * dg);
        var pb = from.B + (t * db);

        // A color far from the segment in sRGB is far in color difference too, so skip the conversion for it
        var er = red - pr;
        var eg = green - pg;
        var eb = blue - pb;
        if (Math.Sqrt((er * er) + (eg * eg) + (eb * eb)) > NearRgbDistance)
        {
            return double.PositiveInfinity;
        }

        return DeltaE76(ToLab(red, green, blue), ToLab(pr, pg, pb));
    }

    /// <summary>
    ///     Converts one sRGB channel to linear light.
    /// </summary>
    /// <param name="channel">The channel, 0 to 255.</param>
    /// <returns>The linear value from 0 to 1.</returns>
    private static double Linearize(double channel)
    {
        var value = channel / 255.0;
        return value <= 0.04045 ? value / 12.92 : Math.Pow((value + 0.055) / 1.055, 2.4);
    }

    /// <summary>
    ///     The function of CIELAB that turns a tristimulus ratio into the scale of lightness.
    /// </summary>
    /// <param name="value">The ratio of a tristimulus value to the white.</param>
    /// <returns>The value of the function.</returns>
    private static double F(double value) => value > Epsilon ? Math.Cbrt(value) : (Slope * value) + (16.0 / 116.0);

    /// <summary>
    ///     A color in CIELAB.
    /// </summary>
    /// <param name="L">The lightness, 0 to 100.</param>
    /// <param name="A">The green to red axis.</param>
    /// <param name="B">The blue to yellow axis.</param>
    public readonly record struct Lab(double L, double A, double B);

    /// <summary>
    ///     A color in sRGB with channels that may have fractions.
    /// </summary>
    /// <param name="R">The red channel, 0 to 255.</param>
    /// <param name="G">The green channel, 0 to 255.</param>
    /// <param name="B">The blue channel, 0 to 255.</param>
    public readonly record struct Rgb(double R, double G, double B);
}
