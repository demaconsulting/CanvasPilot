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

using DemaConsulting.CanvasPilot.PreviewSubsystem;

namespace DemaConsulting.CanvasPilot.AnnotationSubsystem;

/// <summary>
///     How a mark looks on the picture on the screen: the colors, the width of the lines, the size and place of the
///     number badge, and the size of the head of an arrow. They are the same as the ones with which the annotated picture
///     of a sent message is drawn, so that the user sees what the assistant gets; the payload subsystem has its own copy,
///     because it must not depend on the user interface, and a test keeps the two equal.
/// </summary>
internal static class AnnotationOverlayStyle
{
    /// <summary>
    ///     The angle between the shaft of an arrow and each line of its head, in radians.
    /// </summary>
    public static readonly double ArrowHeadAngle = 28 * Math.PI / 180;

    /// <summary>
    ///     The colors of the marks, which repeat after six marks: red, blue, green, orange, purple, and teal.
    /// </summary>
    private static readonly string[] Palette = ["#e53935", "#1e88e5", "#43a047", "#fb8c00", "#8e24aa", "#00acc1"];

    /// <summary>
    ///     Gets the color of the mark with a number.
    /// </summary>
    /// <param name="number">The number of the mark, one or more.</param>
    /// <returns>The color as <c>#rrggbb</c>.</returns>
    public static string ColorOf(int number)
    {
        return Palette[(number - 1) % Palette.Length];
    }

    /// <summary>
    ///     Gets the width of the lines of marks on a picture, which grows with the picture.
    /// </summary>
    /// <param name="width">The width of the picture in pixels.</param>
    /// <param name="height">The height of the picture in pixels.</param>
    /// <returns>The width in pixels, from 3 to 8.</returns>
    public static double StrokeWidth(int width, int height)
    {
        return Math.Clamp(Math.Round(Math.Min(width, height) / 80.0), 3, 8);
    }

    /// <summary>
    ///     Gets the radius of the badge that shows the number of a mark.
    /// </summary>
    /// <param name="width">The width of the picture in pixels.</param>
    /// <param name="height">The height of the picture in pixels.</param>
    /// <returns>The radius in pixels, from 9 to 20.</returns>
    public static double BadgeRadius(int width, int height)
    {
        return Math.Clamp(Math.Round(Math.Min(width, height) * 0.035), 9, 20);
    }

    /// <summary>
    ///     Gets the center of the badge of a mark: the top-left corner of its bounding box, moved so that the whole badge
    ///     is inside the picture.
    /// </summary>
    /// <param name="box">The bounding box of the mark.</param>
    /// <param name="width">The width of the picture in pixels.</param>
    /// <param name="height">The height of the picture in pixels.</param>
    /// <returns>The center of the badge.</returns>
    public static RenderPoint BadgeCenter(RenderBox box, int width, int height)
    {
        var radius = BadgeRadius(width, height);
        return new RenderPoint(
            Math.Clamp(box.X, Math.Min(radius, width / 2.0), Math.Max(width - radius, width / 2.0)),
            Math.Clamp(box.Y, Math.Min(radius, height / 2.0), Math.Max(height - radius, height / 2.0)));
    }

    /// <summary>
    ///     Gets the length of each line of the head of an arrow.
    /// </summary>
    /// <param name="stroke">The width of the lines of the mark.</param>
    /// <param name="shaftLength">The length of the shaft, which the head is not longer than.</param>
    /// <returns>The length in pixels.</returns>
    public static double ArrowWing(double stroke, double shaftLength)
    {
        return Math.Min(Math.Max(12, stroke * 5), shaftLength);
    }
}
