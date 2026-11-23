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

using System.Collections;
using System.Collections.ObjectModel;
using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DemaConsulting.CanvasPilot.AgentSubsystem;
using DemaConsulting.CanvasPilot.AnnotationSubsystem;
using DemaConsulting.CanvasPilot.ComposerSubsystem;
using DemaConsulting.CanvasPilot.LoggingSubsystem;
using DemaConsulting.CanvasPilot.PayloadSubsystem;
using DemaConsulting.CanvasPilot.ProjectSubsystem;
using DemaConsulting.CanvasPilot.SpeechSubsystem;

namespace DemaConsulting.CanvasPilot.ChatSubsystem;

/// <summary>
///     The view model of the chat panel: the transcript of the conversation (the messages that were sent and what the AI
///     copilot answered), the message being composed, the buttons that send it and that listen while they are held, the
///     button that stops the copilot, the banner that says whether the copilot can be used, and the joining of the marks
///     drawn on the picture to the message. It holds no rule of editing, sending, dictation, or answering, which belong to
///     the composer and the turn runner; it connects what they do to what the panel shows.
/// </summary>
/// <remarks>
///     <para>
///         <b>The transcript.</b> The panel appends one <see cref="TranscriptEntryViewModel" /> for every
///         <see cref="ComposerViewModel.MessageSent" />, with the immutable message, so the transcript never shows a
///         draft. When the panel has an AI copilot, the message starts a turn, and the rows of the answer (the reply that
///         streams, the tools it called, and the notices) follow the entry in the order things happen.
///     </para>
///     <para>
///         <b>History.</b> When the panel is given a history store and the project service, the text of every message and
///         of what followed it is saved on this computer, outside the project, when its turn has ended, and is shown again
///         as text rows when the project is opened. A restored message is a <see cref="RestoredEntryViewModel" />
///         (words only: the history keeps no pictures and no payload). What was saved is given as text to the copilot with
///         the next turn, so that the conversation can go on. <see cref="ClearHistoryCommand" /> deletes the saved history,
///         empties the transcript, and makes the copilot forget the conversation. Without a store or a project service
///         the transcript is in memory only and is dropped when the project changes or closes.
///     </para>
///     <para>
///         <b>A turn.</b> While a turn runs <see cref="IsCopilotBusy" /> is <see langword="true" />, the composer cannot
///         send (the message can still be written), and <see cref="StopCommand" /> stops the turn, which keeps the text
///         written so far. Without a copilot (<see langword="null" />) the panel only appends the entry of the message, as
///         before, and nothing is blocked.
///     </para>
///     <para>
///         <b>Readiness.</b> <see cref="RefreshReadinessAsync" /> asks the copilot whether the provider chosen in the
///         settings can be used, and <see cref="ReadinessMessage" /> shows the answer in a banner that is hidden when
///         everything is ready. The panel does not know the settings; whoever does calls the refresh when they change.
///         <see cref="OpenSettingsRequested" /> is raised when the user asks, from the banner or from an error row, to open
///         the settings; the window shell opens them.
///     </para>
///     <para>
///         <b>Send.</b> <see cref="SendCommand" /> is the composer's own command, so what makes sending possible (a project
///         is open, the message has content, nobody holds the talk button, nothing is being sent, the copilot is not
///         working) is decided in one place. When sending fails, the composer keeps the draft and says why in its status
///         text, which the panel shows.
///     </para>
///     <para>
///         <b>Hold to talk.</b> <see cref="BeginPushToTalkAsync" /> and <see cref="EndPushToTalkAsync" /> follow the talk
///         button (and, later, the talk key) and hand the press and the release to the composer, which listens, shows the
///         words as they come in <see cref="ComposerViewModel.LivePartialText" />, and adds them as one block. When dictation
///         cannot work the composer says why and what to do in its status text and nothing changes in the draft; the panel
///         only adds a label for the button that follows the state.
///     </para>
///     <para>
///         <b>Marks.</b> The annotation layer and the composer do not know each other, because the composer already uses
///         the annotation subsystem. The panel joins them: every mark drawn becomes a chip in the message at the caret, as
///         one undo step, and each time the message changes the layer is told which mark chips it holds and whether marks
///         can be made, so that undoing the chip hides the mark and redoing it shows the mark again.
///     </para>
///     <para>
///         The class is owned by the user interface thread. It does not own the composer, the registry, the layer, or the
///         copilot; the composition root does.
///     </para>
/// </remarks>
public sealed partial class ChatPanelViewModel : ObservableObject, IPushToTalkTarget, IDisposable
{
    /// <summary>
    ///     The label of the talk button while nothing listens.
    /// </summary>
    public const string TalkIdleText = "Hold to talk";

    /// <summary>
    ///     The text of the transcript when no message was sent yet.
    /// </summary>
    public const string EmptyTranscriptText = "What would you like to draw?";

    /// <summary>
    ///     The tip under the example prompts of the empty conversation.
    /// </summary>
    public const string EmptyTipText = "Drop a picture, or hold F8 and speak";

    /// <summary>
    ///     The prompts that the empty conversation offers; choosing one writes it in the message.
    /// </summary>
    public static IReadOnlyList<string> ExamplePrompts { get; } =
    [
        "Draw our release flow",
        "Chart downloads by month",
        "Put both on one page",
        "Make it match my style kit",
    ];

    /// <summary>
    ///     The follow-up prompts that are offered above the message while a conversation is going on and the copilot is idle.
    /// </summary>
    public static IReadOnlyList<string> SuggestedPrompts { get; } =
    [
        "Add a legend",
        "Make title larger",
        "Export as PNG",
    ];

    /// <summary>
    ///     The words of the indicator that shows while the copilot works on a message.
    /// </summary>
    public const string WorkingText = "The copilot is working...";

    /// <summary>
    ///     The notice that says that the chat history was cleared, which is not saved.
    /// </summary>
    public const string ClearedText = "Chat history cleared.";

    /// <summary>
    ///     The composer of the message.
    /// </summary>
    private readonly ComposerViewModel _composer;

    /// <summary>
    ///     The layer on which the user marks the picture.
    /// </summary>
    private readonly AnnotationOverlayViewModel _annotation;

    /// <summary>
    ///     Speech, which is prepared ahead of the first press of the talk button, or <see langword="null" /> for none.
    /// </summary>
    private readonly IDictationService? _dictation;

    /// <summary>
    ///     The AI copilot, or <see langword="null" /> for none.
    /// </summary>
    private readonly IAgentService? _agent;

    /// <summary>
    ///     Runs the turns of the copilot and adds their rows, or <see langword="null" /> without a copilot.
    /// </summary>
    private readonly ChatTurnRunner? _runner;

    /// <summary>
    ///     What the runner adds its rows to; it drops them while a turn of another project ends.
    /// </summary>
    private readonly TranscriptSink _sink;

    /// <summary>
    ///     Optional logger for diagnostics.
    /// </summary>
    private readonly RollingFileLogger? _logger;

    /// <summary>
    ///     Where the text of the conversation is kept on this computer, or <see langword="null" /> for nowhere.
    /// </summary>
    private readonly IChatHistoryStore? _history;

    /// <summary>
    ///     The open project, which tells where its history is kept, or <see langword="null" /> for none.
    /// </summary>
    private readonly IProjectService? _projects;

    /// <summary>
    ///     The clock that dates the rows of a turn.
    /// </summary>
    private readonly TimeProvider _clock;

    /// <summary>
    ///     The handler of the composer's property changes, kept so that it can be removed.
    /// </summary>
    private readonly PropertyChangedEventHandler _composerPropertyChanged;

    /// <summary>
    ///     The handler that tells the layer what the message holds, kept so that it can be removed.
    /// </summary>
    private readonly EventHandler _composerChanged;

    /// <summary>
    ///     The handler that adds the chip of every mark that is drawn, kept so that it can be removed.
    /// </summary>
    private readonly EventHandler<MarkAddedEventArgs> _markAdded;

    /// <summary>
    ///     The handler that takes the chips of all marks out of the message, kept so that it can be removed.
    /// </summary>
    private readonly EventHandler _clearMarks;

    /// <summary>
    ///     The handler of the sent messages, kept so that it can be removed.
    /// </summary>
    private readonly EventHandler<MessageSentEventArgs> _messageSent;

    /// <summary>
    ///     The folder of the project the transcript belongs to, or <see langword="null" /> when none was open.
    /// </summary>
    private string? _transcriptProject;

    /// <summary>
    ///     What the latest check of the copilot found, or <see langword="null" /> when none was made.
    /// </summary>
    private AgentReadinessReport? _readiness;

    /// <summary>
    ///     Counts the checks, so that only the latest answer is shown.
    /// </summary>
    private int _readinessVersion;

    /// <summary>
    ///     Whether a check of the copilot is running.
    /// </summary>
    private bool _isCheckingReadiness;

    /// <summary>
    ///     The latest turn, which completes when the turn has ended.
    /// </summary>
    private Task _latestTurn = Task.CompletedTask;

    /// <summary>
    ///     The history of the open project, or <see langword="null" /> when none is kept.
    /// </summary>
    private HistorySession? _session;

    /// <summary>
    ///     Counts the times the conversation was dropped or cleared, so that a turn that ends afterwards does not save rows
    ///     that belong to what was dropped.
    /// </summary>
    private int _generation;

    /// <summary>
    ///     Whether the panel was disposed.
    /// </summary>
    private bool _disposed;

    /// <summary>
    ///     Initializes a new instance of the <see cref="ChatPanelViewModel" /> class and joins the composer and the
    ///     annotation layer.
    /// </summary>
    /// <param name="composer">The composer of the message.</param>
    /// <param name="marks">The marks made for the draft, from which the pictures of the mark chips are made.</param>
    /// <param name="annotation">The layer on which the user marks the picture.</param>
    /// <param name="markImages">Cuts the area around a mark out of its picture, for the picture of a mark chip.</param>
    /// <param name="dictation">Speech, which is prepared ahead of the first press of the talk button, or <see langword="null" /> for none.</param>
    /// <param name="logger">Optional logger for diagnostics.</param>
    /// <param name="agent">The AI copilot that answers the messages, or <see langword="null" /> for none: a sent message is then only added to the transcript.</param>
    /// <param name="clock">The clock and timers of the turns, or <see langword="null" /> for the system's.</param>
    /// <param name="history">Where the text of the conversation is kept on this computer, or <see langword="null" /> for nowhere: the transcript is then only in memory.</param>
    /// <param name="projects">The project service, which tells which project is open and so where its history is kept, or <see langword="null" /> for none: nothing is then saved or restored.</param>
    /// <exception cref="ArgumentNullException">Thrown when an argument other than the dictation service, the logger, the copilot, the clock, the history store, and the project service is <see langword="null" />.</exception>
    public ChatPanelViewModel(
        ComposerViewModel composer,
        MarkRegistry marks,
        AnnotationOverlayViewModel annotation,
        IMarkImageComposer markImages,
        IDictationService? dictation = null,
        RollingFileLogger? logger = null,
        IAgentService? agent = null,
        TimeProvider? clock = null,
        IChatHistoryStore? history = null,
        IProjectService? projects = null)
    {
        ArgumentNullException.ThrowIfNull(composer);
        ArgumentNullException.ThrowIfNull(marks);
        ArgumentNullException.ThrowIfNull(annotation);
        ArgumentNullException.ThrowIfNull(markImages);

        _composer = composer;
        _annotation = annotation;
        _dictation = dictation;
        _agent = agent;
        _logger = logger;
        _history = history;
        _projects = projects;
        _clock = clock ?? TimeProvider.System;
        Transcript = [];
        _sink = new TranscriptSink(Transcript);
        Transcript.CollectionChanged += (_, _) => OnPropertyChanged(nameof(HasTranscript));
        Thumbnails = new ChipThumbnailProvider(chip => CropOfDraftMark(marks, markImages, chip));
        _transcriptProject = composer.ProjectFolder;
        StopCommand = new RelayCommand(Stop, () => IsCopilotBusy);
        OpenSettingsCommand = new RelayCommand(RaiseOpenSettings);
        CheckAgainCommand = new AsyncRelayCommand(RefreshReadinessAsync);
        ClearHistoryCommand = new RelayCommand(ClearHistory);
        CollapseCommand = new RelayCommand(() => CollapseRequested?.Invoke(this, EventArgs.Empty));

        // With a copilot, a sent message starts a turn whose rows follow the entry of the message
        if (agent is not null)
        {
            _runner = new ChatTurnRunner(agent, _sink, clock, logger, RaiseOpenSettings);
            _runner.BusyChanged += OnBusyChanged;
        }

        // The panel follows the composer: what depends on it is announced again, a changed project drops the transcript,
        // and a sent message joins the transcript
        _composerPropertyChanged = OnComposerPropertyChanged;
        _messageSent = (_, e) => OnMessageSent(e.Message);
        _composerChanged = (_, _) => JoinAnnotationToComposer();
        _markAdded = (_, e) => _composer.AddMark(e.Mark);
        _clearMarks = (_, _) => _composer.DeleteMarkChips();
        _composer.PropertyChanged += _composerPropertyChanged;
        _composer.MessageSent += _messageSent;
        _composer.Changed += _composerChanged;
        _annotation.MarkAdded += _markAdded;
        _annotation.ClearMarksRequested += _clearMarks;
        JoinAnnotationToComposer();

        // A project that is already open has a saved conversation
        BeginHistory();
    }

    /// <summary>
    ///     Raised when the user asks to open the settings, from the banner or from the notice of an error. The window shell
    ///     handles it.
    /// </summary>
    public event EventHandler? OpenSettingsRequested;

    /// <summary>
    ///     Raised when the user asks to hide the chat column with the button of the header. The workspace handles it.
    /// </summary>
    public event EventHandler? CollapseRequested;

    /// <summary>
    ///     Gets the composer of the message, which the panel's editing control shows.
    /// </summary>
    public ComposerViewModel Composer => _composer;

    /// <summary>
    ///     Gets the rows of the conversation, oldest first: the messages that were sent, the answers of the copilot, the
    ///     tools it called, and the notices. The panel and the turn runner add to it; nothing else does.
    /// </summary>
    public ObservableCollection<TranscriptRowViewModel> Transcript { get; }

    /// <summary>
    ///     Gets the rows of the messages that were sent, oldest first.
    /// </summary>
    public IEnumerable<TranscriptEntryViewModel> SentEntries => Transcript.OfType<TranscriptEntryViewModel>();

    /// <summary>
    ///     Gets a value indicating whether the transcript has any row in the open project.
    /// </summary>
    public bool HasTranscript => Transcript.Count > 0;

    /// <summary>
    ///     Gets the provider of the small pictures of the chips of the message being composed.
    /// </summary>
    public IChipThumbnailProvider Thumbnails { get; }

    /// <summary>
    ///     Gets the command that sends the message, which is the composer's.
    /// </summary>
    public IAsyncRelayCommand SendCommand => _composer.SendCommand;

    /// <summary>
    ///     Gets the command that stops the turn of the copilot that runs; it cannot run when none does.
    /// </summary>
    public IRelayCommand StopCommand { get; }

    /// <summary>
    ///     Gets the command that opens the settings, which raises <see cref="OpenSettingsRequested" />.
    /// </summary>
    public IRelayCommand OpenSettingsCommand { get; }

    /// <summary>
    ///     Gets the command that checks the copilot again, for the banner.
    /// </summary>
    public IAsyncRelayCommand CheckAgainCommand { get; }

    /// <summary>
    ///     Gets the command that clears the chat history: the saved history of the open project is deleted, the transcript
    ///     is emptied, and the copilot forgets the conversation. There is no question first.
    /// </summary>
    public IRelayCommand ClearHistoryCommand { get; }

    /// <summary>
    ///     Gets the command that asks to hide the chat column, which raises <see cref="CollapseRequested" />.
    /// </summary>
    public IRelayCommand CollapseCommand { get; }

    /// <summary>
    ///     Gets a value indicating whether the follow-up prompts are offered: a conversation is going on and the copilot is
    ///     idle.
    /// </summary>
    public bool ShowSuggestions => HasTranscript && !IsCopilotBusy;

    /// <inheritdoc />
    protected override void OnPropertyChanged(PropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);

        // The follow-up prompts depend on both of these
        if (e.PropertyName is nameof(HasTranscript) or nameof(IsCopilotBusy))
        {
            base.OnPropertyChanged(new PropertyChangedEventArgs(nameof(ShowSuggestions)));
        }
    }

    /// <summary>
    ///     Writes a prompt in the message at the caret, so that the user can change it and send it.
    /// </summary>
    /// <param name="prompt">The prompt.</param>
    /// <returns><see langword="true" /> when the prompt was written; <see langword="false" /> when the message cannot be edited.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="prompt" /> is <see langword="null" />.</exception>
    public bool UsePrompt(string prompt)
    {
        ArgumentNullException.ThrowIfNull(prompt);
        return _composer.CanEdit && _composer.InsertTyped(prompt);
    }

    /// <summary>
    ///     Gets a value indicating whether a turn of the copilot runs.
    /// </summary>
    public bool IsCopilotBusy => _runner?.IsBusy == true;

    /// <summary>
    ///     Gets a value indicating whether the banner of the readiness of the copilot is shown: the latest check found that
    ///     the copilot cannot be used, or can be used with a warning.
    /// </summary>
    public bool HasReadinessIssue => _readiness is { Status: not AgentReadinessStatus.Ready };

    /// <summary>
    ///     Gets what the banner says: what the latest check found and what to do, or an empty string when everything is
    ///     ready.
    /// </summary>
    public string ReadinessMessage => HasReadinessIssue ? _readiness!.Message : string.Empty;

    /// <summary>
    ///     Gets what the latest check of the copilot found, or <see langword="null" /> when none was made yet. Unlike the
    ///     banner it also holds the answer that everything is ready.
    /// </summary>
    public AgentReadinessReport? Readiness => _readiness;

    /// <summary>
    ///     Gets a value indicating whether a check of the copilot is running now.
    /// </summary>
    public bool IsCheckingReadiness => _isCheckingReadiness;

    /// <summary>
    ///     Gets a value indicating whether the panel has a copilot to talk to, which is what a check of the readiness asks.
    /// </summary>
    public bool HasCopilot => _agent is not null;

    /// <summary>
    ///     Gets a value indicating whether the message can be sent now.
    /// </summary>
    public bool CanSend => _composer.CanSend;

    /// <summary>
    ///     Gets a value indicating whether the talk button can be used: a message can be written.
    /// </summary>
    public bool CanTalk => _composer.CanEdit;

    /// <summary>
    ///     Gets a value indicating whether the talk button is held and the application is listening, or starting to, or
    ///     finishing.
    /// </summary>
    public bool IsTalking => _composer.IsDictating;

    /// <summary>
    ///     Gets the label of the talk button, which says what the button is doing.
    /// </summary>
    public string TalkText => _composer.DictationState switch
    {
        DictationState.Starting => "Starting... keep holding",
        DictationState.Listening => "Listening... let go to finish",
        DictationState.Finishing => "Finishing...",
        _ => TalkIdleText,
    };

    /// <summary>
    ///     Gets or sets what shows the payload inspector. While it is <see langword="null" /> the "View payload" commands
    ///     do nothing. The window sets it when the panel is placed in it.
    /// </summary>
    public IPayloadInspectorLauncher? InspectorLauncher { get; set; }

    /// <summary>
    ///     Stops the turn of the copilot that runs, if any. The text written so far is kept and marked as stopped, and the
    ///     composer can send again when the turn has ended, which is soon.
    /// </summary>
    public void Stop() => _runner?.Stop();

    /// <summary>
    ///     Gets a task that completes when the latest turn has ended; it is completed when no turn was started.
    /// </summary>
    /// <returns>The task.</returns>
    public Task WaitForTurnAsync() => _latestTurn;

    /// <summary>
    ///     Asks the copilot whether the provider chosen in the settings can be used and shows the answer in the banner. A
    ///     check that is overtaken by a later one is forgotten, so the banner shows the latest. The panel does not know the
    ///     settings, so whoever does calls this when they change. Nothing happens without a copilot. A failure is logged
    ///     and shown in the banner, never thrown.
    /// </summary>
    /// <returns>A task that completes when the answer was shown.</returns>
    public async Task RefreshReadinessAsync()
    {
        if (_agent is null)
        {
            return;
        }

        var version = ++_readinessVersion;
        SetCheckingReadiness(true);
        AgentReadinessReport report;
        try
        {
            report = await _agent.GetReadinessAsync();
        }
        catch (OperationCanceledException)
        {
            if (version == _readinessVersion)
            {
                SetCheckingReadiness(false);
            }

            return;
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            _logger?.Log(LogLevel.Warning, $"Checking the copilot failed: {ex.GetType().Name}.");
            report = new AgentReadinessReport(AgentReadinessStatus.NotConfigured, $"The copilot could not be checked: {ex.Message}");
        }

        if (version == _readinessVersion && !_disposed)
        {
            _readiness = report;
            OnPropertyChanged(nameof(HasReadinessIssue));
            OnPropertyChanged(nameof(ReadinessMessage));
            OnPropertyChanged(nameof(Readiness));
            SetCheckingReadiness(false);
        }
    }

    /// <summary>
    ///     Records whether a check of the copilot is running and announces a change.
    /// </summary>
    /// <param name="value">Whether a check is running.</param>
    private void SetCheckingReadiness(bool value)
    {
        if (_isCheckingReadiness != value)
        {
            _isCheckingReadiness = value;
            OnPropertyChanged(nameof(IsCheckingReadiness));
        }
    }

    /// <summary>
    ///     Starts listening because the talk button was pressed. The composer decides whether dictation can work and, when
    ///     it cannot, says why and what to do in its status text, which the panel shows; it never throws for that.
    /// </summary>
    /// <returns>A task that completes when listening has started or has been found impossible.</returns>
    public Task BeginPushToTalkAsync() => _composer.BeginPushToTalkAsync();

    /// <summary>
    ///     Stops listening because the talk button was released and adds what was said to the message as one block. A
    ///     release when nothing listens is ignored.
    /// </summary>
    /// <returns>A task that completes when what was said has been added to the message.</returns>
    public Task EndPushToTalkAsync() => _composer.EndPushToTalkAsync();

    /// <summary>
    ///     Loads the speech engine ahead of the first press of the talk button, so that the first press does not wait for
    ///     it. The panel calls it when it is shown. Nothing is loaded when dictation is not available, and a failure is
    ///     logged, never thrown.
    /// </summary>
    /// <returns>A task that completes when the preparation has finished.</returns>
    public async Task PrepareDictationAsync()
    {
        if (_dictation is null)
        {
            return;
        }

        try
        {
            await _dictation.PrepareAsync();
        }
        catch (OperationCanceledException)
        {
            // Preparing was given up; the first press loads the engine instead
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            _logger?.Log(LogLevel.Warning, $"Preparing dictation failed: {ex.Message}");
        }
    }

    /// <summary>
    ///     Stops following the composer and the annotation layer and releases the pictures of the transcript and the
    ///     draft's chips. It does not dispose the composer, the registry, or the layer.
    /// </summary>
    public void Dispose()
    {
        _disposed = true;
        _composer.PropertyChanged -= _composerPropertyChanged;
        _composer.MessageSent -= _messageSent;
        _composer.Changed -= _composerChanged;
        _annotation.MarkAdded -= _markAdded;
        _annotation.ClearMarksRequested -= _clearMarks;
        if (_runner is not null)
        {
            // Nothing is added to a transcript that is dropped, and a turn that is still running is stopped
            _sink.Discarding = true;
            _runner.BusyChanged -= OnBusyChanged;
            _runner.Dispose();
        }

        ClearTranscript();
        if (Thumbnails is IDisposable disposable)
        {
            disposable.Dispose();
        }
    }

    /// <summary>
    ///     Cuts the area around a mark of the draft out of its picture, for the picture of the mark's chip.
    /// </summary>
    /// <param name="marks">The marks of the draft.</param>
    /// <param name="markImages">Cuts the area out.</param>
    /// <param name="chip">The chip of the mark.</param>
    /// <returns>The PNG bytes of the area, or <see langword="null" /> when the registry has no such mark.</returns>
    private static byte[]? CropOfDraftMark(MarkRegistry marks, IMarkImageComposer markImages, MarkChip chip)
    {
        var mark = marks.Find(chip.MarkId);
        if (mark is null)
        {
            return null;
        }

        var images = markImages.Compose(mark.Snapshot, [mark]);
        return images.Crops.Length == 0 ? null : images.Crops[0].Png.ToArray();
    }

    /// <summary>
    ///     Tells the layer which mark chips the message holds and whether marks can be made, so that the layer shows
    ///     exactly the marks of the message.
    /// </summary>
    private void JoinAnnotationToComposer()
    {
        _annotation.IsDrawingAllowed = _composer.CanEdit;
        _annotation.SetActiveMarkIds(_composer.MarkChipIds);
    }

    /// <summary>
    ///     Announces what the panel shows from the composer and drops the transcript when the project changed.
    /// </summary>
    /// <param name="sender">The composer.</param>
    /// <param name="e">The name of the property that changed.</param>
    private void OnComposerPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(ComposerViewModel.CanSend):
                OnPropertyChanged(nameof(CanSend));
                break;

            case nameof(ComposerViewModel.CanEdit):
            case nameof(ComposerViewModel.IsSending):
                // Sending takes the message away from the user for a moment, which also stops the talk button
                OnPropertyChanged(nameof(CanTalk));
                OnPropertyChanged(nameof(CanSend));
                break;

            case nameof(ComposerViewModel.IsDictating):
                OnPropertyChanged(nameof(IsTalking));
                break;

            case nameof(ComposerViewModel.DictationState):
                OnPropertyChanged(nameof(TalkText));
                break;

            case nameof(ComposerViewModel.ProjectFolder):
                // Another project is another conversation: the transcript belongs to its project, and the saved text of the
                // new project, if there is any, comes back below
                if (!string.Equals(_transcriptProject, _composer.ProjectFolder, StringComparison.OrdinalIgnoreCase))
                {
                    _transcriptProject = _composer.ProjectFolder;
                    _generation++;

                    // A turn of the old project is stopped, and what it still says is dropped until it has ended
                    if (_runner is { IsBusy: true })
                    {
                        _sink.Discarding = true;
                        _runner.Stop();
                    }

                    ClearTranscript();

                    // The conversation of the new project, if it was saved, takes the place of the old one
                    BeginHistory();
                }

                break;
        }
    }

    /// <summary>
    ///     Adds a sent message to the end of the transcript and, with a copilot, starts the turn that answers it. The
    ///     message and what follows it are saved when the turn has ended.
    /// </summary>
    /// <param name="message">The message.</param>
    private void OnMessageSent(SentMessage message)
    {
        var session = _session;
        var generation = _generation;
        var start = AppendEntry(message);
        if (_runner is not null)
        {
            _latestTurn = RunTurnAsync(_runner, message, session, generation, start);
        }
        else
        {
            SaveTurn(session, generation, start);
        }
    }

    /// <summary>
    ///     Runs the turn of a message. The runner reports what happens as rows and never throws for it, but nothing that
    ///     goes wrong may escape from a task that nobody awaits, so a failure is logged.
    /// </summary>
    /// <param name="runner">The runner.</param>
    /// <param name="message">The message that was sent.</param>
    /// <param name="session">The history that the turn belongs to, or <see langword="null" /> for none.</param>
    /// <param name="generation">The generation of the conversation when the message was sent.</param>
    /// <param name="start">The index in the transcript of the row of the message.</param>
    /// <returns>A task that completes when the turn has ended.</returns>
    private async Task RunTurnAsync(ChatTurnRunner runner, SentMessage message, HistorySession? session, int generation, int start)
    {
        try
        {
            // The text of what was said before goes to the copilot with the message; the service gives it to a new
            // provider session once and ignores it for a session that has it
            var earlier = session is { Rows.Count: > 0 } ? ChatHistoryMapper.ToAgentTurns(session.Rows) : null;
            var outcome = await runner.RunTurnAsync(message, earlier);
            if (outcome != ChatTurnOutcome.Refused)
            {
                SaveTurn(session, generation, start);
            }
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            _logger?.Log(LogLevel.Error, $"The turn of the copilot ended with {ex.GetType().Name}.");
        }
    }

    /// <summary>
    ///     Saves the message of a turn and what followed it, unless the conversation was dropped or cleared meanwhile.
    ///     A failure to save is a notice in the transcript, which is not saved, and never an exception.
    /// </summary>
    /// <param name="session">The history that the turn belongs to, or <see langword="null" /> for none.</param>
    /// <param name="generation">The generation of the conversation when the message was sent.</param>
    /// <param name="start">The index in the transcript of the row of the message.</param>
    private void SaveTurn(HistorySession? session, int generation, int start)
    {
        if (session is null || _history is null || _disposed || generation != _generation || start >= Transcript.Count)
        {
            return;
        }

        var rows = ChatHistoryMapper.ToHistoryRows(Transcript.Skip(start), _clock.GetUtcNow());
        session.Rows = ChatHistoryStore.Cap([.. session.Rows, .. rows]);
        var result = _history.Save(session.Project, session.Rows);
        if (!result.Succeeded)
        {
            _logger?.Log(LogLevel.Warning, "The chat history of the project could not be saved.");
            Transcript.Add(new NoticeRowViewModel(NoticeSeverity.Error, result.Message));
        }
    }

    /// <summary>
    ///     Opens the saved conversation of the open project: its rows are shown as text and are what the copilot is given
    ///     with the next turn. Nothing happens without a history store, a project service, or an open project.
    /// </summary>
    private void BeginHistory()
    {
        _session = null;
        var folder = _composer.ProjectFolder;
        if (_history is null || folder is null || _projects?.Current is not { } project || !IsSameFolder(project.FolderPath, folder))
        {
            return;
        }

        var rows = _history.Load(project);
        _session = new HistorySession(project) { Rows = rows };
        foreach (var row in rows)
        {
            Transcript.Add(ChatHistoryMapper.ToTranscriptRow(row));
        }

        if (rows.Count > 0)
        {
            _logger?.Log(LogLevel.Info, $"The chat history of the project was restored: {rows.Count} rows.");
        }

        OnPropertyChanged(nameof(HasTranscript));
    }

    /// <summary>
    ///     Deletes the saved history of the open project, empties the transcript, and makes the copilot forget the
    ///     conversation, so that nothing of what was said is left on the screen, on the disk, or with the provider. A turn
    ///     that runs is stopped and what it still says is dropped.
    /// </summary>
    private void ClearHistory()
    {
        _generation++;
        if (_runner is { IsBusy: true })
        {
            _sink.Discarding = true;
            _runner.Stop();
        }

        ClearTranscript();
        var session = _session;
        var result = session is null ? ChatHistoryResult.Ok : _history!.Clear(session.Project);
        _session = session is null ? null : new HistorySession(session.Project) { Rows = [] };
        _agent?.ResetConversation();
        Transcript.Add(result.Succeeded
            ? new NoticeRowViewModel(NoticeSeverity.Information, ClearedText)
            : new NoticeRowViewModel(NoticeSeverity.Error, result.Message));
    }

    /// <summary>
    ///     Determines whether two paths name the same folder, without regard to case or a trailing separator.
    /// </summary>
    /// <param name="first">The first path.</param>
    /// <param name="second">The second path.</param>
    /// <returns><see langword="true" /> when they name the same folder.</returns>
    private static bool IsSameFolder(string first, string second)
    {
        return string.Equals(
            Path.TrimEndingDirectorySeparator(first),
            Path.TrimEndingDirectorySeparator(second),
            StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    ///     Follows the turn of the copilot: the composer cannot send while it runs, and the buttons and the indicator
    ///     follow.
    /// </summary>
    /// <param name="sender">The runner.</param>
    /// <param name="e">The event data.</param>
    private void OnBusyChanged(object? sender, EventArgs e)
    {
        if (_runner!.IsBusy)
        {
            _composer.BlockSending(ComposerViewModel.CopilotWorkingText);
        }
        else
        {
            _sink.Discarding = false;
            _composer.UnblockSending();
        }

        OnPropertyChanged(nameof(IsCopilotBusy));
        StopCommand.NotifyCanExecuteChanged();
    }

    /// <summary>
    ///     Tells the shell that the user asked to open the settings.
    /// </summary>
    private void RaiseOpenSettings() => OpenSettingsRequested?.Invoke(this, EventArgs.Empty);

    /// <summary>
    ///     Adds a sent message to the end of the transcript.
    /// </summary>
    /// <param name="message">The message.</param>
    /// <returns>The index of its row in the transcript.</returns>
    private int AppendEntry(SentMessage message)
    {
        Transcript.Add(new TranscriptEntryViewModel(message, _composer.ProjectFolder, () => InspectorLauncher));
        OnPropertyChanged(nameof(HasTranscript));
        return Transcript.Count - 1;
    }

    /// <summary>
    ///     Removes every message from the transcript and releases their pictures.
    /// </summary>
    private void ClearTranscript()
    {
        foreach (var row in Transcript)
        {
            row.Dispose();
        }

        Transcript.Clear();
        OnPropertyChanged(nameof(HasTranscript));
    }

    /// <summary>
    ///     The saved conversation of the open project: which project it belongs to and the rows that are saved, which the
    ///     panel keeps so that it can give them to the copilot and save them again with the rows of the next turn.
    /// </summary>
    private sealed class HistorySession
    {
        /// <summary>
        ///     Initializes a new instance of the <see cref="HistorySession" /> class.
        /// </summary>
        /// <param name="project">The project.</param>
        public HistorySession(ProjectInfo project)
        {
            Project = project;
        }

        /// <summary>
        ///     Gets the project that the history belongs to.
        /// </summary>
        public ProjectInfo Project { get; }

        /// <summary>
        ///     Gets or sets the rows that are saved, oldest first.
        /// </summary>
        public IReadOnlyList<ChatHistoryRow> Rows { get; set; } = [];
    }

    /// <summary>
    ///     What the turn runner adds its rows to: the transcript, except while a turn that belongs to a project that was
    ///     closed ends, when the rows are dropped. Only adding is meant to be used.
    /// </summary>
    private sealed class TranscriptSink : ICollection<TranscriptRowViewModel>
    {
        /// <summary>
        ///     The transcript of the panel.
        /// </summary>
        private readonly ObservableCollection<TranscriptRowViewModel> _transcript;

        /// <summary>
        ///     Initializes a new instance of the <see cref="TranscriptSink" /> class.
        /// </summary>
        /// <param name="transcript">The transcript of the panel.</param>
        public TranscriptSink(ObservableCollection<TranscriptRowViewModel> transcript)
        {
            _transcript = transcript;
        }

        /// <inheritdoc />
        public int Count => _transcript.Count;

        /// <inheritdoc />
        public bool IsReadOnly => false;

        /// <summary>
        ///     Gets or sets a value indicating whether rows are dropped instead of added.
        /// </summary>
        public bool Discarding { get; set; }

        /// <inheritdoc />
        public void Add(TranscriptRowViewModel item)
        {
            if (Discarding)
            {
                item.Dispose();
                return;
            }

            _transcript.Add(item);
        }

        /// <inheritdoc />
        public void Clear() => _transcript.Clear();

        /// <inheritdoc />
        public bool Contains(TranscriptRowViewModel item) => _transcript.Contains(item);

        /// <inheritdoc />
        public void CopyTo(TranscriptRowViewModel[] array, int arrayIndex) => _transcript.CopyTo(array, arrayIndex);

        /// <inheritdoc />
        public bool Remove(TranscriptRowViewModel item) => _transcript.Remove(item);

        /// <inheritdoc />
        public IEnumerator<TranscriptRowViewModel> GetEnumerator() => _transcript.GetEnumerator();

        /// <inheritdoc />
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
