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
using DemaConsulting.CanvasPilot.ComposerSubsystem;
using DemaConsulting.CanvasPilot.KnowledgeSubsystem;
using DemaConsulting.CanvasPilot.LoggingSubsystem;
using DemaConsulting.CanvasPilot.PreviewSubsystem;
using DemaConsulting.CanvasPilot.ProjectSubsystem;
using DemaConsulting.CanvasPilot.SettingsSubsystem;
using DemaConsulting.CanvasPilot.StyleSubsystem;
using NSubstitute;

namespace DemaConsulting.CanvasPilot.Tests;

/// <summary>
///     Unit tests for the MainWindowShell unit: the shell view model, its dialogs, and the image exporter seam.
///     Most tests use a real project service, recent projects list, and settings service on temporary folders and a
///     hand-written dialog service.
/// </summary>
public sealed class MainWindowShellTests : IDisposable
{
    /// <summary>
    ///     Temporary root created fresh for each test and removed on disposal.
    /// </summary>
    private readonly string _tempRoot = Directory.CreateTempSubdirectory("canvaspilot-tests-").FullName;

    /// <summary>
    ///     The shells created by a test, disposed with the fixture so no watcher outlives it.
    /// </summary>
    private readonly List<MainWindowShell> _shells = [];

    /// <summary>
    ///     The dialog service used by the shells.
    /// </summary>
    private readonly FakeDialogService _dialogs = new();

    /// <summary>
    ///     The folder that holds the projects the tests create.
    /// </summary>
    private string Projects => Path.Combine(_tempRoot, "projects");

    /// <inheritdoc />
    public void Dispose()
    {
        foreach (var shell in _shells)
        {
            shell.Dispose();
        }

        if (Directory.Exists(_tempRoot))
        {
            Directory.Delete(_tempRoot, recursive: true);
        }
    }

    /// <summary>
    ///     Test that a new shell with no project shows the application name and disables the commands that need a
    ///     project or an image.
    /// </summary>
    [Fact]
    public void MainWindowShell_New_NoProject_TitleIsApplicationNameAndProjectCommandsDisabled()
    {
        // Arrange & Act: create a shell over real services with no project open
        var shell = CreateShell();

        // Assert: the title is the application name and only the commands that need nothing can run
        Assert.Equal("CanvasPilot", shell.Title);
        Assert.False(shell.HasProject);
        Assert.Null(shell.CurrentProject);
        Assert.False(shell.HasRecentProjects);
        Assert.Empty(shell.RecentProjects);
        Assert.False(shell.SaveProjectCommand.CanExecute(null));
        Assert.False(shell.CloseProjectCommand.CanExecute(null));
        Assert.False(shell.ExportPngCommand.CanExecute(null));
        Assert.False(shell.CopyImageCommand.CanExecute(null));
        Assert.True(shell.NewProjectCommand.CanExecute(null));
        Assert.True(shell.OpenProjectCommand.CanExecute(null));
        Assert.True(shell.OpenSettingsCommand.CanExecute(null));
        Assert.True(shell.ShowAboutCommand.CanExecute(null));
    }

    /// <summary>
    ///     Test that the shell refuses missing collaborators.
    /// </summary>
    [Fact]
    public void MainWindowShell_Constructor_NullArguments_Throw()
    {
        // Arrange: valid collaborators
        var parts = CreateParts();

        // Act & Assert: each missing collaborator is refused
        Assert.Throws<ArgumentNullException>(() => new MainWindowShell(null!, parts.Recent, parts.Settings, parts.Workspace, []));
        Assert.Throws<ArgumentNullException>(() => new MainWindowShell(parts.Projects, null!, parts.Settings, parts.Workspace, []));
        Assert.Throws<ArgumentNullException>(() => new MainWindowShell(parts.Projects, parts.Recent, null!, parts.Workspace, []));
        Assert.Throws<ArgumentNullException>(() => new MainWindowShell(parts.Projects, parts.Recent, parts.Settings, null!, []));
        Assert.Throws<ArgumentNullException>(() => new MainWindowShell(parts.Projects, parts.Recent, parts.Settings, parts.Workspace, null!));
        Assert.Throws<ArgumentNullException>(() => parts.Workspace.AttachDialogService(null!));
        parts.Workspace.Dispose();
    }

    /// <summary>
    ///     Test that accepting the New Project dialog creates the project, shows its name in the title, adds it to
    ///     the recent projects, and shows it in the explorer.
    /// </summary>
    [Fact]
    public async Task MainWindowShell_NewProject_DialogAccepted_CreatesAndShowsProject()
    {
        // Arrange: a shell whose user enters a name in the dialog and chooses OK
        var shell = CreateShell(defaultProjectsFolder: Directory.CreateDirectory(Projects).FullName);
        string? startFolder = null;
        _dialogs.OnNewProject = dialog =>
        {
            startFolder = dialog.ParentFolder;
            dialog.ProjectName = "Launch Deck";
            dialog.OkCommand.Execute(null);
        };

        // Act: run New Project
        await shell.NewProjectCommand.ExecuteAsync(null);

        // Assert: the project exists, is current, titled, remembered, and shown in the explorer
        var projectFile = Path.Combine(Projects, "Launch Deck", "Launch Deck.canvaspilot");
        Assert.True(File.Exists(projectFile));
        Assert.Equal("Launch Deck - CanvasPilot", shell.Title);
        Assert.True(shell.HasProject);
        Assert.Equal("Launch Deck", shell.CurrentProject!.Name);
        Assert.Equal(Projects, startFolder);
        Assert.Equal(Path.Combine(Projects, "Launch Deck"), shell.Workspace.Explorer.FolderPath);
        Assert.Equal([projectFile], shell.RecentProjects.Select(item => item.FilePath));
        Assert.Equal("Launch Deck", shell.RecentProjects[0].Name);
        Assert.True(shell.HasRecentProjects);
        Assert.True(shell.SaveProjectCommand.CanExecute(null));
        Assert.True(shell.CloseProjectCommand.CanExecute(null));
        Assert.Empty(_dialogs.Messages);
    }

    /// <summary>
    ///     Test that a new project is given the default style kit by the style picker, that a project that is only opened is
    ///     not, and that the status bar follows the project.
    /// </summary>
    [Fact]
    public async Task MainWindowShell_NewProject_WithStylePicker_AppliesTheDefaultOnlyForANewProject()
    {
        // Arrange: a shell with a style picker that counts how often it is asked to give the default
        var applied = 0;
        var parts = CreateParts();
        var catalog = Substitute.For<IStyleKitCatalog>();
        catalog.Entries.Returns([]);
        var picker = new StylePickerViewModel(new FakeActiveStyle(), catalog, parts.Projects, () =>
        {
            applied++;
            return Task.CompletedTask;
        });
        var shell = new MainWindowShell(
            parts.Projects, parts.Recent, parts.Settings, parts.Workspace, [], null, Directory.CreateDirectory(Projects).FullName, null, picker);
        shell.AttachDialogService(_dialogs);
        _shells.Add(shell);
        var pickerWithoutProject = picker.IsVisible;
        _dialogs.OnNewProject = dialog =>
        {
            dialog.ProjectName = "Styled";
            dialog.OkCommand.Execute(null);
        };

        // Act: create a project, close it, and open it again from the list of recent projects
        await shell.NewProjectCommand.ExecuteAsync(null);
        var appliedForNew = applied;
        var pickerWithProject = picker.IsVisible;
        shell.CloseProjectCommand.Execute(null);
        await shell.RecentProjects[0].OpenCommand.ExecuteAsync(null);

        // Assert: the default was given once, for the new project, and the picker was shown with the project
        Assert.Same(picker, shell.StylePicker);
        Assert.False(pickerWithoutProject);
        Assert.Equal(1, appliedForNew);
        Assert.Equal(1, applied);
        Assert.True(pickerWithProject);
    }

    /// <summary>
    ///     Test that a shell with no style picker has none and creates a project as before.
    /// </summary>
    [Fact]
    public async Task MainWindowShell_NewProject_WithoutStylePicker_HasNoPicker()
    {
        // Arrange: a shell with no style picker
        var shell = CreateShell(defaultProjectsFolder: Directory.CreateDirectory(Projects).FullName);
        _dialogs.OnNewProject = dialog =>
        {
            dialog.ProjectName = "Plain";
            dialog.OkCommand.Execute(null);
        };

        // Act: create a project
        await shell.NewProjectCommand.ExecuteAsync(null);

        // Assert: the project is open and there is no picker
        Assert.True(shell.HasProject);
        Assert.Null(shell.StylePicker);
    }

    /// <summary>
    ///     Test that the Check Against Style command is available only while there is a picture to check, follows it, runs the
    ///     check, and tells the user why when nothing was checked.
    /// </summary>
    [Fact]
    public async Task MainWindowShell_CheckAgainstStyle_FollowsThePictureRunsTheCheckAndReportsWhyNot()
    {
        // Arrange: a shell over a check that has no picture at first and answers as the test says
        var check = Substitute.For<ICheckAgainstStyle>();
        check.CanCheck.Returns(false);
        check.RunAsync().Returns(Task.FromResult<string?>(null));
        var parts = CreateParts();
        var shell = new MainWindowShell(
            parts.Projects, parts.Recent, parts.Settings, parts.Workspace, [], null, null, null, null, check);
        shell.AttachDialogService(_dialogs);
        _shells.Add(shell);
        var changes = 0;
        shell.CheckAgainstStyleCommand.CanExecuteChanged += (_, _) => changes++;
        var disabledBefore = !shell.CheckAgainstStyleCommand.CanExecute(null);

        // Act: a picture appears, the check runs and succeeds, then it runs and has nothing to check
        check.CanCheck.Returns(true);
        check.CanCheckChanged += Raise.Event<EventHandler>(check, EventArgs.Empty);
        var enabledWithPicture = shell.CheckAgainstStyleCommand.CanExecute(null);
        await shell.CheckAgainstStyleCommand.ExecuteAsync(null);
        var messagesAfterSuccess = _dialogs.Messages.Count;
        check.RunAsync().Returns(Task.FromResult<string?>("This project has no style kit."));
        await shell.CheckAgainstStyleCommand.ExecuteAsync(null);

        // Assert: the command followed the picture, ran the check twice, and showed a message only for the refusal
        Assert.True(disabledBefore);
        Assert.True(enabledWithPicture);
        Assert.True(changes >= 1);
        Assert.Equal(0, messagesAfterSuccess);
        await check.Received(2).RunAsync();
        Assert.Equal([("Check Against Style", "This project has no style kit.")], _dialogs.Messages);
    }

    /// <summary>
    ///     Test that a shell with nothing that can check a picture leaves the Check Against Style command unavailable.
    /// </summary>
    [Fact]
    public async Task MainWindowShell_CheckAgainstStyle_NothingToCheckWith_IsUnavailableAndDoesNothing()
    {
        // Arrange: a shell with no check
        var shell = CreateShell();

        // Act
        var canExecute = shell.CheckAgainstStyleCommand.CanExecute(null);
        await shell.CheckAgainstStyleCommand.ExecuteAsync(null);

        // Assert
        Assert.False(canExecute);
        Assert.Empty(_dialogs.Messages);
    }

    /// <summary>
    ///     Test that canceling the New Project dialog creates nothing.
    /// </summary>
    [Fact]
    public async Task MainWindowShell_NewProject_DialogCancelled_CreatesNothing()
    {
        // Arrange: a shell whose user cancels the dialog
        var shell = CreateShell(defaultProjectsFolder: Directory.CreateDirectory(Projects).FullName);

        // Act: run New Project
        await shell.NewProjectCommand.ExecuteAsync(null);

        // Assert: the dialog was shown, no project exists, and the title is unchanged
        Assert.Equal(1, _dialogs.NewProjectCount);
        Assert.False(shell.HasProject);
        Assert.Equal("CanvasPilot", shell.Title);
        Assert.Empty(Directory.GetFileSystemEntries(Projects));
    }

    /// <summary>
    ///     Test that a project that cannot be created is reported to the user and changes nothing.
    /// </summary>
    [Fact]
    public async Task MainWindowShell_NewProject_CreateFails_ShowsMessage()
    {
        // Arrange: a project service that refuses to create the accepted project
        var projects = Substitute.For<IProjectService>();
        projects.CreateAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(ProjectResult.Failure("The folder is read-only."));
        var shell = CreateShell(projects);
        _dialogs.OnNewProject = dialog =>
        {
            dialog.ProjectName = "Poster";
            dialog.ParentFolder = Projects;
            dialog.OkCommand.Execute(null);
        };

        // Act: run New Project; the substitute service accepts the entries and then fails to create the project
        await shell.NewProjectCommand.ExecuteAsync(null);

        // Assert: the problem was shown with the dialog title and no project is open
        Assert.Equal([("New Project", "The folder is read-only.")], _dialogs.Messages);
        Assert.False(shell.HasProject);
    }

    /// <summary>
    ///     Test that picking a project file opens the project, shows it, and remembers it.
    /// </summary>
    [Fact]
    public async Task MainWindowShell_OpenProject_FileChosen_OpensAndRemembersProject()
    {
        // Arrange: a project on disk and a shell whose user picks its file
        var file = await CreateProjectFileAsync("Brochure");
        var shell = CreateShell();
        _dialogs.ProjectFileToPick = file;

        // Act: run Open Project
        await shell.OpenProjectCommand.ExecuteAsync(null);

        // Assert: the project is open, titled, shown, and remembered
        Assert.Equal("Brochure - CanvasPilot", shell.Title);
        Assert.Equal(Path.GetDirectoryName(file), shell.Workspace.Explorer.FolderPath);
        Assert.Equal([file], shell.RecentProjects.Select(item => item.FilePath));
    }

    /// <summary>
    ///     Test that canceling the file picker opens nothing.
    /// </summary>
    [Fact]
    public async Task MainWindowShell_OpenProject_PickerCancelled_OpensNothing()
    {
        // Arrange: a shell whose user cancels the picker
        var shell = CreateShell();

        // Act: run Open Project
        await shell.OpenProjectCommand.ExecuteAsync(null);

        // Assert: the picker was shown and nothing changed
        Assert.Equal(1, _dialogs.ProjectPickCount);
        Assert.False(shell.HasProject);
        Assert.Empty(_dialogs.Messages);
    }

    /// <summary>
    ///     Test that a damaged project file is reported to the user and no project opens.
    /// </summary>
    [Fact]
    public async Task MainWindowShell_OpenProject_DamagedFile_ShowsMessageAndOpensNothing()
    {
        // Arrange: a damaged project file and a shell whose user picks it
        Directory.CreateDirectory(Projects);
        var file = Path.Combine(Projects, "Damaged.canvaspilot");
        await File.WriteAllTextAsync(file, "not json", TestContext.Current.CancellationToken);
        var shell = CreateShell();
        _dialogs.ProjectFileToPick = file;

        // Act: run Open Project
        await shell.OpenProjectCommand.ExecuteAsync(null);

        // Assert: the problem was shown, no project is open, and the damaged file is not remembered
        var message = Assert.Single(_dialogs.Messages);
        Assert.Equal("Open Project", message.Title);
        Assert.False(string.IsNullOrWhiteSpace(message.Message));
        Assert.False(shell.HasProject);
        Assert.Empty(shell.RecentProjects);
    }

    /// <summary>
    ///     Test that the Recent Projects menu lists remembered projects most recent first, and that choosing one
    ///     opens it.
    /// </summary>
    [Fact]
    public async Task MainWindowShell_RecentProjects_ListedMostRecentFirstAndOpenOnChoice()
    {
        // Arrange: two remembered projects, then a restarted shell
        var first = await CreateProjectFileAsync("First");
        var second = await CreateProjectFileAsync("Second");
        var settingsFolder = Path.Combine(_tempRoot, "settings");
        var settings = new SettingsService(settingsFolder);
        var recent = new RecentProjects(settings);
        recent.Add(first);
        recent.Add(second);
        var shell = CreateShell(settings: settings, recent: recent);
        var before = shell.RecentProjects.Select(item => item.FilePath).ToArray();

        // Act: choose the older project in the menu
        var older = shell.RecentProjects[1];
        await older.OpenCommand.ExecuteAsync(null);

        // Assert: the menu listed newest first, and the chosen project opened and moved to the top
        Assert.Equal([second, first], before);
        Assert.Equal("First - CanvasPilot", shell.Title);
        Assert.Equal([first, second], shell.RecentProjects.Select(item => item.FilePath));
    }

    /// <summary>
    ///     Test that the picture that the user shows in the open project is remembered for that project, relative to its
    ///     folder, and that selecting a folder or closing the project does not forget it.
    /// </summary>
    [Fact]
    public async Task MainWindowShell_SelectedFile_ShownPicture_IsRememberedForTheProject()
    {
        // Arrange: an open project with a picture in a subfolder
        var file = await CreateProjectFileAsync("Remember");
        var picture = WritePicture(file, Path.Combine("diagrams", "flow.svg"));
        var settings = new SettingsService(Path.Combine(_tempRoot, "settings"));
        var shell = CreateShell(settings: settings);
        _dialogs.ProjectFileToPick = file;
        await shell.OpenProjectCommand.ExecuteAsync(null);

        // Act: show the picture, then select its folder, then close the project
        shell.Workspace.Explorer.SelectFile(picture);
        var remembered = settings.Current.RecentProjectPicture[file];
        shell.Workspace.Explorer.SelectedNode = shell.Workspace.Explorer.FindNode(Path.GetDirectoryName(picture)!);
        var afterFolder = settings.Current.RecentProjectPicture[file];
        shell.CloseProjectCommand.Execute(null);

        // Assert: the relative path was remembered and kept
        Assert.Equal(Path.Combine("diagrams", "flow.svg"), remembered);
        Assert.Null(shell.Workspace.SelectedFile);
        Assert.Equal(remembered, afterFolder);
        Assert.Equal(remembered, settings.Current.RecentProjectPicture[file]);
    }

    /// <summary>
    ///     Test that File, Open Project selects and shows the picture that was last shown in the project.
    /// </summary>
    [Fact]
    public async Task MainWindowShell_OpenProject_RememberedPicture_IsSelected()
    {
        // Arrange: a remembered project with two pictures, the second of which was last shown
        var file = await CreateProjectFileAsync("Reopen");
        WritePicture(file, "a.svg");
        var second = WritePicture(file, "b.svg");
        var settings = new SettingsService(Path.Combine(_tempRoot, "settings"));
        var recent = new RecentProjects(settings);
        recent.Add(file);
        recent.RememberShownPicture(file, "b.svg");
        var shell = CreateShell(settings: settings, recent: recent);
        _dialogs.ProjectFileToPick = file;

        // Act: open the project with File, Open Project
        await shell.OpenProjectCommand.ExecuteAsync(null);

        // Assert: the picture is selected
        Assert.True(shell.HasProject);
        Assert.Equal(second, shell.Workspace.SelectedFile);
    }

    /// <summary>
    ///     Test that choosing a project in the list of recent projects, as on the welcome screen and in the Recent Projects
    ///     menu, selects the picture that was last shown in it, also after a restart.
    /// </summary>
    [Fact]
    public async Task MainWindowShell_RecentProjects_OpenOnChoice_SelectsTheRememberedPicture()
    {
        // Arrange: a project whose picture was shown in an earlier run, and a restarted shell
        var file = await CreateProjectFileAsync("Recent");
        var picture = WritePicture(file, "flow.svg");
        WritePicture(file, "other.svg");
        var settingsFolder = Path.Combine(_tempRoot, "settings");
        var earlier = new RecentProjects(new SettingsService(settingsFolder));
        earlier.Add(file);
        earlier.RememberShownPicture(file, "flow.svg");
        var settings = new SettingsService(settingsFolder);
        settings.Load();
        var shell = CreateShell(settings: settings, recent: new RecentProjects(settings));

        // Act: choose the project in the list
        await shell.RecentProjects[0].OpenCommand.ExecuteAsync(null);

        // Assert: the picture is selected
        Assert.Equal(picture, shell.Workspace.SelectedFile);
    }

    /// <summary>
    ///     Test that a project whose remembered picture was deleted opens with nothing selected, as before.
    /// </summary>
    [Fact]
    public async Task MainWindowShell_OpenProject_RememberedPictureGone_SelectsNothing()
    {
        // Arrange: a remembered picture that was deleted
        var file = await CreateProjectFileAsync("Deleted");
        var picture = WritePicture(file, "gone.svg");
        WritePicture(file, "kept.svg");
        var settings = new SettingsService(Path.Combine(_tempRoot, "settings"));
        var recent = new RecentProjects(settings);
        recent.Add(file);
        recent.RememberShownPicture(file, "gone.svg");
        File.Delete(picture);
        var shell = CreateShell(settings: settings, recent: recent);
        _dialogs.ProjectFileToPick = file;

        // Act: open the project
        await shell.OpenProjectCommand.ExecuteAsync(null);

        // Assert: the project is open with nothing selected, and no problem was shown
        Assert.True(shell.HasProject);
        Assert.Null(shell.Workspace.SelectedFile);
        Assert.Empty(_dialogs.Messages);
    }

    /// <summary>
    ///     Test that two projects each open at their own last shown picture when the user goes back and forth.
    /// </summary>
    [Fact]
    public async Task MainWindowShell_OpenProject_TwoProjects_EachOpensAtItsOwnPicture()
    {
        // Arrange: two projects with two pictures each
        var first = await CreateProjectFileAsync("First");
        WritePicture(first, "one.svg");
        var firstPicture = WritePicture(first, "two.svg");
        var second = await CreateProjectFileAsync("Second");
        var secondPicture = WritePicture(second, "three.svg");
        WritePicture(second, "four.svg");
        var shell = CreateShell();

        // Act: show a picture in each, then open each again
        _dialogs.ProjectFileToPick = first;
        await shell.OpenProjectCommand.ExecuteAsync(null);
        shell.Workspace.Explorer.SelectFile(firstPicture);
        _dialogs.ProjectFileToPick = second;
        await shell.OpenProjectCommand.ExecuteAsync(null);
        var secondAtFirstOpen = shell.Workspace.SelectedFile;
        shell.Workspace.Explorer.SelectFile(secondPicture);
        _dialogs.ProjectFileToPick = first;
        await shell.OpenProjectCommand.ExecuteAsync(null);
        var firstAgain = shell.Workspace.SelectedFile;
        _dialogs.ProjectFileToPick = second;
        await shell.OpenProjectCommand.ExecuteAsync(null);
        var secondAgain = shell.Workspace.SelectedFile;

        // Assert: nothing was selected in the second project at first, and each opened at its own picture afterwards
        Assert.Null(secondAtFirstOpen);
        Assert.Equal(firstPicture, firstAgain);
        Assert.Equal(secondPicture, secondAgain);
    }

    /// <summary>
    ///     Test that a new project, which has no picture that was shown, opens with nothing selected and nothing remembered.
    /// </summary>
    [Fact]
    public async Task MainWindowShell_NewProject_NothingRemembered_SelectsNothing()
    {
        // Arrange: a shell whose user creates a project
        var settings = new SettingsService(Path.Combine(_tempRoot, "settings"));
        var shell = CreateShell(settings: settings, defaultProjectsFolder: Directory.CreateDirectory(Projects).FullName);
        _dialogs.OnNewProject = dialog =>
        {
            dialog.ProjectName = "Fresh";
            dialog.OkCommand.Execute(null);
        };

        // Act: run New Project
        await shell.NewProjectCommand.ExecuteAsync(null);

        // Assert: the project is open with nothing selected and no picture remembered for it
        Assert.True(shell.HasProject);
        Assert.Null(shell.Workspace.SelectedFile);
        Assert.Empty(settings.Current.RecentProjectPicture);
    }

    /// <summary>
    ///     Test that the shell gives the explorer the row height that was saved in the Appearance settings and follows the
    ///     setting when it changes.
    /// </summary>
    [Fact]
    public void MainWindowShell_AppearanceSettings_ComfortableRowsReachTheExplorer()
    {
        // Arrange: settings that ask for comfortable rows, and a shell over them
        var settings = new SettingsService(Path.Combine(_tempRoot, "settings"));
        settings.Update(s => s with { Appearance = s.Appearance with { ComfortableRows = true } });
        var shell = CreateShell(settings: settings);
        var atStart = shell.Workspace.Explorer.ComfortableRows;

        // Act: the user turns the setting off, and then on again
        settings.Update(s => s with { Appearance = s.Appearance with { ComfortableRows = false } });
        var afterOff = shell.Workspace.Explorer.ComfortableRows;
        settings.Update(s => s with { Appearance = s.Appearance with { ComfortableRows = true } });
        var afterOn = shell.Workspace.Explorer.ComfortableRows;

        // Assert: the explorer followed the setting from the start
        Assert.True(atStart);
        Assert.False(afterOff);
        Assert.True(afterOn);
    }

    /// <summary>
    ///     Test that the shell gives the workspace the widths of the explorer and the chat column that were saved, saves the
    ///     widths that the user chooses with a splitter, and saves the size, place, and state of the window that it is told.
    /// </summary>
    [Fact]
    public void MainWindowShell_WindowLayout_RestoresWidthsAndSavesChoicesAndWindow()
    {
        // Arrange: settings that hold a layout, and a shell over them
        var settings = new SettingsService(Path.Combine(_tempRoot, "settings"));
        settings.Update(s => s with { Window = s.Window with { ExplorerWidth = 300, ChatWidth = 500 } });
        var shell = CreateShell(settings: settings);
        var restored = (shell.Workspace.ExplorerWidth, shell.Workspace.ChatWidth, shell.SavedWindow.ExplorerWidth);

        // Act: the user chooses other widths, and the window is closed maximized after it was moved
        shell.Workspace.SetColumnWidths(220, 350);
        var afterChoice = (settings.Current.Window.ExplorerWidth, settings.Current.Window.ChatWidth);
        shell.SaveWindow(1200, 800, 40, 50, maximized: true);
        var saved = settings.Current.Window;

        // Assert: the saved widths were used, the choice was saved, and the window layout was saved with it
        Assert.Equal((300, 500, 300), restored);
        Assert.Equal((220, 350), afterChoice);
        Assert.Equal((1200, 800, 40, 50, SavedWindowState.Maximized), (saved.Width, saved.Height, saved.Left, saved.Top, saved.State));
        Assert.Equal((220, 350), (saved.ExplorerWidth, saved.ChatWidth));
    }

    /// <summary>
    ///     Test that a menu header keeps underscores visible instead of treating them as access keys.
    /// </summary>
    [Fact]
    public void MainWindowShell_RecentProjectItem_NameWithUnderscore_HeaderDoublesUnderscore()
    {
        // Arrange & Act: an item for a project named with an underscore
        var item = new RecentProjectItem("my_project", "C:\\x\\my_project.canvaspilot", new CommunityToolkit.Mvvm.Input.AsyncRelayCommand(() => Task.CompletedTask));

        // Assert: the name is kept and the header is escaped
        Assert.Equal("my_project", item.Name);
        Assert.Equal("my__project", item.Header);
    }

    /// <summary>
    ///     Test that choosing a recent project whose file is gone reports a problem and drops it from the list.
    /// </summary>
    [Fact]
    public async Task MainWindowShell_RecentProjects_MissingProject_ShowsMessageAndRemovesEntry()
    {
        // Arrange: a remembered project, a menu built from it, and then the project folder deleted
        var file = await CreateProjectFileAsync("Gone");
        var settings = new SettingsService(Path.Combine(_tempRoot, "settings"));
        var recent = new RecentProjects(settings);
        recent.Add(file);
        var shell = CreateShell(settings: settings, recent: recent);
        var item = shell.RecentProjects[0];
        Directory.Delete(Path.GetDirectoryName(file)!, recursive: true);

        // Act: choose the project
        await item.OpenCommand.ExecuteAsync(null);

        // Assert: the problem was shown, no project opened, and the menu no longer lists it
        Assert.Single(_dialogs.Messages);
        Assert.False(shell.HasProject);
        Assert.Empty(shell.RecentProjects);
        Assert.False(shell.HasRecentProjects);
    }

    /// <summary>
    ///     Test that Save Project writes the project file with a modified time.
    /// </summary>
    [Fact]
    public async Task MainWindowShell_SaveProject_OpenProject_WritesModifiedTime()
    {
        // Arrange: an open project that has never been saved after creation
        var file = await CreateProjectFileAsync("Saved");
        var shell = CreateShell();
        _dialogs.ProjectFileToPick = file;
        await shell.OpenProjectCommand.ExecuteAsync(null);

        // Act: run Save Project
        await shell.SaveProjectCommand.ExecuteAsync(null);

        // Assert: the file on disk now has a modified time and the shell shows the saved project
        Assert.Contains("modifiedUtc", await File.ReadAllTextAsync(file, TestContext.Current.CancellationToken), StringComparison.OrdinalIgnoreCase);
        Assert.NotNull(shell.CurrentProject!.Document.ModifiedUtc);
        Assert.Empty(_dialogs.Messages);
    }

    /// <summary>
    ///     Test that a failed save is reported to the user.
    /// </summary>
    [Fact]
    public async Task MainWindowShell_SaveProject_SaveFails_ShowsMessage()
    {
        // Arrange: an open project whose save fails
        var folder = Directory.CreateDirectory(Path.Combine(Projects, "P")).FullName;
        var info = new ProjectInfo("P", folder, Path.Combine(folder, "P.canvaspilot"), new ProjectDocument { Name = "P" });
        var projects = Substitute.For<IProjectService>();
        projects.Current.Returns(info);
        projects.SaveAsync(Arg.Any<CancellationToken>()).Returns(ProjectResult.Failure("Disk full."));
        var shell = CreateShell(projects);

        // Act: run Save Project
        await shell.SaveProjectCommand.ExecuteAsync(null);

        // Assert: the problem was shown
        Assert.Equal([("Save Project", "Disk full.")], _dialogs.Messages);
    }

    /// <summary>
    ///     Test that Close Project closes the project, resets the title, and empties the explorer.
    /// </summary>
    [Fact]
    public async Task MainWindowShell_CloseProject_OpenProject_ResetsTitleAndExplorer()
    {
        // Arrange: an open project
        var file = await CreateProjectFileAsync("Closing");
        var shell = CreateShell();
        _dialogs.ProjectFileToPick = file;
        await shell.OpenProjectCommand.ExecuteAsync(null);

        // Act: run Close Project
        shell.CloseProjectCommand.Execute(null);

        // Assert: nothing is open and the commands that need a project are disabled
        Assert.Equal("CanvasPilot", shell.Title);
        Assert.False(shell.HasProject);
        Assert.False(shell.Workspace.Explorer.HasProject);
        Assert.False(shell.CloseProjectCommand.CanExecute(null));
        Assert.False(shell.SaveProjectCommand.CanExecute(null));
    }

    /// <summary>
    ///     Test that a project closed or opened through the project service, not the shell, is still followed by the
    ///     title and explorer.
    /// </summary>
    [Fact]
    public async Task MainWindowShell_ProjectServiceChanges_TitleFollows()
    {
        // Arrange: a shell over a service that another part of the application drives
        var parts = CreateParts();
        var shell = CreateShell(parts);
        var created = await parts.Projects.CreateAsync(Directory.CreateDirectory(Projects).FullName, "Outside", TestContext.Current.CancellationToken);

        // Act & Assert: opening through the service updates the shell, and so does closing
        Assert.True(created.Succeeded, created.Error);
        Assert.Equal("Outside - CanvasPilot", shell.Title);
        Assert.Equal(created.Project!.FolderPath, shell.Workspace.Explorer.FolderPath);
        parts.Projects.Close();
        Assert.Equal("CanvasPilot", shell.Title);
        Assert.False(shell.Workspace.HasProject);
    }

    /// <summary>
    ///     Test that Exit asks the window to close.
    /// </summary>
    [Fact]
    public void MainWindowShell_Exit_RaisesExitRequested()
    {
        // Arrange: a shell with a listener
        var shell = CreateShell();
        var raised = 0;
        shell.ExitRequested += (_, _) => raised++;

        // Act: run Exit
        shell.ExitCommand.Execute(null);

        // Assert: one request was raised
        Assert.Equal(1, raised);
    }

    /// <summary>
    ///     Test that the Image menu commands are disabled until an exporter reports an image, then follow it and
    ///     pass the dialog service to the exporter.
    /// </summary>
    [Fact]
    public async Task MainWindowShell_ImageCommands_FollowExporterAndInvokeIt()
    {
        // Arrange: a shell over an exporter with no image
        var exporter = new FakeImageExporter();
        var shell = CreateShell(exporter: exporter);
        var changes = 0;
        shell.ExportPngCommand.CanExecuteChanged += (_, _) => changes++;
        var disabledBefore = !shell.ExportPngCommand.CanExecute(null) && !shell.CopyImageCommand.CanExecute(null);

        // Act: an image becomes available, then both commands run
        exporter.CanExport = true;
        await shell.ExportPngCommand.ExecuteAsync(null);
        await shell.CopyImageCommand.ExecuteAsync(null);

        // Assert: the commands were disabled, became enabled, and reached the exporter with the dialog service
        Assert.True(disabledBefore);
        Assert.True(shell.ExportPngCommand.CanExecute(null));
        Assert.True(shell.CopyImageCommand.CanExecute(null));
        Assert.True(changes > 0);
        Assert.Equal(1, exporter.ExportCount);
        Assert.Equal(1, exporter.CopyCount);
        Assert.Same(_dialogs, exporter.LastDialogs);
    }

    /// <summary>
    ///     Test that the default exporter never enables the Image menu commands and does nothing when called.
    /// </summary>
    [Fact]
    public async Task NullImageExporter_Default_IsDisabledAndDoesNothing()
    {
        // Arrange: the default exporter
        var exporter = new NullImageExporter();
        exporter.CanExportChanged += (_, _) => { };

        // Act & Assert: it cannot export and both operations complete without effect
        Assert.False(exporter.CanExport);
        await exporter.ExportPngAsync(_dialogs);
        await exporter.CopyImageAsync(_dialogs);
        Assert.Empty(_dialogs.Messages);
    }

    /// <summary>
    ///     Test that Settings shows the settings dialog over the supplied pages and tells the pages when it closed.
    /// </summary>
    [Fact]
    public async Task MainWindowShell_OpenSettings_ShowsDialogWithPagesAndNotifiesClose()
    {
        // Arrange: a shell with one settings page
        var page = new FakeSettingsPage("Fake");
        var shell = CreateShell(pages: [page]);

        // Act: run Settings
        await shell.OpenSettingsCommand.ExecuteAsync(null);

        // Assert: the dialog was shown over the page, and the page was told the dialog closed
        Assert.Equal(1, _dialogs.SettingsCount);
        Assert.Equal([page], _dialogs.LastSettings!.Pages);
        Assert.Equal(1, page.ClosedCount);
    }

    /// <summary>
    ///     Test that About shows the About dialog.
    /// </summary>
    [Fact]
    public async Task MainWindowShell_ShowAbout_ShowsAboutDialog()
    {
        // Arrange: a shell
        var shell = CreateShell();

        // Act: run About
        await shell.ShowAboutCommand.ExecuteAsync(null);

        // Assert: the About dialog was shown
        Assert.Equal(1, _dialogs.AboutCount);
        Assert.Equal("CanvasPilot", _dialogs.LastAbout!.Name);
    }

    /// <summary>
    ///     Test that Drawing Knowledge asks the dialog service to show a browser whose tree is the roots of the catalog,
    ///     and that the catalog is asked for each time the command runs, not when the shell is made.
    /// </summary>
    [Fact]
    public async Task MainWindowShell_ShowDrawingKnowledge_ShowsBrowserOnTheCatalog()
    {
        // Arrange: a shell whose knowledge is a small catalog that counts how often it is asked for
        var catalog = new FakeKnowledgeCatalog(
            FakeKnowledgeCatalog.Card("svg", "SVG", children: ["svg/shapes"]),
            FakeKnowledgeCatalog.Card("svg/shapes", "Shapes"),
            FakeKnowledgeCatalog.Card("mermaid", "Mermaid"));
        var asked = 0;
        var shell = CreateKnowledgeShell(() =>
        {
            asked++;
            return catalog;
        });
        var askedBeforeRun = asked;

        // Act: run Drawing Knowledge
        var canRun = shell.ShowDrawingKnowledgeCommand.CanExecute(null);
        await shell.ShowDrawingKnowledgeCommand.ExecuteAsync(null);

        // Assert: the launcher got a browser over the roots of the catalog
        Assert.True(canRun);
        Assert.Equal(0, askedBeforeRun);
        Assert.Equal(1, asked);
        Assert.Equal(1, _dialogs.KnowledgeBrowserCount);
        Assert.Equal(["svg", "mermaid"], _dialogs.LastKnowledgeBrowser!.Nodes.Select(n => n.Id));
        Assert.Empty(_dialogs.Messages);
    }

    /// <summary>
    ///     Test that Drawing Knowledge is disabled when the application has no knowledge, and does nothing before a dialog
    ///     service is attached.
    /// </summary>
    [Fact]
    public async Task MainWindowShell_ShowDrawingKnowledge_WithoutKnowledgeOrDialogService_DoesNothing()
    {
        // Arrange: a shell with no knowledge, and one with knowledge but no dialog service
        var withoutKnowledge = CreateKnowledgeShell(null);
        var asked = 0;
        var withoutDialogs = CreateKnowledgeShell(
            () =>
            {
                asked++;
                return new FakeKnowledgeCatalog();
            },
            attachDialogs: false);

        // Act: run the command of each
        var disabled = withoutKnowledge.ShowDrawingKnowledgeCommand.CanExecute(null);
        await withoutKnowledge.ShowDrawingKnowledgeCommand.ExecuteAsync(null);
        await withoutDialogs.ShowDrawingKnowledgeCommand.ExecuteAsync(null);

        // Assert: nothing was shown and the catalog was not asked for
        Assert.False(disabled);
        Assert.Equal(0, _dialogs.KnowledgeBrowserCount);
        Assert.Equal(0, asked);
    }

    /// <summary>
    ///     Test that a catalog that cannot be loaded is logged and reported to the user, and that no browser is shown.
    /// </summary>
    [Fact]
    public async Task MainWindowShell_ShowDrawingKnowledge_CatalogCannotBeLoaded_LogsAndShowsMessage()
    {
        // Arrange: a shell whose knowledge cannot be loaded, with a log
        var logger = new RollingFileLogger(Path.Combine(_tempRoot, "logs"));
        var shell = CreateKnowledgeShell(() => throw new InvalidOperationException("The card 'svg/broken' is malformed."), logger);

        // Act: run Drawing Knowledge
        await shell.ShowDrawingKnowledgeCommand.ExecuteAsync(null);

        // Assert: the user is told, the problem is in the log, and no browser was shown
        var message = Assert.Single(_dialogs.Messages);
        Assert.Equal("Drawing Knowledge", message.Title);
        Assert.Contains("svg/broken", message.Message, StringComparison.Ordinal);
        Assert.Equal(0, _dialogs.KnowledgeBrowserCount);
        var log = await File.ReadAllTextAsync(logger.ActiveFilePath, TestContext.Current.CancellationToken);
        Assert.Contains("could not be loaded", log, StringComparison.Ordinal);
        Assert.Contains("svg/broken", log, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Test that Style Kits makes the view model of the window from the dialog service each time it runs and hands it to
    ///     the dialog service to show.
    /// </summary>
    [Fact]
    public async Task MainWindowShell_ShowStyleKits_MakesTheViewModelFromTheDialogsAndShowsIt()
    {
        // Arrange: a shell whose window is made over a real catalog on temporary folders
        using var places = new StyleKitPlaces();
        using var catalog = places.CreateCatalog();
        var received = new List<IStyleKitDialogs>();
        var shell = CreateStyleKitsShell(dialogs =>
        {
            received.Add(dialogs);
            return new StyleKitManagerViewModel(catalog, new FakeActiveStyle(), new RecordingRenderService(), dialogs, places.Projects);
        });

        // Act: run Style Kits twice
        var canRun = shell.ShowStyleKitsCommand.CanExecute(null);
        await shell.ShowStyleKitsCommand.ExecuteAsync(null);
        var first = _dialogs.LastStyleKitManager;
        await shell.ShowStyleKitsCommand.ExecuteAsync(null);

        // Assert: each run asked for a view model from the dialog service and showed it
        Assert.True(canRun);
        Assert.Equal(2, received.Count);
        Assert.All(received, dialogs => Assert.Same(_dialogs, dialogs));
        Assert.Equal(2, _dialogs.StyleKitManagerCount);
        Assert.NotSame(first, _dialogs.LastStyleKitManager);
        Assert.Empty(_dialogs.Messages);
        first!.Dispose();
        _dialogs.LastStyleKitManager!.Dispose();
    }

    /// <summary>
    ///     Test that Style Kits is unusable when the application has no such window, and does nothing before a dialog
    ///     service is attached.
    /// </summary>
    [Fact]
    public async Task MainWindowShell_ShowStyleKits_WithoutWindowOrDialogService_DoesNothing()
    {
        // Arrange: a shell with no window, and one with a window but no dialog service
        var withoutWindow = CreateStyleKitsShell(null);
        var made = 0;
        var withoutDialogs = CreateStyleKitsShell(
            _ =>
            {
                made++;
                throw new InvalidOperationException("Not to be called.");
            },
            attachDialogs: false);

        // Act: run the command of each
        var disabled = withoutWindow.ShowStyleKitsCommand.CanExecute(null);
        await withoutWindow.ShowStyleKitsCommand.ExecuteAsync(null);
        await withoutDialogs.ShowStyleKitsCommand.ExecuteAsync(null);

        // Assert: nothing was made or shown
        Assert.False(disabled);
        Assert.Equal(0, made);
        Assert.Equal(0, _dialogs.StyleKitManagerCount);
    }

    /// <summary>
    ///     Test that commands that need the user do nothing before a dialog service is attached.
    /// </summary>
    [Fact]
    public async Task MainWindowShell_CommandsWithoutDialogService_DoNothing()
    {
        // Arrange: a shell with no dialog service attached
        var parts = CreateParts();
        var shell = new MainWindowShell(parts.Projects, parts.Recent, parts.Settings, parts.Workspace, [], new FakeImageExporter { CanExport = true });
        _shells.Add(shell);

        // Act: run every command that needs the user
        await shell.NewProjectCommand.ExecuteAsync(null);
        await shell.OpenProjectCommand.ExecuteAsync(null);
        await shell.OpenSettingsCommand.ExecuteAsync(null);
        await shell.ShowAboutCommand.ExecuteAsync(null);
        await shell.ExportPngCommand.ExecuteAsync(null);
        await shell.CopyImageCommand.ExecuteAsync(null);

        // Assert: nothing happened
        Assert.False(shell.HasProject);
        Assert.Equal(0, _dialogs.NewProjectCount + _dialogs.SettingsCount + _dialogs.AboutCount);
    }

    /// <summary>
    ///     Test that pickers start in the folder that holds the most recent project's folder.
    /// </summary>
    [Fact]
    public async Task MainWindowShell_OpenProject_WithRecentProject_PickerStartsInProjectsFolder()
    {
        // Arrange: a remembered project and a shell
        var file = await CreateProjectFileAsync("Start");
        var settings = new SettingsService(Path.Combine(_tempRoot, "settings"));
        var recent = new RecentProjects(settings);
        recent.Add(file);
        var shell = CreateShell(settings: settings, recent: recent);

        // Act: run Open Project and cancel the picker
        await shell.OpenProjectCommand.ExecuteAsync(null);

        // Assert: the picker was asked to start in the folder that contains the project folder
        Assert.Equal(Projects, _dialogs.LastInitialFolder);
    }

    /// <summary>
    ///     Test that a disposed shell no longer follows the project service.
    /// </summary>
    [Fact]
    public async Task MainWindowShell_Dispose_StopsFollowingServices()
    {
        // Arrange: a shell over a real service, then disposed
        var parts = CreateParts();
        var shell = CreateShell(parts);
        shell.Dispose();
        shell.Dispose();

        // Act: the service opens a project afterwards
        var created = await parts.Projects.CreateAsync(Directory.CreateDirectory(Projects).FullName, "After", TestContext.Current.CancellationToken);

        // Assert: the shell did not change
        Assert.True(created.Succeeded, created.Error);
        Assert.Equal("CanvasPilot", shell.Title);
    }

    /// <summary>
    ///     Test that Undo and Redo run on the edit target, are enabled only while it has a step to undo or redo, and
    ///     follow it when its steps change.
    /// </summary>
    [Fact]
    public void MainWindowShell_UndoRedo_FocusedTarget_DelegatesAndFollowsTargetState()
    {
        // Arrange: a shell and a target that can undo but not redo
        var shell = CreateShell();
        var target = Substitute.For<IUndoRedoTarget>();
        target.CanUndo.Returns(true);
        target.CanRedo.Returns(false);
        var changes = 0;
        shell.UndoCommand.CanExecuteChanged += (_, _) => changes++;

        // Act: focus moves to the target, it is undone, and then it can redo and cannot undo
        shell.EditTarget = target;
        var undoEnabled = shell.UndoCommand.CanExecute(null);
        var redoEnabled = shell.RedoCommand.CanExecute(null);
        shell.UndoCommand.Execute(null);
        target.CanUndo.Returns(false);
        target.CanRedo.Returns(true);
        target.UndoRedoStateChanged += Raise.Event();
        var undoAfter = shell.UndoCommand.CanExecute(null);
        var redoAfter = shell.RedoCommand.CanExecute(null);
        shell.RedoCommand.Execute(null);

        // Assert: Undo and Redo reached the target, and the commands followed its state
        Assert.True(undoEnabled);
        Assert.False(redoEnabled);
        Assert.False(undoAfter);
        Assert.True(redoAfter);
        target.Received(1).Undo();
        target.Received(1).Redo();
        Assert.True(changes >= 2);
    }

    /// <summary>
    ///     Test that with no edit target both commands are disabled and do nothing, and that a target that was replaced
    ///     no longer changes the commands.
    /// </summary>
    [Fact]
    public void MainWindowShell_UndoRedo_NoTarget_DisabledAndReplacedTargetIgnored()
    {
        // Arrange: a shell that had a target which can undo and redo, and then loses it
        var shell = CreateShell();
        var first = Substitute.For<IUndoRedoTarget>();
        first.CanUndo.Returns(true);
        first.CanRedo.Returns(true);
        shell.EditTarget = first;
        shell.EditTarget = null;
        var changes = 0;
        shell.UndoCommand.CanExecuteChanged += (_, _) => changes++;

        // Act: the old target announces a change and the commands are run
        first.UndoRedoStateChanged += Raise.Event();
        shell.UndoCommand.Execute(null);
        shell.RedoCommand.Execute(null);

        // Assert: nothing is enabled, nothing reached the old target, and the commands were not re-evaluated
        Assert.False(shell.UndoCommand.CanExecute(null));
        Assert.False(shell.RedoCommand.CanExecute(null));
        first.DidNotReceive().Undo();
        first.DidNotReceive().Redo();
        Assert.Equal(0, changes);
    }

    /// <summary>
    ///     Test that the talk key is F9 by default and that pressing and releasing it starts and stops the push-to-talk target once.
    /// </summary>
    [Fact]
    public async Task MainWindowShell_PushToTalk_BeginEnd_DelegatesToTarget()
    {
        // Arrange: a shell with a push-to-talk target
        var shell = CreateShell();
        var target = Substitute.For<IPushToTalkTarget>();
        target.BeginPushToTalkAsync().Returns(Task.CompletedTask);
        target.EndPushToTalkAsync().Returns(Task.CompletedTask);
        shell.PushToTalk = target;

        // Act: press, repeat twice (the keyboard repeating the key), release, and release again
        await shell.BeginPushToTalkAsync();
        var activeWhileHeld = shell.IsPushToTalkActive;
        await shell.BeginPushToTalkAsync();
        await shell.BeginPushToTalkAsync();
        await shell.EndPushToTalkAsync();
        await shell.EndPushToTalkAsync();

        // Assert: the target was started once and stopped once, and the key is the default, F9
        Assert.Equal(("F9", Avalonia.Input.Key.F9, Avalonia.Input.KeyModifiers.None), (shell.TalkKey.Text, shell.TalkKey.Binding.Key, shell.TalkKey.Binding.Modifiers));
        Assert.True(activeWhileHeld);
        Assert.False(shell.IsPushToTalkActive);
        await target.Received(1).BeginPushToTalkAsync();
        await target.Received(1).EndPushToTalkAsync();
    }

    /// <summary>
    ///     Test that the talk key does nothing without a target, and that a release goes to the target that was started
    ///     even when the target was replaced while the key was down.
    /// </summary>
    [Fact]
    public async Task MainWindowShell_PushToTalk_NoTargetOrTargetReplaced_IsIgnoredOrStopsStartedTarget()
    {
        // Arrange: a shell with no target
        var shell = CreateShell();
        await shell.BeginPushToTalkAsync();
        var activeWithoutTarget = shell.IsPushToTalkActive;
        var first = Substitute.For<IPushToTalkTarget>();
        var second = Substitute.For<IPushToTalkTarget>();
        first.BeginPushToTalkAsync().Returns(Task.CompletedTask);
        first.EndPushToTalkAsync().Returns(Task.CompletedTask);

        // Act: start with the first target, replace it with the second, and release
        shell.PushToTalk = first;
        await shell.BeginPushToTalkAsync();
        shell.PushToTalk = second;
        await shell.EndPushToTalkAsync();

        // Assert: the key without a target was not active, and the first target was the one stopped
        Assert.False(activeWithoutTarget);
        await first.Received(1).EndPushToTalkAsync();
        await second.DidNotReceive().BeginPushToTalkAsync();
        await second.DidNotReceive().EndPushToTalkAsync();
    }

    /// <summary>
    ///     Creates a project file on disk through a separate service so the shell's own services are untouched.
    /// </summary>
    /// <param name="name">The project name.</param>
    /// <returns>The full path of the project file.</returns>
    private async Task<string> CreateProjectFileAsync(string name)
    {
        var created = await new ProjectService().CreateAsync(Directory.CreateDirectory(Projects).FullName, name, TestContext.Current.CancellationToken);
        return created.Project!.FilePath;
    }

    /// <summary>
    ///     Writes a small picture into the folder of a project.
    /// </summary>
    /// <param name="projectFile">The project file, whose folder holds the picture.</param>
    /// <param name="relative">The path of the picture relative to the project folder.</param>
    /// <returns>The full path of the picture.</returns>
    private static string WritePicture(string projectFile, string relative)
    {
        var path = Path.Combine(Path.GetDirectoryName(projectFile)!, relative);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, "<svg xmlns='http://www.w3.org/2000/svg' width='10' height='10'/>");
        return path;
    }

    /// <summary>
    ///     Creates the real units a shell is built from, on temporary folders.
    /// </summary>
    /// <returns>The units.</returns>
    private ShellParts CreateParts()
    {
        var logger = new RollingFileLogger(Path.Combine(_tempRoot, "logs"));
        var settings = new SettingsService(Path.Combine(_tempRoot, "settings"), logger);
        var explorer = new ProjectExplorerViewModel(new FakeImageSourceFilter(".svg"), new FakeRefreshScheduler(), logger: logger);
        return new ShellParts(new ProjectService(logger), new RecentProjects(settings), settings, new ProjectWorkspaceViewModel(explorer));
    }

    /// <summary>
    ///     Test that Open Log Folder asks the launcher to show the folder that the logger writes to, and that it can be used
    ///     with no project open.
    /// </summary>
    [Fact]
    public async Task MainWindowShell_OpenLogFolder_ShowsTheFolderOfTheLogger()
    {
        // Arrange: a shell with a logger on a temporary folder and a launcher that records what it was asked
        var root = Directory.CreateTempSubdirectory("canvaspilot-log-folder-").FullName;
        try
        {
            var logger = new RollingFileLogger(Path.Combine(root, "logs"));
            var launcher = new FakeFolderLauncher();
            var shell = CreateLogShell(launcher, logger);

            // Act
            var usable = shell.OpenLogFolderCommand.CanExecute(null);
            await shell.OpenLogFolderCommand.ExecuteAsync(null);

            // Assert
            Assert.True(usable);
            Assert.Equal([logger.LogDirectory], launcher.Opened);
            Assert.Empty(_dialogs.Messages);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    /// <summary>
    ///     Test that without a logger the folder of the default log is shown, and that a launcher that cannot show the folder
    ///     gives the user a message with the reason and the path.
    /// </summary>
    [Fact]
    public async Task MainWindowShell_OpenLogFolder_NoLoggerOrLauncherFails_UsesTheDefaultFolderAndTellsTheUser()
    {
        // Arrange: a launcher that fails, and a shell without a logger
        var launcher = new FakeFolderLauncher { Failure = "No file manager." };
        var shell = CreateLogShell(launcher, logger: null);

        // Act
        await shell.OpenLogFolderCommand.ExecuteAsync(null);

        // Assert: the default folder was tried and the user was told, with the path
        Assert.Equal([RollingFileLogger.DefaultLogDirectory], launcher.Opened);
        var (title, message) = Assert.Single(_dialogs.Messages);
        Assert.Equal("Open Log Folder", title);
        Assert.Contains("No file manager.", message, StringComparison.Ordinal);
        Assert.Contains(RollingFileLogger.DefaultLogDirectory, message, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Test that a shell without a launcher cannot open the folder of the log, and that the command then does nothing.
    /// </summary>
    [Fact]
    public async Task MainWindowShell_OpenLogFolder_WithoutLauncher_IsDisabledAndDoesNothing()
    {
        // Arrange
        var shell = CreateShell(CreateParts());

        // Act and assert
        Assert.False(shell.OpenLogFolderCommand.CanExecute(null));
        await shell.OpenLogFolderCommand.ExecuteAsync(null);
        Assert.Empty(_dialogs.Messages);
    }

    /// <summary>
    ///     Creates a shell with a launcher of folders and the dialog service attached.
    /// </summary>
    /// <param name="launcher">The launcher.</param>
    /// <param name="logger">The logger, or <see langword="null" />.</param>
    /// <returns>The shell, disposed with the fixture.</returns>
    private MainWindowShell CreateLogShell(IFolderLauncher launcher, RollingFileLogger? logger)
    {
        var parts = CreateParts();
        var shell = new MainWindowShell(parts.Projects, parts.Recent, parts.Settings, parts.Workspace, [], logger: logger, folderLauncher: launcher);
        shell.AttachDialogService(_dialogs);
        _shells.Add(shell);
        return shell;
    }

    /// <summary>
    ///     Creates a shell with the dialog service attached.
    /// </summary>
    /// <param name="projects">A project service to use instead of the real one.</param>
    /// <param name="settings">A settings service to use instead of the real one.</param>
    /// <param name="recent">A recent projects list to use instead of the real one.</param>
    /// <param name="pages">The settings pages.</param>
    /// <param name="exporter">The image exporter.</param>
    /// <param name="defaultProjectsFolder">The default projects folder.</param>
    /// <returns>The shell, disposed with the fixture.</returns>
    private MainWindowShell CreateShell(
        IProjectService? projects = null,
        SettingsService? settings = null,
        IRecentProjects? recent = null,
        IReadOnlyList<ISettingsPage>? pages = null,
        IImageExporter? exporter = null,
        string? defaultProjectsFolder = null)
    {
        var parts = CreateParts();
        return CreateShell(
            parts with
            {
                Projects = projects ?? parts.Projects,
                Settings = settings ?? parts.Settings,
                Recent = recent ?? (settings is null ? parts.Recent : new RecentProjects(settings)),
            },
            pages,
            exporter,
            defaultProjectsFolder);
    }

    /// <summary>
    ///     Creates a shell from the given units with the dialog service attached.
    /// </summary>
    /// <param name="parts">The units.</param>
    /// <param name="pages">The settings pages.</param>
    /// <param name="exporter">The image exporter.</param>
    /// <param name="defaultProjectsFolder">The default projects folder.</param>
    /// <returns>The shell, disposed with the fixture.</returns>
    private MainWindowShell CreateShell(
        ShellParts parts,
        IReadOnlyList<ISettingsPage>? pages = null,
        IImageExporter? exporter = null,
        string? defaultProjectsFolder = null)
    {
        var shell = new MainWindowShell(parts.Projects, parts.Recent, parts.Settings, parts.Workspace, pages ?? [], exporter, defaultProjectsFolder);
        shell.AttachDialogService(_dialogs);
        _shells.Add(shell);
        return shell;
    }

    /// <summary>
    ///     Creates a shell with drawing knowledge and, when asked, the dialog service attached.
    /// </summary>
    /// <param name="knowledge">Gets the catalog of the drawing knowledge, or <see langword="null" /> for none.</param>
    /// <param name="logger">The logger, or <see langword="null" />.</param>
    /// <param name="attachDialogs">Whether the dialog service is attached.</param>
    /// <returns>The shell, disposed with the fixture.</returns>
    private MainWindowShell CreateKnowledgeShell(Func<IKnowledgeCatalog>? knowledge, RollingFileLogger? logger = null, bool attachDialogs = true)
    {
        var parts = CreateParts();
        var shell = new MainWindowShell(parts.Projects, parts.Recent, parts.Settings, parts.Workspace, [], null, null, knowledge, logger: logger);
        if (attachDialogs)
        {
            shell.AttachDialogService(_dialogs);
        }

        _shells.Add(shell);
        return shell;
    }

    /// <summary>
    ///     Creates a shell with a window that manages style kits and, when asked, the dialog service attached.
    /// </summary>
    /// <param name="styleKits">Makes the view model of the window, or <see langword="null" /> for none.</param>
    /// <param name="attachDialogs">Whether the dialog service is attached.</param>
    /// <returns>The shell, disposed with the fixture.</returns>
    private MainWindowShell CreateStyleKitsShell(Func<IStyleKitDialogs, StyleKitManagerViewModel>? styleKits, bool attachDialogs = true)
    {
        var parts = CreateParts();
        var shell = new MainWindowShell(parts.Projects, parts.Recent, parts.Settings, parts.Workspace, [], styleKits: styleKits);
        if (attachDialogs)
        {
            shell.AttachDialogService(_dialogs);
        }

        _shells.Add(shell);
        return shell;
    }

    /// <summary>
    ///     The units a shell is built from.
    /// </summary>
    /// <param name="Projects">The project service.</param>
    /// <param name="Recent">The recent projects list.</param>
    /// <param name="Settings">The settings service.</param>
    /// <param name="Workspace">The workspace.</param>
    private sealed record ShellParts(IProjectService Projects, IRecentProjects Recent, ISettingsService Settings, ProjectWorkspaceViewModel Workspace);
}
