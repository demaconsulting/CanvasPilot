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
using DemaConsulting.CanvasPilot.PreviewSubsystem;
using DemaConsulting.CanvasPilot.ProjectSubsystem;
using DemaConsulting.CanvasPilot.SettingsSubsystem;
using DemaConsulting.CanvasPilot.StyleSubsystem;

namespace DemaConsulting.CanvasPilot.Tests;

/// <summary>
///     Unit tests for <see cref="StyleSelection" /> over a real project service, a real settings service, and a real
///     catalog of kits on temporary folders: which kit the open project uses in every case, that the choice is kept in the
///     project file, that a new project starts with the default kit once, and the export sizes.
/// </summary>
public sealed class StyleSelectionTests : IDisposable
{
    /// <summary>
    ///     The folder that holds everything, removed on disposal.
    /// </summary>
    private readonly string _root = Directory.CreateTempSubdirectory("canvaspilot-style-selection-").FullName;

    /// <summary>
    ///     The folder of the kits that come with the program.
    /// </summary>
    private readonly string _builtIn;

    /// <summary>
    ///     The folder of the kits of the user.
    /// </summary>
    private readonly string _user;

    /// <summary>
    ///     The real project service.
    /// </summary>
    private readonly ProjectService _projects = new();

    /// <summary>
    ///     The real settings service.
    /// </summary>
    private readonly SettingsService _settings;

    /// <summary>
    ///     The catalog of kits.
    /// </summary>
    private readonly StyleKitCatalog _catalog;

    /// <summary>
    ///     The unit under test.
    /// </summary>
    private readonly StyleSelection _selection;

    /// <summary>
    ///     Initializes a new instance of the <see cref="StyleSelectionTests" /> class with two built-in kits.
    /// </summary>
    public StyleSelectionTests()
    {
        _builtIn = Directory.CreateDirectory(Path.Combine(_root, "builtin")).FullName;
        _user = Path.Combine(_root, "user");
        _settings = new SettingsService(Path.Combine(_root, "settings"));
        _settings.Load();
        StyleKitPlaces.WriteKit(_builtIn, StyleKitPlaces.Kit("clean", "built-in clean", "Clean"));
        StyleKitPlaces.WriteKit(_builtIn, StyleKitPlaces.Kit("slides-dark", "built-in dark", "Slides Dark") with
        {
            Palette = new Dictionary<string, string> { ["background"] = "#0F172A", ["text"] = "#F1F5F9", ["primary"] = "#60A5FA" }
        });
        _catalog = new StyleKitCatalog(_projects, _settings, _builtIn, _user, TimeSpan.FromSeconds(5));
        _selection = new StyleSelection(_projects, _catalog, _settings);
    }

    /// <summary>
    ///     Gets the token that cancels the running test.
    /// </summary>
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    /// <inheritdoc />
    public void Dispose()
    {
        _selection.Dispose();
        _catalog.Dispose();
        _projects.Close();

        // A refresh that the project change started may still be reading the files; try again for a moment
        for (var attempt = 0; attempt < 40 && Directory.Exists(_root); attempt++)
        {
            try
            {
                Directory.Delete(_root, recursive: true);
            }
            catch (IOException)
            {
                Thread.Sleep(50);
            }
        }
    }

    /// <summary>
    ///     Test that with no project open, and with a project that has no style, there is no active kit and nothing is
    ///     missing.
    /// </summary>
    [Fact]
    public async Task StyleSelection_Active_NoProjectOrNoStyle_IsNullAndNotMissing()
    {
        // Arrange / Act: ask with no project, then with a new project that has no style
        await _catalog.RefreshAsync(Ct);
        var withoutProject = (_selection.Active, _selection.ActiveId, _selection.IsMissing);
        await OpenProjectAsync("Plain");
        var withoutStyle = (_selection.Active, _selection.ActiveId, _selection.IsMissing);

        // Assert: in neither case is there a kit, an id, or a missing kit
        Assert.Equal((null, null, false), withoutProject);
        Assert.Equal((null, null, false), withoutStyle);
    }

    /// <summary>
    ///     Test that a project that names a kit that the catalog has uses that kit, and that the answer is the entry of the
    ///     catalog.
    /// </summary>
    [Fact]
    public async Task StyleSelection_Active_ProjectNamesAKnownKit_ResolvesItThroughTheCatalog()
    {
        // Arrange: a project that uses a built-in kit
        await OpenProjectAsync("Styled");
        await _selection.SetActiveAsync("slides-dark");

        // Act / Assert: the kit is the one of the catalog, with its place
        Assert.Equal("slides-dark", _selection.ActiveId);
        Assert.Same(_catalog.Resolve("slides-dark"), _selection.Active);
        Assert.Equal(StyleKitLocation.BuiltIn, _selection.Active!.Location);
        Assert.False(_selection.IsMissing);
    }

    /// <summary>
    ///     Test that a project that names a kit that nothing has is missing, with no active kit, that the kit is found when
    ///     it appears, and that it is missing again when it is deleted.
    /// </summary>
    [Fact]
    public async Task StyleSelection_Active_UnknownKitThenAddedThenDeleted_IsMissingFoundAndMissingAgain()
    {
        // Arrange: a project that names a kit that nobody has yet
        await OpenProjectAsync("Gone");
        await _selection.SetActiveAsync("team-kit");
        var raised = 0;
        _selection.Changed += (_, _) => Interlocked.Increment(ref raised);

        // Act: look, add the kit as a kit of the user, look, delete it, look
        var missing = (_selection.Active, _selection.IsMissing, _selection.ActiveId);
        var added = _catalog.Save(StyleKitPlaces.Kit("team-kit"), StyleKitLocation.User);
        var found = (_selection.Active?.Kit.Id, _selection.IsMissing);
        _catalog.Delete(added);
        var deleted = (_selection.Active, _selection.IsMissing);

        // Assert: missing with its id kept, found, and missing again, and the changes were announced
        Assert.Equal((null, true, "team-kit"), missing);
        Assert.Equal(("team-kit", false), found);
        Assert.Equal((null, true), deleted);
        Assert.True(raised >= 2);
    }

    /// <summary>
    ///     Test that the answer follows the open project: another project, a project with no style, and a closed project.
    /// </summary>
    [Fact]
    public async Task StyleSelection_Active_ProjectSwitchedOrClosed_FollowsTheOpenProject()
    {
        // Arrange: a project that uses a kit, and another that uses none
        await OpenProjectAsync("First");
        await _selection.SetActiveAsync("clean");
        var first = _projects.Current!.FilePath;
        await OpenProjectAsync("Second");
        var changes = 0;
        _selection.Changed += (_, _) => Interlocked.Increment(ref changes);

        // Act: look at the second, go back to the first, close
        var second = _selection.ActiveId;
        await _projects.OpenAsync(first, Ct);
        var back = _selection.Active?.Kit.Id;
        _projects.Close();
        var closed = (_selection.Active, _selection.ActiveId, _selection.IsMissing);

        // Assert: each answer is that of the project that was open, and the switches were announced
        Assert.Null(second);
        Assert.Equal("clean", back);
        Assert.Equal((null, null, false), closed);
        Assert.True(changes >= 2);
    }

    /// <summary>
    ///     Test that a kit of the project hides a kit of the program with the same id, so the active kit is the copy of the
    ///     project.
    /// </summary>
    [Fact]
    public async Task StyleSelection_Active_KitCopiedIntoProject_IsTheProjectCopy()
    {
        // Arrange: a project that uses a built-in kit and then keeps a copy of it
        await OpenProjectAsync("Frozen");
        await _selection.SetActiveAsync("clean");
        var builtIn = _catalog.Resolve("clean")!;

        // Act: copy the kit into the project
        _catalog.CopyIntoProject(builtIn);

        // Assert: the active kit is the one in the folder of the project
        Assert.Equal(StyleKitLocation.Project, _selection.Active!.Location);
        Assert.StartsWith(_projects.Current!.FolderPath, _selection.Active.FolderPath, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    ///     Test that a choice is written to the project file and read back after the file is opened again, that it is
    ///     announced even when the same kit is chosen again, that no style removes it, and that a choice that cannot be kept
    ///     is an error.
    /// </summary>
    [Fact]
    public async Task StyleSelection_SetActiveAsync_Choice_IsKeptInTheProjectFileAndAnnounced()
    {
        // Arrange: a project and a count of announcements
        await OpenProjectAsync("Kept");
        var file = _projects.Current!.FilePath;
        var announced = 0;
        _selection.Changed += (_, _) => Interlocked.Increment(ref announced);

        // Act: choose a kit, choose it again, and look at the file; reopen it with another service
        await _selection.SetActiveAsync("clean");
        var afterFirst = announced;
        await _selection.SetActiveAsync("clean");
        var afterSecond = announced;
        using var json = JsonDocument.Parse(await File.ReadAllTextAsync(file, Ct));
        var reopened = await new ProjectService().OpenAsync(file, Ct);
        await _selection.SetActiveAsync(null);
        var removed = JsonDocument.Parse(await File.ReadAllTextAsync(file, Ct));

        // Assert: the file says the kit, each call announced, the file read back, and no style removes the property
        Assert.Equal("clean", json.RootElement.GetProperty("activeStyleKit").GetString());
        Assert.True(afterFirst >= 1);
        Assert.True(afterSecond > afterFirst);
        Assert.Equal("clean", reopened.Project!.Document.ActiveStyleKit);
        Assert.False(removed.RootElement.TryGetProperty("activeStyleKit", out _));
        Assert.Null(_selection.ActiveId);
        removed.Dispose();
    }

    /// <summary>
    ///     Test that a choice with no project open, and a text that cannot be the id of a kit, are errors with a message
    ///     and change no file.
    /// </summary>
    [Fact]
    public async Task StyleSelection_SetActiveAsync_NoProjectOrBadId_ThrowsWithTheReason()
    {
        // Arrange: no project open
        var withoutProject = await Assert.ThrowsAsync<InvalidOperationException>(() => _selection.SetActiveAsync("clean"));
        await OpenProjectAsync("Strict");
        var file = _projects.Current!.FilePath;
        var before = await File.ReadAllTextAsync(file, Ct);

        // Act: a bad id
        var badId = await Assert.ThrowsAsync<InvalidOperationException>(() => _selection.SetActiveAsync("Not An Id"));

        // Assert: both messages say why, and the file is unchanged
        Assert.Contains("no open project", withoutProject.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("not a valid style kit id", badId.Message, StringComparison.Ordinal);
        Assert.Equal(before, await File.ReadAllTextAsync(file, Ct));
    }

    /// <summary>
    ///     Test that the default kit of the user is applied to a project that has no style, once, and that it is not applied
    ///     again, over another choice, or when there is nothing to apply.
    /// </summary>
    [Fact]
    public async Task StyleSelection_ApplyDefaultToNewProjectAsync_DefaultKit_IsAppliedOnceAndNotTwice()
    {
        // Arrange: a default kit and a new project
        await _catalog.RefreshAsync(Ct);
        SetDefault("clean");
        await _selection.ApplyDefaultToNewProjectAsync();
        var noProject = _selection.ActiveId;
        await OpenProjectAsync("New");

        // Act: apply, choose another kit, apply again
        await _selection.ApplyDefaultToNewProjectAsync();
        var applied = _selection.ActiveId;
        await _selection.SetActiveAsync("slides-dark");
        await _selection.ApplyDefaultToNewProjectAsync();
        var afterOtherChoice = _selection.ActiveId;

        // Assert: nothing without a project, the default for the new project, and the later choice kept
        Assert.Null(noProject);
        Assert.Equal("clean", applied);
        Assert.Equal("slides-dark", afterOtherChoice);
    }

    /// <summary>
    ///     Test that no default, a default that nothing has, and a project that names a kit that is missing are all left as
    ///     they are.
    /// </summary>
    [Fact]
    public async Task StyleSelection_ApplyDefaultToNewProjectAsync_NoDefaultUnknownDefaultOrMissingKit_ChangesNothing()
    {
        // Arrange: a new project and no default
        await OpenProjectAsync("Untouched");

        // Act: apply with no default, with a default that no kit has, and with a project that names a missing kit
        await _selection.ApplyDefaultToNewProjectAsync();
        var noDefault = _selection.ActiveId;
        SetDefault("nobody-has-this");
        await _selection.ApplyDefaultToNewProjectAsync();
        var unknownDefault = _selection.ActiveId;
        SetDefault("clean");
        await _selection.SetActiveAsync("lost-kit");
        await _selection.ApplyDefaultToNewProjectAsync();
        var missing = _selection.ActiveId;

        // Assert: nothing was applied in any case
        Assert.Null(noDefault);
        Assert.Null(unknownDefault);
        Assert.Equal("lost-kit", missing);
    }

    /// <summary>
    ///     Test that with no kit the sizes are the sizes of the application with their exact values, over white.
    /// </summary>
    [Fact]
    public async Task StyleSelection_GetPresets_NoKit_IsTheApplicationSizesOverWhite()
    {
        // Arrange: a project with no style
        await OpenProjectAsync("Plain");

        // Act
        var presets = _selection.GetPresets();

        // Assert: the three sizes with the values of the application
        Assert.Equal(
            [
                new ExportPreset("app:powerpoint-16-9", "PowerPoint 16:9", 1920, 1080, null, false, "#FFFFFF", ExportPresetOrigin.Application),
                new ExportPreset("app:email-800", "Email (800 wide)", 800, null, null, false, "#FFFFFF", ExportPresetOrigin.Application),
                new ExportPreset("app:a4-portrait-150", "A4 portrait @150 DPI", 1240, 1754, 150, false, "#FFFFFF", ExportPresetOrigin.Application)
            ],
            presets);
        Assert.Equal(presets, StyleSelection.AppPresets);
    }

    /// <summary>
    ///     Test that the sizes of the kit come first, that a size of the kit hides a size of the application with the same
    ///     name ignoring case, that the sizes that are not transparent have the background of the kit, and that a
    ///     transparent size has none.
    /// </summary>
    [Fact]
    public async Task StyleSelection_GetPresets_KitWithSizes_KitFirstThenApplicationNotRenamedOverTheKitBackground()
    {
        // Arrange: a kit of the user with a dark background and three sizes, one of which renames a size of the application
        var kit = StyleKitPlaces.Kit("brand", "A brand", "Brand") with
        {
            Palette = new Dictionary<string, string> { ["background"] = "#102030", ["text"] = "#FFFFFF", ["primary"] = "#2563EB" },
            SizePresets =
            [
                new StyleSizePreset("Banner", 1600, 640, null, false),
                new StyleSizePreset("powerpoint 16:9", 1280, 720, 96, false),
                new StyleSizePreset("Logo", 512, null, null, true)
            ]
        };
        _catalog.Save(kit, StyleKitLocation.User);
        await OpenProjectAsync("Branded");
        await _selection.SetActiveAsync("brand");

        // Act
        var presets = _selection.GetPresets();

        // Assert: kit sizes first with their values and origin, then the two sizes of the application that were not renamed
        Assert.Equal(
            ["Banner", "powerpoint 16:9", "Logo", "Email (800 wide)", "A4 portrait @150 DPI"],
            presets.Select(p => p.Name));
        Assert.Equal(
            [ExportPresetOrigin.StyleKit, ExportPresetOrigin.StyleKit, ExportPresetOrigin.StyleKit, ExportPresetOrigin.Application, ExportPresetOrigin.Application],
            presets.Select(p => p.Origin));
        Assert.Equal(new ExportPreset("kit:brand:banner", "Banner", 1600, 640, null, false, "#102030", ExportPresetOrigin.StyleKit), presets[0]);
        Assert.Equal(new ExportPreset("kit:brand:powerpoint 16:9", "powerpoint 16:9", 1280, 720, 96, false, "#102030", ExportPresetOrigin.StyleKit), presets[1]);
        Assert.Equal(new ExportPreset("kit:brand:logo", "Logo", 512, null, null, true, null, ExportPresetOrigin.StyleKit), presets[2]);
        Assert.Equal("#102030", presets[3].BackgroundHex);
        Assert.Equal("#102030", presets[4].BackgroundHex);
        Assert.Equal(new Dictionary<string, int?> { ["Email (800 wide)"] = 800, ["A4 portrait @150 DPI"] = 1240 }, presets.Skip(3).ToDictionary(p => p.Name, p => p.Width));
    }

    /// <summary>
    ///     Test that a project that names a kit that is missing offers the sizes of the application over white.
    /// </summary>
    [Fact]
    public async Task StyleSelection_GetPresets_MissingKit_IsTheApplicationSizesOverWhite()
    {
        // Arrange: a project that names a kit that nobody has
        await OpenProjectAsync("Lost");
        await _selection.SetActiveAsync("lost-kit");

        // Act
        var presets = _selection.GetPresets();

        // Assert: the application sizes, all over white
        Assert.Equal(3, presets.Count);
        Assert.All(presets, p => Assert.Equal("#FFFFFF", p.BackgroundHex));
    }

    /// <summary>
    ///     Test that a project change and a change of the kits are announced, and that nothing is announced after disposal.
    /// </summary>
    [Fact]
    public async Task StyleSelection_Changed_ProjectOrCatalogChange_IsRaisedUntilDisposed()
    {
        // Arrange: a handler that counts
        var count = 0;
        void Handler(object? sender, EventArgs e) => Interlocked.Increment(ref count);
        _selection.Changed += Handler;

        // Act: open a project, save a kit, then dispose and open another project
        await OpenProjectAsync("Watched");
        var afterProject = count;
        _catalog.Save(StyleKitPlaces.Kit("another"), StyleKitLocation.User);
        var afterCatalog = count;
        _selection.Dispose();
        await OpenProjectAsync("Unwatched");

        // Assert: each source announced, and nothing after disposal
        Assert.True(afterProject >= 1);
        Assert.True(afterCatalog > afterProject);
        Assert.Equal(afterCatalog, count);
    }

    /// <summary>
    ///     Test that missing collaborators are refused.
    /// </summary>
    [Fact]
    public void StyleSelection_Constructor_NullArguments_Throw()
    {
        // Act / Assert: each missing collaborator is refused
        Assert.Throws<ArgumentNullException>(() => new StyleSelection(null!, _catalog, _settings));
        Assert.Throws<ArgumentNullException>(() => new StyleSelection(_projects, null!, _settings));
        Assert.Throws<ArgumentNullException>(() => new StyleSelection(_projects, _catalog, null!));
    }

    /// <summary>
    ///     Creates a project in the temporary folder, makes it the open project, and lets the catalog read its kits.
    /// </summary>
    /// <param name="name">The name of the project.</param>
    /// <returns>A task that completes when the catalog has been refreshed.</returns>
    private async Task OpenProjectAsync(string name)
    {
        var parent = Directory.CreateDirectory(Path.Combine(_root, "projects")).FullName;
        var created = await _projects.CreateAsync(parent, name, Ct);
        Assert.True(created.Succeeded, created.Error);
        await _catalog.RefreshAsync(Ct);
    }

    /// <summary>
    ///     Sets the default kit of the user in the settings.
    /// </summary>
    /// <param name="id">The id of the kit.</param>
    private void SetDefault(string id)
    {
        _settings.Update(s => s with { Styles = s.Styles with { DefaultKitId = id } });
    }
}
