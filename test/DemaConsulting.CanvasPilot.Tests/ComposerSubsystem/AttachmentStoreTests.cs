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

using System.Diagnostics;
using System.Text.RegularExpressions;
using DemaConsulting.CanvasPilot.ComposerSubsystem;
using DemaConsulting.CanvasPilot.ProjectSubsystem;

namespace DemaConsulting.CanvasPilot.Tests;

/// <summary>
///     Tests for the <see cref="AttachmentStore" /> unit and its name rules, on temporary folders: pasted pictures in the
///     project's private data folder, the removal of leftovers, and the copy of attachments into the project when a
///     message is sent, with its limits and its confinement to the project.
/// </summary>
public sealed class AttachmentStoreTests : IDisposable
{
    /// <summary>
    ///     The first bytes of a PNG file, which is all the store checks of a pasted picture.
    /// </summary>
    private static readonly byte[] PngBytes = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 1, 2, 3, 4];

    /// <summary>
    ///     Temporary folder that holds the project and the files that are attached from outside it.
    /// </summary>
    private readonly string _root = Directory.CreateTempSubdirectory("canvaspilot-attach-tests-").FullName;

    /// <summary>
    ///     The links the test created, which are removed before the folder is deleted.
    /// </summary>
    private readonly List<string> _links = [];

    /// <summary>
    ///     Gets the project folder, which exists.
    /// </summary>
    private string Project => Directory.CreateDirectory(Path.Combine(_root, "project")).FullName;

    /// <summary>
    ///     Gets the folder that holds the files that are attached from outside the project, which exists.
    /// </summary>
    private string External => Directory.CreateDirectory(Path.Combine(_root, "external")).FullName;

    /// <inheritdoc />
    public void Dispose()
    {
        // Remove links first, so that deleting the tree never follows them
        foreach (var link in _links.Where(Directory.Exists))
        {
            Directory.Delete(link, recursive: false);
        }

        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    /// <summary>
    ///     Test that a pasted picture is saved as a file with the paste name pattern in the project's private folder for
    ///     attachments, with exactly the bytes that were pasted.
    /// </summary>
    [Fact]
    public async Task AttachmentStore_SavePastedImage_Png_SavesInPrivateDraftFolderWithPasteName()
    {
        // Arrange: a store on a project that has no data folder yet
        var store = new AttachmentStore(Project, timeProvider: new FixedTimeProvider());

        // Act: save a pasted picture
        var path = store.SavePastedImage(PngBytes);

        // Assert: the file is in .canvaspilot/attachments, named by the time and a number, and holds the bytes
        Assert.Equal(Path.Combine(Project, ProjectDocument.DataFolderName, "attachments"), store.PastedImagesFolder);
        Assert.Equal(store.PastedImagesFolder, Path.GetDirectoryName(path));
        Assert.Equal("paste-20261120-093015-1.png", Path.GetFileName(path));
        Assert.Equal(PngBytes, await File.ReadAllBytesAsync(path, TestContext.Current.CancellationToken));
    }

    /// <summary>
    ///     Test that two pictures pasted in the same second get different files and nothing is overwritten.
    /// </summary>
    [Fact]
    public void AttachmentStore_SavePastedImage_TwoInSameSecond_UseDifferentFiles()
    {
        // Arrange: a store whose clock stands still
        var store = new AttachmentStore(Project, timeProvider: new FixedTimeProvider());

        // Act: paste twice
        var first = store.SavePastedImage(PngBytes);
        var second = store.SavePastedImage(PngBytes);

        // Assert: two files with numbered names
        Assert.NotEqual(first, second);
        Assert.Equal("paste-20261120-093015-2.png", Path.GetFileName(second));
        Assert.Equal(2, Directory.GetFiles(store.PastedImagesFolder).Length);
    }

    /// <summary>
    ///     Test that data that is empty, not a PNG picture, or too large is refused and nothing is written.
    /// </summary>
    [Fact]
    public void AttachmentStore_SavePastedImage_NotAPngOrTooLarge_ThrowsArgumentExceptionAndWritesNothing()
    {
        // Arrange: a store, text bytes, and a picture one byte over the limit
        var store = new AttachmentStore(Project);
        var tooLarge = new byte[AttachmentStore.MaximumPastedImageBytes + 1];
        PngBytes.CopyTo(tooLarge, 0);

        // Act and Assert: empty data, text, and a picture over the limit are refused; no file or folder was made
        Assert.Throws<ArgumentException>(() => store.SavePastedImage(ReadOnlyMemory<byte>.Empty));
        Assert.Throws<ArgumentException>(() => store.SavePastedImage("MZ this is a program"u8.ToArray()));
        Assert.Throws<ArgumentException>(() => store.SavePastedImage(tooLarge));
        Assert.False(Directory.Exists(store.PastedImagesFolder));
    }

    /// <summary>
    ///     Test that leftover pasted pictures are deleted unless a chip still refers to them, and that nothing else in
    ///     the folder is touched.
    /// </summary>
    [Fact]
    public void AttachmentStore_RemoveLeftovers_UnreferencedPastedPictures_AreDeletedAndOthersStay()
    {
        // Arrange: three pasted pictures, a file and a folder of the user's in the same folder
        var store = new AttachmentStore(Project, timeProvider: new FixedTimeProvider());
        var kept = store.SavePastedImage(PngBytes);
        var gone1 = store.SavePastedImage(PngBytes);
        var gone2 = store.SavePastedImage(PngBytes);
        var other = Path.Combine(store.PastedImagesFolder, "notes.txt");
        var folder = Directory.CreateDirectory(Path.Combine(store.PastedImagesFolder, "paste-20261120-093015-9.png.d")).FullName;
        File.WriteAllText(other, "mine");

        // Act: remove leftovers while one chip still refers to the first picture
        var removed = store.RemoveLeftovers([kept]);

        // Assert: the two others are deleted; the referenced picture, the other file, and the folder stay
        Assert.Equal(2, removed);
        Assert.True(File.Exists(kept));
        Assert.False(File.Exists(gone1));
        Assert.False(File.Exists(gone2));
        Assert.True(File.Exists(other));
        Assert.True(Directory.Exists(folder));
    }

    /// <summary>
    ///     Test that removing leftovers without a draft folder does nothing.
    /// </summary>
    [Fact]
    public void AttachmentStore_RemoveLeftovers_NoDraftFolder_ReturnsZero()
    {
        // Arrange: a store on a project without a data folder
        var store = new AttachmentStore(Project);

        // Act: remove leftovers
        var removed = store.RemoveLeftovers([]);

        // Assert: nothing was removed and no folder was created
        Assert.Equal(0, removed);
        Assert.False(Directory.Exists(store.PastedImagesFolder));
    }

    /// <summary>
    ///     Test that a file from outside the project is copied into the attachments folder and the original stays.
    /// </summary>
    [Fact]
    public async Task AttachmentStore_StageForSend_ExternalFile_IsCopiedIntoAttachmentsFolder()
    {
        // Arrange: a picture outside the project and a chip for it
        var source = WriteFile(Path.Combine(External, "photo.png"), [1, 2, 3, 4, 5]);
        var store = new AttachmentStore(Project);
        var chip = new ImageChip(source);

        // Act: stage the message's attachments
        var result = store.StageForSend([chip]);

        // Assert: the file is in attachments with the same bytes, the original is untouched, and the result says so
        Assert.True(result.Succeeded);
        var staged = Assert.Single(result.Attachments);
        Assert.Equal("attachments/photo.png", staged.ProjectRelativePath);
        Assert.Equal(AttachmentKind.Image, staged.Kind);
        Assert.Equal("photo.png", staged.Name);
        Assert.Equal(5, staged.SizeBytes);
        Assert.Equal(1, staged.FileCount);
        Assert.True(staged.WasCopied);
        Assert.Equal(chip, staged.Chip);
        Assert.Equal(source, staged.OriginalPath);
        Assert.Equal(new byte[] { 1, 2, 3, 4, 5 }, await File.ReadAllBytesAsync(Path.Combine(Project, "attachments", "photo.png"), TestContext.Current.CancellationToken));
        Assert.True(File.Exists(source));
    }

    /// <summary>
    ///     Test that a name that is taken gets a number instead of being overwritten, for files from different places.
    /// </summary>
    [Fact]
    public async Task AttachmentStore_StageForSend_NameTaken_AddsNumberAndNeverOverwrites()
    {
        // Arrange: a file of the same name already in attachments, and two more with that name from outside
        var existing = WriteFile(Path.Combine(Project, "attachments", "photo.png"), [9]);
        var first = WriteFile(Path.Combine(External, "a", "photo.png"), [1]);
        var second = WriteFile(Path.Combine(External, "b", "photo.png"), [2]);
        var store = new AttachmentStore(Project);

        // Act: stage both
        var result = store.StageForSend([new FileChip(first), new FileChip(second)]);

        // Assert: the numbered names are free ones, in order, and the existing file is unchanged
        Assert.True(result.Succeeded);
        Assert.Equal(["attachments/photo (2).png", "attachments/photo (3).png"], result.Attachments.Select(item => item.ProjectRelativePath));
        Assert.Equal(new byte[] { 9 }, await File.ReadAllBytesAsync(existing, TestContext.Current.CancellationToken));
        Assert.Equal(new byte[] { 1 }, await File.ReadAllBytesAsync(Path.Combine(Project, "attachments", "photo (2).png"), TestContext.Current.CancellationToken));
        Assert.Equal(new byte[] { 2 }, await File.ReadAllBytesAsync(Path.Combine(Project, "attachments", "photo (3).png"), TestContext.Current.CancellationToken));
    }

    /// <summary>
    ///     Test that the same file attached twice is put into the project once and both chips refer to that copy.
    /// </summary>
    [Fact]
    public void AttachmentStore_StageForSend_SameFileTwice_IsCopiedOnce()
    {
        // Arrange: one file and two chips for it
        var source = WriteFile(Path.Combine(External, "data.csv"), [1, 2]);
        var store = new AttachmentStore(Project);

        // Act: stage both chips
        var result = store.StageForSend([new FileChip(source), new FileChip(source)]);

        // Assert: one copy, two attachments that refer to it
        Assert.True(result.Succeeded);
        Assert.Equal(2, result.Attachments.Length);
        Assert.All(result.Attachments, item => Assert.Equal("attachments/data.csv", item.ProjectRelativePath));
        Assert.Single(Directory.GetFiles(Path.Combine(Project, "attachments")));
    }

    /// <summary>
    ///     Test that a folder is copied with everything in it, including empty folders and nested files.
    /// </summary>
    [Fact]
    public async Task AttachmentStore_StageForSend_Folder_IsCopiedRecursively()
    {
        // Arrange: a folder with a file, a nested folder with a file, and an empty folder
        var folder = Path.Combine(External, "photos");
        WriteFile(Path.Combine(folder, "a.txt"), [1, 2, 3]);
        WriteFile(Path.Combine(folder, "nested", "deeper", "b.txt"), [4, 5]);
        Directory.CreateDirectory(Path.Combine(folder, "empty"));
        var store = new AttachmentStore(Project);

        // Act: stage the folder
        var result = store.StageForSend([new FolderChip(folder)]);

        // Assert: the copy has the same tree and bytes, and the result counts files and bytes
        Assert.True(result.Succeeded);
        var staged = Assert.Single(result.Attachments);
        Assert.Equal("attachments/photos", staged.ProjectRelativePath);
        Assert.Equal(AttachmentKind.Folder, staged.Kind);
        Assert.Equal(2, staged.FileCount);
        Assert.Equal(5, staged.SizeBytes);
        var copy = Path.Combine(Project, "attachments", "photos");
        Assert.Equal(new byte[] { 1, 2, 3 }, await File.ReadAllBytesAsync(Path.Combine(copy, "a.txt"), TestContext.Current.CancellationToken));
        Assert.Equal(new byte[] { 4, 5 }, await File.ReadAllBytesAsync(Path.Combine(copy, "nested", "deeper", "b.txt"), TestContext.Current.CancellationToken));
        Assert.True(Directory.Exists(Path.Combine(copy, "empty")));
        Assert.True(Directory.Exists(folder));
    }

    /// <summary>
    ///     Test that a link inside a dropped folder is not followed and not copied, so it cannot pull in files from
    ///     elsewhere.
    /// </summary>
    [Fact]
    public void AttachmentStore_StageForSend_FolderWithLink_SkipsTheLinkAndCopiesTheRest()
    {
        // Arrange: a folder with a file and a link to another folder that holds a secret
        var secret = WriteFile(Path.Combine(External, "secret", "password.txt"), [7]);
        var folder = Path.Combine(External, "dropped");
        WriteFile(Path.Combine(folder, "plain.txt"), [1]);
        CreateFolderLink(Path.Combine(folder, "link"), Path.GetDirectoryName(secret)!);
        var store = new AttachmentStore(Project);

        // Act: stage the folder
        var result = store.StageForSend([new FolderChip(folder)]);

        // Assert: only the plain file was copied and one link was skipped and counted
        Assert.True(result.Succeeded);
        var staged = Assert.Single(result.Attachments);
        Assert.Equal(1, staged.SkippedLinks);
        Assert.Equal(1, staged.FileCount);
        var copy = Path.Combine(Project, "attachments", "dropped");
        Assert.Equal(["plain.txt"], Directory.GetFileSystemEntries(copy).Select(entry => Path.GetFileName(entry)));
        Assert.True(File.Exists(secret));
    }

    /// <summary>
    ///     Test that files that are in the project already are referred to where they are and nothing is copied.
    /// </summary>
    [Fact]
    public void AttachmentStore_StageForSend_ItemsInsideProject_AreReferencedInPlace()
    {
        // Arrange: a file and a folder in the project
        var file = WriteFile(Path.Combine(Project, "drawings", "flow.mmd"), [1, 2, 3]);
        var folder = Directory.CreateDirectory(Path.Combine(Project, "assets")).FullName;
        var store = new AttachmentStore(Project);

        // Act: stage both
        var result = store.StageForSend([new FileChip(file), new FolderChip(folder)]);

        // Assert: both are referred to by their project-relative paths, not copied, and no attachments folder was made
        Assert.True(result.Succeeded);
        Assert.Equal(["drawings/flow.mmd", "assets"], result.Attachments.Select(item => item.ProjectRelativePath));
        Assert.All(result.Attachments, item => Assert.False(item.WasCopied));
        Assert.Equal(3, result.Attachments[0].SizeBytes);
        Assert.False(Directory.Exists(store.AttachmentsFolder));
    }

    /// <summary>
    ///     Test that a pasted picture is moved from the draft folder into the attachments folder when the message is sent.
    /// </summary>
    [Fact]
    public void AttachmentStore_StageForSend_PastedPicture_IsMovedIntoAttachments()
    {
        // Arrange: a pasted picture and its chip
        var store = new AttachmentStore(Project, timeProvider: new FixedTimeProvider());
        var pasted = store.SavePastedImage(PngBytes);

        // Act: stage the message
        var result = store.StageForSend([new ImageChip(pasted, isPasted: true)]);

        // Assert: the picture is in attachments under its name and the draft file is gone
        Assert.True(result.Succeeded);
        Assert.Equal("attachments/paste-20261120-093015-1.png", Assert.Single(result.Attachments).ProjectRelativePath);
        Assert.True(File.Exists(Path.Combine(Project, "attachments", "paste-20261120-093015-1.png")));
        Assert.False(File.Exists(pasted));
    }

    /// <summary>
    ///     Test that a message with more than the allowed number of files is refused, naming the chip, and that nothing
    ///     is copied, not even the attachments before it.
    /// </summary>
    [Fact]
    public void AttachmentStore_StageForSend_TooManyFiles_FailsNamingChipAndCopiesNothing()
    {
        // Arrange: one small file and a folder with one file too many
        var small = WriteFile(Path.Combine(External, "small.txt"), [1]);
        var folder = Path.Combine(External, "many");
        for (var index = 0; index <= AttachmentStore.MaximumFiles; index++)
        {
            WriteFile(Path.Combine(folder, $"f{index}.txt"), [1]);
        }

        var store = new AttachmentStore(Project);
        var chip = new FolderChip(folder);

        // Act: stage both chips
        var result = store.StageForSend([new FileChip(small), chip]);

        // Assert: the failure names the folder chip and the limit, and the project has no attachments folder
        Assert.False(result.Succeeded);
        Assert.Equal(chip, result.Failure!.Chip);
        Assert.Contains("'many'", result.Failure.Reason, StringComparison.Ordinal);
        Assert.Contains("500", result.Failure.Reason, StringComparison.Ordinal);
        Assert.Empty(result.Attachments);
        Assert.False(Directory.Exists(store.AttachmentsFolder));
    }

    /// <summary>
    ///     Test that a file larger than the limit of one message is refused, naming the chip, and nothing is copied.
    /// </summary>
    [Fact]
    public void AttachmentStore_StageForSend_FileOverSizeLimit_FailsNamingChipAndCopiesNothing()
    {
        // Arrange: a file one byte over the limit
        var big = Path.Combine(External, "huge.bin");
        Directory.CreateDirectory(External);
        using (var stream = new FileStream(big, FileMode.CreateNew))
        {
            stream.SetLength(AttachmentStore.MaximumBytes + 1);
        }

        var store = new AttachmentStore(Project);
        var chip = new FileChip(big);

        // Act: stage the chip
        var result = store.StageForSend([chip]);

        // Assert: refused with the name and the limit; nothing was created in the project
        Assert.False(result.Succeeded);
        Assert.Equal(chip, result.Failure!.Chip);
        Assert.Contains("'huge.bin'", result.Failure.Reason, StringComparison.Ordinal);
        Assert.Contains("200 MB", result.Failure.Reason, StringComparison.Ordinal);
        Assert.False(Directory.Exists(store.AttachmentsFolder));
    }

    /// <summary>
    ///     Test that a folder with more folders than allowed is refused.
    /// </summary>
    [Fact]
    public void AttachmentStore_StageForSend_TooManyFolders_Fails()
    {
        // Arrange: a folder with one folder too many
        var folder = Path.Combine(External, "tree");
        for (var index = 0; index <= AttachmentStore.MaximumFolders; index++)
        {
            Directory.CreateDirectory(Path.Combine(folder, $"d{index}"));
        }

        var store = new AttachmentStore(Project);

        // Act: stage the folder
        var result = store.StageForSend([new FolderChip(folder)]);

        // Assert: refused with the limit
        Assert.False(result.Succeeded);
        Assert.Contains("folders", result.Failure!.Reason, StringComparison.Ordinal);
        Assert.False(Directory.Exists(store.AttachmentsFolder));
    }

    /// <summary>
    ///     Test that when one file cannot be copied everything the call created is removed, so nothing is left behind,
    ///     and the failure names the file.
    /// </summary>
    [Fact]
    public void AttachmentStore_StageForSend_FileCannotBeRead_RemovesWhatWasCopiedAndNamesChip()
    {
        // Arrange: a file that can be copied and a file that another program holds open exclusively
        var good = WriteFile(Path.Combine(External, "good.txt"), [1]);
        var locked = WriteFile(Path.Combine(External, "locked.txt"), [2]);
        var store = new AttachmentStore(Project);
        var chip = new FileChip(locked);
        using var hold = new FileStream(locked, FileMode.Open, FileAccess.Read, FileShare.None);

        // Act: stage both
        var result = store.StageForSend([new FileChip(good), chip]);

        // Assert: the failure names the locked file, and neither the first copy nor the attachments folder is left
        Assert.False(result.Succeeded);
        Assert.Equal(chip, result.Failure!.Chip);
        Assert.Contains("'locked.txt'", result.Failure.Reason, StringComparison.Ordinal);
        Assert.False(Directory.Exists(store.AttachmentsFolder));
        Assert.True(File.Exists(good));
    }

    /// <summary>
    ///     Test that the copy stays inside the project: only the attachments folder is added to the project, and the
    ///     files that were attached are not changed or removed.
    /// </summary>
    [Fact]
    public async Task AttachmentStore_StageForSend_ManyKinds_OnlyAttachmentsFolderIsAddedToProject()
    {
        // Arrange: a file, a folder, and a picture from outside, and a project with one file
        WriteFile(Path.Combine(Project, "flow.mmd"), [1]);
        var file = WriteFile(Path.Combine(External, "..data", "notes.txt"), [1, 2]);
        var folder = Path.Combine(External, "set");
        var inner = WriteFile(Path.Combine(folder, "x.svg"), [3]);
        var picture = WriteFile(Path.Combine(External, "shot.png"), PngBytes);
        var before = Snapshot(_root, except: Path.Combine(Project, "attachments"));
        var store = new AttachmentStore(Project);

        // Act: stage them
        var result = store.StageForSend([new FileChip(file), new FolderChip(folder), new ImageChip(picture)]);

        // Assert: everything new is below attachments, and the sources are byte for byte what they were
        Assert.True(result.Succeeded);
        Assert.Equal(before, Snapshot(_root, except: Path.Combine(Project, "attachments")));
        Assert.Equal(
            ["attachments/notes.txt", "attachments/set", "attachments/shot.png"],
            result.Attachments.Select(item => item.ProjectRelativePath));
        Assert.Equal(new byte[] { 3 }, await File.ReadAllBytesAsync(inner, TestContext.Current.CancellationToken));
        Assert.Equal(PngBytes, await File.ReadAllBytesAsync(picture, TestContext.Current.CancellationToken));
    }

    /// <summary>
    ///     Test that things that must not be attached are refused with a reason that names the chip: a path that is not
    ///     safe, a file that is gone, a folder that holds the project, a file in the project's private folder, and a
    ///     picture that claims to be pasted but is not one of the project's.
    /// </summary>
    [Fact]
    public void AttachmentStore_StageForSend_ItemsThatCannotBeAttached_FailWithReasonNamingChip()
    {
        // Arrange: a store, a file in the private data folder, a picture outside the draft folder, and a folder
        var store = new AttachmentStore(Project);
        var priv = WriteFile(Path.Combine(Project, ProjectDocument.DataFolderName, "settings.json"), [1]);
        var elsewhere = WriteFile(Path.Combine(External, "claimed.png"), PngBytes);
        var unsafePath = Path.Combine(External, "sub", "..", "claimed.png");
        var cases = new (AttachmentChip Chip, string Expected)[]
        {
            (new FileChip(unsafePath), "'..'"),
            (new FileChip(Path.Combine(External, "gone.txt")), "no longer exists"),
            (new FolderChip(Path.Combine(External, "gone")), "no longer exists"),
            (new FileChip(External), "is a folder"),
            (new FolderChip(_root), "contains the project folder"),
            (new FileChip(priv), "private data folder"),
            (new ImageChip(elsewhere, isPasted: true), "not a picture that was pasted"),
            (new FolderChip(Project), "contains the project folder")
        };

        foreach (var (chip, expected) in cases)
        {
            // Act: stage the one chip
            var result = store.StageForSend([chip]);

            // Assert: refused, naming the chip, with the reason
            Assert.False(result.Succeeded);
            Assert.Equal(chip, result.Failure!.Chip);
            Assert.Contains(expected, result.Failure.Reason, StringComparison.Ordinal);
        }

        Assert.False(Directory.Exists(store.AttachmentsFolder));
    }

    /// <summary>
    ///     Test that staging nothing succeeds and creates nothing.
    /// </summary>
    [Fact]
    public void AttachmentStore_StageForSend_NoChips_SucceedsAndCreatesNothing()
    {
        // Arrange: a store on a project folder
        var store = new AttachmentStore(Project);

        // Act: stage no chips
        var result = store.StageForSend([]);

        // Assert: success without attachments or folders
        Assert.True(result.Succeeded);
        Assert.Empty(result.Attachments);
        Assert.False(Directory.Exists(store.AttachmentsFolder));
    }

    /// <summary>
    ///     Test that names that come from outside are made safe: characters a name cannot hold, device names, trailing
    ///     dots and spaces, empty names, and long names whose extension stays.
    /// </summary>
    [Theory]
    [InlineData("a:b*c?.txt", "a_b_c_.txt")]
    [InlineData("pipe|name<1>.png", "pipe_name_1_.png")]
    [InlineData("aux.txt", "_aux.txt")]
    [InlineData("CON", "_CON")]
    [InlineData("com1.log", "_com1.log")]
    [InlineData("nul ", "_nul")]
    [InlineData("report. ", "report")]
    [InlineData("  spaced.txt", "spaced.txt")]
    [InlineData("...", "attachment")]
    [InlineData("", "attachment")]
    [InlineData("..data", "..data")]
    [InlineData("console.txt", "console.txt")]
    public void AttachmentNames_SanitizeName_UnsafeNames_BecomeSafeNames(string name, string expected)
    {
        // Arrange: a name from outside (the theory data)

        // Act: make it safe
        var safe = AttachmentNames.SanitizeName(name);

        // Assert: the safe name is as expected
        Assert.Equal(expected, safe);
    }

    /// <summary>
    ///     Test that a long name is shortened in its stem and keeps its extension.
    /// </summary>
    [Fact]
    public void AttachmentNames_SanitizeName_LongName_IsShortenedKeepingExtension()
    {
        // Arrange: a name of 300 characters with an extension
        var name = new string('x', 300) + ".png";

        // Act: make it safe
        var safe = AttachmentNames.SanitizeName(name);

        // Assert: it has the longest allowed length and still ends with the extension
        Assert.Equal(AttachmentNames.MaximumNameLength, safe.Length);
        Assert.EndsWith(".png", safe, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Test that names that become equal inside a folder are made different with numbers, and that the numbering
    ///     ignores case.
    /// </summary>
    [Fact]
    public void AttachmentNames_UniqueName_NamesInASet_GetNumbers()
    {
        // Arrange: a set of names that holds one name
        var taken = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // Act: ask for the same name three times, in different case
        var first = AttachmentNames.UniqueName(taken, "photo.png");
        var second = AttachmentNames.UniqueName(taken, "PHOTO.png");
        var third = AttachmentNames.UniqueName(taken, "photo.png");

        // Assert: numbers before the extension
        Assert.Equal(["photo.png", "PHOTO (2).png", "photo (3).png"], [first, second, third]);
    }

    /// <summary>
    ///     Test that a store refuses a project folder that is not a complete path.
    /// </summary>
    [Fact]
    public void AttachmentStore_New_InvalidArguments_Throw()
    {
        // Arrange, Act, and Assert: a relative folder, a blank one, and null arguments are refused
        Assert.Throws<ArgumentException>(() => new AttachmentStore(Path.Combine("relative", "project")));
        Assert.Throws<ArgumentException>(() => new AttachmentStore(" "));
        var store = new AttachmentStore(Project);
        Assert.Throws<ArgumentNullException>(() => store.StageForSend(null!));
        Assert.Throws<ArgumentNullException>(() => store.RemoveLeftovers(null!));
        Assert.Throws<ArgumentException>(() => store.StageForSend([null!]));
    }

    /// <summary>
    ///     Test that the file name pattern of pasted pictures is the one that leftovers are removed by.
    /// </summary>
    [Fact]
    public void AttachmentStore_SavePastedImage_Name_MatchesDocumentedPattern()
    {
        // Arrange: a store with the real clock
        var store = new AttachmentStore(Project);

        // Act: save a picture
        var name = Path.GetFileName(store.SavePastedImage(PngBytes));

        // Assert: the name has the documented form
        Assert.Matches(new Regex(@"^paste-\d{8}-\d{6}-\d+\.png$"), name);
    }

    /// <summary>
    ///     Writes a file, creating its folders.
    /// </summary>
    /// <param name="path">The full path of the file.</param>
    /// <param name="bytes">The content.</param>
    /// <returns>The path.</returns>
    private static string WriteFile(string path, byte[] bytes)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllBytes(path, bytes);
        return path;
    }

    /// <summary>
    ///     Lists every file and folder below a folder with the size of files, so that two moments can be compared.
    /// </summary>
    /// <param name="folder">The folder.</param>
    /// <param name="except">A folder whose content is left out.</param>
    /// <returns>The sorted description of the tree.</returns>
    private static List<string> Snapshot(string folder, string except) =>
        [.. Directory.EnumerateFileSystemEntries(folder, "*", SearchOption.AllDirectories)
            .Where(entry => !entry.StartsWith(except, StringComparison.Ordinal))
            .Select(entry => File.Exists(entry) ? $"{entry}|{new FileInfo(entry).Length}" : entry)
            .Order(StringComparer.Ordinal)];

    /// <summary>
    ///     Creates a link to a folder: a junction on Windows, which needs no privilege, and a symbolic link elsewhere.
    /// </summary>
    /// <param name="link">The path of the link to create.</param>
    /// <param name="target">The folder the link leads to.</param>
    private void CreateFolderLink(string link, string target)
    {
        _links.Add(link);
        if (!OperatingSystem.IsWindows())
        {
            Directory.CreateSymbolicLink(link, target);
            return;
        }

        using var process = Process.Start(new ProcessStartInfo(
            "powershell.exe",
            $"-NoProfile -NonInteractive -Command \"New-Item -ItemType Junction -Path '{link}' -Target '{target}' | Out-Null\"")
        {
            CreateNoWindow = true,
            UseShellExecute = false,
            RedirectStandardOutput = true
        })!;
        process.StandardOutput.ReadToEnd();
        process.WaitForExit();
        Assert.Equal(0, process.ExitCode);
    }

    /// <summary>
    ///     A clock that stands still, so that names with the time are known.
    /// </summary>
    private sealed class FixedTimeProvider : TimeProvider
    {
        /// <inheritdoc />
        public override DateTimeOffset GetUtcNow() => new(2026, 11, 20, 9, 30, 15, TimeSpan.Zero);
    }
}
