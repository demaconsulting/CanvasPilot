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

using DemaConsulting.CanvasPilot.ProjectSubsystem;
using DemaConsulting.CanvasPilot.SettingsSubsystem;

namespace DemaConsulting.CanvasPilot.AppShellSubsystem;

/// <summary>
///     Shows the application's dialogs and platform pickers on behalf of the view models. The main window
///     implements it, because showing a dialog needs the window as owner; tests supply a fake so no real window
///     is needed. Every method completes when the user has closed the dialog.
/// </summary>
public interface IDialogService : IFolderPicker
{
    /// <summary>
    ///     Shows the New Project dialog modally. The view model reports whether it was accepted.
    /// </summary>
    /// <param name="viewModel">The dialog's view model.</param>
    /// <returns>A task that completes when the dialog has closed.</returns>
    Task ShowNewProjectAsync(NewProjectDialogViewModel viewModel);

    /// <summary>
    ///     Shows the settings dialog modally.
    /// </summary>
    /// <param name="viewModel">The dialog's view model.</param>
    /// <returns>A task that completes when the dialog has closed.</returns>
    Task ShowSettingsAsync(SettingsDialogViewModel viewModel);

    /// <summary>
    ///     Shows the About dialog modally.
    /// </summary>
    /// <param name="viewModel">The dialog's view model.</param>
    /// <returns>A task that completes when the dialog has closed.</returns>
    Task ShowAboutAsync(AboutDialogViewModel viewModel);

    /// <summary>
    ///     Asks the user to choose a project file.
    /// </summary>
    /// <param name="initialFolder">The folder to start in, or <see langword="null" /> for the platform default.</param>
    /// <returns>The full path of the chosen <c>.canvaspilot</c> file, or <see langword="null" /> when cancelled.</returns>
    Task<string?> PickProjectFileAsync(string? initialFolder);

    /// <summary>
    ///     Asks the user where to save a file.
    /// </summary>
    /// <param name="title">The picker title.</param>
    /// <param name="suggestedFileName">The file name to suggest, including its extension.</param>
    /// <param name="filterName">The name of the file type shown in the picker, for example <c>PNG image</c>.</param>
    /// <param name="filterPattern">The file name pattern of the type, for example <c>*.png</c>.</param>
    /// <returns>The full path chosen, or <see langword="null" /> when cancelled.</returns>
    Task<string?> PickSaveFileAsync(string title, string suggestedFileName, string filterName, string filterPattern);

    /// <summary>
    ///     Shows a message with an OK button.
    /// </summary>
    /// <param name="title">The dialog title.</param>
    /// <param name="message">The message.</param>
    /// <returns>A task that completes when the user has closed the message.</returns>
    Task ShowMessageAsync(string title, string message);

    /// <summary>
    ///     Asks the user to confirm an action.
    /// </summary>
    /// <param name="title">The dialog title.</param>
    /// <param name="message">The question.</param>
    /// <param name="confirmText">The label of the button that confirms.</param>
    /// <returns><see langword="true" /> when the user confirmed; <see langword="false" /> when cancelled.</returns>
    Task<bool> ConfirmAsync(string title, string message, string confirmText);

    /// <summary>
    ///     Asks the user to type a line of text.
    /// </summary>
    /// <param name="title">The dialog title.</param>
    /// <param name="message">The prompt.</param>
    /// <param name="initialText">The text to start with.</param>
    /// <param name="confirmText">The label of the button that accepts.</param>
    /// <returns>The text entered, or <see langword="null" /> when the user cancelled.</returns>
    Task<string?> PromptAsync(string title, string message, string initialText, string confirmText);
}
