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

using System.IO.Compression;
using System.Text;
using DemaConsulting.CanvasPilot.StyleSubsystem;

namespace DemaConsulting.CanvasPilot.Tests;

/// <summary>
///     Unit tests for <see cref="StyleKitArchive" />: exporting a kit, importing it again, and refusing the archives that
///     are made to write outside the target, to exhaust the disk, or to put a broken kit in a folder. The archives are made
///     by the tests with entry names that no tool would write.
/// </summary>
public sealed class StyleKitArchiveTests : IDisposable
{
    /// <summary>
    ///     A valid style file for the kit <c>sample</c>.
    /// </summary>
    private static readonly string SampleStyle = StyleKitSerializer.Write(StyleKitPlaces.Kit("sample", "From the zip", "Sample"));

    /// <summary>
    ///     Temporary folder removed on disposal; it holds the archives in <c>zips</c> and the targets in <c>target</c>.
    /// </summary>
    private readonly string _root = Directory.CreateTempSubdirectory("canvaspilot-archive-").FullName;

    /// <summary>
    ///     Gets the folder that the imports write to.
    /// </summary>
    private string Target => Path.Combine(_root, "target");

    /// <summary>
    ///     Gets the folder that holds the archives.
    /// </summary>
    private string Zips => Directory.CreateDirectory(Path.Combine(_root, "zips")).FullName;

    /// <inheritdoc />
    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    /// <summary>
    ///     Validates that a kit that is exported and imported again is the same kit: the same canonical style file, and the
    ///     same bytes in each example, which include bytes that are not text.
    /// </summary>
    [Fact]
    public void StyleKitArchive_ExportThenImport_KitWithExamples_RoundTripsByteForByte()
    {
        // Arrange: a kit with a caption on one of two examples, one of them binary
        var kit = StyleKitPlaces.Kit("travel", "A kit that travels", "Travel") with
        {
            Examples = [new StyleExample("examples/card.svg", string.Empty, "A card.")]
        };
        var source = StyleKitPlaces.WriteKit(Path.Combine(_root, "source"), kit, "plain.mmd");
        var binary = Enumerable.Range(0, 5000).Select(i => (byte)(i * 31 % 251)).ToArray();
        File.WriteAllBytes(Path.Combine(source, "examples", "card.svg"), binary);
        var entry = Entry(source);
        var zip = Path.Combine(Zips, "travel.zip");

        // Act
        StyleKitArchive.Export(entry, zip);
        var result = StyleKitArchive.Import(zip, Target, StyleImportConflict.Refuse);

        // Assert: the entries of the archive, the kit, and the files
        using (var archive = ZipFile.OpenRead(zip))
        {
            Assert.Equal(["examples/card.svg", "examples/plain.mmd", "style.json"], archive.Entries.Select(e => e.FullName).Order());
        }

        Assert.True(result.Succeeded, result.Error);
        Assert.Equal(Path.Combine(Target, "travel"), result.FolderPath);
        Assert.Equal(StyleKitSerializer.Write(entry.Kit), StyleKitSerializer.Write(result.Kit!));
        Assert.Equal(binary, File.ReadAllBytes(Path.Combine(Target, "travel", "examples", "card.svg")));
        Assert.Equal("plain.mmd", File.ReadAllText(Path.Combine(Target, "travel", "examples", "plain.mmd")));
        Assert.Equal("A card.", result.Kit!.Examples.Single(e => e.RelativePath == "examples/card.svg").Caption);
        Assert.Empty(result.Issues);
        Assert.Equal(["travel"], Directory.GetFileSystemEntries(Target).Select(Path.GetFileName));
    }

    /// <summary>
    ///     Validates that an archive whose files are all in one folder is accepted and that folder entries are ignored.
    /// </summary>
    [Fact]
    public void StyleKitArchive_Import_ArchiveWithOneWrapperFolder_IsAccepted()
    {
        // Arrange
        var zip = MakeZip(("wrapper/", []), ("wrapper/examples/", []), ("wrapper/style.json", Bytes(SampleStyle)), ("wrapper/examples/a.svg", Bytes("<svg/>")));

        // Act
        var result = StyleKitArchive.Import(zip, Target, StyleImportConflict.Refuse);

        // Assert
        Assert.True(result.Succeeded, result.Error);
        Assert.Equal("sample", result.Kit!.Id);
        Assert.True(File.Exists(Path.Combine(Target, "sample", "examples", "a.svg")));
        Assert.False(Directory.Exists(Path.Combine(Target, "wrapper")));
    }

    /// <summary>
    ///     Validates that an archive with an entry whose name could be used to write outside the target, or whose name is
    ///     not usable as a file name, is refused as a whole, and that nothing is created anywhere: not outside the target
    ///     and not inside it.
    /// </summary>
    /// <param name="badName">The name of the entry.</param>
    [Theory]
    [InlineData("../evil.txt")]
    [InlineData("examples/../../evil.png")]
    [InlineData("..\\evil.txt")]
    [InlineData("examples\\..\\..\\evil.png")]
    [InlineData("C:\\evil.txt")]
    [InlineData("C:evil.txt")]
    [InlineData("/etc/evil")]
    [InlineData("examples/a:b.svg")]
    [InlineData("examples/a\0b.svg")]
    [InlineData("examples/what?.svg")]
    [InlineData("examples/CON.svg")]
    [InlineData("examples/trailing.")]
    [InlineData("examples/./a.svg")]
    [InlineData("examples//a.svg")]
    [InlineData("//server/share/evil.txt")]
    [InlineData("examples/sub/deep.svg")]
    [InlineData("readme.txt")]
    [InlineData("Style.json")]
    [InlineData("styles/clean/style.json")]
    public void StyleKitArchive_Import_UnsafeOrForeignEntryName_IsRefusedAndNothingIsWritten(string badName)
    {
        // Arrange: a good kit and one more entry with a bad name
        var zip = MakeZip(("style.json", Bytes(SampleStyle)), (badName, Bytes("evil")));

        // Act
        var result = StyleKitArchive.Import(zip, Target, StyleImportConflict.Refuse);

        // Assert
        Assert.False(result.Succeeded);
        Assert.Null(result.Kit);
        Assert.Contains("nothing was imported", result.Error, StringComparison.Ordinal);
        AssertNothingWritten();
    }

    /// <summary>
    ///     Validates that a very long name is refused.
    /// </summary>
    [Fact]
    public void StyleKitArchive_Import_NameOfMoreThan200Characters_IsRefused()
    {
        // Arrange
        var zip = MakeZip(("style.json", Bytes(SampleStyle)), ("examples/" + new string('a', 250) + ".svg", Bytes("x")));

        // Act
        var result = StyleKitArchive.Import(zip, Target, StyleImportConflict.Refuse);

        // Assert
        Assert.False(result.Succeeded);
        Assert.Contains("more than 200 characters", result.Error, StringComparison.Ordinal);
        AssertNothingWritten();
    }

    /// <summary>
    ///     Validates that an entry that was made as a symbolic link is refused.
    /// </summary>
    [Fact]
    public void StyleKitArchive_Import_LinkEntry_IsRefused()
    {
        // Arrange: an entry with the Unix mode of a link
        var path = Path.Combine(Zips, "link.zip");
        using (var stream = File.Create(path))
        using (var archive = new ZipArchive(stream, ZipArchiveMode.Create))
        {
            WriteEntry(archive, "style.json", Bytes(SampleStyle));
            var link = archive.CreateEntry("examples/link.svg");
            link.ExternalAttributes = unchecked((int)0xA1FF0000);
            using var writer = new StreamWriter(link.Open());
            writer.Write("../../secret.txt");
        }

        // Act
        var result = StyleKitArchive.Import(path, Target, StyleImportConflict.Refuse);

        // Assert
        Assert.False(result.Succeeded);
        Assert.Contains("is a link", result.Error, StringComparison.Ordinal);
        AssertNothingWritten();
    }

    /// <summary>
    ///     Validates that the same file name twice in an archive, also in another case, is refused.
    /// </summary>
    [Fact]
    public void StyleKitArchive_Import_SameNameTwice_IsRefused()
    {
        // Arrange
        var zip = MakeZip(("style.json", Bytes(SampleStyle)), ("examples/a.svg", Bytes("1")), ("examples/A.svg", Bytes("2")));

        // Act
        var result = StyleKitArchive.Import(zip, Target, StyleImportConflict.Refuse);

        // Assert
        Assert.False(result.Succeeded);
        Assert.Contains("more than once", result.Error, StringComparison.Ordinal);
        AssertNothingWritten();
    }

    /// <summary>
    ///     Validates that an archive whose header claims a small file that is really large is refused and cleaned up: the
    ///     framework stops reading at the size of the header, so the file that is copied is short, its checksum is not the
    ///     one of the header, and the import fails; at no point is more than the size of the header written.
    /// </summary>
    [Fact]
    public void StyleKitArchive_Import_HeaderThatUnderstatesTheSize_StopsAtTheLimitAndCleansUp()
    {
        // Arrange: a file of 20 MiB of zeros that compresses to almost nothing, with both sizes in the headers set to 10
        var bytes = MakeZipBytes(("style.json", Bytes(SampleStyle)), ("examples/big.svg", new byte[20 * 1024 * 1024]));
        PatchSizes(bytes, "examples/big.svg", 10);
        var zip = Path.Combine(Zips, "lying.zip");
        File.WriteAllBytes(zip, bytes);

        // Act
        var result = StyleKitArchive.Import(zip, Target, StyleImportConflict.Refuse);

        // Assert: refused, and neither the kit nor the hidden folder nor the big file is left
        Assert.False(result.Succeeded);
        Assert.Contains("does not match its header", result.Error, StringComparison.Ordinal);
        AssertNothingWritten();
    }

    /// <summary>
    ///     Validates that a file that the header says is larger than the limit, and files whose sizes together are larger
    ///     than the total limit, are refused before anything is written.
    /// </summary>
    [Fact]
    public void StyleKitArchive_Import_HonestSizesOverTheLimits_AreRefusedBeforeAnythingIsWritten()
    {
        // Arrange: one entry of 17 MiB, and five of 15 MiB, all zeros
        var large = MakeZip(("style.json", Bytes(SampleStyle)), ("examples/big.svg", new byte[17 * 1024 * 1024]));
        var many = MakeZip(
            ("style.json", Bytes(SampleStyle)),
            ("examples/a.svg", new byte[15 * 1024 * 1024]),
            ("examples/b.svg", new byte[15 * 1024 * 1024]),
            ("examples/c.svg", new byte[15 * 1024 * 1024]),
            ("examples/d.svg", new byte[15 * 1024 * 1024]),
            ("examples/e.svg", new byte[15 * 1024 * 1024]));

        // Act
        var tooLarge = StyleKitArchive.Import(large, Target, StyleImportConflict.Refuse);
        var tooMuch = StyleKitArchive.Import(many, Target, StyleImportConflict.Refuse);

        // Assert
        Assert.Contains("the limit is 16777216", tooLarge.Error, StringComparison.Ordinal);
        Assert.Contains("together; the limit is 67108864", tooMuch.Error, StringComparison.Ordinal);
        AssertNothingWritten();
    }

    /// <summary>
    ///     Validates that an archive with more entries than the limit, and one with more example files than a kit may have,
    ///     are refused.
    /// </summary>
    [Fact]
    public void StyleKitArchive_Import_TooManyEntriesOrExamples_AreRefused()
    {
        // Arrange: 101 entries, and 25 example files
        var entries = new List<(string, byte[])> { ("style.json", Bytes(SampleStyle)) };
        entries.AddRange(Enumerable.Range(0, 100).Select(i => ($"examples/f{i:000}.txt", Bytes("x"))));
        var tooMany = MakeZip([.. entries]);
        var examples = MakeZip([("style.json", Bytes(SampleStyle)), .. Enumerable.Range(0, 25).Select(i => ($"examples/f{i:00}.txt", Bytes("x")))]);

        // Act
        var manyResult = StyleKitArchive.Import(tooMany, Target, StyleImportConflict.Refuse);
        var examplesResult = StyleKitArchive.Import(examples, Target, StyleImportConflict.Refuse);

        // Assert
        Assert.Equal("The zip file has 101 entries; the limit is 100.", manyResult.Error);
        Assert.Equal("The zip file has 25 example files; the limit is 24.", examplesResult.Error);
        AssertNothingWritten();
    }

    /// <summary>
    ///     Validates that an archive with no style file is refused, and that a style file with errors is refused with the
    ///     list of its issues and leaves nothing behind.
    /// </summary>
    [Fact]
    public void StyleKitArchive_Import_NoStyleFileOrInvalidStyleFile_IsRefusedWithTheReason()
    {
        // Arrange
        var noStyle = MakeZip(("examples/a.svg", Bytes("<svg/>")));
        var invalid = MakeZip(("style.json", Bytes(SampleStyle.Replace("#2563EB", "red", StringComparison.Ordinal))), ("examples/a.svg", Bytes("<svg/>")));
        var notJson = MakeZip(("style.json", Bytes("{ not json")));

        // Act
        var noStyleResult = StyleKitArchive.Import(noStyle, Target, StyleImportConflict.Refuse);
        var invalidResult = StyleKitArchive.Import(invalid, Target, StyleImportConflict.Refuse);
        var notJsonResult = StyleKitArchive.Import(notJson, Target, StyleImportConflict.Refuse);

        // Assert
        Assert.Contains("has no style.json", noStyleResult.Error, StringComparison.Ordinal);
        Assert.Contains("palette.primary: 'red' is not a color; use #RRGGBB.", invalidResult.Error, StringComparison.Ordinal);
        Assert.Contains(invalidResult.Issues, i => i.Path == "palette.primary" && i.IsError);
        Assert.Contains("The style file in the zip file has errors", notJsonResult.Error, StringComparison.Ordinal);
        AssertNothingWritten();
    }

    /// <summary>
    ///     Validates that a file that is not a zip file, and a file that does not exist, are refused with a message and not
    ///     an exception.
    /// </summary>
    [Fact]
    public void StyleKitArchive_Import_NotAZipOrMissingFile_IsRefusedWithAMessage()
    {
        // Arrange
        var text = Path.Combine(Zips, "text.zip");
        File.WriteAllText(text, "this is not a zip file");

        // Act
        var notZip = StyleKitArchive.Import(text, Target, StyleImportConflict.Refuse);
        var missing = StyleKitArchive.Import(Path.Combine(Zips, "missing.zip"), Target, StyleImportConflict.Refuse);

        // Assert
        Assert.False(notZip.Succeeded);
        Assert.Contains("not a usable zip file", notZip.Error, StringComparison.Ordinal);
        Assert.False(missing.Succeeded);
        Assert.NotNull(missing.Error);
        AssertNothingWritten();
    }

    /// <summary>
    ///     Validates the three ways of dealing with a kit that has the id of one that is there: refuse leaves everything
    ///     as it was, replace puts the new kit in place of the old with none of its files, and keep both gives the new kit
    ///     a number in its id and its name each time.
    /// </summary>
    [Fact]
    public void StyleKitArchive_Import_IdAlreadyThere_RefuseReplaceAndKeepBoth()
    {
        // Arrange: a kit with this id is in the target with an example file, and an archive has a kit with the same id
        var existing = StyleKitPlaces.WriteKit(Target, StyleKitPlaces.Kit("sample", "The old kit"), "old.svg");
        var zip = MakeZip(("style.json", Bytes(SampleStyle)), ("examples/new.svg", Bytes("<svg/>")));

        // Act
        var refused = StyleKitArchive.Import(zip, Target, StyleImportConflict.Refuse);
        var refusedState = Directory.GetFileSystemEntries(Target).Select(Path.GetFileName).Order().ToList();
        var oldText = File.ReadAllText(Path.Combine(existing, "style.json"));
        var oldDescription = StyleKitSerializer.ReadFolder(existing).Kit!.Description;
        var both = StyleKitArchive.Import(zip, Target, StyleImportConflict.KeepBoth);
        var bothAgain = StyleKitArchive.Import(zip, Target, StyleImportConflict.KeepBoth);
        var replaced = StyleKitArchive.Import(zip, Target, StyleImportConflict.Replace);

        // Assert: refuse changed nothing
        Assert.Contains("already has a kit with the id 'sample'", refused.Error, StringComparison.Ordinal);
        Assert.Equal(["sample"], refusedState);
        Assert.Equal("The old kit", oldDescription);
        Assert.Contains("The old kit", oldText, StringComparison.Ordinal);

        // Assert: keep both gives numbers, and the first copy has the new example only
        Assert.True(both.Succeeded, both.Error);
        Assert.Equal("sample-2", both.Kit!.Id);
        Assert.Equal("Sample (2)", both.Kit.Name);
        Assert.Equal("sample-3", bothAgain.Kit!.Id);
        Assert.Equal("Sample (3)", bothAgain.Kit.Name);
        Assert.Equal(["new.svg"], Directory.GetFiles(Path.Combine(Target, "sample-2", "examples")).Select(Path.GetFileName));
        Assert.Equal("sample-2", StyleKitSerializer.ReadFolder(Path.Combine(Target, "sample-2")).Kit!.Id);

        // Assert: replace puts the new kit where the old one was, without its files, and leaves no backup folder
        Assert.True(replaced.Succeeded, replaced.Error);
        Assert.Equal("From the zip", StyleKitSerializer.ReadFolder(existing).Kit!.Description);
        Assert.Equal(["new.svg"], Directory.GetFiles(Path.Combine(existing, "examples")).Select(Path.GetFileName));
        Assert.Equal(["sample", "sample-2", "sample-3"], Directory.GetFileSystemEntries(Target).Select(Path.GetFileName).Order());
    }

    /// <summary>
    ///     Validates that a long name of a kit still fits the limit when the number of a copy is added.
    /// </summary>
    [Fact]
    public void StyleKitArchive_Import_KeepBothWithLongName_StaysWithinTheNameLimit()
    {
        // Arrange: a kit whose name has the longest length that is allowed, with a copy already in the target
        var style = StyleKitSerializer.Write(StyleKitPlaces.Kit("long", string.Empty, new string('n', 60)));
        var zip = MakeZip(("style.json", Bytes(style)));
        StyleKitArchive.Import(zip, Target, StyleImportConflict.Refuse);

        // Act
        var result = StyleKitArchive.Import(zip, Target, StyleImportConflict.KeepBoth);

        // Assert
        Assert.True(result.Succeeded, result.Error);
        Assert.Equal(60, result.Kit!.Name.Length);
        Assert.EndsWith(" (2)", result.Kit.Name, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Validates that an export replaces a file that is there, that a kit whose example file is gone fails with an
    ///     I/O exception and leaves the file that was there as it was, and that missing arguments are refused.
    /// </summary>
    [Fact]
    public void StyleKitArchive_Export_ExistingFileMissingExampleAndNulls_ReplacesFailsAndRefuses()
    {
        // Arrange: a kit with one example and an archive that already exists
        var kit = StyleKitPlaces.Kit("travel") with { Examples = [new StyleExample("examples/a.svg", string.Empty, null)] };
        var folder = StyleKitPlaces.WriteKit(Path.Combine(_root, "source"), kit, "a.svg");
        var entry = Entry(folder);
        var zip = Path.Combine(Zips, "travel.zip");
        File.WriteAllText(zip, "old");

        // Act
        StyleKitArchive.Export(entry, zip);
        var exportedLength = new FileInfo(zip).Length;
        File.Delete(Path.Combine(folder, "examples", "a.svg"));
        var failure = Assert.ThrowsAny<IOException>(() => StyleKitArchive.Export(entry, zip));

        // Assert: the first export replaced the text, the second failed and left the first archive, and no temporary file remains
        Assert.True(exportedLength > 100);
        Assert.Equal(exportedLength, new FileInfo(zip).Length);
        Assert.NotNull(failure.Message);
        Assert.Equal(["travel.zip"], Directory.GetFiles(Zips).Select(Path.GetFileName));
        Assert.Throws<ArgumentNullException>(() => StyleKitArchive.Export(null!, zip));
        Assert.Throws<ArgumentException>(() => StyleKitArchive.Export(entry, " "));
        Assert.Throws<ArgumentException>(() => StyleKitArchive.Import(" ", Target, StyleImportConflict.Refuse));
        Assert.Throws<ArgumentException>(() => StyleKitArchive.Import(zip, "", StyleImportConflict.Refuse));
    }

    /// <summary>
    ///     Reads a kit folder as an entry of the user's place.
    /// </summary>
    /// <param name="folder">The folder.</param>
    /// <returns>The entry.</returns>
    private static StyleKitEntry Entry(string folder)
    {
        var kit = StyleKitSerializer.ReadFolder(folder).Kit!;
        return new StyleKitEntry(kit, StyleKitLocation.User, folder, null, false, false, null, []);
    }

    /// <summary>
    ///     Asserts that an import left nothing: the target folder has no entry (or is not there), and nothing named evil or
    ///     the big file exists anywhere below the temporary folder.
    /// </summary>
    private void AssertNothingWritten()
    {
        Assert.True(!Directory.Exists(Target) || Directory.GetFileSystemEntries(Target).Length == 0, "The target folder is not empty.");
        var files = Directory.GetFiles(_root, "*", SearchOption.AllDirectories).Select(f => Path.GetFileName(f)).ToList();
        Assert.DoesNotContain(files, f => f.StartsWith("evil", StringComparison.OrdinalIgnoreCase) || f.StartsWith("big", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(Directory.GetDirectories(_root, "*", SearchOption.AllDirectories), d => Path.GetFileName(d).EndsWith(".tmp", StringComparison.Ordinal));
    }

    /// <summary>
    ///     Gets the UTF-8 bytes of a text.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <returns>The bytes.</returns>
    private static byte[] Bytes(string text) => Encoding.UTF8.GetBytes(text);

    /// <summary>
    ///     Makes a zip file with entries of any name.
    /// </summary>
    /// <param name="entries">The names and contents; a name that ends with a slash is a folder.</param>
    /// <returns>The path of the file, which is in the folder of the archives.</returns>
    private string MakeZip(params (string Name, byte[] Data)[] entries)
    {
        var path = Path.Combine(Zips, $"{Guid.NewGuid():N}.zip");
        File.WriteAllBytes(path, MakeZipBytes(entries));
        return path;
    }

    /// <summary>
    ///     Makes the bytes of a zip file with entries of any name.
    /// </summary>
    /// <param name="entries">The names and contents.</param>
    /// <returns>The bytes.</returns>
    private static byte[] MakeZipBytes(params (string Name, byte[] Data)[] entries)
    {
        using var stream = new MemoryStream();
        using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var (name, data) in entries)
            {
                WriteEntry(archive, name, data);
            }
        }

        return stream.ToArray();
    }

    /// <summary>
    ///     Adds an entry to an archive.
    /// </summary>
    /// <param name="archive">The archive.</param>
    /// <param name="name">The name of the entry.</param>
    /// <param name="data">The content.</param>
    private static void WriteEntry(ZipArchive archive, string name, byte[] data)
    {
        var entry = archive.CreateEntry(name, CompressionLevel.Fastest);
        using var stream = entry.Open();
        stream.Write(data, 0, data.Length);
    }

    /// <summary>
    ///     Sets the uncompressed size in the local header and in the central directory entry of a named file, as an
    ///     archive that lies about its size does.
    /// </summary>
    /// <param name="zip">The bytes of the archive, changed in place.</param>
    /// <param name="name">The name of the entry.</param>
    /// <param name="size">The size to claim.</param>
    private static void PatchSizes(byte[] zip, string name, int size)
    {
        var nameBytes = Encoding.ASCII.GetBytes(name);
        var patched = 0;
        for (var i = 0; i + 46 + nameBytes.Length < zip.Length; i++)
        {
            // A local header is "PK 3 4" with the name at 30, and a central directory entry is "PK 1 2" with the name at 46
            var isLocal = zip[i] == 'P' && zip[i + 1] == 'K' && zip[i + 2] == 3 && zip[i + 3] == 4;
            var isCentral = zip[i] == 'P' && zip[i + 1] == 'K' && zip[i + 2] == 1 && zip[i + 3] == 2;
            if (!isLocal && !isCentral)
            {
                continue;
            }

            var nameAt = i + (isLocal ? 30 : 46);
            if (!zip.AsSpan(nameAt, nameBytes.Length).SequenceEqual(nameBytes))
            {
                continue;
            }

            BitConverter.TryWriteBytes(zip.AsSpan(i + (isLocal ? 22 : 24), 4), size);
            patched++;
        }

        Assert.Equal(2, patched);
    }
}
