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
///     Headless UI tests for the layout of the main window: the widths of the three columns at several window widths so
///     that nothing is cropped, the widths that were saved, the keyboard use of the splitters, and the saving and restoring
///     of the size of the window.
/// </summary>
public sealed class WorkspaceLayoutViewTests : IDisposable
{
    /// <summary>
    ///     A 40 x 20 SVG with a red rectangle.
    /// </summary>
    private const string RedSvg =
        "<svg xmlns='http://www.w3.org/2000/svg' width='40' height='20'>" +
        "<rect width='40' height='20' fill='#ff0000'/></svg>";

    /// <summary>
    ///     Temporary root removed on disposal.
    /// </summary>
    private readonly string _root = Directory.CreateTempSubdirectory("canvaspilot-ui-layout-").FullName;

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
    ///     Test that at the window widths of the design nothing is cropped: the three columns lie inside the window, the stage
    ///     keeps its least width and holds the whole picture, and the explorer gives up room first when the window is narrow.
    /// </summary>
    /// <param name="width">The width of the window.</param>
    /// <param name="explorer">The expected width of the explorer.</param>
    /// <param name="chat">The expected width of the chat column.</param>
    [AvaloniaTheory]
    [InlineData(1440, 264, 400)]
    [InlineData(1100, 264, 400)]
    [InlineData(1000, 238, 400)]
    [InlineData(960, 198, 400)]
    public async Task WorkspaceLayout_WindowWidth_NothingIsCroppedAndTheExplorerGivesUpRoomFirst(int width, int explorer, int chat)
    {
        // Arrange: a window of the width over a project with a picture that is shown
        var (composition, window) = await OpenWindowAsync(width, 700);

        // Act: read the columns
        var explorerHost = SettingsUiHelpers.Find<ContentControl>(window, "WorkspaceExplorerHost");
        var stageHost = SettingsUiHelpers.Find<ContentControl>(window, "WorkspacePreviewHost");
        var chatHost = SettingsUiHelpers.Find<ContentControl>(window, "WorkspaceChatHost");
        var picture = SettingsUiHelpers.Find<Image>(window, "PreviewImage");
        var stageLeft = LeftOf(stageHost, window);
        var chatRight = LeftOf(chatHost, window) + chatHost.Bounds.Width;
        var pictureLeft = LeftOf(picture, window);

        // Assert: the widths of the design, everything inside the window, and the picture inside the stage
        Assert.Equal(explorer, explorerHost.Bounds.Width, 1);
        Assert.Equal(chat, chatHost.Bounds.Width, 1);
        Assert.True(stageHost.Bounds.Width >= 359.9);
        Assert.True(chatRight <= window.Bounds.Width + 0.5);
        Assert.True(pictureLeft >= stageLeft - 0.5);
        Assert.True(pictureLeft + picture.Bounds.Width <= stageLeft + stageHost.Bounds.Width + 0.5);
        Assert.True(composition.Shell.HasProject);

        window.Close();
    }

    /// <summary>
    ///     Test that the widths that were saved are used when the window opens, and that a choice with the keyboard on a
    ///     splitter, which moves it by 16 pixels, is kept in the view model and saved in the settings.
    /// </summary>
    [AvaloniaFact]
    public async Task WorkspaceLayout_Splitters_SavedWidthsUsedAndKeyboardChoiceSaved()
    {
        // Arrange: settings that hold widths, and a window over a project
        var (composition, window) = await OpenWindowAsync(
            1600,
            800,
            s => s with { Window = s.Window with { ExplorerWidth = 300, ChatWidth = 500 } });
        var explorerHost = SettingsUiHelpers.Find<ContentControl>(window, "WorkspaceExplorerHost");
        var chatHost = SettingsUiHelpers.Find<ContentControl>(window, "WorkspaceChatHost");
        var restored = (explorerHost.Bounds.Width, chatHost.Bounds.Width);

        // Act: focus each splitter and press the right and the left arrow key
        var explorerSplitter = SettingsUiHelpers.Find<GridSplitter>(window, "WorkspaceSplitter");
        explorerSplitter.Focus();
        window.KeyPressQwerty(PhysicalKey.ArrowRight, RawInputModifiers.None);
        window.KeyReleaseQwerty(PhysicalKey.ArrowRight, RawInputModifiers.None);
        LayOut(window);
        var chatSplitter = SettingsUiHelpers.Find<GridSplitter>(window, "WorkspaceChatSplitter");
        chatSplitter.Focus();
        window.KeyPressQwerty(PhysicalKey.ArrowLeft, RawInputModifiers.None);
        window.KeyReleaseQwerty(PhysicalKey.ArrowLeft, RawInputModifiers.None);
        LayOut(window);

        // Assert: the saved widths were used, each key moved its splitter by 16, and the choices were saved
        Assert.Equal((300d, 500d), restored);
        Assert.Equal(316, composition.Shell.Workspace.ExplorerWidth);
        Assert.Equal(516, composition.Shell.Workspace.ChatWidth);
        Assert.Equal(316, explorerHost.Bounds.Width, 1);
        Assert.Equal(516, chatHost.Bounds.Width, 1);
        Assert.Equal((316, 516), (composition.Settings.Current.Window.ExplorerWidth, composition.Settings.Current.Window.ChatWidth));

        window.Close();
    }

    /// <summary>
    ///     Test that closing the window saves its size and state, and that a window made later over the same settings opens
    ///     with that size.
    /// </summary>
    [AvaloniaFact]
    public async Task WorkspaceLayout_CloseWindow_SavesTheSizeAndANewWindowOpensWithIt()
    {
        // Arrange: a shown window that the user made another size
        var (composition, window) = await OpenWindowAsync(1440, 900);
        window.Width = 1200;
        window.Height = 700;
        LayOut(window);

        // Act: close it, and make another window over the same shell
        window.Close();
        var saved = composition.Settings.Current.Window;
        var next = new MainWindowView(composition.Shell);

        // Assert: the size was saved in its normal state, and the next window opens with it
        Assert.Equal((1200, 700, SavedWindowState.Normal), (saved.Width, saved.Height, saved.State));
        Assert.Equal((1200d, 700d), (next.Width, next.Height));
        Assert.Equal(WindowState.Normal, next.WindowState);
    }

    /// <summary>
    ///     Creates a window of a size over a project with a picture that is selected and drawn.
    /// </summary>
    /// <param name="width">The width of the window.</param>
    /// <param name="height">The height of the window.</param>
    /// <param name="seed">Changes the settings before the application is composed, or <see langword="null" /> for none.</param>
    /// <returns>The composition and the shown window.</returns>
    private async Task<(ApplicationComposition Composition, MainWindowView Window)> OpenWindowAsync(
        int width,
        int height,
        Func<AppSettings, AppSettings>? seed = null)
    {
        var token = TestContext.Current.CancellationToken;
        var projects = Directory.CreateDirectory(Path.Combine(_root, "projects")).FullName;
        var created = await new ProjectService().CreateAsync(projects, "Layout", token);
        var picture = Path.Combine(created.Project!.FolderPath, "red.svg");
        await File.WriteAllTextAsync(picture, RedSvg, token);
        var seeding = new SettingsService(Path.Combine(_root, "settings"));
        seeding.Load();
        new RecentProjects(seeding).Add(created.Project.FilePath);
        if (seed is not null)
        {
            seeding.Update(seed);
        }

        var composition = AppShellUiHelpers.Compose(_root);
        _compositions.Add(composition);
        var window = new MainWindowView(composition.Shell) { Width = width, Height = height };
        window.Show();
        await composition.Shell.RecentProjects[0].OpenCommand.ExecuteAsync(null);
        composition.Shell.Workspace.Explorer.SelectFile(picture);
        await composition.Preview.RenderCompletion;
        LayOut(window);
        return (composition, window);
    }

    /// <summary>
    ///     Finds the place of the left edge of a control in a window.
    /// </summary>
    /// <param name="control">The control.</param>
    /// <param name="window">The window.</param>
    /// <returns>The place, in the coordinates of the window.</returns>
    private static double LeftOf(Control control, Window window)
    {
        return control.TranslatePoint(new Point(0, 0), window)?.X ?? double.NaN;
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
}
