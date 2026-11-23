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
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
using DemaConsulting.CanvasPilot.AppShellSubsystem;
using DemaConsulting.CanvasPilot.StyleSubsystem;

namespace DemaConsulting.CanvasPilot.UiTests;

/// <summary>
///     Headless UI tests for the style picker in the title bar of the main window: the list is there only while a
///     project is open, drawn as a pill, shows the kits, and choosing a kit writes it into the project file.
/// </summary>
public sealed class StylePickerViewTests : IDisposable
{
    /// <summary>
    ///     Temporary root removed on disposal.
    /// </summary>
    private readonly string _root = Directory.CreateTempSubdirectory("canvaspilot-ui-style-picker-").FullName;

    /// <summary>
    ///     The compositions created by a test, disposed with the fixture so no watcher outlives it.
    /// </summary>
    private readonly List<ApplicationComposition> _compositions = [];

    /// <inheritdoc />
    public void Dispose()
    {
        foreach (var composition in _compositions)
        {
            composition.Dispose();
        }

        // The kits are read in the background; try again for a moment if a file is still in use
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
    ///     Test that the style list in the title bar is not shown without a project, and is shown with the label and the list
    ///     of the kits once a project is open.
    /// </summary>
    [AvaloniaFact]
    public async Task StylePickerView_TitleBar_ShownOnlyWithAProjectAndListsTheKits()
    {
        // Arrange: a shown main window with no project, whose kits have been read
        var (composition, window) = CreateWindow();
        window.Show();
        await composition.StyleCatalog.RefreshAsync(TestContext.Current.CancellationToken);
        AppShellUiHelpers.Flush();
        var host = SettingsUiHelpers.Find<ContentControl>(window, "StylePickerHost");
        var bar = SettingsUiHelpers.Find<Border>(window, "AppTitleBar");
        var hiddenWithoutProject = !host.GetVisualDescendants().OfType<ComboBox>().Any(box => box.IsEffectivelyVisible);

        // Act: open a project
        await OpenProjectAsync(composition);
        window.UpdateLayout();

        // Assert: the list appeared in the title bar, and it holds no style and every built-in kit
        var picker = SettingsUiHelpers.Find<ComboBox>(window, "StylePicker");
        var labels = picker.Items.OfType<StylePickerItem>().Select(item => item.Label).ToArray();
        Assert.True(hiddenWithoutProject);
        Assert.Contains(bar, picker.GetVisualAncestors());
        Assert.True(picker.IsEffectivelyVisible);
        Assert.Equal(StylePickerViewModel.NoStyleLabel, labels[0]);
        Assert.Contains("Clean (built-in)", labels);
        Assert.Contains("Slides Dark (built-in)", labels);
        Assert.Contains("DEMA (built-in)", labels);
        Assert.Equal(0, picker.SelectedIndex);
        Assert.Equal("Style kit", AutomationProperties.GetName(picker));

        window.Close();
    }

    /// <summary>
    ///     Test that the list is drawn as a pill 26 high that shows the name of the chosen kit without its place and one dot
    ///     per main color of the kit, an empty dashed dot for no style, and that the error text is not shown without an error.
    /// </summary>
    [AvaloniaFact]
    public async Task StylePickerView_Pill_ShowsNameAndSwatchesOfTheChosenKit()
    {
        // Arrange: a shown main window with a project open and the kits read
        var (composition, window) = CreateWindow();
        window.Show();
        await composition.StyleCatalog.RefreshAsync(TestContext.Current.CancellationToken);
        await OpenProjectAsync(composition);
        window.UpdateLayout();
        var picker = SettingsUiHelpers.Find<ComboBox>(window, "StylePicker");
        var noStyle = PillText(picker);

        // Act: choose the DEMA kit
        picker.SelectedIndex = picker.Items.OfType<StylePickerItem>().ToList().FindIndex(item => item.Id == "dema");
        AppShellUiHelpers.Flush();
        window.UpdateLayout();

        // Assert: the pill is 26 high and shows only the name of the kit, with dots for its colors
        var dots = picker.GetVisualDescendants().OfType<Avalonia.Controls.Shapes.Ellipse>().Where(dot => dot.IsEffectivelyVisible).ToArray();
        Assert.Equal(26, picker.Bounds.Height);
        Assert.Equal(StylePickerViewModel.NoStyleLabel, noStyle);
        Assert.Equal("DEMA", PillText(picker));
        Assert.InRange(dots.Length, 1, 3);
        Assert.DoesNotContain(dots, dot => dot.Fill is null);
        Assert.False(SettingsUiHelpers.Find<TextBlock>(window, "StylePickerError").IsVisible);

        window.Close();
    }

    /// <summary>
    ///     Test that a screen reader is told the name of the chosen kit, or "(No style)", as the value of the pill and as the
    ///     name of each choice, and not the text of the record that stands behind the choice.
    /// </summary>
    [AvaloniaFact]
    public async Task StylePickerView_Pill_AnnouncesTheNameOfTheChoiceAndNotTheRecord()
    {
        // Arrange: a shown main window with a project open and the kits read
        var (composition, window) = CreateWindow();
        window.Show();
        await composition.StyleCatalog.RefreshAsync(TestContext.Current.CancellationToken);
        await OpenProjectAsync(composition);
        window.UpdateLayout();
        var picker = SettingsUiHelpers.Find<ComboBox>(window, "StylePicker");
        var peer = ControlAutomationPeer.CreatePeerForElement(picker);
        var value = Assert.IsAssignableFrom<IValueProvider>(peer);
        var noStyle = value.Value;

        // Act: choose the DEMA kit
        picker.SelectedIndex = picker.Items.OfType<StylePickerItem>().ToList().FindIndex(item => item.Id == "dema");
        AppShellUiHelpers.Flush();
        window.UpdateLayout();
        var dema = value.Value;

        // Assert: the value is the name of the choice in both cases, and so is the text of every choice
        Assert.Equal(StylePickerViewModel.NoStyleLabel, noStyle);
        Assert.Equal("DEMA", dema);
        Assert.All(picker.Items.OfType<StylePickerItem>(), item =>
        {
            Assert.Equal(item.DisplayName, item.ToString());
            Assert.DoesNotContain("StylePickerItem", item.ToString(), StringComparison.Ordinal);
        });

        window.Close();
    }

    /// <summary>
    ///     Test that the list ends with an entry that opens the window that manages style kits, and that using it closes the
    ///     list.
    /// </summary>
    [AvaloniaFact]
    public async Task StylePickerView_ManageStyleKitsEntry_OpensTheWindowThatManagesKits()
    {
        // Arrange: a shown main window with a project open and the list opened
        var (composition, window) = CreateWindow();
        window.Show();
        await composition.StyleCatalog.RefreshAsync(TestContext.Current.CancellationToken);
        await OpenProjectAsync(composition);
        window.UpdateLayout();
        var picker = SettingsUiHelpers.Find<ComboBox>(window, "StylePicker");
        picker.IsDropDownOpen = true;
        AppShellUiHelpers.Flush();
        var popup = picker.GetVisualDescendants().OfType<Avalonia.Controls.Primitives.Popup>().Single();
        var entry = SettingsUiHelpers.Find<Button>(popup.Child!, "StylePickerManageKits");

        // Act: use the entry with the pointer
        SettingsUiHelpers.Click(window, entry);
        AppShellUiHelpers.Flush();
        var manager = AppShellUiHelpers.FindDialog<StyleKitManagerView>(window);

        // Assert: the window that manages kits is open, and the list is closed
        Assert.True(manager.IsVisible);
        Assert.False(picker.IsDropDownOpen);
        manager.Close();
        window.Close();
    }

    /// <summary>
    ///     Reads the text of the name that the pill shows.
    /// </summary>
    /// <param name="picker">The pill.</param>
    /// <returns>The text of the name.</returns>
    private static string PillText(ComboBox picker)
    {
        return picker.GetVisualDescendants().OfType<TextBlock>().First(text => text.IsEffectivelyVisible && text.Text is { Length: > 0 }).Text!;
    }

    /// <summary>
    ///     Test that choosing a kit in the list writes it into the project file and keeps it selected, and that choosing no
    ///     style takes it out again.
    /// </summary>
    [AvaloniaFact]
    public async Task StylePickerView_ChooseKit_WritesTheProjectFile()
    {
        // Arrange: a shown window with a project open and the kits read
        var (composition, window) = CreateWindow();
        window.Show();
        await composition.StyleCatalog.RefreshAsync(TestContext.Current.CancellationToken);
        await OpenProjectAsync(composition);
        window.UpdateLayout();
        var picker = SettingsUiHelpers.Find<ComboBox>(window, "StylePicker");
        var slides = picker.Items.OfType<StylePickerItem>().ToList().FindIndex(item => item.Id == "slides-dark");

        // Act: the user chooses a kit
        picker.SelectedIndex = slides;
        await composition.StylePicker.ChangeTask;
        AppShellUiHelpers.Flush();
        var chosen = ReadStyleKit(composition);

        // Act: the user chooses no style
        picker.SelectedIndex = 0;
        await composition.StylePicker.ChangeTask;
        AppShellUiHelpers.Flush();
        var cleared = ReadStyleKit(composition);

        // Assert: the file followed each choice, and the active style is the one that was chosen last
        Assert.Equal("slides-dark", chosen);
        Assert.Null(cleared);
        Assert.Null(composition.ActiveStyle.ActiveId);
        Assert.Equal(0, picker.SelectedIndex);

        window.Close();
    }

    /// <summary>
    ///     Test that a project that names a kit that cannot be found shows it as not found in the list.
    /// </summary>
    [AvaloniaFact]
    public async Task StylePickerView_MissingKit_IsShownAsNotFound()
    {
        // Arrange: a shown window with a project that names an unknown kit
        var (composition, window) = CreateWindow();
        window.Show();
        await composition.StyleCatalog.RefreshAsync(TestContext.Current.CancellationToken);
        await OpenProjectAsync(composition);
        await composition.Projects.SetActiveStyleKitAsync("team-kit", TestContext.Current.CancellationToken);
        AppShellUiHelpers.Flush();
        window.UpdateLayout();

        // Act: read what the list shows
        var picker = SettingsUiHelpers.Find<ComboBox>(window, "StylePicker");
        var selected = (StylePickerItem)picker.SelectedItem!;

        // Assert: the kit that cannot be found is the selected entry and says so
        Assert.Equal("team-kit (not found)", selected.Label);
        Assert.True(selected.IsMissing);

        window.Close();
    }

    /// <summary>
    ///     Creates the application on the temporary root and a window over its shell.
    /// </summary>
    /// <returns>The composition and the window.</returns>
    private (ApplicationComposition Composition, MainWindowView Window) CreateWindow()
    {
        var composition = AppShellUiHelpers.Compose(_root);
        _compositions.Add(composition);
        return (composition, new MainWindowView(composition.Shell));
    }

    /// <summary>
    ///     Creates a project and makes it the open one, as the New Project dialog does.
    /// </summary>
    /// <param name="composition">The application.</param>
    /// <returns>A task that completes when the project is open and the kits of the project are read.</returns>
    private async Task OpenProjectAsync(ApplicationComposition composition)
    {
        var token = TestContext.Current.CancellationToken;
        var parent = Directory.CreateDirectory(Path.Combine(_root, "projects")).FullName;
        var created = await composition.Projects.CreateAsync(parent, "Styled", token);
        Assert.True(created.Succeeded, created.Error);
        await composition.StyleCatalog.RefreshAsync(token);
        AppShellUiHelpers.Flush();
    }

    /// <summary>
    ///     Reads the id of the style kit from the project file of the open project.
    /// </summary>
    /// <param name="composition">The application.</param>
    /// <returns>The id, or <see langword="null" /> when the file names none.</returns>
    private static string? ReadStyleKit(ApplicationComposition composition)
    {
        using var document = JsonDocument.Parse(File.ReadAllText(composition.Projects.Current!.FilePath));
        return document.RootElement.TryGetProperty("activeStyleKit", out var property) ? property.GetString() : null;
    }
}
