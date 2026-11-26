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

using DemaConsulting.CanvasPilot.ChatSubsystem;
using DemaConsulting.CanvasPilot.ComposerSubsystem;
using DemaConsulting.CanvasPilot.PreviewSubsystem;
using DemaConsulting.CanvasPilot.ProjectSubsystem;
using DemaConsulting.CanvasPilot.SettingsSubsystem;
using DemaConsulting.CanvasPilot.StyleSubsystem;

namespace DemaConsulting.CanvasPilot.AppShellSubsystem;

/// <summary>
///     Shows the application's dialogs and platform pickers on behalf of the view models. The main window
///     implements it, because showing a dialog needs the window as owner; tests supply a fake so no real window
///     is needed. Every method completes when the user has closed the dialog, except the window that manages style kits,
///     which does not block and completes when it is shown. It includes the dialogs that lower subsystems ask for through
///     their own interfaces: the folder picker of the project subsystem, the export dialogs of the preview subsystem, the
///     payload inspector of the chat subsystem, the attachment picker of the composer subsystem, and the windows, file
///     pickers, and questions of the style kits window (which include asking the user to confirm an action and to type a
///     line of text).
/// </summary>
public interface IDialogService : IFolderPicker, IExportDialogs, IPayloadInspectorLauncher, IStyleKitDialogs, IAttachmentPicker
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
}
