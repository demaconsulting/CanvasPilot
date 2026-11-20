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

using DemaConsulting.CanvasPilot.LoggingSubsystem;
using DemaConsulting.CanvasPilot.PreviewSubsystem;

namespace DemaConsulting.CanvasPilot.Tests;

/// <summary>
///     Tests for the <see cref="ImageDragSource" /> unit with a hand-written encoder, on a temporary folder.
/// </summary>
public sealed class ImageDragSourceTests : IDisposable
{
    /// <summary>
    ///     The PNG signature the fake encoder starts its output with.
    /// </summary>
    private static readonly byte[] PngSignature = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];

    /// <summary>
    ///     Temporary folder that holds the drag folder and the log, removed on disposal.
    /// </summary>
    private readonly string _root = Directory.CreateTempSubdirectory("canvaspilot-drag-tests-").FullName;

    /// <summary>
    ///     The encoder, which counts how often it was asked.
    /// </summary>
    private readonly CountingEncoder _encoder = new();

    /// <summary>
    ///     Gets the drag folder, which does not exist until a picture is prepared.
    /// </summary>
    private string DragFolder => Path.Combine(_root, "drag");

    /// <inheritdoc />
    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    /// <summary>
    ///     Test that a picture is written as a PNG file named after its source file and the start of its pixel
    ///     fingerprint, in the drag folder, which is created on demand.
    /// </summary>
    [Fact]
    public async Task ImageDragSource_Prepare_Picture_WritesValidPngWithSuggestedName()
    {
        // Arrange: a picture of a diagram file and a drag source over a folder that does not exist yet
        var image = TestImages.Create(Path.Combine(_root, "project", "flow.mmd"), 4, 2);
        var source = new ImageDragSource(_encoder, DragFolder);
        var folderExistedBefore = Directory.Exists(DragFolder);

        // Act: prepare the picture
        var path = source.Prepare(image);

        // Assert: the file has the expected name, holds the encoder's PNG, and nothing else was left in the folder
        Assert.False(folderExistedBefore);
        Assert.Equal(Path.Combine(DragFolder, $"flow-{image.ContentHash[..8]}.png"), path);
        var bytes = await File.ReadAllBytesAsync(path!, TestContext.Current.CancellationToken);
        Assert.Equal(PngSignature, bytes[..8]);
        Assert.Equal(DragFolder, source.Folder);
        Assert.Equal(path, Assert.Single(Directory.EnumerateFileSystemEntries(DragFolder)));
    }

    /// <summary>
    ///     Test that the compound extension of Vega-Lite files is removed from the name.
    /// </summary>
    [Fact]
    public void ImageDragSource_Prepare_VegaLiteFile_NamesFileAfterStemWithoutCompoundExtension()
    {
        // Arrange: the picture of a Vega-Lite chart
        var image = TestImages.Create(Path.Combine(_root, "sales.vl.json"), 3, 3);
        var source = new ImageDragSource(_encoder, DragFolder);

        // Act: prepare the picture
        var path = source.Prepare(image);

        // Assert: the stem is the name before .vl.json
        Assert.Equal($"sales-{image.ContentHash[..8]}.png", Path.GetFileName(path));
    }

    /// <summary>
    ///     Test that the same picture is written once and that a different picture of the same file gets its own file.
    /// </summary>
    [Fact]
    public void ImageDragSource_Prepare_SamePicture_ReusesFile()
    {
        // Arrange: two equal pictures and a different one of the same file
        var source = new ImageDragSource(_encoder, DragFolder);
        var first = TestImages.Create(Path.Combine(_root, "a.svg"), 4, 2);
        var same = TestImages.Create(Path.Combine(_root, "a.svg"), 4, 2);
        var changed = TestImages.Create(Path.Combine(_root, "a.svg"), 5, 2);

        // Act: prepare each
        var firstPath = source.Prepare(first);
        var samePath = source.Prepare(same);
        var changedPath = source.Prepare(changed);

        // Assert: the equal pictures share one file and one encoding; the changed picture has its own file
        Assert.Equal(firstPath, samePath);
        Assert.NotEqual(firstPath, changedPath);
        Assert.Equal(2, _encoder.Calls);
        Assert.Equal(2, Directory.GetFiles(DragFolder).Length);
    }

    /// <summary>
    ///     Test that characters that are not allowed in file names are replaced and that a long name is shortened, so
    ///     the file always stays in the drag folder.
    /// </summary>
    [Fact]
    public void ImageDragSource_Prepare_UnusualSourceName_ProducesSafeFileNameInsideFolder()
    {
        // Arrange: a source name with a character that no file name may hold on any platform, and a very long one
        var source = new ImageDragSource(_encoder, DragFolder);
        var odd = TestImages.Create(Path.Combine(_root, "a\0b.svg"), 2, 2);
        var longName = TestImages.Create(Path.Combine(_root, new string('x', 200) + ".svg"), 2, 2);

        // Act: prepare both
        var oddPath = source.Prepare(odd);
        var longPath = source.Prepare(longName);

        // Assert: both files are directly in the drag folder with safe, bounded names
        Assert.Equal(DragFolder, Path.GetDirectoryName(oddPath));
        Assert.Equal($"a_b-{odd.ContentHash[..8]}.png", Path.GetFileName(oddPath));
        Assert.Equal(DragFolder, Path.GetDirectoryName(longPath));
        Assert.Equal(60 + 1 + 8 + ".png".Length, Path.GetFileName(longPath)!.Length);
    }

    /// <summary>
    ///     Test that a folder that cannot be created gives no file and an entry in the log instead of an exception.
    /// </summary>
    [Fact]
    public async Task ImageDragSource_Prepare_UnwritableFolder_ReturnsNullAndLogs()
    {
        // Arrange: the drag folder is the path of an existing file, so it cannot be created
        var blocker = Path.Combine(_root, "blocker");
        await File.WriteAllTextAsync(blocker, "not a folder", TestContext.Current.CancellationToken);
        var logger = new RollingFileLogger(Path.Combine(_root, "logs"));
        var source = new ImageDragSource(_encoder, blocker, logger);

        // Act: prepare a picture
        var path = source.Prepare(TestImages.Create(Path.Combine(_root, "a.svg"), 2, 2));

        // Assert: nothing was written and the problem was logged with the source file's name
        Assert.Null(path);
        Assert.Equal("not a folder", await File.ReadAllTextAsync(blocker, TestContext.Current.CancellationToken));
        var log = await File.ReadAllTextAsync(logger.ActiveFilePath, TestContext.Current.CancellationToken);
        Assert.Contains("could not be prepared for dragging", log, StringComparison.Ordinal);
        Assert.Contains("a.svg", log, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Test that an encoder that fails with an I/O problem gives no file and leaves no half-written file behind.
    /// </summary>
    [Fact]
    public void ImageDragSource_Prepare_EncoderFails_ReturnsNullAndLeavesNoFile()
    {
        // Arrange: an encoder that fails
        _encoder.Failure = new IOException("disk full");
        var source = new ImageDragSource(_encoder, DragFolder);

        // Act: prepare a picture
        var path = source.Prepare(TestImages.Create(Path.Combine(_root, "a.svg"), 2, 2));

        // Assert: no file, in particular no leftover from the attempt
        Assert.Null(path);
        Assert.Empty(Directory.Exists(DragFolder) ? Directory.GetFiles(DragFolder) : []);
    }

    /// <summary>
    ///     Test that purging removes only the files the unit wrote, and nothing else in the folder.
    /// </summary>
    [Fact]
    public async Task ImageDragSource_Purge_FolderWithOtherFiles_RemovesOnlyOwnFiles()
    {
        // Arrange: two prepared pictures, a leftover of an interrupted write, files and a folder that are not ours
        var source = new ImageDragSource(_encoder, DragFolder);
        var first = source.Prepare(TestImages.Create(Path.Combine(_root, "a.svg"), 2, 2))!;
        var second = source.Prepare(TestImages.Create(Path.Combine(_root, "b.svg"), 3, 2))!;
        var pending = Path.Combine(DragFolder, "c-0123abcd.png.tmp");
        var token = TestContext.Current.CancellationToken;
        await File.WriteAllTextAsync(pending, "x", token);
        var foreign = new[] { "notes.txt", "photo.png", "short-1234567.png", "upper-ABCDEF12.png" }
            .Select(name => Path.Combine(DragFolder, name))
            .ToArray();
        foreach (var file in foreign)
        {
            await File.WriteAllTextAsync(file, "keep", token);
        }

        var nested = Directory.CreateDirectory(Path.Combine(DragFolder, "sub-0123abcd.png")).FullName;
        var nestedFile = Path.Combine(nested, "inner-0123abcd.png");
        await File.WriteAllTextAsync(nestedFile, "keep", token);

        // Act: purge
        var deleted = source.Purge();

        // Assert: the three own files are gone and everything else is untouched
        Assert.Equal(3, deleted);
        Assert.False(File.Exists(first));
        Assert.False(File.Exists(second));
        Assert.False(File.Exists(pending));
        Assert.All(foreign, file => Assert.True(File.Exists(file), file));
        Assert.True(File.Exists(nestedFile));
    }

    /// <summary>
    ///     Test that purging a folder that does not exist is not a problem.
    /// </summary>
    [Fact]
    public void ImageDragSource_Purge_MissingFolder_ReturnsZero()
    {
        // Arrange: a drag source whose folder was never created
        var source = new ImageDragSource(_encoder, DragFolder);

        // Act / Assert: nothing to delete and nothing created
        Assert.Equal(0, source.Purge());
        Assert.False(Directory.Exists(DragFolder));
    }

    /// <summary>
    ///     Test the default folder and that the constructor and methods reject missing arguments.
    /// </summary>
    [Fact]
    public void ImageDragSource_New_InvalidArguments_Throw()
    {
        // Act / Assert: each required argument is checked and the default folder is in the local application data
        Assert.Throws<ArgumentNullException>(() => new ImageDragSource(null!, DragFolder));
        Assert.Throws<ArgumentException>(() => new ImageDragSource(_encoder, " "));
        Assert.Throws<ArgumentNullException>(() => new ImageDragSource(_encoder, DragFolder).Prepare(null!));
        Assert.EndsWith(Path.Combine("CanvasPilot", "drag"), ImageDragSource.DefaultFolder, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Hand-written encoder that returns a fixed PNG signature and counts its calls.
    /// </summary>
    private sealed class CountingEncoder : IImageRenderService
    {
        /// <summary>
        ///     Gets how many pictures were encoded.
        /// </summary>
        public int Calls { get; private set; }

        /// <summary>
        ///     Gets or sets the failure the next encoding throws, or <see langword="null" />.
        /// </summary>
        public Exception? Failure { get; set; }

        /// <inheritdoc />
        public Task<RenderResult> RenderAsync(string filePath, string projectFolder, CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }

        /// <inheritdoc />
        public byte[] EncodePng(RenderedImage image)
        {
            Calls++;
            return Failure is { } failure ? throw failure : [.. PngSignature, (byte)image.Width, (byte)image.Height];
        }
    }
}
