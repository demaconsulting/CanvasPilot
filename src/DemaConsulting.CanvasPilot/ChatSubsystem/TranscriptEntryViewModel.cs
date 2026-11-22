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

using System.Globalization;
using CommunityToolkit.Mvvm.Input;
using DemaConsulting.CanvasPilot.ComposerSubsystem;
using DemaConsulting.CanvasPilot.PayloadSubsystem;
using DemaConsulting.CanvasPilot.UtilitiesSubsystem;

namespace DemaConsulting.CanvasPilot.ChatSubsystem;

/// <summary>
///     One sent message in the transcript: the words in the order they were written, the chips of the attachments and
///     marks in their places with their pictures, and the command that shows exactly what the assistant would receive.
/// </summary>
/// <remarks>
///     <para>
///         The entry is made from the immutable <see cref="SentMessage" />, so nothing in it changes after it is shown.
///         The text keeps the origin of every piece, so the transcript can show dictated words differently from typed ones.
///         The picture of a mark is the cut-out that the payload already holds, and the picture of an attached picture is
///         the copy of the file in the project, so the transcript shows what was sent and not what the files look like now
///         outside the project.
///     </para>
///     <para>
///         The payload of a message is the <see cref="MessagePayload" /> of the payload subsystem. A message whose payload
///         is something else (as a test builder may make) has no payload to inspect, and its command cannot run. The
///         entry owns the picture provider it made and releases it in <see cref="Dispose" />. It is owned by the user
///         interface thread.
///     </para>
///     <para>
///         The entry is the row of the user in the transcript (<see cref="TranscriptRowViewModel" />); the answer of the
///         copilot, the tools it called, and the notices are other rows.
///     </para>
/// </remarks>
public sealed class TranscriptEntryViewModel : TranscriptRowViewModel
{
    /// <summary>
    ///     Gives the launcher of the inspector at the time the command runs, because the launcher can be set after the
    ///     entry was made.
    /// </summary>
    private readonly Func<IPayloadInspectorLauncher?> _launcher;

    /// <summary>
    ///     Gives the pictures of the chips.
    /// </summary>
    private readonly ChipThumbnailProvider _thumbnails;

    /// <summary>
    ///     Initializes a new instance of the <see cref="TranscriptEntryViewModel" /> class.
    /// </summary>
    /// <param name="message">The message that was sent.</param>
    /// <param name="projectFolder">The folder of the project the message was sent in, or <see langword="null" /> when unknown.</param>
    /// <param name="launcher">Gives what shows the payload inspector, or <see langword="null" /> from the function when nothing does.</param>
    /// <param name="files">
    ///     Previews attached picture files, or <see langword="null" /> for the standard preview, which the entry owns.
    /// </param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="message" /> or <paramref name="launcher" /> is <see langword="null" />.</exception>
    public TranscriptEntryViewModel(
        SentMessage message,
        string? projectFolder,
        Func<IPayloadInspectorLauncher?> launcher,
        IChipThumbnailProvider? files = null)
    {
        ArgumentNullException.ThrowIfNull(message);
        ArgumentNullException.ThrowIfNull(launcher);

        Message = message;
        _launcher = launcher;
        Payload = message.Payload as MessagePayload;
        _thumbnails = new ChipThumbnailProvider(CropOfMark, files);
        Items = BuildItems(message, projectFolder);
        Summary = BuildSummary(message);
        SentText = message.SentUtc.ToLocalTime().ToString("t", CultureInfo.CurrentCulture);
        ViewPayloadCommand = new AsyncRelayCommand(ViewPayloadAsync, () => HasPayload);
    }

    /// <inheritdoc />
    public override TranscriptRowKind Kind => TranscriptRowKind.User;

    /// <summary>
    ///     Gets the message that was sent.
    /// </summary>
    public SentMessage Message { get; }

    /// <summary>
    ///     Gets the payload of the message, or <see langword="null" /> when it has none that can be shown.
    /// </summary>
    public MessagePayload? Payload { get; }

    /// <summary>
    ///     Gets a value indicating whether the message has a payload that the inspector can show.
    /// </summary>
    public bool HasPayload => Payload is not null;

    /// <summary>
    ///     Gets the pieces of the message in the order they were written: runs of text and chips.
    /// </summary>
    public IReadOnlyList<TranscriptItem> Items { get; }

    /// <summary>
    ///     Gets the picture provider for the chips of this entry.
    /// </summary>
    public IChipThumbnailProvider Thumbnails => _thumbnails;

    /// <summary>
    ///     Gets a short statement of what the message carried besides words, for example <c>2 marks, 1 attachment</c>, or
    ///     an empty string when it carried nothing else.
    /// </summary>
    public string Summary { get; }

    /// <summary>
    ///     Gets the time at which the message was sent, in the user's local time.
    /// </summary>
    public string SentText { get; }

    /// <summary>
    ///     Gets the command that shows what the assistant would receive for this message.
    /// </summary>
    public IAsyncRelayCommand ViewPayloadCommand { get; }

    /// <summary>
    ///     Releases the pictures that were made for the chips.
    /// </summary>
    /// <param name="disposing"><see langword="true" /> when called from the public method, <see langword="false" /> from a finalizer.</param>
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _thumbnails.Dispose();
        }

        base.Dispose(disposing);
    }

    /// <summary>
    ///     Makes the pieces of the transcript text out of the segments of the message.
    /// </summary>
    /// <param name="message">The message.</param>
    /// <param name="projectFolder">The folder of the project, or <see langword="null" />.</param>
    /// <returns>The pieces, without empty runs of text.</returns>
    private static List<TranscriptItem> BuildItems(SentMessage message, string? projectFolder)
    {
        var items = new List<TranscriptItem>();
        var attachmentIndex = 0;
        foreach (var segment in message.Message.Segments)
        {
            switch (segment)
            {
                case TextSegment text when text.Text.Length > 0:
                    items.Add(new TranscriptItem(text.Text, text.Origin));
                    break;

                case AttachmentChip attachment:
                    // The n-th attachment chip is the n-th staged attachment; a picture is shown from its copy in the project
                    var staged = attachmentIndex < message.Attachments.Length ? message.Attachments[attachmentIndex] : null;
                    attachmentIndex++;
                    items.Add(new TranscriptItem(attachment, ProjectCopyOf(attachment, staged, projectFolder)));
                    break;

                case ChipSegment chip:
                    items.Add(new TranscriptItem(chip, chip));
                    break;
            }
        }

        return items;
    }

    /// <summary>
    ///     Finds the chip to ask for the picture of an attachment: a chip for the copy in the project when it is a picture
    ///     and has one, and the chip itself otherwise.
    /// </summary>
    /// <param name="chip">The chip of the attachment.</param>
    /// <param name="staged">What was staged for it, or <see langword="null" />.</param>
    /// <param name="projectFolder">The folder of the project, or <see langword="null" />.</param>
    /// <returns>The chip to ask for the picture.</returns>
    private static ChipSegment ProjectCopyOf(AttachmentChip chip, StagedAttachment? staged, string? projectFolder)
    {
        if (staged is not { Kind: AttachmentKind.Image } || projectFolder is null)
        {
            return chip;
        }

        try
        {
            return new ImageChip(PathHelpers.SafePathCombine(projectFolder, staged.ProjectRelativePath));
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or IOException)
        {
            // A path that cannot be made safe is never opened; the chip keeps its icon
            return chip;
        }
    }

    /// <summary>
    ///     Writes the short statement of what the message carried besides words.
    /// </summary>
    /// <param name="message">The message.</param>
    /// <returns>The statement, or an empty string.</returns>
    private static string BuildSummary(SentMessage message)
    {
        var parts = new List<string>();
        var marks = message.Message.Chips.OfType<MarkChip>().Count();
        var attachments = message.Message.Chips.OfType<AttachmentChip>().Count();
        if (marks > 0)
        {
            parts.Add(string.Create(CultureInfo.InvariantCulture, $"{marks} {(marks == 1 ? "mark" : "marks")}"));
        }

        if (attachments > 0)
        {
            parts.Add(string.Create(CultureInfo.InvariantCulture, $"{attachments} {(attachments == 1 ? "attachment" : "attachments")}"));
        }

        return string.Join(", ", parts);
    }

    /// <summary>
    ///     Finds the cut-out of a mark in the payload.
    /// </summary>
    /// <param name="chip">The chip of the mark.</param>
    /// <returns>The PNG bytes, or <see langword="null" /> when the payload holds no such mark.</returns>
    private byte[]? CropOfMark(MarkChip chip)
    {
        var mark = Payload?.Images.SelectMany(image => image.Marks).FirstOrDefault(candidate => candidate.Number == chip.Number);
        return mark?.CropPng.ToArray();
    }

    /// <summary>
    ///     Shows the payload inspector for this message.
    /// </summary>
    /// <returns>A task that completes when the user has closed the inspector, or at once when nothing can show it.</returns>
    private async Task ViewPayloadAsync()
    {
        var launcher = _launcher();
        if (Payload is null || launcher is null)
        {
            return;
        }

        // The window releases the view model when it closes; this is the safeguard for a launcher that never shows it
        var viewModel = new PayloadInspectorViewModel(Payload);
        try
        {
            await launcher.ShowPayloadInspectorAsync(viewModel);
        }
        finally
        {
            viewModel.Dispose();
        }
    }
}
