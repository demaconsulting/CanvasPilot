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

using DemaConsulting.CanvasPilot.AgentSubsystem;
using DemaConsulting.CanvasPilot.AnnotationSubsystem;
using DemaConsulting.CanvasPilot.ChatSubsystem;
using DemaConsulting.CanvasPilot.ComposerSubsystem;
using DemaConsulting.CanvasPilot.PayloadSubsystem;
using DemaConsulting.CanvasPilot.PreviewSubsystem;
using DemaConsulting.CanvasPilot.ProjectSubsystem;

namespace DemaConsulting.CanvasPilot.Tests;

/// <summary>
///     Subsystem tests for the Chat subsystem with the real units around it: a picture drawn by the real render service,
///     marks drawn on it by the real annotation layer, a file dropped into the real composer, the real payload builder,
///     and the real chat panel and payload inspector, over a temporary project. Only dictation is a fake, because it needs
///     a model and a microphone.
/// </summary>
public sealed class ChatSubsystemTests : IDisposable
{
    /// <summary>
    ///     Temporary folder that holds the project and a folder outside it.
    /// </summary>
    private readonly string _root = Directory.CreateTempSubdirectory("canvaspilot-chat-tests-").FullName;

    /// <summary>
    ///     The folder of the project.
    /// </summary>
    private readonly string _project;

    /// <summary>
    ///     The service that draws the files, the real one.
    /// </summary>
    private readonly ImageRenderService _renderer = new();

    /// <summary>
    ///     Initializes a new instance of the <see cref="ChatSubsystemTests" /> class.
    /// </summary>
    public ChatSubsystemTests()
    {
        _project = Directory.CreateDirectory(Path.Combine(_root, "project")).FullName;
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    /// <summary>
    ///     Test that a message with typed words, a mark around a node of a drawn diagram, and a dropped file is sent through
    ///     the panel and that the transcript, the files in the project, and the inspector all agree with the draft.
    /// </summary>
    [Fact]
    public async Task ChatSubsystem_SendMessageWithMarkAndAttachment_TranscriptAndPayloadMatchDraft()
    {
        // Arrange: a drawn diagram shown to the annotation layer, a file outside the project, and the real units
        var token = TestContext.Current.CancellationToken;
        var picture = await RenderAsync("flow.mmd", "flowchart LR\n  A[Start] --> B[End]\n");
        var source = new FakeRenderedImageSource { CurrentImage = picture };
        var outside = Directory.CreateDirectory(Path.Combine(_root, "outside")).FullName;
        var notes = Path.Combine(outside, "notes.txt");
        await File.WriteAllTextAsync(notes, "some notes", token);
        var marks = new MarkRegistry();
        var markImages = new MarkImageComposer(_renderer);
        using var composer = new ComposerViewModel(new PayloadBuilder(markImages), marks, new FakeDictationService());
        using var annotation = new AnnotationOverlayViewModel(marks, source, _ => { });
        using var panel = new ChatPanelViewModel(composer, marks, annotation, markImages);
        composer.SetProject(_project);

        // Act: type, draw a rectangle around the node 'Start', drop the file, and send
        composer.InsertTyped("Make this ");
        var box = picture.Positions.First(position => position is { Kind: "node", Text: "Start" }).Box!.Value;
        annotation.SelectTool(AnnotationTool.Rectangle);
        annotation.BeginStroke(new RenderPoint(box.X - 3, box.Y - 3));
        annotation.EndStroke(new RenderPoint(box.X + box.Width + 3, box.Y + box.Height + 3));
        annotation.SkipNote();
        composer.InsertTyped(" like ");
        composer.HandleDrop(new DroppedContent { Paths = [notes] });
        await panel.SendCommand.ExecuteAsync(null);

        // Assert: the transcript holds the message in the order it was written, with the mark chip and the file chip
        var entry = Assert.Single(panel.SentEntries);
        Assert.Equal(["Make this ", "#", " like ", "#"], entry.Items.Select(item => item.Text ?? "#"));
        Assert.IsType<MarkChip>(entry.Items[1].Chip);
        Assert.IsType<FileChip>(entry.Items[3].Chip);
        Assert.Equal("1 mark, 1 attachment", entry.Summary);

        // Assert: the draft, the marks, and the layer are empty, and the file was copied into the project
        Assert.Equal(string.Empty, composer.Document.FlatText);
        Assert.Empty(marks.Marks);
        Assert.Empty(annotation.VisibleMarks);
        Assert.Equal("some notes", await File.ReadAllTextAsync(Path.Combine(_project, "attachments", "notes.txt"), token));

        // Assert: the payload and the inspector hold what was drafted: the text in order, the attachment's path in the
        // project, the marked picture with its mark, and the node under the mark
        var payload = Assert.IsType<MessagePayload>(entry.Message.Payload);
        Assert.Same(payload, entry.Payload);
        Assert.Equal("Make this [Mark 1] like [Attachment 1: notes.txt]", payload.Text);
        Assert.Equal("attachments/notes.txt", Assert.Single(payload.Attachments).ProjectRelativePath);
        var image = Assert.Single(payload.Images);
        Assert.Equal(picture.ContentHash, image.ContentHash);
        var mark = Assert.Single(image.Marks);
        Assert.Contains(mark.SourceElements, element => element.Text == "Start");
        using var inspector = new PayloadInspectorViewModel(payload);
        Assert.Equal(PayloadDescriber.Describe(payload), inspector.Description);
        Assert.Contains("[Mark 1]", inspector.Description);
        Assert.Contains("attachments/notes.txt", inspector.Description);
        Assert.Equal(2, inspector.Pictures.Count);
        Assert.Equal(image.AnnotatedPng.ToArray().Length, inspector.Pictures[0].ByteCount);
        Assert.Equal(mark.CropPng.ToArray().Length, inspector.Pictures[1].ByteCount);
    }

    /// <summary>
    ///     Test that words held to talk are added to the message as one dictated block and reach the payload of the sent
    ///     message with their origin, so that the assistant can tell them from typed words.
    /// </summary>
    [Fact]
    public async Task ChatSubsystem_HoldToTalk_DictatedWords_AreKeptAsDictatedInTheSentPayload()
    {
        // Arrange: the real units with a dictation service that hears a sentence
        var marks = new MarkRegistry();
        var markImages = new MarkImageComposer(_renderer);
        var dictation = new FakeDictationService();
        dictation.Session.FinalText = "turn the arrow red";
        using var composer = new ComposerViewModel(new PayloadBuilder(markImages), marks, dictation);
        using var annotation = new AnnotationOverlayViewModel(marks, new FakeRenderedImageSource(), _ => { });
        using var panel = new ChatPanelViewModel(composer, marks, annotation, markImages, dictation);
        composer.SetProject(_project);
        composer.InsertTyped("Please ");

        // Act: hold the talk button, speak, let go, and send
        await panel.BeginPushToTalkAsync();
        dictation.Session.Say("turn the");
        var live = composer.LivePartialText;
        await panel.EndPushToTalkAsync();
        await panel.SendCommand.ExecuteAsync(null);

        // Assert: the live words were shown, and the payload keeps typed and dictated words apart
        Assert.Equal("turn the", live);
        var payload = Assert.IsType<MessagePayload>(Assert.Single(panel.SentEntries).Message.Payload);
        Assert.Equal("Please turn the arrow red", payload.Text);
        Assert.Equal(
            [(TextOrigin.Typed, "Please "), (TextOrigin.Dictated, "turn the arrow red")],
            payload.Segments.OfType<PayloadTextPart>().Select(part => (part.Origin, part.Text)));
    }

    /// <summary>
    ///     Test that a message that cannot be sent because an attachment is gone keeps the draft, its chips, and its
    ///     marks, adds nothing to the transcript, and says which attachment is the problem.
    /// </summary>
    [Fact]
    public async Task ChatSubsystem_SendMessage_AttachmentCannotBeStaged_KeepsDraftAndTranscriptUnchanged()
    {
        // Arrange: a message with a file that does not exist any more when the message is sent
        var token = TestContext.Current.CancellationToken;
        var outside = Directory.CreateDirectory(Path.Combine(_root, "outside")).FullName;
        var vanishing = Path.Combine(outside, "vanishing.txt");
        await File.WriteAllTextAsync(vanishing, "soon gone", token);
        var marks = new MarkRegistry();
        var markImages = new MarkImageComposer(_renderer);
        using var composer = new ComposerViewModel(new PayloadBuilder(markImages), marks, new FakeDictationService());
        using var annotation = new AnnotationOverlayViewModel(marks, new FakeRenderedImageSource(), _ => { });
        using var panel = new ChatPanelViewModel(composer, marks, annotation, markImages);
        composer.SetProject(_project);
        composer.InsertTyped("see ");
        composer.HandleDrop(new DroppedContent { Paths = [vanishing] });
        File.Delete(vanishing);

        // Act: send
        await panel.SendCommand.ExecuteAsync(null);

        // Assert: nothing was sent, the draft keeps its text and chip, and the status text names the file
        Assert.Empty(panel.Transcript);
        Assert.Equal("see \uFFFC", composer.Document.FlatText);
        Assert.Contains("vanishing.txt", composer.StatusText);
    }

    /// <summary>
    ///     Test that a message sent through the real composer is given to the copilot by the panel with its real payload, that the
    ///     answer streams into rows after the entry of the message with a tool row that says the file was created, and that
    ///     the composer cannot send while the turn runs, but can be written to, and sends again afterwards.
    /// </summary>
    [Fact]
    public async Task ChatSubsystem_ConverseWithCopilot_SentMessageGetsStreamedAnswerAndToolRows_ComposerWaitsMeanwhile()
    {
        // Arrange: the real units, a copilot that answers when the test says so, and the wiring that the panel will have
        var token = TestContext.Current.CancellationToken;
        var marks = new MarkRegistry();
        var markImages = new MarkImageComposer(_renderer);
        using var composer = new ComposerViewModel(new PayloadBuilder(markImages), marks, new FakeDictationService());
        using var annotation = new AnnotationOverlayViewModel(marks, new FakeRenderedImageSource(), _ => { });
        using var agent = new ControlledAgentService();
        using var panel = new ChatPanelViewModel(composer, marks, annotation, markImages, agent: agent, clock: new ManualTimeProvider());
        composer.SetProject(_project);
        composer.InsertTyped("Draw a box");

        // Act: send, and let the copilot write a file and answer
        await panel.SendCommand.ExecuteAsync(null);
        var duringTurn = (composer.CanSend, panel.IsCopilotBusy);
        composer.InsertTyped("And a circle");
        var canSendWhileBusy = composer.CanSend;
        agent.Emit(
            new AgentEvent.TextDelta("I will draw it."),
            new AgentEvent.ToolStarted("c1", "project_write", "path=box.svg, content=(80 characters)"),
            new AgentEvent.FileWritten("box.svg", false),
            new AgentEvent.ToolFinished("c1", "project_write", true, "File 'box.svg' created.", false),
            new AgentEvent.TextDelta("Done."),
            new AgentEvent.Completed());
        await panel.WaitForTurnAsync().WaitAsync(TimeSpan.FromSeconds(10), token);

        // Assert: the copilot got the payload of the message, and the rows follow the entry in the order of events
        var payload = Assert.IsType<MessagePayload>(Assert.Single(panel.SentEntries).Message.Payload);
        Assert.Same(payload, Assert.Single(agent.Requests).Payload);
        var copilotRows = panel.Transcript.Skip(1).ToList();
        Assert.Equal(
            [TranscriptRowKind.User, TranscriptRowKind.Assistant, TranscriptRowKind.Tool, TranscriptRowKind.Assistant],
            panel.Transcript.Select(row => row.Kind));
        Assert.Equal("I will draw it.", Assert.IsType<AssistantReplyViewModel>(copilotRows[0]).Text);
        Assert.Equal("Created `box.svg`", Assert.IsType<ToolActivityRowViewModel>(copilotRows[1]).Summary);
        Assert.True(Assert.IsType<AssistantReplyViewModel>(copilotRows[2]).IsComplete);

        // Assert: the composer could not send while the copilot worked, kept what was written meanwhile, and sends now
        Assert.Equal((false, true), duringTurn);
        Assert.False(canSendWhileBusy);
        Assert.False(panel.IsCopilotBusy);
        Assert.Equal("And a circle", composer.Document.FlatText);
        Assert.True(composer.CanSend);
    }

    /// <summary>
    ///     Test that stopping a turn through the panel keeps the text that was written and marks the reply stopped, that the
    ///     composer can send again at once, and that a turn that fails ends with an error notice that offers the settings
    ///     when they can help, and that the notice opens them through the panel.
    /// </summary>
    [Fact]
    public async Task ChatSubsystem_StopCopilotTurn_KeepsTextFreesTheComposerAndFailureHasRemedy()
    {
        // Arrange: a copilot that streams, and the real panel over the real composer
        var token = TestContext.Current.CancellationToken;
        var marks = new MarkRegistry();
        var markImages = new MarkImageComposer(_renderer);
        using var composer = new ComposerViewModel(new PayloadBuilder(markImages), marks, new FakeDictationService());
        using var annotation = new AnnotationOverlayViewModel(marks, new FakeRenderedImageSource(), _ => { });
        using var agent = new ControlledAgentService();
        using var panel = new ChatPanelViewModel(composer, marks, annotation, markImages, agent: agent, clock: new ManualTimeProvider());
        var settingsRequests = 0;
        panel.OpenSettingsRequested += (_, _) => settingsRequests++;
        composer.SetProject(_project);
        composer.InsertTyped("Draw a box");
        await composer.SendCommand.ExecuteAsync(null);
        agent.Emit(new AgentEvent.TextDelta("Let me think about the"));

        // Act: stop
        composer.InsertTyped("Never mind");
        var blockedBeforeStop = composer.IsSendBlocked;
        Assert.True(panel.StopCommand.CanExecute(null));
        panel.StopCommand.Execute(null);
        await panel.WaitForTurnAsync().WaitAsync(TimeSpan.FromSeconds(10), token);

        // Assert: the partial text is kept in a stopped reply and the composer can send what was written meanwhile
        var reply = Assert.IsType<AssistantReplyViewModel>(Assert.Single(panel.Transcript.OfType<AssistantReplyViewModel>()));
        Assert.Equal(("Let me think about the", AssistantReplyState.Stopped), (reply.Text, reply.State));
        Assert.True(blockedBeforeStop);
        Assert.False(composer.IsSendBlocked);
        Assert.True(composer.CanSend);
        Assert.False(panel.StopCommand.CanExecute(null));

        // Act: send the next message and let the provider fail
        await composer.SendCommand.ExecuteAsync(null);
        agent.Emit(new AgentEvent.Failed(AgentFailureKind.ModelNotFound, "The model 'missing-model' was not found."));
        await panel.WaitForTurnAsync().WaitAsync(TimeSpan.FromSeconds(10), token);

        // Assert: an error notice with the message, the remedy, and the offer of the settings, and the composer is free
        var notice = Assert.IsType<NoticeRowViewModel>(panel.Transcript[^1]);
        Assert.Equal(("The model 'missing-model' was not found.", true, true), (notice.Message, notice.HasRemedy, notice.CanOpenSettings));
        Assert.False(composer.IsSendBlocked);

        // Act: choose "Open Settings" on the notice
        notice.OpenSettingsCommand.Execute(null);

        // Assert: the shell was asked to open the settings
        Assert.Equal(1, settingsRequests);
    }

    /// <summary>
    ///     Test that the real panel over the real composer asks the copilot whether the provider can be used and shows the
    ///     sentence that says what to do, including the sentence of GitHub Copilot that is not signed in, that a warning does not block
    ///     sending, that the sentence goes away when the provider is ready, and that the settings are requested from the
    ///     banner.
    /// </summary>
    [Fact]
    public async Task ChatSubsystem_ReportProviderReadiness_CopilotNotSignedIn_BannerSaysWhatToDoAndSettingsAreRequested()
    {
        // Arrange: the Copilot that is not signed in is configured, and the real panel over the real composer
        var marks = new MarkRegistry();
        var markImages = new MarkImageComposer(_renderer);
        using var composer = new ComposerViewModel(new PayloadBuilder(markImages), marks, new FakeDictationService());
        using var annotation = new AnnotationOverlayViewModel(marks, new FakeRenderedImageSource(), _ => { });
        using var agent = new ControlledAgentService
        {
            Readiness = new AgentReadinessReport(
                AgentReadinessStatus.CopilotNotSignedIn,
                CopilotRuntimeStarter.NotSignedInMessage),
        };
        using var panel = new ChatPanelViewModel(composer, marks, annotation, markImages, agent: agent, clock: new ManualTimeProvider());
        var settingsRequests = 0;
        panel.OpenSettingsRequested += (_, _) => settingsRequests++;
        composer.SetProject(_project);

        // Act: the panel is shown and checks, and the user chooses "Open Settings" in the banner
        await panel.RefreshReadinessAsync();
        panel.OpenSettingsCommand.Execute(null);

        // Assert: the banner says what to do, and the shell was asked to open the settings
        Assert.True(panel.HasReadinessIssue);
        Assert.Equal(CopilotRuntimeStarter.NotSignedInMessage, panel.ReadinessMessage);
        Assert.Equal(1, settingsRequests);

        // Act: the settings choose a provider with a small context window, which only warns
        agent.Readiness = new AgentReadinessReport(AgentReadinessStatus.OllamaContextTooSmall, "The context window is below 16,384.");
        await panel.RefreshReadinessAsync();
        composer.InsertTyped("Draw a box");

        // Assert: the warning is shown and sending is not blocked
        Assert.True(panel.HasReadinessIssue);
        Assert.True(panel.CanSend);

        // Act: the provider becomes ready
        agent.Readiness = new AgentReadinessReport(AgentReadinessStatus.Ready, "Ready.");
        await panel.RefreshReadinessAsync();

        // Assert: the banner is gone
        Assert.False(panel.HasReadinessIssue);
        Assert.Equal(string.Empty, panel.ReadinessMessage);
    }

    /// <summary>
    ///     Test that a full scripted turn in a project made by the real project service saves its words on this computer
    ///     outside the project, that a panel made later in the same project shows them as text and gives them to the copilot,
    ///     that Clear deletes them and resets the copilot, and that through all of it the project folder holds only the
    ///     files that were meant to be there: the project file, its data folder (empty), and the file that the copilot
    ///     created.
    /// </summary>
    [Fact]
    public async Task ChatSubsystem_KeepChatHistoryOnThisComputer_TurnSavedRestoredAndClearedOutsideTheProject()
    {
        // Arrange: a project made by the real project service, and the real store in a folder of the user's computer
        var token = TestContext.Current.CancellationToken;
        var parent = Directory.CreateDirectory(Path.Combine(_root, "parent")).FullName;
        var projects = new ProjectService();
        var created = await projects.CreateAsync(parent, "Demo", token);
        var info = Assert.IsType<ProjectInfo>(created.Project);
        var historyRoot = Path.Combine(_root, "local", "history");
        var store = new ChatHistoryStore(historyRoot);
        var expectedInProject = new[] { ".canvaspilot", "Demo.canvaspilot", "box.svg" };

        // Act: a scripted turn in which the copilot creates a file, in a first panel
        var marks = new MarkRegistry();
        var markImages = new MarkImageComposer(_renderer);
        using (var composer = new ComposerViewModel(new PayloadBuilder(markImages), marks, new FakeDictationService()))
        using (var annotation = new AnnotationOverlayViewModel(marks, new FakeRenderedImageSource(), _ => { }))
        using (var agent = new ControlledAgentService())
        {
            composer.SetProject(info.FolderPath);
            using var panel = new ChatPanelViewModel(composer, marks, annotation, markImages, agent: agent, clock: new ManualTimeProvider(), history: store, projects: projects);
            composer.InsertTyped("Draw a box");
            await panel.SendCommand.ExecuteAsync(null);
            await File.WriteAllTextAsync(Path.Combine(info.FolderPath, "box.svg"), "<svg xmlns=\"http://www.w3.org/2000/svg\" />", token);
            agent.Emit(
                new AgentEvent.TextDelta("Done."),
                new AgentEvent.ToolStarted("c1", "project_write", "path=box.svg"),
                new AgentEvent.FileWritten("box.svg", false),
                new AgentEvent.ToolFinished("c1", "project_write", true, "File 'box.svg' created.", false),
                new AgentEvent.Completed());
            await panel.WaitForTurnAsync().WaitAsync(TimeSpan.FromSeconds(10), token);
        }

        // Assert: the words are saved below the root with the sidecar, and the project holds only the expected files
        var folder = Path.Combine(historyRoot, ProjectHistoryKey.Compute(info.FilePath));
        Assert.Equal(["history.json", "project.json"], Directory.GetFiles(folder).Select(Path.GetFileName).Order(StringComparer.Ordinal));
        Assert.Equal(expectedInProject, ProjectEntries(info.FolderPath));
        Assert.Empty(Directory.GetFileSystemEntries(Path.Combine(info.FolderPath, ".canvaspilot")));
        Assert.Equal([ChatHistoryRowKind.User, ChatHistoryRowKind.Assistant, ChatHistoryRowKind.Tool], store.Load(info).Select(row => row.Kind));

        // Act: the application is started again: a second panel in the same project, and a message
        var marks2 = new MarkRegistry();
        using var composer2 = new ComposerViewModel(new PayloadBuilder(markImages), marks2, new FakeDictationService());
        using var annotation2 = new AnnotationOverlayViewModel(marks2, new FakeRenderedImageSource(), _ => { });
        using var agent2 = new ControlledAgentService();
        composer2.SetProject(info.FolderPath);
        using var panel2 = new ChatPanelViewModel(composer2, marks2, annotation2, markImages, agent: agent2, clock: new ManualTimeProvider(), history: store, projects: projects);

        // Assert: the earlier conversation is there as text, and it is not a message of this session
        Assert.Equal(
            [typeof(RestoredEntryViewModel), typeof(AssistantReplyViewModel), typeof(ToolActivityRowViewModel)],
            panel2.Transcript.Select(row => row.GetType()));
        Assert.Empty(panel2.SentEntries);
        composer2.InsertTyped("Now a circle");
        await panel2.SendCommand.ExecuteAsync(null);
        agent2.Emit(new AgentEvent.Completed());
        await panel2.WaitForTurnAsync().WaitAsync(TimeSpan.FromSeconds(10), token);

        // Assert: the copilot was given the saved words with the message, and the project is as it was
        var restored = Assert.Single(agent2.Requests).RestoredHistory;
        Assert.Equal(new AgentHistoryTurn(AgentHistoryRole.User, "Draw a box"), restored[0]);
        Assert.Equal(new AgentHistoryTurn(AgentHistoryRole.Assistant, "Done."), restored[1]);
        Assert.Equal(expectedInProject, ProjectEntries(info.FolderPath));

        // Act: Clear chat history
        panel2.ClearHistoryCommand.Execute(null);

        // Assert: the history is gone from the computer, the copilot forgot the conversation, and the project is as it was
        Assert.False(Directory.Exists(folder));
        Assert.Empty(store.Load(info));
        Assert.Equal(1, agent2.ResetCount);
        Assert.Equal(expectedInProject, ProjectEntries(info.FolderPath));
        Assert.Empty(Directory.EnumerateFiles(info.FolderPath, "history.json*", SearchOption.AllDirectories));
    }

    /// <summary>
    ///     Lists the names of what is directly in a project folder, in order.
    /// </summary>
    /// <param name="folder">The folder of the project.</param>
    /// <returns>The names.</returns>
    private static string[] ProjectEntries(string folder) =>
        [.. Directory.GetFileSystemEntries(folder).Select(entry => Path.GetFileName(entry)).Order(StringComparer.Ordinal)];

    /// <summary>
    ///     Draws a file in the project and returns its picture.
    /// </summary>
    /// <param name="name">The name of the file.</param>
    /// <param name="content">The content of the file.</param>
    /// <returns>The picture.</returns>
    private async Task<RenderedImage> RenderAsync(string name, string content)
    {
        var token = TestContext.Current.CancellationToken;
        var path = Path.Combine(_project, name);
        await File.WriteAllTextAsync(path, content, token);
        var result = await _renderer.RenderAsync(path, _project, token);
        Assert.True(result.Succeeded, result.Error?.Message);
        return result.Image!;
    }
}
