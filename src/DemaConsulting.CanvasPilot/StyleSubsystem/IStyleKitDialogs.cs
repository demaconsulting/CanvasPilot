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

namespace DemaConsulting.CanvasPilot.StyleSubsystem;

/// <summary>
///     Shows the windows, the questions, and the file pickers that the Style Kits window needs. The main window
///     implements it, because showing a window or a picker needs the main window as its owner; tests supply a substitute,
///     so no window and no platform picker is needed.
/// </summary>
public interface IStyleKitDialogs
{
    /// <summary>
    ///     Shows the window that manages style kits, which does not block the main window. There is one such window at a
    ///     time: when it is open already, the open window is brought to the front, and the view model that was given is
    ///     disposed because it is not used.
    /// </summary>
    /// <param name="viewModel">The view model of the window.</param>
    /// <returns>A task that completes when the window is shown, not when it is closed.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="viewModel" /> is <see langword="null" />.</exception>
    Task ShowStyleKitManagerAsync(StyleKitManagerViewModel viewModel);

    /// <summary>
    ///     Shows the editor of a style kit modally over the window that manages style kits (or over the main window when
    ///     that is not shown).
    /// </summary>
    /// <param name="viewModel">The view model of the editor, which reports whether the kit was saved.</param>
    /// <returns>A task that completes when the editor has closed.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="viewModel" /> is <see langword="null" />.</exception>
    Task ShowStyleKitEditorAsync(StyleKitEditorViewModel viewModel);

    /// <summary>
    ///     Asks the user to choose a zip file that holds a kit.
    /// </summary>
    /// <param name="initialFolder">The folder to start in, or <see langword="null" /> for the platform default.</param>
    /// <returns>The full path of the chosen file, or <see langword="null" /> when the user cancelled.</returns>
    Task<string?> PickZipToImportAsync(string? initialFolder);

    /// <summary>
    ///     Asks the user where to write the zip file of a kit.
    /// </summary>
    /// <param name="suggestedFileName">The file name to suggest, such as <c>clean.zip</c>.</param>
    /// <returns>The full path to write, or <see langword="null" /> when the user cancelled.</returns>
    Task<string?> PickZipToExportAsync(string suggestedFileName);

    /// <summary>
    ///     Asks the user to choose example images, of any number.
    /// </summary>
    /// <param name="initialFolder">The folder to start in, or <see langword="null" /> for the platform default.</param>
    /// <returns>The full paths of the chosen files; empty when the user cancelled.</returns>
    Task<IReadOnlyList<string>> PickExampleFilesAsync(string? initialFolder);

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
