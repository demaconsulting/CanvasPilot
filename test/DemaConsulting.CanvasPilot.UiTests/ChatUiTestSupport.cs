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

using System.Runtime.CompilerServices;
using System.Threading.Channels;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Threading;
using Avalonia.VisualTree;
using DemaConsulting.CanvasPilot.AgentSubsystem;
using DemaConsulting.CanvasPilot.AnnotationSubsystem;
using DemaConsulting.CanvasPilot.ChatSubsystem;
using DemaConsulting.CanvasPilot.ComposerSubsystem;
using DemaConsulting.CanvasPilot.PayloadSubsystem;
using DemaConsulting.CanvasPilot.PreviewSubsystem;
using DemaConsulting.CanvasPilot.ProjectSubsystem;
using DemaConsulting.CanvasPilot.SpeechSubsystem;

namespace DemaConsulting.CanvasPilot.UiTests;

/// <summary>
///     A dictation session for the chat UI tests: it says what the test tells it to and ends with the text the test chose.
/// </summary>
internal sealed class ScriptedDictationSession : IDictationSession
{
    /// <inheritdoc />
    public event EventHandler? PartialTextChanged;

    /// <inheritdoc />
    public string PartialText { get; private set; } = string.Empty;

    /// <inheritdoc />
    public string Problem => string.Empty;

    /// <summary>
    ///     Gets or sets the text that <see cref="EndAsync" /> returns.
    /// </summary>
    public string FinalText { get; set; } = string.Empty;

    /// <summary>
    ///     Gets how many times the session was ended.
    /// </summary>
    public int EndCount { get; private set; }

    /// <summary>
    ///     Makes the recognizer hear words.
    /// </summary>
    /// <param name="text">The words recognized so far.</param>
    public void Say(string text)
    {
        PartialText = text;
        PartialTextChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <inheritdoc />
    public Task<string> EndAsync(CancellationToken cancellationToken = default)
    {
        EndCount++;
        return Task.FromResult(FinalText);
    }

    /// <inheritdoc />
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}

/// <summary>
///     A dictation service for the chat UI tests whose availability the test chooses.
/// </summary>
internal sealed class ScriptedDictationService : IDictationService
{
    /// <summary>
    ///     Gets or sets what dictation says about itself.
    /// </summary>
    public DictationAvailability Availability { get; set; } = DictationAvailability.Ready;

    /// <summary>
    ///     Gets the session that listening starts.
    /// </summary>
    public ScriptedDictationSession Session { get; } = new();

    /// <summary>
    ///     Gets how many times listening was started.
    /// </summary>
    public int BeginCount { get; private set; }

    /// <summary>
    ///     Gets how many times the engine was prepared ahead of time.
    /// </summary>
    public int PrepareCount { get; private set; }

    /// <inheritdoc />
    public DictationAvailability CheckAvailability() => Availability;

    /// <inheritdoc />
    public Task<DictationAvailability> PrepareAsync(CancellationToken cancellationToken = default)
    {
        PrepareCount++;
        return Task.FromResult(Availability);
    }

    /// <inheritdoc />
    public Task<DictationStart> BeginAsync(CancellationToken cancellationToken = default)
    {
        BeginCount++;
        return Task.FromResult(new DictationStart(Session, DictationAvailability.Ready));
    }
}

/// <summary>
///     A builder of sent messages for the chat UI tests that gives a payload with the text of the message.
/// </summary>
internal sealed class TextPayloadBuilder : IMessageBuilder
{
    /// <inheritdoc />
    public Task<SentMessage> BuildAsync(
        ComposerSnapshot message,
        IReadOnlyList<Mark> marks,
        IReadOnlyList<StagedAttachment> attachments,
        CancellationToken cancellationToken = default)
    {
        var text = message.FlatText.Replace(ComposerSnapshot.ChipPlaceholder, '#');
        var payload = new MessagePayload(text, [new PayloadTextPart(text, TextOrigin.Typed)], [], [], DateTimeOffset.UtcNow);
        return Task.FromResult(new SentMessage(message, marks, attachments, payload, DateTimeOffset.UtcNow));
    }
}

/// <summary>
///     A composer of mark pictures for the chat UI tests that gives a small real PNG as the area around every mark.
/// </summary>
internal sealed class StubMarkImageComposer : IMarkImageComposer
{
    /// <summary>
    ///     Gets the PNG bytes that every area is.
    /// </summary>
    public byte[] Png { get; } = MakePng();

    /// <inheritdoc />
    public MarkedPictureImages Compose(RenderedImage picture, IReadOnlyList<Mark> marks) =>
        new(Png, [.. marks.Select(mark => new MarkCrop(mark.Number, Png, new RenderBox(0, 0, 8, 8)))]);

    /// <summary>
    ///     Makes a small PNG file of one pixel.
    /// </summary>
    /// <returns>The bytes of the file.</returns>
    public static byte[] MakePng() =>
        Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8z8BQDwAEhQGAhKmMIQAAAABJRU5ErkJggg==");
}

/// <summary>
///     A source of the shown picture for the chat UI tests.
/// </summary>
internal sealed class FixedPictureSource : IRenderedImageSource
{
    /// <summary>
    ///     The picture.
    /// </summary>
    private RenderedImage? _image;

    /// <inheritdoc />
    public event EventHandler? CurrentImageChanged;

    /// <inheritdoc />
    public RenderedImage? CurrentImage
    {
        get => _image;
        set
        {
            _image = value;
            CurrentImageChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <inheritdoc />
    public string? ProjectFolder { get; set; }
}

/// <summary>
///     A launcher of the payload inspector for the chat UI tests that records what it was asked to show.
/// </summary>
internal sealed class RecordingInspectorLauncher : IPayloadInspectorLauncher
{
    /// <summary>
    ///     Gets what the launcher was asked to show, in order.
    /// </summary>
    public List<PayloadInspectorViewModel> Shown { get; } = [];

    /// <inheritdoc />
    public Task ShowPayloadInspectorAsync(PayloadInspectorViewModel viewModel)
    {
        Shown.Add(viewModel);
        return Task.CompletedTask;
    }
}

/// <summary>
///     An AI copilot for the chat UI tests that answers a turn with the events that the test gives it, when the test
///     gives them, and that reports the readiness that the test chooses.
/// </summary>
internal sealed class ScriptedAgentService : IAgentService
{
    /// <summary>
    ///     The events of the turn that runs, which the test writes.
    /// </summary>
    private Channel<AgentEvent> _events = Channel.CreateUnbounded<AgentEvent>();

    /// <inheritdoc />
    public event EventHandler<FileWrittenEventArgs>? FileWritten;

    /// <summary>
    ///     Raises <see cref="FileWritten" />, as the real service does when a tool wrote a file.
    /// </summary>
    /// <param name="e">What the event carries.</param>
    public void RaiseFileWritten(FileWrittenEventArgs e) => FileWritten?.Invoke(this, e);

    /// <summary>
    ///     Gets or sets what the readiness check finds.
    /// </summary>
    public AgentReadinessReport Readiness { get; set; } = new(AgentReadinessStatus.Ready, "Ready.");

    /// <summary>
    ///     Gets the number of readiness checks that were made.
    /// </summary>
    public int ReadinessCalls { get; private set; }

    /// <summary>
    ///     Gets the requests that were made, in order.
    /// </summary>
    public List<AgentTurnRequest> Requests { get; } = [];

    /// <summary>
    ///     Gets the cancellation token of the latest turn.
    /// </summary>
    public CancellationToken LatestToken { get; private set; }

    /// <summary>
    ///     Gives events to the turn that runs.
    /// </summary>
    /// <param name="events">The events, in order.</param>
    public void Emit(params AgentEvent[] events)
    {
        foreach (var agentEvent in events)
        {
            _events.Writer.TryWrite(agentEvent);
        }
    }

    /// <inheritdoc />
    public Task<AgentReadinessReport> GetReadinessAsync(CancellationToken cancellationToken = default)
    {
        ReadinessCalls++;
        return Task.FromResult(Readiness);
    }

    /// <inheritdoc />
    public IAsyncEnumerable<AgentEvent> RunTurnAsync(AgentTurnRequest request, CancellationToken cancellationToken = default)
    {
        Requests.Add(request);
        LatestToken = cancellationToken;
        _events = Channel.CreateUnbounded<AgentEvent>();
        return Read(_events, cancellationToken);
    }

    /// <inheritdoc />
    public void ResetConversation() => ResetCount++;

    /// <summary>
    ///     Gets the number of times the conversation was reset.
    /// </summary>
    public int ResetCount { get; private set; }

    /// <inheritdoc />
    public void Dispose()
    {
    }

    /// <summary>
    ///     Reads the events of a turn as they are given; a canceled turn just ends, as the real service does.
    /// </summary>
    /// <param name="channel">The events of the turn.</param>
    /// <param name="cancellationToken">The token of the turn.</param>
    /// <returns>The events.</returns>
    private static async IAsyncEnumerable<AgentEvent> Read(
        Channel<AgentEvent> channel,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        while (true)
        {
            var more = false;
            try
            {
                more = await channel.Reader.WaitToReadAsync(cancellationToken);
            }
            catch (OperationCanceledException)
            {
                // The real service ends the stream of a canceled turn without an event
            }

            if (!more)
            {
                yield break;
            }

            while (channel.Reader.TryRead(out var agentEvent))
            {
                yield return agentEvent;
            }
        }
    }
}

/// <summary>
///     A project service for the chat UI tests that reports one project as open and does nothing else.
/// </summary>
internal sealed class FixedProjectService : IProjectService
{
    /// <summary>
    ///     The handlers of the change of the project, which is never raised.
    /// </summary>
    private EventHandler? _changed;

    /// <summary>
    ///     Initializes a new instance of the <see cref="FixedProjectService" /> class.
    /// </summary>
    /// <param name="current">The project that is open.</param>
    public FixedProjectService(ProjectInfo current)
    {
        Current = current;
    }

    /// <inheritdoc />
    public event EventHandler? CurrentChanged
    {
        add => _changed += value;
        remove => _changed -= value;
    }

    /// <inheritdoc />
    public ProjectInfo? Current { get; }

    /// <inheritdoc />
    public string? ValidateProjectName(string? name) => throw new NotSupportedException();

    /// <inheritdoc />
    public string? ValidateParentFolder(string? parentFolder) => throw new NotSupportedException();

    /// <inheritdoc />
    public string? ValidateNewProject(string? parentFolder, string? name) => throw new NotSupportedException();

    /// <inheritdoc />
    public Task<ProjectResult> CreateAsync(string parentFolder, string name, CancellationToken cancellationToken = default) => throw new NotSupportedException();

    /// <inheritdoc />
    public Task<ProjectResult> OpenAsync(string projectFile, CancellationToken cancellationToken = default) => throw new NotSupportedException();

    /// <inheritdoc />
    public Task<ProjectResult> SaveAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();

    /// <inheritdoc />
    public Task<ProjectResult> SetActiveStyleKitAsync(string? styleKitId, CancellationToken cancellationToken = default) => throw new NotSupportedException();

    /// <inheritdoc />
    public void Close() => throw new NotSupportedException();
}

/// <summary>
///     A window with the chat panel, its composer, the annotation layer, and a project folder, for the headless tests of
///     the panel. Only dictation, the builder of sent messages, the pictures of marks, and, when a test asks for one, the
///     AI copilot are replaced.
/// </summary>
internal sealed class ChatHarness : IDisposable
{
    /// <summary>
    ///     Initializes a new instance of the <see cref="ChatHarness" /> class and shows the window.
    /// </summary>
    /// <param name="openProject"><see langword="true" /> to open the project, which makes the message editable.</param>
    /// <param name="agent">The AI copilot of the panel, or <see langword="null" /> for none.</param>
    /// <param name="history">The store of the chat history, or <see langword="null" /> for none; it needs the project to be open.</param>
    /// <param name="prepare">Called with the project folder before the project is opened, to make what the test needs in it or in the history.</param>
    public ChatHarness(bool openProject = true, IAgentService? agent = null, IChatHistoryStore? history = null, Action<ProjectInfo>? prepare = null)
    {
        Folder = Directory.CreateTempSubdirectory("canvaspilot-ui-chat-").FullName;
        Project = Path.Combine(Folder, "project");
        Directory.CreateDirectory(Project);
        ProjectInfo = new ProjectInfo("Demo", Project, Path.Combine(Project, "Demo.canvaspilot"), new ProjectDocument { Name = "Demo" });
        prepare?.Invoke(ProjectInfo);
        Marks = new MarkRegistry();
        Dictation = new ScriptedDictationService();
        Picture = AnnotationUiPictures.Picture();
        Composer = new ComposerViewModel(new TextPayloadBuilder(), Marks, Dictation);
        Annotation = new AnnotationOverlayViewModel(Marks, new FixedPictureSource { CurrentImage = Picture }, _ => { });
        if (openProject)
        {
            Composer.SetProject(Project);
        }

        Chat = new ChatPanelViewModel(Composer, Marks, Annotation, new StubMarkImageComposer(), Dictation, agent: agent, history: history, projects: new FixedProjectService(ProjectInfo));
        View = new ChatPanelView { DataContext = Chat };
        Window = new Window { Width = 420, Height = 640, Content = View };
        Window.Show();
        Layout();
    }

    /// <summary>
    ///     Gets the temporary folder that holds the project and the files of the test.
    /// </summary>
    public string Folder { get; }

    /// <summary>
    ///     Gets the folder of the project.
    /// </summary>
    public string Project { get; }

    /// <summary>
    ///     Gets the project that the project service of the panel reports as open.
    /// </summary>
    public ProjectInfo ProjectInfo { get; }

    /// <summary>
    ///     Gets the marks of the draft.
    /// </summary>
    public MarkRegistry Marks { get; }

    /// <summary>
    ///     Gets the dictation service.
    /// </summary>
    public ScriptedDictationService Dictation { get; }

    /// <summary>
    ///     Gets the picture the annotation layer draws on.
    /// </summary>
    public RenderedImage Picture { get; }

    /// <summary>
    ///     Gets the composer of the message.
    /// </summary>
    public ComposerViewModel Composer { get; }

    /// <summary>
    ///     Gets the annotation layer.
    /// </summary>
    public AnnotationOverlayViewModel Annotation { get; }

    /// <summary>
    ///     Gets the view model of the panel.
    /// </summary>
    public ChatPanelViewModel Chat { get; }

    /// <summary>
    ///     Gets the view under test.
    /// </summary>
    public ChatPanelView View { get; }

    /// <summary>
    ///     Gets the window that shows the view.
    /// </summary>
    public Window Window { get; }

    /// <summary>
    ///     Gets the talk button.
    /// </summary>
    public Button TalkButton => Find<Button>("ChatHoldToTalkButton");

    /// <summary>
    ///     Gets what the talk button says it is doing now, which is its accessible name: an icon has no text.
    /// </summary>
    public string? TalkLabel => AutomationProperties.GetName(TalkButton);

    /// <summary>
    ///     Gets the send button.
    /// </summary>
    public Button SendButton => Find<Button>("ChatSendButton");

    /// <summary>
    ///     Gets the editing control of the message.
    /// </summary>
    public ComposerEditorView Editor => View.FindControl<ComposerEditorView>("ComposerView")!;

    /// <summary>
    ///     Gets the entries that are shown in the transcript.
    /// </summary>
    public IReadOnlyList<TranscriptEntryView> Entries => FindAll<TranscriptEntryView>("TranscriptEntry");

    /// <summary>
    ///     Gets the parts of the answer of the copilot that are shown in the transcript.
    /// </summary>
    public IReadOnlyList<AssistantReplyView> Replies => FindAll<AssistantReplyView>("AssistantReply");

    /// <summary>
    ///     Gets the lines about tools that are shown in the transcript.
    /// </summary>
    public IReadOnlyList<ToolActivityRowView> ToolRows => FindAll<ToolActivityRowView>("ToolActivityRow");

    /// <summary>
    ///     Gets the notices that are shown in the transcript.
    /// </summary>
    public IReadOnlyList<NoticeRowView> Notices => FindAll<NoticeRowView>("NoticeRow");

    /// <summary>
    ///     Gets the restored messages that are shown in the transcript.
    /// </summary>
    public IReadOnlyList<RestoredEntryView> RestoredEntries => FindAll<RestoredEntryView>("RestoredEntry");

    /// <summary>
    ///     Gets the button of the header that opens the menu of more conversation actions.
    /// </summary>
    public Button OverflowButton => Find<Button>("ChatOverflowButton");

    /// <summary>
    ///     Gets the item of the overflow menu that clears the chat history. The menu is a flyout, so its items are not in
    ///     the visual tree until it is open; they are found in the flyout itself.
    /// </summary>
    public MenuItem ClearHistoryItem =>
        ((MenuFlyout)OverflowButton.Flyout!).Items.OfType<MenuItem>().Single(item => AutomationProperties.GetAutomationId(item) == "ChatClearHistoryButton");

    /// <summary>
    ///     Opens the overflow menu, as the user does with its button, and chooses Clear chat history.
    /// </summary>
    public void ClearHistory()
    {
        var flyout = OverflowButton.Flyout!;
        flyout.ShowAt(OverflowButton);
        Layout();
        Assert.True(flyout.IsOpen);
        Assert.True(ClearHistoryItem.Command!.CanExecute(null));
        ClearHistoryItem.Command.Execute(null);
        flyout.Hide();
        Layout();
    }

    /// <summary>
    ///     Gets the stop button.
    /// </summary>
    public Button StopButton => Find<Button>("ChatStopButton");

    /// <summary>
    ///     Gets the row with the indicator that the copilot works and the stop button.
    /// </summary>
    public Grid BusyRow => Find<Grid>("ChatBusyIndicator");

    /// <summary>
    ///     Gets the banner of the readiness of the copilot.
    /// </summary>
    public Border Banner => Find<Border>("ChatReadinessBanner");

    /// <summary>
    ///     Gets the words of a transcript entry as the text block shows them, without the chips.
    /// </summary>
    /// <param name="entry">The entry.</param>
    /// <returns>The words.</returns>
    public static string TextOf(TranscriptEntryView entry) =>
        string.Concat(entry.TextBlocks.SelectMany(block => block.Inlines!.OfType<Avalonia.Controls.Documents.Run>()).Select(run => run.Text));

    /// <summary>
    ///     Finds a control by its automation identifier.
    /// </summary>
    /// <typeparam name="T">The type of the control.</typeparam>
    /// <param name="automationId">The identifier.</param>
    /// <returns>The control.</returns>
    public T Find<T>(string automationId)
        where T : Control => SettingsUiHelpers.Find<T>(Window, automationId);

    /// <summary>
    ///     Finds all controls with an automation identifier.
    /// </summary>
    /// <typeparam name="T">The type of the controls.</typeparam>
    /// <param name="automationId">The identifier.</param>
    /// <returns>The controls.</returns>
    public IReadOnlyList<T> FindAll<T>(string automationId)
        where T : Control =>
        [.. Window.GetVisualDescendants().OfType<T>().Where(control => AutomationProperties.GetAutomationId(control) == automationId)];

    /// <summary>
    ///     Makes the window lay out and run the work that was queued, so that what is shown is current.
    /// </summary>
    public void Layout()
    {
        Window.UpdateLayout();
        Dispatcher.UIThread.RunJobs();
        Window.UpdateLayout();
    }

    /// <summary>
    ///     Waits, while running the work queued on the window, until a condition holds or two seconds have passed.
    /// </summary>
    /// <param name="condition">The condition.</param>
    /// <returns><see langword="true" /> when the condition held in time.</returns>
    public bool WaitFor(Func<bool> condition)
    {
        var deadline = DateTime.UtcNow.AddSeconds(2);
        while (!condition() && DateTime.UtcNow < deadline)
        {
            Dispatcher.UIThread.RunJobs();
            Thread.Sleep(10);
        }

        Layout();
        return condition();
    }

    /// <summary>
    ///     Types text into the message as the keyboard delivers it.
    /// </summary>
    /// <param name="text">The text.</param>
    public void Type(string text)
    {
        Editor.FocusEditor();
        Window.KeyTextInput(text);
        Layout();
    }

    /// <summary>
    ///     Returns the point of a control, in window coordinates.
    /// </summary>
    /// <param name="control">The control.</param>
    /// <returns>The point at the center of the control.</returns>
    public Point CenterOf(Control control) =>
        control.TranslatePoint(new Point(control.Bounds.Width / 2, control.Bounds.Height / 2), Window)
        ?? throw new InvalidOperationException("The control is not part of the window.");

    /// <summary>
    ///     Presses the left mouse button on a control and keeps it down.
    /// </summary>
    /// <param name="control">The control.</param>
    public void PressOn(Control control)
    {
        Window.MouseDown(CenterOf(control), MouseButton.Left);
        Layout();
    }

    /// <summary>
    ///     Releases the left mouse button at a point.
    /// </summary>
    /// <param name="point">The point in window coordinates.</param>
    public void ReleaseAt(Point point)
    {
        Window.MouseUp(point, MouseButton.Left);
        Layout();
    }

    /// <summary>
    ///     Clicks the send button and waits for the message to be sent.
    /// </summary>
    public void ClickSend()
    {
        var before = Chat.Transcript.Count;
        SettingsUiHelpers.Click(Window, SendButton);
        WaitFor(() => Chat.Transcript.Count > before);
    }

    /// <summary>
    ///     Closes the window and removes the temporary folder.
    /// </summary>
    public void Dispose()
    {
        Window.Close();
        Chat.Dispose();
        Annotation.Dispose();
        Composer.Dispose();

        // A preview that is still being decoded in the background can hold a file for a moment, so deleting retries
        for (var attempt = 0; attempt < 40 && Directory.Exists(Folder); attempt++)
        {
            try
            {
                Directory.Delete(Folder, recursive: true);
            }
            catch (IOException)
            {
                Thread.Sleep(50);
            }
        }
    }
}

/// <summary>
///     Makes pictures for the headless tests of the chat.
/// </summary>
internal static class AnnotationUiPictures
{
    /// <summary>
    ///     Makes a picture that is one flat color.
    /// </summary>
    /// <returns>The picture.</returns>
    public static RenderedImage Picture()
    {
        var pixels = new byte[200 * 100 * 4];
        Array.Fill(pixels, (byte)255);
        return new RenderedImage(@"C:\project\flow.mmd", "mermaid", 200, 100, pixels, null, "file-hash");
    }
}
