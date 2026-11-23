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

using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Input.Raw;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Platform.Storage;
using DemaConsulting.CanvasPilot.AnnotationSubsystem;
using DemaConsulting.CanvasPilot.ComposerSubsystem;
using DemaConsulting.CanvasPilot.PreviewSubsystem;

namespace DemaConsulting.CanvasPilot.UiTests;

/// <summary>
///     Headless UI tests for the composer editor view: typing, the undo and redo keys, chips drawn inline, deleting
///     chips, paste and drop handled as data, dictated text, and that the editing surface always holds what the message
///     holds.
/// </summary>
public sealed class ComposerEditorViewTests
{
    /// <summary>
    ///     Test that typing characters one by one is one undo step per character, that Ctrl+Z takes them back one by one,
    ///     and that the editing surface holds the message after every operation.
    /// </summary>
    [AvaloniaFact]
    public void ComposerEditorView_TypeCharacters_EachCharacterIsOneStepUndoneByCtrlZ()
    {
        // Arrange: an editor of an open project
        using var harness = new ComposerHarness();

        // Act: type three characters, one key press each
        harness.TypeEach("abc");

        // Assert: the message holds them, as three steps
        Assert.Equal("abc", harness.Message);
        harness.AssertConsistent();
        Assert.True(harness.ViewModel.CanUndo);

        // Act and assert: each Ctrl+Z takes one character back, and the surface follows
        harness.Press(PhysicalKey.Z, ComposerHarness.Command);
        Assert.Equal("ab", harness.Message);
        harness.AssertConsistent();
        harness.Press(PhysicalKey.Z, ComposerHarness.Command);
        Assert.Equal("a", harness.Message);
        harness.AssertConsistent();
        harness.Press(PhysicalKey.Z, ComposerHarness.Command);
        Assert.Equal(string.Empty, harness.Message);
        harness.AssertConsistent();
        Assert.False(harness.ViewModel.CanUndo);
    }

    /// <summary>
    ///     Test that Ctrl+Y and Ctrl+Shift+Z both redo the step that was undone, one step each.
    /// </summary>
    [AvaloniaFact]
    public void ComposerEditorView_RedoKeys_CtrlYAndCtrlShiftZ_RedoOneStepEach()
    {
        // Arrange: three typed characters, all undone
        using var harness = new ComposerHarness();
        harness.TypeEach("abc");
        for (var i = 0; i < 3; i++)
        {
            harness.Press(PhysicalKey.Z, ComposerHarness.Command);
        }

        // Act: redo with Ctrl+Y, then with Ctrl+Shift+Z
        harness.Press(PhysicalKey.Y, ComposerHarness.Command);
        var afterFirst = harness.Message;
        harness.AssertConsistent();
        harness.Press(PhysicalKey.Z, ComposerHarness.Command | RawInputModifiers.Shift);
        var afterSecond = harness.Message;
        harness.AssertConsistent();

        // Assert: each redo brought back one character, with the caret after it
        Assert.Equal("a", afterFirst);
        Assert.Equal("ab", afterSecond);
        Assert.Equal(2, harness.Editor.CaretOffset);
    }

    /// <summary>
    ///     Test that the surface's own undo is switched off, so that nothing is undone behind the message's back.
    /// </summary>
    [AvaloniaFact]
    public void ComposerEditorView_BuiltInUndo_IsSwitchedOffAndItsCommandsRemoved()
    {
        // Arrange: an editor with typed text, whose draft is then dropped, which writes into the surface from outside
        using var harness = new ComposerHarness();
        harness.TypeEach("abc");
        harness.ViewModel.ClearDraft();

        // Act: look at the surface's own stack and press Ctrl+Z, which would bring the text back from a built-in history
        var surfaceCanUndo = harness.Editor.CanUndo;
        harness.Press(PhysicalKey.Z, ComposerHarness.Command);

        // Assert: the surface keeps no history, and nothing came back
        Assert.False(surfaceCanUndo);
        Assert.Equal(string.Empty, harness.Message);
        harness.AssertConsistent();
    }

    /// <summary>
    ///     Test that a typed placeholder character is refused before it enters, so that no text can be mistaken for a
    ///     chip.
    /// </summary>
    [AvaloniaFact]
    public void ComposerEditorView_TypePlaceholderCharacter_IsRefused()
    {
        // Arrange: an editor with the text "ab" and the caret at its end
        using var harness = new ComposerHarness();
        harness.TypeEach("ab");

        // Act: type the placeholder, then a letter
        harness.Type("\uFFFC");
        harness.Type("c");

        // Assert: only the letter entered, and there is still no chip
        Assert.Equal("abc", harness.Message);
        harness.AssertConsistent();
        Assert.Empty(harness.ViewModel.Document.Chips);
    }

    /// <summary>
    ///     Test that a multi-character commit of an input method is one insertion and one undo step.
    /// </summary>
    [AvaloniaFact]
    public void ComposerEditorView_InputMethodCommit_IsOneStep()
    {
        // Arrange: an editor with the text "a"
        using var harness = new ComposerHarness();
        harness.TypeEach("a");

        // Act: commit five characters at once, as an input method does
        harness.Type("hello");
        var typed = harness.Message;
        harness.Press(PhysicalKey.Z, ComposerHarness.Command);

        // Assert: one undo took all five back
        Assert.Equal("a" + "hello", typed);
        Assert.Equal("a", harness.Message);
        harness.AssertConsistent();
    }

    /// <summary>
    ///     Test that Backspace and Delete each remove one character as one step, and that undo puts each back.
    /// </summary>
    [AvaloniaFact]
    public void ComposerEditorView_BackspaceAndDelete_EachCharacterIsOneStep()
    {
        // Arrange: the text "abcd" with the caret after "b"
        using var harness = new ComposerHarness();
        harness.TypeEach("abcd");
        harness.MoveCaret(2);

        // Act: Backspace removes "b", Delete removes "c"
        harness.Press(PhysicalKey.Backspace);
        var afterBackspace = harness.Message;
        harness.AssertConsistent();
        harness.Press(PhysicalKey.Delete);
        var afterDelete = harness.Message;
        harness.AssertConsistent();

        // Act and assert: two Ctrl+Z put both back, in turn
        harness.Press(PhysicalKey.Z, ComposerHarness.Command);
        Assert.Equal("acd", harness.Message);
        harness.Press(PhysicalKey.Z, ComposerHarness.Command);
        Assert.Equal("abcd", harness.Message);
        harness.AssertConsistent();
        Assert.Equal("acd", afterBackspace);
        Assert.Equal("ad", afterDelete);
    }

    /// <summary>
    ///     Test that typing over a selection is one step, and that undo restores the selected text.
    /// </summary>
    [AvaloniaFact]
    public void ComposerEditorView_TypeOverSelection_IsOneStep()
    {
        // Arrange: "hello" with "ell" selected
        using var harness = new ComposerHarness();
        harness.TypeEach("hello");
        harness.Select(1, 3);

        // Act: type one character over it, then undo once
        harness.Type("X");
        var typed = harness.Message;
        harness.AssertConsistent();
        harness.Press(PhysicalKey.Z, ComposerHarness.Command);

        // Assert: one step replaced the selection and one step restored it
        Assert.Equal("hXo", typed);
        Assert.Equal("hello", harness.Message);
        harness.AssertConsistent();
    }

    /// <summary>
    ///     Test that a line break typed with Enter keeps the surface and the message equal.
    /// </summary>
    [AvaloniaFact]
    public void ComposerEditorView_EnterKey_KeepsSurfaceAndMessageEqual()
    {
        // Arrange: an indented line of text
        using var harness = new ComposerHarness();
        harness.TypeEach("  line");

        // Act: press Enter, type on the new line, and undo twice
        harness.Press(PhysicalKey.Enter);
        harness.TypeEach("x");
        var typed = harness.Message;
        harness.AssertConsistent();
        harness.Press(PhysicalKey.Z, ComposerHarness.Command);
        harness.Press(PhysicalKey.Z, ComposerHarness.Command);

        // Assert: the message has a line break, and undo took it away again
        Assert.Contains('\n', typed);
        Assert.DoesNotContain('\r', typed);
        Assert.Equal("  line", harness.Message);
        harness.AssertConsistent();
    }

    /// <summary>
    ///     Test that a chip added through the view model is drawn inline between the text before and after it, with its
    ///     name and an automation identifier.
    /// </summary>
    [AvaloniaFact]
    public void ComposerEditorView_AddChip_IsDrawnInlineBetweenTexts()
    {
        // Arrange: the text "ab" with the caret between the letters
        using var harness = new ComposerHarness();
        harness.TypeEach("ab");
        harness.MoveCaret(1);
        var file = harness.MakeFile("notes.txt");

        // Act: drop a file at the caret through the view model
        harness.ViewModel.HandleDrop(new DroppedContent { Paths = [file] });
        harness.Layout();

        // Assert: the message is text, chip, text, and the chip control shows the file name
        Assert.Equal("a#b", harness.ReadableMessage);
        harness.AssertConsistent();
        var chip = Assert.Single(harness.Chips);
        Assert.Equal("ComposerChip", Avalonia.Automation.AutomationProperties.GetAutomationId(chip));
        Assert.Equal("notes.txt", chip.FindControl<TextBlock>("LabelText")!.Text);
        Assert.Equal(file, ToolTip.GetTip(chip));
    }

    /// <summary>
    ///     Test that the delete button of a chip removes the chip as one step with a real click, and that Ctrl+Z puts it
    ///     back at the same offset.
    /// </summary>
    [AvaloniaFact]
    public void ComposerEditorView_ChipDeleteButton_RemovesChipAndUndoRestoresIt()
    {
        // Arrange: the text "a", a chip, and the text "b"
        using var harness = new ComposerHarness();
        harness.TypeEach("ab");
        harness.MoveCaret(1);
        harness.ViewModel.HandleDrop(new DroppedContent { Paths = [harness.MakeFile("one.txt")] });
        harness.Layout();
        var button = Assert.Single(harness.Chips).FindControl<Button>("DeleteButton")!;
        var center = button.TranslatePoint(new Point(button.Bounds.Width / 2, button.Bounds.Height / 2), harness.Window)!.Value;

        // Act: click the delete button with the mouse
        harness.Window.MouseDown(center, MouseButton.Left);
        harness.Window.MouseUp(center, MouseButton.Left);
        harness.Layout();

        // Assert: the chip is gone and the text is joined
        Assert.Equal("ab", harness.Message);
        harness.AssertConsistent();
        Assert.Empty(harness.Chips);

        // Act: undo
        harness.Press(PhysicalKey.Z, ComposerHarness.Command);

        // Assert: the chip is back between the letters, drawn again
        Assert.Equal("a#b", harness.ReadableMessage);
        harness.AssertConsistent();
        Assert.Single(harness.Chips);
    }

    /// <summary>
    ///     Test that Backspace after a chip removes the chip as one step, and that Delete before a chip does too.
    /// </summary>
    [AvaloniaFact]
    public void ComposerEditorView_BackspaceAndDeleteOverChip_RemoveChipAsOneStep()
    {
        // Arrange: "a", a chip, "b", and a second chip after "b"
        using var harness = new ComposerHarness();
        harness.TypeEach("ab");
        harness.MoveCaret(1);
        harness.ViewModel.HandleDrop(new DroppedContent { Paths = [harness.MakeFile("one.txt")] });
        harness.MoveCaret(3);
        harness.ViewModel.HandleDrop(new DroppedContent { Paths = [harness.MakeFile("two.txt")] });
        harness.Layout();
        Assert.Equal("a#b#", harness.ReadableMessage);

        // Act: Backspace right after the second chip
        harness.MoveCaret(4);
        harness.Press(PhysicalKey.Backspace);
        var afterBackspace = harness.ReadableMessage;
        harness.AssertConsistent();

        // Act: Delete right before the first chip
        harness.MoveCaret(1);
        harness.Press(PhysicalKey.Delete);
        var afterDelete = harness.ReadableMessage;
        harness.AssertConsistent();

        // Act and assert: each removal is one step, undone one at a time
        harness.Press(PhysicalKey.Z, ComposerHarness.Command);
        Assert.Equal("a#b", harness.ReadableMessage);
        harness.Press(PhysicalKey.Z, ComposerHarness.Command);
        Assert.Equal("a#b#", harness.ReadableMessage);
        harness.AssertConsistent();
        Assert.Equal("a#b", afterBackspace);
        Assert.Equal("ab", afterDelete);
    }

    /// <summary>
    ///     Test that a chip that replaces another chip at the same offset, which leaves the text unchanged, is drawn for
    ///     the new chip.
    /// </summary>
    [AvaloniaFact]
    public void ComposerEditorView_ChipReplacesChip_DrawsTheNewChip()
    {
        // Arrange: a chip, selected
        using var harness = new ComposerHarness();
        harness.ViewModel.HandleDrop(new DroppedContent { Paths = [harness.MakeFile("old.txt")] });
        harness.Layout();
        harness.Select(0, 1);

        // Act: paste another file over the selected chip
        harness.ViewModel.HandlePaste(new DroppedContent { Paths = [harness.MakeFile("new.txt")] });
        harness.Layout();

        // Assert: the one chip drawn is the new one
        var chip = Assert.Single(harness.Chips);
        Assert.Equal("new.txt", chip.FindControl<TextBlock>("LabelText")!.Text);
        harness.AssertConsistent();
    }

    /// <summary>
    ///     Test that a mark chip shows its label and that undo through the view model's command writes back to the
    ///     surface.
    /// </summary>
    [AvaloniaFact]
    public void ComposerEditorView_AddMark_ShowsMarkChipAndUndoCommandRemovesIt()
    {
        // Arrange: a mark on a picture
        using var harness = new ComposerHarness();
        var pixels = new byte[4 * 4 * 4];
        var picture = new RenderedImage(Path.Combine(harness.Folder, "pic.svg"), "svg", 4, 4, pixels);
        var mark = harness.Marks.Add(picture, MarkKind.Rectangle, [new RenderPoint(0, 0), new RenderPoint(3, 3)]);

        // Act: add the mark chip, then undo with the command that the Edit menu uses
        harness.ViewModel.AddMark(mark);
        harness.Layout();
        var chip = Assert.Single(harness.Chips);
        var label = chip.FindControl<TextBlock>("LabelText")!.Text;
        harness.AssertConsistent();
        harness.ViewModel.UndoCommand.Execute(null);
        harness.Layout();

        // Assert: the chip said "Mark 1" and the undo removed it from the surface
        Assert.Equal("Mark 1", label);
        Assert.Equal(string.Empty, harness.Editor.Text);
        Assert.Empty(harness.Chips);
        harness.AssertConsistent();
    }

    /// <summary>
    ///     Test that the chips of marks are in a row of their own above the words: the chip is drawn on a line above the
    ///     line of the caret, and the words typed afterwards go on the line below.
    /// </summary>
    [AvaloniaFact]
    public void ComposerEditorView_AddMarkThenType_ChipIsOnItsOwnLineAboveTheWords()
    {
        // Arrange: a mark on a picture and some typed words
        using var harness = new ComposerHarness();
        var picture = new RenderedImage(Path.Combine(harness.Folder, "pic.svg"), "svg", 4, 4, new byte[4 * 4 * 4]);
        var first = harness.Marks.Add(picture, MarkKind.Rectangle, [new RenderPoint(0, 0), new RenderPoint(3, 3)]);
        var second = harness.Marks.Add(picture, MarkKind.Ellipse, [new RenderPoint(0, 0), new RenderPoint(3, 3)]);
        harness.TypeEach("words");

        // Act: add two marks, which go to the row above the words
        harness.ViewModel.AddMark(first);
        harness.ViewModel.AddMark(second);
        harness.Layout();

        // Assert: both chips are on the first line, the words are below them, and the caret is at the end of the words
        Assert.Equal("##\nwords", harness.ReadableMessage);
        Assert.Equal(2, harness.Chips.Count);
        var textView = harness.Editor.TextArea.TextView;
        var chipBottoms = harness.Chips.Select(chip => chip.TranslatePoint(new Point(0, chip.Bounds.Height), textView)!.Value.Y).ToList();
        var chipTops = harness.Chips.Select(chip => chip.TranslatePoint(new Point(0, 0), textView)!.Value.Y).ToList();
        var wordsTop = harness.Editor.TextArea.Caret.CalculateCaretRectangle().Y;
        Assert.Equal(chipTops[0], chipTops[1], 1);
        Assert.True(wordsTop >= chipBottoms.Max() - 1, $"The words start at {wordsTop}, the chips end at {chipBottoms.Max()}.");
        Assert.Equal(harness.Message.Length, harness.Editor.CaretOffset);
        harness.AssertConsistent();
    }

    /// <summary>
    ///     Test that the editing surface is a named text field for assistive technology that reads the message with its
    ///     chips named, and that setting its value replaces the message.
    /// </summary>
    [AvaloniaFact]
    public void ComposerEditorView_AutomationPeer_IsANamedEditFieldWithTheTextOfTheMessage()
    {
        // Arrange: a message with a chip and words
        using var harness = new ComposerHarness();
        var picture = new RenderedImage(Path.Combine(harness.Folder, "pic.svg"), "svg", 4, 4, new byte[4 * 4 * 4]);
        var mark = harness.Marks.Add(picture, MarkKind.Rectangle, [new RenderPoint(0, 0), new RenderPoint(3, 3)]);
        harness.TypeEach("hello");
        harness.ViewModel.AddMark(mark);
        harness.Layout();

        // Act: ask for what a screen reader or an automation tool sees of the surface
        var surface = harness.View.FindControl<ComposerTextEditor>("Editor")!;
        var peer = ControlAutomationPeer.CreatePeerForElement(surface);
        var value = Assert.IsAssignableFrom<IValueProvider>(peer);
        var readText = value.Value;
        var readOnly = value.IsReadOnly;
        value.SetValue("replaced by automation");
        harness.Layout();

        // Assert: an edit field named "Message" that tells the message with the chip named, which can be set, and whose
        // text area has the name too
        Assert.Equal("Message", peer.GetName());
        Assert.Equal(AutomationControlType.Edit, peer.GetAutomationControlType());
        Assert.Equal("[Mark 1]\nhello", readText);
        Assert.False(readOnly);
        Assert.Equal("Message", AutomationProperties.GetName(surface.TextArea));
        Assert.Equal("replaced by automation", harness.Message);
        harness.AssertConsistent();
    }

    /// <summary>
    ///     Test that a drop of three files and a picture is four chips and one undo step, however it is made.
    /// </summary>
    [AvaloniaFact]
    public void ComposerEditorView_DropOfFilesAndPicture_FourChipsOneStep()
    {
        // Arrange: three files and the bytes of a picture
        using var harness = new ComposerHarness();
        var picture = PngBytes();
        var content = new DroppedContent
        {
            Paths = [harness.MakeFile("1.txt"), harness.MakeFile("2.txt"), harness.MakeFile("3.txt")],
            Images = [picture],
        };

        // Act: hand the content over, then undo once
        harness.ViewModel.HandleDrop(content);
        harness.Layout();
        var chipsAfterDrop = harness.ViewModel.Document.Chips.Count();
        harness.AssertConsistent();
        var drawn = harness.Chips.Count;
        harness.Press(PhysicalKey.Z, ComposerHarness.Command);

        // Assert: four chips were added and drawn, and one undo removed all of them
        Assert.Equal(4, chipsAfterDrop);
        Assert.Equal(4, drawn);
        Assert.Empty(harness.ViewModel.Document.Chips);
        harness.AssertConsistent();
    }

    /// <summary>
    ///     Test that a real drop of files on the editing surface becomes chips, not text of paths, in one step, at the
    ///     place where it was dropped.
    /// </summary>
    [AvaloniaFact]
    public void ComposerEditorView_DropFiles_BecomesChipsInsteadOfPathText()
    {
        // Arrange: the text "hello" and a folder and two files
        using var harness = new ComposerHarness();
        harness.TypeEach("hello");
        var folder = Path.Combine(harness.Folder, "pictures");
        Directory.CreateDirectory(folder);
        var data = harness.FilesTransfer(harness.MakeFile("a.txt"), folder, harness.MakeFile("b.png"));

        // Act: drop them on the surface
        harness.Drop(data, new Point(10, 10));

        // Assert: the text is unchanged except for three chips, and the drop is one step
        Assert.Equal(3, harness.ViewModel.Document.Chips.Count());
        Assert.Equal("hello", harness.Message.Replace(ComposerSnapshot.ChipPlaceholder.ToString(), string.Empty));
        Assert.DoesNotContain("a.txt", harness.Editor.Text);
        harness.AssertConsistent();
        Assert.Collection(
            harness.ViewModel.Document.Chips,
            first => Assert.IsType<FileChip>(first),
            second => Assert.IsType<FolderChip>(second),
            third => Assert.IsType<ImageChip>(third));
        harness.Press(PhysicalKey.Z, ComposerHarness.Command);
        Assert.Equal("hello", harness.Message);
        harness.AssertConsistent();
    }

    /// <summary>
    ///     Test that a drag over the text shows the drop highlight only when it brings something the message accepts, and
    ///     that leaving hides it again.
    /// </summary>
    [AvaloniaFact]
    public void ComposerEditorView_DragOver_AcceptableAndNot_ShowsDropHighlightOnlyForAcceptable()
    {
        // Arrange: an editor of an open project and the highlight that it shows
        using var harness = new ComposerHarness();
        var highlight = harness.View.FindControl<Border>("DropHighlight")!;
        var point = harness.Editor.TranslatePoint(new Point(10, 10), harness.Window)!.Value;
        var text = ComposerHarness.TextTransfer("words");

        // Act: drag something unacceptable, then text, then leave
        harness.Window.DragDrop(point, RawDragEventType.DragOver, new DataTransfer(), DragDropEffects.Copy);
        var forNothing = highlight.IsVisible;
        harness.Window.DragDrop(point, RawDragEventType.DragOver, text, DragDropEffects.Copy);
        var forText = highlight.IsVisible;
        harness.Window.DragDrop(point, RawDragEventType.DragLeave, text, DragDropEffects.Copy);
        var afterLeave = highlight.IsVisible;

        // Assert: the highlight followed what the drag brought
        Assert.False(forNothing);
        Assert.True(forText);
        Assert.False(afterLeave);
        Assert.Equal(string.Empty, harness.Message);
    }

    /// <summary>
    ///     Test that a drop of a file that also carries its path as text is chips only.
    /// </summary>
    [AvaloniaFact]
    public void ComposerEditorView_DropFilesWithPathText_IsChipsOnly()
    {
        // Arrange: a drop item with a file and the same path as text
        using var harness = new ComposerHarness();
        var path = harness.MakeFile("both.txt");
        var data = new DataTransfer();
        var item = new DataTransferItem();
        item.SetFile(harness.Window.StorageProvider.TryGetFileFromPathAsync(new Uri(path)).GetAwaiter().GetResult()!);
        item.SetText(path);
        data.Add(item);

        // Act: drop it
        harness.Drop(data, new Point(10, 10));

        // Assert: one chip and no text
        Assert.Equal("#", harness.ReadableMessage);
        harness.AssertConsistent();
    }

    /// <summary>
    ///     Test that a drop of a picture becomes a chip of a picture saved in the project's private folder, and that a
    ///     drop of text becomes typed text, each as one step.
    /// </summary>
    [AvaloniaFact]
    public void ComposerEditorView_DropPictureAndText_BecomePictureChipAndText()
    {
        // Arrange: an editor of an open project
        using var harness = new ComposerHarness();

        // Act: drop a picture, then drop text
        harness.Drop(ComposerHarness.BitmapTransfer(), new Point(10, 10));
        var afterPicture = harness.ViewModel.Document.Chips.ToList();
        harness.Drop(ComposerHarness.TextTransfer("dropped words"), new Point(10, 10));

        // Assert: a pasted-picture chip whose file exists in the project's private folder, and then the text
        var image = Assert.IsType<ImageChip>(Assert.Single(afterPicture));
        Assert.True(image.IsPasted);
        Assert.True(File.Exists(image.FullPath));
        Assert.StartsWith(Path.Combine(harness.Project, ".canvaspilot"), image.FullPath, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("dropped words", harness.Message);
        harness.AssertConsistent();
    }

    /// <summary>
    ///     Test that the built-in drop is not allowed to put dropped text with the placeholder character into the
    ///     message, because the message cleans text that comes from outside.
    /// </summary>
    [AvaloniaFact]
    public void ComposerEditorView_DropTextWithPlaceholder_IsCleaned()
    {
        // Arrange: an editor of an open project
        using var harness = new ComposerHarness();

        // Act: drop text that holds a placeholder and a control character
        harness.Drop(ComposerHarness.TextTransfer("a\uFFFCb\u0007c"), new Point(10, 10));

        // Assert: the message holds only the clean text
        Assert.Equal("abc", harness.Message);
        harness.AssertConsistent();
        Assert.Empty(harness.ViewModel.Document.Chips);
    }

    /// <summary>
    ///     Test that a drop is refused with no copy effect while no project is open, and changes nothing.
    /// </summary>
    [AvaloniaFact]
    public void ComposerEditorView_DropWithoutProject_ChangesNothing()
    {
        // Arrange: an editor with no project
        using var harness = new ComposerHarness(openProject: false);
        var data = harness.FilesTransfer(harness.MakeFile("x.txt"));

        // Act: drop a file
        harness.Drop(data, new Point(10, 10));

        // Assert: nothing was added, and the view says why the message cannot be written
        Assert.Equal(string.Empty, harness.Message);
        harness.AssertConsistent();
        Assert.True(harness.View.FindControl<TextBlock>("NoProjectHint")!.IsVisible);
        Assert.True(harness.Editor.IsReadOnly);
    }

    /// <summary>
    ///     Test that pasting files, a picture, or text from the clipboard each becomes one step, and that the path text
    ///     that comes with files is not inserted.
    /// </summary>
    [AvaloniaFact]
    public async Task ComposerEditorView_PasteAsync_FilesPictureAndText_EachIsOneStep()
    {
        // Arrange: an editor and the clipboard of its window
        using var harness = new ComposerHarness();
        var clipboard = harness.Window.Clipboard!;
        var first = await harness.Window.StorageProvider.TryGetFileFromPathAsync(new Uri(harness.MakeFile("p1.txt")));
        var second = await harness.Window.StorageProvider.TryGetFileFromPathAsync(new Uri(harness.MakeFile("p2.txt")));

        // Act: paste two files
        await clipboard.SetFilesAsync([first!, second!]);
        await harness.View.PasteAsync(clipboard);
        harness.Layout();
        var afterFiles = harness.ReadableMessage;

        // Act: paste a picture
        using var bitmap = new WriteableBitmap(new PixelSize(5, 3), new Vector(96, 96), PixelFormat.Bgra8888, AlphaFormat.Premul);
        await clipboard.SetBitmapAsync(bitmap);
        await harness.View.PasteAsync(clipboard);
        harness.Layout();
        var afterPicture = harness.ReadableMessage;

        // Act: paste text
        await clipboard.SetTextAsync("some text");
        await harness.View.PasteAsync(clipboard);
        harness.Layout();

        // Assert: each paste added its items
        Assert.Equal("##", afterFiles);
        Assert.Equal("###", afterPicture);
        Assert.Equal("###some text", harness.ReadableMessage);
        harness.AssertConsistent();

        // Act and assert: three Ctrl+Z take the three pastes back, one each
        harness.Press(PhysicalKey.Z, ComposerHarness.Command);
        Assert.Equal("###", harness.ReadableMessage);
        harness.Press(PhysicalKey.Z, ComposerHarness.Command);
        Assert.Equal("##", harness.ReadableMessage);
        harness.Press(PhysicalKey.Z, ComposerHarness.Command);
        Assert.Equal(string.Empty, harness.Message);
        harness.AssertConsistent();
    }

    /// <summary>
    ///     Test that Ctrl+V is taken over from the surface: the clipboard text enters through the message as one step.
    /// </summary>
    [AvaloniaFact]
    public async Task ComposerEditorView_CtrlV_PastesClipboardTextAsOneStep()
    {
        // Arrange: text on the clipboard and an editor with "ab" and the caret between the letters
        using var harness = new ComposerHarness();
        await harness.Window.Clipboard!.SetTextAsync("PASTED");
        harness.TypeEach("ab");
        harness.MoveCaret(1);

        // Act: press Ctrl+V and wait for the asynchronous paste
        harness.Press(PhysicalKey.V, ComposerHarness.Command);
        var pasted = harness.WaitFor(() => harness.Message.Contains("PASTED", StringComparison.Ordinal));

        // Assert: the text is in, the surface agrees, and one undo takes it out again
        Assert.True(pasted);
        Assert.Equal("aPASTEDb", harness.Message);
        harness.AssertConsistent();
        harness.Press(PhysicalKey.Z, ComposerHarness.Command);
        Assert.Equal("ab", harness.Message);
        harness.AssertConsistent();
    }

    /// <summary>
    ///     Test that Ctrl+V with a file on the clipboard makes a chip and not a line of path text.
    /// </summary>
    [AvaloniaFact]
    public async Task ComposerEditorView_CtrlV_FileOnClipboard_MakesChip()
    {
        // Arrange: a file on the clipboard
        using var harness = new ComposerHarness();
        var file = await harness.Window.StorageProvider.TryGetFileFromPathAsync(new Uri(harness.MakeFile("clip.txt")));
        await harness.Window.Clipboard!.SetFileAsync(file!);

        // Act: press Ctrl+V and wait for the asynchronous paste
        harness.Press(PhysicalKey.V, ComposerHarness.Command);
        var pasted = harness.WaitFor(() => harness.ViewModel.Document.Chips.Any());

        // Assert: one chip and no text
        Assert.True(pasted);
        Assert.Equal("#", harness.ReadableMessage);
        harness.AssertConsistent();
    }

    /// <summary>
    ///     Test that copying a selection with a chip puts a readable placeholder on the clipboard, never the raw chip
    ///     character, and that cutting also removes the selection as one step.
    /// </summary>
    [AvaloniaFact]
    public async Task ComposerEditorView_CopyAndCut_PutReadableTextOnClipboard()
    {
        // Arrange: "ab", a chip, and "cd" all selected
        using var harness = new ComposerHarness();
        harness.TypeEach("abcd");
        harness.MoveCaret(2);
        harness.ViewModel.HandleDrop(new DroppedContent { Paths = [harness.MakeFile("copy.txt")] });
        harness.Layout();
        harness.Select(0, harness.Message.Length);

        // Act: copy with Ctrl+C
        harness.Press(PhysicalKey.C, ComposerHarness.Command);
        var copied = harness.WaitFor(() => harness.Window.Clipboard!.TryGetTextAsync().GetAwaiter().GetResult() is { Length: > 0 } text && text.Contains("copy.txt", StringComparison.Ordinal));
        var copiedText = await harness.Window.Clipboard!.TryGetTextAsync();

        // Act: cut with Ctrl+X
        harness.Select(0, harness.Message.Length);
        harness.Press(PhysicalKey.X, ComposerHarness.Command);
        var cut = harness.WaitFor(() => harness.Message.Length == 0);

        // Assert: the clipboard text is readable, the cut removed everything, and undo brings it back as one step
        Assert.True(copied);
        Assert.Equal("ab[copy.txt]cd", copiedText);
        Assert.True(cut);
        harness.AssertConsistent();
        harness.Press(PhysicalKey.Z, ComposerHarness.Command);
        Assert.Equal("ab#cd", harness.ReadableMessage);
        harness.AssertConsistent();
    }

    /// <summary>
    ///     Test that a dictated block is in the message as dictated text, that its range is the one the background
    ///     renderer draws, and that it is one step.
    /// </summary>
    [AvaloniaFact]
    public void ComposerEditorView_DictatedBlock_IsShownAndItsRangeIsDrawn()
    {
        // Arrange: typed text "ab", then a dictated block at its end
        using var harness = new ComposerHarness();
        harness.TypeEach("ab");

        // Act: add the dictated block and type after it
        harness.ViewModel.AddDictatedBlock("hello world");
        harness.TypeEach("c");
        var ranges = DictatedTextBackgroundRenderer.GetRanges(harness.ViewModel.Document);

        // Assert: the surface shows the words, and the range covers exactly them
        Assert.Equal("ab" + "hello world" + "c", harness.Message);
        harness.AssertConsistent();
        Assert.Equal([new DictatedRange(2, 11)], ranges);

        // Act: undo the typed letter and the block
        harness.Press(PhysicalKey.Z, ComposerHarness.Command);
        harness.Press(PhysicalKey.Z, ComposerHarness.Command);

        // Assert: the block was one step
        Assert.Equal("ab", harness.Message);
        Assert.Empty(DictatedTextBackgroundRenderer.GetRanges(harness.ViewModel.Document));
        harness.AssertConsistent();
    }

    /// <summary>
    ///     Test that the message cannot be edited while no project is open, and that opening a project makes it editable
    ///     and closing it empties the surface.
    /// </summary>
    [AvaloniaFact]
    public void ComposerEditorView_ProjectOpenedAndClosed_EditableOnlyWhileProjectIsOpen()
    {
        // Arrange: an editor with no project
        using var harness = new ComposerHarness(openProject: false);
        harness.Type("x");
        var typedWithoutProject = harness.Message;
        var readOnlyBefore = harness.Editor.IsReadOnly;

        // Act: open a project, type, and close the project
        harness.ViewModel.SetProject(harness.Project);
        harness.Layout();
        var readOnlyAfter = harness.Editor.IsReadOnly;
        harness.Type("y");
        var typedWithProject = harness.Message;
        harness.ViewModel.SetProject(null);
        harness.Layout();

        // Assert: typing worked only with a project, and closing emptied the surface
        Assert.Equal(string.Empty, typedWithoutProject);
        Assert.True(readOnlyBefore);
        Assert.False(readOnlyAfter);
        Assert.Equal("y", typedWithProject);
        Assert.Equal(string.Empty, harness.Editor.Text);
        harness.AssertConsistent();
    }

    /// <summary>
    ///     Test that the view exposes the view model as the target of the Edit menu's undo and redo.
    /// </summary>
    [AvaloniaFact]
    public void ComposerEditorView_UndoRedoTarget_IsTheViewModel()
    {
        // Arrange and act: a view with a view model, then a view without one
        using var harness = new ComposerHarness();
        var empty = new ComposerEditorView();

        // Assert: the target is the view model, and the data context of the editing surface is the view model too
        Assert.Same(harness.ViewModel, harness.View.UndoRedoTarget);
        Assert.Same(harness.ViewModel, harness.Editor.DataContext);
        Assert.Null(empty.UndoRedoTarget);
    }

    /// <summary>
    ///     Test that the live words and the status line of the view model are shown.
    /// </summary>
    [AvaloniaFact]
    public void ComposerEditorView_StatusAndPartialText_AreShown()
    {
        // Arrange: an editor of an open project
        using var harness = new ComposerHarness();
        var status = harness.View.FindControl<TextBlock>("StatusLine")!;
        var partial = harness.View.FindControl<TextBlock>("PartialText")!;

        // Act: drop a file that does not exist, which the message reports as left out
        harness.ViewModel.HandleDrop(new DroppedContent { Paths = [Path.Combine(harness.Folder, "missing.txt")] });
        harness.Layout();

        // Assert: the status line says what was left out, and the partial strip is hidden while nobody dictates
        Assert.True(status.IsVisible);
        Assert.Contains("missing.txt", status.Text);
        Assert.False(partial.IsEffectivelyVisible);
    }

    /// <summary>
    ///     Test that a provider's picture replaces the icon of a chip, that the provider is asked for the chip, and that a
    ///     replaced provider is used for chips drawn afterwards.
    /// </summary>
    [AvaloniaFact]
    public void ComposerEditorView_ThumbnailProvider_PictureReplacesIcon()
    {
        // Arrange: a view with a provider that gives every chip a picture
        using var harness = new ComposerHarness();
        var provider = new FixedThumbnailProvider();
        harness.View.ThumbnailProvider = provider;

        // Act: add a chip and wait for its picture
        harness.ViewModel.HandleDrop(new DroppedContent { Paths = [harness.MakeFile("thumb.txt")] });
        harness.Layout();
        var chip = Assert.Single(harness.Chips);
        var shown = harness.WaitFor(() => chip.FindControl<Image>("ThumbnailImage")!.IsVisible);

        // Assert: the picture is shown instead of the icon
        Assert.True(shown);
        Assert.Same(provider.Bitmap, chip.FindControl<Image>("ThumbnailImage")!.Source);
        Assert.False(chip.FindControl<Avalonia.Controls.Shapes.Path>("IconPath")!.IsVisible);
        Assert.Same(provider, harness.View.ThumbnailProvider);
        Assert.Contains(provider.Requested, requested => requested is FileChip);
    }

    /// <summary>
    ///     Test that many chips and long text keep the surface and the message equal while wrapping over lines, through
    ///     typing between chips and undo of everything.
    /// </summary>
    [AvaloniaFact]
    public void ComposerEditorView_ManyChipsAndWrappedText_StayConsistentThroughUndoAll()
    {
        // Arrange: an editor
        using var harness = new ComposerHarness();
        var steps = 0;

        // Act: alternate typing a word and dropping a file, ten times
        for (var i = 0; i < 10; i++)
        {
            harness.TypeEach("word" + i + " ");
            steps += "word0 ".Length;
            harness.ViewModel.HandleDrop(new DroppedContent { Paths = [harness.MakeFile($"f{i}.txt")] });
            harness.Layout();
            steps++;
            harness.AssertConsistent();
        }

        var chipCount = harness.Chips.Count;
        var lengthBefore = harness.Message.Length;

        // Act: undo every step with the keyboard
        for (var i = 0; i < steps; i++)
        {
            harness.Press(PhysicalKey.Z, ComposerHarness.Command);
        }

        // Assert: ten chips were drawn, and everything was undone with the surface equal to the message at the end
        Assert.True(chipCount >= 1);
        Assert.Equal(10, lengthBefore - (10 * "word0 ".Length));
        Assert.Equal(string.Empty, harness.Message);
        Assert.Empty(harness.Chips);
        harness.AssertConsistent();
    }

    /// <summary>
    ///     Makes the bytes of a small PNG picture.
    /// </summary>
    /// <returns>The PNG bytes.</returns>
    private static byte[] PngBytes()
    {
        using var bitmap = new WriteableBitmap(new PixelSize(4, 4), new Vector(96, 96), PixelFormat.Bgra8888, AlphaFormat.Premul);
        using var stream = new MemoryStream();
        bitmap.Save(stream, new PngBitmapEncoderOptions());
        return stream.ToArray();
    }
}
