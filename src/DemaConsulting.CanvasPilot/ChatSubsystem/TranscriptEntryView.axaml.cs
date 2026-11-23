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

using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using DemaConsulting.CanvasPilot.ComposerSubsystem;

namespace DemaConsulting.CanvasPilot.ChatSubsystem;

/// <summary>
///     The view of one sent message in the transcript: the words of the message with the chips of its attachments and
///     marks standing in their places inside the line, and the button that shows the payload.
/// </summary>
/// <remarks>
///     <para>
///         The message is shown in a read-only text block whose runs are the typed and dictated words (dictated words have
///         the same subtle background that they have in the editing control) and whose inline containers hold the same
///         <see cref="ChipView" /> control as the composer, without its delete button, because a sent message cannot be
///         changed. The text can be selected and copied. The view only shows what its view model holds and never opens an
///         attachment.
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
    ///     Gets the chip controls shown in the text of the message, in the order of the message.
    /// </summary>
    public IReadOnlyList<ChipView> Chips =>
        [.. MessageText.Inlines?.OfType<InlineUIContainer>().Select(container => container.Child).OfType<ChipView>() ?? []];

    /// <inheritdoc />
    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);

        // Lay out the words and the chips of the message once for the entry that is shown
        MessageText.Inlines?.Clear();
        if (DataContext is not TranscriptEntryViewModel entry)
        {
            return;
        }

        MessageText.Inlines ??= [];
        foreach (var item in entry.Items)
        {
            if (item.Chip is { } chip)
            {
                var control = new ChipView { IsDeleteButtonVisible = false };
                control.Show(chip, new MappedThumbnailProvider(entry.Thumbnails, item.ThumbnailChip ?? chip));
                MessageText.Inlines.Add(new InlineUIContainer(control));
            }
            else
            {
                var run = new Run(item.Text);
                if (item.Origin == TextOrigin.Dictated)
                {
                    // The subtle shade behind dictated words is a token of the theme, so it follows a change of the theme variant
                    run.Bind(Inline.BackgroundProperty, MessageText.GetResourceObservable("CpFillDictated"));
                }

                MessageText.Inlines.Add(run);
            }
        }
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
