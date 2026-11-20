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

using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using DemaConsulting.CanvasPilot.ChatSubsystem;
using DemaConsulting.CanvasPilot.ComposerSubsystem;
using DemaConsulting.CanvasPilot.PreviewSubsystem;
using DemaConsulting.CanvasPilot.ProjectSubsystem;
using DemaConsulting.CanvasPilot.SettingsSubsystem;

namespace DemaConsulting.CanvasPilot.AppShellSubsystem;

/// <summary>
///     The application main window, bound to a <see cref="MainWindowShell" />. It also implements
///     <see cref="IDialogService" />, because showing dialogs and platform pickers needs the window as owner.
///     Exit closes the window. It also tells the shell what has the focus (for Edit > Undo and Redo), and passes the
///     talk key and the loss of the active state on to it. The window is the payload inspector launcher of the chat
///     panel, which the workspace connects when the dialog service is attached.
/// </summary>
public sealed partial class MainWindowView : Window, IDialogService
{
    /// <summary>
    ///     The project file type offered by the open file picker.
    /// </summary>
    private static readonly FilePickerFileType ProjectFileType = new("CanvasPilot projects")
    {
        Patterns = ["*" + ProjectDocument.FileExtension]
    };

    /// <summary>
    ///     View model bound to the window.
    /// </summary>
    private readonly MainWindowShell? _shell;

    /// <summary>
    ///     Initializes a new instance of the <see cref="MainWindowView" /> class for the XAML loader. The window
    ///     has no content until a view model is supplied.
    /// </summary>
    public MainWindowView()
    {
        InitializeComponent();
    }

    /// <summary>
    ///     Initializes a new instance of the <see cref="MainWindowView" /> class.
    /// </summary>
    /// <param name="shell">The view model that backs the window.</param>
    public MainWindowView(MainWindowShell shell)
        : this()
    {
        ArgumentNullException.ThrowIfNull(shell);

        _shell = shell;
        DataContext = shell;
        shell.AttachDialogService(this);
        shell.ExitRequested += OnExitRequested;

        // What has the focus decides what Edit > Undo and Redo reach; the talk key is watched before any control sees it
        AddHandler(GotFocusEvent, OnFocusGained, RoutingStrategies.Bubble, handledEventsToo: true);
        AddHandler(KeyDownEvent, OnKeyDownTunnel, RoutingStrategies.Tunnel);
        AddHandler(KeyUpEvent, OnKeyUpTunnel, RoutingStrategies.Tunnel);
        Deactivated += OnWindowDeactivated;
    }

    /// <inheritdoc />
    public async Task ShowNewProjectAsync(NewProjectDialogViewModel viewModel)
    {
        ArgumentNullException.ThrowIfNull(viewModel);
        await new NewProjectDialogView(viewModel).ShowDialog<bool>(this);
    }

    /// <inheritdoc />
    public async Task ShowSettingsAsync(SettingsDialogViewModel viewModel)
    {
        ArgumentNullException.ThrowIfNull(viewModel);
        await new SettingsDialogView(viewModel).ShowDialog<bool>(this);
    }

    /// <inheritdoc />
    public async Task ShowAboutAsync(AboutDialogViewModel viewModel)
    {
        ArgumentNullException.ThrowIfNull(viewModel);
        await new AboutDialogView(viewModel).ShowDialog(this);
    }

    /// <inheritdoc />
    public async Task ShowPayloadInspectorAsync(PayloadInspectorViewModel viewModel)
    {
        ArgumentNullException.ThrowIfNull(viewModel);
        await new PayloadInspectorView(viewModel).ShowDialog(this);
    }

    /// <inheritdoc />
    public async Task<string?> PickFolderAsync(string? initialFolder)
    {
        var options = new FolderPickerOpenOptions
        {
            Title = "Choose Folder",
            AllowMultiple = false,
            SuggestedStartLocation = await TryGetFolderAsync(initialFolder)
        };

        var folders = await StorageProvider.OpenFolderPickerAsync(options);
        return folders.Count == 0 ? null : folders[0].TryGetLocalPath();
    }

    /// <inheritdoc />
    public async Task<string?> PickProjectFileAsync(string? initialFolder)
    {
        var options = new FilePickerOpenOptions
        {
            Title = "Open Project",
            AllowMultiple = false,
            FileTypeFilter = [ProjectFileType],
            SuggestedStartLocation = await TryGetFolderAsync(initialFolder)
        };

        var files = await StorageProvider.OpenFilePickerAsync(options);
        return files.Count == 0 ? null : files[0].TryGetLocalPath();
    }

    /// <inheritdoc />
    public async Task<string?> PickSaveFileAsync(string title, string suggestedFileName, string filterName, string filterPattern)
    {
        var options = new FilePickerSaveOptions
        {
            Title = title,
            SuggestedFileName = suggestedFileName,
            DefaultExtension = Path.GetExtension(filterPattern),
            ShowOverwritePrompt = true,
            FileTypeChoices = [new FilePickerFileType(filterName) { Patterns = [filterPattern] }]
        };

        var file = await StorageProvider.SaveFilePickerAsync(options);
        return file?.TryGetLocalPath();
    }

    /// <inheritdoc />
    public async Task<bool> CopyImageToClipboardAsync(RenderedImage image)
    {
        ArgumentNullException.ThrowIfNull(image);

        var clipboard = Clipboard;
        if (clipboard is null)
        {
            return false;
        }

        try
        {
            // The bitmap is handed to the clipboard, which may render it on demand, so it is not disposed here;
            // the flush asks the platform to render it now so it stays available after the application exits.
            var bitmap = RenderedImageBitmap.Create(image);
            await clipboard.SetBitmapAsync(bitmap);
            await clipboard.FlushAsync();
            return true;
        }
        catch (Exception ex) when (ex is InvalidOperationException or NotSupportedException or COMException or IOException)
        {
            // The platform clipboard is shared with other programs and can be locked or unavailable.
            return false;
        }
    }

    /// <inheritdoc />
    public async Task ShowMessageAsync(string title, string message)
    {
        await new MessageDialogView(new MessageDialogViewModel(title, message)).ShowDialog<bool>(this);
    }

    /// <inheritdoc />
    public async Task<bool> ConfirmAsync(string title, string message, string confirmText)
    {
        var viewModel = new MessageDialogViewModel(title, message, confirmText, showCancel: true);
        await new MessageDialogView(viewModel).ShowDialog<bool>(this);
        return viewModel.Accepted;
    }

    /// <inheritdoc />
    public async Task<string?> PromptAsync(string title, string message, string initialText, string confirmText)
    {
        var viewModel = new MessageDialogViewModel(title, message, confirmText, showCancel: true, inputText: initialText);
        await new MessageDialogView(viewModel).ShowDialog<bool>(this);
        return viewModel.Accepted ? viewModel.InputText.Trim() : null;
    }

    /// <inheritdoc />
    protected override void OnClosed(EventArgs e)
    {
        if (_shell is not null)
        {
            _shell.ExitRequested -= OnExitRequested;
            Deactivated -= OnWindowDeactivated;
            _ = _shell.EndPushToTalkAsync();
        }

        base.OnClosed(e);
    }

    /// <summary>
    ///     Finds what the Edit menu reaches: the nearest control from the one that took the focus, itself included, up
    ///     through its parents, whose data context can undo and redo. A control that is part of a menu is skipped, so
    ///     choosing a menu item does not change what the item acts on.
    /// </summary>
    /// <param name="sender">The window.</param>
    /// <param name="e">The event data.</param>
    private void OnFocusGained(object? sender, FocusChangedEventArgs e)
    {
        if (_shell is null || e.Source is not StyledElement source)
        {
            return;
        }

        IUndoRedoTarget? target = null;
        for (var element = source; element is not null; element = element.Parent)
        {
            if (element is Menu or MenuItem)
            {
                return;
            }

            if (element.DataContext is IUndoRedoTarget found)
            {
                target = found;
                break;
            }
        }

        _shell.EditTarget = target;
    }

    /// <summary>
    ///     Starts listening when the talk key goes down. The keyboard repeats a held key, which the shell ignores.
    /// </summary>
    /// <param name="sender">The window.</param>
    /// <param name="e">The event data.</param>
    private void OnKeyDownTunnel(object? sender, KeyEventArgs e)
    {
        if (_shell is not { PushToTalk: not null } || e.Key != MainWindowShell.PushToTalkKey)
        {
            return;
        }

        e.Handled = true;
        _ = _shell.BeginPushToTalkAsync();
    }

    /// <summary>
    ///     Stops listening when the talk key goes up.
    /// </summary>
    /// <param name="sender">The window.</param>
    /// <param name="e">The event data.</param>
    private void OnKeyUpTunnel(object? sender, KeyEventArgs e)
    {
        if (_shell is null || e.Key != MainWindowShell.PushToTalkKey)
        {
            return;
        }

        if (_shell.IsPushToTalkActive)
        {
            e.Handled = true;
        }

        _ = _shell.EndPushToTalkAsync();
    }

    /// <summary>
    ///     Stops listening when the window is no longer the active one, because the release of the key would go to
    ///     another window.
    /// </summary>
    /// <param name="sender">The window.</param>
    /// <param name="e">The event data.</param>
    private void OnWindowDeactivated(object? sender, EventArgs e)
    {
        if (_shell is not null)
        {
            _ = _shell.EndPushToTalkAsync();
        }
    }

    /// <summary>
    ///     Resolves a folder path to a storage folder for a picker's start location.
    /// </summary>
    /// <param name="folder">The folder path, or <see langword="null" />.</param>
    /// <returns>The storage folder, or <see langword="null" /> when none was given or it cannot be used.</returns>
    private async Task<IStorageFolder?> TryGetFolderAsync(string? folder)
    {
        return string.IsNullOrWhiteSpace(folder) ? null : await StorageProvider.TryGetFolderFromPathAsync(folder);
    }

    /// <summary>
    ///     Closes the window when the user chose Exit.
    /// </summary>
    /// <param name="sender">The shell.</param>
    /// <param name="e">The event data.</param>
    private void OnExitRequested(object? sender, EventArgs e)
    {
        Close();
    }
}
