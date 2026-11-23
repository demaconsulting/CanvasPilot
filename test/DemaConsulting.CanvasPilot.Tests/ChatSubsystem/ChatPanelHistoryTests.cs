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
using NSubstitute;

namespace DemaConsulting.CanvasPilot.Tests;

/// <summary>
///     Unit tests for the part of <see cref="ChatPanelViewModel" /> that keeps the conversation on this computer: the
///     words of a turn are saved when it has ended, a project that is opened shows its saved conversation as text rows and
///     gives it to the copilot, the conversation of another project is another one, and <c>ClearHistoryCommand</c>
///     deletes what was saved, empties the transcript, and resets the copilot. The mapping between the transcript and
///     the saved rows, and the row of a restored message, are tested here too. The panel, the composer, and the store
///     are the real units over temporary folders; the copilot is a controlled fake.
/// </summary>
public sealed class ChatPanelHistoryTests : IDisposable
{
    /// <summary>
    ///     How long a test waits for a turn that should end.
    /// </summary>
    private static readonly TimeSpan TurnTimeout = TimeSpan.FromSeconds(10);

    /// <summary>
    ///     Temporary folder that holds the projects and the history.
    /// </summary>
    private readonly string _root = Directory.CreateTempSubdirectory("canvaspilot-chat-history-panel-tests-").FullName;

    /// <summary>
    ///     The rigs that a test made, disposed with the test.
    /// </summary>
    private readonly List<Rig> _rigs = [];

    /// <summary>
    ///     The store that keeps the history of the tests.
    /// </summary>
    private readonly ChatHistoryStore _store;

    /// <summary>
    ///     Initializes a new instance of the <see cref="ChatPanelHistoryTests" /> class.
    /// </summary>
    public ChatPanelHistoryTests()
    {
        _store = new ChatHistoryStore(Path.Combine(_root, "local", "history"));
    }

    /// <inheritdoc />
    public void Dispose()
    {
        foreach (var rig in _rigs)
        {
            rig.Dispose();
        }

        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    /// <summary>
    ///     Test that a completed turn saves the message, the reply, and the tool line as words in the history of the
    ///     project outside the project folder, and that a notice is not saved.
    /// </summary>
    [Fact]
    public async Task ChatPanelViewModel_TurnCompleted_SavesTextRowsOutsideTheProject()
    {
        // Arrange: a project with an empty history
        var project = NewProject("Demo");
        var rig = NewRig(project);

        // Act: a message that the copilot answers with text and a tool, and a notice of the panel
        await rig.SendAsync("Draw a box");
        rig.Agent!.Emit(
            new AgentEvent.TextDelta("Here is the box."),
            new AgentEvent.ToolStarted("c1", "project_write", "path=box.svg"),
            new AgentEvent.FileWritten("box.svg", Replaced: false),
            new AgentEvent.ToolFinished("c1", "project_write", true, "File 'box.svg' created.", false),
            new AgentEvent.Completed());
        await rig.WaitForTurnAsync();

        // Assert: the saved rows are words in the order of the transcript, and nothing is in the project
        var saved = _store.Load(project);
        Assert.Equal([ChatHistoryRowKind.User, ChatHistoryRowKind.Assistant, ChatHistoryRowKind.Tool], saved.Select(row => row.Kind));
        Assert.Equal("Draw a box", saved[0].Text);
        Assert.Equal("Here is the box.", saved[1].Text);
        Assert.Equal(ChatHistoryReplyState.Completed, saved[1].Reply);
        Assert.Equal("project_write", saved[2].Tool);
        Assert.Contains("box.svg", saved[2].Text, StringComparison.Ordinal);
        Assert.Empty(Directory.GetFileSystemEntries(project.FolderPath));
        Assert.True(File.Exists(_store.HistoryFilePath(project)));
    }

    /// <summary>
    ///     Test that a turn that the user stopped is saved with its reply marked as stopped.
    /// </summary>
    [Fact]
    public async Task ChatPanelViewModel_TurnStopped_SavesTheStoppedReply()
    {
        // Arrange: a turn that has written some text
        var rig = NewRig(NewProject("Demo"));
        await rig.SendAsync("Draw a box");
        rig.Agent!.Emit(new AgentEvent.TextDelta("Let me think about"));
        await rig.WaitForRowAsync<AssistantReplyViewModel>();

        // Act: stop it
        rig.Panel.Stop();
        await rig.WaitForTurnAsync();

        // Assert: the words so far are saved as a stopped reply
        var saved = _store.Load(rig.Project!);
        Assert.Equal([ChatHistoryRowKind.User, ChatHistoryRowKind.Assistant], saved.Select(row => row.Kind));
        Assert.Equal("Let me think about", saved[1].Text);
        Assert.Equal(ChatHistoryReplyState.Stopped, saved[1].Reply);
    }

    /// <summary>
    ///     Test that a failed turn saves the message and what was written, but not the notice that says why it failed.
    /// </summary>
    [Fact]
    public async Task ChatPanelViewModel_TurnFailed_SavesTheMessageButNotTheNotice()
    {
        // Arrange and act: a message that fails at once
        var rig = NewRig(NewProject("Demo"));
        await rig.SendAsync("Draw a box");
        rig.Agent!.Emit(new AgentEvent.Failed(AgentFailureKind.ModelNotFound, "The model was not found."));
        await rig.WaitForTurnAsync();

        // Assert: the transcript has the notice, and the history has only the message
        Assert.Contains(rig.Panel.Transcript, row => row is NoticeRowViewModel);
        var saved = Assert.Single(_store.Load(rig.Project!));
        Assert.Equal(ChatHistoryRowKind.User, saved.Kind);
    }

    /// <summary>
    ///     Test that a project that is opened shows what was said before as text rows: the message of the user is a
    ///     restored row with its words and no payload or pictures, the reply and the tool line are the usual rows, and
    ///     none of them is a message that was sent in this session.
    /// </summary>
    [Fact]
    public void ChatPanelViewModel_OpenProjectWithHistory_ShowsSavedConversationAsTextRows()
    {
        // Arrange: a saved conversation
        var project = NewProject("Demo");
        _store.Save(project, SavedConversation());
        var rig = NewRig(project: null);

        // Act: open the project
        rig.Open(project);

        // Assert: four rows of the right kinds, in order
        var transcript = rig.Panel.Transcript;
        Assert.Equal(
            [TranscriptRowKind.User, TranscriptRowKind.Assistant, TranscriptRowKind.Tool, TranscriptRowKind.Assistant],
            transcript.Select(row => row.Kind));
        var user = Assert.IsType<RestoredEntryViewModel>(transcript[0]);
        Assert.Equal("Why is [Mark 1] red? [Attachment: logo.png]", user.Text);
        Assert.Equal("1 mark, 1 attachment", user.Summary);
        Assert.Equal("Mark 1: check this\nAttachment: attachments/logo.png", user.DetailsText);
        Assert.True(user.HasDetails);
        Assert.Equal("Because of the fill.", Assert.IsType<AssistantReplyViewModel>(transcript[1]).Text);
        Assert.Equal(AssistantReplyState.Completed, Assert.IsType<AssistantReplyViewModel>(transcript[1]).State);
        var tool = Assert.IsType<ToolActivityRowViewModel>(transcript[2]);
        Assert.Equal(("project_write", false, false), (tool.ToolName, tool.Succeeded, tool.IsRunning));
        Assert.Equal(AssistantReplyState.Stopped, Assert.IsType<AssistantReplyViewModel>(transcript[3]).State);
        Assert.Empty(rig.Panel.SentEntries);
        Assert.True(rig.Panel.HasTranscript);
    }

    /// <summary>
    ///     Test that a project that is already open when the panel is made is restored at once.
    /// </summary>
    [Fact]
    public void ChatPanelViewModel_New_ProjectAlreadyOpen_RestoresTheConversation()
    {
        // Arrange and act: a saved conversation and a panel made in the open project
        var project = NewProject("Demo");
        _store.Save(project, SavedConversation());
        var rig = NewRig(project);

        // Assert
        Assert.Equal(4, rig.Panel.Transcript.Count);
    }

    /// <summary>
    ///     Test that the first message after a restore gives the copilot the saved words as restored history, with the
    ///     note of the mark, the attachment, and the tool line, and that the next message gives it the new words too.
    /// </summary>
    [Fact]
    public async Task ChatPanelViewModel_SendAfterRestore_GivesTheCopilotTheSavedTextAsRestoredHistory()
    {
        // Arrange: a restored conversation
        var project = NewProject("Demo");
        _store.Save(project, SavedConversation());
        var rig = NewRig(project);

        // Act: the first message after the restore, and a second one
        await rig.SendAsync("And now a circle");
        rig.Agent!.Emit(new AgentEvent.TextDelta("A circle."), new AgentEvent.Completed());
        await rig.WaitForTurnAsync();
        await rig.SendAsync("Make it blue");
        rig.Agent.Emit(new AgentEvent.Completed());
        await rig.WaitForTurnAsync();

        // Assert: the words of the saved turns, in order, then the words of the first new turn
        var first = rig.Agent.Requests[0].RestoredHistory;
        Assert.Equal(
            [
                new AgentHistoryTurn(AgentHistoryRole.User, "Why is [Mark 1] red? [Attachment: logo.png] (Mark 1: check this) (Attached: attachments/logo.png)"),
                new AgentHistoryTurn(AgentHistoryRole.Assistant, "Because of the fill."),
                new AgentHistoryTurn(AgentHistoryRole.Assistant, "[Used the tool project_write: Replaced `flow.svg` (it failed)]"),
                new AgentHistoryTurn(AgentHistoryRole.Assistant, "Stopped here [The reply was stopped.]"),
            ],
            first);
        var second = rig.Agent.Requests[1].RestoredHistory;
        Assert.Equal(6, second.Count);
        Assert.Equal(new AgentHistoryTurn(AgentHistoryRole.User, "And now a circle"), second[4]);
        Assert.Equal(new AgentHistoryTurn(AgentHistoryRole.Assistant, "A circle."), second[5]);
    }

    /// <summary>
    ///     Test that a new project has no history to give, that two projects keep their own conversations, that a project
    ///     that is closed shows nothing, and that coming back restores the conversation.
    /// </summary>
    [Fact]
    public async Task ChatPanelViewModel_ProjectChanges_EachProjectHasItsOwnConversation()
    {
        // Arrange: a conversation in the first project
        var first = NewProject("First");
        var second = NewProject("Second");
        var rig = NewRig(first);
        await rig.SendAsync("In the first");
        rig.Agent!.Emit(new AgentEvent.Completed());
        await rig.WaitForTurnAsync();

        // Act and assert: the other project starts empty and keeps its own words
        rig.Open(second);
        Assert.Empty(rig.Panel.Transcript);
        await rig.SendAsync("In the second");
        rig.Agent.Emit(new AgentEvent.Completed());
        await rig.WaitForTurnAsync();
        Assert.Empty(rig.Agent.Requests[1].RestoredHistory);
        Assert.Equal("In the second", Assert.Single(_store.Load(second)).Text);

        // Act and assert: closing shows nothing, and going back restores the first conversation
        rig.Open(null);
        Assert.Empty(rig.Panel.Transcript);
        rig.Open(first);
        Assert.Equal("In the first", Assert.IsType<RestoredEntryViewModel>(Assert.Single(rig.Panel.Transcript)).Text);
        Assert.Equal("In the first", Assert.Single(_store.Load(first)).Text);
    }

    /// <summary>
    ///     Test that a turn that is stopped because the project changed saves nothing into the history of the project that
    ///     was opened.
    /// </summary>
    [Fact]
    public async Task ChatPanelViewModel_ProjectChangesWhileTurnRuns_SavesNothingIntoTheNewProject()
    {
        // Arrange: a turn that runs in the first project
        var first = NewProject("First");
        var second = NewProject("Second");
        var rig = NewRig(first);
        await rig.SendAsync("In the first");
        rig.Agent!.Emit(new AgentEvent.TextDelta("working"));
        await rig.WaitForRowAsync<AssistantReplyViewModel>();

        // Act: another project is opened, and the turn ends
        rig.Open(second);
        await rig.WaitForTurnAsync();

        // Assert: the new project has no history and no row on the screen
        Assert.Empty(_store.Load(second));
        Assert.Empty(rig.Panel.Transcript);
        Assert.False(File.Exists(_store.HistoryFilePath(second)));
    }

    /// <summary>
    ///     Test that Clear deletes the stored history, empties the transcript but for a notice that says so, resets the
    ///     copilot, saves nothing of the notice, and starts a fresh conversation without restored history.
    /// </summary>
    [Fact]
    public async Task ChatPanelViewModel_ClearHistory_DeletesTheStoredHistoryEmptiesTheTranscriptAndResetsTheCopilot()
    {
        // Arrange: a restored conversation and one turn more
        var project = NewProject("Demo");
        _store.Save(project, SavedConversation());
        var rig = NewRig(project);
        await rig.SendAsync("More");
        rig.Agent!.Emit(new AgentEvent.Completed());
        await rig.WaitForTurnAsync();
        Assert.True(Directory.Exists(_store.FolderOf(project)));

        // Act
        rig.Panel.ClearHistoryCommand.Execute(null);

        // Assert: the folder of the history is gone, the transcript is only the notice, and the copilot was reset once
        Assert.False(Directory.Exists(_store.FolderOf(project)));
        var notice = Assert.IsType<NoticeRowViewModel>(Assert.Single(rig.Panel.Transcript));
        Assert.Equal(ChatPanelViewModel.ClearedText, notice.Message);
        Assert.Equal(NoticeSeverity.Information, notice.Severity);
        Assert.Equal(1, rig.Agent.ResetCount);
        Assert.Empty(rig.Panel.SentEntries);

        // Act: a message in the cleared conversation
        await rig.SendAsync("Fresh");
        rig.Agent.Emit(new AgentEvent.Completed());
        await rig.WaitForTurnAsync();

        // Assert: no restored history is given, and only the new words are saved
        Assert.Empty(rig.Agent.Requests[^1].RestoredHistory);
        Assert.Equal("Fresh", Assert.Single(_store.Load(project)).Text);
    }

    /// <summary>
    ///     Test that Clear while a turn runs stops it, drops what it still says, and saves nothing afterwards.
    /// </summary>
    [Fact]
    public async Task ChatPanelViewModel_ClearHistory_WhileTurnRuns_StopsItDropsItsRowsAndSavesNothing()
    {
        // Arrange: a turn that runs
        var rig = NewRig(NewProject("Demo"));
        await rig.SendAsync("Draw a box");
        rig.Agent!.Emit(new AgentEvent.TextDelta("working"));
        await rig.WaitForRowAsync<AssistantReplyViewModel>();

        // Act: clear, and let the turn end
        rig.Panel.ClearHistoryCommand.Execute(null);
        await rig.WaitForTurnAsync();

        // Assert: only the notice is shown, the turn was canceled, the composer is free, and nothing was saved
        Assert.IsType<NoticeRowViewModel>(Assert.Single(rig.Panel.Transcript));
        Assert.True(rig.Agent.LatestToken.IsCancellationRequested);
        Assert.False(rig.Panel.IsCopilotBusy);
        Assert.Empty(_store.Load(rig.Project!));
        Assert.False(Directory.Exists(_store.FolderOf(rig.Project!)));
    }

    /// <summary>
    ///     Test that Clear works for a panel that has no store and no copilot: the transcript is emptied and nothing fails.
    /// </summary>
    [Fact]
    public async Task ChatPanelViewModel_ClearHistory_WithoutStoreAndCopilot_EmptiesTheTranscript()
    {
        // Arrange: a panel with neither, and a message in its transcript
        var rig = NewRig(NewProject("Demo"), withAgent: false, noStore: true);
        await rig.SendAsync("Hello");
        Assert.Single(rig.Panel.SentEntries);

        // Act
        rig.Panel.ClearHistoryCommand.Execute(null);

        // Assert
        Assert.Equal(ChatPanelViewModel.ClearedText, Assert.IsType<NoticeRowViewModel>(Assert.Single(rig.Panel.Transcript)).Message);
        Assert.Empty(rig.Panel.SentEntries);
    }

    /// <summary>
    ///     Test that a history that cannot be deleted is said in an error notice, while the transcript is still emptied
    ///     and the copilot is reset.
    /// </summary>
    [Fact]
    public async Task ChatPanelViewModel_ClearHistory_StoreCannotDelete_ShowsAnErrorNotice()
    {
        // Arrange: a store that cannot delete
        var store = Substitute.For<IChatHistoryStore>();
        store.Load(Arg.Any<ProjectInfo>()).Returns([]);
        store.Clear(Arg.Any<ProjectInfo>()).Returns(ChatHistoryResult.Failed("The chat history could not be deleted: in use."));
        var rig = NewRig(NewProject("Demo"), store);
        await rig.SendAsync("Hello");
        rig.Agent!.Emit(new AgentEvent.Completed());
        await rig.WaitForTurnAsync();

        // Act
        rig.Panel.ClearHistoryCommand.Execute(null);

        // Assert
        var notice = Assert.IsType<NoticeRowViewModel>(Assert.Single(rig.Panel.Transcript));
        Assert.True(notice.IsError);
        Assert.Equal("The chat history could not be deleted: in use.", notice.Message);
        Assert.Equal(1, rig.Agent.ResetCount);
    }

    /// <summary>
    ///     Test that a history that cannot be saved is said in an error notice that is not saved, and that the
    ///     conversation goes on.
    /// </summary>
    [Fact]
    public async Task ChatPanelViewModel_SaveFails_ShowsAnErrorNoticeAndTheConversationGoesOn()
    {
        // Arrange: a store that cannot save
        var store = Substitute.For<IChatHistoryStore>();
        store.Load(Arg.Any<ProjectInfo>()).Returns([]);
        store.Save(Arg.Any<ProjectInfo>(), Arg.Any<IReadOnlyList<ChatHistoryRow>>()).Returns(ChatHistoryResult.Failed("The chat history could not be saved on this computer: disk full."));
        var rig = NewRig(NewProject("Demo"), store);

        // Act: two messages
        await rig.SendAsync("One");
        rig.Agent!.Emit(new AgentEvent.TextDelta("Answer"), new AgentEvent.Completed());
        await rig.WaitForTurnAsync();
        await rig.SendAsync("Two");
        rig.Agent.Emit(new AgentEvent.Completed());
        await rig.WaitForTurnAsync();

        // Assert: each failed save left a notice, and the second turn was run
        Assert.Equal(2, rig.Panel.Transcript.OfType<NoticeRowViewModel>().Count(notice => notice.IsError && notice.Message.Contains("disk full", StringComparison.Ordinal)));
        Assert.Equal(2, rig.Agent.Requests.Count);
    }

    /// <summary>
    ///     Test that a store whose root lies inside the project is refused by the store, that the panel says so, and that
    ///     nothing is written into the project.
    /// </summary>
    [Fact]
    public async Task ChatPanelViewModel_HistoryRootInsideTheProject_SaysSoAndWritesNothingIntoTheProject()
    {
        // Arrange: a store with its root in the project folder
        var project = NewProject("Demo");
        var store = new ChatHistoryStore(Path.Combine(project.FolderPath, "history"));
        var rig = NewRig(project, store);

        // Act
        await rig.SendAsync("Hello");
        rig.Agent!.Emit(new AgentEvent.Completed());
        await rig.WaitForTurnAsync();

        // Assert: a notice says it, and the project folder is empty
        var notice = Assert.Single(rig.Panel.Transcript.OfType<NoticeRowViewModel>());
        Assert.Contains("inside the project folder", notice.Message, StringComparison.Ordinal);
        Assert.Empty(Directory.GetFileSystemEntries(project.FolderPath));
    }

    /// <summary>
    ///     Test that a panel without a copilot saves the message when it is sent, because no turn follows.
    /// </summary>
    [Fact]
    public async Task ChatPanelViewModel_NoCopilot_SavesTheMessageWhenItIsSent()
    {
        // Arrange and act
        var project = NewProject("Demo");
        var rig = NewRig(project, withAgent: false);
        await rig.SendAsync("Hello");

        // Assert
        Assert.Equal("Hello", Assert.Single(_store.Load(project)).Text);
    }

    /// <summary>
    ///     Test that a panel without a store, or without a project service, keeps the transcript in memory only and
    ///     restores nothing.
    /// </summary>
    [Fact]
    public async Task ChatPanelViewModel_NoStoreOrNoProjectService_SavesAndRestoresNothing()
    {
        // Arrange: a saved conversation, and panels that cannot reach it
        var project = NewProject("Demo");
        _store.Save(project, SavedConversation());
        var withoutStore = NewRig(project, noStore: true);
        var withoutProjects = NewRig(project, withProjects: false);

        // Assert: nothing is restored
        Assert.Empty(withoutStore.Panel.Transcript);
        Assert.Empty(withoutProjects.Panel.Transcript);

        // Act: a message in the panel that has no project service
        await withoutProjects.SendAsync("Hello");
        withoutProjects.Agent!.Emit(new AgentEvent.Completed());
        await withoutProjects.WaitForTurnAsync();

        // Assert: the saved conversation is as it was
        Assert.Equal(4, _store.Load(project).Count);
        Assert.Empty(withoutProjects.Agent.Requests[0].RestoredHistory);
    }

    /// <summary>
    ///     Test that the saved rows of a sent message hold its words with the labels of the chips in their places, the
    ///     numbers and notes of its marks, and the project-relative paths of its attachments, and that rows without
    ///     words, a call that has no result, and a notice are left out.
    /// </summary>
    [Fact]
    public void ChatHistoryMapper_ToHistoryRows_SentMessageAndRows_KeepsWordsLabelsNotesAndPathsOnly()
    {
        // Arrange: a message with words, a mark with a note, an attached picture, and more words
        var picture = AnnotationTestPictures.Picture();
        var geometry = MarkGeometry.Create(MarkKind.Rectangle, [new RenderPoint(10, 20), new RenderPoint(60, 50)], picture.Width, picture.Height);
        var mark = new Mark(Guid.NewGuid(), 1, geometry, "too red", picture, DateTimeOffset.UtcNow);
        var file = new ImageChip(Path.Combine(_root, "elsewhere", "logo.png"));
        var document = new ComposerDocument();
        document.InsertText(0, "Why is ", TextOrigin.Typed);
        document.InsertChip(document.Length, new MarkChip(mark.Id, 1));
        document.InsertText(document.Length, " red? ", TextOrigin.Dictated);
        document.InsertChip(document.Length, file);
        var staged = new StagedAttachment(file, file.FullPath, "attachments/logo.png", AttachmentKind.Image, "logo.png", 5, 1, true, 0);
        var sent = new SentMessage(document.Snapshot(), [mark], [staged], ChatTestPayloads.OfText("payload words"), new DateTimeOffset(2026, 3, 4, 5, 6, 7, TimeSpan.Zero));
        using var entry = new TranscriptEntryViewModel(sent, _root, () => null);
        var running = new ToolActivityRowViewModel("c9", "render_image", "path=a.svg", "Rendering");
        var finished = new ToolActivityRowViewModel("project_write", "Created `a.svg`", true);
        var empty = new AssistantReplyViewModel();
        var failed = new AssistantReplyViewModel("It broke", AssistantReplyState.Failed);
        var notice = new NoticeRowViewModel(NoticeSeverity.Error, "A notice");
        var utc = new DateTimeOffset(2026, 3, 4, 5, 9, 0, TimeSpan.Zero);

        // Act
        var rows = ChatHistoryMapper.ToHistoryRows([entry, running, empty, finished, failed, notice], utc);

        // Assert: the message, the finished tool, and the failed reply, in order
        Assert.Equal([ChatHistoryRowKind.User, ChatHistoryRowKind.Tool, ChatHistoryRowKind.Assistant], rows.Select(row => row.Kind));
        var user = rows[0];
        Assert.Equal("Why is [Mark 1] red? [Attachment: logo.png]", user.Text);
        Assert.Equal(sent.SentUtc, user.Utc);
        Assert.Equal(["attachments/logo.png"], user.Attachments);
        var savedMark = Assert.Single(user.Marks!);
        Assert.Equal((1, "too red"), (savedMark.Number, savedMark.Note));
        Assert.Equal(("project_write", "Created `a.svg`", true, utc), (rows[1].Tool, rows[1].Text, rows[1].Succeeded, rows[1].Utc));
        Assert.Equal(("It broke", ChatHistoryReplyState.Failed), (rows[2].Text, rows[2].Reply));
        Assert.DoesNotContain("payload words", string.Concat(rows.Select(row => row.Text)), StringComparison.Ordinal);
        running.Dispose();
    }

    /// <summary>
    ///     Test that every kind of saved row becomes the row of the transcript that shows it, and that the turns for a new
    ///     provider session carry the words with the notes and the remarks.
    /// </summary>
    [Fact]
    public void ChatHistoryMapper_ToTranscriptRowAndToAgentTurns_CoverEveryKind()
    {
        // Arrange
        var rows = SavedConversation();
        rows.Add(new ChatHistoryRow { Kind = ChatHistoryRowKind.Assistant, Text = "It broke", Reply = ChatHistoryReplyState.Failed });
        rows.Add(new ChatHistoryRow { Kind = ChatHistoryRowKind.Tool, Tool = "render_image", Text = "Rendered `a.svg`", Succeeded = true });

        // Act
        var transcript = rows.Select(ChatHistoryMapper.ToTranscriptRow).ToList();
        var turns = ChatHistoryMapper.ToAgentTurns(rows);

        // Assert: the rows and the turns
        Assert.Equal(
            [typeof(RestoredEntryViewModel), typeof(AssistantReplyViewModel), typeof(ToolActivityRowViewModel), typeof(AssistantReplyViewModel), typeof(AssistantReplyViewModel), typeof(ToolActivityRowViewModel)],
            transcript.Select(row => row.GetType()));
        Assert.Equal(AssistantReplyState.Failed, Assert.IsType<AssistantReplyViewModel>(transcript[4]).State);
        Assert.True(Assert.IsType<ToolActivityRowViewModel>(transcript[5]).Succeeded);
        Assert.Equal("It broke [The reply failed.]", turns[4].Text);
        Assert.Equal("[Used the tool render_image: Rendered `a.svg`]", turns[5].Text);
        Assert.All(transcript, row => row.Dispose());
    }

    /// <summary>
    ///     Test that the row of a restored message is made only from the row of a message of the user, and says what it
    ///     carried in the singular and the plural.
    /// </summary>
    [Fact]
    public void RestoredEntryViewModel_New_OnlyUserRows_AndSummaryCountsMarksAndAttachments()
    {
        // Arrange and act: a message with two marks, one without a note, and two attachments
        var row = new ChatHistoryRow
        {
            Kind = ChatHistoryRowKind.User,
            Utc = new DateTimeOffset(2026, 3, 4, 5, 6, 7, TimeSpan.Zero),
            Text = "[Mark 2] [Mark 1]",
            Marks = [new ChatHistoryMark(2, null), new ChatHistoryMark(1, "first")],
            Attachments = ["attachments/a.png", "attachments/b.txt"],
        };
        var restored = new RestoredEntryViewModel(row);

        // Assert: counts in the plural, marks in the order of their numbers, then the attachments
        Assert.Equal("2 marks, 2 attachments", restored.Summary);
        Assert.Equal("Mark 1: first\nMark 2\nAttachment: attachments/a.png\nAttachment: attachments/b.txt", restored.DetailsText);
        Assert.Equal(TranscriptRowKind.User, restored.Kind);
        Assert.Same(row, restored.Row);
        Assert.False(string.IsNullOrWhiteSpace(restored.SentText));
        Assert.Equal(string.Empty, new RestoredEntryViewModel(new ChatHistoryRow { Kind = ChatHistoryRowKind.User, Text = "plain" }).Summary);
        Assert.Throws<ArgumentException>(() => new RestoredEntryViewModel(new ChatHistoryRow { Kind = ChatHistoryRowKind.Assistant, Text = "x" }));
        Assert.Throws<ArgumentNullException>(() => new RestoredEntryViewModel(null!));
    }

    /// <summary>
    ///     Test that a chip label that was saved with no separator before the words is shown with a space after it, and that
    ///     labels that have a separator or are followed by punctuation are left as they are.
    /// </summary>
    [Fact]
    public void RestoredEntryViewModel_New_ChipLabelGluedToWords_GetsASpace()
    {
        // Arrange and act: rows as an earlier version saved them, and rows that were fine
        static string Shown(string text) => new RestoredEntryViewModel(new ChatHistoryRow { Kind = ChatHistoryRowKind.User, Text = text }).Text;

        // Assert: a space after the glued label, and nothing else changed
        Assert.Equal("[Mark 1] Chart downloads by month", Shown("[Mark 1]Chart downloads by month"));
        Assert.Equal("[Attachment: a b.png] Use it", Shown("[Attachment: a b.png]Use it"));
        Assert.Equal("[Mark 1]\nChart", Shown("[Mark 1]\nChart"));
        Assert.Equal("Why is [Mark 1] red? Is [Mark 2], too?", Shown("Why is [Mark 1] red? Is [Mark 2], too?"));
        Assert.Equal("[Mark 1][Mark 2] Both", Shown("[Mark 1][Mark 2]Both"));
    }

    /// <summary>
    ///     Makes the rows of a conversation that was saved: a message with a mark and an attachment, a reply, a tool that
    ///     failed, and a reply that was stopped.
    /// </summary>
    /// <returns>The rows, oldest first.</returns>
    private static List<ChatHistoryRow> SavedConversation() =>
    [
        new()
        {
            Kind = ChatHistoryRowKind.User,
            Utc = new DateTimeOffset(2026, 5, 6, 7, 8, 9, TimeSpan.Zero),
            Text = "Why is [Mark 1] red? [Attachment: logo.png]",
            Attachments = ["attachments/logo.png"],
            Marks = [new ChatHistoryMark(1, "check this")],
        },
        new() { Kind = ChatHistoryRowKind.Assistant, Utc = new DateTimeOffset(2026, 5, 6, 7, 9, 0, TimeSpan.Zero), Text = "Because of the fill.", Reply = ChatHistoryReplyState.Completed },
        new() { Kind = ChatHistoryRowKind.Tool, Utc = new DateTimeOffset(2026, 5, 6, 7, 9, 1, TimeSpan.Zero), Tool = "project_write", Text = "Replaced `flow.svg`", Succeeded = false },
        new() { Kind = ChatHistoryRowKind.Assistant, Utc = new DateTimeOffset(2026, 5, 6, 7, 9, 2, TimeSpan.Zero), Text = "Stopped here", Reply = ChatHistoryReplyState.Stopped },
    ];

    /// <summary>
    ///     Makes a project in a folder of its own that exists and is empty.
    /// </summary>
    /// <param name="name">The name of the project.</param>
    /// <returns>The project.</returns>
    private ProjectInfo NewProject(string name)
    {
        var folder = Directory.CreateDirectory(Path.Combine(_root, "projects", name)).FullName;
        return new ProjectInfo(name, folder, Path.Combine(folder, name + ProjectDocument.FileExtension), new ProjectDocument { Name = name });
    }

    /// <summary>
    ///     Makes a panel with the real composer and the controlled copilot, and keeps it for disposal.
    /// </summary>
    /// <param name="project">The project that is open when the panel is made, or <see langword="null" /> for none.</param>
    /// <param name="store">The store; the store of the test unless a test gives another or <see langword="null" />.</param>
    /// <param name="withAgent">Whether the panel has a copilot.</param>
    /// <param name="withProjects">Whether the panel has a project service.</param>
    /// <param name="noStore">Whether the panel has no store at all.</param>
    /// <returns>The rig.</returns>
    private Rig NewRig(ProjectInfo? project, IChatHistoryStore? store = null, bool withAgent = true, bool withProjects = true, bool noStore = false)
    {
        var rig = new Rig(project, noStore ? null : store ?? _store, withAgent, withProjects);
        _rigs.Add(rig);
        return rig;
    }

    /// <summary>
    ///     A panel with its composer, annotation layer, project service, and copilot.
    /// </summary>
    private sealed class Rig : IDisposable
    {
        /// <summary>
        ///     Initializes a new instance of the <see cref="Rig" /> class.
        /// </summary>
        /// <param name="project">The project that is open when the panel is made, or <see langword="null" /> for none.</param>
        /// <param name="store">The store of the panel, or <see langword="null" /> for none.</param>
        /// <param name="withAgent">Whether the panel has a copilot.</param>
        /// <param name="withProjects">Whether the panel has a project service.</param>
        public Rig(ProjectInfo? project, IChatHistoryStore? store, bool withAgent, bool withProjects)
        {
            var marks = new MarkRegistry();
            Composer = new ComposerViewModel(new PayloadTextBuilder(), marks, new FakeDictationService());
            Annotation = new AnnotationOverlayViewModel(marks, new FakeRenderedImageSource(), _ => { });
            Agent = withAgent ? new ControlledAgentService() : null;
            Projects = Substitute.For<IProjectService>();
            Project = project;
            Projects.Current.Returns(project);
            if (project is not null)
            {
                Composer.SetProject(project.FolderPath);
            }

            Panel = new ChatPanelViewModel(
                Composer,
                marks,
                Annotation,
                Substitute.For<IMarkImageComposer>(),
                agent: Agent,
                clock: new ManualTimeProvider(),
                history: store,
                projects: withProjects ? Projects : null);
        }

        /// <summary>
        ///     Gets the composer.
        /// </summary>
        public ComposerViewModel Composer { get; }

        /// <summary>
        ///     Gets the annotation layer.
        /// </summary>
        public AnnotationOverlayViewModel Annotation { get; }

        /// <summary>
        ///     Gets the copilot, or <see langword="null" /> when the panel has none.
        /// </summary>
        public ControlledAgentService? Agent { get; }

        /// <summary>
        ///     Gets the project service.
        /// </summary>
        public IProjectService Projects { get; }

        /// <summary>
        ///     Gets the project that is open.
        /// </summary>
        public ProjectInfo? Project { get; private set; }

        /// <summary>
        ///     Gets the panel.
        /// </summary>
        public ChatPanelViewModel Panel { get; }

        /// <summary>
        ///     Opens a project, or closes the open one, as the shell does: the service has the project when the composer is
        ///     told.
        /// </summary>
        /// <param name="project">The project, or <see langword="null" /> to close.</param>
        public void Open(ProjectInfo? project)
        {
            Project = project;
            Projects.Current.Returns(project);
            Composer.SetProject(project?.FolderPath);
        }

        /// <summary>
        ///     Writes a message and sends it.
        /// </summary>
        /// <param name="text">The words.</param>
        /// <returns>A task that completes when the message was sent.</returns>
        public async Task SendAsync(string text)
        {
            Composer.InsertTyped(text);
            await Panel.SendCommand.ExecuteAsync(null).WaitAsync(TestContext.Current.CancellationToken);
        }

        /// <summary>
        ///     Waits until the latest turn has ended.
        /// </summary>
        /// <returns>A task that completes when it has.</returns>
        public Task WaitForTurnAsync() => Panel.WaitForTurnAsync().WaitAsync(TurnTimeout, TestContext.Current.CancellationToken);

        /// <summary>
        ///     Waits until the transcript has a row of a kind.
        /// </summary>
        /// <typeparam name="TRow">The kind of row.</typeparam>
        /// <returns>A task that completes when it has one.</returns>
        public async Task WaitForRowAsync<TRow>()
            where TRow : TranscriptRowViewModel
        {
            var deadline = DateTime.UtcNow + TurnTimeout;
            while (!Panel.Transcript.OfType<TRow>().Any())
            {
                if (DateTime.UtcNow > deadline)
                {
                    throw new TimeoutException($"The transcript did not get a {typeof(TRow).Name}.");
                }

                await Task.Delay(10, TestContext.Current.CancellationToken);
            }
        }

        /// <inheritdoc />
        public void Dispose()
        {
            Panel.Dispose();
            Annotation.Dispose();
            Composer.Dispose();
            Agent?.Dispose();
        }
    }

    /// <summary>
    ///     A builder of sent messages whose payload is a real payload with the text of the message, as the copilot needs.
    /// </summary>
    private sealed class PayloadTextBuilder : IMessageBuilder
    {
        /// <inheritdoc />
        public Task<SentMessage> BuildAsync(
            ComposerSnapshot message,
            IReadOnlyList<Mark> marks,
            IReadOnlyList<StagedAttachment> attachments,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new SentMessage(message, marks, attachments, ChatTestPayloads.OfText(message.FlatText), FakeMessageBuilder.Now));
    }
}
