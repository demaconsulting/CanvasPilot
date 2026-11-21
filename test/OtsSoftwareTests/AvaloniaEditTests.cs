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
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Input.Raw;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using AvaloniaEdit;
using AvaloniaEdit.Document;
using AvaloniaEdit.Rendering;

namespace OtsSoftwareTests;

/// <summary>
///     Self-validation tests for the AvaloniaEdit off-the-shelf software item: the text editing control that the message
///     composer is built on. They run the control headless, with the same calls the composer makes, and are the evidence
///     that it reports changes with exact offsets, draws inline controls in place of placeholder characters, lets the
///     application take over undo, paste, copy, cut, typing, and drops, and that these behave as the composer relies on.
/// </summary>
public class AvaloniaEditTests
{
    /// <summary>
    ///     The character that stands for an inline object in the text.
    /// </summary>
    private const char Placeholder = '\uFFFC';

    /// <summary>
    ///     Test that a placeholder character in the text is drawn as an inline element that holds a control, between the
    ///     pieces of text, and that the control is placed in the editing surface.
    /// </summary>
    [AvaloniaFact]
    public void AvaloniaEdit_VisualLine_PlaceholderCharacter_BecomesInlineObjectElementHoldingControl()
    {
        // Arrange: an editor with a generator that makes a button for each placeholder, and text with two
        var (window, editor) = Show("hello \uFFFC world \uFFFC end");
        var generator = new ButtonGenerator();
        editor.TextArea.TextView.ElementGenerators.Add(generator);
        editor.Text = "hello \uFFFC world \uFFFC end";
        Layout(window);

        // Act: construct the visual line of the text
        var line = editor.TextArea.TextView.GetOrConstructVisualLine(editor.Document.GetLineByNumber(1));

        // Assert: text, inline object, text, inline object, text, and the buttons are parts of the surface
        Assert.Equal(
            ["VisualLineText", "InlineObjectElement", "VisualLineText", "InlineObjectElement", "VisualLineText"],
            line.Elements.Select(element => element.GetType().Name));
        Assert.Equal(2, generator.Buttons.Count);
        Assert.All(generator.Buttons, button => Assert.True(button.IsVisible));

        window.Close();
    }

    /// <summary>
    ///     Test that the caret moves over an inline object as one position and that Backspace and Delete remove it as one
    ///     character.
    /// </summary>
    [AvaloniaFact]
    public void AvaloniaEdit_Caret_MovesOverInlineObjectAsOnePositionAndKeysDeleteItAsOneCharacter()
    {
        // Arrange: an editor with an inline object at offset 6, and the caret before it
        var (window, editor) = Show("hello \uFFFC world");
        editor.TextArea.TextView.ElementGenerators.Add(new ButtonGenerator());
        Layout(window);
        editor.CaretOffset = 6;

        // Act: move right over the object, then press Backspace
        Press(window, PhysicalKey.ArrowRight);
        var caretAfterMove = editor.CaretOffset;
        Press(window, PhysicalKey.Backspace);
        var afterBackspace = editor.Text;

        // Assert: one position was passed, and one character, the object, was removed
        Assert.Equal(7, caretAfterMove);
        Assert.Equal("hello  world", afterBackspace);

        window.Close();
    }

    /// <summary>
    ///     Test that the document reports each typed character, a removal, a commit of several characters, and typing over
    ///     a selection with the exact offset and the removed and inserted text.
    /// </summary>
    [AvaloniaFact]
    public void AvaloniaEdit_Document_TypingAndDeleting_ReportExactChanges()
    {
        // Arrange: an empty editor that records its changes
        var (window, editor) = Show(string.Empty);
        var log = Record(editor);

        // Act: type three characters, Backspace, a commit of five characters, and one character over a selection
        foreach (var character in "abc")
        {
            window.KeyTextInput(character.ToString());
        }

        Press(window, PhysicalKey.Backspace);
        window.KeyTextInput("hello");
        editor.Select(0, 2);
        window.KeyTextInput("X");

        // Assert: one change for each action, with offsets, removals, and insertions exactly as they happened
        Assert.Equal(
            [
                "@0 -0[] +1[a]",
                "@1 -0[] +1[b]",
                "@2 -0[] +1[c]",
                "@2 -1[c] +0[]",
                "@2 -0[] +5[hello]",
                "@0 -2[ab] +1[X]",
            ],
            log);

        window.Close();
    }

    /// <summary>
    ///     Test that a change written into the document by the program raises the same event as a typed one, so that a
    ///     program that writes the message into the editor must tell its own writes from the user's, and that a change
    ///     made inside the changed event is refused while one made when the update finishes is allowed.
    /// </summary>
    [AvaloniaFact]
    public void AvaloniaEdit_Document_ProgrammaticEdit_RaisesChangedAndNestedEditsFollowTheRules()
    {
        // Arrange: an editor with text, that records its changes
        var (window, editor) = Show("abc");
        var log = Record(editor);

        // Act: replace text from the program
        editor.Document.Replace(1, 1, "XYZ");

        // Assert: the event is the same as for an edit by the user
        Assert.Equal(["@1 -1[b] +3[XYZ]"], log);

        // Act: change the document inside the changed event, which must be refused
        Exception? refused = null;
        void Nested(object? sender, DocumentChangeEventArgs e)
        {
            editor.Document.Changed -= Nested;
            try
            {
                editor.Document.Insert(0, "Q");
            }
            catch (InvalidOperationException ex)
            {
                refused = ex;
            }
        }

        editor.Document.Changed += Nested;
        window.KeyTextInput("d");

        // Act: change the document when the update finishes, which must be allowed
        var finished = false;
        void AtFinish(object? sender, EventArgs e)
        {
            editor.Document.UpdateFinished -= AtFinish;
            editor.Document.Insert(0, "F");
            finished = true;
        }

        editor.Document.UpdateFinished += AtFinish;
        window.KeyTextInput("e");

        // Assert: the nested change was refused, and the change at the end of the update was made
        Assert.NotNull(refused);
        Assert.True(finished);
        Assert.StartsWith("F", editor.Text, StringComparison.Ordinal);

        window.Close();
    }

    /// <summary>
    ///     Test that a limit of zero on the undo stack leaves the control without a history, and that removing its undo
    ///     and redo commands and handling the keys in the application leaves the application's own undo in charge.
    /// </summary>
    [AvaloniaFact]
    public void AvaloniaEdit_UndoStack_SizeLimitZeroAndRemovedCommands_LeaveApplicationInCharge()
    {
        // Arrange: an editor whose history is switched off and whose undo and redo commands are removed, and key bindings
        var (window, editor) = Show(string.Empty);
        editor.Document.UndoStack.SizeLimit = 0;
        foreach (var binding in editor.TextArea.CommandBindings.Where(binding => binding.Command.Name is "Undo" or "Redo").ToList())
        {
            editor.TextArea.CommandBindings.Remove(binding);
        }

        var calls = new List<string>();
        editor.KeyBindings.Add(new KeyBinding { Gesture = new KeyGesture(Key.Z, KeyModifiers.Control), Command = new ActionCommand(() => calls.Add("undo")) });
        editor.KeyBindings.Add(new KeyBinding { Gesture = new KeyGesture(Key.Y, KeyModifiers.Control), Command = new ActionCommand(() => calls.Add("redo")) });
        editor.KeyBindings.Add(new KeyBinding { Gesture = new KeyGesture(Key.Z, KeyModifiers.Control | KeyModifiers.Shift), Command = new ActionCommand(() => calls.Add("redo-shift")) });

        // Act: type, then press the three undo and redo gestures
        foreach (var character in "abc")
        {
            window.KeyTextInput(character.ToString());
        }

        Press(window, PhysicalKey.Z, RawInputModifiers.Control);
        Press(window, PhysicalKey.Y, RawInputModifiers.Control);
        Press(window, PhysicalKey.Z, RawInputModifiers.Control | RawInputModifiers.Shift);

        // Assert: the control kept no history and changed nothing, and the application's handlers were called in order
        Assert.False(editor.CanUndo);
        Assert.Equal("abc", editor.Text);
        Assert.Equal(["undo", "redo", "redo-shift"], calls);

        window.Close();
    }

    /// <summary>
    ///     Test that the paste, copy, and cut commands can be replaced by the application, so that the keys and the
    ///     control's own methods reach the application's handlers and the text is untouched.
    /// </summary>
    [AvaloniaFact]
    public void AvaloniaEdit_CommandBindings_PasteCopyCut_CanBeReplaced()
    {
        // Arrange: an editor with text, whose paste, copy, and cut bindings are replaced by counters
        var (window, editor) = Show("hello");
        var calls = new List<string>();
        foreach (var name in new[] { "Paste", "Copy", "Cut" })
        {
            var existing = editor.TextArea.CommandBindings.First(binding => binding.Command.Name == name);
            editor.TextArea.CommandBindings.Remove(existing);
            var captured = name;
            editor.TextArea.CommandBindings.Add(new RoutedCommandBinding(
                existing.Command,
                (_, args) =>
                {
                    calls.Add(captured);
                    args.Handled = true;
                },
                (_, args) =>
                {
                    args.CanExecute = true;
                    args.Handled = true;
                }));
        }

        editor.Select(0, 2);

        // Act: press the three keys and call the control's paste method
        Press(window, PhysicalKey.V, CommandModifier);
        Press(window, PhysicalKey.C, CommandModifier);
        Press(window, PhysicalKey.X, CommandModifier);
        editor.Paste();

        // Assert: every call reached the application and the text was not changed
        Assert.Equal(["Paste", "Copy", "Cut", "Paste"], calls);
        Assert.Equal("hello", editor.Text);

        window.Close();
    }

    /// <summary>
    ///     Test that an application can refuse typed text before it enters, by marking the entering event handled, and
    ///     that the text typed after it still enters.
    /// </summary>
    [AvaloniaFact]
    public void AvaloniaEdit_TextEntering_Handled_RefusesTheTypedText()
    {
        // Arrange: an editor that refuses text with a placeholder character
        var (window, editor) = Show("ab");
        editor.CaretOffset = 2;
        editor.TextArea.TextEntering += (_, e) =>
        {
            if (e.Text is { } text && text.Contains(Placeholder, StringComparison.Ordinal))
            {
                e.Handled = true;
            }
        };

        // Act: type the placeholder, then a letter
        window.KeyTextInput(Placeholder.ToString());
        window.KeyTextInput("c");

        // Assert: only the letter entered
        Assert.Equal("abc", editor.Text);

        window.Close();
    }

    /// <summary>
    ///     Test that a drop of a file on the text view reaches a handler of the application when the application allows
    ///     drops there, even when the drop also carries text, and that the document is not changed by the control's own
    ///     drop handling.
    /// </summary>
    [AvaloniaFact]
    public void AvaloniaEdit_Drop_OnTextViewWithAllowDrop_ReachesApplicationHandler()
    {
        // Arrange: an editor with its own text drag turned off and a handler on the text view that allows drops
        var (window, editor) = Show("hello");
        editor.Options.EnableTextDragDrop = false;
        var view = editor.TextArea.TextView;
        DragDrop.SetAllowDrop(view, true);
        var seen = new List<string>();
        view.AddHandler(DragDrop.DragOverEvent, (_, e) =>
        {
            e.DragEffects = DragDropEffects.Copy;
            e.Handled = true;
        });
        view.AddHandler(DragDrop.DropEvent, (_, e) =>
        {
            seen.Add(string.Join(",", e.DataTransfer.Formats.Select(format => format.Identifier)));
            e.Handled = true;
        });

        var path = Path.Combine(Path.GetTempPath(), $"canvaspilot-ots-drop-{Guid.NewGuid():N}.txt");
        File.WriteAllText(path, "x");
        try
        {
            var item = new DataTransferItem();
            item.SetFile(window.StorageProvider.TryGetFileFromPathAsync(new Uri(path)).GetAwaiter().GetResult()!);
            item.SetText(path);
            var data = new DataTransfer();
            data.Add(item);
            var point = editor.TranslatePoint(new Point(40, 10), window)!.Value;

            // Act: drop the file, with its path as text, on the text view
            window.DragDrop(point, RawDragEventType.DragEnter, data, DragDropEffects.Copy);
            window.DragDrop(point, RawDragEventType.DragOver, data, DragDropEffects.Copy);
            window.DragDrop(point, RawDragEventType.Drop, data, DragDropEffects.Copy);

            // Assert: the application's handler saw the drop, and the control did not insert the path
            Assert.Single(seen);
            Assert.Equal("hello", editor.Text);
        }
        finally
        {
            File.Delete(path);
        }

        window.Close();
    }

    /// <summary>
    ///     Shows an editor with text in a window and puts the focus in it.
    /// </summary>
    /// <param name="text">The text of the editor.</param>
    /// <returns>The window and its editor.</returns>
    private static (Window Window, TextEditor Editor) Show(string text)
    {
        var editor = new TextEditor { Width = 400, Height = 200, WordWrap = true };
        var window = new Window { Width = 500, Height = 300, Content = editor };
        window.Show();
        editor.Text = text;
        editor.TextArea.Focus();
        Layout(window);
        return (window, editor);
    }

    /// <summary>
    ///     Lays the window out and runs the queued work, so that drawn lines are current.
    /// </summary>
    /// <param name="window">The window.</param>
    private static void Layout(Window window)
    {
        window.UpdateLayout();
        Dispatcher.UIThread.RunJobs();
        window.UpdateLayout();
    }

    /// <summary>
    ///     Gets the modifier of the control's clipboard shortcuts: the Command key on macOS and the Control key
    ///     elsewhere, which is the rule the control applies to its own copy, cut, and paste commands.
    /// </summary>
    private static RawInputModifiers CommandModifier => OperatingSystem.IsMacOS() ? RawInputModifiers.Meta : RawInputModifiers.Control;

    /// <summary>
    ///     Presses and releases a key.
    /// </summary>
    /// <param name="window">The window that has the focus.</param>
    /// <param name="key">The physical key.</param>
    /// <param name="modifiers">The modifiers held down.</param>
    private static void Press(Window window, PhysicalKey key, RawInputModifiers modifiers = RawInputModifiers.None)
    {
        window.KeyPressQwerty(key, modifiers);
        window.KeyReleaseQwerty(key, modifiers);
    }

    /// <summary>
    ///     Records the changes of the document of an editor as readable lines.
    /// </summary>
    /// <param name="editor">The editor.</param>
    /// <returns>The list that receives a line for each change.</returns>
    private static List<string> Record(TextEditor editor)
    {
        var log = new List<string>();
        editor.Document.Changed += (_, e) =>
            log.Add($"@{e.Offset} -{e.RemovalLength}[{e.RemovedText.Text}] +{e.InsertionLength}[{e.InsertedText.Text}]");
        return log;
    }

    /// <summary>
    ///     A generator that makes a button for each placeholder character, as the composer makes a chip.
    /// </summary>
    private sealed class ButtonGenerator : VisualLineElementGenerator
    {
        /// <summary>
        ///     Gets the buttons made so far.
        /// </summary>
        public List<Button> Buttons { get; } = [];

        /// <inheritdoc />
        public override int GetFirstInterestedOffset(int startOffset)
        {
            var end = CurrentContext.VisualLine.LastDocumentLine.EndOffset;
            for (var offset = startOffset; offset < end; offset++)
            {
                if (CurrentContext.Document.GetCharAt(offset) == Placeholder)
                {
                    return offset;
                }
            }

            return -1;
        }

        /// <inheritdoc />
        public override VisualLineElement? ConstructElement(int offset)
        {
            if (CurrentContext.Document.GetCharAt(offset) != Placeholder)
            {
                return null;
            }

            var button = new Button { Content = "chip", Width = 40, Height = 18 };
            Buttons.Add(button);
            return new InlineObjectElement(1, button);
        }
    }

    /// <summary>
    ///     A command that runs an action, for key bindings.
    /// </summary>
    /// <param name="action">The action to run.</param>
    private sealed class ActionCommand(Action action) : System.Windows.Input.ICommand
    {
        /// <inheritdoc />
        public event EventHandler? CanExecuteChanged
        {
            add
            {
                // The command can always run, so nobody needs to be told that it changed
            }

            remove
            {
                // Nothing was added
            }
        }

        /// <inheritdoc />
        public bool CanExecute(object? parameter) => true;

        /// <inheritdoc />
        public void Execute(object? parameter) => action();
    }
}
