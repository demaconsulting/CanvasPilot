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

using System.Text.Json;
using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.CanvasPilot.AnnotationSubsystem;
using DemaConsulting.CanvasPilot.ComposerSubsystem;
using DemaConsulting.CanvasPilot.PayloadSubsystem;
using DemaConsulting.CanvasPilot.PreviewSubsystem;

namespace DemaConsulting.CanvasPilot.Tests;

/// <summary>
///     Subsystem tests for the Payload subsystem: the real composer of the message, attachment store, render service,
///     marks, composer of mark pictures, builder, and describer working together on a message with a Mermaid diagram.
/// </summary>
public sealed class PayloadSubsystemTests : IDisposable
{
    /// <summary>
    ///     What the tests build: the parts and the payload.
    /// </summary>
    /// <param name="Picture">The diagram that was drawn.</param>
    /// <param name="Marks">The marks made on it: the rectangle around the node Start and the arrow from Start to End.</param>
    /// <param name="Registry">The registry that holds the marks.</param>
    /// <param name="Document">The message that was composed.</param>
    /// <param name="Payload">The payload that was built from the message.</param>
    /// <param name="Brief">The file that was attached, where the user had it.</param>
    private sealed record Sample(
        RenderedImage Picture,
        IReadOnlyList<Mark> Marks,
        MarkRegistry Registry,
        ComposerDocument Document,
        MessagePayload Payload,
        string Brief);

    /// <summary>
    ///     Temporary folder that holds the project and the file that is attached from outside it.
    /// </summary>
    private readonly string _root = Directory.CreateTempSubdirectory("canvaspilot-payload-tests-").FullName;

    /// <summary>
    ///     The real render service.
    /// </summary>
    private readonly ImageRenderService _renderer = new();

    /// <inheritdoc />
    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    /// <summary>
    ///     Test that the text of the payload has the references to the marks and the attachment at the places the user put
    ///     them, and that the segments keep what was typed apart from what was dictated.
    /// </summary>
    [Fact]
    public async Task PayloadSubsystem_Build_MessageText_HasReferencesInOrderWithOrigins()
    {
        // Arrange / Act: compose and build a message with text, two marks, an attachment, and a dictated ending
        var sample = await BuildSampleAsync();

        // Assert: the text and the segments are in the order of the draft
        Assert.Equal("Make [Mark 1] bigger than [Mark 2], see [Attachment 1: brief.txt] - said thank you", sample.Payload.Text);
        Assert.Equal(
            [
                new PayloadTextPart("Make ", TextOrigin.Typed),
                new PayloadMarkPart(1),
                new PayloadTextPart(" bigger than ", TextOrigin.Typed),
                new PayloadMarkPart(2),
                new PayloadTextPart(", see ", TextOrigin.Typed),
                new PayloadAttachmentPart(1),
                new PayloadTextPart(" - said ", TextOrigin.Typed),
                new PayloadTextPart("thank you", TextOrigin.Dictated)
            ],
            sample.Payload.Segments);
    }

    /// <summary>
    ///     Test that the payload holds the whole picture with the marks drawn and numbered on it, in the size of the picture
    ///     that was shown and with the pixels away from the marks as they were shown.
    /// </summary>
    [Fact]
    public async Task PayloadSubsystem_Build_MarkedPicture_HasAnnotatedImageWithMarksDrawnAndNumbered()
    {
        // Arrange / Act: compose and build the sample message
        var sample = await BuildSampleAsync();
        var image = Assert.Single(sample.Payload.Images);
        using var annotated = PayloadTestSupport.Decode(image.AnnotatedPng);
        var start = StartBox(sample.Picture);

        // Assert: the facts of the picture, a picture of the same size, the left line of the rectangle in the color of
        // mark one, the badge of mark one in its color, and the last pixel as it was shown
        Assert.Equal(sample.Picture.SourcePath, image.SourcePath);
        Assert.Equal(sample.Picture.SourceFileHash, image.SourceFileHash);
        Assert.Equal(sample.Picture.ContentHash, image.ContentHash);
        Assert.Equal("mermaid", image.Format);
        Assert.Equal((sample.Picture.Width, sample.Picture.Height), (annotated.Width, annotated.Height));
        var markOneColor = PayloadTestSupport.ColorOf(MarkImageComposer.ColorOf(1));
        var box = sample.Marks[0].Geometry.BoundingBox;
        Assert.Equal(markOneColor, annotated[(int)box.X, (int)(box.Y + (box.Height / 2))]);
        var badge = MarkImageComposer.BadgeCenter(box, sample.Picture.Width, sample.Picture.Height);
        Assert.Equal(markOneColor, annotated[(int)badge.X - 6, (int)badge.Y]);
        var last = ((sample.Picture.Width * sample.Picture.Height) - 1) * 4;
        var shown = sample.Picture.RgbaPixels.Span;
        Assert.Equal(shown[last + 3], annotated[sample.Picture.Width - 1, sample.Picture.Height - 1].A);
        Assert.True(start.Width > 0);
    }

    /// <summary>
    ///     Test that each mark has a picture of the area around it that is the corresponding region of the annotated
    ///     picture.
    /// </summary>
    [Fact]
    public async Task PayloadSubsystem_Build_Marks_HaveCropsThatAreRegionsOfTheAnnotatedPicture()
    {
        // Arrange / Act: compose and build the sample message
        var sample = await BuildSampleAsync();
        var image = Assert.Single(sample.Payload.Images);
        using var annotated = PayloadTestSupport.Decode(image.AnnotatedPng);

        // Assert: one area for each mark, as large as its box says, inside the picture and holding the mark, and equal to
        // the region of the annotated picture
        Assert.Equal(2, image.Marks.Length);
        foreach (var mark in image.Marks)
        {
            using var crop = PayloadTestSupport.Decode(mark.CropPng);
            var box = mark.CropBox;
            Assert.Equal((int)box.Width, crop.Width);
            Assert.Equal((int)box.Height, crop.Height);
            Assert.True(box.X >= 0 && box.Y >= 0 && box.X + box.Width <= annotated.Width && box.Y + box.Height <= annotated.Height);
            Assert.True(box.X <= mark.BoundingBox.X && box.Y <= mark.BoundingBox.Y);
            for (var y = 0; y < crop.Height; y++)
            {
                for (var x = 0; x < crop.Width; x++)
                {
                    Assert.Equal(annotated[(int)box.X + x, (int)box.Y + y], crop[x, y]);
                }
            }
        }
    }

    /// <summary>
    ///     Test that the marks are described in the pixels of the picture: kind, points, bounding box inside the picture,
    ///     and note.
    /// </summary>
    [Fact]
    public async Task PayloadSubsystem_Build_Marks_HaveGeometryInPicturePixelsAndNote()
    {
        // Arrange / Act: compose and build the sample message
        var sample = await BuildSampleAsync();
        var marks = sample.Payload.Images[0].Marks;

        // Assert: the geometry is that of the marks, in picture pixels, and inside the picture
        Assert.Equal([1, 2], marks.Select(mark => mark.Number));
        Assert.Equal([MarkKind.Rectangle, MarkKind.Arrow], marks.Select(mark => mark.Kind));
        Assert.Equal(sample.Marks[0].Geometry.Points, marks[0].Points);
        Assert.Equal(sample.Marks[1].Geometry.Points, marks[1].Points);
        Assert.Equal(sample.Marks[0].Geometry.BoundingBox, marks[0].BoundingBox);
        Assert.All(marks, mark =>
        {
            Assert.True(mark.BoundingBox.X >= 0 && mark.BoundingBox.Y >= 0);
            Assert.True(mark.BoundingBox.X + mark.BoundingBox.Width <= sample.Picture.Width);
            Assert.True(mark.BoundingBox.Y + mark.BoundingBox.Height <= sample.Picture.Height);
        });
        Assert.Equal("make this bigger", marks[0].Note);
        Assert.Null(marks[1].Note);
    }

    /// <summary>
    ///     Test that the marks on a diagram name the nodes under them, the head of the arrow first, and that a mark on an
    ///     SVG picture says that the picture cannot tell.
    /// </summary>
    [Fact]
    public async Task PayloadSubsystem_Build_Marks_HaveSourceElementsWhereKnownAndNotAvailableOtherwise()
    {
        // Arrange: the sample message, and a second message with a mark on an SVG picture
        var sample = await BuildSampleAsync();
        var token = TestContext.Current.CancellationToken;
        var svgFile = Path.Combine(_root, "project", "logo.svg");
        await File.WriteAllTextAsync(svgFile, "<svg xmlns='http://www.w3.org/2000/svg' width='64' height='32'><rect width='64' height='32' fill='#336699'/></svg>", token);
        var svg = (await _renderer.RenderAsync(svgFile, Path.Combine(_root, "project"), token)).Image!;
        var registry = new MarkRegistry();
        var svgMark = registry.Add(svg, MarkKind.Rectangle, [new RenderPoint(4, 4), new RenderPoint(40, 20)]);
        var document = new ComposerDocument();
        document.InsertChip(0, new MarkChip(svgMark.Id, svgMark.Number));

        // Act: build the second message
        var svgPayload = await new PayloadBuilder(new MarkImageComposer(_renderer)).BuildAsync(document.Snapshot(), registry.Marks, [], token);

        // Assert: the rectangle names its node with the text and line from the renderer; the arrow names the node at its
        // head first and the node at its tail second; the SVG mark is not available, with the reason
        var marks = sample.Payload.Images[0].Marks;
        var rectangleNode = Assert.Single(marks[0].SourceElements, element => element.Kind == "node");
        Assert.Equal("Start", rectangleNode.Text);
        Assert.Equal(sample.Picture.Positions.First(position => position.Text == "Start").Line, rectangleNode.Line);
        Assert.True(marks[0].SourceElementsAvailable);
        Assert.Equal(["End", "Start"], marks[1].SourceElements.Where(element => element.Kind == "node").Select(element => element.Text));
        var notAvailable = svgPayload.Images[0].Marks[0];
        Assert.False(notAvailable.SourceElementsAvailable);
        Assert.Equal("svg provides no element positions", notAvailable.SourceElementsUnavailableReason);
        Assert.Empty(notAvailable.SourceElements);
    }

    /// <summary>
    ///     Test that the attachment is in the payload with the place where the user had it and its path in the project,
    ///     and that the file was copied there.
    /// </summary>
    [Fact]
    public async Task PayloadSubsystem_Build_Attachments_HaveOriginalAndProjectRelativePaths()
    {
        // Arrange / Act: compose and build the sample message, which attaches a file from outside the project
        var sample = await BuildSampleAsync();

        // Assert: the attachment names both paths, and the copy is in the project
        var attachment = Assert.Single(sample.Payload.Attachments);
        Assert.Equal(1, attachment.Index);
        Assert.Equal(AttachmentKind.File, attachment.Kind);
        Assert.Equal("brief.txt", attachment.Name);
        Assert.Equal(sample.Brief, attachment.OriginalPath);
        Assert.Equal("attachments/brief.txt", attachment.ProjectRelativePath);
        Assert.Equal(new FileInfo(sample.Brief).Length, attachment.SizeBytes);
        Assert.True(File.Exists(Path.Combine(_root, "project", "attachments", "brief.txt")));
    }

    /// <summary>
    ///     Test that a payload that has been built does not change when the draft is cleared and written again and the
    ///     marks are cleared, which is what happens right after sending.
    /// </summary>
    [Fact]
    public async Task PayloadSubsystem_Build_Payload_IsNotChangedByTheNextDraft()
    {
        // Arrange: the sample message and what its payload says
        var sample = await BuildSampleAsync();
        var text = sample.Payload.Text;
        var annotated = sample.Payload.Images[0].AnnotatedPng.ToArray();
        var description = PayloadDescriber.Describe(sample.Payload);

        // Act: the draft is cleared and a new one is written, and the marks are cleared
        sample.Document.Clear();
        sample.Document.InsertText(0, "Next message", TextOrigin.Typed);
        sample.Registry.Clear();

        // Assert: the payload holds, and describes, what it did
        Assert.Equal(text, sample.Payload.Text);
        Assert.Equal(annotated, sample.Payload.Images[0].AnnotatedPng.ToArray());
        Assert.Equal(2, sample.Payload.Images[0].Marks.Length);
        Assert.Equal(description, PayloadDescriber.Describe(sample.Payload));
    }

    /// <summary>
    ///     Test that the description of a payload lists everything the assistant receives: the text, the attachment with
    ///     its paths, the picture with its facts, and each mark with its geometry and the parts of the source under it.
    /// </summary>
    [Fact]
    public async Task PayloadSubsystem_Describe_Payload_ListsEverythingTheAssistantReceives()
    {
        // Arrange / Act: build the sample message and describe it
        var sample = await BuildSampleAsync();
        var description = PayloadDescriber.Describe(sample.Payload);
        using var document = JsonDocument.Parse(description);
        var root = document.RootElement;
        var image = root.GetProperty("images")[0];

        // Assert: the text, the attachment, the picture facts, and the marks are all in the description
        Assert.Equal(sample.Payload.Text, root.GetProperty("text").GetString());
        Assert.Equal("attachments/brief.txt", root.GetProperty("attachments")[0].GetProperty("projectRelativePath").GetString());
        Assert.Equal(sample.Picture.ContentHash, image.GetProperty("contentHash").GetString());
        Assert.Equal(sample.Picture.SourceFileHash, image.GetProperty("sourceFileHash").GetString());
        Assert.Equal(2, image.GetProperty("marks").GetArrayLength());
        Assert.Equal("make this bigger", image.GetProperty("marks")[0].GetProperty("note").GetString());
        Assert.Contains("\"text\": \"Start\"", description, StringComparison.Ordinal);
        Assert.Contains("\"origin\": \"dictated\"", description, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Gets the box of the node <c>Start</c> of a drawn diagram.
    /// </summary>
    /// <param name="picture">The picture.</param>
    /// <returns>The box.</returns>
    private static RenderBox StartBox(RenderedImage picture)
    {
        return picture.Positions.First(position => position is { Kind: "node", Text: "Start" }).Box!.Value;
    }

    /// <summary>
    ///     Draws a diagram in a project, makes a rectangle mark with a note around its node <c>Start</c> and an arrow mark
    ///     from that node to the node <c>End</c>, composes a message with text, both marks, an attached file from outside the
    ///     project, and a dictated ending, puts the attachment into the project, and builds the payload on a background
    ///     thread, all with the real units.
    /// </summary>
    /// <returns>The parts and the payload.</returns>
    private async Task<Sample> BuildSampleAsync()
    {
        var token = TestContext.Current.CancellationToken;
        var project = Directory.CreateDirectory(Path.Combine(_root, "project")).FullName;
        var external = Directory.CreateDirectory(Path.Combine(_root, "external")).FullName;
        var brief = Path.Combine(external, "brief.txt");
        await File.WriteAllTextAsync(brief, "the brief", token);
        var diagram = Path.Combine(project, "flow.mmd");
        await File.WriteAllTextAsync(diagram, "flowchart LR\n  A[Start] --> B[End]\n", token);
        var rendered = await _renderer.RenderAsync(diagram, project, token);
        Assert.True(rendered.Succeeded, rendered.Error?.Message);
        var picture = rendered.Image!;

        // Mark the node Start with a rectangle with whole-pixel corners and a note, and point an arrow from it to End
        var start = StartBox(picture);
        var end = picture.Positions.First(position => position is { Kind: "node", Text: "End" }).Box!.Value;
        var registry = new MarkRegistry();
        var rectangle = registry.Add(
            picture,
            MarkKind.Rectangle,
            [new RenderPoint(Math.Floor(start.X) - 4, Math.Floor(start.Y) - 4), new RenderPoint(Math.Ceiling(start.X + start.Width) + 4, Math.Ceiling(start.Y + start.Height) + 4)],
            "make this bigger");
        var arrow = registry.Add(
            picture,
            MarkKind.Arrow,
            [new RenderPoint(start.X + (start.Width / 2), start.Y + (start.Height / 2)), new RenderPoint(end.X + (end.Width / 2), end.Y + (end.Height / 2))]);

        // Compose the message and put the attachment into the project, as sending does
        var attachment = new FileChip(brief);
        var document = new ComposerDocument();
        document.InsertText(0, "Make ", TextOrigin.Typed);
        document.InsertChip(document.Length, new MarkChip(rectangle.Id, rectangle.Number));
        document.InsertText(document.Length, " bigger than ", TextOrigin.Typed);
        document.InsertChip(document.Length, new MarkChip(arrow.Id, arrow.Number));
        document.InsertText(document.Length, ", see ", TextOrigin.Typed);
        document.InsertChip(document.Length, attachment);
        document.InsertText(document.Length, " - said ", TextOrigin.Typed);
        document.InsertText(document.Length, "thank you", TextOrigin.Dictated);
        var staging = new AttachmentStore(project).StageForSend([attachment]);
        Assert.True(staging.Succeeded, staging.Failure?.Reason);

        // Build the payload the way the chat will: on a background thread
        var payload = await new PayloadBuilder(new MarkImageComposer(_renderer)).BuildAsync(document.Snapshot(), registry.Marks, staging.Attachments, token);
        return new Sample(picture, [rectangle, arrow], registry, document, payload, brief);
    }
}
