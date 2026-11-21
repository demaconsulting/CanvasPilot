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

using DemaConsulting.CanvasPilot.ProjectSubsystem;
using DemaConsulting.CanvasPilot.SettingsSubsystem;
using DemaConsulting.CanvasPilot.StyleSubsystem;

namespace DemaConsulting.CanvasPilot.Tests;

/// <summary>
///     Hand-written folder picker for tests: it answers with a path that a test sets and remembers where it was asked to
///     start.
/// </summary>
public sealed class FixedFolderPicker : IFolderPicker
{
    /// <summary>
    ///     Gets or sets the folder that the user chooses, or <see langword="null" /> for cancel.
    /// </summary>
    public string? Chosen { get; set; }

    /// <summary>
    ///     Gets the folder that the picker was last asked to start in.
    /// </summary>
    public string? LastInitialFolder { get; private set; }

    /// <summary>
    ///     Gets how many times the user was asked.
    /// </summary>
    public int Asked { get; private set; }

    /// <inheritdoc />
    public Task<string?> PickFolderAsync(string? initialFolder)
    {
        Asked++;
        LastInitialFolder = initialFolder;
        return Task.FromResult(Chosen);
    }
}

/// <summary>
///     Unit tests for <see cref="StyleSettingsPageViewModel" /> over a real catalog of kits and a real settings service on
///     temporary folders: how the folders and their status are shown, adding by path and with the picker, removing and
///     reordering, the default kit, what applying changes, an entry that cannot be saved, and the following of the catalog.
/// </summary>
public sealed class StyleSettingsPageTests : IDisposable
{
    /// <summary>
    ///     The places that hold the kits, with a settings service.
    /// </summary>
    private readonly StyleKitPlaces _places = new();

    /// <summary>
    ///     The catalog of kits over the places.
    /// </summary>
    private readonly StyleKitCatalog _catalog;

    /// <summary>
    ///     The picker that the page asks for a folder.
    /// </summary>
    private readonly FixedFolderPicker _picker = new();

    /// <summary>
    ///     Initializes a new instance of the <see cref="StyleSettingsPageTests" /> class with two kits of the program.
    /// </summary>
    public StyleSettingsPageTests()
    {
        StyleKitPlaces.WriteKit(_places.BuiltIn, StyleKitPlaces.Kit("clean", "Clean.", "Clean"));
        StyleKitPlaces.WriteKit(_places.BuiltIn, StyleKitPlaces.Kit("slides-dark", "Dark.", "Slides Dark"));
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
        _places.Dispose();
    }

    /// <summary>
    ///     Test that loading shows the folders in order, each with whether the catalog could read it, a folder that was not
    ///     read yet, and the name of the page.
    /// </summary>
    [Fact]
    public async Task StyleSettingsPage_Load_Folders_AreListedInOrderWithTheirStatus()
    {
        // Arrange: one shared folder that exists, one that does not, and one that the catalog has not been told about
        Directory.CreateDirectory(_places.Shared1);
        _places.SetSharedFolders(_places.Shared1, _places.Shared2);
        await _catalog.PendingRefresh;
        await _catalog.RefreshAsync(Ct);
        var settings = _places.Settings.Current with
        {
            Styles = _places.Settings.Current.Styles with
            {
                SharedKitFolders = [_places.Shared1, _places.Shared2, Path.Combine(_places.Root, "later")]
            }
        };
        var page = CreatePage();

        // Act
        page.Load(settings);

        // Assert: the order was kept and each row says what is known about the folder
        Assert.Equal("Style Kits", page.Title);
        Assert.Equal([_places.Shared1, _places.Shared2, Path.Combine(_places.Root, "later")], page.Folders.Select(row => row.Path));
        Assert.Equal("Reachable.", page.Folders[0].StatusText);
        Assert.StartsWith("Not reachable now", page.Folders[1].StatusText, StringComparison.Ordinal);
        Assert.StartsWith("Not read yet", page.Folders[2].StatusText, StringComparison.Ordinal);
        Assert.All(page.Folders, row => Assert.True(row.IsValid));
        Assert.True(page.IsValid);
        page.OnClosed();
    }

    /// <summary>
    ///     Test that a typed or pasted full path is added at the end and selected, with the white space around it dropped,
    ///     and that a path that is the same folder with another case or a closing separator is refused.
    /// </summary>
    [Fact]
    public void StyleSettingsPage_AddTypedFolder_FullPath_IsAddedSelectedAndDuplicatesAreRefused()
    {
        // Arrange: a page with one folder
        var page = CreatePage();
        page.Load(SettingsWith(_places.Shared1));
        var cannotAddNothing = !page.AddTypedFolderCommand.CanExecute(null);

        // Act: add a second folder, then the first again as another spelling of the same folder
        page.NewFolderText = $"  {_places.Shared2}  ";
        var canAdd = page.AddTypedFolderCommand.CanExecute(null);
        page.AddTypedFolderCommand.Execute(null);
        var afterAdd = (Paths: page.Folders.Select(row => row.Path).ToArray(), Selected: page.SelectedFolder?.Path, page.NewFolderText, page.HasFolderError);
        page.NewFolderText = _places.Shared1.ToUpperInvariant() + Path.DirectorySeparatorChar;
        page.AddTypedFolderCommand.Execute(null);

        // Assert
        Assert.True(cannotAddNothing);
        Assert.True(canAdd);
        Assert.Equal([_places.Shared1, _places.Shared2], afterAdd.Paths);
        Assert.Equal((_places.Shared2, string.Empty, false), (afterAdd.Selected, afterAdd.NewFolderText, afterAdd.HasFolderError));
        Assert.Equal(2, page.Folders.Count);
        Assert.True(page.HasFolderError);
        Assert.Contains("in the list already", page.FolderError, StringComparison.Ordinal);
        page.OnClosed();
    }

    /// <summary>
    ///     Test that a path that is not a full path is refused with a message and not added.
    /// </summary>
    [Theory]
    [InlineData("kits\\team")]
    [InlineData("..\\team")]
    [InlineData("\\team")]
    public void StyleSettingsPage_AddTypedFolder_NotAFullPath_IsRefusedWithAMessage(string text)
    {
        // Arrange: an empty page
        var page = CreatePage();
        page.Load(SettingsWith());

        // Act
        page.NewFolderText = text;
        page.AddTypedFolderCommand.Execute(null);

        // Assert: nothing was added, the text stays for the user to correct, and the message names the text
        Assert.Empty(page.Folders);
        Assert.Equal(text, page.NewFolderText);
        Assert.True(page.HasFolderError);
        Assert.Contains($"'{text}'", page.FolderError, StringComparison.Ordinal);
        Assert.True(page.IsValid);
    }

    /// <summary>
    ///     Test that browsing adds the folder that the user chooses, starts in the selected folder that exists, does nothing
    ///     when the user cancels or when the window has no picker yet.
    /// </summary>
    [Fact]
    public async Task StyleSettingsPage_BrowseFolder_PickerChoosesCancelsOrIsMissing_AddsOrDoesNothing()
    {
        // Arrange: a page with one folder that exists, selected
        Directory.CreateDirectory(_places.Shared1);
        var page = CreatePage();
        page.Load(SettingsWith(_places.Shared1));
        page.SelectedFolder = page.Folders[0];
        IFolderPicker? available = _picker;
        var withoutPicker = new StyleSettingsPageViewModel(_catalog, () => available);

        // Act: the user chooses a folder, then cancels, then the window has no picker
        _picker.Chosen = _places.Shared2;
        await page.BrowseFolderCommand.ExecuteAsync(null);
        var afterChoice = page.Folders.Select(row => row.Path).ToArray();
        var initialOfFirstAsk = _picker.LastInitialFolder;
        _picker.Chosen = null;
        await page.BrowseFolderCommand.ExecuteAsync(null);
        var afterCancel = page.Folders.Count;
        available = null;
        _picker.Chosen = Path.Combine(_places.Root, "other");
        await withoutPicker.BrowseFolderCommand.ExecuteAsync(null);

        // Assert: the choice was added and the other two did nothing
        Assert.Equal([_places.Shared1, _places.Shared2], afterChoice);
        Assert.Equal(_places.Shared1, initialOfFirstAsk);
        Assert.Equal(2, _picker.Asked);
        Assert.Equal(2, afterCancel);
        Assert.Empty(withoutPicker.Folders);
    }

    /// <summary>
    ///     Test that a folder can be removed, moved up, and moved down, that the commands are usable only where they can
    ///     work, and that the selection stays on the folder that moved.
    /// </summary>
    [Fact]
    public void StyleSettingsPage_RemoveAndMove_SelectedFolder_ChangesTheOrderAndTheCommandsFollow()
    {
        // Arrange: three folders, none selected
        var page = CreatePage();
        var a = Path.Combine(_places.Root, "a");
        var b = Path.Combine(_places.Root, "b");
        var c = Path.Combine(_places.Root, "c");
        page.Load(SettingsWith(a, b, c));
        var noneSelected = (page.RemoveFolderCommand.CanExecute(null), page.MoveFolderUpCommand.CanExecute(null), page.MoveFolderDownCommand.CanExecute(null));

        // Act: the first folder cannot move up, moves down twice, and the last cannot move down
        page.SelectedFolder = page.Folders[0];
        var first = (page.MoveFolderUpCommand.CanExecute(null), page.MoveFolderDownCommand.CanExecute(null));
        page.MoveFolderDownCommand.Execute(null);
        page.MoveFolderDownCommand.Execute(null);
        var afterDowns = page.Folders.Select(row => row.Path).ToArray();
        var last = (page.MoveFolderUpCommand.CanExecute(null), page.MoveFolderDownCommand.CanExecute(null));
        var selectedAfterMove = page.SelectedFolder?.Path;
        page.MoveFolderUpCommand.Execute(null);
        var afterUp = page.Folders.Select(row => row.Path).ToArray();
        page.RemoveFolderCommand.Execute(null);
        var afterRemove = page.Folders.Select(row => row.Path).ToArray();
        var selectedAfterRemove = page.SelectedFolder?.Path;

        // Assert
        Assert.Equal((false, false, false), noneSelected);
        Assert.Equal((false, true), first);
        Assert.Equal([b, c, a], afterDowns);
        Assert.Equal((true, false), last);
        Assert.Equal(a, selectedAfterMove);
        Assert.Equal([b, a, c], afterUp);
        Assert.Equal([b, c], afterRemove);
        Assert.Equal(c, selectedAfterRemove);
        page.OnClosed();
    }

    /// <summary>
    ///     Test that applying merges only the style settings into the settings that it is given, with the folders in order and
    ///     the default kit, and leaves every other member as it was.
    /// </summary>
    [Fact]
    public async Task StyleSettingsPage_Apply_FoldersAndDefaultKit_MergesOnlyTheStyles()
    {
        // Arrange: settings with other members set, and a page in which a folder was added and a default kit chosen
        await _catalog.RefreshAsync(Ct);
        var other = _places.Settings.Current with
        {
            RecentProjects = ["C:\\work\\p.canvaspilot"],
            AiProvider = _places.Settings.Current.AiProvider with { ModelName = "kept" },
        };
        var page = CreatePage();
        page.Load(other);
        page.NewFolderText = _places.Shared2;
        page.AddTypedFolderCommand.Execute(null);
        page.NewFolderText = _places.Shared1;
        page.AddTypedFolderCommand.Execute(null);
        page.SelectedFolder = page.Folders[1];
        page.MoveFolderUpCommand.Execute(null);
        page.SelectedDefaultKit = page.DefaultKitChoices.Single(choice => choice.Id == "clean");

        // Act
        var applied = page.Apply(other);

        // Assert: the folders and the default kit are the page's, and nothing else changed
        Assert.Equal([_places.Shared1, _places.Shared2], applied.Styles.SharedKitFolders);
        Assert.Equal("clean", applied.Styles.DefaultKitId);
        Assert.Equal(other with { Styles = applied.Styles }, applied);
        Assert.Equal(["C:\\work\\p.canvaspilot"], applied.RecentProjects);
        Assert.Equal("kept", applied.AiProvider.ModelName);
        page.OnClosed();
    }

    /// <summary>
    ///     Test that a folder that is not a full path makes the page invalid and cannot be applied, that removing it makes
    ///     the page valid again, and that a folder that cannot be reached is allowed.
    /// </summary>
    [Fact]
    public void StyleSettingsPage_IsValid_EntryThatIsNotAFullPath_BlocksApplyUntilRemoved()
    {
        // Arrange: settings that hold a relative folder, as an edited file might, and a folder that does not exist
        var unreachable = Path.Combine(_places.Root, "no-such-folder");
        var page = CreatePage();
        var changes = new List<string?>();
        page.PropertyChanged += (_, e) => changes.Add(e.PropertyName);
        page.Load(SettingsWith(unreachable, "relative\\kits"));

        // Act
        var validWithRelative = page.IsValid;
        var rowIsValid = page.Folders.Select(row => row.IsValid).ToArray();
        var statusOfRelative = page.Folders[1].StatusText;
        var applyThrows = Assert.Throws<InvalidOperationException>(() => page.Apply(_places.Settings.Current));
        page.SelectedFolder = page.Folders[1];
        page.RemoveFolderCommand.Execute(null);
        var applied = page.Apply(_places.Settings.Current);

        // Assert: the relative entry blocked the apply and the unreachable folder did not
        Assert.False(validWithRelative);
        Assert.Equal([true, false], rowIsValid);
        Assert.Contains("Not a full path", statusOfRelative, StringComparison.Ordinal);
        Assert.Contains("full path", applyThrows.Message, StringComparison.Ordinal);
        Assert.True(page.IsValid);
        Assert.Contains(nameof(StyleSettingsPageViewModel.IsValid), changes);
        Assert.Equal([unreachable], applied.Styles.SharedKitFolders);
        page.OnClosed();
    }

    /// <summary>
    ///     Test that the choices of the default kit are no style and each kit that is not hidden, that the default of the
    ///     settings is selected, that a default that no kit has is kept and shown as not found, and that choosing another one
    ///     changes what is applied.
    /// </summary>
    [Fact]
    public async Task StyleSettingsPage_DefaultKit_ChoicesSelectionAndUnknownDefault_AreShownAndApplied()
    {
        // Arrange: the kits are read, and the settings name a default that no kit has
        await _catalog.RefreshAsync(Ct);
        var page = CreatePage();
        page.Load(SettingsWithDefault("team-kit"));

        // Act
        var labels = page.DefaultKitChoices.Select(choice => choice.Label).ToArray();
        var selectedUnknown = page.SelectedDefaultKit;
        var keptWhenApplied = page.Apply(_places.Settings.Current).Styles.DefaultKitId;
        page.SelectedDefaultKit = page.DefaultKitChoices.Single(choice => choice.Id == "slides-dark");
        var chosen = page.Apply(_places.Settings.Current).Styles.DefaultKitId;
        page.SelectedDefaultKit = page.DefaultKitChoices[0];
        var none = page.Apply(_places.Settings.Current).Styles.DefaultKitId;
        page.Load(SettingsWithDefault("clean"));
        var reloaded = page.SelectedDefaultKit?.Label;

        // Assert
        Assert.Equal([StyleSettingsPageViewModel.NoStyleLabel, "Clean (built-in)", "Slides Dark (built-in)", "team-kit (not found)"], labels);
        Assert.True(selectedUnknown!.IsMissing);
        Assert.Equal("team-kit", keptWhenApplied);
        Assert.Equal("slides-dark", chosen);
        Assert.Equal(string.Empty, none);
        Assert.Equal("Clean (built-in)", reloaded);
        Assert.DoesNotContain(page.DefaultKitChoices, choice => choice.IsMissing);
        page.OnClosed();
    }

    /// <summary>
    ///     Test that while the page is open it follows the catalog, so that a folder that was not read yet shows its status
    ///     and the kits appear among the choices when they have been read, and that after the page is closed it follows no
    ///     more, and follows again when it is loaded again.
    /// </summary>
    [Fact]
    public async Task StyleSettingsPage_CatalogChanges_AreFollowedWhileOpenOnly()
    {
        // Arrange: the page is loaded before the kits were read, with a default kit that the catalog will have
        Directory.CreateDirectory(_places.Shared1);
        var page = CreatePage();
        page.Load(SettingsWith(_places.Shared1) with { Styles = new StyleSettings { SharedKitFolders = [_places.Shared1], DefaultKitId = "clean" } });
        var before = (page.Folders[0].StatusText, page.SelectedDefaultKit!.Label);

        // Act: the settings name the folder and the catalog reads the places; then the page is closed and a folder is added
        _places.SetSharedFolders(_places.Shared1);
        await _catalog.PendingRefresh;
        await _catalog.RefreshAsync(Ct);
        var whileOpen = (page.Folders[0].StatusText, page.SelectedDefaultKit.Label);
        page.OnClosed();
        page.OnClosed();
        Directory.Delete(_places.Shared1);
        await _catalog.RefreshAsync(Ct);
        var whileClosed = page.Folders[0].StatusText;
        page.Load(SettingsWith(_places.Shared1));
        var afterLoad = page.Folders[0].StatusText;
        Directory.CreateDirectory(_places.Shared1);
        await _catalog.RefreshAsync(Ct);
        var afterRestore = page.Folders[0].StatusText;

        // Assert: the status and the choice followed the catalog while the page was open, and not while it was closed
        Assert.StartsWith("Not read yet", before.Item1, StringComparison.Ordinal);
        Assert.Equal("clean (not found)", before.Item2);
        Assert.Equal(("Reachable.", "Clean (built-in)"), whileOpen);
        Assert.Equal("Reachable.", whileClosed);
        Assert.StartsWith("Not reachable now", afterLoad, StringComparison.Ordinal);
        Assert.Equal("Reachable.", afterRestore);
        page.OnClosed();
    }

    /// <summary>
    ///     Test that missing collaborators and missing settings are refused.
    /// </summary>
    [Fact]
    public void StyleSettingsPage_NullArguments_Throw()
    {
        // Arrange
        var page = CreatePage();

        // Act / Assert
        Assert.Throws<ArgumentNullException>(() => new StyleSettingsPageViewModel(null!, () => _picker));
        Assert.Throws<ArgumentNullException>(() => new StyleSettingsPageViewModel(_catalog, null!));
        Assert.Throws<ArgumentNullException>(() => page.Load(null!));
        Assert.Throws<ArgumentNullException>(() => page.Apply(null!));
    }

    /// <summary>
    ///     Test that the rule for a folder is a full path without a character that a path cannot have.
    /// </summary>
    [Fact]
    public void StyleSettingsPage_IsFullPath_Texts_FollowTheRuleOfTheSettings()
    {
        // Act / Assert
        Assert.True(StyleSettingsPageViewModel.IsFullPath(_places.Root));
        Assert.True(StyleSettingsPageViewModel.IsFullPath("\\\\server\\share\\kits"));
        Assert.False(StyleSettingsPageViewModel.IsFullPath(null));
        Assert.False(StyleSettingsPageViewModel.IsFullPath("   "));
        Assert.False(StyleSettingsPageViewModel.IsFullPath("kits"));
        Assert.False(StyleSettingsPageViewModel.IsFullPath("C:kits"));
        Assert.False(StyleSettingsPageViewModel.IsFullPath(_places.Root + "\0"));
    }

    /// <summary>
    ///     Creates a page over the catalog and the fixed picker.
    /// </summary>
    /// <returns>The page.</returns>
    private StyleSettingsPageViewModel CreatePage() => new(_catalog, () => _picker);

    /// <summary>
    ///     Makes settings with the given shared folders.
    /// </summary>
    /// <param name="folders">The folders, in order.</param>
    /// <returns>The settings.</returns>
    private AppSettings SettingsWith(params string[] folders) =>
        _places.Settings.Current with { Styles = new StyleSettings { SharedKitFolders = folders, DefaultKitId = string.Empty } };

    /// <summary>
    ///     Makes settings with the given default kit and no shared folder.
    /// </summary>
    /// <param name="id">The id of the default kit.</param>
    /// <returns>The settings.</returns>
    private AppSettings SettingsWithDefault(string id) =>
        _places.Settings.Current with { Styles = new StyleSettings { SharedKitFolders = [], DefaultKitId = id } };
}
