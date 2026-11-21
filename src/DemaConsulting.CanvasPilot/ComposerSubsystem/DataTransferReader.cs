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

using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Media.Imaging;
using Avalonia.Platform.Storage;

namespace DemaConsulting.CanvasPilot.ComposerSubsystem;

/// <summary>
///     Reads what a drag or the clipboard brought and turns it into <see cref="DroppedContent" />, which is all the
///     composer knows about drops and pastes: the paths of files and folders, the PNG bytes of a picture, and plain
///     text.
/// </summary>
/// <remarks>
///     <para>
///         <b>One kind wins.</b> Files and folders are taken first, then a picture, then text. Programs that offer files
///         also offer their paths as text, and programs that offer a picture can offer text about it, so taking the most
///         specific kind keeps one drop from becoming a chip and also a line of text.
///     </para>
///     <para>
///         <b>Data only.</b> The reader asks for the paths of the dropped items and for the bytes of a picture and never
///         opens, runs, or reads the files. A picture is encoded as PNG so that what is kept does not depend on the
///         format of the program that offered it. Items that are not on the local computer have no path and are left
///         out. The methods never throw for what a program offers; unreadable content is reported as empty.
///     </para>
/// </remarks>
public static class DataTransferReader
{
    /// <summary>
    ///     Gets a value indicating whether the data of a drag has anything the composer accepts: files, a picture, or
    ///     text.
    /// </summary>
    /// <param name="data">The data of the drag.</param>
    /// <returns><see langword="true" /> when the drag brought files, a picture, or text.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="data" /> is <see langword="null" />.</exception>
    public static bool IsAcceptable(IDataTransfer data)
    {
        ArgumentNullException.ThrowIfNull(data);
        return data.Contains(DataFormat.File) || data.Contains(DataFormat.Bitmap) || data.Contains(DataFormat.Text);
    }

    /// <summary>
    ///     Reads the data of a drop.
    /// </summary>
    /// <param name="data">The data of the drop.</param>
    /// <returns>What the drop brought, empty when nothing could be read.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="data" /> is <see langword="null" />.</exception>
    public static DroppedContent Read(IDataTransfer data)
    {
        ArgumentNullException.ThrowIfNull(data);

        // Take the most specific kind that is there, and never fail for what a program offered
        try
        {
            var files = data.TryGetFiles();
            if (files is { Length: > 0 })
            {
                return FromFiles(files);
            }

            using var bitmap = data.Contains(DataFormat.Bitmap) ? data.TryGetBitmap() : null;
            if (bitmap is not null)
            {
                return FromBitmap(bitmap);
            }

            return FromText(data.TryGetText());
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            return new DroppedContent();
        }
    }

    /// <summary>
    ///     Reads the clipboard.
    /// </summary>
    /// <param name="clipboard">The clipboard of the window.</param>
    /// <returns>A task with what the clipboard holds, empty when it holds nothing the composer accepts or cannot be read.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="clipboard" /> is <see langword="null" />.</exception>
    public static async Task<DroppedContent> ReadAsync(IClipboard clipboard)
    {
        ArgumentNullException.ThrowIfNull(clipboard);

        // Take the most specific kind that is there, and never fail for what a program put on the clipboard
        try
        {
            using var data = await clipboard.TryGetDataAsync();
            if (data is null)
            {
                return new DroppedContent();
            }

            var files = await data.TryGetFilesAsync();
            if (files is { Length: > 0 })
            {
                return FromFiles(files);
            }

            using var bitmap = data.Contains(DataFormat.Bitmap) ? await data.TryGetBitmapAsync() : null;
            if (bitmap is not null)
            {
                return FromBitmap(bitmap);
            }

            return FromText(await data.TryGetTextAsync());
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            return new DroppedContent();
        }
    }

    /// <summary>
    ///     Makes the content of dropped files and folders, which is only where they are.
    /// </summary>
    /// <param name="items">The dropped items.</param>
    /// <returns>The paths of the items that are on the local computer, in order.</returns>
    private static DroppedContent FromFiles(IEnumerable<IStorageItem> items)
    {
        return new DroppedContent
        {
            Paths = [.. items.Select(item => item.TryGetLocalPath()).OfType<string>()],
        };
    }

    /// <summary>
    ///     Makes the content of a dropped or pasted picture, as PNG bytes.
    /// </summary>
    /// <param name="bitmap">The picture.</param>
    /// <returns>The content, empty when the picture could not be encoded.</returns>
    private static DroppedContent FromBitmap(Bitmap bitmap)
    {
        using var stream = new MemoryStream();
        bitmap.Save(stream, new PngBitmapEncoderOptions());
        return new DroppedContent { Images = [stream.ToArray()] };
    }

    /// <summary>
    ///     Makes the content of dropped or pasted text.
    /// </summary>
    /// <param name="text">The text, or <see langword="null" /> when there is none.</param>
    /// <returns>The content.</returns>
    private static DroppedContent FromText(string? text)
    {
        return new DroppedContent { Text = string.IsNullOrEmpty(text) ? null : text };
    }
}
