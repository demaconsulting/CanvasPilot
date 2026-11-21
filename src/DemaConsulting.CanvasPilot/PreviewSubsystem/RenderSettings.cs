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

namespace DemaConsulting.CanvasPilot.PreviewSubsystem;

/// <summary>
///     How a file is drawn when the picture is not wanted at its natural size: the size, what the margin and the
///     transparent parts are filled with, and the resolution of a page.
/// </summary>
/// <remarks>
///     <para>
///         With both <paramref name="Width" /> and <paramref name="Height" /> the picture is fitted inside that box
///         keeping its aspect ratio, so the result is exactly that size and the margin has the background (or is
///         transparent); the picture is never stretched. With one of them the picture is scaled to it keeping its
///         aspect ratio. With neither the picture has its natural size.
///     </para>
///     <para>
///         The values are checked when the file is drawn, not here, so that an invalid size is reported as the result of
///         the render. A picture that is drawn with these settings has the pixels of the new size, so the positions of
///         the elements and the anchors that it reports are those of the new size.
///     </para>
/// </remarks>
/// <param name="Width">The width in pixels, 1 to 8192, or <see langword="null" /> for the natural width or the one that keeps the ratio.</param>
/// <param name="Height">The height in pixels, 1 to 8192, or <see langword="null" /> for the natural height or the one that keeps the ratio.</param>
/// <param name="Transparent"><see langword="true" /> to keep transparency (the margin and transparent parts stay transparent); the background is then ignored.</param>
/// <param name="BackgroundHex">The color, <c>#RRGGBB</c>, that the picture is flattened over, or <see langword="null" /> to leave transparent parts as they are.</param>
/// <param name="Dpi">The resolution, greater than 0 and at most 2400, that the natural page of a PDF, PowerPoint, or Visio file is sized at; every other kind ignores it. <see langword="null" /> means 96.</param>
public sealed record RenderSettings(
    int? Width = null,
    int? Height = null,
    bool Transparent = false,
    string? BackgroundHex = null,
    double? Dpi = null)
{
    /// <summary>
    ///     The largest width, height, or page resolution that is accepted for a size, in pixels (the largest surface
    ///     that CanvasNet draws).
    /// </summary>
    public const int MaxPixels = 8192;

    /// <summary>
    ///     The largest page resolution that is accepted, in dots per inch.
    /// </summary>
    public const double MaxDpi = 2400;

    /// <summary>
    ///     Gets the settings that draw a picture at its natural size and leave its transparency alone, which is what a
    ///     render without settings does.
    /// </summary>
    public static RenderSettings AsRendered { get; } = new();

    /// <summary>
    ///     Gets a value indicating whether these settings change nothing about how the file is drawn.
    /// </summary>
    public bool IsAsRendered => Width is null && Height is null && !Transparent && BackgroundHex is null && Dpi is null;
}
