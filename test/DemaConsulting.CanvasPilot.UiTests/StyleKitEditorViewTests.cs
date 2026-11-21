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

using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using DemaConsulting.CanvasPilot.ProjectSubsystem;
using DemaConsulting.CanvasPilot.SettingsSubsystem;
using DemaConsulting.CanvasPilot.StyleSubsystem;

namespace DemaConsulting.CanvasPilot.UiTests;

/// <summary>
///     Headless UI tests for the editor of a style kit: the tabs that show every part of a kit, the Save button that is off
///     while the draft has an error, the buttons that add and remove rows, the example files, and Cancel, over a real catalog
///     of kits on temporary folders.
/// </summary>
public sealed class StyleKitEditorViewTests : IDisposable
{
    /// <summary>
    ///     Temporary root removed on disposal.
    /// </summary>
    private readonly string _root = Directory.CreateTempSubdirectory("canvaspilot-ui-style-editor-").FullName;

    /// <summary>
    ///     The real project service, which the catalog follows.
    /// </summary>
    private readonly ProjectService _projects = new();

    /// <summary>
    ///     The catalog of kits, over a folder of the user inside the temporary root.
    /// </summary>
    private readonly StyleKitCatalog _catalog;

    /// <summary>
    ///     Initializes a new instance of the <see cref="StyleKitEditorViewTests" /> class.
    /// </summary>
    public StyleKitEditorViewTests()
    {
        var settings = new SettingsService(Path.Combine(_root, "settings"));
        settings.Load();
        _catalog = new StyleKitCatalog(_projects, settings, userFolder: Path.Combine(_root, "styles"));
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _catalog.Dispose();
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
    ///     Test that the window has a tab for each part of a kit, that the first tab shows the name and the id, and that the
    ///     palette and the fonts and numbers are shown on their tabs.
    /// </summary>
    [AvaloniaFact]
    public void StyleKitEditorView_Show_TabsShowEveryPartOfTheKit()
    {
        // Arrange: a new kit in the editor
        var (window, editor) = ShowEditor();

        // Act: read the first tab, then the palette, and the fonts and numbers
        var tabs = SettingsUiHelpers.Find<TabControl>(window, "StyleEditorTabs");
        var headers = tabs.Items.OfType<TabItem>().Select(tab => tab.Header?.ToString() ?? string.Empty).ToArray();
        var name = SettingsUiHelpers.Find<TextBox>(window, "StyleEditorNameBox").Text;
        var id = SettingsUiHelpers.Find<TextBlock>(window, "StyleEditorIdText").Text;
        Select(window, 1);
        var palette = SettingsUiHelpers.Find<ItemsControl>(window, "StyleEditorPalette");
        var hexes = window.GetVisualDescendants().OfType<TextBox>().Where(box => AutomationProperties.GetAutomationId(box) == "ColorEditorHexBox").Select(box => box.Text ?? string.Empty).ToArray();
        var roleBoxes = window.GetVisualDescendants().OfType<TextBox>().Where(box => AutomationProperties.GetAutomationId(box) == "ColorEditorRoleBox").ToArray();
        var contrast = SettingsUiHelpers.Find<TextBlock>(window, "StyleEditorContrast").Text;
        Select(window, 2);
        var heading = SettingsUiHelpers.Find<TextBox>(window, "StyleEditorHeadingFont").Text;
        var stroke = SettingsUiHelpers.Find<NumericUpDown>(window, "StyleEditorStrokeBox").Value;
        var radius = SettingsUiHelpers.Find<NumericUpDown>(window, "StyleEditorRadiusBox").Value;

        // Assert
        Assert.Equal(["General", "Palette", "Type and shape", "Mermaid and Vega-Lite", "Guidance", "Sizes", "Examples"], headers);
        Assert.Equal("Poster", name);
        Assert.Equal("poster", id);
        Assert.Equal(3, palette.ItemCount);
        Assert.Equal(["#FFFFFF", "#0F172A", "#2563EB"], hexes);
        Assert.All(roleBoxes, box => Assert.False(box.IsEnabled));
        Assert.Contains("reads well", contrast, StringComparison.Ordinal);
        Assert.Equal("sans-serif", heading);
        Assert.Equal(2m, stroke);
        Assert.Equal(8m, radius);
        Assert.False(editor.IsDirty);

        window.Close();
    }

    /// <summary>
    ///     Test that Save is off while a color is not a color, that the error is shown, and that Save is on again when the
    ///     color is fixed.
    /// </summary>
    [AvaloniaFact]
    public void StyleKitEditorView_Save_BadColorThenFixed_IsDisabledThenEnabled()
    {
        // Arrange: an editor on the palette tab
        var (window, editor) = ShowEditor();
        Select(window, 1);
        var save = SettingsUiHelpers.Find<Button>(window, "StyleEditorSaveButton");
        var primary = HexBoxes(window)[2];

        // Act: type something that is not a color, then fix it
        primary.Text = "#12";
        Dispatcher.UIThread.RunJobs();
        var errors = SettingsUiHelpers.Find<ItemsControl>(window, "StyleEditorErrors");
        var rowError = window.GetVisualDescendants().OfType<TextBlock>().Where(block => AutomationProperties.GetAutomationId(block) == "ColorEditorHexError").ElementAt(2);
        var whileBad = (SaveEnabled: save.IsEffectivelyEnabled, ErrorsVisible: errors.IsVisible, ErrorCount: errors.ItemCount, RowErrorVisible: rowError.IsVisible, RowErrorText: rowError.Text);
        primary.Text = "#112233";
        Dispatcher.UIThread.RunJobs();
        var afterFix = (SaveEnabled: save.IsEffectivelyEnabled, ErrorsVisible: errors.IsVisible);

        // Assert
        Assert.False(whileBad.SaveEnabled);
        Assert.True(whileBad.ErrorsVisible);
        Assert.Equal(1, whileBad.ErrorCount);
        Assert.True(whileBad.RowErrorVisible);
        Assert.Equal("'#12' is not a color; use #RRGGBB.", whileBad.RowErrorText);
        Assert.True(afterFix.SaveEnabled);
        Assert.False(afterFix.ErrorsVisible);
        Assert.Equal("#112233", editor.PaletteRows[2].Hex);

        window.Close();
    }

    /// <summary>
    ///     Test that clicking Save writes the changed name to the kit folder and closes the window, and that clicking Cancel
    ///     closes the window and writes nothing.
    /// </summary>
    [AvaloniaFact]
    public void StyleKitEditorView_SaveAndCancelButtons_WriteOrLeaveTheKitAndClose()
    {
        // Arrange: two editors over two kits
        var (saveWindow, saveEditor) = ShowEditor("First");
        var (cancelWindow, cancelEditor) = ShowEditor("Second");
        var saveClosed = false;
        var cancelClosed = false;
        saveWindow.Closed += (_, _) => saveClosed = true;
        cancelWindow.Closed += (_, _) => cancelClosed = true;

        // Act: rename each kit, save the first, and cancel the second
        SettingsUiHelpers.Find<TextBox>(saveWindow, "StyleEditorNameBox").Text = "First renamed";
        SettingsUiHelpers.Find<TextBox>(cancelWindow, "StyleEditorNameBox").Text = "Second renamed";
        Dispatcher.UIThread.RunJobs();
        SettingsUiHelpers.Click(saveWindow, SettingsUiHelpers.Find<Button>(saveWindow, "StyleEditorSaveButton"));
        SettingsUiHelpers.Click(cancelWindow, SettingsUiHelpers.Find<Button>(cancelWindow, "StyleEditorCancelButton"));

        // Assert
        Assert.True(saveClosed);
        Assert.True(cancelClosed);
        Assert.True(saveEditor.Saved);
        Assert.False(cancelEditor.Saved);
        Assert.Equal("First renamed", StyleKitSerializer.ReadFolder(Path.Combine(_root, "styles", "first")).Kit!.Name);
        Assert.Equal("Second", StyleKitSerializer.ReadFolder(Path.Combine(_root, "styles", "second")).Kit!.Name);
    }

    /// <summary>
    ///     Test that Add size adds a row of an export size with its boxes, that its Remove button removes it, and that a
    ///     width that is not a number shows an error.
    /// </summary>
    [AvaloniaFact]
    public void StyleKitEditorView_Sizes_AddAndRemoveRow_ChangeThePresets()
    {
        // Arrange: an editor on the Sizes tab
        var (window, editor) = ShowEditor();
        Select(window, 5);
        var presets = SettingsUiHelpers.Find<ItemsControl>(window, "StyleEditorPresets");

        // Act: add a size, read its boxes, type a width that is not a number, and remove the row
        SettingsUiHelpers.Click(window, SettingsUiHelpers.Find<Button>(window, "StyleEditorAddPresetButton"));
        Dispatcher.UIThread.RunJobs();
        var afterAdd = (Count: presets.ItemCount, Name: SettingsUiHelpers.Find<TextBox>(window, "StyleEditorPresetNameBox").Text, Width: SettingsUiHelpers.Find<TextBox>(window, "StyleEditorPresetWidthBox").Text);
        SettingsUiHelpers.Find<TextBox>(window, "StyleEditorPresetWidthBox").Text = "wide";
        Dispatcher.UIThread.RunJobs();
        var errorShown = SettingsUiHelpers.Find<ItemsControl>(window, "StyleEditorErrors").IsVisible;
        SettingsUiHelpers.Click(window, SettingsUiHelpers.Find<Button>(window, "StyleEditorRemoveRowButton"));
        Dispatcher.UIThread.RunJobs();

        // Assert
        Assert.Equal(1, afterAdd.Count);
        Assert.Equal("New size", afterAdd.Name);
        Assert.Equal("1280", afterAdd.Width);
        Assert.True(errorShown);
        Assert.Equal(0, presets.ItemCount);
        Assert.Empty(editor.Presets);
        Assert.False(editor.HasErrors);

        window.Close();
    }

    /// <summary>
    ///     Test the tabs of the Mermaid and Vega-Lite parts, of the advice, and of the examples: a variable and a line can be
    ///     added and removed, the theme list is shown only while the kit has a theme, and an example file that is chosen is
    ///     listed with a caption box and can be removed.
    /// </summary>
    [AvaloniaFact]
    public void StyleKitEditorView_MermaidGuidanceAndExamples_RowsAreAddedAndRemoved()
    {
        // Arrange: an editor and a file to choose as an example
        var (window, editor) = ShowEditor();
        var picture = Path.Combine(_root, "logo.svg");
        File.WriteAllText(picture, "<svg xmlns='http://www.w3.org/2000/svg'/>");
        editor.PickExampleFiles = _ => Task.FromResult<IReadOnlyList<string>>([picture]);

        // Act: the Mermaid tab, with the theme switched on and a variable added and removed
        Select(window, 3);
        var themeHiddenAtFirst = !SettingsUiHelpers.Find<CheckBox>(window, "StyleEditorUseMermaid").IsChecked.GetValueOrDefault();
        SettingsUiHelpers.Find<CheckBox>(window, "StyleEditorUseMermaid").IsChecked = true;
        Dispatcher.UIThread.RunJobs();
        var themeVisible = SettingsUiHelpers.Find<ComboBox>(window, "StyleEditorMermaidTheme").IsVisible;
        SettingsUiHelpers.Click(window, SettingsUiHelpers.Find<Button>(window, "StyleEditorAddMermaidVariableButton"));
        Dispatcher.UIThread.RunJobs();
        var variables = SettingsUiHelpers.Find<ItemsControl>(window, "StyleEditorMermaidVariables").ItemCount;
        SettingsUiHelpers.Click(window, SettingsUiHelpers.Find<Button>(window, "StyleEditorRemoveRowButton"));
        Dispatcher.UIThread.RunJobs();

        // Act: the advice tab, with a line added to each list and one removed
        Select(window, 4);
        SettingsUiHelpers.Click(window, SettingsUiHelpers.Find<Button>(window, "StyleEditorAddDoButton"));
        SettingsUiHelpers.Click(window, SettingsUiHelpers.Find<Button>(window, "StyleEditorAddDontButton"));
        Dispatcher.UIThread.RunJobs();
        var lines = (editor.DoItems.Count, editor.DontItems.Count);
        SettingsUiHelpers.Click(window, window.GetVisualDescendants().OfType<Button>().First(b => AutomationProperties.GetAutomationId(b) == "StyleEditorRemoveRowButton"));
        Dispatcher.UIThread.RunJobs();

        // Act: the examples tab, with a file added through the picker and then removed
        Select(window, 6);
        SettingsUiHelpers.Click(window, SettingsUiHelpers.Find<Button>(window, "StyleEditorAddExamplesButton"));
        Dispatcher.UIThread.RunJobs();
        var example = SettingsUiHelpers.Find<TextBlock>(window, "StyleEditorExampleName").Text;
        SettingsUiHelpers.Find<TextBox>(window, "StyleEditorExampleCaptionBox").Text = "The logo.";
        Dispatcher.UIThread.RunJobs();
        var caption = editor.Examples[0].Caption;
        SettingsUiHelpers.Click(window, SettingsUiHelpers.Find<Button>(window, "StyleEditorRemoveRowButton"));
        Dispatcher.UIThread.RunJobs();

        // Assert
        Assert.True(themeHiddenAtFirst);
        Assert.True(themeVisible);
        Assert.Equal(1, variables);
        Assert.Empty(editor.MermaidVariables);
        Assert.Equal((1, 1), lines);
        Assert.Equal(1, editor.DoItems.Count + editor.DontItems.Count);
        Assert.Equal("logo.svg (new)", example);
        Assert.Equal("The logo.", caption);
        Assert.Empty(editor.Examples);

        window.Close();
    }

    /// <summary>
    ///     Makes a new kit of the user, opens the editor on it, and shows the window.
    /// </summary>
    /// <param name="name">The name of the kit.</param>
    /// <returns>The window and its view model.</returns>
    private (StyleKitEditorView Window, StyleKitEditorViewModel Editor) ShowEditor(string name = "Poster")
    {
        var entry = _catalog.CreateBlank(name, StyleKitLocation.User);
        var editor = new StyleKitEditorViewModel(_catalog, entry);
        var window = new StyleKitEditorView(editor);
        window.Show();
        Dispatcher.UIThread.RunJobs();
        return (window, editor);
    }

    /// <summary>
    ///     Chooses a tab and lets the window lay out its content.
    /// </summary>
    /// <param name="window">The window.</param>
    /// <param name="index">The index of the tab.</param>
    private static void Select(Window window, int index)
    {
        SettingsUiHelpers.Find<TabControl>(window, "StyleEditorTabs").SelectedIndex = index;
        Dispatcher.UIThread.RunJobs();
    }

    /// <summary>
    ///     Finds the boxes that hold the colors of the palette, in order.
    /// </summary>
    /// <param name="window">The window.</param>
    /// <returns>The boxes.</returns>
    private static TextBox[] HexBoxes(Window window) =>
        window.GetVisualDescendants().OfType<TextBox>().Where(box => AutomationProperties.GetAutomationId(box) == "ColorEditorHexBox").ToArray();
}

/// <summary>
///     Headless UI tests for the control that edits one color: the swatch, the text, the error, and the sliders in the flyout
///     that stay in step with the text.
/// </summary>
public sealed class ColorEditorViewTests
{
    /// <summary>
    ///     Test that the swatch has the color of the text, follows the text, and that text that is not a color shows the
    ///     error and no color.
    /// </summary>
    [AvaloniaFact]
    public void ColorEditorView_Hex_SwatchFollowsTheTextAndErrorIsShown()
    {
        // Arrange: a control for a required color
        var row = new StyleColorRowViewModel("primary", "#2563EB", isRequired: true);
        var window = Show(row);

        // Act: read the swatch, change the text, and make it wrong
        var swatch = SettingsUiHelpers.Find<Border>(window, "ColorEditorSwatch");
        var first = ((ISolidColorBrush)swatch.Background!).Color;
        var hexBox = SettingsUiHelpers.Find<TextBox>(window, "ColorEditorHexBox");
        hexBox.Text = "#FF0000";
        Dispatcher.UIThread.RunJobs();
        var second = ((ISolidColorBrush)swatch.Background).Color;
        hexBox.Text = "red";
        Dispatcher.UIThread.RunJobs();
        var error = SettingsUiHelpers.Find<TextBlock>(window, "ColorEditorHexError");
        var third = swatch.Background;

        // Assert
        Assert.Equal(Color.Parse("#2563EB"), first);
        Assert.Equal(Colors.Red, second);
        Assert.True(error.IsVisible);
        Assert.Equal("'red' is not a color; use #RRGGBB.", error.Text);
        Assert.Equal(Brushes.Transparent, third);
        Assert.False(SettingsUiHelpers.Find<TextBox>(window, "ColorEditorRoleBox").IsEnabled);

        window.Close();
    }

    /// <summary>
    ///     Test that the sliders of the flyout show the channels of the text, that moving a slider changes the text, and that
    ///     changing the text moves the sliders.
    /// </summary>
    [AvaloniaFact]
    public void ColorEditorView_Sliders_StayInStepWithTheText()
    {
        // Arrange: a control for an extra color, with the flyout opened
        var row = new StyleColorRowViewModel("brand", "#102030", isRequired: false);
        var window = Show(row);
        var button = SettingsUiHelpers.Find<Button>(window, "ColorEditorSlidersButton");
        var flyout = (Flyout)button.Flyout!;
        flyout.ShowAt(button);
        Dispatcher.UIThread.RunJobs();
        var content = (Control)flyout.Content!;
        var sliders = content.GetVisualDescendants().OfType<Slider>().ToDictionary(slider => AutomationProperties.GetAutomationId(slider)!);

        // Act: read the sliders, move two of them, and change the text
        var initial = (sliders["ColorEditorRedSlider"].Value, sliders["ColorEditorGreenSlider"].Value, sliders["ColorEditorBlueSlider"].Value);
        sliders["ColorEditorRedSlider"].Value = 255;
        sliders["ColorEditorBlueSlider"].Value = 7;
        Dispatcher.UIThread.RunJobs();
        var afterSliders = row.Hex;
        SettingsUiHelpers.Find<TextBox>(window, "ColorEditorHexBox").Text = "#0A141E";
        Dispatcher.UIThread.RunJobs();
        var afterText = (sliders["ColorEditorRedSlider"].Value, sliders["ColorEditorGreenSlider"].Value, sliders["ColorEditorBlueSlider"].Value);

        // Assert
        Assert.Equal((16d, 32d, 48d), initial);
        Assert.Equal("#FF2007", afterSliders);
        Assert.Equal((10d, 20d, 30d), afterText);
        Assert.True(SettingsUiHelpers.Find<TextBox>(window, "ColorEditorRoleBox").IsEnabled);

        flyout.Hide();
        window.Close();
    }

    /// <summary>
    ///     Shows a color editor for a row in a window.
    /// </summary>
    /// <param name="row">The row to edit.</param>
    /// <returns>The window.</returns>
    private static Window Show(StyleColorRowViewModel row)
    {
        var window = new Window { Width = 520, Height = 160, Content = new ColorEditorView { DataContext = row } };
        window.Show();
        Dispatcher.UIThread.RunJobs();
        return window;
    }
}
