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
using DemaConsulting.CanvasPilot.SettingsSubsystem;

namespace DemaConsulting.CanvasPilot.UiTests;

/// <summary>
///     Headless UI tests for the AI provider settings page view.
/// </summary>
public sealed class AiProviderSettingsPageViewTests
{
    /// <summary>
    ///     Test that the Ollama endpoint is enabled only while Ollama is selected, and the Copilot tool path only
    ///     while GitHub Copilot is selected.
    /// </summary>
    [AvaloniaFact]
    public void AiProviderSettingsPageView_SelectProvider_EnablesMatchingEntry()
    {
        // Arrange: a shown page with default settings, which select GitHub Copilot
        var (window, viewModel) = ShowPage();
        var endpoint = SettingsUiHelpers.Find<TextBox>(window, "AiOllamaEndpointTextBox");
        var cliPath = SettingsUiHelpers.Find<TextBox>(window, "AiCopilotCliPathTextBox");
        var providers = SettingsUiHelpers.Find<ComboBox>(window, "AiProviderComboBox");
        var copilotOnlyEndpointEnabled = endpoint.IsEnabled;
        var copilotOnlyCliPathEnabled = cliPath.IsEnabled;

        // Act: choose Ollama in the combo box
        providers.SelectedIndex = 1;

        // Assert: Copilot showed only its path entry, and Ollama shows only its endpoint entry
        Assert.False(copilotOnlyEndpointEnabled);
        Assert.True(copilotOnlyCliPathEnabled);
        Assert.True(viewModel.IsOllamaSelected);
        Assert.True(endpoint.IsEnabled);
        Assert.False(cliPath.IsEnabled);

        window.Close();
    }

    /// <summary>
    ///     Test that an invalid context window shows its error message, and a valid one hides it.
    /// </summary>
    [AvaloniaFact]
    public void AiProviderSettingsPageView_InvalidContextWindow_ShowsError()
    {
        // Arrange: a shown page
        var (window, viewModel) = ShowPage();
        var box = SettingsUiHelpers.Find<TextBox>(window, "AiContextWindowTextBox");
        var error = SettingsUiHelpers.Find<TextBlock>(window, "AiContextWindowError");
        var initiallyVisible = error.IsVisible;

        // Act: type an out-of-range value, then a valid one
        box.Text = "100";
        var visibleWhenInvalid = error.IsVisible;
        var textWhenInvalid = error.Text;
        box.Text = "16384";

        // Assert: the error is hidden, shown with the bounds, then hidden again
        Assert.False(initiallyVisible);
        Assert.True(visibleWhenInvalid);
        Assert.Contains("512", textWhenInvalid);
        Assert.False(error.IsVisible);
        Assert.Equal("16384", viewModel.ContextWindowText);

        window.Close();
    }

    /// <summary>
    ///     Test that the unrestricted agent tools opt-in starts unchecked and follows the user's choice.
    /// </summary>
    [AvaloniaFact]
    public void AiProviderSettingsPageView_UnrestrictedAgentToolsCheckBox_StartsOffAndTogglesSetting()
    {
        // Arrange: a shown page with default settings
        var (window, viewModel) = ShowPage();
        var checkBox = SettingsUiHelpers.Find<CheckBox>(window, "AiUnrestrictedAgentToolsCheckBox");
        var initiallyChecked = checkBox.IsChecked;

        // Act: the user checks the box
        checkBox.IsChecked = true;

        // Assert: it started unchecked and the view model followed
        Assert.False(initiallyChecked);
        Assert.True(viewModel.UnrestrictedAgentTools);

        window.Close();
    }

    /// <summary>
    ///     Test that values loaded into the view model appear in the page's controls.
    /// </summary>
    [AvaloniaFact]
    public void AiProviderSettingsPageView_LoadedSettings_AreShownInControls()
    {
        // Arrange: a view model loaded with stored settings
        var viewModel = new AiProviderSettingsPageViewModel();
        viewModel.Load(new AppSettings
        {
            AiProvider = new AiProviderSettings { ModelName = "stored-model", ContextWindowTokens = 4096 },
        });

        // Act: show the page
        var window = new Window { Content = new AiProviderSettingsPageView { DataContext = viewModel } };
        window.Show();

        // Assert: the controls show the stored values
        Assert.Equal("stored-model", SettingsUiHelpers.Find<TextBox>(window, "AiModelNameTextBox").Text);
        Assert.Equal("4096", SettingsUiHelpers.Find<TextBox>(window, "AiContextWindowTextBox").Text);

        window.Close();
    }

    /// <summary>
    ///     Shows an AI provider page over default settings in a plain window.
    /// </summary>
    /// <returns>The window and the page's view model.</returns>
    private static (Window Window, AiProviderSettingsPageViewModel ViewModel) ShowPage()
    {
        var viewModel = new AiProviderSettingsPageViewModel();
        viewModel.Load(new AppSettings());
        var window = new Window { Content = new AiProviderSettingsPageView { DataContext = viewModel } };
        window.Show();
        return (window, viewModel);
    }
}
