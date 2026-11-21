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
using DemaConsulting.CanvasPilot.PreviewSubsystem;
using DemaConsulting.CanvasPilot.StyleSubsystem;
using NSubstitute;

namespace DemaConsulting.CanvasPilot.Tests;

/// <summary>
///     Unit tests for <see cref="StyleKitManagerViewModel" />, its items and groups, and <see cref="StyleThumbnailCache" />
///     over a real catalog of kits and the real archive on temporary folders, with hand-written windows and pickers and a
///     render service that completes at once: the groups and notes, the preview of a kit, the pictures of the examples,
///     every command with every refusal, and the import and export of kits.
/// </summary>
public sealed class StyleKitManagerTests : IDisposable
{
    /// <summary>
    ///     The places that hold kits.
    /// </summary>
    private readonly StyleKitPlaces _places = new();

    /// <summary>
    ///     The real catalog over the places.
    /// </summary>
    private readonly StyleKitCatalog _catalog;

    /// <summary>
    ///     The kit that the open project uses.
    /// </summary>
    private readonly FakeActiveStyle _active = new();

    /// <summary>
    ///     Draws the pictures of the examples.
    /// </summary>
    private readonly RecordingRenderService _renderer = new();

    /// <summary>
    ///     The windows and pickers.
    /// </summary>
    private readonly FakeStyleKitDialogs _dialogs = new();

    /// <summary>
    ///     The view models that a test made, disposed after the test.
    /// </summary>
    private readonly List<StyleKitManagerViewModel> _managers = [];

    /// <summary>
    ///     Initializes a new instance of the <see cref="StyleKitManagerTests" /> class.
    /// </summary>
    public StyleKitManagerTests()
    {
        _catalog = _places.CreateCatalog();
        _places.Catalog = _catalog;
    }

    /// <summary>
    ///     Gets the token that cancels the running test.
    /// </summary>
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    /// <inheritdoc />
    public void Dispose()
    {
        foreach (var manager in _managers)
        {
            manager.Dispose();
        }

        _places.Dispose();
    }

    /// <summary>
    ///     Test that the groups follow the places: the kits of each place in its group, the group of the project only while a
    ///     project is open, an empty group says so, and a place that cannot be read and a kit that cannot be used are noted.
    /// </summary>
    [Fact]
    public async Task StyleKitManager_Groups_PlacesKitsAndNotes_AreListedByPlace()
    {
        // Arrange: a built-in kit, two of the user, one of a shared folder, a shared folder that is not there, and a broken kit
        StyleKitPlaces.WriteKit(_places.BuiltIn, StyleKitPlaces.Kit("clean", "built-in", "Clean"));
        StyleKitPlaces.WriteKit(_places.User, StyleKitPlaces.Kit("mine", "user", "Mine"));
        StyleKitPlaces.WriteKit(_places.User, StyleKitPlaces.Kit("zeta", "user", "Zeta"));
        StyleKitPlaces.WriteKit(_places.Shared1, StyleKitPlaces.Kit("team", "shared", "Team"));
        Directory.CreateDirectory(Path.Combine(_places.Shared1, "broken"));
        await File.WriteAllTextAsync(Path.Combine(_places.Shared1, "broken", "style.json"), "{ not json", Ct);
        _places.SetSharedFolders(_places.Shared1, _places.Shared2);
        var manager = await CreateAsync();

        // Act: read the groups without a project, then with one
        var without = manager.Groups.Select(group => group.Title).ToArray();
        await OpenProjectAsync();
        var with = manager.Groups.Select(group => group.Title).ToArray();
        var shared = manager.Groups.Single(group => group.Location == StyleKitLocation.Shared);

        // Assert
        Assert.Equal(["My kits", "Shared", "Built-in"], without);
        Assert.Equal(["This project", "My kits", "Shared", "Built-in"], with);
        Assert.Equal(["Mine", "Zeta"], manager.Groups.Single(group => group.Location == StyleKitLocation.User).Items.Select(item => item.Name));
        Assert.Equal(["Team"], shared.Items.Select(item => item.Name));
        Assert.Contains(shared.Notes, note => note.StartsWith("Not reachable: " + _places.Shared2, StringComparison.Ordinal));
        Assert.Contains(shared.Notes, note => note.StartsWith("Cannot be used: broken", StringComparison.Ordinal));
        Assert.True(shared.HasNotes);
        Assert.True(manager.Groups[0].IsEmpty);
        Assert.Contains("Copy a kit into the project", manager.Groups[0].EmptyText, StringComparison.Ordinal);
        Assert.True(manager.HasProject);
        Assert.Equal("Style Kits", StyleKitManagerViewModel.WindowTitle);
        Assert.Equal(["My kits", "This project"], StyleKitManagerViewModel.TargetChoices);
    }

    /// <summary>
    ///     Test that a group with no kit says what to do, also when no shared folder is set.
    /// </summary>
    [Fact]
    public async Task StyleKitManager_Groups_EmptyPlaces_SayWhatToDo()
    {
        // Arrange and act: no kits at all
        var manager = await CreateAsync();

        // Assert
        var texts = manager.Groups.ToDictionary(group => group.Location, group => group.EmptyText);
        Assert.Contains("Use New or Duplicate", texts[StyleKitLocation.User], StringComparison.Ordinal);
        Assert.Contains("No shared folders are set", texts[StyleKitLocation.Shared], StringComparison.Ordinal);
        Assert.Contains("No kits come with the program", texts[StyleKitLocation.BuiltIn], StringComparison.Ordinal);
        Assert.All(manager.Groups, group => Assert.True(group.IsEmpty));
        Assert.Null(manager.SelectedItem);
        Assert.False(manager.HasSelection);
    }

    /// <summary>
    ///     Test that a kit that a more local kit hides is dimmed, says what hides it, and cannot be used in the project.
    /// </summary>
    [Fact]
    public async Task StyleKitManager_Groups_OverriddenKit_IsDimmedAndCannotBeUsed()
    {
        // Arrange: the same id in the kits of the user and the kits of the program, and a project
        StyleKitPlaces.WriteKit(_places.BuiltIn, StyleKitPlaces.Kit("clean", "built-in", "Clean"));
        StyleKitPlaces.WriteKit(_places.User, StyleKitPlaces.Kit("clean", "user", "Clean mine"));
        var manager = await CreateAsync();
        await OpenProjectAsync();
        var hidden = Item(manager, StyleKitLocation.BuiltIn, "clean");
        var winner = Item(manager, StyleKitLocation.User, "clean");

        // Act: choose each
        manager.SelectItemCommand.Execute(hidden);
        var hiddenCanUse = manager.UseInProjectCommand.CanExecute(null);
        manager.SelectItemCommand.Execute(winner);
        var winnerCanUse = manager.UseInProjectCommand.CanExecute(null);

        // Assert
        Assert.True(hidden.IsOverridden);
        Assert.Equal(0.5, hidden.CardOpacity);
        Assert.Equal("Hidden by the kit with the same id in My kits.", hidden.OverriddenText);
        Assert.False(winner.IsOverridden);
        Assert.Equal(1, winner.CardOpacity);
        Assert.Equal(string.Empty, winner.OverriddenText);
        Assert.False(hiddenCanUse);
        Assert.True(winnerCanUse);
    }

    /// <summary>
    ///     Test that choosing a kit shows everything about it: the colors with their roles, the fonts, the numbers, the
    ///     Mermaid and Vega-Lite parts, the advice, the sizes, the warnings, the text for an assistant and its length, and the
    ///     place.
    /// </summary>
    [Fact]
    public async Task StyleKitManager_Select_Kit_ShowsPreviewData()
    {
        // Arrange: a kit that has every part and a font that is not bundled
        var kit = FullKit("team") with { Fonts = new StyleFonts("Inter, sans-serif", "sans-serif", "monospace") };
        StyleKitPlaces.WriteKit(_places.User, kit, "a.svg");
        var manager = await CreateAsync();
        var item = Item(manager, StyleKitLocation.User, "team");
        var other = StyleKitPlaces.Kit("other");
        StyleKitPlaces.WriteKit(_places.User, other);
        await _catalog.RefreshAsync(Ct);

        // Act: choose the kit
        manager.SelectItemCommand.Execute(Item(manager, StyleKitLocation.User, "team"));
        var chosen = manager.SelectedItem!;

        // Assert: the preview has every part
        Assert.True(manager.HasSelection);
        Assert.True(chosen.IsSelected);
        Assert.False(Item(manager, StyleKitLocation.User, "other").IsSelected);
        Assert.Equal("Team", chosen.Name);
        Assert.Equal("The look of the team.", chosen.Description);
        Assert.Equal("my kits", chosen.PlaceText);
        Assert.StartsWith("From my kits: ", chosen.LocationText, StringComparison.Ordinal);
        Assert.Equal(["background", "text", "primary", "accent"], chosen.Swatches.Select(swatch => swatch.Role));
        Assert.Equal(["#FFFFFF", "#0F172A", "#2563EB", "#F59E0B"], chosen.Swatches.Select(swatch => swatch.Hex));
        Assert.Equal(["#000000", "#FFFFFF", "#FFFFFF", "#000000"], chosen.Swatches.Select(swatch => swatch.LabelHex));
        Assert.Equal(["#FFFFFF", "#0F172A", "#2563EB", "#F59E0B"], chosen.StripColors);
        Assert.Equal(["Headings: Inter, sans-serif", "Body: sans-serif", "Monospace: monospace"], chosen.FontLines);
        Assert.Equal("Line width 2 px, corner radius 12 px, spacing 16 px", chosen.NumbersText);
        Assert.Equal("Mermaid theme base with 1 variable.", chosen.MermaidText);
        Assert.StartsWith("Vega-Lite configuration of ", chosen.VegaLiteText, StringComparison.Ordinal);
        Assert.True(chosen.HasNotes);
        Assert.Equal("Keep it plain.", chosen.Notes);
        Assert.Equal(["Use the palette"], chosen.DoItems);
        Assert.Equal(["Use gradients"], chosen.DontItems);
        Assert.Equal(["Slide: 1920 x 1080 px", "Banner: 1600 px wide, 150 dpi, transparent"], chosen.PresetLines);
        Assert.True(chosen.HasPresets);
        Assert.True(chosen.HasWarnings);
        Assert.Contains("fonts.heading: 'Inter'", chosen.Warnings.Single(), StringComparison.Ordinal);
        Assert.Contains("Team", chosen.GuidanceText, StringComparison.Ordinal);
        Assert.Equal($"{chosen.GuidanceText.Length:N0} of 4,000 characters", chosen.GuidanceSizeText);
        Assert.True(chosen.GuidanceText.Length <= StyleGuidance.BudgetCharacters);
        Assert.True(chosen.HasExamples);
        Assert.False(chosen.IsReadOnly);
        Assert.Equal(string.Empty, chosen.ReadOnlyText);
        Assert.Equal(item.Entry.FolderPath, chosen.Entry.FolderPath);
    }

    /// <summary>
    ///     Test that the pictures of the examples are drawn 240 pixels wide from the folder of the kit, once for each file,
    ///     not again when the kit is chosen again, and again when the file has changed.
    /// </summary>
    [Fact]
    public async Task StyleKitManager_Thumbnails_ChosenKit_AreDrawnOnceAndAgainWhenTheFileChanges()
    {
        // Arrange: a kit with two examples that have captions
        StyleKitPlaces.WriteKit(_places.User, FullKit("team") with { Examples = [new StyleExample("examples/a.svg", string.Empty, "First"), new StyleExample("examples/b.svg", string.Empty, "Second")] }, "a.svg", "b.svg");
        var manager = await CreateAsync();
        var item = Item(manager, StyleKitLocation.User, "team");

        // Act: choose the kit and wait for the pictures
        manager.SelectItemCommand.Execute(item);
        await manager.ThumbnailTask;
        var firstRequests = _renderer.Requests.ToArray();

        // Act: choose nothing, then the kit again; the pictures come from the cache
        manager.SelectItemCommand.Execute(null);
        manager.SelectItemCommand.Execute(item);
        await manager.ThumbnailTask;
        var afterReselect = _renderer.Requests.Count;

        // Act: change one file and choose the kit again
        manager.SelectItemCommand.Execute(null);
        await File.WriteAllTextAsync(Path.Combine(item.Entry.FolderPath, "examples", "a.svg"), "changed text that is longer", Ct);
        manager.SelectItemCommand.Execute(item);
        await manager.ThumbnailTask;

        // Assert
        Assert.Equal(2, firstRequests.Length);
        Assert.All(firstRequests, request => Assert.Equal(item.Entry.FolderPath, request.Folder));
        Assert.All(firstRequests, request => Assert.Equal(new RenderSettings(Width: 240), request.Settings));
        Assert.Equal(["a.svg", "b.svg"], firstRequests.Select(request => Path.GetFileName(request.File)).Order());
        Assert.Equal(2, afterReselect);
        Assert.Equal(3, _renderer.Requests.Count);
        Assert.Equal(["a.svg: First", "b.svg: Second"], item.Thumbnails.Select(thumbnail => thumbnail.Label));
        Assert.All(item.Thumbnails, thumbnail => Assert.True(thumbnail.HasImage));
        Assert.All(item.Thumbnails, thumbnail => Assert.False(thumbnail.HasPlaceholder));
    }

    /// <summary>
    ///     Test that a picture that cannot be drawn shows the reason where the picture would be, and the others are drawn.
    /// </summary>
    [Fact]
    public async Task StyleKitManager_Thumbnails_FailingRender_ShowsAPlaceholderWithTheReason()
    {
        // Arrange: a kit with two examples of which one cannot be drawn
        StyleKitPlaces.WriteKit(_places.User, FullKit("team"), "a.svg", "b.svg");
        _renderer.FailingFiles.Add("b.svg");
        var manager = await CreateAsync();
        var item = Item(manager, StyleKitLocation.User, "team");

        // Act
        manager.SelectItemCommand.Execute(item);
        await manager.ThumbnailTask;

        // Assert
        Assert.True(item.Thumbnails[0].HasImage);
        Assert.False(item.Thumbnails[1].HasImage);
        Assert.True(item.Thumbnails[1].HasPlaceholder);
        Assert.Equal("Could not be drawn: b.svg has a syntax error.", item.Thumbnails[1].PlaceholderText);
        Assert.Equal("b.svg", item.Thumbnails[1].Label);
    }

    /// <summary>
    ///     Test that choosing another kit while pictures are drawn stops showing the first kit's pictures, and that a kit with
    ///     no example has no pictures.
    /// </summary>
    [Fact]
    public async Task StyleKitManager_Thumbnails_AnotherKitChosenWhileDrawing_DoesNotShowTheOldPictures()
    {
        // Arrange: a kit with an example and a kit with none, and a render that waits
        StyleKitPlaces.WriteKit(_places.User, FullKit("team"), "a.svg");
        StyleKitPlaces.WriteKit(_places.User, StyleKitPlaces.Kit("plain"));
        var gate = new TaskCompletionSource();
        _renderer.Gate = gate.Task;
        var manager = await CreateAsync();
        var team = Item(manager, StyleKitLocation.User, "team");
        var plain = Item(manager, StyleKitLocation.User, "plain");

        // Act: choose the first kit, choose the second before the picture is drawn, and let the drawing finish
        manager.SelectItemCommand.Execute(team);
        var firstDrawing = manager.ThumbnailTask;
        manager.SelectItemCommand.Execute(plain);
        gate.SetResult();
        await Task.WhenAny(firstDrawing, Task.Delay(2000, Ct));

        // Assert: the picture of the first kit was not shown, and the second kit has none
        Assert.False(team.Thumbnails[0].HasImage);
        Assert.Empty(plain.Thumbnails);
        Assert.False(plain.HasExamples);
        await manager.ThumbnailTask;
    }

    /// <summary>
    ///     Test that the commands that change a kit are not available for a kit of the program or of a shared folder, and
    ///     that the commands that read it are.
    /// </summary>
    [Fact]
    public async Task StyleKitManager_Commands_BuiltInSharedAndOwnKits_EnableOnlyWhatApplies()
    {
        // Arrange: a built-in kit, a shared kit, and a kit of the user, and a project
        StyleKitPlaces.WriteKit(_places.BuiltIn, StyleKitPlaces.Kit("clean"));
        StyleKitPlaces.WriteKit(_places.Shared1, StyleKitPlaces.Kit("team"));
        StyleKitPlaces.WriteKit(_places.User, StyleKitPlaces.Kit("mine"));
        _places.SetSharedFolders(_places.Shared1);
        var manager = await CreateAsync();
        await OpenProjectAsync();

        // Act and assert: with nothing chosen
        Assert.False(manager.EditCommand.CanExecute(null));
        Assert.False(manager.DeleteCommand.CanExecute(null));
        Assert.False(manager.DuplicateCommand.CanExecute(null));
        Assert.False(manager.ExportCommand.CanExecute(null));
        Assert.False(manager.UseInProjectCommand.CanExecute(null));
        Assert.False(manager.CopyIntoProjectCommand.CanExecute(null));
        Assert.True(manager.NewCommand.CanExecute(null));
        Assert.True(manager.ImportCommand.CanExecute(null));
        Assert.True(manager.RefreshCommand.CanExecute(null));

        // Built-in and shared kits: read only
        foreach (var location in new[] { StyleKitLocation.BuiltIn, StyleKitLocation.Shared })
        {
            manager.SelectItemCommand.Execute(manager.Groups.Single(group => group.Location == location).Items.Single());
            Assert.True(manager.IsSelectionReadOnly);
            Assert.False(manager.EditCommand.CanExecute(null));
            Assert.False(manager.DeleteCommand.CanExecute(null));
            Assert.True(manager.DuplicateCommand.CanExecute(null));
            Assert.True(manager.ExportCommand.CanExecute(null));
            Assert.True(manager.UseInProjectCommand.CanExecute(null));
            Assert.True(manager.CopyIntoProjectCommand.CanExecute(null));
            Assert.NotEqual(string.Empty, manager.SelectedItem!.ReadOnlyText);
        }

        // A kit of the user: everything
        manager.SelectItemCommand.Execute(Item(manager, StyleKitLocation.User, "mine"));
        Assert.False(manager.IsSelectionReadOnly);
        Assert.True(manager.EditCommand.CanExecute(null));
        Assert.True(manager.DeleteCommand.CanExecute(null));
    }

    /// <summary>
    ///     Test that a kit can be used in the project only while a project is open, that the id is written, that the kit
    ///     that the project uses is marked, and that a refusal is said.
    /// </summary>
    [Fact]
    public async Task StyleKitManager_UseInProject_ChosenKit_IsMadeTheActiveStyleOrTheReasonIsSaid()
    {
        // Arrange: a kit and no project
        StyleKitPlaces.WriteKit(_places.User, StyleKitPlaces.Kit("mine", string.Empty, "Mine"));
        var manager = await CreateAsync();
        var item = Item(manager, StyleKitLocation.User, "mine");
        manager.SelectItemCommand.Execute(item);
        var closedProject = manager.UseInProjectCommand.CanExecute(null);

        // Act: open a project, use the kit, and let the active style announce it
        await OpenProjectAsync();
        var canUse = manager.UseInProjectCommand.CanExecute(null);
        await manager.UseInProjectCommand.ExecuteAsync(null);
        _active.Use(manager.SelectedItem!.Entry);
        var inUse = manager.SelectedItem.IsInUse;

        // Act: a refusal of the active style is said
        var refusing = Substitute.For<IActiveStyle>();
        refusing.SetActiveAsync(Arg.Any<string?>()).Returns(Task.FromException(new InvalidOperationException("The project file cannot be written.")));
        var second = new StyleKitManagerViewModel(_catalog, refusing, _renderer, _dialogs, _places.Projects);
        _managers.Add(second);
        second.SelectItemCommand.Execute(Item(second, StyleKitLocation.User, "mine"));
        await second.UseInProjectCommand.ExecuteAsync(null);

        // Assert
        Assert.False(closedProject);
        Assert.True(canUse);
        Assert.Equal("mine", _active.ActiveId);
        Assert.True(inUse);
        Assert.Equal("The project file cannot be written.", second.StatusMessage);
        Assert.True(second.HasStatusMessage);
        Assert.Equal("'Mine' is now the style of this project.", manager.StatusMessage);
    }

    /// <summary>
    ///     Test that New asks for a name, makes the kit in the place that is chosen, chooses it, and opens it in the editor,
    ///     and that a cancel and a place that cannot be written change nothing.
    /// </summary>
    [Fact]
    public async Task StyleKitManager_New_NamePromptAndTargets_MakesTheKitAndOpensTheEditor()
    {
        // Arrange: no kits and no project
        var manager = await CreateAsync();

        // Act: cancel the prompt
        _dialogs.PromptResult = null;
        await manager.NewCommand.ExecuteAsync(null);
        var afterCancel = Directory.Exists(_places.User);

        // Act: a name, saved in the kits of the user
        _dialogs.PromptResult = "Poster";
        await manager.NewCommand.ExecuteAsync(null);

        // Act: the project as the place, with no project open
        manager.TargetIndex = 1;
        _dialogs.PromptResult = "Banner";
        await manager.NewCommand.ExecuteAsync(null);
        var refusal = manager.StatusMessage;

        // Act: the project as the place, with a project open
        await OpenProjectAsync();
        await manager.NewCommand.ExecuteAsync(null);

        // Assert
        Assert.False(afterCancel);
        Assert.True(File.Exists(Path.Combine(_places.User, "poster", "style.json")));
        Assert.Equal("Poster", StyleKitSerializer.ReadFolder(Path.Combine(_places.User, "poster")).Kit!.Name);
        Assert.Contains("My kits", _dialogs.Prompts[0].Message, StringComparison.Ordinal);
        Assert.Equal("New kit", _dialogs.Prompts[0].InitialText);
        Assert.Equal(["poster", "banner"], _dialogs.Editors.Select(editor => editor.Id));
        Assert.Contains("project", refusal, StringComparison.OrdinalIgnoreCase);
        Assert.True(File.Exists(Path.Combine(_places.ProjectStyles, "banner", "style.json")));
        Assert.Equal(StyleKitLocation.Project, manager.SelectedItem!.Entry.Location);
        Assert.Equal("Banner", manager.SelectedItem.Name);
        Assert.Equal(StyleKitLocation.Project, manager.TargetLocation);
    }

    /// <summary>
    ///     Test that Duplicate makes a copy with the examples under the name that was typed, chooses it, and opens it in the
    ///     editor, also for a kit that is read only.
    /// </summary>
    [Fact]
    public async Task StyleKitManager_Duplicate_BuiltInKit_MakesAnEditableCopyWithItsExamples()
    {
        // Arrange: a built-in kit with an example
        StyleKitPlaces.WriteKit(_places.BuiltIn, StyleKitPlaces.Kit("clean", "built-in", "Clean"), "a.svg");
        var manager = await CreateAsync();
        manager.SelectItemCommand.Execute(Item(manager, StyleKitLocation.BuiltIn, "clean"));

        // Act
        _dialogs.PromptResult = "Clean twice";
        await manager.DuplicateCommand.ExecuteAsync(null);

        // Assert: the copy is in the kits of the user with the example, is chosen, and was opened in the editor
        Assert.Equal("Clean copy", _dialogs.Prompts.Single().InitialText);
        Assert.True(File.Exists(Path.Combine(_places.User, "clean-twice", "examples", "a.svg")));
        Assert.Equal("clean-twice", manager.SelectedItem!.Id);
        Assert.False(manager.SelectedItem.IsReadOnly);
        Assert.Equal("clean-twice", _dialogs.Editors.Single().Id);
        Assert.Contains("Made the kit 'Clean twice' as a copy of 'Clean'", manager.StatusMessage, StringComparison.Ordinal);

        // Act: cancel the prompt
        _dialogs.PromptResult = null;
        await manager.DuplicateCommand.ExecuteAsync(null);
        Assert.Single(_dialogs.Editors);
    }

    /// <summary>
    ///     Test that Edit opens the editor on the chosen kit and, when the editor saved, shows the new values and keeps the
    ///     kit chosen.
    /// </summary>
    [Fact]
    public async Task StyleKitManager_Edit_UserKit_OpensTheEditorAndShowsTheSavedKit()
    {
        // Arrange: a kit of the user
        StyleKitPlaces.WriteKit(_places.User, StyleKitPlaces.Kit("mine", string.Empty, "Mine"));
        var manager = await CreateAsync();
        manager.SelectItemCommand.Execute(Item(manager, StyleKitLocation.User, "mine"));

        // Act: an editor that is canceled, and then one that renames the kit and saves
        await manager.EditCommand.ExecuteAsync(null);
        var afterCancel = manager.SelectedItem!.Name;
        _dialogs.OnEditor = editor =>
        {
            editor.Name = "Renamed";
            editor.SaveCommand.Execute(null);
        };
        await manager.EditCommand.ExecuteAsync(null);

        // Assert
        Assert.Equal("Mine", afterCancel);
        Assert.Equal(2, _dialogs.Editors.Count);
        Assert.NotNull(_dialogs.Editors[0].PickExampleFiles);
        Assert.Equal("Renamed", manager.SelectedItem.Name);
        Assert.Equal("Saved the kit 'Renamed'.", manager.StatusMessage);
        Assert.Equal("Renamed", StyleKitSerializer.ReadFolder(Path.Combine(_places.User, "mine")).Kit!.Name);
    }

    /// <summary>
    ///     Test that the picker of the editor is the one of the window, so that the editor can ask for example files.
    /// </summary>
    [Fact]
    public async Task StyleKitManager_Edit_ExamplePicker_IsTheWindowPicker()
    {
        // Arrange: a kit of the user and a file to choose
        StyleKitPlaces.WriteKit(_places.User, StyleKitPlaces.Kit("mine"));
        var chosen = Path.Combine(_places.Root, "pick.svg");
        await File.WriteAllTextAsync(chosen, "x", Ct);
        _dialogs.ExampleFiles = [chosen];
        var manager = await CreateAsync();
        manager.SelectItemCommand.Execute(Item(manager, StyleKitLocation.User, "mine"));
        _dialogs.OnEditor = editor => editor.AddExamplesCommand.Execute(null);

        // Act
        await manager.EditCommand.ExecuteAsync(null);

        // Assert
        Assert.Equal(["pick.svg"], _dialogs.Editors.Single().Examples.Select(row => row.FileName));
    }

    /// <summary>
    ///     Test that Delete asks, deletes the folder when confirmed, leaves the kit when not, and says what went wrong.
    /// </summary>
    [Fact]
    public async Task StyleKitManager_Delete_UserKit_AsksAndRemovesTheFolder()
    {
        // Arrange: two kits of the user
        StyleKitPlaces.WriteKit(_places.User, StyleKitPlaces.Kit("mine", string.Empty, "Mine"));
        StyleKitPlaces.WriteKit(_places.User, StyleKitPlaces.Kit("other", string.Empty, "Other"));
        var manager = await CreateAsync();
        manager.SelectItemCommand.Execute(Item(manager, StyleKitLocation.User, "mine"));

        // Act: say no, then yes
        _dialogs.ConfirmDefault = false;
        await manager.DeleteCommand.ExecuteAsync(null);
        var afterNo = Directory.Exists(Path.Combine(_places.User, "mine"));
        _dialogs.ConfirmDefault = true;
        await manager.DeleteCommand.ExecuteAsync(null);

        // Assert
        Assert.True(afterNo);
        Assert.False(Directory.Exists(Path.Combine(_places.User, "mine")));
        Assert.True(Directory.Exists(Path.Combine(_places.User, "other")));
        Assert.Equal(["Other"], manager.Groups.Single(group => group.Location == StyleKitLocation.User).Items.Select(item => item.Name));
        Assert.Null(manager.SelectedItem);
        Assert.Equal("Delete", _dialogs.Confirmations[0].ConfirmText);
        Assert.Contains("'Mine'", _dialogs.Confirmations[0].Message, StringComparison.Ordinal);
        Assert.Equal("Deleted the kit 'Mine'.", manager.StatusMessage);
    }

    /// <summary>
    ///     Test that Export writes the zip file of the kit under the suggested name, that a cancel writes nothing, and that
    ///     a file that cannot be written is said.
    /// </summary>
    [Fact]
    public async Task StyleKitManager_Export_ChosenKit_WritesAZipFileOrSaysWhy()
    {
        // Arrange: a kit with an example
        StyleKitPlaces.WriteKit(_places.User, StyleKitPlaces.Kit("team", string.Empty, "Team"), "a.svg");
        var manager = await CreateAsync();
        manager.SelectItemCommand.Execute(Item(manager, StyleKitLocation.User, "team"));
        var zip = Path.Combine(_places.Root, "out", "team.zip");
        Directory.CreateDirectory(Path.GetDirectoryName(zip)!);

        // Act: cancel, then a file, then a folder that is not there
        _dialogs.ZipToExport = null;
        await manager.ExportCommand.ExecuteAsync(null);
        var afterCancel = File.Exists(zip);
        _dialogs.ZipToExport = zip;
        await manager.ExportCommand.ExecuteAsync(null);
        var written = manager.StatusMessage;
        _dialogs.ZipToExport = Path.Combine(_places.Root, "nowhere", "team.zip");
        await manager.ExportCommand.ExecuteAsync(null);

        // Assert
        Assert.False(afterCancel);
        Assert.Equal(["team.zip", "team.zip", "team.zip"], _dialogs.SuggestedZipNames);
        await using (var archive = await ZipFile.OpenReadAsync(zip, Ct))
        {
            Assert.Equal(["examples/a.svg", "style.json"], archive.Entries.Select(entry => entry.FullName).Order());
        }

        Assert.StartsWith("Exported the kit 'Team' to ", written, StringComparison.Ordinal);
        Assert.Contains("nowhere", manager.StatusMessage, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Test that Import reads a kit from a zip file into the chosen place, chooses it, and asks what to do with a kit
    ///     that is there already: replace it, keep both, or nothing.
    /// </summary>
    [Fact]
    public async Task StyleKitManager_Import_ZipFile_ImportsAndAsksAboutAnExistingKit()
    {
        // Arrange: a kit of the user that is exported and then deleted, so the zip file can bring it back
        StyleKitPlaces.WriteKit(_places.User, StyleKitPlaces.Kit("team", "first", "Team"), "a.svg");
        var manager = await CreateAsync();
        var zip = Path.Combine(_places.Root, "team.zip");
        StyleKitArchive.Export(Item(manager, StyleKitLocation.User, "team").Entry, zip);
        manager.SelectItemCommand.Execute(Item(manager, StyleKitLocation.User, "team"));
        _dialogs.ConfirmDefault = true;
        await manager.DeleteCommand.ExecuteAsync(null);
        _dialogs.Confirmations.Clear();
        _dialogs.ZipToImport = zip;

        // Act: cancel the picker, then import into the empty place
        _dialogs.ZipToImport = null;
        await manager.ImportCommand.ExecuteAsync(null);
        var afterCancel = Directory.Exists(Path.Combine(_places.User, "team"));
        _dialogs.ZipToImport = zip;
        await manager.ImportCommand.ExecuteAsync(null);
        var imported = manager.SelectedItem?.Id;
        var importedMessage = manager.StatusMessage;
        var asked = _dialogs.Confirmations.Count;

        // Act: import again and replace, keep both, and then do neither
        _dialogs.ConfirmAnswers["Replace"] = true;
        await manager.ImportCommand.ExecuteAsync(null);
        var afterReplace = _dialogs.Confirmations.Count;
        _dialogs.ConfirmAnswers["Replace"] = false;
        _dialogs.ConfirmAnswers["Keep both"] = true;
        await manager.ImportCommand.ExecuteAsync(null);
        var keptBoth = Directory.Exists(Path.Combine(_places.User, "team-2"));
        _dialogs.ConfirmAnswers["Keep both"] = false;
        await manager.ImportCommand.ExecuteAsync(null);

        // Assert
        Assert.False(afterCancel);
        Assert.Equal("team", imported);
        Assert.Equal("Imported the kit 'Team'.", importedMessage);
        Assert.Equal(0, asked);
        Assert.Equal(1, afterReplace);
        Assert.True(keptBoth);
        Assert.Equal("Nothing was imported.", manager.StatusMessage);
        Assert.True(File.Exists(Path.Combine(_places.User, "team", "examples", "a.svg")));
        Assert.Equal(["Team", "Team (2)"], manager.Groups.Single(group => group.Location == StyleKitLocation.User).Items.Select(item => item.Name));
    }

    /// <summary>
    ///     Test that Import refuses an archive that is not safe and says why, writes nothing outside the place, and says that
    ///     the project is needed when the project is the place.
    /// </summary>
    [Fact]
    public async Task StyleKitManager_Import_HostileZipAndMissingProject_AreRefusedWithAReason()
    {
        // Arrange: a zip file with a name that climbs out, and a file that is not a zip file
        var manager = await CreateAsync();
        var hostile = Path.Combine(_places.Root, "hostile.zip");
        using (var stream = File.Create(hostile))
        using (var archive = new ZipArchive(stream, ZipArchiveMode.Create))
        {
            await using var entry = await archive.CreateEntry("../evil.txt").OpenAsync(Ct);
            await entry.WriteAsync("evil"u8.ToArray(), Ct);
        }

        var notZip = Path.Combine(_places.Root, "plain.zip");
        await File.WriteAllTextAsync(notZip, "not a zip file", Ct);

        // Act
        _dialogs.ZipToImport = hostile;
        await manager.ImportCommand.ExecuteAsync(null);
        var hostileMessage = manager.StatusMessage;
        _dialogs.ZipToImport = notZip;
        await manager.ImportCommand.ExecuteAsync(null);
        var notZipMessage = manager.StatusMessage;
        manager.TargetIndex = 1;
        await manager.ImportCommand.ExecuteAsync(null);

        // Assert
        Assert.Contains("evil.txt", hostileMessage, StringComparison.Ordinal);
        Assert.False(File.Exists(Path.Combine(_places.Root, "evil.txt")));
        Assert.False(File.Exists(Path.Combine(_places.Root, "user", "evil.txt")));
        Assert.Contains("not a usable zip file", notZipMessage, StringComparison.Ordinal);
        Assert.Contains("Open a project first", manager.StatusMessage, StringComparison.Ordinal);
        Assert.Empty(_dialogs.Confirmations);
    }

    /// <summary>
    ///     Test that Copy into project copies the kit with its examples and id into the project, chooses the copy, and says
    ///     so when the project has a kit with that id, and that it is not available without a project or for a kit that is
    ///     in the project.
    /// </summary>
    [Fact]
    public async Task StyleKitManager_CopyIntoProject_BuiltInKit_FreezesACopyInTheProject()
    {
        // Arrange: a built-in kit with an example, and no project
        StyleKitPlaces.WriteKit(_places.BuiltIn, StyleKitPlaces.Kit("clean", "built-in", "Clean"), "a.svg");
        var manager = await CreateAsync();
        var builtIn = Item(manager, StyleKitLocation.BuiltIn, "clean");
        manager.SelectItemCommand.Execute(builtIn);
        var withoutProject = manager.CopyIntoProjectCommand.CanExecute(null);

        // Act: open a project and copy
        await OpenProjectAsync();
        await manager.CopyIntoProjectCommand.ExecuteAsync(null);
        var copied = manager.SelectedItem!;
        var canCopyAgain = manager.CopyIntoProjectCommand.CanExecute(null);

        // Act: choose the built-in kit again, which is hidden by the copy, and copy it again
        manager.SelectItemCommand.Execute(Item(manager, StyleKitLocation.BuiltIn, "clean"));
        await manager.CopyIntoProjectCommand.ExecuteAsync(null);

        // Assert
        Assert.False(withoutProject);
        Assert.Equal(StyleKitLocation.Project, copied.Entry.Location);
        Assert.True(File.Exists(Path.Combine(_places.ProjectStyles, "clean", "examples", "a.svg")));
        Assert.False(canCopyAgain);
        Assert.Contains("already has a kit with the id 'clean'", manager.StatusMessage, StringComparison.Ordinal);
        Assert.True(Item(manager, StyleKitLocation.BuiltIn, "clean").IsOverridden);
    }

    /// <summary>
    ///     Test that Refresh reads the places again, that Close asks the window to close, and that the groups follow the
    ///     project when it is opened and closed.
    /// </summary>
    [Fact]
    public async Task StyleKitManager_RefreshCloseAndProjectChanges_FollowTheCatalogAndTheProject()
    {
        // Arrange
        var manager = await CreateAsync();
        var closed = 0;
        manager.CloseRequested += (_, _) => closed++;
        StyleKitPlaces.WriteKit(_places.User, StyleKitPlaces.Kit("late", string.Empty, "Late"));

        // Act: the kit that was written is not known until the places are read again
        var before = manager.Groups.Single(group => group.Location == StyleKitLocation.User).Items.Count;
        await manager.RefreshCommand.ExecuteAsync(null);
        var after = manager.Groups.Single(group => group.Location == StyleKitLocation.User).Items.Count;
        manager.CloseCommand.Execute(null);
        await OpenProjectAsync();
        var hasProject = manager.HasProject;
        _places.CloseProject();
        await _catalog.PendingRefresh;

        // Assert
        Assert.Equal(0, before);
        Assert.Equal(1, after);
        Assert.Equal("The kits were read again.", manager.StatusMessage);
        Assert.Equal(1, closed);
        Assert.True(hasProject);
        Assert.False(manager.HasProject);
        Assert.DoesNotContain(manager.Groups, group => group.Location == StyleKitLocation.Project);
    }

    /// <summary>
    ///     Test that a disposed view model does not follow the catalog any more, that the choice of the place is held to the
    ///     two places, and that missing arguments are refused.
    /// </summary>
    [Fact]
    public async Task StyleKitManager_Dispose_StopsFollowingAndArgumentsAreChecked()
    {
        // Arrange
        var manager = await CreateAsync();
        manager.TargetIndex = 7;
        var held = manager.TargetIndex;
        manager.TargetIndex = -3;
        var heldLow = manager.TargetIndex;

        // Act: dispose, and then change the kits
        manager.Dispose();
        manager.Dispose();
        StyleKitPlaces.WriteKit(_places.User, StyleKitPlaces.Kit("later"));
        await _catalog.RefreshAsync(Ct);

        // Assert
        Assert.Equal(1, held);
        Assert.Equal(0, heldLow);
        Assert.Empty(manager.Groups.Single(group => group.Location == StyleKitLocation.User).Items);
        Assert.Throws<ArgumentNullException>(() => new StyleKitManagerViewModel(null!, _active, _renderer, _dialogs, _places.Projects));
        Assert.Throws<ArgumentNullException>(() => new StyleKitManagerViewModel(_catalog, null!, _renderer, _dialogs, _places.Projects));
        Assert.Throws<ArgumentNullException>(() => new StyleKitManagerViewModel(_catalog, _active, null!, _dialogs, _places.Projects));
        Assert.Throws<ArgumentNullException>(() => new StyleKitManagerViewModel(_catalog, _active, _renderer, null!, _places.Projects));
        Assert.Throws<ArgumentNullException>(() => new StyleKitManagerViewModel(_catalog, _active, _renderer, _dialogs, null!));
        Assert.Throws<ArgumentNullException>(() => new StyleKitItemViewModel(null!));
        Assert.Throws<ArgumentNullException>(() => new StyleThumbnailViewModel(null!, string.Empty));
        Assert.Throws<ArgumentNullException>(() => new StyleKitGroupViewModel(StyleKitLocation.User, null!, [], [], string.Empty));
    }

    /// <summary>
    ///     Test that a file that is asked for twice at once is drawn once, that the picture is kept, and that a caller that
    ///     stops waiting does not stop the drawing.
    /// </summary>
    [Fact]
    public async Task StyleThumbnailCache_GetAsync_ConcurrentRepeatedAndAbandoned_DrawsOnce()
    {
        // Arrange: a render that waits, and a file
        var gate = new TaskCompletionSource();
        _renderer.Gate = gate.Task;
        using var cache = new StyleThumbnailCache(_renderer);
        var file = WriteFile("one.svg", "one");

        // Act: two callers ask at once, one stops waiting, and the drawing is let go
        var first = cache.GetAsync(file, _places.Root, Ct);
        var second = cache.GetAsync(file, _places.Root, Ct);
        using var cancel = new CancellationTokenSource();
        var abandoned = cache.GetAsync(file, _places.Root, cancel.Token);
        await cancel.CancelAsync();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => abandoned);
        gate.SetResult();
        var results = await Task.WhenAll(first, second);
        var third = await cache.GetAsync(file, _places.Root, Ct);

        // Assert
        Assert.Single(_renderer.Requests);
        Assert.Equal(1, cache.RenderCount);
        Assert.NotNull(results[0].Image);
        Assert.Same(results[0].Image, results[1].Image);
        Assert.Same(results[0].Image, third.Image);
        Assert.Equal(1, cache.Count);
        Assert.Equal(240, StyleThumbnailCache.ThumbnailWidth);
    }

    /// <summary>
    ///     Test that the picture that was asked for longest ago is the one that goes when the cache is full.
    /// </summary>
    [Fact]
    public async Task StyleThumbnailCache_GetAsync_FullCache_ForgetsTheOneUsedLeastRecently()
    {
        // Arrange: a cache for two pictures and three files
        using var cache = new StyleThumbnailCache(_renderer, capacity: 2);
        var one = WriteFile("one.svg", "one");
        var two = WriteFile("two.svg", "two");
        var three = WriteFile("three.svg", "three");

        // Act: ask for one, two, one again, three (which pushes out two), then one and two
        await cache.GetAsync(one, _places.Root, Ct);
        await cache.GetAsync(two, _places.Root, Ct);
        await cache.GetAsync(one, _places.Root, Ct);
        await cache.GetAsync(three, _places.Root, Ct);
        var afterThree = cache.RenderCount;
        await cache.GetAsync(one, _places.Root, Ct);
        var afterOne = cache.RenderCount;
        await cache.GetAsync(two, _places.Root, Ct);

        // Assert
        Assert.Equal(3, afterThree);
        Assert.Equal(3, afterOne);
        Assert.Equal(4, cache.RenderCount);
        Assert.Equal(2, cache.Count);
        cache.Clear();
        Assert.Equal(0, cache.Count);
    }

    /// <summary>
    ///     Test that a file that is not there is reported without a drawing, that a drawing that fails with an exception is
    ///     a failure and not remembered, and that arguments are checked.
    /// </summary>
    [Fact]
    public async Task StyleThumbnailCache_GetAsync_MissingFileAndFailures_AreReportedAndNotRemembered()
    {
        // Arrange: a renderer that throws, and a file
        var throwing = Substitute.For<IImageRenderService>();
        throwing.RenderAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<RenderSettings?>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException<RenderResult>(new InvalidOperationException("The renderer broke.")));
        using var cache = new StyleThumbnailCache(throwing);
        var file = WriteFile("one.svg", "one");

        // Act
        var missing = await cache.GetAsync(Path.Combine(_places.Root, "nothing.svg"), _places.Root, Ct);
        var failed = await cache.GetAsync(file, _places.Root, Ct);
        var again = await cache.GetAsync(file, _places.Root, Ct);

        // Assert
        Assert.Equal("The file is not there.", missing.Error);
        Assert.Equal("The renderer broke.", failed.Error);
        Assert.True(failed.IsTransient);
        Assert.Equal("The renderer broke.", again.Error);
        Assert.Equal(2, cache.RenderCount);
        Assert.Equal(0, cache.Count);
        Assert.Throws<ArgumentNullException>(() => new StyleThumbnailCache(null!));
        Assert.Throws<ArgumentOutOfRangeException>(() => new StyleThumbnailCache(_renderer, 0));
        await Assert.ThrowsAsync<ArgumentException>(() => cache.GetAsync(" ", _places.Root, Ct));
        await Assert.ThrowsAsync<ArgumentException>(() => cache.GetAsync(file, string.Empty, Ct));
    }

    /// <summary>
    ///     Makes a kit with every part: four colors, three font lists, a Mermaid theme with one variable, a Vega-Lite
    ///     configuration, advice, two sizes, and an example with a caption.
    /// </summary>
    /// <param name="id">The id of the kit.</param>
    /// <returns>The kit.</returns>
    private static StyleKit FullKit(string id) =>
        new(
            id,
            "Team",
            "The look of the team.",
            new Dictionary<string, string>
            {
                ["background"] = "#FFFFFF",
                ["text"] = "#0F172A",
                ["primary"] = "#2563EB",
                ["accent"] = "#F59E0B"
            },
            new StyleFonts("sans-serif", "sans-serif", "monospace"),
            2,
            12,
            16,
            new StyleMermaid("base", new Dictionary<string, string> { ["primaryColor"] = "#2563EB" }),
            "{\"range\":{\"category\":[\"#2563EB\",\"#F59E0B\"]}}",
            new StyleGuidanceText("Keep it plain.", ["Use the palette"], ["Use gradients"]),
            [new StyleSizePreset("Slide", 1920, 1080, null, false), new StyleSizePreset("Banner", 1600, null, 150, true)],
            [new StyleExample("examples/a.svg", string.Empty, "The first example")]);

    /// <summary>
    ///     Finds the item of a kit.
    /// </summary>
    /// <param name="manager">The view model.</param>
    /// <param name="location">The place of the kit.</param>
    /// <param name="id">The id of the kit.</param>
    /// <returns>The item.</returns>
    private static StyleKitItemViewModel Item(StyleKitManagerViewModel manager, StyleKitLocation location, string id) =>
        manager.Groups.SelectMany(group => group.Items).Single(item => item.Entry.Location == location && item.Id == id);

    /// <summary>
    ///     Reads the places and makes a view model over them.
    /// </summary>
    /// <returns>The view model, which is disposed after the test.</returns>
    private async Task<StyleKitManagerViewModel> CreateAsync()
    {
        await _catalog.PendingRefresh;
        await _catalog.RefreshAsync(Ct);
        var manager = new StyleKitManagerViewModel(_catalog, _active, _renderer, _dialogs, _places.Projects);
        _managers.Add(manager);
        return manager;
    }

    /// <summary>
    ///     Opens a project and waits until the catalog has read the places again because of it.
    /// </summary>
    /// <returns>A task that completes when the places are read.</returns>
    private async Task OpenProjectAsync()
    {
        _places.OpenProject();
        await _catalog.PendingRefresh;
        await _catalog.RefreshAsync(Ct);
    }

    /// <summary>
    ///     Writes a file in the temporary folder.
    /// </summary>
    /// <param name="name">The name of the file.</param>
    /// <param name="text">The text.</param>
    /// <returns>The full path of the file.</returns>
    private string WriteFile(string name, string text)
    {
        var path = Path.Combine(_places.Root, name);
        File.WriteAllText(path, text);
        return path;
    }
}
