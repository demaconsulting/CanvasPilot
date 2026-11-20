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
using DemaConsulting.CanvasPilot.LoggingSubsystem;
using DemaConsulting.CanvasPilot.ProjectSubsystem;
using DemaConsulting.CanvasPilot.SettingsSubsystem;
using DemaConsulting.CanvasPilot.UtilitiesSubsystem;

namespace DemaConsulting.CanvasPilot.Tests;

/// <summary>
///     System-level tests for CanvasPilot that compose real units on temporary directories without opening a
///     window.
/// </summary>
public sealed class CanvasPilotTests : IDisposable
{
    /// <summary>
    ///     Temporary root directory created fresh for each test and removed on disposal.
    /// </summary>
    private readonly string _tempRoot = Directory.CreateTempSubdirectory("canvaspilot-tests-").FullName;

    /// <inheritdoc />
    public void Dispose()
    {
        if (Directory.Exists(_tempRoot))
        {
            Directory.Delete(_tempRoot, recursive: true);
        }
    }

    /// <summary>
    ///     Test that an application event and a failure are recorded in a local log file under the log folder.
    /// </summary>
    [Fact]
    public void CanvasPilot_OperationalLogging_ApplicationEvent_WritesLocalLog()
    {
        // Arrange: the application's log folder, resolved beneath a per-user data root
        var logDirectory = PathHelpers.SafePathCombine(_tempRoot, Path.Combine("CanvasPilot", "logs"));
        var logger = new RollingFileLogger(logDirectory);

        // Act: record an application event and a failure
        logger.Log(LogLevel.Info, "Application started");
        logger.Log(LogLevel.Error, "Operation failed", new InvalidOperationException("diagnostic detail"));

        // Assert: the log folder holds a local log containing both entries
        var logFile = Assert.Single(Directory.GetFiles(logDirectory, "*.log"));
        var content = File.ReadAllText(logFile);
        Assert.Contains("Application started", content);
        Assert.Contains("Operation failed", content);
        Assert.Contains("diagnostic detail", content);
    }

    /// <summary>
    ///     Test that file locations built from caller-supplied names cannot escape their intended folder.
    /// </summary>
    [Fact]
    public void CanvasPilot_SafeFileHandling_TraversalName_IsRejected()
    {
        // Arrange: a supplied name that attempts to leave the intended folder
        const string suppliedName = "../../outside.log";

        // Act / Assert: the location is refused, and a name within the folder is accepted
        Assert.Throws<ArgumentException>(() => PathHelpers.SafePathCombine(_tempRoot, suppliedName));
        Assert.StartsWith(_tempRoot, PathHelpers.SafePathCombine(_tempRoot, "inside.log"), StringComparison.Ordinal);
    }

    /// <summary>
    ///     Test that user settings written by one run are restored by the next run, and that a damaged settings
    ///     file does not stop the application from starting.
    /// </summary>
    [Fact]
    public void CanvasPilot_PersistUserSettings_SaveAndReload_RestoresSettingsAndToleratesDamage()
    {
        // Arrange: the application's per-user settings folder and log, composed as at startup
        var settingsFolder = PathHelpers.SafePathCombine(_tempRoot, "CanvasPilot");
        var logger = new RollingFileLogger(PathHelpers.SafePathCombine(_tempRoot, "logs"));
        new SettingsService(settingsFolder, logger).Save(new AppSettings { RecentProjects = ["C:\\Work\\a.canvaspilot"] });

        // Act: a second run loads the settings, then the file is damaged and a third run loads again
        var restored = new SettingsService(settingsFolder, logger).Load();
        File.WriteAllText(Path.Combine(settingsFolder, SettingsService.FileName), "not json");
        var afterDamage = new SettingsService(settingsFolder, logger).Load();

        // Assert: the saved settings were restored, and the damaged file produced defaults, a kept-aside copy,
        // and a log entry
        Assert.Equal(["C:\\Work\\a.canvaspilot"], restored.RecentProjects);
        Assert.Empty(afterDamage.RecentProjects);
        Assert.True(File.Exists(Path.Combine(settingsFolder, SettingsService.FileName + ".bad")));
        Assert.Contains("corrupt", File.ReadAllText(logger.ActiveFilePath));
    }

    /// <summary>
    ///     Test that AI provider choices made in the settings dialog are stored with the opt-in tools off by
    ///     default, and restored on the next run.
    /// </summary>
    [Fact]
    public void CanvasPilot_ConfigureAiProvider_DialogOk_StoresChoicesAndKeepsToolsOptIn()
    {
        // Arrange: the settings dialog over the AI provider page, as the application composes it
        var settingsFolder = PathHelpers.SafePathCombine(_tempRoot, "CanvasPilot");
        var service = new SettingsService(settingsFolder);
        var page = new AiProviderSettingsPageViewModel();
        var dialog = new SettingsDialogViewModel(service, [page]);
        page.SelectedProvider = page.Providers[1];
        page.ModelName = "llama3";
        page.ContextWindowText = "32768";
        page.OllamaEndpoint = "http://ollama.example.test:11434";

        // Act: the user chooses OK, and a later run loads the settings
        dialog.OkCommand.Execute(null);
        var restored = new SettingsService(settingsFolder).Load().AiProvider;

        // Assert: the choices were stored, and unrestricted agent tools remain off because the user never opted in
        Assert.Equal(AiProviderKind.Ollama, restored.Provider);
        Assert.Equal("llama3", restored.ModelName);
        Assert.Equal(32768, restored.ContextWindowTokens);
        Assert.Equal("http://ollama.example.test:11434", restored.OllamaEndpoint);
        Assert.False(restored.UnrestrictedAgentTools);
    }

    /// <summary>
    ///     Test that a user can create a project, close it, and open it again from the recent projects list, and
    ///     that an unusable project file is refused with a message.
    /// </summary>
    [Fact]
    public async Task CanvasPilot_ManageProjects_NewProject_CreatesReopensAndRemembersProject()
    {
        // Arrange: the application's services composed as at startup
        var token = TestContext.Current.CancellationToken;
        var logger = new RollingFileLogger(PathHelpers.SafePathCombine(_tempRoot, "logs"));
        var projectService = new ProjectService(logger);
        var settingsFolder = PathHelpers.SafePathCombine(_tempRoot, "CanvasPilot");
        var settings = new SettingsService(settingsFolder, logger);
        var recent = new RecentProjects(settings);
        var parent = Directory.CreateDirectory(PathHelpers.SafePathCombine(_tempRoot, "Documents")).FullName;

        // Act: create a project, remember it, close it, and open it from the list; then try a damaged file
        var created = await projectService.CreateAsync(parent, "Launch Deck", token);
        recent.Add(created.Project!.FilePath);
        projectService.Close();
        var reloaded = new SettingsService(settingsFolder);
        reloaded.Load();
        var fromList = await projectService.OpenAsync(new RecentProjects(reloaded).GetRecent()[0], token);
        var damaged = Path.Combine(parent, "Damaged.canvaspilot");
        await File.WriteAllTextAsync(damaged, "not json", token);
        var refused = await projectService.OpenAsync(damaged, token);

        // Assert: the project reopened from the list, and the damaged file was refused without changing it
        Assert.True(fromList.Succeeded, fromList.Error);
        Assert.Equal("Launch Deck", fromList.Project!.Name);
        Assert.Same(fromList.Project, projectService.Current);
        Assert.False(refused.Succeeded);
        Assert.Same(fromList.Project, projectService.Current);
        Assert.Contains("Damaged.canvaspilot", await File.ReadAllTextAsync(logger.ActiveFilePath, token));
    }

    /// <summary>
    ///     Test that the project explorer lists the image source files of the open project, so the user can pick
    ///     one to work on.
    /// </summary>
    [Fact]
    public async Task CanvasPilot_BrowseProjectImages_OpenProject_ListsImageSourceFiles()
    {
        // Arrange: a created project that holds two images and other content
        var token = TestContext.Current.CancellationToken;
        var parent = Directory.CreateDirectory(PathHelpers.SafePathCombine(_tempRoot, "Documents")).FullName;
        var projectService = new ProjectService();
        var project = (await projectService.CreateAsync(parent, "Brochure", token)).Project!;
        await File.WriteAllTextAsync(Path.Combine(project.FolderPath, "cover.svg"), "<svg/>", token);
        await File.WriteAllTextAsync(Path.Combine(project.FolderPath, "chart.svg"), "<svg/>", token);
        await File.WriteAllTextAsync(Path.Combine(project.FolderPath, "notes.txt"), "notes", token);
        using var explorer = new ProjectExplorerViewModel(new CanvasNetImageSourceFilter(), new FakeRefreshScheduler());

        // Act: show the open project in the explorer and select an image
        explorer.Open(projectService.Current!.FolderPath);
        explorer.SelectFile(Path.Combine(project.FolderPath, "cover.svg"));

        // Assert: the images are listed, the other content is not, and the selection is available
        Assert.Equal(["chart.svg", "cover.svg"], explorer.Items.Select(item => item.Name));
        Assert.Equal(Path.Combine(project.FolderPath, "cover.svg"), explorer.SelectedFile);
    }

    /// <summary>
    ///     Test that the application starts on every supported platform: the units are composed exactly as at
    ///     startup, without a window, the settings are loaded, the start is logged, and the shell is ready.
    /// </summary>
    [Fact]
    public void CanvasPilot_Startup_ComposeApplication_ProducesWorkingShell()
    {
        // Arrange: per-user folders under a temporary data root, as the application resolves them
        var logger = new RollingFileLogger(PathHelpers.SafePathCombine(_tempRoot, Path.Combine("CanvasPilot", "logs")));
        var settings = new SettingsService(PathHelpers.SafePathCombine(_tempRoot, "CanvasPilot"), logger);

        // Act: compose the application as App does at startup, then shut it down
        var composition = ApplicationComposition.Create(logger, settings, new FakeRefreshScheduler());
        var shell = composition.Shell;
        var title = shell.Title;
        var commandsReady = shell.NewProjectCommand.CanExecute(null) && shell.OpenProjectCommand.CanExecute(null)
            && shell.OpenSettingsCommand.CanExecute(null) && shell.ShowAboutCommand.CanExecute(null) && shell.ExitCommand.CanExecute(null);
        var projectCommandsOff = !shell.SaveProjectCommand.CanExecute(null) && !shell.CloseProjectCommand.CanExecute(null);
        composition.Dispose();

        // Assert: the shell is titled and idle, the settings are in effect, and the start and stop were logged
        Assert.Equal("CanvasPilot", title);
        Assert.True(commandsReady);
        Assert.True(projectCommandsOff);
        Assert.False(shell.HasProject);
        Assert.Equal(new AppSettings().AiProvider, composition.Settings.Current.AiProvider);
        var log = File.ReadAllText(logger.ActiveFilePath);
        Assert.Contains("Application started.", log, StringComparison.Ordinal);
        Assert.Contains("Application stopped.", log, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Test that a user can work from the main window shell: create a project, see its image files in the
    ///     explorer, find it in the recent projects, and change a setting that survives a restart.
    /// </summary>
    [Fact]
    public async Task CanvasPilot_DesktopShell_CreateProject_ExplorerRecentProjectsAndSettingsRoundTrip()
    {
        // Arrange: the application composed on temporary folders, with a user who creates a project and edits settings
        var token = TestContext.Current.CancellationToken;
        var documents = Directory.CreateDirectory(PathHelpers.SafePathCombine(_tempRoot, "Documents")).FullName;
        var logger = new RollingFileLogger(PathHelpers.SafePathCombine(_tempRoot, "logs"));
        var settingsFolder = PathHelpers.SafePathCombine(_tempRoot, "CanvasPilot");
        var dialogs = new FakeDialogService
        {
            OnNewProject = dialog =>
            {
                dialog.ProjectName = "Menu Board";
                dialog.OkCommand.Execute(null);
            },
            OnSettings = dialog =>
            {
                var page = (AiProviderSettingsPageViewModel)dialog.Pages[0];
                page.ModelName = "shell-model";
                dialog.OkCommand.Execute(null);
            },
        };
        var composition = ApplicationComposition.Create(logger, new SettingsService(settingsFolder, logger), new FakeRefreshScheduler(), defaultProjectsFolder: documents);
        composition.Shell.AttachDialogService(dialogs);

        // Act: create the project, add images, refresh the explorer, change a setting, and restart the application
        await composition.Shell.NewProjectCommand.ExecuteAsync(null);
        var folder = composition.Shell.CurrentProject!.FolderPath;
        await File.WriteAllTextAsync(Path.Combine(folder, "menu.svg"), "<svg/>", token);
        await File.WriteAllTextAsync(Path.Combine(folder, "notes.txt"), "notes", token);
        composition.Shell.Workspace.Explorer.Refresh();
        var title = composition.Shell.Title;
        var listed = composition.Shell.Workspace.Explorer.Items.Select(item => item.Name).ToArray();
        await composition.Shell.OpenSettingsCommand.ExecuteAsync(null);
        composition.Dispose();
        using var restarted = ApplicationComposition.Create(logger, new SettingsService(settingsFolder, logger), new FakeRefreshScheduler());

        // Assert: the title named the project, only the image was listed, the project is in the recent menu after
        // the restart, and the changed setting was restored
        Assert.Equal("Menu Board - CanvasPilot", title);
        Assert.Equal(["menu.svg"], listed);
        Assert.Equal(["Menu Board"], restarted.Shell.RecentProjects.Select(item => item.Name));
        Assert.Equal("shell-model", restarted.Settings.Current.AiProvider.ModelName);
    }
}
