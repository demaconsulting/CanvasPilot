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

using DemaConsulting.CanvasPilot.AgentSubsystem;
using DemaConsulting.CanvasPilot.AnnotationSubsystem;
using DemaConsulting.CanvasPilot.AppShellSubsystem;
using DemaConsulting.CanvasPilot.ChatSubsystem;
using DemaConsulting.CanvasPilot.KnowledgeSubsystem;
using DemaConsulting.CanvasPilot.LoggingSubsystem;
using DemaConsulting.CanvasPilot.SettingsSubsystem;
using DemaConsulting.CanvasPilot.SpeechSubsystem;
using DemaConsulting.CanvasPilot.StyleSubsystem;
using Microsoft.Extensions.AI;

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
        Type? thirdPageType = null;
        _dialogs.OnSettings = dialog =>
        {
            titles = dialog.Pages.Select(page => page.Title).ToArray();
            secondPageType = dialog.Pages[1].GetType();
            thirdPageType = dialog.Pages[2].GetType();
            dialog.CancelCommand.Execute(null);
        };

        // Act: open Settings
        await composition.Shell.OpenSettingsCommand.ExecuteAsync(null);

        // Assert: both pages are hosted
        Assert.Equal(["AI Provider", "Speech", "Style Kits", "Appearance"], titles);
        Assert.Equal(typeof(SpeechSettingsPageViewModel), secondPageType);
        Assert.Equal(typeof(StyleSettingsPageViewModel), thirdPageType);
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
        Assert.Equal(6, about.Dependencies.Count);
    }

    /// <summary>
    ///     Test that Drawing Knowledge run on the composed shell gives the dialog service a browser over the catalog of the
    ///     application, whose tree is the roots of the catalog and which can show a card of CanvasPilot.
    /// </summary>
    [Fact]
    public async Task AppShellSubsystem_DrawingKnowledge_ComposedShell_ShowsBrowserOnTheCatalogOfTheApplication()
    {
        // Arrange: a composed application
        var composition = Compose();

        // Act: run Drawing Knowledge, and choose a card in the browser that was shown
        var enabled = composition.Shell.ShowDrawingKnowledgeCommand.CanExecute(null);
        await composition.Shell.ShowDrawingKnowledgeCommand.ExecuteAsync(null);
        var browser = _dialogs.LastKnowledgeBrowser!;
        var shown = browser.Select("canvaspilot/project-layout");

        // Assert: the browser is over the catalog of the application
        Assert.True(enabled);
        Assert.Equal(1, _dialogs.KnowledgeBrowserCount);
        Assert.Equal(KnowledgeCatalog.Default.Roots.Select(r => r.Id), browser.Nodes.Select(n => n.Id));
        Assert.True(shown);
        Assert.Equal("canvaspilot/project-layout", browser.SelectedCard!.Id);
    }

    /// <summary>
    ///     Test that Style Kits run on the composed shell gives the dialog service a window over the catalog of the
    ///     application, which lists the kits that come with the program and can make a kit of the user.
    /// </summary>
    [Fact]
    public async Task AppShellSubsystem_StyleKits_ComposedShell_ShowsTheWindowOnTheCatalogOfTheApplication()
    {
        // Arrange: a composed application whose kits are read
        var composition = Compose();
        await composition.StyleCatalog.RefreshAsync(TestContext.Current.CancellationToken);

        // Act: run Style Kits, and make a kit in the window that was shown
        var enabled = composition.Shell.ShowStyleKitsCommand.CanExecute(null);
        await composition.Shell.ShowStyleKitsCommand.ExecuteAsync(null);
        var manager = _dialogs.LastStyleKitManager!;
        _dialogs.PromptResult = "Poster";
        await manager.NewCommand.ExecuteAsync(null);

        // Assert: the window lists the kits of the program, and the new kit is in the folder of the user of the composition
        Assert.True(enabled);
        Assert.Equal(1, _dialogs.StyleKitManagerCount);
        Assert.Equal(
            ["Clean", "DEMA", "Slides Dark"],
            manager.Groups.Single(group => group.Location == StyleKitLocation.BuiltIn).Items.Select(item => item.Name));
        Assert.Equal("Poster", manager.SelectedItem!.Name);
        Assert.StartsWith(Path.Combine(_tempRoot, "styles"), manager.SelectedItem.Entry.FolderPath, StringComparison.OrdinalIgnoreCase);
        Assert.Single(_dialogs.StyleKitEditors);
        manager.Dispose();
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
    ///     Test that the chat panel is part of the composition, is joined to the composer and the annotation layer, sends a
    ///     message of the open project into its transcript, and drops the transcript when the project is closed.
    /// </summary>
    [Fact]
    public async Task AppShellSubsystem_ComposeApplication_ChatPanelFollowsTheOpenProject()
    {
        // Arrange: a composition with no project open
        var composition = Compose();
        var beforeProject = composition.Chat.CanTalk;

        // Act: create a project, send a message through the chat panel, then close the project
        await composition.Projects.CreateAsync(Projects, "Demo", TestContext.Current.CancellationToken);
        var withProject = composition.Chat.CanTalk;
        composition.Composer.InsertTyped("hello");
        await composition.Chat.SendCommand.ExecuteAsync(null);
        var sent = composition.Chat.Transcript.Count;
        composition.Projects.Close();

        // Assert: the panel works with the composition's composer, can be used only while a project is open, kept one
        // message, and has none after the close
        Assert.Same(composition.Composer, composition.Chat.Composer);
        Assert.False(beforeProject);
        Assert.True(withProject);
        Assert.Equal(1, sent);
        Assert.Empty(composition.Chat.Transcript);
    }

    /// <summary>
    ///     Test that composing the application places the chat panel in the workspace and the shell, the workspace shows it
    ///     only while a project is open, the dialog service becomes the panel's launcher of the payload inspector, and the
    ///     talk key of the shell reaches the panel.
    /// </summary>
    [Fact]
    public async Task AppShellSubsystem_ComposeApplication_ChatIsTheChatColumnAndTheTalkKeyTarget()
    {
        // Arrange: a composition with no project open
        var composition = Compose();
        var beforeProject = composition.Shell.Workspace.IsChatVisible;

        // Act: create a project and hold the talk key of the shell
        await composition.Projects.CreateAsync(Projects, "Demo", TestContext.Current.CancellationToken);
        var withProject = composition.Shell.Workspace.IsChatVisible;
        await composition.Shell.BeginPushToTalkAsync();
        var status = composition.Composer.StatusText;
        await composition.Shell.EndPushToTalkAsync();
        composition.Projects.Close();
        var afterClose = composition.Shell.Workspace.IsChatVisible;

        // Assert: the chat is in the workspace, shown only with a project, launches the inspector through the dialog
        // service, and is what the talk key reached (no speech model is chosen, so the composer says why nothing started)
        Assert.Same(composition.Chat, composition.Shell.Workspace.Chat);
        Assert.Same(composition.Chat, composition.Shell.PushToTalk);
        Assert.Same(_dialogs, composition.Chat.InspectorLauncher);
        Assert.False(beforeProject);
        Assert.True(withProject);
        Assert.False(afterClose);
        Assert.False(composition.Shell.IsPushToTalkActive);
        Assert.NotEmpty(status);
    }

    /// <summary>
    ///     Test that composing the application wires the style kits: the catalog reads the kits that come with the program in
    ///     the background, the picker offers them, a new project starts with the default kit of the user, the two style tools
    ///     are described, and the exporter asks the user for a size from the kit and the application.
    /// </summary>
    [Fact]
    public async Task AppShellSubsystem_ComposeApplication_StyleKitsPickerDefaultToolsAndExporterAreWired()
    {
        // Arrange: a composition whose user chose the Clean kit as the default for new projects
        var token = TestContext.Current.CancellationToken;
        var composition = Compose();
        composition.Settings.Update(settings => settings with { Styles = settings.Styles with { DefaultKitId = "clean" } });
        await composition.StyleCatalog.RefreshAsync(token);
        var idsBefore = composition.StyleCatalog.Entries.Select(entry => entry.Kit.Id).ToArray();
        var visibleBefore = composition.StylePicker.IsVisible;
        _dialogs.OnNewProject = dialog =>
        {
            dialog.ProjectName = "Styled";
            dialog.OkCommand.Execute(null);
        };

        // Act: create a project, and then export the picture that is shown in it
        await composition.Shell.NewProjectCommand.ExecuteAsync(null);
        var folder = composition.Shell.CurrentProject!.FolderPath;
        await File.WriteAllTextAsync(
            Path.Combine(folder, "logo.svg"),
            "<svg xmlns='http://www.w3.org/2000/svg' width='32' height='16'><rect width='32' height='16' fill='#00ff00'/></svg>",
            token);
        composition.Shell.Workspace.Explorer.Refresh();
        composition.Shell.Workspace.Explorer.SelectFile(Path.Combine(folder, "logo.svg"));
        await composition.Preview.RenderCompletion;
        await composition.Shell.ExportPngCommand.ExecuteAsync(null);

        // Assert: the kits of the program were found, the default became the style of the new project, the picker shows
        // it, the tools are described, and the exporter offered the sizes
        Assert.Equal(["clean", "dema", "slides-dark"], idsBefore.Order(StringComparer.Ordinal));
        Assert.False(visibleBefore);
        Assert.Equal("clean", composition.ActiveStyle.ActiveId);
        Assert.Equal("clean", composition.StylePicker.SelectedItem!.Id);
        Assert.True(composition.StylePicker.IsVisible);
        Assert.Same(composition.StylePicker, composition.Shell.StylePicker);
        Assert.Equal(["style_get", "style_check"], composition.StyleToolDescriptors.Select(tool => tool.Name));
        Assert.Equal(1, _dialogs.PresetOfferCount);
    }

    /// <summary>
    ///     Test that Check Against Style on the composed shell measures the shown picture against the kit of the project and
    ///     shows the result among the notes of the preview, tells the user when the project has no style, and that the notes
    ///     are gone when the picture is drawn again.
    /// </summary>
    [Fact]
    public async Task AppShellSubsystem_CheckAgainstStyle_ComposedShell_ShowsNotesInThePreview()
    {
        // Arrange: a project that uses the Clean kit and shows a picture that is mostly a lime green that the kit does not have
        var token = TestContext.Current.CancellationToken;
        var composition = Compose();
        await composition.StyleCatalog.RefreshAsync(token);
        _dialogs.OnNewProject = dialog =>
        {
            dialog.ProjectName = "Checked";
            dialog.OkCommand.Execute(null);
        };
        await composition.Shell.NewProjectCommand.ExecuteAsync(null);
        var folder = composition.Shell.CurrentProject!.FolderPath;
        await File.WriteAllTextAsync(
            Path.Combine(folder, "lime.svg"),
            "<svg xmlns='http://www.w3.org/2000/svg' width='40' height='40'><rect width='40' height='40' fill='#00FF00'/></svg>",
            token);
        composition.Shell.Workspace.Explorer.Refresh();
        composition.Shell.Workspace.Explorer.SelectFile(Path.Combine(folder, "lime.svg"));
        await composition.Preview.RenderCompletion;
        var enabledWithPicture = composition.Shell.CheckAgainstStyleCommand.CanExecute(null);

        // Act: check with no style, then choose Clean and check again
        await composition.Shell.CheckAgainstStyleCommand.ExecuteAsync(null);
        var messagesWithoutStyle = _dialogs.Messages.ToArray();
        var notesWithoutStyle = composition.Preview.Diagnostics.Count;
        await composition.ActiveStyle.SetActiveAsync("clean");
        await composition.Shell.CheckAgainstStyleCommand.ExecuteAsync(null);
        var notes = composition.Preview.Diagnostics.ToArray();
        var expanded = composition.Preview.AreDiagnosticsExpanded;

        // Act: draw another picture
        await File.WriteAllTextAsync(
            Path.Combine(folder, "plain.svg"),
            "<svg xmlns='http://www.w3.org/2000/svg' width='40' height='40'><rect width='40' height='40' fill='#FFFFFF'/></svg>",
            token);
        composition.Shell.Workspace.Explorer.Refresh();
        composition.Shell.Workspace.Explorer.SelectFile(Path.Combine(folder, "plain.svg"));
        await composition.Preview.RenderCompletion;

        // Assert: the command was enabled, the user was told about the missing style, and the notes name the lime picture
        Assert.True(enabledWithPicture);
        Assert.Equal("Check Against Style", Assert.Single(messagesWithoutStyle).Title);
        Assert.Contains("no style kit", messagesWithoutStyle[0].Message, StringComparison.Ordinal);
        Assert.Equal(0, notesWithoutStyle);
        Assert.Contains(notes, note => note.Code == "style-summary" && note.Message.Contains("FAILED", StringComparison.Ordinal));
        Assert.Contains(notes, note => note.Code == "style-off-palette" && note.Message.Contains("#00FF00", StringComparison.OrdinalIgnoreCase));
        Assert.True(expanded);
        Assert.DoesNotContain(composition.Preview.Diagnostics, note => note.Code.StartsWith("style-", StringComparison.Ordinal));
    }

    /// <summary>
    ///     Test that composing the application with the folders for the data of the AI copilot gives the chat panel a copilot
    ///     and the AI Provider page a Check connection that asks the copilot's readiness check, with the stand-ins that the
    ///     test passes for the Ollama server, and that composing without the folders builds no copilot and no check.
    /// </summary>
    [Fact]
    public async Task AppShellSubsystem_ComposeApplication_CopilotIsBuiltFromItsFoldersAndFeedsTheSettingsPage()
    {
        // Arrange: a composition without the folders, and one with them over a scripted Ollama server
        AiProviderSettingsPageViewModel? plainPage = null;
        _dialogs.OnSettings = dialog =>
        {
            plainPage = Assert.IsType<AiProviderSettingsPageViewModel>(dialog.Pages[0]);
            dialog.CancelCommand.Execute(null);
        };
        var plain = Compose();
        await plain.Shell.OpenSettingsCommand.ExecuteAsync(null);
        var rig = ComposeWithCopilot();
        rig.Probe.Models = new OllamaModelList(true, ["gemma3:latest"]);
        AiProviderSettingsPageViewModel? page = null;
        _dialogs.OnSettings = dialog =>
        {
            page = Assert.IsType<AiProviderSettingsPageViewModel>(dialog.Pages[0]);
            page.SelectedProvider = page.Providers[1];
            page.ModelName = "gemma3";
            dialog.CancelCommand.Execute(null);
        };

        // Act: open the settings of the composition that has the copilot and check the connection on the page
        await rig.Composition.Shell.OpenSettingsCommand.ExecuteAsync(null);
        await page!.CheckConnectionCommand.ExecuteAsync(null);

        // Assert: only the second composition has a copilot and a check, and the check asked the stand-in server, in the
        // address of the settings page
        Assert.Null(plain.Agent);
        Assert.False(plainPage!.HasConnectionCheck);
        Assert.IsType<AgentService>(rig.Composition.Agent);
        Assert.True(page.HasConnectionCheck);
        Assert.True(page.IsCheckConnectionOk);
        Assert.Equal(new Uri("http://localhost:11434"), Assert.Single(rig.Probe.ListQuestions).Endpoint);
        Assert.Empty(rig.Factory.Requests);
    }

    /// <summary>
    ///     Test that the chat panel's request for the settings opens the settings dialog of the shell, and that a change of
    ///     the AI provider settings makes the panel check the provider again, while a change of other settings does not.
    /// </summary>
    [Fact]
    public async Task AppShellSubsystem_ComposeApplication_CopilotAsksForSettingsAndIsCheckedAgainWhenTheyChange()
    {
        // Arrange: a composition whose settings choose GitHub Copilot, which cannot be used on this machine because the
        // CLI is not signed in, with the panel having checked that
        var token = TestContext.Current.CancellationToken;
        var rig = ComposeWithCopilot();
        rig.Probe.Models = new OllamaModelList(true, ["gemma3:latest"]);
        await rig.Composition.Chat.RefreshReadinessAsync();
        var before = rig.Composition.Chat.ReadinessMessage;
        var questionsBefore = rig.Probe.ListQuestions.Count;

        // Act: the panel asks for the settings, then another setting changes, then the user chooses Ollama
        rig.Composition.Chat.OpenSettingsCommand.Execute(null);
        var opened = _dialogs.SettingsCount;
        rig.Composition.Settings.Update(settings => settings with { RecentProjects = ["C:\\Work\\a.canvaspilot"] });
        await Task.Delay(50, token);
        var questionsAfterOther = rig.Probe.ListQuestions.Count;
        rig.Composition.Settings.Update(settings => settings with { AiProvider = new AiProviderSettings { Provider = AiProviderKind.Ollama, ModelName = "gemma3" } });
        await WaitUntilAsync(() => !rig.Composition.Chat.HasReadinessIssue, token);

        // Assert: the dialog was opened once, the other change asked nothing, and the provider change was checked: the
        // Copilot problem is gone and the Ollama server was asked
        Assert.Contains("gh auth login", before, StringComparison.Ordinal);
        Assert.Equal(1, opened);
        Assert.Equal(questionsBefore, questionsAfterOther);
        Assert.Equal(string.Empty, rig.Composition.Chat.ReadinessMessage);
        Assert.True(rig.Probe.ListQuestions.Count > questionsAfterOther);
    }

    /// <summary>
    ///     Test that a file that the copilot writes while it answers is shown in the project explorer and selected, which
    ///     draws its picture, and that the message, the answer, and the tool row are in the transcript.
    /// </summary>
    [Fact]
    public async Task AppShellSubsystem_ComposeApplication_CopilotWritesFile_ExplorerSelectsItAndPreviewDrawsIt()
    {
        // Arrange: a composition over a scripted model that writes a drawing, with a project open and Ollama chosen
        var token = TestContext.Current.CancellationToken;
        const string drawing = "<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"40\" height=\"20\"><rect width=\"40\" height=\"20\" fill=\"#0000ff\"/></svg>";
        var model = new ScriptedChatClient(
            [
                new ChatResponseUpdate(ChatRole.Assistant, "I will draw a box. "),
                new ChatResponseUpdate(
                    ChatRole.Assistant,
                    [new FunctionCallContent("w1", "project_write", new Dictionary<string, object?> { ["path"] = "box.svg", ["content"] = drawing })]),
            ],
            [new ChatResponseUpdate(ChatRole.Assistant, "The box is drawn.")]);
        var rig = ComposeWithCopilot(model);
        rig.Composition.Settings.Update(settings => settings with { AiProvider = new AiProviderSettings { Provider = AiProviderKind.Ollama, ModelName = "gemma3" } });
        var project = (await rig.Composition.Projects.CreateAsync(Projects, "Demo", token)).Project!;

        // Act: send a message and wait for the answer
        rig.Composition.Composer.InsertTyped("Draw a box");
        await rig.Composition.Chat.SendCommand.ExecuteAsync(null);
        await rig.Composition.Chat.WaitForTurnAsync().WaitAsync(TimeSpan.FromSeconds(20), token);
        await rig.Composition.Preview.RenderCompletion;

        // Assert: the file is in the project, the explorer lists and selects it, and the preview shows its picture
        var path = Path.Combine(project.FolderPath, "box.svg");
        Assert.Equal(drawing, await File.ReadAllTextAsync(path, token));
        Assert.Equal(path, rig.Composition.Shell.Workspace.Explorer.SelectedFile, ignoreCase: true);
        Assert.True(rig.Composition.Preview.HasImage);
        Assert.Equal((40, 20), (rig.Composition.Preview.Image!.Width, rig.Composition.Preview.Image.Height));
        Assert.Equal(
            [TranscriptRowKind.User, TranscriptRowKind.Assistant, TranscriptRowKind.Tool, TranscriptRowKind.Assistant],
            rig.Composition.Chat.Transcript.Select(row => row.Kind));
    }

    /// <summary>
    ///     Test that disposing the composition disposes the copilot and its factory once, before the services it listens to,
    ///     and that nothing reacts to the settings afterwards.
    /// </summary>
    [Fact]
    public async Task AppShellSubsystem_ComposeApplication_DisposeStopsTheCopilotAndTheWiring()
    {
        // Arrange: a composition with the copilot, whose chat panel has checked the provider
        var token = TestContext.Current.CancellationToken;
        var rig = ComposeWithCopilot();
        rig.Probe.Models = new OllamaModelList(true, ["gemma3:latest"]);
        await rig.Composition.Chat.RefreshReadinessAsync();
        var questions = rig.Probe.ListQuestions.Count;

        // Act: dispose, dispose again, and change the provider
        rig.Composition.Dispose();
        rig.Composition.Dispose();
        rig.Composition.Settings.Update(settings => settings with { AiProvider = new AiProviderSettings { Provider = AiProviderKind.Ollama, ModelName = "gemma3" } });
        await Task.Delay(50, token);

        // Assert: the factory was disposed once, and the change was not looked at
        Assert.Equal(1, rig.Factory.DisposeCount);
        Assert.Equal(questions, rig.Probe.ListQuestions.Count);
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
            speechModels: SpeechTestData.Models(),
            styleUserFolder: Path.Combine(_tempRoot, "styles"));
        composition.Shell.AttachDialogService(_dialogs);
        _compositions.Add(composition);
        return composition;
    }

    /// <summary>
    ///     What a composition with the AI copilot is built over, and what a test looks at.
    /// </summary>
    /// <param name="Composition">The composed application, with the dialog service attached.</param>
    /// <param name="Factory">The factory that makes the agents on the scripted model.</param>
    /// <param name="Probe">The stand-in for the Ollama server.</param>
    private sealed record CopilotRig(ApplicationComposition Composition, ScriptedAgentFactory Factory, FakeOllamaProbe Probe);

    /// <summary>
    ///     Composes the application with the AI copilot on the test folders: the data folders are in the temporary root, the
    ///     Copilot CLI and the processes are those of the development machine without a sign-in, and the Ollama server and
    ///     the model are stand-ins.
    /// </summary>
    /// <param name="model">The scripted model; one that has no answers when <see langword="null" />.</param>
    /// <returns>The composition and what it is built over.</returns>
    private CopilotRig ComposeWithCopilot(IChatClient? model = null)
    {
        Directory.CreateDirectory(Projects);
        var logger = new RollingFileLogger(Path.Combine(_tempRoot, "logs"));
        var settings = new SettingsService(Path.Combine(_tempRoot, "settings"), logger);
        var runner = new FakeProcessRunner();
        runner.AnswerVersion(DevMachine.RealCli, "1.0.114");
        var factory = new ScriptedAgentFactory(model ?? new ScriptedChatClient());
        var probe = new FakeOllamaProbe();
        var composition = ApplicationComposition.Create(
            logger,
            settings,
            new FakeRefreshScheduler(),
            defaultProjectsFolder: Projects,
            audioDevices: SpeechTestData.AudioDevices([], []),
            speechModels: SpeechTestData.Models(),
            styleUserFolder: Path.Combine(_tempRoot, "styles"),
            localDataRoot: Path.Combine(_tempRoot, "local"),
            agentFactory: factory,
            processRunner: runner,
            ollamaProbe: probe,
            environment: DevMachine.CreateEnvironment(),
            cliFileSystem: DevMachine.CreateFileSystem());
        composition.Shell.AttachDialogService(_dialogs);
        _compositions.Add(composition);
        return new CopilotRig(composition, factory, probe);
    }

    /// <summary>
    ///     Waits until a condition holds, which a check that runs in the background needs.
    /// </summary>
    /// <param name="condition">The condition.</param>
    /// <param name="cancellationToken">A token that ends the wait.</param>
    /// <returns>A task that completes when the condition holds or fails after ten seconds.</returns>
    private static async Task WaitUntilAsync(Func<bool> condition, CancellationToken cancellationToken)
    {
        var deadline = DateTime.UtcNow.AddSeconds(10);
        while (!condition())
        {
            Assert.True(DateTime.UtcNow < deadline, "The condition did not hold within ten seconds.");
            await Task.Delay(10, cancellationToken);
        }
    }
}
