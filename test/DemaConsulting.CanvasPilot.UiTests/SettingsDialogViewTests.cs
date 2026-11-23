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

using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using DemaConsulting.CanvasPilot.SettingsSubsystem;

namespace DemaConsulting.CanvasPilot.UiTests;

/// <summary>
///     Headless UI tests for the settings dialog window and page navigation.
/// </summary>
public sealed class SettingsDialogViewTests : IDisposable
{
    /// <summary>
    ///     Temporary settings folder created fresh for each test and removed on disposal.
    /// </summary>
    private readonly string _tempRoot = Directory.CreateTempSubdirectory("canvaspilot-ui-tests-").FullName;

    /// <summary>
    ///     Service over the temporary settings folder.
    /// </summary>
    private readonly SettingsService _service;

    /// <summary>
    ///     Creates the test fixture.
    /// </summary>
    public SettingsDialogViewTests()
    {
        _service = new SettingsService(_tempRoot);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (Directory.Exists(_tempRoot))
        {
            Directory.Delete(_tempRoot, recursive: true);
        }
    }

    /// <summary>
    ///     Test that the dialog lists every page and shows the first page's content.
    /// </summary>
    [AvaloniaFact]
    public void SettingsDialogView_Show_ListsPagesAndShowsFirstPageContent()
    {
        // Arrange: a dialog over the AI provider page and a second page
        var (window, _, _) = CreateDialog();

        // Act: show the window
        window.Show();

        // Assert: both titles are listed and only the first page's content is present
        var list = SettingsUiHelpers.Find<ListBox>(window, "SettingsPageList");
        Assert.Equal(2, list.ItemCount);
        Assert.Equal("Settings", window.Title);
        Assert.NotNull(SettingsUiHelpers.Find<ComboBox>(window, "AiProviderComboBox"));
        Assert.DoesNotContain(window.GetVisualDescendants().OfType<TextBlock>(), t => t.Text == "Test page content");

        window.Close();
    }

    /// <summary>
    ///     Test that choosing another page in the list shows that page's content.
    /// </summary>
    [AvaloniaFact]
    public void SettingsDialogView_SelectOtherPage_ShowsItsContent()
    {
        // Arrange: a shown dialog on its first page
        var (window, _, _) = CreateDialog();
        window.Show();
        var list = SettingsUiHelpers.Find<ListBox>(window, "SettingsPageList");

        // Act: select the second page
        list.SelectedIndex = 1;
        Dispatcher.UIThread.RunJobs();

        // Assert: the second page's content replaces the first
        Assert.Contains(window.GetVisualDescendants().OfType<TextBlock>(), t => t.Text == "Test page content");
        Assert.DoesNotContain(window.GetVisualDescendants().OfType<ComboBox>(), IsAiProviderCombo);

        window.Close();
    }

    /// <summary>
    ///     Test that the heading over the page shows the title and the sentence of the chosen page, that every page in the rail
    ///     has an icon, and that a problem is shown in the banner of the footer with an icon and words.
    /// </summary>
    [AvaloniaFact]
    public void SettingsDialogView_ChosenPage_ShowsHeadingIconsAndErrorBanner()
    {
        // Arrange: a shown dialog on its first page
        var (window, _, _) = CreateDialog();
        window.Show();
        Dispatcher.UIThread.RunJobs();
        var list = SettingsUiHelpers.Find<ListBox>(window, "SettingsPageList");
        var title = SettingsUiHelpers.Find<TextBlock>(window, "SettingsPageTitle");
        var description = SettingsUiHelpers.Find<TextBlock>(window, "SettingsPageDescription");
        var error = SettingsUiHelpers.Find<TextBlock>(window, "SettingsErrorMessage");
        var firstTitle = title.Text;
        var firstDescription = description.Text;
        var bannerBefore = error.IsEffectivelyVisible;

        // Act: choose the second page and report a problem
        list.SelectedIndex = 1;
        ((SettingsDialogViewModel)window.DataContext!).ErrorMessage = "The value cannot be saved.";
        Dispatcher.UIThread.RunJobs();

        // Assert: the heading followed the page, each item has an icon with a shape, and the banner shows with its words
        Assert.Equal("AI Provider", firstTitle);
        Assert.False(string.IsNullOrEmpty(firstDescription));
        Assert.Equal("Other", title.Text);
        Assert.False(description.IsEffectivelyVisible);
        Assert.Equal(2, list.GetVisualDescendants().OfType<PathIcon>().Count(icon => icon.Data is not null));
        Assert.False(bannerBefore);
        Assert.True(error.IsEffectivelyVisible);

        window.Close();
    }

    /// <summary>
    ///     Test that the OK button saves the page entries and closes the window.
    /// </summary>
    [AvaloniaFact]
    public void SettingsDialogView_OkButton_SavesAndClosesWindow()
    {
        // Arrange: a shown dialog with an edited model name
        var (window, aiPage, _) = CreateDialog();
        var closed = false;
        window.Closed += (_, _) => closed = true;
        window.Show();
        aiPage.ModelName = "from-ui";

        // Act: click OK
        SettingsUiHelpers.Click(window, SettingsUiHelpers.Find<Button>(window, "SettingsOkButton"));

        // Assert: the entry was saved and the window closed
        Assert.True(closed);
        Assert.Equal("from-ui", new SettingsService(_tempRoot).Load().AiProvider.ModelName);
    }

    /// <summary>
    ///     Test that the Apply button saves the page entries and leaves the window open.
    /// </summary>
    [AvaloniaFact]
    public void SettingsDialogView_ApplyButton_SavesAndKeepsWindowOpen()
    {
        // Arrange: a shown dialog with an edited model name
        var (window, aiPage, _) = CreateDialog();
        window.Show();
        aiPage.ModelName = "applied";

        // Act: click Apply
        SettingsUiHelpers.Click(window, SettingsUiHelpers.Find<Button>(window, "SettingsApplyButton"));

        // Assert: the entry was saved and the window is still open
        Assert.True(window.IsVisible);
        Assert.Equal("applied", new SettingsService(_tempRoot).Load().AiProvider.ModelName);

        window.Close();
    }

    /// <summary>
    ///     Test that the Cancel button closes the window without saving.
    /// </summary>
    [AvaloniaFact]
    public void SettingsDialogView_CancelButton_ClosesWithoutSaving()
    {
        // Arrange: a shown dialog with an edited model name
        var (window, aiPage, _) = CreateDialog();
        window.Show();
        aiPage.ModelName = "discarded";

        // Act: click Cancel
        SettingsUiHelpers.Click(window, SettingsUiHelpers.Find<Button>(window, "SettingsCancelButton"));

        // Assert: the window closed and nothing was saved
        Assert.False(window.IsVisible);
        Assert.False(File.Exists(_service.SettingsFilePath));
    }

    /// <summary>
    ///     Test that OK and Apply are disabled while a page is invalid, and Cancel stays available.
    /// </summary>
    [AvaloniaFact]
    public void SettingsDialogView_InvalidPage_DisablesOkAndApply()
    {
        // Arrange: a shown dialog
        var (window, aiPage, _) = CreateDialog();
        window.Show();

        // Act: type an invalid context window
        aiPage.ContextWindowText = "1";

        // Assert: OK and Apply are disabled and Cancel is enabled
        Assert.False(SettingsUiHelpers.Find<Button>(window, "SettingsOkButton").IsEffectivelyEnabled);
        Assert.False(SettingsUiHelpers.Find<Button>(window, "SettingsApplyButton").IsEffectivelyEnabled);
        Assert.True(SettingsUiHelpers.Find<Button>(window, "SettingsCancelButton").IsEffectivelyEnabled);

        window.Close();
    }

    /// <summary>
    ///     Test that closing the window by any route tells the pages so they can release resources.
    /// </summary>
    [AvaloniaFact]
    public void SettingsDialogView_CloseWindow_NotifiesPages()
    {
        // Arrange: a shown dialog
        var (window, _, otherPage) = CreateDialog();
        window.Show();

        // Act: close the window directly, as the window's close button does
        window.Close();

        // Assert: the pages were told once
        Assert.Equal(1, otherPage.ClosedCount);
    }

    /// <summary>
    ///     Determines whether a combo box is the AI provider combo box.
    /// </summary>
    /// <param name="comboBox">The combo box.</param>
    /// <returns><see langword="true" /> when it is the AI provider combo box.</returns>
    private static bool IsAiProviderCombo(ComboBox comboBox)
    {
        return Avalonia.Automation.AutomationProperties.GetAutomationId(comboBox) == "AiProviderComboBox";
    }

    /// <summary>
    ///     Creates a dialog window over the AI provider page and a minimal second page.
    /// </summary>
    /// <returns>The window, the AI provider page, and the second page.</returns>
    private (SettingsDialogView Window, AiProviderSettingsPageViewModel AiPage, TestSettingsPageViewModel OtherPage) CreateDialog()
    {
        var aiPage = new AiProviderSettingsPageViewModel();
        var otherPage = new TestSettingsPageViewModel("Other");
        var viewModel = new SettingsDialogViewModel(_service, [aiPage, otherPage]);
        return (new SettingsDialogView(viewModel), aiPage, otherPage);
    }
}
