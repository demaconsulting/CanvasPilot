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
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DemaConsulting.CanvasPilot.AnnotationSubsystem;
using DemaConsulting.CanvasPilot.LoggingSubsystem;
using DemaConsulting.CanvasPilot.SpeechSubsystem;

namespace DemaConsulting.CanvasPilot.ComposerSubsystem;

/// <summary>
///     Where dictation is: not started, being started, listening, or finishing.
/// </summary>
public enum DictationState
{
    /// <summary>Nothing is listening.</summary>
    Idle,

    /// <summary>The talk button or key is held and listening is being started, which can take a while the first time.</summary>
    Starting,

    /// <summary>The talk button or key is held and the application is listening.</summary>
    Listening,

    /// <summary>The talk button or key was released and the application is finishing the words it heard.</summary>
    Finishing,
}

/// <summary>
///     The orchestration of the message being composed, without a window: it owns the document, its undo history, the
///     intake of dropped and pasted content, and the push-to-talk dictation; it connects the marks and the attachments
///     of the project to the message; and it sends the message. The editing control that shows the message reports what
///     the user does through this class, and shows what it holds, so that every rule is tested without a window.
/// </summary>
/// <remarks>
///     <para>
///         <b>One action, one step.</b> Every method that changes the message makes exactly one <see cref="ComposerEdit" />
///         and executes it through <see cref="ComposerHistory" />, so the granularity of undo is that of the user's
///         actions: one typed or deleted character, one selection deleted or typed over, one dictated block, one drop or
///         paste of any number of items, one mark added, one chip deleted. An action that changes nothing, such as an
///         empty dictation or a drop of nothing that could be accepted, is not a step. <see cref="Changed" /> is raised
///         once for each action, after the message, the caret, and the undo state all agree.
///     </para>
///     <para>
///         <b>Projects.</b> The message can only be edited while a project is open (<see cref="SetProject" />), because
///         attachments and marks belong to the project. Opening, closing, or changing the project drops the draft, its
///         history, its marks, and the pictures that were saved for it, and gives up a dictation in progress.
///     </para>
///     <para>
///         <b>Sending.</b> <see cref="SendAsync" /> puts the attachments into the project, has the
///         <see cref="IMessageBuilder" /> build the message for the assistant, and only when both worked empties the
///         draft, its history, and the marks, removes saved pictures that nothing refers to, and raises
///         <see cref="MessageSent" /> with the immutable message. When anything fails the draft stays exactly as it was
///         and the status text says why. While a message is being sent the draft cannot be changed.
///     </para>
///     <para>
///         <b>Dictation.</b> <see cref="BeginPushToTalkAsync" /> and <see cref="EndPushToTalkAsync" /> follow the talk
///         button or key. While it is held the words recognized so far are in <see cref="LivePartialText" />; when it is
///         released the final words are added at the caret as one dictated block. A release before listening has started
///         makes the start give up. When dictation cannot work, <see cref="StatusText" /> says why and what to do, and the
///         draft is not changed. Marks are never made by voice.
///     </para>
///     <para>
///         The class is owned by the user interface thread. The recognizer raises its events on another thread, which
///         are moved to the thread that pressed the talk button; tests without a synchronization context run them
///         directly.
///     </para>
/// </remarks>
public sealed partial class ComposerViewModel : ObservableObject, IUndoRedoTarget, IPushToTalkTarget, IDisposable
{
    /// <summary>
    ///     The text shown when the message cannot be edited because no project is open.
    /// </summary>
    public const string NoProjectText = "Open a project to write a message.";

    /// <summary>
    ///     The placeholder of the editing control while the message is empty.
    /// </summary>
    public const string PlaceholderText = "Describe a change, or mark the picture…";

    /// <summary>
    ///     The reason that the chat gives for <see cref="BlockSending" /> while the AI copilot works on an earlier message.
    /// </summary>
    public const string CopilotWorkingText = "The copilot is still working. Press Stop to cancel.";

    /// <summary>
    ///     The most items of one kind that a status message names before it says how many more there are.
    /// </summary>
    private const int NamedNotes = 3;

    /// <summary>
    ///     The message being composed.
    /// </summary>
    private readonly ComposerDocument _document = new();

    /// <summary>
    ///     The undo and redo of the message.
    /// </summary>
    private readonly ComposerHistory _history;

    /// <summary>
    ///     Builds what is kept and given to the assistant when the message is sent.
    /// </summary>
    private readonly IMessageBuilder _builder;

    /// <summary>
    ///     The marks made for the draft.
    /// </summary>
    private readonly MarkRegistry _marks;

    /// <summary>
    ///     Dictation, which turns held speech into text.
    /// </summary>
    private readonly IDictationService _dictation;

    /// <summary>
    ///     Makes the attachment store of a project folder.
    /// </summary>
    private readonly Func<string, IAttachmentStore> _storeFactory;

    /// <summary>
    ///     Optional logger for diagnostics.
    /// </summary>
    private readonly RollingFileLogger? _logger;

    /// <summary>
    ///     The context given at construction, to which the words of the recognizer are moved, or <see langword="null" /> to
    ///     use the context of the thread that presses the talk button.
    /// </summary>
    private readonly SynchronizationContext? _fixedContext;

    /// <summary>
    ///     The attachment store of the open project, or <see langword="null" /> when no project is open.
    /// </summary>
    private IAttachmentStore? _store;

    /// <summary>
    ///     The intake of dropped and pasted content for the open project.
    /// </summary>
    private AttachmentIntake? _intake;

    /// <summary>
    ///     The caret, which is also the start of the selection.
    /// </summary>
    private int _caretOffset;

    /// <summary>
    ///     The number of selected positions after the caret.
    /// </summary>
    private int _selectionLength;

    /// <summary>
    ///     Counts the changes of the project, so that work that was started for an earlier project is not applied.
    /// </summary>
    private int _projectEpoch;

    /// <summary>
    ///     Counts the dictations that were given up, so that a dictation that was given up does not change the message.
    /// </summary>
    private int _dictationEpoch;

    /// <summary>
    ///     Gives up the start of the dictation that is being started.
    /// </summary>
    private CancellationTokenSource? _startCancellation;

    /// <summary>
    ///     The session that is listening.
    /// </summary>
    private IDictationSession? _session;

    /// <summary>
    ///     The context of the thread that pressed the talk button, to which the recognizer's events are moved.
    /// </summary>
    private SynchronizationContext? _context;

    /// <summary>
    ///     Whether the talk button was released while listening was being started.
    /// </summary>
    private bool _releaseRequested;

    /// <summary>
    ///     Why sending is blocked from outside, or an empty string when it is not.
    /// </summary>
    private string _sendBlockedReason = string.Empty;

    /// <summary>
    ///     A sentence about the last action, or an empty string.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasStatus))]
    private string _statusText = string.Empty;

    /// <summary>
    ///     The words recognized so far while the talk button is held.
    /// </summary>
    [ObservableProperty]
    private string _livePartialText = string.Empty;

    /// <summary>
    ///     Whether a message is being sent.
    /// </summary>
    [ObservableProperty]
    private bool _isSending;

    /// <summary>
    ///     Where dictation is.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsDictating))]
    private DictationState _dictationState;

    /// <summary>
    ///     Initializes a new instance of the <see cref="ComposerViewModel" /> class with an empty message and no project.
    /// </summary>
    /// <param name="builder">Builds what is kept and given to the assistant when the message is sent.</param>
    /// <param name="marks">The marks made for the draft; emptied when the message is sent.</param>
    /// <param name="dictation">Dictation, which turns held speech into text.</param>
    /// <param name="storeFactory">
    ///     Makes the attachment store of a project folder, or <see langword="null" /> for <see cref="AttachmentStore" />.
    /// </param>
    /// <param name="logger">Optional logger for diagnostics.</param>
    /// <param name="context">
    ///     The context to which the words of the recognizer, which come from another thread, are moved, or
    ///     <see langword="null" /> for the context of the thread that presses the talk button, which is the interface
    ///     thread of the running application.
    /// </param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="builder" />, <paramref name="marks" />, or <paramref name="dictation" /> is <see langword="null" />.</exception>
    public ComposerViewModel(
        IMessageBuilder builder,
        MarkRegistry marks,
        IDictationService dictation,
        Func<string, IAttachmentStore>? storeFactory = null,
        RollingFileLogger? logger = null,
        SynchronizationContext? context = null)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(marks);
        ArgumentNullException.ThrowIfNull(dictation);

        _builder = builder;
        _marks = marks;
        _dictation = dictation;
        _logger = logger;
        _fixedContext = context;
        _storeFactory = storeFactory ?? (folder => new AttachmentStore(folder, logger));
        _history = new ComposerHistory(_document);
        UndoCommand = new RelayCommand(Undo, () => CanUndo);
        RedoCommand = new RelayCommand(Redo, () => CanRedo);
        SendCommand = new AsyncRelayCommand(token => SendAsync(token), () => CanSend);
    }

    /// <summary>
    ///     Raised once for each action that changed the message, its caret, or what can be undone, so that the editing
    ///     control can show what the message now holds.
    /// </summary>
    public event EventHandler? Changed;

    /// <inheritdoc />
    public event EventHandler? UndoRedoStateChanged;

    /// <summary>
    ///     Raised when a message was sent, with the immutable message. Nothing of it is left in the composer then.
    /// </summary>
    public event EventHandler<MessageSentEventArgs>? MessageSent;

    /// <summary>
    ///     Gets the command that undoes the latest step.
    /// </summary>
    public IRelayCommand UndoCommand { get; }

    /// <summary>
    ///     Gets the command that redoes the step that was undone last.
    /// </summary>
    public IRelayCommand RedoCommand { get; }

    /// <summary>
    ///     Gets the command that sends the message.
    /// </summary>
    public IAsyncRelayCommand SendCommand { get; }

    /// <summary>
    ///     Gets the message being composed. The editing control reads it; every change goes through this class.
    /// </summary>
    public ComposerDocument Document => _document;

    /// <summary>
    ///     Gets the identities of the marks whose chips are in the message, in the order of the chips. The overlay on the
    ///     picture shows exactly these marks.
    /// </summary>
    public IReadOnlyList<Guid> MarkChipIds => [.. _document.Chips.OfType<MarkChip>().Select(chip => chip.MarkId)];

    /// <summary>
    ///     Gets a value indicating whether a project is open, which is what the message needs.
    /// </summary>
    public bool HasProject => _store is not null;

    /// <summary>
    ///     Gets the folder of the open project, or <see langword="null" /> when none is open.
    /// </summary>
    public string? ProjectFolder => _store?.ProjectFolder;

    /// <summary>
    ///     Gets a value indicating whether the message can be changed: a project is open and no message is being sent.
    /// </summary>
    public bool CanEdit => HasProject && !IsSending;

    /// <summary>
    ///     Gets a value indicating whether the message has anything to send: a word, or a chip.
    /// </summary>
    public bool HasContent => _document.FlatText.Any(character => !char.IsWhiteSpace(character));

    /// <summary>
    ///     Gets a value indicating whether the editing control shows its placeholder: a project is open and the message has
    ///     neither text nor chips.
    /// </summary>
    public bool ShowsPlaceholder => HasProject && _document.FlatText.Length == 0;

    /// <summary>
    ///     Gets a value indicating whether the message can be sent now: it has content, can be edited, nobody is holding
    ///     the talk button, and nothing outside the composer blocks sending (see <see cref="BlockSending" />).
    /// </summary>
    public bool CanSend => CanEdit && HasContent && DictationState == DictationState.Idle && !IsSendBlocked;

    /// <summary>
    ///     Gets a value indicating whether sending is blocked from outside, which is the case while the AI copilot works
    ///     on an earlier message. Editing is not blocked.
    /// </summary>
    public bool IsSendBlocked => _sendBlockedReason.Length > 0;

    /// <summary>
    ///     Gets the sentence that says why sending is blocked, or an empty string when it is not.
    /// </summary>
    public string SendBlockedReason => _sendBlockedReason;

    /// <inheritdoc />
    public bool CanUndo => CanEdit && _history.CanUndo;

    /// <inheritdoc />
    public bool CanRedo => CanEdit && _history.CanRedo;

    /// <summary>
    ///     Gets a value indicating whether there is a sentence about the last action to show.
    /// </summary>
    public bool HasStatus => StatusText.Length > 0;

    /// <summary>
    ///     Gets a value indicating whether dictation is going on in any phase.
    /// </summary>
    public bool IsDictating => DictationState != DictationState.Idle;

    /// <summary>
    ///     Gets the caret, which is also the start of the selection. New text, dictated words, and chips go here.
    /// </summary>
    public int CaretOffset => _caretOffset;

    /// <summary>
    ///     Gets the number of selected positions after <see cref="CaretOffset" />; zero when nothing is selected.
    /// </summary>
    public int SelectionLength => _selectionLength;

    /// <summary>
    ///     Tells the view model where the caret is and what is selected, as the editing control reports it. Values outside
    ///     the message are brought back into it.
    /// </summary>
    /// <param name="caretOffset">The start of the selection, or the caret when nothing is selected.</param>
    /// <param name="selectionLength">The number of selected positions after it; zero for none.</param>
    public void SetSelection(int caretOffset, int selectionLength = 0)
    {
        var start = Math.Clamp(caretOffset, 0, _document.Length);
        var length = Math.Clamp(selectionLength, 0, _document.Length - start);
        if (start == _caretOffset && length == _selectionLength)
        {
            return;
        }

        _caretOffset = start;
        _selectionLength = length;
        OnPropertyChanged(nameof(CaretOffset));
        OnPropertyChanged(nameof(SelectionLength));
    }

    /// <summary>
    ///     Makes a project the one the message is composed in, or closes the project when there is none. The draft, its
    ///     history, its marks, and the pictures that were saved for it are dropped, a dictation in progress is given up,
    ///     and saved pictures that an earlier session left in the new project's draft folder are removed. Setting the
    ///     project that is already set changes nothing.
    /// </summary>
    /// <param name="projectFolder">The folder of the project, or <see langword="null" /> when no project is open.</param>
    public void SetProject(string? projectFolder)
    {
        var comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        if (string.Equals(projectFolder, _store?.ProjectFolder, comparison))
        {
            return;
        }

        // Drop the draft of the old project, and give up what was started for it
        _projectEpoch++;
        DiscardDictation();
        var oldStore = _store;
        _store = null;
        _intake = null;
        DropDraft();
        RemoveLeftoversQuietly(oldStore);

        // Open the new project, which a folder that cannot be used does not do
        if (projectFolder is not null)
        {
            try
            {
                var store = _storeFactory(projectFolder);
                _intake = new AttachmentIntake(store, _logger);
                _store = store;
                RemoveLeftoversQuietly(store);
                StatusText = string.Empty;
            }
            catch (Exception ex) when (ex is ArgumentException or IOException or UnauthorizedAccessException or NotSupportedException)
            {
                _intake = null;
                _store = null;
                Log(LogLevel.Warning, $"The message cannot be composed in '{projectFolder}': {ex.Message}");
                StatusText = $"The message cannot be written in this project: {ex.Message}";
            }
        }
        else
        {
            StatusText = string.Empty;
        }

        OnPropertyChanged(nameof(HasProject));
        OnPropertyChanged(nameof(ProjectFolder));
        NotifyDocumentChanged();
    }

    /// <summary>
    ///     Applies a change of text that the editing control reports: it removed some positions at an offset and put some
    ///     text there. Typing a character, a backspace, a delete, typing over a selection, and deleting a selection are
    ///     each one step; the text is typed text and is cleaned first.
    /// </summary>
    /// <param name="offset">The offset of the change.</param>
    /// <param name="removedLength">The number of positions that were removed; zero when text was only inserted.</param>
    /// <param name="insertedText">The text that was put there; empty when positions were only removed.</param>
    /// <param name="caretBefore">The caret before the change, or <see langword="null" /> for the end of the removed range.</param>
    /// <returns><see langword="true" /> when the message changed; <see langword="false" /> when nothing could be changed.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="insertedText" /> is <see langword="null" />.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when the range is not inside the message.</exception>
    public bool ApplyTextChange(int offset, int removedLength, string insertedText, int? caretBefore = null)
    {
        ArgumentNullException.ThrowIfNull(insertedText);
        if (!BeginEdit())
        {
            return false;
        }

        var edit = insertedText.Length == 0
            ? ComposerEdit.Delete(_document, offset, removedLength, caretBefore)
            : ComposerEdit.TypeText(_document, offset, insertedText, removedLength, caretBefore);
        return Execute(edit);
    }

    /// <summary>
    ///     Types text at the caret, replacing the selection when there is one, as one step.
    /// </summary>
    /// <param name="text">The typed text.</param>
    /// <returns><see langword="true" /> when the message changed.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="text" /> is <see langword="null" />.</exception>
    public bool InsertTyped(string text) => ApplyTextChange(_caretOffset, _selectionLength, text);

    /// <summary>
    ///     Deletes a range of text and chips, as one step.
    /// </summary>
    /// <param name="offset">The offset of the first position to delete.</param>
    /// <param name="length">The number of positions to delete.</param>
    /// <returns><see langword="true" /> when the message changed.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when the range is not inside the message.</exception>
    public bool Delete(int offset, int length) => ApplyTextChange(offset, length, string.Empty);

    /// <summary>
    ///     Does what the Backspace key does: deletes the selection, or the one position before the caret, as one step.
    /// </summary>
    /// <returns><see langword="true" /> when the message changed.</returns>
    public bool DeleteBackward()
    {
        if (_selectionLength > 0)
        {
            return Delete(_caretOffset, _selectionLength);
        }

        return _caretOffset > 0 && Delete(_caretOffset - 1, 1);
    }

    /// <summary>
    ///     Does what the Delete key does: deletes the selection, or the one position after the caret, as one step.
    /// </summary>
    /// <returns><see langword="true" /> when the message changed.</returns>
    public bool DeleteForward()
    {
        if (_selectionLength > 0)
        {
            return Delete(_caretOffset, _selectionLength);
        }

        return _caretOffset < _document.Length &&
               ApplyTextChange(_caretOffset, 1, string.Empty, caretBefore: _caretOffset);
    }

    /// <summary>
    ///     Adds a block of dictated text at the caret, replacing the selection when there is one, as one step however
    ///     many words it holds. Empty text is not a step.
    /// </summary>
    /// <param name="text">The recognized text.</param>
    /// <returns><see langword="true" /> when the message changed.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="text" /> is <see langword="null" />.</exception>
    public bool AddDictatedBlock(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        return BeginEdit() && Execute(ComposerEdit.Dictate(_document, _caretOffset, text, _selectionLength));
    }

    /// <summary>
    ///     Adds the chip of a mark at the caret as one step. The mark must have been made in this composer's registry;
    ///     marks are made by pointing at the picture, never by voice.
    /// </summary>
    /// <param name="mark">The mark that was made.</param>
    /// <returns><see langword="true" /> when the chip was added.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="mark" /> is <see langword="null" />.</exception>
    /// <exception cref="ArgumentException">Thrown when the mark is not in the registry of this composer.</exception>
    public bool AddMark(Mark mark)
    {
        ArgumentNullException.ThrowIfNull(mark);
        if (!ReferenceEquals(_marks.Find(mark.Id), mark))
        {
            throw new ArgumentException("The mark was not made in this composer's registry.", nameof(mark));
        }

        return BeginEdit() && Execute(ComposerEdit.AddMark(_document, _caretOffset, new MarkChip(mark.Id, mark.Number)));
    }

    /// <summary>
    ///     Deletes the chip at an offset, as the delete button of the chip does, as one step.
    /// </summary>
    /// <param name="offset">The offset of the chip.</param>
    /// <returns><see langword="true" /> when the chip was deleted.</returns>
    /// <exception cref="ArgumentException">Thrown when the position at the offset is text and not a chip.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when the offset is not inside the message.</exception>
    public bool DeleteChip(int offset) => BeginEdit() && Execute(ComposerEdit.DeleteChip(_document, offset));

    /// <summary>
    ///     Deletes the chips of all marks from the message, one step for each chip, as the delete button of each chip does.
    /// </summary>
    /// <returns>How many chips were deleted.</returns>
    public int DeleteMarkChips()
    {
        var deleted = 0;
        while (FindMarkChipOffset() is { } offset && DeleteChip(offset))
        {
            deleted++;
        }

        return deleted;
    }

    /// <summary>
    ///     Finds the offset of the first chip of a mark.
    /// </summary>
    /// <returns>The offset, or <see langword="null" /> when the message holds no mark chip.</returns>
    private int? FindMarkChipOffset()
    {
        var offset = 0;
        foreach (var segment in _document.Segments)
        {
            if (segment is MarkChip)
            {
                return offset;
            }

            offset += segment.Length;
        }

        return null;
    }

    /// <summary>
    ///     Handles content that was dropped on the message: its files, folders, pictures, and text become chips and text
    ///     at the place where it was dropped, as one step however many items there are. What could not be accepted is left
    ///     out and named in <see cref="StatusText" />; when nothing could be accepted there is no step.
    /// </summary>
    /// <param name="content">What the drop brought.</param>
    /// <param name="offset">The offset of the drop, or <see langword="null" /> for the caret.</param>
    /// <returns><see langword="true" /> when something was added.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="content" /> is <see langword="null" />.</exception>
    public bool HandleDrop(DroppedContent content, int? offset = null)
    {
        ArgumentNullException.ThrowIfNull(content);
        return AddContent(content, Math.Clamp(offset ?? _caretOffset, 0, _document.Length), 0);
    }

    /// <summary>
    ///     Handles content that was pasted into the message: like a drop, but at the caret and replacing the selection.
    ///     A paste of plain text is one step too.
    /// </summary>
    /// <param name="content">What the clipboard brought.</param>
    /// <returns><see langword="true" /> when something was added.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="content" /> is <see langword="null" />.</exception>
    public bool HandlePaste(DroppedContent content)
    {
        ArgumentNullException.ThrowIfNull(content);
        return AddContent(content, _caretOffset, _selectionLength);
    }

    /// <inheritdoc />
    public void Undo()
    {
        if (!CanUndo)
        {
            return;
        }

        StatusText = string.Empty;
        var caret = _history.Undo();
        FinishHistoryStep(caret);
    }

    /// <inheritdoc />
    public void Redo()
    {
        if (!CanRedo)
        {
            return;
        }

        StatusText = string.Empty;
        var caret = _history.Redo();
        FinishHistoryStep(caret);
    }

    /// <summary>
    ///     Empties the draft: the message, its history, the marks, and the pictures that were saved for it. A dictation in
    ///     progress is given up.
    /// </summary>
    public void ClearDraft()
    {
        DiscardDictation();
        DropDraft();
        RemoveLeftoversQuietly(_store);
        StatusText = string.Empty;
        NotifyDocumentChanged();
    }

    /// <summary>
    ///     Blocks sending until <see cref="UnblockSending" /> is called, for a reason that the composer does not know, such
    ///     as an AI copilot that is still working on an earlier message. The message can still be edited, so that the
    ///     user can write the next one meanwhile; <see cref="CanSend" /> is <see langword="false" />, the send command
    ///     cannot run, and <see cref="StatusText" /> shows the reason.
    /// </summary>
    /// <param name="reason">The sentence that says why, which is not empty.</param>
    /// <exception cref="ArgumentException">Thrown when <paramref name="reason" /> is empty or white space.</exception>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="reason" /> is <see langword="null" />.</exception>
    public void BlockSending(string reason)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);

        _sendBlockedReason = reason;
        StatusText = reason;
        NotifySendBlockChanged();
    }

    /// <summary>
    ///     Lifts the block of <see cref="BlockSending" />. The status text is cleared when it still shows the reason of
    ///     the block; a later sentence about another action stays. Nothing happens when sending was not blocked.
    /// </summary>
    public void UnblockSending()
    {
        if (!IsSendBlocked)
        {
            return;
        }

        if (string.Equals(StatusText, _sendBlockedReason, StringComparison.Ordinal))
        {
            StatusText = string.Empty;
        }

        _sendBlockedReason = string.Empty;
        NotifySendBlockChanged();
    }

    /// <summary>
    ///     Sends the message: puts the attachments into the project, has the message built for the assistant, and then
    ///     empties the draft, its history, and the marks, removes saved pictures that nothing refers to, and raises
    ///     <see cref="MessageSent" />. Nothing is changed when the message is empty, no project is open, a talk button is
    ///     held, sending is blocked from outside (<see cref="BlockSending" />), or anything fails, and then
    ///     <see cref="StatusText" /> says why.
    /// </summary>
    /// <param name="cancellationToken">Cancels the sending before the message is built.</param>
    /// <returns><see langword="true" /> when the message was sent.</returns>
    public async Task<bool> SendAsync(CancellationToken cancellationToken = default)
    {
        // Check that there is something to send and that nothing else is going on
        if (!HasProject)
        {
            StatusText = NoProjectText;
            return false;
        }

        if (IsSending)
        {
            return false;
        }

        if (IsSendBlocked)
        {
            StatusText = _sendBlockedReason;
            return false;
        }

        if (DictationState != DictationState.Idle)
        {
            StatusText = "Let go of the talk button before sending the message.";
            return false;
        }

        if (!HasContent)
        {
            StatusText = "Write a message before sending it.";
            return false;
        }

        var store = _store!;
        var epoch = _projectEpoch;
        IsSending = true;
        StatusText = "Sending the message...";
        NotifyDocumentChanged();
        SentMessage? sent = null;
        try
        {
            // Take what the message is now; it cannot change while it is sent, because the draft cannot be edited
            var snapshot = _document.Snapshot();
            var chips = snapshot.Chips.OfType<AttachmentChip>().ToList();
            var markIds = snapshot.Chips.OfType<MarkChip>().Select(chip => chip.MarkId).ToList();

            // Put the attachments into the project, off the interface thread; all of them or none
            var staging = await Task.Run(() => store.StageForSend(chips), cancellationToken);
            if (!staging.Succeeded)
            {
                StatusText = $"The message was not sent. {staging.Failure!.Reason}";
                return false;
            }

            // Build what is kept and given to the assistant
            var message = await _builder.BuildAsync(snapshot, _marks.ActiveMarks(markIds), staging.Attachments, cancellationToken);

            // A project that was closed meanwhile takes the draft with it, and the message is not sent into the next one
            if (epoch != _projectEpoch)
            {
                return false;
            }

            // Everything worked: forget the draft, its history, and the marks, and the pictures nothing refers to now
            _history.CompleteSend();
            _marks.Clear();
            _caretOffset = 0;
            _selectionLength = 0;
            LivePartialText = string.Empty;
            await Task.Run(() => RemoveLeftoversQuietly(store), CancellationToken.None);
            StatusText = string.Empty;
            sent = message;
        }
        catch (OperationCanceledException)
        {
            StatusText = "Sending was canceled. The message is still here.";
            return false;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            Log(LogLevel.Error, $"The message could not be sent: {ex}");
            StatusText = $"The message was not sent: {ex.Message}";
            return false;
        }
        finally
        {
            IsSending = false;
            NotifyDocumentChanged();
        }

        MessageSent?.Invoke(this, new MessageSentEventArgs(sent));
        return true;
    }

    /// <inheritdoc />
    public async Task BeginPushToTalkAsync()
    {
        // Dictation needs the message to go into, and a press while one is going on is ignored
        if (!HasProject)
        {
            StatusText = NoProjectText;
            return;
        }

        if (IsSending || DictationState != DictationState.Idle)
        {
            return;
        }

        var availability = _dictation.CheckAvailability();
        if (!availability.IsAvailable)
        {
            StatusText = availability.Message;
            return;
        }

        // Start listening; the engine may need a while to load the first time, during which the button can be released
        var epoch = _dictationEpoch;
        var cancellation = new CancellationTokenSource();
        _startCancellation = cancellation;
        _releaseRequested = false;
        _context = _fixedContext ?? SynchronizationContext.Current;
        StatusText = string.Empty;
        LivePartialText = string.Empty;
        SetDictationState(DictationState.Starting);

        DictationStart? start = null;
        string? failure = null;
        try
        {
            start = await _dictation.BeginAsync(cancellation.Token);
        }
        catch (OperationCanceledException)
        {
            // The button was released before listening started: nothing was heard and nothing is added
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            Log(LogLevel.Error, $"Dictation could not be started: {ex}");
            failure = $"Dictation could not be started: {ex.Message}";
        }
        finally
        {
            if (ReferenceEquals(_startCancellation, cancellation))
            {
                _startCancellation = null;
            }

            cancellation.Dispose();
        }

        // Dictation that was given up meanwhile, by closing the project, leaves nothing behind
        if (epoch != _dictationEpoch)
        {
            if (start?.Session is { } stale)
            {
                _ = ReleaseSessionAsync(stale);
            }

            return;
        }

        if (failure is not null || start?.Session is null)
        {
            SetDictationState(DictationState.Idle);
            StatusText = failure ?? (start is { Availability.IsAvailable: false } ? start.Availability.Message : string.Empty);
            return;
        }

        _session = start.Session;
        _session.PartialTextChanged += OnPartialTextChanged;
        SetDictationState(DictationState.Listening);

        // A release that came while listening was being started ends it at once
        if (_releaseRequested)
        {
            await FinishDictationAsync();
        }
    }

    /// <inheritdoc />
    public async Task EndPushToTalkAsync()
    {
        switch (DictationState)
        {
            case DictationState.Starting:
                // Listening has not started: remember the release and make the start give up
                _releaseRequested = true;
                if (_startCancellation is { } starting)
                {
                    await starting.CancelAsync();
                }

                break;

            case DictationState.Listening:
                await FinishDictationAsync();
                break;

            default:
                // Idle, or already finishing: a second release does nothing
                break;
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        DiscardDictation();
    }

    /// <summary>
    ///     Clears the sentence about the last action and checks that the message can be changed.
    /// </summary>
    /// <returns><see langword="true" /> when the message can be changed.</returns>
    private bool BeginEdit()
    {
        if (!HasProject)
        {
            StatusText = NoProjectText;
            return false;
        }

        if (IsSending)
        {
            return false;
        }

        StatusText = string.Empty;
        return true;
    }

    /// <summary>
    ///     Executes an edit as one step and brings the caret, the undo state, and the observers up to date.
    /// </summary>
    /// <param name="edit">The edit, made from the message as it is now.</param>
    /// <returns><see langword="true" /> when the edit changed the message.</returns>
    private bool Execute(ComposerEdit edit)
    {
        if (!_history.Execute(edit))
        {
            return false;
        }

        _caretOffset = edit.CaretAfter;
        _selectionLength = 0;
        NotifyCaretAndDocumentChanged();
        return true;
    }

    /// <summary>
    ///     Brings the caret and the observers up to date after an undo or a redo.
    /// </summary>
    /// <param name="caret">The caret to restore.</param>
    private void FinishHistoryStep(int? caret)
    {
        _caretOffset = Math.Clamp(caret ?? _caretOffset, 0, _document.Length);
        _selectionLength = 0;
        NotifyCaretAndDocumentChanged();
    }

    /// <summary>
    ///     Makes chips and text of dropped or pasted content and adds them as one step.
    /// </summary>
    /// <param name="content">What was dropped or pasted.</param>
    /// <param name="offset">The offset at which the items are put.</param>
    /// <param name="replaceLength">The length of the selection that the items replace.</param>
    /// <returns><see langword="true" /> when something was added.</returns>
    private bool AddContent(DroppedContent content, int offset, int replaceLength)
    {
        if (!BeginEdit())
        {
            return false;
        }

        // The intake only looks at the content and saves pasted pictures; nothing is copied while drafting
        var result = _intake!.Accept(content);
        var added = !result.Segments.IsEmpty &&
                    Execute(ComposerEdit.Drop(_document, offset, result.Segments, replaceLength));

        // Say what was left out and what to check, so that no item disappears without a word
        StatusText = Summarize(result);
        return added;
    }

    /// <summary>
    ///     Describes what the intake left out and what it accepted with a warning.
    /// </summary>
    /// <param name="result">What the intake made of the content.</param>
    /// <returns>A sentence, or an empty string when everything was accepted.</returns>
    private static string Summarize(AttachmentIntakeResult result)
    {
        var parts = new List<string>(2);
        if (!result.Skipped.IsEmpty)
        {
            parts.Add($"Left out: {Describe(result.Skipped)}");
        }

        if (!result.Flagged.IsEmpty)
        {
            parts.Add($"Please check: {Describe(result.Flagged)}");
        }

        return string.Join(' ', parts);
    }

    /// <summary>
    ///     Names the first few notes and says how many more there are.
    /// </summary>
    /// <param name="notes">The notes.</param>
    /// <returns>The description.</returns>
    private static string Describe(IReadOnlyList<IntakeNote> notes)
    {
        var text = string.Join(' ', notes.Take(NamedNotes).Select(note => $"{note.Item} - {note.Reason.TrimEnd('.')}."));
        return notes.Count > NamedNotes
            ? $"{text} And {(notes.Count - NamedNotes).ToString(CultureInfo.InvariantCulture)} more."
            : text;
    }

    /// <summary>
    ///     Finishes the dictation that is listening: stops it, and adds the words as one dictated block.
    /// </summary>
    /// <returns>A task that completes when the words have been added.</returns>
    private async Task FinishDictationAsync()
    {
        var session = _session;
        if (session is null)
        {
            return;
        }

        // Stop listening for changes and let the recognizer finish the words it already heard
        var epoch = _dictationEpoch;
        _session = null;
        session.PartialTextChanged -= OnPartialTextChanged;
        SetDictationState(DictationState.Finishing);
        var text = string.Empty;
        var problem = string.Empty;
        try
        {
            text = await session.EndAsync(CancellationToken.None);
            problem = session.Problem;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            Log(LogLevel.Error, $"Dictation ended with an error: {ex}");
            problem = $"Dictation stopped with an error: {ex.Message}";
        }
        finally
        {
            await DisposeSessionQuietlyAsync(session);
        }

        // Dictation that was given up while it finished, by closing the project, adds nothing
        if (epoch != _dictationEpoch)
        {
            return;
        }

        LivePartialText = string.Empty;
        SetDictationState(DictationState.Idle);

        // Add the words as one block at the caret, and keep the explanation of anything that went wrong
        if (!string.IsNullOrWhiteSpace(text))
        {
            AddDictatedBlock(text);
            if (problem.Length > 0)
            {
                StatusText = problem;
            }
        }
        else
        {
            StatusText = problem.Length > 0 ? problem : "No speech was recognized, so nothing was added.";
        }
    }

    /// <summary>
    ///     Gives up dictation without adding anything: a start that is under way is cancelled, and a session that is
    ///     listening is ended and released in the background.
    /// </summary>
    private void DiscardDictation()
    {
        _dictationEpoch++;
        _releaseRequested = false;
        _startCancellation?.Cancel();
        _startCancellation = null;
        var session = _session;
        _session = null;
        if (session is not null)
        {
            session.PartialTextChanged -= OnPartialTextChanged;
            _ = ReleaseSessionAsync(session);
        }

        LivePartialText = string.Empty;
        SetDictationState(DictationState.Idle);
    }

    /// <summary>
    ///     Ends a session and releases it, ignoring failures, which is what is left to do with a session that was given
    ///     up.
    /// </summary>
    /// <param name="session">The session.</param>
    /// <returns>A task that completes when the session is released.</returns>
    private async Task ReleaseSessionAsync(IDictationSession session)
    {
        try
        {
            await session.EndAsync(CancellationToken.None);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            Log(LogLevel.Warning, $"A dictation that was given up did not end cleanly: {ex.Message}");
        }
        catch (OperationCanceledException)
        {
            // Giving up is what was wanted
        }
        finally
        {
            await DisposeSessionQuietlyAsync(session);
        }
    }

    /// <summary>
    ///     Disposes a session, ignoring failures.
    /// </summary>
    /// <param name="session">The session.</param>
    /// <returns>A task that completes when the session is disposed.</returns>
    private async Task DisposeSessionQuietlyAsync(IDictationSession session)
    {
        try
        {
            await session.DisposeAsync();
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            Log(LogLevel.Warning, $"A dictation session did not release cleanly: {ex.Message}");
        }
    }

    /// <summary>
    ///     Shows the words that the recognizer has so far. The recognizer raises the event on its own thread, so the
    ///     change is moved to the thread that pressed the talk button.
    /// </summary>
    /// <param name="sender">The session.</param>
    /// <param name="e">The event data.</param>
    private void OnPartialTextChanged(object? sender, EventArgs e)
    {
        if (sender is not IDictationSession session)
        {
            return;
        }

        var text = session.PartialText;
        var epoch = _dictationEpoch;
        var context = _context;

        // Only the session that is listening may change what is shown
        void Apply()
        {
            if (epoch == _dictationEpoch && DictationState == DictationState.Listening)
            {
                LivePartialText = text;
            }
        }

        if (context is null || ReferenceEquals(context, SynchronizationContext.Current))
        {
            Apply();
        }
        else
        {
            context.Post(_ => Apply(), null);
        }
    }

    /// <summary>
    ///     Sets where dictation is and brings what depends on it up to date.
    /// </summary>
    /// <param name="state">The new state.</param>
    private void SetDictationState(DictationState state)
    {
        if (DictationState == state)
        {
            return;
        }

        DictationState = state;
        OnPropertyChanged(nameof(CanSend));
        SendCommand.NotifyCanExecuteChanged();
    }

    /// <summary>
    ///     Tells the observers that the block of sending changed and what depends on it.
    /// </summary>
    private void NotifySendBlockChanged()
    {
        OnPropertyChanged(nameof(IsSendBlocked));
        OnPropertyChanged(nameof(SendBlockedReason));
        OnPropertyChanged(nameof(CanSend));
        SendCommand.NotifyCanExecuteChanged();
    }

    /// <summary>
    ///     Empties the message, its history, and the marks without telling the observers.
    /// </summary>
    private void DropDraft()
    {
        _document.Clear();
        _history.Clear();
        _marks.Clear();
        _caretOffset = 0;
        _selectionLength = 0;
        LivePartialText = string.Empty;
    }

    /// <summary>
    ///     Removes the pictures that were saved for a draft and that no chip refers to, which after the draft was dropped
    ///     is all of them; a failure is logged and ignored, because the next cleanup finds them again.
    /// </summary>
    /// <param name="store">The store of the project the draft belonged to, or <see langword="null" /> for none.</param>
    private void RemoveLeftoversQuietly(IAttachmentStore? store)
    {
        if (store is null)
        {
            return;
        }

        var keep = _document.Chips.OfType<ImageChip>().Where(chip => chip.IsPasted).Select(chip => chip.FullPath).ToList();
        try
        {
            store.RemoveLeftovers(keep);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Log(LogLevel.Warning, $"Saved pictures of the draft could not be removed: {ex.Message}");
        }
    }

    /// <summary>
    ///     Tells the observers that the caret moved along with the change of the message.
    /// </summary>
    private void NotifyCaretAndDocumentChanged()
    {
        OnPropertyChanged(nameof(CaretOffset));
        OnPropertyChanged(nameof(SelectionLength));
        NotifyDocumentChanged();
    }

    /// <summary>
    ///     Tells the observers that the message, what can be undone, or what can be sent may have changed, once.
    /// </summary>
    private void NotifyDocumentChanged()
    {
        OnPropertyChanged(nameof(Document));
        OnPropertyChanged(nameof(MarkChipIds));
        OnPropertyChanged(nameof(HasContent));
        OnPropertyChanged(nameof(ShowsPlaceholder));
        OnPropertyChanged(nameof(CanEdit));
        OnPropertyChanged(nameof(CanSend));
        OnPropertyChanged(nameof(CanUndo));
        OnPropertyChanged(nameof(CanRedo));
        UndoCommand.NotifyCanExecuteChanged();
        RedoCommand.NotifyCanExecuteChanged();
        SendCommand.NotifyCanExecuteChanged();
        UndoRedoStateChanged?.Invoke(this, EventArgs.Empty);
        Changed?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    ///     Writes a line to the log, when there is a log.
    /// </summary>
    /// <param name="level">The severity.</param>
    /// <param name="message">The text.</param>
    private void Log(LogLevel level, string message)
    {
        _logger?.Log(level, message);
    }
}
