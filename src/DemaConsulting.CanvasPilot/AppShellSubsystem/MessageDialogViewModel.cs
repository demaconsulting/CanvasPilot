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

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace DemaConsulting.CanvasPilot.AppShellSubsystem;

/// <summary>
///     Event data for <see cref="MessageDialogViewModel.CloseRequested" />.
/// </summary>
/// <param name="accepted">
///     <see langword="true" /> when the dialog closed through its accepting button; <see langword="false" /> when
///     it was cancelled.
/// </param>
public sealed class MessageDialogCloseRequestedEventArgs(bool accepted) : EventArgs
{
    /// <summary>
    ///     Gets a value indicating whether the dialog closed through its accepting button.
    /// </summary>
    public bool Accepted { get; } = accepted;
}

/// <summary>
///     View model of the small dialog the shell uses for messages, confirmations, and one-line prompts. A message
///     has an OK button; a confirmation adds Cancel; a prompt adds a text entry whose content must not be blank.
/// </summary>
public sealed partial class MessageDialogViewModel : ObservableObject
{
    /// <summary>
    ///     The text typed in a prompt.
    /// </summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(OkCommand))]
    private string _inputText;

    /// <summary>
    ///     Creates the dialog view model.
    /// </summary>
    /// <param name="title">The dialog title.</param>
    /// <param name="message">The message or question.</param>
    /// <param name="okText">The label of the accepting button.</param>
    /// <param name="showCancel">Whether the dialog has a Cancel button.</param>
    /// <param name="inputText">The initial text of a prompt, or <see langword="null" /> when there is no text entry.</param>
    public MessageDialogViewModel(string title, string message, string okText = "OK", bool showCancel = false, string? inputText = null)
    {
        ArgumentNullException.ThrowIfNull(title);
        ArgumentNullException.ThrowIfNull(message);
        ArgumentException.ThrowIfNullOrWhiteSpace(okText);

        Title = title;
        Message = message;
        OkText = okText;
        ShowCancel = showCancel;
        IsPrompt = inputText is not null;
        _inputText = inputText ?? string.Empty;
    }

    /// <summary>
    ///     Raised when the dialog asks its host to close.
    /// </summary>
    public event EventHandler<MessageDialogCloseRequestedEventArgs>? CloseRequested;

    /// <summary>
    ///     Gets the dialog title.
    /// </summary>
    public string Title { get; }

    /// <summary>
    ///     Gets the message or question.
    /// </summary>
    public string Message { get; }

    /// <summary>
    ///     Gets the label of the accepting button.
    /// </summary>
    public string OkText { get; }

    /// <summary>
    ///     Gets a value indicating whether the dialog has a Cancel button.
    /// </summary>
    public bool ShowCancel { get; }

    /// <summary>
    ///     Gets a value indicating whether the dialog has a text entry.
    /// </summary>
    public bool IsPrompt { get; }

    /// <summary>
    ///     Gets a value indicating whether the dialog was closed through its accepting button.
    /// </summary>
    public bool Accepted { get; private set; }

    /// <summary>
    ///     Gets a value indicating whether the accepting button can be used: always, except for a prompt with a
    ///     blank entry.
    /// </summary>
    public bool CanAccept => !IsPrompt || !string.IsNullOrWhiteSpace(InputText);

    /// <summary>
    ///     Accepts the dialog and closes it.
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanAccept))]
    private void Ok()
    {
        Accepted = true;
        CloseRequested?.Invoke(this, new MessageDialogCloseRequestedEventArgs(accepted: true));
    }

    /// <summary>
    ///     Closes the dialog without accepting.
    /// </summary>
    [RelayCommand]
    private void Cancel()
    {
        CloseRequested?.Invoke(this, new MessageDialogCloseRequestedEventArgs(accepted: false));
    }
}
