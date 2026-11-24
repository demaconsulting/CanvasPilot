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
using DemaConsulting.CanvasPilot.PreviewSubsystem;
using DemaConsulting.CanvasPilot.SpeechSubsystem;
using NSubstitute;

namespace DemaConsulting.CanvasPilot.Tests;

/// <summary>
///     Unit tests for <see cref="ComposerViewModel" />: each rule of the granularity of undo through the view model, the
///     commands and the notifications, dropped and pasted content, the project the message belongs to, sending, and
///     push-to-talk dictation. The attachment store is a substitute, the dictation service and the builder of sent
///     messages are controllable fakes, and the document, the history, the intake, and the marks are the real units.
/// </summary>
public sealed class ComposerViewModelTests : IDisposable
{
    /// <summary>
    ///     The character that stands for a chip in the flat text.
    /// </summary>
    private const string Chip = "\uFFFC";

    /// <summary>
    ///     The first bytes of a PNG file.
    /// </summary>
    private static readonly byte[] PngBytes = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 1];

    /// <summary>
    ///     Temporary folder that holds the project and the files that are dropped from outside it.
    /// </summary>
    private readonly string _root = Directory.CreateTempSubdirectory("canvaspilot-composer-model-tests-").FullName;

    /// <summary>
    ///     The builder of sent messages.
    /// </summary>
    private readonly FakeMessageBuilder _builder = new();

    /// <summary>
    ///     The dictation service.
    /// </summary>
    private readonly FakeDictationService _dictation = new();

    /// <summary>
    ///     The marks made for the draft.
    /// </summary>
    private readonly MarkRegistry _marks = new();

    /// <summary>
    ///     The stores that were made, by project folder.
    /// </summary>
    private readonly Dictionary<string, IAttachmentStore> _stores = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    ///     The number of pictures that the stores saved, which makes their paths different.
    /// </summary>
    private int _savedPictures;

    /// <summary>
    ///     The folder of the open project.
    /// </summary>
    private readonly string _project;

    /// <summary>
    ///     The view model under test, in an open project.
    /// </summary>
    private readonly ComposerViewModel _vm;

    /// <summary>
    ///     Initializes a new instance of the <see cref="ComposerViewModelTests" /> class with a view model in an open project.
    /// </summary>
    public ComposerViewModelTests()
    {
        _project = Directory.CreateDirectory(Path.Combine(_root, "project")).FullName;
        _vm = CreateViewModel();
        _vm.SetProject(_project);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _vm.Dispose();
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    // ----- one action, one step -----

    /// <summary>
    ///     Test that every typed character is its own step and each undo takes back exactly one character.
    /// </summary>
    [Fact]
    public void ComposerViewModel_InsertTyped_EachCharacterIsOneStep()
    {
        // Arrange: nothing typed yet
        // Act: type "abc" one character at a time, as the editing control reports it
        foreach (var character in "abc")
        {
            Assert.True(_vm.InsertTyped(character.ToString()));
        }

        // Assert: the text is complete, the caret is after it, and each undo removes the last character only
        Assert.Equal("abc", _vm.Document.FlatText);
        Assert.Equal(3, _vm.CaretOffset);
        _vm.Undo();
        Assert.Equal("ab", _vm.Document.FlatText);
        _vm.Undo();
        Assert.Equal("a", _vm.Document.FlatText);
        _vm.Undo();
        Assert.Equal(string.Empty, _vm.Document.FlatText);
        Assert.False(_vm.CanUndo);
    }

    /// <summary>
    ///     Test that a change reported by the editing control is applied as it says: text put at an offset, and removed
    ///     positions, both as typed text.
    /// </summary>
    [Fact]
    public void ComposerViewModel_ApplyTextChange_ReportedChange_IsAppliedAtItsOffsetAsTypedText()
    {
        // Arrange: a draft "Hello world"
        _vm.ApplyTextChange(0, 0, "Hello world");

        // Act: the control reports that "world" was replaced by "there"
        var changed = _vm.ApplyTextChange(6, 5, "there");

        // Assert: the text changed, the new text is typed, the caret is after it, and one undo restores "world"
        Assert.True(changed);
        Assert.Equal("Hello there", _vm.Document.FlatText);
        Assert.All(_vm.Document.Segments.OfType<TextSegment>(), segment => Assert.Equal(TextOrigin.Typed, segment.Origin));
        Assert.Equal(11, _vm.CaretOffset);
        _vm.Undo();
        Assert.Equal("Hello world", _vm.Document.FlatText);
    }

    /// <summary>
    ///     Test that Backspace and Delete each remove one character as one step.
    /// </summary>
    [Fact]
    public void ComposerViewModel_BackspaceAndDelete_EachCharacterIsOneStep()
    {
        // Arrange: a draft "abcd" typed as one paste, with the caret after "b"
        _vm.HandlePaste(new DroppedContent { Text = "abcd" });
        _vm.SetSelection(2);

        // Act: Backspace removes "b", then Delete removes "c"
        _vm.DeleteBackward();
        _vm.DeleteForward();

        // Assert: "ad" with two steps, and the caret between the remaining characters
        Assert.Equal("ad", _vm.Document.FlatText);
        Assert.Equal(1, _vm.CaretOffset);
        _vm.Undo();
        Assert.Equal("acd", _vm.Document.FlatText);
        _vm.Undo();
        Assert.Equal("abcd", _vm.Document.FlatText);
    }

    /// <summary>
    ///     Test that Backspace at the start and Delete at the end change nothing and are no step.
    /// </summary>
    [Fact]
    public void ComposerViewModel_BackspaceAtStartAndDeleteAtEnd_AreNoStep()
    {
        // Arrange: a draft "a" that is the starting point of the history (pasted, then undone is not needed: it is one step)
        _vm.InsertTyped("a");
        _vm.SetSelection(0);

        // Act: Backspace at the start, then Delete at the end
        var backspace = _vm.DeleteBackward();
        _vm.SetSelection(1);
        var delete = _vm.DeleteForward();

        // Assert: neither did anything, and the one step that exists is the typing
        Assert.False(backspace);
        Assert.False(delete);
        _vm.Undo();
        Assert.Equal(string.Empty, _vm.Document.FlatText);
        Assert.False(_vm.CanUndo);
    }

    /// <summary>
    ///     Test that deleting a selection is one step, however many characters and chips it covers.
    /// </summary>
    [Fact]
    public void ComposerViewModel_Delete_SelectionOfTextAndChip_IsOneStep()
    {
        // Arrange: "ab", a file chip, and "cd" in one draft
        _vm.InsertTyped("ab");
        _vm.HandleDrop(new DroppedContent { Paths = [CreateFile("a.txt")] });
        _vm.InsertTyped("cd");

        // Act: select "b", the chip, and "c", and press Backspace
        _vm.SetSelection(1, 3);
        _vm.DeleteBackward();

        // Assert: "ad" remains and one undo brings back everything that was selected
        Assert.Equal("ad", _vm.Document.FlatText);
        _vm.Undo();
        Assert.Equal("ab" + Chip + "cd", _vm.Document.FlatText);
    }

    /// <summary>
    ///     Test that typing over a selection replaces it and is one step.
    /// </summary>
    [Fact]
    public void ComposerViewModel_InsertTyped_OverSelection_IsOneStep()
    {
        // Arrange: a draft "abcd" with "bc" selected
        _vm.InsertTyped("abcd");
        _vm.SetSelection(1, 2);

        // Act: type "XYZ" over the selection
        _vm.InsertTyped("XYZ");

        // Assert: the selection was replaced, and one undo restores it
        Assert.Equal("aXYZd", _vm.Document.FlatText);
        Assert.Equal(4, _vm.CaretOffset);
        Assert.Equal(0, _vm.SelectionLength);
        _vm.Undo();
        Assert.Equal("abcd", _vm.Document.FlatText);
    }

    /// <summary>
    ///     Test that a dictated block is one step however many words it holds, and is dictated text.
    /// </summary>
    [Fact]
    public void ComposerViewModel_AddDictatedBlock_ManyWords_IsOneStepOfDictatedText()
    {
        // Arrange: a typed start
        _vm.InsertTyped("Make ");

        // Act: add a block of eight words
        var added = _vm.AddDictatedBlock("the second box blue and the arrow thicker");

        // Assert: the block is dictated text after the typed text, the caret is after it, and one undo removes all of it
        Assert.True(added);
        Assert.Collection(
            _vm.Document.Segments,
            segment => Assert.Equal(new TextSegment("Make ", TextOrigin.Typed), segment),
            segment => Assert.Equal(new TextSegment("the second box blue and the arrow thicker", TextOrigin.Dictated), segment));
        Assert.Equal(5 + 41, _vm.CaretOffset);
        _vm.Undo();
        Assert.Equal("Make ", _vm.Document.FlatText);
    }

    /// <summary>
    ///     Test that dictated text that is empty or only white space does not make a step.
    /// </summary>
    [Theory]
    [InlineData("")]
    [InlineData("\u0001\u0002")]
    public void ComposerViewModel_AddDictatedBlock_NothingToAdd_IsNoStep(string text)
    {
        // Arrange: a draft with one typed step
        _vm.InsertTyped("a");

        // Act: add nothing
        var added = _vm.AddDictatedBlock(text);

        // Assert: nothing changed, and the only step is still the typing
        Assert.False(added);
        Assert.Equal("a", _vm.Document.FlatText);
        _vm.Undo();
        Assert.False(_vm.CanUndo);
    }

    /// <summary>
    ///     Test that dictated text that looks like a chip or holds control characters stays text: voice cannot make a mark
    ///     or any other chip.
    /// </summary>
    [Fact]
    public void ComposerViewModel_AddDictatedBlock_TextThatLooksLikeAChip_StaysCleanedText()
    {
        // Arrange: nothing yet
        // Act: dictate text with the chip placeholder and a mark reference
        _vm.AddDictatedBlock("see [Mark 1]" + Chip + " now");

        // Assert: no chip was made and the placeholder was removed
        Assert.Empty(_vm.Document.Chips);
        Assert.Equal("see [Mark 1] now", _vm.Document.FlatText);
        Assert.Empty(_vm.MarkChipIds);
    }

    /// <summary>
    ///     Test that a drop of three files and two pictures is one step, and that one undo removes all five chips.
    /// </summary>
    [Fact]
    public void ComposerViewModel_HandleDrop_ThreeFilesAndTwoPictures_IsOneStep()
    {
        // Arrange: a typed start and content from outside the project
        _vm.InsertTyped("Hi ");
        var content = new DroppedContent
        {
            Paths = [CreateFile("a.txt"), CreateFile("b.txt"), CreateFile("c.txt")],
            Images = [PngBytes, PngBytes],
        };

        // Act: drop it at the caret
        var added = _vm.HandleDrop(content);

        // Assert: five chips were added at once, the caret is after them, and one undo removes them all
        Assert.True(added);
        Assert.Equal(5, _vm.Document.Chips.Count());
        Assert.Equal(3, _vm.Document.Chips.OfType<FileChip>().Count());
        Assert.Equal(2, _vm.Document.Chips.OfType<ImageChip>().Count(chip => chip.IsPasted));
        Assert.Equal(8, _vm.CaretOffset);
        _vm.Undo();
        Assert.Equal("Hi ", _vm.Document.FlatText);
        Assert.Equal(3, _vm.CaretOffset);
    }

    /// <summary>
    ///     Test that a drop at an offset puts the items there and not at the caret.
    /// </summary>
    [Fact]
    public void ComposerViewModel_HandleDrop_AtOffset_PutsItemsThere()
    {
        // Arrange: a draft "abcd" with the caret at the end
        _vm.InsertTyped("abcd");

        // Act: drop a file between "ab" and "cd"
        _vm.HandleDrop(new DroppedContent { Paths = [CreateFile("a.txt")] }, offset: 2);

        // Assert: the chip is between the texts
        Assert.Equal("ab" + Chip + "cd", _vm.Document.FlatText);
        Assert.Equal(3, _vm.CaretOffset);
    }

    /// <summary>
    ///     Test that a paste of plain text is one step however long it is, and a paste of items replaces the selection.
    /// </summary>
    [Fact]
    public void ComposerViewModel_HandlePaste_TextAndItems_AreOneStepAndReplaceSelection()
    {
        // Arrange: a draft "a b c d" with "b c" selected
        _vm.InsertTyped("a b c d");
        _vm.SetSelection(2, 3);

        // Act: paste a long text, then paste a file
        _vm.HandlePaste(new DroppedContent { Text = "one two three four five" });
        var afterText = _vm.Document.FlatText;
        _vm.HandlePaste(new DroppedContent { Paths = [CreateFile("a.txt")] });

        // Assert: each paste was one step: the selection was replaced by the text, and the file chip added at the caret
        Assert.Equal("a one two three four five d", afterText);
        Assert.Equal("a one two three four five" + Chip + " d", _vm.Document.FlatText);
        _vm.Undo();
        Assert.Equal(afterText, _vm.Document.FlatText);
        _vm.Undo();
        Assert.Equal("a b c d", _vm.Document.FlatText);
    }

    /// <summary>
    ///     Test that adding a mark is one step that inserts its chip into the row of chips at the start of the message with a
    ///     line break below the row, that the caret stays after the words, that undo removes the chip and the break, and that
    ///     redo brings them back.
    /// </summary>
    [Fact]
    public void ComposerViewModel_AddMark_IsOneStepAndUndoRemovesTheChip()
    {
        // Arrange: a typed start and a mark that was drawn
        _vm.InsertTyped("see ");
        var mark = NewMark();

        // Act: add the mark's chip, undo, redo
        var added = _vm.AddMark(mark);
        var idsAfterAdd = _vm.MarkChipIds.ToList();
        _vm.Undo();
        var idsAfterUndo = _vm.MarkChipIds.ToList();
        _vm.Redo();

        // Assert: one chip with the mark's number at the caret, gone after undo, back after redo
        Assert.True(added);
        Assert.Equal([mark.Id], idsAfterAdd);
        Assert.Empty(idsAfterUndo);
        Assert.Equal([mark.Id], _vm.MarkChipIds);
        Assert.Equal(new MarkChip(mark.Id, mark.Number), _vm.Document.Chips.Single());
        Assert.Equal(Chip + "\nsee ", _vm.Document.FlatText);
        Assert.Equal(Chip.Length + 1 + "see ".Length, _vm.CaretOffset);
        Assert.Same(mark, _marks.Find(mark.Id));
    }

    /// <summary>
    ///     Test that a mark added to an empty message, and then a second one, are both in the row of chips, with one line
    ///     feed below the row, and that the caret is below the row where the words go.
    /// </summary>
    [Fact]
    public void ComposerViewModel_AddMark_EmptyMessageAndSecondMark_KeepsOneRowAboveTheWords()
    {
        // Arrange: two marks that were drawn
        var first = NewMark();
        var second = NewMark();

        // Act: add both, and then type
        _vm.AddMark(first);
        _vm.AddMark(second);
        var caretBeforeTyping = _vm.CaretOffset;
        _vm.InsertTyped("words");

        // Assert: the row, one line feed, and the words; the caret was below the row before the words were typed
        Assert.Equal(Chip + Chip + "\nwords", _vm.Document.FlatText);
        Assert.Equal([first.Id, second.Id], _vm.MarkChipIds);
        Assert.Equal(3, caretBeforeTyping);
        Assert.Equal(_vm.Document.Length, _vm.CaretOffset);
    }

    /// <summary>
    ///     Test that words typed, dictated, or pasted with the caret in or at the end of the row of chips go to the line
    ///     below the row, and that a caret on the line of the words, or a selection, keeps its place.
    /// </summary>
    [Fact]
    public void ComposerViewModel_NewText_CaretInTheChipRow_GoesBelowTheRow()
    {
        // Arrange: a message that is one mark chip and a line feed, with the caret put at the end of the row, as a click does
        _vm.AddMark(NewMark());
        _vm.SetSelection(1);

        // Act: type, dictate, and paste with the caret in the row, going back into the row each time
        var offsetInRow = _vm.CaretForNewText();
        _vm.InsertTyped("typed ");
        _vm.SetSelection(0);
        _vm.AddDictatedBlock("spoken ");
        _vm.SetSelection(1);
        _vm.HandlePaste(new DroppedContent { Text = "pasted " });
        _vm.SetSelection(3);
        var offsetInWords = _vm.CaretForNewText();

        // Assert: the words are all below the row, each put at the start of the line of the words, one line feed separates
        // them from the chip, and the caret in the words stays
        Assert.Equal(2, offsetInRow);
        Assert.Equal(Chip + "\npasted spoken typed ", _vm.Document.FlatText);
        Assert.Equal(3, offsetInWords);
    }

    /// <summary>
    ///     Test that files chosen with the attach button go to the row of chips above the words as one step, wherever the
    ///     caret is, and that a drop at the caret still puts them in the text.
    /// </summary>
    [Fact]
    public void ComposerViewModel_AttachFiles_GoToTheRowAboveTheWordsInOneStep()
    {
        // Arrange: a typed start with the caret at its end
        _vm.InsertTyped("Hi ");

        // Act: attach two files, and then drop a third at the caret
        var attached = _vm.AttachFiles(new DroppedContent { Paths = [CreateFile("a.txt"), CreateFile("b.txt")] });
        var afterAttach = _vm.Document.FlatText;
        _vm.Undo();
        var afterUndo = _vm.Document.FlatText;
        _vm.Redo();
        _vm.HandleDrop(new DroppedContent { Paths = [CreateFile("c.txt")] });

        // Assert: the two chips, a line feed, and the words, as one step; the dropped file is after the words
        Assert.True(attached);
        Assert.Equal(Chip + Chip + "\nHi ", afterAttach);
        Assert.Equal("Hi ", afterUndo);
        Assert.Equal(Chip + Chip + "\nHi " + Chip, _vm.Document.FlatText);
        Assert.Throws<ArgumentNullException>(() => _vm.AttachFiles(null!));
    }

    /// <summary>
    ///     Test that a mark that was not made in the registry of the composer is refused.
    /// </summary>
    [Fact]
    public void ComposerViewModel_AddMark_MarkOfAnotherRegistry_ThrowsArgumentException()
    {
        // Arrange: a mark made elsewhere
        var foreign = new MarkRegistry().Add(AnnotationTestPictures.Picture(), MarkKind.Rectangle, [new RenderPoint(1, 1), new RenderPoint(20, 20)]);

        // Act / Assert: it is refused and the draft is unchanged
        Assert.Throws<ArgumentException>(() => _vm.AddMark(foreign));
        Assert.Throws<ArgumentNullException>(() => _vm.AddMark(null!));
        Assert.Empty(_vm.Document.Chips);
    }

    /// <summary>
    ///     Test that the composer gives the current note of a mark from its registry and passes each change of a note
    ///     on to its observers, until it is disposed.
    /// </summary>
    [Fact]
    public void ComposerViewModel_GetMarkNote_NoteChangedInRegistry_IsGivenAndAnnounced()
    {
        // Arrange: a mark with a note whose chip is in the message, and an observer of note changes
        var mark = _marks.Add(AnnotationTestPictures.Picture(), MarkKind.Rectangle, [new RenderPoint(10, 10), new RenderPoint(60, 40)], "before");
        _vm.AddMark(mark);
        var announced = new List<Mark>();
        _vm.MarkNoteChanged += (_, e) => announced.Add(e.Mark);
        var before = _vm.GetMarkNote(mark.Id);

        // Act: change the note in the registry
        var changed = _marks.ChangeNote(mark.Id, "after");

        // Assert: the composer gave the note before and after, announced the change once, and knows no stranger
        Assert.Equal("before", before);
        Assert.Equal("after", _vm.GetMarkNote(mark.Id));
        Assert.Equal([changed!], announced);
        Assert.Null(_vm.GetMarkNote(Guid.NewGuid()));

        // Act: dispose the composer and change the note again
        _vm.Dispose();
        _marks.ChangeNote(mark.Id, "later");

        // Assert: nothing more was announced
        Assert.Single(announced);
    }

    /// <summary>
    ///     Test that a request to change the note of a mark whose chip is in an editable message is passed on with the
    ///     mark, and that it changes neither the message, the caret, the status, nor the undo history.
    /// </summary>
    [Fact]
    public void ComposerViewModel_RequestMarkNoteEdit_ChipInEditableMessage_RaisesRequestWithoutAStep()
    {
        // Arrange: a message with words and a mark chip, and an observer of requests and changes
        _vm.InsertTyped("see");
        var mark = NewMark();
        _vm.AddMark(mark);
        _vm.Undo();
        _vm.Redo();
        var requested = new List<Guid>();
        var changed = 0;
        _vm.MarkNoteEditRequested += (_, e) => requested.Add(e.MarkId);
        _vm.Changed += (_, _) => changed++;
        var before = (_vm.Document.FlatText, _vm.CaretOffset, _vm.CanUndo, _vm.CanRedo, _vm.StatusText);

        // Act: ask to change the note of the mark
        var passed = _vm.RequestMarkNoteEdit(mark.Id);

        // Assert: the request was passed on once with the mark, and nothing of the message changed
        Assert.True(passed);
        Assert.Equal([mark.Id], requested);
        Assert.Equal(0, changed);
        Assert.Equal(before, (_vm.Document.FlatText, _vm.CaretOffset, _vm.CanUndo, _vm.CanRedo, _vm.StatusText));
    }

    /// <summary>
    ///     Test that a request to change a note is refused for a mark without a chip in the message, while no project is
    ///     open, and while the message is being sent.
    /// </summary>
    [Fact]
    public async Task ComposerViewModel_RequestMarkNoteEdit_NoChipNoProjectOrSending_IsRefused()
    {
        // Arrange: a mark with a chip, a mark without one, and an observer of requests
        var withChip = NewMark();
        var withoutChip = NewMark();
        _vm.AddMark(withChip);
        var requested = 0;
        _vm.MarkNoteEditRequested += (_, _) => requested++;

        // Act: ask for the mark without a chip, then for the other while a send waits in the builder
        var noChip = _vm.RequestMarkNoteEdit(withoutChip.Id);
        _builder.Gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var send = _vm.SendAsync(TestContext.Current.CancellationToken);
        Assert.True(SpinWait.SpinUntil(() => _builder.Messages.Count == 1, TimeSpan.FromSeconds(10)));
        var sending = _vm.RequestMarkNoteEdit(withChip.Id);
        _builder.Gate.SetResult();
        await send;

        // Act: put a chip in a new message, then close the project and ask again
        var next = NewMark();
        _vm.AddMark(next);
        _vm.SetProject(null);
        var noProject = _vm.RequestMarkNoteEdit(next.Id);

        // Assert: every request was refused and none was passed on
        Assert.False(noChip);
        Assert.False(sending);
        Assert.False(noProject);
        Assert.Equal(0, requested);
    }

    /// <summary>
    ///     Test that F2 beside a mark chip asks for the mark of the chip just before the caret, or else just after it, and
    ///     for nothing when no mark chip is beside the caret.
    /// </summary>
    [Fact]
    public void ComposerViewModel_RequestMarkNoteEditAtCaret_ChipBesideCaret_RequestsThatMark()
    {
        // Arrange: two mark chips in the row above the words "hi"
        var first = NewMark();
        var second = NewMark();
        _vm.AddMark(first);
        _vm.AddMark(second);
        _vm.InsertTyped("hi");
        var requested = new List<Guid>();
        _vm.MarkNoteEditRequested += (_, e) => requested.Add(e.MarkId);

        // Act: put the caret before the first chip, between the chips, after the second chip, and in the words
        _vm.SetSelection(0);
        var atStart = _vm.RequestMarkNoteEditAtCaret();
        _vm.SetSelection(1);
        var between = _vm.RequestMarkNoteEditAtCaret();
        _vm.SetSelection(2);
        var afterSecond = _vm.RequestMarkNoteEditAtCaret();
        _vm.SetSelection(4);
        var inWords = _vm.RequestMarkNoteEditAtCaret();

        // Assert: the chip after the caret at the start, the one before it otherwise, and nothing in the words
        Assert.Equal([true, true, true, false], new[] { atStart, between, afterSecond, inWords });
        Assert.Equal([first.Id, first.Id, second.Id], requested);
    }

    /// <summary>
    ///     Test that deleting a chip is one step and that undo puts it back at the same offset.
    /// </summary>
    [Fact]
    public void ComposerViewModel_DeleteChip_IsOneStepAndUndoRestoresItAtTheSameOffset()
    {
        // Arrange: "a", a file chip, and "b"
        _vm.InsertTyped("a");
        _vm.HandleDrop(new DroppedContent { Paths = [CreateFile("a.txt")] });
        _vm.InsertTyped("b");
        var chip = _vm.Document.Chips.Single();

        // Act: delete the chip with its button, then undo
        var deleted = _vm.DeleteChip(1);
        var textAfterDelete = _vm.Document.FlatText;
        _vm.Undo();

        // Assert: the chip went and came back between the texts
        Assert.True(deleted);
        Assert.Equal("ab", textAfterDelete);
        Assert.Equal(chip, _vm.Document.Segments[1]);
        Assert.Equal("a" + Chip + "b", _vm.Document.FlatText);
        Assert.Throws<ArgumentException>(() => _vm.DeleteChip(0));
    }

    /// <summary>
    ///     Test that redo gives back the same steps as undo took, one at a time, for every kind of action.
    /// </summary>
    [Fact]
    public void ComposerViewModel_Redo_MixedActions_GivesTheSameStepsBack()
    {
        // Arrange: typing, a dictated block, a drop, a mark, and a deleted chip, each recorded
        var states = new List<string> { _vm.Document.FlatText };
        _vm.InsertTyped("a");
        states.Add(_vm.Document.FlatText);
        _vm.InsertTyped("b");
        states.Add(_vm.Document.FlatText);
        _vm.AddDictatedBlock(" dictated words");
        states.Add(_vm.Document.FlatText);
        _vm.HandleDrop(new DroppedContent { Paths = [CreateFile("a.txt"), CreateFile("b.txt")] });
        states.Add(_vm.Document.FlatText);
        _vm.AddMark(NewMark());
        states.Add(_vm.Document.FlatText);
        _vm.DeleteChip(_vm.Document.Length - 1);
        states.Add(_vm.Document.FlatText);

        // Act / Assert: undo walks back through the states, redo walks forward through the same ones
        for (var index = states.Count - 2; index >= 0; index--)
        {
            _vm.Undo();
            Assert.Equal(states[index], _vm.Document.FlatText);
        }

        for (var index = 1; index < states.Count; index++)
        {
            _vm.Redo();
            Assert.Equal(states[index], _vm.Document.FlatText);
        }

        Assert.False(_vm.CanRedo);
    }

    /// <summary>
    ///     Test that a new action after an undo ends the steps that could have been redone.
    /// </summary>
    [Fact]
    public void ComposerViewModel_InsertTyped_AfterUndo_DiscardsRedo()
    {
        // Arrange: "ab" typed and the last character undone
        _vm.InsertTyped("a");
        _vm.InsertTyped("b");
        _vm.Undo();
        Assert.True(_vm.CanRedo);

        // Act: type something else
        _vm.InsertTyped("c");

        // Assert: nothing can be redone and the text is "ac"
        Assert.False(_vm.CanRedo);
        Assert.Equal("ac", _vm.Document.FlatText);
    }

    // ----- undo and redo as commands and as a target -----

    /// <summary>
    ///     Test that the undo and redo commands can execute exactly when there is a step, tell when that changes, and run
    ///     the step.
    /// </summary>
    [Fact]
    public void ComposerViewModel_UndoRedoCommands_CanExecuteFollowsHistoryAndRunsSteps()
    {
        // Arrange: an empty draft, and counters for the notifications of the two commands
        var undoChanges = 0;
        var redoChanges = 0;
        _vm.UndoCommand.CanExecuteChanged += (_, _) => undoChanges++;
        _vm.RedoCommand.CanExecuteChanged += (_, _) => redoChanges++;
        Assert.False(_vm.UndoCommand.CanExecute(null));
        Assert.False(_vm.RedoCommand.CanExecute(null));

        // Act: type, undo with the command, redo with the command
        _vm.InsertTyped("a");
        var canUndoAfterTyping = _vm.UndoCommand.CanExecute(null);
        _vm.UndoCommand.Execute(null);
        var canRedoAfterUndo = _vm.RedoCommand.CanExecute(null);
        _vm.RedoCommand.Execute(null);

        // Assert: the commands followed the history, were told about each change, and ran the steps
        Assert.True(canUndoAfterTyping);
        Assert.True(canRedoAfterUndo);
        Assert.Equal("a", _vm.Document.FlatText);
        Assert.True(undoChanges >= 3);
        Assert.True(redoChanges >= 2);
        Assert.False(_vm.RedoCommand.CanExecute(null));
    }

    /// <summary>
    ///     Test that the composer is an undo and redo target for the Edit menu: it tells when the state changes and
    ///     undoes and redoes through the interface.
    /// </summary>
    [Fact]
    public void ComposerViewModel_UndoRedoTarget_ReportsStateAndSteps()
    {
        // Arrange: the composer as the menu sees it, and a count of its notifications
        IUndoRedoTarget target = _vm;
        var notifications = 0;
        target.UndoRedoStateChanged += (_, _) => notifications++;
        var changed = new List<string?>();
        _vm.PropertyChanged += (_, e) => changed.Add(e.PropertyName);

        // Act: type, then undo and redo through the interface
        _vm.InsertTyped("a");
        var canUndo = target.CanUndo;
        target.Undo();
        var canRedo = target.CanRedo;
        target.Redo();

        // Assert: the interface saw the state, was told after each action, and the properties were announced
        Assert.True(canUndo);
        Assert.True(canRedo);
        Assert.Equal("a", _vm.Document.FlatText);
        Assert.Equal(3, notifications);
        Assert.Contains(nameof(ComposerViewModel.CanUndo), changed);
        Assert.Contains(nameof(ComposerViewModel.CanRedo), changed);
        Assert.Contains(nameof(ComposerViewModel.CanSend), changed);
    }

    /// <summary>
    ///     Test that undo and redo with nothing to do are ignored.
    /// </summary>
    [Fact]
    public void ComposerViewModel_UndoAndRedo_NothingToDo_DoNothing()
    {
        // Arrange: an empty draft and a count of changes
        var changes = 0;
        _vm.Changed += (_, _) => changes++;

        // Act: undo and redo
        _vm.Undo();
        _vm.Redo();

        // Assert: no change was reported
        Assert.Equal(0, changes);
    }

    /// <summary>
    ///     Test that undo puts the caret where it was before the step, and redo puts it where it was after.
    /// </summary>
    [Fact]
    public void ComposerViewModel_Undo_RestoresCaretOfTheStep()
    {
        // Arrange: "abcd" typed as a block, and a word dictated in the middle
        _vm.InsertTyped("abcd");
        _vm.SetSelection(2);
        _vm.AddDictatedBlock("XY");

        // Act / Assert: undo returns the caret to before the block, redo to after it
        _vm.Undo();
        Assert.Equal(2, _vm.CaretOffset);
        Assert.Equal("abcd", _vm.Document.FlatText);
        _vm.Redo();
        Assert.Equal(4, _vm.CaretOffset);
        Assert.Equal("abXYcd", _vm.Document.FlatText);
    }

    /// <summary>
    ///     Test that the caret and selection reported by the control are kept inside the message.
    /// </summary>
    [Fact]
    public void ComposerViewModel_SetSelection_OutsideMessage_IsBroughtBackInside()
    {
        // Arrange: a draft of four characters
        _vm.InsertTyped("abcd");

        // Act: report a caret and selection that stick out
        _vm.SetSelection(99, 5);
        var beyondEnd = (_vm.CaretOffset, _vm.SelectionLength);
        _vm.SetSelection(-3, 99);

        // Assert: the caret is clamped, and the selection cannot go past the end
        Assert.Equal((4, 0), beyondEnd);
        Assert.Equal((0, 4), (_vm.CaretOffset, _vm.SelectionLength));
    }

    /// <summary>
    ///     Test that the changed event is raised once for each action, whatever the action holds.
    /// </summary>
    [Fact]
    public void ComposerViewModel_Changed_EachActionRaisesItOnce()
    {
        // Arrange: a count of the events
        var changes = 0;
        _vm.Changed += (_, _) => changes++;
        var mark = NewMark();

        // Act / Assert: type, drop five items, dictate, add a mark, delete a chip, undo, and redo: one event each
        _vm.InsertTyped("a");
        Assert.Equal(1, changes);
        _vm.HandleDrop(new DroppedContent { Paths = [CreateFile("a.txt"), CreateFile("b.txt")], Images = [PngBytes] });
        Assert.Equal(2, changes);
        _vm.AddDictatedBlock("words");
        Assert.Equal(3, changes);
        _vm.AddMark(mark);
        Assert.Equal(4, changes);
        _vm.DeleteChip(0);
        Assert.Equal(5, changes);
        _vm.Undo();
        Assert.Equal(6, changes);
        _vm.Redo();
        Assert.Equal(7, changes);
    }

    // ----- dropped and pasted content -----

    /// <summary>
    ///     Test that a drop of nothing that can be accepted is no step and says what was left out and why.
    /// </summary>
    [Fact]
    public void ComposerViewModel_HandleDrop_NothingAccepted_IsNoStepAndNamesWhatWasLeftOut()
    {
        // Arrange: a path that does not exist
        var missing = Path.Combine(_root, "missing.txt");

        // Act: drop it
        var added = _vm.HandleDrop(new DroppedContent { Paths = [missing] });

        // Assert: nothing was added, nothing can be undone, and the status names the path
        Assert.False(added);
        Assert.Equal(0, _vm.Document.Length);
        Assert.False(_vm.CanUndo);
        Assert.StartsWith("Left out:", _vm.StatusText, StringComparison.Ordinal);
        Assert.Contains("missing.txt", _vm.StatusText, StringComparison.Ordinal);
        Assert.True(_vm.HasStatus);
    }

    /// <summary>
    ///     Test that a drop of good and bad items adds the good ones as one step and names the bad ones.
    /// </summary>
    [Fact]
    public void ComposerViewModel_HandleDrop_GoodAndBadItems_AddsTheGoodOnesAndNamesTheBadOnes()
    {
        // Arrange: one file that exists and one that does not
        var good = CreateFile("good.txt");
        var bad = Path.Combine(_root, "bad.txt");

        // Act: drop both
        var added = _vm.HandleDrop(new DroppedContent { Paths = [good, bad] });

        // Assert: one chip for the good file, one step, and a status that names the bad one
        Assert.True(added);
        Assert.Equal(good, Assert.IsType<FileChip>(Assert.Single(_vm.Document.Chips)).FullPath);
        Assert.Contains("bad.txt", _vm.StatusText, StringComparison.Ordinal);
        _vm.Undo();
        Assert.False(_vm.CanUndo);
    }

    /// <summary>
    ///     Test that a status names the first three items that were left out and says how many more there are.
    /// </summary>
    [Fact]
    public void ComposerViewModel_HandleDrop_ManyLeftOut_NamesThreeAndCountsTheRest()
    {
        // Arrange: five paths that do not exist
        var paths = Enumerable.Range(1, 5).Select(i => Path.Combine(_root, $"missing{i}.txt")).ToList();

        // Act: drop them
        _vm.HandleDrop(new DroppedContent { Paths = paths });

        // Assert: the first three are named and the rest are counted
        Assert.Contains("missing1.txt", _vm.StatusText, StringComparison.Ordinal);
        Assert.Contains("missing3.txt", _vm.StatusText, StringComparison.Ordinal);
        Assert.DoesNotContain("missing4.txt", _vm.StatusText, StringComparison.Ordinal);
        Assert.Contains("And 2 more", _vm.StatusText, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Test that a pasted picture that the store refuses is left out with the store's reason and the rest is still added.
    /// </summary>
    [Fact]
    public void ComposerViewModel_HandlePaste_PictureRefusedByStore_IsLeftOutWithReason()
    {
        // Arrange: a store that refuses the pasted bytes
        var store = _stores[_project];
        store.SavePastedImage(Arg.Any<ReadOnlyMemory<byte>>()).Returns(_ => throw new ArgumentException("The data is not a PNG picture."));

        // Act: paste a picture and some text
        var added = _vm.HandlePaste(new DroppedContent { Images = [new byte[] { 1, 2, 3 }], Text = "caption" });

        // Assert: the text was added and the status gives the store's reason
        Assert.True(added);
        Assert.Equal("caption", _vm.Document.FlatText);
        Assert.Contains("not a PNG", _vm.StatusText, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Test that a later action clears the sentence about the earlier one.
    /// </summary>
    [Fact]
    public void ComposerViewModel_StatusText_NextAction_ClearsIt()
    {
        // Arrange: a status from a refused drop
        _vm.HandleDrop(new DroppedContent { Paths = [Path.Combine(_root, "missing.txt")] });
        Assert.True(_vm.HasStatus);

        // Act: type
        _vm.InsertTyped("a");

        // Assert: the status is gone
        Assert.False(_vm.HasStatus);
        Assert.Equal(string.Empty, _vm.StatusText);
    }

    // ----- the project -----

    /// <summary>
    ///     Test that without a project nothing can be edited, and the status says to open one.
    /// </summary>
    [Fact]
    public void ComposerViewModel_NoProject_EditingIsRefusedAndStatusExplains()
    {
        // Arrange: a composer in no project
        using var vm = CreateViewModel();

        // Act: try each kind of change
        var typed = vm.InsertTyped("a");
        var dropped = vm.HandleDrop(new DroppedContent { Paths = [CreateFile("a.txt")] });
        var dictated = vm.AddDictatedBlock("words");

        // Assert: nothing changed and the composer says what to do
        Assert.False(typed);
        Assert.False(dropped);
        Assert.False(dictated);
        Assert.False(vm.HasProject);
        Assert.False(vm.CanEdit);
        Assert.False(vm.CanSend);
        Assert.Null(vm.ProjectFolder);
        Assert.Equal(0, vm.Document.Length);
        Assert.Equal(ComposerViewModel.NoProjectText, vm.StatusText);
    }

    /// <summary>
    ///     Test that changing the project drops the draft, its history, its marks, and the saved pictures, and that setting
    ///     the project that is open changes nothing.
    /// </summary>
    [Fact]
    public void ComposerViewModel_SetProject_OtherProject_DropsDraftHistoryMarksAndSavedPictures()
    {
        // Arrange: a draft with text, a chip, and a mark in one project
        _vm.InsertTyped("draft ");
        _vm.HandleDrop(new DroppedContent { Images = [PngBytes] });
        _vm.AddMark(NewMark());
        var other = Directory.CreateDirectory(Path.Combine(_root, "other")).FullName;
        _vm.SetProject(_project);
        Assert.NotEqual(0, _vm.Document.Length);
        _stores[_project].ClearReceivedCalls();

        // Act: open another project
        _vm.SetProject(other);

        // Assert: the draft, the history, and the marks are gone, the saved pictures of both projects were looked for, and
        // the composer works in the new project
        Assert.Equal(0, _vm.Document.Length);
        Assert.False(_vm.CanUndo);
        Assert.Empty(_marks.Marks);
        Assert.Equal(other, _vm.ProjectFolder);
        _stores[_project].Received(1).RemoveLeftovers(Arg.Is<IReadOnlyCollection<string>>(keep => keep.Count == 0));
        _stores[other].Received(1).RemoveLeftovers(Arg.Is<IReadOnlyCollection<string>>(keep => keep.Count == 0));
        Assert.True(_vm.InsertTyped("new"));
    }

    /// <summary>
    ///     Test that setting the project that is already open keeps the draft.
    /// </summary>
    [Fact]
    public void ComposerViewModel_SetProject_SameProject_KeepsDraft()
    {
        // Arrange: a draft
        _vm.InsertTyped("draft");

        // Act: set the same project again
        _vm.SetProject(_project);

        // Assert: the draft and its history are still there
        Assert.Equal("draft", _vm.Document.FlatText);
        Assert.True(_vm.CanUndo);
    }

    /// <summary>
    ///     Test that closing the project makes the composer unusable and drops the draft.
    /// </summary>
    [Fact]
    public void ComposerViewModel_SetProject_Null_ClosesTheProjectAndDropsTheDraft()
    {
        // Arrange: a draft
        _vm.InsertTyped("draft");

        // Act: close the project
        _vm.SetProject(null);

        // Assert: no draft, nothing to undo, and editing is refused
        Assert.False(_vm.HasProject);
        Assert.Equal(0, _vm.Document.Length);
        Assert.False(_vm.CanUndo);
        Assert.False(_vm.InsertTyped("a"));
        Assert.Equal(ComposerViewModel.NoProjectText, _vm.StatusText);
    }

    /// <summary>
    ///     Test that a project folder that the store cannot use leaves the composer without a project and says why.
    /// </summary>
    [Fact]
    public void ComposerViewModel_SetProject_UnusableFolder_ExplainsAndHasNoProject()
    {
        // Arrange: a composer whose store factory refuses the folder
        using var vm = new ComposerViewModel(_builder, _marks, _dictation, _ => throw new ArgumentException("The folder is not usable."));

        // Act: open the project
        vm.SetProject(_project);

        // Assert: no project, and the status explains
        Assert.False(vm.HasProject);
        Assert.Contains("The folder is not usable.", vm.StatusText, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Test that clearing the draft empties the message, the history, the marks, and the saved pictures.
    /// </summary>
    [Fact]
    public void ComposerViewModel_ClearDraft_EmptiesEverything()
    {
        // Arrange: a draft with text and a mark
        _vm.InsertTyped("draft");
        _vm.AddMark(NewMark());
        _stores[_project].ClearReceivedCalls();

        // Act: clear it
        _vm.ClearDraft();

        // Assert: nothing is left to undo or to send, the marks are gone, and the saved pictures were removed
        Assert.Equal(0, _vm.Document.Length);
        Assert.False(_vm.CanUndo);
        Assert.False(_vm.CanRedo);
        Assert.Empty(_marks.Marks);
        _stores[_project].Received(1).RemoveLeftovers(Arg.Is<IReadOnlyCollection<string>>(keep => keep.Count == 0));
    }

    // ----- sending -----

    /// <summary>
    ///     Test that sending stages the attachments, builds the message with the marks that have chips, empties the draft,
    ///     the history, and the marks, removes saved pictures, and raises the event with the immutable message.
    /// </summary>
    [Fact]
    public async Task ComposerViewModel_SendAsync_TextAttachmentAndMark_StagesBuildsClearsAndRaisesEvent()
    {
        // Arrange: a draft with text, a dictated block, a file, and a mark, and one more mark that has no chip
        _vm.InsertTyped("Look at ");
        _vm.AddDictatedBlock("this box");
        _vm.HandleDrop(new DroppedContent { Paths = [CreateFile("brief.txt")] });
        var mark = NewMark();
        _vm.AddMark(mark);
        var unused = NewMark();
        var chips = _vm.Document.Chips.OfType<AttachmentChip>().ToList();
        var sentMessages = new List<SentMessage>();
        _vm.MessageSent += (_, e) => sentMessages.Add(e.Message);
        _stores[_project].ClearReceivedCalls();

        // Act: send
        var sent = await _vm.SendAsync(TestContext.Current.CancellationToken);

        // Assert: the attachments were put into the project and given to the builder together with the message and the
        // one mark that has a chip
        Assert.True(sent);
        _stores[_project].Received(1).StageForSend(Arg.Is<IReadOnlyList<AttachmentChip>>(list => list.SequenceEqual(chips)));
        var built = Assert.Single(_builder.Messages);
        Assert.Equal(Chip + "\nLook at this box" + Chip, built.FlatText);
        Assert.Equal([mark], _builder.Marks.Single());
        Assert.NotNull(unused);
        Assert.Equal(chips, _builder.Attachments.Single().Select(item => item.Chip));

        // Assert: one event with the immutable message, whose payload came from the builder
        var message = Assert.Single(sentMessages);
        Assert.Equal(FakeMessageBuilder.Now, message.SentUtc);
        Assert.Equal("payload of: " + Chip + "\nLook at this box" + Chip, message.Payload);
        Assert.Equal([mark], message.Marks);
        Assert.Equal("attachments/brief.txt", Assert.Single(message.Attachments).ProjectRelativePath);

        // Assert: nothing is left in the composer and the saved pictures that nothing refers to were removed
        Assert.Equal(0, _vm.Document.Length);
        Assert.Equal(0, _vm.CaretOffset);
        Assert.False(_vm.CanUndo);
        Assert.False(_vm.CanRedo);
        Assert.False(_vm.CanSend);
        Assert.False(_vm.IsSending);
        Assert.Empty(_marks.Marks);
        Assert.Empty(_vm.MarkChipIds);
        _stores[_project].Received(1).RemoveLeftovers(Arg.Is<IReadOnlyCollection<string>>(keep => keep.Count == 0));
    }

    /// <summary>
    ///     Test that after sending nothing of the sent message can be undone or redone, and the next message has its own
    ///     history.
    /// </summary>
    [Fact]
    public async Task ComposerViewModel_SendAsync_ClearsUndoStack()
    {
        // Arrange: a message with steps that can be undone and redone
        _vm.InsertTyped("a");
        _vm.InsertTyped("b");
        _vm.Undo();
        _vm.InsertTyped("c");
        _vm.Undo();
        Assert.True(_vm.CanRedo);
        _vm.Redo();

        // Act: send, then undo and redo, write the next message, and undo it
        await _vm.SendAsync(TestContext.Current.CancellationToken);
        _vm.Undo();
        _vm.Redo();
        var textAfterUndoRedo = _vm.Document.FlatText;
        _vm.InsertTyped("next");
        _vm.Undo();
        _vm.Undo();

        // Assert: undo and redo did nothing to the sent message, and undoing the next message went back to an empty
        // draft and no further: the sent message is not under it
        Assert.Equal(string.Empty, textAfterUndoRedo);
        Assert.Equal(0, _vm.Document.Length);
        Assert.False(_vm.CanUndo);
        _vm.Redo();
        Assert.Equal("next", _vm.Document.FlatText);
        Assert.False(_vm.CanRedo);
    }

    /// <summary>
    ///     Test that a message that holds only a chip can be sent, and that an empty or blank message cannot.
    /// </summary>
    [Fact]
    public async Task ComposerViewModel_SendAsync_EmptyBlankAndChipOnlyMessages_AreHandledAsExpected()
    {
        // Arrange: an empty draft
        // Act: try to send nothing, then only spaces, then only a chip
        var empty = await _vm.SendAsync(TestContext.Current.CancellationToken);
        _vm.InsertTyped("   ");
        var blank = await _vm.SendAsync(TestContext.Current.CancellationToken);
        var statusForBlank = _vm.StatusText;
        _vm.ClearDraft();
        _vm.HandleDrop(new DroppedContent { Paths = [CreateFile("only.txt")] });
        var chipOnly = await _vm.SendAsync(TestContext.Current.CancellationToken);

        // Assert: the first two were not sent and said so, the last was sent
        Assert.False(empty);
        Assert.False(blank);
        Assert.Contains("Write a message", statusForBlank, StringComparison.Ordinal);
        Assert.True(chipOnly);
        Assert.Single(_builder.Messages);
    }

    /// <summary>
    ///     Test that when an attachment cannot be put into the project the draft is kept as it was, nothing is built, no
    ///     event is raised, and the status names the attachment.
    /// </summary>
    [Fact]
    public async Task ComposerViewModel_SendAsync_StagingFails_KeepsDraftAndSaysWhich()
    {
        // Arrange: a draft with text, a file, and a mark, and a store that cannot take the file
        _vm.InsertTyped("see ");
        _vm.HandleDrop(new DroppedContent { Paths = [CreateFile("huge.bin")] });
        var mark = NewMark();
        _vm.AddMark(mark);
        var chip = _vm.Document.Chips.OfType<AttachmentChip>().Single();
        _stores[_project].StageForSend(Arg.Any<IReadOnlyList<AttachmentChip>>())
            .Returns(StagingResult.Fail(chip, "'huge.bin' is too large to attach."));
        var raised = false;
        _vm.MessageSent += (_, _) => raised = true;
        var before = _vm.Document.Snapshot();

        // Act: send
        var sent = await _vm.SendAsync(TestContext.Current.CancellationToken);

        // Assert: nothing happened to the draft, its history, or the marks, and the status says what is wrong
        Assert.False(sent);
        Assert.False(raised);
        Assert.Empty(_builder.Messages);
        Assert.True(_vm.Document.Snapshot().HasSameContentAs(before));
        Assert.True(_vm.CanUndo);
        Assert.Equal([mark], _marks.Marks);
        Assert.False(_vm.IsSending);
        Assert.True(_vm.CanSend);
        Assert.Contains("'huge.bin' is too large to attach.", _vm.StatusText, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Test that when the message cannot be built the draft is kept as it was and the status says why.
    /// </summary>
    [Fact]
    public async Task ComposerViewModel_SendAsync_BuildFails_KeepsDraftAndSaysWhy()
    {
        // Arrange: a draft and a builder that fails
        _vm.InsertTyped("hello");
        _builder.Failure = new InvalidOperationException("The marks cannot be drawn.");
        var raised = false;
        _vm.MessageSent += (_, _) => raised = true;

        // Act: send
        var sent = await _vm.SendAsync(TestContext.Current.CancellationToken);

        // Assert: the draft and its step are kept, nothing was raised, and the status gives the reason
        Assert.False(sent);
        Assert.False(raised);
        Assert.Equal("hello", _vm.Document.FlatText);
        Assert.True(_vm.CanUndo);
        Assert.Contains("The marks cannot be drawn.", _vm.StatusText, StringComparison.Ordinal);
        Assert.False(_vm.IsSending);

        // Act: send again after the cause is gone
        _builder.Failure = null;
        var sentAgain = await _vm.SendAsync(TestContext.Current.CancellationToken);

        // Assert: the same draft is sent
        Assert.True(sentAgain);
        Assert.Equal(0, _vm.Document.Length);
    }

    /// <summary>
    ///     Test that while a message is being sent the draft cannot be changed and a second send is refused, and that
    ///     everything works again afterwards.
    /// </summary>
    [Fact]
    public async Task ComposerViewModel_SendAsync_WhileSending_DraftCannotBeChanged()
    {
        // Arrange: a draft and a builder that waits
        _vm.InsertTyped("hello");
        _builder.Gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        // Act: start sending and wait until the builder has the message
        var send = _vm.SendAsync(TestContext.Current.CancellationToken);
        Assert.True(SpinWait.SpinUntil(() => _builder.Messages.Count == 1, TimeSpan.FromSeconds(10)));

        // Assert: the composer is busy: editing, undoing, dropping, and sending again do nothing
        Assert.True(_vm.IsSending);
        Assert.False(_vm.CanEdit);
        Assert.False(_vm.CanSend);
        Assert.False(_vm.InsertTyped("x"));
        Assert.False(_vm.HandleDrop(new DroppedContent { Text = "x" }));
        Assert.False(_vm.CanUndo);
        Assert.False(await _vm.SendAsync(TestContext.Current.CancellationToken));
        Assert.Equal("hello", _vm.Document.FlatText);

        // Act: let the build finish
        _builder.Gate.SetResult();
        var sent = await send;

        // Assert: the message was sent once and the composer is free again
        Assert.True(sent);
        Assert.False(_vm.IsSending);
        Assert.True(_vm.CanEdit);
        Assert.Single(_builder.Messages);
        Assert.True(_vm.InsertTyped("next"));
    }

    /// <summary>
    ///     Test that closing the project while a message is being sent drops the draft and does not send the message into
    ///     the next project.
    /// </summary>
    [Fact]
    public async Task ComposerViewModel_SendAsync_ProjectClosedMeanwhile_IsNotSent()
    {
        // Arrange: a draft and a builder that waits
        _vm.InsertTyped("hello");
        _builder.Gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var raised = false;
        _vm.MessageSent += (_, _) => raised = true;
        var send = _vm.SendAsync(TestContext.Current.CancellationToken);
        Assert.True(SpinWait.SpinUntil(() => _builder.Messages.Count == 1, TimeSpan.FromSeconds(10)));

        // Act: close the project, then let the build finish
        _vm.SetProject(null);
        _builder.Gate.SetResult();
        var sent = await send;

        // Assert: the message was not sent and the composer has no project and no draft
        Assert.False(sent);
        Assert.False(raised);
        Assert.False(_vm.HasProject);
        Assert.Equal(0, _vm.Document.Length);
        Assert.False(_vm.IsSending);
    }

    /// <summary>
    ///     Test that canceling the send keeps the draft.
    /// </summary>
    [Fact]
    public async Task ComposerViewModel_SendAsync_Canceled_KeepsDraft()
    {
        // Arrange: a draft and a builder that waits
        _vm.InsertTyped("hello");
        _builder.Gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        var send = _vm.SendAsync(cancellation.Token);
        Assert.True(SpinWait.SpinUntil(() => _builder.Messages.Count == 1, TimeSpan.FromSeconds(10)));

        // Act: cancel
        await cancellation.CancelAsync();
        var sent = await send;

        // Assert: nothing was sent, the draft is kept, and the status says so
        Assert.False(sent);
        Assert.Equal("hello", _vm.Document.FlatText);
        Assert.Contains("canceled", _vm.StatusText, StringComparison.Ordinal);
        Assert.False(_vm.IsSending);
    }

    /// <summary>
    ///     Test that the send command sends the message and that it can only be executed when there is something to send.
    /// </summary>
    [Fact]
    public async Task ComposerViewModel_SendCommand_ExecutesOnlyWithContent()
    {
        // Arrange: an empty draft
        Assert.False(_vm.SendCommand.CanExecute(null));
        _vm.InsertTyped("hello");
        Assert.True(_vm.SendCommand.CanExecute(null));

        // Act: execute the command
        await _vm.SendCommand.ExecuteAsync(null);

        // Assert: the message was sent
        Assert.Single(_builder.Messages);
        Assert.False(_vm.SendCommand.CanExecute(null));
    }

    // ----- dictation -----

    /// <summary>
    ///     Test that each reason why dictation cannot work is shown as its own message and changes nothing.
    /// </summary>
    [Theory]
    [InlineData(DictationAvailabilityReason.NoModelChosen, "Choose a speech-to-text model on the Speech page of the settings.")]
    [InlineData(DictationAvailabilityReason.ModelNotInstalled, "The chosen model is not downloaded yet.")]
    [InlineData(DictationAvailabilityReason.SpeechRuntimeMissing, "The files that run speech recognition are missing.")]
    [InlineData(DictationAvailabilityReason.NoCaptureDevice, "No microphone can be used.")]
    [InlineData(DictationAvailabilityReason.EngineUnavailable, "The speech engine could not be started.")]
    public async Task ComposerViewModel_PushToTalk_Unavailable_ShowsReasonAndChangesNothing(DictationAvailabilityReason reason, string message)
    {
        // Arrange: dictation that cannot work for a reason, and a draft
        _dictation.Availability = new DictationAvailability(reason, message);
        _vm.InsertTyped("draft");

        // Act: press and release the talk button
        await _vm.BeginPushToTalkAsync();
        await _vm.EndPushToTalkAsync();

        // Assert: nothing listened, the message says why, and the draft is unchanged
        Assert.Equal(message, _vm.StatusText);
        Assert.Equal(0, _dictation.BeginCount);
        Assert.Equal(DictationState.Idle, _vm.DictationState);
        Assert.False(_vm.IsDictating);
        Assert.Equal("draft", _vm.Document.FlatText);
    }

    /// <summary>
    ///     Test that holding the talk button shows the words as they are recognized, and that releasing it adds the final
    ///     words at the caret as one dictated block, going through the states one after the other.
    /// </summary>
    [Fact]
    public async Task ComposerViewModel_PushToTalk_HoldAndRelease_ShowsLiveTextThenAddsOneDictatedBlock()
    {
        // Arrange: a typed start, a session that will end with its words, and a record of the states
        _vm.InsertTyped("Make ");
        _dictation.Session.FinalText = "the box blue";
        var states = new List<DictationState>();
        _vm.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(ComposerViewModel.DictationState))
            {
                states.Add(_vm.DictationState);
            }
        };

        // Act: press, hear words, release
        await _vm.BeginPushToTalkAsync();
        var listening = (_vm.DictationState, _vm.IsDictating, _vm.CanSend);
        _dictation.Session.Say("the box");
        var live = _vm.LivePartialText;
        _dictation.Session.Say("the box blue");
        var liveLater = _vm.LivePartialText;
        await _vm.EndPushToTalkAsync();

        // Assert: it listened and the words were shown while it did, and sending was not possible meanwhile
        Assert.Equal((DictationState.Listening, true, false), listening);
        Assert.Equal("the box", live);
        Assert.Equal("the box blue", liveLater);

        // Assert: after the release the words are one dictated block at the caret and the live text is gone
        Assert.Equal([DictationState.Starting, DictationState.Listening, DictationState.Finishing, DictationState.Idle], states);
        Assert.Equal(string.Empty, _vm.LivePartialText);
        Assert.Equal(new TextSegment("the box blue", TextOrigin.Dictated), _vm.Document.Segments[1]);
        Assert.Equal("Make the box blue", _vm.Document.FlatText);
        Assert.Equal(1, _dictation.Session.EndCount);
        Assert.Equal(1, _dictation.Session.DisposeCount);
        Assert.True(_vm.CanSend);

        // Assert: one undo removes the whole block
        _vm.Undo();
        Assert.Equal("Make ", _vm.Document.FlatText);
    }

    /// <summary>
    ///     Test that the words are added at the caret and replace the selection.
    /// </summary>
    [Fact]
    public async Task ComposerViewModel_PushToTalk_Release_AddsAtCaretOverSelection()
    {
        // Arrange: a draft "abcd" with "bc" selected
        _vm.InsertTyped("abcd");
        _vm.SetSelection(1, 2);
        _dictation.Session.FinalText = "X";

        // Act: dictate
        await _vm.BeginPushToTalkAsync();
        await _vm.EndPushToTalkAsync();

        // Assert: the selection was replaced by the dictated word, and one undo restores it
        Assert.Equal("aXd", _vm.Document.FlatText);
        _vm.Undo();
        Assert.Equal("abcd", _vm.Document.FlatText);
    }

    /// <summary>
    ///     Test that a second press while dictation is listening or starting is ignored.
    /// </summary>
    [Fact]
    public async Task ComposerViewModel_PushToTalk_SecondPress_IsIgnored()
    {
        // Arrange: a start that waits
        _dictation.Gate = new TaskCompletionSource();
        var first = _vm.BeginPushToTalkAsync();

        // Act: press again while starting, let the start finish, press again while listening
        await _vm.BeginPushToTalkAsync();
        _dictation.Gate.SetResult();
        await first;
        await _vm.BeginPushToTalkAsync();

        // Assert: listening was asked for once and the composer is listening
        Assert.Equal(1, _dictation.BeginCount);
        Assert.Equal(DictationState.Listening, _vm.DictationState);
        await _vm.EndPushToTalkAsync();
    }

    /// <summary>
    ///     Test that a release before listening has started makes the start give up, and nothing is added.
    /// </summary>
    [Fact]
    public async Task ComposerViewModel_PushToTalk_ReleaseWhileStarting_GivesUpAndAddsNothing()
    {
        // Arrange: a start that waits until it is cancelled
        _dictation.Gate = new TaskCompletionSource();
        var begin = _vm.BeginPushToTalkAsync();
        var startingState = _vm.DictationState;

        // Act: release the talk button before listening started
        await _vm.EndPushToTalkAsync();
        await begin;

        // Assert: the start was cancelled, no session was used, the composer is idle, and the draft is empty
        Assert.Equal(DictationState.Starting, startingState);
        Assert.True(_dictation.WasCanceled);
        Assert.Equal(DictationState.Idle, _vm.DictationState);
        Assert.Equal(0, _dictation.Session.EndCount);
        Assert.Equal(0, _vm.Document.Length);
        Assert.False(_vm.CanUndo);
        Assert.Equal(string.Empty, _vm.StatusText);
    }

    /// <summary>
    ///     Test that when listening started at the very moment of the release, the session is ended at once and what it
    ///     heard is still added.
    /// </summary>
    [Fact]
    public async Task ComposerViewModel_PushToTalk_ReleaseWhileStartingButSessionArrives_EndsItAndAddsWords()
    {
        // Arrange: a start that does not notice the cancellation
        _dictation.Gate = new TaskCompletionSource();
        _dictation.IgnoreCancellation = true;
        _dictation.Session.FinalText = "quick words";
        var begin = _vm.BeginPushToTalkAsync();

        // Act: release, then the session arrives
        await _vm.EndPushToTalkAsync();
        _dictation.Gate.SetResult();
        await begin;

        // Assert: the session was ended and released, and the words were added as one block
        Assert.Equal(DictationState.Idle, _vm.DictationState);
        Assert.Equal(1, _dictation.Session.EndCount);
        Assert.Equal(1, _dictation.Session.DisposeCount);
        Assert.Equal("quick words", _vm.Document.FlatText);
    }

    /// <summary>
    ///     Test that a release without a press is ignored.
    /// </summary>
    [Fact]
    public async Task ComposerViewModel_PushToTalk_ReleaseWithoutPress_IsIgnored()
    {
        // Arrange: nothing listening
        // Act: release
        await _vm.EndPushToTalkAsync();

        // Assert: nothing happened
        Assert.Equal(DictationState.Idle, _vm.DictationState);
        Assert.Equal(string.Empty, _vm.StatusText);
        Assert.Equal(0, _dictation.Session.EndCount);
    }

    /// <summary>
    ///     Test that nothing that was heard is no step and says so.
    /// </summary>
    [Fact]
    public async Task ComposerViewModel_PushToTalk_NothingHeard_IsNoStepAndSaysSo()
    {
        // Arrange: a session that heard nothing, and a draft with one step
        _vm.InsertTyped("a");
        _dictation.Session.FinalText = "  ";

        // Act: press and release
        await _vm.BeginPushToTalkAsync();
        await _vm.EndPushToTalkAsync();

        // Assert: the draft is unchanged, there is one step, and the status says that nothing was recognized
        Assert.Equal("a", _vm.Document.FlatText);
        Assert.Contains("No speech was recognized", _vm.StatusText, StringComparison.Ordinal);
        _vm.Undo();
        Assert.False(_vm.CanUndo);
    }

    /// <summary>
    ///     Test that a problem of the recognizer is shown, and that the words heard before it are still added.
    /// </summary>
    [Fact]
    public async Task ComposerViewModel_PushToTalk_SessionProblem_ShownAndWordsKept()
    {
        // Arrange: a session that heard words and then lost the microphone
        _dictation.Session.FinalText = "first part";
        _dictation.Session.Problem = "The microphone was unplugged.";

        // Act: press and release
        await _vm.BeginPushToTalkAsync();
        await _vm.EndPushToTalkAsync();

        // Assert: the words were added and the status explains the problem
        Assert.Equal("first part", _vm.Document.FlatText);
        Assert.Equal("The microphone was unplugged.", _vm.StatusText);
    }

    /// <summary>
    ///     Test that a problem without any words is shown instead of the message that nothing was heard.
    /// </summary>
    [Fact]
    public async Task ComposerViewModel_PushToTalk_SessionProblemWithoutWords_ShowsTheProblem()
    {
        // Arrange: a session that failed at once
        _dictation.Session.Problem = "Speech recognition stopped: the model could not be read.";

        // Act: press and release
        await _vm.BeginPushToTalkAsync();
        await _vm.EndPushToTalkAsync();

        // Assert: nothing was added and the status explains
        Assert.Equal(0, _vm.Document.Length);
        Assert.Equal("Speech recognition stopped: the model could not be read.", _vm.StatusText);
    }

    /// <summary>
    ///     Test that when the service starts no session and says why, the reason is shown, and that when it says the user
    ///     is already dictating nothing is shown.
    /// </summary>
    [Fact]
    public async Task ComposerViewModel_PushToTalk_StartWithoutSession_ShowsReasonOrIsSilent()
    {
        // Arrange: a service whose start fails to load the engine
        _dictation.Start = new DictationStart(null, new DictationAvailability(DictationAvailabilityReason.EngineUnavailable, "The speech engine could not be started."));

        // Act: press
        await _vm.BeginPushToTalkAsync();

        // Assert: idle, with the reason
        Assert.Equal(DictationState.Idle, _vm.DictationState);
        Assert.Equal("The speech engine could not be started.", _vm.StatusText);

        // Arrange: a service that ignores the request because it is already listening
        _dictation.Start = new DictationStart(null, DictationAvailability.Ready);
        _vm.InsertTyped("x");

        // Act: press
        await _vm.BeginPushToTalkAsync();

        // Assert: idle and silent
        Assert.Equal(DictationState.Idle, _vm.DictationState);
        Assert.Equal(string.Empty, _vm.StatusText);
    }

    /// <summary>
    ///     Test that a failure of the start is shown and does not leave the composer listening.
    /// </summary>
    [Fact]
    public async Task ComposerViewModel_PushToTalk_StartThrows_ExplainsAndIsIdle()
    {
        // Arrange: a service that throws
        _dictation.BeginFailure = new InvalidOperationException("boom");

        // Act: press
        await _vm.BeginPushToTalkAsync();

        // Assert: idle and the status explains
        Assert.Equal(DictationState.Idle, _vm.DictationState);
        Assert.Contains("Dictation could not be started", _vm.StatusText, StringComparison.Ordinal);
        Assert.Contains("boom", _vm.StatusText, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Test that a failure while finishing is shown, the session is still released, and nothing is added.
    /// </summary>
    [Fact]
    public async Task ComposerViewModel_PushToTalk_EndThrows_ExplainsReleasesSessionAndAddsNothing()
    {
        // Arrange: a session that fails when it ends
        _dictation.Session.EndFailure = new InvalidOperationException("boom");

        // Act: press and release
        await _vm.BeginPushToTalkAsync();
        await _vm.EndPushToTalkAsync();

        // Assert: the session was released, nothing was added, the composer is idle, and the status explains
        Assert.Equal(1, _dictation.Session.DisposeCount);
        Assert.Equal(0, _vm.Document.Length);
        Assert.Equal(DictationState.Idle, _vm.DictationState);
        Assert.Contains("Dictation stopped with an error", _vm.StatusText, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Test that closing the project while listening gives the dictation up: the session is released and nothing is
    ///     added to the next draft.
    /// </summary>
    [Fact]
    public async Task ComposerViewModel_PushToTalk_ProjectClosedWhileListening_GivesUpAndAddsNothing()
    {
        // Arrange: listening
        _dictation.Session.FinalText = "never added";
        await _vm.BeginPushToTalkAsync();
        _dictation.Session.Say("never");

        // Act: close the project, open it again, and release the button
        _vm.SetProject(null);
        _vm.SetProject(_project);
        await _vm.EndPushToTalkAsync();

        // Assert: the session was released, the live text is gone, and nothing was added
        Assert.Equal(DictationState.Idle, _vm.DictationState);
        Assert.Equal(1, _dictation.Session.EndCount);
        Assert.Equal(1, _dictation.Session.DisposeCount);
        Assert.Equal(string.Empty, _vm.LivePartialText);
        Assert.Equal(0, _vm.Document.Length);
    }

    /// <summary>
    ///     Test that disposing the composer releases a session that is listening.
    /// </summary>
    [Fact]
    public async Task ComposerViewModel_Dispose_WhileListening_ReleasesTheSession()
    {
        // Arrange: listening
        await _vm.BeginPushToTalkAsync();

        // Act: dispose
        _vm.Dispose();

        // Assert: the session was ended and released
        Assert.Equal(1, _dictation.Session.EndCount);
        Assert.Equal(1, _dictation.Session.DisposeCount);
        Assert.Equal(DictationState.Idle, _vm.DictationState);
    }

    /// <summary>
    ///     Test that live text that the recognizer reports from another thread is moved to the thread that pressed the
    ///     button, and is dropped when it arrives after the release.
    /// </summary>
    [Fact]
    public async Task ComposerViewModel_PushToTalk_LiveTextFromAnotherThread_IsMovedToTheInterfaceThread()
    {
        // Arrange: a composer that moves the words of the recognizer to a context that only queues the work, as the
        // interface thread would, and a talk button that is pressed
        var context = new QueueingSynchronizationContext();
        using var vm = new ComposerViewModel(_builder, _marks, _dictation, StoreFor, context: context);
        vm.SetProject(_project);
        await vm.BeginPushToTalkAsync();

        // Act: the recognizer reports words from a thread of the pool
        await Task.Run(() => _dictation.Session.Say("from the pool"), TestContext.Current.CancellationToken);
        var beforeRunning = vm.LivePartialText;
        var pending = context.Pending;
        context.RunAll();

        // Assert: the text was not changed on the other thread, and was changed when the work ran on the interface thread
        Assert.Equal(string.Empty, beforeRunning);
        Assert.Equal(1, pending);
        Assert.Equal("from the pool", vm.LivePartialText);

        // Act: words that arrive after the release are dropped
        await vm.EndPushToTalkAsync();
        await Task.Run(() => _dictation.Session.Say("too late"), TestContext.Current.CancellationToken);
        context.RunAll();

        // Assert: the live text stays empty
        Assert.Equal(string.Empty, vm.LivePartialText);
    }

    /// <summary>
    ///     Test that without a project dictation says so and does not listen.
    /// </summary>
    [Fact]
    public async Task ComposerViewModel_PushToTalk_NoProject_SaysSoAndDoesNotListen()
    {
        // Arrange: a composer without a project
        using var vm = CreateViewModel();

        // Act: press
        await vm.BeginPushToTalkAsync();

        // Assert: nothing listened and the status says to open a project
        Assert.Equal(0, _dictation.BeginCount);
        Assert.Equal(ComposerViewModel.NoProjectText, vm.StatusText);
    }

    /// <summary>
    ///     Test that sending is refused while the talk button is held and that the message says to let go first.
    /// </summary>
    [Fact]
    public async Task ComposerViewModel_SendAsync_WhileTalkButtonHeld_IsRefused()
    {
        // Arrange: a draft and a held talk button
        _vm.InsertTyped("hello");
        await _vm.BeginPushToTalkAsync();

        // Act: send
        var sent = await _vm.SendAsync(TestContext.Current.CancellationToken);
        var statusWhileHeld = _vm.StatusText;
        await _vm.EndPushToTalkAsync();

        // Assert: the message was not sent and the status said why
        Assert.False(sent);
        Assert.Empty(_builder.Messages);
        Assert.Contains("talk button", statusWhileHeld, StringComparison.Ordinal);
    }

    // ----- block from outside -----

    /// <summary>
    ///     Test that blocking sending makes sending impossible with the reason in the status text, while editing stays
    ///     allowed, and that lifting the block restores both and clears the reason.
    /// </summary>
    [Fact]
    public void ComposerViewModel_BlockSending_CannotSendButCanEdit_UnblockRestoresAndClearsTheReason()
    {
        // Arrange: a draft that can be sent, and the changes that are announced
        _vm.InsertTyped("hello");
        var changed = new List<string>();
        _vm.PropertyChanged += (_, e) => changed.Add(e.PropertyName ?? string.Empty);
        Assert.True(_vm.CanSend);
        Assert.False(_vm.IsSendBlocked);

        // Act: block sending
        _vm.BlockSending(ComposerViewModel.CopilotWorkingText);

        // Assert: it cannot be sent, the command cannot run, the reason is shown, and editing still works
        Assert.False(_vm.CanSend);
        Assert.False(_vm.SendCommand.CanExecute(null));
        Assert.True(_vm.IsSendBlocked);
        Assert.Equal("The copilot is still working. Press Stop to cancel.", _vm.SendBlockedReason);
        Assert.Equal(ComposerViewModel.CopilotWorkingText, _vm.StatusText);
        Assert.True(_vm.CanEdit);
        _vm.InsertTyped(" world");
        Assert.Equal("hello world", _vm.Document.FlatText);
        Assert.Contains(nameof(ComposerViewModel.CanSend), changed);
        Assert.Contains(nameof(ComposerViewModel.IsSendBlocked), changed);

        // Act: lift the block
        _vm.UnblockSending();

        // Assert: it can be sent again and the reason is gone
        Assert.True(_vm.CanSend);
        Assert.True(_vm.SendCommand.CanExecute(null));
        Assert.False(_vm.IsSendBlocked);
        Assert.Equal(string.Empty, _vm.SendBlockedReason);
        Assert.Equal(string.Empty, _vm.StatusText);
    }

    /// <summary>
    ///     Test that sending is refused while blocked, with the reason as the status text, nothing is built, and the draft
    ///     is kept and can be sent once the block is lifted.
    /// </summary>
    [Fact]
    public async Task ComposerViewModel_SendAsync_WhileBlocked_IsRefusedWithTheReasonAndKeepsTheDraft()
    {
        // Arrange: a draft and a block, and a status that is something else
        _vm.InsertTyped("hello");
        _vm.BlockSending(ComposerViewModel.CopilotWorkingText);
        _vm.InsertTyped("!");

        // Act: send while blocked
        var refused = await _vm.SendAsync(TestContext.Current.CancellationToken);

        // Assert: refused, with the reason, and nothing was built
        Assert.False(refused);
        Assert.Empty(_builder.Messages);
        Assert.Equal(ComposerViewModel.CopilotWorkingText, _vm.StatusText);
        Assert.Equal("hello!", _vm.Document.FlatText);

        // Act: lift the block and send
        _vm.UnblockSending();
        var sent = await _vm.SendAsync(TestContext.Current.CancellationToken);

        // Assert: the same draft was sent
        Assert.True(sent);
        Assert.Single(_builder.Messages);
    }

    /// <summary>
    ///     Test that lifting the block keeps a sentence about another action, that lifting without a block does nothing,
    ///     and that a block needs a reason.
    /// </summary>
    [Fact]
    public void ComposerViewModel_UnblockSending_OtherStatusAndNoBlockAndEmptyReason_AreHandled()
    {
        // Arrange: a block
        _vm.InsertTyped("hello");
        _vm.BlockSending("Busy.");

        // Act / Assert: lifting without a block changes nothing, and a block needs a reason
        var other = CreateViewModel();
        other.UnblockSending();
        Assert.False(other.IsSendBlocked);
        Assert.Equal(string.Empty, other.StatusText);
        Assert.Throws<ArgumentException>(() => _vm.BlockSending(" "));
        Assert.Throws<ArgumentNullException>(() => _vm.BlockSending(null!));

        // Act: another sentence replaces the reason, and then the block is lifted
        _vm.StatusText = "Another sentence.";
        _vm.UnblockSending();

        // Assert: the other sentence stays
        Assert.False(_vm.IsSendBlocked);
        Assert.Equal("Another sentence.", _vm.StatusText);
    }

    // ----- construction -----

    /// <summary>
    ///     Test that the constructor refuses missing collaborators.
    /// </summary>
    [Fact]
    public void ComposerViewModel_Constructor_MissingArguments_ThrowArgumentNullException()
    {
        // Arrange / Act / Assert: each required argument is checked
        Assert.Throws<ArgumentNullException>(() => new ComposerViewModel(null!, _marks, _dictation));
        Assert.Throws<ArgumentNullException>(() => new ComposerViewModel(_builder, null!, _dictation));
        Assert.Throws<ArgumentNullException>(() => new ComposerViewModel(_builder, _marks, null!));
        Assert.Throws<ArgumentNullException>(() => _vm.ApplyTextChange(0, 0, null!));
        Assert.Throws<ArgumentNullException>(() => _vm.HandleDrop(null!));
        Assert.Throws<ArgumentNullException>(() => _vm.HandlePaste(null!));
        Assert.Throws<ArgumentNullException>(() => _vm.AddDictatedBlock(null!));
    }

    /// <summary>
    ///     Makes a composer whose stores are substitutes that accept every attachment.
    /// </summary>
    /// <returns>The composer, which is in no project.</returns>
    private ComposerViewModel CreateViewModel() => new(_builder, _marks, _dictation, StoreFor);

    /// <summary>
    ///     Makes, or finds, the substitute store of a project folder.
    /// </summary>
    /// <param name="folder">The project folder.</param>
    /// <returns>The store.</returns>
    private IAttachmentStore StoreFor(string folder)
    {
        if (_stores.TryGetValue(folder, out var existing))
        {
            return existing;
        }

        var store = Substitute.For<IAttachmentStore>();
        store.ProjectFolder.Returns(folder);
        store.PastedImagesFolder.Returns(Path.Combine(folder, ".canvaspilot", "attachments"));
        store.SavePastedImage(Arg.Any<ReadOnlyMemory<byte>>())
            .Returns(_ => Path.Combine(folder, ".canvaspilot", "attachments", $"paste-{Interlocked.Increment(ref _savedPictures)}.png"));
        store.StageForSend(Arg.Any<IReadOnlyList<AttachmentChip>>())
            .Returns(call => StagingResult.Success(call.Arg<IReadOnlyList<AttachmentChip>>()
                .Select(chip => PayloadTestSupport.Staged(chip, "attachments/" + chip.Name))));
        _stores[folder] = store;
        return store;
    }

    /// <summary>
    ///     Makes a mark on a small picture in the registry of the composer.
    /// </summary>
    /// <returns>The mark.</returns>
    private Mark NewMark() => _marks.Add(AnnotationTestPictures.Picture(), MarkKind.Rectangle, [new RenderPoint(10, 10), new RenderPoint(60, 40)]);

    /// <summary>
    ///     Creates a file outside the project.
    /// </summary>
    /// <param name="name">The name of the file.</param>
    /// <returns>The full path of the file.</returns>
    private string CreateFile(string name)
    {
        var folder = Directory.CreateDirectory(Path.Combine(_root, "external")).FullName;
        var path = Path.Combine(folder, name);
        File.WriteAllBytes(path, [1, 2, 3]);
        return path;
    }
}
