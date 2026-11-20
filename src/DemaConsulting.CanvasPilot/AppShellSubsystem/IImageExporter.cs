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

namespace DemaConsulting.CanvasPilot.AppShellSubsystem;

/// <summary>
///     The extension point behind the Image menu. The shell enables Export as PNG and Copy Image only while
///     <see cref="CanExport" /> is true and runs them through this interface, so the unit that owns the
///     rendered image (the image preview) can plug in without the shell knowing about rendering.
/// </summary>
public interface IImageExporter
{
    /// <summary>
    ///     Raised after <see cref="CanExport" /> changed.
    /// </summary>
    event EventHandler? CanExportChanged;

    /// <summary>
    ///     Gets a value indicating whether there is a rendered image that can be exported or copied.
    /// </summary>
    bool CanExport { get; }

    /// <summary>
    ///     Saves the rendered image as a PNG file, asking the user where to save it.
    /// </summary>
    /// <param name="dialogs">The dialog service to ask the user with and to report problems through.</param>
    /// <returns>A task that completes when the export has finished or was cancelled.</returns>
    Task ExportPngAsync(IDialogService dialogs);

    /// <summary>
    ///     Copies the rendered image to the clipboard.
    /// </summary>
    /// <param name="dialogs">The dialog service to report problems through.</param>
    /// <returns>A task that completes when the copy has finished.</returns>
    Task CopyImageAsync(IDialogService dialogs);
}

/// <summary>
///     The exporter used while no image can be exported: it is never enabled and does nothing.
/// </summary>
public sealed class NullImageExporter : IImageExporter
{
    /// <inheritdoc />
    /// <remarks>Never raised, because <see cref="CanExport" /> never changes.</remarks>
    public event EventHandler? CanExportChanged
    {
        add => _ = value;
        remove => _ = value;
    }

    /// <inheritdoc />
    public bool CanExport => false;

    /// <inheritdoc />
    public Task ExportPngAsync(IDialogService dialogs)
    {
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task CopyImageAsync(IDialogService dialogs)
    {
        return Task.CompletedTask;
    }
}
