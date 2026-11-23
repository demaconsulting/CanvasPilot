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
using CommunityToolkit.Mvvm.Input;
using DemaConsulting.CanvasPilot.ProjectSubsystem;
using DemaConsulting.CanvasPilot.SettingsSubsystem;
using DemaConsulting.CanvasPilot.StyleSubsystem;

namespace DemaConsulting.CanvasPilot.Tests;

/// <summary>
///     Unit tests for <see cref="StylePickerViewModel" /> over a real project service, a real settings service, a real
///     catalog of kits, and a real selection on temporary folders: the choices it offers, that a choice is written into the
///     project file, how a kit that cannot be found and a choice that cannot be kept are shown, and when it is visible.
/// </summary>
public sealed class StylePickerTests : IDisposable
{
    /// <summary>
    ///     The folder that holds everything, removed on disposal.
    /// </summary>
    private readonly string _root = Directory.CreateTempSubdirectory("canvaspilot-style-picker-").FullName;

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
    ///     The kit that the open project uses.
    /// </summary>
    private readonly StyleSelection _selection;

    /// <summary>
    ///     Initializes a new instance of the <see cref="StylePickerTests" /> class with two built-in kits.
    /// </summary>
    public StylePickerTests()
    {
        _builtIn = Directory.CreateDirectory(Path.Combine(_root, "builtin")).FullName;
        _user = Path.Combine(_root, "user");
        _settings = new SettingsService(Path.Combine(_root, "settings"));
        _settings.Load();
        StyleKitPlaces.WriteKit(_builtIn, StyleKitPlaces.Kit("clean", "A light, neutral kit.", "Clean"));
        StyleKitPlaces.WriteKit(_builtIn, StyleKitPlaces.Kit("slides-dark", "For slides.", "Slides Dark"));
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
    ///     Test that the list holds no style first and then each kit with the place it comes from, and that it is shown only
    ///     while a project is open.
    /// </summary>
    [Fact]
    public async Task StylePicker_Items_KitsOfTheCatalog_NoStyleFirstAndVisibleOnlyWithAProject()
    {
        // Arrange: the kits are read, and no project is open
        using var picker = CreatePicker();
        await _catalog.RefreshAsync(Ct);

        // Act: read the list without a project, then with one
        var hiddenItems = picker.Items.Select(item => item.Label).ToArray();
        var visibleWithoutProject = picker.IsVisible;
        await OpenProjectAsync("Shown");

        // Assert: no style is first and selected, each kit says where it is from, and the list follows the project
        Assert.Equal([StylePickerViewModel.NoStyleLabel, "Clean (built-in)", "Slides Dark (built-in)"], hiddenItems);
        Assert.False(visibleWithoutProject);
        Assert.True(picker.IsVisible);
        Assert.Equal(StylePickerViewModel.NoStyleLabel, picker.SelectedItem!.Label);
        Assert.Contains("A light, neutral kit.", picker.Items[1].ToolTip, StringComparison.Ordinal);
        Assert.Contains(_builtIn, picker.Items[1].ToolTip, StringComparison.Ordinal);
        _projects.Close();
        Assert.False(picker.IsVisible);
    }

    /// <summary>
    ///     Test that a choice gives the name of the kit without its place and up to three main colors of its palette, that no
    ///     style has none, and that the background and the text are never among the colors.
    /// </summary>
    [Fact]
    public async Task StylePicker_Items_KitPalette_GivesNameAndUpToThreeMainColors()
    {
        // Arrange: a kit with many colors besides the two kits of the fixture
        var vivid = StyleKitPlaces.Kit("vivid", "Colors.", "Vivid") with
        {
            Palette = new Dictionary<string, string>
            {
                ["background"] = "#FFFFFF",
                ["text"] = "#000000",
                ["highlight"] = "#111111",
                ["accent"] = "#333333",
                ["primary"] = "#4F46E5",
                ["secondary"] = "#06B6D4",
                ["extra"] = "#444444"
            }
        };
        StyleKitPlaces.WriteKit(_builtIn, vivid);
        using var picker = CreatePicker();
        await _catalog.RefreshAsync(Ct);

        // Act: read the choices
        var none = picker.Items[0];
        var clean = picker.Items.Single(item => item.Id == "clean");
        var colorful = picker.Items.Single(item => item.Id == "vivid");

        // Assert: no style is drawn empty, a plain kit has its one color, and a rich kit has primary, secondary, and accent
        Assert.Equal(StylePickerViewModel.NoStyleLabel, none.DisplayName);
        Assert.True(none.HasNoSwatches);
        Assert.Empty(none.Swatches);
        Assert.Equal("Clean", clean.DisplayName);
        Assert.Equal("Clean (built-in)", clean.Label);
        Assert.Equal(["#2563EB"], clean.Swatches);
        Assert.False(clean.HasNoSwatches);
        Assert.Equal("Vivid", colorful.DisplayName);
        Assert.Equal(["#4F46E5", "#06B6D4", "#333333"], colorful.Swatches);
    }

    /// <summary>
    ///     Test that the command behind the footer entry of the list can be set and taken away, and that the list says
    ///     whether it has one.
    /// </summary>
    [Fact]
    public void StylePicker_ManageKitsCommand_SetThenCleared_IsAnnouncedAndTold()
    {
        // Arrange: a picker with no such command that announces its changes
        using var picker = CreatePicker();
        var changed = new List<string?>();
        picker.PropertyChanged += (_, e) => changed.Add(e.PropertyName);
        var before = picker.HasManageKits;
        var ran = 0;

        // Act: set a command, run it through the picker, and take it away
        picker.ManageKitsCommand = new RelayCommand(() => ran++);
        var withCommand = picker.HasManageKits;
        picker.ManageKitsCommand.Execute(null);
        picker.ManageKitsCommand = null;

        // Assert: the picker told and announced it each time
        Assert.False(before);
        Assert.True(withCommand);
        Assert.Equal(1, ran);
        Assert.False(picker.HasManageKits);
        Assert.Equal(2, changed.Count(name => name == nameof(StylePickerViewModel.HasManageKits)));
    }

    /// <summary>
    ///     Test that choosing a kit writes its id into the project file, that the selection stays on it, and that choosing no
    ///     style removes the id again.
    /// </summary>
    [Fact]
    public async Task StylePicker_Select_KitThenNoStyle_IsWrittenToTheProjectFile()
    {
        // Arrange: a project and a picker that shows the kits
        using var picker = CreatePicker();
        await OpenProjectAsync("Chosen");

        // Act: choose a kit and wait for the file to be written
        picker.SelectedItem = picker.Items.Single(item => item.Id == "slides-dark");
        await picker.ChangeTask;
        var withKit = ReadStyleProperty();
        var selectedAfterKit = picker.SelectedItem.Id;

        // Act: choose no style
        picker.SelectedItem = picker.Items[0];
        await picker.ChangeTask;
        var withoutKit = ReadStyleProperty();

        // Assert: the file says what was chosen, and nothing was reported
        Assert.Equal("slides-dark", withKit);
        Assert.Equal("slides-dark", selectedAfterKit);
        Assert.Null(withoutKit);
        Assert.Null(picker.SelectedItem.Id);
        Assert.False(picker.HasError);
        Assert.Equal(string.Empty, picker.ErrorMessage);
    }

    /// <summary>
    ///     Test that a kit that the project names and that cannot be found is shown as not found and selected, that choosing
    ///     it again changes nothing, and that it goes when another choice is made.
    /// </summary>
    [Fact]
    public async Task StylePicker_MissingKit_IsShownAsNotFoundAndGoesWhenAnotherIsChosen()
    {
        // Arrange: a project whose file names a kit that nobody has
        using var picker = CreatePicker();
        await OpenProjectAsync("Gone");
        await _projects.SetActiveStyleKitAsync("team-kit", Ct);

        // Act: read the list, and choose the missing kit again
        var missing = picker.Items[^1];
        var selectedWhileMissing = picker.SelectedItem;
        picker.SelectedItem = missing;
        await picker.ChangeTask;
        var stillMissing = ReadStyleProperty();
        picker.SelectedItem = picker.Items.Single(item => item.Id == "clean");
        await picker.ChangeTask;

        // Assert: the missing kit was shown with the reason, was left alone, and went with the next choice
        Assert.Equal("team-kit (not found)", missing.Label);
        Assert.True(missing.IsMissing);
        Assert.Contains("cannot be found", missing.ToolTip, StringComparison.Ordinal);
        Assert.Equal(missing, selectedWhileMissing);
        Assert.Equal("team-kit", stillMissing);
        Assert.DoesNotContain(picker.Items, item => item.IsMissing);
        Assert.Equal("clean", picker.SelectedItem.Id);
        Assert.Equal("clean", ReadStyleProperty());
    }

    /// <summary>
    ///     Test that a kit of a more local place that hides a kit of the same id is the only one listed for the id.
    /// </summary>
    [Fact]
    public async Task StylePicker_Items_KitHiddenByAMoreLocalKit_IsListedOnceWithTheLocalPlace()
    {
        // Arrange: a kit of the user that has the id of a built-in kit
        StyleKitPlaces.WriteKit(_user, StyleKitPlaces.Kit("clean", "My own clean.", "My Clean"));
        using var picker = CreatePicker();
        await _catalog.RefreshAsync(Ct);

        // Act / Assert: the id is listed once, with the kit of the user
        var clean = picker.Items.Where(item => item.Id == "clean").ToArray();
        Assert.Single(clean);
        Assert.Equal("My Clean (my kits)", clean[0].Label);
        Assert.Equal(3, picker.Items.Count);
    }

    /// <summary>
    ///     Test that a choice that cannot be kept, because no project is open, is reported and that the list shows what the
    ///     project really uses again.
    /// </summary>
    [Fact]
    public async Task StylePicker_Select_NoProject_ReportsTheReasonAndShowsNoStyleAgain()
    {
        // Arrange: kits but no project
        using var picker = CreatePicker();
        await _catalog.RefreshAsync(Ct);

        // Act: choose a kit
        picker.SelectedItem = picker.Items.Single(item => item.Id == "clean");
        await picker.ChangeTask;

        // Assert: the reason is shown and the selection is back on no style
        Assert.True(picker.HasError);
        Assert.Contains("project", picker.ErrorMessage, StringComparison.OrdinalIgnoreCase);
        Assert.Null(picker.SelectedItem.Id);

        // Act: a choice that works clears the report
        await OpenProjectAsync("Late");
        picker.SelectedItem = picker.Items.Single(item => item.Id == "clean");
        await picker.ChangeTask;

        // Assert: the report is gone
        Assert.False(picker.HasError);
        Assert.Equal("clean", ReadStyleProperty());
    }

    /// <summary>
    ///     Test that the list follows a choice that is made elsewhere, such as by another part of the application or by the
    ///     project file of another project.
    /// </summary>
    [Fact]
    public async Task StylePicker_SelectedItem_ChoiceMadeElsewhere_IsFollowedWithoutWritingAgain()
    {
        // Arrange: a picker over an open project
        using var picker = CreatePicker();
        await OpenProjectAsync("Elsewhere");

        // Act: the selection object makes the choice, as another part of the application would
        await _selection.SetActiveAsync("slides-dark");

        // Assert: the list shows it and no choice of the list was started
        Assert.Equal("slides-dark", picker.SelectedItem!.Id);
        Assert.True(picker.ChangeTask.IsCompleted);
    }

    /// <summary>
    ///     Test that giving a new project its default kit is passed on, and that it is nothing to do when there is no way
    ///     to give it.
    /// </summary>
    [Fact]
    public async Task StylePicker_ApplyDefaultToNewProjectAsync_PassesOnOrDoesNothing()
    {
        // Arrange: a picker whose default is applied through the selection, and one with nothing to apply
        _settings.Update(s => s with { Styles = s.Styles with { DefaultKitId = "clean" } });
        using var withDefault = CreatePicker();
        using var without = new StylePickerViewModel(_selection, _catalog, _projects);
        await OpenProjectAsync("Defaulted");

        // Act
        await without.ApplyDefaultToNewProjectAsync();
        var afterWithout = ReadStyleProperty();
        await withDefault.ApplyDefaultToNewProjectAsync();

        // Assert: only the picker that had a way to apply the default applied it
        Assert.Null(afterWithout);
        Assert.Equal("clean", ReadStyleProperty());
        Assert.Equal("clean", withDefault.SelectedItem!.Id);
    }

    /// <summary>
    ///     Test that the places are named for the user in the same words every time.
    /// </summary>
    [Fact]
    public void StylePicker_PlaceText_EveryPlace_IsNamedForTheUser()
    {
        // Act / Assert: each place has its name
        Assert.Equal("project", StylePickerViewModel.PlaceText(StyleKitLocation.Project));
        Assert.Equal("my kits", StylePickerViewModel.PlaceText(StyleKitLocation.User));
        Assert.Equal("shared", StylePickerViewModel.PlaceText(StyleKitLocation.Shared));
        Assert.Equal("built-in", StylePickerViewModel.PlaceText(StyleKitLocation.BuiltIn));
    }

    /// <summary>
    ///     Test that a disposed picker stops following and that missing collaborators are refused.
    /// </summary>
    [Fact]
    public async Task StylePicker_DisposeAndConstructor_StopsFollowingAndRefusesNull()
    {
        // Arrange: a picker that is disposed before the kits are read
        var picker = CreatePicker();
        picker.Dispose();
        picker.Dispose();

        // Act: the kits are read afterwards
        await _catalog.RefreshAsync(Ct);

        // Assert: the list was not changed, and null collaborators are refused
        Assert.Single(picker.Items);
        Assert.Throws<ArgumentNullException>(() => new StylePickerViewModel(null!, _catalog, _projects));
        Assert.Throws<ArgumentNullException>(() => new StylePickerViewModel(_selection, null!, _projects));
        Assert.Throws<ArgumentNullException>(() => new StylePickerViewModel(_selection, _catalog, null!));
    }

    /// <summary>
    ///     Creates a picker over the real units with the default of the selection as its way to start a project.
    /// </summary>
    /// <returns>The picker.</returns>
    private StylePickerViewModel CreatePicker() =>
        new(_selection, _catalog, _projects, _selection.ApplyDefaultToNewProjectAsync);

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

        // The catalog reads the places again on its own when the project changes; wait for that before reading them
        await _catalog.PendingRefresh;
        await _catalog.RefreshAsync(Ct);
    }

    /// <summary>
    ///     Reads the id of the kit from the project file of the open project.
    /// </summary>
    /// <returns>The id, or <see langword="null" /> when the file names none.</returns>
    private string? ReadStyleProperty()
    {
        using var document = JsonDocument.Parse(File.ReadAllText(_projects.Current!.FilePath));
        return document.RootElement.TryGetProperty("activeStyleKit", out var property) ? property.GetString() : null;
    }
}
