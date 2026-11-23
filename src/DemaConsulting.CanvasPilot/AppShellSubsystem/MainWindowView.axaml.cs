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
using Avalonia.Threading;
using Avalonia.VisualTree;
using DemaConsulting.CanvasNet.Viewer;
using DemaConsulting.CanvasPilot.ChatSubsystem;
using DemaConsulting.CanvasPilot.ComposerSubsystem;
using DemaConsulting.CanvasPilot.KnowledgeSubsystem;
using DemaConsulting.CanvasPilot.PreviewSubsystem;
using DemaConsulting.CanvasPilot.ProjectSubsystem;
using DemaConsulting.CanvasPilot.SettingsSubsystem;
using DemaConsulting.CanvasPilot.StyleSubsystem;

namespace DemaConsulting.CanvasPilot.AppShellSubsystem;

/// <summary>
///     The application main window, bound to a <see cref="MainWindowShell" />. It also implements
///     <see cref="IDialogService" />, because showing dialogs and platform pickers needs the window as owner.
///     Exit closes the window. It also tells the shell what has the focus (for Edit > Undo and Redo), and passes the
///     talk key and the loss of the active state on to it. The window is the payload inspector launcher of the chat
///     panel, which the workspace connects when the dialog service is attached. It also shows the browser of the drawing
///     knowledge, and the window that manages style kits, each as a window of its own that does not block it, one at a time,
///     and the editor of a style kit modally over the latter.
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
    ///     The zip file type offered by the pickers of the kits.
    /// </summary>
    private static readonly FilePickerFileType ZipFileType = new("Zip files")
    {
        Patterns = ["*.zip"]
    };

    /// <summary>
    ///     The image source file types that CanvasNet draws, offered by the picker of the example images of a kit.
    /// </summary>
    private static readonly FilePickerFileType ImageSourceFileType = new("Image source files")
    {
        Patterns = [.. CanvasNetViewer.Extensions.Select(extension => "*" + extension)]
    };

    /// <summary>
    ///     The width of one window button that the window draws over the right end of the title bar (the theme's
    ///     <c>CaptionButtonWidth</c>).
    /// </summary>
    private const double CaptionButtonWidth = 45;

    /// <summary>
    ///     The space between two window buttons (the <c>Spacing</c> of the panel that holds them in the template of the
    ///     drawn decorations).
    /// </summary>
    private const double CaptionButtonSpacing = 2;

    /// <summary>
    ///     The number of window buttons that remain once the full-screen button is removed: minimize, maximize or restore,
    ///     and close.
    /// </summary>
    private const int CaptionButtonCount = 3;

    /// <summary>
    ///     The thickness of the frame line that the drawn decorations keep at the right of the buttons.
    /// </summary>
    private const double CaptionFrameThickness = 1;

    /// <summary>
    ///     The width of the window buttons that Windows draws over the right end of the title bar: three buttons of 45 with
    ///     the space between them and the frame line, which is 140 device-independent pixels. It is the fallback for the
    ///     time before the buttons are laid out; the width that is measured wins.
    /// </summary>
    internal const double WindowsCaptionButtonsWidth =
        (CaptionButtonCount * CaptionButtonWidth) + ((CaptionButtonCount - 1) * CaptionButtonSpacing) + CaptionFrameThickness;

    /// <summary>
    ///     The name of the panel in the drawn decorations that holds the window buttons.
    /// </summary>
    private const string CaptionButtonsPanelName = "PART_OverlayPanel";

    /// <summary>
    ///     The width of the traffic lights that macOS draws over the left end of the title bar.
    /// </summary>
    private const double MacTrafficLightsWidth = 78;

    /// <summary>
    ///     The place of the window while it is in its normal state, or <see langword="null" /> when it was not reported yet.
    /// </summary>
    private PixelPoint? _normalPosition;

    /// <summary>
    ///     View model bound to the window.
    /// </summary>
    private readonly MainWindowShell? _shell;

    /// <summary>
    ///     The browser of the drawing knowledge while it is open, or <see langword="null" />.
    /// </summary>
    private KnowledgeBrowserView? _knowledgeBrowser;

    /// <summary>
    ///     Whether the XAML is fully loaded. While it loads, the window has no name scope and the named title bar
    ///     controls do not exist yet, but the platform can already raise property changes (the extension into the
    ///     frame is one), so changes that reach the title bar are ignored until this is set.
    /// </summary>
    private bool _componentReady;

    /// <summary>
    ///     The window that manages style kits while it is open, or <see langword="null" />.
    /// </summary>
    private StyleKitManagerView? _styleKitManager;

    /// <summary>
    ///     Initializes a new instance of the <see cref="MainWindowView" /> class for the XAML loader. The window
    ///     has no content until a view model is supplied.
    /// </summary>
    public MainWindowView()
    {
        InitializeComponent();

        // The platform may already have raised the extension while the XAML was loading (when it was ignored, see
        // OnPropertyChanged), so apply the value it reached once, now that the title bar controls exist
        _componentReady = true;
        UpdateCaptionReserve();
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
        ApplySavedLayout(shell.SavedWindow);
        PositionChanged += OnWindowPositionChanged;

        // What has the focus decides what Edit > Undo and Redo reach; the talk key is watched before any control sees it
        AddHandler(GotFocusEvent, OnFocusGained, RoutingStrategies.Bubble, handledEventsToo: true);
        AddHandler(KeyDownEvent, OnKeyDownTunnel, RoutingStrategies.Tunnel);
        AddHandler(KeyUpEvent, OnKeyUpTunnel, RoutingStrategies.Tunnel);
        Deactivated += OnWindowDeactivated;
    }

    /// <summary>
    ///     Works out how much room the title bar keeps free for the buttons the system draws over it: on the right for
    ///     Windows (the minimize, maximize, and close buttons) and on the left for macOS (the traffic lights). A window that
    ///     is not extended into its frame has its buttons in a system title bar of its own, so nothing is kept free.
    /// </summary>
    /// <param name="isExtended">Whether the window extends into its frame.</param>
    /// <param name="isWindows">Whether the platform is Windows.</param>
    /// <param name="isMacOs">Whether the platform is macOS.</param>
    /// <returns>The width kept free on the left and on the right, in device-independent pixels.</returns>
    internal static (double Left, double Right) CaptionReserve(bool isExtended, bool isWindows, bool isMacOs)
    {
        if (!isExtended)
        {
            return (0, 0);
        }

        if (isWindows)
        {
            return (0, WindowsCaptionButtonsWidth);
        }

        return isMacOs ? (MacTrafficLightsWidth, 0) : (0, 0);
    }

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        // Raised while the XAML loads on a real window (the hint in the XAML sets it); the constructor applies it after
        if (change.Property == IsExtendedIntoWindowDecorationsProperty && _componentReady)
        {
            UpdateCaptionReserve();
            RequestCaptionReserveUpdate();
        }
        else if (change.Property == WindowStateProperty)
        {
            // The window has no full-screen mode: its button is removed, and nothing else may take it there, because the
            // only way back from full screen is a pop-up bar that is hard to find
            if (WindowState == WindowState.FullScreen)
            {
                WindowState = WindowState.Normal;
            }

            RequestCaptionReserveUpdate();
        }
    }

    /// <inheritdoc />
    protected override void OnOpened(EventArgs e)
    {
        base.OnOpened(e);
        RequestCaptionReserveUpdate();
    }

    /// <inheritdoc />
    protected override void OnSizeChanged(SizeChangedEventArgs e)
    {
        base.OnSizeChanged(e);
        RequestCaptionReserveUpdate();
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
    public Task ShowKnowledgeBrowserAsync(KnowledgeBrowserViewModel viewModel)
    {
        ArgumentNullException.ThrowIfNull(viewModel);

        // There is one browser: asking again brings the open one to the front, as it was left
        if (_knowledgeBrowser is { } open)
        {
            if (open.WindowState == WindowState.Minimized)
            {
                open.WindowState = WindowState.Normal;
            }

            open.Activate();
            return Task.CompletedTask;
        }

        var browser = new KnowledgeBrowserView(viewModel);
        browser.Closed += OnKnowledgeBrowserClosed;
        _knowledgeBrowser = browser;
        browser.Show(this);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task ShowStyleKitManagerAsync(StyleKitManagerViewModel viewModel)
    {
        ArgumentNullException.ThrowIfNull(viewModel);

        // There is one window: asking again brings the open one to the front, and the view model that was made is not used
        if (_styleKitManager is { } open)
        {
            if (open.WindowState == WindowState.Minimized)
            {
                open.WindowState = WindowState.Normal;
            }

            open.Activate();
            viewModel.Dispose();
            return Task.CompletedTask;
        }

        var manager = new StyleKitManagerView(viewModel);
        manager.Closed += OnStyleKitManagerClosed;
        _styleKitManager = manager;
        manager.Show(this);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public async Task ShowStyleKitEditorAsync(StyleKitEditorViewModel viewModel)
    {
        ArgumentNullException.ThrowIfNull(viewModel);

        // The editor is modal over the window that manages kits, which it is opened from, or else over the main window
        Window owner = _styleKitManager is { IsVisible: true } manager ? manager : this;
        await new StyleKitEditorView(viewModel).ShowDialog(owner);
    }

    /// <inheritdoc />
    public async Task<string?> PickZipToImportAsync(string? initialFolder)
    {
        var options = new FilePickerOpenOptions
        {
            Title = "Import Style Kit",
            AllowMultiple = false,
            FileTypeFilter = [ZipFileType],
            SuggestedStartLocation = await TryGetFolderAsync(initialFolder)
        };

        var files = await StorageProvider.OpenFilePickerAsync(options);
        return files.Count == 0 ? null : files[0].TryGetLocalPath();
    }

    /// <inheritdoc />
    public Task<string?> PickZipToExportAsync(string suggestedFileName)
    {
        return PickSaveFileAsync("Export Style Kit", suggestedFileName, "Zip files", "*.zip");
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<string>> PickExampleFilesAsync(string? initialFolder)
    {
        var options = new FilePickerOpenOptions
        {
            Title = "Add Example Images",
            AllowMultiple = true,
            FileTypeFilter = [ImageSourceFileType, FilePickerFileTypes.All],
            SuggestedStartLocation = await TryGetFolderAsync(initialFolder)
        };

        var files = await StorageProvider.OpenFilePickerAsync(options);
        return files.Select(file => file.TryGetLocalPath()).OfType<string>().ToList();
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<string>> PickAttachmentsAsync(string? initialFolder)
    {
        var options = new FilePickerOpenOptions
        {
            Title = "Attach Files",
            AllowMultiple = true,
            FileTypeFilter = [FilePickerFileTypes.All],
            SuggestedStartLocation = await TryGetFolderAsync(initialFolder)
        };

        var files = await StorageProvider.OpenFilePickerAsync(options);
        return files.Select(file => file.TryGetLocalPath()).OfType<string>().ToList();
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
    public async Task<ExportPreset?> PickExportPresetAsync(IReadOnlyList<ExportPreset> presets, ExportPreset? last)
    {
        ArgumentNullException.ThrowIfNull(presets);

        var viewModel = new ExportPresetDialogViewModel(presets, last);
        await new ExportPresetDialogView(viewModel).ShowDialog<bool>(this);
        return viewModel.Accepted ? viewModel.SelectedPreset : null;
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
    protected override void OnClosing(WindowClosingEventArgs e)
    {
        base.OnClosing(e);

        // The layout is saved as the window is closed: the size it has in its normal state, where it was, and whether it
        // was maximized
        if (_shell is not null && !e.Cancel)
        {
            var place = _normalPosition ?? (WindowState == WindowState.Normal ? Position : null);
            _shell.SaveWindow(Width, Height, place?.X, place?.Y, WindowState == WindowState.Maximized);
        }
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
    ///     Forgets the browser of the drawing knowledge when it is closed, so that the next request opens a new one.
    /// </summary>
    /// <param name="sender">The browser window.</param>
    /// <param name="e">The event data.</param>
    private void OnKnowledgeBrowserClosed(object? sender, EventArgs e)
    {
        if (sender is KnowledgeBrowserView browser)
        {
            browser.Closed -= OnKnowledgeBrowserClosed;
            if (ReferenceEquals(browser, _knowledgeBrowser))
            {
                _knowledgeBrowser = null;
            }
        }
    }

    /// <summary>
    ///     Forgets the window that manages style kits when it is closed, so that the next request opens a new one.
    /// </summary>
    /// <param name="sender">The window.</param>
    /// <param name="e">The event data.</param>
    private void OnStyleKitManagerClosed(object? sender, EventArgs e)
    {
        if (sender is StyleKitManagerView manager)
        {
            manager.Closed -= OnStyleKitManagerClosed;
            if (ReferenceEquals(manager, _styleKitManager))
            {
                _styleKitManager = null;
            }
        }
    }

    /// <summary>
    ///     Gives the window the size, place, and state that were saved, made to fit the screens that are connected now.
    /// </summary>
    /// <param name="saved">The saved layout.</param>
    private void ApplySavedLayout(WindowSettings saved)
    {
        var areas = Screens?.All.Select(screen => screen.WorkingArea).ToList() ?? [];
        var scaling = Screens?.Primary?.Scaling ?? 1;
        var placement = WindowPlacement.Resolve(saved, areas, scaling);
        Width = placement.Width;
        Height = placement.Height;
        if (placement.Position is { } position)
        {
            WindowStartupLocation = WindowStartupLocation.Manual;
            Position = position;
            _normalPosition = position;
        }

        WindowState = placement.State;
    }

    /// <summary>
    ///     Remembers where the window is while it is in its normal state, because the place that the system reports while
    ///     the window is maximized is not the place it returns to.
    /// </summary>
    /// <param name="sender">The window.</param>
    /// <param name="e">The event data.</param>
    private void OnWindowPositionChanged(object? sender, PixelPointEventArgs e)
    {
        if (WindowState == WindowState.Normal)
        {
            _normalPosition = e.Point;
        }
    }

    /// <summary>
    ///     Keeps the room free at the ends of the title bar that the buttons of the system need. The controls are the
    ///     generated fields of the XAML names (a name lookup throws while the window has no name scope, which is the
    ///     case during the load); a control that does not exist yet has nothing to update.
    /// </summary>
    private void UpdateCaptionReserve()
    {
        UpdateCaptionReserve(IsExtendedIntoWindowDecorations, OperatingSystem.IsWindows(), OperatingSystem.IsMacOS());
    }

    /// <summary>
    ///     Keeps the room free at the ends of the title bar for the facts of a platform; the facts are given so that a test
    ///     can say what a window on Windows would do, which the headless platform cannot be.
    /// </summary>
    /// <param name="isExtended">Whether the window extends into its frame.</param>
    /// <param name="isWindows">Whether the platform is Windows.</param>
    /// <param name="isMacOs">Whether the platform is macOS.</param>
    internal void UpdateCaptionReserve(bool isExtended, bool isWindows, bool isMacOs)
    {
        ApplyCaptionReserve(TitleBarLeadingReserve, TitleBarCaptionReserve, isExtended, isWindows, isMacOs);

        // The window draws its own buttons, whose size and spacing are the theme's, so the room is taken from where they
        // really are as soon as there is a layout; the constant is the fallback. The buttons are not on every platform
        // (macOS draws its own at the left), so only a platform that reserves room on the right is measured
        if (isExtended && CaptionReserve(true, isWindows, isMacOs).Right > 0
            && MeasureCaptionButtonsReserve(this) is { } measured)
        {
            TitleBarCaptionReserve.Width = measured;
        }
    }

    /// <summary>
    ///     Measures how much of the right end of the window the buttons that the window draws over its title bar cover,
    ///     from their left edge to the edge of the window, so that the pill next to them is never under a button.
    /// </summary>
    /// <remarks>
    ///     The drawn decorations are not part of the visual tree of the window: the host of the window holds the window
    ///     and, as siblings of it, the layers of the decorations. The buttons are therefore searched from the top of the
    ///     visual tree and not from the window, and their place is taken relative to the window.
    /// </remarks>
    /// <param name="window">The window.</param>
    /// <returns>The width in device-independent pixels, or <see langword="null" /> when the buttons are not laid out.</returns>
    internal static double? MeasureCaptionButtonsReserve(Control window)
    {
        Visual top = window;
        while (top.GetVisualParent() is { } parent)
        {
            top = parent;
        }

        var buttons = top.GetVisualDescendants()
            .OfType<StackPanel>()
            .FirstOrDefault(panel => panel.Name == CaptionButtonsPanelName && panel.IsEffectivelyVisible && panel.Bounds.Width > 0);
        return buttons?.TranslatePoint(new Point(0, 0), window) is { } origin
            ? Math.Max(0, window.Bounds.Width - origin.X)
            : null;
    }

    /// <summary>
    ///     Asks for the room that the window buttons need to be worked out again once the window has been laid out.
    /// </summary>
    private void RequestCaptionReserveUpdate()
    {
        if (_componentReady)
        {
            Dispatcher.UIThread.Post(UpdateCaptionReserve, DispatcherPriority.Loaded);
        }
    }

    /// <summary>
    ///     Sets the widths of the two title bar reserves; a reserve that is <see langword="null" /> (the control does not
    ///     exist yet) is left alone.
    /// </summary>
    /// <param name="leading">The reserve on the left, or <see langword="null" />.</param>
    /// <param name="trailing">The reserve on the right, or <see langword="null" />.</param>
    /// <param name="isExtended">Whether the window extends into its frame.</param>
    /// <param name="isWindows">Whether the platform is Windows.</param>
    /// <param name="isMacOs">Whether the platform is macOS.</param>
    internal static void ApplyCaptionReserve(Border? leading, Border? trailing, bool isExtended, bool isWindows, bool isMacOs)
    {
        var (left, right) = CaptionReserve(isExtended, isWindows, isMacOs);
        if (leading is not null)
        {
            leading.Width = left;
        }

        if (trailing is not null)
        {
            trailing.Width = right;
        }
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
