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

using System.Text.RegularExpressions;
using DemaConsulting.CanvasPilot.LoggingSubsystem;
using DemaConsulting.CanvasPilot.UtilitiesSubsystem;

namespace DemaConsulting.CanvasPilot.PreviewSubsystem;

/// <summary>
///     Prepares the shown picture for dragging it out of the application into another program, such as a
///     presentation, a mail message, or a folder. Programs that accept a dropped picture ask for a file or for image
///     data, so the picture is written as a PNG file in a folder that belongs to the application, which the view
///     then offers together with the picture's pixels. The file is named after the source file and the first
///     characters of the picture's fingerprint, so the same picture is written once. The unit has no dependency on
///     the user interface.
/// </summary>
public sealed partial class ImageDragSource
{
    /// <summary>
    ///     The number of characters of the picture's fingerprint that are part of the file name.
    /// </summary>
    private const int HashLength = 8;

    /// <summary>
    ///     The largest number of characters of the source file's name that are part of the file name.
    /// </summary>
    private const int MaximumStemLength = 60;

    /// <summary>
    ///     The suffix of a file that is still being written.
    /// </summary>
    private const string PendingSuffix = ".tmp";

    /// <summary>
    ///     Encodes the picture as PNG.
    /// </summary>
    private readonly IImageRenderService _encoder;

    /// <summary>
    ///     Optional logger for diagnostics.
    /// </summary>
    private readonly RollingFileLogger? _logger;

    /// <summary>
    ///     Initializes a new instance of the <see cref="ImageDragSource" /> class.
    /// </summary>
    /// <param name="encoder">Encodes the picture as PNG.</param>
    /// <param name="folder">The folder that holds the files prepared for dragging; it is created when needed.</param>
    /// <param name="logger">Optional logger for diagnostics.</param>
    public ImageDragSource(IImageRenderService encoder, string folder, RollingFileLogger? logger = null)
    {
        ArgumentNullException.ThrowIfNull(encoder);
        ArgumentException.ThrowIfNullOrWhiteSpace(folder);

        _encoder = encoder;
        Folder = folder;
        _logger = logger;
    }

    /// <summary>
    ///     Gets the folder in which the application keeps the files it prepares for dragging: a folder named
    ///     <c>drag</c> in the application's folder in the per-user local application data folder.
    /// </summary>
    public static string DefaultFolder =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "CanvasPilot", "drag");

    /// <summary>
    ///     Gets the folder that holds the files prepared for dragging.
    /// </summary>
    public string Folder { get; }

    /// <summary>
    ///     Writes a picture as a PNG file for dragging, unless the same picture was written already.
    /// </summary>
    /// <param name="image">The picture.</param>
    /// <returns>
    ///     The full path of the PNG file, named after the picture's source file and the start of its pixel
    ///     fingerprint, or <see langword="null" /> when the file could not be written. Problems are logged.
    /// </returns>
    public string? Prepare(RenderedImage image)
    {
        ArgumentNullException.ThrowIfNull(image);

        var path = string.Empty;
        try
        {
            path = PathHelpers.SafePathCombine(Folder, FileNameFor(image));
            if (File.Exists(path) && new FileInfo(path).Length > 0)
            {
                return path;
            }

            // Write beside the target and move into place, so a file that was cut short is never mistaken for the
            // picture the next time.
            Directory.CreateDirectory(Folder);
            var pending = path + PendingSuffix;
            File.WriteAllBytes(pending, _encoder.EncodePng(image));
            File.Move(pending, path, overwrite: true);
            return path;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            Log(LogLevel.Warning, $"The picture of '{image.SourcePath}' could not be prepared for dragging.", ex);
            if (path.Length > 0)
            {
                TryDelete(path + PendingSuffix);
            }

            return null;
        }
    }

    /// <summary>
    ///     Deletes the files this unit wrote in its folder, and nothing else: files with other names, folders, and
    ///     anything inside folders stay.
    /// </summary>
    /// <returns>The number of files deleted.</returns>
    public int Purge()
    {
        var deleted = 0;
        try
        {
            if (!Directory.Exists(Folder))
            {
                return 0;
            }

            foreach (var file in Directory.EnumerateFiles(Folder))
            {
                var name = Path.GetFileName(file);
                if (!OwnFileName().IsMatch(name))
                {
                    continue;
                }

                if (TryDelete(file))
                {
                    deleted++;
                }
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Log(LogLevel.Warning, $"The folder '{Folder}' for dragged pictures could not be cleaned up.", ex);
        }

        return deleted;
    }

    /// <summary>
    ///     Gets the name of the file for a picture: the source file's name without its extension (limited in length
    ///     and without characters that are not allowed in file names), a hyphen, the start of the pixel fingerprint,
    ///     and <c>.png</c>.
    /// </summary>
    /// <param name="image">The picture.</param>
    /// <returns>The file name.</returns>
    private static string FileNameFor(RenderedImage image)
    {
        var suggested = ImageExporter.SuggestFileName(image.SourcePath);
        var stem = suggested[..^".png".Length];
        var characters = stem
            .Select(character => Array.IndexOf(Path.GetInvalidFileNameChars(), character) >= 0 ? '_' : character)
            .Take(MaximumStemLength)
            .ToArray();
        return $"{new string(characters).TrimEnd('.', ' ')}-{image.ContentHash[..HashLength]}.png";
    }

    /// <summary>
    ///     Matches the names of the files this unit writes: a stem, a hyphen, eight hexadecimal digits, and
    ///     <c>.png</c>, optionally followed by the suffix of a file that is still being written.
    /// </summary>
    /// <returns>The regular expression.</returns>
    [GeneratedRegex(@"^.+-[0-9a-f]{8}\.png(\.tmp)?$", RegexOptions.CultureInvariant)]
    private static partial Regex OwnFileName();

    /// <summary>
    ///     Deletes a file, ignoring a file that is missing or in use.
    /// </summary>
    /// <param name="path">The file.</param>
    /// <returns><see langword="true" /> when the file was deleted.</returns>
    private bool TryDelete(string path)
    {
        try
        {
            if (!File.Exists(path))
            {
                return false;
            }

            File.Delete(path);
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
        {
            Log(LogLevel.Warning, $"The file '{path}' could not be deleted.", ex);
            return false;
        }
    }

    /// <summary>
    ///     Writes a diagnostic entry when a logger is available. Failing to log never blocks a drag.
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
