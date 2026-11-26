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

using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Layout;
using Avalonia.Markup.Xaml.MarkupExtensions;
using Avalonia.Media;

namespace DemaConsulting.CanvasPilot.ChatSubsystem;

/// <summary>
///     Draws the blocks that <see cref="ReplyMarkdown" /> read from a reply of the copilot: headings, paragraphs,
///     list items, and blocks of code, with bold, italic, and code text, as text that the user can select and copy.
/// </summary>
/// <remarks>
///     The panel has a text block for each block, so that the text of a reply can be selected and copied as the text
///     of any page can. It is made again from the blocks every time <see cref="Blocks" /> changes, which is when a
///     reply is complete; the blocks are immutable, so nothing else changes them. Nothing in it can run or open
///     anything.
/// </remarks>
public sealed class MarkdownBlocksPanel : StackPanel
{
    /// <summary>
    ///     The blocks to draw.
    /// </summary>
    public static readonly StyledProperty<IReadOnlyList<MarkdownBlock>?> BlocksProperty =
        AvaloniaProperty.Register<MarkdownBlocksPanel, IReadOnlyList<MarkdownBlock>?>(nameof(Blocks));

    /// <summary>
    ///     The font of code, which is a fixed-width font on every platform.
    /// </summary>
    private static readonly FontFamily CodeFont = new("Cascadia Mono,Consolas,Menlo,monospace");

    /// <summary>
    ///     The sizes of the text of headings of the levels from 1 to 6.
    /// </summary>
    private static readonly double[] HeadingSizes = [20, 16, 14, 14, 14, 14];

    /// <summary>
    ///     Initializes a new instance of the <see cref="MarkdownBlocksPanel" /> class.
    /// </summary>
    public MarkdownBlocksPanel()
    {
        Spacing = 6;
    }

    /// <summary>
    ///     Gets or sets the blocks to draw.
    /// </summary>
    public IReadOnlyList<MarkdownBlock>? Blocks
    {
        get => GetValue(BlocksProperty);
        set => SetValue(BlocksProperty, value);
    }

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == BlocksProperty)
        {
            Rebuild(Blocks);
        }
    }

    /// <summary>
    ///     Draws blocks in place of what is drawn.
    /// </summary>
    /// <param name="blocks">The blocks, or <see langword="null" /> for none.</param>
    private void Rebuild(IReadOnlyList<MarkdownBlock>? blocks)
    {
        Children.Clear();
        if (blocks is null)
        {
            return;
        }

        foreach (var block in blocks)
        {
            Control? control = block switch
            {
                HeadingBlock heading => DrawHeading(heading),
                ListItemBlock item => DrawListItem(item),
                CodeBlock code => DrawCode(code),
                ParagraphBlock paragraph => DrawText(paragraph.Spans),
                _ => null
            };

            if (control is not null)
            {
                Children.Add(control);
            }
        }
    }

    /// <summary>
    ///     Draws a heading.
    /// </summary>
    /// <param name="heading">The heading.</param>
    /// <returns>The control.</returns>
    private static SelectableTextBlock DrawHeading(HeadingBlock heading)
    {
        var text = DrawText(heading.Spans);
        text.FontWeight = FontWeight.SemiBold;
        text.FontSize = HeadingSizes[Math.Clamp(heading.Level, 1, HeadingSizes.Length) - 1];
        text.Margin = new Thickness(0, 8, 0, 0);
        AutomationProperties.SetAutomationId(text, "ReplyMarkdownHeading");
        return text;
    }

    /// <summary>
    ///     Draws a list item: its marker, with a dot for a bullet, and its text.
    /// </summary>
    /// <param name="item">The item.</param>
    /// <returns>The control.</returns>
    private static Grid DrawListItem(ListItemBlock item)
    {
        var isBullet = item.Marker is "-" or "*" or "+";
        var marker = new TextBlock
        {
            Text = isBullet ? "\u2022" : item.Marker,
            MinWidth = 18,
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(0, 0, 6, 0)
        };
        var text = DrawText(item.Spans);
        AutomationProperties.SetAutomationId(text, "ReplyMarkdownItem");
        Grid.SetColumn(text, 1);

        var grid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("Auto,*"),
            Margin = new Thickness(12, 0, 0, 0)
        };
        grid.Children.Add(marker);
        grid.Children.Add(text);
        return grid;
    }

    /// <summary>
    ///     Draws a block of code: its lines in a fixed-width font, which scroll sideways when they are long.
    /// </summary>
    /// <param name="code">The block.</param>
    /// <returns>The control.</returns>
    private static Border DrawCode(CodeBlock code)
    {
        var text = new SelectableTextBlock
        {
            Text = code.Text,
            FontFamily = CodeFont,
            TextWrapping = TextWrapping.NoWrap
        };
        AutomationProperties.SetAutomationId(text, "ReplyMarkdownCode");

        var border = new Border
        {
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(10),
            Child = new ScrollViewer
            {
                HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto,
                VerticalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled,
                Content = text
            }
        };

        // The block is a raised surface of the theme, so it follows a change of the theme variant
        border.Bind(Border.BackgroundProperty, new DynamicResourceExtension("CpSurfaceRaised"));
        border.Bind(Border.BorderBrushProperty, new DynamicResourceExtension("CpBorderSubtle"));
        return border;
    }

    /// <summary>
    ///     Draws the runs of a paragraph, heading, or item as text that wraps.
    /// </summary>
    /// <param name="spans">The runs.</param>
    /// <returns>The control.</returns>
    private static SelectableTextBlock DrawText(IReadOnlyList<MarkdownSpan> spans)
    {
        var text = new SelectableTextBlock { TextWrapping = TextWrapping.Wrap };
        AutomationProperties.SetAutomationId(text, "ReplyMarkdownText");
        foreach (var span in spans)
        {
            var run = new Run(span.Text);
            switch (span.Style)
            {
                case MarkdownSpanStyle.Bold:
                    run.FontWeight = FontWeight.Bold;
                    break;
                case MarkdownSpanStyle.Italic:
                    run.FontStyle = FontStyle.Italic;
                    break;
                case MarkdownSpanStyle.Code:
                    run.FontFamily = CodeFont;
                    // The shade behind code is a token of the theme, so it follows a change of the theme variant
                    run.Bind(Inline.BackgroundProperty, text.GetResourceObservable("CpFillCode"));
                    break;
                default:
                    break;
            }

            text.Inlines!.Add(run);
        }

        return text;
    }
}
