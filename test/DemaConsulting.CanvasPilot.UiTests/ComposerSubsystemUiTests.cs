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
using Avalonia.Input.Platform;
using Avalonia.Input.Raw;
using DemaConsulting.CanvasPilot.ComposerSubsystem;

namespace DemaConsulting.CanvasPilot.UiTests;

/// <summary>
///     Headless UI tests of the Composer subsystem with its editing view: the view, the view model, the document, the
///     history, the intake, and the store together, with only the dictation and the message builder replaced by simple
///     stand-ins.
/// </summary>
public sealed class ComposerSubsystemUiTests
{
    /// <summary>
    ///     Test that a message made of typed text, a dictated block, dropped files, pasted text, and more typing keeps
    ///     the editing surface equal to the message at every step, and that every action is one step of undo and redo.
    /// </summary>
    [AvaloniaFact]
    public async Task ComposerSubsystem_EditMessageInEditor_MixedActions_SurfaceAlwaysEqualsMessage()
    {
        // Arrange: an editor of an open project and some text on the clipboard
        using var harness = new ComposerHarness();
        await harness.Window.Clipboard!.SetTextAsync("world");
        var states = new List<string> { harness.ReadableMessage };

        // Act: type, dictate, drop two files at the end, paste, and type again, noting the message after each action
        harness.TypeEach("Hi ");
        states.Add(harness.ReadableMessage);
        harness.ViewModel.AddDictatedBlock("brave new ");
        harness.Layout();
        harness.AssertConsistent();
        states.Add(harness.ReadableMessage);
        harness.MoveCaret(harness.Message.Length);
        harness.Drop(harness.FilesTransfer(harness.MakeFile("a.txt"), harness.MakeFile("b.txt")), new Point(300, 150));
        harness.AssertConsistent();
        states.Add(harness.ReadableMessage);
        harness.MoveCaret(harness.Message.Length);
        await harness.View.PasteAsync(harness.Window.Clipboard);
        harness.Layout();
        harness.AssertConsistent();
        states.Add(harness.ReadableMessage);
        harness.TypeEach("!");
        harness.AssertConsistent();
        states.Add(harness.ReadableMessage);

        // Assert: the final message is the text, the dictated words, two chips, the pasted text, and the typed mark
        Assert.Equal("Hi brave new ##world!", harness.ReadableMessage);

        // Act and assert: undo every action, checking the state and the surface each time
        for (var index = states.Count - 2; index >= 0; index--)
        {
            // Typing is one step per character, so the steps of a typed word are taken together
            while (harness.ReadableMessage != states[index])
            {
                harness.Press(PhysicalKey.Z, ComposerHarness.Command);
                harness.AssertConsistent();
            }
        }

        Assert.Equal(string.Empty, harness.Message);

        // Act and assert: redo everything with Ctrl+Y, ending at the same message
        while (harness.ViewModel.CanRedo)
        {
            harness.Press(PhysicalKey.Y, ComposerHarness.Command);
            harness.AssertConsistent();
        }

        Assert.Equal("Hi brave new ##world!", harness.ReadableMessage);
        Assert.Equal(2, harness.ViewModel.Document.Chips.Count());
    }

    /// <summary>
    ///     Test that chips of a file, a folder, and a picture are drawn inline in the order of the message, that deleting
    ///     one with its button is one step, and that undo and redo draw them again in the same places.
    /// </summary>
    [AvaloniaFact]
    public void ComposerSubsystem_ShowChipsInline_DropThenDeleteAndUndo_ChipsFollowTheMessage()
    {
        // Arrange: the text "see " followed by a dropped file, folder, and picture
        using var harness = new ComposerHarness();
        harness.TypeEach("see ");
        var folder = Directory.CreateDirectory(Path.Combine(harness.Folder, "refs")).FullName;
        harness.Drop(harness.FilesTransfer(harness.MakeFile("plan.txt"), folder, harness.MakeFile("logo.png")), new Point(300, 150));
        static IEnumerable<string?> Labels(ComposerHarness h) =>
            h.Chips.Select(chip => chip.FindControl<TextBlock>("LabelText")!.Text);

        // Assert: three chips, drawn in the order of the message after the text
        Assert.Equal("see ###", harness.ReadableMessage);
        Assert.Equal(["plan.txt", "refs", "logo.png"], Labels(harness));
        harness.AssertConsistent();

        // Act: delete the middle chip with its button
        var button = harness.Chips[1].FindControl<Button>("DeleteButton")!;
        var center = button.TranslatePoint(new Point(button.Bounds.Width / 2, button.Bounds.Height / 2), harness.Window)!.Value;
        harness.Window.MouseDown(center, MouseButton.Left);
        harness.Window.MouseUp(center, MouseButton.Left);
        harness.Layout();

        // Assert: the other two are left, in order
        Assert.Equal(["plan.txt", "logo.png"], Labels(harness));
        harness.AssertConsistent();

        // Act and assert: undo brings the middle chip back in its place, and redo removes it again
        harness.Press(PhysicalKey.Z, ComposerHarness.Command);
        Assert.Equal(["plan.txt", "refs", "logo.png"], Labels(harness));
        harness.AssertConsistent();
        harness.Press(PhysicalKey.Y, ComposerHarness.Command);
        Assert.Equal(["plan.txt", "logo.png"], Labels(harness));
        harness.AssertConsistent();
    }
}
