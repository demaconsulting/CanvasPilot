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
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Platform.Storage;
using DemaConsulting.CanvasPilot.ComposerSubsystem;

namespace DemaConsulting.CanvasPilot.UiTests;

/// <summary>
///     Headless UI tests for the parts of the composer editor that carry data and drawing: the reader of drops and the
///     clipboard, the preview provider of picture chips, the chip control, the dictated-text ranges, and the rules for
///     refused characters and for text copied to the clipboard.
/// </summary>
public sealed class ComposerEditorSupportTests : IDisposable
{
    /// <summary>
    ///     The first eight bytes of every PNG file.
    /// </summary>
    private static readonly byte[] PngSignature = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];

    /// <summary>
    ///     Temporary folder for the files of a test, removed on disposal.
    /// </summary>
    private readonly string _folder = Directory.CreateTempSubdirectory("canvaspilot-ui-composer-support-").FullName;

    /// <inheritdoc />
    public void Dispose()
    {
        if (Directory.Exists(_folder))
        {
            Directory.Delete(_folder, recursive: true);
        }
    }

    /// <summary>
    ///     Test that a drop of a file and a folder is read as their local paths, in order, and nothing else.
    /// </summary>
    [AvaloniaFact]
    public void DataTransferReader_Read_FilesAndFolder_ReturnsLocalPaths()
    {
        // Arrange: a window for its storage provider, a file, a folder, and a transfer with both
        var window = new Window();
        var file = WriteFile("a.txt");
        var folder = Directory.CreateDirectory(Path.Combine(_folder, "folder")).FullName;
        var data = new DataTransfer();
        foreach (IStorageItem item in new IStorageItem[]
                 {
                     window.StorageProvider.TryGetFileFromPathAsync(new Uri(file)).GetAwaiter().GetResult()!,
                     window.StorageProvider.TryGetFolderFromPathAsync(new Uri(folder)).GetAwaiter().GetResult()!,
                 })
        {
            var entry = new DataTransferItem();
            entry.SetFile(item);
            data.Add(entry);
        }

        // Act: read the drop
        var content = DataTransferReader.Read(data);

        // Assert: the two paths, no picture, no text
        Assert.Equal([file, folder], content.Paths.Select(path => path.TrimEnd(Path.DirectorySeparatorChar)));
        Assert.Empty(content.Images);
        Assert.Null(content.Text);
    }

    /// <summary>
    ///     Test that files win over the text that comes with them, and a picture wins over text.
    /// </summary>
    [AvaloniaFact]
    public void DataTransferReader_Read_FilesWithText_OnlyFilesAreTaken()
    {
        // Arrange: one item with a file and its path as text
        var window = new Window();
        var file = WriteFile("both.txt");
        var entry = new DataTransferItem();
        entry.SetFile(window.StorageProvider.TryGetFileFromPathAsync(new Uri(file)).GetAwaiter().GetResult()!);
        entry.SetText(file);
        var data = new DataTransfer();
        data.Add(entry);

        // Act: read the drop
        var content = DataTransferReader.Read(data);

        // Assert: the file, and no text
        Assert.Equal([file], content.Paths);
        Assert.Null(content.Text);
    }

    /// <summary>
    ///     Test that a dropped bitmap is read as PNG bytes of the same size, and not as text.
    /// </summary>
    [AvaloniaFact]
    public void DataTransferReader_Read_Bitmap_ReturnsPngBytes()
    {
        // Arrange: a transfer with a bitmap of 6 x 4 pixels
        var data = ComposerHarness.BitmapTransfer();

        // Act: read the drop
        var content = DataTransferReader.Read(data);

        // Assert: one PNG of the same size
        var image = Assert.Single(content.Images);
        Assert.Equal(PngSignature, image.ToArray().Take(8));
        using var decoded = new Bitmap(new MemoryStream(image.ToArray()));
        Assert.Equal(new PixelSize(6, 4), decoded.PixelSize);
        Assert.Empty(content.Paths);
        Assert.Null(content.Text);
    }

    /// <summary>
    ///     Test that dropped text is read as text, and that an empty transfer gives empty content.
    /// </summary>
    [AvaloniaFact]
    public void DataTransferReader_Read_TextAndEmpty_ReturnsTextOrNothing()
    {
        // Arrange: a transfer with text, and one with nothing
        var withText = ComposerHarness.TextTransfer("some words");
        var empty = new DataTransfer();

        // Act: read both
        var text = DataTransferReader.Read(withText);
        var nothing = DataTransferReader.Read(empty);

        // Assert: the words, then nothing
        Assert.Equal("some words", text.Text);
        Assert.Null(nothing.Text);
        Assert.Empty(nothing.Paths);
        Assert.Empty(nothing.Images);
    }

    /// <summary>
    ///     Test that a drag is acceptable when it brings files, a picture, or text, and not when it brings nothing.
    /// </summary>
    [AvaloniaFact]
    public void DataTransferReader_IsAcceptable_FilesPictureTextAndNothing()
    {
        // Arrange: transfers of each kind
        var window = new Window();
        var file = new DataTransferItem();
        file.SetFile(window.StorageProvider.TryGetFileFromPathAsync(new Uri(WriteFile("x.txt"))).GetAwaiter().GetResult()!);
        var files = new DataTransfer();
        files.Add(file);

        // Act and assert: each kind is acceptable and an empty transfer is not
        Assert.True(DataTransferReader.IsAcceptable(files));
        Assert.True(DataTransferReader.IsAcceptable(ComposerHarness.BitmapTransfer()));
        Assert.True(DataTransferReader.IsAcceptable(ComposerHarness.TextTransfer("t")));
        Assert.False(DataTransferReader.IsAcceptable(new DataTransfer()));
    }

    /// <summary>
    ///     Test that the reader refuses missing arguments.
    /// </summary>
    [AvaloniaFact]
    public async Task DataTransferReader_NullArguments_Throw()
    {
        // Act and assert: each entry point refuses null
        Assert.Throws<ArgumentNullException>(() => DataTransferReader.Read(null!));
        Assert.Throws<ArgumentNullException>(() => DataTransferReader.IsAcceptable(null!));
        await Assert.ThrowsAsync<ArgumentNullException>(() => DataTransferReader.ReadAsync(null!));
    }

    /// <summary>
    ///     Test that the clipboard is read as files first, then a picture, then text, and as nothing when it is empty.
    /// </summary>
    [AvaloniaFact]
    public async Task DataTransferReader_ReadAsync_Clipboard_FilesPictureTextAndEmpty()
    {
        // Arrange: the clipboard of a window, which starts empty
        var window = new Window();
        window.Show();
        var clipboard = window.Clipboard!;
        await clipboard.ClearAsync();
        var empty = await DataTransferReader.ReadAsync(clipboard);

        // Act: read text, a picture, and a file in turn
        await clipboard.SetTextAsync("clip text");
        var text = await DataTransferReader.ReadAsync(clipboard);
        using var bitmap = new WriteableBitmap(new PixelSize(3, 2), new Vector(96, 96), PixelFormat.Bgra8888, AlphaFormat.Premul);
        await clipboard.SetBitmapAsync(bitmap);
        var picture = await DataTransferReader.ReadAsync(clipboard);
        var path = WriteFile("clip.txt");
        await clipboard.SetFileAsync((await window.StorageProvider.TryGetFileFromPathAsync(new Uri(path)))!);
        var files = await DataTransferReader.ReadAsync(clipboard);

        // Assert: each read gave the one kind that was there
        Assert.Empty(empty.Paths);
        Assert.Null(empty.Text);
        Assert.Equal("clip text", text.Text);
        Assert.Single(picture.Images);
        Assert.Equal([path], files.Paths);
        window.Close();
    }

    /// <summary>
    ///     Test that a picture file gets a preview, that the same unchanged file gets the same preview, and that a
    ///     changed file gets a new one.
    /// </summary>
    [AvaloniaFact]
    public async Task ImageChipThumbnailProvider_GetThumbnailAsync_PictureFile_ReturnsCachedPreviewUntilChanged()
    {
        // Arrange: a PNG file and a provider
        using var provider = new ImageChipThumbnailProvider();
        var path = Path.Combine(_folder, "pic.png");
        await File.WriteAllBytesAsync(path, MakePng(8, 8));
        var chip = new ImageChip(path);

        // Act: ask twice, change the file, ask again
        var first = await provider.GetThumbnailAsync(chip, CancellationToken.None);
        var second = await provider.GetThumbnailAsync(chip, CancellationToken.None);
        await File.WriteAllBytesAsync(path, MakePng(16, 16));
        File.SetLastWriteTimeUtc(path, DateTime.UtcNow.AddMinutes(1));
        var third = await provider.GetThumbnailAsync(chip, CancellationToken.None);

        // Assert: a preview, the same one again, and a different one for the new file
        Assert.NotNull(first);
        Assert.Same(first, second);
        Assert.NotNull(third);
        Assert.NotSame(first, third);
    }

    /// <summary>
    ///     Test that files that are not pictures the decoder knows, missing files, and files that are too large have no
    ///     preview and do not fail.
    /// </summary>
    [AvaloniaFact]
    public async Task ImageChipThumbnailProvider_GetThumbnailAsync_NotAPictureMissingOrTooLarge_ReturnsNull()
    {
        // Arrange: a text file with a picture name, a missing file, and a file larger than the limit
        using var provider = new ImageChipThumbnailProvider();
        var notPicture = WriteFile("fake.png");
        var missing = Path.Combine(_folder, "missing.png");
        var large = Path.Combine(_folder, "large.png");
        using (var stream = new FileStream(large, FileMode.Create))
        {
            stream.SetLength(ImageChipThumbnailProvider.MaxFileBytes + 1);
        }

        // Act: ask for each
        var fake = await provider.GetThumbnailAsync(new ImageChip(notPicture), CancellationToken.None);
        var gone = await provider.GetThumbnailAsync(new ImageChip(missing), CancellationToken.None);
        var big = await provider.GetThumbnailAsync(new ImageChip(large), CancellationToken.None);

        // Assert: none has a preview
        Assert.Null(fake);
        Assert.Null(gone);
        Assert.Null(big);
    }

    /// <summary>
    ///     Test that only picture chips get a preview, that a cancelled request gets none, and that a missing chip is
    ///     refused.
    /// </summary>
    [AvaloniaFact]
    public async Task ImageChipThumbnailProvider_GetThumbnailAsync_OtherChipsAndCancellation_ReturnNull()
    {
        // Arrange: a provider, a real PNG, and a token that is already cancelled
        using var provider = new ImageChipThumbnailProvider();
        var path = Path.Combine(_folder, "ok.png");
        await File.WriteAllBytesAsync(path, MakePng(4, 4));
        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync();

        // Act: ask for a file chip, a folder chip, a mark chip, and a picture chip with the cancelled token
        var file = await provider.GetThumbnailAsync(new FileChip(path), CancellationToken.None);
        var folder = await provider.GetThumbnailAsync(new FolderChip(_folder), CancellationToken.None);
        var mark = await provider.GetThumbnailAsync(new MarkChip(Guid.NewGuid(), 1), CancellationToken.None);
        var late = await provider.GetThumbnailAsync(new ImageChip(path), cancelled.Token);

        // Assert: none has a preview, and a missing chip is refused
        Assert.Null(file);
        Assert.Null(folder);
        Assert.Null(mark);
        Assert.Null(late);
        await Assert.ThrowsAsync<ArgumentNullException>(() => provider.GetThumbnailAsync(null!, CancellationToken.None));
    }

    /// <summary>
    ///     Test that the chip control shows the name, the icon, and the tooltip of each kind of chip, and a picture
    ///     label for a mark.
    /// </summary>
    [AvaloniaFact]
    public void ChipView_Show_EachKindOfChip_ShowsLabelIconAndTooltip()
    {
        // Arrange: one chip of each kind
        var path = Path.Combine(_folder, "dir", "name.txt");
        var mark = new MarkChip(Guid.NewGuid(), 3);
        ChipSegment[] chips = [new FileChip(path), new FolderChip(Path.Combine(_folder, "dir")), new ImageChip(path), mark];

        // Act: show each in a control
        var views = chips.Select(chip =>
        {
            var view = new ChipView();
            view.Show(chip, null);
            return view;
        }).ToList();

        // Assert: labels, tooltips, and distinct icon shapes
        Assert.Equal(["name.txt", "dir", "name.txt", "Mark 3"], views.Select(view => view.FindControl<TextBlock>("LabelText")!.Text));
        Assert.Equal(path, ToolTip.GetTip(views[0]));
        Assert.Equal("Mark 3 on the picture", ToolTip.GetTip(views[3]));
        Assert.Equal(4, views.Select(view => view.FindControl<Avalonia.Controls.Shapes.Path>("IconPath")!.Data!.Bounds.ToString()).Distinct().Count());
        Assert.Same(chips[2], views[2].Chip);
    }

    /// <summary>
    ///     Test that the delete button of a chip control raises the delete request, and the label helper names every
    ///     kind of chip and refuses a missing one.
    /// </summary>
    [AvaloniaFact]
    public void ChipView_DeleteButton_RaisesDeleteRequested()
    {
        // Arrange: a chip control that counts delete requests
        var view = new ChipView();
        view.Show(new FileChip(Path.Combine(_folder, "d.txt")), null);
        var requests = 0;
        view.DeleteRequested += (_, _) => requests++;

        // Act: press the delete button
        view.FindControl<Button>("DeleteButton")!.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

        // Assert: one request, and the helper behaves
        Assert.Equal(1, requests);
        Assert.Equal("d.txt", ChipView.GetLabel(new FileChip(Path.Combine(_folder, "d.txt"))));
        Assert.Throws<ArgumentNullException>(() => ChipView.GetLabel(null!));
        Assert.Throws<ArgumentNullException>(() => view.Show(null!, null));
    }

    /// <summary>
    ///     Test that a provider that fails leaves the icon on the chip and does not break the control.
    /// </summary>
    [AvaloniaFact]
    public void ChipView_ProviderThrows_KeepsIcon()
    {
        // Arrange: a chip with a provider that throws, in a window so that it is attached
        var view = new ChipView();
        view.Show(new FileChip(Path.Combine(_folder, "e.txt")), new ThrowingProvider());
        var window = new Window { Content = view };

        // Act: show the window, which makes the chip ask for its picture
        window.Show();
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        // Assert: the icon is still shown and no picture is
        Assert.True(view.FindControl<Avalonia.Controls.Shapes.Path>("IconPath")!.IsVisible);
        Assert.False(view.FindControl<Image>("ThumbnailImage")!.IsVisible);
        window.Close();
    }

    /// <summary>
    ///     Test that the dictated ranges are the dictated text only, with chips and typed text between them left out, and
    ///     that the renderer refuses missing arguments.
    /// </summary>
    [AvaloniaFact]
    public void DictatedTextBackgroundRenderer_GetRanges_DictatedTextBetweenChipsAndTyped_ReturnsOnlyDictated()
    {
        // Arrange: typed "ab", a chip, dictated "cde", typed "f", dictated "gh"
        var document = new ComposerDocument();
        document.InsertText(0, "ab", TextOrigin.Typed);
        document.InsertChip(2, new FileChip(Path.Combine(_folder, "c.txt")));
        document.InsertText(3, "cde", TextOrigin.Dictated);
        document.InsertText(6, "f", TextOrigin.Typed);
        document.InsertText(7, "gh", TextOrigin.Dictated);

        // Act: find the ranges
        var ranges = DictatedTextBackgroundRenderer.GetRanges(document);

        // Assert: the two dictated stretches, as offsets that count the chip as one position
        Assert.Equal([new DictatedRange(3, 3), new DictatedRange(7, 2)], ranges);
        Assert.Throws<ArgumentNullException>(() => DictatedTextBackgroundRenderer.GetRanges(null!));
        Assert.Throws<ArgumentNullException>(() => new DictatedTextBackgroundRenderer(null!));
    }

    /// <summary>
    ///     Test the rule for characters that must not be typed: the placeholder and control characters other than tab and
    ///     line breaks.
    /// </summary>
    [Fact]
    public void ComposerEditorView_ContainsForbiddenCharacter_Table_FollowsRule()
    {
        // Arrange: texts that are fine and texts that are not
        string[] allowed = ["hello", "tab\there", "line\r\nbreak", string.Empty];
        string[] refused = ["a\uFFFCb", "bell\u0007", "\uFFFC"];

        // Act and assert: the rule accepts the first and refuses the second
        Assert.All(allowed, text => Assert.False(ComposerEditorView.ContainsForbiddenCharacter(text)));
        Assert.All(refused, text => Assert.True(ComposerEditorView.ContainsForbiddenCharacter(text)));
    }

    /// <summary>
    ///     Test that text for the clipboard shows each chip as its name in brackets, never as the placeholder character.
    /// </summary>
    [AvaloniaFact]
    public void ComposerEditorView_DescribeForClipboard_TextAndChips_ReadableWithoutPlaceholder()
    {
        // Arrange: text, a file chip, a mark chip, and text
        ComposerSegment[] segments =
        [
            new TextSegment("see ", TextOrigin.Typed),
            new FileChip(Path.Combine(_folder, "plan.docx")),
            new MarkChip(Guid.NewGuid(), 2),
            new TextSegment(" now", TextOrigin.Dictated),
        ];

        // Act: describe them
        var text = ComposerEditorView.DescribeForClipboard(segments);

        // Assert: readable names, and no placeholder
        Assert.Equal("see [plan.docx][Mark 2] now", text);
        Assert.DoesNotContain(ComposerSnapshot.ChipPlaceholder, text);
        Assert.Throws<ArgumentNullException>(() => ComposerEditorView.DescribeForClipboard(null!));
        Assert.Throws<ArgumentNullException>(() => ComposerEditorView.ContainsForbiddenCharacter(null!));
    }

    /// <summary>
    ///     Writes a small text file in the test folder.
    /// </summary>
    /// <param name="name">The name of the file.</param>
    /// <returns>The full path of the file.</returns>
    private string WriteFile(string name)
    {
        var path = Path.Combine(_folder, name);
        File.WriteAllText(path, "content of " + name);
        return path;
    }

    /// <summary>
    ///     Makes the bytes of a PNG picture of a size.
    /// </summary>
    /// <param name="width">The width in pixels.</param>
    /// <param name="height">The height in pixels.</param>
    /// <returns>The PNG bytes.</returns>
    private static byte[] MakePng(int width, int height)
    {
        using var bitmap = new WriteableBitmap(new PixelSize(width, height), new Vector(96, 96), PixelFormat.Bgra8888, AlphaFormat.Premul);
        using var stream = new MemoryStream();
        bitmap.Save(stream, new PngBitmapEncoderOptions());
        return stream.ToArray();
    }

    /// <summary>
    ///     A provider of chip pictures that fails, which a chip must survive.
    /// </summary>
    private sealed class ThrowingProvider : IChipThumbnailProvider
    {
        /// <inheritdoc />
        public Task<Bitmap?> GetThumbnailAsync(ChipSegment chip, CancellationToken cancellationToken) =>
            throw new InvalidOperationException("The provider failed.");
    }
}
