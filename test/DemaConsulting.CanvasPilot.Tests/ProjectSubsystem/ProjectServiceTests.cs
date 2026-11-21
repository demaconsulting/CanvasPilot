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
using DemaConsulting.CanvasPilot.LoggingSubsystem;
using DemaConsulting.CanvasPilot.ProjectSubsystem;

namespace DemaConsulting.CanvasPilot.Tests;

/// <summary>
///     Unit tests for <see cref="ProjectService" />.
/// </summary>
public sealed class ProjectServiceTests : IDisposable
{
    /// <summary>
    ///     The time every test sees, so created and modified times can be checked exactly.
    /// </summary>
    private static readonly DateTimeOffset CreatedTime = new(2026, 3, 4, 5, 6, 7, TimeSpan.Zero);

    /// <summary>
    ///     Temporary root created fresh for each test and removed on disposal.
    /// </summary>
    private readonly string _tempRoot = Directory.CreateTempSubdirectory("canvaspilot-tests-").FullName;

    /// <summary>
    ///     Clock that returns <see cref="CreatedTime" /> until a test changes it.
    /// </summary>
    private readonly SettableTimeProvider _clock = new(CreatedTime);

    /// <summary>
    ///     Gets the token that cancels the running test.
    /// </summary>
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    /// <summary>
    ///     The folder that will contain new projects.
    /// </summary>
    private string Parent => Path.Combine(_tempRoot, "projects");

    /// <inheritdoc />
    public void Dispose()
    {
        if (Directory.Exists(_tempRoot))
        {
            Directory.Delete(_tempRoot, recursive: true);
        }
    }

    /// <summary>
    ///     Test that creating a project makes the folder, the project file with its documented content, and the
    ///     empty data folder, and makes the project the open project.
    /// </summary>
    [Fact]
    public async Task ProjectService_CreateAsync_ValidName_CreatesFolderProjectFileAndDataFolder()
    {
        // Arrange: an existing parent folder and a service that announces changes
        Directory.CreateDirectory(Parent);
        var service = CreateService();
        var changes = 0;
        service.CurrentChanged += (_, _) => changes++;

        // Act: create a project
        var result = await service.CreateAsync(Parent, "Sky Chart", Ct);

        // Assert: the folder holds the project file and an empty data folder, and nothing else
        var folder = Path.Combine(Parent, "Sky Chart");
        var file = Path.Combine(folder, "Sky Chart.canvaspilot");
        Assert.True(result.Succeeded, result.Error);
        Assert.Equal(
            [Path.Combine(folder, ".canvaspilot"), file],
            Directory.GetFileSystemEntries(folder).Order(StringComparer.Ordinal));
        Assert.Empty(Directory.GetFileSystemEntries(Path.Combine(folder, ".canvaspilot")));
        if (OperatingSystem.IsWindows())
        {
            Assert.True(new DirectoryInfo(Path.Combine(folder, ".canvaspilot")).Attributes.HasFlag(FileAttributes.Hidden));
        }

        using var json = JsonDocument.Parse(await File.ReadAllTextAsync(file, Ct));
        Assert.Equal(1, json.RootElement.GetProperty("formatVersion").GetInt32());
        Assert.Equal("Sky Chart", json.RootElement.GetProperty("name").GetString());
        Assert.Equal(CreatedTime, json.RootElement.GetProperty("createdUtc").GetDateTimeOffset());
        Assert.Equal(new ProjectInfo("Sky Chart", folder, file, result.Project!.Document), service.Current);
        Assert.Equal(1, changes);
    }

    /// <summary>
    ///     Test that surrounding white space in the name is not part of the project name or folder.
    /// </summary>
    [Fact]
    public async Task ProjectService_CreateAsync_NameWithSurroundingSpaces_TrimsName()
    {
        // Arrange: an existing parent folder
        Directory.CreateDirectory(Parent);

        // Act: create a project with a padded name
        var result = await CreateService().CreateAsync(Parent, "  Padded  ", Ct);

        // Assert: the name and folder are trimmed
        Assert.True(result.Succeeded, result.Error);
        Assert.Equal("Padded", result.Project!.Name);
        Assert.True(Directory.Exists(Path.Combine(Parent, "Padded")));
    }

    /// <summary>
    ///     Test that an existing empty folder can become the project folder.
    /// </summary>
    [Fact]
    public async Task ProjectService_CreateAsync_ExistingEmptyFolder_Succeeds()
    {
        // Arrange: an empty folder with the project's name
        Directory.CreateDirectory(Path.Combine(Parent, "Empty"));

        // Act: create the project there
        var result = await CreateService().CreateAsync(Parent, "Empty", Ct);

        // Assert: the project file was written into the existing folder
        Assert.True(result.Succeeded, result.Error);
        Assert.True(File.Exists(Path.Combine(Parent, "Empty", "Empty.canvaspilot")));
    }

    /// <summary>
    ///     Test that a non-empty existing folder is refused and left exactly as it was.
    /// </summary>
    [Fact]
    public async Task ProjectService_CreateAsync_ExistingNonEmptyFolder_FailsAndLeavesContent()
    {
        // Arrange: a folder with the project's name that already holds a file
        var folder = Directory.CreateDirectory(Path.Combine(Parent, "Busy")).FullName;
        await File.WriteAllTextAsync(Path.Combine(folder, "keep.txt"), "keep", Ct);
        var service = CreateService();

        // Act: try to create the project there
        var result = await service.CreateAsync(Parent, "Busy", Ct);

        // Assert: it failed with an explanation, and the folder and service are unchanged
        Assert.False(result.Succeeded);
        Assert.Contains("not empty", result.Error);
        Assert.Equal(["keep.txt"], Directory.GetFileSystemEntries(folder).Select(Path.GetFileName));
        Assert.Null(service.Current);
    }

    /// <summary>
    ///     Test that an existing file with the project's name is refused.
    /// </summary>
    [Fact]
    public async Task ProjectService_CreateAsync_ExistingFileWithName_Fails()
    {
        // Arrange: a file where the project folder would go
        Directory.CreateDirectory(Parent);
        await File.WriteAllTextAsync(Path.Combine(Parent, "Taken"), "x", Ct);

        // Act: try to create the project
        var result = await CreateService().CreateAsync(Parent, "Taken", Ct);

        // Assert: it failed with an explanation
        Assert.False(result.Succeeded);
        Assert.Contains("file named 'Taken'", result.Error);
    }

    /// <summary>
    ///     Test that names that cannot be file names, or are reserved or empty, are refused without touching the
    ///     file system.
    /// </summary>
    /// <param name="name">The invalid name.</param>
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(".")]
    [InlineData("..")]
    [InlineData(".hidden")]
    [InlineData("trailing.")]
    [InlineData("a/b")]
    [InlineData("a\\b")]
    [InlineData("a:b")]
    [InlineData("a*b")]
    [InlineData("a?b")]
    [InlineData("a\"b")]
    [InlineData("a<b")]
    [InlineData("a|b")]
    [InlineData("tab\tname")]
    [InlineData("CON")]
    [InlineData("nul")]
    [InlineData("Com1")]
    [InlineData("LPT9.txt")]
    [InlineData("../escape")]
    public async Task ProjectService_CreateAsync_InvalidName_FailsWithoutCreatingAnything(string name)
    {
        // Arrange: an existing parent folder
        Directory.CreateDirectory(Parent);
        var service = CreateService();

        // Act: try to create a project with the invalid name
        var result = await service.CreateAsync(Parent, name, Ct);

        // Assert: it failed with a message, nothing was created, and no project is open
        Assert.False(result.Succeeded);
        Assert.NotEmpty(result.Error);
        Assert.Empty(Directory.GetFileSystemEntries(Parent));
        Assert.Null(service.Current);
    }

    /// <summary>
    ///     Test that a name that is too long is refused, and the longest accepted name is allowed.
    /// </summary>
    [Fact]
    public void ProjectService_ValidateProjectName_LengthLimit_IsEnforced()
    {
        // Arrange / Act: check names around the limit
        var service = CreateService();
        var atLimit = service.ValidateProjectName(new string('a', 100));
        var overLimit = service.ValidateProjectName(new string('a', 101));

        // Assert: only the longer name is refused
        Assert.Null(atLimit);
        Assert.Contains("100", overLimit);
    }

    /// <summary>
    ///     Test that ordinary names, including spaces, periods inside the name, and non-ASCII letters, are accepted.
    /// </summary>
    /// <param name="name">The valid name.</param>
    [Theory]
    [InlineData("My Project")]
    [InlineData("version 1.2")]
    [InlineData("Name-1_x")]
    [InlineData("Café déjà vu")]
    [InlineData("console")]
    public void ProjectService_ValidateProjectName_OrdinaryName_IsAccepted(string name)
    {
        // Arrange / Act: check the name
        var problem = CreateService().ValidateProjectName(name);

        // Assert: no problem is reported
        Assert.Null(problem);
    }

    /// <summary>
    ///     Test that a missing, empty, or relative parent folder is refused.
    /// </summary>
    /// <param name="parent">The parent folder entry; <c>{missing}</c> stands for a folder that does not exist.</param>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("relative\\folder")]
    [InlineData("{missing}")]
    public async Task ProjectService_CreateAsync_UnusableParentFolder_Fails(string? parent)
    {
        // Arrange: the parent entry, resolved to a path that does not exist when requested
        var entry = parent == "{missing}" ? Path.Combine(_tempRoot, "missing") : parent;

        // Act: try to create a project there
        var result = await CreateService().CreateAsync(entry!, "Project", Ct);

        // Assert: it failed with a message and the missing folder was not created
        Assert.False(result.Succeeded);
        Assert.NotEmpty(result.Error);
        Assert.False(Directory.Exists(Path.Combine(_tempRoot, "missing")));
    }

    /// <summary>
    ///     Test that a cancelled creation removes the folder it made and reports the cancellation.
    /// </summary>
    [Fact]
    public async Task ProjectService_CreateAsync_Cancelled_RemovesFolderItCreated()
    {
        // Arrange: a parent folder and an already cancelled token
        Directory.CreateDirectory(Parent);
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();
        var service = CreateService();

        // Act: create with the cancelled token
        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => service.CreateAsync(Parent, "Cancelled", cancellation.Token));

        // Assert: nothing is left behind and no project is open
        Assert.False(Directory.Exists(Path.Combine(Parent, "Cancelled")));
        Assert.Null(service.Current);
    }

    /// <summary>
    ///     Test that a cancelled creation in a pre-existing empty folder removes only what it added.
    /// </summary>
    [Fact]
    public async Task ProjectService_CreateAsync_CancelledInExistingFolder_KeepsFolderRemovesAdditions()
    {
        // Arrange: an existing empty project folder and an already cancelled token
        var folder = Directory.CreateDirectory(Path.Combine(Parent, "Mine")).FullName;
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        // Act: create with the cancelled token
        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => CreateService().CreateAsync(Parent, "Mine", cancellation.Token));

        // Assert: the folder remains, empty
        Assert.True(Directory.Exists(folder));
        Assert.Empty(Directory.GetFileSystemEntries(folder));
    }

    /// <summary>
    ///     Test that an input/output failure while creating is returned as a failed result and logged, not thrown.
    /// </summary>
    [Fact]
    public async Task ProjectService_CreateAsync_IoFailure_ReturnsFailureAndLogsError()
    {
        // Arrange: a clock that fails as the document is built, and a logger
        Directory.CreateDirectory(Parent);
        var logger = new RollingFileLogger(Path.Combine(_tempRoot, "logs"));
        var service = new ProjectService(logger, new ThrowingTimeProvider());

        // Act: create a project
        var result = await service.CreateAsync(Parent, "Broken", Ct);

        // Assert: a failed result carries the reason, the error is logged, and nothing was left behind
        Assert.False(result.Succeeded);
        Assert.Contains("clock failed", result.Error);
        Assert.Contains("clock failed", await File.ReadAllTextAsync(logger.ActiveFilePath, Ct));
        Assert.False(Directory.Exists(Path.Combine(Parent, "Broken")));
        Assert.Null(service.Current);
    }

    /// <summary>
    ///     Test that a refused creation is logged.
    /// </summary>
    [Fact]
    public async Task ProjectService_CreateAsync_InvalidName_LogsWarning()
    {
        // Arrange: a service with a logger
        Directory.CreateDirectory(Parent);
        var logger = new RollingFileLogger(Path.Combine(_tempRoot, "logs"));
        var service = new ProjectService(logger, _clock);

        // Act: try an invalid name
        await service.CreateAsync(Parent, "bad:name", Ct);

        // Assert: the log names the refused project
        var log = await File.ReadAllTextAsync(logger.ActiveFilePath, Ct);
        Assert.Contains("bad:name", log);
        Assert.Contains("[Warning]", log);
    }

    /// <summary>
    ///     Test that the project file written by create reads back as the same project.
    /// </summary>
    [Fact]
    public async Task ProjectService_OpenAsync_CreatedProject_ReturnsSameProject()
    {
        // Arrange: a created project and a second service, as after a restart
        Directory.CreateDirectory(Parent);
        var created = (await CreateService().CreateAsync(Parent, "Round Trip", Ct)).Project!;
        var second = CreateService();

        // Act: open the project file
        var result = await second.OpenAsync(created.FilePath, Ct);

        // Assert: the project matches and is now the open project
        Assert.True(result.Succeeded, result.Error);
        Assert.Equal("Round Trip", result.Project!.Name);
        Assert.Equal(created.FolderPath, result.Project.FolderPath);
        Assert.Equal(created.FilePath, result.Project.FilePath);
        Assert.Equal(CreatedTime, result.Project.Document.CreatedUtc);
        Assert.Same(result.Project, second.Current);
    }

    /// <summary>
    ///     Test that comments, trailing commas, different casing, and unknown properties do not stop a project
    ///     file from opening.
    /// </summary>
    [Fact]
    public async Task ProjectService_OpenAsync_UnknownPropertiesAndComments_AreIgnored()
    {
        // Arrange: a hand-written project file with extras
        var file = WriteProjectFile(
            "Extras",
            """
            {
              // a comment
              "FormatVersion": 1,
              "NAME": "Extras",
              "createdUtc": "2026-01-02T03:04:05Z",
              "futureThing": { "a": [1, 2, 3] },
            }
            """);

        // Act: open it
        var result = await CreateService().OpenAsync(file, Ct);

        // Assert: it opened with the known values
        Assert.True(result.Succeeded, result.Error);
        Assert.Equal("Extras", result.Project!.Name);
        Assert.Equal(new DateTimeOffset(2026, 1, 2, 3, 4, 5, TimeSpan.Zero), result.Project.Document.CreatedUtc);
    }

    /// <summary>
    ///     Test that a project file without a name takes its name from the file name.
    /// </summary>
    [Fact]
    public async Task ProjectService_OpenAsync_MissingName_UsesFileName()
    {
        // Arrange: a project file with no name
        var file = WriteProjectFile("From File", """{ "formatVersion": 1 }""");

        // Act: open it
        var result = await CreateService().OpenAsync(file, Ct);

        // Assert: the name comes from the file name
        Assert.True(result.Succeeded, result.Error);
        Assert.Equal("From File", result.Project!.Name);
    }

    /// <summary>
    ///     Test that a project file from a newer format is refused with a clear message and leaves the open project
    ///     unchanged.
    /// </summary>
    [Fact]
    public async Task ProjectService_OpenAsync_NewerFormat_FailsWithClearMessageAndKeepsCurrent()
    {
        // Arrange: an open project and a project file of format version 2
        Directory.CreateDirectory(Parent);
        var service = CreateService();
        var open = (await service.CreateAsync(Parent, "Open One", Ct)).Project!;
        var newer = WriteProjectFile("Newer", """{ "formatVersion": 2, "name": "Newer" }""");

        // Act: try to open the newer file
        var result = await service.OpenAsync(newer, Ct);

        // Assert: it failed with an update hint and the open project is unchanged
        Assert.False(result.Succeeded);
        Assert.Contains("newer version", result.Error);
        Assert.Contains("2", result.Error);
        Assert.Same(open, service.Current);
    }

    /// <summary>
    ///     Test that files that are not valid project files are refused with a message rather than thrown.
    /// </summary>
    /// <param name="content">The file content.</param>
    [Theory]
    [InlineData("")]
    [InlineData("not json at all")]
    [InlineData("{ \"formatVersion\": 1, ")]
    [InlineData("[1, 2, 3]")]
    [InlineData("null")]
    [InlineData("{ \"name\": \"No Version\" }")]
    [InlineData("{ \"formatVersion\": 0, \"name\": \"Zero\" }")]
    [InlineData("{ \"formatVersion\": \"one\", \"name\": \"Text\" }")]
    [InlineData("{ \"formatVersion\": 1, \"createdUtc\": \"yesterday\" }")]
    public async Task ProjectService_OpenAsync_NotAProjectFile_FailsAndLogs(string content)
    {
        // Arrange: a file with unusable content and a logger
        var file = WriteProjectFile("Bad", content);
        var logger = new RollingFileLogger(Path.Combine(_tempRoot, "logs"));
        var service = new ProjectService(logger, _clock);

        // Act: try to open it
        var result = await service.OpenAsync(file, Ct);

        // Assert: it failed with a message naming the file, was logged, and nothing is open
        Assert.False(result.Succeeded);
        Assert.Contains("Bad.canvaspilot", result.Error);
        Assert.Contains("Bad.canvaspilot", await File.ReadAllTextAsync(logger.ActiveFilePath, Ct));
        Assert.Null(service.Current);
    }

    /// <summary>
    ///     Test that a missing file, a file with the wrong extension, and a blank path are refused.
    /// </summary>
    [Fact]
    public async Task ProjectService_OpenAsync_MissingWrongOrBlankPath_Fails()
    {
        // Arrange: a service, a text file, and a missing project file
        var service = CreateService();
        Directory.CreateDirectory(_tempRoot);
        var text = Path.Combine(_tempRoot, "notes.txt");
        await File.WriteAllTextAsync(text, "x", Ct);

        // Act: try each
        var missing = await service.OpenAsync(Path.Combine(_tempRoot, "gone.canvaspilot"), Ct);
        var wrongType = await service.OpenAsync(text, Ct);
        var blank = await service.OpenAsync("  ", Ct);
        var invalid = await service.OpenAsync("bad\0path.canvaspilot", Ct);

        // Assert: each failed with its own message
        Assert.Contains("was not found", missing.Error);
        Assert.Contains("not a CanvasPilot project file", wrongType.Error);
        Assert.Contains("Choose a project file", blank.Error);
        Assert.False(invalid.Succeeded);
    }

    /// <summary>
    ///     Test that opening a second project replaces the first and announces the change.
    /// </summary>
    [Fact]
    public async Task ProjectService_OpenAsync_SecondProject_ReplacesCurrentAndRaisesEvent()
    {
        // Arrange: one open project and a second project file
        Directory.CreateDirectory(Parent);
        var service = CreateService();
        await service.CreateAsync(Parent, "First", Ct);
        var second = WriteProjectFile("Second", """{ "formatVersion": 1, "name": "Second" }""");
        var changes = 0;
        service.CurrentChanged += (_, _) => changes++;

        // Act: open the second
        var result = await service.OpenAsync(second, Ct);

        // Assert: it is now the open project and one change was announced
        Assert.True(result.Succeeded, result.Error);
        Assert.Equal("Second", service.Current!.Name);
        Assert.Equal(1, changes);
    }

    /// <summary>
    ///     Test that saving rewrites the project file with a new modified time and the same content.
    /// </summary>
    [Fact]
    public async Task ProjectService_SaveAsync_OpenProject_UpdatesModifiedTimeAndKeepsContent()
    {
        // Arrange: a created project and a later time
        Directory.CreateDirectory(Parent);
        var service = CreateService();
        var created = (await service.CreateAsync(Parent, "Saved", Ct)).Project!;
        var later = CreatedTime.AddHours(2);
        _clock.UtcNow = later;

        // Act: save
        var result = await service.SaveAsync(Ct);

        // Assert: the file holds the modified time, the original created time, and no temporary file remains
        Assert.True(result.Succeeded, result.Error);
        using var json = JsonDocument.Parse(await File.ReadAllTextAsync(created.FilePath, Ct));
        Assert.Equal(later, json.RootElement.GetProperty("modifiedUtc").GetDateTimeOffset());
        Assert.Equal(CreatedTime, json.RootElement.GetProperty("createdUtc").GetDateTimeOffset());
        Assert.Equal("Saved", json.RootElement.GetProperty("name").GetString());
        Assert.Equal(later, service.Current!.Document.ModifiedUtc);
        Assert.Equal(
            [Path.Combine(created.FolderPath, ".canvaspilot"), created.FilePath],
            Directory.GetFileSystemEntries(created.FolderPath).Order(StringComparer.Ordinal));
    }

    /// <summary>
    ///     Test that saving keeps properties this version does not know.
    /// </summary>
    [Fact]
    public async Task ProjectService_SaveAsync_UnknownProperties_ArePreserved()
    {
        // Arrange: an opened project file with an unknown property
        var file = WriteProjectFile("Keep", """{ "formatVersion": 1, "name": "Keep", "futureThing": { "a": 7 } }""");
        var service = CreateService();
        await service.OpenAsync(file, Ct);

        // Act: save
        var result = await service.SaveAsync(Ct);

        // Assert: the unknown property is still in the file
        Assert.True(result.Succeeded, result.Error);
        using var json = JsonDocument.Parse(await File.ReadAllTextAsync(file, Ct));
        Assert.Equal(7, json.RootElement.GetProperty("futureThing").GetProperty("a").GetInt32());
    }

    /// <summary>
    ///     Test that saving with no open project fails with a message.
    /// </summary>
    [Fact]
    public async Task ProjectService_SaveAsync_NoProject_Fails()
    {
        // Arrange / Act: save with nothing open
        var result = await CreateService().SaveAsync(Ct);

        // Assert: it failed with a message
        Assert.False(result.Succeeded);
        Assert.Contains("no open project", result.Error, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    ///     Test that a save that cannot write is reported as a failed result and logged.
    /// </summary>
    [Fact]
    public async Task ProjectService_SaveAsync_FolderRemoved_FailsAndLogsError()
    {
        // Arrange: an open project whose folder was deleted
        Directory.CreateDirectory(Parent);
        var logger = new RollingFileLogger(Path.Combine(_tempRoot, "logs"));
        var service = new ProjectService(logger, _clock);
        var project = (await service.CreateAsync(Parent, "Vanishing", Ct)).Project!;
        Directory.Delete(project.FolderPath, recursive: true);

        // Act: save
        var result = await service.SaveAsync(Ct);

        // Assert: the failure is returned and logged, and the project stays open
        Assert.False(result.Succeeded);
        Assert.Contains("could not be saved", result.Error);
        Assert.Contains("Vanishing.canvaspilot", await File.ReadAllTextAsync(logger.ActiveFilePath, Ct));
        Assert.Same(project, service.Current);
    }

    /// <summary>
    ///     Test that setting the style kit of the open project writes its id into the project file, makes it part of the
    ///     open project, raises the change once, and that reopening the file reads it back.
    /// </summary>
    [Fact]
    public async Task ProjectService_SetActiveStyleKitAsync_ValidId_IsWrittenAndReadBackAfterReopen()
    {
        // Arrange: a created project, a later time, and a handler that counts changes
        Directory.CreateDirectory(Parent);
        var service = CreateService();
        var created = (await service.CreateAsync(Parent, "Styled", Ct)).Project!;
        var later = CreatedTime.AddHours(1);
        _clock.UtcNow = later;
        var changes = 0;
        service.CurrentChanged += (_, _) => changes++;

        // Act: set the kit and open the file again with a second service
        var result = await service.SetActiveStyleKitAsync("slides-dark", Ct);
        var reopened = await CreateService().OpenAsync(created.FilePath, Ct);

        // Assert: the file, the open project, the event, and the reopened project all say the same
        Assert.True(result.Succeeded, result.Error);
        using var json = JsonDocument.Parse(await File.ReadAllTextAsync(created.FilePath, Ct));
        Assert.Equal("slides-dark", json.RootElement.GetProperty("activeStyleKit").GetString());
        Assert.Equal(later, json.RootElement.GetProperty("modifiedUtc").GetDateTimeOffset());
        Assert.Equal("slides-dark", service.Current!.Document.ActiveStyleKit);
        Assert.Equal("slides-dark", result.Project!.Document.ActiveStyleKit);
        Assert.Equal(1, changes);
        Assert.Equal("slides-dark", reopened.Project!.Document.ActiveStyleKit);
        Assert.Equal(
            [Path.Combine(created.FolderPath, ".canvaspilot"), created.FilePath],
            Directory.GetFileSystemEntries(created.FolderPath).Order(StringComparer.Ordinal));
    }

    /// <summary>
    ///     Test that setting no style removes the property from the project file, and that setting the style that the
    ///     project already has changes nothing and raises nothing.
    /// </summary>
    [Fact]
    public async Task ProjectService_SetActiveStyleKitAsync_NullOrSameId_RemovesPropertyOrDoesNothing()
    {
        // Arrange: a project that uses a kit
        Directory.CreateDirectory(Parent);
        var service = CreateService();
        var created = (await service.CreateAsync(Parent, "Plain", Ct)).Project!;
        await service.SetActiveStyleKitAsync("clean", Ct);
        _clock.UtcNow = CreatedTime.AddHours(3);
        var changes = 0;
        service.CurrentChanged += (_, _) => changes++;

        // Act: set the same kit, then none, then none again
        var same = await service.SetActiveStyleKitAsync("clean", Ct);
        var sameText = await File.ReadAllTextAsync(created.FilePath, Ct);
        var none = await service.SetActiveStyleKitAsync(null, Ct);
        var noneText = await File.ReadAllTextAsync(created.FilePath, Ct);
        var noneAgain = await service.SetActiveStyleKitAsync(null, Ct);

        // Assert: the same kit wrote nothing, none removed the property, and none again wrote nothing
        Assert.True(same.Succeeded && none.Succeeded && noneAgain.Succeeded);
        Assert.Contains("\"activeStyleKit\": \"clean\"", sameText, StringComparison.Ordinal);
        Assert.DoesNotContain("activeStyleKit", noneText, StringComparison.Ordinal);
        Assert.Null(service.Current!.Document.ActiveStyleKit);
        Assert.Equal(1, changes);
        Assert.Equal(CreatedTime.AddHours(3), service.Current.Document.ModifiedUtc);
    }

    /// <summary>
    ///     Test that a project file from before style kits, which has no such property, is read as having no style, and
    ///     that setting a style keeps the properties of the file that this version does not know.
    /// </summary>
    [Fact]
    public async Task ProjectService_SetActiveStyleKitAsync_OlderFileWithUnknownProperty_ReadsAsNoStyleAndKeepsProperty()
    {
        // Arrange: a project file written by an earlier version and one that has an unknown property
        var file = WriteProjectFile("Old", """{ "formatVersion": 1, "name": "Old", "futureThing": { "a": 7 } }""");
        var service = CreateService();
        var opened = await service.OpenAsync(file, Ct);

        // Act: set a style
        var result = await service.SetActiveStyleKitAsync("dema", Ct);

        // Assert: it read as having no style, and the file has the style and the unknown property
        Assert.Null(opened.Project!.Document.ActiveStyleKit);
        Assert.True(result.Succeeded, result.Error);
        using var json = JsonDocument.Parse(await File.ReadAllTextAsync(file, Ct));
        Assert.Equal("dema", json.RootElement.GetProperty("activeStyleKit").GetString());
        Assert.Equal(7, json.RootElement.GetProperty("futureThing").GetProperty("a").GetInt32());
    }

    /// <summary>
    ///     Test that an id that no style kit can have is refused with a message that says what is allowed, and that the
    ///     file and the open project stay as they were.
    /// </summary>
    /// <param name="id">The id to refuse.</param>
    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("Clean")]
    [InlineData("-clean")]
    [InlineData("has space")]
    [InlineData("under_score")]
    [InlineData("../evil")]
    [InlineData("a\\b")]
    [InlineData("clean\n")]
    [InlineData("0123456789012345678901234567890123456789012345678")]
    public async Task ProjectService_SetActiveStyleKitAsync_InvalidId_IsRefused(string id)
    {
        // Arrange: an open project
        Directory.CreateDirectory(Parent);
        var service = CreateService();
        var created = (await service.CreateAsync(Parent, "Strict", Ct)).Project!;
        var before = await File.ReadAllTextAsync(created.FilePath, Ct);

        // Act
        var result = await service.SetActiveStyleKitAsync(id, Ct);

        // Assert
        Assert.False(result.Succeeded);
        Assert.Contains("not a valid style kit id", result.Error, StringComparison.Ordinal);
        Assert.Equal(before, await File.ReadAllTextAsync(created.FilePath, Ct));
        Assert.Same(created, service.Current);
    }

    /// <summary>
    ///     Test that an id of 48 characters is accepted.
    /// </summary>
    [Fact]
    public async Task ProjectService_SetActiveStyleKitAsync_LongestId_IsAccepted()
    {
        // Arrange
        Directory.CreateDirectory(Parent);
        var service = CreateService();
        await service.CreateAsync(Parent, "Long", Ct);
        var id = "9" + new string('a', 47);

        // Act
        var result = await service.SetActiveStyleKitAsync(id, Ct);

        // Assert
        Assert.True(result.Succeeded, result.Error);
        Assert.Equal(id, service.Current!.Document.ActiveStyleKit);
    }

    /// <summary>
    ///     Test that changing the style with no open project fails with a message and writes nothing.
    /// </summary>
    [Fact]
    public async Task ProjectService_SetActiveStyleKitAsync_NoProject_Fails()
    {
        // Arrange / Act
        var result = await CreateService().SetActiveStyleKitAsync("clean", Ct);

        // Assert
        Assert.False(result.Succeeded);
        Assert.Contains("no open project", result.Error, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    ///     Test that a change that cannot be written is a failed result that is logged, and that the open project keeps
    ///     the style it had.
    /// </summary>
    [Fact]
    public async Task ProjectService_SetActiveStyleKitAsync_FolderRemoved_FailsAndKeepsTheOpenProject()
    {
        // Arrange: an open project whose folder was deleted
        Directory.CreateDirectory(Parent);
        var logger = new RollingFileLogger(Path.Combine(_tempRoot, "logs"));
        var service = new ProjectService(logger, _clock);
        var project = (await service.CreateAsync(Parent, "Gone", Ct)).Project!;
        Directory.Delete(project.FolderPath, recursive: true);

        // Act
        var result = await service.SetActiveStyleKitAsync("clean", Ct);

        // Assert
        Assert.False(result.Succeeded);
        Assert.Contains("could not be saved", result.Error, StringComparison.Ordinal);
        Assert.Same(project, service.Current);
        Assert.Null(service.Current!.Document.ActiveStyleKit);
    }

    /// <summary>
    ///     Test that closing clears the open project and announces the change once, and that closing again does
    ///     nothing.
    /// </summary>
    [Fact]
    public async Task ProjectService_Close_OpenProject_ClearsCurrentAndRaisesEventOnce()
    {
        // Arrange: an open project
        Directory.CreateDirectory(Parent);
        var service = CreateService();
        await service.CreateAsync(Parent, "Closing", Ct);
        var changes = 0;
        service.CurrentChanged += (_, _) => changes++;

        // Act: close twice
        service.Close();
        service.Close();

        // Assert: nothing is open and only the first close was announced
        Assert.Null(service.Current);
        Assert.Equal(1, changes);
    }

    /// <summary>
    ///     Test that a project file can be reopened after it was closed, and saved again.
    /// </summary>
    [Fact]
    public async Task ProjectService_CloseThenOpen_SameFile_OpensAgain()
    {
        // Arrange: a created and closed project
        Directory.CreateDirectory(Parent);
        var service = CreateService();
        var created = (await service.CreateAsync(Parent, "Again", Ct)).Project!;
        service.Close();

        // Act: open the file again and save
        var opened = await service.OpenAsync(created.FilePath, Ct);
        var saved = await service.SaveAsync(Ct);

        // Assert: both worked
        Assert.True(opened.Succeeded, opened.Error);
        Assert.True(saved.Succeeded, saved.Error);
    }

    /// <summary>
    ///     Test that checking a new project reports the name problem first, then the folder problem.
    /// </summary>
    [Fact]
    public void ProjectService_ValidateNewProject_NameProblemComesFirst()
    {
        // Arrange: a service and an existing parent
        Directory.CreateDirectory(Parent);
        var service = CreateService();

        // Act: check combinations
        var badName = service.ValidateNewProject(Parent, "a/b");
        var badFolder = service.ValidateNewProject(Path.Combine(_tempRoot, "nope"), "fine");
        var good = service.ValidateNewProject(Parent, "fine");

        // Assert: the name problem, the folder problem, then no problem
        Assert.Contains("must not contain", badName);
        Assert.Contains("does not exist", badFolder);
        Assert.Null(good);
    }

    /// <summary>
    ///     Creates a service that uses the test clock.
    /// </summary>
    /// <returns>The service.</returns>
    private ProjectService CreateService() => new(timeProvider: _clock);

    /// <summary>
    ///     Writes a project file with the given content into its own folder.
    /// </summary>
    /// <param name="name">The project name, used for the folder and file name.</param>
    /// <param name="content">The file content.</param>
    /// <returns>The full path of the project file.</returns>
    private string WriteProjectFile(string name, string content)
    {
        var folder = Directory.CreateDirectory(Path.Combine(_tempRoot, "opened", name)).FullName;
        var file = Path.Combine(folder, name + ".canvaspilot");
        File.WriteAllText(file, content);
        return file;
    }

    /// <summary>
    ///     A clock that returns a time tests can change.
    /// </summary>
    /// <param name="initial">The first time.</param>
    private sealed class SettableTimeProvider(DateTimeOffset initial) : TimeProvider
    {
        /// <summary>
        ///     Gets or sets the time the clock returns.
        /// </summary>
        public DateTimeOffset UtcNow { get; set; } = initial;

        /// <inheritdoc />
        public override DateTimeOffset GetUtcNow() => UtcNow;
    }

    /// <summary>
    ///     A clock that fails, to simulate an input/output failure during creation.
    /// </summary>
    private sealed class ThrowingTimeProvider : TimeProvider
    {
        /// <inheritdoc />
        public override DateTimeOffset GetUtcNow() => throw new IOException("clock failed");
    }
}
