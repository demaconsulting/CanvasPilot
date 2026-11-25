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

using DemaConsulting.CanvasPilot.AnnotationSubsystem;
using DemaConsulting.CanvasPilot.ChatSubsystem;
using DemaConsulting.CanvasPilot.ComposerSubsystem;
using DemaConsulting.CanvasPilot.PayloadSubsystem;
using DemaConsulting.CanvasPilot.PreviewSubsystem;
using DemaConsulting.CanvasPilot.SpeechSubsystem;
using NSubstitute;

namespace DemaConsulting.CanvasPilot.Tests;

/// <summary>
///     Unit tests for <see cref="ChatPanelViewModel" />, <see cref="TranscriptEntryViewModel" />, and
///     <see cref="ChipThumbnailProvider" />: when the message can be sent, what the transcript holds, how a mark drawn on
///     the picture becomes a chip, how the talk button follows dictation, and the command that shows the payload. The
///     composer, the registry, and the annotation layer are the real units over a temporary project; the dictation
///     service and the builder of sent messages are controllable fakes, and the composer of mark pictures is a
///     substitute.
/// </summary>
public sealed class ChatPanelTests : IDisposable
{
    /// <summary>
    ///     Temporary folder that holds the projects.
    /// </summary>
    private readonly string _root = Directory.CreateTempSubdirectory("canvaspilot-chat-panel-tests-").FullName;

    /// <summary>
    ///     The folder of the open project.
    /// </summary>
    private readonly string _project;

    /// <summary>
    ///     The builder of sent messages; it gives a text as the payload unless a test replaces it.
    /// </summary>
    private readonly FakeMessageBuilder _builder = new();

    /// <summary>
    ///     The dictation service.
    /// </summary>
    private readonly FakeDictationService _dictation = new();

    /// <summary>
    ///     The marks made for the draft.
    /// </summary>
    private readonly MarkRegistry _marks = new();

    /// <summary>
    ///     The source of the picture the annotation layer draws on.
    /// </summary>
    private readonly FakeRenderedImageSource _source = new() { CurrentImage = AnnotationTestPictures.Picture() };

    /// <summary>
    ///     The composer of mark pictures.
    /// </summary>
    private readonly IMarkImageComposer _markImages = Substitute.For<IMarkImageComposer>();

    /// <summary>
    ///     The composer of the message.
    /// </summary>
    private readonly ComposerViewModel _composer;

    /// <summary>
    ///     The annotation layer.
    /// </summary>
    private readonly AnnotationOverlayViewModel _annotation;

    /// <summary>
    ///     The chat panel under test.
    /// </summary>
    private readonly ChatPanelViewModel _panel;

    /// <summary>
    ///     Initializes a new instance of the <see cref="ChatPanelTests" /> class with a chat panel in an open project.
    /// </summary>
    public ChatPanelTests()
    {
        _project = Directory.CreateDirectory(Path.Combine(_root, "project")).FullName;
        _composer = new ComposerViewModel(_builder, _marks, _dictation);
        _annotation = new AnnotationOverlayViewModel(_marks, _source, _ => { });
        _panel = new ChatPanelViewModel(_composer, _marks, _annotation, _markImages, _dictation);
        _composer.SetProject(_project);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _panel.Dispose();
        _annotation.Dispose();
        _composer.Dispose();
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    /// <summary>
    ///     Test that the panel refuses missing arguments.
    /// </summary>
    [Fact]
    public void ChatPanelViewModel_New_NullArguments_Throw()
    {
        // Arrange / Act / Assert: each required argument is checked
        Assert.Throws<ArgumentNullException>(() => new ChatPanelViewModel(null!, _marks, _annotation, _markImages));
        Assert.Throws<ArgumentNullException>(() => new ChatPanelViewModel(_composer, null!, _annotation, _markImages));
        Assert.Throws<ArgumentNullException>(() => new ChatPanelViewModel(_composer, _marks, null!, _markImages));
        Assert.Throws<ArgumentNullException>(() => new ChatPanelViewModel(_composer, _marks, _annotation, null!));
    }

    /// <summary>
    ///     Test that a new panel has an empty transcript and nothing to send.
    /// </summary>
    [Fact]
    public void ChatPanelViewModel_New_HasEmptyTranscriptAndCannotSend()
    {
        // Arrange / Act: the panel of the constructor in an open project

        // Assert: nothing was sent and an empty message cannot be sent
        Assert.Empty(_panel.Transcript);
        Assert.False(_panel.HasTranscript);
        Assert.False(_panel.CanSend);
        Assert.False(_panel.SendCommand.CanExecute(null));
        Assert.Same(_composer, _panel.Composer);
    }

    /// <summary>
    ///     Test that the send state follows the message: it cannot be sent without a project, or without words, can be sent
    ///     with words, and cannot be sent while the talk button is held; each change is announced.
    /// </summary>
    [Fact]
    public async Task ChatPanelViewModel_CanSend_FollowsProjectContentAndTalkButton()
    {
        // Arrange: a panel in an open project that announces what changes
        var changed = new List<string?>();
        _panel.PropertyChanged += (_, e) => changed.Add(e.PropertyName);

        // Act / Assert: words make the message sendable, and the announcement says so
        _composer.InsertTyped("Hello");
        Assert.True(_panel.CanSend);
        Assert.True(_panel.SendCommand.CanExecute(null));
        Assert.Contains(nameof(ChatPanelViewModel.CanSend), changed);

        // Act / Assert: while the talk button is held, the message cannot be sent
        await _panel.BeginPushToTalkAsync();
        Assert.False(_panel.CanSend);
        await _panel.EndPushToTalkAsync();
        Assert.True(_panel.CanSend);

        // Act / Assert: with the project closed, nothing can be sent or said
        _composer.SetProject(null);
        Assert.False(_panel.CanSend);
        Assert.False(_panel.CanTalk);
    }

    /// <summary>
    ///     Test that sending adds one entry with the typed words to the transcript and empties the draft.
    /// </summary>
    [Fact]
    public async Task ChatPanelViewModel_Send_TypedText_AddsOneEntryAndEmptiesTheDraft()
    {
        // Arrange: a typed message
        _composer.InsertTyped("Make the box blue");

        // Act: send it
        await _panel.SendCommand.ExecuteAsync(null);

        // Assert: the transcript holds that one message with its words, and nothing of it is left in the composer
        var entry = Assert.Single(_panel.SentEntries);
        var item = Assert.Single(entry.Items);
        Assert.Equal("Make the box blue", item.Text);
        Assert.Equal(TextOrigin.Typed, item.Origin);
        Assert.False(item.IsChip);
        Assert.True(_panel.HasTranscript);
        Assert.Equal(string.Empty, _composer.Document.FlatText);
        Assert.False(_composer.CanUndo);
        Assert.Equal(string.Empty, entry.Summary);
        Assert.False(entry.HasPayload);
    }

    /// <summary>
    ///     Test that the transcript lists only sent messages, oldest first, and never the draft.
    /// </summary>
    [Fact]
    public async Task ChatPanelViewModel_Transcript_TwoMessagesAndADraft_ListsOnlySentMessagesInOrder()
    {
        // Arrange / Act: two messages are sent and a third is only typed
        _composer.InsertTyped("first");
        await _panel.SendCommand.ExecuteAsync(null);
        _composer.InsertTyped("second");
        await _panel.SendCommand.ExecuteAsync(null);
        _composer.InsertTyped("draft");

        // Assert: the draft is not in the transcript, and the sent messages are in the order they were sent
        Assert.Equal(["first", "second"], _panel.SentEntries.Select(entry => entry.Items[0].Text));
    }

    /// <summary>
    ///     Test that a message with typed words, dictated words, and a mark is shown in the order it was written, with the
    ///     origin of each text, and says what it carried besides words.
    /// </summary>
    [Fact]
    public async Task ChatPanelViewModel_Send_TypedDictatedAndMark_EntryKeepsOrderOriginsAndSummary()
    {
        // Arrange: "Hello " typed, "box" dictated, and a mark added after them
        _composer.InsertTyped("Hello ");
        _dictation.Session.FinalText = "box";
        await _panel.BeginPushToTalkAsync();
        await _panel.EndPushToTalkAsync();
        var mark = _marks.Add(_source.CurrentImage!, MarkKind.Rectangle, [new RenderPoint(5, 5), new RenderPoint(40, 30)]);
        _composer.AddMark(mark);

        // Act: send it
        await _panel.SendCommand.ExecuteAsync(null);

        // Assert: the mark chip first, in the row above the words, then the typed and the dictated words in the order they were
        // written, with a summary naming the mark
        var entry = Assert.Single(_panel.SentEntries);
        Assert.Equal(3, entry.Items.Count);
        var chip = Assert.IsType<MarkChip>(entry.Items[0].Chip);
        Assert.Equal(("\nHello ", TextOrigin.Typed), (entry.Items[1].Text, entry.Items[1].Origin));
        Assert.Equal(("box", TextOrigin.Dictated), (entry.Items[2].Text, entry.Items[2].Origin));
        Assert.Equal(1, chip.Number);
        Assert.Equal("1 mark", entry.Summary);
    }

    /// <summary>
    ///     Test that a message that cannot be sent leaves the draft as it was, adds nothing to the transcript, and says why.
    /// </summary>
    [Fact]
    public async Task ChatPanelViewModel_Send_BuildFails_KeepsDraftAndTranscriptUnchangedAndSaysWhy()
    {
        // Arrange: a typed message and a builder that fails
        _composer.InsertTyped("keep me");
        _builder.Failure = new InvalidOperationException("no picture");

        // Act: try to send
        await _panel.SendCommand.ExecuteAsync(null);

        // Assert: the draft is still there, the transcript is empty, and the status text says it was not sent
        Assert.Empty(_panel.Transcript);
        Assert.Equal("keep me", _composer.Document.FlatText);
        Assert.Contains("not sent", _composer.StatusText);
        Assert.Contains("no picture", _composer.StatusText);
    }

    /// <summary>
    ///     Test that a mark drawn on the picture becomes one chip in the row of chips of the message as one undo step, that the
    ///     layer shows it while the chip is in the message, and that undo removes the chip and hides the mark and redo brings
    ///     both back.
    /// </summary>
    [Fact]
    public void ChatPanelViewModel_MarkDrawn_BecomesOneChipStepAndUndoHidesTheMark()
    {
        // Arrange: some text, and the rectangle tool
        _composer.InsertTyped("see ");
        _annotation.SelectTool(AnnotationTool.Rectangle);

        // Act: draw a rectangle on the picture and skip the note
        _annotation.BeginStroke(new RenderPoint(10, 10));
        _annotation.ContinueStroke(new RenderPoint(30, 20));
        _annotation.EndStroke(new RenderPoint(50, 40));
        _annotation.SkipNote();

        // Assert: one mark chip is in the row above the text and the layer shows the mark
        Assert.Equal("\uFFFC\nsee ", _composer.Document.FlatText);
        var chip = Assert.Single(_composer.Document.Chips.OfType<MarkChip>());
        Assert.Equal(1, chip.Number);
        Assert.Single(_annotation.VisibleMarks);

        // Act / Assert: one undo takes the chip out, and the mark is no longer shown
        _composer.Undo();
        Assert.Equal("see ", _composer.Document.FlatText);
        Assert.Empty(_annotation.VisibleMarks);

        // Act / Assert: redo brings the chip and the mark back
        _composer.Redo();
        Assert.Single(_composer.Document.Chips.OfType<MarkChip>());
        Assert.Single(_annotation.VisibleMarks);
    }

    /// <summary>
    ///     Test that the layer can make marks exactly while a message can be written.
    /// </summary>
    [Fact]
    public void ChatPanelViewModel_DrawingAllowed_FollowsWhetherAMessageCanBeWritten()
    {
        // Arrange / Act / Assert: allowed in the open project, not allowed after it is closed, allowed again in another
        Assert.True(_annotation.IsDrawingAllowed);
        _composer.SetProject(null);
        Assert.False(_annotation.IsDrawingAllowed);
        _composer.SetProject(_project);
        Assert.True(_annotation.IsDrawingAllowed);
    }

    /// <summary>
    ///     Test that sending a message with a mark empties the layer, so that no mark of a sent message stays on the picture.
    /// </summary>
    [Fact]
    public async Task ChatPanelViewModel_Send_MessageWithMark_LayerShowsNoMarkAfterwards()
    {
        // Arrange: a message with a mark
        var mark = _marks.Add(_source.CurrentImage!, MarkKind.Rectangle, [new RenderPoint(5, 5), new RenderPoint(40, 30)]);
        _composer.AddMark(mark);
        _annotation.SetActiveMarkIds(_composer.MarkChipIds);
        Assert.Single(_annotation.VisibleMarks);

        // Act: send it
        await _panel.SendCommand.ExecuteAsync(null);

        // Assert: the registry and the layer hold no mark any more
        Assert.Empty(_marks.Marks);
        Assert.Empty(_annotation.VisibleMarks);
    }

    /// <summary>
    ///     Test that a request from a mark's chip in the message opens the mark's note for changing on the layer, and
    ///     that the note confirmed there is the one the composer gives for the chip.
    /// </summary>
    [Fact]
    public void ChatPanelViewModel_MarkNoteEditRequested_OpensTheNoteOnTheLayer()
    {
        // Arrange: a mark with a note drawn on the layer, whose chip the panel put in the message
        _annotation.SelectTool(AnnotationTool.Rectangle);
        _annotation.BeginStroke(new RenderPoint(10, 10));
        _annotation.EndStroke(new RenderPoint(50, 40));
        _annotation.NoteText = "first";
        _annotation.CommitNote();
        _annotation.SelectTool(AnnotationTool.None);
        var chip = Assert.Single(_composer.Document.Chips.OfType<MarkChip>());

        // Act: ask from the chip to change the note
        var requested = _composer.RequestMarkNoteEdit(chip.MarkId);

        // Assert: the layer opened the note of that mark, filled with it
        Assert.True(requested);
        Assert.True(_annotation.IsEditingMarkNote);
        Assert.Equal(chip.MarkId, _annotation.EditingMarkId);
        Assert.Equal("first", _annotation.NoteText);

        // Act: change the note on the layer and confirm it
        _annotation.NoteText = "second";
        _annotation.CommitNote();

        // Assert: the composer gives the changed note for the chip, and the message is unchanged
        Assert.Equal("second", _composer.GetMarkNote(chip.MarkId));
        Assert.Equal(chip, Assert.Single(_composer.Document.Chips.OfType<MarkChip>()));
    }

    /// <summary>
    ///     Test that a request from a mark's chip that the layer cannot follow, because no picture is shown or a shape is
    ///     being drawn, adds an information notice that names the mark and says what to do, once for each cause, and
    ///     leaves the mark, its note, and the message as they were.
    /// </summary>
    [Fact]
    public void ChatPanelViewModel_MarkNoteEditRequested_LayerCannotOpen_ShowsNoticeWithWhatToDo()
    {
        // Arrange: a mark with a note drawn on the layer, whose chip the panel put in the message
        var picture = _source.CurrentImage;
        _annotation.SelectTool(AnnotationTool.Rectangle);
        _annotation.BeginStroke(new RenderPoint(10, 10));
        _annotation.EndStroke(new RenderPoint(50, 40));
        _annotation.NoteText = "keep me";
        _annotation.CommitNote();
        var chip = Assert.Single(_composer.Document.Chips.OfType<MarkChip>());
        var message = _composer.Document.FlatText;

        // Act: hide the picture and ask twice from the chip to change the note
        _source.CurrentImage = null;
        _composer.RequestMarkNoteEdit(chip.MarkId);
        _composer.RequestMarkNoteEdit(chip.MarkId);
        var noPicture = _panel.Transcript.ToList();
        var openedWithoutPicture = _annotation.IsNoteEditing;

        // Act: show the picture again, start a shape, and ask once more
        _source.CurrentImage = picture;
        _annotation.SelectTool(AnnotationTool.Rectangle);
        _annotation.BeginStroke(new RenderPoint(60, 60));
        _composer.RequestMarkNoteEdit(chip.MarkId);

        // Assert: no note card opened, and one notice for each cause, naming the mark and saying what to do
        Assert.False(openedWithoutPicture);
        Assert.False(_annotation.IsEditingMarkNote);
        var first = Assert.IsType<NoticeRowViewModel>(Assert.Single(noPicture));
        Assert.Equal(NoticeSeverity.Information, first.Severity);
        Assert.Equal("The note of Mark 1 cannot be changed now.", first.Message);
        Assert.Equal(ChatPanelViewModel.NoteNeedsPictureRemedy, first.Remedy);
        Assert.Equal(2, _panel.Transcript.Count);
        var second = Assert.IsType<NoticeRowViewModel>(_panel.Transcript[1]);
        Assert.Equal("The note of Mark 1 cannot be changed now.", second.Message);
        Assert.Equal(ChatPanelViewModel.NoteWaitsForShapeRemedy, second.Remedy);

        // Assert: the mark keeps its note and the message is as it was
        Assert.Equal("keep me", _composer.GetMarkNote(chip.MarkId));
        Assert.Equal(message, _composer.Document.FlatText);
    }

    /// <summary>
    ///     Test that the rows that open a group are the first row and the first row of the copilot after a notice, so that the
    ///     name of the copilot is shown once for a run of replies and tools.
    /// </summary>
    [Fact]
    public void ChatPanelViewModel_Transcript_MarksTheRowsThatOpenAGroup()
    {
        // Arrange / Act: a reply and a tool, a notice, and a tool and a reply
        var rows = new TranscriptRowViewModel[]
        {
            new AssistantReplyViewModel("First", AssistantReplyState.Completed),
            new ToolActivityRowViewModel("render_image", "Rendered `a.svg`", succeeded: true),
            new NoticeRowViewModel(NoticeSeverity.Information, "Something happened"),
            new ToolActivityRowViewModel("render_image", "Rendered `b.svg`", succeeded: true),
            new AssistantReplyViewModel("Second", AssistantReplyState.Completed),
        };
        foreach (var row in rows)
        {
            _panel.Transcript.Add(row);
        }

        // Assert: the group starts at the first row and at the tool after the notice
        Assert.Equal([true, false, false, true, false], rows.Select(row => row.OpensGroup));
    }

    /// <summary>
    ///     Test that a tool row shows a check or a cross once it has ended, the time it took when that is worth saying, and
    ///     nothing of the sort while it is under way or when it was restored.
    /// </summary>
    [Fact]
    public void ToolActivityRowViewModel_Finish_ShowsOutcomeAndElapsedTime()
    {
        // Arrange: a call that started on a clock that the test moves
        var clock = new ManualTimeProvider();
        var row = new ToolActivityRowViewModel("c1", "render_image", "path=a.svg", "Rendering", clock);
        Assert.True(row.IsRunning);
        Assert.False(row.ShowsSuccess);
        Assert.False(row.ShowsFailure);
        Assert.Equal(string.Empty, row.ElapsedText);

        // Act: the result arrives after 0.8 seconds
        clock.Advance(TimeSpan.FromMilliseconds(800));
        row.Finish("Rendered `a.svg`", succeeded: true, hasImage: true);

        // Assert
        Assert.True(row.ShowsSuccess);
        Assert.False(row.ShowsFailure);
        Assert.Equal(string.Format(System.Globalization.CultureInfo.CurrentCulture, "{0:0.0} s", 0.8), row.ElapsedText);

        // Act / Assert: a quick failure shows a cross and no time, and a restored row shows no time either
        var quick = new ToolActivityRowViewModel("c2", "render_image", string.Empty, "Rendering", clock);
        quick.Finish("Could not render", succeeded: false, hasImage: false);
        Assert.True(quick.ShowsFailure);
        Assert.Equal(string.Empty, quick.ElapsedText);
        Assert.Equal(string.Empty, new ToolActivityRowViewModel("render_image", "Rendered `a.svg`", succeeded: true).ElapsedText);
    }
    /// <summary>
    ///     Test that a prompt is written in the message, that the follow-up prompts show only for a conversation, and that
    ///     the collapse command raises its request.
    /// </summary>
    [Fact]
    public void ChatPanelViewModel_PromptsAndCollapse_WriteTheMessageAndAnnounce()
    {
        // Arrange: a panel with a project open and nothing said
        _composer.SetProject(Path.GetTempPath());
        var changed = new List<string?>();
        _panel.PropertyChanged += (_, e) => changed.Add(e.PropertyName);
        var collapses = 0;
        _panel.CollapseRequested += (_, _) => collapses++;
        Assert.False(_panel.ShowSuggestions);

        // Act
        var prompt = _panel.Suggestions[1];
        var written = _panel.UsePrompt(prompt);
        _panel.CollapseCommand.Execute(null);

        // Assert: the prompt is the message, and the collapse was requested once; the picture of the fixture is shown, so
        // the project has a drawing and the prompts are the refinements with the export
        Assert.True(written);
        Assert.Equal(prompt, _composer.Document.FlatText);
        Assert.Equal(1, collapses);
        Assert.Equal(FollowUpSuggestions.Plan(hasDrawing: true, hasPictureShown: true, busy: false), _panel.Suggestions);
        Assert.Throws<ArgumentNullException>(() => _panel.UsePrompt(null!));
    }
    /// <summary>
    ///     Test that the label of the talk button says what the button is doing and that each change is announced.
    /// </summary>
    [Fact]
    public async Task ChatPanelViewModel_TalkText_FollowsDictationState()
    {
        // Arrange: a panel that announces what changes, and a start that waits for the test
        var changed = new List<string?>();
        _panel.PropertyChanged += (_, e) => changed.Add(e.PropertyName);
        _dictation.Gate = new TaskCompletionSource();
        Assert.Equal(ChatPanelViewModel.TalkIdleText, _panel.TalkText);
        Assert.False(_panel.IsTalking);

        // Act / Assert: pressed, the label says that listening is starting
        var pressed = _panel.BeginPushToTalkAsync();
        Assert.Equal("Starting... keep holding", _panel.TalkText);
        Assert.True(_panel.IsTalking);
        _dictation.Gate.SetResult();
        await pressed;

        // Assert: listening, then finished and back to the first label
        Assert.Equal("Listening... let go to finish", _panel.TalkText);
        await _panel.EndPushToTalkAsync();
        Assert.Equal(ChatPanelViewModel.TalkIdleText, _panel.TalkText);
        Assert.False(_panel.IsTalking);
        Assert.Contains(nameof(ChatPanelViewModel.TalkText), changed);
        Assert.Contains(nameof(ChatPanelViewModel.IsTalking), changed);
    }

    /// <summary>
    ///     Test that holding the talk button shows the words as they are recognized and that letting go adds them to the
    ///     message as one block that one undo removes.
    /// </summary>
    [Fact]
    public async Task ChatPanelViewModel_PushToTalk_PressSpeakRelease_AddsOneDictatedBlockAsOneStep()
    {
        // Arrange: a message with some typed words, and speech that ends in a sentence
        _composer.InsertTyped("Please ");
        _dictation.Session.FinalText = "draw a circle";

        // Act: press, speak, and look at the live words
        await _panel.BeginPushToTalkAsync();
        _dictation.Session.Say("draw a");
        var live = _composer.LivePartialText;
        await _panel.EndPushToTalkAsync();

        // Assert: the live words were shown, the final words are one dictated block after the typed words, and one undo
        // takes the whole block away
        Assert.Equal("draw a", live);
        var dictated = Assert.Single(_composer.Document.Segments.OfType<TextSegment>(), segment => segment.Origin == TextOrigin.Dictated);
        Assert.Equal("draw a circle", dictated.Text);
        _composer.Undo();
        Assert.Equal("Please ", _composer.Document.FlatText);
    }

    /// <summary>
    ///     Test that a press while dictation is impossible says why and what to do and changes nothing in the message.
    /// </summary>
    [Fact]
    public async Task ChatPanelViewModel_PushToTalk_NoModelChosen_ShowsReasonAndChangesNothing()
    {
        // Arrange: dictation that is not available, and some typed words
        const string reason = "No speech model is chosen. Choose one on the Speech page of the settings.";
        _dictation.Availability = new DictationAvailability(DictationAvailabilityReason.NoModelChosen, reason);
        _composer.InsertTyped("typed");

        // Act: press and release
        await _panel.BeginPushToTalkAsync();
        await _panel.EndPushToTalkAsync();

        // Assert: the status text is the reason, nothing listened, and the message is as it was
        Assert.Equal(reason, _composer.StatusText);
        Assert.Equal(0, _dictation.BeginCount);
        Assert.False(_panel.IsTalking);
        Assert.Equal("typed", _composer.Document.FlatText);
    }

    /// <summary>
    ///     Test that a second press while the first is held does not start a second listening.
    /// </summary>
    [Fact]
    public async Task ChatPanelViewModel_PushToTalk_SecondPressWhileHeld_IsIgnored()
    {
        // Arrange / Act: press twice before letting go
        await _panel.BeginPushToTalkAsync();
        await _panel.BeginPushToTalkAsync();
        await _panel.EndPushToTalkAsync();

        // Assert: listening was started once
        Assert.Equal(1, _dictation.BeginCount);
    }

    /// <summary>
    ///     Test that the talk button cannot be used without a project.
    /// </summary>
    [Fact]
    public void ChatPanelViewModel_CanTalk_NoProject_IsFalse()
    {
        // Arrange / Act: close the project
        _composer.SetProject(null);

        // Assert: nothing can be said into a message that cannot exist
        Assert.False(_panel.CanTalk);
    }

    /// <summary>
    ///     Test that the speech engine is prepared ahead of time when the panel asks, and that a failure is not thrown.
    /// </summary>
    [Fact]
    public async Task ChatPanelViewModel_PrepareDictation_PreparesOnceAndSwallowsFailure()
    {
        // Arrange / Act: prepare, then prepare again with a service that fails
        await _panel.PrepareDictationAsync();
        _dictation.PrepareFailure = new InvalidOperationException("engine broke");
        await _panel.PrepareDictationAsync();

        // Assert: both were asked and the failure did not escape
        Assert.Equal(2, _dictation.PrepareCount);
    }

    /// <summary>
    ///     Test that a panel without a dictation service prepares nothing and does not fail.
    /// </summary>
    [Fact]
    public async Task ChatPanelViewModel_PrepareDictation_NoDictationService_DoesNothing()
    {
        // Arrange: a panel made without dictation
        using var panel = new ChatPanelViewModel(_composer, _marks, _annotation, _markImages);

        // Act: prepare
        var preparing = panel.PrepareDictationAsync();
        await preparing;

        // Assert: the preparation completed without a service to prepare
        Assert.True(preparing.IsCompletedSuccessfully);
    }

    /// <summary>
    ///     Test that another project, or no project, is another conversation: the transcript is dropped.
    /// </summary>
    [Fact]
    public async Task ChatPanelViewModel_ProjectChanges_DropsTheTranscript()
    {
        // Arrange: a sent message
        _composer.InsertTyped("one");
        await _panel.SendCommand.ExecuteAsync(null);
        Assert.Single(_panel.Transcript);
        var announced = new List<string?>();
        _panel.PropertyChanged += (_, e) => announced.Add(e.PropertyName);

        // Act: open another project
        _composer.SetProject(Directory.CreateDirectory(Path.Combine(_root, "other")).FullName);

        // Assert: the transcript is empty and says so, and it is empty again after sending in the new project
        Assert.Empty(_panel.Transcript);
        Assert.False(_panel.HasTranscript);
        Assert.Contains(nameof(ChatPanelViewModel.HasTranscript), announced);
        _composer.InsertTyped("two");
        await _panel.SendCommand.ExecuteAsync(null);
        Assert.Equal("two", Assert.Single(_panel.SentEntries).Items[0].Text);

        // Act / Assert: closing the project drops it as well
        _composer.SetProject(null);
        Assert.Empty(_panel.Transcript);
    }

    /// <summary>
    ///     Test that the command of a message with a payload shows the inspector for exactly that payload.
    /// </summary>
    [Fact]
    public async Task TranscriptEntryViewModel_ViewPayloadCommand_ShowsInspectorForThePayloadOfTheMessage()
    {
        // Arrange: a message whose payload is a real payload, and a launcher that records what it was asked to show
        var payload = ChatTestPayloads.WithMarks(1, 2);
        using var composer = new ComposerViewModel(new PayloadMessageBuilder(payload), _marks, _dictation);
        composer.SetProject(_project);
        using var panel = new ChatPanelViewModel(composer, _marks, new AnnotationOverlayViewModel(_marks, _source, _ => { }), _markImages);
        var launcher = Substitute.For<IPayloadInspectorLauncher>();
        PayloadInspectorViewModel? shown = null;
        launcher.ShowPayloadInspectorAsync(Arg.Do<PayloadInspectorViewModel>(viewModel => shown = viewModel)).Returns(Task.CompletedTask);
        panel.InspectorLauncher = launcher;
        composer.InsertTyped("look");
        await composer.SendCommand.ExecuteAsync(null);
        var entry = Assert.Single(panel.SentEntries);

        // Act: choose "View payload"
        Assert.True(entry.HasPayload);
        Assert.True(entry.ViewPayloadCommand.CanExecute(null));
        await entry.ViewPayloadCommand.ExecuteAsync(null);

        // Assert: the launcher got an inspector that holds that payload and its description
        Assert.NotNull(shown);
        Assert.Same(payload, shown.Payload);
        Assert.Equal(PayloadDescriber.Describe(payload), shown.Description);
    }

    /// <summary>
    ///     Test that the command of a message does nothing while nothing can show the inspector, and cannot run for a
    ///     message without a payload.
    /// </summary>
    [Fact]
    public async Task TranscriptEntryViewModel_ViewPayloadCommand_NoLauncherOrNoPayload_DoesNothing()
    {
        // Arrange: a message with a payload and a panel that has no launcher, and a message without a payload
        var payload = ChatTestPayloads.OfText("plain");
        using var composer = new ComposerViewModel(new PayloadMessageBuilder(payload), _marks, _dictation);
        composer.SetProject(_project);
        using var panel = new ChatPanelViewModel(composer, _marks, new AnnotationOverlayViewModel(_marks, _source, _ => { }), _markImages);
        composer.InsertTyped("plain");
        await composer.SendCommand.ExecuteAsync(null);
        var withPayload = Assert.Single(panel.SentEntries);
        _composer.InsertTyped("no payload");
        await _panel.SendCommand.ExecuteAsync(null);
        var withoutPayload = Assert.Single(_panel.SentEntries);

        // Act: choose "View payload" on both
        await withPayload.ViewPayloadCommand.ExecuteAsync(null);

        // Assert: the first completes without a launcher, and the second cannot run at all
        Assert.True(withPayload.HasPayload);
        Assert.False(withoutPayload.HasPayload);
        Assert.False(withoutPayload.ViewPayloadCommand.CanExecute(null));
    }

    /// <summary>
    ///     Test that the entry of a message says what it carried besides words, in the singular and the plural.
    /// </summary>
    [Fact]
    public void TranscriptEntryViewModel_Summary_MarksAndAttachments_AreCountedWithTheirNouns()
    {
        // Arrange: a message with two marks and one attached file, as the composer snapshot holds it
        var document = new ComposerDocument();
        document.InsertChip(0, new MarkChip(Guid.NewGuid(), 1));
        document.InsertChip(1, new MarkChip(Guid.NewGuid(), 2));
        var file = new FileChip(Path.Combine(_root, "notes.txt"));
        document.InsertChip(2, file);
        var staged = new StagedAttachment(file, file.FullPath, "attachments/notes.txt", AttachmentKind.File, "notes.txt", 5, 1, true, 0);
        var sent = new SentMessage(document.Snapshot(), [], [staged], "payload", FakeMessageBuilder.Now);

        // Act: make the entry
        using var entry = new TranscriptEntryViewModel(sent, _project, () => null);

        // Assert: the summary counts both
        Assert.Equal("2 marks, 1 attachment", entry.Summary);
        Assert.Equal(3, entry.Items.Count);
        Assert.All(entry.Items, item => Assert.True(item.IsChip));
    }

    /// <summary>
    ///     Test that an attached picture of a sent message is previewed from its copy in the project, not from the file the
    ///     user dropped.
    /// </summary>
    [Fact]
    public void TranscriptEntryViewModel_Items_AttachedPicture_AsksForTheCopyInTheProject()
    {
        // Arrange: a picture that was copied into the project's attachments folder when the message was sent
        var document = new ComposerDocument();
        var picture = new ImageChip(Path.Combine(_root, "elsewhere", "photo.png"));
        document.InsertChip(0, picture);
        var staged = new StagedAttachment(picture, picture.FullPath, "attachments/photo.png", AttachmentKind.Image, "photo.png", 5, 1, true, 0);
        var sent = new SentMessage(document.Snapshot(), [], [staged], "payload", FakeMessageBuilder.Now);

        // Act: make the entry
        using var entry = new TranscriptEntryViewModel(sent, _project, () => null);

        // Assert: the chip shown is the original, and the picture is asked for from the copy in the project
        var item = Assert.Single(entry.Items);
        Assert.Same(picture, item.Chip);
        var copy = Assert.IsType<ImageChip>(item.ThumbnailChip);
        Assert.Equal(Path.GetFullPath(Path.Combine(_project, "attachments", "photo.png")), Path.GetFullPath(copy.FullPath));
    }

    /// <summary>
    ///     Test that a disposed panel no longer follows the composer, so a message sent afterwards is not added and a
    ///     request from a mark's chip to change its note opens nothing on the layer.
    /// </summary>
    [Fact]
    public async Task ChatPanelViewModel_Dispose_StopsFollowingTheComposer()
    {
        // Arrange: a panel that is disposed
        using var composer = new ComposerViewModel(_builder, _marks, _dictation);
        composer.SetProject(_project);
        using var annotation = new AnnotationOverlayViewModel(_marks, _source, _ => { });
        var panel = new ChatPanelViewModel(composer, _marks, annotation, _markImages);
        panel.Dispose();

        // Act: ask to change the note of a mark from its chip
        var mark = _marks.Add(_source.CurrentImage!, MarkKind.Rectangle, [new RenderPoint(5, 5), new RenderPoint(40, 30)], "note");
        composer.AddMark(mark);
        annotation.SetActiveMarkIds(composer.MarkChipIds);
        var requested = composer.RequestMarkNoteEdit(mark.Id);

        // Assert: the composer passed the request on, but the layer did not follow
        Assert.True(requested);
        Assert.False(annotation.IsNoteEditing);

        // Act: send a message in the composer
        composer.InsertTyped("later");
        await composer.SendCommand.ExecuteAsync(null);

        // Assert: the transcript did not follow
        Assert.Empty(panel.Transcript);
    }

    /// <summary>
    ///     Test that the provider of chip pictures refuses a missing function and gives no picture for a chip that has
    ///     none, without throwing.
    /// </summary>
    [Fact]
    public async Task ChipThumbnailProvider_GetThumbnail_NoPictureOrFailure_GivesNullAndDoesNotThrow()
    {
        // Arrange: a provider whose function has no picture for one mark and fails for another
        var missing = new MarkChip(Guid.NewGuid(), 1);
        var failing = new MarkChip(Guid.NewGuid(), 2);
        using var provider = new ChipThumbnailProvider(chip => chip.Number == 1 ? null : throw new InvalidOperationException("boom"));
        Assert.Throws<ArgumentNullException>(() => new ChipThumbnailProvider(null!));

        // Act: ask for the pictures, and for a folder chip, which only the file provider can answer
        var none = await provider.GetThumbnailAsync(missing, TestContext.Current.CancellationToken);
        var broken = await provider.GetThumbnailAsync(failing, TestContext.Current.CancellationToken);
        var folder = await provider.GetThumbnailAsync(new FolderChip(_root), TestContext.Current.CancellationToken);

        // Assert: nobody got a picture and nothing was thrown
        Assert.Null(none);
        Assert.Null(broken);
        Assert.Null(folder);
    }
}
