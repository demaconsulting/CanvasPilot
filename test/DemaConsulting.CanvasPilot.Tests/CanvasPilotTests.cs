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
using DemaConsulting.CanvasPilot.ChatSubsystem;
using DemaConsulting.CanvasPilot.ComposerSubsystem;
using DemaConsulting.CanvasPilot.KnowledgeSubsystem;
using DemaConsulting.CanvasPilot.LoggingSubsystem;
using DemaConsulting.CanvasPilot.PayloadSubsystem;
using DemaConsulting.CanvasPilot.PreviewSubsystem;
using DemaConsulting.CanvasPilot.ProjectSubsystem;
using DemaConsulting.CanvasPilot.SettingsSubsystem;
using DemaConsulting.CanvasPilot.SpeechSubsystem;
using DemaConsulting.CanvasPilot.UtilitiesSubsystem;
using DemaConsulting.Speech.AudioSubsystem;
using DemaConsulting.Speech.ModelManagementSubsystem;
using DemaConsulting.Speech.RecognitionSubsystem;

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

    /// <summary>
    ///     What the tests composed and must release before the temporary root is removed, in the order it was created.
    /// </summary>
    private readonly List<IDisposable> _disposables = [];

    /// <inheritdoc />
    public void Dispose()
    {
        // Release what was composed, last first, so that a test that failed halfway leaves no file open
        for (var index = _disposables.Count - 1; index >= 0; index--)
        {
            _disposables[index].Dispose();
        }

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
    ///     Test that the knowledge that the application holds can be looked up as an assistant would: the compact index
    ///     fits its budget and names the cards of CanvasNet and of CanvasPilot, the tool answers the index, a card, an
    ///     example, and a search, the skills cover every card, and the cards that are only about the host program are not
    ///     there while the cards for embedding pages and files are, all from the program itself without a network, a
    ///     window, or a language model.
    /// </summary>
    [Fact]
    public void CanvasPilot_ProvideDrawingKnowledge_ComposedCatalog_IndexCardExampleAndSearchWork()
    {
        // Arrange: the catalog of the application, with the units that are built on it
        var catalog = KnowledgeCatalog.Default;
        var prompts = new KnowledgePromptBuilder(catalog);
        var tool = Assert.Single(KnowledgeTools.CreateAll(catalog), t => t.Name == KnowledgeTools.HelpToolName);

        // Act: read the index, then ask the tool as an assistant would, and make the skills
        var index = prompts.BuildIndex();
        var indexAnswer = tool.Invoke("{}");
        var cardAnswer = tool.Invoke("{\"topic\":\"canvaspilot/marks\"}");
        var exampleAnswer = tool.Invoke("{\"example\":\"mark-payload\"}");
        var searchAnswer = tool.Invoke("{\"query\":\"gradient\"}");
        var skills = KnowledgeSkills.Create(catalog).ToList();

        // Assert: the index fits its budget and has a card of each source; the tool answers each kind of request from the
        // catalog; there is a skill for every card; and nothing that is only about the host program is offered
        Assert.True(index.Length <= KnowledgePromptBuilder.IndexBudgetCharacters);
        Assert.Contains("- canvaspilot: ", index, StringComparison.Ordinal);
        Assert.Contains("- svg: ", index, StringComparison.Ordinal);
        Assert.Equal(index, indexAnswer);
        Assert.Contains("canvaspilot/marks", cardAnswer, StringComparison.Ordinal);
        Assert.Contains("```", exampleAnswer, StringComparison.Ordinal);
        Assert.DoesNotContain("No card matches", searchAnswer, StringComparison.Ordinal);
        Assert.Equal(catalog.Cards.Count, skills.Count);
        Assert.Equal(skills.Count, skills.Select(s => s.Name).Distinct(StringComparer.Ordinal).Count());
        Assert.NotNull(catalog.Find("canvaspilot/project-layout"));
        Assert.DoesNotContain(catalog.Cards, card => card.Id is "errors" or "svg/options" or "svg/flatten");
        Assert.NotNull(catalog.Find("pdf"));
        Assert.Contains("\n- pdf: ", index, StringComparison.Ordinal);
        Assert.DoesNotContain("\n- svg/options: ", index, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Test that a user who chooses Drawing Knowledge in the Help menu of the composed application gets a browser over
    ///     the catalog of the application: its tree is the roots of the catalog, a search finds a card, a card shows its
    ///     text and a related card that can be followed, and an example can be copied, without a window.
    /// </summary>
    [Fact]
    public async Task CanvasPilot_BrowseDrawingKnowledge_HelpMenuCommand_ShowsBrowserOnTheCatalog()
    {
        // Arrange: the application composed as at startup, with a dialog service that plays the window
        var logger = new RollingFileLogger(PathHelpers.SafePathCombine(_tempRoot, "logs"));
        var composition = ApplicationComposition.Create(
            logger,
            new SettingsService(PathHelpers.SafePathCombine(_tempRoot, "CanvasPilot"), logger),
            new FakeRefreshScheduler());
        _disposables.Add(composition);
        var dialogs = new FakeDialogService();
        composition.Shell.AttachDialogService(dialogs);
        var clipboard = new RecordingKnowledgeClipboard();

        // Act: the user chooses Drawing Knowledge, searches for a word, opens the card of the project layout, follows a
        // related card, and copies the first example of the card of deliverables
        var enabled = composition.Shell.ShowDrawingKnowledgeCommand.CanExecute(null);
        await composition.Shell.ShowDrawingKnowledgeCommand.ExecuteAsync(null);
        var browser = dialogs.LastKnowledgeBrowser!;
        browser.Clipboard = clipboard;
        browser.SearchText = "gradient";
        var hits = browser.Results.Select(hit => hit.Id).ToList();
        browser.Select("canvaspilot/project-layout");
        var layoutTitle = browser.CardTitle;
        var layoutBlocks = browser.Blocks.Count;
        var related = browser.SeeAlso[0];
        related.NavigateCommand.Execute(related.Id);
        var followed = browser.SelectedCard!.Id;
        browser.Select("canvaspilot/deliverables");
        await browser.CopyExampleCommand.ExecuteAsync(browser.Examples[0]);

        // Assert: the browser is over the catalog of the application and everything worked as the user would see it
        Assert.True(enabled);
        Assert.Equal(1, dialogs.KnowledgeBrowserCount);
        Assert.Equal(KnowledgeCatalog.Default.Roots.Select(r => r.Id), browser.Nodes.Select(n => n.Id));
        Assert.Contains("canvaspilot", browser.Nodes.Select(n => n.Id));
        Assert.NotEmpty(hits);
        Assert.False(string.IsNullOrWhiteSpace(layoutTitle));
        Assert.True(layoutBlocks > 0);
        Assert.Equal(related.Id, followed);
        Assert.Equal([browser.Examples[0].Content], clipboard.Texts);
        Assert.Equal($"Copied example '{browser.Examples[0].Name}'.", browser.Status);
        Assert.Empty(dialogs.Messages);
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
        dialogs.ExportPresetToPick = ExportPreset.AsRendered;

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
    ///     Test that a user who draws a rectangle on the shown picture gets a numbered mark whose chip is in the message at
    ///     the caret, that undoing the chip hides the mark and redoing shows it, that dragging the picture out is off while
    ///     the tool is chosen, and that the mark of an earlier picture is hidden when the edited file is shown again.
    /// </summary>
    [Fact]
    public async Task CanvasPilot_MarkImage_DrawRectangleOnShownPicture_AddsMarkChipAndHidesMarkOnNewPicture()
    {
        // Arrange: the application with a project that holds a picture, which is shown, and some text in the message
        var token = TestContext.Current.CancellationToken;
        var (composition, _) = await ComposeWithProjectAsync();
        var folder = composition.Shell.CurrentProject!.FolderPath;
        var picture = Path.Combine(folder, "logo.svg");
        await File.WriteAllTextAsync(picture, "<svg xmlns='http://www.w3.org/2000/svg' width='80' height='40'><rect width='80' height='40' fill='#336699'/></svg>", token);
        composition.Shell.Workspace.Explorer.Refresh();
        composition.Shell.Workspace.Explorer.SelectFile(picture);
        await composition.Preview.RenderCompletion;
        composition.Composer.InsertTyped("Look here: ");
        var overlay = composition.Annotation;

        // Act: choose the rectangle tool, draw, and skip the note; undo and redo the chip; then edit the file and show it again
        var toolsBefore = overlay.ToolsEnabled;
        overlay.SelectTool(AnnotationTool.Rectangle);
        var dragOutWhileTool = composition.Preview.IsDragOutEnabled;
        overlay.BeginStroke(new RenderPoint(10, 5));
        overlay.EndStroke(new RenderPoint(50, 30));
        overlay.SkipNote();
        var chips = composition.Composer.Document.Chips.OfType<MarkChip>().ToList();
        var shown = overlay.VisibleMarks.Count;
        composition.Composer.Undo();
        var afterUndo = (composition.Composer.Document.Chips.Count(), overlay.VisibleMarks.Count);
        composition.Composer.Redo();
        var afterRedo = overlay.VisibleMarks.Count;
        await File.WriteAllTextAsync(picture, "<svg xmlns='http://www.w3.org/2000/svg' width='80' height='40'><rect width='80' height='40' fill='#993366'/></svg>", token);
        await File.WriteAllTextAsync(Path.Combine(folder, "other.svg"), "<svg xmlns='http://www.w3.org/2000/svg' width='8' height='8'/>", token);
        composition.Shell.Workspace.Explorer.Refresh();
        composition.Shell.Workspace.Explorer.SelectFile(Path.Combine(folder, "other.svg"));
        await composition.Preview.RenderCompletion;
        composition.Shell.Workspace.Explorer.SelectFile(picture);
        await composition.Preview.RenderCompletion;
        var onNewPicture = (overlay.VisibleMarks.Count, overlay.HiddenMarkCount, composition.Composer.MarkChipIds.Count);
        overlay.SelectTool(AnnotationTool.None);
        var dragOutAfterTool = composition.Preview.IsDragOutEnabled;
        composition.Dispose();

        // Assert: the mark is number one with the chip after the typed text; undo and redo hide and show it; the new
        // picture hides it but the message keeps it; dragging out was off only while the tool was chosen
        Assert.True(toolsBefore);
        Assert.False(dragOutWhileTool);
        Assert.True(dragOutAfterTool);
        var chip = Assert.Single(chips);
        Assert.Equal(1, chip.Number);
        Assert.Equal(1, shown);
        Assert.Equal((0, 0), afterUndo);
        Assert.Equal(1, afterRedo);
        Assert.Equal((0, 1, 1), onNewPicture);
        Assert.Same(overlay, composition.Preview.Overlay);
        Assert.IsType<AnnotationToolbarViewModel>(composition.Preview.Toolbar);
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
    ///     Test that a user who draws a rectangle around a node of the shown diagram, writes a message, and sends it through
    ///     the chat panel sees the message in the transcript with its mark chip, and that "View payload" shows exactly what
    ///     the assistant would receive: the text with the place of the mark, the annotated picture, the area around the
    ///     mark, and the node under it.
    /// </summary>
    [Fact]
    public async Task CanvasPilot_InspectPayload_SentMessageWithMark_InspectorShowsExactlyThePayload()
    {
        // Arrange: the application with a project that holds a diagram, which is shown, and a window that shows the
        // inspector on its own
        var token = TestContext.Current.CancellationToken;
        var (composition, _) = await ComposeWithProjectAsync();
        var folder = composition.Shell.CurrentProject!.FolderPath;
        var diagram = Path.Combine(folder, "flow.mmd");
        await File.WriteAllTextAsync(diagram, "flowchart LR\n  A[Start] --> B[End]\n", token);
        composition.Shell.Workspace.Explorer.Refresh();
        composition.Shell.Workspace.Explorer.SelectFile(diagram);
        await composition.Preview.RenderCompletion;
        var start = composition.Preview.Image!.Positions.First(position => position is { Kind: "node", Text: "Start" }).Box!.Value;
        var launcher = new RecordingInspectorLauncher();
        composition.Chat.InspectorLauncher = launcher;

        // Act: type, mark the node 'Start' with the rectangle tool, type more, send, and choose "View payload"
        composition.Composer.InsertTyped("Make ");
        composition.Annotation.SelectTool(AnnotationTool.Rectangle);
        composition.Annotation.BeginStroke(new RenderPoint(start.X - 4, start.Y - 4));
        composition.Annotation.EndStroke(new RenderPoint(start.X + start.Width + 4, start.Y + start.Height + 4));
        composition.Annotation.SkipNote();
        composition.Composer.InsertTyped(" green");
        var canSend = composition.Chat.CanSend;
        await composition.Chat.SendCommand.ExecuteAsync(null);
        var entry = Assert.Single(composition.Chat.Transcript);
        await entry.ViewPayloadCommand.ExecuteAsync(null);
        var shown = Assert.Single(launcher.Shown);
        var description = shown.Description;
        var payload = entry.Payload;
        var pictures = shown.Pictures.Count;
        composition.Dispose();

        // Assert: the message was sendable, the transcript shows its words and chip, the inspector shows the payload as
        // the describer writes it, with the place of the mark and the node under it, and two pictures
        Assert.True(canSend);
        Assert.Equal(["Make ", "#", " green"], entry.Items.Select(item => item.Text ?? "#"));
        Assert.NotNull(payload);
        Assert.Equal("Make [Mark 1] green", payload.Text);
        Assert.Equal(PayloadDescriber.Describe(payload), description);
        Assert.Contains("\"text\": \"Start\"", description);
        Assert.Equal(2, pictures);
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
    ///     Test that a user who holds to talk with a speech model chosen in the settings gets the words that were
    ///     recognized as one dictated block of the message, with the application composed as it is at startup over a
    ///     managed speech engine and a recorded sound file, and that a user without a model is told what to do.
    /// </summary>
    [Fact]
    public async Task CanvasPilot_DictateMessage_HoldToTalk_AddsOneDictatedBlockAndExplainsWhenUnavailable()
    {
        // Arrange: the application composed over a model service whose model is installed in a temporary store and
        // an audio service whose microphone is a recorded sound file
        var token = TestContext.Current.CancellationToken;
        var logger = new RollingFileLogger(PathHelpers.SafePathCombine(_tempRoot, "logs"));
        var settings = new SettingsService(PathHelpers.SafePathCombine(_tempRoot, "CanvasPilot"), logger);
        var modelStore = PathHelpers.SafePathCombine(_tempRoot, "models");
        DictationTestFiles.InstallModel(modelStore, "stt-fake");
        var wave = PathHelpers.SafePathCombine(_tempRoot, "speech.wav");
        DictationTestFiles.WriteSilentWave(wave, seconds: 1, sampleRate: 16000);
        using var models = new SpeechModelService(
            () => new SpeechModelCatalog(new SpeechModelStoreOptions { RootPathOverride = modelStore }).AddModels(
                new FakeRecognitionModel(
                    "stt-fake",
                    () => new FakeRecognitionBackend(
                        [new SpeechRecognitionResult("make the", IsFinal: false)],
                        new SpeechRecognitionResult("Make the second box blue.", IsFinal: true)))));
        var audio = new AudioDeviceService(
            () => [SpeechTestData.Capture("Desk mic")],
            () => [],
            () => { },
            captureCreator: _ => new WavFileAudioCaptureDevice(wave, 1600));
        using var composition = ApplicationComposition.Create(
            logger,
            settings,
            new FakeRefreshScheduler(),
            audioDevices: audio,
            speechModels: models);

        // Act: ask before a model is chosen, choose the model as the settings page does, ask again, then hold to
        // talk, let go, and add the words to the message as the chat will
        var withoutModel = composition.Dictation.CheckAvailability();
        settings.Update(current => current with { Speech = current.Speech with { SttModelId = "stt-fake" } });
        var withModel = composition.Dictation.CheckAvailability();
        var start = await composition.Dictation.BeginAsync(token);
        var text = await start.Session!.EndAsync(token);
        var document = new ComposerDocument();
        var history = new ComposerHistory(document);
        history.Execute(ComposerEdit.TypeText(document, 0, "Please "));
        history.Execute(ComposerEdit.Dictate(document, document.Length, text));

        // Assert: the first answer says to choose a model, the second is ready, and the message holds the typed text
        // and one dictated block
        Assert.Equal(DictationAvailabilityReason.NoModelChosen, withoutModel.Reason);
        Assert.Contains("Speech page", withoutModel.Message, StringComparison.Ordinal);
        Assert.True(withModel.IsAvailable);
        Assert.Equal("Make the second box blue.", text);
        Assert.Equal(
            [new TextSegment("Please ", TextOrigin.Typed), new TextSegment("Make the second box blue.", TextOrigin.Dictated)],
            document.Snapshot().Segments);
    }

    /// <summary>
    ///     Test that a user who types, marks a node of the shown diagram with a note, and attaches a file sends one message
    ///     whose transcript entry shows the words, the mark chip, and the file chip in the order they were placed, whose
    ///     payload holds the text with its references, the marked picture, and the attachment in the project, and that the
    ///     draft is empty afterwards.
    /// </summary>
    [Fact]
    public async Task CanvasPilot_ComposeMessage_TypedAttachmentAndMark_SendsAndShowsInTranscript()
    {
        // Arrange: the application with a project that holds a diagram, which is shown, and a file outside the project
        var token = TestContext.Current.CancellationToken;
        var (composition, _) = await ComposeWithProjectAsync();
        var folder = composition.Shell.CurrentProject!.FolderPath;
        var (_, picture) = await ShowDiagramAsync(composition, "flow.mmd", "flowchart LR\n  A[Start] --> B[End]\n");
        var brief = Path.Combine(Directory.CreateDirectory(Path.Combine(_tempRoot, "external")).FullName, "brief.txt");
        await File.WriteAllTextAsync(brief, "the brief", token);
        var emptyBefore = composition.Chat.CanSend;

        // Act: type, mark the node 'Start' with a note, type more, drop the file, and send through the chat panel
        composition.Composer.InsertTyped("Make ");
        DrawRectangleAround(composition, NodeBox(picture, "Start"), "bigger");
        composition.Composer.InsertTyped(" like ");
        composition.Composer.HandleDrop(new DroppedContent { Paths = [brief] });
        var canSend = composition.Chat.CanSend;
        var attachmentsWhileDrafting = Directory.Exists(Path.Combine(folder, "attachments"));
        await composition.Chat.SendCommand.ExecuteAsync(null);
        var entry = Assert.Single(composition.Chat.Transcript);
        var items = entry.Items.Select(item => item.Text ?? "#").ToList();
        var chips = entry.Items.Select(item => item.Chip).ToList();
        var summary = entry.Summary;
        var payload = entry.Payload!;
        var afterSend = (composition.Composer.Document.Length, composition.Composer.CanUndo, composition.Chat.CanSend, composition.Marks.Marks.Length, composition.Annotation.VisibleMarks.Count);
        var copy = await File.ReadAllTextAsync(Path.Combine(folder, "attachments", "brief.txt"), token);
        composition.Dispose();

        // Assert: the message could be sent only once it had words; the transcript holds words and chips in order; the
        // payload holds the text, the mark with its note and the node under it, and the attachment's path; the file was
        // copied only at send; the draft, its history, and the marks are gone
        Assert.False(emptyBefore);
        Assert.True(canSend);
        Assert.False(attachmentsWhileDrafting);
        Assert.Equal(["Make ", "#", " like ", "#"], items);
        Assert.IsType<MarkChip>(chips[1]);
        Assert.IsType<FileChip>(chips[3]);
        Assert.Equal("1 mark, 1 attachment", summary);
        Assert.Equal("Make [Mark 1] like [Attachment 1: brief.txt]", payload.Text);
        Assert.Equal("attachments/brief.txt", Assert.Single(payload.Attachments).ProjectRelativePath);
        var mark = Assert.Single(Assert.Single(payload.Images).Marks);
        Assert.Equal("bigger", mark.Note);
        Assert.Equal("Start", Assert.Single(mark.SourceElements, element => element.Kind == "node").Text);
        Assert.Equal("the brief", copy);
        Assert.Equal((0, false, false, 0, 0), afterSend);
    }

    /// <summary>
    ///     Test that a user who types, dictates, drops files, marks the picture, and deletes a chip can undo every action
    ///     one at a time with the Edit menu's command, back to the empty message, and redo them one at a time, with typing
    ///     one character per step and a dictated block or a drop of several files one step, that sending leaves nothing to
    ///     undo, and that nothing of the draft is written into the project.
    /// </summary>
    [Fact]
    public async Task CanvasPilot_UndoRedoMessage_EachActionKind_StepsAsSpecified()
    {
        // Arrange: the application with a project that holds a diagram, which is shown, and two files outside the project,
        // with the composer as the target that the Edit menu undoes and redoes
        var token = TestContext.Current.CancellationToken;
        var (composition, _) = await ComposeWithProjectAsync();
        var folder = composition.Shell.CurrentProject!.FolderPath;
        var (_, picture) = await ShowDiagramAsync(composition, "flow.mmd", "flowchart LR\n  A[Start] --> B[End]\n");
        var external = Directory.CreateDirectory(Path.Combine(_tempRoot, "external")).FullName;
        var first = Path.Combine(external, "a.txt");
        var second = Path.Combine(external, "b.txt");
        await File.WriteAllTextAsync(first, "a", token);
        await File.WriteAllTextAsync(second, "b", token);
        var composer = composition.Composer;
        composition.Shell.EditTarget = composer;
        string[] ProjectEntries() => [.. Directory.GetFileSystemEntries(folder, "*", SearchOption.AllDirectories).Order(StringComparer.Ordinal)];
        (string Text, int Chips, int Marks) State() => (composer.Document.FlatText, composer.Document.Chips.Count(), composition.Annotation.VisibleMarks.Count);
        var entriesBefore = ProjectEntries();
        var states = new List<(string Text, int Chips, int Marks)> { State() };

        // Act: type two characters, dictate a block, drop two files, mark the picture, and delete a file chip, one action
        // each, noting the state after each; then undo them all with the Edit menu's command and redo them all
        composer.InsertTyped("a");
        states.Add(State());
        composer.InsertTyped("b");
        states.Add(State());
        composer.AddDictatedBlock(" make it red");
        states.Add(State());
        composer.HandleDrop(new DroppedContent { Paths = [first, second] });
        states.Add(State());
        DrawRectangleAround(composition, NodeBox(picture, "Start"));
        states.Add(State());
        composer.DeleteChip(composer.Document.FlatText.IndexOf('\uFFFC', StringComparison.Ordinal));
        states.Add(State());
        var entriesAfterDrafting = ProjectEntries();
        var undone = new List<(string Text, int Chips, int Marks)>();
        while (composition.Shell.UndoCommand.CanExecute(null))
        {
            composition.Shell.UndoCommand.Execute(null);
            undone.Add(State());
        }

        var redone = new List<(string Text, int Chips, int Marks)>();
        while (composition.Shell.RedoCommand.CanExecute(null))
        {
            composition.Shell.RedoCommand.Execute(null);
            redone.Add(State());
        }

        await composer.SendAsync(token);
        var afterSend = (composer.Document.Length, composer.CanUndo, composer.CanRedo, composition.Shell.UndoCommand.CanExecute(null), composition.Shell.RedoCommand.CanExecute(null));
        composition.Dispose();

        // Assert: every action was one step, going back passes through the states in reverse down to the empty message
        // and going forward through them in order; sending leaves nothing to undo or redo; nothing was written to the
        // project before the send
        Assert.Equal(6, undone.Count);
        Assert.Equal(states.AsEnumerable().Reverse().Skip(1), undone);
        Assert.Equal(states.Skip(1), redone);
        Assert.Equal(("ab make it red", 0, 0), (states[3].Text, states[3].Chips, states[3].Marks));
        Assert.Equal(2, states[4].Chips);
        Assert.Equal((3, 1), (states[5].Chips, states[5].Marks));
        Assert.Equal((2, 1), (states[6].Chips, states[6].Marks));
        Assert.Equal(entriesBefore, entriesAfterDrafting);
        Assert.Equal((0, false, false, false, false), afterSend);
    }

    /// <summary>
    ///     Test that a user who drops a file, a folder, and a picture and pastes a screenshot sees only chips while the
    ///     message is a draft, with nothing copied into the project, and that sending copies them into the project's
    ///     attachments folder byte for byte, without overwriting a file that is already there, and without leaving the
    ///     pasted picture in the draft folder.
    /// </summary>
    [Fact]
    public async Task CanvasPilot_AttachToMessage_DropFileFolderImage_CopiedIntoProjectOnlyAtSend()
    {
        // Arrange: the application with a project, and a file, a folder with a nested file, and a picture outside it
        var token = TestContext.Current.CancellationToken;
        var (composition, _) = await ComposeWithProjectAsync();
        var folder = composition.Shell.CurrentProject!.FolderPath;
        var store = new AttachmentStore(folder);
        var outside = Directory.CreateDirectory(Path.Combine(_tempRoot, "outside")).FullName;
        var brief = Path.Combine(outside, "brief.txt");
        var photos = Directory.CreateDirectory(Path.Combine(outside, "photos", "more")).FullName;
        var sketch = Path.Combine(outside, "sketch.png");
        byte[] picture = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 1, 2, 3, 4];
        byte[] screenshot = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 5, 6, 7];
        await File.WriteAllTextAsync(brief, "the brief", token);
        await File.WriteAllTextAsync(Path.Combine(outside, "photos", "a.txt"), "photo notes", token);
        await File.WriteAllTextAsync(Path.Combine(photos, "b.txt"), "more notes", token);
        await File.WriteAllBytesAsync(sketch, picture, token);

        // Act: type, drop the three things in one drop, paste the screenshot, look at the project while drafting, then
        // put a file of the same name into the attachments folder, as an earlier message could have, and send
        composition.Composer.InsertTyped("Use these ");
        composition.Composer.HandleDrop(new DroppedContent { Paths = [brief, Path.Combine(outside, "photos"), sketch] });
        composition.Composer.HandlePaste(new DroppedContent { Images = [screenshot] });
        var chipKinds = composition.Composer.Document.Chips.Select(chip => chip.GetType()).ToList();
        var attachmentsWhileDrafting = Directory.Exists(store.AttachmentsFolder);
        Directory.CreateDirectory(store.AttachmentsFolder);
        await File.WriteAllTextAsync(Path.Combine(store.AttachmentsFolder, "brief.txt"), "mine", token);
        await composition.Chat.SendCommand.ExecuteAsync(null);
        var payload = Assert.Single(composition.Chat.Transcript).Payload!;
        var paths = payload.Attachments.Select(attachment => attachment.ProjectRelativePath).ToList();
        var briefCopy = await File.ReadAllTextAsync(Path.Combine(folder, paths[0]), token);
        var folderCopy = (
            await File.ReadAllTextAsync(Path.Combine(folder, paths[1], "a.txt"), token),
            await File.ReadAllTextAsync(Path.Combine(folder, paths[1], "more", "b.txt"), token));
        var sketchCopy = await File.ReadAllBytesAsync(Path.Combine(folder, paths[2]), token);
        var screenshotCopy = await File.ReadAllBytesAsync(Path.Combine(folder, paths[3]), token);
        var mine = await File.ReadAllTextAsync(Path.Combine(store.AttachmentsFolder, "brief.txt"), token);
        var leftover = Directory.GetFiles(store.PastedImagesFolder);
        var originalsKept = (File.Exists(brief), File.Exists(sketch), Directory.Exists(Path.Combine(outside, "photos")));
        composition.Dispose();

        // Assert: four chips in the draft and nothing in the project; at send each thing is in the attachments folder
        // with its exact content, the file that was already there is untouched, the draft folder is empty, and the
        // originals are where they were
        Assert.Equal([typeof(FileChip), typeof(FolderChip), typeof(ImageChip), typeof(ImageChip)], chipKinds);
        Assert.False(attachmentsWhileDrafting);
        Assert.Equal(4, paths.Count);
        Assert.All(paths, path => Assert.StartsWith("attachments/", path, StringComparison.Ordinal));
        Assert.NotEqual("attachments/brief.txt", paths[0]);
        Assert.Equal("mine", mine);
        Assert.Equal("the brief", briefCopy);
        Assert.Equal(("photo notes", "more notes"), folderCopy);
        Assert.Equal(picture, sketchCopy);
        Assert.Equal(screenshot, screenshotCopy);
        Assert.Empty(leftover);
        Assert.Equal((true, true, true), originalsKept);
    }

    /// <summary>
    ///     Test that a user who drops programs, scripts, a shortcut, a web page, a picture that is no picture, and a file
    ///     that another program holds open exclusively only gets chips: nothing is opened, run, or shown, the files are
    ///     copied unchanged when the message is sent, and a script that would leave a marker file when run does not.
    /// </summary>
    [Fact]
    public async Task CanvasPilot_AttachToMessage_DroppedContent_IsNeverOpened()
    {
        // Arrange: the application with a project and a diagram that is shown, and dropped files whose content would do
        // something if it were run, shown, or followed: scripts that write a marker file, and others
        var token = TestContext.Current.CancellationToken;
        var (composition, _) = await ComposeWithProjectAsync();
        var folder = composition.Shell.CurrentProject!.FolderPath;
        var (_, shown) = await ShowDiagramAsync(composition, "flow.mmd", "flowchart LR\n  A[Start] --> B[End]\n");
        var outside = Directory.CreateDirectory(Path.Combine(_tempRoot, "outside")).FullName;
        var marker = Path.Combine(outside, "marker.txt");
        var contents = new Dictionary<string, string>
        {
            ["run.bat"] = $"@echo off\r\necho ran> \"{marker}\"\r\n",
            ["run.ps1"] = $"Set-Content -Path '{marker}' -Value ran\r\n",
            ["run.sh"] = $"#!/bin/sh\necho ran > '{marker}'\n",
            ["page.html"] = "<html><script>alert(1)</script></html>",
            ["link.lnk"] = "not a shortcut",
            ["picture.svg"] = "<svg onload='alert(1)'/>",
            ["fake.png"] = "this is not a picture",
            ["setup.exe"] = "MZ-not-really",
        };
        var paths = new List<string>();
        foreach (var (name, text) in contents)
        {
            var path = Path.Combine(outside, name);
            await File.WriteAllTextAsync(path, text, token);
            paths.Add(path);
        }

        var locked = Path.Combine(outside, "locked.docx");
        await File.WriteAllTextAsync(locked, "secret", token);

        // Act: drop everything while one file is held open exclusively, which could not be read; look at what is shown;
        // then let go of the file and send
        FileStream hold = new(locked, FileMode.Open, FileAccess.Read, FileShare.None);
        bool dropped;
        try
        {
            dropped = composition.Composer.HandleDrop(new DroppedContent { Paths = [.. paths, locked] });
        }
        finally
        {
            await hold.DisposeAsync();
        }

        var chipKinds = composition.Composer.Document.Chips.Select(chip => chip.GetType()).ToList();
        var statusAfterDrop = composition.Composer.StatusText;
        var stillShown = composition.Preview.Image;
        await composition.Chat.SendCommand.ExecuteAsync(null);
        var payload = Assert.Single(composition.Chat.Transcript).Payload!;
        var copies = new Dictionary<string, string>();
        foreach (var attachment in payload.Attachments)
        {
            copies[Path.GetFileName(attachment.ProjectRelativePath)] = await File.ReadAllTextAsync(Path.Combine(folder, attachment.ProjectRelativePath), token);
        }

        var originals = new Dictionary<string, string>();
        foreach (var path in paths)
        {
            originals[Path.GetFileName(path)] = await File.ReadAllTextAsync(path, token);
        }

        var markerWritten = File.Exists(marker);
        composition.Dispose();

        // Assert: every dropped thing is a chip, a file or a picture by its name only, and nothing is in the status; the
        // shown picture is the diagram still; the copies and the originals hold the content that was dropped; no
        // script ran
        Assert.True(dropped);
        Assert.Equal(contents.Count + 1, chipKinds.Count);
        Assert.Equal(["fake.png", "picture.svg"], payload.Attachments.Where(attachment => attachment.Kind == AttachmentKind.Image).Select(attachment => Path.GetFileName(attachment.ProjectRelativePath)).Order(StringComparer.Ordinal));
        Assert.Equal(string.Empty, statusAfterDrop);
        Assert.Same(shown, stillShown);
        Assert.Equal(contents.Count + 1, payload.Attachments.Length);
        Assert.Equal(contents.Append(new("locked.docx", "secret")).OrderBy(pair => pair.Key, StringComparer.Ordinal), copies.OrderBy(pair => pair.Key, StringComparer.Ordinal));
        Assert.Equal(contents.OrderBy(pair => pair.Key, StringComparer.Ordinal), originals.OrderBy(pair => pair.Key, StringComparer.Ordinal));
        Assert.False(markerWritten);
    }

    /// <summary>
    ///     Test that a user who holds the talk key, with a speech model chosen in the settings, lets go and gets the
    ///     recognized words as one block of dictated text after the typed text, that the words were not in the message
    ///     while the key was held, and that one undo removes the whole block.
    /// </summary>
    [Fact]
    public async Task CanvasPilot_DictateMessage_HeldAndReleased_InsertsOneBlock()
    {
        // Arrange: the application over a model that is installed and chosen and a microphone that is a recorded sound
        // file, with a project, and some typed text
        var (composition, _) = await ComposeWithDictationAsync(modelInstalled: true);
        composition.Composer.InsertTyped("Please ");

        // Act: hold the talk key, look at the message and the button, let go, and then undo and redo
        await composition.Shell.BeginPushToTalkAsync();
        var held = (composition.Shell.IsPushToTalkActive, composition.Chat.IsTalking, composition.Composer.CanSend, composition.Composer.Document.FlatText);
        await composition.Shell.EndPushToTalkAsync();
        var released = (composition.Shell.IsPushToTalkActive, composition.Chat.IsTalking, composition.Composer.StatusText);
        var segments = composition.Composer.Document.Snapshot().Segments;
        composition.Composer.Undo();
        var afterUndo = composition.Composer.Document.FlatText;
        composition.Composer.Redo();
        var afterRedo = composition.Composer.Document.FlatText;
        composition.Dispose();

        // Assert: while held the message was unchanged and could not be sent; after the release the typed text and one
        // dictated block are in the message in order; undo took the whole block out and redo put it back
        Assert.Equal((true, true, false, "Please "), held);
        Assert.Equal((false, false, string.Empty), released);
        Assert.Collection(
            segments,
            segment => Assert.Equal(new TextSegment("Please ", TextOrigin.Typed), segment),
            segment =>
            {
                var text = Assert.IsType<TextSegment>(segment);
                Assert.Equal(TextOrigin.Dictated, text.Origin);
                Assert.Equal("Make the second box blue.", text.Text.Trim());
            });
        Assert.Equal("Please ", afterUndo);
        Assert.Equal("Please Make the second box blue.", afterRedo.Trim().Replace("  ", " ", StringComparison.Ordinal));
    }

    /// <summary>
    ///     Test that a user who holds the talk key with no speech model chosen, or a chosen model that is not installed, is
    ///     told what to do, that the microphone is not opened, that the message is as it was, and that it can still be
    ///     sent.
    /// </summary>
    [Fact]
    public async Task CanvasPilot_DictateMessage_NoModel_ExplainsAndKeepsDraft()
    {
        // Arrange: the application with a project and a microphone that fails when it is opened, with typed text
        var (composition, settings) = await ComposeWithDictationAsync(modelInstalled: false);
        composition.Composer.InsertTyped("Keep this ");

        // Act: hold and release the talk key with no model chosen, then with a model chosen that is not installed, and
        // send the message
        await composition.Shell.BeginPushToTalkAsync();
        await composition.Shell.EndPushToTalkAsync();
        var withoutModel = (composition.Composer.StatusText, composition.Dictation.CheckAvailability().Reason);
        var afterFirst = (composition.Composer.Document.FlatText, composition.Composer.DictationState, composition.Chat.IsTalking, composition.Composer.CanUndo);
        settings.Update(current => current with { Speech = current.Speech with { SttModelId = "stt-missing" } });
        await composition.Shell.BeginPushToTalkAsync();
        await composition.Shell.EndPushToTalkAsync();
        var notInstalled = (composition.Composer.StatusText, composition.Dictation.CheckAvailability().Reason);
        var afterSecond = (composition.Composer.Document.FlatText, composition.Composer.DictationState, composition.Chat.IsTalking);
        var canSend = composition.Chat.CanSend;
        await composition.Chat.SendCommand.ExecuteAsync(null);
        var sent = Assert.Single(composition.Chat.Transcript).Payload!.Text;
        composition.Dispose();

        // Assert: each answer says what to do and is not a failure, nothing was added or lost, no talk state is left over,
        // and the message was sent with the words that were typed
        Assert.Equal(DictationAvailabilityReason.NoModelChosen, withoutModel.Reason);
        Assert.Contains("Speech page", withoutModel.StatusText, StringComparison.Ordinal);
        Assert.Equal(DictationAvailabilityReason.ModelNotInstalled, notInstalled.Reason);
        Assert.Contains("stt-missing", notInstalled.StatusText, StringComparison.Ordinal);
        Assert.NotEqual(withoutModel.StatusText, notInstalled.StatusText);
        Assert.Equal(("Keep this ", DictationState.Idle, false, true), afterFirst);
        Assert.Equal(("Keep this ", DictationState.Idle, false), afterSecond);
        Assert.True(canSend);
        Assert.Equal("Keep this", sent.Trim());
    }

    /// <summary>
    ///     Test that a user who draws on the shown picture with the pointer and with the keyboard gets numbered marks, each
    ///     with its chip in the message at the place where the user was writing, and that each mark holds the exact
    ///     picture that was shown when it was drawn: the same picture, with its file and pixel fingerprints.
    /// </summary>
    [Fact]
    public async Task CanvasPilot_MarkImage_DrawOnShownPicture_AddsNumberedChipPinnedToSnapshot()
    {
        // Arrange: the application with a project that holds a diagram, which is shown, and some typed text
        var (composition, _) = await ComposeWithProjectAsync();
        var (_, picture) = await ShowDiagramAsync(composition, "flow.mmd", "flowchart LR\n  A[Start] --> B[End]\n");
        composition.Composer.InsertTyped("Look: ");

        // Act: mark the node 'Start' with the pointer and a note, type, and mark with the keyboard cursor
        DrawRectangleAround(composition, NodeBox(picture, "Start"), "this one");
        composition.Composer.InsertTyped(" and ");
        composition.Annotation.SelectTool(AnnotationTool.Ellipse);
        composition.Annotation.MoveCursor(-20, -10);
        composition.Annotation.ConfirmCursor();
        composition.Annotation.MoveCursor(60, 30);
        composition.Annotation.ConfirmCursor();
        composition.Annotation.SkipNote();
        var marks = composition.Marks.Marks;
        var segments = composition.Composer.Document.Snapshot().Segments;
        var visible = composition.Annotation.VisibleMarks.Select(mark => mark.Number).ToList();
        composition.Dispose();

        // Assert: two marks numbered in order, the rectangle with its note around the node and the ellipse from the
        // keyboard; each holds the very picture that was shown, with its fingerprints; the message holds their chips in
        // the order the user wrote them, and the layer shows both
        Assert.Equal([1, 2], marks.Select(mark => mark.Number));
        Assert.Equal([MarkKind.Rectangle, MarkKind.Ellipse], marks.Select(mark => mark.Kind));
        Assert.Equal("this one", marks[0].Note);
        Assert.Null(marks[1].Note);
        Assert.All(marks, mark =>
        {
            Assert.Same(picture, mark.Snapshot);
            Assert.Equal(picture.SnapshotKey, mark.Snapshot.SnapshotKey);
            Assert.Equal((64, 64), (mark.Snapshot.SourceFileHash?.Length, mark.Snapshot.ContentHash.Length));
        });
        var start = NodeBox(picture, "Start");
        Assert.True(marks[0].Geometry.Points[0].X < start.X && marks[0].Geometry.Points[1].X > start.X + start.Width);
        Assert.Collection(
            segments,
            segment => Assert.Equal(new TextSegment("Look: ", TextOrigin.Typed), segment),
            segment => Assert.Equal(new MarkChip(marks[0].Id, 1), segment),
            segment => Assert.Equal(new TextSegment(" and ", TextOrigin.Typed), segment),
            segment => Assert.Equal(new MarkChip(marks[1].Id, 2), segment));
        Assert.Equal([1, 2], visible);
    }

    /// <summary>
    ///     Test that a mark stays exactly where the user drew it when its diagram is drawn again: drawing the unchanged
    ///     file again shows the mark in the same place, and after the file was edited the mark is not shown on the new
    ///     picture and is not moved to it, and the message that is sent holds the picture the mark was drawn on, with the
    ///     part of the source that was under the mark then.
    /// </summary>
    [Fact]
    public async Task CanvasPilot_MarkImage_Rerender_DoesNotMoveMarks()
    {
        // Arrange: the application with a project that holds a diagram, which is shown, with a mark around the node 'Start'
        var token = TestContext.Current.CancellationToken;
        var (composition, _) = await ComposeWithProjectAsync();
        var (diagram, original) = await ShowDiagramAsync(composition, "flow.mmd", "flowchart LR\n  A[Start] --> B[End]\n");
        composition.Composer.InsertTyped("Fix ");
        DrawRectangleAround(composition, NodeBox(original, "Start"), "here");
        var mark = Assert.Single(composition.Marks.Marks);
        var points = mark.Geometry.Points.ToList();

        // Act: draw the unchanged file again, then edit the node 'Start' so that the picture changes, draw it again, and
        // send the message
        var sameAgain = await ShowDiagramAgainAsync(composition, diagram);
        var onSamePicture = (composition.Annotation.VisibleMarks.Count, composition.Annotation.HiddenMarkCount);
        await File.WriteAllTextAsync(diagram, "flowchart LR\n  A[Begin with a much longer title] --> B[End]\n", token);
        var edited = await ShowDiagramAgainAsync(composition, diagram);
        var onNewPicture = (composition.Annotation.VisibleMarks.Count, composition.Annotation.HiddenMarkCount, composition.Composer.MarkChipIds.Count);
        var afterEdit = (mark.Snapshot, mark.Geometry.Points.ToList());
        await composition.Chat.SendCommand.ExecuteAsync(null);
        var payload = Assert.Single(composition.Chat.Transcript).Payload!;
        composition.Dispose();

        // Assert: the unchanged picture shows the mark where it was; the edited one has other pixels and does not show it
        // but the message keeps it; the mark still holds the first picture and the same points; the payload is made from
        // that picture and the node that was under the mark then
        Assert.Equal(original.SnapshotKey, sameAgain.SnapshotKey);
        Assert.Equal((1, 0), onSamePicture);
        Assert.NotEqual(original.ContentHash, edited.ContentHash);
        Assert.Equal((0, 1, 1), onNewPicture);
        Assert.Same(original, afterEdit.Snapshot);
        Assert.Equal(points, afterEdit.Item2);
        var image = Assert.Single(payload.Images);
        Assert.Equal((original.SourceFileHash, original.ContentHash), (image.SourceFileHash, image.ContentHash));
        Assert.Equal(points, Assert.Single(image.Marks).Points);
        Assert.Equal("Start", Assert.Single(Assert.Single(image.Marks).SourceElements, element => element.Kind == "node").Text);
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
        _disposables.Add(composition);
        composition.Shell.AttachDialogService(dialogs);
        await composition.Shell.NewProjectCommand.ExecuteAsync(null);
        return (composition, dialogs);
    }

    /// <summary>
    ///     Composes the application over a speech model service whose model is installed in a temporary store (or whose
    ///     store holds none) and an audio service whose microphone is a recorded sound file (or one that must never be
    ///     opened), and has a user create a project. No microphone, speech runtime, or network is used.
    /// </summary>
    /// <param name="modelInstalled">
    ///     Whether the model is installed and chosen in the settings, as the Speech page of the settings does.
    /// </param>
    /// <returns>The composition, whose project is open, and the settings service it uses.</returns>
    private async Task<(ApplicationComposition Composition, SettingsService Settings)> ComposeWithDictationAsync(bool modelInstalled)
    {
        var documents = Directory.CreateDirectory(PathHelpers.SafePathCombine(_tempRoot, "Documents")).FullName;
        var logger = new RollingFileLogger(PathHelpers.SafePathCombine(_tempRoot, "logs"));
        var settings = new SettingsService(PathHelpers.SafePathCombine(_tempRoot, "CanvasPilot"), logger);
        var modelStore = PathHelpers.SafePathCombine(_tempRoot, "models");
        var wave = PathHelpers.SafePathCombine(_tempRoot, "speech.wav");
        Directory.CreateDirectory(modelStore);
        if (modelInstalled)
        {
            DictationTestFiles.InstallModel(modelStore, "stt-fake");
            DictationTestFiles.WriteSilentWave(wave, seconds: 1, sampleRate: 16000);
        }

        var models = new SpeechModelService(
            () => new SpeechModelCatalog(new SpeechModelStoreOptions { RootPathOverride = modelStore }).AddModels(
                new FakeRecognitionModel(
                    "stt-fake",
                    () => new FakeRecognitionBackend(
                        [new SpeechRecognitionResult("make the", IsFinal: false)],
                        new SpeechRecognitionResult("Make the second box blue.", IsFinal: true)))));
        _disposables.Add(models);
        var audio = new AudioDeviceService(
            () => [SpeechTestData.Capture("Desk mic")],
            () => [],
            () => { },
            captureCreator: _ => modelInstalled
                ? new WavFileAudioCaptureDevice(wave, 1600)
                : throw new InvalidOperationException("The microphone must not be opened without a model."));
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
            settings,
            new FakeRefreshScheduler(),
            defaultProjectsFolder: documents,
            audioDevices: audio,
            speechModels: models,
            dragFolder: PathHelpers.SafePathCombine(_tempRoot, "drag"));
        _disposables.Add(composition);
        composition.Shell.AttachDialogService(dialogs);
        if (modelInstalled)
        {
            settings.Update(current => current with { Speech = current.Speech with { SttModelId = "stt-fake" } });
        }

        await composition.Shell.NewProjectCommand.ExecuteAsync(null);
        return (composition, settings);
    }

    /// <summary>
    ///     Writes a diagram into the open project, has the user select it, and waits until its picture is shown.
    /// </summary>
    /// <param name="composition">The composed application.</param>
    /// <param name="name">The file name of the diagram.</param>
    /// <param name="source">The content of the diagram.</param>
    /// <returns>The path of the diagram and the picture that is shown.</returns>
    private static async Task<(string Path, RenderedImage Picture)> ShowDiagramAsync(ApplicationComposition composition, string name, string source)
    {
        var path = Path.Combine(composition.Shell.CurrentProject!.FolderPath, name);
        await File.WriteAllTextAsync(path, source, TestContext.Current.CancellationToken);
        composition.Shell.Workspace.Explorer.Refresh();
        composition.Shell.Workspace.Explorer.SelectFile(path);
        await composition.Preview.RenderCompletion;
        return (path, composition.Preview.Image!);
    }

    /// <summary>
    ///     Has the user show another file and then the diagram again, which draws the diagram anew from its file as it
    ///     is then, and waits until the new picture is shown.
    /// </summary>
    /// <param name="composition">The composed application.</param>
    /// <param name="diagram">The path of the diagram.</param>
    /// <returns>The picture that is shown.</returns>
    private static async Task<RenderedImage> ShowDiagramAgainAsync(ApplicationComposition composition, string diagram)
    {
        var other = Path.Combine(composition.Shell.CurrentProject!.FolderPath, "other.svg");
        await File.WriteAllTextAsync(other, "<svg xmlns='http://www.w3.org/2000/svg' width='8' height='8'/>", TestContext.Current.CancellationToken);
        composition.Shell.Workspace.Explorer.Refresh();
        composition.Shell.Workspace.Explorer.SelectFile(other);
        await composition.Preview.RenderCompletion;
        composition.Shell.Workspace.Explorer.SelectFile(diagram);
        await composition.Preview.RenderCompletion;
        return composition.Preview.Image!;
    }

    /// <summary>
    ///     Finds the box of a node of a picture of a diagram.
    /// </summary>
    /// <param name="picture">The picture.</param>
    /// <param name="text">The text of the node.</param>
    /// <returns>The box of the node, in picture pixels.</returns>
    private static RenderBox NodeBox(RenderedImage picture, string text) =>
        picture.Positions.First(position => position.Kind == "node" && position.Text == text).Box!.Value;

    /// <summary>
    ///     Has the user draw a rectangle a little larger than a box on the shown picture with the pointer and finish the
    ///     mark with a note or without one.
    /// </summary>
    /// <param name="composition">The composed application.</param>
    /// <param name="box">The box to mark, in picture pixels.</param>
    /// <param name="note">The note, or <see langword="null" /> to skip it.</param>
    private static void DrawRectangleAround(ApplicationComposition composition, RenderBox box, string? note = null)
    {
        var overlay = composition.Annotation;
        overlay.SelectTool(AnnotationTool.Rectangle);
        overlay.BeginStroke(new RenderPoint(box.X - 4, box.Y - 4));
        overlay.EndStroke(new RenderPoint(box.X + box.Width + 4, box.Y + box.Height + 4));
        if (note is null)
        {
            overlay.SkipNote();
        }
        else
        {
            overlay.NoteText = note;
            overlay.CommitNote();
        }
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
