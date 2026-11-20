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

using DemaConsulting.CanvasPilot.ComposerSubsystem;

namespace DemaConsulting.CanvasPilot.Tests;

/// <summary>
///     Subsystem tests for the Composer subsystem: the document and the history working together on one draft.
/// </summary>
public sealed class ComposerSubsystemTests
{
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
}
