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
using DemaConsulting.CanvasPilot.ComposerSubsystem;
using DemaConsulting.CanvasPilot.LoggingSubsystem;
using DemaConsulting.CanvasPilot.PayloadSubsystem;
using DemaConsulting.CanvasPilot.PreviewSubsystem;
using DemaConsulting.CanvasPilot.ProjectSubsystem;
using DemaConsulting.CanvasPilot.SettingsSubsystem;
using DemaConsulting.CanvasPilot.SpeechSubsystem;
using DemaConsulting.CanvasPilot.UtilitiesSubsystem;
using DemaConsulting.Speech.ModelManagementSubsystem;

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
    ///     Test that a user can rename and delete image source files from the project explorer, is asked before a
    ///     file is deleted, and is told when a change is refused.
    /// </summary>
    [Fact]
    public async Task CanvasPilot_ManageProjectFiles_RenameAndDelete_UpdateFilesAndReportProblems()
    {
        // Arrange: the application with a project that holds two images, the first selected
        var token = TestContext.Current.CancellationToken;
        var (composition, dialogs) = await ComposeWithProjectAsync();
        var folder = composition.Shell.CurrentProject!.FolderPath;
        await File.WriteAllTextAsync(Path.Combine(folder, "a.svg"), "<svg/>", token);
        await File.WriteAllTextAsync(Path.Combine(folder, "b.svg"), "<svg/>", token);
        var workspace = composition.Shell.Workspace;
        workspace.Explorer.Refresh();
        workspace.Explorer.SelectFile(Path.Combine(folder, "a.svg"));

        // Act: refuse a name that is not an image source file, rename to a good name, decline then confirm the delete
        dialogs.PromptResult = "notes.txt";
        await workspace.RenameSelectedCommand.ExecuteAsync(null);
        var refused = dialogs.Messages.Count;
        dialogs.PromptResult = "cover.svg";
        await workspace.RenameSelectedCommand.ExecuteAsync(null);
        dialogs.ConfirmResult = false;
        await workspace.DeleteSelectedCommand.ExecuteAsync(null);
        var keptWhenDeclined = File.Exists(Path.Combine(folder, "cover.svg"));
        dialogs.ConfirmResult = true;
        await workspace.DeleteSelectedCommand.ExecuteAsync(null);
        var listed = workspace.Explorer.Items.Select(item => item.Name).ToArray();
        composition.Dispose();

        // Assert: the bad name was reported, the rename and confirmed delete happened, and the decline kept the file
        Assert.Equal(1, refused);
        Assert.True(keptWhenDeclined);
        Assert.Equal(2, dialogs.Confirmations.Count);
        Assert.False(File.Exists(Path.Combine(folder, "a.svg")));
        Assert.False(File.Exists(Path.Combine(folder, "cover.svg")));
        Assert.True(File.Exists(Path.Combine(folder, "b.svg")));
        Assert.Equal(["b.svg"], listed);
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
    ///     Test that a user who selects an image file in the project sees it rendered, and sees why a broken file
    ///     could not be drawn, without any window.
    /// </summary>
    [Fact]
    public async Task CanvasPilot_PreviewImages_SelectedFile_ShowsRenderedPictureOrError()
    {
        // Arrange: the application with a created project that holds a good and a broken SVG
        var token = TestContext.Current.CancellationToken;
        var (composition, _) = await ComposeWithProjectAsync();
        var folder = composition.Shell.CurrentProject!.FolderPath;
        await File.WriteAllTextAsync(Path.Combine(folder, "logo.svg"), "<svg xmlns='http://www.w3.org/2000/svg' width='32' height='16'><rect width='32' height='16' fill='#00ff00'/></svg>", token);
        await File.WriteAllTextAsync(Path.Combine(folder, "broken.svg"), "<svg><not closed", token);
        composition.Shell.Workspace.Explorer.Refresh();

        // Act: select the good file, then the broken one
        composition.Shell.Workspace.Explorer.SelectFile(Path.Combine(folder, "logo.svg"));
        await composition.Preview.RenderCompletion;
        var shown = (composition.Preview.HasImage, composition.Preview.Image?.Width, composition.Preview.Image?.Height, composition.Shell.ImageExporter.CanExport);
        composition.Shell.Workspace.Explorer.SelectFile(Path.Combine(folder, "broken.svg"));
        await composition.Preview.RenderCompletion;
        var failed = (composition.Preview.HasError, composition.Preview.Error?.Code, composition.Shell.ImageExporter.CanExport);
        composition.Dispose();

        // Assert: the good file was rendered at its size and could be exported; the broken one gave an error and no picture
        Assert.Equal((true, 32, 16, true), shown);
        Assert.Equal((true, "cannot-draw", false), failed);
    }

    /// <summary>
    ///     Test that a user can export the rendered picture as a PNG file from the Image menu command.
    /// </summary>
    [Fact]
    public async Task CanvasPilot_ExportImages_RenderedSvg_WritesValidPngFile()
    {
        // Arrange: the application with a project, a rendered SVG, and a user who chooses where to save
        var token = TestContext.Current.CancellationToken;
        var (composition, dialogs) = await ComposeWithProjectAsync();
        var folder = composition.Shell.CurrentProject!.FolderPath;
        await File.WriteAllTextAsync(Path.Combine(folder, "logo.svg"), "<svg xmlns='http://www.w3.org/2000/svg' width='32' height='16'><rect width='32' height='16' fill='#00ff00'/></svg>", token);
        composition.Shell.Workspace.Explorer.Refresh();
        composition.Shell.Workspace.Explorer.SelectFile(Path.Combine(folder, "logo.svg"));
        await composition.Preview.RenderCompletion;
        var target = Path.Combine(_tempRoot, "exported.png");
        dialogs.SaveFileToPick = target;

        // Act: run the export command of the Image menu
        var canExport = composition.Shell.ExportPngCommand.CanExecute(null);
        await composition.Shell.ExportPngCommand.ExecuteAsync(null);
        composition.Dispose();

        // Assert: the command was enabled and the file is a PNG of the picture's size
        Assert.True(canExport);
        var png = await File.ReadAllBytesAsync(target, token);
        Assert.Equal([0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A], png[..8]);
        Assert.Equal(32, System.Buffers.Binary.BinaryPrimitives.ReadInt32BigEndian(png.AsSpan(16, 4)));
        Assert.Equal(16, System.Buffers.Binary.BinaryPrimitives.ReadInt32BigEndian(png.AsSpan(20, 4)));
    }

    /// <summary>
    ///     Test that a user who selects a diagram sees a picture that is pinned to the exact file and pixels shown:
    ///     the picture carries fingerprints of the file and the pixels, and the positions of the diagram's elements, and
    ///     all of them change when the file is edited.
    /// </summary>
    [Fact]
    public async Task CanvasPilot_BuildMessagePayload_SelectedDiagram_PinsPictureToFileAndReportsElements()
    {
        // Arrange: the application with a project that holds a diagram
        var token = TestContext.Current.CancellationToken;
        var (composition, _) = await ComposeWithProjectAsync();
        var folder = composition.Shell.CurrentProject!.FolderPath;
        var diagram = Path.Combine(folder, "flow.mmd");
        await File.WriteAllTextAsync(diagram, "flowchart LR\n  A[Start] --> B[End]\n", token);
        composition.Shell.Workspace.Explorer.Refresh();

        // Act: select the diagram, show another file, edit the diagram, and select it again
        composition.Shell.Workspace.Explorer.SelectFile(diagram);
        await composition.Preview.RenderCompletion;
        var before = composition.Preview.Image;
        await File.WriteAllTextAsync(Path.Combine(folder, "other.svg"), "<svg xmlns='http://www.w3.org/2000/svg' width='8' height='8'/>", token);
        composition.Shell.Workspace.Explorer.Refresh();
        composition.Shell.Workspace.Explorer.SelectFile(Path.Combine(folder, "other.svg"));
        await composition.Preview.RenderCompletion;
        await File.WriteAllTextAsync(diagram, "flowchart LR\n  A[Start] --> B[Finish]\n", token);
        composition.Shell.Workspace.Explorer.SelectFile(diagram);
        await composition.Preview.RenderCompletion;
        var after = composition.Preview.Image;
        composition.Dispose();

        // Assert: each picture names its file, holds fingerprints, and reports the nodes with their text; the edit
        // changed the fingerprints, the snapshot key, and the text of the second node
        Assert.NotNull(before);
        Assert.NotNull(after);
        Assert.Equal(diagram, before.SourcePath);
        Assert.Equal(64, before.SourceFileHash?.Length);
        Assert.Equal(64, before.ContentHash.Length);
        Assert.Equal(["Start", "End"], before.Positions.Where(position => position.Kind == "node").Select(node => node.Text));
        Assert.All(before.Positions, position => Assert.NotNull(position.Box));
        Assert.Equal(["Start", "Finish"], after.Positions.Where(position => position.Kind == "node").Select(node => node.Text));
        Assert.NotEqual(before.SourceFileHash, after.SourceFileHash);
        Assert.NotEqual(before.ContentHash, after.ContentHash);
        Assert.NotEqual(before.SnapshotKey, after.SnapshotKey);
    }

    /// <summary>
    ///     Test that the picture the user sees has a layer over it, in the workspace's preview pane, that marks can be
    ///     placed on, and that the layer stays while another picture is shown.
    /// </summary>
    [Fact]
    public async Task CanvasPilot_MarkImage_ShownPicture_HasOverlayLayerThatSurvivesNewPictures()
    {
        // Arrange: the application with a project that holds two pictures, and some content for the layer
        var token = TestContext.Current.CancellationToken;
        var (composition, _) = await ComposeWithProjectAsync();
        var folder = composition.Shell.CurrentProject!.FolderPath;
        await File.WriteAllTextAsync(Path.Combine(folder, "a.svg"), "<svg xmlns='http://www.w3.org/2000/svg' width='32' height='16'/>", token);
        await File.WriteAllTextAsync(Path.Combine(folder, "b.svg"), "<svg xmlns='http://www.w3.org/2000/svg' width='8' height='8'/>", token);
        composition.Shell.Workspace.Explorer.Refresh();
        var layer = new object();

        // Act: put content in the layer of the workspace's preview pane, then show one picture and then the other
        var pane = Assert.IsType<PreviewPaneViewModel>(composition.Shell.Workspace.Preview);
        pane.Overlay = layer;
        composition.Shell.Workspace.Explorer.SelectFile(Path.Combine(folder, "a.svg"));
        await composition.Preview.RenderCompletion;
        var first = (pane.HasOverlay, pane.Image?.Width);
        composition.Shell.Workspace.Explorer.SelectFile(Path.Combine(folder, "b.svg"));
        await composition.Preview.RenderCompletion;
        var second = (pane.HasOverlay, pane.Image?.Width);
        composition.Dispose();

        // Assert: the pane in the workspace is the application's pane, and its layer holds the content over both pictures
        Assert.Same(composition.Preview, pane);
        Assert.Same(layer, pane.Overlay);
        Assert.Equal((true, 32), first);
        Assert.Equal((true, 8), second);
    }

    /// <summary>
    ///     Test that a user who marks the shown diagram gets numbered marks that are pinned to the exact picture they were
    ///     drawn on: they hold its pixels and fingerprints, they are not shown on the picture of the edited file, and the
    ///     parts of the source under a mark are the ones the renderer reported for that picture.
    /// </summary>
    [Fact]
    public async Task CanvasPilot_MarkImage_MarksOnShownPicture_AreNumberedAndStayOnTheirPicture()
    {
        // Arrange: the application with a project that holds a diagram, which is shown
        var token = TestContext.Current.CancellationToken;
        var (composition, _) = await ComposeWithProjectAsync();
        var folder = composition.Shell.CurrentProject!.FolderPath;
        var diagram = Path.Combine(folder, "flow.mmd");
        await File.WriteAllTextAsync(diagram, "flowchart LR\n  A[Start] --> B[End]\n", token);
        composition.Shell.Workspace.Explorer.Refresh();
        composition.Shell.Workspace.Explorer.SelectFile(diagram);
        await composition.Preview.RenderCompletion;
        var before = composition.Preview.Image!;
        var registry = new MarkRegistry();
        var start = before.Positions.First(position => position is { Kind: "node", Text: "Start" }).Box!.Value;

        // Act: draw two marks, the first around the node 'Start' with a note, edit the file, show the new picture, and
        // draw a third mark
        var first = registry.Add(before, MarkKind.Rectangle, [new RenderPoint(start.X - 2, start.Y - 2), new RenderPoint(start.X + start.Width + 2, start.Y + start.Height + 2)], "the start");
        var second = registry.Add(before, MarkKind.Arrow, [new RenderPoint(1, 1), new RenderPoint(start.X + 1, start.Y + 1)]);
        var found = MarkSourceResolver.Resolve(first);
        await File.WriteAllTextAsync(diagram, "flowchart LR\n  A[Start] --> B[Finish]\n", token);
        await File.WriteAllTextAsync(Path.Combine(folder, "other.svg"), "<svg xmlns='http://www.w3.org/2000/svg' width='8' height='8'/>", token);
        composition.Shell.Workspace.Explorer.Refresh();
        composition.Shell.Workspace.Explorer.SelectFile(Path.Combine(folder, "other.svg"));
        await composition.Preview.RenderCompletion;
        composition.Shell.Workspace.Explorer.SelectFile(diagram);
        await composition.Preview.RenderCompletion;
        var after = composition.Preview.Image!;
        var third = registry.Add(after, MarkKind.Ellipse, [new RenderPoint(start.X, start.Y), new RenderPoint(start.X + start.Width, start.Y + start.Height)]);
        var chips = new[] { first.Id, second.Id, third.Id };
        composition.Dispose();

        // Assert: numbered one to three; the first two hold the old picture, the third the new one; each picture shows
        // only its own marks while all three belong to the message; the node was found under the first mark
        Assert.Equal([1, 2, 3], new[] { first.Number, second.Number, third.Number });
        Assert.Equal("the start", first.Note);
        Assert.Same(before, first.Snapshot);
        Assert.Same(after, third.Snapshot);
        Assert.NotEqual(before.ContentHash, after.ContentHash);
        Assert.Equal([first, second], registry.VisibleMarks(chips, before.SnapshotKey));
        Assert.Equal([third], registry.VisibleMarks(chips, after.SnapshotKey));
        Assert.Equal(3, registry.ActiveMarks(chips).Count);
        Assert.Contains(found.Elements, element => element is { Kind: "node", Text: "Start" });
    }

    /// <summary>
    ///     Test that a user who sends a message with marks on the shown diagram and an attached file gets a payload for the
    ///     assistant that holds the text with its references, the picture with the marks drawn and numbered, the area
    ///     around each mark, the geometry of each mark in picture pixels, the parts of the source under each mark, and the
    ///     attachment with its path in the project, and that the draft is gone.
    /// </summary>
    [Fact]
    public async Task CanvasPilot_BuildMessagePayload_MermaidWithMarks_ContainsImagesCropsGeometryAndSourceElements()
    {
        // Arrange: the application with a project that holds a diagram, which is shown, and a file outside the project
        var token = TestContext.Current.CancellationToken;
        var (composition, _) = await ComposeWithProjectAsync();
        var folder = composition.Shell.CurrentProject!.FolderPath;
        var diagram = Path.Combine(folder, "flow.mmd");
        await File.WriteAllTextAsync(diagram, "flowchart LR\n  A[Start] --> B[End]\n", token);
        var brief = Path.Combine(Directory.CreateDirectory(Path.Combine(_tempRoot, "external")).FullName, "brief.txt");
        await File.WriteAllTextAsync(brief, "the brief", token);
        composition.Shell.Workspace.Explorer.Refresh();
        composition.Shell.Workspace.Explorer.SelectFile(diagram);
        await composition.Preview.RenderCompletion;
        var picture = composition.Preview.Image!;
        var start = picture.Positions.First(position => position is { Kind: "node", Text: "Start" }).Box!.Value;
        var end = picture.Positions.First(position => position is { Kind: "node", Text: "End" }).Box!.Value;

        // Act: mark both nodes, compose a message with text, both marks, the file, and dictated text, and send it the
        // way the chat will: take the snapshot, put the attachment into the project, and build the payload
        var registry = new MarkRegistry();
        var first = registry.Add(picture, MarkKind.Rectangle, [new RenderPoint(start.X - 4, start.Y - 4), new RenderPoint(start.X + start.Width + 4, start.Y + start.Height + 4)], "bigger");
        var second = registry.Add(picture, MarkKind.Ellipse, [new RenderPoint(end.X - 4, end.Y - 4), new RenderPoint(end.X + end.Width + 4, end.Y + end.Height + 4)]);
        var document = new ComposerDocument();
        var history = new ComposerHistory(document);
        history.Execute(ComposerEdit.TypeText(document, 0, "Swap "));
        history.Execute(ComposerEdit.AddMark(document, document.Length, new MarkChip(first.Id, first.Number)));
        history.Execute(ComposerEdit.TypeText(document, document.Length, " and "));
        history.Execute(ComposerEdit.AddMark(document, document.Length, new MarkChip(second.Id, second.Number)));
        history.Execute(ComposerEdit.Drop(document, document.Length, [new TextSegment(" see ", TextOrigin.Typed), new FileChip(brief)]));
        history.Execute(ComposerEdit.Dictate(document, document.Length, " please"));
        var snapshot = history.CompleteSend();
        var staging = new AttachmentStore(folder).StageForSend([.. snapshot.Chips.OfType<AttachmentChip>()]);
        var renderer = new ImageRenderService();
        var payload = await new PayloadBuilder(new MarkImageComposer(renderer)).BuildAsync(snapshot, registry.Marks, staging.Attachments, token);
        composition.Dispose();

        // Assert: the text and the attachment; one image with the facts of the shown picture, an annotated picture of its
        // size, and two marks with their geometry, note, area, and source elements; the draft is empty
        Assert.True(staging.Succeeded, staging.Failure?.Reason);
        Assert.Equal("Swap [Mark 1] and [Mark 2] see [Attachment 1: brief.txt] please", payload.Text);
        Assert.Equal("attachments/brief.txt", Assert.Single(payload.Attachments).ProjectRelativePath);
        Assert.True(File.Exists(Path.Combine(folder, "attachments", "brief.txt")));
        var image = Assert.Single(payload.Images);
        Assert.Equal((picture.SourcePath, picture.SourceFileHash, picture.ContentHash), (image.SourcePath, image.SourceFileHash, image.ContentHash));
        using var annotated = PayloadTestSupport.Decode(image.AnnotatedPng);
        Assert.Equal((picture.Width, picture.Height), (annotated.Width, annotated.Height));
        Assert.Equal([1, 2], image.Marks.Select(mark => mark.Number));
        Assert.Equal([MarkKind.Rectangle, MarkKind.Ellipse], image.Marks.Select(mark => mark.Kind));
        Assert.Equal(first.Geometry.Points, image.Marks[0].Points);
        Assert.Equal("bigger", image.Marks[0].Note);
        Assert.All(image.Marks, mark =>
        {
            using var crop = PayloadTestSupport.Decode(mark.CropPng);
            Assert.Equal((int)mark.CropBox.Width, crop.Width);
            Assert.Equal((int)mark.CropBox.Height, crop.Height);
        });
        Assert.Equal("Start", Assert.Single(image.Marks[0].SourceElements, element => element.Kind == "node").Text);
        Assert.Equal("End", Assert.Single(image.Marks[1].SourceElements, element => element.Kind == "node").Text);
        Assert.Equal(0, document.Length);
        Assert.Equal(0, history.UndoCount);
    }

    /// <summary>
    ///     Test that a user who drags the shown picture out of the preview gives the other program a PNG file of exactly
    ///     the picture shown, that no file is offered when the pointer is used for something else, and that the file
    ///     does not outlive the application.
    /// </summary>
    [Fact]
    public async Task CanvasPilot_DragImageOut_ShownPicture_ProvidesPngFile()
    {
        // Arrange: the application with a project that holds an SVG, and a folder for dragged pictures that holds a
        // file of an earlier run
        var token = TestContext.Current.CancellationToken;
        var dragFolder = Directory.CreateDirectory(PathHelpers.SafePathCombine(_tempRoot, "dragged")).FullName;
        var earlier = Path.Combine(dragFolder, "old-0123abcd.png");
        await File.WriteAllBytesAsync(earlier, [1, 2, 3], token);
        var (composition, _) = await ComposeWithProjectAsync(dragFolder);
        var folder = composition.Shell.CurrentProject!.FolderPath;
        await File.WriteAllTextAsync(Path.Combine(folder, "logo.svg"), "<svg xmlns='http://www.w3.org/2000/svg' width='32' height='16'><rect width='32' height='16' fill='#00ff00'/></svg>", token);
        composition.Shell.Workspace.Explorer.Refresh();
        var earlierRemovedAtStart = !File.Exists(earlier);
        var withoutPicture = composition.Preview.PrepareDragFile();

        // Act: show the picture, prepare it for dragging, once while an annotation tool uses the pointer, and close
        composition.Shell.Workspace.Explorer.SelectFile(Path.Combine(folder, "logo.svg"));
        await composition.Preview.RenderCompletion;
        var path = composition.Preview.PrepareDragFile();
        var bytes = path is null ? [] : await File.ReadAllBytesAsync(path, token);
        composition.Preview.IsDragOutEnabled = false;
        var whileToolActive = composition.Preview.PrepareDragFile();
        composition.Dispose();

        // Assert: the file of the earlier run was removed at start; nothing is offered without a picture or while the
        // tool is active; the offered file is a PNG of the picture in the drag folder; closing removes it
        Assert.True(earlierRemovedAtStart);
        Assert.Null(withoutPicture);
        Assert.Null(whileToolActive);
        Assert.NotNull(path);
        Assert.Equal(dragFolder, Path.GetDirectoryName(path));
        Assert.StartsWith("logo-", Path.GetFileName(path), StringComparison.Ordinal);
        Assert.Equal([0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A], bytes[..8]);
        Assert.Equal(32, System.Buffers.Binary.BinaryPrimitives.ReadInt32BigEndian(bytes.AsSpan(16, 4)));
        Assert.Equal(16, System.Buffers.Binary.BinaryPrimitives.ReadInt32BigEndian(bytes.AsSpan(20, 4)));
        Assert.False(File.Exists(path));
    }

    /// <summary>
    ///     Test that a user who types, dictates, and attaches things composes one message in which everything stays in
    ///     the order it was added, with the kind of each part kept.
    /// </summary>
    [Fact]
    public void CanvasPilot_ComposeMessage_TextDictationAndChips_AreOneOrderedMessage()
    {
        // Arrange: an empty message and its history, as the chat panel will hold them
        var document = new ComposerDocument();
        var history = new ComposerHistory(document);

        // Act: type, dictate a block, attach a file and a folder in one drop, add a mark, and type more
        history.Execute(ComposerEdit.TypeText(document, document.Length, "Look at "));
        history.Execute(ComposerEdit.Dictate(document, document.Length, "the second box"));
        history.Execute(ComposerEdit.Drop(document, document.Length, [new FileChip(@"C:\notes\brief.txt"), new FolderChip(@"C:\notes\photos")]));
        history.Execute(ComposerEdit.AddMark(document, 8, new MarkChip(Guid.NewGuid(), 1)));
        history.Execute(ComposerEdit.TypeText(document, document.Length, " please"));
        var message = document.Snapshot();

        // Assert: the parts are in the order they were added, the mark sits where the caret was, and origins are kept
        Assert.Collection(
            message.Segments,
            segment => Assert.Equal(new TextSegment("Look at ", TextOrigin.Typed), segment),
            segment => Assert.IsType<MarkChip>(segment),
            segment => Assert.Equal(new TextSegment("the second box", TextOrigin.Dictated), segment),
            segment => Assert.IsType<FileChip>(segment),
            segment => Assert.IsType<FolderChip>(segment),
            segment => Assert.Equal(new TextSegment(" please", TextOrigin.Typed), segment));
        Assert.Equal(8 + 1 + 14 + 2 + 7, message.Length);
    }

    /// <summary>
    ///     Test that a user can undo and redo the message one action at a time, that sending clears what can be undone,
    ///     and that nothing of the draft is written to disk.
    /// </summary>
    [Fact]
    public void CanvasPilot_UndoRedoMessage_StepsOfEachAction_UndoAndRedoOneAtATimeAndSendClears()
    {
        // Arrange: a draft of a typed character, a dictated block, a drop of two items, and a deleted chip
        var folderBefore = Directory.GetFileSystemEntries(_tempRoot).Length;
        var document = new ComposerDocument();
        var history = new ComposerHistory(document);
        history.Execute(ComposerEdit.TypeText(document, 0, "a"));
        history.Execute(ComposerEdit.Dictate(document, 1, " dictated block of words"));
        history.Execute(ComposerEdit.Drop(document, document.Length, [new FileChip(@"C:\a.txt"), new ImageChip(@"C:\b.png")]));
        history.Execute(ComposerEdit.DeleteChip(document, document.Length - 1));

        // Act: undo the deleted chip, the drop, and the dictation, redo the dictation, then send
        history.Undo();
        var afterUndoChip = document.Chips.Count();
        history.Undo();
        var afterUndoDrop = document.Chips.Count();
        history.Undo();
        var afterUndoDictation = document.FlatText;
        history.Redo();
        var afterRedoDictation = document.FlatText;
        history.CompleteSend();

        // Assert: each action was undone and redone as one step, sending left nothing to undo, and no file was written
        Assert.Equal(2, afterUndoChip);
        Assert.Equal(0, afterUndoDrop);
        Assert.Equal("a", afterUndoDictation);
        Assert.Equal("a dictated block of words", afterRedoDictation);
        Assert.False(history.CanUndo);
        Assert.False(history.CanRedo);
        Assert.Equal(0, document.Length);
        Assert.Equal(folderBefore, Directory.GetFileSystemEntries(_tempRoot).Length);
    }

    /// <summary>
    ///     Test that a user who drops files, a folder, and a program, and pastes a screenshot, gets chips without
    ///     anything being copied while drafting, that undo removes them, and that sending copies the things into the
    ///     project's attachments folder unchanged, without the explorer listing them or the draft folder keeping
    ///     anything.
    /// </summary>
    [Fact]
    public async Task CanvasPilot_AttachToMessage_DroppedAndPastedContent_IsCopiedIntoProjectAtSend()
    {
        // Arrange: a created project with an image, and content from outside the project: a file, a folder, a program
        var token = TestContext.Current.CancellationToken;
        var parent = Directory.CreateDirectory(PathHelpers.SafePathCombine(_tempRoot, "Documents")).FullName;
        var projectService = new ProjectService();
        var project = (await projectService.CreateAsync(parent, "Poster", token)).Project!;
        await File.WriteAllTextAsync(Path.Combine(project.FolderPath, "cover.svg"), "<svg/>", token);
        var outside = Directory.CreateDirectory(PathHelpers.SafePathCombine(_tempRoot, "outside")).FullName;
        var brief = Path.Combine(outside, "brief.txt");
        var program = Path.Combine(outside, "setup.exe");
        var folder = Directory.CreateDirectory(Path.Combine(outside, "photos")).FullName;
        await File.WriteAllTextAsync(brief, "the brief", token);
        await File.WriteAllBytesAsync(program, [0x4D, 0x5A, 1, 2], token);
        await File.WriteAllTextAsync(Path.Combine(folder, "a.txt"), "photo notes", token);
        byte[] screenshot = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 5, 6];
        var store = new AttachmentStore(project.FolderPath);
        var intake = new AttachmentIntake(store);
        var document = new ComposerDocument();
        var history = new ComposerHistory(document);

        // Act: drop and paste in one operation, check that nothing is copied, undo, redo, and send
        var accepted = intake.Accept(new DroppedContent { Paths = [brief, program, folder], Images = [screenshot] });
        history.Execute(ComposerEdit.Drop(document, 0, accepted.Segments));
        var attachmentsWhileDrafting = Directory.Exists(store.AttachmentsFolder);
        history.Undo();
        var chipsAfterUndo = document.Chips.Count();
        history.Redo();
        var chipsAfterRedo = document.Chips.Count();
        var result = store.StageForSend([.. history.CompleteSend().Chips.OfType<AttachmentChip>()]);
        using var explorer = new ProjectExplorerViewModel(new CanvasNetImageSourceFilter(), new FakeRefreshScheduler());
        explorer.Open(project.FolderPath);

        // Assert: nothing was copied while drafting; the chips came and went as one step; everything is in attachments
        // byte for byte; the explorer shows only the project's image; the draft folder holds nothing
        Assert.False(attachmentsWhileDrafting);
        Assert.Equal(0, chipsAfterUndo);
        Assert.Equal(4, chipsAfterRedo);
        Assert.True(result.Succeeded);
        Assert.Equal(4, result.Attachments.Length);
        Assert.Equal("the brief", await File.ReadAllTextAsync(Path.Combine(store.AttachmentsFolder, "brief.txt"), token));
        Assert.Equal(new byte[] { 0x4D, 0x5A, 1, 2 }, await File.ReadAllBytesAsync(Path.Combine(store.AttachmentsFolder, "setup.exe"), token));
        Assert.Equal("photo notes", await File.ReadAllTextAsync(Path.Combine(store.AttachmentsFolder, "photos", "a.txt"), token));
        Assert.Equal(screenshot, await File.ReadAllBytesAsync(Path.Combine(project.FolderPath, result.Attachments[3].ProjectRelativePath), token));
        Assert.Equal(["cover.svg"], explorer.Items.Select(item => item.Name));
        Assert.Empty(Directory.GetFiles(store.PastedImagesFolder));
        Assert.True(File.Exists(brief));
    }

    /// <summary>
    ///     Composes the application on temporary folders and has a user create a project through the New Project dialog.
    /// </summary>
    /// <param name="dragFolder">The folder for pictures dragged out, or <see langword="null" /> for one in the temporary root.</param>
    /// <returns>The composition, whose project is open, and the dialog service attached to its shell.</returns>
    private async Task<(ApplicationComposition Composition, FakeDialogService Dialogs)> ComposeWithProjectAsync(string? dragFolder = null)
    {
        var documents = Directory.CreateDirectory(PathHelpers.SafePathCombine(_tempRoot, "Documents")).FullName;
        var logger = new RollingFileLogger(PathHelpers.SafePathCombine(_tempRoot, "logs"));
        var dialogs = new FakeDialogService
        {
            OnNewProject = dialog =>
            {
                dialog.ProjectName = "Poster";
                dialog.OkCommand.Execute(null);
            },
        };
        var composition = ApplicationComposition.Create(
            logger,
            new SettingsService(PathHelpers.SafePathCombine(_tempRoot, "CanvasPilot"), logger),
            new FakeRefreshScheduler(),
            defaultProjectsFolder: documents,
            dragFolder: dragFolder ?? PathHelpers.SafePathCombine(_tempRoot, "drag"));
        composition.Shell.AttachDialogService(dialogs);
        await composition.Shell.NewProjectCommand.ExecuteAsync(null);
        return (composition, dialogs);
    }

    /// <summary>
    ///     Test that a user can choose audio devices, speech models, and a model setting in the Speech page of the
    ///     settings dialog, with the choices stored by device name and model identifier and restored on the next run,
    ///     using substitute audio and model services so no hardware or download is needed.
    /// </summary>
    [Fact]
    public async Task CanvasPilot_ConfigureSpeech_DialogOk_StoresChoicesAndRestoresThemNextRun()
    {
        // Arrange: the application composed with substitute speech services, and a user who edits the Speech page
        var settingsFolder = PathHelpers.SafePathCombine(_tempRoot, "CanvasPilot");
        var logger = new RollingFileLogger(PathHelpers.SafePathCombine(_tempRoot, "logs"));
        var audio = SpeechTestData.AudioDevices(["Desk mic", "Headset mic"], ["Speakers", "Headset"]);
        var models = SpeechTestData.Models(
            SpeechTestData.Descriptor("stt-fast", SpeechModelRole.Recognition),
            SpeechTestData.Descriptor("tts-warm", SpeechModelRole.Synthesis, SpeechModelState.Downloaded, SpeechTestData.Choice()));
        var dialogs = new FakeDialogService
        {
            OnSettings = dialog =>
            {
                var page = (SpeechSettingsPageViewModel)dialog.Pages[1];
                page.Devices.SelectedCaptureDevice = page.Devices.CaptureDevices.Single(device => device.Name == "Headset mic");
                page.Devices.SelectedPlaybackDevice = page.Devices.PlaybackDevices.Single(device => device.Name == "Headset");
                page.Models.SelectedSttModel = page.Models.SttModels.Single();
                page.Models.SelectedTtsModel = page.Models.TtsModels.Single();
                var voice = (ChoiceParameterViewModel)page.TtsParameters.Parameters[0];
                voice.SelectedOption = voice.Options[0];
                dialog.OkCommand.Execute(null);
            },
        };
        var composition = ApplicationComposition.Create(
            logger,
            new SettingsService(settingsFolder, logger),
            new FakeRefreshScheduler(),
            audioDevices: audio,
            speechModels: models);
        composition.Shell.AttachDialogService(dialogs);

        // Act: open Settings and choose OK, then restart the application and open Settings again
        await composition.Shell.OpenSettingsCommand.ExecuteAsync(null);
        composition.Dispose();
        using var restarted = ApplicationComposition.Create(
            logger,
            new SettingsService(settingsFolder, logger),
            new FakeRefreshScheduler(),
            audioDevices: audio,
            speechModels: models);
        var restoredPage = new SpeechSettingsPageViewModel(audio, models, SpeechTestData.Inline);
        restoredPage.Load(restarted.Settings.Current);

        // Assert: the choices were stored by name and identifier, and the page of the next run shows them
        var speech = restarted.Settings.Current.Speech;
        Assert.Equal("Headset mic", speech.CaptureDevice);
        Assert.Equal("Headset", speech.PlaybackDevice);
        Assert.Equal("stt-fast", speech.SttModelId);
        Assert.Equal("tts-warm", speech.TtsModelId);
        Assert.Equal("a", speech.ModelParameters["tts-warm"]["voice"]);
        Assert.Equal("Headset mic", restoredPage.Devices.CaptureDeviceName);
        Assert.Equal("a", restoredPage.TtsParameters.Parameters[0].ValueText);
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
        var composition = ApplicationComposition.Create(
            logger,
            new SettingsService(settingsFolder, logger),
            new FakeRefreshScheduler(),
            defaultProjectsFolder: documents,
            audioDevices: SpeechTestData.AudioDevices([], []),
            speechModels: SpeechTestData.Models());
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
