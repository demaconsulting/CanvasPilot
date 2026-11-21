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

using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.CanvasNet.Codecs;
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

    /// <summary>
    ///     Test that an exporter with no source of sizes, or a source with none, never asks about sizes and writes the
    ///     picture as it is shown, as it did before sizes existed.
    /// </summary>
    [Fact]
    public async Task ImageExporter_ExportPngAsync_NoPresetSourceOrNoPresets_DoesNotAskAndExportsAsShown()
    {
        // Arrange: a shown picture, a chosen file, and two exporters: without a source and with an empty one
        _source.CurrentImage = TestImages.Create(Path.Combine(_folder, "cover.svg"), 2, 2);
        var withoutSource = new ImageExporter(_source, new ImageRenderService());
        var withEmptySource = new ImageExporter(_source, new ImageRenderService(), null, new FakePresetSource());
        _dialogs.SaveFileToPick = Path.Combine(_folder, "one.png");

        // Act: export with each
        await withoutSource.ExportPngAsync(_dialogs);
        _dialogs.SaveFileToPick = Path.Combine(_folder, "two.png");
        await withEmptySource.ExportPngAsync(_dialogs);

        // Assert: both files exist, the user was never asked about sizes, and the files are the same
        Assert.Empty(_dialogs.PresetOffers);
        Assert.Equal(
            await File.ReadAllBytesAsync(Path.Combine(_folder, "one.png"), TestContext.Current.CancellationToken),
            await File.ReadAllBytesAsync(Path.Combine(_folder, "two.png"), TestContext.Current.CancellationToken));
    }

    /// <summary>
    ///     Test that the user is offered the picture as it is shown first and then the sizes of the source, in order, and
    ///     that choosing the picture as it is shown encodes the shown picture and draws nothing again.
    /// </summary>
    [Fact]
    public async Task ImageExporter_ExportPngAsync_AsRenderedChosen_OffersListAndEncodesShownPicture()
    {
        // Arrange: a source with two sizes, a renderer that would answer, and a user who keeps the picture as it is shown
        var image = TestImages.Create(Path.Combine(_folder, "cover.svg"), 2, 2);
        _source.CurrentImage = image;
        var renderer = Substitute.For<IImageRenderService>();
        renderer.EncodePng(image).Returns([1, 2, 3]);
        var exporter = new ImageExporter(_source, renderer, null, new FakePresetSource(Slide, Mail));
        _dialogs.PresetNameToPick = "As rendered";
        _dialogs.SaveFileToPick = Path.Combine(_folder, "out.png");

        // Act: export
        await exporter.ExportPngAsync(_dialogs);

        // Assert: the list was as rendered, the sizes in order, nothing was drawn again, and the file is the encoding
        var offer = Assert.Single(_dialogs.PresetOffers);
        Assert.Equal(["As rendered", "Slide", "Mail"], offer.Presets.Select(p => p.Name));
        Assert.Equal(ExportPresetOrigin.AsRendered, offer.Presets[0].Origin);
        Assert.Null(offer.Last);
        await renderer.DidNotReceiveWithAnyArgs().RenderAsync(default!, default!, default, TestContext.Current.CancellationToken);
        Assert.Equal([1, 2, 3], await File.ReadAllBytesAsync(Path.Combine(_folder, "out.png"), TestContext.Current.CancellationToken));
    }

    /// <summary>
    ///     Test that a size draws the source file again with exactly the settings of the preset in the folder of the
    ///     project, encodes the new picture with the resolution of the preset, and writes those bytes.
    /// </summary>
    [Fact]
    public async Task ImageExporter_ExportPngAsync_PresetChosen_RendersWithExactSettingsAndWritesEncoderBytes()
    {
        // Arrange: a shown picture of a source file, a renderer that answers with a drawn picture, and a size with a resolution
        var shown = TestImages.Create(Path.Combine(_folder, "cover.svg"), 2, 2);
        var drawn = TestImages.Create(Path.Combine(_folder, "cover.svg"), 4, 3);
        _source.CurrentImage = shown;
        _source.ProjectFolder = _folder;
        var renderer = Substitute.For<IImageRenderService>();
        renderer.RenderAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<RenderSettings?>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(RenderResult.Success(drawn)));
        renderer.EncodePng(drawn, 150d).Returns([9, 8, 7]);
        var a4 = new ExportPreset("app:a4", "A4", 1240, 1754, 150, false, "#FFFFFF", ExportPresetOrigin.Application);
        var exporter = new ImageExporter(_source, renderer, null, new FakePresetSource(a4));
        _dialogs.PresetToPick = a4;
        _dialogs.SaveFileToPick = Path.Combine(_folder, "a4.png");

        // Act: export
        await exporter.ExportPngAsync(_dialogs);

        // Assert: the file was drawn again with exactly the settings, and the bytes of the encoder are the file
        await renderer.Received(1).RenderAsync(
            shown.SourcePath,
            _folder,
            new RenderSettings(1240, 1754, false, "#FFFFFF", 150),
            Arg.Any<CancellationToken>());
        renderer.DidNotReceive().EncodePng(shown);
        Assert.Equal([9, 8, 7], await File.ReadAllBytesAsync(Path.Combine(_folder, "a4.png"), TestContext.Current.CancellationToken));
        Assert.Empty(_dialogs.Messages);
    }

    /// <summary>
    ///     Test that a transparent size draws without a background and that a size with no resolution writes none.
    /// </summary>
    [Fact]
    public async Task ImageExporter_ExportPngAsync_TransparentPresetWithoutDpi_AsksForTransparencyAndNoResolution()
    {
        // Arrange: a transparent size that also names a background, which it must ignore
        var shown = TestImages.Create(Path.Combine(_folder, "cover.svg"), 2, 2);
        var drawn = TestImages.Create(Path.Combine(_folder, "cover.svg"), 4, 3);
        _source.CurrentImage = shown;
        var renderer = Substitute.For<IImageRenderService>();
        renderer.RenderAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<RenderSettings?>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(RenderResult.Success(drawn)));
        renderer.EncodePng(drawn, null).Returns([5]);
        var logo = new ExportPreset("kit:logo", "Logo", 512, null, null, true, "#102030", ExportPresetOrigin.StyleKit);
        var exporter = new ImageExporter(_source, renderer, null, new FakePresetSource(logo));
        _dialogs.PresetToPick = logo;
        _dialogs.SaveFileToPick = Path.Combine(_folder, "logo.png");

        // Act: export with no project folder known, so the folder of the source file is the folder it may read
        await exporter.ExportPngAsync(_dialogs);

        // Assert: transparency without a background, the folder of the file, and no resolution
        await renderer.Received(1).RenderAsync(
            shown.SourcePath,
            _folder,
            new RenderSettings(512, null, true, null, null),
            Arg.Any<CancellationToken>());
        Assert.Equal([5], await File.ReadAllBytesAsync(Path.Combine(_folder, "logo.png"), TestContext.Current.CancellationToken));
    }

    /// <summary>
    ///     Test that canceling at the size dialog, or at the save dialog after a size was chosen, draws nothing and writes
    ///     nothing.
    /// </summary>
    [Fact]
    public async Task ImageExporter_ExportPngAsync_CancelAtEitherDialog_DrawsAndWritesNothing()
    {
        // Arrange: a shown picture, a renderer that must not be asked, and a source with one size
        _source.CurrentImage = TestImages.Create(Path.Combine(_folder, "cover.svg"), 2, 2);
        var renderer = Substitute.For<IImageRenderService>();
        var exporter = new ImageExporter(_source, renderer, null, new FakePresetSource(Slide));

        // Act: cancel at the size dialog, then choose a size and cancel the save dialog
        _dialogs.PresetToPick = null;
        _dialogs.SaveFileToPick = Path.Combine(_folder, "never.png");
        await exporter.ExportPngAsync(_dialogs);
        _dialogs.PresetToPick = Slide;
        _dialogs.SaveFileToPick = null;
        await exporter.ExportPngAsync(_dialogs);

        // Assert: nothing was drawn, nothing was written, and nothing was reported
        Assert.Equal(2, _dialogs.PresetOffers.Count);
        await renderer.DidNotReceiveWithAnyArgs().RenderAsync(default!, default!, default, TestContext.Current.CancellationToken);
        Assert.Empty(Directory.GetFiles(_folder));
        Assert.Empty(_dialogs.Messages);
    }

    /// <summary>
    ///     Test that a source file that cannot be drawn at the size is reported with the reason of the renderer and no
    ///     file is written, and that a resolution the encoder refuses is reported too.
    /// </summary>
    [Fact]
    public async Task ImageExporter_ExportPngAsync_RenderFails_ShowsMessageWithReasonAndWritesNoFile()
    {
        // Arrange: a renderer that refuses the size, and a size to choose
        _source.CurrentImage = TestImages.Create(Path.Combine(_folder, "cover.svg"), 2, 2);
        var renderer = Substitute.For<IImageRenderService>();
        renderer.RenderAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<RenderSettings?>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(RenderResult.Failure(new RenderError("invalid-settings", "The width must be 1 to 8192."))));
        var huge = new ExportPreset("app:huge", "Huge", 99999, 1, null, false, null, ExportPresetOrigin.Application);
        var exporter = new ImageExporter(_source, renderer, null, new FakePresetSource(huge));
        _dialogs.PresetToPick = huge;
        _dialogs.SaveFileToPick = Path.Combine(_folder, "huge.png");

        // Act: export
        await exporter.ExportPngAsync(_dialogs);

        // Assert: one message with the title and the reason, and no file
        var message = Assert.Single(_dialogs.Messages);
        Assert.Equal("Export as PNG", message.Title);
        Assert.Contains("The width must be 1 to 8192.", message.Message, StringComparison.Ordinal);
        Assert.False(File.Exists(Path.Combine(_folder, "huge.png")));
    }

    /// <summary>
    ///     Test that a render that was cancelled writes no file and says nothing, and that the encoder refusing the
    ///     resolution is shown as a message and writes no file.
    /// </summary>
    [Fact]
    public async Task ImageExporter_ExportPngAsync_RenderCancelledOrEncoderRefuses_WritesNoFile()
    {
        // Arrange: a renderer that is cancelled once and then draws, and an encoder that refuses the resolution
        var drawn = TestImages.Create(Path.Combine(_folder, "cover.svg"), 4, 3);
        _source.CurrentImage = TestImages.Create(Path.Combine(_folder, "cover.svg"), 2, 2);
        var renderer = Substitute.For<IImageRenderService>();
        renderer.RenderAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<RenderSettings?>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(RenderResult.Cancelled()), Task.FromResult(RenderResult.Success(drawn)));
        renderer.EncodePng(drawn, Arg.Any<double?>()).Returns(_ => throw new ArgumentException("Not allowed."));
        var print = new ExportPreset("app:print", "Print", 100, 100, 5000, false, null, ExportPresetOrigin.Application);
        var exporter = new ImageExporter(_source, renderer, null, new FakePresetSource(print));
        _dialogs.PresetToPick = print;
        _dialogs.SaveFileToPick = Path.Combine(_folder, "print.png");

        // Act: export twice
        await exporter.ExportPngAsync(_dialogs);
        var afterCancelled = _dialogs.Messages.Count;
        await exporter.ExportPngAsync(_dialogs);

        // Assert: the cancelled render said nothing, the refused resolution was shown, and no file exists
        Assert.Equal(0, afterCancelled);
        var message = Assert.Single(_dialogs.Messages);
        Assert.StartsWith("The image could not be saved:", message.Message, StringComparison.Ordinal);
        Assert.False(File.Exists(Path.Combine(_folder, "print.png")));
    }

    /// <summary>
    ///     Test that the choice of the user is offered as the selected one the next time, for the length of the session.
    /// </summary>
    [Fact]
    public async Task ImageExporter_ExportPngAsync_SecondExport_OffersTheLastChoice()
    {
        // Arrange: a renderer that draws, a source with two sizes, and a user who picks the second
        var shown = TestImages.Create(Path.Combine(_folder, "cover.svg"), 2, 2);
        _source.CurrentImage = shown;
        var renderer = Substitute.For<IImageRenderService>();
        renderer.RenderAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<RenderSettings?>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(RenderResult.Success(shown)));
        renderer.EncodePng(Arg.Any<RenderedImage>(), Arg.Any<double?>()).Returns([1]);
        var exporter = new ImageExporter(_source, renderer, null, new FakePresetSource(Slide, Mail));
        _dialogs.PresetToPick = Mail;
        _dialogs.SaveFileToPick = Path.Combine(_folder, "mail.png");

        // Act: export twice
        await exporter.ExportPngAsync(_dialogs);
        await exporter.ExportPngAsync(_dialogs);

        // Assert: the first offer had no last choice and the second had the one that was picked
        Assert.Equal(2, _dialogs.PresetOffers.Count);
        Assert.Null(_dialogs.PresetOffers[0].Last);
        Assert.Equal(Mail, _dialogs.PresetOffers[1].Last);
    }

    /// <summary>
    ///     Test with the real renderer that a vector file exported at a preset size is a PNG of exactly that size, with the
    ///     resolution of the preset in the file and the margin filled with the background of the preset.
    /// </summary>
    [Fact]
    public async Task ImageExporter_ExportPngAsync_SvgAtPresetSize_WritesPngOfThatSizeWithResolution()
    {
        // Arrange: an SVG file, the real renderer, the picture it shows, and a size with a background and a resolution
        var svg = Path.Combine(_folder, "banner.svg");
        await File.WriteAllTextAsync(
            svg,
            "<svg xmlns='http://www.w3.org/2000/svg' width='1600' height='640'><rect width='1600' height='640' fill='#7C3AED'/></svg>",
            TestContext.Current.CancellationToken);
        var renderer = new ImageRenderService();
        var shown = (await renderer.RenderAsync(svg, _folder, TestContext.Current.CancellationToken)).Image!;
        _source.CurrentImage = shown;
        _source.ProjectFolder = _folder;
        var slide = new ExportPreset("app:slide", "Slide", 1920, 1080, 96, false, "#0A1022", ExportPresetOrigin.Application);
        var exporter = new ImageExporter(_source, renderer, null, new FakePresetSource(slide));
        _dialogs.PresetToPick = slide;
        _dialogs.SaveFileToPick = Path.Combine(_folder, "banner.png");

        // Act: export
        await exporter.ExportPngAsync(_dialogs);

        // Assert: the file decodes to 1920 x 1080, has the background in its corner and the resolution chunk
        var bytes = await File.ReadAllBytesAsync(Path.Combine(_folder, "banner.png"), TestContext.Current.CancellationToken);
        using var stream = new MemoryStream(bytes);
        using var decoded = PngCodec.Load(stream);
        Assert.Equal((1920, 1080), (decoded.Width, decoded.Height));
        Assert.Equal(new Rgba32(10, 16, 34, 255), decoded[0, 0]);
        Assert.Contains("pHYs", System.Text.Encoding.Latin1.GetString(bytes), StringComparison.Ordinal);
        Assert.Empty(_dialogs.Messages);
    }

    /// <summary>
    ///     A size for slides, from the application.
    /// </summary>
    private static ExportPreset Slide { get; } =
        new("app:slide", "Slide", 1920, 1080, null, false, "#FFFFFF", ExportPresetOrigin.Application);

    /// <summary>
    ///     A size for mail, from the style kit.
    /// </summary>
    private static ExportPreset Mail { get; } =
        new("kit:mail", "Mail", 800, null, null, false, "#FFFFFF", ExportPresetOrigin.StyleKit);

    /// <summary>
    ///     Hand-written source of sizes for tests.
    /// </summary>
    /// <param name="presets">The sizes to offer.</param>
    private sealed class FakePresetSource(params ExportPreset[] presets) : IExportPresetSource
    {
        /// <inheritdoc />
        public IReadOnlyList<ExportPreset> GetPresets() => presets;
    }
}

