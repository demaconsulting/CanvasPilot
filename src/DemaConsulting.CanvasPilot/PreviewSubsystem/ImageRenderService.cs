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

using System.Security.Cryptography;
using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.CanvasNet.Codecs;
using DemaConsulting.CanvasNet.Files;
using DemaConsulting.CanvasNet.ImageSources;
using DemaConsulting.CanvasNet.Positions;
using DemaConsulting.CanvasNet.Viewer;
using DemaConsulting.CanvasPilot.LoggingSubsystem;

namespace DemaConsulting.CanvasPilot.PreviewSubsystem;

/// <summary>
///     Draws image source files with <see cref="CanvasNetViewer" /> on a thread-pool thread, copies the pixels of
///     the picture, the fingerprints that identify it, and the positions of its elements into a
///     <see cref="RenderedImage" />, and encodes pictures as PNG with CanvasNet's PNG codec.
///     Every failure to draw a file is returned as a <see cref="RenderError" />, so callers never handle viewer
///     exceptions.
/// </summary>
public sealed class ImageRenderService : IImageRenderService
{
    /// <summary>
    ///     Draws a file with the given options; the viewer by default, a substitute in tests.
    /// </summary>
    private readonly Func<string, ViewerRenderOptions, ViewerImage> _render;

    /// <summary>
    ///     Optional logger for problems with files.
    /// </summary>
    private readonly RollingFileLogger? _logger;

    /// <summary>
    ///     Initializes a new instance of the <see cref="ImageRenderService" /> class that draws with the viewer.
    /// </summary>
    /// <param name="logger">Optional logger for diagnostics.</param>
    public ImageRenderService(RollingFileLogger? logger = null)
        : this(CanvasNetViewer.Render, logger)
    {
    }

    /// <summary>
    ///     Initializes a new instance of the <see cref="ImageRenderService" /> class around a drawing function.
    /// </summary>
    /// <param name="render">Draws a file with the given options.</param>
    /// <param name="logger">Optional logger for diagnostics.</param>
    internal ImageRenderService(Func<string, ViewerRenderOptions, ViewerImage> render, RollingFileLogger? logger = null)
    {
        ArgumentNullException.ThrowIfNull(render);

        _render = render;
        _logger = logger;
    }

    /// <inheritdoc />
    public Task<RenderResult> RenderAsync(string filePath, string projectFolder, CancellationToken cancellationToken)
    {
        return RenderAsync(filePath, projectFolder, null, cancellationToken);
    }

    /// <inheritdoc />
    public Task<RenderResult> RenderAsync(string filePath, string projectFolder, RenderSettings? settings, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(projectFolder);

        // The viewer observes the token itself, so the task is not started with it: a cancelled render
        // completes with a cancelled result instead of a cancelled task.
        return Task.Run(() => Render(filePath, projectFolder, settings, cancellationToken), CancellationToken.None);
    }

    /// <inheritdoc />
    public byte[] EncodePng(RenderedImage image)
    {
        return EncodePng(image, null);
    }

    /// <inheritdoc />
    public byte[] EncodePng(RenderedImage image, double? dpi)
    {
        ArgumentNullException.ThrowIfNull(image);

        using var surface = new Surface(image.Width, image.Height);
        var rowBytes = image.Width * 4;
        for (var y = 0; y < image.Height; y++)
        {
            image.RgbaPixels.Span.Slice(y * rowBytes, rowBytes).CopyTo(surface.GetRowSpanBytes(y));
        }

        using var stream = new MemoryStream();
        PngCodec.Save(surface, stream);
        var png = stream.ToArray();

        // The codec writes no resolution, so one that is asked for is added as a chunk that leaves the pixels alone
        return dpi is { } resolution ? PngPhysicalSize.Insert(png, resolution) : png;
    }

    /// <summary>
    ///     Draws a file and turns the outcome, including every ordinary failure, into a result. Runs on a
    ///     thread-pool thread.
    /// </summary>
    /// <param name="filePath">The file to draw.</param>
    /// <param name="projectFolder">The folder the file may read from.</param>
    /// <param name="settings">How to draw the file, or <see langword="null" /> to draw it as it is.</param>
    /// <param name="cancellationToken">Cancels the render.</param>
    /// <returns>The result.</returns>
    private RenderResult Render(string filePath, string projectFolder, RenderSettings? settings, CancellationToken cancellationToken)
    {
        var fullPath = filePath;
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            fullPath = Path.GetFullPath(filePath);
            var options = CreateOptions(Path.GetFullPath(projectFolder), settings, cancellationToken);

            using var viewerImage = _render(fullPath, options);
            var sourceFileHash = HashSourceFile(fullPath);
            return RenderResult.Success(Copy(fullPath, viewerImage, sourceFileHash));
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return RenderResult.Cancelled();
        }
        catch (Exception ex)
        {
            // Rendering parses arbitrary user files with many format libraries. Whatever goes wrong must reach the
            // user as a message and never escape a background thread, so every failure becomes a result here.
            var error = ToError(ex);
            Log(LogLevel.Warning, $"Rendering '{fullPath}' failed ({error.Code}): {error.Message}", ex);
            return RenderResult.Failure(error);
        }
    }

    /// <summary>
    ///     Builds the options of the viewer: the confinement of the files, the cancellation, the positions of the
    ///     elements, and the size, background, and resolution that the settings ask for.
    /// </summary>
    /// <param name="projectFolder">The full path of the folder the file may read from.</param>
    /// <param name="settings">How to draw the file, or <see langword="null" /> to draw it as it is.</param>
    /// <param name="cancellationToken">Cancels the render.</param>
    /// <returns>The options.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when a setting is outside what CanvasNet draws.</exception>
    private static ViewerRenderOptions CreateOptions(string projectFolder, RenderSettings? settings, CancellationToken cancellationToken)
    {
        // The size is checked here with a message that says what is allowed; the viewer would refuse it too, but later
        // and in its own words
        CheckPixels(settings?.Width, nameof(RenderSettings.Width));
        CheckPixels(settings?.Height, nameof(RenderSettings.Height));
        if (settings?.Dpi is { } dpi && (!double.IsFinite(dpi) || dpi <= 0 || dpi > RenderSettings.MaxDpi))
        {
            throw new ArgumentOutOfRangeException(nameof(settings), dpi, $"The resolution must be greater than 0 and at most {RenderSettings.MaxDpi} dots per inch.");
        }

        return new ViewerRenderOptions
        {
            FileAccess = FileAccessPolicy.UnderRoots(projectFolder),
            CancellationToken = cancellationToken,

            // Ask for the position of every element: Mermaid and Vega-Lite report nodes, edges, marks, and
            // axes, and every other kind ignores the request. OpenSCAD anchors are reported without asking.
            Positions = PositionSelection.All,

            // The size: both sides fit the picture into a box of that size keeping its ratio, one side scales it
            Width = settings?.Width,
            Height = settings?.Height,
            Background = ToBackground(settings),
            Dpi = settings?.Dpi
        };
    }

    /// <summary>
    ///     Checks that a size in pixels, when it is given, is one that CanvasNet draws.
    /// </summary>
    /// <param name="pixels">The size, or <see langword="null" />.</param>
    /// <param name="name">The name of the setting for the message.</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when the size is given and is outside 1 to 8192.</exception>
    private static void CheckPixels(int? pixels, string name)
    {
        if (pixels is { } value && (value < 1 || value > RenderSettings.MaxPixels))
        {
            throw new ArgumentOutOfRangeException(name, value, $"The {name.ToLowerInvariant()} must be from 1 to {RenderSettings.MaxPixels} pixels.");
        }
    }

    /// <summary>
    ///     Chooses the background of the render from the settings.
    /// </summary>
    /// <param name="settings">The settings, or <see langword="null" />.</param>
    /// <returns>
    ///     A fully transparent color to keep transparency, the color of the settings to flatten over it, or
    ///     <see langword="null" /> to leave the picture as the file draws it.
    /// </returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when the background is not a color of the form <c>#RRGGBB</c>.</exception>
    private static Rgba32? ToBackground(RenderSettings? settings)
    {
        if (settings is null)
        {
            return null;
        }

        // Transparency wins over a color, so a preset that says transparent is never flattened by a stray color
        if (settings.Transparent)
        {
            return new Rgba32(0, 0, 0, 0);
        }

        if (settings.BackgroundHex is null)
        {
            return null;
        }

        return Rgba32.TryParse(settings.BackgroundHex, out var color)
            ? color
            : throw new ArgumentOutOfRangeException(nameof(settings), settings.BackgroundHex, "The background must be a color of the form #RRGGBB.");
    }

    /// <summary>
    ///     Copies the pixels, notes, element positions, and anchors of a viewer picture, so the picture can be
    ///     disposed.
    /// </summary>
    /// <param name="fullPath">The file that was drawn.</param>
    /// <param name="viewerImage">The viewer picture.</param>
    /// <param name="sourceFileHash">The fingerprint of the file that was drawn, or <see langword="null" />.</param>
    /// <returns>The copy.</returns>
    private static RenderedImage Copy(string fullPath, ViewerImage viewerImage, string? sourceFileHash)
    {
        var surface = viewerImage.Surface;
        var rowBytes = surface.Width * 4;
        var pixels = new byte[rowBytes * surface.Height];
        for (var y = 0; y < surface.Height; y++)
        {
            surface.GetRowSpanBytes(y).CopyTo(pixels.AsSpan(y * rowBytes, rowBytes));
        }

        var diagnostics = viewerImage.Diagnostics
            .Select(note => new RenderDiagnostic(ToSeverity(note.Severity), note.Code, note.Message))
            .ToList();
        var positions = viewerImage.Positions.Select(ToPosition).ToList();
        var anchors = viewerImage.Anchors
            .Select(anchor => new RenderAnchor(anchor.Name, anchor.PixelX, anchor.PixelY))
            .ToList();
        return new RenderedImage(
            fullPath,
            viewerImage.Format,
            surface.Width,
            surface.Height,
            pixels,
            diagnostics,
            sourceFileHash,
            positions,
            viewerImage.PositionsTruncated,
            anchors);
    }

    /// <summary>
    ///     Converts an element position of CanvasNet. Empty text and empty element references mean the same as none.
    /// </summary>
    /// <param name="position">The CanvasNet position.</param>
    /// <returns>The matching position.</returns>
    private static RenderPosition ToPosition(ImagePosition position)
    {
        return new RenderPosition(
            position.Kind,
            position.Id,
            NullIfEmpty(position.Text),
            position.Line,
            NullIfEmpty(position.From),
            NullIfEmpty(position.To),
            position.Box is { } box ? new RenderBox(box.X, box.Y, box.Width, box.Height) : null,
            position.Pixel is { } pixel ? new RenderPoint(pixel.X, pixel.Y) : null);
    }

    /// <summary>
    ///     Gives <see langword="null" /> for an empty text.
    /// </summary>
    /// <param name="text">The text, or <see langword="null" />.</param>
    /// <returns>The text, or <see langword="null" /> when it is null or empty.</returns>
    private static string? NullIfEmpty(string? text)
    {
        return string.IsNullOrEmpty(text) ? null : text;
    }

    /// <summary>
    ///     Computes the fingerprint of the bytes of the file that was drawn. A file that was removed or locked
    ///     since it was drawn has none, which does not fail the render.
    /// </summary>
    /// <param name="fullPath">The file that was drawn.</param>
    /// <returns>The SHA-256 hash as lower case hexadecimal, or <see langword="null" /> when the file cannot be read.</returns>
    private static string? HashSourceFile(string fullPath)
    {
        try
        {
            using var stream = new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            return Convert.ToHexStringLower(SHA256.HashData(stream));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    /// <summary>
    ///     Converts the severity of a CanvasNet note.
    /// </summary>
    /// <param name="severity">The CanvasNet severity.</param>
    /// <returns>The matching severity.</returns>
    private static RenderDiagnosticSeverity ToSeverity(ImageDiagnosticSeverity severity)
    {
        return severity switch
        {
            ImageDiagnosticSeverity.Info => RenderDiagnosticSeverity.Info,
            ImageDiagnosticSeverity.Unsupported => RenderDiagnosticSeverity.Unsupported,
            _ => RenderDiagnosticSeverity.Warning
        };
    }

    /// <summary>
    ///     Describes an exception that stopped a render as an error.
    /// </summary>
    /// <param name="exception">The exception.</param>
    /// <returns>The error, with CanvasNet's own code and hint for a viewer error.</returns>
    private static RenderError ToError(Exception exception)
    {
        return exception switch
        {
            ViewerException viewer => new RenderError(viewer.Code, viewer.Message, viewer.Hint),
            ArgumentOutOfRangeException =>
                new RenderError(RenderErrorCodes.InvalidSettings, exception.Message, "Use a size from 1 to 8192 pixels, a color like #RRGGBB, and a resolution up to 2400 dots per inch."),
            FileNotFoundException or DirectoryNotFoundException =>
                new RenderError(RenderErrorCodes.InputNotFound, exception.Message, "Check that the file still exists in the project folder."),
            NotSupportedException =>
                new RenderError(RenderErrorCodes.UnsupportedFormat, exception.Message, "Rename the file to one of the supported file name extensions."),
            ImageSourceException =>
                new RenderError(RenderErrorCodes.CannotDraw, exception.Message, "Fix the problem described in the message and save the file."),
            ImageSourceBudgetException =>
                new RenderError(RenderErrorCodes.SizeLimit, exception.Message, "Use a smaller image or reduce its size."),
            FileReferenceException or IOException or UnauthorizedAccessException =>
                new RenderError(RenderErrorCodes.InputUnreadable, exception.Message, "Check that the file can be read and is not larger than 16 MiB."),
            _ => new RenderError(RenderErrorCodes.OperationFailed, exception.Message)
        };
    }

    /// <summary>
    ///     Writes a diagnostic entry when a logger is available. Failing to log never fails a render.
    /// </summary>
    /// <param name="level">Severity of the event.</param>
    /// <param name="message">Event text.</param>
    /// <param name="exception">Optional failure context.</param>
    private void Log(LogLevel level, string message, Exception? exception)
    {
        try
        {
            _logger?.Log(level, message, exception);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Best effort: diagnostics must not make a file problem worse.
        }
    }
}
