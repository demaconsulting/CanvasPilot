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
using DemaConsulting.CanvasPilot.ComposerSubsystem;
using DemaConsulting.CanvasPilot.PayloadSubsystem;
using DemaConsulting.CanvasPilot.PreviewSubsystem;

namespace DemaConsulting.CanvasPilot.Tests;

/// <summary>
///     Unit tests for <see cref="PayloadBuilder" />, with a composer that draws nothing and the real resolver of the
///     parts of the source under a mark.
/// </summary>
public sealed class PayloadBuilderTests
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
    ///     The moment the builder is told it is.
    /// </summary>
    private static readonly DateTimeOffset Now = new(2026, 11, 20, 15, 0, 0, TimeSpan.Zero);

    /// <summary>
    ///     A diagram of 400 by 200 pixels with two nodes, as a Mermaid picture reports them.
    /// </summary>
    private static readonly RenderPosition[] Nodes =
    [
        AnnotationTestPictures.Node("A", "Start", 20, 20, 100, 40, line: 2),
        AnnotationTestPictures.Node("B", "End", 200, 20, 100, 40, line: 3)
    ];

    /// <summary>
    ///     The composer that draws nothing.
    /// </summary>
    private readonly FakeMarkImageComposer _composer = new();

    /// <summary>
    ///     The registry that makes the marks.
    /// </summary>
    private readonly MarkRegistry _registry = new();

    /// <summary>
    ///     Makes the builder under test.
    /// </summary>
    /// <returns>The builder.</returns>
    private PayloadBuilder CreateBuilder() => new(_composer, new FixedTimeProvider(Now));

    /// <summary>
    ///     Makes a diagram picture; different fills give different pictures of the same file.
    /// </summary>
    /// <param name="fill">The fill of the pixels.</param>
    /// <returns>The picture.</returns>
    private static RenderedImage Diagram(byte fill = 255) => AnnotationTestPictures.Picture(width: 400, height: 200, fill: fill, positions: Nodes);

    /// <summary>
    ///     Makes a rectangle mark around the node A of a picture.
    /// </summary>
    /// <param name="picture">The picture.</param>
    /// <param name="note">The note of the mark.</param>
    /// <returns>The mark.</returns>
    private Mark MarkNodeA(RenderedImage picture, string? note = null)
    {
        return _registry.Add(picture, MarkKind.Rectangle, [new RenderPoint(10, 10), new RenderPoint(130, 70)], note);
    }

    /// <summary>
    ///     Makes a mark chip for a mark.
    /// </summary>
    /// <param name="mark">The mark.</param>
    /// <returns>The chip.</returns>
    private static MarkChip ChipOf(Mark mark) => new(mark.Id, mark.Number);

    /// <summary>
    ///     Test that the text and the segments hold the typed text, the dictated text, and the marks and attachments in the
    ///     order the user placed them, with a reference for each.
    /// </summary>
    [Fact]
    public void PayloadBuilder_Build_MessageWithTextMarksAndAttachments_KeepsOrderAndMakesReferences()
    {
        // Arrange: a message "Move " + mark 2 + " next to " + file + " and I said " + dictated "make it blue" + mark 1 + folder
        var picture = Diagram();
        var first = MarkNodeA(picture);
        var second = MarkNodeA(picture);
        var file = new FileChip(@"C:\notes\brief.txt");
        var folder = new FolderChip(@"C:\notes\photos");
        var document = new ComposerDocument();
        document.InsertText(0, "Move ", TextOrigin.Typed);
        document.InsertChip(document.Length, ChipOf(second));
        document.InsertText(document.Length, " next to ", TextOrigin.Typed);
        document.InsertChip(document.Length, file);
        document.InsertText(document.Length, " and I said ", TextOrigin.Typed);
        document.InsertText(document.Length, "make it blue", TextOrigin.Dictated);
        document.InsertChip(document.Length, ChipOf(first));
        document.InsertChip(document.Length, folder);
        var staged = new[]
        {
            PayloadTestSupport.Staged(file, "attachments/brief.txt"),
            PayloadTestSupport.Staged(folder, "attachments/photos")
        };

        // Act: build the payload
        var payload = CreateBuilder().Build(document.Snapshot(), _registry.Marks, staged);

        // Assert: the text has the references in place, and the segments keep the origins and the order
        Assert.Equal("Move [Mark 2] next to [Attachment 1: brief.txt] and I said make it blue[Mark 1][Attachment 2: photos]", payload.Text);
        Assert.Equal(
            [
                new PayloadTextPart("Move ", TextOrigin.Typed),
                new PayloadMarkPart(2),
                new PayloadTextPart(" next to ", TextOrigin.Typed),
                new PayloadAttachmentPart(1),
                new PayloadTextPart(" and I said ", TextOrigin.Typed),
                new PayloadTextPart("make it blue", TextOrigin.Dictated),
                new PayloadMarkPart(1),
                new PayloadAttachmentPart(2)
            ],
            payload.Segments);
        Assert.Equal(1, payload.Version);
        Assert.Equal(Now, payload.CreatedUtc);
    }

    /// <summary>
    ///     Test that the attachments carry their original path, their path in the project, their kind, name, and size, and
    ///     their index in the text.
    /// </summary>
    [Fact]
    public void PayloadBuilder_Build_Attachments_CarryOriginalAndProjectRelativePaths()
    {
        // Arrange: a message with a file, a folder, and a picture attached
        var file = new FileChip(@"C:\notes\brief.txt");
        var folder = new FolderChip(@"C:\notes\photos");
        var image = new ImageChip(@"C:\shots\screen.png", isPasted: false);
        var document = new ComposerDocument();
        document.Insert(0, [file, folder, image]);
        var staged = new[]
        {
            PayloadTestSupport.Staged(file, "attachments/brief.txt", 120),
            PayloadTestSupport.Staged(folder, "attachments/photos", 4096),
            PayloadTestSupport.Staged(image, "attachments/screen (2).png", 77)
        };

        // Act: build the payload
        var payload = CreateBuilder().Build(document.Snapshot(), [], staged);

        // Assert: one attachment for each chip, in order, one-based, with both paths
        Assert.Equal(
            [
                new PayloadAttachment(1, AttachmentKind.File, "brief.txt", @"C:\notes\brief.txt", "attachments/brief.txt", 120, 1),
                new PayloadAttachment(2, AttachmentKind.Folder, "photos", @"C:\notes\photos", "attachments/photos", 4096, 2),
                new PayloadAttachment(3, AttachmentKind.Image, "screen (2).png", @"C:\shots\screen.png", "attachments/screen (2).png", 77, 1)
            ],
            payload.Attachments);
        Assert.Equal("[Attachment 1: brief.txt][Attachment 2: photos][Attachment 3: screen (2).png]", payload.Text);
        Assert.Empty(payload.Images);
    }

    /// <summary>
    ///     Test that a mark is described in the pixels of its picture with its number, shape, points, bounding box, note,
    ///     the area drawn for it, and the picture and fingerprints it was drawn on.
    /// </summary>
    [Fact]
    public void PayloadBuilder_Build_Mark_CarriesGeometryInPicturePixelsNoteAndImageFacts()
    {
        // Arrange: a message with one mark with a note, on a picture with known facts
        var picture = Diagram();
        var mark = _registry.Add(picture, MarkKind.Rectangle, [new RenderPoint(130, 70), new RenderPoint(10, 10)], "this node");
        var document = new ComposerDocument();
        document.InsertChip(0, ChipOf(mark));

        // Act: build the payload
        var payload = CreateBuilder().Build(document.Snapshot(), _registry.Marks, []);

        // Assert: one image with the facts of the picture, the annotated picture and the area as the composer drew them,
        // and one mark with its geometry in normal form in picture pixels
        var image = Assert.Single(payload.Images);
        Assert.Equal(picture.SourcePath, image.SourcePath);
        Assert.Equal(picture.SourceFileHash, image.SourceFileHash);
        Assert.Equal(picture.ContentHash, image.ContentHash);
        Assert.Equal("mermaid", image.Format);
        Assert.Equal(400, image.Width);
        Assert.Equal(200, image.Height);
        Assert.Equal(FakeMarkImageComposer.AnnotatedBytesFor(picture, 0), image.AnnotatedPng.ToArray());
        var described = Assert.Single(image.Marks);
        Assert.Equal(1, described.Number);
        Assert.Equal(MarkKind.Rectangle, described.Kind);
        Assert.Equal([new RenderPoint(10, 10), new RenderPoint(130, 70)], described.Points);
        Assert.Equal(new RenderBox(10, 10, 120, 60), described.BoundingBox);
        Assert.Equal("this node", described.Note);
        Assert.Equal(FakeMarkImageComposer.AnnotatedBytesFor(picture, 1), described.CropPng.ToArray());
        Assert.Equal(new RenderBox(0, 0, 64, 64), described.CropBox);
    }

    /// <summary>
    ///     Test that each mark names the parts of the source file under it from the picture's positions, and that a picture
    ///     without positions says that it cannot tell, with the reason, instead of an empty list.
    /// </summary>
    [Fact]
    public void PayloadBuilder_Build_Marks_CarrySourceElementsWhereKnownAndNotAvailableOtherwise()
    {
        // Arrange: a mark around node A of a diagram and a mark on an SVG picture
        var diagram = Diagram();
        var svg = AnnotationTestPictures.Picture(path: @"C:\project\logo.svg", width: 64, height: 32, format: "svg");
        var onDiagram = MarkNodeA(diagram);
        var onSvg = _registry.Add(svg, MarkKind.Ellipse, [new RenderPoint(4, 4), new RenderPoint(40, 20)]);
        var document = new ComposerDocument();
        document.Insert(0, [ChipOf(onDiagram), ChipOf(onSvg)]);

        // Act: build the payload
        var payload = CreateBuilder().Build(document.Snapshot(), _registry.Marks, []);

        // Assert: the diagram's mark has node A with its text, line, and box; the SVG's mark has no elements, is not
        // available, and says why
        var diagramMark = payload.Images[0].Marks.Single();
        Assert.True(diagramMark.SourceElementsAvailable);
        Assert.Null(diagramMark.SourceElementsUnavailableReason);
        Assert.False(diagramMark.SourceElementsTruncated);
        var node = Assert.Single(diagramMark.SourceElements);
        Assert.Equal(new SourceElementReference("node", "A", "Start", 2, new RenderBox(20, 20, 100, 40), null), node);
        var svgMark = payload.Images[1].Marks.Single();
        Assert.False(svgMark.SourceElementsAvailable);
        Assert.Equal("svg provides no element positions", svgMark.SourceElementsUnavailableReason);
        Assert.Empty(svgMark.SourceElements);
    }

    /// <summary>
    ///     Test that marks on two versions of a file give two images, in the order of the first mark that refers to each,
    ///     with the marks of each picture in the order of their numbers, and that the composer is asked once for each.
    /// </summary>
    [Fact]
    public void PayloadBuilder_Build_MarksOnTwoPictures_GivesOneImageForEachInOrderOfFirstMark()
    {
        // Arrange: marks 1 and 3 on the earlier picture and mark 2 on the later picture; the message names 2 first
        var earlier = Diagram(fill: 10);
        var later = Diagram(fill: 20);
        var one = MarkNodeA(earlier);
        var two = MarkNodeA(later);
        var three = MarkNodeA(earlier);
        var document = new ComposerDocument();
        document.Insert(0, [ChipOf(two), ChipOf(three), ChipOf(one)]);

        // Act: build the payload
        var payload = CreateBuilder().Build(document.Snapshot(), _registry.Marks, []);

        // Assert: the later picture comes first because mark 2 is first in the message; the marks of the earlier picture
        // are in the order of their numbers; the composer drew each picture once with the marks of that picture
        Assert.Equal([later.ContentHash, earlier.ContentHash], payload.Images.Select(image => image.ContentHash));
        Assert.Equal([2], payload.Images[0].Marks.Select(mark => mark.Number));
        Assert.Equal([1, 3], payload.Images[1].Marks.Select(mark => mark.Number));
        Assert.Equal([later, earlier], _composer.Pictures);
        Assert.Equal([[2], [1, 3]], _composer.MarkNumbers);
        Assert.Equal("[Mark 2][Mark 3][Mark 1]", payload.Text);
    }

    /// <summary>
    ///     Test that the same picture drawn again with the same pixels counts as one picture.
    /// </summary>
    [Fact]
    public void PayloadBuilder_Build_SamePictureAsTwoObjects_GivesOneImage()
    {
        // Arrange: two marks on two picture objects with the same file and pixels
        var one = MarkNodeA(Diagram(fill: 30));
        var two = MarkNodeA(Diagram(fill: 30));
        var document = new ComposerDocument();
        document.Insert(0, [ChipOf(one), ChipOf(two)]);

        // Act: build the payload
        var payload = CreateBuilder().Build(document.Snapshot(), _registry.Marks, []);

        // Assert: one image with both marks
        var image = Assert.Single(payload.Images);
        Assert.Equal([1, 2], image.Marks.Select(mark => mark.Number));
        Assert.Single(_composer.Pictures);
    }

    /// <summary>
    ///     Test that a mark whose chip is not in the message is left out, with the picture that only it was drawn on, and
    ///     that a mark with a chip that appears twice is described once.
    /// </summary>
    [Fact]
    public void PayloadBuilder_Build_MarkWithoutChip_IsOmittedAndRepeatedChipIsOneMark()
    {
        // Arrange: three marks on two pictures; the message has the chip of the first twice and none of the others
        var earlier = Diagram(fill: 10);
        var later = Diagram(fill: 20);
        var one = MarkNodeA(earlier);
        MarkNodeA(earlier);
        MarkNodeA(later);
        var document = new ComposerDocument();
        document.Insert(0, [ChipOf(one), new MarkChip(one.Id, one.Number)]);

        // Act: build the payload
        var payload = CreateBuilder().Build(document.Snapshot(), _registry.Marks, []);

        // Assert: one image with one mark; the picture of the unused marks is not drawn
        var image = Assert.Single(payload.Images);
        Assert.Equal(earlier.ContentHash, image.ContentHash);
        Assert.Equal([1], image.Marks.Select(mark => mark.Number));
        Assert.Equal([earlier], _composer.Pictures);
        Assert.Equal("[Mark 1][Mark 1]", payload.Text);
    }

    /// <summary>
    ///     Test that a message of only chips has an empty text of references and that a message of only text has no images
    ///     and never asks the composer to draw.
    /// </summary>
    [Fact]
    public void PayloadBuilder_Build_TextOnlyOrChipsOnly_HasNoImagesAndDrawsNothing()
    {
        // Arrange: a message of text, and a message of one attachment chip
        var textOnly = new ComposerDocument();
        textOnly.InsertText(0, "Please make the logo bigger.", TextOrigin.Typed);
        var file = new FileChip(@"C:\notes\brief.txt");
        var chipsOnly = new ComposerDocument();
        chipsOnly.InsertChip(0, file);

        // Act: build both
        var textPayload = CreateBuilder().Build(textOnly.Snapshot(), _registry.Marks, []);
        var chipPayload = CreateBuilder().Build(chipsOnly.Snapshot(), [], [PayloadTestSupport.Staged(file, "attachments/brief.txt")]);

        // Assert: the text is what was typed, with no images and no drawing; the chip message is just the reference
        Assert.Equal("Please make the logo bigger.", textPayload.Text);
        Assert.Equal([new PayloadTextPart("Please make the logo bigger.", TextOrigin.Typed)], textPayload.Segments);
        Assert.Empty(textPayload.Images);
        Assert.Equal("[Attachment 1: brief.txt]", chipPayload.Text);
        Assert.Empty(_composer.Pictures);
    }

    /// <summary>
    ///     Test that an empty message gives an empty payload, which the chat decides whether it may send.
    /// </summary>
    [Fact]
    public void PayloadBuilder_Build_EmptyMessage_GivesEmptyPayload()
    {
        // Arrange / Act: build an empty message
        var payload = CreateBuilder().Build(new ComposerDocument().Snapshot(), [], []);

        // Assert: nothing in it
        Assert.Equal(string.Empty, payload.Text);
        Assert.Empty(payload.Segments);
        Assert.Empty(payload.Attachments);
        Assert.Empty(payload.Images);
    }

    /// <summary>
    ///     Test that attachments that are not those of the chips, in order, are refused, because the references of the text
    ///     would point at other things.
    /// </summary>
    [Fact]
    public void PayloadBuilder_Build_AttachmentsThatAreNotTheChipsInOrder_ThrowsArgumentException()
    {
        // Arrange: a message with two attachment chips
        var first = new FileChip(@"C:\notes\a.txt");
        var second = new FileChip(@"C:\notes\b.txt");
        var document = new ComposerDocument();
        document.Insert(0, [first, second]);
        var builder = CreateBuilder();
        var stagedFirst = PayloadTestSupport.Staged(first, "attachments/a.txt");
        var stagedSecond = PayloadTestSupport.Staged(second, "attachments/b.txt");

        // Act / Assert: too few, too many, and the wrong order are refused
        Assert.Throws<ArgumentException>(() => builder.Build(document.Snapshot(), [], [stagedFirst]));
        Assert.Throws<ArgumentException>(() => builder.Build(document.Snapshot(), [], [stagedFirst, stagedSecond, stagedSecond]));
        Assert.Throws<ArgumentException>(() => builder.Build(document.Snapshot(), [], [stagedSecond, stagedFirst]));
    }

    /// <summary>
    ///     Test that a chip of a mark that is not in the list, or that shows another number than the mark has, is refused.
    /// </summary>
    [Fact]
    public void PayloadBuilder_Build_MarkChipWithoutMarkOrWithOtherNumber_ThrowsArgumentException()
    {
        // Arrange: a mark and a message with a chip for a mark that was not made, and one with a wrong number
        var mark = MarkNodeA(Diagram());
        var unknown = new ComposerDocument();
        unknown.InsertChip(0, new MarkChip(Guid.NewGuid(), 1));
        var wrongNumber = new ComposerDocument();
        wrongNumber.InsertChip(0, new MarkChip(mark.Id, 7));
        var builder = CreateBuilder();

        // Act / Assert: both are refused
        Assert.Throws<ArgumentException>(() => builder.Build(unknown.Snapshot(), _registry.Marks, []));
        Assert.Throws<ArgumentException>(() => builder.Build(wrongNumber.Snapshot(), _registry.Marks, []));
    }

    /// <summary>
    ///     Test that a composer that gives fewer areas than marks is reported as an invalid operation.
    /// </summary>
    [Fact]
    public void PayloadBuilder_Build_ComposerReturnsWrongNumberOfAreas_ThrowsInvalidOperationException()
    {
        // Arrange: a message with two marks and a composer that makes one area
        var picture = Diagram();
        var one = MarkNodeA(picture);
        var two = MarkNodeA(picture);
        var document = new ComposerDocument();
        document.Insert(0, [ChipOf(one), ChipOf(two)]);
        _composer.CropCount = 1;

        // Act / Assert: the payload cannot be built
        Assert.Throws<InvalidOperationException>(() => CreateBuilder().Build(document.Snapshot(), _registry.Marks, []));
    }

    /// <summary>
    ///     Test that building on a background thread gives the same payload as building directly, and that a canceled
    ///     build does not start.
    /// </summary>
    [Fact]
    public async Task PayloadBuilder_BuildAsync_GivesTheSamePayloadAsBuildAndHonorsCancellation()
    {
        // Arrange: a message with a mark
        var token = TestContext.Current.CancellationToken;
        var mark = MarkNodeA(Diagram());
        var document = new ComposerDocument();
        document.InsertText(0, "Look: ", TextOrigin.Typed);
        document.InsertChip(document.Length, ChipOf(mark));
        var snapshot = document.Snapshot();
        var builder = CreateBuilder();
        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync();

        // Act: build both ways, and with a canceled token
        var direct = BuildDirectly(builder, snapshot);
        var background = await builder.BuildAsync(snapshot, _registry.Marks, [], token);

        // Assert: the same text, segments, and images; the canceled build is canceled
        Assert.Equal(direct.Text, background.Text);
        Assert.Equal(direct.Segments, background.Segments);
        Assert.Equal(direct.Images[0].ContentHash, background.Images[0].ContentHash);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => builder.BuildAsync(snapshot, _registry.Marks, [], cancelled.Token));
    }

    /// <summary>
    ///     Builds the payload of a message on the calling thread.
    /// </summary>
    /// <param name="builder">The builder.</param>
    /// <param name="snapshot">The message.</param>
    /// <returns>The payload, built with all marks of the registry and no attachments.</returns>
    private MessagePayload BuildDirectly(PayloadBuilder builder, ComposerSnapshot snapshot)
    {
        return builder.Build(snapshot, _registry.Marks, []);
    }

    /// <summary>
    ///     Test that missing arguments are refused.
    /// </summary>
    [Fact]
    public async Task PayloadBuilder_Build_MissingArguments_ThrowArgumentNullException()
    {
        // Arrange: a builder and a snapshot
        var builder = CreateBuilder();
        var snapshot = new ComposerDocument().Snapshot();

        // Act / Assert: each missing argument is refused
        Assert.Throws<ArgumentNullException>(() => new PayloadBuilder(null!));
        Assert.Throws<ArgumentNullException>(() => builder.Build(null!, [], []));
        Assert.Throws<ArgumentNullException>(() => builder.Build(snapshot, null!, []));
        Assert.Throws<ArgumentNullException>(() => builder.Build(snapshot, [], null!));
        await Assert.ThrowsAsync<ArgumentNullException>(() => builder.BuildAsync(null!, [], [], TestContext.Current.CancellationToken));
    }

    /// <summary>
    ///     Test that a payload does not change when the registry is cleared and the draft goes on, which is what happens
    ///     right after sending.
    /// </summary>
    [Fact]
    public void PayloadBuilder_Build_Payload_IsNotChangedByLaterChangesToDraftAndRegistry()
    {
        // Arrange: a message with text and a mark, built into a payload
        var mark = MarkNodeA(Diagram(), "check");
        var document = new ComposerDocument();
        document.InsertText(0, "See ", TextOrigin.Typed);
        document.InsertChip(document.Length, ChipOf(mark));
        var payload = CreateBuilder().Build(document.Snapshot(), _registry.Marks, []);
        var textBefore = payload.Text;
        var cropBefore = payload.Images[0].Marks[0].CropPng.ToArray();

        // Act: the draft is cleared and edited, and the registry is cleared
        document.Clear();
        document.InsertText(0, "Something else", TextOrigin.Typed);
        _registry.Clear();

        // Assert: the payload holds what it held
        Assert.Equal(textBefore, payload.Text);
        Assert.Equal("See [Mark 1]", payload.Text);
        Assert.Equal(cropBefore, payload.Images[0].Marks[0].CropPng.ToArray());
        Assert.Equal("check", payload.Images[0].Marks[0].Note);
    }
}
