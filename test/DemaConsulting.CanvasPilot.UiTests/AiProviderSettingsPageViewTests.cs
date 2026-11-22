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
using Avalonia.VisualTree;
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
    ///     Test that the unrestricted agent tools option is not offered: no check box and no text about it is shown,
    ///     while the stored value is still carried through.
    /// </summary>
    [AvaloniaFact]
    public void AiProviderSettingsPageView_UnrestrictedAgentTools_IsNotOffered()
    {
        // Arrange: a shown page loaded with the stored value switched on
        var viewModel = new AiProviderSettingsPageViewModel();
        viewModel.Load(new AppSettings { AiProvider = new AiProviderSettings { UnrestrictedAgentTools = true } });
        var window = new Window { Content = new AiProviderSettingsPageView { DataContext = viewModel } };
        window.Show();

        // Act: look at everything that is shown
        var checkBoxes = window.GetVisualDescendants().OfType<CheckBox>().ToList();
        var texts = window.GetVisualDescendants().OfType<TextBlock>().Select(block => block.Text ?? string.Empty).ToList();

        // Assert: nothing to check, nothing about unrestricted tools, and the value is still applied unchanged
        Assert.Empty(checkBoxes);
        Assert.DoesNotContain(texts, text => text.Contains("unrestricted", StringComparison.OrdinalIgnoreCase));
        Assert.True(viewModel.Apply(new AppSettings()).AiProvider.UnrestrictedAgentTools);

        window.Close();
    }

    /// <summary>
    ///     Test that the reasoning level is shown disabled, with the stored value and the note that says why.
    /// </summary>
    [AvaloniaFact]
    public void AiProviderSettingsPageView_ReasoningLevel_IsDisabledWithNote()
    {
        // Arrange: a shown page loaded with a stored reasoning level
        var viewModel = new AiProviderSettingsPageViewModel();
        viewModel.Load(new AppSettings { AiProvider = new AiProviderSettings { ReasoningLevel = ReasoningLevel.High } });
        var window = new Window { Content = new AiProviderSettingsPageView { DataContext = viewModel } };
        window.Show();

        // Act: find the combo box and its note
        var combo = SettingsUiHelpers.Find<ComboBox>(window, "AiReasoningLevelComboBox");
        var note = SettingsUiHelpers.Find<TextBlock>(window, "AiReasoningLevelNote");

        // Assert: the combo box cannot be used, still shows the stored value, and the note is the agreed sentence
        Assert.False(combo.IsEnabled);
        Assert.Equal(ReasoningLevel.High, viewModel.SelectedReasoningLevel.Value);
        Assert.Equal(viewModel.SelectedReasoningLevel, combo.SelectedItem);
        Assert.Equal("Not available yet: the AI library does not expose it", note.Text);
        Assert.True(note.IsVisible);

        window.Close();
    }

    /// <summary>
    ///     Test that the context window is shown as an Ollama setting: the label says so, a note replaces the warning while
    ///     GitHub Copilot is selected, and a window below the recommended minimum shows a warning under the field while
    ///     Ollama is selected.
    /// </summary>
    [AvaloniaFact]
    public void AiProviderSettingsPageView_ContextWindow_IsOllamaOnlyAndWarnsBelowTheRecommendedMinimum()
    {
        // Arrange: a shown page with GitHub Copilot selected
        var (window, viewModel) = ShowPage();
        var warning = SettingsUiHelpers.Find<TextBlock>(window, "AiContextWindowWarning");
        var note = SettingsUiHelpers.Find<TextBlock>(window, "AiContextWindowNote");
        var box = SettingsUiHelpers.Find<TextBox>(window, "AiContextWindowTextBox");
        var labelShown = window.GetVisualDescendants().OfType<TextBlock>().Any(block => block.Text == "Context window (tokens, Ollama only)");
        var copilotNoteVisible = note.IsVisible;
        var copilotNoteText = note.Text;

        // Act: choose Ollama, type a window just below the minimum, then the minimum
        SettingsUiHelpers.Find<ComboBox>(window, "AiProviderComboBox").SelectedIndex = 1;
        var ollamaNoteVisible = note.IsVisible;
        box.Text = "16383";
        var warnedBelow = warning.IsVisible;
        var warningText = warning.Text;
        box.Text = "16384";

        // Assert: Copilot got the note and no warning, Ollama got the warning below the minimum and no note, and the
        // warning is gone at the minimum and never made the page invalid
        Assert.True(labelShown);
        Assert.True(copilotNoteVisible);
        Assert.Contains("Ollama only", copilotNoteText, StringComparison.Ordinal);
        Assert.False(ollamaNoteVisible);
        Assert.True(warnedBelow);
        Assert.Contains("16,384", warningText, StringComparison.Ordinal);
        Assert.False(warning.IsVisible);
        Assert.True(viewModel.IsValid);

        window.Close();
    }

    /// <summary>
    ///     Test that the notes of the Copilot tool path say which executable is used and that shims are ignored.
    /// </summary>
    [AvaloniaFact]
    public void AiProviderSettingsPageView_CopilotCliPath_ShowsNoteOnInstalledExecutable()
    {
        // Arrange / Act: a shown page
        var (window, _) = ShowPage();
        var note = SettingsUiHelpers.Find<TextBlock>(window, "AiCopilotCliPathNote");

        // Assert: the note names the executable, the minimum version, and the shims
        Assert.True(note.IsVisible);
        Assert.Contains("copilot.exe", note.Text, StringComparison.Ordinal);
        Assert.Contains("1.0.113", note.Text, StringComparison.Ordinal);
        Assert.Contains("shims", note.Text, StringComparison.Ordinal);

        window.Close();
    }

    /// <summary>
    ///     Test that the Check connection button runs the provider check over the entries on the page and shows its
    ///     answer, in red when the provider cannot be used, and that a page without a check has no button.
    /// </summary>
    [AvaloniaFact]
    public void AiProviderSettingsPageView_CheckConnection_ShowsTheAnswerOfTheCheck()
    {
        // Arrange: a shown page over a check that gives the answer the test sets
        var check = new FixedCheck(new ProviderCheckResult(false, "Copilot is not signed in."));
        var viewModel = new AiProviderSettingsPageViewModel(check);
        viewModel.Load(new AppSettings());
        var window = new Window { Width = 520, Height = 1100, Content = new AiProviderSettingsPageView { DataContext = viewModel } };
        window.Show();
        var button = SettingsUiHelpers.Find<Button>(window, "AiCheckConnectionButton");
        var result = SettingsUiHelpers.Find<TextBlock>(window, "AiCheckConnectionResult");
        var visibleBefore = result.IsVisible;

        // Act: the user chooses Check connection, then the check finds the provider usable and the user chooses it again
        SettingsUiHelpers.Click(window, button);
        var failureText = result.Text;
        var failureClasses = result.Classes.ToList();
        check.Result = new ProviderCheckResult(true, "Copilot is ready.");
        SettingsUiHelpers.Click(window, button);

        // Assert: the button was offered, nothing was shown before, the failure was shown as one, and the next answer was not
        Assert.True(button.IsVisible);
        Assert.False(visibleBefore);
        Assert.Equal("Copilot is not signed in.", failureText);
        Assert.Contains("failed", failureClasses);
        Assert.Equal("Copilot is ready.", result.Text);
        Assert.DoesNotContain("failed", result.Classes);
        Assert.Equal(2, check.Calls);

        // Act and assert: a page without a check has no button
        var (plainWindow, _) = ShowPage();
        Assert.False(SettingsUiHelpers.Find<Button>(plainWindow, "AiCheckConnectionButton").IsVisible);

        plainWindow.Close();
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
    ///     A provider check that answers at once with a result the test can change and counts its calls.
    /// </summary>
    /// <param name="result">The first answer.</param>
    private sealed class FixedCheck(ProviderCheckResult result) : IProviderCheck
    {
        /// <summary>Gets or sets the answer that the next call gives.</summary>
        public ProviderCheckResult Result { get; set; } = result;

        /// <summary>Gets the number of calls.</summary>
        public int Calls { get; private set; }

        /// <inheritdoc />
        public Task<ProviderCheckResult> CheckAsync(AiProviderSettings settings, CancellationToken cancellationToken = default)
        {
            Calls++;
            return Task.FromResult(Result);
        }
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
