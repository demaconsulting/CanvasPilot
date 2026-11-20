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

using DemaConsulting.CanvasPilot.AnnotationSubsystem;
using DemaConsulting.CanvasPilot.AppShellSubsystem;
using DemaConsulting.CanvasPilot.LoggingSubsystem;
using DemaConsulting.CanvasPilot.SettingsSubsystem;
using DemaConsulting.CanvasPilot.SpeechSubsystem;

namespace DemaConsulting.CanvasPilot.Tests;

/// <summary>
///     Subsystem tests for the AppShell subsystem: the composition root, the shell, and the workspace working
///     together over the real project, settings, and logging units on temporary folders, with a hand-written dialog
///     service in place of the window.
/// </summary>
public sealed class AppShellSubsystemTests : IDisposable
{
    /// <summary>
    ///     Temporary root created fresh for each test and removed on disposal.
    /// </summary>
    private readonly string _tempRoot = Directory.CreateTempSubdirectory("canvaspilot-tests-").FullName;

    /// <summary>
    ///     The compositions created by a test, disposed with the fixture so no watcher outlives it.
    /// </summary>
    private readonly List<ApplicationComposition> _compositions = [];

    /// <summary>
    ///     The dialog service attached to the shells.
    /// </summary>
    private readonly FakeDialogService _dialogs = new();

    /// <summary>
    ///     The folder that holds the projects the tests create.
    /// </summary>
    private string Projects => Path.Combine(_tempRoot, "projects");

    /// <inheritdoc />
    public void Dispose()
    {
        foreach (var composition in _compositions)
        {
            composition.Dispose();
        }

        if (Directory.Exists(_tempRoot))
        {
            Directory.Delete(_tempRoot, recursive: true);
        }
    }

    /// <summary>
    ///     Test that composing the application loads the stored settings and recent projects, logs the start and the
    ///     stop, and exposes the composed units.
    /// </summary>
    [Fact]
    public async Task AppShellSubsystem_ComposeApplication_LoadsSettingsListsRecentProjectsAndLogs()
    {
        // Arrange: a first run that stored an AI model name and remembered a project
        var token = TestContext.Current.CancellationToken;
        var firstRun = Compose();
        _dialogs.OnNewProject = dialog =>
        {
            dialog.ProjectName = "Remembered";
            dialog.OkCommand.Execute(null);
        };
        await firstRun.Shell.NewProjectCommand.ExecuteAsync(null);
        firstRun.Settings.Update(settings => settings with { AiProvider = settings.AiProvider with { ModelName = "stored-model" } });
        firstRun.Dispose();

        // Act: a second run composes the application again over the same folders
        var secondRun = Compose();

        // Assert: the stored settings were loaded, the project is in the menu, and both runs logged their start
        Assert.Equal("stored-model", secondRun.Settings.Current.AiProvider.ModelName);
        Assert.Equal(["Remembered"], secondRun.Shell.RecentProjects.Select(item => item.Name));
        Assert.False(secondRun.Shell.HasProject);
        var log = await File.ReadAllTextAsync(secondRun.Logger.ActiveFilePath, token);
        Assert.Contains("Application started.", log, StringComparison.Ordinal);
        Assert.Contains("Application stopped.", log, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Test that a project can be created, saved, closed, and reopened from the Recent Projects menu through the
    ///     shell commands, with the title and explorer following.
    /// </summary>
    [Fact]
    public async Task AppShellSubsystem_ProjectLifecycle_NewSaveCloseReopenFromRecent()
    {
        // Arrange: a composed application whose user creates a project with one image
        var composition = Compose();
        var shell = composition.Shell;
        _dialogs.OnNewProject = dialog =>
        {
            dialog.ProjectName = "Lifecycle";
            dialog.OkCommand.Execute(null);
        };
        await shell.NewProjectCommand.ExecuteAsync(null);
        var folder = shell.CurrentProject!.FolderPath;
        await File.WriteAllTextAsync(Path.Combine(folder, "logo.svg"), "<svg/>", TestContext.Current.CancellationToken);
        shell.Workspace.Explorer.Refresh();
        var titleWhileOpen = shell.Title;
        var listedWhileOpen = shell.Workspace.Explorer.Items.Select(item => item.Name).ToArray();

        // Act: save, close, and reopen from the Recent Projects menu
        await shell.SaveProjectCommand.ExecuteAsync(null);
        shell.CloseProjectCommand.Execute(null);
        var titleWhileClosed = shell.Title;
        await shell.RecentProjects[0].OpenCommand.ExecuteAsync(null);

        // Assert: the title and explorer followed each step, and the saved project reopened with its file
        Assert.Equal("Lifecycle - CanvasPilot", titleWhileOpen);
        Assert.Equal(["logo.svg"], listedWhileOpen);
        Assert.Equal("CanvasPilot", titleWhileClosed);
        Assert.Equal("Lifecycle - CanvasPilot", shell.Title);
        Assert.Equal(["logo.svg"], shell.Workspace.Explorer.Items.Select(item => item.Name));
        Assert.NotNull(shell.CurrentProject.Document.ModifiedUtc);
        Assert.Empty(_dialogs.Messages);
    }

    /// <summary>
    ///     Test that renaming and deleting through the workspace change the project folder and the explorer list.
    /// </summary>
    [Fact]
    public async Task AppShellSubsystem_ManageFiles_RenameAndDelete_UpdateFolderAndExplorer()
    {
        // Arrange: a created project with two images, the first selected
        var token = TestContext.Current.CancellationToken;
        var composition = Compose();
        var shell = composition.Shell;
        _dialogs.OnNewProject = dialog =>
        {
            dialog.ProjectName = "Files";
            dialog.OkCommand.Execute(null);
        };
        await shell.NewProjectCommand.ExecuteAsync(null);
        var folder = shell.CurrentProject!.FolderPath;
        await File.WriteAllTextAsync(Path.Combine(folder, "a.svg"), "<svg/>", token);
        await File.WriteAllTextAsync(Path.Combine(folder, "b.svg"), "<svg/>", token);
        shell.Workspace.Explorer.Refresh();
        shell.Workspace.Explorer.SelectFile(Path.Combine(folder, "a.svg"));

        // Act: rename the selected file, then delete it after confirming
        _dialogs.PromptResult = "cover.svg";
        await shell.Workspace.RenameSelectedCommand.ExecuteAsync(null);
        var afterRename = shell.Workspace.Explorer.Items.Select(item => item.Name).ToArray();
        _dialogs.ConfirmResult = true;
        await shell.Workspace.DeleteSelectedCommand.ExecuteAsync(null);

        // Assert: the rename and delete happened on disk and in the list
        Assert.Equal(["b.svg", "cover.svg"], afterRename);
        Assert.Equal(["b.svg"], shell.Workspace.Explorer.Items.Select(item => item.Name));
        Assert.False(File.Exists(Path.Combine(folder, "cover.svg")));
        Assert.True(File.Exists(Path.Combine(folder, "b.svg")));
        Assert.Empty(_dialogs.Messages);
    }

    /// <summary>
    ///     Test that choices made in the settings dialog opened from the shell are stored and restored by the next
    ///     run.
    /// </summary>
    [Fact]
    public async Task AppShellSubsystem_Settings_DialogOk_StoresChoicesForNextRun()
    {
        // Arrange: a composed application whose user edits the AI provider page and chooses OK
        var firstRun = Compose();
        _dialogs.OnSettings = dialog =>
        {
            var page = Assert.IsType<AiProviderSettingsPageViewModel>(dialog.Pages[0]);
            page.ModelName = "gpt-test";
            dialog.OkCommand.Execute(null);
        };

        // Act: open Settings, then start a second run over the same folders
        await firstRun.Shell.OpenSettingsCommand.ExecuteAsync(null);
        firstRun.Dispose();
        var secondRun = Compose();

        // Assert: the choice was stored and the next run has it
        Assert.Equal("gpt-test", secondRun.Settings.Current.AiProvider.ModelName);
    }

    /// <summary>
    ///     Test that the settings dialog of the composed application hosts the AI Provider page and the Speech page,
    ///     in that order.
    /// </summary>
    [Fact]
    public async Task AppShellSubsystem_Settings_ComposedShell_HostsAiProviderAndSpeechPages()
    {
        // Arrange: a composed application whose settings dialog is inspected and closed
        var composition = Compose();
        string[] titles = [];
        Type? secondPageType = null;
        _dialogs.OnSettings = dialog =>
        {
            titles = dialog.Pages.Select(page => page.Title).ToArray();
            secondPageType = dialog.Pages[1].GetType();
            dialog.CancelCommand.Execute(null);
        };

        // Act: open Settings
        await composition.Shell.OpenSettingsCommand.ExecuteAsync(null);

        // Assert: both pages are hosted
        Assert.Equal(["AI Provider", "Speech"], titles);
        Assert.Equal(typeof(SpeechSettingsPageViewModel), secondPageType);
    }

    /// <summary>
    ///     Test that the About dialog opened from the composed shell shows the application identity and libraries.
    /// </summary>
    [Fact]
    public async Task AppShellSubsystem_About_ComposedShell_ShowsIdentityVersionAndLibraries()
    {
        // Arrange: a composed application
        var composition = Compose();

        // Act: run About
        await composition.Shell.ShowAboutCommand.ExecuteAsync(null);

        // Assert: the dialog shows the name, a version, and the libraries
        var about = _dialogs.LastAbout!;
        Assert.Equal("CanvasPilot", about.Name);
        Assert.False(string.IsNullOrWhiteSpace(about.Version));
        Assert.Equal(5, about.Dependencies.Count);
    }

    /// <summary>
    ///     Test that the composer of the message follows the project that is open: it works in the folder of a project
    ///     that is created, and has no message and refuses editing after the project is closed.
    /// </summary>
    [Fact]
    public async Task AppShellSubsystem_ComposeApplication_ComposerFollowsTheOpenProject()
    {
        // Arrange: a composition with no project open
        var composition = Compose();
        var beforeProject = composition.Composer.HasProject;

        // Act: create a project, write in the composer, then close the project
        var created = await composition.Projects.CreateAsync(Projects, "Demo", TestContext.Current.CancellationToken);
        var folder = created.Project!.FolderPath;
        var typed = composition.Composer.InsertTyped("hello");
        var projectFolder = composition.Composer.ProjectFolder;
        composition.Projects.Close();

        // Assert: it had no project, then the project folder and the typed text, and no project and no text after the close
        Assert.False(beforeProject);
        Assert.True(typed);
        Assert.Equal(folder, projectFolder);
        Assert.False(composition.Composer.HasProject);
        Assert.Equal(0, composition.Composer.Document.Length);
        Assert.False(composition.Composer.InsertTyped("more"));
        Assert.Empty(composition.Marks.Marks);
    }

    /// <summary>
    ///     Test that the marking layer is the overlay of the preview pane and has its tools in the pane's top bar, and that
    ///     it can make marks only while a project is open, because only then can the message take their chips.
    /// </summary>
    [Fact]
    public async Task AppShellSubsystem_ComposeApplication_AnnotationLayerIsOnThePictureAndFollowsTheOpenProject()
    {
        // Arrange: a composition with no project open
        var composition = Compose();
        var beforeProject = composition.Annotation.IsDrawingAllowed;

        // Act: create a project, then close it
        await composition.Projects.CreateAsync(Projects, "Demo", TestContext.Current.CancellationToken);
        var withProject = composition.Annotation.IsDrawingAllowed;
        composition.Projects.Close();
        var afterClose = composition.Annotation.IsDrawingAllowed;

        // Assert: the layer and its tool bar are in the preview pane, and marks are allowed only while a project is open
        Assert.Same(composition.Annotation, composition.Preview.Overlay);
        Assert.Same(composition.Annotation, Assert.IsType<AnnotationToolbarViewModel>(composition.Preview.Toolbar).Annotation);
        Assert.False(beforeProject);
        Assert.True(withProject);
        Assert.False(afterClose);
    }

    /// <summary>
    ///     Test that composing the application refuses missing parts.
    /// </summary>
    [Fact]
    public void AppShellSubsystem_Create_NullArguments_Throw()
    {
        // Arrange: valid parts
        var logger = new RollingFileLogger(Path.Combine(_tempRoot, "logs"));
        var settings = new SettingsService(Path.Combine(_tempRoot, "settings"), logger);
        var scheduler = new FakeRefreshScheduler();

        // Act & Assert: each missing part is refused
        Assert.Throws<ArgumentNullException>(() => ApplicationComposition.Create(null!, settings, scheduler));
        Assert.Throws<ArgumentNullException>(() => ApplicationComposition.Create(logger, null!, scheduler));
        Assert.Throws<ArgumentNullException>(() => ApplicationComposition.Create(logger, settings, null!));
    }

    /// <summary>
    ///     Composes the application on the test folders with the dialog service attached.
    /// </summary>
    /// <returns>The composition, disposed with the fixture.</returns>
    private ApplicationComposition Compose()
    {
        Directory.CreateDirectory(Projects);
        var logger = new RollingFileLogger(Path.Combine(_tempRoot, "logs"));
        var settings = new SettingsService(Path.Combine(_tempRoot, "settings"), logger);
        var composition = ApplicationComposition.Create(
            logger,
            settings,
            new FakeRefreshScheduler(),
            defaultProjectsFolder: Projects,
            audioDevices: SpeechTestData.AudioDevices([], []),
            speechModels: SpeechTestData.Models());
        composition.Shell.AttachDialogService(_dialogs);
        _compositions.Add(composition);
        return composition;
    }
}
