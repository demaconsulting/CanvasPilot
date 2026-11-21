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

using System.Buffers.Binary;
using System.Security.Cryptography;

namespace DemaConsulting.CanvasPilot.PreviewSubsystem;

/// <summary>
///     How serious a note about a rendered image is.
/// </summary>
public enum RenderDiagnosticSeverity
{
    /// <summary>Information only; the picture is complete.</summary>
    Info,

    /// <summary>Something was wrong or ignored, but the picture was still drawn.</summary>
    Warning,

    /// <summary>The file uses a feature that is not drawn; the picture is drawn without it.</summary>
    Unsupported
}

/// <summary>
///     A note that the renderer reported about a rendered image, such as an embedded picture that was not drawn.
/// </summary>
/// <param name="Severity">How serious the note is.</param>
/// <param name="Code">The short, stable, machine-readable code of the note.</param>
/// <param name="Message">The single-line, human-readable description.</param>
public sealed record RenderDiagnostic(RenderDiagnosticSeverity Severity, string Code, string Message)
{
    /// <summary>
    ///     Gets the note as one line for display, for example <c>Warning: image-access-denied: image was not drawn</c>.
    /// </summary>
    public string DisplayText => $"{Severity}: {Code}: {Message}";
}

/// <summary>
///     A point in the pixel coordinates of a rendered picture: the origin is the top-left corner, X grows to the
///     right and Y grows downward. The coordinates are continuous, so they may have fractions.
/// </summary>
/// <param name="X">The distance from the left edge, in pixels.</param>
/// <param name="Y">The distance from the top edge, in pixels.</param>
public readonly record struct RenderPoint(double X, double Y);

/// <summary>
///     A rectangle in the pixel coordinates of a rendered picture, with the same origin and axes as
///     <see cref="RenderPoint" />.
/// </summary>
/// <param name="X">The left edge, in pixels.</param>
/// <param name="Y">The top edge, in pixels.</param>
/// <param name="Width">The width in pixels; not negative.</param>
/// <param name="Height">The height in pixels; not negative.</param>
public readonly record struct RenderBox(double X, double Y, double Width, double Height);

/// <summary>
///     One element of a rendered diagram or chart, such as a node, an edge, a bar, or an axis, with where it was
///     drawn. Only Mermaid diagrams and Vega-Lite charts report elements.
/// </summary>
/// <param name="Kind">The kind of element, a lower case word such as <c>node</c>, <c>edge</c>, <c>mark</c>, or <c>axis</c>.</param>
/// <param name="Id">The identifier the source gives the element, or one the renderer derives; may be empty.</param>
/// <param name="Text">The text of the element, such as a label, or <see langword="null" /> for none.</param>
/// <param name="Line">The 1-based line of the source file that declares the element, or <see langword="null" /> when unknown.</param>
/// <param name="From">The identifier of the element an edge starts at, or <see langword="null" />.</param>
/// <param name="To">The identifier of the element an edge ends at, or <see langword="null" />.</param>
/// <param name="Box">The bounds of the element in picture pixels, or <see langword="null" /> when unknown.</param>
/// <param name="Pixel">A point on the element a pointer can aim at, or <see langword="null" /> when unknown.</param>
public sealed record RenderPosition(
    string Kind,
    string Id,
    string? Text = null,
    int? Line = null,
    string? From = null,
    string? To = null,
    RenderBox? Box = null,
    RenderPoint? Pixel = null);

/// <summary>
///     A named point that an OpenSCAD model declared with <c>echo("anchor", name, [x, y, z])</c>, with where it
///     appears in the rendered picture.
/// </summary>
/// <param name="Name">The name the model gives the anchor.</param>
/// <param name="PixelX">The distance of the anchor from the left edge of the picture, or <see langword="null" /> when unknown.</param>
/// <param name="PixelY">The distance of the anchor from the top edge of the picture, or <see langword="null" /> when unknown.</param>
public sealed record RenderAnchor(string Name, double? PixelX = null, double? PixelY = null);

/// <summary>
///     A rendered picture: its size, its pixels, the notes the renderer reported, and the facts that identify
///     exactly which picture it is: the file it came from, fingerprints of the file and of the pixels, and where the
///     elements of a diagram, chart, or model were drawn. The pixels are copied out of the renderer, so the object
///     holds no native or disposable resources and may be shared freely.
/// </summary>
public sealed class RenderedImage
{
    /// <summary>
    ///     Initializes a new instance of the <see cref="RenderedImage" /> class.
    /// </summary>
    /// <param name="sourcePath">The full path of the file that was rendered.</param>
    /// <param name="format">The name of the format that was drawn, for example <c>svg</c>.</param>
    /// <param name="width">The width in pixels; at least 1.</param>
    /// <param name="height">The height in pixels; at least 1.</param>
    /// <param name="rgbaPixels">
    ///     The pixels: four bytes per pixel in the order red, green, blue, alpha with straight (not premultiplied)
    ///     alpha, rows from the top with no padding, so exactly <c>width * height * 4</c> bytes.
    /// </param>
    /// <param name="diagnostics">The notes the renderer reported; <see langword="null" /> for none.</param>
    /// <param name="sourceFileHash">
    ///     The SHA-256 fingerprint of the bytes of the source file as lower case hexadecimal, or
    ///     <see langword="null" /> when the file could not be read for the fingerprint.
    /// </param>
    /// <param name="positions">The elements of a diagram or chart with where they were drawn; <see langword="null" /> for none.</param>
    /// <param name="positionsTruncated">Whether the renderer left out elements because there were too many.</param>
    /// <param name="anchors">The anchors of a model with where they appear; <see langword="null" /> for none.</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when the width or height is less than 1.</exception>
    /// <exception cref="ArgumentException">Thrown when the pixel data is not the size the dimensions require.</exception>
    public RenderedImage(
        string sourcePath,
        string format,
        int width,
        int height,
        ReadOnlyMemory<byte> rgbaPixels,
        IReadOnlyList<RenderDiagnostic>? diagnostics = null,
        string? sourceFileHash = null,
        IReadOnlyList<RenderPosition>? positions = null,
        bool positionsTruncated = false,
        IReadOnlyList<RenderAnchor>? anchors = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourcePath);
        ArgumentNullException.ThrowIfNull(format);
        ArgumentOutOfRangeException.ThrowIfLessThan(width, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(height, 1);
        if (rgbaPixels.Length != (long)width * height * 4)
        {
            throw new ArgumentException(
                $"A {width} x {height} image needs {(long)width * height * 4} bytes of pixels, not {rgbaPixels.Length}.",
                nameof(rgbaPixels));
        }

        SourcePath = sourcePath;
        Format = format;
        Width = width;
        Height = height;
        RgbaPixels = rgbaPixels;
        Diagnostics = diagnostics ?? [];
        SourceFileHash = sourceFileHash;
        Positions = positions is null ? [] : positions.ToArray();
        PositionsTruncated = positionsTruncated;
        Anchors = anchors is null ? [] : anchors.ToArray();
        ContentHash = ComputeContentHash(width, height, rgbaPixels.Span);
    }

    /// <summary>
    ///     Gets the full path of the file that was rendered.
    /// </summary>
    public string SourcePath { get; }

    /// <summary>
    ///     Gets the name of the format that was drawn, for example <c>svg</c>.
    /// </summary>
    public string Format { get; }

    /// <summary>
    ///     Gets the width in pixels.
    /// </summary>
    public int Width { get; }

    /// <summary>
    ///     Gets the height in pixels.
    /// </summary>
    public int Height { get; }

    /// <summary>
    ///     Gets the pixels: red, green, blue, alpha per pixel with straight alpha, rows from the top, no padding.
    /// </summary>
    public ReadOnlyMemory<byte> RgbaPixels { get; }

    /// <summary>
    ///     Gets the notes the renderer reported.
    /// </summary>
    public IReadOnlyList<RenderDiagnostic> Diagnostics { get; }

    /// <summary>
    ///     Gets the SHA-256 fingerprint, as lower case hexadecimal, of what the user saw: the width and height and
    ///     every pixel. Two pictures with the same fingerprint look the same.
    /// </summary>
    public string ContentHash { get; }

    /// <summary>
    ///     Gets the SHA-256 fingerprint, as lower case hexadecimal, of the bytes of the source file as they were
    ///     when the picture was drawn, or <see langword="null" /> when the file could not be read for it.
    /// </summary>
    public string? SourceFileHash { get; }

    /// <summary>
    ///     Gets the elements of a Mermaid diagram or Vega-Lite chart with where they were drawn, in picture pixels;
    ///     empty for every other kind of file.
    /// </summary>
    public IReadOnlyList<RenderPosition> Positions { get; }

    /// <summary>
    ///     Gets a value indicating whether <see cref="Positions" /> lacks elements because the renderer reports at
    ///     most a fixed number of them.
    /// </summary>
    public bool PositionsTruncated { get; }

    /// <summary>
    ///     Gets the anchors that an OpenSCAD model declared, with where they appear in picture pixels; empty for every
    ///     other kind of file and for a model without anchors.
    /// </summary>
    public IReadOnlyList<RenderAnchor> Anchors { get; }

    /// <summary>
    ///     Gets the identity of this exact picture: the path of the source file and the fingerprint of the pixels.
    ///     Pictures with equal keys are the same picture of the same file.
    /// </summary>
    public string SnapshotKey => $"{SourcePath}#{ContentHash}";

    /// <summary>
    ///     Computes the fingerprint of a picture.
    /// </summary>
    /// <param name="width">The width in pixels.</param>
    /// <param name="height">The height in pixels.</param>
    /// <param name="pixels">The pixels.</param>
    /// <returns>The SHA-256 hash of the width, the height, and the pixels, as lower case hexadecimal.</returns>
    private static string ComputeContentHash(int width, int height, ReadOnlySpan<byte> pixels)
    {
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        Span<byte> size = stackalloc byte[8];
        BinaryPrimitives.WriteInt32LittleEndian(size, width);
        BinaryPrimitives.WriteInt32LittleEndian(size[4..], height);
        hash.AppendData(size);
        hash.AppendData(pixels);
        return Convert.ToHexStringLower(hash.GetHashAndReset());
    }
}

/// <summary>
///     Why a file could not be rendered.
/// </summary>
/// <param name="Code">
///     The stable, machine-readable error code. For failures that CanvasNet reports as a viewer error this is its
///     own code (for example <c>page-out-of-range</c>); otherwise one of the codes in <see cref="RenderErrorCodes" />.
/// </param>
/// <param name="Message">A human-readable description of the problem.</param>
/// <param name="Hint">A suggestion for fixing the problem, or <see langword="null" /> when there is none.</param>
public sealed record RenderError(string Code, string Message, string? Hint = null);

/// <summary>
///     The error codes the renderer uses for failures that CanvasNet does not describe with a code of its own.
///     They reuse the names of CanvasNet's own error codes where one fits.
/// </summary>
public static class RenderErrorCodes
{
    /// <summary>The file to render does not exist.</summary>
    public const string InputNotFound = "input-not-found";

    /// <summary>The file exists but cannot be read.</summary>
    public const string InputUnreadable = "input-unreadable";

    /// <summary>No image source draws files of this type.</summary>
    public const string UnsupportedFormat = "unsupported-format";

    /// <summary>The file was found but its content cannot be drawn, for example because it has a syntax error.</summary>
    public const string CannotDraw = "cannot-draw";

    /// <summary>The picture is larger than the renderer's limits allow.</summary>
    public const string SizeLimit = "size-limit";

    /// <summary>The render failed for a reason that is none of the above.</summary>
    public const string OperationFailed = "operation-failed";

    /// <summary>The settings of the render (a size, a background, or a resolution) are not values that CanvasNet draws.</summary>
    public const string InvalidSettings = "invalid-settings";
}

/// <summary>
///     The outcome of rendering a file: a picture, an error, or cancellation. Ordinary render failures are
///     results, never exceptions.
/// </summary>
public sealed class RenderResult
{
    /// <summary>
    ///     Initializes a new instance of the <see cref="RenderResult" /> class.
    /// </summary>
    /// <param name="image">The picture, or <see langword="null" />.</param>
    /// <param name="error">The error, or <see langword="null" />.</param>
    /// <param name="isCancelled">Whether the render was cancelled.</param>
    private RenderResult(RenderedImage? image, RenderError? error, bool isCancelled)
    {
        Image = image;
        Error = error;
        IsCancelled = isCancelled;
    }

    /// <summary>
    ///     Gets the picture, or <see langword="null" /> when the render failed or was cancelled.
    /// </summary>
    public RenderedImage? Image { get; }

    /// <summary>
    ///     Gets the error, or <see langword="null" /> when the render succeeded or was cancelled.
    /// </summary>
    public RenderError? Error { get; }

    /// <summary>
    ///     Gets a value indicating whether the render was cancelled before it finished.
    /// </summary>
    public bool IsCancelled { get; }

    /// <summary>
    ///     Gets a value indicating whether the render produced a picture.
    /// </summary>
    public bool Succeeded => Image is not null;

    /// <summary>
    ///     Creates the result of a render that produced a picture.
    /// </summary>
    /// <param name="image">The picture.</param>
    /// <returns>The result.</returns>
    public static RenderResult Success(RenderedImage image)
    {
        ArgumentNullException.ThrowIfNull(image);
        return new RenderResult(image, null, false);
    }

    /// <summary>
    ///     Creates the result of a render that failed.
    /// </summary>
    /// <param name="error">The error.</param>
    /// <returns>The result.</returns>
    public static RenderResult Failure(RenderError error)
    {
        ArgumentNullException.ThrowIfNull(error);
        return new RenderResult(null, error, false);
    }

    /// <summary>
    ///     Creates the result of a render that was cancelled.
    /// </summary>
    /// <returns>The result.</returns>
    public static RenderResult Cancelled()
    {
        return new RenderResult(null, null, true);
    }
}
