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
using System.Text.RegularExpressions;

namespace DemaConsulting.CanvasPilot.ChatSubsystem;

/// <summary>
///     A message of the user that was restored from the chat history of the project, shown as words only: the text with
///     the labels of its marks and attachments in their places, and a line for each mark note and attachment. It has no
///     pictures, because the history keeps none, and no payload to inspect.
/// </summary>
/// <remarks>
///     The row is immutable and holds nothing to release. It is the row of the user in the transcript, like
///     <see cref="TranscriptEntryViewModel" />, but it is not one of the <see cref="ChatPanelViewModel.SentEntries" />,
///     because it was not sent in this session.
/// </remarks>
public sealed partial class RestoredEntryViewModel : TranscriptRowViewModel
{
    /// <summary>
    ///     What the row says about itself, so that the lack of pictures is not a surprise.
    /// </summary>
    public const string RestoredNote = "Saved text from an earlier session; pictures are not kept.";

    /// <summary>
    ///     Initializes a new instance of the <see cref="RestoredEntryViewModel" /> class.
    /// </summary>
    /// <param name="row">The saved row of a message of the user.</param>
    /// <exception cref="ArgumentNullException">Thrown when the row is <see langword="null" />.</exception>
    /// <exception cref="ArgumentException">Thrown when the row is not the row of a message of the user.</exception>
    public RestoredEntryViewModel(ChatHistoryRow row)
    {
        ArgumentNullException.ThrowIfNull(row);
        if (row.Kind != ChatHistoryRowKind.User)
        {
            throw new ArgumentException("Only the row of a message of the user can be shown as a restored message.", nameof(row));
        }

        Row = row;
        Text = ChipLabelWithoutSeparator().Replace(row.Text, "$1 ");
        SentText = row.Utc.ToLocalTime().ToString("g", CultureInfo.CurrentCulture);
        var marks = row.Marks ?? [];
        var attachments = row.Attachments ?? [];
        Summary = BuildSummary(marks.Count, attachments.Count);
        DetailsText = string.Join(
            '\n',
            marks.OrderBy(mark => mark.Number).Select(DescribeMark).Concat(attachments.Select(path => $"Attachment: {path}")));
    }

    /// <inheritdoc />
    public override TranscriptRowKind Kind => TranscriptRowKind.User;

    /// <summary>
    ///     Gets the saved row.
    /// </summary>
    public ChatHistoryRow Row { get; }

    /// <summary>
    ///     Gets the words of the message with the labels of its marks and attachments in their places, with a space after a
    ///     label that was saved with no separator before the words.
    /// </summary>
    public string Text { get; }

    /// <summary>
    ///     Gets the date and time at which the message was sent, in the user's local time.
    /// </summary>
    public string SentText { get; }

    /// <summary>
    ///     Gets a short statement of what the message carried besides words, or an empty string.
    /// </summary>
    public string Summary { get; }

    /// <summary>
    ///     Gets one line for each mark that has a note and each attachment, or an empty string when there is none.
    /// </summary>
    public string DetailsText { get; }

    /// <summary>
    ///     Gets a value indicating whether the row has lines about marks or attachments.
    /// </summary>
    public bool HasDetails => DetailsText.Length > 0;

    /// <summary>
    ///     Finds the label of a chip that is followed by a word with nothing between them, as rows saved before the chips
    ///     were put on a line of their own hold them (<c>[Mark 1]Chart</c>); a space is put after such a label.
    /// </summary>
    /// <returns>The pattern.</returns>
    [GeneratedRegex(@"(\[(?:Mark \d+|Attachment: [^\]\r\n]*)\])(?=[\p{L}\p{N}])", RegexOptions.CultureInvariant)]
    private static partial Regex ChipLabelWithoutSeparator();

    /// <summary>
    ///     Writes the line about a mark.
    /// </summary>
    /// <param name="mark">The mark.</param>
    /// <returns>The line.</returns>
    private static string DescribeMark(ChatHistoryMark mark)
    {
        return string.IsNullOrWhiteSpace(mark.Note)
            ? string.Create(CultureInfo.InvariantCulture, $"Mark {mark.Number}")
            : string.Create(CultureInfo.InvariantCulture, $"Mark {mark.Number}: {mark.Note}");
    }

    /// <summary>
    ///     Writes the short statement of what the message carried besides words.
    /// </summary>
    /// <param name="marks">The number of marks.</param>
    /// <param name="attachments">The number of attachments.</param>
    /// <returns>The statement, or an empty string.</returns>
    private static string BuildSummary(int marks, int attachments)
    {
        var parts = new List<string>();
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
}
