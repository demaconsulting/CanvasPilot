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

using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using DemaConsulting.CanvasPilot.ComposerSubsystem;

namespace DemaConsulting.CanvasPilot.ChatSubsystem;

/// <summary>
///     The view of one sent message in the transcript: the words of the message and the chips of its attachments and
///     marks, in the order of the message, and the button that shows the payload.
/// </summary>
/// <remarks>
///     <para>
///         The message is shown as blocks one below the other: a read-only text block for each run of typed and dictated
///         words (dictated words have the same subtle background that they have in the editing control), and a wrapping
///         row for each run of chips, which hold the same <see cref="ChipView" /> control as the composer, without its
///         delete button, because a sent message cannot be changed. The chips are not put inside a line of words, because
///         a line is only as high as its words and would cut the chips off. The line break that the composer puts after
///         its row of chips is the break between the blocks. The text can be selected and copied. The view only shows
///         what its view model holds and never opens an attachment.
///     </para>
///     <para>
///         The picture of an attached picture comes from its copy in the project, which the view model names as the chip to
///         ask the provider about; a small adapter passes that chip to the provider while the chip control shows the chip
///         the user saw.
///     </para>
/// </remarks>
public sealed partial class TranscriptEntryView : UserControl
{
    /// <summary>
    ///     Initializes a new instance of the <see cref="TranscriptEntryView" /> class.
    /// </summary>
    public TranscriptEntryView()
    {
        InitializeComponent();
    }

    /// <summary>
    ///     Gets the chip controls shown in the rows of the message, in the order of the message.
    /// </summary>
    public IReadOnlyList<ChipView> Chips =>
        [.. MessageBlocks.Children.OfType<WrapPanel>().SelectMany(row => row.Children).OfType<ChipView>()];

    /// <summary>
    ///     Gets the text blocks of the message, in the order of the message. Each holds one run of words; the words of
    ///     a message that has chips between them are in more than one block.
    /// </summary>
    public IReadOnlyList<SelectableTextBlock> TextBlocks => [.. MessageBlocks.Children.OfType<SelectableTextBlock>()];

    /// <inheritdoc />
    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);

        // Lay out the words and the chips of the message once for the entry that is shown
        MessageBlocks.Children.Clear();
        if (DataContext is not TranscriptEntryViewModel entry)
        {
            return;
        }

        WrapPanel? chipRow = null;
        SelectableTextBlock? textBlock = null;
        foreach (var item in entry.Items)
        {
            if (item.Chip is { } chip)
            {
                // Consecutive chips share one row that wraps, so that the bubble grows with them
                if (chipRow is null)
                {
                    chipRow = new WrapPanel { Orientation = Orientation.Horizontal, ItemSpacing = 4, LineSpacing = 4 };
                    MessageBlocks.Children.Add(chipRow);
                }

                textBlock = null;
                var control = new ChipView { IsDeleteButtonVisible = false };
                control.Show(chip, new MappedThumbnailProvider(entry.Thumbnails, item.ThumbnailChip ?? chip));
                chipRow.Children.Add(control);
                continue;
            }

            // The line break that the composer puts after the row of chips is the break between the blocks here
            var text = item.Text ?? string.Empty;
            if (chipRow is not null && textBlock is null)
            {
                text = text.StartsWith('\n') ? text[1..] : text;
            }

            chipRow = null;
            if (text.Length == 0)
            {
                continue;
            }

            if (textBlock is null)
            {
                textBlock = CreateTextBlock();
                MessageBlocks.Children.Add(textBlock);
            }

            var run = new Run(text);
            if (item.Origin == TextOrigin.Dictated)
            {
                // The subtle shade behind dictated words is a token of the theme, so it follows a change of the theme variant
                run.Bind(Inline.BackgroundProperty, textBlock.GetResourceObservable("CpFillDictated"));
            }

            textBlock.Inlines!.Add(run);
        }
    }

    /// <summary>
    ///     Makes a text block for a run of words in the style of the bubble.
    /// </summary>
    /// <returns>The empty text block.</returns>
    private static SelectableTextBlock CreateTextBlock()
    {
        // The colors and sizes come from the style of the view, which follows the theme tokens
        var block = new SelectableTextBlock { TextWrapping = TextWrapping.Wrap, Inlines = [] };
        AutomationProperties.SetAutomationId(block, "TranscriptMessageText");
        return block;
    }

    /// <summary>
    ///     Asks a provider for the picture of another chip than the one that is shown: the copy of an attached picture in
    ///     the project instead of the file the user dropped.
    /// </summary>
    private sealed class MappedThumbnailProvider : IChipThumbnailProvider
    {
        /// <summary>
        ///     The provider that makes the pictures.
        /// </summary>
        private readonly IChipThumbnailProvider _inner;

        /// <summary>
        ///     The chip that is asked for.
        /// </summary>
        private readonly ChipSegment _target;

        /// <summary>
        ///     Initializes a new instance of the <see cref="MappedThumbnailProvider" /> class.
        /// </summary>
        /// <param name="inner">The provider that makes the pictures.</param>
        /// <param name="target">The chip to ask it for.</param>
        public MappedThumbnailProvider(IChipThumbnailProvider inner, ChipSegment target)
        {
            _inner = inner;
            _target = target;
        }

        /// <inheritdoc />
        public Task<Bitmap?> GetThumbnailAsync(ChipSegment chip, CancellationToken cancellationToken) =>
            _inner.GetThumbnailAsync(_target, cancellationToken);
    }
}
