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

using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using DemaConsulting.CanvasPilot.ProjectSubsystem;
using DemaConsulting.CanvasPilot.SettingsSubsystem;
using DemaConsulting.CanvasPilot.StyleSubsystem;

namespace DemaConsulting.CanvasPilot.UiTests;

/// <summary>
///     Headless UI tests for the Style Kits settings page: the list of shared folders with its buttons, the default kit, and
///     the page inside the settings dialog, with the real catalog of kits that come with the program.
/// </summary>
public sealed class StyleSettingsPageViewTests : IDisposable
{
    /// <summary>
    ///     Temporary root removed on disposal.
    /// </summary>
    private readonly string _root = Directory.CreateTempSubdirectory("canvaspilot-ui-style-settings-").FullName;

    /// <summary>
    ///     The real project service, which the catalog follows.
    /// </summary>
    private readonly ProjectService _projects = new();

    /// <summary>
    ///     The real settings service.
    /// </summary>
    private readonly SettingsService _settings;

    /// <summary>
    ///     The catalog of the kits that come with the program.
    /// </summary>
    private readonly StyleKitCatalog _catalog;

    /// <summary>
    ///     Initializes a new instance of the <see cref="StyleSettingsPageViewTests" /> class.
    /// </summary>
    public StyleSettingsPageViewTests()
    {
        _settings = new SettingsService(Path.Combine(_root, "settings"));
        _settings.Load();
        _catalog = new StyleKitCatalog(_projects, _settings, userFolder: Path.Combine(_root, "styles"));
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _catalog.Dispose();
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
    ///     Test that the page shows the folders with their status and the choices of the default kit with the stored one
    ///     selected.
    /// </summary>
    [AvaloniaFact]
    public async Task StyleSettingsPageView_Show_ListsFoldersAndTheDefaultKit()
    {
        // Arrange: kits that are read, and settings with a folder that is not there and a default kit
        await _catalog.RefreshAsync(TestContext.Current.CancellationToken);
        var missing = Path.Combine(_root, "team");
        var (window, page) = ShowPage(new AppSettings { Styles = new StyleSettings { SharedKitFolders = [missing], DefaultKitId = "slides-dark" } });

        // Act: read what the page shows
        var folders = SettingsUiHelpers.Find<ListBox>(window, "StyleSharedFolders");
        var combo = SettingsUiHelpers.Find<ComboBox>(window, "StyleDefaultKitComboBox");
        var labels = combo.Items.OfType<StyleKitChoice>().Select(choice => choice.Label).ToArray();

        // Assert: the folder is listed with its status, and the default kit is chosen among the kits of the program
        Assert.Equal([missing], folders.Items.OfType<StyleFolderItem>().Select(row => row.Path));
        Assert.StartsWith("Not read yet", folders.Items.OfType<StyleFolderItem>().Single().StatusText, StringComparison.Ordinal);
        Assert.Equal(StyleSettingsPageViewModel.NoStyleLabel, labels[0]);
        Assert.Contains("Clean (built-in)", labels);
        Assert.Contains("DEMA (built-in)", labels);
        Assert.Equal("slides-dark", ((StyleKitChoice)combo.SelectedItem!).Id);
        Assert.Equal("Style Kits", page.Title);

        window.Close();
    }

    /// <summary>
    ///     Test that the closed default kit list tells a screen reader the label that it shows, for no style, a kit, and a
    ///     default that no kit has, and never the text of the record behind it.
    /// </summary>
    [AvaloniaFact]
    public async Task StyleSettingsPageView_DefaultKit_AnnouncesTheShownLabelAndNotTheRecord()
    {
        // Arrange: kits that are read, and settings with a default that no kit has
        await _catalog.RefreshAsync(TestContext.Current.CancellationToken);
        var (window, _) = ShowPage(new AppSettings { Styles = new StyleSettings { DefaultKitId = "team-kit" } });
        var combo = SettingsUiHelpers.Find<ComboBox>(window, "StyleDefaultKitComboBox");
        var missing = SettingsUiHelpers.AnnouncedValue(combo);

        // Act: choose no style, then the DEMA kit, and read what the closed list tells a screen reader each time
        combo.SelectedIndex = 0;
        Dispatcher.UIThread.RunJobs();
        var noStyle = SettingsUiHelpers.AnnouncedValue(combo);
        combo.SelectedIndex = combo.Items.OfType<StyleKitChoice>().ToList().FindIndex(choice => choice.Id == "dema");
        Dispatcher.UIThread.RunJobs();
        var dema = SettingsUiHelpers.AnnouncedValue(combo);

        // Assert: each value is the shown label and not the record
        Assert.Equal("team-kit (not found)", missing);
        Assert.Equal(StyleSettingsPageViewModel.NoStyleLabel, noStyle);
        Assert.Equal("DEMA (built-in)", dema);
        Assert.All([missing, noStyle, dema], value => Assert.DoesNotContain(nameof(StyleKitChoice), value, StringComparison.Ordinal));

        window.Close();
    }

    /// <summary>
    ///     Test that a screen reader names every row of the shared folders by the path that it shows, and not by the text
    ///     of the record behind it.
    /// </summary>
    [AvaloniaFact]
    public void StyleSettingsPageView_SharedFolders_RowsAreNamedByTheirPath()
    {
        // Arrange: settings with two shared folders
        var one = Path.Combine(_root, "one");
        var two = Path.Combine(_root, "two");
        var (window, _) = ShowPage(new AppSettings { Styles = new StyleSettings { SharedKitFolders = [one, two] } });

        // Act: read the names of the rows
        var names = SettingsUiHelpers.AnnouncedRowNames(SettingsUiHelpers.Find<ListBox>(window, "StyleSharedFolders"));

        // Assert: each row is named by its path
        Assert.Equal([one, two], names);

        window.Close();
    }

    /// <summary>
    ///     Test that typing a path and clicking Add lists the folder, that Move up, Move down, and Remove act on the selected
    ///     folder, and that a path that is not a full path shows a message.
    /// </summary>
    [AvaloniaFact]
    public void StyleSettingsPageView_Buttons_AddMoveAndRemoveFolders()
    {
        // Arrange: a page with no folder
        var (window, page) = ShowPage(new AppSettings());
        var one = Path.Combine(_root, "one");
        var two = Path.Combine(_root, "two");

        // Act: add two folders by typing, a third that is not a full path, move the second up, and remove the first
        Add(window, one);
        Add(window, two);
        Add(window, "relative");
        var error = SettingsUiHelpers.Find<TextBlock>(window, "StyleFolderError");
        var errorShown = (error.IsVisible, error.Text);
        var list = SettingsUiHelpers.Find<ListBox>(window, "StyleSharedFolders");
        list.SelectedIndex = 1;
        Dispatcher.UIThread.RunJobs();
        SettingsUiHelpers.Click(window, SettingsUiHelpers.Find<Button>(window, "StyleFolderUpButton"));
        var afterUp = page.Folders.Select(row => row.Path).ToArray();
        list.SelectedIndex = 1;
        Dispatcher.UIThread.RunJobs();
        SettingsUiHelpers.Click(window, SettingsUiHelpers.Find<Button>(window, "StyleFolderRemoveButton"));

        // Assert
        Assert.True(errorShown.IsVisible);
        Assert.Contains("'relative'", errorShown.Text, StringComparison.Ordinal);
        Assert.Equal([two, one], afterUp);
        Assert.Equal([two], page.Folders.Select(row => row.Path));

        window.Close();
    }

    /// <summary>
    ///     Test that in the settings dialog, folders and the default kit that are chosen on the page and saved with OK are
    ///     written to the settings file, and the page is listed with the other pages.
    /// </summary>
    [AvaloniaFact]
    public async Task SettingsDialogView_StyleKitsPage_OkButtonSavesFoldersAndDefaultKit()
    {
        // Arrange: a settings dialog over the AI Provider page and the Style Kits page, with the kits read
        await _catalog.RefreshAsync(TestContext.Current.CancellationToken);
        var folder = Path.Combine(_root, "team-kits");
        var page = new StyleSettingsPageViewModel(_catalog, () => null);
        var viewModel = new SettingsDialogViewModel(_settings, [new AiProviderSettingsPageViewModel(), page]);
        var window = new SettingsDialogView(viewModel);
        window.Show();
        var list = SettingsUiHelpers.Find<ListBox>(window, "SettingsPageList");
        var titles = list.Items.OfType<ISettingsPage>().Select(item => item.Title).ToArray();

        // Act: show the page, add a folder, choose the DEMA kit as the default, and click OK
        list.SelectedIndex = 1;
        Dispatcher.UIThread.RunJobs();
        Add(window, folder);
        var combo = SettingsUiHelpers.Find<ComboBox>(window, "StyleDefaultKitComboBox");
        combo.SelectedIndex = combo.Items.OfType<StyleKitChoice>().ToList().FindIndex(choice => choice.Id == "dema");
        SettingsUiHelpers.Click(window, SettingsUiHelpers.Find<Button>(window, "SettingsOkButton"));

        // Assert: the page was listed, and what was chosen is in the settings file
        var stored = new SettingsService(Path.Combine(_root, "settings")).Load().Styles;
        Assert.Equal(["AI Provider", "Style Kits"], titles);
        Assert.Equal([folder], stored.SharedKitFolders);
        Assert.Equal("dema", stored.DefaultKitId);
    }

    /// <summary>
    ///     Types a path into the text box of the page and clicks Add.
    /// </summary>
    /// <param name="window">The window that shows the page.</param>
    /// <param name="path">The text to type.</param>
    private static void Add(Window window, string path)
    {
        SettingsUiHelpers.Find<TextBox>(window, "StyleNewFolderTextBox").Text = path;
        Dispatcher.UIThread.RunJobs();
        SettingsUiHelpers.Click(window, SettingsUiHelpers.Find<Button>(window, "StyleFolderAddButton"));
        Dispatcher.UIThread.RunJobs();
    }

    /// <summary>
    ///     Loads a page with settings and shows it in a window that is large enough to show every control.
    /// </summary>
    /// <param name="settings">The settings to load.</param>
    /// <returns>The shown window and the page.</returns>
    private (Window Window, StyleSettingsPageViewModel Page) ShowPage(AppSettings settings)
    {
        var page = new StyleSettingsPageViewModel(_catalog, () => null);
        page.Load(settings);
        var window = new Window
        {
            Width = 700,
            Height = 900,
            Content = new StyleSettingsPageView { DataContext = page },
        };
        window.Show();
        Dispatcher.UIThread.RunJobs();
        return (window, page);
    }
}
