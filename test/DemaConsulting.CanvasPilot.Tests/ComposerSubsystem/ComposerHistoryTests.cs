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
///     Tests for the <see cref="ComposerHistory" /> unit and the <see cref="ComposerEdit" /> factories that give each
///     user action its undo granularity. There is one test for each rule of the granularity.
/// </summary>
public sealed class ComposerHistoryTests
{
    /// <summary>
    ///     The character that stands for a chip in the flat text.
    /// </summary>
    private const string Chip = "\uFFFC";
    /// <summary>
    ///     The document the history works on.
    /// </summary>
    private readonly ComposerDocument _document = new();

    /// <summary>
    ///     The history under test.
    /// </summary>
    private readonly ComposerHistory _history;

    /// <summary>
    ///     Initializes a new instance of the <see cref="ComposerHistoryTests" /> class with an empty draft.
    /// </summary>
    public ComposerHistoryTests()
    {
        _history = new ComposerHistory(_document);
    }

    /// <summary>
    ///     Test that every typed character is its own step and each undo takes back exactly one character.
    /// </summary>
    [Fact]
    public void ComposerHistory_TypedCharacters_EachIsOneStep()
    {
        // Arrange: nothing typed yet
        // Act: type "abc" one character at a time, as the editing control reports it
        foreach (var character in "abc")
        {
            _history.Execute(ComposerEdit.TypeText(_document, _document.Length, character.ToString()));
        }

        // Assert: three steps, and each undo removes the last character only
        Assert.Equal(3, _history.UndoCount);
        Assert.Equal("abc", _document.FlatText);
        _history.Undo();
        Assert.Equal("ab", _document.FlatText);
        _history.Undo();
        Assert.Equal("a", _document.FlatText);
        _history.Undo();
        Assert.Equal(string.Empty, _document.FlatText);
        Assert.False(_history.CanUndo);
    }

    /// <summary>
    ///     Test that Backspace and Delete each remove one character as one step.
    /// </summary>
    [Fact]
    public void ComposerHistory_BackspaceAndDelete_EachCharacterIsOneStep()
    {
        // Arrange: a draft "abcd", which is the starting point and not a step
        _document.InsertText(0, "abcd", TextOrigin.Typed);

        // Act: Backspace with the caret after "b", then Delete with the caret before "c" (now at offset 1)
        _history.Execute(ComposerEdit.Delete(_document, 1, 1));
        _history.Execute(ComposerEdit.Delete(_document, 1, 1, caretBefore: 1));

        // Assert: two steps; undo brings the characters back one at a time
        Assert.Equal("ad", _document.FlatText);
        Assert.Equal(2, _history.UndoCount);
        _history.Undo();
        Assert.Equal("acd", _document.FlatText);
        _history.Undo();
        Assert.Equal("abcd", _document.FlatText);
    }

    /// <summary>
    ///     Test that deleting a selection is one step however many characters it covers.
    /// </summary>
    [Fact]
    public void ComposerHistory_DeleteSelection_IsOneStep()
    {
        // Arrange: a draft with text and a chip, and a selection that covers both
        _document.InsertText(0, "abcd", TextOrigin.Typed);
        _document.InsertChip(2, new FileChip(@"C:\a.txt"));

        // Act: delete the selection of three positions: the second character, the chip, and the third character
        _history.Execute(ComposerEdit.Delete(_document, 1, 3));

        // Assert: one step that undo brings back completely
        Assert.Equal(1, _history.UndoCount);
        Assert.Equal("ad", _document.FlatText);
        _history.Undo();
        Assert.Equal("ab" + Chip + "cd", _document.FlatText);
        Assert.IsType<FileChip>(Assert.Single(_document.Chips));
    }

    /// <summary>
    ///     Test that typing over a selection replaces it as one step.
    /// </summary>
    [Fact]
    public void ComposerHistory_TypeOverSelection_IsOneStep()
    {
        // Arrange: a draft with a three character selection
        _document.InsertText(0, "abcdef", TextOrigin.Typed);

        // Act: type "X" over "bcd"
        _history.Execute(ComposerEdit.TypeText(_document, 1, "X", replaceLength: 3));

        // Assert: one step that is undone in one go
        Assert.Equal("aXef", _document.FlatText);
        Assert.Equal(1, _history.UndoCount);
        _history.Undo();
        Assert.Equal("abcdef", _document.FlatText);
    }

    /// <summary>
    ///     Test that a whole dictated block is one step, whatever its length.
    /// </summary>
    [Fact]
    public void ComposerHistory_DictatedBlock_IsOneStep()
    {
        // Arrange: typed text before the caret
        _history.Execute(ComposerEdit.TypeText(_document, 0, "Please "));

        // Act: dictate a block of many words
        _history.Execute(ComposerEdit.Dictate(_document, _document.Length, "make the boxes blue and add an arrow"));

        // Assert: two steps in all; one undo takes the whole block and nothing else
        Assert.Equal(2, _history.UndoCount);
        _history.Undo();
        Assert.Equal("Please ", _document.FlatText);
        Assert.Single(_document.Segments);
        _history.Redo();
        Assert.Equal(TextOrigin.Dictated, ((TextSegment)_document.Segments[^1]).Origin);
    }

    /// <summary>
    ///     Test that a drop of several items, chips and text together, is one step.
    /// </summary>
    [Fact]
    public void ComposerHistory_DropOfSeveralItems_IsOneStep()
    {
        // Arrange: typed text and a drop of three chips and a text
        _history.Execute(ComposerEdit.TypeText(_document, 0, "see "));
        var items = new ComposerSegment[]
        {
            new FileChip(@"C:\a.txt"),
            new FolderChip(@"C:\data"),
            new ImageChip(@"C:\b.png"),
            new TextSegment(" and more", TextOrigin.Typed)
        };

        // Act: drop all items at the end
        _history.Execute(ComposerEdit.Drop(_document, _document.Length, items));

        // Assert: one step for the drop; undo removes all items at once
        Assert.Equal(2, _history.UndoCount);
        Assert.Equal(3, _document.Chips.Count());
        _history.Undo();
        Assert.Equal("see ", _document.FlatText);
        Assert.Empty(_document.Chips);
    }

    /// <summary>
    ///     Test that pasting text is one step, however long the text is.
    /// </summary>
    [Fact]
    public void ComposerHistory_TextPaste_IsOneStep()
    {
        // Arrange: an empty draft
        // Act: paste a long text with line breaks
        _history.Execute(ComposerEdit.Paste(_document, 0, "line one\r\nline two\r\nline three"));

        // Assert: one step that undo removes completely
        Assert.Equal(1, _history.UndoCount);
        Assert.Equal("line one\nline two\nline three", _document.FlatText);
        _history.Undo();
        Assert.Equal(0, _document.Length);
    }

    /// <summary>
    ///     Test that adding chips to the row at the start is one step that puts them after the chips that are already there
    ///     and a line feed below the row, keeps the words after the line feed, and is undone and redone as one.
    /// </summary>
    [Fact]
    public void ComposerHistory_AddLeadingChips_PutsChipsInTheRowAboveTheWordsInOneStep()
    {
        // Arrange: a message with a chip in the row, a line feed, and words, and the caret at the end of the words
        var first = new MarkChip(Guid.NewGuid(), 1);
        var second = new MarkChip(Guid.NewGuid(), 2);
        _history.Execute(ComposerEdit.AddLeadingChips(_document, [first], 0));
        _history.Execute(ComposerEdit.TypeText(_document, 2, "words"));
        var undoCount = _history.UndoCount;

        // Act: add another chip with the caret at the end of the words
        var edit = ComposerEdit.AddLeadingChips(_document, [second], _document.Length);
        _history.Execute(edit);

        // Assert: the chip is next to the first one, no second line feed was added, the caret moved with the words, and
        // it is one step
        Assert.Equal(Chip + Chip + "\nwords", _document.FlatText);
        Assert.Equal([first, second], _document.Chips);
        Assert.Equal(_document.Length, edit.CaretAfter);
        Assert.Equal(undoCount + 1, _history.UndoCount);
        _history.Undo();
        Assert.Equal(Chip + "\nwords", _document.FlatText);
        _history.Redo();
        Assert.Equal(Chip + Chip + "\nwords", _document.FlatText);
    }

    /// <summary>
    ///     Test that chips added to an empty message are followed by a line feed and that the caret goes below the row,
    ///     where the words are typed.
    /// </summary>
    [Fact]
    public void ComposerHistory_AddLeadingChips_EmptyMessage_AddsTheLineFeedAndPutsTheCaretBelowTheRow()
    {
        // Arrange: an empty message and two chips
        var chips = new ChipSegment[] { new MarkChip(Guid.NewGuid(), 1), new MarkChip(Guid.NewGuid(), 2) };

        // Act: add them
        var edit = ComposerEdit.AddLeadingChips(_document, chips, 0);
        _history.Execute(edit);

        // Assert: the row, the line feed, and the caret on the line below
        Assert.Equal(Chip + Chip + "\n", _document.FlatText);
        Assert.Equal(3, edit.CaretAfter);
        Assert.True(ComposerEdit.AddLeadingChips(_document, [], 3).IsEmpty);
    }

    /// <summary>
    ///     Test that adding a mark is one step that removes the mark's chip when undone.
    /// </summary>
    [Fact]
    public void ComposerHistory_AddMark_IsOneStep()
    {
        // Arrange: typed text and a mark
        _history.Execute(ComposerEdit.TypeText(_document, 0, "move this"));
        var mark = new MarkChip(Guid.NewGuid(), 1);

        // Act: add the mark at the caret
        _history.Execute(ComposerEdit.AddMark(_document, 4, mark));

        // Assert: one step, which is undone and redone as one
        Assert.Equal(2, _history.UndoCount);
        Assert.Equal(mark, Assert.Single(_document.Chips));
        _history.Undo();
        Assert.Empty(_document.Chips);
        _history.Redo();
        Assert.Equal(mark, Assert.Single(_document.Chips));
        Assert.Equal("move" + Chip + " this", _document.FlatText);
    }

    /// <summary>
    ///     Test that deleting a chip is one step that brings the chip back when undone.
    /// </summary>
    [Fact]
    public void ComposerHistory_DeleteChip_IsOneStep()
    {
        // Arrange: a draft with a chip between two pieces of text, which is the starting point
        _document.InsertText(0, "ab", TextOrigin.Typed);
        var chip = new FileChip(@"C:\a.txt");
        _document.InsertChip(1, chip);

        // Act: delete the chip with its button
        _history.Execute(ComposerEdit.DeleteChip(_document, 1));

        // Assert: one step; undo puts the same chip back at the same place
        Assert.Equal(1, _history.UndoCount);
        Assert.Equal("ab", _document.FlatText);
        _history.Undo();
        Assert.Equal("a" + Chip + "b", _document.FlatText);
        Assert.Equal(chip, Assert.Single(_document.Chips));
    }

    /// <summary>
    ///     Test that a chip can only be deleted where there is one.
    /// </summary>
    [Fact]
    public void ComposerEdit_DeleteChip_OffsetOfTextOrEnd_ThrowsArgumentException()
    {
        // Arrange: a draft with text only
        _document.InsertText(0, "ab", TextOrigin.Typed);

        // Act and Assert: text at the offset and the end of the draft are both rejected
        Assert.Throws<ArgumentException>(() => ComposerEdit.DeleteChip(_document, 0));
        Assert.Throws<ArgumentException>(() => ComposerEdit.DeleteChip(_document, 2));
    }

    /// <summary>
    ///     Test that redo applies the same steps again, so a redo has the same granularity as the original action.
    /// </summary>
    [Fact]
    public void ComposerHistory_Redo_RestoresSameGranularity()
    {
        // Arrange: a draft built by a typed character, a dictated block, a drop of two items, and a mark
        _history.Execute(ComposerEdit.TypeText(_document, 0, "a"));
        _history.Execute(ComposerEdit.Dictate(_document, 1, " spoken words here"));
        _history.Execute(ComposerEdit.Drop(_document, _document.Length, [new FileChip(@"C:\a.txt"), new FolderChip(@"C:\d")]));
        _history.Execute(ComposerEdit.AddMark(_document, _document.Length, new MarkChip(Guid.NewGuid(), 1)));
        var complete = _document.Snapshot();

        // Act: undo everything, then redo one step at a time
        var states = new List<ComposerSnapshot>();
        while (_history.CanUndo)
        {
            states.Add(_document.Snapshot());
            _history.Undo();
        }

        // Assert: four undo steps, and four redo steps pass through the same states in the opposite order
        Assert.Equal(4, states.Count);
        Assert.Equal(0, _document.Length);
        Assert.Equal(4, _history.RedoCount);
        for (var index = states.Count - 1; index >= 0; index--)
        {
            _history.Redo();
            Assert.True(_document.Snapshot().HasSameContentAs(states[index]));
        }

        Assert.True(_document.Snapshot().HasSameContentAs(complete));
        Assert.False(_history.CanRedo);
    }

    /// <summary>
    ///     Test that undo and redo give back the caret of the step.
    /// </summary>
    [Fact]
    public void ComposerHistory_UndoAndRedo_RestoreCaret()
    {
        // Arrange: a draft "abc" and a Backspace after "b", which has the caret at offset 2 before and 1 after
        _document.InsertText(0, "abc", TextOrigin.Typed);
        _history.Execute(ComposerEdit.Delete(_document, 1, 1));

        // Act: undo and redo the step
        var undoCaret = _history.Undo();
        var redoCaret = _history.Redo();

        // Assert: undo returns the caret from before the step and redo the caret after it
        Assert.Equal(2, undoCaret);
        Assert.Equal(1, redoCaret);
    }

    /// <summary>
    ///     Test that a new edit after an undo discards the steps that could have been redone.
    /// </summary>
    [Fact]
    public void ComposerHistory_NewEditAfterUndo_ClearsRedo()
    {
        // Arrange: two typed characters, one of them undone
        _history.Execute(ComposerEdit.TypeText(_document, 0, "a"));
        _history.Execute(ComposerEdit.TypeText(_document, 1, "b"));
        _history.Undo();
        var redoBefore = _history.RedoCount;

        // Act: type another character
        _history.Execute(ComposerEdit.TypeText(_document, 1, "c"));

        // Assert: the undone step cannot be redone any more
        Assert.Equal(1, redoBefore);
        Assert.False(_history.CanRedo);
        Assert.Null(_history.Redo());
        Assert.Equal("ac", _document.FlatText);
    }

    /// <summary>
    ///     Test that clearing the history forgets every step and leaves the document alone.
    /// </summary>
    [Fact]
    public void ComposerHistory_Clear_EmptiesBothStacks()
    {
        // Arrange: a draft with one undone step and one step that can be undone
        _history.Execute(ComposerEdit.TypeText(_document, 0, "a"));
        _history.Execute(ComposerEdit.TypeText(_document, 1, "b"));
        _history.Undo();

        // Act: clear the history
        _history.Clear();

        // Assert: nothing to undo or redo and the text is as it was
        Assert.False(_history.CanUndo);
        Assert.False(_history.CanRedo);
        Assert.Equal("a", _document.FlatText);
    }

    /// <summary>
    ///     Test that sending clears the draft and both stacks and hands over what was written.
    /// </summary>
    [Fact]
    public void ComposerHistory_CompleteSend_ClearsStacksAndDocumentAndReturnsContent()
    {
        // Arrange: a draft with text and a chip, one step undone
        _history.Execute(ComposerEdit.TypeText(_document, 0, "send me"));
        _history.Execute(ComposerEdit.Drop(_document, 7, [new FileChip(@"C:\a.txt")]));
        _history.Execute(ComposerEdit.TypeText(_document, 8, "!"));
        _history.Undo();

        // Act: send
        var sent = _history.CompleteSend();

        // Assert: the content is returned, the draft is empty and nothing can be undone or redone
        Assert.Equal("send me" + Chip, sent.FlatText);
        Assert.Equal(0, _document.Length);
        Assert.False(_history.CanUndo);
        Assert.False(_history.CanRedo);
        Assert.Null(_history.Undo());
        Assert.Null(_history.Redo());
    }

    /// <summary>
    ///     Test that the oldest step is dropped when the depth limit is passed.
    /// </summary>
    [Fact]
    public void ComposerHistory_Limit_DropsOldestStep()
    {
        // Arrange: a history that keeps three steps
        var history = new ComposerHistory(_document, depthLimit: 3);

        // Act: type five characters
        foreach (var character in "abcde")
        {
            history.Execute(ComposerEdit.TypeText(_document, _document.Length, character.ToString()));
        }

        // Assert: only three steps can be undone, and the first two characters stay
        Assert.Equal(3, history.UndoCount);
        while (history.CanUndo)
        {
            history.Undo();
        }

        Assert.Equal("ab", _document.FlatText);
    }

    /// <summary>
    ///     Test that undo and redo with nothing to do change nothing.
    /// </summary>
    [Fact]
    public void ComposerHistory_UndoOnEmpty_DoesNothing()
    {
        // Arrange: a draft with text that was not typed through the history
        _document.InsertText(0, "start", TextOrigin.Typed);

        // Act: undo and redo
        var undo = _history.Undo();
        var redo = _history.Redo();

        // Assert: nothing happened
        Assert.Null(undo);
        Assert.Null(redo);
        Assert.Equal("start", _document.FlatText);
    }

    /// <summary>
    ///     Test that an edit that changes nothing, such as empty dictation, is not recorded as a step.
    /// </summary>
    [Fact]
    public void ComposerHistory_EmptyEdits_AreNotRecorded()
    {
        // Arrange: a draft with text
        _document.InsertText(0, "abc", TextOrigin.Typed);

        // Act: execute empty dictation, a text of only control characters, an empty drop, and a zero length delete
        var results = new[]
        {
            _history.Execute(ComposerEdit.Dictate(_document, 3, string.Empty)),
            _history.Execute(ComposerEdit.Paste(_document, 3, "\u0001" + Chip)),
            _history.Execute(ComposerEdit.Drop(_document, 3, [])),
            _history.Execute(ComposerEdit.Delete(_document, 1, 0))
        };

        // Assert: none was recorded
        Assert.All(results, recorded => Assert.False(recorded));
        Assert.False(_history.CanUndo);
        Assert.Equal("abc", _document.FlatText);
    }

    /// <summary>
    ///     Test that an edit made for another state of the document is refused without any change.
    /// </summary>
    [Fact]
    public void ComposerHistory_Execute_EditOfAnotherDocumentState_ThrowsAndChangesNothing()
    {
        // Arrange: an edit that deletes "b", and a document that has changed since
        _document.InsertText(0, "abc", TextOrigin.Typed);
        var edit = ComposerEdit.Delete(_document, 1, 1);
        _document.Delete(1, 1);
        _document.InsertText(1, "Z", TextOrigin.Typed);

        // Act and Assert: executing the edit throws, and neither the document nor the history changed
        Assert.Throws<InvalidOperationException>(() => _history.Execute(edit));
        Assert.Equal("aZc", _document.FlatText);
        Assert.False(_history.CanUndo);
    }

    /// <summary>
    ///     Test that a history made over a document that has content, as after a restart, has nothing to undo, so a
    ///     draft is never restored from earlier.
    /// </summary>
    [Fact]
    public void ComposerHistory_NewHistoryOverDocumentWithContent_HasNoSteps()
    {
        // Arrange: a document with content, such as one recreated from an earlier draft
        _document.InsertText(0, "earlier draft", TextOrigin.Typed);

        // Act: start a history over it
        var history = new ComposerHistory(_document);

        // Assert: the content is the start; there is nothing to undo or redo
        Assert.False(history.CanUndo);
        Assert.False(history.CanRedo);
        Assert.Equal(ComposerHistory.DefaultDepthLimit, history.DepthLimit);
        Assert.Same(_document, history.Document);
    }

    /// <summary>
    ///     Test that invalid constructor arguments are rejected.
    /// </summary>
    [Fact]
    public void ComposerHistory_Constructor_InvalidArguments_Throws()
    {
        // Arrange, Act, and Assert: a missing document and a limit below one are rejected
        Assert.Throws<ArgumentNullException>(() => new ComposerHistory(null!));
        Assert.Throws<ArgumentOutOfRangeException>(() => new ComposerHistory(_document, 0));
        Assert.Throws<ArgumentNullException>(() => _history.Execute(null!));
    }
}
