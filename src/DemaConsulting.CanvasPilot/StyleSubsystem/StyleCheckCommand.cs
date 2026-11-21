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
using DemaConsulting.CanvasPilot.PreviewSubsystem;

namespace DemaConsulting.CanvasPilot.StyleSubsystem;

/// <summary>
///     The check of the shown picture against the style kit of the open project, as the user asks for it: it takes the
///     picture that is shown, measures its colors against the palette of the kit on a worker thread, and shows what it found
///     among the notes of the picture.
/// </summary>
/// <remarks>
///     <para>
///         The notes replace the notes of an earlier check and go away when the picture is drawn again, because they
///         describe that picture; a check whose picture was replaced while it ran is dropped. The unit changes nothing but the
///         notes: it does not write a file and does not change the style of the project.
///     </para>
///     <para>
///         The result is applied by the code that follows the wait for the worker, which runs on the synchronization
///         context of the thread that called <see cref="RunAsync" />, so the notes are changed on the thread that owns the
///         user interface when the command is run from it.
///     </para>
/// </remarks>
public sealed class StyleCheckCommand : ICheckAgainstStyle, IDisposable
{
    /// <summary>
    ///     Gives the picture that is shown.
    /// </summary>
    private readonly IRenderedImageSource _images;

    /// <summary>
    ///     Says which kit the open project uses.
    /// </summary>
    private readonly IActiveStyle _active;

    /// <summary>
    ///     Takes the notes of the check.
    /// </summary>
    private readonly IRenderNoteSink _notes;

    /// <summary>
    ///     Optional logger.
    /// </summary>
    private readonly RollingFileLogger? _logger;

    /// <summary>
    ///     Whether the command has been disposed.
    /// </summary>
    private bool _disposed;

    /// <summary>
    ///     Initializes a new instance of the <see cref="StyleCheckCommand" /> class.
    /// </summary>
    /// <param name="images">Gives the picture that is shown, and says when it changes.</param>
    /// <param name="active">Says which kit the open project uses.</param>
    /// <param name="notes">Takes the notes of the check.</param>
    /// <param name="logger">Optional logger for diagnostics.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="images" />, <paramref name="active" />, or <paramref name="notes" /> is <see langword="null" />.</exception>
    public StyleCheckCommand(IRenderedImageSource images, IActiveStyle active, IRenderNoteSink notes, RollingFileLogger? logger = null)
    {
        ArgumentNullException.ThrowIfNull(images);
        ArgumentNullException.ThrowIfNull(active);
        ArgumentNullException.ThrowIfNull(notes);

        _images = images;
        _active = active;
        _notes = notes;
        _logger = logger;
        _images.CurrentImageChanged += OnCurrentImageChanged;
    }

    /// <inheritdoc />
    public event EventHandler? CanCheckChanged;

    /// <inheritdoc />
    public bool CanCheck => _images.CurrentImage is not null;

    /// <inheritdoc />
    public async Task<string?> RunAsync()
    {
        // Take the picture once: it is the picture that is checked, and the one the result must still belong to
        var image = _images.CurrentImage;
        if (image is null)
        {
            return "There is no picture to check. Select a picture first.";
        }

        var entry = _active.Active;
        if (entry is null)
        {
            return _active.IsMissing
                ? $"The style kit '{_active.ActiveId}' of this project cannot be found. Choose another style kit in the status bar."
                : "This project has no style kit. Choose a style kit in the status bar first.";
        }

        StyleCheckReport report;
        try
        {
            // The measure walks every pixel, so it does not run on the thread of the user interface
            report = await Task.Run(() => StyleChecker.Check(image, entry.Kit));
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            Log(LogLevel.Error, $"The picture '{image.SourcePath}' could not be checked against the style kit '{entry.Kit.Id}'.", ex);
            return $"The picture could not be checked: {ex.Message}";
        }

        // The notes describe the picture that was measured; a picture that replaced it has its own notes
        if (!_disposed && ReferenceEquals(_images.CurrentImage, image))
        {
            _notes.ShowNotes(StyleCheckReport.NoteGroup, report.ToNotes());
        }

        return null;
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _images.CurrentImageChanged -= OnCurrentImageChanged;
    }

    /// <summary>
    ///     Announces that there may now be a picture to check, or none.
    /// </summary>
    /// <param name="sender">The source of the pictures.</param>
    /// <param name="e">The event data.</param>
    private void OnCurrentImageChanged(object? sender, EventArgs e)
    {
        CanCheckChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    ///     Writes a diagnostic entry when a logger is available. Failing to log never blocks the caller.
    /// </summary>
    /// <param name="level">Severity of the event.</param>
    /// <param name="message">Event text.</param>
    /// <param name="exception">The failure, or <see langword="null" />.</param>
    private void Log(LogLevel level, string message, Exception? exception = null)
    {
        try
        {
            _logger?.Log(level, message, exception);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ObjectDisposedException)
        {
            // Logging must never break the check.
        }
    }
}
