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
using DemaConsulting.CanvasPilot.PreviewSubsystem;

namespace DemaConsulting.CanvasPilot.Tests;

/// <summary>
///     Subsystem tests for the Annotation subsystem: marks on pictures that the real render service drew, and the
///     parts of the source file under them.
/// </summary>
public sealed class AnnotationSubsystemTests : IDisposable
{
    /// <summary>
    ///     Temporary folder that is the project and holds the files that are drawn.
    /// </summary>
    private readonly string _project = Directory.CreateTempSubdirectory("canvaspilot-annotation-tests-").FullName;

    /// <summary>
    ///     The service that draws the files, the real one.
    /// </summary>
    private readonly ImageRenderService _renderer = new();

    /// <inheritdoc />
    public void Dispose()
    {
        if (Directory.Exists(_project))
        {
            Directory.Delete(_project, recursive: true);
        }
    }

    /// <summary>
    ///     Test that marks drawn on a rendered diagram are numbered in order and keep their geometry in the pixels of the
    ///     picture and their note, whatever size the mark was drawn at.
    /// </summary>
    [Fact]
    public async Task AnnotationSubsystem_MarkPicture_MarksOnRenderedDiagram_AreNumberedAndKeepPixelGeometryAndNote()
    {
        // Arrange: a drawn diagram and a registry
        var picture = await RenderAsync("flow.mmd", "flowchart LR\n  A[Start] --> B[End]\n");
        var registry = new MarkRegistry();

        // Act: draw a rectangle with a note, drag an ellipse from the bottom-right to the top-left, and draw an arrow
        var first = registry.Add(picture, MarkKind.Rectangle, [new RenderPoint(5, 6), new RenderPoint(40.5, 30)], "  this one \n please ");
        var second = registry.Add(picture, MarkKind.Ellipse, [new RenderPoint(90, 50), new RenderPoint(30, 20)]);
        var third = registry.Add(picture, MarkKind.Arrow, [new RenderPoint(1, 1), new RenderPoint(50, 25)]);

        // Assert: numbers one to three, geometry in picture pixels, the cleaned note, and the picture they were drawn on
        Assert.Equal([1, 2, 3], new[] { first.Number, second.Number, third.Number });
        Assert.Equal(new RenderBox(5, 6, 35.5, 24), first.Geometry.BoundingBox);
        Assert.Equal([new RenderPoint(30, 20), new RenderPoint(90, 50)], second.Geometry.Points);
        Assert.Equal([new RenderPoint(1, 1), new RenderPoint(50, 25)], third.Geometry.Points);
        Assert.Equal("this one   please", first.Note);
        Assert.Null(second.Note);
        Assert.All(registry.Marks, mark => Assert.Same(picture, mark.Snapshot));
    }

    /// <summary>
    ///     Test that after the file changes and a new picture is drawn, the marks of the earlier picture stay with it, are
    ///     not shown on the new picture, and are still part of the message while their chips are, and that the next mark
    ///     continues the numbering on the new picture.
    /// </summary>
    [Fact]
    public async Task AnnotationSubsystem_MarkPicture_FileChanged_MarksStayOnTheirOwnPicture()
    {
        // Arrange: a diagram with a mark, then the file is edited and drawn again
        var before = await RenderAsync("flow.mmd", "flowchart LR\n  A[Start] --> B[End]\n");
        var registry = new MarkRegistry();
        var oldMark = registry.Add(before, MarkKind.Rectangle, [new RenderPoint(10, 10), new RenderPoint(50, 40)]);
        var after = await RenderAsync("flow.mmd", "flowchart LR\n  A[Start] --> B[Finish] --> C[Done]\n");

        // Act: draw a mark on the new picture and ask what each picture shows and what the message holds
        var newMark = registry.Add(after, MarkKind.Rectangle, [new RenderPoint(10, 10), new RenderPoint(50, 40)]);
        var chips = new[] { oldMark.Id, newMark.Id };
        var shownOnBefore = registry.VisibleMarks(chips, before.SnapshotKey);
        var shownOnAfter = registry.VisibleMarks(chips, after.SnapshotKey);
        var inMessage = registry.ActiveMarks(chips);

        // Assert: each picture shows only its own mark, both marks are in the message, the old mark still holds the
        // old pixels, and the numbering continued
        Assert.NotEqual(before.SnapshotKey, after.SnapshotKey);
        Assert.Equal([oldMark], shownOnBefore);
        Assert.Equal([newMark], shownOnAfter);
        Assert.Equal([oldMark, newMark], inMessage);
        Assert.Same(before, oldMark.Snapshot);
        Assert.Equal([1, 2], inMessage.Select(mark => mark.Number));
    }

    /// <summary>
    ///     Test that a mark drawn around a node of a rendered Mermaid diagram finds that node, with its text and its line
    ///     in the source file, from the positions the renderer reported.
    /// </summary>
    [Fact]
    public async Task AnnotationSubsystem_FindSourceElements_MermaidNode_IsReportedWithTextAndLine()
    {
        // Arrange: a drawn diagram and a mark around its node 'Start' with a margin of 3 pixels
        var picture = await RenderAsync("flow.mmd", "flowchart LR\n  A[Start] --> B[End]\n");
        var node = picture.Positions.First(position => position is { Kind: "node", Text: "Start" });
        var box = node.Box!.Value;
        var registry = new MarkRegistry();
        var mark = registry.Add(picture, MarkKind.Rectangle, [new RenderPoint(box.X - 3, box.Y - 3), new RenderPoint(box.X + box.Width + 3, box.Y + box.Height + 3)]);

        // Act: find the parts of the source under the mark
        var result = MarkSourceResolver.Resolve(mark);

        // Assert: the node is first and carries what the renderer knows about it
        Assert.True(result.IsAvailable);
        var found = Assert.Single(result.Elements, element => element.Kind == "node");
        Assert.Equal("Start", found.Text);
        Assert.Equal(node.Id, found.Id);
        Assert.Equal(node.Line, found.Line);
        Assert.Equal(node.Box, found.Box);
        Assert.DoesNotContain(result.Elements, element => element.Text == "End");
    }

    /// <summary>
    ///     Test that a mark over an anchor of a rendered OpenSCAD model finds the anchor by its name and pixel.
    /// </summary>
    [Fact]
    public async Task AnnotationSubsystem_FindSourceElements_ScadAnchor_IsReportedWithPixel()
    {
        // Arrange: a drawn model with one anchor and a mark around the anchor's pixel
        var picture = await RenderAsync("part.scad", "cube([10, 10, 10]);\necho(\"anchor\", \"top\", [5, 5, 10]);\n");
        var anchor = Assert.Single(picture.Anchors);
        var registry = new MarkRegistry();
        var x = anchor.PixelX!.Value;
        var y = anchor.PixelY!.Value;
        var mark = registry.Add(picture, MarkKind.Ellipse, [new RenderPoint(x - 20, y - 20), new RenderPoint(x + 20, y + 20)]);

        // Act: find the parts of the source under the mark
        var result = MarkSourceResolver.Resolve(mark);

        // Assert: the anchor, by name, at its pixel
        Assert.True(result.IsAvailable);
        var found = Assert.Single(result.Elements);
        Assert.Equal("anchor", found.Kind);
        Assert.Equal("top", found.Id);
        Assert.Equal(new RenderPoint(x, y), found.Pixel);
    }

    /// <summary>
    ///     Test that a mark on a picture of a kind that knows nothing about its elements is told so, with the reason.
    /// </summary>
    [Fact]
    public async Task AnnotationSubsystem_FindSourceElements_SvgPicture_SaysNotAvailable()
    {
        // Arrange: a drawn SVG file and a mark on it
        var picture = await RenderAsync("logo.svg", "<svg xmlns='http://www.w3.org/2000/svg' width='64' height='32'><rect width='64' height='32' fill='#336699'/></svg>");
        var mark = new MarkRegistry().Add(picture, MarkKind.Rectangle, [new RenderPoint(4, 4), new RenderPoint(40, 20)]);

        // Act: find the parts of the source under the mark
        var result = MarkSourceResolver.Resolve(mark);

        // Assert: not available, with the format in the reason, and no elements
        Assert.False(result.IsAvailable);
        Assert.Equal("svg provides no element positions", result.UnavailableReason);
        Assert.Empty(result.Elements);
    }

    /// <summary>
    ///     Test that the overlay draws numbered marks on a rendered diagram with the four tools and a note, pinned to the
    ///     rendered picture with the parts of the source under each mark found from them, and that the picture cannot be
    ///     dragged out while a tool is chosen.
    /// </summary>
    [Fact]
    public async Task AnnotationSubsystem_DrawMarks_OverlayOnRenderedDiagram_MakesNumberedMarksPinnedToThePicture()
    {
        // Arrange: a drawn diagram that is shown, a registry, and an overlay that reports to a drag-out switch
        var picture = await RenderAsync("flow.mmd", "flowchart LR\n  A[Start] --> B[End]\n");
        var source = new FakeRenderedImageSource { CurrentImage = picture };
        var registry = new MarkRegistry();
        var dragOut = new List<bool>();
        using var overlay = new AnnotationOverlayViewModel(registry, source, dragOut.Add);
        var start = picture.Positions.First(position => position is { Kind: "node", Text: "Start" });
        var box = start.Box!.Value;

        // Act: draw a rectangle around the first node with a note, an ellipse, a freehand line, and an arrow, then no tool
        overlay.SelectTool(AnnotationTool.Rectangle);
        overlay.BeginStroke(new RenderPoint(box.X - 4, box.Y - 4));
        overlay.EndStroke(new RenderPoint(box.X + box.Width + 4, box.Y + box.Height + 4));
        overlay.NoteText = "rename this";
        overlay.CommitNote();
        overlay.SelectTool(AnnotationTool.Ellipse);
        overlay.BeginStroke(new RenderPoint(2, 2));
        overlay.EndStroke(new RenderPoint(12, 12));
        overlay.SkipNote();
        overlay.SelectTool(AnnotationTool.Freehand);
        overlay.BeginStroke(new RenderPoint(1, 1));
        overlay.ContinueStroke(new RenderPoint(8, 20));
        overlay.EndStroke(new RenderPoint(30, 4));
        overlay.SkipNote();
        overlay.SelectTool(AnnotationTool.Arrow);
        overlay.BeginStroke(new RenderPoint(1, 2));
        overlay.EndStroke(new RenderPoint(20, 14));
        overlay.SkipNote();
        overlay.SelectTool(AnnotationTool.None);

        // Assert: four marks in order with their kinds, the note, and the very picture; the first one covers the node it was
        // drawn around; and dragging out was off while the tools were in use and is on again
        Assert.Equal([MarkKind.Rectangle, MarkKind.Ellipse, MarkKind.Freehand, MarkKind.Arrow], registry.Marks.Select(mark => mark.Kind));
        Assert.Equal([1, 2, 3, 4], registry.Marks.Select(mark => mark.Number));
        Assert.Equal("rename this", registry.Marks[0].Note);
        Assert.All(registry.Marks, mark => Assert.Same(picture, mark.Snapshot));
        var found = Assert.Single(MarkSourceResolver.Resolve(registry.Marks[0]).Elements, element => element.Kind == "node");
        Assert.Equal(start.Text, found.Text);
        Assert.Equal(start.Id, found.Id);
        Assert.Equal([false, false, false, false, true], dragOut);
    }

    /// <summary>
    ///     Test that after the file changes the overlay shows only the marks of the picture that is shown, keeps the others
    ///     in the message, and shows them again with their picture.
    /// </summary>
    [Fact]
    public async Task AnnotationSubsystem_DrawMarks_FileChanged_OverlayShowsOnlyMarksOfTheShownPicture()
    {
        // Arrange: a diagram that is shown, with a mark drawn on it whose chip is in the message
        var before = await RenderAsync("flow.mmd", "flowchart LR\n  A[Start] --> B[End]\n");
        var source = new FakeRenderedImageSource { CurrentImage = before };
        var registry = new MarkRegistry();
        using var overlay = new AnnotationOverlayViewModel(registry, source);
        var chips = new List<Guid>();
        overlay.MarkAdded += (_, e) =>
        {
            chips.Add(e.Mark.Id);
            overlay.SetActiveMarkIds(chips);
        };
        overlay.SelectTool(AnnotationTool.Rectangle);
        overlay.BeginStroke(new RenderPoint(5, 5));
        overlay.EndStroke(new RenderPoint(40, 30));
        overlay.SkipNote();
        var shownBefore = overlay.VisibleMarks.Count;

        // Act: the file is edited and drawn again, a mark is drawn on the new picture, and the first picture is shown again
        var after = await RenderAsync("flow.mmd", "flowchart LR\n  A[Start] --> B[Finish] --> C[Done]\n");
        source.CurrentImage = after;
        var hidden = (overlay.VisibleMarks.Count, overlay.HiddenMarkCount);
        overlay.BeginStroke(new RenderPoint(5, 5));
        overlay.EndStroke(new RenderPoint(40, 30));
        overlay.SkipNote();
        var onNew = (overlay.VisibleMarks.Single().Number, overlay.HiddenMarkCount);
        source.CurrentImage = before;
        var backOnOld = (overlay.VisibleMarks.Single().Number, overlay.HiddenMarkCount);

        // Assert: each picture showed only its own mark, nothing was lost, and the numbers continued
        Assert.Equal(1, shownBefore);
        Assert.Equal((0, 1), hidden);
        Assert.Equal((2, 1), onNew);
        Assert.Equal((1, 1), backOnOld);
        Assert.Equal(2, registry.Marks.Length);
        Assert.Same(before, registry.Marks[0].Snapshot);
        Assert.Same(after, registry.Marks[1].Snapshot);
    }

    /// <summary>
    ///     Test that the marks the overlay shows on a rendered diagram carry their notes, a mark whose note was skipped
    ///     carries none, and that the place of each note label stays inside the picture.
    /// </summary>
    [Fact]
    public async Task AnnotationSubsystem_ShowMarkNotes_MarksOnRenderedDiagram_ShownMarksCarryTheirNotes()
    {
        // Arrange: a drawn diagram that is shown, a registry, and an overlay joined to a stand-in for the message
        var picture = await RenderAsync("flow.mmd", "flowchart LR\n  A[Start] --> B[End]\n");
        var source = new FakeRenderedImageSource { CurrentImage = picture };
        var registry = new MarkRegistry();
        using var overlay = new AnnotationOverlayViewModel(registry, source);
        var chips = new List<Guid>();
        overlay.MarkAdded += (_, e) =>
        {
            chips.Add(e.Mark.Id);
            overlay.SetActiveMarkIds(chips);
        };

        // Act: draw one mark with a note and one whose note is skipped
        overlay.SelectTool(AnnotationTool.Rectangle);
        overlay.BeginStroke(new RenderPoint(4, 4));
        overlay.EndStroke(new RenderPoint(40, 30));
        overlay.NoteText = "make this node bigger";
        overlay.CommitNote();
        overlay.BeginStroke(new RenderPoint(50, 4));
        overlay.EndStroke(new RenderPoint(90, 30));
        overlay.SkipNote();

        // Assert: the shown marks carry the note and no note, and each label would sit inside the picture
        Assert.Equal(new string?[] { "make this node bigger", null }, overlay.VisibleMarks.Select(mark => mark.Note));
        Assert.All(overlay.VisibleMarks, mark =>
        {
            var layout = AnnotationOverlayStyle.NoteLabel(mark.Geometry.BoundingBox, picture.Width, picture.Height);
            var outer = layout.LeftOfBadge ? layout.Edge - layout.MaxWidth : layout.Edge + layout.MaxWidth;
            Assert.InRange(outer, 0, picture.Width);
        });
    }

    /// <summary>
    ///     Test that changing the note of a mark through the overlay keeps the mark in the registry with its number,
    ///     shape, place, and picture, changes only its note, shows it at once, keeps it when cancelled, and does not
    ///     change a copy of the marks handed out before, as a sent message holds.
    /// </summary>
    [Fact]
    public async Task AnnotationSubsystem_EditMarkNotes_ThroughOverlay_RegistryKeepsTheMarkAndChangesOnlyItsNote()
    {
        // Arrange: a drawn diagram with a mark with a note whose chip is in the message, and a copy handed out
        var picture = await RenderAsync("flow.mmd", "flowchart LR\n  A[Start] --> B[End]\n");
        var source = new FakeRenderedImageSource { CurrentImage = picture };
        var registry = new MarkRegistry();
        using var overlay = new AnnotationOverlayViewModel(registry, source);
        overlay.SelectTool(AnnotationTool.Ellipse);
        overlay.BeginStroke(new RenderPoint(4, 4));
        overlay.EndStroke(new RenderPoint(40, 30));
        overlay.NoteText = "first";
        overlay.CommitNote();
        var mark = registry.Marks[0];
        overlay.SetActiveMarkIds([mark.Id]);
        var handedOut = registry.ActiveMarks([mark.Id]);
        overlay.SelectTool(AnnotationTool.None);

        // Act: change the note and confirm, then change it again and cancel
        overlay.BeginEditNote(mark.Id);
        overlay.NoteText = "second";
        overlay.CommitNote();
        var afterCommit = overlay.VisibleMarks.Single();
        overlay.BeginEditNote(mark.Id);
        overlay.NoteText = "third";
        overlay.Cancel();

        // Assert: the same mark with the new note, kept when cancelled, and the copy handed out before is unchanged
        var now = Assert.Single(registry.Marks);
        Assert.Equal((mark.Id, mark.Number, mark.Kind), (now.Id, now.Number, now.Kind));
        Assert.Equal(mark.Geometry.Points, now.Geometry.Points);
        Assert.Same(picture, now.Snapshot);
        Assert.Equal("second", afterCommit.Note);
        Assert.Equal("second", now.Note);
        Assert.Equal("first", handedOut[0].Note);
    }

    /// <summary>
    ///     Writes a file in the project and draws it with the real render service.
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
