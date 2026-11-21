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
using System.Text;
using DemaConsulting.CanvasPilot.AnnotationSubsystem;
using DemaConsulting.CanvasPilot.ComposerSubsystem;
using DemaConsulting.CanvasPilot.PreviewSubsystem;

namespace DemaConsulting.CanvasPilot.PayloadSubsystem;

/// <summary>
///     Builds the <see cref="MessagePayload" /> of a message that is being sent from what the composer held, the marks
///     that were drawn, and the attachments that were put into the project: the text with its references in order, the
///     attachments with their paths, and, for each picture that marks were drawn on, the annotated image, the area
///     around each mark, the geometry of each mark in picture pixels, and the parts of the source file under it.
/// </summary>
/// <remarks>
///     <para>
///         The builder reads only values that cannot change: a <see cref="ComposerSnapshot" />, the list of marks as it
///         was when the message was sent, and the staged attachments. It can therefore run on a background thread while
///         the user interface goes on, and nothing the user does meanwhile can change the payload. Drawing the marks is
///         done by the <see cref="IMarkImageComposer" />, so the builder can be tested without drawing.
///     </para>
///     <para>
///         A mark is part of the message exactly while its chip is in the snapshot: marks in the list without a chip are
///         left out, and so are the pictures that only they were drawn on. A chip whose mark is not in the list is an
///         error, because the message would then name a mark that cannot be shown. The builder holds no state and is
///         safe for concurrent use when its collaborators are.
///     </para>
/// </remarks>
public sealed class PayloadBuilder : IMessageBuilder
{
    /// <summary>
    ///     Makes the annotated pictures and the areas around the marks.
    /// </summary>
    private readonly IMarkImageComposer _composer;

    /// <summary>
    ///     The source of the time at which the payload is built.
    /// </summary>
    private readonly TimeProvider _timeProvider;

    /// <summary>
    ///     Initializes a new instance of the <see cref="PayloadBuilder" /> class.
    /// </summary>
    /// <param name="composer">Draws the marks on their pictures and cuts the areas around them.</param>
    /// <param name="timeProvider">The source of the time of building; the system clock when <see langword="null" />.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="composer" /> is <see langword="null" />.</exception>
    public PayloadBuilder(IMarkImageComposer composer, TimeProvider? timeProvider = null)
    {
        ArgumentNullException.ThrowIfNull(composer);

        _composer = composer;
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    /// <summary>
    ///     Builds the payload of a message.
    /// </summary>
    /// <param name="message">What the composer held when the message was sent.</param>
    /// <param name="marks">All marks that were made for the draft; only those whose chips are in the message are used.</param>
    /// <param name="attachments">
    ///     The attachments as they are in the project, one for each attachment chip of the message, in the order of the
    ///     chips (see <see cref="IAttachmentStore.StageForSend" />).
    /// </param>
    /// <returns>The payload.</returns>
    /// <exception cref="ArgumentNullException">Thrown when an argument is <see langword="null" />.</exception>
    /// <exception cref="ArgumentException">
    ///     Thrown when the attachments are not those of the chips of the message, or a mark chip names a mark that is not in
    ///     <paramref name="marks" /> or shows another number than the mark has.
    /// </exception>
    /// <exception cref="InvalidOperationException">Thrown when the marks cannot be drawn on their pictures.</exception>
    public MessagePayload Build(ComposerSnapshot message, IReadOnlyList<Mark> marks, IReadOnlyList<StagedAttachment> attachments)
    {
        ArgumentNullException.ThrowIfNull(message);
        ArgumentNullException.ThrowIfNull(marks);
        ArgumentNullException.ThrowIfNull(attachments);

        // The attachments must be those of the chips, in order, or the references of the text would point at other things
        var chips = message.Chips.OfType<AttachmentChip>().ToList();
        if (chips.Count != attachments.Count || chips.Where((chip, index) => !chip.Equals(attachments[index].Chip)).Any())
        {
            throw new ArgumentException("The attachments are not the attachments of the message, in the order of its chips.", nameof(attachments));
        }

        // Walk the message once: the text with its references, the segments, and the marks in the order of their chips
        var text = new StringBuilder();
        var segments = new List<PayloadSegment>();
        var usedMarks = new List<Mark>();
        var attachmentIndex = 0;
        foreach (var segment in message.Segments)
        {
            switch (segment)
            {
                case TextSegment run:
                    text.Append(run.Text);
                    segments.Add(new PayloadTextPart(run.Text, run.Origin));
                    break;

                case AttachmentChip:
                    var staged = attachments[attachmentIndex];
                    attachmentIndex++;
                    text.Append(CultureInfo.InvariantCulture, $"[Attachment {attachmentIndex}: {staged.Name}]");
                    segments.Add(new PayloadAttachmentPart(attachmentIndex));
                    break;

                case MarkChip chip:
                    var mark = FindMark(marks, chip);
                    text.Append(CultureInfo.InvariantCulture, $"[Mark {mark.Number}]");
                    segments.Add(new PayloadMarkPart(mark.Number));
                    if (usedMarks.All(used => used.Id != mark.Id))
                    {
                        usedMarks.Add(mark);
                    }

                    break;
            }
        }

        // Make one image for each distinct picture that marks were drawn on, in the order of the first mark that uses it
        var images = usedMarks
            .GroupBy(mark => mark.Snapshot.SnapshotKey, StringComparer.Ordinal)
            .Select(group => BuildImage(group.First().Snapshot, [.. group.OrderBy(mark => mark.Number)]))
            .ToList();

        // Describe the attachments the way the reader finds them
        var described = attachments.Select((staged, index) => new PayloadAttachment(
            index + 1,
            staged.Kind,
            staged.Name,
            staged.OriginalPath,
            staged.ProjectRelativePath,
            staged.SizeBytes,
            staged.FileCount));
        return new MessagePayload(text.ToString(), segments, described, images, _timeProvider.GetUtcNow());
    }

    /// <summary>
    ///     Builds the payload of a message on a thread-pool thread, so that drawing the marks does not block the caller.
    /// </summary>
    /// <param name="message">What the composer held when the message was sent.</param>
    /// <param name="marks">All marks that were made for the draft.</param>
    /// <param name="attachments">The attachments as they are in the project, in the order of the chips.</param>
    /// <param name="cancellationToken">Cancels the build before it starts.</param>
    /// <returns>A task with the payload; it fails with the exceptions of <see cref="Build" />.</returns>
    public Task<MessagePayload> BuildAsync(
        ComposerSnapshot message,
        IReadOnlyList<Mark> marks,
        IReadOnlyList<StagedAttachment> attachments,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(message);
        ArgumentNullException.ThrowIfNull(marks);
        ArgumentNullException.ThrowIfNull(attachments);

        return Task.Run(() => Build(message, marks, attachments), cancellationToken);
    }

    /// <summary>
    ///     Builds the payload of a message and returns it as the sent message that the composer keeps: the message, the
    ///     marks whose chips are in it, the attachments, and the payload. This is how the composer, which the payload
    ///     subsystem uses and which therefore cannot use the payload subsystem, has its messages built.
    /// </summary>
    /// <param name="message">What the composer held when the message was sent.</param>
    /// <param name="marks">All marks that were made for the draft; only those whose chips are in the message are kept.</param>
    /// <param name="attachments">The attachments as they are in the project, in the order of the chips.</param>
    /// <param name="cancellationToken">Cancels the build before it starts.</param>
    /// <returns>A task with the sent message, whose payload is a <see cref="MessagePayload" />.</returns>
    async Task<SentMessage> IMessageBuilder.BuildAsync(
        ComposerSnapshot message,
        IReadOnlyList<Mark> marks,
        IReadOnlyList<StagedAttachment> attachments,
        CancellationToken cancellationToken)
    {
        var payload = await BuildAsync(message, marks, attachments, cancellationToken);

        // Keep the marks that are part of the message, in the order of their numbers
        var chipIds = message.Chips.OfType<MarkChip>().Select(chip => chip.MarkId).ToHashSet();
        var used = marks.Where(mark => chipIds.Contains(mark.Id)).OrderBy(mark => mark.Number);
        return new SentMessage(message, used, attachments, payload, payload.CreatedUtc);
    }

    /// <summary>
    ///     Finds the mark that a chip stands for and checks that the chip shows its number.
    /// </summary>
    /// <param name="marks">The marks that were made.</param>
    /// <param name="chip">The chip.</param>
    /// <returns>The mark.</returns>
    /// <exception cref="ArgumentException">Thrown when there is no such mark or the numbers differ.</exception>
    private static Mark FindMark(IReadOnlyList<Mark> marks, MarkChip chip)
    {
        var mark = marks.FirstOrDefault(candidate => candidate.Id == chip.MarkId)
            ?? throw new ArgumentException($"The message holds a chip for mark {chip.Number}, which was not made.", nameof(marks));
        if (mark.Number != chip.Number)
        {
            throw new ArgumentException($"The chip of mark {mark.Number} shows the number {chip.Number}.", nameof(marks));
        }

        return mark;
    }

    /// <summary>
    ///     Builds the image of one picture with its marks.
    /// </summary>
    /// <param name="picture">The picture.</param>
    /// <param name="marks">The marks that were drawn on it, in the order of their numbers.</param>
    /// <returns>The image.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the composer does not return an area for every mark.</exception>
    private PayloadImage BuildImage(RenderedImage picture, IReadOnlyList<Mark> marks)
    {
        // Draw the marks and cut the areas; the areas come in the order of the marks and must be all there
        var drawn = _composer.Compose(picture, marks);
        if (drawn.Crops.Length != marks.Count)
        {
            throw new InvalidOperationException($"{drawn.Crops.Length} areas were made for {marks.Count} marks.");
        }

        // Describe each mark with its area and the parts of the source file under it
        var described = new List<PayloadMark>(marks.Count);
        for (var i = 0; i < marks.Count; i++)
        {
            var mark = marks[i];
            var crop = drawn.Crops[i];
            var found = MarkSourceResolver.Resolve(mark);
            described.Add(new PayloadMark(
                mark.Number,
                mark.Kind,
                mark.Geometry.Points,
                mark.Geometry.BoundingBox,
                mark.Note,
                crop.Png,
                crop.Box,
                found.Elements,
                found.IsAvailable,
                found.UnavailableReason,
                found.IsTruncated));
        }

        return new PayloadImage(
            picture.SourcePath,
            picture.SourceFileHash,
            picture.ContentHash,
            picture.Format,
            picture.Width,
            picture.Height,
            drawn.AnnotatedPng,
            described);
    }
}
