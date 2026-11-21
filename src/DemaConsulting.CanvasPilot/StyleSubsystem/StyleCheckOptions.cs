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
///     The limits of a check of a picture against the palette of a kit. The defaults are the values that the design
///     fixes; each can be changed in one place.
/// </summary>
/// <remarks>
///     The distances are CIE76 color differences (see <see cref="StyleChecker" />): about 2.3 is the smallest difference
///     that the eye notices, and 10 is a color that is clearly of the same family. The record is immutable.
/// </remarks>
public sealed record StyleCheckOptions
{
    /// <summary>
    ///     The color difference within which a color counts as a palette color.
    /// </summary>
    public const double DefaultTolerance = 10;

    /// <summary>
    ///     The color difference within which colors that are off the palette are reported as one color.
    /// </summary>
    public const double DefaultClusterDelta = 8;

    /// <summary>
    ///     The color difference from a straight line between two palette colors within which a color counts as a blend of
    ///     them, which is what the edge of an anti-aliased shape is made of.
    /// </summary>
    public const double DefaultBlendTolerance = 5;

    /// <summary>
    ///     The share of the pixels that a color that is off the palette must have to be listed.
    /// </summary>
    public const double DefaultMinReportShare = 0.01;

    /// <summary>
    ///     The share of the pixels that may be off the palette for the picture to pass.
    /// </summary>
    public const double DefaultMaxOffPaletteShare = 0.05;

    /// <summary>
    ///     The most colors that are off the palette that a report lists; the rest are summed.
    /// </summary>
    public const int MaxListed = 8;

    /// <summary>
    ///     The alpha below which a pixel is taken as empty and is not counted.
    /// </summary>
    public const int TransparentBelowAlpha = 16;

    /// <summary>
    ///     Gets the color difference within which a color counts as a palette color, 0 to 100.
    /// </summary>
    public double Tolerance { get; init; } = DefaultTolerance;

    /// <summary>
    ///     Gets the color difference within which colors that are off the palette are reported as one, 0 to 100.
    /// </summary>
    public double ClusterDelta { get; init; } = DefaultClusterDelta;

    /// <summary>
    ///     Gets the color difference from a line between two palette colors within which a color counts as a blend, 0 to 100.
    /// </summary>
    public double BlendTolerance { get; init; } = DefaultBlendTolerance;

    /// <summary>
    ///     Gets the share of the pixels, 0 to 1, that a color that is off the palette must have to be listed.
    /// </summary>
    public double MinReportShare { get; init; } = DefaultMinReportShare;

    /// <summary>
    ///     Gets the share of the pixels, 0 to 1, that may be off the palette for the picture to pass.
    /// </summary>
    public double MaxOffPaletteShare { get; init; } = DefaultMaxOffPaletteShare;

    /// <summary>
    ///     Checks that every limit is a number in its range.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when a limit is not a number or is outside its range; the message names it.</exception>
    public void Validate()
    {
        CheckRange(Tolerance, 0, 100, nameof(Tolerance));
        CheckRange(ClusterDelta, 0, 100, nameof(ClusterDelta));
        CheckRange(BlendTolerance, 0, 100, nameof(BlendTolerance));
        CheckRange(MinReportShare, 0, 1, nameof(MinReportShare));
        CheckRange(MaxOffPaletteShare, 0, 1, nameof(MaxOffPaletteShare));
    }

    /// <summary>
    ///     Checks that a value is a number between two limits.
    /// </summary>
    /// <param name="value">The value.</param>
    /// <param name="minimum">The smallest allowed value.</param>
    /// <param name="maximum">The largest allowed value.</param>
    /// <param name="name">The name of the limit.</param>
    private static void CheckRange(double value, double minimum, double maximum, string name)
    {
        if (!(value >= minimum && value <= maximum))
        {
            throw new ArgumentOutOfRangeException(name, value, $"{name} must be a number from {minimum} to {maximum}.");
        }
    }
}
