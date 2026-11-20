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
using DemaConsulting.CanvasPilot.PayloadSubsystem;
using DemaConsulting.CanvasPilot.PreviewSubsystem;

namespace DemaConsulting.CanvasPilot.Tests;

/// <summary>
///     Unit tests for <see cref="AnnotationOverlayViewModel" />, the logic of the layer on which the user marks the shown
///     picture, and for the style that the layer shares with the annotated picture of a message.
/// </summary>
public sealed class AnnotationOverlayTests
{
    /// <summary>
    ///     The marks of the message.
    /// </summary>
    private readonly MarkRegistry _marks = new();

    /// <summary>
    ///     The source of the shown picture, which shows a picture of 200 x 100 pixels from the start.
    /// </summary>
    private readonly FakeRenderedImageSource _source = new() { CurrentImage = AnnotationTestPictures.Picture() };

    /// <summary>
    ///     What the overlay told the host about dragging the picture out, in order.
    /// </summary>
    private readonly List<bool> _dragOut = [];

    /// <summary>
    ///     The marks that the overlay reported, in order.
    /// </summary>
    private readonly List<Mark> _added = [];

    /// <summary>
    ///     Makes an overlay over the fields of the fixture and collects what it reports.
    /// </summary>
    /// <returns>The overlay.</returns>
    private AnnotationOverlayViewModel CreateOverlay()
    {
        var overlay = new AnnotationOverlayViewModel(_marks, _source, _dragOut.Add);
        overlay.MarkAdded += (_, e) => _added.Add(e.Mark);
        return overlay;
    }

    /// <summary>
    ///     Draws a shape with the pointer and makes its mark without a note.
    /// </summary>
    /// <param name="overlay">The overlay.</param>
    /// <param name="from">Where the pointer is pressed.</param>
    /// <param name="to">Where the pointer is released.</param>
    private static void Draw(AnnotationOverlayViewModel overlay, RenderPoint from, RenderPoint to)
    {
        overlay.BeginStroke(from);
        overlay.ContinueStroke(new RenderPoint((from.X + to.X) / 2, (from.Y + to.Y) / 2));
        overlay.EndStroke(to);
        overlay.SkipNote();
    }

    /// <summary>
    ///     Test that the overlay refuses missing arguments.
    /// </summary>
    [Fact]
    public void AnnotationOverlayViewModel_New_NullArguments_Throw()
    {
        // Arrange, Act & Assert: each missing part is refused
        Assert.Throws<ArgumentNullException>(() => new AnnotationOverlayViewModel(null!, _source));
        Assert.Throws<ArgumentNullException>(() => new AnnotationOverlayViewModel(_marks, null!));
        Assert.Throws<ArgumentNullException>(() => new AnnotationToolbarViewModel(null!));
        Assert.Throws<ArgumentNullException>(() => new MarkAddedEventArgs(null!));
    }

    /// <summary>
    ///     Test that no tool can be chosen while no picture is shown, and that the overlay says why.
    /// </summary>
    [Fact]
    public void AnnotationOverlayViewModel_SelectTool_NoPicture_StaysOnPointerAndSaysWhy()
    {
        // Arrange: an overlay while no picture is shown
        _source.CurrentImage = null;
        var overlay = CreateOverlay();
        var changed = new List<string?>();
        overlay.PropertyChanged += (_, e) => changed.Add(e.PropertyName);

        // Act: choose the rectangle tool
        overlay.SelectTool(AnnotationTool.Rectangle);

        // Assert: nothing is chosen, the tool buttons are told to go back, drawing starts nothing, and the text says why
        Assert.False(overlay.ToolsEnabled);
        Assert.Equal(AnnotationTool.None, overlay.CurrentTool);
        Assert.True(overlay.IsPointerSelected);
        Assert.Contains(nameof(AnnotationOverlayViewModel.IsRectangleSelected), changed);
        Assert.False(overlay.BeginStroke(new RenderPoint(5, 5)));
        Assert.Equal("Show a picture to mark it.", overlay.StatusText);
        Assert.Empty(_dragOut);
    }

    /// <summary>
    ///     Test that a tool can be chosen only while the host allows marks, and that choosing none always works.
    /// </summary>
    [Fact]
    public void AnnotationOverlayViewModel_SelectTool_DrawingNotAllowed_IsRefusedAndExplained()
    {
        // Arrange: an overlay whose host does not allow marks, as while no project is open
        var overlay = CreateOverlay();
        overlay.IsDrawingAllowed = false;

        // Act: choose a tool, and then no tool
        overlay.SelectTool(AnnotationTool.Arrow);
        var afterTool = overlay.CurrentTool;
        overlay.SelectTool(AnnotationTool.None);

        // Assert: the tool was refused with a reason, and none is chosen
        Assert.Equal(AnnotationTool.None, afterTool);
        Assert.False(overlay.ToolsEnabled);
        Assert.Equal("Open a project to mark the picture.", overlay.StatusText);
        Assert.Equal(AnnotationTool.None, overlay.CurrentTool);
    }

    /// <summary>
    ///     Test that choosing a tool turns dragging the picture out off, and choosing none turns it on again.
    /// </summary>
    [Fact]
    public void AnnotationOverlayViewModel_SelectTool_ToolThenNone_TurnsDragOutOffThenOn()
    {
        // Arrange: an overlay with a picture
        var overlay = CreateOverlay();

        // Act: choose the rectangle, switch to the arrow, and choose none
        overlay.SelectTool(AnnotationTool.Rectangle);
        overlay.SelectTool(AnnotationTool.Rectangle);
        overlay.SelectTool(AnnotationTool.Arrow);
        overlay.SelectTool(AnnotationTool.None);

        // Assert: the host was told only when the tool changed between none and a tool or between tools
        Assert.Equal([false, false, true], _dragOut);
        Assert.False(overlay.IsToolActive);
        Assert.True(overlay.IsPointerSelected);
    }

    /// <summary>
    ///     Test that the check boxes of the tools choose the tool, uncheck to no tool, and that the pointer cannot be
    ///     unchecked.
    /// </summary>
    [Fact]
    public void AnnotationOverlayViewModel_ToolFlags_SetFromCheckBoxes_ChooseAndRevert()
    {
        // Arrange: an overlay with a picture
        var overlay = CreateOverlay();
        var changed = new List<string?>();
        overlay.PropertyChanged += (_, e) => changed.Add(e.PropertyName);

        // Act: check the ellipse, check the freehand tool, clear its check, and clear the check of the pointer
        overlay.IsEllipseSelected = true;
        var ellipse = overlay.CurrentTool;
        overlay.IsFreehandSelected = true;
        var freehand = (overlay.CurrentTool, overlay.IsEllipseSelected, overlay.IsFreehandSelected);
        overlay.IsFreehandSelected = false;
        var none = overlay.CurrentTool;
        changed.Clear();
        overlay.IsPointerSelected = false;

        // Assert: each check chose its tool, clearing it chose none, and the pointer stayed checked and was announced again
        Assert.Equal(AnnotationTool.Ellipse, ellipse);
        Assert.Equal((AnnotationTool.Freehand, false, true), freehand);
        Assert.Equal(AnnotationTool.None, none);
        Assert.True(overlay.IsPointerSelected);
        Assert.Contains(nameof(AnnotationOverlayViewModel.IsPointerSelected), changed);
        Assert.Throws<ArgumentOutOfRangeException>(() => overlay.SelectTool((AnnotationTool)99));
    }

    /// <summary>
    ///     Test that a rectangle that is pressed, moved, and released is drawn in picture pixels, waits for its note, and
    ///     is made as the first mark pinned to the shown picture when the note is skipped.
    /// </summary>
    [Fact]
    public void AnnotationOverlayViewModel_Stroke_RectangleDragged_MakesNumberedMarkInPicturePixels()
    {
        // Arrange: an overlay with the rectangle tool
        var overlay = CreateOverlay();
        overlay.SelectTool(AnnotationTool.Rectangle);

        // Act: press at (30, 20), move, release at (80, 60)
        var started = overlay.BeginStroke(new RenderPoint(30, 20));
        overlay.ContinueStroke(new RenderPoint(55, 40));
        var drawing = (overlay.IsDrawing, overlay.HasDraft, overlay.DraftKind, _added.Count);
        var draft = overlay.DraftPoints.ToArray();
        overlay.EndStroke(new RenderPoint(80, 60));
        var waiting = (overlay.IsDrawing, overlay.IsNoteEditing, overlay.PendingNumber, _added.Count, _marks.Marks.Length);
        overlay.SkipNote();

        // Assert: the draft followed the pointer, the finished shape waited for the note, and then one mark was made
        Assert.True(started);
        Assert.Equal((true, true, MarkKind.Rectangle, 0), drawing);
        Assert.Equal([new RenderPoint(30, 20), new RenderPoint(55, 40)], draft);
        Assert.Equal((false, true, 1, 0, 0), waiting);
        var mark = Assert.Single(_added);
        Assert.Equal(1, mark.Number);
        Assert.Equal(MarkKind.Rectangle, mark.Kind);
        Assert.Equal([new RenderPoint(30, 20), new RenderPoint(80, 60)], mark.Geometry.Points);
        Assert.Null(mark.Note);
        Assert.Same(_source.CurrentImage, mark.Snapshot);
        Assert.Same(mark, _marks.Find(mark.Id));
        Assert.False(overlay.HasDraft);
        Assert.False(overlay.IsNoteEditing);
    }

    /// <summary>
    ///     Test that the ellipse and the arrow tools make marks of their own kind with the points they were drawn with.
    /// </summary>
    /// <param name="tool">The tool.</param>
    /// <param name="kind">The kind of the mark that it makes.</param>
    [Theory]
    [InlineData(AnnotationTool.Ellipse, MarkKind.Ellipse)]
    [InlineData(AnnotationTool.Arrow, MarkKind.Arrow)]
    public void AnnotationOverlayViewModel_Stroke_EllipseAndArrow_MakeMarksOfTheirKind(AnnotationTool tool, MarkKind kind)
    {
        // Arrange: an overlay with the tool
        var overlay = CreateOverlay();
        overlay.SelectTool(tool);

        // Act: draw from the lower right to the upper left
        Draw(overlay, new RenderPoint(150, 80), new RenderPoint(40, 25));

        // Assert: one mark of the kind, with corners in normal form for the ellipse and tail and head for the arrow
        var mark = Assert.Single(_added);
        Assert.Equal(kind, mark.Kind);
        var expected = kind == MarkKind.Ellipse
            ? new[] { new RenderPoint(40, 25), new RenderPoint(150, 80) }
            : [new RenderPoint(150, 80), new RenderPoint(40, 25)];
        Assert.Equal(expected, mark.Geometry.Points);
    }

    /// <summary>
    ///     Test that a freehand stroke keeps the path of the pointer, simplified, with its first and last point.
    /// </summary>
    [Fact]
    public void AnnotationOverlayViewModel_Stroke_Freehand_KeepsSimplifiedPathWithEnds()
    {
        // Arrange: an overlay with the freehand tool
        var overlay = CreateOverlay();
        overlay.SelectTool(AnnotationTool.Freehand);

        // Act: draw a straight line with many points, one of them so close to the last that it adds nothing
        overlay.BeginStroke(new RenderPoint(10, 10));
        for (var x = 11; x <= 100; x++)
        {
            overlay.ContinueStroke(new RenderPoint(x, 10));
        }

        overlay.ContinueStroke(new RenderPoint(100.1, 10.1));
        var drawn = overlay.DraftPoints.Count;
        overlay.EndStroke(new RenderPoint(100, 10));
        overlay.SkipNote();

        // Assert: the draft kept the points a pixel apart, and the mark is the straight line with its two ends
        Assert.Equal(91, drawn);
        var mark = Assert.Single(_added);
        Assert.Equal(MarkKind.Freehand, mark.Kind);
        Assert.Equal([new RenderPoint(10, 10), new RenderPoint(100, 10)], mark.Geometry.Points);
    }

    /// <summary>
    ///     Test that a click and a stroke of less than a pixel make no mark and say so.
    /// </summary>
    [Fact]
    public void AnnotationOverlayViewModel_Stroke_ClickWithoutDrag_MakesNoMarkAndSaysSo()
    {
        // Arrange: an overlay with the rectangle tool
        var overlay = CreateOverlay();
        overlay.SelectTool(AnnotationTool.Rectangle);

        // Act: press and release at the same point, and press and release 0.4 pixel away
        overlay.BeginStroke(new RenderPoint(30, 20));
        overlay.EndStroke(new RenderPoint(30, 20));
        var afterClick = (overlay.HasDraft, overlay.IsNoteEditing, overlay.StatusText);
        overlay.BeginStroke(new RenderPoint(30, 20));
        overlay.EndStroke(new RenderPoint(30.4, 20.4));

        // Assert: no note was asked for, no mark was made, and the text explains that a click is no mark
        Assert.Equal((false, false, "Drag to draw a mark; a click makes none."), afterClick);
        Assert.False(overlay.HasDraft);
        Assert.Empty(_added);
        Assert.Empty(_marks.Marks);
    }

    /// <summary>
    ///     Test that a shape that leaves the picture is cut to it, whatever direction it was drawn in.
    /// </summary>
    [Fact]
    public void AnnotationOverlayViewModel_Stroke_PointerOutsidePicture_IsClampedToPicture()
    {
        // Arrange: an overlay with the rectangle tool, over a picture of 200 x 100 pixels
        var overlay = CreateOverlay();
        overlay.SelectTool(AnnotationTool.Rectangle);

        // Act: start beyond the lower right corner, go beyond the upper left, and ignore a point that is not a number
        overlay.BeginStroke(new RenderPoint(300, 150));
        overlay.ContinueStroke(new RenderPoint(double.NaN, 5));
        var draft = overlay.DraftPoints.ToArray();
        overlay.EndStroke(new RenderPoint(-20, -10));
        overlay.SkipNote();

        // Assert: the draft and the mark use points inside the picture
        Assert.Equal([new RenderPoint(200, 100), new RenderPoint(200, 100)], draft);
        var mark = Assert.Single(_added);
        Assert.Equal([new RenderPoint(0, 0), new RenderPoint(200, 100)], mark.Geometry.Points);
        Assert.False(overlay.BeginStroke(new RenderPoint(double.PositiveInfinity, 1)));
    }

    /// <summary>
    ///     Test that no shape starts while no tool is chosen, so the pointer keeps its own function.
    /// </summary>
    [Fact]
    public void AnnotationOverlayViewModel_BeginStroke_NoToolChosen_StartsNothing()
    {
        // Arrange: an overlay with a picture and no tool
        var overlay = CreateOverlay();

        // Act: press, move, and release
        var started = overlay.BeginStroke(new RenderPoint(30, 20));
        overlay.ContinueStroke(new RenderPoint(60, 50));
        overlay.EndStroke(new RenderPoint(80, 60));

        // Assert: nothing was drawn or made
        Assert.False(started);
        Assert.False(overlay.HasDraft);
        Assert.Empty(_added);
        Assert.Equal("Choose a tool to mark the picture.", overlay.StatusText);
    }

    /// <summary>
    ///     Test that a note that is typed is kept with the mark, as one clean line cut to the length that a mark keeps.
    /// </summary>
    [Fact]
    public void AnnotationOverlayViewModel_Note_Typed_IsKeptCleanAndCappedAtTwoHundredCharacters()
    {
        // Arrange: a finished rectangle that waits for its note
        var overlay = CreateOverlay();
        overlay.SelectTool(AnnotationTool.Rectangle);
        overlay.BeginStroke(new RenderPoint(10, 10));
        overlay.EndStroke(new RenderPoint(60, 40));
        var prompt = overlay.StatusText;

        // Act: type a note with a line break that is longer than the limit, and confirm it
        overlay.NoteText = "first line\nsecond " + new string('x', 300);
        var typed = overlay.NoteText;
        overlay.CommitNote();

        // Assert: the text box content was cut to 200 characters, and the mark holds one line of at most 200
        Assert.Equal("Mark 1: add a short note, or skip it.", prompt);
        Assert.Equal(200, typed.Length);
        var mark = Assert.Single(_added);
        Assert.NotNull(mark.Note);
        Assert.Equal(200, mark.Note.Length);
        Assert.StartsWith("first line second xxx", mark.Note, StringComparison.Ordinal);
        Assert.DoesNotContain('\n', mark.Note);
        Assert.Equal(string.Empty, overlay.NoteText);
    }

    /// <summary>
    ///     Test that a note that is skipped makes the mark without it, even when text was typed.
    /// </summary>
    [Fact]
    public void AnnotationOverlayViewModel_Note_Skipped_MakesMarkWithoutNote()
    {
        // Arrange: a finished ellipse with a typed note
        var overlay = CreateOverlay();
        overlay.SelectTool(AnnotationTool.Ellipse);
        overlay.BeginStroke(new RenderPoint(10, 10));
        overlay.EndStroke(new RenderPoint(60, 40));
        overlay.NoteText = "not wanted";

        // Act: skip the note
        overlay.SkipNote();

        // Assert: the mark has no note
        Assert.Null(Assert.Single(_added).Note);
    }

    /// <summary>
    ///     Test that starting the next shape, or choosing another tool, keeps the finished shape with the note typed so far.
    /// </summary>
    [Fact]
    public void AnnotationOverlayViewModel_Note_NextShapeOrOtherToolChosen_KeepsPendingMarkWithTypedNote()
    {
        // Arrange: a finished rectangle that waits for its note, which is partly typed
        var overlay = CreateOverlay();
        overlay.SelectTool(AnnotationTool.Rectangle);
        overlay.BeginStroke(new RenderPoint(10, 10));
        overlay.EndStroke(new RenderPoint(60, 40));
        overlay.NoteText = "first";

        // Act: start the next shape, finish it, and then choose another tool while it waits for its note
        overlay.BeginStroke(new RenderPoint(70, 10));
        var secondPending = (overlay.IsDrawing, overlay.PendingNumber, _added.Count);
        overlay.EndStroke(new RenderPoint(120, 40));
        overlay.NoteText = "second";
        overlay.SelectTool(AnnotationTool.Arrow);

        // Assert: both marks were made with their notes, numbered one and two, and the second was pending as number two
        Assert.Equal((true, 2, 1), secondPending);
        Assert.Equal(["first", "second"], _added.Select(mark => mark.Note));
        Assert.Equal([1, 2], _added.Select(mark => mark.Number));
        Assert.False(overlay.IsNoteEditing);
        Assert.Equal(AnnotationTool.Arrow, overlay.CurrentTool);
    }

    /// <summary>
    ///     Test that a mark is in the registry, with the shown picture, when it is reported, so that whoever adds its chip
    ///     can find it.
    /// </summary>
    [Fact]
    public void AnnotationOverlayViewModel_MarkAdded_Raised_MarkIsAlreadyInRegistryAndStateIsClean()
    {
        // Arrange: an overlay whose observer looks at the registry and at the overlay when it is told
        var overlay = new AnnotationOverlayViewModel(_marks, _source);
        var seen = new List<(bool Found, bool Draft, bool Note)>();
        overlay.MarkAdded += (_, e) => seen.Add((ReferenceEquals(_marks.Find(e.Mark.Id), e.Mark), overlay.HasDraft, overlay.IsNoteEditing));
        overlay.SelectTool(AnnotationTool.Rectangle);

        // Act: draw a mark
        Draw(overlay, new RenderPoint(10, 10), new RenderPoint(60, 40));

        // Assert: it was found in the registry and the overlay had no shape under construction any more
        Assert.Equal([(true, false, false)], seen);
    }

    /// <summary>
    ///     Test that Escape throws away a shape that is being drawn and one that waits for its note, makes no mark, and
    ///     then chooses no tool, and that it has no effect when there is nothing to do.
    /// </summary>
    [Fact]
    public void AnnotationOverlayViewModel_Escape_DuringStrokeNoteAndIdle_CancelsThenLeavesTheTool()
    {
        // Arrange: an overlay with the rectangle tool
        var overlay = CreateOverlay();
        overlay.SelectTool(AnnotationTool.Rectangle);

        // Act: cancel during the stroke, cancel while the note waits, press Escape idle with a tool, and again without
        overlay.BeginStroke(new RenderPoint(10, 10));
        var duringStroke = (overlay.Escape(), overlay.HasDraft, overlay.CurrentTool);
        overlay.BeginStroke(new RenderPoint(10, 10));
        overlay.EndStroke(new RenderPoint(60, 40));
        var duringNote = (overlay.Escape(), overlay.HasDraft, overlay.IsNoteEditing, overlay.CurrentTool);
        var idleWithTool = (overlay.Escape(), overlay.CurrentTool);
        var idleWithoutTool = overlay.Escape();

        // Assert: shapes were thrown away with the tool kept, then the tool was left, then nothing happened
        Assert.Equal((true, false, AnnotationTool.Rectangle), duringStroke);
        Assert.Equal((true, false, false, AnnotationTool.Rectangle), duringNote);
        Assert.Equal((true, AnnotationTool.None), idleWithTool);
        Assert.False(idleWithoutTool);
        Assert.Empty(_added);
        Assert.Empty(_marks.Marks);
        Assert.Equal([false, true], _dragOut);
    }

    /// <summary>
    ///     Test that the cancel command works only while there is something to cancel.
    /// </summary>
    [Fact]
    public void AnnotationOverlayViewModel_CancelCommand_OnlyWhileShapeUnderConstruction()
    {
        // Arrange: an overlay with the arrow tool
        var overlay = CreateOverlay();
        overlay.SelectTool(AnnotationTool.Arrow);
        var idle = overlay.CancelCommand.CanExecute(null);

        // Act: start a shape and run the command, then finish another and check the note commands
        overlay.BeginStroke(new RenderPoint(10, 10));
        var drawing = overlay.CancelCommand.CanExecute(null);
        overlay.CancelCommand.Execute(null);
        overlay.BeginStroke(new RenderPoint(10, 10));
        overlay.EndStroke(new RenderPoint(90, 50));
        var noteCommands = (overlay.CommitNoteCommand.CanExecute(null), overlay.SkipNoteCommand.CanExecute(null));
        overlay.CommitNoteCommand.Execute(null);

        // Assert: the cancel command was available only while drawing, and the note commands only while the note waited
        Assert.False(idle);
        Assert.True(drawing);
        Assert.Equal((true, true), noteCommands);
        Assert.Single(_added);
        Assert.False(overlay.SkipNoteCommand.CanExecute(null));
    }

    /// <summary>
    ///     Test that a mark of the shown picture is shown only while its chip is in the message, that the marks of another
    ///     picture of the same file are hidden and counted, stay in the registry, and are shown again with their picture.
    /// </summary>
    [Fact]
    public void AnnotationOverlayViewModel_SetActiveMarkIds_NewPicture_HidesMarksOfEarlierPictureAndCountsThem()
    {
        // Arrange: a mark on the first picture and a mark on the second picture of the same file, both with chips
        var overlay = CreateOverlay();
        var first = _source.CurrentImage!;
        var second = AnnotationTestPictures.Picture(fill: 7);
        var onFirst = _marks.Add(first, MarkKind.Rectangle, [new RenderPoint(10, 10), new RenderPoint(50, 40)]);
        var onSecond = _marks.Add(second, MarkKind.Ellipse, [new RenderPoint(20, 20), new RenderPoint(70, 60)]);
        var withoutChip = _marks.Add(first, MarkKind.Arrow, [new RenderPoint(1, 1), new RenderPoint(30, 30)]);

        // Act: tell which chips are in the message, then show the second picture, then the first again
        overlay.SetActiveMarkIds([onFirst.Id, onSecond.Id]);
        var onFirstPicture = (string.Join(',', overlay.VisibleMarks.Select(mark => mark.Number)), overlay.HiddenMarkCount, overlay.HiddenMarksText);
        _source.CurrentImage = second;
        var onSecondPicture = (string.Join(',', overlay.VisibleMarks.Select(mark => mark.Number)), overlay.HiddenMarkCount, overlay.HasHiddenMarks, overlay.HiddenMarksText);
        _source.CurrentImage = first;
        var backOnFirst = string.Join(',', overlay.VisibleMarks.Select(mark => mark.Number));

        // Assert: only marks with chips and the shown picture are visible, the others are counted, and nothing is lost
        Assert.Equal(("1", 1, "1 mark of the message was made on another picture and is hidden here."), onFirstPicture);
        Assert.Equal(("2", 1, true, "1 mark of the message was made on another picture and is hidden here."), onSecondPicture);
        Assert.Equal("1", backOnFirst);
        Assert.Equal(3, _marks.Marks.Length);
        Assert.Equal(3, withoutChip.Number);
    }

    /// <summary>
    ///     Test that the same pixels drawn again show the marks again, that marks are hidden while no picture is shown, and
    ///     that several hidden marks are counted with the plural text.
    /// </summary>
    [Fact]
    public void AnnotationOverlayViewModel_SetActiveMarkIds_SamePixelsAgainOrNoPicture_ShowsOrHidesAllMarks()
    {
        // Arrange: two marks on a picture, both with chips
        var overlay = CreateOverlay();
        var first = _source.CurrentImage!;
        var one = _marks.Add(first, MarkKind.Rectangle, [new RenderPoint(10, 10), new RenderPoint(50, 40)]);
        var two = _marks.Add(first, MarkKind.Rectangle, [new RenderPoint(60, 10), new RenderPoint(90, 40)]);
        overlay.SetActiveMarkIds([one.Id, two.Id]);

        // Act: the file is drawn again with the same pixels, then no picture is shown
        _source.CurrentImage = AnnotationTestPictures.Picture();
        var again = overlay.VisibleMarks.Count;
        _source.CurrentImage = null;

        // Assert: the marks follow the pixels, and with no picture all of them are hidden and counted
        Assert.Equal(2, again);
        Assert.Empty(overlay.VisibleMarks);
        Assert.Equal(2, overlay.HiddenMarkCount);
        Assert.Equal("2 marks of the message were made on another picture and are hidden here.", overlay.HiddenMarksText);
        Assert.Throws<ArgumentNullException>(() => overlay.SetActiveMarkIds(null!));
    }

    /// <summary>
    ///     Test that a new picture with other pixels throws away a shape that was started on the old one, and that the
    ///     same pixels drawn again keep it.
    /// </summary>
    [Fact]
    public void AnnotationOverlayViewModel_PictureChanged_WhileDrawing_CancelsOnlyForOtherPixels()
    {
        // Arrange: a shape that is being drawn
        var overlay = CreateOverlay();
        overlay.SelectTool(AnnotationTool.Rectangle);
        overlay.BeginStroke(new RenderPoint(10, 10));

        // Act: the same pixels are drawn again, then other pixels
        _source.CurrentImage = AnnotationTestPictures.Picture();
        var sameKept = overlay.IsDrawing;
        _source.CurrentImage = AnnotationTestPictures.Picture(fill: 3);

        // Assert: the shape survived the first and not the second, and the tool is still chosen
        Assert.True(sameKept);
        Assert.False(overlay.IsDrawing);
        Assert.False(overlay.HasDraft);
        Assert.Equal(AnnotationTool.Rectangle, overlay.CurrentTool);
        Assert.Empty(_added);
    }

    /// <summary>
    ///     Test that a mark is pinned to the picture that was shown when its shape was started.
    /// </summary>
    [Fact]
    public void AnnotationOverlayViewModel_Stroke_PictureReplacedWithSamePixelsBeforeNote_MarkKeepsPictureOfFirstPress()
    {
        // Arrange: a finished shape that waits for its note
        var overlay = CreateOverlay();
        var first = _source.CurrentImage!;
        overlay.SelectTool(AnnotationTool.Rectangle);
        overlay.BeginStroke(new RenderPoint(10, 10));
        overlay.EndStroke(new RenderPoint(60, 40));

        // Act: the same pixels are drawn again as a new picture object, and the note is skipped
        _source.CurrentImage = AnnotationTestPictures.Picture();
        overlay.SkipNote();

        // Assert: the mark holds the picture on which it was started
        Assert.Same(first, Assert.Single(_added).Snapshot);
    }

    /// <summary>
    ///     Test that when no picture is shown any more a shape under construction is thrown away, the tool stays chosen for
    ///     the next picture, nothing can be drawn meanwhile, and drawing works again on the next picture.
    /// </summary>
    [Fact]
    public void AnnotationOverlayViewModel_PictureCleared_ShapeIsThrownAwayAndToolWaitsForNextPicture()
    {
        // Arrange: a tool is chosen and a shape is being drawn
        var overlay = CreateOverlay();
        overlay.SelectTool(AnnotationTool.Freehand);
        overlay.BeginStroke(new RenderPoint(10, 10));

        // Act: the picture goes away, a stroke is tried, and a new picture is shown
        _source.CurrentImage = null;
        var whileNone = (overlay.HasPicture, overlay.HasDraft, overlay.CurrentTool, overlay.BeginStroke(new RenderPoint(5, 5)), overlay.StatusText, overlay.PictureWidth);
        _source.CurrentImage = AnnotationTestPictures.Picture(fill: 9);
        var again = overlay.BeginStroke(new RenderPoint(5, 5));

        // Assert: nothing was drawn without a picture, the tool stayed and dragging out stayed off, and the next picture can be drawn on
        Assert.Equal((false, false, AnnotationTool.Freehand, false, "Show a picture to mark it.", 0), whileNone);
        Assert.True(again);
        Assert.Equal([false], _dragOut);
    }

    /// <summary>
    ///     Test that while the host does not allow marks any more, for example because the message is being sent, a shape is
    ///     thrown away and the tool is given up.
    /// </summary>
    [Fact]
    public void AnnotationOverlayViewModel_IsDrawingAllowed_TurnedOffWhileNoteWaits_ThrowsShapeAwayAndGivesUpTool()
    {
        // Arrange: a finished shape that waits for its note
        var overlay = CreateOverlay();
        overlay.SelectTool(AnnotationTool.Rectangle);
        overlay.BeginStroke(new RenderPoint(10, 10));
        overlay.EndStroke(new RenderPoint(60, 40));

        // Act: the host stops allowing marks, then allows them again
        overlay.IsDrawingAllowed = false;
        var off = (overlay.IsNoteEditing, overlay.CurrentTool, overlay.ToolsEnabled);
        overlay.IsDrawingAllowed = true;

        // Assert: no mark was made, the tool was given up, and the tools can be used again
        Assert.Equal((false, AnnotationTool.None, false), off);
        Assert.True(overlay.ToolsEnabled);
        Assert.Empty(_added);
        Assert.Equal([false, true], _dragOut);
    }

    /// <summary>
    ///     Test that the arrow keys and Enter draw a rectangle with the keyboard, starting in the middle of the picture,
    ///     with the same result as the pointer.
    /// </summary>
    [Fact]
    public void AnnotationOverlayViewModel_Keyboard_ArrowKeysAndEnter_DrawRectangle()
    {
        // Arrange: an overlay with the rectangle tool over a picture of 200 x 100 pixels
        var overlay = CreateOverlay();
        overlay.SelectTool(AnnotationTool.Rectangle);

        // Act: move the cursor, start the shape there, move on, and finish the shape
        var moved = overlay.MoveCursor(-AnnotationOverlayViewModel.CursorStep, 0);
        var cursor = overlay.KeyboardCursor;
        var started = overlay.ConfirmCursor();
        overlay.MoveCursor(AnnotationOverlayViewModel.LargeCursorStep, AnnotationOverlayViewModel.LargeCursorStep);
        overlay.MoveCursor(AnnotationOverlayViewModel.SmallCursorStep, 0);
        var finished = overlay.ConfirmCursor();
        overlay.SkipNote();

        // Assert: the cursor began at the center, and the mark runs from where Enter was pressed first to where it was pressed again
        Assert.True(moved);
        Assert.Equal(new RenderPoint(92, 50), cursor);
        Assert.True(started);
        Assert.True(finished);
        var mark = Assert.Single(_added);
        Assert.Equal([new RenderPoint(92, 50), new RenderPoint(133, 90)], mark.Geometry.Points);
    }

    /// <summary>
    ///     Test that the keyboard does nothing without a tool, that the cursor stays inside the picture, that the pointer
    ///     hides the cursor, and that the keys are left to the note while it waits.
    /// </summary>
    [Fact]
    public void AnnotationOverlayViewModel_Keyboard_NoToolOrNoteWaiting_DoesNothingAndCursorStaysInPicture()
    {
        // Arrange: an overlay with no tool
        var overlay = CreateOverlay();
        var withoutTool = (overlay.MoveCursor(1, 0), overlay.ConfirmCursor(), overlay.KeyboardCursor);

        // Act: choose a tool, push the cursor far out of the picture, use the pointer, and finish a shape so a note waits
        overlay.SelectTool(AnnotationTool.Arrow);
        overlay.MoveCursor(-5000, -5000);
        var corner = overlay.KeyboardCursor;
        overlay.BeginStroke(new RenderPoint(20, 20));
        var afterPointer = overlay.KeyboardCursor;
        overlay.EndStroke(new RenderPoint(90, 60));
        var whileNote = (overlay.MoveCursor(1, 0), overlay.ConfirmCursor());

        // Assert: nothing happened without a tool, the cursor stopped at the corner, the pointer hid it, and the note kept the keys
        Assert.Equal((false, false, (RenderPoint?)null), withoutTool);
        Assert.Equal(new RenderPoint(0, 0), corner);
        Assert.Null(afterPointer);
        Assert.Equal((false, false), whileNote);
        Assert.True(overlay.IsNoteEditing);
    }

    /// <summary>
    ///     Test that hiding the cursor and the cursor of a shape that is made with the keyboard are announced, and that a
    ///     shape that the keyboard started continues with every move of the cursor.
    /// </summary>
    [Fact]
    public void AnnotationOverlayViewModel_Keyboard_FreehandMovesAddPointsAndHideCursorStops()
    {
        // Arrange: an overlay with the freehand tool and a cursor at the center
        var overlay = CreateOverlay();
        overlay.SelectTool(AnnotationTool.Freehand);
        overlay.ConfirmCursor();

        // Act: move three times, and hide the cursor
        overlay.MoveCursor(10, 0);
        overlay.MoveCursor(0, 10);
        overlay.MoveCursor(10, 10);
        var points = overlay.DraftPoints.ToArray();
        overlay.HideCursor();
        overlay.HideCursor();

        // Assert: the draft has the start and the three moves, and the cursor is gone
        Assert.Equal([new RenderPoint(100, 50), new RenderPoint(110, 50), new RenderPoint(110, 60), new RenderPoint(120, 70)], points);
        Assert.Null(overlay.KeyboardCursor);
    }

    /// <summary>
    ///     Test that a freehand stroke longer than the limit keeps replacing its last point instead of growing.
    /// </summary>
    [Fact]
    public void AnnotationOverlayViewModel_Stroke_FreehandBeyondLimit_ReplacesLastPoint()
    {
        // Arrange: a freehand stroke that already holds the most points
        var overlay = CreateOverlay();
        overlay.SelectTool(AnnotationTool.Freehand);
        overlay.BeginStroke(new RenderPoint(0, 0));
        for (var i = 1; i < AnnotationOverlayViewModel.MaximumDraftPoints; i++)
        {
            overlay.ContinueStroke(new RenderPoint(i % 200, (i / 200) % 100 + (i % 2)));
        }

        var full = overlay.DraftPoints.Count;

        // Act: move on
        overlay.ContinueStroke(new RenderPoint(199, 99));
        overlay.ContinueStroke(new RenderPoint(5, 99));

        // Assert: the stroke did not grow and its last point follows the pointer
        Assert.Equal(AnnotationOverlayViewModel.MaximumDraftPoints, full);
        Assert.Equal(AnnotationOverlayViewModel.MaximumDraftPoints, overlay.DraftPoints.Count);
        Assert.Equal(new RenderPoint(5, 99), overlay.DraftPoints[^1]);
    }

    /// <summary>
    ///     Test that disposing the overlay gives the picture back to the pointer and stops it following the picture.
    /// </summary>
    [Fact]
    public void AnnotationOverlayViewModel_Dispose_ToolChosen_AllowsDragOutAndStopsFollowing()
    {
        // Arrange: an overlay with a tool
        var overlay = CreateOverlay();
        overlay.SelectTool(AnnotationTool.Ellipse);

        // Act: dispose it twice and change the picture
        overlay.Dispose();
        overlay.Dispose();
        var changed = new List<string?>();
        overlay.PropertyChanged += (_, e) => changed.Add(e.PropertyName);
        _source.CurrentImage = AnnotationTestPictures.Picture(fill: 1);

        // Assert: the host was told once that dragging out is allowed, and the overlay no longer reacts
        Assert.Equal([false, true], _dragOut);
        Assert.Empty(changed);
        Assert.Equal(AnnotationTool.None, overlay.CurrentTool);
    }

    /// <summary>
    ///     Test that the names of the tools read as words in sentences.
    /// </summary>
    [Fact]
    public void AnnotationOverlayViewModel_ToolName_EachTool_IsAWord()
    {
        // Arrange, Act & Assert: every tool has its own name
        Assert.Equal("rectangle", AnnotationOverlayViewModel.ToolName(AnnotationTool.Rectangle));
        Assert.Equal("ellipse", AnnotationOverlayViewModel.ToolName(AnnotationTool.Ellipse));
        Assert.Equal("freehand line", AnnotationOverlayViewModel.ToolName(AnnotationTool.Freehand));
        Assert.Equal("arrow", AnnotationOverlayViewModel.ToolName(AnnotationTool.Arrow));
        Assert.Equal("pointer", AnnotationOverlayViewModel.ToolName(AnnotationTool.None));
    }

    /// <summary>
    ///     Test that the status text tells what to do for each state of the tool.
    /// </summary>
    [Fact]
    public void AnnotationOverlayViewModel_StatusText_EachState_TellsWhatToDo()
    {
        // Arrange: an overlay with a picture
        var overlay = CreateOverlay();
        var noTool = overlay.StatusText;

        // Act: choose a tool, start a shape
        overlay.SelectTool(AnnotationTool.Arrow);
        var withTool = overlay.StatusText;
        overlay.BeginStroke(new RenderPoint(10, 10));
        var drawing = overlay.StatusText;

        // Assert: each state has its own sentence
        Assert.Equal("Choose a tool to mark the picture.", noTool);
        Assert.Equal("Drag on the picture to draw an arrow, or use the arrow keys and Enter. Escape stops.", withTool);
        Assert.Equal("Release to finish the mark. Escape cancels it.", drawing);
    }

    /// <summary>
    ///     Test that the numbers, colors, and sizes of marks on the screen are those of the annotated picture that is sent,
    ///     so that the user sees what the assistant gets.
    /// </summary>
    [Fact]
    public void AnnotationOverlayStyle_Values_EqualThoseOfMarkImageComposer()
    {
        // Arrange: a few picture sizes and mark numbers, and a box near a corner
        var sizes = new[] { (40, 30), (200, 100), (800, 600), (4000, 3000) };
        var box = new RenderBox(2, 3, 10, 10);

        // Act & Assert: every value equals the one that the composer uses
        for (var number = 1; number <= 8; number++)
        {
            Assert.Equal(MarkImageComposer.ColorOf(number), AnnotationOverlayStyle.ColorOf(number));
        }

        foreach (var (width, height) in sizes)
        {
            Assert.Equal(MarkImageComposer.StrokeWidth(width, height), AnnotationOverlayStyle.StrokeWidth(width, height));
            Assert.Equal(MarkImageComposer.BadgeRadius(width, height), AnnotationOverlayStyle.BadgeRadius(width, height));
            Assert.Equal(MarkImageComposer.BadgeCenter(box, width, height), AnnotationOverlayStyle.BadgeCenter(box, width, height));
        }
    }
}
