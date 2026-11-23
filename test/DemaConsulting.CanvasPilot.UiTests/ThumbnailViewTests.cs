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
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using DemaConsulting.CanvasPilot.PreviewSubsystem;
using DemaConsulting.CanvasPilot.ProjectSubsystem;

namespace DemaConsulting.CanvasPilot.UiTests;

/// <summary>
///     Headless UI tests for the thumbnails of the explorer: the provider that makes them over the real render service, and
///     the rows that show them.
/// </summary>
public sealed class ThumbnailViewTests : IDisposable
{
    /// <summary>
    ///     A 40 x 20 SVG with a red rectangle.
    /// </summary>
    private const string RedSvg =
        "<svg xmlns='http://www.w3.org/2000/svg' width='40' height='20'><rect width='40' height='20' fill='#ff0000'/></svg>";

    /// <summary>
    ///     A 40 x 20 SVG with a blue rectangle.
    /// </summary>
    private const string BlueSvg =
        "<svg xmlns='http://www.w3.org/2000/svg' width='40' height='20'><rect width='40' height='20' fill='#0000ff'/></svg>";

    /// <summary>
    ///     Temporary project folder removed on disposal.
    /// </summary>
    private readonly string _folder = Directory.CreateTempSubdirectory("canvaspilot-ui-thumbs-").FullName;

    /// <summary>
    ///     The disposables created by a test, disposed with the fixture so no watcher outlives it.
    /// </summary>
    private readonly List<IDisposable> _disposables = [];

    /// <inheritdoc />
    public void Dispose()
    {
        foreach (var disposable in _disposables)
        {
            disposable.Dispose();
        }

        if (Directory.Exists(_folder))
        {
            Directory.Delete(_folder, recursive: true);
        }
    }

    /// <summary>
    ///     Test that the provider draws a file at the size of a thumbnail, gives every caller a bitmap of its own, draws a file
    ///     once while it does not change, and draws it again when it changes.
    /// </summary>
    [AvaloniaFact]
    public async Task ThumbnailProvider_CreateAsync_DrawsOnceUntilTheFileChanges()
    {
        // Arrange: a provider over the real render service and a picture
        var token = TestContext.Current.CancellationToken;
        var path = Path.Combine(_folder, "red.svg");
        await File.WriteAllTextAsync(path, RedSvg, token);
        var provider = CreateProvider();

        // Act: ask twice, change the file, and ask again
        using var first = await provider.CreateAsync(path, _folder, token);
        using var second = await provider.CreateAsync(path, _folder, token);
        var renderedAfterTwo = provider.RenderCount;
        await File.WriteAllTextAsync(path, BlueSvg + " ", token);
        using var third = await provider.CreateAsync(path, _folder, token);

        // Assert: the size of a thumbnail, a bitmap for each caller, one render for two calls, and a render after the change
        Assert.NotNull(first);
        Assert.Equal(new PixelSize(ThumbnailProvider.ThumbnailWidth, ThumbnailProvider.ThumbnailHeight), first.PixelSize);
        Assert.NotNull(second);
        Assert.NotSame(first, second);
        Assert.Equal(1, renderedAfterTwo);
        Assert.NotNull(third);
        Assert.Equal(2, provider.RenderCount);
    }

    /// <summary>
    ///     Test that the cache holds at most its capacity, drops the least recently used thumbnail first, and that a file that
    ///     is missing or cannot be drawn gives no bitmap.
    /// </summary>
    [AvaloniaFact]
    public async Task ThumbnailProvider_CreateAsync_FullCacheOrBadFile_EvictsLeastRecentlyUsedAndGivesNoBitmap()
    {
        // Arrange: a provider that holds two thumbnails, three pictures, a broken picture, and a missing file
        var token = TestContext.Current.CancellationToken;
        foreach (var name in new[] { "a.svg", "b.svg", "c.svg" })
        {
            await File.WriteAllTextAsync(Path.Combine(_folder, name), RedSvg, token);
        }

        var broken = Path.Combine(_folder, "broken.svg");
        await File.WriteAllTextAsync(broken, "<svg><not closed", token);
        var provider = CreateProvider(capacity: 2);

        // Act: ask for a and b, use a again, ask for c (which pushes b out), and ask for b, a, the broken one, and a missing one
        (await provider.CreateAsync(Path.Combine(_folder, "a.svg"), _folder, token))?.Dispose();
        (await provider.CreateAsync(Path.Combine(_folder, "b.svg"), _folder, token))?.Dispose();
        (await provider.CreateAsync(Path.Combine(_folder, "a.svg"), _folder, token))?.Dispose();
        (await provider.CreateAsync(Path.Combine(_folder, "c.svg"), _folder, token))?.Dispose();
        var cachedAfterThree = provider.CachedCount;
        var rendersAfterThree = provider.RenderCount;
        (await provider.CreateAsync(Path.Combine(_folder, "a.svg"), _folder, token))?.Dispose();
        var rendersAfterA = provider.RenderCount;
        (await provider.CreateAsync(Path.Combine(_folder, "b.svg"), _folder, token))?.Dispose();
        var rendersAfterB = provider.RenderCount;
        var brokenBitmap = await provider.CreateAsync(broken, _folder, token);
        var missingBitmap = await provider.CreateAsync(Path.Combine(_folder, "missing.svg"), _folder, token);
        provider.Clear();

        // Assert: two cached, three renders so far, a still cached, b gone and drawn again, and no bitmap for the bad ones
        Assert.Equal(2, cachedAfterThree);
        Assert.Equal(3, rendersAfterThree);
        Assert.Equal(3, rendersAfterA);
        Assert.Equal(4, rendersAfterB);
        Assert.Null(brokenBitmap);
        Assert.Null(missingBitmap);
        Assert.Equal(0, provider.CachedCount);
        Assert.Throws<ArgumentNullException>(() => new ThumbnailProvider(null!));
        Assert.Throws<ArgumentOutOfRangeException>(() => new ThumbnailProvider(new ImageRenderService(), 0));
        await Assert.ThrowsAsync<ArgumentException>(() => provider.CreateAsync(" ", _folder, token));
    }

    /// <summary>
    ///     Test that a row of a picture shows a thumbnail 28 by 18 once it is made, that a row of a file that is not drawn and a
    ///     folder show none, that turning thumbnails off takes them away, and that turning them on makes them again.
    /// </summary>
    [AvaloniaFact]
    public async Task ProjectExplorerView_Thumbnails_ShownOnRowsOfDrawnFilesAndFollowTheSetting()
    {
        // Arrange: a project with a picture, a PDF asset, and a folder, in a shown explorer that makes thumbnails
        var token = TestContext.Current.CancellationToken;
        await File.WriteAllTextAsync(Path.Combine(_folder, "red.svg"), RedSvg, token);
        await File.WriteAllTextAsync(Path.Combine(_folder, "plan.pdf"), "pdf", token);
        Directory.CreateDirectory(Path.Combine(_folder, "assets"));
        var provider = CreateProvider();
        var explorer = new ProjectExplorerViewModel(new CanvasNetImageSourceFilter(), new NeverRefreshScheduler(), thumbnails: provider);
        _disposables.Add(explorer);
        var window = new Window { Width = 400, Height = 400, Content = new ProjectExplorerView { DataContext = explorer } };
        window.Show();

        // Act: open the project and wait for the thumbnail of the picture
        explorer.Open(_folder);
        await WaitUntilAsync(() => explorer.FindNode(Path.Combine(_folder, "red.svg"))!.HasThumbnail, token);
        window.UpdateLayout();
        var shown = ThumbnailBoxes(window);
        var picture = explorer.FindNode(Path.Combine(_folder, "red.svg"))!;
        explorer.ShowThumbnails = false;
        window.UpdateLayout();
        var whileOff = (ThumbnailBoxes(window).Length, picture.HasThumbnail);
        explorer.ShowThumbnails = true;
        await WaitUntilAsync(() => picture.HasThumbnail, token);
        window.UpdateLayout();

        // Assert: one box of 28 by 18 for the picture only; none while off; one again after
        Assert.Single(shown);
        Assert.Equal((28d, 18d), (shown[0].Bounds.Width, shown[0].Bounds.Height));
        Assert.Equal((0, false), whileOff);
        Assert.Single(ThumbnailBoxes(window));
        Assert.False(explorer.FindNode(Path.Combine(_folder, "plan.pdf"))!.HasThumbnail);
        Assert.False(explorer.FindNode(Path.Combine(_folder, "assets"))!.HasThumbnail);

        window.Close();
    }

    /// <summary>
    ///     Creates a provider over the real render service and registers it for disposal.
    /// </summary>
    /// <param name="capacity">How many thumbnails the cache holds.</param>
    /// <returns>The provider.</returns>
    private ThumbnailProvider CreateProvider(int capacity = 256)
    {
        var provider = new ThumbnailProvider(new ImageRenderService(), capacity);
        _disposables.Add(provider);
        return provider;
    }

    /// <summary>
    ///     Finds the boxes of the thumbnails that are shown in a window.
    /// </summary>
    /// <param name="window">The window.</param>
    /// <returns>The boxes.</returns>
    private static Border[] ThumbnailBoxes(Window window)
    {
        return [.. window.GetVisualDescendants().OfType<Border>()
            .Where(border => Avalonia.Automation.AutomationProperties.GetAutomationId(border) == "ExplorerThumbnail" && border.IsEffectivelyVisible)];
    }

    /// <summary>
    ///     Waits, keeping the user interface thread running, until a condition holds or a timeout passes.
    /// </summary>
    /// <param name="condition">The condition.</param>
    /// <param name="cancellationToken">Cancels the wait.</param>
    /// <returns>A task that completes when the condition holds or the timeout passed.</returns>
    private static async Task WaitUntilAsync(Func<bool> condition, CancellationToken cancellationToken)
    {
        var deadline = DateTime.UtcNow.AddSeconds(20);
        while (!condition() && DateTime.UtcNow < deadline)
        {
            await Task.Delay(50, cancellationToken);
            Dispatcher.UIThread.RunJobs();
        }
    }
}
