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

using DemaConsulting.CanvasPilot.StyleSubsystem;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace DemaConsulting.CanvasPilot.Tests;

/// <summary>
///     Unit tests for <see cref="StyleKitEditorViewModel" /> and its rows over a real catalog of kits on temporary folders:
///     that the draft holds every part of a kit, that each wrong value gives the message of the reader of the files and
///     stops the save, that rows can be added and removed, and that the files of the examples are changed only by a save.
/// </summary>
public sealed class StyleKitEditorTests : IDisposable
{
    /// <summary>
    ///     The places that hold kits, with a project service and a settings service.
    /// </summary>
    private readonly StyleKitPlaces _places = new();

    /// <summary>
    ///     The real catalog over the places.
    /// </summary>
    private readonly StyleKitCatalog _catalog;

    /// <summary>
    ///     Initializes a new instance of the <see cref="StyleKitEditorTests" /> class.
    /// </summary>
    public StyleKitEditorTests()
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
        _places.Dispose();
    }

    /// <summary>
    ///     Test that the draft of a kit that has every part gives back that kit, is not changed, and has no problem.
    /// </summary>
    [Fact]
    public async Task StyleKitEditor_Open_KitWithEveryPart_DraftHoldsEveryFieldAndIsClean()
    {
        // Arrange: a kit with every part, an example with a caption and one without
        var entry = await CreateEntryAsync(FullKit(), "a.svg", "b.svg");

        // Act: open the editor
        var editor = new StyleKitEditorViewModel(_catalog, entry);

        // Assert: every field of the kit is in the draft and the draft builds the same kit
        Assert.Equal("team", editor.Id);
        Assert.Equal("Team", editor.Name);
        Assert.Equal("The look of the team.", editor.Description);
        Assert.Equal(["background", "text", "primary", "accent", "success"], editor.PaletteRows.Select(row => row.Role));
        Assert.Equal(["#FFFFFF", "#0F172A", "#2563EB", "#F59E0B", "#16A34A"], editor.PaletteRows.Select(row => row.Hex));
        Assert.Equal([true, true, true, false, false], editor.PaletteRows.Select(row => row.IsRequired));
        Assert.Equal("Arial, sans-serif", editor.HeadingFont);
        Assert.Equal(2m, editor.StrokeWidth);
        Assert.Equal(12m, editor.CornerRadius);
        Assert.Equal(16m, editor.Spacing);
        Assert.True(editor.UseMermaid);
        Assert.Equal("base", editor.MermaidTheme);
        Assert.Equal(["primaryColor", "lineColor"], editor.MermaidVariables.Select(row => row.Name));
        Assert.Contains("\"category\"", editor.VegaLiteConfigText, StringComparison.Ordinal);
        Assert.Equal("Keep it plain.", editor.Notes);
        Assert.Equal(["Use the palette"], editor.DoItems.Select(row => row.Text));
        Assert.Equal(["Use gradients"], editor.DontItems.Select(row => row.Text));
        Assert.Equal(["Slide", "Banner"], editor.Presets.Select(row => row.Name));
        Assert.Equal(["a.svg", "b.svg"], editor.Examples.Select(row => row.FileName));
        Assert.Equal(["The first example", string.Empty], editor.Examples.Select(row => row.Caption));
        Assert.Equal(StyleKitSerializer.Write(entry.Kit), StyleKitSerializer.Write(editor.BuildKit()));
        Assert.Empty(editor.Errors);
        Assert.Empty(editor.Warnings);
        Assert.True(editor.CanSave);
        Assert.False(editor.IsDirty);
        Assert.Contains("reads well", editor.ContrastText, StringComparison.Ordinal);
        Assert.Contains(entry.FolderPath, editor.LocationText, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Test that every field that is changed in the draft is written by a save and read back from the folder, and that
    ///     the editor reports the saved kit and asks to close.
    /// </summary>
    [Fact]
    public async Task StyleKitEditor_Save_EveryFieldChanged_IsWrittenAndReadBack()
    {
        // Arrange: an editor over a kit with every part
        var entry = await CreateEntryAsync(FullKit(), "a.svg");
        var editor = new StyleKitEditorViewModel(_catalog, entry);
        var closed = 0;
        editor.CloseRequested += (_, _) => closed++;

        // Act: change every part, then save
        editor.Name = "Team Two";
        editor.Description = "Changed.";
        editor.PaletteRows[2].Hex = "#112233";
        editor.AddColorCommand.Execute(null);
        editor.PaletteRows[^1].Hex = "#445566";
        editor.HeadingFont = "Georgia, serif";
        editor.BodyFont = "Arial, sans-serif";
        editor.MonospaceFont = "Consolas, monospace";
        editor.StrokeWidth = 3m;
        editor.CornerRadius = 4m;
        editor.Spacing = 20m;
        editor.MermaidTheme = "dark";
        editor.MermaidVariables[0].Value = "#112233";
        editor.AddMermaidVariableCommand.Execute(null);
        editor.MermaidVariables[^1].Name = "fontSize";
        editor.MermaidVariables[^1].Value = "16px";
        editor.VegaLiteConfigText = "{ \"axis\": { \"labelColor\": \"#0F172A\" } }";
        editor.Notes = "New notes.";
        editor.AddDoCommand.Execute(null);
        editor.DoItems[^1].Text = "Keep margins";
        editor.RemoveGuidanceItemCommand.Execute(editor.DontItems[0]);
        editor.AddPresetCommand.Execute(null);
        editor.Presets[^1].Name = "Square";
        editor.Presets[^1].WidthText = "512";
        editor.Presets[^1].HeightText = "512";
        editor.Presets[^1].DpiText = "300";
        editor.Presets[^1].Transparent = true;
        editor.Examples[0].Caption = "Renamed caption";
        editor.SaveCommand.Execute(null);

        // Assert: the editor saved and closed, and the folder holds the new kit
        var read = StyleKitSerializer.ReadFolder(entry.FolderPath);
        var kit = read.Kit!;
        Assert.True(read.Succeeded);
        Assert.True(editor.Saved);
        Assert.Equal(1, closed);
        Assert.Equal("team", editor.SavedEntry!.Kit.Id);
        Assert.False(editor.HasSaveError);
        Assert.Equal("Team Two", kit.Name);
        Assert.Equal("Changed.", kit.Description);
        Assert.Equal("#112233", kit.Palette["primary"]);
        Assert.Equal("#445566", kit.Palette["secondary"]);
        Assert.Equal(new StyleFonts("Georgia, serif", "Arial, sans-serif", "Consolas, monospace"), kit.Fonts);
        Assert.Equal([3d, 4d, 20d], [kit.StrokeWidth, kit.CornerRadius, kit.Spacing]);
        Assert.Equal("dark", kit.Mermaid!.Theme);
        Assert.Equal("#112233", kit.Mermaid.ThemeVariables["primaryColor"]);
        Assert.Equal("16px", kit.Mermaid.ThemeVariables["fontSize"]);
        Assert.Equal("{\"axis\":{\"labelColor\":\"#0F172A\"}}", kit.VegaLiteConfigJson);
        Assert.Equal("New notes.", kit.Guidance.Notes);
        Assert.Equal(["Use the palette", "Keep margins"], kit.Guidance.Do);
        Assert.Empty(kit.Guidance.Dont);
        Assert.Equal("Square", kit.SizePresets[^1].Name);
        Assert.Equal(new StyleSizePreset("Square", 512, 512, 300, true), kit.SizePresets[^1]);
        Assert.Equal("Renamed caption", kit.Examples.Single().Caption);
    }

    /// <summary>
    ///     Test that each wrong value gives the message that the reader of the files gives for it, names the property, and
    ///     stops the save until it is fixed.
    /// </summary>
    [Fact]
    public async Task StyleKitEditor_Validate_WrongValues_GiveTheMessagesOfTheReaderAndStopTheSave()
    {
        // Arrange: a valid kit that is open in the editor
        var editor = new StyleKitEditorViewModel(_catalog, await CreateEntryAsync(FullKit()));

        // Act and assert: a color that is not a color
        editor.PaletteRows[2].Hex = "#12";
        Assert.Contains("palette.primary: '#12' is not a color; use #RRGGBB.", editor.Errors);
        Assert.False(editor.CanSave);
        Assert.False(editor.SaveCommand.CanExecute(null));
        Assert.False(editor.PaletteRows[2].IsHexValid);
        Assert.Equal("'#12' is not a color; use #RRGGBB.", editor.PaletteRows[2].HexError);
        editor.PaletteRows[2].Hex = "#2563EB";
        Assert.Empty(editor.Errors);
        Assert.True(editor.SaveCommand.CanExecute(null));

        // A name that is empty, a number out of range, and a font list that is too long
        editor.Name = "  ";
        editor.StrokeWidth = 40m;
        editor.BodyFont = new string('a', 130);
        Assert.Contains(editor.Errors, error => error.StartsWith("name:", StringComparison.Ordinal));
        Assert.Contains(editor.Errors, error => error.StartsWith("stroke.width:", StringComparison.Ordinal));
        Assert.Contains(editor.Errors, error => error.StartsWith("fonts.body:", StringComparison.Ordinal));
        editor.Name = "Team";
        editor.StrokeWidth = 2m;
        editor.BodyFont = "sans-serif";
        Assert.Empty(editor.Errors);

        // A Mermaid theme that does not exist and a Vega-Lite configuration that is not an object or not JSON
        editor.MermaidTheme = "neon";
        Assert.Contains(editor.Errors, error => error.StartsWith("mermaid.theme:", StringComparison.Ordinal));
        editor.MermaidTheme = "base";
        editor.VegaLiteConfigText = "[1, 2]";
        Assert.Contains(editor.Errors, error => error.Contains("must be a JSON object", StringComparison.Ordinal));
        editor.VegaLiteConfigText = "{ not json";
        Assert.Contains(editor.Errors, error => error.StartsWith("vegaLite.config: is not valid JSON", StringComparison.Ordinal));
        editor.VegaLiteConfigText = "{\"a\":\"" + new string('x', 9000) + "\"}";
        Assert.Contains(editor.Errors, error => error.Contains("limit", StringComparison.Ordinal) && error.StartsWith("vegaLite.config:", StringComparison.Ordinal));
        editor.VegaLiteConfigText = string.Empty;
        Assert.Empty(editor.Errors);
        Assert.Null(editor.BuildKit().VegaLiteConfigJson);

        // Too many lines of advice
        for (var i = 0; i < 11; i++)
        {
            editor.AddDoCommand.Execute(null);
            editor.DoItems[^1].Text = $"Rule {i}";
        }

        Assert.Contains(editor.Errors, error => error.StartsWith("guidance.do: has 12 entries", StringComparison.Ordinal));
    }

    /// <summary>
    ///     Test that a font that CanvasNet does not bundle is a warning that does not stop the save.
    /// </summary>
    [Fact]
    public async Task StyleKitEditor_Validate_FontThatIsNotBundled_IsAWarningAndKitCanBeSaved()
    {
        // Arrange: an editor over a valid kit
        var editor = new StyleKitEditorViewModel(_catalog, await CreateEntryAsync(FullKit()));

        // Act: name a font that is not bundled
        editor.HeadingFont = "Inter, sans-serif";

        // Assert: one warning that names the property, no error, and the kit can be saved
        Assert.Empty(editor.Errors);
        Assert.Single(editor.Warnings);
        Assert.StartsWith("fonts.heading: 'Inter'", editor.Warnings[0], StringComparison.Ordinal);
        Assert.True(editor.HasWarnings);
        Assert.True(editor.CanSave);
    }

    /// <summary>
    ///     Test the palette: the next usual role is offered, a required color cannot be removed, an extra color can, two
    ///     colors with one name and a color with no name are errors, and the sliders and the text agree.
    /// </summary>
    [Fact]
    public async Task StyleKitEditor_Palette_AddRemoveRenameAndSliders_FollowTheRules()
    {
        // Arrange: an editor over a kit whose palette has the three required roles, an accent, and a success color
        var editor = new StyleKitEditorViewModel(_catalog, await CreateEntryAsync(FullKit()));

        // Act: add colors until the usual roles are used and a numbered name is needed
        editor.AddColorCommand.Execute(null);
        editor.AddColorCommand.Execute(null);
        editor.AddColorCommand.Execute(null);
        editor.AddColorCommand.Execute(null);
        var added = editor.PaletteRows.Skip(5).Select(row => row.Role).ToArray();
        editor.RemoveColorCommand.Execute(editor.PaletteRows[0]);
        editor.RemoveColorCommand.Execute(editor.PaletteRows[^1]);

        // Assert: roles are taken in order, a required color stays, and an extra color goes
        Assert.Equal(["secondary", "surface", "muted", "color-1"], added);
        Assert.Equal("background", editor.PaletteRows[0].Role);
        Assert.False(editor.PaletteRows[0].IsRoleEditable);
        Assert.True(editor.PaletteRows[^1].IsRoleEditable);
        Assert.DoesNotContain(editor.PaletteRows, row => row.Role == "color-1");
        Assert.Empty(editor.Errors);

        // Act: two colors with one name, and a color with no name
        editor.PaletteRows[^1].Role = "accent";
        Assert.Contains("palette.accent: is used for more than one color; every color needs its own name.", editor.Errors);
        editor.PaletteRows[^1].Role = " ";
        Assert.Contains("palette: a color has no name; give every color a name.", editor.Errors);
        editor.PaletteRows[^1].Role = "brand";
        Assert.Empty(editor.Errors);

        // Act: the sliders write the text and the text sets the sliders
        var row = editor.PaletteRows[^1];
        row.Hex = "#102030";
        var channels = (row.Red, row.Green, row.Blue);
        row.Red = 255;
        row.Green = 300.4;
        row.Blue = -3;
        var afterSliders = row.Hex;
        row.Hex = "nonsense";
        var invalid = (row.Red, row.Green, row.Blue, row.HexError.Length > 0);

        // Assert
        Assert.Equal((16d, 32d, 48d), channels);
        Assert.Equal("#FFFF00", afterSliders);
        Assert.Equal((0d, 0d, 0d, true), invalid);
    }

    /// <summary>
    ///     Test that low contrast between the text and the background is said.
    /// </summary>
    [Fact]
    public async Task StyleKitEditor_ContrastText_LowAndHighContrast_AreSaid()
    {
        // Arrange: an editor over a kit that reads well
        var editor = new StyleKitEditorViewModel(_catalog, await CreateEntryAsync(FullKit()));

        // Act: make the text nearly the color of the background, then not a color
        editor.PaletteRows[1].Hex = "#EEEEEE";
        var low = editor.ContrastText;
        editor.PaletteRows[1].Hex = "oops";
        var none = editor.ContrastText;

        // Assert
        Assert.Contains("which is low", low, StringComparison.Ordinal);
        Assert.Equal(string.Empty, none);
    }

    /// <summary>
    ///     Test the Mermaid part: it can be switched off, variables can be added and removed, a blank line is left out, and a
    ///     variable that is named twice is an error.
    /// </summary>
    [Fact]
    public async Task StyleKitEditor_Mermaid_SwitchedOffAndVariableRows_FollowTheRules()
    {
        // Arrange: an editor over a kit with a Mermaid theme
        var editor = new StyleKitEditorViewModel(_catalog, await CreateEntryAsync(FullKit()));

        // Act: add a blank row, then one with a value and no name, then a duplicate, and switch the part off
        editor.AddMermaidVariableCommand.Execute(null);
        var withBlank = editor.BuildKit().Mermaid!.ThemeVariables.Count;
        var noError = editor.Errors.Count;
        editor.MermaidVariables[^1].Value = "red";
        var valueOnly = editor.Errors.ToArray();
        editor.MermaidVariables[^1].Name = "lineColor";
        var duplicate = editor.Errors.ToArray();
        editor.RemoveMermaidVariableCommand.Execute(editor.MermaidVariables[^1]);
        editor.UseMermaid = false;

        // Assert
        Assert.Equal(2, withBlank);
        Assert.Equal(0, noError);
        Assert.Equal(["mermaid.themeVariables: a variable has a value but no name."], valueOnly);
        Assert.Equal(["mermaid.themeVariables.lineColor: is set more than once."], duplicate);
        Assert.Null(editor.BuildKit().Mermaid);
        Assert.Empty(editor.Errors);
    }

    /// <summary>
    ///     Test the lists of advice: blank lines are left out of the kit.
    /// </summary>
    [Fact]
    public async Task StyleKitEditor_Guidance_BlankLines_AreLeftOut()
    {
        // Arrange: an editor over a kit with advice
        var editor = new StyleKitEditorViewModel(_catalog, await CreateEntryAsync(FullKit()));

        // Act: add a blank line to each list and read the kit
        editor.AddDoCommand.Execute(null);
        editor.AddDontCommand.Execute(null);
        var kit = editor.BuildKit();

        // Assert: the lines are in the draft and not in the kit, and are no error
        Assert.Equal(2, editor.DoItems.Count);
        Assert.Equal(2, editor.DontItems.Count);
        Assert.Single(kit.Guidance.Do);
        Assert.Single(kit.Guidance.Dont);
        Assert.Empty(editor.Errors);
    }

    /// <summary>
    ///     Test the export sizes: a row can be added and removed, a number that is not a number is said once where it is, and
    ///     a size with no side, two sizes with one name, and a side out of range are errors.
    /// </summary>
    [Fact]
    public async Task StyleKitEditor_Presets_AddRemoveAndWrongNumbers_GiveMessages()
    {
        // Arrange: an editor over a kit with two sizes
        var editor = new StyleKitEditorViewModel(_catalog, await CreateEntryAsync(FullKit()));

        // Act and assert: a new size has a free name and a usable size
        editor.AddPresetCommand.Execute(null);
        editor.AddPresetCommand.Execute(null);
        Assert.Equal(["Slide", "Banner", "New size", "New size 2"], editor.Presets.Select(row => row.Name));
        Assert.Empty(editor.Errors);
        editor.RemovePresetCommand.Execute(editor.Presets[3]);
        editor.RemovePresetCommand.Execute(null);
        Assert.Equal(3, editor.Presets.Count);

        // A width that is not a number is said once, with the property
        editor.Presets[2].WidthText = "abc";
        editor.Presets[2].DpiText = "fast";
        Assert.Contains("sizePresets[2].width: 'abc' is not a whole number of pixels.", editor.Errors);
        Assert.Contains("sizePresets[2].dpi: 'fast' is not a number.", editor.Errors);

        // A size with no side, a side that is too large, a name that is used twice, and a resolution that is out of range
        editor.Presets[2].WidthText = string.Empty;
        editor.Presets[2].HeightText = string.Empty;
        editor.Presets[2].DpiText = "5";
        Assert.Contains("sizePresets[2]: needs a width, a height, or both.", editor.Errors);
        Assert.Contains(editor.Errors, error => error.StartsWith("sizePresets[2].dpi:", StringComparison.Ordinal));
        editor.Presets[2].WidthText = "9000";
        editor.Presets[2].DpiText = string.Empty;
        editor.Presets[2].Name = "slide";
        Assert.Contains(editor.Errors, error => error.StartsWith("sizePresets[2].width:", StringComparison.Ordinal));
        Assert.Contains(editor.Errors, error => error.StartsWith("sizePresets[2].name: 'slide' is used by another", StringComparison.Ordinal));
        editor.Presets[2].Name = "Wide";
        editor.Presets[2].WidthText = "1000";
        Assert.Empty(editor.Errors);
        Assert.Equal(new StyleSizePreset("Wide", 1000, null, null, false), editor.BuildKit().SizePresets[2]);
    }

    /// <summary>
    ///     Test that the files of the examples are changed only by a save: a file that is added is copied, a file that is
    ///     removed is deleted, and the kit file names what is left with its captions.
    /// </summary>
    [Fact]
    public async Task StyleKitEditor_Examples_AddAndRemove_ChangeTheFilesOnlyWhenSaved()
    {
        // Arrange: a kit with two examples and a file elsewhere that the user chooses
        var entry = await CreateEntryAsync(FullKit(), "a.svg", "b.svg");
        var examples = Path.Combine(entry.FolderPath, "examples");
        var outside = Path.Combine(_places.Root, "outside");
        Directory.CreateDirectory(outside);
        var chosen = Path.Combine(outside, "c.svg");
        await File.WriteAllTextAsync(chosen, "third", Ct);
        var editor = new StyleKitEditorViewModel(_catalog, entry);

        // Act: add one example, remove another, and caption the new one; nothing is saved yet
        var added = editor.AddExampleFiles([chosen]);
        editor.RemoveExampleCommand.Execute(editor.Examples[1]);
        editor.Examples[^1].Caption = "Third";
        var before = Directory.EnumerateFiles(examples).Select(file => Path.GetFileName(file)).Order().ToArray();
        var dirtyBefore = editor.IsDirty;
        editor.SaveCommand.Execute(null);
        var after = Directory.EnumerateFiles(examples).Select(file => Path.GetFileName(file)).Order().ToArray();
        var read = StyleKitSerializer.ReadFolder(entry.FolderPath).Kit!;

        // Assert
        Assert.Equal(1, added);
        Assert.Equal(["a.svg", "b.svg"], before);
        Assert.True(dirtyBefore);
        Assert.Equal(["a.svg", "c.svg"], after);
        Assert.Equal("third", (await File.ReadAllTextAsync(Path.Combine(examples, "c.svg"), Ct)));
        Assert.Equal(["examples/a.svg", "examples/c.svg"], read.Examples.Select(example => example.RelativePath));
        Assert.Equal(["The first example", "Third"], read.Examples.Select(example => example.Caption));
        Assert.True(File.Exists(chosen));
    }

    /// <summary>
    ///     Test that canceling leaves every file as it was, even after examples were added and removed and values changed.
    /// </summary>
    [Fact]
    public async Task StyleKitEditor_Cancel_AfterEdits_LeavesTheDiskUntouched()
    {
        // Arrange: a kit with an example, and a snapshot of its folder
        var entry = await CreateEntryAsync(FullKit(), "a.svg");
        var chosen = Path.Combine(_places.Root, "new.png");
        await File.WriteAllTextAsync(chosen, "png", Ct);
        var editor = new StyleKitEditorViewModel(_catalog, entry);
        var closed = 0;
        editor.CloseRequested += (_, _) => closed++;
        var snapshot = Snapshot(entry.FolderPath);

        // Act: edit, add, remove, and cancel
        editor.Name = "Changed";
        editor.AddExampleFiles([chosen]);
        editor.RemoveExampleCommand.Execute(editor.Examples[0]);
        editor.CancelCommand.Execute(null);

        // Assert: the folder is the same, the editor says it did not save and asked to close
        Assert.Equal(snapshot, Snapshot(entry.FolderPath));
        Assert.False(editor.Saved);
        Assert.Null(editor.SavedEntry);
        Assert.Equal(1, closed);
    }

    /// <summary>
    ///     Test that a file that has the name of an example that stays gets a number, also when it is chosen twice, and that
    ///     the file that stays is not overwritten.
    /// </summary>
    [Fact]
    public async Task StyleKitEditor_Examples_NameCollisions_GetANumberAndNothingIsOverwritten()
    {
        // Arrange: a kit with the example a.svg and two files elsewhere with that name and different text
        var entry = await CreateEntryAsync(FullKit(), "a.svg");
        var first = Path.Combine(_places.Root, "one", "a.svg");
        var second = Path.Combine(_places.Root, "two", "a.svg");
        Directory.CreateDirectory(Path.GetDirectoryName(first)!);
        Directory.CreateDirectory(Path.GetDirectoryName(second)!);
        await File.WriteAllTextAsync(first, "first", Ct);
        await File.WriteAllTextAsync(second, "second", Ct);
        var editor = new StyleKitEditorViewModel(_catalog, entry);

        // Act: choose both and save
        editor.AddExampleFiles([first, second]);
        var names = editor.Examples.Select(row => row.FileName).ToArray();
        editor.SaveCommand.Execute(null);
        var examples = Path.Combine(entry.FolderPath, "examples");

        // Assert: the draft shows the numbers, and the folder has all three files with their own text
        Assert.Equal(["a.svg", "a-2.svg", "a-3.svg"], names);
        Assert.Equal("a.svg", (await File.ReadAllTextAsync(Path.Combine(examples, "a.svg"), Ct)));
        Assert.Equal("first", (await File.ReadAllTextAsync(Path.Combine(examples, "a-2.svg"), Ct)));
        Assert.Equal("second", (await File.ReadAllTextAsync(Path.Combine(examples, "a-3.svg"), Ct)));
    }

    /// <summary>
    ///     Test that a file that is not there and a file over the limit are not added and the reason is given, and that the
    ///     kit that is not changed in the examples keeps its example files when saved.
    /// </summary>
    [Fact]
    public async Task StyleKitEditor_Examples_MissingAndTooLargeFiles_AreNotAddedAndSaveKeepsTheRest()
    {
        // Arrange: a kit with an example, a file that is not there, and a file one byte over the limit
        var entry = await CreateEntryAsync(FullKit(), "a.svg");
        var big = Path.Combine(_places.Root, "big.png");
        using (var stream = new FileStream(big, FileMode.Create))
        {
            stream.SetLength(StyleKitSerializer.MaxExampleBytes + 1);
        }

        var editor = new StyleKitEditorViewModel(_catalog, entry);

        // Act: choose both, then change a value that is not an example and save
        var added = editor.AddExampleFiles([Path.Combine(_places.Root, "nothing.svg"), big]);
        var message = editor.ExampleMessage;
        editor.Name = "Kept";
        editor.SaveCommand.Execute(null);

        // Assert: nothing was added, both reasons are said, and the example file of the kit is still there
        Assert.Equal(0, added);
        Assert.Contains("'nothing.svg' was not found.", message, StringComparison.Ordinal);
        Assert.Contains("'big.png' is larger than 16 MiB", message, StringComparison.Ordinal);
        Assert.True(editor.HasExampleMessage);
        Assert.Equal("a.svg", (await File.ReadAllTextAsync(Path.Combine(entry.FolderPath, "examples", "a.svg"), Ct)));
        Assert.Equal("Kept", StyleKitSerializer.ReadFolder(entry.FolderPath).Kit!.Name);
    }

    /// <summary>
    ///     Test that the command that adds example files asks the picker that the window supplies, and does nothing without
    ///     one.
    /// </summary>
    [Fact]
    public async Task StyleKitEditor_AddExamplesCommand_PickerSuppliedOrMissing_AddsOrDoesNothing()
    {
        // Arrange: a file to choose and an editor with no picker
        var entry = await CreateEntryAsync(FullKit());
        var chosen = Path.Combine(_places.Root, "pick.svg");
        await File.WriteAllTextAsync(chosen, "x", Ct);
        var editor = new StyleKitEditorViewModel(_catalog, entry);

        // Act: run the command with no picker, with a picker that cancels, and with one that chooses a file
        await editor.AddExamplesCommand.ExecuteAsync(null);
        var withoutPicker = editor.Examples.Count;
        editor.PickExampleFiles = _ => Task.FromResult<IReadOnlyList<string>>([]);
        await editor.AddExamplesCommand.ExecuteAsync(null);
        var cancelled = editor.Examples.Count;
        editor.PickExampleFiles = _ => Task.FromResult<IReadOnlyList<string>>([chosen]);
        await editor.AddExamplesCommand.ExecuteAsync(null);

        // Assert
        Assert.Equal(0, withoutPicker);
        Assert.Equal(0, cancelled);
        Assert.Equal(["pick.svg"], editor.Examples.Select(row => row.FileName));
        Assert.True(editor.Examples[0].IsNew);
        Assert.Equal("pick.svg (new)", editor.Examples[0].DisplayText);
    }

    /// <summary>
    ///     Test that the editor tells whether the draft differs from the kit, and that changing a value back is not a change.
    /// </summary>
    [Fact]
    public async Task StyleKitEditor_IsDirty_ChangeAndChangeBack_FollowsTheDraft()
    {
        // Arrange: an editor over a kit
        var editor = new StyleKitEditorViewModel(_catalog, await CreateEntryAsync(FullKit()));

        // Act and assert
        Assert.False(editor.IsDirty);
        editor.Name = "Other";
        Assert.True(editor.IsDirty);
        editor.Name = "Team";
        Assert.False(editor.IsDirty);
        editor.VegaLiteConfigText = "{ broken";
        Assert.True(editor.IsDirty);
        editor.AddDoCommand.Execute(null);
        Assert.True(editor.IsDirty);
    }

    /// <summary>
    ///     Test that a failure of the catalog is shown, keeps the editor open, and takes away the example files that the save
    ///     had copied.
    /// </summary>
    [Fact]
    public async Task StyleKitEditor_Save_CatalogFails_ShowsTheReasonAndRemovesTheCopies()
    {
        // Arrange: an editor over a catalog that cannot write, and an example to copy
        var entry = await CreateEntryAsync(FullKit(), "a.svg");
        var failing = Substitute.For<IStyleKitCatalog>();
        failing.Save(Arg.Any<StyleKit>(), Arg.Any<StyleKitLocation>()).Throws(new IOException("The disk is full."));
        var chosen = Path.Combine(_places.Root, "new.svg");
        await File.WriteAllTextAsync(chosen, "new", Ct);
        var editor = new StyleKitEditorViewModel(failing, entry);
        editor.AddExampleFiles([chosen]);
        var closed = 0;
        editor.CloseRequested += (_, _) => closed++;

        // Act
        editor.SaveCommand.Execute(null);

        // Assert: the reason is shown, the copy is gone, and the editor stays
        Assert.Equal("The kit could not be saved: The disk is full.", editor.SaveError);
        Assert.True(editor.HasSaveError);
        Assert.False(editor.Saved);
        Assert.Equal(0, closed);
        Assert.Equal(["a.svg"], Directory.EnumerateFiles(Path.Combine(entry.FolderPath, "examples")).Select(file => Path.GetFileName(file)));
    }

    /// <summary>
    ///     Test that a kit that is read only cannot be opened in the editor and that missing arguments are refused.
    /// </summary>
    [Fact]
    public async Task StyleKitEditor_Constructor_ReadOnlyKitAndNullArguments_AreRefused()
    {
        // Arrange: a built-in kit and a kit of the user
        StyleKitPlaces.WriteKit(_places.BuiltIn, StyleKitPlaces.Kit("clean"));
        await _catalog.RefreshAsync(Ct);
        var builtIn = _catalog.Resolve("clean")!;
        var own = await CreateEntryAsync(FullKit());

        // Act and assert
        var exception = Assert.Throws<InvalidOperationException>(() => new StyleKitEditorViewModel(_catalog, builtIn));
        Assert.Contains("read only", exception.Message, StringComparison.Ordinal);
        Assert.Throws<ArgumentNullException>(() => new StyleKitEditorViewModel(null!, own));
        Assert.Throws<ArgumentNullException>(() => new StyleKitEditorViewModel(_catalog, null!));
        Assert.Throws<ArgumentNullException>(() => new StyleColorRowViewModel(null!, "#000000", false));
        Assert.Throws<ArgumentNullException>(() => new StyleTextRowViewModel(null!));
        Assert.Throws<ArgumentNullException>(() => new StyleNameValueRowViewModel("a", null!));
        Assert.Throws<ArgumentNullException>(() => new StyleExampleRowViewModel("a", "b", null!, false));
        Assert.Throws<ArgumentNullException>(() => new StylePresetRowViewModel(null!, null, null, null, false));
        Assert.Throws<ArgumentNullException>(() => new StylePresetRowViewModel("a", 1, 1, null, false).ToPreset(null!));
        Assert.Throws<ArgumentNullException>(() => new StyleKitEditorViewModel(_catalog, own).AddExampleFiles(null!));
    }

    /// <summary>
    ///     Makes a kit with every part: five colors, three font lists, a Mermaid theme with two variables, a Vega-Lite
    ///     configuration, advice, two sizes, and an example with a caption.
    /// </summary>
    /// <returns>The kit.</returns>
    private static StyleKit FullKit() =>
        new(
            "team",
            "Team",
            "The look of the team.",
            new Dictionary<string, string>
            {
                ["background"] = "#FFFFFF",
                ["text"] = "#0F172A",
                ["primary"] = "#2563EB",
                ["accent"] = "#F59E0B",
                ["success"] = "#16A34A"
            },
            new StyleFonts("Arial, sans-serif", "sans-serif", "monospace"),
            2,
            12,
            16,
            new StyleMermaid("base", new Dictionary<string, string> { ["primaryColor"] = "#2563EB", ["lineColor"] = "#475569" }),
            "{\"range\":{\"category\":[\"#2563EB\",\"#F59E0B\"]}}",
            new StyleGuidanceText("Keep it plain.", ["Use the palette"], ["Use gradients"]),
            [new StyleSizePreset("Slide", 1920, 1080, null, false), new StyleSizePreset("Banner", 1600, null, 150, true)],
            [new StyleExample("examples/a.svg", string.Empty, "The first example")]);

    /// <summary>
    ///     Writes a kit into the folder of the user with example files and finds it in the catalog.
    /// </summary>
    /// <param name="kit">The kit.</param>
    /// <param name="examples">The names of the example files; each holds its own name as text.</param>
    /// <returns>The entry of the kit.</returns>
    private async Task<StyleKitEntry> CreateEntryAsync(StyleKit kit, params string[] examples)
    {
        StyleKitPlaces.WriteKit(_places.User, kit, examples);
        await _catalog.RefreshAsync(Ct);
        return _catalog.Resolve(kit.Id)!;
    }

    /// <summary>
    ///     Lists every file of a folder with its text, so that two states of a folder can be compared.
    /// </summary>
    /// <param name="folder">The folder.</param>
    /// <returns>The relative path and the text of each file.</returns>
    private static string[] Snapshot(string folder)
    {
        return Directory.EnumerateFiles(folder, "*", SearchOption.AllDirectories)
            .Order(StringComparer.Ordinal)
            .Select(file => $"{Path.GetRelativePath(folder, file)}={File.ReadAllText(file)}")
            .ToArray();
    }
}
