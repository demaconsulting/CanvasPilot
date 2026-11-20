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

using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Input.Platform;
using CommunityToolkit.Mvvm.Input;
using DemaConsulting.CanvasPilot.AppShellSubsystem;
using DemaConsulting.CanvasPilot.PreviewSubsystem;
using DemaConsulting.CanvasPilot.ProjectSubsystem;
using DemaConsulting.CanvasPilot.SettingsSubsystem;

namespace DemaConsulting.CanvasPilot.UiTests;

/// <summary>
///     Headless UI tests for the main window: its menus, the dialogs it opens, the title, and the dialog service it
///     implements.
/// </summary>
public sealed class MainWindowViewTests : IDisposable
{
    /// <summary>
    ///     Temporary root removed on disposal.
    /// </summary>
    private readonly string _root = Directory.CreateTempSubdirectory("canvaspilot-ui-tests-").FullName;

    /// <summary>
    ///     The compositions created by a test, disposed with the fixture so no watcher outlives it.
    /// </summary>
    private readonly List<ApplicationComposition> _compositions = [];

    /// <inheritdoc />
    public void Dispose()
    {
        foreach (var composition in _compositions)
        {
            composition.Dispose();
        }

        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    /// <summary>
    ///     Test that the main window opens headless and shows the shell title.
    /// </summary>
    [AvaloniaFact]
    public void MainWindowView_Show_OpensWithShellTitle()
    {
        // Arrange: the application composed on temporary folders
        var (composition, window) = CreateWindow();

        // Act: show the window headless
        window.Show();

        // Assert: the window is visible and bound to the shell title
        Assert.True(window.IsVisible);
        Assert.Equal("CanvasPilot", window.Title);
        Assert.Equal(composition.Shell.Title, window.Title);

        window.Close();
    }

    /// <summary>
    ///     Test that the menus have the expected structure, and that every menu item has a name and an automation
    ///     identifier that are the same.
    /// </summary>
    [AvaloniaFact]
    public void MainWindowView_Menus_HaveExpectedStructureAndAutomationIds()
    {
        // Arrange: a shown main window
        var (_, window) = CreateWindow();
        window.Show();

        // Act: read the menu tree
        var menu = SettingsUiHelpers.Find<Menu>(window, "MainMenu");
        var structure = menu.Items.OfType<MenuItem>()
            .Select(top => (Id: IdOf(top), Children: top.Items.OfType<MenuItem>().Select(IdOf).ToArray()))
            .ToArray();

        // Assert: File, Image, Tools, and Help hold the expected items in order
        Assert.Equal(["FileMenu", "ImageMenu", "ToolsMenu", "HelpMenu"], structure.Select(top => top.Id));
        Assert.Equal(
            ["NewProjectMenuItem", "OpenProjectMenuItem", "RecentProjectsMenuItem", "SaveProjectMenuItem", "CloseProjectMenuItem", "ExitMenuItem"],
            structure[0].Children);
        Assert.Equal(["ExportPngMenuItem", "CopyImageMenuItem"], structure[1].Children);
        Assert.Equal(["SettingsMenuItem"], structure[2].Children);
        Assert.Equal(["AboutMenuItem"], structure[3].Children);
        foreach (var item in structure.SelectMany(top => top.Children.Prepend(top.Id)))
        {
            var found = window.FindControl<MenuItem>(item);
            Assert.NotNull(found);
            Assert.Equal(item, IdOf(found));
        }

        Assert.Equal(
            ["_File", "_Image", "_Tools", "_Help"],
            menu.Items.OfType<MenuItem>().Select(top => top.Header as string));

        window.Close();
    }

    /// <summary>
    ///     Test that with no project open only the commands that need nothing are enabled.
    /// </summary>
    [AvaloniaFact]
    public void MainWindowView_Menus_NoProject_EnabledStateFollowsCommands()
    {
        // Arrange: a shown main window with no project and no exportable image
        var (_, window) = CreateWindow();
        window.Show();

        // Act & Assert: read which items are enabled
        foreach (var enabled in new[] { "NewProjectMenuItem", "OpenProjectMenuItem", "ExitMenuItem", "SettingsMenuItem", "AboutMenuItem" })
        {
            Assert.True(IsUsable(window.FindControl<MenuItem>(enabled)!), enabled);
        }

        foreach (var disabled in new[] { "RecentProjectsMenuItem", "SaveProjectMenuItem", "CloseProjectMenuItem", "ExportPngMenuItem", "CopyImageMenuItem" })
        {
            Assert.False(IsUsable(window.FindControl<MenuItem>(disabled)!), disabled);
        }

        window.Close();
    }

    /// <summary>
    ///     Test that the Exit menu item closes the window.
    /// </summary>
    [AvaloniaFact]
    public void MainWindowView_ExitMenuItem_ClosesWindow()
    {
        // Arrange: a shown main window
        var (_, window) = CreateWindow();
        window.Show();
        var closed = false;
        window.Closed += (_, _) => closed = true;

        // Act: run the command behind the Exit menu item
        window.FindControl<MenuItem>("ExitMenuItem")!.Command!.Execute(null);

        // Assert: the window closed
        Assert.True(closed);
        Assert.False(window.IsVisible);
    }

    /// <summary>
    ///     Test that the About menu item opens the About dialog with the application's identity and dependencies,
    ///     and that its Close button closes it.
    /// </summary>
    [AvaloniaFact]
    public async Task MainWindowView_AboutMenuItem_OpensAboutDialogAndCloseButtonCloses()
    {
        // Arrange: a shown main window
        var (_, window) = CreateWindow();
        window.Show();

        // Act: run About and look at the dialog
        var shown = ((IAsyncRelayCommand)window.FindControl<MenuItem>("AboutMenuItem")!.Command!).ExecuteAsync(null);
        var dialog = AppShellUiHelpers.FindDialog<AboutDialogView>(window);
        var name = SettingsUiHelpers.Find<TextBlock>(dialog, "AboutName").Text;
        var tagline = SettingsUiHelpers.Find<TextBlock>(dialog, "AboutTagline").Text;
        var version = SettingsUiHelpers.Find<TextBlock>(dialog, "AboutVersion").Text;
        var copyright = SettingsUiHelpers.Find<TextBlock>(dialog, "AboutCopyright").Text;
        var dependencies = SettingsUiHelpers.Find<ItemsControl>(dialog, "AboutDependencies").ItemCount;
        SettingsUiHelpers.Click(dialog, SettingsUiHelpers.Find<Button>(dialog, "AboutCloseButton"));
        await shown;

        // Assert: the dialog showed the identity and the four libraries, and Close ended the command
        Assert.Equal("CanvasPilot", name);
        Assert.Equal("AI-assisted drawing creation with CanvasNet", tagline);
        Assert.StartsWith("Version ", version, StringComparison.Ordinal);
        Assert.Equal("Copyright (c) DEMA Consulting", copyright);
        Assert.Equal(4, dependencies);
        Assert.Empty(window.OwnedWindows);

        window.Close();
    }

    /// <summary>
    ///     Test that the Settings menu item opens the settings dialog with the AI Provider and Speech pages, and that
    ///     Cancel closes it.
    /// </summary>
    [AvaloniaFact]
    public async Task MainWindowView_SettingsMenuItem_OpensSettingsDialogAndCancelCloses()
    {
        // Arrange: a shown main window
        var (_, window) = CreateWindow();
        window.Show();

        // Act: run Settings, look at the dialog, and cancel it
        var shown = ((IAsyncRelayCommand)window.FindControl<MenuItem>("SettingsMenuItem")!.Command!).ExecuteAsync(null);
        var dialog = AppShellUiHelpers.FindDialog<SettingsDialogView>(window);
        var list = SettingsUiHelpers.Find<ListBox>(dialog, "SettingsPageList");
        var titles = list.Items.OfType<ISettingsPage>().Select(page => page.Title).ToArray();
        SettingsUiHelpers.Click(dialog, SettingsUiHelpers.Find<Button>(dialog, "SettingsCancelButton"));
        await shown;

        // Assert: the dialog listed both pages, and Cancel closed it
        Assert.Equal(["AI Provider", "Speech"], titles);
        Assert.Empty(window.OwnedWindows);

        window.Close();
    }

    /// <summary>
    ///     Test that the New Project menu item opens the dialog, that Cancel closes it without creating a project,
    ///     and that choosing OK with a name creates the project, updates the title, and lists its files.
    /// </summary>
    [AvaloniaFact]
    public async Task MainWindowView_NewProjectMenuItem_CancelThenOk_CreatesProjectAndUpdatesTitle()
    {
        // Arrange: a shown main window
        var (_, window) = CreateWindow();
        window.Show();
        var command = (IAsyncRelayCommand)window.FindControl<MenuItem>("NewProjectMenuItem")!.Command!;

        // Act: open the dialog and cancel it; open it again, type a name, and choose OK
        var cancelled = command.ExecuteAsync(null);
        var first = AppShellUiHelpers.FindDialog<NewProjectDialogView>(window);
        SettingsUiHelpers.Click(first, SettingsUiHelpers.Find<Button>(first, "NewProjectCancelButton"));
        await cancelled;
        var titleAfterCancel = window.Title;
        var accepted = command.ExecuteAsync(null);
        var second = AppShellUiHelpers.FindDialog<NewProjectDialogView>(window);
        SettingsUiHelpers.Find<TextBox>(second, "NewProjectNameTextBox").Text = "Window Project";
        AppShellUiHelpers.Flush();
        SettingsUiHelpers.Click(second, SettingsUiHelpers.Find<Button>(second, "NewProjectOkButton"));
        await accepted;
        AppShellUiHelpers.Flush();

        // Assert: nothing was created by the cancel; the OK created the project, which titles the window and shows in
        // the explorer and the workspace
        Assert.Equal("CanvasPilot", titleAfterCancel);
        Assert.Equal("Window Project - CanvasPilot", window.Title);
        Assert.True(File.Exists(Path.Combine(_root, "projects", "Window Project", "Window Project.canvaspilot")));
        Assert.Equal("No file selected", SettingsUiHelpers.Find<TextBlock>(window, "WorkspacePlaceholder").Text);
        Assert.True(IsUsable(window.FindControl<MenuItem>("SaveProjectMenuItem")!));
        Assert.True(IsUsable(window.FindControl<MenuItem>("RecentProjectsMenuItem")!));
        Assert.Equal(1, window.FindControl<MenuItem>("RecentProjectsMenuItem")!.ItemCount);

        window.Close();
    }

    /// <summary>
    ///     Test that the explorer in the window lists the image files of a project opened from the Recent Projects
    ///     menu, and that Close Project empties the window again.
    /// </summary>
    [AvaloniaFact]
    public async Task MainWindowView_RecentProject_Open_ListsFilesAndCloseProjectResetsWindow()
    {
        // Arrange: a project with two images and a text file, remembered by an earlier run
        var projectFile = await CreateRememberedProjectAsync("Gallery", "b.svg", "a.svg", "notes.txt");
        var (composition, window) = CreateWindow();
        window.Show();
        var recentMenu = window.FindControl<MenuItem>("RecentProjectsMenuItem")!;

        // Act: open the project from the recent list, then close it
        var item = Assert.IsType<RecentProjectItem>(recentMenu.Items.Cast<object>().Single());
        await item.OpenCommand.ExecuteAsync(null);
        AppShellUiHelpers.Flush();
        var list = SettingsUiHelpers.Find<ListBox>(window, "ProjectExplorerList");
        var listed = list.Items.OfType<ProjectFileItem>().Select(file => file.Name).ToArray();
        var titleWhileOpen = window.Title;
        window.FindControl<MenuItem>("CloseProjectMenuItem")!.Command!.Execute(null);
        AppShellUiHelpers.Flush();

        // Assert: the window listed only the image files, and closing restored the empty state
        Assert.Equal(projectFile, item.FilePath);
        Assert.Equal("Gallery", item.Name);
        Assert.Equal(["a.svg", "b.svg"], listed);
        Assert.Equal("Gallery - CanvasPilot", titleWhileOpen);
        Assert.Equal("CanvasPilot", window.Title);
        Assert.Equal(0, list.ItemCount);
        Assert.Equal("No project open", SettingsUiHelpers.Find<TextBlock>(window, "WorkspacePlaceholder").Text);
        Assert.False(composition.Shell.HasProject);

        window.Close();
    }

    /// <summary>
    ///     Test that the Recent Projects submenu shows each project by name and that its item opens the project.
    /// </summary>
    [AvaloniaFact]
    public async Task MainWindowView_RecentProjectsSubmenu_ItemShowsNameAndOpensProject()
    {
        // Arrange: a window over a remembered project named with an underscore
        await CreateRememberedProjectAsync("my_gallery");
        var (composition, window) = CreateWindow();
        window.Show();

        // Act: open the File menu and its Recent Projects submenu, then run the item's command
        window.FindControl<MenuItem>("FileMenu")!.Open();
        var recentMenu = window.FindControl<MenuItem>("RecentProjectsMenuItem")!;
        recentMenu.Open();
        AppShellUiHelpers.Flush();
        var container = Assert.IsType<MenuItem>(recentMenu.ContainerFromIndex(0));
        var header = container.Header;
        await ((IAsyncRelayCommand)container.Command!).ExecuteAsync(null);

        // Assert: the item showed the name with the underscore escaped, and running it opened the project
        Assert.Equal("my__gallery", header);
        Assert.Equal("my_gallery - CanvasPilot", composition.Shell.Title);

        window.Close();
    }

    /// <summary>
    ///     Test that the placeholder follows the selected file, and that supplied preview content replaces it.
    /// </summary>
    [AvaloniaFact]
    public async Task MainWindowView_Workspace_SelectionAndPreview_ChangePlaceholderAndContent()
    {
        // Arrange: a window showing a project with one image; the composed preview pane is removed so the
        // workspace's own placeholder and preview slot can be checked on their own
        await CreateRememberedProjectAsync("Preview", "a.svg");
        var (composition, window) = CreateWindow();
        composition.Shell.Workspace.Preview = null;
        window.Show();
        await composition.Shell.RecentProjects[0].OpenCommand.ExecuteAsync(null);
        AppShellUiHelpers.Flush();
        var placeholder = SettingsUiHelpers.Find<TextBlock>(window, "WorkspacePlaceholder");
        var withoutSelection = placeholder.Text;

        // Act: select the file, then supply preview content
        SettingsUiHelpers.Find<ListBox>(window, "ProjectExplorerList").SelectedIndex = 0;
        AppShellUiHelpers.Flush();
        var withSelection = placeholder.Text;
        composition.Shell.Workspace.Preview = "Rendered picture";
        AppShellUiHelpers.Flush();

        // Assert: the text followed the selection, and the preview host replaced the placeholder
        Assert.Equal("No file selected", withoutSelection);
        Assert.Equal("No preview available", withSelection);
        Assert.False(placeholder.IsEffectivelyVisible);
        Assert.True(SettingsUiHelpers.Find<ContentControl>(window, "WorkspacePreviewHost").IsEffectivelyVisible);

        window.Close();
    }

    /// <summary>
    ///     Test that selecting an image in the open project shows its rendered picture in the workspace's preview
    ///     pane and enables the Image menu items, and that closing the project disables them again.
    /// </summary>
    [AvaloniaFact]
    public async Task MainWindowView_SelectSvg_ShowsPreviewAndEnablesImageMenu()
    {
        // Arrange: a window showing a project that holds a valid SVG
        await CreateRememberedProjectAsync("Preview", "a.svg");
        var (composition, window) = CreateWindow();
        window.Show();
        await composition.Shell.RecentProjects[0].OpenCommand.ExecuteAsync(null);
        var folder = composition.Shell.CurrentProject!.FolderPath;
        await File.WriteAllTextAsync(
            Path.Combine(folder, "a.svg"),
            "<svg xmlns='http://www.w3.org/2000/svg' width='30' height='12'><rect width='30' height='12' fill='#0000ff'/></svg>",
            TestContext.Current.CancellationToken);
        composition.Shell.Workspace.Explorer.Refresh();
        var export = window.FindControl<MenuItem>("ExportPngMenuItem")!;
        var copy = window.FindControl<MenuItem>("CopyImageMenuItem")!;
        var disabledBefore = !IsUsable(export) && !IsUsable(copy);

        // Act: select the file in the explorer and wait for the render
        SettingsUiHelpers.Find<ListBox>(window, "ProjectExplorerList").SelectedIndex = 0;
        await composition.Preview.RenderCompletion;
        AppShellUiHelpers.Flush();
        var enabledWithPicture = IsUsable(export) && IsUsable(copy);
        var bitmap = SettingsUiHelpers.Find<Image>(window, "PreviewImage").Source;
        var shownSize = Assert.IsType<Avalonia.Media.Imaging.WriteableBitmap>(bitmap).PixelSize;
        composition.Shell.CloseProjectCommand.Execute(null);
        AppShellUiHelpers.Flush();

        // Assert: the picture was shown at its size in the preview pane inside the workspace and the menu followed it
        Assert.True(disabledBefore);
        Assert.True(enabledWithPicture);
        Assert.Equal(new Avalonia.PixelSize(30, 12), shownSize);
        Assert.False(IsUsable(export));
        Assert.False(IsUsable(copy));

        window.Close();
    }

    /// <summary>
    ///     Test that copying a picture to the clipboard reports whether the platform clipboard took it, and that it
    ///     is readable as a bitmap when it did.
    /// </summary>
    [AvaloniaFact]
    public async Task MainWindowView_CopyImageToClipboard_Picture_ReportsResultAndIsReadable()
    {
        // Arrange: a shown main window and a 2 x 2 picture
        var (_, window) = CreateWindow();
        window.Show();
        var pixels = new byte[2 * 2 * 4];
        Array.Fill(pixels, (byte)255);
        var image = new RenderedImage("a.svg", "svg", 2, 2, pixels);

        // Act: copy it to the clipboard of the window
        var copied = await window.CopyImageToClipboardAsync(image);
        var clipboard = window.Clipboard;
        var readBack = copied && clipboard is not null ? await clipboard.TryGetBitmapAsync() : null;

        // Assert: the result matches whether the platform has a clipboard, and a copied picture can be read back
        Assert.Equal(clipboard is not null, copied);
        Assert.Equal(copied, readBack is not null);
        Assert.Equal(copied ? new Avalonia.PixelSize(2, 2) : default, readBack?.PixelSize ?? default);

        window.Close();
    }

    /// <summary>
    ///     Test that the Image menu items become enabled when an exporter reports an image, and run the exporter.
    /// </summary>
    [AvaloniaFact]
    public void MainWindowView_ImageMenu_ExporterEnables_ItemsEnabledAndRunExporter()
    {
        // Arrange: a window over an exporter with no image
        var exporter = new SwitchableImageExporter();
        var (_, window) = CreateWindow(exporter);
        window.Show();
        var export = window.FindControl<MenuItem>("ExportPngMenuItem")!;
        var copy = window.FindControl<MenuItem>("CopyImageMenuItem")!;
        var disabledBefore = !IsUsable(export) && !IsUsable(copy);

        // Act: an image becomes available and both items are run
        exporter.CanExport = true;
        AppShellUiHelpers.Flush();
        export.Command!.Execute(null);
        copy.Command!.Execute(null);

        // Assert: the items were disabled, became enabled, and reached the exporter
        Assert.True(disabledBefore);
        Assert.True(IsUsable(export));
        Assert.True(IsUsable(copy));
        Assert.Equal(1, exporter.Exports);
        Assert.Equal(1, exporter.Copies);

        window.Close();
    }

    /// <summary>
    ///     Test that the window's message dialog shows its text and closes with OK.
    /// </summary>
    [AvaloniaFact]
    public async Task MainWindowView_ShowMessage_OkButtonClosesDialog()
    {
        // Arrange: a shown main window
        var (_, window) = CreateWindow();
        window.Show();

        // Act: show a message and choose OK
        var shown = window.ShowMessageAsync("Open Project", "The file is damaged.");
        var dialog = AppShellUiHelpers.FindDialog<MessageDialogView>(window);
        var text = SettingsUiHelpers.Find<TextBlock>(dialog, "MessageDialogText").Text;
        var input = SettingsUiHelpers.Find<TextBox>(dialog, "MessageDialogInput").IsVisible;
        var cancel = SettingsUiHelpers.Find<Button>(dialog, "MessageDialogCancelButton").IsVisible;
        SettingsUiHelpers.Click(dialog, SettingsUiHelpers.Find<Button>(dialog, "MessageDialogOkButton"));
        await shown;

        // Assert: the message was shown without a text entry or Cancel, and OK closed it
        Assert.Equal("Open Project", dialog.Title);
        Assert.Equal("The file is damaged.", text);
        Assert.False(input);
        Assert.False(cancel);
        Assert.Empty(window.OwnedWindows);

        window.Close();
    }

    /// <summary>
    ///     Test that a confirmation returns true for the confirm button and false for Cancel.
    /// </summary>
    [AvaloniaFact]
    public async Task MainWindowView_Confirm_ConfirmAndCancelButtons_ReturnAnswer()
    {
        // Arrange: a shown main window
        var (_, window) = CreateWindow();
        window.Show();

        // Act: ask twice, canceling the first and confirming the second
        var first = window.ConfirmAsync("Delete File", "Delete 'a.svg'?", "Delete");
        var firstDialog = AppShellUiHelpers.FindDialog<MessageDialogView>(window);
        SettingsUiHelpers.Click(firstDialog, SettingsUiHelpers.Find<Button>(firstDialog, "MessageDialogCancelButton"));
        var cancelled = await first;
        var second = window.ConfirmAsync("Delete File", "Delete 'a.svg'?", "Delete");
        var secondDialog = AppShellUiHelpers.FindDialog<MessageDialogView>(window);
        var confirmLabel = SettingsUiHelpers.Find<Button>(secondDialog, "MessageDialogOkButton").Content;
        SettingsUiHelpers.Click(secondDialog, SettingsUiHelpers.Find<Button>(secondDialog, "MessageDialogOkButton"));
        var confirmed = await second;

        // Assert: Cancel answered false, and the confirm button, labeled as asked, answered true
        Assert.False(cancelled);
        Assert.True(confirmed);
        Assert.Equal("Delete", confirmLabel);

        window.Close();
    }

    /// <summary>
    ///     Test that a prompt returns the trimmed text typed by the user, and null when cancelled.
    /// </summary>
    [AvaloniaFact]
    public async Task MainWindowView_Prompt_TypedTextAndCancel_ReturnTrimmedTextOrNull()
    {
        // Arrange: a shown main window
        var (_, window) = CreateWindow();
        window.Show();

        // Act: prompt twice, typing a padded name and choosing OK, then canceling
        var first = window.PromptAsync("Rename File", "New name:", "a.svg", "Rename");
        var firstDialog = AppShellUiHelpers.FindDialog<MessageDialogView>(window);
        var initial = SettingsUiHelpers.Find<TextBox>(firstDialog, "MessageDialogInput").Text;
        SettingsUiHelpers.Find<TextBox>(firstDialog, "MessageDialogInput").Text = "  cover.svg ";
        AppShellUiHelpers.Flush();
        SettingsUiHelpers.Click(firstDialog, SettingsUiHelpers.Find<Button>(firstDialog, "MessageDialogOkButton"));
        var typed = await first;
        var second = window.PromptAsync("Rename File", "New name:", "a.svg", "Rename");
        var secondDialog = AppShellUiHelpers.FindDialog<MessageDialogView>(window);
        SettingsUiHelpers.Click(secondDialog, SettingsUiHelpers.Find<Button>(secondDialog, "MessageDialogCancelButton"));
        var cancelled = await second;

        // Assert: the prompt started with the current name, returned the trimmed entry, and returned null on Cancel
        Assert.Equal("a.svg", initial);
        Assert.Equal("cover.svg", typed);
        Assert.Null(cancelled);

        window.Close();
    }

    /// <summary>
    ///     Test that deleting the selected file through the workspace asks the user in a dialog and removes the file
    ///     from the project folder and the list.
    /// </summary>
    [AvaloniaFact]
    public async Task MainWindowView_DeleteSelectedFile_ConfirmDialog_RemovesFile()
    {
        // Arrange: a window showing a project with a selected file
        var projectFile = await CreateRememberedProjectAsync("Cleanup", "a.svg", "b.svg");
        var folder = Path.GetDirectoryName(projectFile)!;
        var (composition, window) = CreateWindow();
        window.Show();
        await composition.Shell.RecentProjects[0].OpenCommand.ExecuteAsync(null);
        AppShellUiHelpers.Flush();
        SettingsUiHelpers.Find<ListBox>(window, "ProjectExplorerList").SelectedIndex = 0;
        AppShellUiHelpers.Flush();

        // Act: run Delete and confirm in the dialog
        var deleting = composition.Shell.Workspace.DeleteSelectedCommand.ExecuteAsync(null);
        var dialog = AppShellUiHelpers.FindDialog<MessageDialogView>(window);
        var question = SettingsUiHelpers.Find<TextBlock>(dialog, "MessageDialogText").Text;
        SettingsUiHelpers.Click(dialog, SettingsUiHelpers.Find<Button>(dialog, "MessageDialogOkButton"));
        await deleting;
        AppShellUiHelpers.Flush();

        // Assert: the dialog named the file, and the file is gone from disk and from the list
        Assert.Contains("a.svg", question, StringComparison.Ordinal);
        Assert.False(File.Exists(Path.Combine(folder, "a.svg")));
        Assert.True(File.Exists(Path.Combine(folder, "b.svg")));
        Assert.Equal(1, SettingsUiHelpers.Find<ListBox>(window, "ProjectExplorerList").ItemCount);

        window.Close();
    }

    /// <summary>
    ///     Test that the platform pickers, which are not available headless, answer that the user cancelled instead
    ///     of failing.
    /// </summary>
    [AvaloniaFact]
    public async Task MainWindowView_Pickers_HeadlessPlatform_ReturnNull()
    {
        // Arrange: a shown main window on the headless platform
        var (_, window) = CreateWindow();
        window.Show();

        // Act: ask for a folder, a project file, and a save location
        var folder = await window.PickFolderAsync(_root);
        var file = await window.PickProjectFileAsync(null);
        var save = await window.PickSaveFileAsync("Export", "picture.png", "PNG image", "*.png");

        // Assert: none returned a path
        Assert.Null(folder);
        Assert.Null(file);
        Assert.Null(save);

        window.Close();
    }

    /// <summary>
    ///     Tells whether a menu item can be used: it is enabled and its command, if any, can run. A closed submenu's items
    ///     are not attached to a visual tree, so their effective enabled state is not maintained until it is opened.
    /// </summary>
    /// <param name="item">The menu item.</param>
    /// <returns><see langword="true" /> when the item can be used.</returns>
    private static bool IsUsable(MenuItem item)
    {
        return item.IsEnabled && (item.Command?.CanExecute(item.CommandParameter) ?? true);
    }

    /// <summary>
    ///     Reads the automation identifier of a control.
    /// </summary>
    /// <param name="control">The control.</param>
    /// <returns>The identifier, or an empty string when there is none.</returns>
    private static string IdOf(Control control)
    {
        return AutomationProperties.GetAutomationId(control) ?? string.Empty;
    }

    /// <summary>
    ///     Creates the application on the temporary root and a window over its shell.
    /// </summary>
    /// <param name="exporter">What the Image menu exports, or <see langword="null" /> for none.</param>
    /// <returns>The composition and the window.</returns>
    private (ApplicationComposition Composition, MainWindowView Window) CreateWindow(IImageExporter? exporter = null)
    {
        var composition = AppShellUiHelpers.Compose(_root, exporter);
        _compositions.Add(composition);
        return (composition, new MainWindowView(composition.Shell));
    }

    /// <summary>
    ///     Creates a project with the given files and remembers it in the recent projects, as an earlier run would.
    /// </summary>
    /// <param name="name">The project name.</param>
    /// <param name="files">The names of the files to create in the project folder.</param>
    /// <returns>The full path of the project file.</returns>
    private async Task<string> CreateRememberedProjectAsync(string name, params string[] files)
    {
        var token = TestContext.Current.CancellationToken;
        var projects = Directory.CreateDirectory(Path.Combine(_root, "projects")).FullName;
        var created = await new ProjectService().CreateAsync(projects, name, token);
        foreach (var file in files)
        {
            await File.WriteAllTextAsync(Path.Combine(created.Project!.FolderPath, file), "<svg/>", token);
        }

        new RecentProjects(new SettingsService(Path.Combine(_root, "settings"))).Add(created.Project!.FilePath);
        return created.Project.FilePath;
    }
}
