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

namespace DemaConsulting.CanvasPilot.Tests;

/// <summary>
///     Makes rendered pictures for the tests of the units that work with marks and payloads, without drawing a file.
/// </summary>
internal static class AnnotationTestPictures
{
    /// <summary>
    ///     Makes a picture that is one flat color, with the element positions and anchors a test needs.
    /// </summary>
    /// <param name="path">The path of the file the picture claims to come from.</param>
    /// <param name="width">The width in pixels.</param>
    /// <param name="height">The height in pixels.</param>
    /// <param name="fill">The value of the red, green, and blue bytes; different values make different fingerprints.</param>
    /// <param name="format">The format the picture claims to have been drawn from.</param>
    /// <param name="positions">The element positions, or <see langword="null" /> for none.</param>
    /// <param name="anchors">The anchors, or <see langword="null" /> for none.</param>
    /// <param name="positionsTruncated">Whether the renderer left positions out.</param>
    /// <returns>The picture.</returns>
    public static RenderedImage Picture(
        string path = @"C:\project\flow.mmd",
        int width = 200,
        int height = 100,
        byte fill = 255,
        string format = "mermaid",
        IReadOnlyList<RenderPosition>? positions = null,
        IReadOnlyList<RenderAnchor>? anchors = null,
        bool positionsTruncated = false)
    {
        var pixels = new byte[width * height * 4];
        for (var i = 0; i < pixels.Length; i += 4)
        {
            pixels[i] = fill;
            pixels[i + 1] = fill;
            pixels[i + 2] = fill;
            pixels[i + 3] = 255;
        }

        return new RenderedImage(path, format, width, height, pixels, null, "file-hash", positions, positionsTruncated, anchors);
    }

    /// <summary>
    ///     Makes the position of a node of a diagram.
    /// </summary>
    /// <param name="id">The identifier of the node.</param>
    /// <param name="text">The text of the node.</param>
    /// <param name="x">The left edge of the box.</param>
    /// <param name="y">The top edge of the box.</param>
    /// <param name="width">The width of the box.</param>
    /// <param name="height">The height of the box.</param>
    /// <param name="line">The source line of the node.</param>
    /// <returns>The position.</returns>
    public static RenderPosition Node(string id, string text, double x, double y, double width, double height, int line = 2)
    {
        return new RenderPosition("node", id, text, line, Box: new RenderBox(x, y, width, height));
    }
}
