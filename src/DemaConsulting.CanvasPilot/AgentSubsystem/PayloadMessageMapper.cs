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
using DemaConsulting.CanvasPilot.PayloadSubsystem;
using DemaConsulting.CanvasPilot.PreviewSubsystem;
using Microsoft.Extensions.AI;

namespace DemaConsulting.CanvasPilot.AgentSubsystem;

/// <summary>
///     Turns the message that the user sent (<see cref="MessagePayload" />) into the user message that a model is given:
///     the words of the user followed by a compact block of text that says what is attached and what the marks are, and the
///     pictures as image content.
/// </summary>
/// <remarks>
///     <para>
///         <b>Text.</b> The text of the message comes first, unchanged (it already holds <c>[Attachment n: name]</c> and
///         <c>[Mark n]</c> at the places the user put them). When the message has attachments or pictures, a block that
///         starts with <c>[Message context]</c> follows: the attachments (index, kind, name, and the path relative to the
///         project folder), the pictures (the source path relative to the project folder, the format, and the size in
///         pixels), and for each mark its number, shape, note, bounding box and close-up box in the pixels of its picture,
///         and up to <see cref="MaxSourceElementsPerMark" /> parts of the source that lie under it, with a statement when
///         parts were left out or when the picture cannot say which parts lie under the mark. The block ends with a list of
///         the images in the order in which they follow, so that a model can tell which image is which.
///     </para>
///     <para>
///         <b>Images.</b> Each annotated picture comes first, as PNG image content, in the order of the pictures. The
///         close-ups of the marks follow, in the order of the numbers of the marks, at most <see cref="MaxCrops" /> of
///         them; a sentence in the text says how many marks have no close-up and which ones.
///     </para>
///     <para>
///         <b>Privacy.</b> Only paths relative to the project folder are written: the place where the user had an
///         attachment is never sent, and a picture whose file is not inside the project is named by its file name only.
///         Every value that the user or a file supplies (names, notes, texts of elements) is written on one line and is
///         cut at <see cref="MaxFieldCharacters" /> characters, so that it cannot add a line to the block.
///     </para>
///     <para>
///         The mapping is pure and deterministic: the same payload and folder give the same text and the same images in the
///         same order, with line feeds as line breaks, numbers rounded to whole pixels, and no dependence on the culture or
///         the clock. The unit holds no state and is safe for concurrent use.
///     </para>
/// </remarks>
public static class PayloadMessageMapper
{
    /// <summary>
    ///     The media type of every image that is sent.
    /// </summary>
    public const string ImageMediaType = "image/png";

    /// <summary>
    ///     The most close-ups of marks that are sent.
    /// </summary>
    public const int MaxCrops = 10;

    /// <summary>
    ///     The most parts of the source that are written for one mark.
    /// </summary>
    public const int MaxSourceElementsPerMark = 5;

    /// <summary>
    ///     The most characters of a name, note, or text that are written; more is cut with an ellipsis.
    /// </summary>
    public const int MaxFieldCharacters = 200;

    /// <summary>
    ///     The line that starts the block with the context of the message.
    /// </summary>
    internal const string ContextHeader = "[Message context]";

    /// <summary>
    ///     Maps a message to the user message that a model is given.
    /// </summary>
    /// <param name="payload">The message that the user sent.</param>
    /// <param name="projectFolder">The full path of the project folder, to which the paths that are written are relative.</param>
    /// <returns>
    ///     A message of the role user whose first content is the text, followed by the annotated pictures and then the
    ///     close-ups, each as a PNG.
    /// </returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="payload" /> or <paramref name="projectFolder" /> is <see langword="null" />.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="projectFolder" /> is empty or white space.</exception>
    public static ChatMessage Map(MessagePayload payload, string projectFolder)
    {
        ArgumentNullException.ThrowIfNull(payload);
        ArgumentException.ThrowIfNullOrWhiteSpace(projectFolder);

        // The close-ups of the marks of all pictures, in the order of their numbers, and the ones that are sent
        var marks = payload.Images
            .SelectMany((image, index) => image.Marks.Select(mark => (Picture: index + 1, Mark: mark)))
            .OrderBy(entry => entry.Mark.Number)
            .ThenBy(entry => entry.Picture)
            .ToList();
        var sent = marks.Take(MaxCrops).ToList();
        var left = marks.Skip(MaxCrops).ToList();

        var contents = new List<AIContent> { new TextContent(BuildText(payload, projectFolder, sent, left)) };
        for (var index = 0; index < payload.Images.Length; index++)
        {
            contents.Add(new DataContent(payload.Images[index].AnnotatedPng, ImageMediaType) { Name = $"picture-{index + 1}.png" });
        }

        foreach (var (_, mark) in sent)
        {
            contents.Add(new DataContent(mark.CropPng, ImageMediaType) { Name = $"mark-{mark.Number}.png" });
        }

        return new ChatMessage(ChatRole.User, contents);
    }

    /// <summary>
    ///     Writes the text of the message: the words of the user and the block with the context.
    /// </summary>
    /// <param name="payload">The message.</param>
    /// <param name="projectFolder">The project folder.</param>
    /// <param name="sent">The marks whose close-ups are sent, with the number of their picture.</param>
    /// <param name="left">The marks whose close-ups are left out.</param>
    /// <returns>The text.</returns>
    private static string BuildText(
        MessagePayload payload,
        string projectFolder,
        List<(int Picture, PayloadMark Mark)> sent,
        List<(int Picture, PayloadMark Mark)> left)
    {
        var text = new StringBuilder(payload.Text.Replace("\r\n", "\n", StringComparison.Ordinal).TrimEnd());
        if (payload.Attachments.Length == 0 && payload.Images.Length == 0)
        {
            return text.ToString();
        }

        if (text.Length > 0)
        {
            text.Append("\n\n");
        }

        text.Append(ContextHeader);
        AppendAttachments(text, payload);
        AppendPictures(text, payload, projectFolder);
        AppendImageList(text, payload, sent, left);
        return text.ToString();
    }

    /// <summary>
    ///     Writes the attachments: index, kind, name, and the path relative to the project folder.
    /// </summary>
    /// <param name="text">The text to add to.</param>
    /// <param name="payload">The message.</param>
    private static void AppendAttachments(StringBuilder text, MessagePayload payload)
    {
        if (payload.Attachments.Length == 0)
        {
            return;
        }

        text.Append("\nAttachments (paths are relative to the project folder):");
        foreach (var attachment in payload.Attachments)
        {
            text.Append("\n- Attachment ").Append(Number(attachment.Index)).Append(": ")
                .Append(attachment.Kind.ToString().ToLowerInvariant()).Append(" \"").Append(Clean(attachment.Name))
                .Append("\" at ").Append(Clean(attachment.ProjectRelativePath.Replace('\\', '/')));
            if (attachment.Kind == ComposerSubsystem.AttachmentKind.Folder && attachment.FileCount > 0)
            {
                text.Append(" (").Append(Number(attachment.FileCount)).Append(attachment.FileCount == 1 ? " file)" : " files)");
            }
        }
    }

    /// <summary>
    ///     Writes the pictures and their marks.
    /// </summary>
    /// <param name="text">The text to add to.</param>
    /// <param name="payload">The message.</param>
    /// <param name="projectFolder">The project folder.</param>
    private static void AppendPictures(StringBuilder text, MessagePayload payload, string projectFolder)
    {
        if (payload.Images.Length == 0)
        {
            return;
        }

        text.Append("\nPictures with marks (all positions are in picture pixels, with the origin at the top-left corner):");
        for (var index = 0; index < payload.Images.Length; index++)
        {
            var image = payload.Images[index];
            text.Append("\n- Picture ").Append(Number(index + 1)).Append(": ").Append(Clean(RelativeSource(image.SourcePath, projectFolder)))
                .Append(" (").Append(Clean(image.Format)).Append(", ").Append(Number(image.Width)).Append(" x ")
                .Append(Number(image.Height)).Append(" px)");
            foreach (var mark in image.Marks)
            {
                AppendMark(text, mark);
            }
        }
    }

    /// <summary>
    ///     Writes one mark: its number, shape, note, boxes, and the parts of the source under it.
    /// </summary>
    /// <param name="text">The text to add to.</param>
    /// <param name="mark">The mark.</param>
    private static void AppendMark(StringBuilder text, PayloadMark mark)
    {
        text.Append("\n  - Mark ").Append(Number(mark.Number)).Append(": ").Append(mark.Kind.ToString().ToLowerInvariant());
        text.Append(string.IsNullOrWhiteSpace(mark.Note) ? "; no note" : "; note \"" + Clean(mark.Note) + "\"");
        text.Append("; box ").Append(Box(mark.BoundingBox)).Append("; close-up ").Append(Box(mark.CropBox));

        text.Append("\n    Source elements: ");
        if (!mark.SourceElementsAvailable)
        {
            var reason = string.IsNullOrWhiteSpace(mark.SourceElementsUnavailableReason) ? "the picture cannot say" : Clean(mark.SourceElementsUnavailableReason);
            text.Append("not available (").Append(reason).Append(')');
            return;
        }

        if (mark.SourceElements.Length == 0)
        {
            text.Append("none under the mark");
            return;
        }

        text.Append(string.Join("; ", mark.SourceElements.Take(MaxSourceElementsPerMark).Select(Element)));
        if (mark.SourceElementsTruncated || mark.SourceElements.Length > MaxSourceElementsPerMark)
        {
            text.Append("; more elements are left out");
        }
    }

    /// <summary>
    ///     Writes the list of the images in the order in which they follow the text, and the sentence about the close-ups that
    ///     are left out.
    /// </summary>
    /// <param name="text">The text to add to.</param>
    /// <param name="payload">The message.</param>
    /// <param name="sent">The marks whose close-ups are sent.</param>
    /// <param name="left">The marks whose close-ups are left out.</param>
    private static void AppendImageList(
        StringBuilder text,
        MessagePayload payload,
        List<(int Picture, PayloadMark Mark)> sent,
        List<(int Picture, PayloadMark Mark)> left)
    {
        if (payload.Images.Length == 0)
        {
            return;
        }

        text.Append("\nImages that follow this text, in order:");
        for (var index = 0; index < payload.Images.Length; index++)
        {
            text.Append("\n- Image ").Append(Number(index + 1)).Append(": picture ").Append(Number(index + 1)).Append(" with all its marks drawn on it");
        }

        var imageNumber = payload.Images.Length;
        foreach (var (picture, mark) in sent)
        {
            imageNumber++;
            text.Append("\n- Image ").Append(Number(imageNumber)).Append(": close-up of mark ").Append(Number(mark.Number))
                .Append(" (picture ").Append(Number(picture)).Append(')');
        }

        if (left.Count > 0)
        {
            var numbers = string.Join(", ", left.Select(entry => Number(entry.Mark.Number)));
            text.Append("\nClose-ups left out to save room: ").Append(Number(left.Count)).Append(left.Count == 1 ? " more mark (" : " more marks (")
                .Append(numbers).Append("); their boxes are given above.");
        }
    }

    /// <summary>
    ///     Writes one part of the source under a mark.
    /// </summary>
    /// <param name="element">The part.</param>
    /// <returns>The text: kind, text, and identifier, line, and box when they are known.</returns>
    private static string Element(SourceElementReference element)
    {
        var text = new StringBuilder(string.IsNullOrWhiteSpace(element.Kind) ? "element" : Clean(element.Kind));
        if (!string.IsNullOrWhiteSpace(element.Text))
        {
            text.Append(" \"").Append(Clean(element.Text)).Append('"');
        }

        var details = new List<string>();
        if (!string.IsNullOrWhiteSpace(element.Id))
        {
            details.Add("id " + Clean(element.Id));
        }

        if (element.Line is { } line)
        {
            details.Add("line " + Number(line));
        }

        if (element.Box is { } box)
        {
            details.Add("box " + Box(box));
        }

        if (details.Count > 0)
        {
            text.Append(" (").Append(string.Join(", ", details)).Append(')');
        }

        return text.ToString();
    }

    /// <summary>
    ///     Writes a box in whole pixels.
    /// </summary>
    /// <param name="box">The box.</param>
    /// <returns>The text <c>x=1, y=2, w=3, h=4</c>.</returns>
    private static string Box(RenderBox box)
    {
        return $"x={Pixels(box.X)}, y={Pixels(box.Y)}, w={Pixels(box.Width)}, h={Pixels(box.Height)}";
    }

    /// <summary>
    ///     Rounds a number of pixels to a whole number, with halves away from zero, and writes it without a culture.
    /// </summary>
    /// <param name="value">The number.</param>
    /// <returns>The text.</returns>
    private static string Pixels(double value)
    {
        // A tiny negative number rounds to negative zero, which would be written as -0
        var rounded = Math.Round(value, MidpointRounding.AwayFromZero);
        return rounded == 0 ? "0" : rounded.ToString("0", CultureInfo.InvariantCulture);
    }

    /// <summary>
    ///     Writes a whole number without a culture.
    /// </summary>
    /// <param name="value">The number.</param>
    /// <returns>The text.</returns>
    private static string Number(int value)
    {
        return value.ToString(CultureInfo.InvariantCulture);
    }

    /// <summary>
    ///     Writes the path of the file of a picture relative to the project folder, with forward slashes; a file that is not
    ///     inside the project is written by its file name only.
    /// </summary>
    /// <param name="sourcePath">The full path of the file.</param>
    /// <param name="projectFolder">The project folder.</param>
    /// <returns>The path.</returns>
    private static string RelativeSource(string sourcePath, string projectFolder)
    {
        var relative = Path.GetRelativePath(projectFolder, sourcePath);
        var inside = !Path.IsPathRooted(relative) && relative != ".." && !relative.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal)
            && !relative.StartsWith("../", StringComparison.Ordinal);
        return inside ? relative.Replace('\\', '/') : Path.GetFileName(sourcePath) + " (outside the project)";
    }

    /// <summary>
    ///     Makes a value that the user or a file supplied fit on one line: every run of white space or control characters
    ///     becomes one space, double quotes become single quotes so that a value cannot end its own quotation, and a value
    ///     longer than <see cref="MaxFieldCharacters" /> is cut with an ellipsis.
    /// </summary>
    /// <param name="value">The value.</param>
    /// <returns>The line.</returns>
    private static string Clean(string value)
    {
        var builder = new StringBuilder(value.Length);
        var space = false;
        foreach (var c in value.Trim())
        {
            if (char.IsWhiteSpace(c) || char.IsControl(c))
            {
                space = true;
                continue;
            }

            if (space)
            {
                builder.Append(' ');
                space = false;
            }

            builder.Append(c == '"' ? '\'' : c);
        }

        return builder.Length <= MaxFieldCharacters ? builder.ToString() : builder.ToString(0, MaxFieldCharacters - 3) + "...";
    }
}
