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

using Avalonia.Media.Imaging;
using DemaConsulting.CanvasPilot.LoggingSubsystem;
using DemaConsulting.CanvasPilot.ProjectSubsystem;

namespace DemaConsulting.CanvasPilot.AppShellSubsystem;

/// <summary>
///     Draws the pictures of the recent project cards in the background and gives each to its card, unless the cards were
///     replaced in the meantime, in which case the drawing is stopped and the bitmap is released.
/// </summary>
internal sealed class RecentThumbnailLoader : IDisposable
{
    /// <summary>
    ///     Draws the pictures.
    /// </summary>
    private readonly IThumbnailSource _source;

    /// <summary>
    ///     Runs an action on the thread of the user interface.
    /// </summary>
    private readonly Action<Action> _onOwnerThread;

    /// <summary>
    ///     The application log, or null.
    /// </summary>
    private readonly RollingFileLogger? _logger;

    /// <summary>
    ///     Stops the drawing for the cards of the generation that is current.
    /// </summary>
    private CancellationTokenSource _generation = new();

    /// <summary>
    ///     Initializes a new instance of the <see cref="RecentThumbnailLoader" /> class.
    /// </summary>
    /// <param name="source">Draws the pictures.</param>
    /// <param name="onOwnerThread">Runs an action on the thread of the user interface.</param>
    /// <param name="logger">The application log, or <see langword="null" />.</param>
    public RecentThumbnailLoader(IThumbnailSource source, Action<Action> onOwnerThread, RollingFileLogger? logger)
    {
        _source = source;
        _onOwnerThread = onOwnerThread;
        _logger = logger;
    }

    /// <summary>
    ///     Stops the drawing that is under way, because the cards it was for are replaced.
    /// </summary>
    public void Restart()
    {
        _generation.Cancel();
        _generation.Dispose();
        _generation = new CancellationTokenSource();
    }

    /// <summary>
    ///     Starts drawing the picture of a card.
    /// </summary>
    /// <param name="item">The card.</param>
    /// <param name="picture">The first picture of the project.</param>
    /// <param name="projectFolder">The folder of the project.</param>
    public void Load(RecentProjectItem item, string picture, string projectFolder)
    {
        var generation = _generation;
        _ = LoadAsync(item, picture, projectFolder, generation);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _generation.Cancel();
        _generation.Dispose();
    }

    /// <summary>
    ///     Draws the picture of a card and gives it to the card, unless the cards were replaced meanwhile.
    /// </summary>
    /// <param name="item">The card.</param>
    /// <param name="picture">The first picture of the project.</param>
    /// <param name="projectFolder">The folder of the project.</param>
    /// <param name="generation">The generation of cards that the picture is for.</param>
    /// <returns>A task that completes when the card has its picture or it could not be drawn.</returns>
    private async Task LoadAsync(RecentProjectItem item, string picture, string projectFolder, CancellationTokenSource generation)
    {
        var token = generation.Token;
        Bitmap? bitmap = null;
        try
        {
            bitmap = await _source.CreateAsync(picture, projectFolder, WelcomeViewModel.CardPictureWidth, WelcomeViewModel.CardPictureHeight, token)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // The cards were replaced before the picture was drawn
        }
        catch (Exception ex) when (ex is IOException or InvalidOperationException)
        {
            // A card without a picture says so with an icon; the project opens just the same
            _logger?.Log(LogLevel.Warning, $"The picture of a recent project could not be drawn: {ex.Message}");
        }

        if (bitmap is null)
        {
            return;
        }

        _onOwnerThread(() =>
        {
            if (token.IsCancellationRequested)
            {
                bitmap.Dispose();
            }
            else
            {
                item.SetThumbnail(bitmap);
            }
        });
    }
}
