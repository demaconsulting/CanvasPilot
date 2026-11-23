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

using System.Globalization;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using DemaConsulting.CanvasPilot.AnnotationSubsystem;
using DemaConsulting.CanvasPilot.AppShellSubsystem;
using DemaConsulting.CanvasPilot.PreviewSubsystem;
using DemaConsulting.CanvasPilot.ProjectSubsystem;
using DemaConsulting.CanvasPilot.SettingsSubsystem;
using DemaConsulting.CanvasPilot.StyleSubsystem;

namespace DemaConsulting.CanvasPilot.UiTests;

/// <summary>
///     The opt-in tests that draw the screenshots of the README and the user guide: the main window in a conversation, the
///     welcome screen, and the Style Kits manager. Each test builds its demonstration project in a temporary folder, shows
///     the real window headless, and saves it to the folder that the environment variable
///     <c>CANVASPILOT_SCREENSHOT_DIR</c> names; without the variable every test here skips itself. The script
///     <c>docs/images/render-screenshots.ps1</c> runs them.
/// </summary>
/// <remarks>
///     The pictures are of the real application, so they change when the application changes. The tests do not assert what
///     the pictures show: the other UI tests do that. The content is the same on every run, the culture is fixed to US
///     English, and the headless platform draws with the embedded Inter font, so a new drawing differs from the committed one
///     only by the anti-aliasing of the platform and by the render time that the status bar tells.
/// </remarks>
public sealed class ScreenshotTests : IDisposable
{
    /// <summary>
    ///     The width of the windows of the screenshots, in logical pixels.
    /// </summary>
    private const double WindowWidth = 1600;

    /// <summary>
    ///     The height of the windows of the screenshots, in logical pixels.
    /// </summary>
    private const double WindowHeight = 1000;

    /// <summary>
    ///     How long a screenshot waits for the pictures that are drawn in the background before it draws what it has.
    /// </summary>
    private static readonly TimeSpan SettleLimit = TimeSpan.FromSeconds(30);

    /// <summary>
    ///     Temporary root that holds the log, settings, and style folders of the application, removed on disposal.
    /// </summary>
    private readonly string _root = Directory.CreateTempSubdirectory("canvaspilot-screenshots-").FullName;

    /// <summary>
    ///     The folder that holds the demonstration projects, whose path is shown on the cards of the welcome screen, so it is a
    ///     short neutral path when the computer allows one. It is made when a test first asks for it, so that a run that skips
    ///     the screenshots makes nothing, and it is removed on disposal.
    /// </summary>
    private readonly Lazy<string> _projectsFolder = new(MakeProjectsFolder);

    /// <summary>
    ///     The compositions created by a test, disposed with the fixture so no watcher outlives it.
    /// </summary>
    private readonly List<ApplicationComposition> _compositions = [];

    /// <summary>
    ///     What the tests made besides the compositions, disposed with the fixture.
    /// </summary>
    private readonly List<IDisposable> _owned = [];

    /// <summary>
    ///     The culture of the thread before the test changed it.
    /// </summary>
    private readonly CultureInfo _culture = CultureInfo.CurrentCulture;

    /// <summary>
    ///     The user interface culture of the thread before the test changed it.
    /// </summary>
    private readonly CultureInfo _uiCulture = CultureInfo.CurrentUICulture;

    /// <summary>
    ///     Initializes a new instance of the <see cref="ScreenshotTests" /> class and fixes the culture, so that times and
    ///     numbers are written the same on every computer.
    /// </summary>
    public ScreenshotTests()
    {
        CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("en-US");
        CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("en-US");
    }

    /// <inheritdoc />
    public void Dispose()
    {
        CultureInfo.CurrentCulture = _culture;
        CultureInfo.CurrentUICulture = _uiCulture;
        foreach (var owned in _owned)
        {
            owned.Dispose();
        }

        foreach (var composition in _compositions)
        {
            composition.Dispose();
        }

        DeleteFolder(_root);
        if (_projectsFolder.IsValueCreated)
        {
            DeleteFolder(_projectsFolder.Value);
        }
    }

    /// <summary>
    ///     Draws the main window in the middle of a conversation: a picture with two marks on it, the explorer, the answers of
    ///     the copilot with the tools it used, and a message that holds the two marks. It writes the dark picture that the
    ///     README shows and the light one beside it.
    /// </summary>
    /// <returns>A task that completes when the pictures are written.</returns>
    [AvaloniaFact]
    [Trait("Category", ScreenshotSupport.Category)]
    public async Task Screenshot_MainWindow_ConversationWithPictureMarksAndTools()
    {
        // Arrange: the requested folder (skips the test when none was requested), the user's style kits, and the application
        var folder = ScreenshotSupport.RequireFolder();
        ScreenshotDemo.WriteUserKits(Path.Combine(_root, "styles"));
        var composition = Compose();
        await composition.StyleStartupRefresh;
        _owned.Add(await ScreenshotDemo.ShowReadyProviderAsync(composition));
        var project = await ScreenshotDemo.CreateProjectAsync(_projectsFolder.Value, ScreenshotDemo.ProjectName, ScreenshotDemo.WriteReleaseOverview);
        var window = new MainWindowView(composition.Shell);
        window.Show();

        // Act: open the project with the DEMA style kit, show the overview, mark two places, and write the message
        await composition.Projects.OpenAsync(project.FilePath, TestContext.Current.CancellationToken);
        await composition.Projects.SetActiveStyleKitAsync("dema", TestContext.Current.CancellationToken);
        var explorer = composition.Shell.Workspace.Explorer;
        foreach (var node in explorer.Roots.Where(root => root.IsFolder))
        {
            node.IsExpanded = true;
        }

        explorer.SelectFile(Path.Combine(project.FolderPath, ScreenshotDemo.MainPicture));
        await composition.Preview.RenderCompletion;
        Draw(composition, AnnotationTool.Ellipse, ScreenshotDemo.EllipseMark);
        Draw(composition, AnnotationTool.Rectangle, ScreenshotDemo.RectangleMark);
        ScreenshotDemo.FillTranscript(composition.Chat, project.FolderPath);
        composition.Composer.InsertTyped(" Make mark 1 red, and label the tallest bar in mark 2 with its value.");
        window.GetVisualDescendants().OfType<ComposerSubsystem.ComposerEditorView>().First().FocusEditor();
        await SettleAsync(() =>
            AllNodes(explorer.Roots).Where(node => node.IsPreviewable).All(node => node.HasThumbnail)
            && ChipPictures(window).Count == 2 && ChipPictures(window).All(picture => picture.IsVisible));

        // Assert: the dark picture and the light one were written
        var dark = ScreenshotSupport.Capture(window, folder, "main-window.png", WindowWidth, WindowHeight, ThemeVariant.Dark);
        var light = ScreenshotSupport.Capture(window, folder, "main-window-light.png", WindowWidth, WindowHeight, ThemeVariant.Light);
        window.Close();
        Assert.True(File.Exists(dark));
        Assert.True(File.Exists(light));
    }

    /// <summary>
    ///     Draws the welcome screen with four recent projects, each with the small picture of its first picture.
    /// </summary>
    /// <returns>A task that completes when the picture is written.</returns>
    [AvaloniaFact]
    [Trait("Category", ScreenshotSupport.Category)]
    public async Task Screenshot_Welcome_RecentProjectsWithThumbnails()
    {
        // Arrange: four remembered projects, opened one day, three days, a week, and a month ago, and a ready provider
        var folder = ScreenshotSupport.RequireFolder();
        var settings = new SettingsService(Path.Combine(_root, "settings"));
        settings.Load();
        var now = DateTimeOffset.Now;
        await RememberAsync(settings, "Architecture Sketch", ScreenshotDemo.ArchitectureSvg, 5, now.AddDays(-9));
        await RememberAsync(settings, "Onboarding Flow", ScreenshotDemo.OnboardingSvg, 2, now.AddDays(-20));
        await RememberAsync(settings, "Quarterly Review", ScreenshotDemo.ReviewSvg, 7, now.AddDays(-3));
        await RememberAsync(settings, "Release Overview", ScreenshotDemo.OverviewSvg, 3, now.AddDays(-1));
        var composition = Compose();
        _owned.Add(await ScreenshotDemo.ShowReadyProviderAsync(composition));
        var window = new MainWindowView(composition.Shell);
        window.Show();

        // Act: wait for the small pictures of the cards
        await SettleAsync(() => composition.Shell.RecentProjects.Count == 4 && composition.Shell.RecentProjects.All(item => item.HasThumbnail));

        // Assert: the picture was written
        var path = ScreenshotSupport.Capture(window, folder, "welcome.png", WindowWidth, WindowHeight, ThemeVariant.Dark);
        window.Close();
        Assert.True(File.Exists(path));
    }

    /// <summary>
    ///     Draws the Style Kits manager over a project that uses the DEMA kit, with the kits that come with the program and
    ///     two kits of the user, and the pictures of the examples of the chosen kit.
    /// </summary>
    /// <returns>A task that completes when the picture is written.</returns>
    [AvaloniaFact]
    [Trait("Category", ScreenshotSupport.Category)]
    public async Task Screenshot_StyleKits_ManagerWithBuiltInAndUserKits()
    {
        // Arrange: the user's kits in a neutral folder, an open project that uses DEMA, and the main window that owns the manager
        var folder = ScreenshotSupport.RequireFolder();
        var kits = Path.Combine(Path.GetDirectoryName(_projectsFolder.Value)!, "Styles");
        ScreenshotDemo.WriteUserKits(kits);
        var composition = Compose(kits);
        await composition.StyleStartupRefresh;
        var project = await ScreenshotDemo.CreateProjectAsync(_projectsFolder.Value, ScreenshotDemo.ProjectName, ScreenshotDemo.WriteReleaseOverview);
        var main = new MainWindowView(composition.Shell);
        main.Show();
        await composition.Projects.OpenAsync(project.FilePath, TestContext.Current.CancellationToken);
        await composition.Projects.SetActiveStyleKitAsync("dema", TestContext.Current.CancellationToken);

        // Act: open the manager from the Tools menu command and choose a kit of the user, so that its examples are drawn
        var opened = composition.Shell.ShowStyleKitsCommand.ExecuteAsync(null);
        var dialog = AppShellUiHelpers.FindDialog<StyleKitManagerView>(main);
        var manager = (StyleKitManagerViewModel)dialog.DataContext!;
        manager.SelectedItem = manager.Groups.SelectMany(group => group.Items).First(item => item.Id == "team-blue");
        await manager.ThumbnailTask;
        await SettleAsync(() => true);

        // Assert: the picture was written
        var path = ScreenshotSupport.Capture(dialog, folder, "style-kits.png", 1180, 1000, ThemeVariant.Dark);
        dialog.Close();
        await opened;
        main.Close();
        Assert.True(File.Exists(path));
    }

    /// <summary>
    ///     Makes the folder that holds the demonstration projects: a short folder at the root of the drive when the computer
    ///     allows one, so that the paths on the cards of the welcome screen are neutral, and a temporary folder otherwise.
    /// </summary>
    /// <returns>The full path of the new, empty folder.</returns>
    private static string MakeProjectsFolder()
    {
        var preferred = Path.Combine(Path.GetPathRoot(Path.GetTempPath()) ?? string.Empty, "CanvasPilot", "Projects");
        try
        {
            // Use the neutral folder only when it is new, so that nothing of the user is ever removed with it
            if (!Directory.Exists(Path.GetDirectoryName(preferred)))
            {
                return Directory.CreateDirectory(preferred).FullName;
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // The root of the drive cannot be written, so the temporary folder is used
        }

        return Directory.CreateTempSubdirectory("canvaspilot-screenshot-projects-").FullName;
    }

    /// <summary>
    ///     Removes a folder of the test and, when it is the neutral folder, the folder above it that was made with it.
    /// </summary>
    /// <param name="path">The folder.</param>
    private static void DeleteFolder(string path)
    {
        var target = Path.GetFileName(path) == "Projects" && Path.GetFileName(Path.GetDirectoryName(path)) == "CanvasPilot"
            ? Path.GetDirectoryName(path)!
            : path;
        if (Directory.Exists(target))
        {
            Directory.Delete(target, recursive: true);
        }
    }

    /// <summary>
    ///     Lists a tree of nodes of the explorer, each node after its parent.
    /// </summary>
    /// <param name="nodes">The nodes of the first level.</param>
    /// <returns>Every node of the tree.</returns>
    private static IEnumerable<ProjectNodeViewModel> AllNodes(IEnumerable<ProjectNodeViewModel> nodes)
    {
        foreach (var node in nodes)
        {
            yield return node;
            foreach (var child in AllNodes(node.Children))
            {
                yield return child;
            }
        }
    }

    /// <summary>
    ///     Finds the small pictures on the chips of the message that is being written.
    /// </summary>
    /// <param name="window">The main window.</param>
    /// <returns>The picture controls of the chips, whether they show a picture yet or not.</returns>
    private static List<Image> ChipPictures(MainWindowView window)
    {
        return window.GetVisualDescendants().OfType<Image>()
            .Where(image => AutomationProperties.GetAutomationId(image) == "ComposerChipThumbnail")
            .ToList();
    }

    /// <summary>
    ///     Draws a mark on the shown picture as the pointer does, and gives it no note.
    /// </summary>
    /// <param name="composition">The application.</param>
    /// <param name="tool">The tool that draws it.</param>
    /// <param name="corners">The two corners of the shape, in the pixels of the picture.</param>
    private static void Draw(ApplicationComposition composition, AnnotationTool tool, (RenderPoint From, RenderPoint To) corners)
    {
        composition.Annotation.SelectTool(tool);
        Assert.True(composition.Annotation.BeginStroke(corners.From));
        composition.Annotation.EndStroke(corners.To);
        composition.Annotation.SkipNote();
    }

    /// <summary>
    ///     Waits, while the user interface keeps running, until the pictures that are drawn in the background are there, and
    ///     then lets the window draw once more. It gives up after <see cref="SettleLimit" /> and draws what it has.
    /// </summary>
    /// <param name="ready">Tells whether the pictures are there.</param>
    /// <returns>A task that completes when the pictures are there or the limit has passed.</returns>
    private static async Task SettleAsync(Func<bool> ready)
    {
        var start = DateTime.UtcNow;
        while (!ready() && DateTime.UtcNow - start < SettleLimit)
        {
            Dispatcher.UIThread.RunJobs();
            await Task.Delay(50, TestContext.Current.CancellationToken);
        }

        Dispatcher.UIThread.RunJobs();
    }

    /// <summary>
    ///     Creates a project for the welcome screen and remembers it in the recent projects with the time it was opened.
    /// </summary>
    /// <param name="settings">The settings of the application, which hold the list.</param>
    /// <param name="name">The project name.</param>
    /// <param name="picture">The SVG text of the first picture.</param>
    /// <param name="pictures">How many pictures the project holds.</param>
    /// <param name="opened">When the project was opened.</param>
    /// <returns>A task that completes when the project is remembered.</returns>
    private async Task RememberAsync(SettingsService settings, string name, string picture, int pictures, DateTimeOffset opened)
    {
        var project = await ScreenshotDemo.CreateProjectAsync(_projectsFolder.Value, name, folder => ScreenshotDemo.WriteWelcomeProject(folder, picture, pictures));
        new RecentProjects(settings, new FixedClock(opened)).Add(project.FilePath);
    }

    /// <summary>
    ///     Creates the application on the temporary root.
    /// </summary>
    /// <param name="styleFolder">The folder of the style kits of the user, or <see langword="null" /> for the one in the temporary root.</param>
    /// <returns>The composition.</returns>
    private ApplicationComposition Compose(string? styleFolder = null)
    {
        var composition = AppShellUiHelpers.Compose(_root, styleFolder: styleFolder);
        _compositions.Add(composition);
        return composition;
    }

    /// <summary>
    ///     A clock that always says the same time, to give a project the time it was opened.
    /// </summary>
    private sealed class FixedClock : TimeProvider
    {
        /// <summary>
        ///     The time the clock says.
        /// </summary>
        private readonly DateTimeOffset _now;

        /// <summary>
        ///     Initializes a new instance of the <see cref="FixedClock" /> class.
        /// </summary>
        /// <param name="now">The time the clock says.</param>
        public FixedClock(DateTimeOffset now)
        {
            _now = now;
        }

        /// <inheritdoc />
        public override DateTimeOffset GetUtcNow() => _now.ToUniversalTime();
    }
}
