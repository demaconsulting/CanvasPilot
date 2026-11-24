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
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;

namespace DemaConsulting.CanvasPilot.ComposerSubsystem;

/// <summary>
///     The control that shows one chip inside a line of the message: an icon for the kind of chip, or the picture of
///     the item when there is one, the name of the item, and a button that deletes the chip. The tooltip shows the full
///     path of an attachment, or says that a mark is on the picture, with its note when it has one.
/// </summary>
/// <remarks>
///     The control only shows the chip and reports that its delete button was pressed or that the user asks to change
///     the note of a mark, by a double-click or the Edit note item of its context menu; it never changes the message and
///     never opens the item the chip stands for. The path is only displayed. The delete button does not take the focus,
///     so deleting a chip with the mouse leaves the caret in the message.
/// </remarks>
public sealed partial class ChipView : UserControl
{
    /// <summary>
    ///     The icon of a file: a sheet with a folded corner.
    /// </summary>
    private const string FileIcon = "M3,1 L10,1 L14,5 L14,15 L3,15 Z M10,1 L10,5 L14,5";

    /// <summary>
    ///     The icon of a folder: a folder with a tab.
    /// </summary>
    private const string FolderIcon = "M1,3 L6,3 L8,5 L15,5 L15,14 L1,14 Z";

    /// <summary>
    ///     The icon of a picture: a frame with a hill and a sun.
    /// </summary>
    private const string ImageIcon = "M1,2 L15,2 L15,14 L1,14 Z M2,13 L6,8 L9,11 L11,9 L14,13 Z";

    /// <summary>
    ///     The icon of a mark: a ring with a dot.
    /// </summary>
    private const string MarkIcon = "M8,1 A7,7 0 1 0 8,15 A7,7 0 1 0 8,1 Z M8,5 A3,3 0 1 1 8,11 A3,3 0 1 1 8,5 Z";

    /// <summary>
    ///     Gives the picture of the chip, or <see langword="null" /> for none.
    /// </summary>
    private IChipThumbnailProvider? _provider;

    /// <summary>
    ///     Cancels the request for the picture when the chip is no longer shown.
    /// </summary>
    private CancellationTokenSource? _cancellation;

    /// <summary>
    ///     Whether the chip of a mark offers to change the mark's note.
    /// </summary>
    private bool _isNoteEditable;

    /// <summary>
    ///     Initializes a new instance of the <see cref="ChipView" /> class, which shows no chip until <see cref="Show" />
    ///     is called.
    /// </summary>
    public ChipView()
    {
        InitializeComponent();
        DeleteButton.Click += OnDeleteClicked;
    }

    /// <summary>
    ///     Raised when the user presses the delete button of the chip.
    /// </summary>
    public event EventHandler? DeleteRequested;

    /// <summary>
    ///     Raised when the user asks to change the note of the mark of the chip: by a double-click on the chip or by the
    ///     Edit note item of its context menu. Only raised for the chip of a mark while <see cref="IsNoteEditable" /> is
    ///     set.
    /// </summary>
    public event EventHandler? EditNoteRequested;

    /// <summary>
    ///     Gets the chip that is shown, or <see langword="null" /> before <see cref="Show" /> was called.
    /// </summary>
    public ChipSegment? Chip { get; private set; }

    /// <summary>
    ///     Gets or sets a value indicating whether the chip of a mark offers to change the mark's note, by a
    ///     double-click and by the Edit note item of its context menu. It is off by default; the message being written
    ///     turns it on, and the transcript of sent messages leaves it off, because a sent message cannot be changed.
    /// </summary>
    public bool IsNoteEditable
    {
        get => _isNoteEditable;
        set
        {
            _isNoteEditable = value;
            UpdateNoteEditing();
        }
    }

    /// <summary>
    ///     Gets or sets a value indicating whether the delete button is shown. It is shown by default, in the message being
    ///     written; the transcript of sent messages hides it, because a sent message cannot be changed.
    /// </summary>
    public bool IsDeleteButtonVisible
    {
        get => DeleteButton.IsVisible;
        set => DeleteButton.IsVisible = value;
    }

    /// <summary>
    ///     Gets the text that the chip shows: the name of the attached item, or the label of the mark.
    /// </summary>
    /// <param name="chip">The chip.</param>
    /// <returns>The text.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="chip" /> is <see langword="null" />.</exception>
    public static string GetLabel(ChipSegment chip)
    {
        ArgumentNullException.ThrowIfNull(chip);
        return chip switch
        {
            AttachmentChip attachment => attachment.Name,
            MarkChip mark => mark.Label,
            _ => "Item",
        };
    }

    /// <summary>
    ///     Gets the tooltip of the chip of a mark: that the mark is on the picture, followed by its note when it has one.
    /// </summary>
    /// <param name="chip">The chip of the mark.</param>
    /// <param name="note">The note of the mark, or <see langword="null" /> or empty for none.</param>
    /// <returns>The text, as "Mark 2 on the picture" or "Mark 2 on the picture: the note".</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="chip" /> is <see langword="null" />.</exception>
    public static string GetMarkToolTip(MarkChip chip, string? note)
    {
        ArgumentNullException.ThrowIfNull(chip);
        return string.IsNullOrEmpty(note) ? $"{chip.Label} on the picture" : $"{chip.Label} on the picture: {note}";
    }

    /// <summary>
    ///     Shows a chip.
    /// </summary>
    /// <param name="chip">The chip to show.</param>
    /// <param name="provider">Gives the picture of the chip, or <see langword="null" /> to show only the icon.</param>
    /// <param name="note">
    ///     The note of the mark of a mark chip, which its tooltip shows, or <see langword="null" /> for none; ignored for
    ///     other chips.
    /// </param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="chip" /> is <see langword="null" />.</exception>
    public void Show(ChipSegment chip, IChipThumbnailProvider? provider, string? note = null)
    {
        ArgumentNullException.ThrowIfNull(chip);
        Chip = chip;
        _provider = provider;
        LabelText.Text = GetLabel(chip);
        ChipBorder.Classes.Set("mark", chip is MarkChip);
        Avalonia.Automation.AutomationProperties.SetName(DeleteButton, $"Remove {GetLabel(chip)}");
        ToolTip.SetTip(DeleteButton, $"Remove {GetLabel(chip)}");
        IconPath.Data = Geometry.Parse(chip switch
        {
            FolderChip => FolderIcon,
            ImageChip => ImageIcon,
            MarkChip => MarkIcon,
            _ => FileIcon,
        });

        // The full path of an attachment is only displayed; a mark says what it is, with its note
        ToolTip.SetTip(this, chip switch
        {
            AttachmentChip attachment => attachment.FullPath,
            MarkChip mark => GetMarkToolTip(mark, note),
            _ => null,
        });
        Avalonia.Automation.AutomationProperties.SetName(this, GetLabel(chip));
        UpdateNoteEditing();
    }

    /// <inheritdoc />
    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);

        // A double-click on the chip of a mark asks to change its note, and is kept from selecting a word of the message
        if (!e.Handled && e.ClickCount == 2 && _isNoteEditable && Chip is MarkChip && e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            EditNoteRequested?.Invoke(this, EventArgs.Empty);
            e.Handled = true;
        }
    }

    /// <inheritdoc />
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);

        // Ask for the picture only when the chip is really shown, and give up when it goes away
        if (Chip is not null && _provider is not null)
        {
            _cancellation?.Cancel();
            _cancellation = new CancellationTokenSource();
            _ = LoadThumbnailAsync(Chip, _provider, _cancellation.Token);
        }
    }

    /// <inheritdoc />
    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        _cancellation?.Cancel();
        _cancellation?.Dispose();
        _cancellation = null;
        base.OnDetachedFromVisualTree(e);
    }

    /// <summary>
    ///     Asks for the picture of the chip and shows it in place of the icon when it comes.
    /// </summary>
    /// <param name="chip">The chip.</param>
    /// <param name="provider">Gives the picture.</param>
    /// <param name="cancellationToken">Cancelled when the chip is no longer shown.</param>
    /// <returns>A task that completes when the picture is shown, there is none, or the chip is gone.</returns>
    private async Task LoadThumbnailAsync(ChipSegment chip, IChipThumbnailProvider provider, CancellationToken cancellationToken)
    {
        try
        {
            var bitmap = await provider.GetThumbnailAsync(chip, cancellationToken);
            if (bitmap is not null && !cancellationToken.IsCancellationRequested)
            {
                ThumbnailImage.Source = bitmap;
                ThumbnailImage.IsVisible = true;
                IconPath.IsVisible = false;
            }
        }
        catch (OperationCanceledException)
        {
            // The chip went away before its picture came; nothing to show
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            // A provider that fails leaves the icon; a provider is asked not to throw, but a chip must never break typing
        }
    }

    /// <summary>
    ///     Reports that the delete button was pressed.
    /// </summary>
    /// <param name="sender">The button.</param>
    /// <param name="e">The event data.</param>
    private void OnDeleteClicked(object? sender, RoutedEventArgs e)
    {
        e.Handled = true;
        DeleteRequested?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    ///     Gives the chip of a mark whose note may be changed a context menu with the Edit note item, named for a screen
    ///     reader and showing its key, F2; takes the menu away from any other chip.
    /// </summary>
    private void UpdateNoteEditing()
    {
        if (!_isNoteEditable || Chip is not MarkChip mark)
        {
            ContextMenu = null;
            return;
        }

        var item = new MenuItem
        {
            Header = "Edit note…",
            InputGesture = new KeyGesture(Key.F2),
        };
        Avalonia.Automation.AutomationProperties.SetAutomationId(item, "ComposerChipEditNote");
        Avalonia.Automation.AutomationProperties.SetName(item, $"Edit the note of {mark.Label}");
        item.Click += (_, e) =>
        {
            e.Handled = true;
            EditNoteRequested?.Invoke(this, EventArgs.Empty);
        };
        ContextMenu = new ContextMenu { Items = { item } };
    }
}
