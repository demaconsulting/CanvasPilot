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

using System.Reflection;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.VisualTree;
using CommunityToolkit.Mvvm.Input;
using DemaConsulting.CanvasPilot.AppShellSubsystem;
using DemaConsulting.CanvasPilot.ChatSubsystem;
using DemaConsulting.CanvasPilot.ComposerSubsystem;
using DemaConsulting.CanvasPilot.KnowledgeSubsystem;
using DemaConsulting.CanvasPilot.PreviewSubsystem;
using DemaConsulting.CanvasPilot.ProjectSubsystem;
using DemaConsulting.CanvasPilot.SettingsSubsystem;
using DemaConsulting.CanvasPilot.StyleSubsystem;

namespace DemaConsulting.CanvasPilot.UiTests;

/// <summary>
///     A push-to-talk target for the window tests that counts how often it was started and stopped.
/// </summary>
internal sealed class RecordingTalkTarget : IPushToTalkTarget
{
    /// <summary>
    ///     Gets how many times listening was started.
    /// </summary>
    public int Begins { get; private set; }

    /// <summary>
    ///     Gets how many times listening was stopped.
    /// </summary>
    public int Ends { get; private set; }

    /// <inheritdoc />
    public Task BeginPushToTalkAsync()
    {
        Begins++;
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task EndPushToTalkAsync()
    {
        Ends++;
        return Task.CompletedTask;
    }
}
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
    ///     Test that the window has the default and the smallest size of the design and shows a 44 px title bar that holds
    ///     the menu, and that the room kept for the buttons of the system is none while the window is not extended into its
    ///     frame (as in the headless platform).
    /// </summary>
    [AvaloniaFact]
    public void MainWindowView_Show_HasTitleBarWithMenuAndTheDesignSizes()
    {
        // Arrange: a shown main window
        var (_, window) = CreateWindow();

        // Act: show it and find the title bar
        window.Show();
        AppShellUiHelpers.Flush();
        var bar = SettingsUiHelpers.Find<Border>(window, "AppTitleBar");
        var menu = SettingsUiHelpers.Find<Menu>(window, "MainMenu");

        // Assert: the sizes of the design, a title bar 44 high over the width of the window, and the menu inside it
        Assert.Equal(1440, window.Width);
        Assert.Equal(900, window.Height);
        Assert.Equal(960, window.MinWidth);
        Assert.Equal(600, window.MinHeight);
        Assert.Equal(44, bar.Bounds.Height);
        Assert.Equal(window.Bounds.Width, bar.Bounds.Width);
        Assert.Contains(bar, menu.GetVisualAncestors());
        Assert.Equal(0, window.FindControl<Border>("TitleBarCaptionReserve")!.Bounds.Width);
        Assert.Equal(0, window.FindControl<Border>("TitleBarLeadingReserve")!.Bounds.Width);

        window.Close();
    }

    /// <summary>
    ///     Test that the room kept free in the title bar for the buttons of the system is on the right for Windows, on the
    ///     left for macOS, and none elsewhere or when the window is not extended into its frame.
    /// </summary>
    /// <param name="isExtended">Whether the window extends into its frame.</param>
    /// <param name="isWindows">Whether the platform is Windows.</param>
    /// <param name="isMacOs">Whether the platform is macOS.</param>
    /// <param name="left">The expected room on the left.</param>
    /// <param name="right">The expected room on the right.</param>
    [Theory]
    [InlineData(false, true, false, 0, 0)]
    [InlineData(false, false, true, 0, 0)]
    [InlineData(true, true, false, 0, 138)]
    [InlineData(true, false, true, 78, 0)]
    [InlineData(true, false, false, 0, 0)]
    public void MainWindowView_CaptionReserve_FollowsPlatformAndExtension(bool isExtended, bool isWindows, bool isMacOs, double left, double right)
    {
        // Act: ask for the room that is kept free
        var reserve = MainWindowView.CaptionReserve(isExtended, isWindows, isMacOs);

        // Assert: it is where the platform draws its buttons
        Assert.Equal((left, right), reserve);
    }

    /// <summary>
    ///     Test that setting the title bar reserves tolerates controls that do not exist yet (the state while the XAML
    ///     loads on a real window) and sets the widths where the controls exist.
    /// </summary>
    [Fact]
    public void MainWindowView_ApplyCaptionReserve_ToleratesMissingControls()
    {
        // Arrange: reserves of no width and a missing control
        var leading = new Border();
        var trailing = new Border();

        // Act: apply with a missing control, then with Windows and macOS
        MainWindowView.ApplyCaptionReserve(null, null, true, true, false);
        MainWindowView.ApplyCaptionReserve(leading, trailing, true, true, false);
        var windowsWidths = (leading.Width, trailing.Width);
        MainWindowView.ApplyCaptionReserve(leading, trailing, true, false, true);
        var macWidths = (leading.Width, trailing.Width);
        MainWindowView.ApplyCaptionReserve(leading, null, false, true, false);

        // Assert: nothing threw, the widths follow the platform, and a window that is not extended keeps none
        Assert.Equal((0d, 138d), windowsWidths);
        Assert.Equal((78d, 0d), macWidths);
        Assert.Equal(0, leading.Width);
    }

    /// <summary>
    ///     Test that the window ignores the extension into its frame while the XAML loads (when its title bar controls and
    ///     name scope do not exist, which crashed the published program at the start), and keeps the room free for the
    ///     buttons of the system once the extension is raised on the loaded window.
    /// </summary>
    [AvaloniaFact]
    public void MainWindowView_ExtendedIntoFrame_IgnoredWhileLoadingAndAppliedAfterwards()
    {
        // Arrange: a shown window, and the field that tells it the XAML is loaded
        var (_, window) = CreateWindow();
        window.Show();
        AppShellUiHelpers.Flush();
        var ready = typeof(MainWindowView).GetField("_componentReady", BindingFlags.Instance | BindingFlags.NonPublic)!;
        var raise = typeof(Window).GetMethod("ExtendClientAreaToDecorationsChanged", BindingFlags.Instance | BindingFlags.NonPublic)!;
        var trailing = window.FindControl<Border>("TitleBarCaptionReserve")!;
        var expected = MainWindowView.CaptionReserve(true, OperatingSystem.IsWindows(), OperatingSystem.IsMacOS());

        // Act: the platform raises the extension while the window is still loading, then on the loaded window
        ready.SetValue(window, false);
        raise.Invoke(window, [true]);
        var whileLoading = trailing.Width;
        ready.SetValue(window, true);
        raise.Invoke(window, [false]);
        raise.Invoke(window, [true]);
        AppShellUiHelpers.Flush();

        // Assert: the first is ignored without an error, and the second keeps the room the platform needs
        Assert.True(window.IsExtendedIntoWindowDecorations);
        Assert.Equal(0, whileLoading);
        Assert.Equal(expected.Right, trailing.Width);
        Assert.Equal(expected.Left, window.FindControl<Border>("TitleBarLeadingReserve")!.Width);

        window.Close();
    }

    /// <summary>
    ///     Test that the title bar shows the name of the open project in bold with the application name after it, the
    ///     application name alone while no project is open, and that the window title of the system is unchanged.
    /// </summary>
    [AvaloniaFact]
    public async Task MainWindowView_TitleBar_ShowsProjectNameThenApplicationName()
    {
        // Arrange: a remembered project and a shown main window
        await CreateRememberedProjectAsync("Release Overview");
        var (composition, window) = CreateWindow();
        window.Show();
        AppShellUiHelpers.Flush();
        var text = SettingsUiHelpers.Find<TextBlock>(window, "AppTitleBarText");

        // Act: read the title bar with no project, open the project, read it again, and close the project
        string Read() => string.Concat(text.Inlines!.OfType<Avalonia.Controls.Documents.Run>().Select(run => run.Text));
        var closedText = Read();
        await composition.Shell.RecentProjects[0].OpenCommand.ExecuteAsync(null);
        AppShellUiHelpers.Flush();
        var openText = Read();
        var openTitle = window.Title;
        window.FindControl<MenuItem>("CloseProjectMenuItem")!.Command!.Execute(null);
        AppShellUiHelpers.Flush();

        // Assert: the name is bold with the application name after it while the project is open; the system title is unchanged
        Assert.Equal("CanvasPilot", closedText);
        Assert.Equal("Release Overview \u00B7 CanvasPilot", openText);
        Assert.Equal("Release Overview - CanvasPilot", openTitle);
        Assert.Equal("CanvasPilot", Read());

        window.Close();
    }

    /// <summary>
    ///     Test that the status bar is always at the bottom of the window with its fixed height, says that no project is open,
    ///     and, once a project is open and a picture is selected, reports the file, its size, its format, and its render time
    ///     with an icon and with words, and a render that failed with words.
    /// </summary>
    [AvaloniaFact]
    public async Task MainWindowView_StatusBar_AlwaysShownAndReportsThePicture()
    {
        // Arrange: a shown main window and a project with a picture and a broken file
        await CreateRememberedProjectAsync("Gallery");
        var (composition, window) = CreateWindow();
        window.Show();
        AppShellUiHelpers.Flush();
        var bar = SettingsUiHelpers.Find<Border>(window, "StatusBar");
        var status = SettingsUiHelpers.Find<Panel>(window, "PreviewStatus");
        var withoutProject = (bar.IsVisible, bar.Bounds.Height, AutomationProperties.GetName(status), SettingsUiHelpers.Find<TextBlock>(window, "StatusBarIdle").IsVisible);

        // Act: open the project, select the picture, and wait for it to be drawn
        await composition.Shell.RecentProjects[0].OpenCommand.ExecuteAsync(null);
        var folder = composition.Shell.CurrentProject!.FolderPath;
        await File.WriteAllTextAsync(
            Path.Combine(folder, "red.svg"),
            "<svg xmlns='http://www.w3.org/2000/svg' width='40' height='20'><rect width='40' height='20' fill='#ff0000'/></svg>",
            TestContext.Current.CancellationToken);
        await File.WriteAllTextAsync(Path.Combine(folder, "broken.svg"), "<svg><not closed", TestContext.Current.CancellationToken);
        composition.Shell.Workspace.Explorer.Refresh();
        AppShellUiHelpers.Flush();
        var noPicture = AutomationProperties.GetName(status);
        composition.Shell.Workspace.Explorer.SelectFile(Path.Combine(folder, "red.svg"));
        await composition.Preview.RenderCompletion;
        AppShellUiHelpers.Flush();
        var shown = (
            SettingsUiHelpers.Find<StackPanel>(window, "StatusBarReady").IsVisible,
            SettingsUiHelpers.Find<TextBlock>(window, "StatusBarFile").Text,
            SettingsUiHelpers.Find<TextBlock>(window, "StatusBarSize").Text,
            SettingsUiHelpers.Find<TextBlock>(window, "StatusBarFormat").Text,
            SettingsUiHelpers.Find<TextBlock>(window, "StatusBarRenderTime").Text,
            AutomationProperties.GetName(status));
        var saved = SettingsUiHelpers.Find<TextBlock>(window, "StatusBarProjectSaved");
        composition.Shell.Workspace.Explorer.SelectFile(Path.Combine(folder, "broken.svg"));
        await composition.Preview.RenderCompletion;
        AppShellUiHelpers.Flush();
        var failed = (
            SettingsUiHelpers.Find<StackPanel>(window, "StatusBarFailed").IsVisible,
            SettingsUiHelpers.Find<StackPanel>(window, "StatusBarReady").IsVisible,
            AutomationProperties.GetName(status));

        // Assert: always shown at 26 high; idle words with no project and with no picture; the picture's facts; the failure
        Assert.Equal((true, 26d, "No project open", true), withoutProject);
        Assert.Equal("No picture selected", noPicture);
        Assert.True(shown.Item1);
        Assert.Equal("red.svg", shown.Item2);
        Assert.Equal("40 \u00D7 20 px", shown.Item3);
        Assert.Equal("SVG", shown.Item4);
        Assert.StartsWith("Rendered in ", shown.Item5, StringComparison.Ordinal);
        Assert.Equal("red.svg: 40 x 20 px, svg", shown.Item6);
        Assert.True(saved.IsEffectivelyVisible);
        Assert.Equal((true, false, "broken.svg: render failed"), failed);

        window.Close();
    }

    /// <summary>
    ///     Test that the AI provider pill is in the title bar, names the provider and says in words how it is, and that using
    ///     it opens the settings on the AI provider page.
    /// </summary>
    [AvaloniaFact]
    public async Task MainWindowView_ProviderPill_InTheTitleBar_OpensTheSettingsOnTheProviderPage()
    {
        // Arrange: a shown main window
        var (_, window) = CreateWindow();
        window.Show();
        AppShellUiHelpers.Flush();
        var bar = SettingsUiHelpers.Find<Border>(window, "AppTitleBar");
        var pill = SettingsUiHelpers.Find<Button>(window, "ProviderPill");
        var name = AutomationProperties.GetName(pill);
        var barRight = bar.TranslatePoint(new Point(bar.Bounds.Width, 0), window)!.Value.X;
        var pillRight = pill.TranslatePoint(new Point(pill.Bounds.Width, 0), window)!.Value.X;

        // Act: use the pill with the pointer, and read the page that the settings show
        SettingsUiHelpers.Click(window, pill);
        var dialog = AppShellUiHelpers.FindDialog<SettingsDialogView>(window);
        var page = Assert.IsAssignableFrom<ISettingsPage>(SettingsUiHelpers.Find<ListBox>(dialog, "SettingsPageList").SelectedItem);
        SettingsUiHelpers.Click(dialog, SettingsUiHelpers.Find<Button>(dialog, "SettingsCancelButton"));
        await Task.Yield();

        // Assert: the pill is in the title bar inside its right edge, says what it is, and opened the AI provider page
        Assert.Contains(bar, pill.GetVisualAncestors());
        Assert.Equal(26, pill.Bounds.Height);
        Assert.InRange(pillRight, 0, barRight);
        Assert.StartsWith("AI provider: Copilot, ", name, StringComparison.Ordinal);
        Assert.Equal("AI Provider", page.Title);

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

        // Assert: File, Edit, Image, Tools, and Help hold the expected items in order
        Assert.Equal(["FileMenu", "EditMenu", "ImageMenu", "ToolsMenu", "HelpMenu"], structure.Select(top => top.Id));
        Assert.Equal(
            ["NewProjectMenuItem", "OpenProjectMenuItem", "RecentProjectsMenuItem", "SaveProjectMenuItem", "CloseProjectMenuItem", "ExitMenuItem"],
            structure[0].Children);
        Assert.Equal(["UndoMenuItem", "RedoMenuItem"], structure[1].Children);
        Assert.Equal(["ExportPngMenuItem", "CopyImageMenuItem", "CheckAgainstStyleMenuItem"], structure[2].Children);
        Assert.Equal(["StyleKitsMenuItem", "SettingsMenuItem"], structure[3].Children);
        Assert.Equal(["DrawingKnowledgeMenuItem", "AboutMenuItem"], structure[4].Children);
        foreach (var item in structure.SelectMany(top => top.Children.Prepend(top.Id)))
        {
            var found = window.FindControl<MenuItem>(item);
            Assert.NotNull(found);
            Assert.Equal(item, IdOf(found));
        }

        Assert.Equal(
            ["_File", "_Edit", "_Image", "_Tools", "_Help"],
            menu.Items.OfType<MenuItem>().Select(top => top.Header as string));
        Assert.Equal(OperatingSystem.IsMacOS() ? new KeyGesture(Key.Z, KeyModifiers.Meta) : new KeyGesture(Key.Z, KeyModifiers.Control), window.FindControl<MenuItem>("UndoMenuItem")!.InputGesture);
        Assert.Equal(OperatingSystem.IsMacOS() ? new KeyGesture(Key.Z, KeyModifiers.Meta | KeyModifiers.Shift) : new KeyGesture(Key.Y, KeyModifiers.Control), window.FindControl<MenuItem>("RedoMenuItem")!.InputGesture);
        Assert.Equal("_Drawing Knowledge...", window.FindControl<MenuItem>("DrawingKnowledgeMenuItem")!.Header);
        Assert.Equal("Style _Kits...", window.FindControl<MenuItem>("StyleKitsMenuItem")!.Header);
        Assert.Single(window.FindControl<MenuItem>("HelpMenu")!.Items.OfType<Separator>());

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
        foreach (var enabled in new[] { "NewProjectMenuItem", "OpenProjectMenuItem", "ExitMenuItem", "StyleKitsMenuItem", "SettingsMenuItem", "DrawingKnowledgeMenuItem", "AboutMenuItem" })
        {
            Assert.True(IsUsable(window.FindControl<MenuItem>(enabled)!), enabled);
        }

        foreach (var disabled in new[] { "RecentProjectsMenuItem", "SaveProjectMenuItem", "CloseProjectMenuItem", "UndoMenuItem", "RedoMenuItem", "ExportPngMenuItem", "CopyImageMenuItem", "CheckAgainstStyleMenuItem" })
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
    ///     Test that the Drawing Knowledge menu item shows the browser as a window that the main window owns and that does
    ///     not block it, with the roots of the catalog of the application in its tree.
    /// </summary>
    [AvaloniaFact]
    public async Task MainWindowView_DrawingKnowledgeMenuItem_ShowsBrowserWindowThatDoesNotBlock()
    {
        // Arrange: a shown main window
        var (_, window) = CreateWindow();
        window.Show();

        // Act: run Drawing Knowledge
        var shown = ((IAsyncRelayCommand)window.FindControl<MenuItem>("DrawingKnowledgeMenuItem")!.Command!).ExecuteAsync(null);
        await shown;
        var browser = AppShellUiHelpers.FindDialog<KnowledgeBrowserView>(window);

        // Assert: the command finished while the window is open, the window is the catalog's tree, and the main window is
        // still usable
        Assert.True(browser.IsVisible);
        Assert.Equal("Drawing Knowledge", browser.Title);
        var tree = SettingsUiHelpers.Find<TreeView>(browser, "KnowledgeCardTree");
        Assert.Equal(KnowledgeCatalog.Default.Roots.Count, tree.ItemCount);
        Assert.True(window.IsEnabled);
        browser.Close();
        window.Close();
    }

    /// <summary>
    ///     Test that asking again while the browser is open brings the same window to the front and does not open
    ///     another, and that asking after it was closed opens a new one.
    /// </summary>
    [AvaloniaFact]
    public async Task MainWindowView_DrawingKnowledgeMenuItem_SecondRequest_ReusesTheOpenBrowser()
    {
        // Arrange: a shown main window with the browser open, in which a card was chosen
        var (_, window) = CreateWindow();
        window.Show();
        var command = (IAsyncRelayCommand)window.FindControl<MenuItem>("DrawingKnowledgeMenuItem")!.Command!;
        await command.ExecuteAsync(null);
        var first = AppShellUiHelpers.FindDialog<KnowledgeBrowserView>(window);
        var firstViewModel = (KnowledgeBrowserViewModel)first.DataContext!;
        firstViewModel.Select("canvaspilot");

        // Act: ask again, and look at what is open
        await command.ExecuteAsync(null);
        var second = AppShellUiHelpers.FindDialog<KnowledgeBrowserView>(window);

        // Assert: there is one browser, the same one, still showing the card that was chosen
        Assert.Same(first, second);
        Assert.Same(firstViewModel, second.DataContext);
        Assert.Equal("canvaspilot", firstViewModel.SelectedCard!.Id);

        // Act: close it and ask once more
        first.Close();
        await command.ExecuteAsync(null);
        var third = AppShellUiHelpers.FindDialog<KnowledgeBrowserView>(window);

        // Assert: a new browser was opened, with nothing chosen
        Assert.NotSame(first, third);
        Assert.Null(((KnowledgeBrowserViewModel)third.DataContext!).SelectedCard);
        third.Close();
        window.Close();
    }

    /// <summary>
    ///     Test that the Style Kits menu item shows the window that manages kits as a window that the main window owns and
    ///     that does not block it, with the kits that come with the program listed in it.
    /// </summary>
    [AvaloniaFact]
    public async Task MainWindowView_StyleKitsMenuItem_ShowsWindowThatDoesNotBlock()
    {
        // Arrange: a shown main window whose kits were read
        var (composition, window) = CreateWindow();
        window.Show();
        await composition.StyleCatalog.RefreshAsync(TestContext.Current.CancellationToken);

        // Act: run Style Kits
        await ((IAsyncRelayCommand)window.FindControl<MenuItem>("StyleKitsMenuItem")!.Command!).ExecuteAsync(null);
        var manager = AppShellUiHelpers.FindDialog<StyleKitManagerView>(window);
        var cards = manager.GetVisualDescendants().OfType<Button>().Where(button => AutomationProperties.GetAutomationId(button) == "StyleKitCard").ToArray();

        // Assert: the command finished while the window is open, and it lists the kits of the program
        Assert.True(manager.IsVisible);
        Assert.Equal("Style Kits", manager.Title);
        Assert.Equal(3, cards.Length);
        Assert.True(window.IsEnabled);
        manager.Close();
        window.Close();
    }

    /// <summary>
    ///     Test that asking again while the window that manages kits is open brings the same window to the front and does not
    ///     open another, and that asking after it was closed opens a new one.
    /// </summary>
    [AvaloniaFact]
    public async Task MainWindowView_StyleKitsMenuItem_SecondRequest_ReusesTheOpenWindow()
    {
        // Arrange: a shown main window with the window that manages kits open and a kit chosen in it
        var (composition, window) = CreateWindow();
        window.Show();
        await composition.StyleCatalog.RefreshAsync(TestContext.Current.CancellationToken);
        var command = (IAsyncRelayCommand)window.FindControl<MenuItem>("StyleKitsMenuItem")!.Command!;
        await command.ExecuteAsync(null);
        var first = AppShellUiHelpers.FindDialog<StyleKitManagerView>(window);
        var firstViewModel = (StyleKitManagerViewModel)first.DataContext!;
        firstViewModel.SelectItemCommand.Execute(firstViewModel.Groups[^1].Items[0]);

        // Act: ask again, and look at what is open
        await command.ExecuteAsync(null);
        var second = AppShellUiHelpers.FindDialog<StyleKitManagerView>(window);

        // Assert: there is one window, the same one, still showing the kit that was chosen
        Assert.Same(first, second);
        Assert.Same(firstViewModel, second.DataContext);
        Assert.True(firstViewModel.HasSelection);

        // Act: close it and ask once more
        first.Close();
        await command.ExecuteAsync(null);
        var third = AppShellUiHelpers.FindDialog<StyleKitManagerView>(window);

        // Assert: a new window was opened, with nothing chosen
        Assert.NotSame(first, third);
        Assert.False(((StyleKitManagerViewModel)third.DataContext!).HasSelection);
        third.Close();
        window.Close();
    }

    /// <summary>
    ///     Test that the main window shows the editor of a kit as a modal window over the window that manages kits and over
    ///     itself when that is not open, and that the editor's Cancel button completes the request.
    /// </summary>
    [AvaloniaFact]
    public async Task MainWindowView_ShowStyleKitEditor_OverTheManagerOrTheMainWindow_ShowsEditorModally()
    {
        // Arrange: a main window and a kit of the user to edit
        var (composition, window) = CreateWindow();
        window.Show();
        var entry = composition.StyleCatalog.CreateBlank("Poster", StyleKitLocation.User);

        // Act: ask for the editor with no manager open, and cancel it
        var shown = window.ShowStyleKitEditorAsync(new StyleKitEditorViewModel(composition.StyleCatalog, entry));
        var editor = AppShellUiHelpers.FindDialog<StyleKitEditorView>(window);
        var overMain = editor.IsVisible;
        SettingsUiHelpers.Click(editor, SettingsUiHelpers.Find<Button>(editor, "StyleEditorCancelButton"));
        await shown;

        // Act: open the manager and ask again; the editor is owned by the manager
        await ((IAsyncRelayCommand)window.FindControl<MenuItem>("StyleKitsMenuItem")!.Command!).ExecuteAsync(null);
        var manager = AppShellUiHelpers.FindDialog<StyleKitManagerView>(window);
        var second = window.ShowStyleKitEditorAsync(new StyleKitEditorViewModel(composition.StyleCatalog, entry));
        var overManager = AppShellUiHelpers.FindDialog<StyleKitEditorView>(manager);
        SettingsUiHelpers.Click(overManager, SettingsUiHelpers.Find<Button>(overManager, "StyleEditorCancelButton"));
        await second;

        // Assert
        Assert.True(overMain);
        Assert.Empty(manager.OwnedWindows);
        manager.Close();
        window.Close();
    }

    /// <summary>
    ///     Test that closing the main window closes the window that manages kits and the browser that it owns.
    /// </summary>
    [AvaloniaFact]
    public async Task MainWindowView_Close_ClosesTheStyleKitsWindowItOwns()
    {
        // Arrange: a main window with the window that manages kits open
        var (_, window) = CreateWindow();
        window.Show();
        await ((IAsyncRelayCommand)window.FindControl<MenuItem>("StyleKitsMenuItem")!.Command!).ExecuteAsync(null);
        var manager = AppShellUiHelpers.FindDialog<StyleKitManagerView>(window);

        // Act: close the main window
        window.Close();

        // Assert: the window that manages kits is closed with it
        Assert.False(manager.IsVisible);
    }

    /// <summary>
    ///     Test that closing the main window closes the browser that it owns.
    /// </summary>
    [AvaloniaFact]
    public async Task MainWindowView_Close_ClosesTheBrowserItOwns()
    {
        // Arrange: a main window with the browser open
        var (_, window) = CreateWindow();
        window.Show();
        await ((IAsyncRelayCommand)window.FindControl<MenuItem>("DrawingKnowledgeMenuItem")!.Command!).ExecuteAsync(null);
        var browser = AppShellUiHelpers.FindDialog<KnowledgeBrowserView>(window);

        // Act: close the main window
        window.Close();

        // Assert: the browser is closed with it
        Assert.False(browser.IsVisible);
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

        // Assert: the dialog showed the identity and the six libraries, and Close ended the command
        Assert.Equal("CanvasPilot", name);
        Assert.Equal("AI-assisted drawing creation with CanvasNet", tagline);
        Assert.StartsWith("Version ", version, StringComparison.Ordinal);
        Assert.Equal("Copyright (c) DEMA Consulting", copyright);
        Assert.Equal(6, dependencies);
        Assert.Empty(window.OwnedWindows);

        window.Close();
    }

    /// <summary>
    ///     Test that the Settings menu item opens the settings dialog with the AI Provider, Speech, Style Kits, and Appearance pages, and
    ///     that Cancel closes it.
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

        // Assert: the dialog listed all four pages, and Cancel closed it
        Assert.Equal(["AI Provider", "Speech", "Style Kits", "Appearance"], titles);
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
        var list = SettingsUiHelpers.Find<TreeView>(window, "ProjectExplorerList");
        var listed = list.Items.OfType<ProjectNodeViewModel>().Select(file => file.Name).ToArray();
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
        Assert.True(SettingsUiHelpers.Find<ContentControl>(window, "WelcomeHost").IsEffectivelyVisible);
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
        SelectFirstExplorerNode(window);
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
        SelectFirstExplorerNode(window);
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
    ///     Test that Check Against Style in the Image menu is usable only while a picture is shown, and that running it shows
    ///     the result in the open list of notes of the preview.
    /// </summary>
    [AvaloniaFact]
    public async Task MainWindowView_CheckAgainstStyleMenuItem_PictureShown_EnablesAndShowsNotesInThePreview()
    {
        // Arrange: a window showing a project with a blue picture, whose style is the DEMA kit
        var token = TestContext.Current.CancellationToken;
        await CreateRememberedProjectAsync("Checked", "a.svg");
        var (composition, window) = CreateWindow();
        window.Show();
        await composition.StyleCatalog.RefreshAsync(token);
        await composition.Shell.RecentProjects[0].OpenCommand.ExecuteAsync(null);
        await composition.ActiveStyle.SetActiveAsync("dema");
        var folder = composition.Shell.CurrentProject!.FolderPath;
        await File.WriteAllTextAsync(
            Path.Combine(folder, "a.svg"),
            "<svg xmlns='http://www.w3.org/2000/svg' width='30' height='12'><rect width='30' height='12' fill='#0000ff'/></svg>",
            token);
        composition.Shell.Workspace.Explorer.Refresh();
        var item = window.FindControl<MenuItem>("CheckAgainstStyleMenuItem")!;
        var disabledBefore = !IsUsable(item);

        // Act: show the picture, and run the item
        SelectFirstExplorerNode(window);
        await composition.Preview.RenderCompletion;
        AppShellUiHelpers.Flush();
        var enabledWithPicture = IsUsable(item);
        await Assert.IsAssignableFrom<IAsyncRelayCommand>(item.Command).ExecuteAsync(null);
        AppShellUiHelpers.Flush();
        window.UpdateLayout();

        // Assert: the notes are listed and the list is open
        var expander = SettingsUiHelpers.Find<Expander>(window, "PreviewDiagnosticsExpander");
        var notes = SettingsUiHelpers.Find<ItemsControl>(window, "PreviewDiagnostics").Items.OfType<RenderDiagnostic>().ToArray();
        Assert.True(disabledBefore);
        Assert.True(enabledWithPicture);
        Assert.True(expander.IsVisible);
        Assert.True(expander.IsExpanded);
        Assert.Contains(notes, note => note.Code == "style-summary" && note.Message.Contains("FAILED", StringComparison.Ordinal));
        Assert.Contains(notes, note => note.Code == "style-off-palette");

        composition.Shell.CloseProjectCommand.Execute(null);
        AppShellUiHelpers.Flush();
        Assert.False(IsUsable(item));

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
        SelectFirstExplorerNode(window);
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
        Assert.Equal(1, SettingsUiHelpers.Find<TreeView>(window, "ProjectExplorerList").ItemCount);

        window.Close();
    }

    /// <summary>
    ///     Test that the chat column is hidden while no project is open and shown, to the right of the preview, with the
    ///     chat panel in it once a project is open, and that the workspace has three columns with a splitter on each side
    ///     of the preview.
    /// </summary>
    [AvaloniaFact]
    public async Task MainWindowView_Workspace_ChatColumnVisibleOnlyWithProject_ThreeColumns()
    {
        // Arrange: a shown window with no project open
        var projectFile = await CreateRememberedProjectAsync("Layout", "a.svg");
        var (composition, window) = CreateWindow();
        window.Show();
        AppShellUiHelpers.Flush();
        // The workspace is not even built while the welcome screen takes its place
        var chatHostBefore = window.GetVisualDescendants().OfType<ContentControl>()
            .FirstOrDefault(control => Avalonia.Automation.AutomationProperties.GetAutomationId(control) == "WorkspaceChatHost");
        var chatSplitterBefore = window.GetVisualDescendants().OfType<GridSplitter>()
            .FirstOrDefault(control => Avalonia.Automation.AutomationProperties.GetAutomationId(control) == "WorkspaceChatSplitter");
        var chatVisibleBefore = chatHostBefore?.IsEffectivelyVisible ?? false;
        var chatWidthBefore = chatHostBefore?.Bounds.Width ?? 0;
        var splitterVisibleBefore = chatSplitterBefore?.IsEffectivelyVisible ?? false;

        // Act: open the project, then close it
        await composition.Shell.RecentProjects[0].OpenCommand.ExecuteAsync(null);
        LayOut(window);
        var chatHost = SettingsUiHelpers.Find<ContentControl>(window, "WorkspaceChatHost");
        var explorerHost = SettingsUiHelpers.Find<ContentControl>(window, "WorkspaceExplorerHost");
        var previewHost = SettingsUiHelpers.Find<ContentControl>(window, "WorkspacePreviewHost");
        var chatVisibleWith = chatHost.IsVisible;
        var chatWidthWith = chatHost.Bounds.Width;
        var explorerWidth = explorerHost.Bounds.Width;
        var previewWidth = previewHost.Bounds.Width;
        var explorerRight = LeftOf(explorerHost, window) + explorerHost.Bounds.Width;
        var previewLeft = LeftOf(previewHost, window);
        var previewRight = previewLeft + previewHost.Bounds.Width;
        var chatLeft = LeftOf(chatHost, window);
        var panels = window.GetVisualDescendants().OfType<ChatPanelView>().Count();
        composition.Shell.CloseProjectCommand.Execute(null);
        LayOut(window);
        var chatVisibleAfter = chatHost.IsVisible;

        // Assert: the chat column is 400 wide on the right of the preview only while a project is open
        Assert.False(chatVisibleBefore);
        Assert.Equal(0, chatWidthBefore);
        Assert.False(splitterVisibleBefore);
        Assert.True(chatVisibleWith);
        Assert.Equal(1, panels);
        Assert.Equal(400, chatWidthWith, 1);
        Assert.Equal(264, explorerWidth, 1);
        Assert.True(previewWidth >= 360);
        Assert.True(previewLeft > explorerRight);
        Assert.True(chatLeft > previewRight);
        Assert.False(chatVisibleAfter);
        Assert.True(File.Exists(projectFile));

        window.Close();
    }

    /// <summary>
    ///     Test that Undo is enabled after typing in the message, runs on the message, and is disabled again when the
    ///     explorer has the focus.
    /// </summary>
    [AvaloniaFact]
    public async Task MainWindowView_EditMenu_TypingInComposerEnablesUndoAndExplorerFocusDisables()
    {
        // Arrange: a window showing a project, with the message editor and the explorer list in it
        await CreateRememberedProjectAsync("Editing", "a.svg");
        var (composition, window) = CreateWindow();
        window.Show();
        await composition.Shell.RecentProjects[0].OpenCommand.ExecuteAsync(null);
        LayOut(window);
        var undo = window.FindControl<MenuItem>("UndoMenuItem")!;
        var redo = window.FindControl<MenuItem>("RedoMenuItem")!;
        var editor = window.GetVisualDescendants().OfType<ComposerEditorView>().Single();
        var beforeTyping = IsUsable(undo);

        // Act: type in the message, undo it from the menu, redo it, and move the focus to the explorer
        editor.FocusEditor();
        window.KeyTextInput("hello");
        LayOut(window);
        var afterTyping = (Undo: IsUsable(undo), Redo: IsUsable(redo), Target: composition.Shell.EditTarget);
        undo.Command!.Execute(null);
        LayOut(window);
        var afterUndo = (Text: composition.Composer.Document.FlatText, Undo: IsUsable(undo), Redo: IsUsable(redo));
        redo.Command!.Execute(null);
        LayOut(window);
        var afterRedo = composition.Composer.Document.FlatText;
        var list = SettingsUiHelpers.Find<TreeView>(window, "ProjectExplorerList");
        var focused = list.ContainerFromIndex(0)!.Focus();
        LayOut(window);
        var afterExplorerFocus = (Target: composition.Shell.EditTarget, Undo: IsUsable(undo), Redo: IsUsable(redo));
        Assert.True(focused);

        // Assert: typing made Undo usable, the menu acted on the message, and the explorer has nothing to undo
        Assert.False(beforeTyping);
        Assert.True(afterTyping.Undo);
        Assert.False(afterTyping.Redo);
        Assert.Same(composition.Composer, afterTyping.Target);
        Assert.NotEqual("hello", afterUndo.Text);
        Assert.True(afterUndo.Redo);
        Assert.Equal("hello", afterRedo);
        Assert.Null(afterExplorerFocus.Target);
        Assert.False(afterExplorerFocus.Undo);
        Assert.False(afterExplorerFocus.Redo);

        window.Close();
    }

    /// <summary>
    ///     Test that holding F8 starts the push-to-talk target once, even when the key repeats, that releasing it stops the
    ///     target, and that other keys do nothing.
    /// </summary>
    [AvaloniaFact]
    public void MainWindowView_PushToTalkKey_DownAndUp_DrivesTarget()
    {
        // Arrange: a shown window whose shell has a recording push-to-talk target
        var (composition, window) = CreateWindow();
        var target = new RecordingTalkTarget();
        composition.Shell.PushToTalk = target;
        window.Show();

        // Act: press F8 and let it repeat, press another key, release F8, and release it again
        window.KeyPressQwerty(PhysicalKey.F8, RawInputModifiers.None);
        window.KeyPressQwerty(PhysicalKey.F8, RawInputModifiers.None);
        var heldBegins = target.Begins;
        window.KeyPressQwerty(PhysicalKey.F9, RawInputModifiers.None);
        window.KeyReleaseQwerty(PhysicalKey.F9, RawInputModifiers.None);
        window.KeyReleaseQwerty(PhysicalKey.F8, RawInputModifiers.None);
        window.KeyReleaseQwerty(PhysicalKey.F8, RawInputModifiers.None);

        // Assert: one start while held, one stop at the release, and nothing for the other key
        Assert.Equal(1, heldBegins);
        Assert.Equal(1, target.Begins);
        Assert.Equal(1, target.Ends);
        Assert.False(composition.Shell.IsPushToTalkActive);

        window.Close();
    }

    /// <summary>
    ///     Test that F8 does nothing while the shell has no push-to-talk target.
    /// </summary>
    [AvaloniaFact]
    public void MainWindowView_PushToTalkKey_NoTarget_IsIgnored()
    {
        // Arrange: a shown window whose shell has no push-to-talk target
        var (composition, window) = CreateWindow();
        composition.Shell.PushToTalk = null;
        window.Show();

        // Act: press and release F8
        window.KeyPressQwerty(PhysicalKey.F8, RawInputModifiers.None);
        var active = composition.Shell.IsPushToTalkActive;
        window.KeyReleaseQwerty(PhysicalKey.F8, RawInputModifiers.None);

        // Assert: nothing started
        Assert.False(active);

        window.Close();
    }

    /// <summary>
    ///     Test that closing the window while the talk key is held stops the listening.
    /// </summary>
    [AvaloniaFact]
    public void MainWindowView_PushToTalkKey_WindowClosedWhileHeld_StopsTarget()
    {
        // Arrange: a shown window with the talk key held
        var (composition, window) = CreateWindow();
        var target = new RecordingTalkTarget();
        composition.Shell.PushToTalk = target;
        window.Show();
        window.KeyPressQwerty(PhysicalKey.F8, RawInputModifiers.None);

        // Act: close the window
        window.Close();

        // Assert: the target was stopped once
        Assert.Equal(1, target.Begins);
        Assert.Equal(1, target.Ends);
    }

    /// <summary>
    ///     Test that the window stops listening when another window becomes the active one while the talk key is held.
    /// </summary>
    [AvaloniaFact]
    public void MainWindowView_PushToTalkKey_WindowDeactivatedWhileHeld_StopsTarget()
    {
        // Arrange: a shown, active window with the talk key held
        var (composition, window) = CreateWindow();
        var target = new RecordingTalkTarget();
        composition.Shell.PushToTalk = target;
        window.Show();
        window.Activate();
        window.KeyPressQwerty(PhysicalKey.F8, RawInputModifiers.None);

        // Act: the platform tells the window that another window became the active one (the headless platform never does)
        var deactivated = (Action?)typeof(Avalonia.Platform.IWindowBaseImpl)
            .GetProperty("Deactivated", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic)!
            .GetValue(window.PlatformImpl);
        deactivated!.Invoke();
        AppShellUiHelpers.Flush();

        // Assert: the target was stopped once when the window was deactivated, and the release that follows changes nothing
        var endsAfterDeactivation = target.Ends;
        window.KeyReleaseQwerty(PhysicalKey.F8, RawInputModifiers.None);
        Assert.Equal(1, target.Begins);
        Assert.Equal(1, endsAfterDeactivation);
        Assert.Equal(1, target.Ends);

        window.Close();
    }

    /// <summary>
    ///     Test that the dialog service of the window is the launcher of the payload inspector of the composed chat panel.
    /// </summary>
    [AvaloniaFact]
    public void MainWindowView_Show_ChatPanelLaunchesInspectorThroughWindow()
    {
        // Arrange & Act: compose the application and create its window
        var (composition, window) = CreateWindow();

        // Assert: the panel asks the window for the inspector
        Assert.Same(window, composition.Chat.InspectorLauncher);

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
    ///     Finds where the left edge of a control is in the window.
    /// </summary>
    /// <param name="control">The control.</param>
    /// <param name="window">The window.</param>
    /// <returns>The distance of the left edge from the left edge of the window.</returns>
    private static double LeftOf(Control control, Window window)
    {
        return control.TranslatePoint(new Point(0, 0), window)?.X ?? double.NaN;
    }

    /// <summary>
    ///     Selects the first node of the explorer tree, as a click on the first row does.
    /// </summary>
    /// <param name="window">The window.</param>
    private static void SelectFirstExplorerNode(Window window)
    {
        var tree = SettingsUiHelpers.Find<TreeView>(window, "ProjectExplorerList");
        tree.SelectedItem = tree.Items[0];
    }

    /// <summary>
    ///     Lays the window out and runs the work that is queued, so that what is shown is current.
    /// </summary>
    /// <param name="window">The window.</param>
    private static void LayOut(Window window)
    {
        window.UpdateLayout();
        AppShellUiHelpers.Flush();
        window.UpdateLayout();
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
