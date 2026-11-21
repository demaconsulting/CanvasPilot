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
using DemaConsulting.CanvasPilot.StyleSubsystem;
using NSubstitute;

namespace DemaConsulting.CanvasPilot.Tests;

/// <summary>
///     Unit tests for <see cref="StyleKitCatalog" /> on temporary folders: finding the kits in every place, the choice by
///     precedence, a shared folder that cannot be reached, broken kits, the places that are read only, and creating,
///     duplicating, copying, and deleting kits. A folder that hangs or is refused is imitated with a function that lists
///     the folders, so no test needs a network.
/// </summary>
public sealed class StyleKitCatalogTests : IDisposable
{
    /// <summary>
    ///     The places and the services.
    /// </summary>
    private readonly StyleKitPlaces _places = new();

    /// <summary>
    ///     Gets the token that cancels the running test.
    /// </summary>
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    /// <inheritdoc />
    public void Dispose() => _places.Dispose();

    /// <summary>
    ///     Validates that kits are found in the program, the folder of the user, the open project, and every shared folder,
    ///     with the place, the folder, the read-only flag, and the shared folder of each, in the order of the places and then
    ///     by name, and that the places are listed in the order of precedence.
    /// </summary>
    [Fact]
    public async Task StyleKitCatalog_RefreshAsync_KitsInEveryLocation_AreFoundWithTheirPlaces()
    {
        // Arrange: one or two kits in each place, with names that sort the other way than their ids
        StyleKitPlaces.WriteKit(_places.BuiltIn, StyleKitPlaces.Kit("b-one", name: "Beta"));
        StyleKitPlaces.WriteKit(_places.BuiltIn, StyleKitPlaces.Kit("b-two", name: "Alpha"));
        StyleKitPlaces.WriteKit(_places.User, StyleKitPlaces.Kit("u-one"));
        StyleKitPlaces.WriteKit(_places.Shared1, StyleKitPlaces.Kit("s-one"));
        StyleKitPlaces.WriteKit(_places.Shared2, StyleKitPlaces.Kit("s-two"));
        StyleKitPlaces.WriteKit(_places.ProjectStyles, StyleKitPlaces.Kit("p-one"));
        _places.SetSharedFolders(_places.Shared1, _places.Shared2);
        _places.OpenProject();
        using var catalog = _places.CreateCatalog();

        // Act
        await catalog.RefreshAsync(Ct);

        // Assert: the entries are in the order of the places and by name inside a place
        Assert.Equal(["p-one", "u-one", "s-one", "s-two", "b-two", "b-one"], catalog.Entries.Select(e => e.Kit.Id));
        Assert.Equal(
            [StyleKitLocation.Project, StyleKitLocation.User, StyleKitLocation.Shared, StyleKitLocation.Shared, StyleKitLocation.BuiltIn, StyleKitLocation.BuiltIn],
            catalog.Entries.Select(e => e.Location));
        Assert.Equal([false, false, true, true, true, true], catalog.Entries.Select(e => e.IsReadOnly));
        Assert.Equal(_places.Shared1, catalog.Entries.Single(e => e.Kit.Id == "s-one").SharedRoot);
        Assert.Null(catalog.Entries.Single(e => e.Kit.Id == "u-one").SharedRoot);
        Assert.Equal(Path.Combine(_places.User, "u-one"), catalog.Entries.Single(e => e.Kit.Id == "u-one").FolderPath);
        Assert.All(catalog.Entries, e => Assert.False(e.IsOverridden));

        // Assert: the places, in the order of precedence, with their folders
        Assert.Equal(
            [StyleKitLocation.Project, StyleKitLocation.User, StyleKitLocation.Shared, StyleKitLocation.Shared, StyleKitLocation.BuiltIn],
            catalog.Locations.Select(l => l.Location));
        Assert.Equal(
            [_places.ProjectStyles, _places.User, _places.Shared1, _places.Shared2, _places.BuiltIn],
            catalog.Locations.Select(l => l.Path));
        Assert.All(catalog.Locations, l => Assert.True(l.IsAvailable, l.Problem));
    }

    /// <summary>
    ///     Validates that, for every pair of places that hold the same id, the kit of the more local place is the one that is
    ///     found by the id, and the other is still listed and says what hides it.
    /// </summary>
    /// <param name="higher">The more local place.</param>
    /// <param name="lower">The less local place.</param>
    /// <param name="overriddenBy">The name that the entry of the lower place gives to the place that hides it.</param>
    [Theory]
    [InlineData(StyleKitLocation.Project, StyleKitLocation.User, "This project")]
    [InlineData(StyleKitLocation.Project, StyleKitLocation.Shared, "This project")]
    [InlineData(StyleKitLocation.Project, StyleKitLocation.BuiltIn, "This project")]
    [InlineData(StyleKitLocation.User, StyleKitLocation.Shared, "My kits")]
    [InlineData(StyleKitLocation.User, StyleKitLocation.BuiltIn, "My kits")]
    [InlineData(StyleKitLocation.Shared, StyleKitLocation.BuiltIn, "Shared folder")]
    public async Task StyleKitCatalog_Resolve_SameIdInTwoLocations_TheMoreLocalWins(StyleKitLocation higher, StyleKitLocation lower, string overriddenBy)
    {
        // Arrange: the same id in two places, with a description that says which place a copy is in
        WriteIn(higher, "clean", "from-higher");
        WriteIn(lower, "clean", "from-lower");
        _places.SetSharedFolders(_places.Shared1);
        _places.OpenProject();
        using var catalog = _places.CreateCatalog();

        // Act
        await catalog.RefreshAsync(Ct);
        var winner = catalog.Resolve("clean");
        var hidden = catalog.Entries.Single(e => e.IsOverridden);

        // Assert
        Assert.Equal(2, catalog.Entries.Count);
        Assert.Equal("from-higher", winner!.Kit.Description);
        Assert.Equal(higher, winner.Location);
        Assert.False(winner.IsOverridden);
        Assert.Null(winner.OverriddenBy);
        Assert.Equal("from-lower", hidden.Kit.Description);
        Assert.Equal(lower, hidden.Location);
        Assert.StartsWith(overriddenBy, hidden.OverriddenBy, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Validates the whole chain: the same id in the project, the folder of the user, two shared folders, and the
    ///     program is found as the kit of the project, an earlier shared folder wins over a later one, and every other copy
    ///     is listed as hidden by the copy that wins.
    /// </summary>
    [Fact]
    public async Task StyleKitCatalog_Resolve_SameIdEverywhere_ChainFollowsThePrecedence()
    {
        // Arrange
        StyleKitPlaces.WriteKit(_places.ProjectStyles, StyleKitPlaces.Kit("clean", "project"));
        StyleKitPlaces.WriteKit(_places.User, StyleKitPlaces.Kit("clean", "user"));
        StyleKitPlaces.WriteKit(_places.Shared1, StyleKitPlaces.Kit("clean", "shared-1"));
        StyleKitPlaces.WriteKit(_places.Shared2, StyleKitPlaces.Kit("clean", "shared-2"));
        StyleKitPlaces.WriteKit(_places.BuiltIn, StyleKitPlaces.Kit("clean", "builtin"));
        _places.SetSharedFolders(_places.Shared1, _places.Shared2);
        _places.OpenProject();
        using var catalog = _places.CreateCatalog();

        // Act
        await catalog.RefreshAsync(Ct);

        // Assert: five entries in the order of precedence, only the first wins, and the rest name the winner
        Assert.Equal(["project", "user", "shared-1", "shared-2", "builtin"], catalog.Entries.Select(e => e.Kit.Description));
        Assert.Equal([false, true, true, true, true], catalog.Entries.Select(e => e.IsOverridden));
        Assert.Equal("project", catalog.Resolve("clean")!.Kit.Description);
        Assert.All(catalog.Entries.Skip(1), e => Assert.Equal("This project", e.OverriddenBy));

        // Act: the project kit is deleted, so the next place takes over
        catalog.Delete(catalog.Entries[0]);

        // Assert
        Assert.Equal("user", catalog.Resolve("clean")!.Kit.Description);
        Assert.Equal("My kits", catalog.Entries.Single(e => e.Kit.Description == "shared-1").OverriddenBy);
    }

    /// <summary>
    ///     Validates that a lookup of an id that no kit has finds nothing and a missing id is refused.
    /// </summary>
    [Fact]
    public async Task StyleKitCatalog_Resolve_UnknownIdOrNull_FindsNothingOrIsRefused()
    {
        // Arrange
        StyleKitPlaces.WriteKit(_places.User, StyleKitPlaces.Kit("mine"));
        using var catalog = _places.CreateCatalog();

        // Act
        var before = catalog.Resolve("mine");
        await catalog.RefreshAsync(Ct);

        // Assert: nothing is known before the first refresh, and only the id that exists is found afterwards
        Assert.Null(before);
        Assert.NotNull(catalog.Resolve("mine"));
        Assert.Null(catalog.Resolve("other"));
        Assert.Null(catalog.Resolve("MINE"));
        Assert.Throws<ArgumentNullException>(() => catalog.Resolve(null!));
    }

    /// <summary>
    ///     Validates that a shared folder that does not exist is a place that is not available, with a reason, and that the
    ///     other places are still read and the refresh does not fail.
    /// </summary>
    [Fact]
    public async Task StyleKitCatalog_RefreshAsync_MissingSharedFolder_IsReportedAndOthersAreStillRead()
    {
        // Arrange: one shared folder that exists and one that does not
        StyleKitPlaces.WriteKit(_places.Shared1, StyleKitPlaces.Kit("team"));
        StyleKitPlaces.WriteKit(_places.User, StyleKitPlaces.Kit("mine"));
        _places.SetSharedFolders(_places.Shared2, _places.Shared1);
        using var catalog = _places.CreateCatalog();

        // Act
        await catalog.RefreshAsync(Ct);

        // Assert
        var missing = catalog.Locations.Single(l => l.Path == _places.Shared2);
        Assert.False(missing.IsAvailable);
        Assert.Equal("The folder was not found or cannot be reached.", missing.Problem);
        Assert.True(catalog.Locations.Single(l => l.Path == _places.Shared1).IsAvailable);
        Assert.Equal(["mine", "team"], catalog.Entries.Select(e => e.Kit.Id).Order());
    }

    /// <summary>
    ///     Validates that a shared folder that does not answer within the time limit is reported as not reachable, that the
    ///     refresh finishes and the other places are read, and that the kits of that folder are not listed. A function that
    ///     lists the folders and blocks imitates the folder.
    /// </summary>
    [Fact]
    public async Task StyleKitCatalog_RefreshAsync_SharedFolderThatHangs_TimesOutAndOthersAreStillRead()
    {
        // Arrange: a shared folder whose listing blocks until the test lets it go
        StyleKitPlaces.WriteKit(_places.User, StyleKitPlaces.Kit("mine"));
        Directory.CreateDirectory(_places.Shared1);
        Directory.CreateDirectory(_places.Shared2);
        StyleKitPlaces.WriteKit(_places.Shared2, StyleKitPlaces.Kit("team"));
        _places.SetSharedFolders(_places.Shared1, _places.Shared2);
        using var release = new ManualResetEventSlim(false);
        using var catalog = _places.CreateCatalog(
            TimeSpan.FromMilliseconds(200),
            folder =>
            {
                if (string.Equals(folder, _places.Shared1, StringComparison.OrdinalIgnoreCase))
                {
                    release.Wait(TimeSpan.FromSeconds(60), Ct);
                }

                return StyleKitScanner.ListSubFolders(folder);
            });

        try
        {
            // Act: the refresh must finish while the listing of the first folder is still blocked
            await catalog.RefreshAsync(Ct).WaitAsync(TimeSpan.FromSeconds(30), Ct);

            // Assert
            var hung = catalog.Locations.Single(l => l.Path == _places.Shared1);
            Assert.False(hung.IsAvailable);
            Assert.Contains("did not answer", hung.Problem, StringComparison.Ordinal);
            Assert.True(catalog.Locations.Single(l => l.Path == _places.Shared2).IsAvailable);
            Assert.Equal(["mine", "team"], catalog.Entries.Select(e => e.Kit.Id).Order());
        }
        finally
        {
            release.Set();
        }
    }

    /// <summary>
    ///     Validates that a shared folder that cannot be read (access is denied, the network failed) is not available, with
    ///     the reason, and does not fail the refresh.
    /// </summary>
    [Fact]
    public async Task StyleKitCatalog_RefreshAsync_SharedFolderThatIsRefused_IsNotAvailableWithTheReason()
    {
        // Arrange: a shared folder whose listing fails with access denied and one that fails with an unexpected error
        Directory.CreateDirectory(_places.Shared1);
        Directory.CreateDirectory(_places.Shared2);
        _places.SetSharedFolders(_places.Shared1, _places.Shared2);
        using var catalog = _places.CreateCatalog(
            TimeSpan.FromSeconds(5),
            folder =>
            {
                if (string.Equals(folder, _places.Shared1, StringComparison.OrdinalIgnoreCase))
                {
                    throw new UnauthorizedAccessException("Access to the path is denied.");
                }

                return string.Equals(folder, _places.Shared2, StringComparison.OrdinalIgnoreCase)
                    ? throw new InvalidProgramException("unexpected")
                    : StyleKitScanner.ListSubFolders(folder);
            });

        // Act
        await catalog.RefreshAsync(Ct);

        // Assert
        var denied = catalog.Locations.Single(l => l.Path == _places.Shared1);
        Assert.False(denied.IsAvailable);
        Assert.Equal("The folder could not be read: Access to the path is denied.", denied.Problem);
        var unexpected = catalog.Locations.Single(l => l.Path == _places.Shared2);
        Assert.False(unexpected.IsAvailable);
        Assert.StartsWith("The folder could not be read:", unexpected.Problem, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Validates that a kit folder with errors is listed as broken with the reasons and is not a kit, that a folder with
    ///     no style file and a hidden folder are ignored, that a second folder with the same id is broken, and that the
    ///     other kits of the place are found.
    /// </summary>
    [Fact]
    public async Task StyleKitCatalog_RefreshAsync_BrokenFolders_AreIsolatedAndListed()
    {
        // Arrange: a good kit, a kit with a bad color, a folder with no style file, a hidden folder, and a repeated id
        StyleKitPlaces.WriteKit(_places.User, StyleKitPlaces.Kit("good"));
        var bad = Directory.CreateDirectory(Path.Combine(_places.User, "bad")).FullName;
        await File.WriteAllTextAsync(
            Path.Combine(bad, "style.json"),
            """{ "formatVersion": 1, "id": "bad", "name": "Bad", "palette": { "background": "#FFFFFF", "text": "red", "primary": "#000000" } }""",
            Ct);
        Directory.CreateDirectory(Path.Combine(_places.User, "not-a-kit"));
        await File.WriteAllTextAsync(Path.Combine(_places.User, "not-a-kit", "notes.txt"), "x", Ct);
        var hidden = Directory.CreateDirectory(Path.Combine(_places.User, ".good.0123.tmp")).FullName;
        await File.WriteAllTextAsync(Path.Combine(hidden, "style.json"), "{ not json", Ct);
        var copy = Directory.CreateDirectory(Path.Combine(_places.User, "good-copy")).FullName;
        File.Copy(Path.Combine(_places.User, "good", "style.json"), Path.Combine(copy, "style.json"));
        using var catalog = _places.CreateCatalog();

        // Act
        await catalog.RefreshAsync(Ct);

        // Assert: only the good kit, and the two broken folders with their reasons
        Assert.Equal(["good"], catalog.Entries.Select(e => e.Kit.Id));
        var user = catalog.Locations.Single(l => l.Location == StyleKitLocation.User);
        Assert.True(user.IsAvailable);
        Assert.Equal([bad, copy], user.Broken.Select(b => b.FolderPath).Order());
        Assert.Contains(user.Broken.Single(b => b.FolderPath == bad).Issues, i => i.ToString() == "palette.text: 'red' is not a color; use #RRGGBB.");
        Assert.Contains(user.Broken.Single(b => b.FolderPath == copy).Issues, i => i.Message.Contains("is also used by the kit in", StringComparison.Ordinal));
    }

    /// <summary>
    ///     Validates that a folder that is named differently than the id of its kit is read, with the warning of the reader
    ///     on the entry.
    /// </summary>
    [Fact]
    public async Task StyleKitCatalog_RefreshAsync_FolderNamedDifferentlyThanTheId_IsReadWithWarning()
    {
        // Arrange
        var folder = Directory.CreateDirectory(Path.Combine(_places.User, "other-name")).FullName;
        await File.WriteAllTextAsync(Path.Combine(folder, "style.json"), StyleKitSerializer.Write(StyleKitPlaces.Kit("real-id")), Ct);
        using var catalog = _places.CreateCatalog();

        // Act
        await catalog.RefreshAsync(Ct);

        // Assert
        var entry = Assert.Single(catalog.Entries);
        Assert.Equal("real-id", entry.Kit.Id);
        Assert.Equal(folder, entry.FolderPath);
        Assert.Contains(entry.Warnings, w => w.Path == "id" && w.Message.Contains("'other-name'", StringComparison.Ordinal));
    }

    /// <summary>
    ///     Validates that the places of the user and of the project are empty and available when their folders do not exist
    ///     yet, that the kits that come with the program are not available when their folder is missing, and that no project
    ///     place is listed while no project is open.
    /// </summary>
    [Fact]
    public async Task StyleKitCatalog_RefreshAsync_MissingLocalFolders_AreEmptyOrReportedAndNoProjectMeansNoProjectPlace()
    {
        // Arrange: nothing is written, and the folder of the program is removed
        Directory.Delete(_places.BuiltIn);
        using var catalog = _places.CreateCatalog();

        // Act
        await catalog.RefreshAsync(Ct);

        // Assert: the folder of the user is available and empty, there is no project place, the program's place is missing
        Assert.Empty(catalog.Entries);
        Assert.Equal([StyleKitLocation.User, StyleKitLocation.BuiltIn], catalog.Locations.Select(l => l.Location));
        Assert.True(catalog.Locations[0].IsAvailable);
        Assert.False(catalog.Locations[1].IsAvailable);
        Assert.Equal("The kits that come with the program were not found beside the program.", catalog.Locations[1].Problem);

        // Act: a project is opened that has no styles folder yet
        _places.OpenProject();
        await catalog.PendingRefresh;

        // Assert: the project place is listed, available, and empty
        Assert.Equal(StyleKitLocation.Project, catalog.Locations[0].Location);
        Assert.True(catalog.Locations[0].IsAvailable);
    }

    /// <summary>
    ///     Validates that the catalog reads again when the open project changes: the kits of the project appear when it is
    ///     opened and are gone when it is closed.
    /// </summary>
    [Fact]
    public async Task StyleKitCatalog_ProjectChanged_RefreshesTheKitsOfTheProject()
    {
        // Arrange: a project with a kit, and a catalog that was refreshed before the project was open
        StyleKitPlaces.WriteKit(_places.ProjectStyles, StyleKitPlaces.Kit("frozen"));
        using var catalog = _places.CreateCatalog();
        await catalog.RefreshAsync(Ct);
        var before = catalog.Entries.Count;
        var changes = 0;
        catalog.Changed += (_, _) => Interlocked.Increment(ref changes);

        // Act: open the project, then close it
        _places.OpenProject();
        await catalog.PendingRefresh;
        var whileOpen = catalog.Entries.Select(e => e.Kit.Id).ToList();
        _places.CloseProject();
        await catalog.PendingRefresh;

        // Assert
        Assert.Equal(0, before);
        Assert.Equal(["frozen"], whileOpen);
        Assert.Empty(catalog.Entries);
        Assert.Equal(2, Volatile.Read(ref changes));
    }

    /// <summary>
    ///     Validates that a change of the shared folders in the settings reads the catalog again, and that a change of
    ///     another setting does not.
    /// </summary>
    [Fact]
    public async Task StyleKitCatalog_SettingsChanged_OnlyAChangeOfSharedFoldersRefreshes()
    {
        // Arrange: a shared folder that is not listed yet
        StyleKitPlaces.WriteKit(_places.Shared1, StyleKitPlaces.Kit("team"));
        using var catalog = _places.CreateCatalog();
        await catalog.RefreshAsync(Ct);
        var changes = 0;
        catalog.Changed += (_, _) => Interlocked.Increment(ref changes);

        // Act: change another setting, then the shared folders
        _places.Settings.Update(s => s with { AiProvider = s.AiProvider with { ModelName = "llama3" } });
        await catalog.PendingRefresh;
        var afterOther = Volatile.Read(ref changes);
        _places.SetSharedFolders(_places.Shared1);
        await catalog.PendingRefresh;

        // Assert
        Assert.Equal(0, afterOther);
        Assert.Equal(1, Volatile.Read(ref changes));
        Assert.Equal(["team"], catalog.Entries.Select(e => e.Kit.Id));
    }

    /// <summary>
    ///     Validates that one refresh raises the change event once, and that a refresh that is cancelled before it is applied
    ///     leaves the entries as they were.
    /// </summary>
    [Fact]
    public async Task StyleKitCatalog_RefreshAsync_RaisesChangedOnceAndCancelledRefreshChangesNothing()
    {
        // Arrange
        StyleKitPlaces.WriteKit(_places.User, StyleKitPlaces.Kit("mine"));
        using var catalog = _places.CreateCatalog();
        var changes = 0;
        catalog.Changed += (_, _) => changes++;
        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync();

        // Act
        await catalog.RefreshAsync(Ct);
        var applied = catalog.Entries;
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => catalog.RefreshAsync(cancelled.Token));

        // Assert
        Assert.Equal(1, changes);
        Assert.Same(applied, catalog.Entries);
    }

    /// <summary>
    ///     Validates that the kits that come with the program and the shared kits cannot be saved, created, duplicated into,
    ///     or deleted, and that their entries are marked read only.
    /// </summary>
    [Fact]
    public async Task StyleKitCatalog_ReadOnlyPlaces_RefuseEveryChange()
    {
        // Arrange
        StyleKitPlaces.WriteKit(_places.BuiltIn, StyleKitPlaces.Kit("clean"));
        StyleKitPlaces.WriteKit(_places.Shared1, StyleKitPlaces.Kit("team"));
        _places.SetSharedFolders(_places.Shared1);
        using var catalog = _places.CreateCatalog();
        await catalog.RefreshAsync(Ct);
        var builtIn = catalog.Resolve("clean")!;
        var shared = catalog.Resolve("team")!;

        // Act / Assert
        Assert.True(builtIn.IsReadOnly);
        Assert.True(shared.IsReadOnly);
        foreach (var target in new[] { StyleKitLocation.BuiltIn, StyleKitLocation.Shared })
        {
            Assert.Throws<InvalidOperationException>(() => catalog.Save(StyleKitPlaces.Kit("x"), target));
            Assert.Throws<InvalidOperationException>(() => catalog.CreateBlank("x", target));
            Assert.Throws<InvalidOperationException>(() => catalog.Duplicate(builtIn, "x", target));
        }

        Assert.Throws<InvalidOperationException>(() => catalog.Delete(builtIn));
        Assert.Throws<InvalidOperationException>(() => catalog.Delete(shared));
        Assert.True(Directory.Exists(builtIn.FolderPath));
        Assert.True(Directory.Exists(shared.FolderPath));
    }

    /// <summary>
    ///     Validates that saving a kit to the folder of the user writes a folder named as the id, keeps the example files
    ///     that are in it, returns the entry, raises the change event, and refuses a kit that has an error.
    /// </summary>
    [Fact]
    public async Task StyleKitCatalog_Save_UserKit_WritesFolderKeepsExamplesAndRefusesInvalidKit()
    {
        // Arrange: a kit of the user with an example file, and a change of its description
        var folder = StyleKitPlaces.WriteKit(_places.User, StyleKitPlaces.Kit("mine", "old"), "logo.svg");
        using var catalog = _places.CreateCatalog();
        await catalog.RefreshAsync(Ct);
        var original = catalog.Resolve("mine")!;
        var changes = 0;
        catalog.Changed += (_, _) => changes++;

        // Act
        var saved = catalog.Save(original.Kit with { Description = "new" }, StyleKitLocation.User);
        var invalid = Assert.Throws<ArgumentException>(() => catalog.Save(original.Kit with { Id = "Bad Id" }, StyleKitLocation.User));

        // Assert: the entry and the file have the change, the example is still there, and no temporary file remains
        Assert.Equal("new", saved.Kit.Description);
        Assert.Equal(StyleKitLocation.User, saved.Location);
        Assert.Equal(folder, saved.FolderPath);
        Assert.Equal("new", StyleKitSerializer.ReadFolder(folder).Kit!.Description);
        Assert.True(File.Exists(Path.Combine(folder, "examples", "logo.svg")));
        Assert.Equal(["examples", "style.json"], Directory.GetFileSystemEntries(folder).Select(Path.GetFileName).Order());
        Assert.Equal(1, changes);
        Assert.Contains("'Bad Id' is not a valid id", invalid.Message, StringComparison.Ordinal);
        Assert.Throws<ArgumentNullException>(() => catalog.Save(null!, StyleKitLocation.User));
    }

    /// <summary>
    ///     Validates that saving to the project writes into its styles folder, and that saving to the project with no project
    ///     open is refused.
    /// </summary>
    [Fact]
    public async Task StyleKitCatalog_Save_ProjectKit_WritesIntoTheStylesFolderOfTheProjectAndNeedsAProject()
    {
        // Arrange: no project is open yet
        using var catalog = _places.CreateCatalog();
        await catalog.RefreshAsync(Ct);

        // Act / Assert: refused with no project
        Assert.Throws<InvalidOperationException>(() => catalog.Save(StyleKitPlaces.Kit("frozen"), StyleKitLocation.Project));
        Assert.Throws<InvalidOperationException>(() => catalog.CreateBlank("Frozen", StyleKitLocation.Project));

        // Act: open the project and save
        _places.OpenProject();
        await catalog.PendingRefresh;
        var saved = catalog.Save(StyleKitPlaces.Kit("frozen"), StyleKitLocation.Project);

        // Assert
        Assert.Equal(StyleKitLocation.Project, saved.Location);
        Assert.Equal(Path.Combine(_places.ProjectStyles, "frozen"), saved.FolderPath);
        Assert.False(saved.IsReadOnly);
        Assert.True(File.Exists(Path.Combine(_places.ProjectStyles, "frozen", "style.json")));
    }

    /// <summary>
    ///     Validates that a new kit has a valid palette, an id that is made from its name and that no known kit has (also
    ///     not a kit of another place), numbers when the id is taken, and the name <c>New kit</c> when none is given.
    /// </summary>
    [Fact]
    public async Task StyleKitCatalog_CreateBlank_Names_GetUniqueIdsAndAValidKit()
    {
        // Arrange: a kit that comes with the program with the id that a kit named Clean would have
        StyleKitPlaces.WriteKit(_places.BuiltIn, StyleKitPlaces.Kit("clean"));
        using var catalog = _places.CreateCatalog();
        await catalog.RefreshAsync(Ct);

        // Act
        var first = catalog.CreateBlank("New kit", StyleKitLocation.User);
        var second = catalog.CreateBlank("New kit", StyleKitLocation.User);
        var clean = catalog.CreateBlank("Clean", StyleKitLocation.User);
        var unnamed = catalog.CreateBlank("  ", StyleKitLocation.User);
        var symbols = catalog.CreateBlank("!!!", StyleKitLocation.User);

        // Assert: ids are made from the names and are unique, also against the kit of the program
        Assert.Equal(["new-kit", "new-kit-2", "clean-2", "new-kit-3", "new-kit-4"], new[] { first, second, clean, unnamed, symbols }.Select(e => e.Kit.Id));
        Assert.Equal("New kit", first.Kit.Name);
        Assert.Equal("Clean", clean.Kit.Name);
        Assert.Equal("New kit", unnamed.Kit.Name);
        Assert.Empty(StyleKitSerializer.Validate(first.Kit));
        Assert.Equal(["background", "text", "primary"], first.Kit.Palette.Keys);
        Assert.All(new[] { first, second, clean, unnamed, symbols }, e => Assert.Equal(StyleKitLocation.User, e.Location));
        Assert.Equal(StyleKitLocation.BuiltIn, catalog.Resolve("clean")!.Location);
    }

    /// <summary>
    ///     Validates the ids that are made from names: lower-case letters and digits with a dash for each run of other
    ///     characters, no dash at an end, at most 40 characters, and <c>new-kit</c> when nothing is left.
    /// </summary>
    /// <param name="name">The name.</param>
    /// <param name="expected">The base of the id.</param>
    [Theory]
    [InlineData("Team Look", "team-look")]
    [InlineData("  --Slides: Dark / 16:9--  ", "slides-dark-16-9")]
    [InlineData("A", "a")]
    [InlineData("9 lives", "9-lives")]
    [InlineData("!!!", "new-kit")]
    [InlineData("", "new-kit")]
    [InlineData("Über Cool", "ber-cool")]
    [InlineData("aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa bbbbbbbb", "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa")]
    [InlineData("aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa bbbbbbbb", "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa")]
    public void StyleKitCatalog_IdBaseFromName_FollowsTheRules(string name, string expected)
    {
        // Act
        var id = StyleKitCatalog.IdBaseFromName(name);

        // Assert
        Assert.Equal(expected, id);
        Assert.True(StyleKitSerializer.IsValidId(id));
    }

    /// <summary>
    ///     Validates that a duplicate has a new id made from the new name, the same content, and a copy of each example file
    ///     with the same bytes and captions, that it can come from any place, and that the original is not changed.
    /// </summary>
    [Fact]
    public async Task StyleKitCatalog_Duplicate_KitWithExamples_CopiesEverythingUnderANewId()
    {
        // Arrange: a built-in kit with two example files and a caption
        var kit = StyleKitPlaces.Kit("clean", "Light") with
        {
            Examples = [new StyleExample("examples/card.svg", string.Empty, "A card.")]
        };
        var source = StyleKitPlaces.WriteKit(_places.BuiltIn, kit, "card.svg", "flow.mmd");
        using var catalog = _places.CreateCatalog();
        await catalog.RefreshAsync(Ct);
        _places.OpenProject();
        await catalog.PendingRefresh;
        var original = catalog.Resolve("clean")!;

        // Act: copy it to the user and to the project
        var toUser = catalog.Duplicate(original, "My Clean", StyleKitLocation.User);
        var toProject = catalog.Duplicate(original, "My Clean", StyleKitLocation.Project);

        // Assert: new ids, same content, new name, copied examples, and the original is as it was
        Assert.Equal("my-clean", toUser.Kit.Id);
        Assert.Equal("my-clean-2", toProject.Kit.Id);
        Assert.Equal("My Clean", toUser.Kit.Name);
        Assert.Equal("Light", toUser.Kit.Description);
        Assert.Equal(original.Kit.Palette, toUser.Kit.Palette);
        Assert.Equal(["examples/card.svg", "examples/flow.mmd"], toUser.Kit.Examples.Select(e => e.RelativePath));
        Assert.Equal("A card.", toUser.Kit.Examples[0].Caption);
        Assert.Equal(Path.Combine(_places.User, "my-clean", "examples", "card.svg"), toUser.Kit.Examples[0].FullPath);
        Assert.Equal("flow.mmd", await File.ReadAllTextAsync(Path.Combine(_places.User, "my-clean", "examples", "flow.mmd"), Ct));
        Assert.Equal(StyleKitLocation.Project, toProject.Location);
        Assert.True(File.Exists(Path.Combine(_places.ProjectStyles, "my-clean-2", "examples", "card.svg")));
        Assert.Equal(["card.svg", "flow.mmd"], Directory.GetFiles(Path.Combine(source, "examples")).Select(Path.GetFileName).Order());
        Assert.Equal(StyleKitSerializer.Write(kit), await File.ReadAllTextAsync(Path.Combine(source, "style.json"), Ct));
        Assert.Equal(".", Path.GetRelativePath(_places.User, Path.GetDirectoryName(toUser.FolderPath)!));
        Assert.Equal(["my-clean"], Directory.GetDirectories(_places.User).Select(Path.GetFileName));
    }

    /// <summary>
    ///     Validates that removing a kit of the user or of the project removes its folder and the entry, and that an entry
    ///     whose folder is not directly in the place that it says it is in is refused and removes nothing.
    /// </summary>
    [Fact]
    public async Task StyleKitCatalog_Delete_UserAndProjectKits_RemoveFolderAndRefuseOtherFolders()
    {
        // Arrange: a kit of the user, a kit of the project, and a folder beside them that must survive
        var mine = StyleKitPlaces.WriteKit(_places.User, StyleKitPlaces.Kit("mine"), "a.svg");
        var frozen = StyleKitPlaces.WriteKit(_places.ProjectStyles, StyleKitPlaces.Kit("frozen"));
        var other = Directory.CreateDirectory(Path.Combine(_places.Root, "other")).FullName;
        await File.WriteAllTextAsync(Path.Combine(other, "keep.txt"), "x", Ct);
        _places.OpenProject();
        using var catalog = _places.CreateCatalog();
        await catalog.RefreshAsync(Ct);
        var mineEntry = catalog.Resolve("mine")!;
        var frozenEntry = catalog.Resolve("frozen")!;

        // Act / Assert: entries that point at other folders are refused
        Assert.Throws<InvalidOperationException>(() => catalog.Delete(mineEntry with { FolderPath = other }));
        Assert.Throws<InvalidOperationException>(() => catalog.Delete(mineEntry with { FolderPath = Path.Combine(mine, "..", "..", "other") }));
        Assert.Throws<InvalidOperationException>(() => catalog.Delete(mineEntry with { FolderPath = _places.User }));
        Assert.Throws<InvalidOperationException>(() => catalog.Delete(mineEntry with { Location = StyleKitLocation.Project }));
        Assert.True(Directory.Exists(other));
        Assert.True(Directory.Exists(mine));

        // Act: the real entries
        catalog.Delete(mineEntry);
        catalog.Delete(frozenEntry);

        // Assert
        Assert.False(Directory.Exists(mine));
        Assert.False(Directory.Exists(frozen));
        Assert.Empty(catalog.Entries);
        Assert.True(File.Exists(Path.Combine(other, "keep.txt")));
        Assert.Throws<ArgumentNullException>(() => catalog.Delete(null!));
    }

    /// <summary>
    ///     Validates that a kit is copied into the project with its id and examples and then wins over the original, that
    ///     it is refused with no project open and when the project already has the id.
    /// </summary>
    [Fact]
    public async Task StyleKitCatalog_CopyIntoProject_SharedKit_FreezesItInTheProject()
    {
        // Arrange: a shared kit with an example
        StyleKitPlaces.WriteKit(_places.Shared1, StyleKitPlaces.Kit("team", "shared"), "logo.svg");
        _places.SetSharedFolders(_places.Shared1);
        using var catalog = _places.CreateCatalog();
        await catalog.RefreshAsync(Ct);
        var shared = catalog.Resolve("team")!;

        // Act / Assert: refused with no project
        Assert.Throws<InvalidOperationException>(() => catalog.CopyIntoProject(shared));

        // Act: open the project and copy
        _places.OpenProject();
        await catalog.PendingRefresh;
        var copy = catalog.CopyIntoProject(catalog.Resolve("team")!);

        // Assert: the copy is in the project and wins, the shared kit is hidden, and a second copy is refused
        Assert.Equal(StyleKitLocation.Project, copy.Location);
        Assert.Equal("team", copy.Kit.Id);
        Assert.Equal(Path.Combine(_places.ProjectStyles, "team"), copy.FolderPath);
        Assert.True(File.Exists(Path.Combine(copy.FolderPath, "examples", "logo.svg")));
        Assert.Equal("team", catalog.Resolve("team")!.Kit.Id);
        Assert.Equal(StyleKitLocation.Project, catalog.Resolve("team")!.Location);
        Assert.True(catalog.Entries.Single(e => e.Location == StyleKitLocation.Shared).IsOverridden);
        Assert.Throws<InvalidOperationException>(() => catalog.CopyIntoProject(shared));
        Assert.Throws<ArgumentNullException>(() => catalog.CopyIntoProject(null!));
    }

    /// <summary>
    ///     Validates that a failure while copying leaves no folder behind, not even a hidden one, and no kit.
    /// </summary>
    [Fact]
    public async Task StyleKitCatalog_Duplicate_ExampleFileMissing_LeavesNothingBehind()
    {
        // Arrange: a kit that lists an example whose file was removed after it was read
        var kit = StyleKitPlaces.Kit("clean") with { Examples = [new StyleExample("examples/gone.svg", string.Empty, null)] };
        StyleKitPlaces.WriteKit(_places.BuiltIn, kit, "gone.svg");
        using var catalog = _places.CreateCatalog();
        await catalog.RefreshAsync(Ct);
        var entry = catalog.Resolve("clean")!;
        File.Delete(Path.Combine(entry.FolderPath, "examples", "gone.svg"));

        // Act / Assert
        Assert.ThrowsAny<IOException>(() => catalog.Duplicate(entry, "Copy", StyleKitLocation.User));
        Assert.Empty(Directory.GetFileSystemEntries(_places.User));
    }

    /// <summary>
    ///     Validates that the constructor refuses missing services.
    /// </summary>
    [Fact]
    public void StyleKitCatalog_Constructor_NullServices_ThrowArgumentNullException()
    {
        // Act / Assert
        Assert.Throws<ArgumentNullException>(() => new StyleKitCatalog(null!, _places.Settings));
        Assert.Throws<ArgumentNullException>(() => new StyleKitCatalog(_places.Projects, null!));
    }

    /// <summary>
    ///     Validates that a place with more kit folders than the limit is read up to the limit and says so.
    /// </summary>
    [Fact]
    public async Task StyleKitCatalog_RefreshAsync_MoreKitFoldersThanTheLimit_ReadsTheLimitAndSaysSo()
    {
        // Arrange: one folder more than the limit
        foreach (var number in Enumerable.Range(0, StyleKitScanner.MaxKitsPerRoot + 1))
        {
            StyleKitPlaces.WriteKit(_places.User, StyleKitPlaces.Kit($"kit-{number:000}"));
        }

        using var catalog = _places.CreateCatalog();

        // Act
        await catalog.RefreshAsync(Ct);

        // Assert
        Assert.Equal(StyleKitScanner.MaxKitsPerRoot, catalog.Entries.Count);
        Assert.Equal($"Only the first {StyleKitScanner.MaxKitsPerRoot} kit folders are read.", catalog.Locations.Single(l => l.Location == StyleKitLocation.User).Problem);
    }

    /// <summary>
    ///     Validates that a broken kit is logged, so that support can see why a kit does not show.
    /// </summary>
    [Fact]
    public async Task StyleKitCatalog_RefreshAsync_BrokenKit_IsLogged()
    {
        // Arrange
        var folder = Directory.CreateDirectory(Path.Combine(_places.User, "bad")).FullName;
        await File.WriteAllTextAsync(Path.Combine(folder, "style.json"), "{ not json", Ct);
        var logger = new RollingFileLogger(Path.Combine(_places.Root, "logs"));
        using var catalog = new StyleKitCatalog(_places.Projects, _places.Settings, _places.BuiltIn, _places.User, TimeSpan.FromSeconds(5), logger);

        // Act
        await catalog.RefreshAsync(Ct);

        // Assert
        var log = await File.ReadAllTextAsync(logger.ActiveFilePath, Ct);
        Assert.Contains("[Warning]", log, StringComparison.Ordinal);
        Assert.Contains("cannot be used", log, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Writes a kit into the place of a location, using the first shared folder for a shared kit.
    /// </summary>
    /// <param name="location">The place.</param>
    /// <param name="id">The id of the kit.</param>
    /// <param name="description">The description that tells copies apart.</param>
    private void WriteIn(StyleKitLocation location, string id, string description)
    {
        var root = location switch
        {
            StyleKitLocation.Project => _places.ProjectStyles,
            StyleKitLocation.User => _places.User,
            StyleKitLocation.Shared => _places.Shared1,
            _ => _places.BuiltIn
        };
        StyleKitPlaces.WriteKit(root, StyleKitPlaces.Kit(id, description));
    }
}
