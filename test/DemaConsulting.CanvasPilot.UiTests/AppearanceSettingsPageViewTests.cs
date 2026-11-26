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
using Avalonia.Threading;
using Avalonia.VisualTree;
using DemaConsulting.CanvasPilot.SettingsSubsystem;

namespace DemaConsulting.CanvasPilot.UiTests;

/// <summary>
///     Headless UI tests of the Appearance page of the settings dialog: what it shows for the stored settings, and that
///     choosing a theme and an explorer option and accepting the dialog stores them.
/// </summary>
public sealed class AppearanceSettingsPageViewTests : IDisposable
{
    /// <summary>
    ///     Temporary settings folder created fresh for each test and removed on disposal.
    /// </summary>
    private readonly string _tempRoot = Directory.CreateTempSubdirectory("canvaspilot-ui-tests-").FullName;

    /// <inheritdoc />
    public void Dispose()
    {
        if (Directory.Exists(_tempRoot))
        {
            Directory.Delete(_tempRoot, recursive: true);
        }
    }

    /// <summary>
    ///     Test that the page shows the stored appearance with the three themes in order, and that its controls have
    ///     automation identifiers and names.
    /// </summary>
    [AvaloniaFact]
    public void AppearanceSettingsPageView_Show_ShowsStoredAppearanceWithAutomationIds()
    {
        // Arrange: a stored pinned light theme with no thumbnails and tall rows
        var service = new SettingsService(_tempRoot);
        service.Save(new AppSettings
        {
            Appearance = new AppearanceSettings { Theme = ThemeChoice.Light, ShowThumbnails = false, ComfortableRows = true }
        });
        var (window, _, _) = CreateDialog(service);

        // Act: show the dialog on the Appearance page
        window.Show();
        Dispatcher.UIThread.RunJobs();

        // Assert: the page is there with its controls and shows the stored values
        Assert.NotNull(SettingsUiHelpers.Find<UserControl>(window, "SettingsAppearancePage"));
        var theme = SettingsUiHelpers.Find<ComboBox>(window, "AppearanceThemeComboBox");
        Assert.Equal(
            ["Follow system", "Dark", "Light"],
            theme.Items.OfType<SettingsChoice<ThemeChoice>>().Select(choice => choice.Label));
        Assert.Equal(ThemeChoice.Light, Assert.IsType<SettingsChoice<ThemeChoice>>(theme.SelectedItem).Value);
        Assert.Equal("Theme", AutomationProperties.GetName(theme));
        Assert.False(SettingsUiHelpers.Find<CheckBox>(window, "AppearanceShowThumbnailsCheckBox").IsChecked);
        Assert.True(SettingsUiHelpers.Find<CheckBox>(window, "AppearanceComfortableRowsCheckBox").IsChecked);

        window.Close();
    }

    /// <summary>
    ///     Test that the closed theme list tells a screen reader the label of the chosen theme, as it shows it, for every
    ///     theme.
    /// </summary>
    [AvaloniaFact]
    public void AppearanceSettingsPageView_ThemeList_AnnouncesTheShownTheme()
    {
        // Arrange: a dialog on the Appearance page over default settings
        var service = new SettingsService(_tempRoot);
        var (window, _, _) = CreateDialog(service);
        window.Show();
        Dispatcher.UIThread.RunJobs();
        var theme = SettingsUiHelpers.Find<ComboBox>(window, "AppearanceThemeComboBox");
        var announced = new List<string?>();

        // Act: choose each theme in turn and read what the closed list tells a screen reader
        foreach (var choice in theme.Items.OfType<SettingsChoice<ThemeChoice>>().ToList())
        {
            theme.SelectedItem = choice;
            Dispatcher.UIThread.RunJobs();
            announced.Add(SettingsUiHelpers.AnnouncedValue(theme));
        }

        // Assert: each value is the label of the theme
        Assert.Equal(["Follow system", "Dark", "Light"], announced);

        window.Close();
    }

    /// <summary>
    ///     Test that choosing the dark theme and a compact list on the page and then choosing OK stores them.
    /// </summary>
    [AvaloniaFact]
    public void AppearanceSettingsPageView_ChooseThemeAndOptions_Ok_StoresThem()
    {
        // Arrange: a dialog on the Appearance page over default settings
        var service = new SettingsService(_tempRoot);
        var (window, _, _) = CreateDialog(service);
        window.Show();
        Dispatcher.UIThread.RunJobs();

        // Act: choose Dark, turn the thumbnails off, turn the tall rows on, and choose OK
        var theme = SettingsUiHelpers.Find<ComboBox>(window, "AppearanceThemeComboBox");
        theme.SelectedItem = theme.Items.OfType<SettingsChoice<ThemeChoice>>().Single(choice => choice.Value == ThemeChoice.Dark);
        SettingsUiHelpers.Find<CheckBox>(window, "AppearanceShowThumbnailsCheckBox").IsChecked = false;
        SettingsUiHelpers.Find<CheckBox>(window, "AppearanceComfortableRowsCheckBox").IsChecked = true;
        SettingsUiHelpers.Click(window, SettingsUiHelpers.Find<Button>(window, "SettingsOkButton"));
        Dispatcher.UIThread.RunJobs();
        var stored = new SettingsService(_tempRoot).Load().Appearance;

        // Assert: the choices were stored
        Assert.Equal(ThemeChoice.Dark, stored.Theme);
        Assert.False(stored.ShowThumbnails);
        Assert.True(stored.ComfortableRows);
    }

    /// <summary>
    ///     Creates a settings dialog window that has only the Appearance page.
    /// </summary>
    /// <param name="service">The settings service.</param>
    /// <returns>The window, its view model, and the page.</returns>
    private static (SettingsDialogView Window, SettingsDialogViewModel Dialog, AppearanceSettingsPageViewModel Page)
        CreateDialog(SettingsService service)
    {
        var page = new AppearanceSettingsPageViewModel();
        var dialog = new SettingsDialogViewModel(service, [page]);
        var window = new SettingsDialogView(dialog);
        return (window, dialog, page);
    }
}
