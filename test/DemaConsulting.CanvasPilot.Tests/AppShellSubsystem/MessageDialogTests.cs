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

using DemaConsulting.CanvasPilot.AppShellSubsystem;

namespace DemaConsulting.CanvasPilot.Tests;

/// <summary>
///     Unit tests for the message, confirmation, and prompt dialog view model used by the MainWindowShell unit.
/// </summary>
public sealed class MessageDialogTests
{
    /// <summary>
    ///     Test that a message dialog has only an OK button, and OK accepts and asks to close.
    /// </summary>
    [Fact]
    public void MessageDialogViewModel_Message_OkAcceptsAndRequestsClose()
    {
        // Arrange: a message dialog with a listener
        var viewModel = new MessageDialogViewModel("Open Project", "The file is damaged.");
        var results = new List<bool>();
        viewModel.CloseRequested += (_, e) => results.Add(e.Accepted);

        // Act: choose OK
        viewModel.OkCommand.Execute(null);

        // Assert: the texts are exposed, there is no Cancel or text entry, and the dialog was accepted
        Assert.Equal("Open Project", viewModel.Title);
        Assert.Equal("The file is damaged.", viewModel.Message);
        Assert.Equal("OK", viewModel.OkText);
        Assert.False(viewModel.ShowCancel);
        Assert.False(viewModel.IsPrompt);
        Assert.True(viewModel.Accepted);
        Assert.Equal([true], results);
    }

    /// <summary>
    ///     Test that Cancel closes a confirmation without accepting it.
    /// </summary>
    [Fact]
    public void MessageDialogViewModel_Confirmation_CancelRequestsCloseWithoutAcceptance()
    {
        // Arrange: a confirmation with a listener
        var viewModel = new MessageDialogViewModel("Delete File", "Delete 'a.svg'?", "Delete", showCancel: true);
        var results = new List<bool>();
        viewModel.CloseRequested += (_, e) => results.Add(e.Accepted);

        // Act: choose Cancel
        viewModel.CancelCommand.Execute(null);

        // Assert: Cancel is offered, the button shows the confirm text, and the dialog was not accepted
        Assert.True(viewModel.ShowCancel);
        Assert.Equal("Delete", viewModel.OkText);
        Assert.False(viewModel.Accepted);
        Assert.Equal([false], results);
    }

    /// <summary>
    ///     Test that a prompt cannot be accepted while its entry is blank.
    /// </summary>
    [Fact]
    public void MessageDialogViewModel_Prompt_BlankEntry_CannotBeAccepted()
    {
        // Arrange: a prompt that starts with text
        var viewModel = new MessageDialogViewModel("Rename File", "New name:", "Rename", showCancel: true, inputText: "a.svg");
        var changes = 0;
        viewModel.OkCommand.CanExecuteChanged += (_, _) => changes++;
        var enabledWithText = viewModel.OkCommand.CanExecute(null);

        // Act: clear the entry, then type a name again
        viewModel.InputText = "  ";
        var enabledWhenBlank = viewModel.OkCommand.CanExecute(null);
        viewModel.InputText = "b.svg";

        // Assert: OK follows the entry and the command announced the changes
        Assert.True(viewModel.IsPrompt);
        Assert.True(enabledWithText);
        Assert.False(enabledWhenBlank);
        Assert.True(viewModel.OkCommand.CanExecute(null));
        Assert.True(changes >= 2);
    }

    /// <summary>
    ///     Test that the dialog refuses missing texts and a blank accept label.
    /// </summary>
    [Fact]
    public void MessageDialogViewModel_Constructor_InvalidArguments_Throw()
    {
        // Arrange, Act & Assert: missing texts and a blank label are refused
        Assert.Throws<ArgumentNullException>(() => new MessageDialogViewModel(null!, "m"));
        Assert.Throws<ArgumentNullException>(() => new MessageDialogViewModel("t", null!));
        Assert.Throws<ArgumentException>(() => new MessageDialogViewModel("t", "m", " "));
    }
}
