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

using System.ComponentModel;
using System.Text;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
using AvaloniaEdit;
using AvaloniaEdit.Document;
using AvaloniaEdit.Rendering;
using CommunityToolkit.Mvvm.Input;

namespace DemaConsulting.CanvasPilot.ComposerSubsystem;

/// <summary>
///     The view of the message being composed: one editing surface in which typed text, dictated text, and chips for
///     attached files, folders, pictures, and marks stand in order, with a caret, a selection, wrapping, and the input
///     methods of the operating system. It shows what <see cref="ComposerViewModel" /> holds and reports everything the
///     user does to it as the view model's actions, so every rule of editing, undo, and redo is the view model's.
/// </summary>
/// <remarks>
///     <para>
///         <b>Mirror, not owner.</b> The editing surface holds the text of the message with one placeholder character
///         for each chip, which is exactly <see cref="ComposerDocument.FlatText" />. The n-th placeholder is the n-th chip
///         of the message, so offsets are the same on both sides and no chip carries an identity of its own in the text.
///         Each change that the user makes in the surface is turned into one call of the view model
///         (<see cref="ComposerViewModel.ApplyTextChange" />), grouped by the update that the surface makes it in, so
///         that typing a character, deleting with Backspace or Delete, typing over a selection, and cutting are one
///         step each. Each change of the view model that did not come from the surface, such as an undo, a dictated
///         block, a drop, or a deleted chip, is written into the surface under a flag that keeps the write from being
///         taken for the user's edit. After every change the surface text is compared with the message, and the surface
///         is made equal to the message when they differ, which is what keeps text that the message cleaned, or a change
///         that the view model refused, from ever showing.
///     </para>
///     <para>
///         <b>Undo and redo.</b> The surface's own undo stack is switched off and its undo and redo commands are removed,
///         so the only history is the message's. Ctrl+Z undoes, and Ctrl+Y and Ctrl+Shift+Z redo, through the view
///         model, which is also the <see cref="IUndoRedoTarget" /> that the Edit menu uses for the control that has the
///         focus.
///     </para>
///     <para>
///         <b>Data from outside.</b> Paste, drop, copy, and cut are taken over from the surface, which would otherwise
///         put dropped paths in as text, ignore pictures, and copy the placeholder characters to other programs. What a
///         drag or the clipboard brings is read as data only by <see cref="DataTransferReader" /> and handed to the view
///         model; typed text that holds a placeholder or a control character is refused before it enters.
///     </para>
///     <para>
///         <b>Mark notes.</b> The chip of a mark shows the mark's current note in its tooltip, and is built again when a
///         note changes. A double-click on the chip, the Edit note item of its context menu, or F2 with the caret beside
///         it asks the view model to change the note; the change itself is made on the layer over the picture and is
///         not a step of the message's history.
///     </para>
///     <para>
///         The view belongs to the user interface thread.
///     </para>
/// </remarks>
public sealed partial class ComposerEditorView : UserControl
{
    /// <summary>
    ///     The character that stands for a chip in the text of the editing surface.
    /// </summary>
    private const char Placeholder = ComposerSnapshot.ChipPlaceholder;

    /// <summary>
    ///     The changes the surface has made since its current update began.
    /// </summary>
    private readonly List<TextChange> _changes = [];

    /// <summary>
    ///     Draws the background behind dictated words.
    /// </summary>
    private readonly DictatedTextBackgroundRenderer _dictatedRenderer;

    /// <summary>
    ///     The provider of previews that the view made for itself, used until another one is set.
    /// </summary>
    private readonly ImageChipThumbnailProvider _ownThumbnails = new();

    /// <summary>
    ///     The view model that is shown, or <see langword="null" />.
    /// </summary>
    private ComposerViewModel? _viewModel;

    /// <summary>
    ///     Gives the pictures of chips, or <see langword="null" /> for the view's own.
    /// </summary>
    private IChipThumbnailProvider? _thumbnails;

    /// <summary>
    ///     The chips that the surface was last drawn for, so that a change of chips that leaves the text as it is, such as
    ///     one chip taking the place of another, is drawn again.
    /// </summary>
    private ChipSegment[] _drawnChips = [];

    /// <summary>
    ///     Whether the view is writing the message into the surface, which the surface's events must not take for an
    ///     edit of the user.
    /// </summary>
    private bool _writing;

    /// <summary>
    ///     Whether the view is handing an edit of the surface to the view model.
    /// </summary>
    private bool _handing;

    /// <summary>
    ///     Whether an update of the surface's document is going on.
    /// </summary>
    private bool _inUpdate;

    /// <summary>
    ///     The caret of the surface when the current update began.
    /// </summary>
    private int _caretBefore;

    /// <summary>
    ///     Initializes a new instance of the <see cref="ComposerEditorView" /> class.
    /// </summary>
    public ComposerEditorView()
    {
        InitializeComponent();
        _dictatedRenderer = new DictatedTextBackgroundRenderer(() => _viewModel?.Document);
        ConfigureEditor();
    }

    /// <summary>
    ///     Gets or sets the provider of the small pictures that chips show. The default shows a preview of attached
    ///     picture files and keeps the icon of every other chip; the chat panel replaces it with one that also knows
    ///     the marks. Setting it applies to chips that are drawn after that.
    /// </summary>
    public IChipThumbnailProvider? ThumbnailProvider
    {
        get => _thumbnails ?? _ownThumbnails;
        set => _thumbnails = value;
    }

    /// <summary>
    ///     Gets what the Edit menu undoes and redoes for this control: the view model of the message, or
    ///     <see langword="null" /> when there is none. The menu finds it from the data context of the control that has the
    ///     focus, which is this view's data context for the editing surface and everything in it.
    /// </summary>
    public IUndoRedoTarget? UndoRedoTarget => _viewModel;

    /// <summary>
    ///     Gets the text that the editing surface holds, with one placeholder character for each chip. It is always the
    ///     same as <see cref="ComposerDocument.FlatText" /> of the message that is shown.
    /// </summary>
    public string SurfaceText => Editor.Text;

    /// <summary>
    ///     Gets a value indicating whether the editing surface and the message hold the same text, which is the
    ///     consistency that every operation keeps.
    /// </summary>
    public bool IsConsistent => _viewModel is null || string.Equals(Editor.Text, _viewModel.Document.FlatText, StringComparison.Ordinal);

    /// <summary>
    ///     Puts the keyboard focus in the editing surface.
    /// </summary>
    public void FocusEditor()
    {
        Editor.TextArea.Focus();
    }

    /// <summary>
    ///     Reads the clipboard and pastes what it holds into the message at the caret, replacing the selection, as one
    ///     step: files and folders become chips, a picture becomes a chip of a picture saved in the project, and text is
    ///     inserted as text. Nothing is pasted when the message cannot be edited or the clipboard holds nothing the
    ///     message accepts.
    /// </summary>
    /// <param name="clipboard">The clipboard to read, or <see langword="null" /> for the clipboard of the window.</param>
    /// <returns>A task that completes when the paste is done.</returns>
    public async Task PasteAsync(IClipboard? clipboard = null)
    {
        var viewModel = _viewModel;
        clipboard ??= TopLevel.GetTopLevel(this)?.Clipboard;
        if (viewModel is null || !viewModel.CanEdit || clipboard is null)
        {
            return;
        }

        // The clipboard is data from outside, read without being opened or run, and then handed over as one step
        var content = await DataTransferReader.ReadAsync(clipboard);
        viewModel.HandlePaste(content);
    }

    /// <summary>
    ///     Handles a drop of data on the message at a point, as one step: files and folders become chips, a picture
    ///     becomes a chip of a picture saved in the project, and text is inserted as text, where the point is.
    /// </summary>
    /// <param name="data">What was dropped.</param>
    /// <param name="point">Where it was dropped, in the coordinates of the editing surface, or <see langword="null" /> for the caret.</param>
    /// <returns><see langword="true" /> when something was added to the message.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="data" /> is <see langword="null" />.</exception>
    public bool HandleDrop(IDataTransfer data, Point? point = null)
    {
        ArgumentNullException.ThrowIfNull(data);
        var viewModel = _viewModel;
        if (viewModel is null || !viewModel.CanEdit)
        {
            return false;
        }

        // The point is only a place in the text; what was dropped is data and goes through the view model
        var content = DataTransferReader.Read(data);
        return viewModel.HandleDrop(content, point is { } at ? GetOffset(at) : null);
    }

    /// <summary>
    ///     Gets the text that is put on the clipboard for part of the message: the text as it is, and for each chip a
    ///     readable placeholder in brackets, so that the raw chip character never reaches another program.
    /// </summary>
    /// <param name="segments">The segments that were selected.</param>
    /// <returns>The text.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="segments" /> is <see langword="null" />.</exception>
    public static string DescribeForClipboard(IEnumerable<ComposerSegment> segments)
    {
        ArgumentNullException.ThrowIfNull(segments);
        var builder = new StringBuilder();
        foreach (var segment in segments)
        {
            builder.Append(segment is TextSegment text ? text.Text : $"[{ChipView.GetLabel((ChipSegment)segment)}]");
        }

        return builder.ToString();
    }

    /// <summary>
    ///     Gets a value indicating whether typed or input-method text holds a character that must not enter the message:
    ///     the placeholder character of a chip, or a control character other than a tab or a line break.
    /// </summary>
    /// <param name="text">The text that is about to be entered.</param>
    /// <returns><see langword="true" /> when the text must be refused.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="text" /> is <see langword="null" />.</exception>
    public static bool ContainsForbiddenCharacter(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        return text.Any(character => character == Placeholder || (char.IsControl(character) && character is not ('\t' or '\n' or '\r')));
    }

    /// <inheritdoc />
    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);
        Detach();
        Attach(DataContext as ComposerViewModel);
    }

    /// <inheritdoc />
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);

        // Follow the view model that was released when the view was detached
        if (_viewModel is null && DataContext is ComposerViewModel viewModel)
        {
            Attach(viewModel);
        }
    }

    /// <inheritdoc />
    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        Detach();
        _ownThumbnails.Dispose();
        base.OnDetachedFromVisualTree(e);
    }

    /// <summary>
    ///     Gets the modifier of the platform's command shortcuts, such as undo and paste: the Command key on macOS and
    ///     the Control key elsewhere. It is the rule that the editing control applies to its own clipboard shortcuts, so
    ///     the shortcuts that the view adds are the same keys as the ones the control has.
    /// </summary>
    private static KeyModifiers CommandModifier => OperatingSystem.IsMacOS() ? KeyModifiers.Meta : KeyModifiers.Control;

    /// <summary>
    ///     Finds the text change that turns one text into another, as the part that differs between a common start and a
    ///     common end.
    /// </summary>
    /// <param name="before">The text as it is.</param>
    /// <param name="after">The text it is to become.</param>
    /// <returns>The change; it removes and inserts nothing when the texts are equal.</returns>
    private static TextChange Diff(string before, string after)
    {
        // Skip what both start with, then what both end with, without letting the two overlap
        var start = 0;
        var limit = Math.Min(before.Length, after.Length);
        while (start < limit && before[start] == after[start])
        {
            start++;
        }

        var end = 0;
        while (end < limit - start && before[before.Length - 1 - end] == after[after.Length - 1 - end])
        {
            end++;
        }

        return new TextChange(start, before.Length - start - end, after.Substring(start, after.Length - start - end));
    }

    /// <summary>
    ///     Sets up the editing surface once: the history is switched off, built-in commands are taken over, the chip and
    ///     dictation drawing is added, and the events that report the user's edits are connected.
    /// </summary>
    private void ConfigureEditor()
    {
        var area = Editor.TextArea;

        // Assistive technology reads the message with its chips named, and can replace it as a whole
        AutomationProperties.SetName(area, "Message");
        Editor.ValueProvider = () => _viewModel is { } viewModel ? DescribeForClipboard(viewModel.Document.Segments) : Editor.Text;
        Editor.ValueSetter = SetMessageFromAutomation;

        // The message owns the history, so the surface must not keep one, and its commands must not reach it
        Editor.Document.UndoStack.SizeLimit = 0;
        foreach (var binding in area.CommandBindings.Where(binding => binding.Command.Name is "Undo" or "Redo").ToList())
        {
            area.CommandBindings.Remove(binding);
        }

        Editor.KeyBindings.Add(new KeyBinding { Gesture = new KeyGesture(Key.Z, CommandModifier), Command = new RelayCommand(() => _viewModel?.Undo()) });
        Editor.KeyBindings.Add(new KeyBinding { Gesture = new KeyGesture(Key.Y, CommandModifier), Command = new RelayCommand(() => _viewModel?.Redo()) });
        Editor.KeyBindings.Add(new KeyBinding { Gesture = new KeyGesture(Key.Z, CommandModifier | KeyModifiers.Shift), Command = new RelayCommand(() => _viewModel?.Redo()) });

        // F2 beside the chip of a mark asks to change the mark's note, as the Edit note item of the chip's menu says
        Editor.KeyBindings.Add(new KeyBinding { Gesture = new KeyGesture(Key.F2), Command = new RelayCommand(() => _viewModel?.RequestMarkNoteEditAtCaret()) });

        // A drag of text inside the surface would be a removal and an insertion that breaks the chips, so it is off
        var options = Editor.Options;
        options.EnableTextDragDrop = false;
        options.AllowToggleOverstrikeMode = false;
        options.EnableHyperlinks = false;
        options.EnableEmailHyperlinks = false;
        options.CutCopyWholeLine = false;
        options.AcceptsTab = false;

        // Chips are inline controls, and dictated words have a background
        area.TextView.ElementGenerators.Add(new ChipElementGenerator(CreateChip));
        area.TextView.BackgroundRenderers.Add(_dictatedRenderer);

        // Take over the commands that move data between the message and other programs
        Rebind(area, "Paste", () => _ = PasteFromCommandAsync(), () => _viewModel is { CanEdit: true });
        Rebind(area, "Copy", () => _ = CopySelectionAsync(cut: false), () => Editor.SelectionLength > 0);
        Rebind(area, "Cut", () => _ = CopySelectionAsync(cut: true), () => Editor.SelectionLength > 0 && _viewModel is { CanEdit: true });

        // Refuse what must not be typed, and report what the user does
        area.TextEntering += OnTextEntering;
        Editor.Document.UpdateStarted += OnUpdateStarted;
        Editor.Document.Changed += OnDocumentChanged;
        Editor.Document.UpdateFinished += OnUpdateFinished;
        area.Caret.PositionChanged += OnSelectionChanged;
        area.SelectionChanged += OnSelectionChanged;

        // A drop on the text, and on its edges, goes through the message; the text view is reached before the built-in drop.
        // The surface allows drops only for its own drag of text, which is off, so the message allows them itself
        foreach (Interactive target in new Interactive[] { area.TextView, Editor })
        {
            DragDrop.SetAllowDrop(target, true);
            target.AddHandler(DragDrop.DragOverEvent, OnDragOver);
            target.AddHandler(DragDrop.DragLeaveEvent, OnDragLeave);
            target.AddHandler(DragDrop.DropEvent, OnDrop);
        }
    }

    /// <summary>
    ///     Replaces the whole message by a text that automation sets, as one step of the message. Chips are not kept,
    ///     because the text has no way to name them.
    /// </summary>
    /// <param name="text">The new text.</param>
    private void SetMessageFromAutomation(string text)
    {
        if (_viewModel is { CanEdit: true } viewModel)
        {
            viewModel.ApplyTextChange(0, viewModel.Document.Length, text);
        }
    }

    /// <summary>
    ///     Replaces a built-in command of the editing surface by the view's own handling.
    /// </summary>
    /// <param name="area">The text area that holds the command bindings.</param>
    /// <param name="name">The name of the command, such as <c>Paste</c>.</param>
    /// <param name="execute">What the command does.</param>
    /// <param name="canExecute">Whether the command can be used now.</param>
    private static void Rebind(AvaloniaEdit.Editing.TextArea area, string name, Action execute, Func<bool> canExecute)
    {
        var existing = area.CommandBindings.FirstOrDefault(binding => binding.Command.Name == name);
        if (existing is null)
        {
            return;
        }

        area.CommandBindings.Remove(existing);
        area.CommandBindings.Add(new RoutedCommandBinding(
            existing.Command,
            (_, args) =>
            {
                args.Handled = true;
                execute();
            },
            (_, args) =>
            {
                args.CanExecute = canExecute();
                args.Handled = true;
            }));
    }

    /// <summary>
    ///     Starts to follow a view model and shows its message.
    /// </summary>
    /// <param name="viewModel">The view model, or <see langword="null" /> for none.</param>
    private void Attach(ComposerViewModel? viewModel)
    {
        _viewModel = viewModel;
        if (viewModel is null)
        {
            return;
        }

        viewModel.Changed += OnViewModelChanged;
        viewModel.PropertyChanged += OnViewModelPropertyChanged;
        viewModel.MarkNoteChanged += OnMarkNoteChanged;
        Editor.IsReadOnly = !viewModel.CanEdit;
        WriteMessageToSurface();
    }

    /// <summary>
    ///     Stops following the view model.
    /// </summary>
    private void Detach()
    {
        if (_viewModel is null)
        {
            return;
        }

        _viewModel.Changed -= OnViewModelChanged;
        _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
        _viewModel.MarkNoteChanged -= OnMarkNoteChanged;
        _viewModel = null;
    }

    /// <summary>
    ///     Builds the chips again when the note of a mark changed, so that the chip of the mark shows the new note.
    /// </summary>
    /// <param name="sender">The view model.</param>
    /// <param name="e">Not used; every chip reads the current note of its mark when it is built again.</param>
    private void OnMarkNoteChanged(object? sender, EventArgs e)
    {
        Editor.TextArea.TextView.Redraw();
    }

    /// <summary>
    ///     Makes the control for the chip of the message with a number, for the generator that draws chips in the text.
    ///     The chip of a mark shows the mark's current note and offers to change it.
    /// </summary>
    /// <param name="index">The number of the chip, counted from zero in the order of the message.</param>
    /// <returns>The control, or <see langword="null" /> when the message has no such chip.</returns>
    private Control? CreateChip(int index)
    {
        var chip = _viewModel?.Document.Chips.ElementAtOrDefault(index);
        if (chip is null)
        {
            return null;
        }

        var note = chip is MarkChip mark ? _viewModel?.GetMarkNote(mark.MarkId) : null;
        var view = new ChipView { IsNoteEditable = chip is MarkChip };
        view.Show(chip, ThumbnailProvider, note);
        view.DeleteRequested += OnChipDeleteRequested;
        view.EditNoteRequested += OnChipEditNoteRequested;
        return view;
    }

    /// <summary>
    ///     Asks the view model to change the note of the mark whose chip was double-clicked or whose Edit note item was
    ///     chosen.
    /// </summary>
    /// <param name="sender">The chip control.</param>
    /// <param name="e">The event data.</param>
    private void OnChipEditNoteRequested(object? sender, EventArgs e)
    {
        if (sender is ChipView { Chip: MarkChip markChip })
        {
            _viewModel?.RequestMarkNoteEdit(markChip.MarkId);
        }
    }

    /// <summary>
    ///     Deletes the chip whose delete button was pressed, as one step.
    /// </summary>
    /// <param name="sender">The chip control.</param>
    /// <param name="e">The event data.</param>
    private void OnChipDeleteRequested(object? sender, EventArgs e)
    {
        var viewModel = _viewModel;
        if (sender is not ChipView { Chip: { } chip } || viewModel is null)
        {
            return;
        }

        // Find where the chip is now by its identity, because two chips of the same file are equal but not the same chip
        var offset = 0;
        foreach (var segment in viewModel.Document.Segments)
        {
            if (ReferenceEquals(segment, chip))
            {
                viewModel.DeleteChip(offset);
                return;
            }

            offset += segment.Length;
        }
    }

    /// <summary>
    ///     Refuses typed or input-method text that holds a character that must not enter the message, and moves a caret that is
    ///     in the row of chips to the line below before the text enters.
    /// </summary>
    /// <param name="sender">The text area.</param>
    /// <param name="e">The event data.</param>
    private void OnTextEntering(object? sender, TextInputEventArgs e)
    {
        if (e.Text is not { } text)
        {
            return;
        }

        if (ContainsForbiddenCharacter(text))
        {
            e.Handled = true;
            return;
        }

        // Words typed with the caret in the row of chips (a click on the row puts it there) go to the line below, so that
        // they never join the chips; the caret moves before the text enters
        if (_viewModel is { } viewModel && Editor.SelectionLength == 0 && viewModel.CaretForNewText() is var offset && offset != Editor.CaretOffset)
        {
            Editor.CaretOffset = offset;
        }
    }

    /// <summary>
    ///     Notes the start of an update of the surface, remembering where the caret was, which is what an undo restores.
    /// </summary>
    /// <param name="sender">The document.</param>
    /// <param name="e">The event data.</param>
    private void OnUpdateStarted(object? sender, EventArgs e)
    {
        if (_writing)
        {
            return;
        }

        _inUpdate = true;
        _changes.Clear();
        _caretBefore = Editor.CaretOffset;
    }

    /// <summary>
    ///     Collects a change of the surface that the user made.
    /// </summary>
    /// <param name="sender">The document.</param>
    /// <param name="e">The event data.</param>
    private void OnDocumentChanged(object? sender, DocumentChangeEventArgs e)
    {
        if (_writing)
        {
            return;
        }

        _changes.Add(new TextChange(e.Offset, e.RemovalLength, e.InsertedText.Text));
    }

    /// <summary>
    ///     Hands the changes of one update of the surface to the view model as one action.
    /// </summary>
    /// <param name="sender">The document.</param>
    /// <param name="e">The event data.</param>
    private void OnUpdateFinished(object? sender, EventArgs e)
    {
        if (_writing)
        {
            return;
        }

        _inUpdate = false;
        var viewModel = _viewModel;
        if (viewModel is null || _changes.Count == 0)
        {
            _changes.Clear();
            return;
        }

        // One change is exact; several, such as a line break with its indentation, are taken as the difference they make
        var change = _changes.Count == 1 ? _changes[0] : Diff(viewModel.Document.FlatText, Editor.Text);
        _changes.Clear();
        if (change.IsEmpty)
        {
            return;
        }

        // Hand the edit over, unless it does not fit the message, which is then made equal to the surface's source of truth
        if (change.Offset + change.RemovedLength <= viewModel.Document.Length)
        {
            _handing = true;
            try
            {
                viewModel.ApplyTextChange(change.Offset, change.RemovedLength, change.Inserted, _caretBefore);
            }
            finally
            {
                _handing = false;
            }
        }

        MakeSurfaceEqualToMessage();
    }

    /// <summary>
    ///     Writes into the surface a change of the message that the user did not make there.
    /// </summary>
    /// <param name="sender">The view model.</param>
    /// <param name="e">The event data.</param>
    private void OnViewModelChanged(object? sender, EventArgs e)
    {
        if (_handing)
        {
            return;
        }

        WriteMessageToSurface();
    }

    /// <summary>
    ///     Keeps the surface read-only while the message cannot be edited.
    /// </summary>
    /// <param name="sender">The view model.</param>
    /// <param name="e">The event data.</param>
    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (_viewModel is { } viewModel)
        {
            Editor.IsReadOnly = !viewModel.CanEdit;
        }
    }

    /// <summary>
    ///     Tells the view model where the caret and the selection are, which is where new text and chips go.
    /// </summary>
    /// <param name="sender">The caret or the text area.</param>
    /// <param name="e">The event data.</param>
    private void OnSelectionChanged(object? sender, EventArgs e)
    {
        // While the surface is being changed the message is not yet the same, so the caret is told afterwards
        if (_writing || _inUpdate || _handing)
        {
            return;
        }

        SendSelectionToViewModel();
    }

    /// <summary>
    ///     Gives the view model the selection of the surface.
    /// </summary>
    private void SendSelectionToViewModel()
    {
        var length = Editor.SelectionLength;
        _viewModel?.SetSelection(length > 0 ? Editor.SelectionStart : Editor.CaretOffset, length);
    }

    /// <summary>
    ///     Shows a drag that brings something the message accepts as a drop that will be a copy.
    /// </summary>
    /// <param name="sender">The control the drag is over.</param>
    /// <param name="e">The event data.</param>
    private void OnDragOver(object? sender, DragEventArgs e)
    {
        var accepts = _viewModel is { CanEdit: true } && DataTransferReader.IsAcceptable(e.DataTransfer);
        e.DragEffects = accepts ? DragDropEffects.Copy : DragDropEffects.None;
        DropHighlight.IsVisible = accepts;
        e.Handled = true;
    }

    /// <summary>
    ///     Stops showing the drag.
    /// </summary>
    /// <param name="sender">The control the drag left.</param>
    /// <param name="e">The event data.</param>
    private void OnDragLeave(object? sender, DragEventArgs e)
    {
        DropHighlight.IsVisible = false;
    }

    /// <summary>
    ///     Handles a drop on the message.
    /// </summary>
    /// <param name="sender">The control the drop is on.</param>
    /// <param name="e">The event data.</param>
    private void OnDrop(object? sender, DragEventArgs e)
    {
        DropHighlight.IsVisible = false;
        e.Handled = true;
        HandleDrop(e.DataTransfer, e.GetPosition(Editor));
    }

    /// <summary>
    ///     Pastes from the clipboard for the paste command, without letting a failure escape the command.
    /// </summary>
    /// <returns>A task that completes when the paste is done.</returns>
    private async Task PasteFromCommandAsync()
    {
        try
        {
            await PasteAsync();
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            if (_viewModel is { } viewModel)
            {
                viewModel.StatusText = $"The clipboard could not be pasted: {ex.Message}";
            }
        }
    }

    /// <summary>
    ///     Puts the selected part of the message on the clipboard as readable text, and deletes it as one step when it is
    ///     cut.
    /// </summary>
    /// <param name="cut"><see langword="true" /> to delete the selection after it was copied.</param>
    /// <returns>A task that completes when the clipboard was written.</returns>
    private async Task CopySelectionAsync(bool cut)
    {
        var viewModel = _viewModel;
        var clipboard = TopLevel.GetTopLevel(this)?.Clipboard;
        var length = Editor.SelectionLength;
        if (viewModel is null || clipboard is null || length == 0)
        {
            return;
        }

        // Take the text from the message, because the surface would give the raw chip characters
        var start = Editor.SelectionStart;
        var text = DescribeForClipboard(viewModel.Document.GetSlice(start, length));
        try
        {
            await clipboard.SetTextAsync(text);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            viewModel.StatusText = $"The text could not be put on the clipboard: {ex.Message}";
            return;
        }

        if (cut)
        {
            viewModel.Delete(start, length);
        }
    }

    /// <summary>
    ///     Finds the offset of the message that is under a point of the surface.
    /// </summary>
    /// <param name="point">The point, in the coordinates of the surface.</param>
    /// <returns>The offset, or <see langword="null" /> when the point is not over the text.</returns>
    private int? GetOffset(Point point)
    {
        var position = Editor.GetPositionFromPoint(point);
        return position is { } at ? Editor.Document.GetOffset(at.Line, at.Column) : null;
    }

    /// <summary>
    ///     Writes the message into the surface as the smallest change, puts the caret where the view model has it, and
    ///     draws what depends on the message again.
    /// </summary>
    private void WriteMessageToSurface()
    {
        var viewModel = _viewModel;
        if (viewModel is null)
        {
            return;
        }

        _writing = true;
        try
        {
            var change = Diff(Editor.Text, viewModel.Document.FlatText);
            if (!change.IsEmpty)
            {
                Editor.Document.Replace(change.Offset, change.RemovedLength, change.Inserted);
            }

            Editor.TextArea.ClearSelection();
            Editor.CaretOffset = Math.Clamp(viewModel.CaretOffset, 0, Editor.Document.TextLength);
        }
        finally
        {
            _writing = false;
        }

        DrawAgainIfChipsChanged();
        SendSelectionToViewModel();
    }

    /// <summary>
    ///     Makes the surface hold exactly the message after an edit was handed over, which restores the surface when the
    ///     message cleaned the text or refused the change, and tells the view model where the caret ended up.
    /// </summary>
    private void MakeSurfaceEqualToMessage()
    {
        if (_viewModel is null)
        {
            return;
        }

        if (IsConsistent)
        {
            DrawAgainIfChipsChanged();
            SendSelectionToViewModel();
        }
        else
        {
            WriteMessageToSurface();
        }
    }

    /// <summary>
    ///     Draws the lines again when the chips of the message are not the ones that were drawn, and the dictated
    ///     background in any case, because both depend on the message and not only on the text.
    /// </summary>
    private void DrawAgainIfChipsChanged()
    {
        var chips = _viewModel?.Document.Chips.ToArray() ?? [];
        var changed = chips.Length != _drawnChips.Length;
        for (var index = 0; !changed && index < chips.Length; index++)
        {
            changed = !ReferenceEquals(chips[index], _drawnChips[index]);
        }

        _drawnChips = chips;
        if (changed)
        {
            Editor.TextArea.TextView.Redraw();
        }
        else
        {
            Editor.TextArea.TextView.InvalidateLayer(_dictatedRenderer.Layer);
        }
    }

    /// <summary>
    ///     One change of text: some positions removed at an offset and some text put there.
    /// </summary>
    /// <param name="Offset">The offset of the change.</param>
    /// <param name="RemovedLength">The number of positions removed.</param>
    /// <param name="Inserted">The text inserted.</param>
    private readonly record struct TextChange(int Offset, int RemovedLength, string Inserted)
    {
        /// <summary>
        ///     Gets a value indicating whether the change removes and inserts nothing.
        /// </summary>
        public bool IsEmpty => RemovedLength == 0 && Inserted.Length == 0;
    }
}
