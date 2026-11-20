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
using NSubstitute;

namespace DemaConsulting.CanvasPilot.Tests;

/// <summary>
///     Tests for the <see cref="ImageExporter" /> unit with a fake picture source, the real PNG encoder, a fake
///     export dialogs, and a temporary folder.
/// </summary>
public sealed class ImageExporterTests : IDisposable
{
    /// <summary>
    ///     The PNG file signature.
    /// </summary>
    private static readonly byte[] PngSignature = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];

    /// <summary>
    ///     Temporary folder removed on disposal.
    /// </summary>
    private readonly string _folder = Directory.CreateTempSubdirectory("canvaspilot-export-tests-").FullName;

    /// <summary>
    ///     The source of the shown picture.
    /// </summary>
    private readonly FakeRenderedImageSource _source = new();

    /// <summary>
    ///     The export dialogs that answer the save picker.
    /// </summary>
    private readonly FakeExportDialogs _dialogs = new();

    /// <inheritdoc />
    public void Dispose()
    {
        if (Directory.Exists(_folder))
        {
            Directory.Delete(_folder, recursive: true);
        }
    }

    /// <summary>
    ///     Test that export is possible only while a picture is shown, and that listeners are told when that changes.
    /// </summary>
    [Fact]
    public void ImageExporter_CanExport_FollowsShownPicture_AndRaisesEvent()
    {
        // Arrange: an exporter with no picture and a handler that counts events
        var exporter = new ImageExporter(_source, new ImageRenderService());
        var raised = 0;
        exporter.CanExportChanged += (_, _) => raised++;
        var initially = exporter.CanExport;

        // Act: a picture appears and then goes away
        _source.CurrentImage = TestImages.Create("a.svg", 2, 2);
        var withPicture = exporter.CanExport;
        _source.CurrentImage = null;

        // Assert: export was possible only with a picture and each change was announced
        Assert.False(initially);
        Assert.True(withPicture);
        Assert.False(exporter.CanExport);
        Assert.Equal(2, raised);
    }

    /// <summary>
    ///     Test that exporting writes a valid PNG of the shown picture to the file the user chose, and suggests the
    ///     source file's name with a .png extension.
    /// </summary>
    [Fact]
    public async Task ImageExporter_ExportPngAsync_PictureShown_WritesValidPngToChosenFile()
    {
        // Arrange: a shown picture and a user who chooses a file
        var target = Path.Combine(_folder, "out.png");
        var pixels = new byte[4 * 3 * 4];
        Array.Fill(pixels, (byte)200);
        _source.CurrentImage = new RenderedImage(Path.Combine(_folder, "cover.svg"), "svg", 4, 3, pixels);
        _dialogs.SaveFileToPick = target;
        var exporter = new ImageExporter(_source, new ImageRenderService());

        // Act: export
        await exporter.ExportPngAsync(_dialogs);

        // Assert: the file starts with the PNG signature and has content beyond it, and no message was shown
        var bytes = await File.ReadAllBytesAsync(target, TestContext.Current.CancellationToken);
        Assert.Equal(PngSignature, bytes[..8]);
        Assert.True(bytes.Length > 40);
        Assert.Empty(_dialogs.Messages);
    }

    /// <summary>
    ///     Test that a chosen name without the .png extension gets it, so the exported file is a PNG by name too.
    /// </summary>
    [Fact]
    public async Task ImageExporter_ExportPngAsync_NameWithoutExtension_AppendsPng()
    {
        // Arrange: a shown picture and a chosen name that has no extension
        _source.CurrentImage = TestImages.Create(Path.Combine(_folder, "cover.svg"), 2, 2);
        _dialogs.SaveFileToPick = Path.Combine(_folder, "chosen");
        var exporter = new ImageExporter(_source, new ImageRenderService());

        // Act: export
        await exporter.ExportPngAsync(_dialogs);

        // Assert: the file with the extension exists
        Assert.True(File.Exists(Path.Combine(_folder, "chosen.png")));
        Assert.False(File.Exists(Path.Combine(_folder, "chosen")));
    }

    /// <summary>
    ///     Test that canceling the save dialog writes nothing, and that exporting with no picture does nothing.
    /// </summary>
    [Fact]
    public async Task ImageExporter_ExportPngAsync_CancelledOrNoPicture_WritesNothing()
    {
        // Arrange: an exporter with no picture, then one with a picture and a cancelled dialog
        var exporter = new ImageExporter(_source, new ImageRenderService());
        _dialogs.SaveFileToPick = Path.Combine(_folder, "never.png");

        // Act: export without a picture, then with a picture and a cancelled dialog
        await exporter.ExportPngAsync(_dialogs);
        _source.CurrentImage = TestImages.Create("a.svg", 2, 2);
        _dialogs.SaveFileToPick = null;
        await exporter.ExportPngAsync(_dialogs);

        // Assert: no file exists and nothing was reported
        Assert.Empty(Directory.GetFiles(_folder));
        Assert.Empty(_dialogs.Messages);
    }

    /// <summary>
    ///     Test that a file that cannot be written is reported to the user instead of failing.
    /// </summary>
    [Fact]
    public async Task ImageExporter_ExportPngAsync_UnwritableTarget_ShowsMessage()
    {
        // Arrange: a picture and a target in a folder that does not exist
        _source.CurrentImage = TestImages.Create("a.svg", 2, 2);
        _dialogs.SaveFileToPick = Path.Combine(_folder, "missing-folder", "out.png");
        var exporter = new ImageExporter(_source, new ImageRenderService());

        // Act: export
        await exporter.ExportPngAsync(_dialogs);

        // Assert: the user was told that the image could not be saved
        var message = Assert.Single(_dialogs.Messages);
        Assert.Equal("Export as PNG", message.Title);
        Assert.StartsWith("The image could not be saved:", message.Message, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Test the file name suggested for the save dialog.
    /// </summary>
    [Theory]
    [InlineData("cover.svg", "cover.png")]
    [InlineData("diagram.mmd", "diagram.png")]
    [InlineData("chart.vl.json", "chart.png")]
    [InlineData("CHART.VL.JSON", "CHART.png")]
    [InlineData("my.chart.svg", "my.chart.png")]
    [InlineData(".svg", "image.png")]
    public void ImageExporter_SuggestFileName_SourcePath_ReplacesImageExtensionWithPng(string source, string expected)
    {
        // Act / Assert: the suggestion is the file's stem with .png
        Assert.Equal(expected, ImageExporter.SuggestFileName(source));
    }

    /// <summary>
    ///     Test that copying hands the shown picture to the clipboard and says nothing when it works.
    /// </summary>
    [Fact]
    public async Task ImageExporter_CopyImageAsync_PictureShown_CopiesPictureToClipboard()
    {
        // Arrange: a shown picture and a working clipboard
        var picture = TestImages.Create("a.svg", 2, 2);
        _source.CurrentImage = picture;
        var exporter = new ImageExporter(_source, new ImageRenderService());

        // Act: copy
        await exporter.CopyImageAsync(_dialogs);

        // Assert: the picture was copied and nothing was reported
        Assert.Same(picture, Assert.Single(_dialogs.CopiedImages));
        Assert.Empty(_dialogs.Messages);
    }

    /// <summary>
    ///     Test that a clipboard that cannot be used is reported to the user, and that copying with no picture
    ///     does nothing.
    /// </summary>
    [Fact]
    public async Task ImageExporter_CopyImageAsync_ClipboardUnavailableOrNoPicture_ShowsMessageOrDoesNothing()
    {
        // Arrange: an exporter and a clipboard that refuses
        var exporter = new ImageExporter(_source, new ImageRenderService());
        _dialogs.ClipboardAvailable = false;

        // Act: copy with no picture, then with a picture
        await exporter.CopyImageAsync(_dialogs);
        var afterNoPicture = _dialogs.CopiedImages.Count + _dialogs.Messages.Count;
        _source.CurrentImage = TestImages.Create("a.svg", 2, 2);
        await exporter.CopyImageAsync(_dialogs);

        // Assert: nothing happened without a picture, and the refusal was reported
        Assert.Equal(0, afterNoPicture);
        var message = Assert.Single(_dialogs.Messages);
        Assert.Equal("Copy Image", message.Title);
        Assert.Contains("clipboard", message.Message, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Test that the exporter needs only the export dialogs of the Preview subsystem: it exports and copies
    ///     through a substitute that implements nothing of the application's dialog service.
    /// </summary>
    [Fact]
    public async Task ImageExporter_ExportAndCopy_UsingExportDialogsOnly_DoNotRequireDialogService()
    {
        // Arrange: a shown picture and a substitute for the export dialogs that picks a target and accepts the clipboard
        var target = Path.Combine(_folder, "only-export-dialogs.png");
        _source.CurrentImage = TestImages.Create(Path.Combine(_folder, "cover.svg"), 2, 2);
        var dialogs = Substitute.For<IExportDialogs>();
        dialogs.PickSaveFileAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>())
            .Returns(Task.FromResult<string?>(target));
        dialogs.CopyImageToClipboardAsync(Arg.Any<RenderedImage>()).Returns(Task.FromResult(true));
        var exporter = new ImageExporter(_source, new ImageRenderService());

        // Act: export and copy through the substitute
        await exporter.ExportPngAsync(dialogs);
        await exporter.CopyImageAsync(dialogs);

        // Assert: the file was written, the picture was copied and no message was shown
        Assert.True(File.Exists(target));
        await dialogs.Received(1).CopyImageToClipboardAsync(_source.CurrentImage);
        await dialogs.DidNotReceive().ShowMessageAsync(Arg.Any<string>(), Arg.Any<string>());
    }
    /// <summary>
    ///     Test that the constructor and methods reject missing arguments.
    /// </summary>
    [Fact]
    public async Task ImageExporter_InvalidArguments_Throw()
    {
        // Arrange: an exporter
        var exporter = new ImageExporter(_source, new ImageRenderService());

        // Act / Assert: nulls and blanks are refused
        Assert.Throws<ArgumentNullException>(() => new ImageExporter(null!, new ImageRenderService()));
        Assert.Throws<ArgumentNullException>(() => new ImageExporter(_source, null!));
        Assert.Throws<ArgumentException>(() => ImageExporter.SuggestFileName(" "));
        await Assert.ThrowsAsync<ArgumentNullException>(() => exporter.ExportPngAsync(null!));
        await Assert.ThrowsAsync<ArgumentNullException>(() => exporter.CopyImageAsync(null!));
    }
}
