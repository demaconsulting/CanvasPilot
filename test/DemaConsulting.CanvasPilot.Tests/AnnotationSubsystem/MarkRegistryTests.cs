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
///     Unit tests for <see cref="MarkRegistry" /> and the <see cref="MarkGeometry" /> it makes.
/// </summary>
public sealed class MarkRegistryTests
{
    /// <summary>
    ///     A time provider that always says the same time.
    /// </summary>
    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        /// <inheritdoc />
        public override DateTimeOffset GetUtcNow() => now;
    }

    /// <summary>
    ///     The corners of a drag from the top-left to the bottom-right.
    /// </summary>
    private static readonly RenderPoint[] Drag = [new(10, 20), new(60, 50)];

    /// <summary>
    ///     Test that a mark keeps the very picture it was drawn on, its shape and number, and the time it was made.
    /// </summary>
    [Fact]
    public void MarkRegistry_Add_Rectangle_PinsMarkToPictureAndNumbersItOne()
    {
        // Arrange: a registry with a fixed clock and a picture
        var now = new DateTimeOffset(2026, 11, 20, 9, 30, 0, TimeSpan.Zero);
        var registry = new MarkRegistry(new FixedTimeProvider(now));
        var picture = AnnotationTestPictures.Picture();

        // Act: draw a rectangle
        var mark = registry.Add(picture, MarkKind.Rectangle, Drag);

        // Assert: the mark holds the same picture object and the picture's identity, number one, and the clock's time
        Assert.Same(picture, mark.Snapshot);
        Assert.Equal(picture.SnapshotKey, mark.Snapshot.SnapshotKey);
        Assert.Equal(1, mark.Number);
        Assert.Equal(MarkKind.Rectangle, mark.Kind);
        Assert.Equal(now, mark.CreatedUtc);
        Assert.NotEqual(Guid.Empty, mark.Id);
        Assert.Same(mark, registry.Find(mark.Id));
        Assert.Null(registry.Find(Guid.NewGuid()));
    }

    /// <summary>
    ///     Test that marks are numbered one, two, three in the order they are made, whatever picture they are on.
    /// </summary>
    [Fact]
    public void MarkRegistry_Add_SeveralMarks_NumbersThemInOrderOfCreation()
    {
        // Arrange: a registry and two pictures of the same file
        var registry = new MarkRegistry();
        var first = AnnotationTestPictures.Picture(fill: 10);
        var second = AnnotationTestPictures.Picture(fill: 20);

        // Act: make three marks, the last on the second picture
        var a = registry.Add(first, MarkKind.Rectangle, Drag);
        var b = registry.Add(first, MarkKind.Ellipse, Drag);
        var c = registry.Add(second, MarkKind.Arrow, Drag);

        // Assert: the numbers are 1, 2, 3 and the registry lists the marks in that order
        Assert.Equal([1, 2, 3], new[] { a.Number, b.Number, c.Number });
        Assert.Equal([a, b, c], registry.Marks);
        Assert.Equal(3, new HashSet<Guid>([a.Id, b.Id, c.Id]).Count);
    }

    /// <summary>
    ///     Test that a mark whose chip was removed from the message keeps its number, so the next mark does not reuse it.
    /// </summary>
    [Fact]
    public void MarkRegistry_Add_AfterMarkWasHidden_DoesNotReuseItsNumber()
    {
        // Arrange: two marks, the first of which has no chip in the message any more
        var registry = new MarkRegistry();
        var picture = AnnotationTestPictures.Picture();
        var first = registry.Add(picture, MarkKind.Rectangle, Drag);
        var second = registry.Add(picture, MarkKind.Rectangle, Drag);

        // Act: only the second mark's chip is in the message; draw another mark
        var active = registry.ActiveMarks([second.Id]);
        var third = registry.Add(picture, MarkKind.Rectangle, Drag);

        // Assert: the active list holds only the second mark with its number, and the new mark continues the numbering
        Assert.Equal([second], active);
        Assert.Equal(1, first.Number);
        Assert.Equal(3, third.Number);
    }

    /// <summary>
    ///     Test that only marks whose chips are in the message are active, in the order of their numbers, and that
    ///     repeated and unknown identities are ignored.
    /// </summary>
    [Fact]
    public void MarkRegistry_ActiveMarks_ChipsPresent_ReturnsOnlyThoseMarksInNumberOrder()
    {
        // Arrange: three marks and the identities of two chips, listed out of order, with a repeat and a stranger
        var registry = new MarkRegistry();
        var picture = AnnotationTestPictures.Picture();
        var a = registry.Add(picture, MarkKind.Rectangle, Drag);
        var b = registry.Add(picture, MarkKind.Rectangle, Drag);
        var c = registry.Add(picture, MarkKind.Rectangle, Drag);

        // Act: ask for the marks of the chips
        var active = registry.ActiveMarks([c.Id, a.Id, c.Id, Guid.NewGuid()]);

        // Assert: the marks of the two chips, ordered by number
        Assert.Equal([a, c], active);
        Assert.DoesNotContain(b, active);
        Assert.Empty(registry.ActiveMarks([]));
    }

    /// <summary>
    ///     Test that a picture shows only the active marks that were drawn on exactly that picture, while the marks of an
    ///     earlier version of the file stay in the message.
    /// </summary>
    [Fact]
    public void MarkRegistry_VisibleMarks_NewPictureOfSameFile_HidesMarksOfTheEarlierPicture()
    {
        // Arrange: two pictures of the same file with different pixels, and a mark on each
        var registry = new MarkRegistry();
        var earlier = AnnotationTestPictures.Picture(fill: 10);
        var later = AnnotationTestPictures.Picture(fill: 20);
        var onEarlier = registry.Add(earlier, MarkKind.Rectangle, Drag);
        var onLater = registry.Add(later, MarkKind.Ellipse, Drag);
        var chips = new[] { onEarlier.Id, onLater.Id };

        // Act: ask what each picture shows and what the message holds
        var shownOnEarlier = registry.VisibleMarks(chips, earlier.SnapshotKey);
        var shownOnLater = registry.VisibleMarks(chips, later.SnapshotKey);
        var inMessage = registry.ActiveMarks(chips);

        // Assert: each picture shows its own mark only, and both marks are still in the message
        Assert.Equal([onEarlier], shownOnEarlier);
        Assert.Equal([onLater], shownOnLater);
        Assert.Equal([onEarlier, onLater], inMessage);
        Assert.NotEqual(earlier.SnapshotKey, later.SnapshotKey);
    }

    /// <summary>
    ///     Test that a picture that is drawn again with the same pixels counts as the same picture, so the marks stay
    ///     visible when nothing visible changed.
    /// </summary>
    [Fact]
    public void MarkRegistry_VisibleMarks_SamePixelsDrawnAgain_ShowsTheMarks()
    {
        // Arrange: a mark on a picture, and a second picture object with the same file and pixels
        var registry = new MarkRegistry();
        var picture = AnnotationTestPictures.Picture(fill: 40);
        var again = AnnotationTestPictures.Picture(fill: 40);
        var mark = registry.Add(picture, MarkKind.Rectangle, Drag);

        // Act: ask what the new picture shows
        var shown = registry.VisibleMarks([mark.Id], again.SnapshotKey);

        // Assert: the mark is shown
        Assert.NotSame(picture, again);
        Assert.Equal([mark], shown);
    }

    /// <summary>
    ///     Test that clearing, which is what sending does, removes every mark and starts the numbering again.
    /// </summary>
    [Fact]
    public void MarkRegistry_Clear_RemovesEveryMarkAndRestartsNumbering()
    {
        // Arrange: a registry with two marks
        var registry = new MarkRegistry();
        var picture = AnnotationTestPictures.Picture();
        var old = registry.Add(picture, MarkKind.Rectangle, Drag);
        registry.Add(picture, MarkKind.Rectangle, Drag);

        // Act: clear and draw a mark
        registry.Clear();
        var afterClear = registry.Add(picture, MarkKind.Rectangle, Drag);

        // Assert: only the new mark exists and it is number one; the old one cannot be found
        Assert.Equal([afterClear], registry.Marks);
        Assert.Equal(1, afterClear.Number);
        Assert.Null(registry.Find(old.Id));
        Assert.Empty(registry.ActiveMarks([old.Id]));
    }

    /// <summary>
    ///     Test that the list of marks is a copy that later marks do not change.
    /// </summary>
    [Fact]
    public void MarkRegistry_Marks_ReturnsCopyThatLaterAddsDoNotChange()
    {
        // Arrange: a registry with one mark and a copy of its list
        var registry = new MarkRegistry();
        var picture = AnnotationTestPictures.Picture();
        registry.Add(picture, MarkKind.Rectangle, Drag);
        var copy = registry.Marks;

        // Act: add another mark and clear
        registry.Add(picture, MarkKind.Rectangle, Drag);
        registry.Clear();

        // Assert: the copy still holds the first mark only
        Assert.Single(copy);
        Assert.Empty(registry.Marks);
    }

    /// <summary>
    ///     Test that a drag from the bottom-right to the top-left gives the same rectangle as the opposite drag, with
    ///     the top-left corner first.
    /// </summary>
    [Theory]
    [InlineData(MarkKind.Rectangle)]
    [InlineData(MarkKind.Ellipse)]
    public void MarkRegistry_Add_ShapeDraggedUpAndLeft_NormalizesCorners(MarkKind kind)
    {
        // Arrange: a registry and a picture
        var registry = new MarkRegistry();
        var picture = AnnotationTestPictures.Picture();

        // Act: drag from the bottom-right corner to the top-left corner
        var mark = registry.Add(picture, kind, [new RenderPoint(60, 50), new RenderPoint(10, 20)]);

        // Assert: the corners are top-left then bottom-right, and the box is the same as for the opposite drag
        Assert.Equal([new RenderPoint(10, 20), new RenderPoint(60, 50)], mark.Geometry.Points);
        Assert.Equal(new RenderBox(10, 20, 50, 30), mark.Geometry.BoundingBox);
    }

    /// <summary>
    ///     Test that coordinates outside the picture are moved to its edge, so every mark lies inside its picture.
    /// </summary>
    [Fact]
    public void MarkRegistry_Add_PointsOutsidePicture_AreClampedToPictureBounds()
    {
        // Arrange: a 200 x 100 picture and a drag that starts outside it on the left and ends below it
        var registry = new MarkRegistry();
        var picture = AnnotationTestPictures.Picture(width: 200, height: 100);

        // Act: draw the rectangle
        var mark = registry.Add(picture, MarkKind.Rectangle, [new RenderPoint(-30, 10), new RenderPoint(150, 400)]);

        // Assert: the corners are on the left edge and the bottom edge
        Assert.Equal([new RenderPoint(0, 10), new RenderPoint(150, 100)], mark.Geometry.Points);
        Assert.Equal(new RenderBox(0, 10, 150, 90), mark.Geometry.BoundingBox);
    }

    /// <summary>
    ///     Test that an arrow keeps its tail first and its head last, and its bounding box.
    /// </summary>
    [Fact]
    public void MarkRegistry_Add_Arrow_KeepsTailAndHeadOnly()
    {
        // Arrange: a registry and a picture
        var registry = new MarkRegistry();
        var picture = AnnotationTestPictures.Picture();

        // Act: draw an arrow with points between its ends
        var mark = registry.Add(picture, MarkKind.Arrow, [new RenderPoint(100, 80), new RenderPoint(95, 70), new RenderPoint(40, 20)]);

        // Assert: tail then head, and the box around both
        Assert.Equal([new RenderPoint(100, 80), new RenderPoint(40, 20)], mark.Geometry.Points);
        Assert.Equal(new RenderBox(40, 20, 60, 60), mark.Geometry.BoundingBox);
    }

    /// <summary>
    ///     Test that a long freehand stroke is simplified to at most the limit of points, keeping its first and last
    ///     point.
    /// </summary>
    [Fact]
    public void MarkRegistry_Add_LongFreehandStroke_IsSimplifiedKeepingEndPoints()
    {
        // Arrange: a registry, a large picture, and a wavy stroke of 5000 points
        var registry = new MarkRegistry();
        var picture = AnnotationTestPictures.Picture(width: 5000, height: 400);
        var stroke = Enumerable.Range(0, 5000).Select(i => new RenderPoint(i, 200 + (150 * Math.Sin(i / 7.0)))).ToArray();

        // Act: draw the freehand mark
        var mark = registry.Add(picture, MarkKind.Freehand, stroke);

        // Assert: no more than the limit, the ends are those of the stroke, and the shape still spans the stroke
        Assert.InRange(mark.Geometry.Points.Length, 2, MarkGeometry.MaximumFreehandPoints);
        Assert.Equal(stroke[0], mark.Geometry.Points[0]);
        Assert.Equal(stroke[^1], mark.Geometry.Points[^1]);
        Assert.Equal(0, mark.Geometry.BoundingBox.X);
        Assert.Equal(4999, mark.Geometry.BoundingBox.X + mark.Geometry.BoundingBox.Width);
        Assert.True(mark.Geometry.BoundingBox.Height > 280);
    }

    /// <summary>
    ///     Test that a freehand stroke along a straight line keeps only its two ends and that repeated points are dropped.
    /// </summary>
    [Fact]
    public void MarkRegistry_Add_StraightFreehandStroke_CollapsesToItsEnds()
    {
        // Arrange: a registry, a picture, and a straight stroke with repeated points
        var registry = new MarkRegistry();
        var picture = AnnotationTestPictures.Picture();
        var stroke = new[] { new RenderPoint(10, 10), new RenderPoint(10, 10), new RenderPoint(30, 10), new RenderPoint(50, 10), new RenderPoint(50, 10) };

        // Act: draw the mark
        var mark = registry.Add(picture, MarkKind.Freehand, stroke);

        // Assert: only the two ends remain and the box is a line
        Assert.Equal([new RenderPoint(10, 10), new RenderPoint(50, 10)], mark.Geometry.Points);
        Assert.Equal(new RenderBox(10, 10, 40, 0), mark.Geometry.BoundingBox);
    }

    /// <summary>
    ///     Test that a click, a stroke that never moved, and a shape thinner than a pixel are refused and leave the
    ///     registry unchanged.
    /// </summary>
    [Theory]
    [InlineData(MarkKind.Rectangle, 30, 30, 30, 30)]
    [InlineData(MarkKind.Rectangle, 30, 30, 30.5, 90)]
    [InlineData(MarkKind.Ellipse, 30, 30, 90, 30.2)]
    [InlineData(MarkKind.Arrow, 30, 30, 30.4, 30)]
    [InlineData(MarkKind.Freehand, 30, 30, 30, 30)]
    public void MarkRegistry_Add_ShapeSmallerThanAPixel_ThrowsAndAddsNothing(MarkKind kind, double x1, double y1, double x2, double y2)
    {
        // Arrange: a registry and a picture
        var registry = new MarkRegistry();
        var picture = AnnotationTestPictures.Picture();

        // Act / Assert: the shape is refused, and nothing was added
        Assert.Throws<ArgumentException>(() => registry.Add(picture, kind, [new RenderPoint(x1, y1), new RenderPoint(x2, y2)]));
        Assert.Empty(registry.Marks);
    }

    /// <summary>
    ///     Test that a mark needs two points with finite coordinates and a known shape.
    /// </summary>
    [Fact]
    public void MarkRegistry_Add_InvalidArguments_Throw()
    {
        // Arrange: a registry and a picture
        var registry = new MarkRegistry();
        var picture = AnnotationTestPictures.Picture();

        // Act / Assert: each invalid argument is refused with its own exception
        Assert.Throws<ArgumentNullException>(() => registry.Add(null!, MarkKind.Rectangle, Drag));
        Assert.Throws<ArgumentNullException>(() => registry.Add(picture, MarkKind.Rectangle, null!));
        Assert.Throws<ArgumentException>(() => registry.Add(picture, MarkKind.Rectangle, [new RenderPoint(1, 1)]));
        Assert.Throws<ArgumentException>(() => registry.Add(picture, MarkKind.Rectangle, [new RenderPoint(double.NaN, 1), new RenderPoint(5, 5)]));
        Assert.Throws<ArgumentException>(() => registry.Add(picture, MarkKind.Rectangle, [new RenderPoint(1, 1), new RenderPoint(double.PositiveInfinity, 5)]));
        Assert.Throws<ArgumentOutOfRangeException>(() => registry.Add(picture, (MarkKind)99, Drag));
        Assert.Throws<ArgumentNullException>(() => registry.ActiveMarks(null!));
        Assert.Throws<ArgumentNullException>(() => registry.VisibleMarks([], null!));
        Assert.Empty(registry.Marks);
    }

    /// <summary>
    ///     Test that a note is kept as one clean line of at most 200 characters, and that a note of nothing is no note.
    /// </summary>
    [Fact]
    public void MarkRegistry_Add_Note_IsOneCleanLineOfAtMostTwoHundredCharacters()
    {
        // Arrange: a registry and a picture
        var registry = new MarkRegistry();
        var picture = AnnotationTestPictures.Picture();

        // Act: add marks with a plain note, a note with line breaks and tabs, a long note, a blank note, and no note
        var plain = registry.Add(picture, MarkKind.Rectangle, Drag, "  move this box  ");
        var multiline = registry.Add(picture, MarkKind.Rectangle, Drag, "first\r\nsecond\tthird\u0007");
        var longNote = registry.Add(picture, MarkKind.Rectangle, Drag, new string('x', 300));
        var blank = registry.Add(picture, MarkKind.Rectangle, Drag, " \r\n\t ");
        var none = registry.Add(picture, MarkKind.Rectangle, Drag);

        // Assert: trimmed, one line, cut at 200, and nothing for a blank or missing note
        Assert.Equal("move this box", plain.Note);
        Assert.Equal("first  second third", multiline.Note);
        Assert.Equal(new string('x', MarkRegistry.MaximumNoteLength), longNote.Note);
        Assert.Null(blank.Note);
        Assert.Null(none.Note);
    }

    /// <summary>
    ///     Test that cutting a note never leaves half of a character that needs two UTF-16 code units.
    /// </summary>
    [Fact]
    public void MarkRegistry_CleanNote_CutInsideSurrogatePair_DropsTheWholeCharacter()
    {
        // Arrange: 199 letters followed by a character outside the basic plane, which is two code units
        var note = new string('a', 199) + "\U0001F600" + "tail";

        // Act: clean the note
        var cleaned = MarkRegistry.CleanNote(note);

        // Assert: the 199 letters remain and the pair is removed whole
        Assert.Equal(new string('a', 199), cleaned);
        Assert.Null(MarkRegistry.CleanNote(null));
    }

    /// <summary>
    ///     Test that a mark needs a number of at least one, a shape, and a picture.
    /// </summary>
    [Fact]
    public void Mark_New_InvalidArguments_Throw()
    {
        // Arrange: valid parts
        var picture = AnnotationTestPictures.Picture();
        var geometry = MarkGeometry.Create(MarkKind.Rectangle, Drag, picture.Width, picture.Height);

        // Act / Assert: each invalid part is refused
        Assert.Throws<ArgumentOutOfRangeException>(() => new Mark(Guid.NewGuid(), 0, geometry, null, picture, DateTimeOffset.UtcNow));
        Assert.Throws<ArgumentNullException>(() => new Mark(Guid.NewGuid(), 1, null!, null, picture, DateTimeOffset.UtcNow));
        Assert.Throws<ArgumentNullException>(() => new Mark(Guid.NewGuid(), 1, geometry, null, null!, DateTimeOffset.UtcNow));
        Assert.Throws<ArgumentOutOfRangeException>(() => MarkGeometry.Create(MarkKind.Rectangle, Drag, 0, 10));
        Assert.Throws<ArgumentOutOfRangeException>(() => MarkGeometry.Create(MarkKind.Rectangle, Drag, 10, 0));
    }

    /// <summary>
    ///     Test that changing the note of a mark keeps its identity, number, geometry, picture, time, and place in the
    ///     order, changes only the note, and tells the observers once with the changed mark.
    /// </summary>
    [Fact]
    public void MarkRegistry_ChangeNote_KnownMark_ChangesOnlyTheNoteAndTellsObservers()
    {
        // Arrange: three marks, the second with a note, and an observer of note changes
        var now = new DateTimeOffset(2026, 11, 24, 8, 0, 0, TimeSpan.Zero);
        var registry = new MarkRegistry(new FixedTimeProvider(now));
        var picture = AnnotationTestPictures.Picture();
        var first = registry.Add(picture, MarkKind.Rectangle, Drag);
        var second = registry.Add(picture, MarkKind.Ellipse, Drag, "old note");
        var third = registry.Add(picture, MarkKind.Arrow, Drag);
        var raised = new List<Mark>();
        registry.NoteChanged += (_, e) => raised.Add(e.Mark);

        // Act: change the note of the second mark
        var changed = registry.ChangeNote(second.Id, "new note");

        // Assert: the changed mark is a new instance of the same mark with the new note, in the same place, told once
        Assert.NotNull(changed);
        Assert.NotSame(second, changed);
        Assert.Equal(second.Id, changed.Id);
        Assert.Equal(second.Number, changed.Number);
        Assert.Same(second.Geometry, changed.Geometry);
        Assert.Same(second.Snapshot, changed.Snapshot);
        Assert.Equal(second.CreatedUtc, changed.CreatedUtc);
        Assert.Equal("new note", changed.Note);
        Assert.Equal([first, changed, third], registry.Marks);
        Assert.Same(changed, registry.Find(second.Id));
        Assert.Equal([changed], raised);

        // Act: take the note away
        var cleared = registry.ChangeNote(second.Id, null);

        // Assert: the mark has no note any more, and the observers were told again
        Assert.Null(cleared!.Note);
        Assert.Equal(2, raised.Count);
    }

    /// <summary>
    ///     Test that a changed note is cleaned and cut as the note of a new mark is.
    /// </summary>
    [Fact]
    public void MarkRegistry_ChangeNote_NoteIsCleanedAndCappedLikeANewNote()
    {
        // Arrange: a mark without a note
        var registry = new MarkRegistry();
        var mark = registry.Add(AnnotationTestPictures.Picture(), MarkKind.Rectangle, Drag);

        // Act: give it a note with line breaks and tabs, then one that is too long, then one of only spaces
        var multiLine = registry.ChangeNote(mark.Id, "  two\r\nlines\twith tab  ");
        var tooLong = registry.ChangeNote(mark.Id, new string('x', 300));
        var blank = registry.ChangeNote(mark.Id, "   \n ");

        // Assert: one trimmed line, cut to the limit, and no note for blank text
        Assert.Equal(MarkRegistry.CleanNote("  two\r\nlines\twith tab  "), multiLine!.Note);
        Assert.Equal(MarkRegistry.MaximumNoteLength, tooLong!.Note!.Length);
        Assert.Null(blank!.Note);
    }

    /// <summary>
    ///     Test that a note that is the same once cleaned, or a mark the registry does not hold, changes nothing and tells
    ///     nobody.
    /// </summary>
    [Fact]
    public void MarkRegistry_ChangeNote_SameNoteOrUnknownMark_ChangesNothingAndTellsNobody()
    {
        // Arrange: a mark with a note, and an observer
        var registry = new MarkRegistry();
        var mark = registry.Add(AnnotationTestPictures.Picture(), MarkKind.Rectangle, Drag, "keep");
        var raised = 0;
        registry.NoteChanged += (_, _) => raised++;

        // Act: give it the same note with spaces around it, and change the note of a mark that is not there
        var same = registry.ChangeNote(mark.Id, "  keep ");
        var unknown = registry.ChangeNote(Guid.NewGuid(), "anything");

        // Assert: the same instance comes back, nothing is found for the stranger, and nobody was told
        Assert.Same(mark, same);
        Assert.Null(unknown);
        Assert.Equal([mark], registry.Marks);
        Assert.Equal(0, raised);
    }

    /// <summary>
    ///     Test that the marks and lists handed out before a note changed keep the note they had, as a sent message
    ///     needs.
    /// </summary>
    [Fact]
    public void MarkRegistry_ChangeNote_EarlierListsAndMarks_StayAsTheyWere()
    {
        // Arrange: a mark with a note, and the lists handed out before the change
        var registry = new MarkRegistry();
        var picture = AnnotationTestPictures.Picture();
        var mark = registry.Add(picture, MarkKind.Rectangle, Drag, "before");
        var copy = registry.Marks;
        var active = registry.ActiveMarks([mark.Id]);
        var visible = registry.VisibleMarks([mark.Id], picture.SnapshotKey);

        // Act: change the note
        registry.ChangeNote(mark.Id, "after");

        // Assert: what was handed out still says "before", and only what is asked for now says "after"
        Assert.Equal("before", mark.Note);
        Assert.Equal("before", copy[0].Note);
        Assert.Equal("before", active[0].Note);
        Assert.Equal("before", visible[0].Note);
        Assert.Equal("after", registry.ActiveMarks([mark.Id])[0].Note);
        Assert.Throws<ArgumentNullException>(() => new MarkNoteChangedEventArgs(null!));
    }
}
