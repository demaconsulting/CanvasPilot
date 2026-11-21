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
        var entry = Assert.Single(panel.Transcript);
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
        var payload = Assert.IsType<MessagePayload>(Assert.Single(panel.Transcript).Message.Payload);
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
