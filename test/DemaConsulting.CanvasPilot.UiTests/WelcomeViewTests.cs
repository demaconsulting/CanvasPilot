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
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.VisualTree;
using DemaConsulting.CanvasPilot.AppShellSubsystem;
using DemaConsulting.CanvasPilot.ProjectSubsystem;
using DemaConsulting.CanvasPilot.SettingsSubsystem;

namespace DemaConsulting.CanvasPilot.UiTests;

/// <summary>
///     Headless UI tests for the welcome screen that fills the main window while no project is open: what it shows, how the
///     recent project cards are used with the pointer and the keyboard, and when it gives way to the workspace.
/// </summary>
public sealed class WelcomeViewTests : IDisposable
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
    ///     Test that a window with no project shows the welcome screen with the two ways to start, the steps, and the empty
    ///     card, and does not show the workspace.
    /// </summary>
    [AvaloniaFact]
    public void WelcomeView_NoProject_ShowsStartButtonsStepsAndEmptyCard()
    {
        // Arrange & Act: a shown window with no project and no recent project
        var window = ShowWindow(out _);

        // Assert: the welcome screen is shown in place of the workspace, with the buttons and the empty card
        Assert.True(SettingsUiHelpers.Find<ContentControl>(window, "WelcomeHost").IsEffectivelyVisible);
        Assert.False(SettingsUiHelpers.Find<ContentControl>(window, "WorkspaceHost").IsEffectivelyVisible);
        Assert.Equal("New project", ButtonText(SettingsUiHelpers.Find<Button>(window, "WelcomeNewProjectButton")));
        Assert.Equal("Open project", ButtonText(SettingsUiHelpers.Find<Button>(window, "WelcomeOpenProjectButton")));
        Assert.True(SettingsUiHelpers.Find<Border>(window, "WelcomeEmptyCard").IsEffectivelyVisible);
        Assert.False(SettingsUiHelpers.Find<ItemsControl>(window, "WelcomeRecentList").IsEffectivelyVisible);
        Assert.True(SettingsUiHelpers.Find<Border>(window, "WelcomeStepDescribe").IsEffectivelyVisible);
        Assert.True(SettingsUiHelpers.Find<Border>(window, "WelcomeStepMark").IsEffectivelyVisible);
        Assert.True(SettingsUiHelpers.Find<Border>(window, "WelcomeStepShare").IsEffectivelyVisible);

        window.Close();
    }

    /// <summary>
    ///     Test that the New project button asks for a new project and the Open project button asks for a project file.
    /// </summary>
    [AvaloniaFact]
    public async Task WelcomeView_StartButtons_OpenTheNewProjectDialogAndTheFilePicker()
    {
        // Arrange: a shown window with no project
        var window = ShowWindow(out var composition);
        var welcome = SettingsUiHelpers.Find<WelcomeView>(window, "WelcomeView");

        // Act: click New project, cancel the dialog, and run the command of Open project
        var started = ((CommunityToolkit.Mvvm.Input.IAsyncRelayCommand)SettingsUiHelpers.Find<Button>(welcome, "WelcomeNewProjectButton").Command!).ExecuteAsync(null);
        var dialog = AppShellUiHelpers.FindDialog<NewProjectDialogView>(window);
        SettingsUiHelpers.Click(dialog, SettingsUiHelpers.Find<Button>(dialog, "NewProjectCancelButton"));
        await started;

        // Assert: the buttons run the commands of the menu, and the cancelled dialog created nothing
        Assert.Same(composition.Shell.NewProjectCommand, SettingsUiHelpers.Find<Button>(welcome, "WelcomeNewProjectButton").Command);
        Assert.Same(composition.Shell.OpenProjectCommand, SettingsUiHelpers.Find<Button>(welcome, "WelcomeOpenProjectButton").Command);
        Assert.False(composition.Shell.HasProject);

        window.Close();
    }

    /// <summary>
    ///     Test that the recent projects are cards in order of opening, each with its name, folder, and the words about when it
    ///     was opened and how many pictures it holds, and that a card opens its project and gives way to the workspace.
    /// </summary>
    [AvaloniaFact]
    public async Task WelcomeView_RecentProjects_ListCardsAndOpenAProject()
    {
        // Arrange: two remembered projects, the second opened last and holding two pictures
        var first = await CreateRememberedProjectAsync("Quarterly Review", "chart.svg");
        var second = await CreateRememberedProjectAsync("Release Overview", "a.svg", "b.svg");
        var window = ShowWindow(out var composition);

        // Act: read the cards, then click the first
        var cards = RecentCards(window);
        var texts = cards[0].GetVisualDescendants().OfType<TextBlock>().Select(text => text.Text).ToList();
        var names = cards.Select(card => Avalonia.Automation.AutomationProperties.GetName(card)).ToList();
        SettingsUiHelpers.Click(window, cards[0]);
        await Task.Delay(100, TestContext.Current.CancellationToken);
        AppShellUiHelpers.Flush();

        // Assert: the newest first with the words, and the click opened the project and showed the workspace
        Assert.Equal(2, cards.Count);
        Assert.Contains("Release Overview", texts);
        Assert.Contains(Path.GetDirectoryName(second), texts);
        Assert.Contains(texts, text => text is not null && text.StartsWith("Opened ", StringComparison.Ordinal) && text.EndsWith("2 pictures", StringComparison.Ordinal));
        Assert.StartsWith("Release Overview, opened", names[0], StringComparison.Ordinal);
        Assert.StartsWith("Quarterly Review, opened", names[1], StringComparison.Ordinal);
        Assert.Equal(first, composition.Shell.RecentProjects[1].FilePath);
        Assert.True(composition.Shell.HasProject);
        Assert.False(SettingsUiHelpers.Find<ContentControl>(window, "WelcomeHost").IsEffectivelyVisible);
        Assert.True(SettingsUiHelpers.Find<ContentControl>(window, "WorkspaceHost").IsEffectivelyVisible);

        window.Close();
    }

    /// <summary>
    ///     Test that the arrow keys move the focus between the cards and that Delete removes the focused card from the list.
    /// </summary>
    [AvaloniaFact]
    public async Task WelcomeView_Keyboard_ArrowsMoveBetweenCardsAndDeleteRemovesFromList()
    {
        // Arrange: three remembered projects and a shown window; the focus is on the first card
        await CreateRememberedProjectAsync("One");
        await CreateRememberedProjectAsync("Two");
        var three = await CreateRememberedProjectAsync("Three");
        var window = ShowWindow(out var composition);
        var cards = RecentCards(window);
        cards[0].Focus();
        AppShellUiHelpers.Flush();

        // Act: move right, then delete the card that has the focus
        window.KeyPressQwerty(PhysicalKey.ArrowRight, RawInputModifiers.None);
        AppShellUiHelpers.Flush();
        var focusedAfterRight = cards.FindIndex(card => card.IsFocused);
        window.KeyPressQwerty(PhysicalKey.Delete, RawInputModifiers.None);
        AppShellUiHelpers.Flush();

        // Assert: the focus moved to the second card, and Delete took that project off the list but not off the disk
        Assert.Equal(1, focusedAfterRight);
        Assert.Equal(["Three", "One"], composition.Shell.RecentProjects.Select(item => item.Name));
        Assert.Equal(2, RecentCards(window).Count);
        Assert.True(File.Exists(three));

        window.Close();
    }

    /// <summary>
    ///     Test that the notice about the AI provider shows with its Open Settings button when the provider cannot be used, and
    ///     that the button opens the settings.
    /// </summary>
    [AvaloniaFact]
    public async Task WelcomeView_ProviderNotUsable_ShowsNoticeWhoseButtonOpensSettings()
    {
        // Arrange: a window over an application with no AI provider, so the check finds the provider unusable
        var window = ShowWindow(out _);
        var banner = SettingsUiHelpers.Find<Border>(window, "WelcomeProviderBanner");
        var button = SettingsUiHelpers.Find<Button>(window, "WelcomeOpenSettingsButton");

        // Act: use the button
        var opened = ((CommunityToolkit.Mvvm.Input.IAsyncRelayCommand)button.Command!).ExecuteAsync(null);
        var dialog = AppShellUiHelpers.FindDialog<SettingsDialogView>(window);
        var title = SettingsUiHelpers.Find<TextBlock>(window, "WelcomeProviderBannerTitle").Text;
        dialog.Close();
        await opened;

        // Assert: the notice was shown with words, and the button opened the settings dialog
        Assert.True(banner.IsEffectivelyVisible);
        Assert.Equal("Copilot is not ready", title);
        Assert.Equal("Open Settings", ButtonText(button));

        window.Close();
    }

    /// <summary>
    ///     Creates the main window, shows it, and lets it lay out.
    /// </summary>
    /// <param name="composition">The application the window shows.</param>
    /// <returns>The shown window.</returns>
    private MainWindowView ShowWindow(out ApplicationComposition composition)
    {
        composition = AppShellUiHelpers.Compose(_root);
        _compositions.Add(composition);
        var window = new MainWindowView(composition.Shell);
        window.Show();
        AppShellUiHelpers.Flush();
        return window;
    }

    /// <summary>
    ///     Finds the recent project cards that are shown.
    /// </summary>
    /// <param name="window">The window.</param>
    /// <returns>The card buttons, in order.</returns>
    private static List<Button> RecentCards(MainWindowView window)
    {
        return window.GetVisualDescendants().OfType<Button>()
            .Where(button => Avalonia.Automation.AutomationProperties.GetAutomationId(button) == "WelcomeRecentCard")
            .ToList();
    }

    /// <summary>
    ///     Reads the words on a button that holds an icon and a text.
    /// </summary>
    /// <param name="button">The button.</param>
    /// <returns>The text on the button.</returns>
    private static string? ButtonText(Button button)
    {
        return button.Content as string ?? button.GetVisualDescendants().OfType<TextBlock>().FirstOrDefault()?.Text;
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

        var settings = new SettingsService(Path.Combine(_root, "settings"));
        settings.Load();
        new RecentProjects(settings).Add(created.Project!.FilePath);
        return created.Project.FilePath;
    }
}
