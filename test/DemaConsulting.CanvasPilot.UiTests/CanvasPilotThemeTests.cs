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

using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using CommunityToolkit.Mvvm.Input;
using DemaConsulting.CanvasPilot.AnnotationSubsystem;
using DemaConsulting.CanvasPilot.AppShellSubsystem;
using DemaConsulting.CanvasPilot.ChatSubsystem;
using DemaConsulting.CanvasPilot.ProjectSubsystem;
using DemaConsulting.CanvasPilot.SettingsSubsystem;

namespace DemaConsulting.CanvasPilot.UiTests;

/// <summary>
///     Headless system tests of the whole application in its main window: the composed window is drawn in the dark
///     and the light theme that the system asks for, the menu of the picture on the stage opens, is named, and
///     stays closed while marking, and a project opened from the welcome screen shows the picture that was last shown in
///     it and the latest message of its conversation.
/// </summary>
public sealed class CanvasPilotThemeTests : IDisposable
{
    /// <summary>
    ///     Temporary root removed on disposal.
    /// </summary>
    private readonly string _root = Directory.CreateTempSubdirectory("canvaspilot-ui-tests-").FullName;

    /// <summary>
    ///     The composition created by a test, disposed with the fixture so no watcher outlives it.
    /// </summary>
    private readonly List<ApplicationComposition> _compositions = [];

    /// <inheritdoc />
    public void Dispose()
    {
        // The application is shared by all tests of the assembly, so the variant that a test chose is taken back
        if (Application.Current is { } application)
        {
            application.RequestedThemeVariant = ThemeVariant.Default;
        }

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
    ///     Test that a project opened from its card on the welcome screen of the composed window selects and draws the
    ///     picture that was last shown in it, and shows its restored conversation scrolled to the latest message.
    /// </summary>
    [AvaloniaFact]
    public async Task CanvasPilot_ResumeProject_ComposedWindow_ShowsTheLastPictureAndTheLatestMessage()
    {
        // Arrange: a remembered project with two pictures, the second shown last, and a long saved conversation; the
        // provider is Ollama without a model, which is not ready without asking a server
        var token = TestContext.Current.CancellationToken;
        var projects = Directory.CreateDirectory(Path.Combine(_root, "projects")).FullName;
        var project = (await new ProjectService().CreateAsync(projects, "Resume", token)).Project!;
        await File.WriteAllTextAsync(Path.Combine(project.FolderPath, "a.svg"), "<svg xmlns='http://www.w3.org/2000/svg' width='20' height='10'/>", token);
        var picture = Path.Combine(project.FolderPath, "b.svg");
        await File.WriteAllTextAsync(picture, "<svg xmlns='http://www.w3.org/2000/svg' width='30' height='10'/>", token);
        var settings = new SettingsService(Path.Combine(_root, "settings"));
        settings.Load();
        var recent = new RecentProjects(settings);
        recent.Add(project.FilePath);
        recent.RememberShownPicture(project.FilePath, "b.svg");
        settings.Update(s => s with { AiProvider = new AiProviderSettings { Provider = AiProviderKind.Ollama, ModelName = string.Empty } });
        var historyRoot = Path.Combine(_root, "history");
        new ChatHistoryStore(historyRoot).Save(project, ChatHarness.LongConversation());
        var composition = AppShellUiHelpers.Compose(_root, historyRoot: historyRoot);
        _compositions.Add(composition);
        var window = new MainWindowView(composition.Shell) { Width = 1280, Height = 800 };
        window.Show();
        AppShellUiHelpers.Flush();

        // Act: click the card of the project on the welcome screen
        var card = window.GetVisualDescendants().OfType<Button>().First(button => AutomationProperties.GetAutomationId(button) == "WelcomeRecentCard");
        var center = card.TranslatePoint(new Point(card.Bounds.Width / 2, card.Bounds.Height / 2), window)!.Value;
        window.MouseDown(center, MouseButton.Left);
        window.MouseUp(center, MouseButton.Left);
        await Task.Delay(100, token);
        AppShellUiHelpers.Flush();
        await composition.Preview.RenderCompletion;
        AppShellUiHelpers.Flush();
        window.UpdateLayout();
        Dispatcher.UIThread.RunJobs();
        var scroll = window.GetVisualDescendants().OfType<ScrollViewer>().Single(viewer => AutomationProperties.GetAutomationId(viewer) == "ChatTranscriptScroll");

        // Assert: the last shown picture is selected and drawn
        Assert.True(composition.Shell.HasProject);
        Assert.Equal(picture, composition.Shell.Workspace.SelectedFile);
        Assert.Equal(picture, composition.Preview.CurrentImage?.SourcePath);

        // Assert: the conversation was restored and is scrolled to its latest message
        Assert.True(composition.Chat.Transcript.Count >= 40);
        Assert.True(scroll.Extent.Height > scroll.Viewport.Height, $"Extent {scroll.Extent.Height}, viewport {scroll.Viewport.Height}.");
        Assert.True(
            scroll.Offset.Y > 0 && scroll.Offset.Y + scroll.Viewport.Height >= scroll.Extent.Height - 24,
            $"Offset {scroll.Offset.Y}, viewport {scroll.Viewport.Height}, extent {scroll.Extent.Height}.");

        window.Close();
    }

    /// <summary>
    ///     Test that the composed main window is drawn with a dark surface in the dark theme and a light surface in
    ///     the light theme, as measured on the picture of the window.
    /// </summary>
    [AvaloniaFact]
    public void CanvasPilot_Theme_ComposedWindowIsDarkInDarkAndLightInLight()
    {
        // Arrange: the composed application in its main window
        var composition = AppShellUiHelpers.Compose(_root);
        _compositions.Add(composition);
        var window = new MainWindowView(composition.Shell);

        // Act: draw it in the dark and in the light variant and measure how bright the picture is on average
        var dark = MeanBrightness(window, "theme-dark.png", ThemeVariant.Dark);
        var light = MeanBrightness(window, "theme-light.png", ThemeVariant.Light);
        window.Close();

        // Assert: the dark picture is dark, the light one is light, and they are clearly apart
        Assert.True(dark < 0.3, $"The dark window has a mean brightness of {dark:F2}.");
        Assert.True(light > 0.6, $"The light window has a mean brightness of {light:F2}.");
    }

    /// <summary>
    ///     Test that the user can choose the dark theme on the Appearance page of the Settings dialog of the running
    ///     application: the open main window turns dark when OK is chosen, and the choice is stored for the next run.
    /// </summary>
    [AvaloniaFact]
    public async Task CanvasPilot_Theme_ChoiceInSettingsDialogChangesTheWindowAndIsRemembered()
    {
        // Arrange: the application composed for the running application, with its main window shown
        var composition = AppShellUiHelpers.Compose(_root, application: Application.Current);
        _compositions.Add(composition);
        var window = new MainWindowView(composition.Shell);
        window.Show();
        Dispatcher.UIThread.RunJobs();

        // Act: open Settings, go to the Appearance page, choose Dark, and choose OK
        var shown = ((IAsyncRelayCommand)window.FindControl<MenuItem>("SettingsMenuItem")!.Command!).ExecuteAsync(null);
        var dialog = AppShellUiHelpers.FindDialog<SettingsDialogView>(window);
        var pages = SettingsUiHelpers.Find<ListBox>(dialog, "SettingsPageList");
        pages.SelectedItem = pages.Items.OfType<ISettingsPage>().Single(page => page.Title == "Appearance");
        Dispatcher.UIThread.RunJobs();
        var theme = SettingsUiHelpers.Find<ComboBox>(dialog, "AppearanceThemeComboBox");
        theme.SelectedItem = theme.Items.OfType<SettingsChoice<ThemeChoice>>().Single(choice => choice.Value == ThemeChoice.Dark);
        SettingsUiHelpers.Click(dialog, SettingsUiHelpers.Find<Button>(dialog, "SettingsOkButton"));
        await shown;
        Dispatcher.UIThread.RunJobs();
        var remembered = new SettingsService(Path.Combine(_root, "settings")).Load().Appearance.Theme;

        // Assert: the window is dark, and the choice is what the next run reads
        Assert.Equal(ThemeVariant.Dark, window.ActualThemeVariant);
        Assert.Equal(
            ThemeUiHelpers.ColorOf("CpSurfaceWindow", ThemeVariant.Dark),
            Assert.IsAssignableFrom<ISolidColorBrush>(window.Background).Color);
        Assert.Equal(ThemeChoice.Dark, remembered);
        window.Close();
    }

    /// <summary>
    ///     Test that, in the composed main window with the real image exporter, the menu of the picture opens on a
    ///     right-click of the stage and on Shift+F10 and the Menu key with the stage focused, that it and its items are
    ///     named for screen readers, that its items are usable only while a picture is shown, and that it opens neither
    ///     by a right-click nor by a key while a marking tool is chosen.
    /// </summary>
    [AvaloniaFact]
    public async Task CanvasPilot_PictureMenu_ComposedWindow_OpensNamedItemsAndStaysClosedWhileMarking()
    {
        // Arrange: a remembered project, the composed application with nothing substituted, its main window shown
        // with the project open, and a 40 x 20 SVG and a broken one in the project
        var token = TestContext.Current.CancellationToken;
        var projects = Directory.CreateDirectory(Path.Combine(_root, "projects")).FullName;
        var created = await new ProjectService().CreateAsync(projects, "Menu", token);
        new RecentProjects(new SettingsService(Path.Combine(_root, "settings"))).Add(created.Project!.FilePath);
        var composition = AppShellUiHelpers.Compose(_root);
        _compositions.Add(composition);
        var window = new MainWindowView(composition.Shell);
        window.Show();
        await composition.Shell.RecentProjects[0].OpenCommand.ExecuteAsync(null);
        var folder = composition.Shell.CurrentProject!.FolderPath;
        await File.WriteAllTextAsync(
            Path.Combine(folder, "red.svg"),
            "<svg xmlns='http://www.w3.org/2000/svg' width='40' height='20'><rect width='40' height='20' fill='#ff0000'/></svg>",
            token);
        await File.WriteAllTextAsync(Path.Combine(folder, "broken.svg"), "<svg><not closed", token);
        var explorer = composition.Shell.Workspace.Explorer;
        explorer.Refresh();
        AppShellUiHelpers.Flush();
        var stage = SettingsUiHelpers.Find<Grid>(window, "PreviewStage");
        var menu = stage.ContextMenu ?? throw new InvalidOperationException("The stage has no menu.");
        var stageLeft = stage.TranslatePoint(new Point(20, stage.Bounds.Height / 2), window)
                        ?? throw new InvalidOperationException("The stage is not in the window.");

        // Act: with no file selected right-click the empty stage; show the picture, right-click it, read the names, and
        // press Shift+F10 and the Menu key on the focused stage; choose the rectangle tool, right-click the picture,
        // and press the Menu key and Shift+F10 on the focused stage; put the tool away and right-click the picture;
        // then show the broken file and right-click the error
        RightClick(window, stageLeft);
        var openedOnEmptyStage = menu.IsOpen;
        var usableOnEmptyStage = UsableItems(menu);
        menu.Close();
        explorer.SelectFile(Path.Combine(folder, "red.svg"));
        await composition.Preview.RenderCompletion;
        AppShellUiHelpers.Flush();
        var scroller = SettingsUiHelpers.Find<ScrollViewer>(window, "PreviewScroller");
        var picture = Center(window, SettingsUiHelpers.Find<Image>(window, "PreviewImage"));
        RightClick(window, picture);
        var openedOnPicture = menu.IsOpen;
        var usableOnPicture = UsableItems(menu);
        var itemNames = menu.Items.OfType<MenuItem>().Select(AutomationProperties.GetName).ToList();
        var menuName = AutomationProperties.GetName(menu);
        menu.Close();
        scroller.Focus();
        PressKey(window, PhysicalKey.F10, RawInputModifiers.Shift);
        var openedByShiftF10 = menu.IsOpen;
        menu.Close();
        scroller.Focus();
        PressKey(window, PhysicalKey.ContextMenu, RawInputModifiers.None);
        var openedByMenuKey = menu.IsOpen;
        menu.Close();
        composition.Annotation.SelectTool(AnnotationTool.Rectangle);
        AppShellUiHelpers.Flush();
        RightClick(window, picture);
        var openedByRightClickWhileMarking = menu.IsOpen;
        menu.Close();
        scroller.Focus();
        PressKey(window, PhysicalKey.ContextMenu, RawInputModifiers.None);
        var openedByMenuKeyWhileMarking = menu.IsOpen;
        menu.Close();
        scroller.Focus();
        PressKey(window, PhysicalKey.F10, RawInputModifiers.Shift);
        var openedByShiftF10WhileMarking = menu.IsOpen;
        menu.Close();
        composition.Annotation.SelectTool(AnnotationTool.None);
        AppShellUiHelpers.Flush();
        RightClick(window, picture);
        var openedAfterMarking = menu.IsOpen;
        menu.Close();
        explorer.SelectFile(Path.Combine(folder, "broken.svg"));
        await composition.Preview.RenderCompletion;
        AppShellUiHelpers.Flush();
        RightClick(window, Center(window, SettingsUiHelpers.Find<StackPanel>(window, "PreviewErrorPanel")));
        var openedOnError = menu.IsOpen;
        var usableOnError = UsableItems(menu);
        menu.Close();

        // Assert: the empty stage and the error open the menu with unusable items; with the picture shown it opens all
        // three ways, is named, and its items are named and usable; nothing opens while marking, and the picture opens
        // it again after
        Assert.True(openedOnEmptyStage);
        Assert.Equal([false, false], usableOnEmptyStage);
        Assert.True(openedOnPicture);
        Assert.True(openedByShiftF10);
        Assert.True(openedByMenuKey);
        Assert.Equal([true, true], usableOnPicture);
        Assert.Equal("Picture", menuName);
        Assert.Equal(["Copy image", "Export as PNG"], itemNames);
        Assert.False(openedByRightClickWhileMarking);
        Assert.False(openedByMenuKeyWhileMarking);
        Assert.False(openedByShiftF10WhileMarking);
        Assert.True(openedAfterMarking);
        Assert.True(openedOnError);
        Assert.Equal([false, false], usableOnError);

        window.Close();
    }

    /// <summary>
    ///     Presses and releases the right button at a point of a window, the way a user right-clicks.
    /// </summary>
    /// <param name="window">The window.</param>
    /// <param name="point">The point, in the window's coordinates.</param>
    private static void RightClick(Window window, Point point)
    {
        window.MouseDown(point, MouseButton.Right);
        window.MouseUp(point, MouseButton.Right);
        AppShellUiHelpers.Flush();
    }

    /// <summary>
    ///     Presses and releases a key with modifiers on a window, the way a user types it.
    /// </summary>
    /// <param name="window">The window.</param>
    /// <param name="key">The physical key.</param>
    /// <param name="modifiers">The modifiers held down.</param>
    private static void PressKey(Window window, PhysicalKey key, RawInputModifiers modifiers)
    {
        window.KeyPressQwerty(key, modifiers);
        window.KeyReleaseQwerty(key, modifiers);
        AppShellUiHelpers.Flush();
    }

    /// <summary>
    ///     Reads whether each item of a menu is usable, in the order of the items.
    /// </summary>
    /// <param name="menu">The menu.</param>
    /// <returns>Whether each item is usable.</returns>
    private static bool[] UsableItems(ContextMenu menu)
    {
        return menu.Items.OfType<MenuItem>().Select(item => item.IsEffectivelyEnabled).ToArray();
    }

    /// <summary>
    ///     Gets the center of a control in a window's coordinates.
    /// </summary>
    /// <param name="window">The window.</param>
    /// <param name="control">The control.</param>
    /// <returns>The point at the control's center.</returns>
    private static Point Center(Window window, Control control)
    {
        return control.TranslatePoint(new Point(control.Bounds.Width / 2, control.Bounds.Height / 2), window)
               ?? throw new InvalidOperationException("The control is not in the window.");
    }

    /// <summary>
    ///     Draws the window in a theme variant and measures the mean brightness of the picture.
    /// </summary>
    /// <param name="window">The window.</param>
    /// <param name="fileName">The name of the temporary picture.</param>
    /// <param name="variant">The theme variant.</param>
    /// <returns>The mean brightness, from 0 (black) to 1 (white), sampled on a coarse grid.</returns>
    private double MeanBrightness(MainWindowView window, string fileName, ThemeVariant variant)
    {
        // Draw the window to a temporary picture and read the pixels back
        var path = ScreenshotSupport.Capture(window, _root, fileName, 960, 600, variant);
        using var picture = new Bitmap(path);
        var (pixels, stride) = ScreenshotSupport.ReadPixels(picture);

        // Average the perceived brightness of every 8th pixel in both directions
        double sum = 0;
        var count = 0;
        for (var y = 0; y < picture.PixelSize.Height; y += 8)
        {
            for (var x = 0; x < picture.PixelSize.Width; x += 8)
            {
                var offset = (y * stride) + (x * 4);
                sum += ((0.299 * pixels[offset]) + (0.587 * pixels[offset + 1]) + (0.114 * pixels[offset + 2])) / 255.0;
                count++;
            }
        }

        return sum / count;
    }
}
