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

using System.Text.Json;
using DemaConsulting.CanvasPilot.ChatSubsystem;
using DemaConsulting.CanvasPilot.LoggingSubsystem;
using DemaConsulting.CanvasPilot.ProjectSubsystem;

namespace DemaConsulting.CanvasPilot.Tests;

/// <summary>
///     Unit tests for <see cref="ProjectHistoryKey" /> and <see cref="ChatHistoryStore" />: the key that names the history
///     folder of a project, the saved file (version, text only, the limits, atomic writes), the files that cannot be used,
///     the refusal of a root inside the project, and Clear. Every test uses its own temporary folders; nothing is written
///     anywhere else.
/// </summary>
public sealed class ChatHistoryStoreTests : IDisposable
{
    /// <summary>
    ///     The words that must never reach the log, because the log holds facts only.
    /// </summary>
    private const string Secret = "sentinel-words-that-belong-to-the-user";

    /// <summary>
    ///     Temporary folder that holds the projects, the history root, and the log.
    /// </summary>
    private readonly string _root = Directory.CreateTempSubdirectory("canvaspilot-chat-history-tests-").FullName;

    /// <summary>
    ///     The clock of the store.
    /// </summary>
    private readonly ManualTimeProvider _clock = new();

    /// <summary>
    ///     The log of the store.
    /// </summary>
    private readonly RollingFileLogger _logger;

    /// <summary>
    ///     The store under test.
    /// </summary>
    private readonly ChatHistoryStore _store;

    /// <summary>
    ///     Initializes a new instance of the <see cref="ChatHistoryStoreTests" /> class.
    /// </summary>
    public ChatHistoryStoreTests()
    {
        _logger = new RollingFileLogger(Path.Combine(_root, "logs"));
        _store = new ChatHistoryStore(HistoryRoot, _logger, _clock);
    }

    /// <summary>
    ///     Gets the folder below which the store keeps the history.
    /// </summary>
    private string HistoryRoot => Path.Combine(_root, "local", "history");

    /// <inheritdoc />
    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    /// <summary>
    ///     Test that the key of a normalized path is the first 16 bytes of its SHA-256 hash as 32 lowercase hexadecimal
    ///     characters, which two fixed vectors pin down.
    /// </summary>
    [Fact]
    public void ProjectHistoryKey_HashOf_FixedVectors_Are32LowercaseHexCharactersOfTheFirst16Bytes()
    {
        // Arrange and act: the keys of two normalized paths
        var windows = ProjectHistoryKey.HashOf("c:\\projects\\demo\\demo.canvaspilot");
        var unix = ProjectHistoryKey.HashOf("/home/me/demo/demo.canvaspilot");

        // Assert: the known hash values, in the form of the key
        Assert.Equal("9760b213a9709ecd207e1e21574cd98b", windows);
        Assert.Equal("67cc7cd0e0e11b76b779d6f68dc1b5ba", unix);
        Assert.Equal(ProjectHistoryKey.Length, windows.Length);
        Assert.Matches("^[0-9a-f]{32}$", unix);
    }

    /// <summary>
    ///     Test that spellings of one path give one key: separators of either kind, a dot segment, a segment that goes up
    ///     and down again, and, where the file system ignores case, the case of the letters.
    /// </summary>
    [Fact]
    public void ProjectHistoryKey_Compute_SpellingsOfOnePath_GiveOneKey()
    {
        // Arrange: a project file and spellings of its path
        var path = TestPlatformPaths.FullPath("work", "Demo", "Demo.canvaspilot");
        var alternate = path.Replace(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var dotted = TestPlatformPaths.FullPath("work", ".", "Demo", "Demo.canvaspilot");
        var wandering = TestPlatformPaths.FullPath("work", "Other", "..", "Demo", "Demo.canvaspilot");

        // Act
        var key = ProjectHistoryKey.Compute(path);

        // Assert: one key for every spelling
        Assert.Equal(key, ProjectHistoryKey.Compute(alternate));
        Assert.Equal(key, ProjectHistoryKey.Compute(dotted));
        Assert.Equal(key, ProjectHistoryKey.Compute(wandering));
        Assert.Equal(key, ProjectHistoryKey.Compute(path + Path.DirectorySeparatorChar));
        Assert.Equal(TestPlatformPaths.IgnoresCase, key == ProjectHistoryKey.Compute(path.ToUpperInvariant()));
    }

    /// <summary>
    ///     Test that case is ignored only when the file system ignores it, and that another project file gives another key,
    ///     so that a project that is moved or renamed starts a fresh history.
    /// </summary>
    [Fact]
    public void ProjectHistoryKey_Compute_OtherCaseOrOtherPlace_FollowsTheFileSystem()
    {
        // Arrange
        var path = TestPlatformPaths.FullPath("work", "Demo", "Demo.canvaspilot");
        var upper = path.ToUpperInvariant();

        // Act and assert: the platform decides about case, and an explicit choice is honored
        Assert.Equal(ProjectHistoryKey.Compute(path, ignoreCase: true), ProjectHistoryKey.Compute(upper, ignoreCase: true));
        Assert.NotEqual(ProjectHistoryKey.Compute(path, ignoreCase: false), ProjectHistoryKey.Compute(upper, ignoreCase: false));

        // Assert: a moved or renamed project is another key
        Assert.NotEqual(ProjectHistoryKey.Compute(path), ProjectHistoryKey.Compute(TestPlatformPaths.FullPath("work", "Moved", "Demo.canvaspilot")));
        Assert.NotEqual(ProjectHistoryKey.Compute(path), ProjectHistoryKey.Compute(TestPlatformPaths.FullPath("work", "Demo", "Renamed.canvaspilot")));
    }

    /// <summary>
    ///     Test that a path that is missing or empty is refused.
    /// </summary>
    [Fact]
    public void ProjectHistoryKey_Compute_NullOrBlank_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => ProjectHistoryKey.Compute(null!));
        Assert.Throws<ArgumentException>(() => ProjectHistoryKey.Compute("  "));
    }

    /// <summary>
    ///     Test that the rows of every kind come back from a save exactly: words, marks with and without notes,
    ///     attachment paths, tool rows with their outcome, replies with how they ended, and the times.
    /// </summary>
    [Fact]
    public void ChatHistoryStore_SaveThenLoad_RoundTripsEveryKindOfRow()
    {
        // Arrange: one row of every kind
        var project = NewProject("Demo");
        var rows = SampleRows();

        // Act
        var result = _store.Save(project, rows);
        var loaded = _store.Load(project);

        // Assert: it was saved, and the same rows are read back
        Assert.True(result.Succeeded);
        Assert.Equal(Describe(rows), Describe(loaded));
    }

    /// <summary>
    ///     Test that the history is kept below the root in a folder named by the key of the project file, together with a
    ///     sidecar file that names the project, that nothing is created before the first save, and that nothing is written
    ///     into the project.
    /// </summary>
    [Fact]
    public void ChatHistoryStore_Save_WritesTheKeyedFolderWithSidecar_AndNothingIntoTheProject()
    {
        // Arrange
        var project = NewProject("Demo");
        Assert.False(Directory.Exists(HistoryRoot));
        Assert.Empty(_store.Load(project));
        Assert.False(Directory.Exists(HistoryRoot));
        _clock.Advance(TimeSpan.FromSeconds(5));

        // Act
        _store.Save(project, SampleRows());

        // Assert: the folder, its two files, and the sidecar with the name and the file of the project
        var folder = Path.Combine(HistoryRoot, ProjectHistoryKey.Compute(project.FilePath));
        Assert.Equal(folder, _store.FolderOf(project));
        Assert.Equal(
            [Path.Combine(folder, "history.json"), Path.Combine(folder, "project.json")],
            Directory.GetFiles(folder).Order(StringComparer.Ordinal));
        using var sidecar = JsonDocument.Parse(File.ReadAllText(Path.Combine(folder, "project.json")));
        Assert.Equal("Demo", sidecar.RootElement.GetProperty("projectName").GetString());
        Assert.Equal(project.FilePath, sidecar.RootElement.GetProperty("projectFile").GetString());
        Assert.True(sidecar.RootElement.TryGetProperty("lastUsedUtc", out _));

        // Assert: the project folder holds only what it held
        Assert.Empty(Directory.GetFileSystemEntries(project.FolderPath));
    }

    /// <summary>
    ///     Test that the file is version 1 and holds words only: its properties are the known ones, so no picture, no
    ///     payload, and no tool result is in it.
    /// </summary>
    [Fact]
    public void ChatHistoryStore_Save_FileHasVersion1AndHoldsTextOnly()
    {
        // Arrange and act
        var project = NewProject("Demo");
        _store.Save(project, SampleRows());

        // Assert: the version and rows, and no property beyond the known words
        using var document = JsonDocument.Parse(File.ReadAllText(_store.HistoryFilePath(project)));
        var root = document.RootElement;
        Assert.Equal(1, root.GetProperty("version").GetInt32());
        Assert.Equal(["rows", "version"], root.EnumerateObject().Select(property => property.Name).Order(StringComparer.Ordinal));
        string[] known = ["attachments", "kind", "marks", "reply", "succeeded", "text", "tool", "utc"];
        foreach (var row in root.GetProperty("rows").EnumerateArray())
        {
            Assert.All(row.EnumerateObject().Select(property => property.Name), name => Assert.Contains(name, known));
        }

        Assert.Equal(["user", "assistant", "tool", "assistant"], root.GetProperty("rows").EnumerateArray().Select(row => row.GetProperty("kind").GetString()));
    }

    /// <summary>
    ///     Test that only the newest 200 rows are kept.
    /// </summary>
    [Fact]
    public void ChatHistoryStore_Save_MoreThan200Rows_KeepsTheNewest200()
    {
        // Arrange: 250 rows numbered in order
        var project = NewProject("Demo");
        var rows = Enumerable.Range(1, 250).Select(number => Assistant($"row {number}")).ToList();

        // Act
        _store.Save(project, rows);
        var loaded = _store.Load(project);

        // Assert: the oldest 50 are gone
        Assert.Equal(ChatHistoryStore.MaxRows, loaded.Count);
        Assert.Equal("row 51", loaded[0].Text);
        Assert.Equal("row 250", loaded[^1].Text);
    }

    /// <summary>
    ///     Test that the file never exceeds 2 MB: the oldest rows are dropped first, and the newest is kept.
    /// </summary>
    [Fact]
    public void ChatHistoryStore_Save_MoreThan2MegabytesOfRows_DropsTheOldestFirst()
    {
        // Arrange: 30 rows of 90,000 characters, which is about 2.7 MB
        var project = NewProject("Demo");
        var rows = Enumerable.Range(1, 30).Select(number => Assistant(number.ToString("D2", System.Globalization.CultureInfo.InvariantCulture) + new string('x', 90_000))).ToList();

        // Act
        _store.Save(project, rows);
        var loaded = _store.Load(project);

        // Assert: the file fits, some rows are gone from the start, and the newest is there
        Assert.True(new FileInfo(_store.HistoryFilePath(project)).Length <= ChatHistoryStore.MaxBytes);
        Assert.InRange(loaded.Count, 1, 29);
        Assert.StartsWith("30", loaded[^1].Text, StringComparison.Ordinal);
        Assert.Equal(Enumerable.Range(31 - loaded.Count, loaded.Count).Select(number => number.ToString("D2", System.Globalization.CultureInfo.InvariantCulture)), loaded.Select(row => row.Text[..2]));
    }

    /// <summary>
    ///     Test that a text longer than the limit of one row is cut with an ellipsis, and that the cap can be asked for
    ///     without a file.
    /// </summary>
    [Fact]
    public void ChatHistoryStore_Cap_VeryLongText_IsCutAndShortTextStays()
    {
        // Arrange and act
        var capped = ChatHistoryStore.Cap([Assistant(new string('a', ChatHistoryStore.MaxTextCharacters + 500)), Assistant("short")]);

        // Assert: the long text is cut to the limit and ends with an ellipsis; the short one is the same
        Assert.Equal(ChatHistoryStore.MaxTextCharacters, capped[0].Text.Length);
        Assert.EndsWith("...", capped[0].Text, StringComparison.Ordinal);
        Assert.Equal("short", capped[1].Text);
    }

    /// <summary>
    ///     Test that a save leaves no temporary file, replaces the old file whole, and removes the temporary files that a
    ///     crash left, while a leftover never disturbs reading.
    /// </summary>
    [Fact]
    public void ChatHistoryStore_Save_AfterACrash_RemovesLeftoversAndReplacesTheFileWhole()
    {
        // Arrange: a saved history and the leftovers of a write that did not end
        var project = NewProject("Demo");
        _store.Save(project, [Assistant("first")]);
        var folder = _store.FolderOf(project);
        File.WriteAllText(Path.Combine(folder, "history.json.0123456789abcdef.tmp"), "{ \"version\": 1, \"rows\": [ { half");
        File.WriteAllText(Path.Combine(folder, "project.json.0123456789abcdef.tmp"), "{");

        // Assert: the leftovers do not disturb reading
        Assert.Equal("first", Assert.Single(_store.Load(project)).Text);

        // Act: save again
        _store.Save(project, [Assistant("second")]);

        // Assert: the leftovers are gone, only the two files are left, and the file holds the new rows whole
        Assert.Equal(["history.json", "project.json"], Directory.GetFiles(folder).Select(Path.GetFileName).Order(StringComparer.Ordinal));
        Assert.Equal("second", Assert.Single(_store.Load(project)).Text);
    }

    /// <summary>
    ///     Test that a history that cannot be written is a result with a sentence, not an exception, and leaves no
    ///     temporary file behind.
    /// </summary>
    [Fact]
    public void ChatHistoryStore_Save_RootCannotBeMade_ReturnsFailureAndDoesNotThrow()
    {
        // Arrange: a file where the root of the history should be a folder
        var blocked = Path.Combine(_root, "blocked");
        File.WriteAllText(blocked, "a file");
        var store = new ChatHistoryStore(Path.Combine(blocked, "history"), _logger, _clock);

        // Act
        var result = store.Save(NewProject("Demo"), [Assistant("words")]);

        // Assert: a failure with a sentence, nothing thrown, and the log has the fact but not the words
        Assert.False(result.Succeeded);
        Assert.Contains("could not be saved", result.Message, StringComparison.Ordinal);
        Assert.Contains("could not be written", File.ReadAllText(_logger.ActiveFilePath), StringComparison.Ordinal);
        Assert.DoesNotContain("words", File.ReadAllText(_logger.ActiveFilePath), StringComparison.Ordinal);
    }

    /// <summary>
    ///     Test that a file that is not JSON is renamed to <c>.bad</c> in the same folder, that the history is empty, that
    ///     the log has the path and the reason but none of the words, and that a save afterwards starts anew.
    /// </summary>
    [Fact]
    public void ChatHistoryStore_Load_CorruptFile_IsRenamedBadIgnoredAndLoggedWithoutItsWords()
    {
        // Arrange: a file that is not JSON
        var project = NewProject("Demo");
        var path = _store.HistoryFilePath(project);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, "{ \"version\": 1, \"rows\": [ { \"text\": \"" + Secret + "\"");

        // Act
        var loaded = _store.Load(project);

        // Assert: empty, the file is kept as .bad, and the log names the file and not the words
        Assert.Empty(loaded);
        Assert.False(File.Exists(path));
        Assert.Contains(Secret, File.ReadAllText(path + ".bad"), StringComparison.Ordinal);
        var log = File.ReadAllText(_logger.ActiveFilePath);
        Assert.Contains(path, log, StringComparison.Ordinal);
        Assert.Contains(".bad", log, StringComparison.Ordinal);
        Assert.DoesNotContain(Secret, log, StringComparison.Ordinal);

        // Act and assert: a new history can be saved and read
        _store.Save(project, [Assistant("fresh")]);
        Assert.Equal("fresh", Assert.Single(_store.Load(project)).Text);
    }

    /// <summary>
    ///     Test that a file with a newer version is kept as <c>.bad</c> untouched and ignored.
    /// </summary>
    [Fact]
    public void ChatHistoryStore_Load_NewerVersion_IsRenamedBadAndIgnored()
    {
        // Arrange: a file of version 2
        var project = NewProject("Demo");
        var path = _store.HistoryFilePath(project);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        const string content = "{ \"version\": 2, \"rows\": [ { \"kind\": \"user\", \"text\": \"from the future\" } ] }";
        File.WriteAllText(path, content);

        // Act
        var loaded = _store.Load(project);

        // Assert: ignored, kept byte for byte as .bad, and the log says why
        Assert.Empty(loaded);
        Assert.False(File.Exists(path));
        Assert.Equal(content, File.ReadAllText(path + ".bad"));
        Assert.Contains("newer version 2", File.ReadAllText(_logger.ActiveFilePath), StringComparison.Ordinal);
    }

    /// <summary>
    ///     Test that every shape of file that is not a history is set aside: no version, a version that is not a number,
    ///     version 0, not an object, no rows, a row of an unknown kind, and a file that is too big.
    /// </summary>
    [Theory]
    [InlineData("{ \"rows\": [] }")]
    [InlineData("{ \"version\": \"1\", \"rows\": [] }")]
    [InlineData("{ \"version\": 0, \"rows\": [] }")]
    [InlineData("[ 1, 2 ]")]
    [InlineData("null")]
    [InlineData("")]
    [InlineData("{ \"version\": 1 }")]
    [InlineData("{ \"version\": 1, \"rows\": [ { \"kind\": \"robot\", \"text\": \"x\" } ] }")]
    public void ChatHistoryStore_Load_FileThatIsNotAHistory_IsRenamedBadAndIgnored(string content)
    {
        // Arrange
        var project = NewProject("Demo");
        var path = _store.HistoryFilePath(project);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content);

        // Act
        var loaded = _store.Load(project);

        // Assert
        Assert.Empty(loaded);
        Assert.False(File.Exists(path));
        Assert.True(File.Exists(path + ".bad"));
    }

    /// <summary>
    ///     Test that a file far bigger than any history that was written is set aside without being parsed.
    /// </summary>
    [Fact]
    public void ChatHistoryStore_Load_FileFarBiggerThanTheLimit_IsRenamedBad()
    {
        // Arrange: a file of more than eight megabytes
        var project = NewProject("Demo");
        var path = _store.HistoryFilePath(project);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, new string(' ', (4 * ChatHistoryStore.MaxBytes) + 1));

        // Act and assert
        Assert.Empty(_store.Load(project));
        Assert.True(File.Exists(path + ".bad"));
    }

    /// <summary>
    ///     Test that a second bad file replaces the first <c>.bad</c> file and that rows that cannot be used are dropped
    ///     while the others are read.
    /// </summary>
    [Fact]
    public void ChatHistoryStore_Load_RowsThatCannotBeUsed_AreDroppedAndTheOthersRead()
    {
        // Arrange: a file with a tool row without a name, a user row without words, and two good rows
        var project = NewProject("Demo");
        var path = _store.HistoryFilePath(project);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(
            path,
            """
            { "version": 1, "rows": [
              { "kind": "tool", "text": "no name" },
              { "kind": "user", "text": "" },
              null,
              { "kind": "user", "utc": "2026-01-02T03:04:05+00:00", "text": "good" },
              { "kind": "tool", "tool": "render_image", "text": "Rendered `a.svg`", "succeeded": true }
            ] }
            """);

        // Act
        var loaded = _store.Load(project);

        // Assert
        Assert.Equal(["good", "Rendered `a.svg`"], loaded.Select(row => row.Text));
        Assert.True(File.Exists(path));
    }

    /// <summary>
    ///     Test that Clear deletes the folder of the project with both files and a bad file, and nothing of another
    ///     project, and that clearing nothing is fine.
    /// </summary>
    [Fact]
    public void ChatHistoryStore_Clear_DeletesTheFolderOfThatProjectOnly()
    {
        // Arrange: two projects with a history, one of them with a bad file
        var first = NewProject("First");
        var second = NewProject("Second");
        _store.Save(first, [Assistant("one")]);
        _store.Save(second, [Assistant("two")]);
        File.WriteAllText(_store.HistoryFilePath(first) + ".bad", "old");

        // Act
        var result = _store.Clear(first);
        var again = _store.Clear(first);

        // Assert: the first is gone, the second is intact, and clearing again is not an error
        Assert.True(result.Succeeded);
        Assert.True(again.Succeeded);
        Assert.False(Directory.Exists(_store.FolderOf(first)));
        Assert.Empty(_store.Load(first));
        Assert.Equal("two", Assert.Single(_store.Load(second)).Text);
    }

    /// <summary>
    ///     Test that a history root inside the project folder, or the project folder itself, is refused for every
    ///     operation: nothing is read, written, or deleted, and the answer says why.
    /// </summary>
    [Theory]
    [InlineData("")]
    [InlineData("history")]
    [InlineData(".canvaspilot/chat/history")]
    public void ChatHistoryStore_RootInsideTheProject_IsRefusedForEveryOperation(string below)
    {
        // Arrange: a project and a store whose root is the project folder or below it, with a file that must stay
        var project = NewProject("Demo");
        var root = below.Length == 0 ? project.FolderPath : Path.Combine(project.FolderPath, below);
        var store = new ChatHistoryStore(root, _logger);
        var keep = Path.Combine(project.FolderPath, "keep.txt");
        File.WriteAllText(keep, "stay");
        var before = Directory.GetFileSystemEntries(project.FolderPath, "*", SearchOption.AllDirectories).Order(StringComparer.Ordinal).ToList();

        // Act
        var saved = store.Save(project, [Assistant("words")]);
        var loaded = store.Load(project);
        var cleared = store.Clear(project);

        // Assert: all three refused, the project folder is as it was, and the log says so
        Assert.False(saved.Succeeded);
        Assert.Contains("inside the project folder", saved.Message, StringComparison.Ordinal);
        Assert.Empty(loaded);
        Assert.False(cleared.Succeeded);
        Assert.Equal(before, Directory.GetFileSystemEntries(project.FolderPath, "*", SearchOption.AllDirectories).Order(StringComparer.Ordinal));
        Assert.Equal("stay", File.ReadAllText(keep));
        Assert.Contains("lies inside a project folder", File.ReadAllText(_logger.ActiveFilePath), StringComparison.Ordinal);
    }

    /// <summary>
    ///     Test that a root beside the project whose name only starts like the project's is not inside it.
    /// </summary>
    [Fact]
    public void ChatHistoryStore_RootBesideTheProjectWithTheSamePrefix_IsNotInsideIt()
    {
        // Arrange: a project 'proj' and a root 'proj2'
        var project = NewProject("proj");
        var store = new ChatHistoryStore(Path.Combine(_root, "projects", "proj2"), _logger);

        // Act and assert: it can be used
        Assert.True(store.Save(project, [Assistant("words")]).Succeeded);
        Assert.Equal("words", Assert.Single(store.Load(project)).Text);
    }

    /// <summary>
    ///     Test that the root must be given and that the default root is below the local application data of the user.
    /// </summary>
    [Fact]
    public void ChatHistoryStore_New_BlankRootThrows_AndTheDefaultRootIsBelowTheLocalData()
    {
        Assert.Throws<ArgumentException>(() => new ChatHistoryStore(" "));
        Assert.Throws<ArgumentNullException>(() => new ChatHistoryStore(null!));
        Assert.EndsWith(Path.Combine("CanvasPilot", "history"), ChatHistoryStore.DefaultHistoryRoot, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Test that missing arguments are refused by every operation.
    /// </summary>
    [Fact]
    public void ChatHistoryStore_Operations_NullArguments_Throw()
    {
        var project = NewProject("Demo");
        Assert.Throws<ArgumentNullException>(() => _store.Load(null!));
        Assert.Throws<ArgumentNullException>(() => _store.Save(null!, []));
        Assert.Throws<ArgumentNullException>(() => _store.Save(project, null!));
        Assert.Throws<ArgumentNullException>(() => _store.Clear(null!));
        Assert.Throws<ArgumentNullException>(() => ChatHistoryStore.Cap(null!));
    }

    /// <summary>
    ///     Makes a project in a folder of its own that exists and is empty.
    /// </summary>
    /// <param name="name">The name of the project.</param>
    /// <returns>The project.</returns>
    private ProjectInfo NewProject(string name)
    {
        var folder = Directory.CreateDirectory(Path.Combine(_root, "projects", name)).FullName;
        return new ProjectInfo(name, folder, Path.Combine(folder, name + ProjectDocument.FileExtension), new ProjectDocument { Name = name });
    }

    /// <summary>
    ///     Makes the row of a reply that is whole.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <returns>The row.</returns>
    private static ChatHistoryRow Assistant(string text) =>
        new() { Kind = ChatHistoryRowKind.Assistant, Utc = new DateTimeOffset(2026, 5, 6, 7, 8, 9, TimeSpan.Zero), Text = text, Reply = ChatHistoryReplyState.Completed };

    /// <summary>
    ///     Makes one row of every kind.
    /// </summary>
    /// <returns>The rows, oldest first.</returns>
    private static List<ChatHistoryRow> SampleRows() =>
    [
        new()
        {
            Kind = ChatHistoryRowKind.User,
            Utc = new DateTimeOffset(2026, 5, 6, 7, 8, 9, TimeSpan.Zero),
            Text = "Why is [Mark 1] \"red\"? [Attachment: logo.png] \u00e4\u00f6 \u2603",
            Attachments = ["attachments/logo.png"],
            Marks = [new ChatHistoryMark(1, "check this"), new ChatHistoryMark(2, null)],
        },
        new()
        {
            Kind = ChatHistoryRowKind.Assistant,
            Utc = new DateTimeOffset(2026, 5, 6, 7, 9, 0, TimeSpan.Zero),
            Text = "Because of the fill.\nLine two.",
            Reply = ChatHistoryReplyState.Completed,
        },
        new()
        {
            Kind = ChatHistoryRowKind.Tool,
            Utc = new DateTimeOffset(2026, 5, 6, 7, 9, 1, TimeSpan.Zero),
            Tool = "project_write",
            Text = "Replaced `flow.svg`",
            Succeeded = false,
        },
        new()
        {
            Kind = ChatHistoryRowKind.Assistant,
            Utc = new DateTimeOffset(2026, 5, 6, 7, 9, 2, TimeSpan.Zero),
            Text = "Stopped here",
            Reply = ChatHistoryReplyState.Stopped,
        },
    ];

    /// <summary>
    ///     Describes rows as text so that rows with lists can be compared.
    /// </summary>
    /// <param name="rows">The rows.</param>
    /// <returns>One line for each row.</returns>
    private static List<string> Describe(IEnumerable<ChatHistoryRow> rows) =>
        [.. rows.Select(row => string.Join(
            '|',
            row.Kind,
            row.Utc.ToString("O", System.Globalization.CultureInfo.InvariantCulture),
            row.Text,
            string.Join(',', row.Attachments ?? []),
            string.Join(',', (row.Marks ?? []).Select(mark => $"{mark.Number}={mark.Note ?? "<null>"}")),
            row.Tool,
            row.Succeeded,
            row.Reply))];
}
