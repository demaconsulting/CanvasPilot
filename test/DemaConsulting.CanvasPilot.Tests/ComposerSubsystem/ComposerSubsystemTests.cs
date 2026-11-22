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
///     Subsystem tests for the Composer subsystem: the document and the history working together on one draft.
/// </summary>
public sealed class ComposerSubsystemTests : IDisposable
{
    /// <summary>
    ///     The first bytes of a PNG file.
    /// </summary>
    private static readonly byte[] PngBytes = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 7, 8, 9];

    /// <summary>
    ///     Temporary folder that holds the project and the files that are dropped from outside it.
    /// </summary>
    private readonly string _root = Directory.CreateTempSubdirectory("canvaspilot-composer-tests-").FullName;

    /// <inheritdoc />
    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    /// <summary>
    ///     Test that dropping files, a folder, and a pasted picture and then undoing the drop removes the chips and
    ///     copies nothing into the project: the only thing that exists is the pasted picture in the draft folder, which
    ///     is removed as a leftover when no chip refers to it.
    /// </summary>
    [Fact]
    public void ComposerSubsystem_DropThenUndo_RemovesChipsAndCopiesNothing()
    {
        // Arrange: a project, a draft, and content from outside the project
        var (store, intake, document, history, external) = CreateDraft();
        var file = WriteFile(Path.Combine(external, "brief.txt"), [1, 2, 3]);
        var folder = Path.Combine(external, "photos");
        WriteFile(Path.Combine(folder, "a.png"), PngBytes);

        // Act: drop everything in one operation, then undo it
        var accepted = intake.Accept(new DroppedContent { Paths = [file, folder], Images = [PngBytes], Text = " for you" });
        history.Execute(ComposerEdit.Drop(document, 0, accepted.Segments));
        var chipsAfterDrop = document.Chips.Count();
        var stepsAfterDrop = history.UndoCount;
        history.Undo();
        var leftoversRemoved = store.RemoveLeftovers([.. document.Chips.OfType<ImageChip>().Select(chip => chip.FullPath)]);

        // Assert: one step held three chips and the text; after undo nothing is in the draft, nothing was copied into
        // the project, and the pasted picture that no chip refers to was removed
        Assert.Equal(3, chipsAfterDrop);
        Assert.Equal(1, stepsAfterDrop);
        Assert.Equal(0, document.Length);
        Assert.False(Directory.Exists(store.AttachmentsFolder));
        Assert.Equal(1, leftoversRemoved);
        Assert.Empty(Directory.GetFiles(store.PastedImagesFolder));
        Assert.True(File.Exists(file));
    }

    /// <summary>
    ///     Test that sending copies what is attached from outside the project into the project's attachments folder,
    ///     moves the pasted picture there, refers to things that are in the project in place, and leaves a clean draft.
    /// </summary>
    [Fact]
    public void ComposerSubsystem_Send_CopiesExternalFilesIntoProjectAttachments()
    {
        // Arrange: a draft with a file and a folder from outside, a pasted picture, and a file in the project
        var (store, intake, document, history, external) = CreateDraft();
        var file = WriteFile(Path.Combine(external, "brief.txt"), [1, 2, 3]);
        var folder = Path.Combine(external, "photos");
        WriteFile(Path.Combine(folder, "a.png"), PngBytes);
        var inside = WriteFile(Path.Combine(store.ProjectFolder, "flow.mmd"), [4]);
        var accepted = intake.Accept(new DroppedContent { Paths = [file, folder, inside], Images = [PngBytes] });
        history.Execute(ComposerEdit.Drop(document, 0, accepted.Segments));

        // Act: send, then put the attachments into the project
        var sent = history.CompleteSend();
        var result = store.StageForSend([.. sent.Chips.OfType<AttachmentChip>()]);

        // Assert: external things are in attachments, the project's own file is referred to in place, the pasted
        // picture was moved out of the draft folder, and the draft is empty
        Assert.True(result.Succeeded);
        Assert.Equal(
            ["attachments/brief.txt", "attachments/photos", "flow.mmd", "attachments/paste-"],
            result.Attachments.Select(item => item.ProjectRelativePath.StartsWith("attachments/paste-", StringComparison.Ordinal) ? "attachments/paste-" : item.ProjectRelativePath));
        Assert.Equal([true, true, false, true], result.Attachments.Select(item => item.WasCopied));
        Assert.True(File.Exists(Path.Combine(store.AttachmentsFolder, "photos", "a.png")));
        Assert.Empty(Directory.GetFiles(store.PastedImagesFolder));
        Assert.Equal(0, document.Length);
        Assert.True(File.Exists(file));
    }

    /// <summary>
    ///     Test that dropped content is treated as data: a program and a script become chips, are copied byte for byte
    ///     when sent, are not changed, and unsafe paths are left out, so nothing dropped is opened or run.
    /// </summary>
    [Fact]
    public async Task ComposerSubsystem_Drop_ProgramsScriptsAndUnsafePaths_AreOnlyDataAndNeverRun()
    {
        // Arrange: a draft and dropped files with the content of a program and a script, and unsafe paths
        var (store, intake, document, history, external) = CreateDraft();
        var program = WriteFile(Path.Combine(external, "setup.exe"), [0x4D, 0x5A, 1, 2]);
        var script = WriteFile(Path.Combine(external, "run.ps1"), "Remove-Item C:\\ -Recurse"u8.ToArray());
        var unsafePaths = new[] { Path.Combine(external, "sub", "..", "setup.exe"), @"\\.\C:\", "relative.exe" };

        // Act: drop everything, send, and stage
        var accepted = intake.Accept(new DroppedContent { Paths = [program, script, .. unsafePaths] });
        history.Execute(ComposerEdit.Drop(document, 0, accepted.Segments));
        var result = store.StageForSend([.. history.CompleteSend().Chips.OfType<AttachmentChip>()]);

        // Assert: both files are chips and are copied unchanged, every unsafe path was left out with a reason
        Assert.Equal(2, accepted.Segments.Length);
        Assert.Equal(3, accepted.Skipped.Length);
        Assert.True(result.Succeeded);
        Assert.Equal(
            await File.ReadAllBytesAsync(program, TestContext.Current.CancellationToken),
            await File.ReadAllBytesAsync(Path.Combine(store.AttachmentsFolder, "setup.exe"), TestContext.Current.CancellationToken));
        Assert.Equal(
            await File.ReadAllBytesAsync(script, TestContext.Current.CancellationToken),
            await File.ReadAllBytesAsync(Path.Combine(store.AttachmentsFolder, "run.ps1"), TestContext.Current.CancellationToken));
    }

    /// <summary>
    ///     Test that typed text, dictated text, and chips of every kind that are added in turn stay in the order they
    ///     were added, each with its origin and its kind.
    /// </summary>
    [Fact]
    public void ComposerSubsystem_Compose_MixedParts_KeepOrderOriginAndKind()
    {
        // Arrange: a draft and its history
        var document = new ComposerDocument();
        var history = new ComposerHistory(document);
        var mark = new MarkChip(Guid.NewGuid(), 1);

        // Act: type, dictate, drop a file, a folder, and an image together, add a mark inside the typed text
        history.Execute(ComposerEdit.TypeText(document, 0, "Use this: "));
        history.Execute(ComposerEdit.Dictate(document, document.Length, "and make it larger"));
        history.Execute(ComposerEdit.Drop(
            document,
            document.Length,
            [new FileChip(@"C:\a.txt"), new FolderChip(@"C:\d"), new ImageChip(@"C:\b.png", isPasted: true)]));
        history.Execute(ComposerEdit.AddMark(document, 4, mark));
        var message = document.Snapshot();

        // Assert: the parts are in order, the typed text was split around the mark, and kinds and origins are kept
        Assert.Collection(
            message.Segments,
            segment => Assert.Equal(new TextSegment("Use ", TextOrigin.Typed), segment),
            segment => Assert.Equal(mark, segment),
            segment => Assert.Equal(new TextSegment("this: ", TextOrigin.Typed), segment),
            segment => Assert.Equal(new TextSegment("and make it larger", TextOrigin.Dictated), segment),
            segment => Assert.IsType<FileChip>(segment),
            segment => Assert.IsType<FolderChip>(segment),
            segment => Assert.True(Assert.IsType<ImageChip>(segment).IsPasted));
        Assert.Equal(4 + 1 + 6 + 18 + 3, message.Length);
    }

    /// <summary>
    ///     Test that a mixed sequence of user actions can be undone back to the empty draft and redone forward, each
    ///     step passing through the state it had when it was made.
    /// </summary>
    [Fact]
    public void ComposerSubsystem_UndoRedo_MixedSequence_ReturnsToEachStateInOrder()
    {
        // Arrange: a draft and its history
        var document = new ComposerDocument();
        var history = new ComposerHistory(document);
        var states = new List<ComposerSnapshot> { document.Snapshot() };

        // Act: a typed word (one character per step), a dictated block, a drop of two items, a mark, and a chip deleted
        foreach (var character in "Fix")
        {
            history.Execute(ComposerEdit.TypeText(document, document.Length, character.ToString()));
            states.Add(document.Snapshot());
        }

        history.Execute(ComposerEdit.Dictate(document, document.Length, " the title of this box"));
        states.Add(document.Snapshot());
        history.Execute(ComposerEdit.Drop(document, document.Length, [new FileChip(@"C:\a.txt"), new ImageChip(@"C:\b.png")]));
        states.Add(document.Snapshot());
        history.Execute(ComposerEdit.AddMark(document, 3, new MarkChip(Guid.NewGuid(), 1)));
        states.Add(document.Snapshot());
        history.Execute(ComposerEdit.DeleteChip(document, document.Length - 1));
        states.Add(document.Snapshot());

        // Assert: undo returns through every earlier state in reverse order and ends with the empty draft
        Assert.Equal(states.Count - 1, history.UndoCount);
        for (var index = states.Count - 2; index >= 0; index--)
        {
            history.Undo();
            Assert.True(document.Snapshot().HasSameContentAs(states[index]), $"state {index} after undo");
        }

        Assert.Equal(0, document.Length);

        // Assert: redo goes forward through the same states in order
        for (var index = 1; index < states.Count; index++)
        {
            history.Redo();
            Assert.True(document.Snapshot().HasSameContentAs(states[index]), $"state {index} after redo");
        }

        Assert.False(history.CanRedo);
    }

    /// <summary>
    ///     Test that sending hands over the draft and leaves an empty draft with no history, so that the next draft
    ///     cannot be undone into the sent one.
    /// </summary>
    [Fact]
    public void ComposerSubsystem_Send_ClearsHistoryAndDocument()
    {
        // Arrange: a draft with text, a dictated block, and a chip, with steps that can be undone and redone
        var document = new ComposerDocument();
        var history = new ComposerHistory(document);
        history.Execute(ComposerEdit.TypeText(document, 0, "Hello"));
        history.Execute(ComposerEdit.Dictate(document, 5, " there"));
        history.Execute(ComposerEdit.Drop(document, 11, [new FolderChip(@"C:\data")]));
        history.Undo();

        // Act: send, then type the next message
        var sent = history.CompleteSend();
        history.Execute(ComposerEdit.TypeText(document, 0, "Next"));

        // Assert: the sent content is complete, the new draft holds only the new text, and undo reaches only it
        Assert.Equal("Hello there", sent.FlatText);
        Assert.Equal("Next", document.FlatText);
        Assert.Equal(1, history.UndoCount);
        Assert.False(history.CanRedo);
        history.Undo();
        Assert.Equal(0, document.Length);
        Assert.False(history.CanUndo);
    }

    /// <summary>
    ///     Test that a snapshot taken for sending cannot be changed by later typing or by the next draft.
    /// </summary>
    [Fact]
    public void ComposerSubsystem_Send_SnapshotIsUnaffectedByNextDraft()
    {
        // Arrange: a draft with a chip
        var document = new ComposerDocument();
        var history = new ComposerHistory(document);
        history.Execute(ComposerEdit.Drop(document, 0, [new FileChip(@"C:\a.txt")]));

        // Act: send and then compose another message with another chip
        var sent = history.CompleteSend();
        history.Execute(ComposerEdit.Drop(document, 0, [new FolderChip(@"C:\d"), new MarkChip(Guid.NewGuid(), 1)]));

        // Assert: the sent snapshot still holds its one file chip
        Assert.IsType<FileChip>(Assert.Single(sent.Chips));
        Assert.Equal(2, document.Chips.Count());
    }

    /// <summary>
    ///     Test that a message written through the view model, with typed text, a mark, a dropped file, and a pasted picture,
    ///     is sent with the real store and the real payload builder: the attachments are in the project, the payload holds
    ///     the text with its references and the picture with the mark, and nothing is left in the draft.
    /// </summary>
    [Fact]
    public async Task ComposerSubsystem_SendThroughViewModel_CopiesAttachmentsBuildsPayloadAndClearsDraft()
    {
        // Arrange: a project, a view model over the real store and payload builder, and content from outside
        var project = Directory.CreateDirectory(Path.Combine(_root, "project")).FullName;
        var external = Directory.CreateDirectory(Path.Combine(_root, "external")).FullName;
        var file = WriteFile(Path.Combine(external, "brief.txt"), [1, 2, 3]);
        var marks = new MarkRegistry();
        using var viewModel = new ComposerViewModel(new PayloadBuilder(new FakeMarkImageComposer()), marks, new FakeDictationService());
        viewModel.SetProject(project);
        var picture = AnnotationTestPictures.Picture();
        var mark = marks.Add(picture, MarkKind.Rectangle, [new RenderPoint(10, 10), new RenderPoint(60, 40)]);
        var sentMessages = new List<SentMessage>();
        viewModel.MessageSent += (_, e) => sentMessages.Add(e.Message);

        // Act: write the message, send it, and try to undo it
        viewModel.InsertTyped("See ");
        viewModel.AddMark(mark);
        viewModel.AddDictatedBlock(" and this");
        viewModel.HandleDrop(new DroppedContent { Paths = [file], Images = [PngBytes] });
        var pastedPicture = viewModel.Document.Chips.OfType<ImageChip>().Single().FullPath;
        var sent = await viewModel.SendAsync(TestContext.Current.CancellationToken);
        viewModel.Undo();

        // Assert: the message was sent once, with a payload that has the references, the attachments, and the picture
        Assert.True(sent);
        var message = Assert.Single(sentMessages);
        var payload = Assert.IsType<MessagePayload>(message.Payload);
        Assert.StartsWith("See [Mark 1] and this[Attachment 1: brief.txt][Attachment 2: paste-", payload.Text, StringComparison.Ordinal);
        Assert.Equal(["attachments/brief.txt", "attachments/" + Path.GetFileName(pastedPicture)], payload.Attachments.Select(item => item.ProjectRelativePath));
        var image = Assert.Single(payload.Images);
        Assert.Equal(picture.SourcePath, image.SourcePath);
        Assert.Equal(1, Assert.Single(image.Marks).Number);
        Assert.Equal([mark], message.Marks);

        // Assert: the files are in the project, the saved picture was moved out of the draft folder, and the draft is empty
        Assert.True(File.Exists(Path.Combine(project, "attachments", "brief.txt")));
        Assert.True(File.Exists(Path.Combine(project, "attachments", Path.GetFileName(pastedPicture))));
        Assert.False(File.Exists(pastedPicture));
        Assert.True(File.Exists(file));
        Assert.Equal(0, viewModel.Document.Length);
        Assert.False(viewModel.CanUndo);
        Assert.Empty(marks.Marks);
    }

    /// <summary>
    ///     Test that a message that cannot be attached because a file is too large is not sent: the draft, the history,
    ///     and the marks stay, nothing is left in the project, and removing the chip lets the message go.
    /// </summary>
    [Fact]
    public async Task ComposerSubsystem_SendThroughViewModel_AttachmentTooLarge_KeepsDraftAndLeavesProjectUntouched()
    {
        // Arrange: a project and a dropped file that is one byte larger than a message may carry
        var project = Directory.CreateDirectory(Path.Combine(_root, "project")).FullName;
        var external = Directory.CreateDirectory(Path.Combine(_root, "external")).FullName;
        var huge = Path.Combine(external, "huge.bin");
        using (var stream = new FileStream(huge, FileMode.Create))
        {
            stream.SetLength(AttachmentStore.MaximumBytes + 1);
        }

        using var viewModel = new ComposerViewModel(new PayloadBuilder(new FakeMarkImageComposer()), new MarkRegistry(), new FakeDictationService());
        viewModel.SetProject(project);
        viewModel.InsertTyped("here it is");
        viewModel.HandleDrop(new DroppedContent { Paths = [huge] });

        // Act: send, then remove the chip and send again
        var firstTry = await viewModel.SendAsync(TestContext.Current.CancellationToken);
        var statusAfterFirstTry = viewModel.StatusText;
        var attachmentsFolderExists = Directory.Exists(Path.Combine(project, "attachments"));
        var hasChipBefore = viewModel.Document.Chips.Any();
        viewModel.DeleteChip(viewModel.Document.Length - 1);
        var secondTry = await viewModel.SendAsync(TestContext.Current.CancellationToken);

        // Assert: the first send kept the draft and said which attachment is the problem, the project has no copy, and the
        // second send worked
        Assert.False(firstTry);
        Assert.True(hasChipBefore);
        Assert.Contains("huge.bin", statusAfterFirstTry, StringComparison.Ordinal);
        Assert.False(attachmentsFolderExists);
        Assert.True(secondTry);
        Assert.Equal(0, viewModel.Document.Length);
    }

    /// <summary>
    ///     Test that sending is held back while the owner blocks it, over the real store and payload builder: the message
    ///     can be written meanwhile, nothing reaches the project, and the whole draft goes out once the block is lifted.
    /// </summary>
    [Fact]
    public async Task ComposerSubsystem_BlockSendingThroughViewModel_WritesMeanwhileAndSendsTheWholeDraftAfterwards()
    {
        // Arrange: a project, a view model over the real store and payload builder, and a block
        var project = Directory.CreateDirectory(Path.Combine(_root, "project")).FullName;
        var external = Directory.CreateDirectory(Path.Combine(_root, "external")).FullName;
        var file = WriteFile(Path.Combine(external, "brief.txt"), [1, 2, 3]);
        using var viewModel = new ComposerViewModel(new PayloadBuilder(new FakeMarkImageComposer()), new MarkRegistry(), new FakeDictationService());
        viewModel.SetProject(project);
        var sentMessages = new List<SentMessage>();
        viewModel.MessageSent += (_, e) => sentMessages.Add(e.Message);
        viewModel.BlockSending(ComposerViewModel.CopilotWorkingText);

        // Act: write and attach while blocked, try to send, then lift the block and send
        viewModel.InsertTyped("Next, ");
        viewModel.HandleDrop(new DroppedContent { Paths = [file] });
        var refused = await viewModel.SendAsync(TestContext.Current.CancellationToken);
        var attachmentsWhileBlocked = Directory.Exists(Path.Combine(project, "attachments"));
        viewModel.UnblockSending();
        var sent = await viewModel.SendAsync(TestContext.Current.CancellationToken);

        // Assert: nothing was sent or copied while blocked, and the whole draft was sent afterwards
        Assert.False(refused);
        Assert.False(attachmentsWhileBlocked);
        Assert.True(sent);
        var payload = Assert.IsType<MessagePayload>(Assert.Single(sentMessages).Payload);
        Assert.StartsWith("Next, [Attachment 1: brief.txt]", payload.Text, StringComparison.Ordinal);
        Assert.True(File.Exists(Path.Combine(project, "attachments", "brief.txt")));
    }

    /// <summary>
    ///     Test that a message written through the view model with every kind of action, over the real store and intake,
    ///     is undone and redone one step at a time with every step passing through the state it had when it was made.
    /// </summary>
    [Fact]
    public void ComposerSubsystem_EditThroughViewModel_MixedActionsUndoAndRedoOneStepEach()
    {
        // Arrange: a project, a view model over the real store and intake, and files from outside
        var project = Directory.CreateDirectory(Path.Combine(_root, "project")).FullName;
        var external = Directory.CreateDirectory(Path.Combine(_root, "external")).FullName;
        var marks = new MarkRegistry();
        using var viewModel = new ComposerViewModel(new PayloadBuilder(new FakeMarkImageComposer()), marks, new FakeDictationService());
        viewModel.SetProject(project);
        var mark = marks.Add(AnnotationTestPictures.Picture(), MarkKind.Rectangle, [new RenderPoint(10, 10), new RenderPoint(60, 40)]);
        var states = new List<ComposerSnapshot> { viewModel.Document.Snapshot() };

        // Act: type two characters, dictate a block, drop two files and a pasted picture, add a mark, delete a chip, and
        // delete the character before the caret, each as the user's action
        viewModel.InsertTyped("H");
        states.Add(viewModel.Document.Snapshot());
        viewModel.InsertTyped("i");
        states.Add(viewModel.Document.Snapshot());
        viewModel.AddDictatedBlock(" there my friend");
        states.Add(viewModel.Document.Snapshot());
        viewModel.HandleDrop(new DroppedContent
        {
            Paths = [WriteFile(Path.Combine(external, "a.txt"), [1]), WriteFile(Path.Combine(external, "b.txt"), [2])],
            Images = [PngBytes],
        });
        states.Add(viewModel.Document.Snapshot());
        viewModel.AddMark(mark);
        states.Add(viewModel.Document.Snapshot());
        viewModel.DeleteChip(viewModel.Document.Length - 1);
        states.Add(viewModel.Document.Snapshot());
        viewModel.DeleteBackward();
        states.Add(viewModel.Document.Snapshot());

        // Assert: undo walks back through every state, redo walks forward through the same ones, and nothing was copied
        // into the project while drafting
        for (var index = states.Count - 2; index >= 0; index--)
        {
            viewModel.Undo();
            Assert.True(viewModel.Document.Snapshot().HasSameContentAs(states[index]), $"state {index} after undo");
        }

        for (var index = 1; index < states.Count; index++)
        {
            viewModel.Redo();
            Assert.True(viewModel.Document.Snapshot().HasSameContentAs(states[index]), $"state {index} after redo");
        }

        Assert.False(viewModel.CanRedo);
        Assert.False(Directory.Exists(Path.Combine(project, "attachments")));
    }

    /// <summary>
    ///     Test that dictating through the view model adds the words as one dictated block at the caret that one undo
    ///     removes, and that sending it keeps the origin of the words.
    /// </summary>
    [Fact]
    public async Task ComposerSubsystem_DictateThroughViewModel_HoldAndRelease_AddsOneDictatedBlockAsOneStep()
    {
        // Arrange: a project, a view model with a dictation service that hears words, and a typed start
        var project = Directory.CreateDirectory(Path.Combine(_root, "project")).FullName;
        var dictation = new FakeDictationService();
        dictation.Session.FinalText = "make the second box blue";
        using var viewModel = new ComposerViewModel(new PayloadBuilder(new FakeMarkImageComposer()), new MarkRegistry(), dictation);
        viewModel.SetProject(project);
        viewModel.InsertTyped("Please ");
        SentMessage? sentMessage = null;
        viewModel.MessageSent += (_, e) => sentMessage = e.Message;

        // Act: hold the talk button, hear words, and let go
        await viewModel.BeginPushToTalkAsync();
        dictation.Session.Say("make the second");
        var live = viewModel.LivePartialText;
        await viewModel.EndPushToTalkAsync();
        var afterRelease = viewModel.Document.Snapshot();
        viewModel.Undo();
        var afterUndo = viewModel.Document.FlatText;
        viewModel.Redo();
        await viewModel.SendAsync(TestContext.Current.CancellationToken);

        // Assert: the live words were shown, the block is dictated text after the typed text, undo removed it as one step,
        // and the payload keeps the origin of the words
        Assert.Equal("make the second", live);
        Assert.Equal(new TextSegment("make the second box blue", TextOrigin.Dictated), afterRelease.Segments[1]);
        Assert.Equal("Please ", afterUndo);
        var payload = Assert.IsType<MessagePayload>(Assert.IsType<SentMessage>(sentMessage).Payload);
        Assert.Equal(
            [new PayloadTextPart("Please ", TextOrigin.Typed), new PayloadTextPart("make the second box blue", TextOrigin.Dictated)],
            payload.Segments);
    }

    /// <summary>
    ///     Creates the units of a draft over a project folder and a folder for content from outside the project.
    /// </summary>
    /// <returns>The store, the intake, the document, the history, and the folder for outside content.</returns>
    private (AttachmentStore Store, AttachmentIntake Intake, ComposerDocument Document, ComposerHistory History, string External) CreateDraft()
    {
        var project = Directory.CreateDirectory(Path.Combine(_root, "project")).FullName;
        var external = Directory.CreateDirectory(Path.Combine(_root, "external")).FullName;
        var store = new AttachmentStore(project);
        var document = new ComposerDocument();
        return (store, new AttachmentIntake(store), document, new ComposerHistory(document), external);
    }

    /// <summary>
    ///     Writes a file, creating its folders.
    /// </summary>
    /// <param name="path">The full path of the file.</param>
    /// <param name="bytes">The content.</param>
    /// <returns>The path.</returns>
    private static string WriteFile(string path, byte[] bytes)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllBytes(path, bytes);
        return path;
    }
}
