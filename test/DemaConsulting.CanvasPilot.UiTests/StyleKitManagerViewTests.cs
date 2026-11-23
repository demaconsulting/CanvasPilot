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

using System.Text.Json;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using DemaConsulting.CanvasPilot.PreviewSubsystem;
using DemaConsulting.CanvasPilot.ProjectSubsystem;
using DemaConsulting.CanvasPilot.SettingsSubsystem;
using DemaConsulting.CanvasPilot.StyleSubsystem;

namespace DemaConsulting.CanvasPilot.UiTests;

/// <summary>
///     Hand-written windows and pickers of the Style Kits window for the headless tests: it records what was asked for and
///     answers as the test says, so no platform picker or modal window is needed.
/// </summary>
internal sealed class FakeStyleKitDialogs : IStyleKitDialogs
{
    /// <summary>
    ///     Gets or sets what the user does in the editor; when <see langword="null" />, the user cancels.
    /// </summary>
    public Action<StyleKitEditorViewModel>? OnEditor { get; set; }

    /// <summary>
    ///     Gets or sets the zip file that the user chooses to import.
    /// </summary>
    public string? ZipToImport { get; set; }

    /// <summary>
    ///     Gets or sets the zip file that the user chooses to write.
    /// </summary>
    public string? ZipToExport { get; set; }

    /// <summary>
    ///     Gets or sets whether the user confirms.
    /// </summary>
    public bool ConfirmResult { get; set; }

    /// <summary>
    ///     Gets or sets the text that the user types in a prompt.
    /// </summary>
    public string? PromptResult { get; set; }

    /// <summary>
    ///     Gets the questions that were asked, as the title.
    /// </summary>
    public List<string> Questions { get; } = [];

    /// <inheritdoc />
    public Task ShowStyleKitManagerAsync(StyleKitManagerViewModel viewModel) => Task.CompletedTask;

    /// <inheritdoc />
    public Task ShowStyleKitEditorAsync(StyleKitEditorViewModel viewModel)
    {
        if (OnEditor is null)
        {
            viewModel.CancelCommand.Execute(null);
        }
        else
        {
            OnEditor(viewModel);
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task<string?> PickZipToImportAsync(string? initialFolder) => Task.FromResult(ZipToImport);

    /// <inheritdoc />
    public Task<string?> PickZipToExportAsync(string suggestedFileName) => Task.FromResult(ZipToExport);

    /// <inheritdoc />
    public Task<IReadOnlyList<string>> PickExampleFilesAsync(string? initialFolder) => Task.FromResult<IReadOnlyList<string>>([]);

    /// <inheritdoc />
    public Task<bool> ConfirmAsync(string title, string message, string confirmText)
    {
        Questions.Add(title);
        return Task.FromResult(ConfirmResult);
    }

    /// <inheritdoc />
    public Task<string?> PromptAsync(string title, string message, string initialText, string confirmText)
    {
        Questions.Add(title);
        return Task.FromResult(PromptResult);
    }
}

/// <summary>
///     Headless UI tests for the window that manages style kits: the groups and cards, the preview of a chosen kit with the
///     example images drawn by the real renderer, and the buttons, over a real catalog, project service, and archive on
///     temporary folders with the kits that come with the program.
/// </summary>
public sealed class StyleKitManagerViewTests : IDisposable
{
    /// <summary>
    ///     Temporary root removed on disposal.
    /// </summary>
    private readonly string _root = Directory.CreateTempSubdirectory("canvaspilot-ui-style-manager-").FullName;

    /// <summary>
    ///     The real project service.
    /// </summary>
    private readonly ProjectService _projects = new();

    /// <summary>
    ///     The real settings service.
    /// </summary>
    private readonly SettingsService _settings;

    /// <summary>
    ///     The catalog of kits, with the kits that come with the program.
    /// </summary>
    private readonly StyleKitCatalog _catalog;

    /// <summary>
    ///     The kit that the open project uses.
    /// </summary>
    private readonly StyleSelection _selection;

    /// <summary>
    ///     The windows and pickers.
    /// </summary>
    private readonly FakeStyleKitDialogs _dialogs = new();

    /// <summary>
    ///     Initializes a new instance of the <see cref="StyleKitManagerViewTests" /> class.
    /// </summary>
    public StyleKitManagerViewTests()
    {
        _settings = new SettingsService(Path.Combine(_root, "settings"));
        _settings.Load();
        _catalog = new StyleKitCatalog(_projects, _settings, userFolder: Path.Combine(_root, "styles"));
        _selection = new StyleSelection(_projects, _catalog, _settings);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _selection.Dispose();
        _catalog.Dispose();
        _projects.Close();
        for (var attempt = 0; attempt < 40 && Directory.Exists(_root); attempt++)
        {
            try
            {
                Directory.Delete(_root, recursive: true);
            }
            catch (IOException)
            {
                Thread.Sleep(50);
            }
        }
    }

    /// <summary>
    ///     Test that the window shows the groups and a card for each kit that comes with the program, with a strip of colors,
    ///     and that choosing a kit shows its colors, its fonts, the text for an assistant, and the example images drawn by the
    ///     renderer.
    /// </summary>
    [AvaloniaFact]
    public async Task StyleKitManagerView_Show_ListsGroupsAndChoosingAKitShowsThePreview()
    {
        // Arrange: a window over the kits that come with the program
        var (window, manager) = await ShowAsync();

        // Act: read the groups and the cards, choose the DEMA kit, and wait for its pictures
        var titles = Texts(window, "StyleKitGroupTitle");
        var cards = Cards(window);
        var names = cards.Select(card => ((StyleKitItemViewModel)card.DataContext!).Name).ToArray();
        var noSelection = SettingsUiHelpers.Find<TextBlock>(window, "StyleKitNoSelection").IsVisible;
        SettingsUiHelpers.Click(window, cards.Single(card => ((StyleKitItemViewModel)card.DataContext!).Id == "dema"));
        await manager.ThumbnailTask;
        Dispatcher.UIThread.RunJobs();
        var name = SettingsUiHelpers.Find<TextBlock>(window, "StylePreviewName").Text;
        var swatches = SettingsUiHelpers.Find<ItemsControl>(window, "StylePreviewSwatches").ItemCount;
        var thumbnails = window.GetVisualDescendants().OfType<Image>().Where(image => AutomationProperties.GetAutomationId(image) == "StyleThumbnailImage").ToArray();
        var labels = Texts(window, "StyleThumbnailLabel");
        var size = SettingsUiHelpers.Find<TextBlock>(window, "StylePreviewGuidanceSize").Text;

        // Assert
        Assert.Equal(["My kits", "Shared", "Built-in"], titles);
        Assert.Equal(["Clean", "DEMA", "Slides Dark"], names);
        Assert.True(noSelection);
        Assert.Equal("DEMA", name);
        Assert.Equal(manager.SelectedItem!.Swatches.Count, swatches);
        Assert.Equal(3, thumbnails.Length);
        Assert.All(thumbnails, image => Assert.NotNull(image.Source));
        Assert.All(thumbnails, image => Assert.True(image.IsVisible));
        Assert.Equal(3, labels.Length);
        Assert.EndsWith("of 4,000 characters", size, StringComparison.Ordinal);
        Assert.Contains("selected", cards.Single(card => ((StyleKitItemViewModel)card.DataContext!).Id == "dema").Classes);
        Assert.False(SettingsUiHelpers.Find<TextBlock>(window, "StyleKitNoSelection").IsVisible);

        window.Close();
    }

    /// <summary>
    ///     Test that a kit that comes with the program has Edit and Delete off and Duplicate, Export, and New on, and that a
    ///     kit of the user has them all on.
    /// </summary>
    [AvaloniaFact]
    public async Task StyleKitManagerView_Buttons_BuiltInAndOwnKits_EnableWhatApplies()
    {
        // Arrange: a window with a kit of the user
        var own = _catalog.CreateBlank("Mine", StyleKitLocation.User);
        var (window, manager) = await ShowAsync();

        // Act: choose a built-in kit and read the buttons, then the kit of the user
        manager.SelectItemCommand.Execute(manager.Groups[^1].Items[0]);
        Dispatcher.UIThread.RunJobs();
        var builtIn = ButtonStates(window);
        var readOnlyText = SettingsUiHelpers.Find<TextBlock>(window, "StylePreviewReadOnly").Text;
        manager.SelectItemCommand.Execute(manager.Groups[0].Items.Single(item => item.Id == own.Kit.Id));
        Dispatcher.UIThread.RunJobs();
        var mine = ButtonStates(window);

        // Assert
        Assert.False(builtIn["StyleKitEditButton"]);
        Assert.False(builtIn["StyleKitDeleteButton"]);
        Assert.True(builtIn["StyleKitDuplicateButton"]);
        Assert.True(builtIn["StyleKitExportButton"]);
        Assert.True(builtIn["StyleKitNewButton"]);
        Assert.True(builtIn["StyleKitImportButton"]);
        Assert.False(builtIn["StyleKitUseButton"]);
        Assert.False(builtIn["StyleKitCopyIntoProjectButton"]);
        Assert.True(mine["StyleKitEditButton"]);
        Assert.True(mine["StyleKitDeleteButton"]);
        Assert.Contains("cannot be changed", readOnlyText, StringComparison.Ordinal);

        window.Close();
    }

    /// <summary>
    ///     Test that Use in this project writes the id of the kit into the project file, that the card says that the kit is
    ///     in use, and that Copy into project makes a copy in the folder of the project.
    /// </summary>
    [AvaloniaFact]
    public async Task StyleKitManagerView_UseInProjectAndCopyIntoProject_ChangeTheProject()
    {
        // Arrange: an open project and a window with a built-in kit chosen
        var created = await _projects.CreateAsync(Directory.CreateDirectory(Path.Combine(_root, "projects")).FullName, "Demo", TestContext.Current.CancellationToken);
        Assert.True(created.Succeeded, created.Error);
        var (window, manager) = await ShowAsync();
        var clean = manager.Groups[^1].Items.Single(item => item.Id == "clean");
        manager.SelectItemCommand.Execute(clean);
        Dispatcher.UIThread.RunJobs();

        // Act: copy the kit into the project, choose the built-in kit again, and use the copy
        SettingsUiHelpers.Click(window, SettingsUiHelpers.Find<Button>(window, "StyleKitCopyIntoProjectButton"));
        await WaitAsync(() => manager.SelectedItem?.Entry.Location == StyleKitLocation.Project, () => manager.StatusMessage);
        var copied = File.Exists(Path.Combine(created.Project!.FolderPath, "styles", "clean", "style.json"));
        SettingsUiHelpers.Click(window, SettingsUiHelpers.Find<Button>(window, "StyleKitUseButton"));
        await WaitAsync(() => _selection.ActiveId == "clean");
        Dispatcher.UIThread.RunJobs();
        var projectFile = JsonDocument.Parse(await File.ReadAllTextAsync(created.Project.FilePath, TestContext.Current.CancellationToken));
        var inUse = Cards(window).Where(card => ((StyleKitItemViewModel)card.DataContext!).IsInUse).Select(card => ((StyleKitItemViewModel)card.DataContext!).PlaceText).ToArray();

        // Assert
        Assert.True(copied);
        Assert.Equal("clean", projectFile.RootElement.GetProperty("activeStyleKit").GetString());
        Assert.Equal(["project"], inUse);
        Assert.Equal(["This project", "My kits", "Shared", "Built-in"], Texts(window, "StyleKitGroupTitle"));

        window.Close();
    }

    /// <summary>
    ///     Test that Delete asks and removes the kit of the user and its card, that Export writes a zip file, that Import
    ///     brings the kit back, and that Edit shows the kit as it was saved.
    /// </summary>
    [AvaloniaFact]
    public async Task StyleKitManagerView_DeleteExportImportAndEdit_ChangeTheListAndTheFiles()
    {
        // Arrange: a window with a kit of the user chosen
        var own = _catalog.CreateBlank("Mine", StyleKitLocation.User);
        var zip = Path.Combine(_root, "mine.zip");
        var (window, manager) = await ShowAsync();
        manager.SelectItemCommand.Execute(manager.Groups[0].Items.Single(item => item.Id == own.Kit.Id));
        Dispatcher.UIThread.RunJobs();

        // Act: edit the kit in a fake editor that renames it and saves
        _dialogs.OnEditor = editor =>
        {
            editor.Name = "Mine renamed";
            editor.SaveCommand.Execute(null);
        };
        SettingsUiHelpers.Click(window, SettingsUiHelpers.Find<Button>(window, "StyleKitEditButton"));
        await WaitAsync(() => manager.SelectedItem?.Name == "Mine renamed");
        var renamed = Cards(window).Select(card => ((StyleKitItemViewModel)card.DataContext!).Name).Contains("Mine renamed");

        // Act: export the kit, say no to deleting, then yes, and import it again
        _dialogs.ZipToExport = zip;
        SettingsUiHelpers.Click(window, SettingsUiHelpers.Find<Button>(window, "StyleKitExportButton"));
        await WaitAsync(() => File.Exists(zip));
        _dialogs.ConfirmResult = false;
        SettingsUiHelpers.Click(window, SettingsUiHelpers.Find<Button>(window, "StyleKitDeleteButton"));
        await WaitAsync(() => _dialogs.Questions.Contains("Delete Style Kit"));
        var stillThere = Directory.Exists(own.FolderPath);
        _dialogs.ConfirmResult = true;
        SettingsUiHelpers.Click(window, SettingsUiHelpers.Find<Button>(window, "StyleKitDeleteButton"));
        await WaitAsync(() => !Directory.Exists(own.FolderPath));
        Dispatcher.UIThread.RunJobs();
        var deleted = Cards(window).All(card => ((StyleKitItemViewModel)card.DataContext!).Id != own.Kit.Id);
        _dialogs.ZipToImport = zip;
        SettingsUiHelpers.Click(window, SettingsUiHelpers.Find<Button>(window, "StyleKitImportButton"));
        await WaitAsync(() => Directory.Exists(own.FolderPath));
        await WaitAsync(() => manager.SelectedItem?.Name == "Mine renamed");
        Dispatcher.UIThread.RunJobs();
        var status = SettingsUiHelpers.Find<TextBlock>(window, "StyleKitStatus");

        // Assert
        Assert.True(renamed);
        Assert.True(stillThere);
        Assert.True(deleted);
        Assert.Equal("Mine renamed", StyleKitSerializer.ReadFolder(own.FolderPath).Kit!.Name);
        Assert.True(status.IsVisible);
        Assert.StartsWith("Imported the kit 'Mine renamed'", status.Text, StringComparison.Ordinal);

        window.Close();
    }

    /// <summary>
    ///     Test that New asks for a name and makes the kit, that a shared folder that cannot be reached is noted in its group,
    ///     and that Close closes the window and Refresh reads the kits again.
    /// </summary>
    [AvaloniaFact]
    public async Task StyleKitManagerView_NewNotesAndClose_ShowTheKitTheNoteAndCloseTheWindow()
    {
        // Arrange: a shared folder that is not there, and a window
        var missing = Path.Combine(_root, "team");
        _settings.Update(settings => settings with { Styles = settings.Styles with { SharedKitFolders = [missing] } });
        await _catalog.RefreshAsync(TestContext.Current.CancellationToken);
        var (window, manager) = await ShowAsync();
        var closed = false;
        window.Closed += (_, _) => closed = true;

        // Act: make a kit, read the notes, refresh, and close
        _dialogs.PromptResult = "Poster";
        SettingsUiHelpers.Click(window, SettingsUiHelpers.Find<Button>(window, "StyleKitNewButton"));
        await WaitAsync(() => manager.SelectedItem?.Name == "Poster");
        Dispatcher.UIThread.RunJobs();
        var notes = Texts(window, "StyleKitGroupNote");
        var previewName = SettingsUiHelpers.Find<TextBlock>(window, "StylePreviewName").Text;
        SettingsUiHelpers.Click(window, SettingsUiHelpers.Find<Button>(window, "StyleKitRefreshButton"));
        await WaitAsync(() => manager.StatusMessage == "The kits were read again.");
        SettingsUiHelpers.Click(window, SettingsUiHelpers.Find<Button>(window, "StyleKitCloseButton"));

        // Assert
        Assert.Contains(notes, note => note.StartsWith("Not reachable: " + missing, StringComparison.Ordinal));
        Assert.Equal("Poster", previewName);
        Assert.True(closed);
    }

    /// <summary>
    ///     Test that the chosen kit is marked with a check besides its accent outline, so the choice is not told by color alone,
    ///     and that the other cards have no check.
    /// </summary>
    [AvaloniaFact]
    public async Task StyleKitManagerView_ChoosingAKit_MarksOnlyThatCardWithACheck()
    {
        // Arrange: a window over the kits that come with the program
        var (window, _) = await ShowAsync();
        var cards = Cards(window);

        // Act: choose the second card
        SettingsUiHelpers.Click(window, cards[1]);
        Dispatcher.UIThread.RunJobs();

        // Assert: only the chosen card shows its check
        var checks = cards.Select(card => card.GetVisualDescendants().OfType<PathIcon>().Count(icon => icon.IsEffectivelyVisible && icon.Foreground is not null && icon.Parent is Grid)).ToArray();
        Assert.Contains("selected", cards[1].Classes);
        Assert.Equal(1, checks[1]);
        Assert.All(checks.Where((_, index) => index != 1), count => Assert.Equal(0, count));

        window.Close();
    }

    /// <summary>
    ///     Makes the window over the real catalog, with its kits read, and shows it.
    /// </summary>
    /// <returns>The window and its view model.</returns>
    private async Task<(StyleKitManagerView Window, StyleKitManagerViewModel Manager)> ShowAsync()
    {
        await _catalog.RefreshAsync(TestContext.Current.CancellationToken);
        var manager = new StyleKitManagerViewModel(_catalog, _selection, new ImageRenderService(), _dialogs, _projects);
        var window = new StyleKitManagerView(manager);
        window.Show();
        Dispatcher.UIThread.RunJobs();
        return (window, manager);
    }

    /// <summary>
    ///     Reads the texts of the controls with an automation identifier.
    /// </summary>
    /// <param name="window">The window.</param>
    /// <param name="automationId">The identifier.</param>
    /// <returns>The texts, in order.</returns>
    private static string[] Texts(Window window, string automationId) =>
        window.GetVisualDescendants().OfType<TextBlock>().Where(block => AutomationProperties.GetAutomationId(block) == automationId).Select(block => block.Text ?? string.Empty).ToArray();

    /// <summary>
    ///     Finds the cards of the kits.
    /// </summary>
    /// <param name="window">The window.</param>
    /// <returns>The buttons of the cards, in order.</returns>
    private static Button[] Cards(Window window) =>
        window.GetVisualDescendants().OfType<Button>().Where(button => AutomationProperties.GetAutomationId(button) == "StyleKitCard").ToArray();

    /// <summary>
    ///     Reads whether each action button can be used.
    /// </summary>
    /// <param name="window">The window.</param>
    /// <returns>The state of each button, by automation identifier.</returns>
    private static Dictionary<string, bool> ButtonStates(Window window) =>
        window.GetVisualDescendants().OfType<Button>()
            .Select(button => (Id: AutomationProperties.GetAutomationId(button), button.IsEffectivelyEnabled))
            .Where(state => state.Id is not null && state.Id != "StyleKitCard")
            .ToDictionary(state => state.Id!, state => state.IsEffectivelyEnabled);

    /// <summary>
    ///     Lets the dispatcher and the tasks that the buttons started run until a condition holds.
    /// </summary>
    /// <param name="condition">The condition.</param>
    /// <param name="describe">Says what state things are in, for the message when the condition does not hold, or <see langword="null" />.</param>
    /// <returns>A task that completes when the condition holds.</returns>
    private static async Task WaitAsync(Func<bool> condition, Func<string>? describe = null)
    {
        for (var attempt = 0; attempt < 400 && !condition(); attempt++)
        {
            Dispatcher.UIThread.RunJobs();
            await Task.Delay(25, TestContext.Current.CancellationToken);
        }

        Dispatcher.UIThread.RunJobs();
        Assert.True(condition(), "The condition did not hold in time. " + describe?.Invoke());
    }
}
