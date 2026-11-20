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
///     A rendered picture: its size, its pixels, and the notes the renderer reported. The pixels are copied out
///     of the renderer, so the object holds no native or disposable resources and may be shared freely.
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
    /// <exception cref="ArgumentOutOfRangeException">Thrown when the width or height is less than 1.</exception>
    /// <exception cref="ArgumentException">Thrown when the pixel data is not the size the dimensions require.</exception>
    public RenderedImage(
        string sourcePath,
        string format,
        int width,
        int height,
        ReadOnlyMemory<byte> rgbaPixels,
        IReadOnlyList<RenderDiagnostic>? diagnostics = null)
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
