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

using DemaConsulting.CanvasPilot.ComposerSubsystem;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace DemaConsulting.CanvasPilot.Tests;

/// <summary>
///     Tests for the <see cref="AttachmentIntake" /> unit with a substitute attachment store, on temporary files. The
///     tests show that dropped content is classified without being opened and that unsafe, missing, repeated, and
///     excessive content is left out with a reason.
/// </summary>
public sealed class AttachmentIntakeTests : IDisposable
{
    /// <summary>
    ///     The first bytes of a PNG file.
    /// </summary>
    private static readonly byte[] PngBytes = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 1];

    /// <summary>
    ///     Temporary folder that holds the dropped files, removed on disposal.
    /// </summary>
    private readonly string _root = Directory.CreateTempSubdirectory("canvaspilot-intake-tests-").FullName;

    /// <summary>
    ///     The substitute store, which records what the intake asks of it.
    /// </summary>
    private readonly IAttachmentStore _store = Substitute.For<IAttachmentStore>();

    /// <summary>
    ///     The intake under test.
    /// </summary>
    private readonly AttachmentIntake _intake;

    /// <summary>
    ///     Initializes a new instance of the <see cref="AttachmentIntakeTests" /> class.
    /// </summary>
    public AttachmentIntakeTests()
    {
        _intake = new AttachmentIntake(_store);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    /// <summary>
    ///     Test that an existing file becomes a file chip that refers to the original path.
    /// </summary>
    [Fact]
    public void AttachmentIntake_Accept_ExistingFile_BecomesFileChipWithOriginalPath()
    {
        // Arrange: a text file outside the project
        var path = CreateFile("notes.txt");

        // Act: drop it
        var result = _intake.Accept(new DroppedContent { Paths = [path] });

        // Assert: one file chip with the original path and nothing else
        Assert.Equal(new FileChip(path), Assert.Single(result.Segments));
        Assert.Empty(result.Skipped);
        Assert.Empty(result.Flagged);
    }

    /// <summary>
    ///     Test that an existing folder becomes a folder chip, also when the path ends with a separator.
    /// </summary>
    [Fact]
    public void AttachmentIntake_Accept_ExistingFolder_BecomesFolderChip()
    {
        // Arrange: a folder with a file, given with a trailing separator
        var folder = Directory.CreateDirectory(Path.Combine(_root, "photos")).FullName;
        CreateFile(Path.Combine("photos", "a.png"));

        // Act: drop the folder
        var result = _intake.Accept(new DroppedContent { Paths = [folder + Path.DirectorySeparatorChar] });

        // Assert: a folder chip for the folder without the trailing separator
        Assert.Equal(new FolderChip(folder), Assert.Single(result.Segments));
    }

    /// <summary>
    ///     Test that files with a picture extension become picture chips that refer to the original path, and that no
    ///     copy is made.
    /// </summary>
    [Theory]
    [InlineData("a.png")]
    [InlineData("b.JPG")]
    [InlineData("c.jpeg")]
    [InlineData("d.svg")]
    [InlineData("e.gif")]
    [InlineData("f.webp")]
    [InlineData("g.bmp")]
    [InlineData("h.tif")]
    public void AttachmentIntake_Accept_PictureFile_BecomesImageChipReferencingOriginalPath(string name)
    {
        // Arrange: a picture file outside the project
        var path = CreateFile(name);

        // Act: drop it
        var result = _intake.Accept(new DroppedContent { Paths = [path] });

        // Assert: a picture chip that is not marked as pasted, and the store was not asked for anything
        var chip = Assert.IsType<ImageChip>(Assert.Single(result.Segments));
        Assert.Equal(path, chip.FullPath);
        Assert.False(chip.IsPasted);
        Assert.Empty(_store.ReceivedCalls());
    }

    /// <summary>
    ///     Test that a path that does not exist is left out with a reason and makes no chip.
    /// </summary>
    [Fact]
    public void AttachmentIntake_Accept_MissingPath_IsSkippedWithReason()
    {
        // Arrange: a path of a file that is not there
        var path = Path.Combine(_root, "gone.txt");

        // Act: drop it
        var result = _intake.Accept(new DroppedContent { Paths = [path] });

        // Assert: no chip, one note with the path and the reason
        Assert.Empty(result.Segments);
        var note = Assert.Single(result.Skipped);
        Assert.Equal(path, note.Item);
        Assert.Contains("does not exist", note.Reason, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Test that the same item given twice in one drop is accepted once.
    /// </summary>
    [Fact]
    public void AttachmentIntake_Accept_SamePathTwice_IsAcceptedOnce()
    {
        // Arrange: one file named twice, once with a redundant segment
        var path = CreateFile("data.csv");
        var again = Path.Combine(_root, "other", "..", "data.csv");
        Directory.CreateDirectory(Path.Combine(_root, "other"));

        // Act: drop the file, then the second spelling, then the first again
        var result = _intake.Accept(new DroppedContent { Paths = [path, path] });

        // Assert: one chip and one note about the repeat; the spelling with '..' is refused by itself
        Assert.Equal(new FileChip(path), Assert.Single(result.Segments));
        Assert.Contains("twice", Assert.Single(result.Skipped).Reason, StringComparison.Ordinal);
        Assert.Contains("'..'", Assert.Single(_intake.Accept(new DroppedContent { Paths = [again] }).Skipped).Reason, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Test that paths that are not safe to refer to are left out with a reason and the store is never asked.
    /// </summary>
    [Theory]
    [InlineData("relative.txt")]
    [InlineData(@"..\escape.txt")]
    [InlineData(@"C:\data\..\other.txt")]
    [InlineData(@"C:\data\con.txt")]
    [InlineData(@"C:\data\NUL")]
    [InlineData(@"C:\data\file.txt:hidden")]
    [InlineData(@"C:\data\name.")]
    [InlineData(@"C:\data\a|b.txt")]
    [InlineData(@"C:\data\what?.txt")]
    [InlineData(@"\\.\C:\data")]
    [InlineData(@"\\?\C:\data\file.txt")]
    [InlineData("C:\\data\\bell\u0007.txt")]
    [InlineData("")]
    [InlineData("   ")]
    public void AttachmentIntake_Accept_UnsafePath_IsSkippedWithReasonAndMakesNoChip(string path)
    {
        // Arrange: a path that could reach a device, an alternate data stream, or another place (the theory data)

        // Act: drop it
        var result = _intake.Accept(new DroppedContent { Paths = [path] });

        // Assert: nothing was accepted, one note says why, and nothing was asked of the store
        Assert.Empty(result.Segments);
        Assert.False(string.IsNullOrWhiteSpace(Assert.Single(result.Skipped).Reason));
        Assert.Empty(_store.ReceivedCalls());
    }

    /// <summary>
    ///     Test that a whole drive is not accepted.
    /// </summary>
    [Fact]
    public void AttachmentIntake_Accept_DriveRoot_IsSkipped()
    {
        // Arrange: the root of the drive of the temporary folder
        var root = Path.GetPathRoot(_root)!;

        // Act: drop it
        var result = _intake.Accept(new DroppedContent { Paths = [root] });

        // Assert: refused
        Assert.Empty(result.Segments);
        Assert.Contains("drive", Assert.Single(result.Skipped).Reason, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Test that a pasted picture is saved through the store and becomes a picture chip that is marked as pasted.
    /// </summary>
    [Fact]
    public void AttachmentIntake_Accept_PastedPicture_IsSavedAndBecomesPastedImageChip()
    {
        // Arrange: a store that saves the picture under a known path and records the bytes it was given
        var saved = Path.Combine(_root, ".canvaspilot", "attachments", "paste-20261120-093015-1.png");
        byte[]? received = null;
        _store.SavePastedImage(Arg.Do<ReadOnlyMemory<byte>>(data => received = data.ToArray())).Returns(saved);

        // Act: paste a picture
        var result = _intake.Accept(new DroppedContent { Images = [PngBytes] });

        // Assert: a pasted picture chip for the saved file, and the store got exactly the bytes
        Assert.Equal(new ImageChip(saved, isPasted: true), Assert.Single(result.Segments));
        Assert.Equal(PngBytes, received);
    }

    /// <summary>
    ///     Test that a picture that the store refuses is left out with the reason and does not stop the others.
    /// </summary>
    [Fact]
    public void AttachmentIntake_Accept_PastedPictureRefusedByStore_IsSkippedAndOthersStay()
    {
        // Arrange: a store that refuses the first picture and saves the second, and a file
        var saved = Path.Combine(_root, "saved.png");
        _store.SavePastedImage(Arg.Any<ReadOnlyMemory<byte>>())
            .Returns(_ => throw new ArgumentException("The data is not a PNG picture."), _ => saved);
        var path = CreateFile("a.txt");

        // Act: drop a file and paste two pictures
        var result = _intake.Accept(new DroppedContent { Paths = [path], Images = [new byte[] { 1 }, PngBytes] });

        // Assert: the file and the second picture were accepted, the first picture was left out with its reason
        Assert.Equal([new FileChip(path), new ImageChip(saved, isPasted: true)], result.Segments);
        var note = Assert.Single(result.Skipped);
        Assert.Equal("pasted picture 1", note.Item);
        Assert.Contains("not a PNG", note.Reason, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Test that a picture that cannot be written is left out with the reason.
    /// </summary>
    [Fact]
    public void AttachmentIntake_Accept_PastedPictureCannotBeWritten_IsSkipped()
    {
        // Arrange: a store that cannot write
        _store.SavePastedImage(Arg.Any<ReadOnlyMemory<byte>>()).Throws(new IOException("The disk is full."));

        // Act: paste a picture
        var result = _intake.Accept(new DroppedContent { Images = [PngBytes] });

        // Assert: nothing accepted and the reason is reported
        Assert.Empty(result.Segments);
        Assert.Contains("disk is full", Assert.Single(result.Skipped).Reason, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Test that dropped text is cleaned of the chip placeholder and control characters and becomes typed text.
    /// </summary>
    [Fact]
    public void AttachmentIntake_Accept_TextWithPlaceholderAndControlCharacters_IsSanitized()
    {
        // Arrange: text from another program with a placeholder, a bell, and Windows line breaks
        var text = "first" + ComposerSnapshot.ChipPlaceholder + "\u0007\r\nsecond";

        // Act: drop it
        var result = _intake.Accept(new DroppedContent { Text = text });

        // Assert: one typed segment with clean text
        Assert.Equal(new TextSegment("first\nsecond", TextOrigin.Typed), Assert.Single(result.Segments));
    }

    /// <summary>
    ///     Test that text of only unwanted characters, and no text, make no segment.
    /// </summary>
    [Fact]
    public void AttachmentIntake_Accept_TextOfOnlyControlCharactersOrNoText_MakesNoSegment()
    {
        // Arrange and Act: drop text that holds nothing that stays, an empty text, and nothing
        var controls = _intake.Accept(new DroppedContent { Text = "\u0001" + ComposerSnapshot.ChipPlaceholder });
        var empty = _intake.Accept(new DroppedContent { Text = string.Empty });
        var nothing = _intake.Accept(new DroppedContent());

        // Assert: no segments and nothing to report
        Assert.Empty(controls.Segments);
        Assert.Empty(empty.Segments);
        Assert.Empty(nothing.Segments);
        Assert.Empty(nothing.Skipped);
    }

    /// <summary>
    ///     Test that very long text is shortened and flagged.
    /// </summary>
    [Fact]
    public void AttachmentIntake_Accept_VeryLongText_IsShortenedAndFlagged()
    {
        // Arrange: text one character over the limit
        var text = new string('x', AttachmentIntake.MaximumTextLength + 1);

        // Act: drop it
        var result = _intake.Accept(new DroppedContent { Text = text });

        // Assert: the text holds the limit and a note says it was shortened
        Assert.Equal(AttachmentIntake.MaximumTextLength, Assert.Single(result.Segments).Length);
        Assert.Contains("shortened", Assert.Single(result.Flagged).Reason, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Test that a file that is larger than a message may carry is accepted but flagged, so the user is told before
    ///     sending.
    /// </summary>
    [Fact]
    public void AttachmentIntake_Accept_FileLargerThanLimit_IsAcceptedAndFlagged()
    {
        // Arrange: a file one byte over the limit
        var path = Path.Combine(_root, "huge.bin");
        using (var stream = new FileStream(path, FileMode.CreateNew))
        {
            stream.SetLength(AttachmentStore.MaximumBytes + 1);
        }

        // Act: drop it
        var result = _intake.Accept(new DroppedContent { Paths = [path] });

        // Assert: a chip and a warning that names the limit
        Assert.Equal(new FileChip(path), Assert.Single(result.Segments));
        var note = Assert.Single(result.Flagged);
        Assert.Equal(path, note.Item);
        Assert.Contains("200 MB", note.Reason, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Test that dropped content is never opened: files of every kind of program or script, one of them held open
    ///     exclusively by another program, become chips, keep their bytes, and the store is never asked to read, save,
    ///     copy, or delete anything.
    /// </summary>
    [Fact]
    public async Task AttachmentIntake_Accept_ProgramsAndScripts_AreOnlyChipsAndNeverOpened()
    {
        // Arrange: files with executable extensions and a locked file
        var names = new[] { "setup.exe", "run.bat", "script.ps1", "macro.docm", "link.lnk", "page.html", "app.dll" };
        var paths = names.Select(name => CreateFile(name, "MZ-not-really")).ToList();
        var locked = CreateFile("locked.exe", "secret");
        using var hold = new FileStream(locked, FileMode.Open, FileAccess.Read, FileShare.None);

        // Act: drop all of them
        var result = _intake.Accept(new DroppedContent { Paths = [.. paths, locked] });

        // Assert: one file chip for each, in order, with the content unchanged and the store untouched
        Assert.Equal(names.Length + 1, result.Segments.Length);
        Assert.All(result.Segments, segment => Assert.IsType<FileChip>(segment));
        Assert.Empty(result.Skipped);
        foreach (var path in paths)
        {
            Assert.Equal("MZ-not-really", await File.ReadAllTextAsync(path, TestContext.Current.CancellationToken));
        }

        Assert.Empty(_store.ReceivedCalls());
    }

    /// <summary>
    ///     Test that one drop is bounded: only the allowed number of items is accepted and the rest is reported.
    /// </summary>
    [Fact]
    public void AttachmentIntake_Accept_MoreThanMaximumItems_AcceptsMaximumAndReportsRest()
    {
        // Arrange: more files than one drop may bring
        var paths = Enumerable.Range(0, AttachmentIntake.MaximumItems + 5).Select(index => CreateFile($"f{index}.txt")).ToList();

        // Act: drop them all
        var result = _intake.Accept(new DroppedContent { Paths = paths });

        // Assert: the first ones are accepted in order and each of the rest has a note
        Assert.Equal(AttachmentIntake.MaximumItems, result.Segments.Length);
        Assert.Equal(new FileChip(paths[0]), result.Segments[0]);
        Assert.Equal(5, result.Skipped.Length);
        Assert.All(result.Skipped, note => Assert.Contains("at a time", note.Reason, StringComparison.Ordinal));
    }

    /// <summary>
    ///     Test that the segments come in the order paths, pasted pictures, text, and that one problem does not stop
    ///     the others.
    /// </summary>
    [Fact]
    public void AttachmentIntake_Accept_MixedContent_KeepsOrderAndAcceptsTheRest()
    {
        // Arrange: a file, a missing path, a folder, a pasted picture, and text
        var file = CreateFile("a.txt");
        var folder = Directory.CreateDirectory(Path.Combine(_root, "set")).FullName;
        var saved = Path.Combine(_root, "pasted.png");
        _store.SavePastedImage(Arg.Any<ReadOnlyMemory<byte>>()).Returns(saved);

        // Act: drop them
        var result = _intake.Accept(new DroppedContent
        {
            Paths = [file, Path.Combine(_root, "missing"), folder],
            Images = [PngBytes],
            Text = "see attached"
        });

        // Assert: file, folder, pasted picture, and text in order, with one note for the missing path
        Assert.Equal(
            [new FileChip(file), new FolderChip(folder), new ImageChip(saved, isPasted: true), new TextSegment("see attached", TextOrigin.Typed)],
            result.Segments);
        Assert.Single(result.Skipped);
    }

    /// <summary>
    ///     Test that missing arguments are refused.
    /// </summary>
    [Fact]
    public void AttachmentIntake_New_AndAccept_NullArguments_Throw()
    {
        // Arrange, Act, and Assert: no store and no content are refused
        Assert.Throws<ArgumentNullException>(() => new AttachmentIntake(null!));
        Assert.Throws<ArgumentNullException>(() => _intake.Accept(null!));
    }

    /// <summary>
    ///     Creates a file in the temporary folder.
    /// </summary>
    /// <param name="relativePath">The path relative to the temporary folder.</param>
    /// <param name="content">The text of the file.</param>
    /// <returns>The full path.</returns>
    private string CreateFile(string relativePath, string content = "x")
    {
        var path = Path.Combine(_root, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content);
        return path;
    }
}
