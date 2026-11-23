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
using DemaConsulting.CanvasPilot.ProjectSubsystem;
using NSubstitute;

namespace DemaConsulting.CanvasPilot.Tests;

/// <summary>
///     Unit tests for <see cref="FollowUpSuggestions" />, the planner of the prompts that the chat panel offers, and for how
///     the panel follows it: ways to start while the project has no drawing, ways to refine once it has one, and nothing
///     while the copilot works.
/// </summary>
public sealed class FollowUpSuggestionsTests : IDisposable
{
    /// <summary>
    ///     How long a test waits for a turn that should end.
    /// </summary>
    private static readonly TimeSpan TurnTimeout = TimeSpan.FromSeconds(10);

    /// <summary>
    ///     Temporary folder that holds the projects.
    /// </summary>
    private readonly string _root = Directory.CreateTempSubdirectory("canvaspilot-suggestions-tests-").FullName;

    /// <summary>
    ///     The copilot, which answers when the test says so.
    /// </summary>
    private readonly ControlledAgentService _agent = new();

    /// <summary>
    ///     The picture that the preview shows, which a test sets.
    /// </summary>
    private readonly FakeRenderedImageSource _picture = new();

    /// <summary>
    ///     The composer of the message.
    /// </summary>
    private readonly ComposerViewModel _composer;

    /// <summary>
    ///     The annotation layer.
    /// </summary>
    private readonly AnnotationOverlayViewModel _annotation;

    /// <summary>
    ///     The chat panel under test, which looks in the project folder for a drawing.
    /// </summary>
    private readonly ChatPanelViewModel _panel;

    /// <summary>
    ///     Initializes a new instance of the <see cref="FollowUpSuggestionsTests" /> class with a chat panel in an open, empty
    ///     project.
    /// </summary>
    public FollowUpSuggestionsTests()
    {
        var marks = new MarkRegistry();
        _composer = new ComposerViewModel(new PayloadTextBuilder(), marks, new FakeDictationService());
        _annotation = new AnnotationOverlayViewModel(marks, _picture, _ => { });
        _panel = new ChatPanelViewModel(
            _composer,
            marks,
            _annotation,
            Substitute.For<IMarkImageComposer>(),
            agent: _agent,
            clock: new ManualTimeProvider(),
            pictureFilter: new CanvasNetImageSourceFilter());
        _composer.SetProject(NewProject("project"));
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _panel.Dispose();
        _annotation.Dispose();
        _composer.Dispose();
        _agent.Dispose();
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    /// <summary>
    ///     Test that a project with no drawing is offered ways to start, that a project with a drawing is offered ways to
    ///     refine it, that the export is offered only while a picture is shown, and that nothing is offered while the
    ///     copilot works.
    /// </summary>
    [Fact]
    public void FollowUpSuggestions_Plan_FollowsDrawingPictureAndBusy()
    {
        // Act and assert
        Assert.Equal(FollowUpSuggestions.StarterPrompts, FollowUpSuggestions.Plan(hasDrawing: false, hasPictureShown: false, busy: false));
        Assert.Contains("Draw a flowchart of our release process", FollowUpSuggestions.StarterPrompts);
        Assert.Contains("Make a title card for a launch announcement", FollowUpSuggestions.StarterPrompts);
        Assert.Equal(FollowUpSuggestions.RefinementPrompts, FollowUpSuggestions.Plan(hasDrawing: true, hasPictureShown: false, busy: false));
        Assert.Equal(
            [.. FollowUpSuggestions.RefinementPrompts, FollowUpSuggestions.ExportPrompt],
            FollowUpSuggestions.Plan(hasDrawing: true, hasPictureShown: true, busy: false));
        Assert.Empty(FollowUpSuggestions.Plan(hasDrawing: true, hasPictureShown: true, busy: true));
        Assert.Empty(FollowUpSuggestions.Plan(hasDrawing: false, hasPictureShown: false, busy: true));
    }

    /// <summary>
    ///     Test that an empty project offers the starter prompts, which the empty conversation shows, and not the
    ///     refinements, and that the prompts above the message are not shown before there is a conversation.
    /// </summary>
    [Fact]
    public void ChatPanelViewModel_EmptyProject_OffersStarterPrompts()
    {
        // Act and assert
        Assert.Equal(FollowUpSuggestions.StarterPrompts, _panel.Suggestions);
        Assert.True(_panel.HasSuggestions);
        Assert.False(_panel.ShowSuggestions);
    }

    /// <summary>
    ///     Test that a conversation in a project that still has no drawing keeps the starter prompts above the message once
    ///     the turn has ended, offers nothing while the turn runs, and offers the refinements after the copilot wrote a
    ///     drawing, announcing each change.
    /// </summary>
    [Fact]
    public async Task ChatPanelViewModel_TurnWritesDrawing_SuggestionsHideWhileBusyThenBecomeRefinements()
    {
        // Arrange
        var token = TestContext.Current.CancellationToken;
        var announced = new List<string?>();
        _panel.PropertyChanged += (_, e) => announced.Add(e.PropertyName);
        _composer.InsertTyped("Hello");

        // Act: a turn that only talks, then a turn that writes a drawing
        await _panel.SendCommand.ExecuteAsync(null);
        var whileBusy = (_panel.ShowSuggestions, _panel.Suggestions.Count);
        _agent.Emit(new AgentEvent.TextDelta("Hi"), new AgentEvent.Completed());
        await _panel.WaitForTurnAsync().WaitAsync(TurnTimeout, token);
        var afterTalk = (_panel.ShowSuggestions, _panel.Suggestions);
        _composer.InsertTyped("Draw a box");
        await _panel.SendCommand.ExecuteAsync(null);
        _agent.RaiseFileWritten(new FileWrittenEventArgs("box.svg", created: true));
        var busyAgain = _panel.ShowSuggestions;
        _agent.Emit(new AgentEvent.Completed());
        await _panel.WaitForTurnAsync().WaitAsync(TurnTimeout, token);

        // Assert
        Assert.Equal((false, 0), whileBusy);
        Assert.True(afterTalk.ShowSuggestions);
        Assert.Equal(FollowUpSuggestions.StarterPrompts, afterTalk.Suggestions);
        Assert.False(busyAgain);
        Assert.True(_panel.ShowSuggestions);
        Assert.Equal(FollowUpSuggestions.RefinementPrompts, _panel.Suggestions);
        Assert.Contains(nameof(ChatPanelViewModel.Suggestions), announced);
        Assert.Contains(nameof(ChatPanelViewModel.ShowSuggestions), announced);
    }

    /// <summary>
    ///     Test that a file that is not a drawing does not give the project one.
    /// </summary>
    [Fact]
    public async Task ChatPanelViewModel_TurnWritesNotes_StillOffersStarterPrompts()
    {
        // Arrange
        var token = TestContext.Current.CancellationToken;
        _composer.InsertTyped("Write notes");
        await _panel.SendCommand.ExecuteAsync(null);

        // Act
        _agent.RaiseFileWritten(new FileWrittenEventArgs("notes.txt", created: true));
        _agent.Emit(new AgentEvent.Completed());
        await _panel.WaitForTurnAsync().WaitAsync(TurnTimeout, token);

        // Assert
        Assert.Equal(FollowUpSuggestions.StarterPrompts, _panel.Suggestions);
    }

    /// <summary>
    ///     Test that a project that already holds a drawing (also in a folder inside it, but not in the folders of the
    ///     attachments and the style kits) is offered the refinements when it is opened, that another project is judged by
    ///     its own folder, and that a drawing that the copilot wrote does not follow the user into another project.
    /// </summary>
    [Fact]
    public async Task ChatPanelViewModel_OpenProject_WithDrawingOffersRefinementsAndEachProjectIsJudgedAlone()
    {
        // Arrange: projects with a nested drawing, with a picture that is only an attachment, and an empty one
        var token = TestContext.Current.CancellationToken;
        var drawn = NewProject("drawn");
        Directory.CreateDirectory(Path.Combine(drawn, "diagrams"));
        await File.WriteAllTextAsync(Path.Combine(drawn, "diagrams", "flow.svg"), "<svg/>", token);
        var attached = NewProject("attached");
        Directory.CreateDirectory(Path.Combine(attached, "attachments"));
        await File.WriteAllTextAsync(Path.Combine(attached, "attachments", "photo.svg"), "<svg/>", token);
        var empty = NewProject("empty");

        // Act
        _composer.SetProject(drawn);
        var inDrawn = _panel.Suggestions;
        _composer.SetProject(attached);
        var inAttached = _panel.Suggestions;
        _composer.InsertTyped("Draw");
        await _panel.SendCommand.ExecuteAsync(null);
        _agent.RaiseFileWritten(new FileWrittenEventArgs("a.svg", created: true));
        _agent.Emit(new AgentEvent.Completed());
        await _panel.WaitForTurnAsync().WaitAsync(TurnTimeout, TestContext.Current.CancellationToken);
        var afterWrite = _panel.Suggestions;
        _composer.SetProject(empty);
        var inEmpty = _panel.Suggestions;

        // Assert
        Assert.Equal(FollowUpSuggestions.RefinementPrompts, inDrawn);
        Assert.Equal(FollowUpSuggestions.StarterPrompts, inAttached);
        Assert.Equal(FollowUpSuggestions.RefinementPrompts, afterWrite);
        Assert.Equal(FollowUpSuggestions.StarterPrompts, inEmpty);
    }

    /// <summary>
    ///     Test that a picture that is shown gives the project a drawing and adds the export to the prompts, and that the
    ///     prompts go back when the picture goes away.
    /// </summary>
    [Fact]
    public void ChatPanelViewModel_PictureShown_OffersRefinementsWithExport()
    {
        // Arrange
        var before = _panel.Suggestions;

        // Act: a picture is shown, and goes away
        _picture.CurrentImage = AnnotationTestPictures.Picture();
        var shown = _panel.Suggestions;
        _picture.CurrentImage = null;
        var gone = _panel.Suggestions;

        // Assert
        Assert.Equal(FollowUpSuggestions.StarterPrompts, before);
        Assert.Equal([.. FollowUpSuggestions.RefinementPrompts, FollowUpSuggestions.ExportPrompt], shown);
        Assert.Equal(FollowUpSuggestions.StarterPrompts, gone);
    }

    /// <summary>
    ///     Makes a project folder under the temporary root.
    /// </summary>
    /// <param name="name">The name of the folder.</param>
    /// <returns>The full path of the folder.</returns>
    private string NewProject(string name) => Directory.CreateDirectory(Path.Combine(_root, name)).FullName;

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
