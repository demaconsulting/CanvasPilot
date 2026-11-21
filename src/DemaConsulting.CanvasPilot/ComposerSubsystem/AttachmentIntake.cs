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

using System.Collections.Immutable;
using System.Globalization;
using DemaConsulting.CanvasPilot.LoggingSubsystem;

namespace DemaConsulting.CanvasPilot.ComposerSubsystem;

/// <summary>
///     Something about dropped or pasted content that the user should be told: an item that was left out and why, or an
///     item that was accepted with a warning.
/// </summary>
/// <param name="Item">What the note is about: the path, or a description such as <c>pasted picture 2</c>.</param>
/// <param name="Reason">A sentence that says what is wrong.</param>
public sealed record IntakeNote(string Item, string Reason);

/// <summary>
///     What the intake made of dropped or pasted content.
/// </summary>
public sealed class AttachmentIntakeResult
{
    /// <summary>
    ///     Initializes a new instance of the <see cref="AttachmentIntakeResult" /> class.
    /// </summary>
    /// <param name="segments">The chips and text to put into the message, in order.</param>
    /// <param name="skipped">The items that were left out.</param>
    /// <param name="flagged">The items that were accepted with a warning.</param>
    internal AttachmentIntakeResult(
        IEnumerable<ComposerSegment> segments,
        IEnumerable<IntakeNote> skipped,
        IEnumerable<IntakeNote> flagged)
    {
        Segments = [.. segments];
        Skipped = [.. skipped];
        Flagged = [.. flagged];
    }

    /// <summary>
    ///     Gets the chips and text for the message, in order: files, folders and pictures that were dropped, then
    ///     pictures that were pasted, then text. It is empty when nothing was accepted.
    /// </summary>
    public ImmutableArray<ComposerSegment> Segments { get; }

    /// <summary>
    ///     Gets the items that were left out, each with the reason, for example a path that does not exist or a name
    ///     that is not safe.
    /// </summary>
    public ImmutableArray<IntakeNote> Skipped { get; }

    /// <summary>
    ///     Gets the items that were accepted with a warning, for example a file that is too large to be sent.
    /// </summary>
    public ImmutableArray<IntakeNote> Flagged { get; }
}

/// <summary>
///     Turns dropped and pasted content into chips and text for the message. It decides what each dropped path is, a
///     file, a folder, or a picture, saves pasted pictures, and cleans pasted text, and it treats all of it strictly as
///     data: nothing that is dropped is opened, run, rendered, or followed.
/// </summary>
/// <remarks>
///     <para>
///         A dropped path is only looked at, never read: the intake asks the file system whether the path is a file or a
///         folder and how large a file is, which does not open it. A file with any extension, programs and scripts
///         included, therefore becomes a chip like any other, and nothing happens to it until the message is sent, when
///         the store copies its bytes. Paths that are not safe to refer to (relative paths, parts such as <c>..</c>,
///         device names, alternate data streams, the device namespace) are left out with a reason. Pictures that are
///         pasted are saved through the store, which checks them. Text is cleaned like all text of the message and
///         limited in length.
///     </para>
///     <para>
///         An operation is bounded: at most <see cref="MaximumItems" /> paths and pictures are accepted, and the rest
///         is reported as left out. Paths that name the same thing are accepted once. The intake does not change the
///         message; the caller puts the segments into it as one drop or paste edit. The type holds no state and is
///         safe for concurrent use as far as the store is.
///     </para>
/// </remarks>
public sealed class AttachmentIntake
{
    /// <summary>
    ///     The largest number of paths and pictures accepted from one drop or paste.
    /// </summary>
    public const int MaximumItems = 200;

    /// <summary>
    ///     The largest number of characters of text accepted from one drop or paste.
    /// </summary>
    public const int MaximumTextLength = 100_000;

    /// <summary>
    ///     The file extensions of the pictures that become picture chips.
    /// </summary>
    private static readonly HashSet<string> ImageExtensions =
        new([".png", ".jpg", ".jpeg", ".gif", ".bmp", ".webp", ".svg", ".tif", ".tiff"], StringComparer.OrdinalIgnoreCase);

    /// <summary>
    ///     Where pasted pictures are saved.
    /// </summary>
    private readonly IAttachmentStore _store;

    /// <summary>
    ///     Optional logger for diagnostics.
    /// </summary>
    private readonly RollingFileLogger? _logger;

    /// <summary>
    ///     Initializes a new instance of the <see cref="AttachmentIntake" /> class.
    /// </summary>
    /// <param name="store">Where pasted pictures are saved.</param>
    /// <param name="logger">Optional logger for diagnostics.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="store" /> is <see langword="null" />.</exception>
    public AttachmentIntake(IAttachmentStore store, RollingFileLogger? logger = null)
    {
        ArgumentNullException.ThrowIfNull(store);
        _store = store;
        _logger = logger;
    }

    /// <summary>
    ///     Makes the chips and text for the content of one drop or paste.
    /// </summary>
    /// <param name="content">The dropped or pasted content.</param>
    /// <returns>The segments to insert, with the items that were left out or flagged and why.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="content" /> is <see langword="null" />.</exception>
    public AttachmentIntakeResult Accept(DroppedContent content)
    {
        ArgumentNullException.ThrowIfNull(content);

        var segments = new List<ComposerSegment>();
        var skipped = new List<IntakeNote>();
        var flagged = new List<IntakeNote>();
        var seen = new HashSet<string>(AttachmentNames.PathComparer);

        // Paths first, in the order given, then pasted pictures; both count toward the bound of one operation
        var accepted = 0;
        foreach (var path in content.Paths)
        {
            if (accepted >= MaximumItems)
            {
                skipped.Add(new IntakeNote(path ?? string.Empty, $"Only {MaximumItems} items are accepted at a time."));
                continue;
            }

            var chip = ClassifyPath(path, seen, skipped, flagged);
            if (chip is not null)
            {
                segments.Add(chip);
                accepted++;
            }
        }

        for (var index = 0; index < content.Images.Count; index++)
        {
            var label = string.Create(CultureInfo.InvariantCulture, $"pasted picture {index + 1}");
            if (accepted >= MaximumItems)
            {
                skipped.Add(new IntakeNote(label, $"Only {MaximumItems} items are accepted at a time."));
                continue;
            }

            var chip = SaveImage(content.Images[index], label, skipped);
            if (chip is not null)
            {
                segments.Add(chip);
                accepted++;
            }
        }

        // Text is cleaned like all text of the message and limited in length
        AddText(content.Text, segments, flagged);

        foreach (var note in skipped)
        {
            Log(LogLevel.Info, $"Left out '{note.Item}': {note.Reason}");
        }

        return new AttachmentIntakeResult(segments, skipped, flagged);
    }

    /// <summary>
    ///     Decides what a dropped path is and makes its chip, without opening it.
    /// </summary>
    /// <param name="path">The path as the operating system gave it.</param>
    /// <param name="seen">The paths accepted so far in this operation.</param>
    /// <param name="skipped">Receives the reason when the path is left out.</param>
    /// <param name="flagged">Receives a warning when the path is accepted with one.</param>
    /// <returns>The chip, or <see langword="null" /> when the path was left out.</returns>
    private static AttachmentChip? ClassifyPath(string? path, HashSet<string> seen, List<IntakeNote> skipped, List<IntakeNote> flagged)
    {
        var item = path ?? string.Empty;

        // Refuse paths that are not safe to refer to before touching the file system at all
        var problem = AttachmentNames.FindProblemWithPath(path);
        if (problem is not null)
        {
            skipped.Add(new IntakeNote(item, problem));
            return null;
        }

        try
        {
            var full = Path.TrimEndingDirectorySeparator(Path.GetFullPath(path!));
            if (full.Length == 0 || Path.GetPathRoot(full) == full)
            {
                skipped.Add(new IntakeNote(item, "A whole drive cannot be attached."));
                return null;
            }

            // The same thing named twice is accepted once
            if (!seen.Add(full))
            {
                skipped.Add(new IntakeNote(item, "The same item was dropped twice."));
                return null;
            }

            // Asking whether it is a folder or a file does not open it
            if (Directory.Exists(full))
            {
                return new FolderChip(full);
            }

            if (!File.Exists(full))
            {
                skipped.Add(new IntakeNote(item, "The item does not exist."));
                return null;
            }

            if (new FileInfo(full).Length > AttachmentStore.MaximumBytes)
            {
                flagged.Add(new IntakeNote(item, $"The file is larger than {AttachmentStore.MaximumBytes / 1024 / 1024} MB, so the message cannot be sent with it."));
            }

            return ImageExtensions.Contains(Path.GetExtension(full)) ? new ImageChip(full) : new FileChip(full);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            skipped.Add(new IntakeNote(item, $"The item could not be read: {ex.Message}"));
            return null;
        }
    }

    /// <summary>
    ///     Saves a pasted picture through the store and makes its chip.
    /// </summary>
    /// <param name="data">The PNG bytes.</param>
    /// <param name="label">What to call the picture in a note.</param>
    /// <param name="skipped">Receives the reason when the picture is left out.</param>
    /// <returns>The chip, or <see langword="null" /> when the picture was left out.</returns>
    private ImageChip? SaveImage(ReadOnlyMemory<byte> data, string label, List<IntakeNote> skipped)
    {
        try
        {
            return new ImageChip(_store.SavePastedImage(data), isPasted: true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
        {
            skipped.Add(new IntakeNote(label, $"The picture could not be saved: {ex.Message}"));
            return null;
        }
    }

    /// <summary>
    ///     Adds dropped or pasted text to the segments, cleaned and limited.
    /// </summary>
    /// <param name="text">The text, or <see langword="null" />.</param>
    /// <param name="segments">Receives the text segment.</param>
    /// <param name="flagged">Receives a warning when the text was shortened.</param>
    private static void AddText(string? text, List<ComposerSegment> segments, List<IntakeNote> flagged)
    {
        if (string.IsNullOrEmpty(text))
        {
            return;
        }

        var cleaned = ComposerDocument.SanitizeText(text);
        if (cleaned.Length > MaximumTextLength)
        {
            cleaned = cleaned[..MaximumTextLength];
            flagged.Add(new IntakeNote("text", $"The text was shortened to {MaximumTextLength} characters."));
        }

        if (cleaned.Length > 0)
        {
            segments.Add(new TextSegment(cleaned, TextOrigin.Typed));
        }
    }

    /// <summary>
    ///     Writes a diagnostic entry when a logger is available. Failing to log never blocks an intake.
    /// </summary>
    /// <param name="level">Severity of the event.</param>
    /// <param name="message">Event text.</param>
    private void Log(LogLevel level, string message)
    {
        try
        {
            _logger?.Log(level, message);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Best effort: diagnostics must not make a drop fail.
        }
    }
}
