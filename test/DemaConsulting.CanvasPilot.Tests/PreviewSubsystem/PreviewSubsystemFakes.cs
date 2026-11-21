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

using DemaConsulting.CanvasPilot.PreviewSubsystem;

namespace DemaConsulting.CanvasPilot.Tests;

/// <summary>
///     Hand-written export dialogs for tests: it answers the save picker with a configured path, records the
///     pictures copied and the messages shown, and can imitate a clipboard that refuses.
/// </summary>
public sealed class FakeExportDialogs : IExportDialogs
{
    /// <summary>
    ///     Gets or sets the path the save picker returns, or <see langword="null" /> to imitate canceling.
    /// </summary>
    public string? SaveFileToPick { get; set; }

    /// <summary>
    ///     Gets or sets a value indicating whether the clipboard accepts pictures.
    /// </summary>
    public bool ClipboardAvailable { get; set; } = true;

    /// <summary>
    ///     Gets the pictures that were copied to the clipboard.
    /// </summary>
    public List<RenderedImage> CopiedImages { get; } = [];

    /// <summary>
    ///     Gets the messages shown, as title and message.
    /// </summary>
    public List<(string Title, string Message)> Messages { get; } = [];

    /// <inheritdoc />
    public Task<string?> PickSaveFileAsync(string title, string suggestedFileName, string filterName, string filterPattern)
    {
        return Task.FromResult(SaveFileToPick);
    }

    /// <inheritdoc />
    public Task<bool> CopyImageToClipboardAsync(RenderedImage image)
    {
        CopiedImages.Add(image);
        return Task.FromResult(ClipboardAvailable);
    }

    /// <inheritdoc />
    public Task ShowMessageAsync(string title, string message)
    {
        Messages.Add((title, message));
        return Task.CompletedTask;
    }
}
/// <summary>
///     Hand-written render service for tests: every render waits until the test completes it, so tests control
///     the order in which results arrive and can check that stale renders are cancelled and ignored.
/// </summary>
public sealed class FakeImageRenderService : IImageRenderService
{
    /// <summary>
    ///     Every render that was requested, in order.
    /// </summary>
    private readonly List<Request> _requests = [];

    /// <summary>
    ///     Gets the renders that were requested, in order.
    /// </summary>
    public IReadOnlyList<Request> Requests => _requests;

    /// <summary>
    ///     Gets or sets the exception the next render throws instead of returning a result, to imitate a defective
    ///     service; reset after use.
    /// </summary>
    public Exception? ThrowOnNextRender { get; set; }

    /// <inheritdoc />
    public Task<RenderResult> RenderAsync(string filePath, string projectFolder, CancellationToken cancellationToken)
    {
        return RenderAsync(filePath, projectFolder, null, cancellationToken);
    }

    /// <inheritdoc />
    public Task<RenderResult> RenderAsync(string filePath, string projectFolder, RenderSettings? settings, CancellationToken cancellationToken)
    {
        if (ThrowOnNextRender is { } exception)
        {
            ThrowOnNextRender = null;
            return Task.FromException<RenderResult>(exception);
        }

        var request = new Request(filePath, projectFolder, cancellationToken) { Settings = settings };
        _requests.Add(request);
        return request.Completion.Task;
    }

    /// <inheritdoc />
    public byte[] EncodePng(RenderedImage image)
    {
        return EncodePng(image, null);
    }

    /// <inheritdoc />
    public byte[] EncodePng(RenderedImage image, double? dpi)
    {
        EncodedDpi.Add(dpi);
        return [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, (byte)image.Width, (byte)image.Height];
    }

    /// <summary>
    ///     Gets the resolutions that pictures were encoded with, in order; <see langword="null" /> for none.
    /// </summary>
    public List<double?> EncodedDpi { get; } = [];

    /// <summary>
    ///     One requested render.
    /// </summary>
    /// <param name="FilePath">The file that was to be rendered.</param>
    /// <param name="ProjectFolder">The project folder that was passed.</param>
    /// <param name="Token">The cancellation token that was passed.</param>
    public sealed record Request(string FilePath, string ProjectFolder, CancellationToken Token)
    {
        /// <summary>
        ///     Gets the settings that were passed, or <see langword="null" /> when the file was to be drawn as it is.
        /// </summary>
        public RenderSettings? Settings { get; init; }

        /// <summary>
        ///     Gets the source that completes the render.
        /// </summary>
        public TaskCompletionSource<RenderResult> Completion { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        /// <summary>
        ///     Completes the render with a picture of the given size.
        /// </summary>
        /// <param name="width">The width.</param>
        /// <param name="height">The height.</param>
        /// <param name="diagnostics">The notes to report, or <see langword="null" />.</param>
        public void Succeed(int width = 4, int height = 2, params RenderDiagnostic[] diagnostics)
        {
            Completion.SetResult(RenderResult.Success(TestImages.Create(FilePath, width, height, diagnostics)));
        }

        /// <summary>
        ///     Completes the render with an error.
        /// </summary>
        /// <param name="code">The error code.</param>
        /// <param name="message">The error message.</param>
        /// <param name="hint">The error hint, or <see langword="null" />.</param>
        public void Fail(string code, string message, string? hint = null)
        {
            Completion.SetResult(RenderResult.Failure(new RenderError(code, message, hint)));
        }

        /// <summary>
        ///     Completes the render as cancelled.
        /// </summary>
        public void Cancel()
        {
            Completion.SetResult(RenderResult.Cancelled());
        }
    }
}

/// <summary>
///     Builds small pictures for tests.
/// </summary>
public static class TestImages
{
    /// <summary>
    ///     Creates a picture whose pixels are all the same opaque color.
    /// </summary>
    /// <param name="sourcePath">The path of the file the picture is said to come from.</param>
    /// <param name="width">The width.</param>
    /// <param name="height">The height.</param>
    /// <param name="diagnostics">The notes, or <see langword="null" /> for none.</param>
    /// <returns>The picture.</returns>
    public static RenderedImage Create(string sourcePath, int width, int height, IReadOnlyList<RenderDiagnostic>? diagnostics = null)
    {
        var pixels = new byte[width * height * 4];
        for (var index = 0; index < pixels.Length; index += 4)
        {
            pixels[index] = 10;
            pixels[index + 1] = 20;
            pixels[index + 2] = 30;
            pixels[index + 3] = 255;
        }

        return new RenderedImage(sourcePath, "svg", width, height, pixels, diagnostics);
    }
}

/// <summary>
///     Hand-written source of the shown picture for tests.
/// </summary>
public sealed class FakeRenderedImageSource : IRenderedImageSource
{
    /// <summary>
    ///     The picture.
    /// </summary>
    private RenderedImage? _image;

    /// <inheritdoc />
    public event EventHandler? CurrentImageChanged;

    /// <inheritdoc />
    public RenderedImage? CurrentImage
    {
        get => _image;
        set
        {
            _image = value;
            CurrentImageChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <inheritdoc />
    public string? ProjectFolder { get; set; }
}
