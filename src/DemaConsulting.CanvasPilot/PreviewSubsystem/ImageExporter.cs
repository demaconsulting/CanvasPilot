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

using DemaConsulting.CanvasPilot.LoggingSubsystem;

namespace DemaConsulting.CanvasPilot.PreviewSubsystem;

/// <summary>
///     Exports the picture that the preview pane shows, as a PNG file or on the clipboard, for the Image menu.
///     It can export only while a picture is shown. It follows its picture source for as long as that source
///     lives, so it needs no disposal of its own.
/// </summary>
public sealed class ImageExporter : IImageExporter
{
    /// <summary>
    ///     The title of the dialogs this exporter shows.
    /// </summary>
    private const string ExportTitle = "Export as PNG";

    /// <summary>
    ///     The title of the messages about copying.
    /// </summary>
    private const string CopyTitle = "Copy Image";

    /// <summary>
    ///     The extension of PNG files.
    /// </summary>
    private const string PngExtension = ".png";

    /// <summary>
    ///     The compound extension of Vega-Lite files, whose stem is the part before it.
    /// </summary>
    private const string VegaLiteExtension = ".vl.json";

    /// <summary>
    ///     Provides the picture to export.
    /// </summary>
    private readonly IRenderedImageSource _source;

    /// <summary>
    ///     Encodes the picture as PNG.
    /// </summary>
    private readonly IImageRenderService _encoder;

    /// <summary>
    ///     Optional logger for diagnostics.
    /// </summary>
    private readonly RollingFileLogger? _logger;

    /// <summary>
    ///     Supplies the sizes to offer, or <see langword="null" /> when the picture is always exported as it is shown.
    /// </summary>
    private readonly IExportPresetSource? _presets;

    /// <summary>
    ///     The way that the user chose the last time, for the length of the session, or <see langword="null" /> when
    ///     there was none.
    /// </summary>
    private ExportPreset? _lastChoice;

    /// <summary>
    ///     Initializes a new instance of the <see cref="ImageExporter" /> class.
    /// </summary>
    /// <param name="source">Provides the picture to export.</param>
    /// <param name="encoder">Encodes the picture as PNG and draws it again at a size.</param>
    /// <param name="logger">Optional logger for diagnostics.</param>
    /// <param name="presets">
    ///     Supplies the sizes to offer; <see langword="null" /> to export the picture as it is shown without asking.
    /// </param>
    public ImageExporter(
        IRenderedImageSource source,
        IImageRenderService encoder,
        RollingFileLogger? logger = null,
        IExportPresetSource? presets = null)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(encoder);

        _source = source;
        _encoder = encoder;
        _logger = logger;
        _presets = presets;
        _source.CurrentImageChanged += OnCurrentImageChanged;
    }

    /// <inheritdoc />
    public event EventHandler? CanExportChanged;

    /// <inheritdoc />
    public bool CanExport => _source.CurrentImage is not null;

    /// <summary>
    ///     Gets the PNG file name suggested for the picture of a file: the file's name without its image source
    ///     extension, followed by <c>.png</c>.
    /// </summary>
    /// <param name="sourcePath">The path of the image source file.</param>
    /// <returns>The suggested file name.</returns>
    public static string SuggestFileName(string sourcePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourcePath);

        var name = Path.GetFileName(sourcePath);
        var stem = name.EndsWith(VegaLiteExtension, StringComparison.OrdinalIgnoreCase)
            ? name[..^VegaLiteExtension.Length]
            : Path.GetFileNameWithoutExtension(name);
        return (stem.Length == 0 ? "image" : stem) + PngExtension;
    }

    /// <inheritdoc />
    public async Task ExportPngAsync(IExportDialogs dialogs)
    {
        ArgumentNullException.ThrowIfNull(dialogs);

        if (_source.CurrentImage is not { } image)
        {
            return;
        }

        // Offer the sizes when there are any; the picture as it is shown is always the first and the old behavior
        var choice = ExportPreset.AsRendered;
        var offered = _presets?.GetPresets();
        if (offered is { Count: > 0 })
        {
            var choices = new List<ExportPreset>(offered.Count + 1) { ExportPreset.AsRendered };
            choices.AddRange(offered);
            if (await dialogs.PickExportPresetAsync(choices, _lastChoice) is not { } picked)
            {
                return;
            }

            _lastChoice = picked;
            choice = picked;
        }

        var chosen = await dialogs.PickSaveFileAsync(ExportTitle, SuggestFileName(image.SourcePath), "PNG image", "*" + PngExtension);
        if (chosen is null)
        {
            return;
        }

        var path = string.Equals(Path.GetExtension(chosen), PngExtension, StringComparison.OrdinalIgnoreCase)
            ? chosen
            : chosen + PngExtension;
        try
        {
            // The picture as shown is encoded as it is; a size means drawing the source file again at that size, which
            // is sharper than stretching the shown pixels, and any failure of that is shown and writes no file
            byte[] bytes;
            if (choice.IsAsRendered)
            {
                bytes = _encoder.EncodePng(image);
            }
            else
            {
                var folder = _source.ProjectFolder ?? Path.GetDirectoryName(image.SourcePath) ?? string.Empty;
                var result = await _encoder.RenderAsync(image.SourcePath, folder, choice.ToRenderSettings(), CancellationToken.None);
                if (result.Image is not { } drawn)
                {
                    if (result.Error is { } error)
                    {
                        Log(LogLevel.Warning, $"'{image.SourcePath}' could not be drawn for the preset '{choice.Name}': {error.Code}: {error.Message}");
                        await dialogs.ShowMessageAsync(ExportTitle, $"The picture could not be drawn at that size: {error.Message}");
                    }

                    return;
                }

                bytes = _encoder.EncodePng(drawn, choice.Dpi);
            }

            await File.WriteAllBytesAsync(path, bytes);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException or ArgumentException)
        {
            Log(LogLevel.Error, $"The image could not be exported to '{path}'.", ex);
            await dialogs.ShowMessageAsync(ExportTitle, $"The image could not be saved: {ex.Message}");
            return;
        }

        Log(LogLevel.Info, $"Exported '{image.SourcePath}' to '{path}'.");
    }

    /// <inheritdoc />
    public async Task CopyImageAsync(IExportDialogs dialogs)
    {
        ArgumentNullException.ThrowIfNull(dialogs);

        if (_source.CurrentImage is not { } image)
        {
            return;
        }

        if (!await dialogs.CopyImageToClipboardAsync(image))
        {
            Log(LogLevel.Warning, $"The image of '{image.SourcePath}' could not be copied to the clipboard.");
            await dialogs.ShowMessageAsync(CopyTitle, "The image could not be copied to the clipboard.");
        }
    }


    /// <summary>
    ///     Tells listeners that the ability to export may have changed.
    /// </summary>
    /// <param name="sender">The picture source.</param>
    /// <param name="e">The event data.</param>
    private void OnCurrentImageChanged(object? sender, EventArgs e)
    {
        CanExportChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    ///     Writes a diagnostic entry when a logger is available. Failing to log never blocks an export.
    /// </summary>
    /// <param name="level">Severity of the event.</param>
    /// <param name="message">Event text.</param>
    /// <param name="exception">Optional failure context.</param>
    private void Log(LogLevel level, string message, Exception? exception = null)
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
