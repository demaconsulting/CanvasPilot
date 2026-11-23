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
    ///     Test that the entries of the provider that is not selected are hidden, not just disabled: the Ollama endpoint
    ///     only while Ollama is selected, and the Copilot tool path only while GitHub Copilot is selected.
    /// </summary>
    [AvaloniaFact]
    public void AiProviderSettingsPageView_SelectProvider_ShowsMatchingEntry()
    {
        // Arrange: a shown page with default settings, which select GitHub Copilot
        var (window, viewModel) = ShowPage();
        var endpoint = SettingsUiHelpers.Find<TextBox>(window, "AiOllamaEndpointTextBox");
        var cliPath = SettingsUiHelpers.Find<TextBox>(window, "AiCopilotCliPathTextBox");
        var providers = SettingsUiHelpers.Find<ComboBox>(window, "AiProviderComboBox");
        var copilotOnlyEndpointVisible = endpoint.IsVisible;
        var copilotOnlyCliPathVisible = cliPath.IsVisible;

        // Act: choose Ollama in the combo box
        providers.SelectedIndex = 1;

        // Assert: Copilot showed only its path entry, and Ollama shows only its endpoint entry
        Assert.False(copilotOnlyEndpointVisible);
        Assert.True(copilotOnlyCliPathVisible);
        Assert.True(viewModel.IsOllamaSelected);
        Assert.True(endpoint.IsVisible);
        Assert.True(endpoint.IsEnabled);
        Assert.False(cliPath.IsVisible);

        window.Close();
    }

    /// <summary>
    ///     Test that an invalid context window shows its error message, and a valid one hides it, while Ollama is selected.
    /// </summary>
    [AvaloniaFact]
    public void AiProviderSettingsPageView_InvalidContextWindow_ShowsError()
    {
        // Arrange: a shown page with Ollama selected
        var (window, viewModel) = ShowPage(AiProviderKind.Ollama);
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
    ///     Test that the reasoning level of a model that offers none is disabled, and that the stored value is still shown
    ///     while nothing is known about the model.
    /// </summary>
    [AvaloniaFact]
    public void AiProviderSettingsPageView_ReasoningEffort_IsDisabledWhileTheModelIsNotKnown()
    {
        // Arrange: a shown page without a model list, loaded with a stored model and reasoning effort
        var viewModel = new AiProviderSettingsPageViewModel();
        viewModel.Load(new AppSettings { AiProvider = new AiProviderSettings { ModelName = "typed-model", ReasoningEffort = "xhigh" } });
        var window = new Window { Content = new AiProviderSettingsPageView { DataContext = viewModel } };
        window.Show();

        // Act: find the combo box and its note
        var combo = SettingsUiHelpers.Find<ComboBox>(window, "AiReasoningEffortComboBox");
        var note = SettingsUiHelpers.Find<TextBlock>(window, "AiReasoningEffortNote");

        // Assert: the combo box cannot be used, still shows the stored value, and the note says why
        Assert.False(combo.IsEnabled);
        Assert.Equal("xhigh", ((SettingsChoice<string>)combo.SelectedItem!).Value);
        Assert.True(note.IsVisible);
        Assert.Contains("not known", note.Text, StringComparison.Ordinal);

        window.Close();
    }

    /// <summary>
    ///     Test that the page lists the models with a note on what each lacks, shows the capabilities and the warnings of the
    ///     chosen model, enables the reasoning level from the model, and stores the model and the level that were chosen.
    /// </summary>
    [AvaloniaFact]
    public async Task AiProviderSettingsPageView_ModelList_ShowsCapabilitiesWarningsAndReasoningLevels()
    {
        // Arrange: a shown page over a source with a model that can do everything and one that cannot see pictures
        var source = new FixedModels(
            Model("full-model", vision: true, efforts: ["low", "high", "xhigh"]),
            Model("blind-model", vision: false));
        var viewModel = new AiProviderSettingsPageViewModel(null, source, TimeSpan.Zero);
        viewModel.Load(new AppSettings());
        await viewModel.ModelListLoaded;
        var window = new Window { Width = 520, Height = 1300, Content = new AiProviderSettingsPageView { DataContext = viewModel } };
        window.Show();
        var models = SettingsUiHelpers.Find<ComboBox>(window, "AiModelComboBox");
        var details = SettingsUiHelpers.Find<TextBlock>(window, "AiModelDetails");
        var warning = SettingsUiHelpers.Find<TextBlock>(window, "AiModelWarning");
        var efforts = SettingsUiHelpers.Find<ComboBox>(window, "AiReasoningEffortComboBox");
        var shownBefore = models.IsVisible;
        var effortsEnabledBefore = efforts.IsEnabled;

        // Act: choose the model that sees pictures and a level, then the one that cannot see
        models.SelectedItem = viewModel.Models.Single(choice => choice.Id == "full-model");
        var effortsEnabledForFull = efforts.IsEnabled;
        var detailsOfFull = details.Text;
        var warningShownForFull = warning.IsVisible;
        efforts.SelectedItem = viewModel.ReasoningEfforts.Single(choice => choice.Value == "xhigh");
        var stored = viewModel.Apply(new AppSettings()).AiProvider;
        models.SelectedItem = viewModel.Models.Single(choice => choice.Id == "blind-model");

        // Assert: the list was shown with its entries, the capabilities were listed, the warning appeared only for the model
        // that lacks pictures, the level was enabled only while the model offers some, and the choice was stored
        Assert.True(shownBefore);
        Assert.False(effortsEnabledBefore);
        Assert.Equal(3, models.ItemCount);
        Assert.Contains(viewModel.Models, choice => choice.Label.Contains("no pictures", StringComparison.Ordinal));
        Assert.True(effortsEnabledForFull);
        Assert.Contains("Tool calling: yes", detailsOfFull, StringComparison.Ordinal);
        Assert.Contains("low, high, xhigh", detailsOfFull, StringComparison.Ordinal);
        Assert.False(warningShownForFull);
        Assert.Equal("full-model", stored.ModelName);
        Assert.Equal("xhigh", stored.ReasoningEffort);
        Assert.True(warning.IsVisible);
        Assert.Contains("does not accept pictures", warning.Text, StringComparison.Ordinal);
        Assert.False(efforts.IsEnabled);
        Assert.Equal(string.Empty, viewModel.Apply(new AppSettings()).AiProvider.ReasoningEffort);

        window.Close();
    }

    /// <summary>
    ///     Test that the page shows the warning that a high reasoning level on a large model is slow and costly only while
    ///     such a model and level are chosen.
    /// </summary>
    [AvaloniaFact]
    public async Task AiProviderSettingsPageView_HighEffortOnLargeModel_ShowsTheCostWarning()
    {
        // Arrange: a shown page over a large model with levels
        var large = Model("opus", efforts: ["low", "high"]) with { BillingMultiplier = 15, Category = "powerful" };
        var viewModel = new AiProviderSettingsPageViewModel(null, new FixedModels(large), TimeSpan.Zero);
        viewModel.Load(new AppSettings { AiProvider = new AiProviderSettings { ModelName = "opus", ReasoningEffort = "low" } });
        await viewModel.ModelListLoaded;
        var window = new Window { Width = 520, Height = 1300, Content = new AiProviderSettingsPageView { DataContext = viewModel } };
        window.Show();
        var warning = SettingsUiHelpers.Find<TextBlock>(window, "AiEffortCostWarning");
        var efforts = SettingsUiHelpers.Find<ComboBox>(window, "AiReasoningEffortComboBox");
        var shownAtLow = warning.IsEffectivelyVisible;

        // Act: choose the high level
        efforts.SelectedItem = viewModel.ReasoningEfforts.Single(choice => choice.Value == "high");

        // Assert: the warning appears with the cost of the model
        Assert.False(shownAtLow);
        Assert.True(warning.IsEffectivelyVisible);
        Assert.Contains("15 premium requests", warning.Text, StringComparison.Ordinal);

        window.Close();
    }

    /// <summary>
    ///     Test that a model that cannot be used is shown disabled in the list, and that the typed model name and the message
    ///     stay available when the list could not be read, with the Refresh models button asking again.
    /// </summary>
    [AvaloniaFact]
    public async Task AiProviderSettingsPageView_ModelListFails_KeepsTheTypedNameAndShowsTheMessage()
    {
        // Arrange: a page over a source that cannot be asked
        var source = new FixedModels { Message = "Ollama did not answer at http://localhost:11434." };
        var viewModel = new AiProviderSettingsPageViewModel(null, source, TimeSpan.Zero);
        viewModel.Load(new AppSettings { AiProvider = new AiProviderSettings { ModelName = "typed-model" } });
        await viewModel.ModelListLoaded;
        var window = new Window { Width = 520, Height = 1300, Content = new AiProviderSettingsPageView { DataContext = viewModel } };
        window.Show();
        var models = SettingsUiHelpers.Find<ComboBox>(window, "AiModelComboBox");
        var message = SettingsUiHelpers.Find<TextBlock>(window, "AiModelListMessage");
        var refresh = SettingsUiHelpers.Find<Button>(window, "AiRefreshModelsButton");
        var typed = SettingsUiHelpers.Find<TextBox>(window, "AiModelNameTextBox");
        var messageBefore = message.Text;
        var listShownBefore = models.IsVisible;

        // Act: the server comes up, and the user chooses Refresh models
        source.Message = string.Empty;
        source.Models = [Model("typed-model"), Model("disabled-model", usable: false)];
        SettingsUiHelpers.Click(window, refresh);
        await viewModel.ModelListLoaded;

        // Assert: before, no list but the message and the typed name; after, the list with the typed name chosen and the
        // model that cannot be used disabled
        Assert.Equal(2, source.Calls);
        Assert.False(listShownBefore);
        Assert.Contains("did not answer", messageBefore ?? string.Empty, StringComparison.Ordinal);
        Assert.Equal("typed-model", typed.Text);
        Assert.True(models.IsVisible);
        Assert.Equal("typed-model", ((ModelChoice)models.SelectedItem!).Id);
        Assert.True(source.LastRefresh);
        Assert.False(viewModel.Models.Single(choice => choice.Id == "disabled-model").IsSelectable);

        window.Close();
    }

    /// <summary>
    ///     A source of models that answers at once with what the test sets.
    /// </summary>
    /// <param name="models">The models to answer with.</param>
    private sealed class FixedModels(params ProviderModelInfo[] models) : IProviderModelSource
    {
        /// <summary>Gets or sets the models to answer with.</summary>
        public ProviderModelInfo[] Models { get; set; } = models;

        /// <summary>Gets or sets the message to answer with.</summary>
        public string Message { get; set; } = string.Empty;

        /// <summary>Gets the number of calls.</summary>
        public int Calls { get; private set; }

        /// <summary>Gets a value indicating whether the last call asked to refresh.</summary>
        public bool LastRefresh { get; private set; }

        /// <inheritdoc />
        public Task<ProviderModelList> ListModelsAsync(AiProviderSettings settings, bool refresh = false, CancellationToken cancellationToken = default)
        {
            Calls++;
            LastRefresh = refresh;
            return Task.FromResult(new ProviderModelList(Models, Message));
        }
    }

    /// <summary>
    ///     Makes a model of the provider.
    /// </summary>
    /// <param name="id">The id of the model.</param>
    /// <param name="vision">Whether the model accepts pictures.</param>
    /// <param name="efforts">The ids of the efforts that the model offers.</param>
    /// <param name="usable">Whether the model can be used.</param>
    /// <returns>The model.</returns>
    private static ProviderModelInfo Model(string id, bool vision = true, string[]? efforts = null, bool usable = true)
    {
        var offered = efforts ?? [];
        return new ProviderModelInfo(
            id,
            id,
            true,
            vision,
            offered.Length > 0,
            256_000,
            [.. offered.Select(effort => new ProviderEffortChoice(effort, effort))],
            null,
            usable,
            usable ? null : "Disabled by the organization.",
            []);
    }

    /// <summary>
    ///     Test that the context window is shown as an Ollama setting: it is hidden while GitHub Copilot is selected, and
    ///     while Ollama is selected a window below the recommended minimum shows a warning under the field.
    /// </summary>
    [AvaloniaFact]
    public void AiProviderSettingsPageView_ContextWindow_IsOllamaOnlyAndWarnsBelowTheRecommendedMinimum()
    {
        // Arrange: a shown page with GitHub Copilot selected
        var (window, viewModel) = ShowPage();
        var warning = SettingsUiHelpers.Find<TextBlock>(window, "AiContextWindowWarning");
        var box = SettingsUiHelpers.Find<TextBox>(window, "AiContextWindowTextBox");
        var label = SettingsUiHelpers.Find<TextBlock>(window, "AiContextWindowLabel");
        var hiddenForCopilot = !box.IsVisible && !label.IsVisible;

        // Act: choose Ollama, type a window just below the minimum, then the minimum
        SettingsUiHelpers.Find<ComboBox>(window, "AiProviderComboBox").SelectedIndex = 1;
        var shownForOllama = box.IsVisible && label.IsVisible;
        box.Text = "16383";
        var warnedBelow = warning.IsVisible;
        var warningText = warning.Text;
        box.Text = "16384";

        // Assert: Copilot hid the entry, Ollama shows it with the warning below the minimum, and the warning is gone at
        // the minimum and never made the page invalid
        Assert.True(hiddenForCopilot);
        Assert.Equal("Context window (tokens)", label.Text);
        Assert.True(shownForOllama);
        Assert.True(warnedBelow);
        Assert.Contains("16,384", warningText, StringComparison.Ordinal);
        Assert.False(warning.IsVisible);
        Assert.True(viewModel.IsValid);

        window.Close();
    }

    /// <summary>
    ///     The automation ids of the entries, labels, notes, and messages that belong to Ollama only.
    /// </summary>
    private static readonly string[] OllamaOnlyIds =
    [
        "AiContextWindowLabel",
        "AiContextWindowTextBox",
        "AiContextWindowError",
        "AiContextWindowWarning",
        "AiOllamaEndpointLabel",
        "AiOllamaEndpointTextBox",
        "AiOllamaEndpointError",
    ];

    /// <summary>
    ///     The automation ids of the entries, labels, notes, and messages that belong to GitHub Copilot only.
    /// </summary>
    private static readonly string[] CopilotOnlyIds =
    [
        "AiCopilotCliPathLabel",
        "AiCopilotCliPathTextBox",
        "AiCopilotCliPathError",
        "AiCopilotCliPathNote",
        "AiEffortCostWarning",
    ];

    /// <summary>
    ///     Test that with GitHub Copilot selected every entry, label, note, and message of Ollama is hidden, even when the
    ///     hidden entries hold values that would produce errors and warnings, and that every Copilot entry is shown.
    /// </summary>
    [AvaloniaFact]
    public async Task AiProviderSettingsPageView_CopilotSelected_ShowsOnlyCopilotControls()
    {
        // Arrange: a page with GitHub Copilot selected, a large model at a high level, and junk in the Ollama entries
        var large = Model("opus", efforts: ["low", "high"]) with { BillingMultiplier = 15, Category = "powerful" };
        var viewModel = new AiProviderSettingsPageViewModel(null, new FixedModels(large), TimeSpan.Zero);
        viewModel.Load(new AppSettings
        {
            AiProvider = new AiProviderSettings { ModelName = "opus", ReasoningEffort = "high", ContextWindowTokens = 4096 },
        });
        await viewModel.ModelListLoaded;
        viewModel.OllamaEndpoint = "junk";
        viewModel.ContextWindowText = "junk";
        var window = new Window { Width = 520, Height = 1300, Content = new AiProviderSettingsPageView { DataContext = viewModel } };
        window.Show();

        // Act: look at every Ollama control and every Copilot control
        var ollama = OllamaOnlyIds.Select(id => (Id: id, Control: SettingsUiHelpers.Find<Control>(window, id))).ToList();
        var copilot = CopilotOnlyIds.Select(id => (Id: id, Control: SettingsUiHelpers.Find<Control>(window, id))).ToList();

        // Assert: no Ollama control is visible or effectively visible, every Copilot control is, and the page is valid
        Assert.True(viewModel.IsCopilotSelected);
        Assert.All(ollama, item => Assert.False(item.Control.IsVisible, $"{item.Id} is visible while GitHub Copilot is selected."));
        Assert.All(ollama, item => Assert.False(item.Control.IsEffectivelyVisible, $"{item.Id} is effectively visible while GitHub Copilot is selected."));
        Assert.All(copilot.Where(item => item.Id != "AiCopilotCliPathError"), item => Assert.True(item.Control.IsVisible, $"{item.Id} is hidden while GitHub Copilot is selected."));
        Assert.All(copilot.Where(item => item.Id != "AiCopilotCliPathError"), item => Assert.True(item.Control.IsEffectivelyVisible, $"{item.Id} is not effectively visible while GitHub Copilot is selected."));
        Assert.False(copilot.Single(item => item.Id == "AiCopilotCliPathError").Control.IsVisible);
        Assert.True(viewModel.IsValid);

        window.Close();
    }

    /// <summary>
    ///     Test that with Ollama selected every entry, label, note, and message of GitHub Copilot is hidden, even while the
    ///     Copilot entries hold values that would produce errors and a warning, and that every Ollama entry is shown.
    /// </summary>
    [AvaloniaFact]
    public async Task AiProviderSettingsPageView_OllamaSelected_ShowsOnlyOllamaControls()
    {
        // Arrange: a page with GitHub Copilot selected whose model list has a large model at a high level, and a path that
        // is not valid
        var large = Model("opus", efforts: ["low", "high"]) with { BillingMultiplier = 15, Category = "powerful" };
        var viewModel = new AiProviderSettingsPageViewModel(null, new FixedModels(large), TimeSpan.Zero);
        viewModel.Load(new AppSettings
        {
            AiProvider = new AiProviderSettings { ModelName = "opus", ReasoningEffort = "high" },
        });
        await viewModel.ModelListLoaded;
        viewModel.CopilotCliPath = "bad\0path";
        var window = new Window { Width = 520, Height = 1300, Content = new AiProviderSettingsPageView { DataContext = viewModel } };
        window.Show();
        var costWarningForCopilot = SettingsUiHelpers.Find<TextBlock>(window, "AiEffortCostWarning").IsEffectivelyVisible;
        var cliErrorForCopilot = SettingsUiHelpers.Find<TextBlock>(window, "AiCopilotCliPathError").IsEffectivelyVisible;

        // Act: choose Ollama, which asks the same source for models, and let the list load; type a bad endpoint
        viewModel.SelectedProvider = viewModel.Providers[1];
        await viewModel.ModelListLoaded;
        viewModel.OllamaEndpoint = "junk";
        var ollama = OllamaOnlyIds.Select(id => (Id: id, Control: SettingsUiHelpers.Find<Control>(window, id))).ToList();
        var copilot = CopilotOnlyIds.Select(id => (Id: id, Control: SettingsUiHelpers.Find<Control>(window, id))).ToList();

        // Assert: the Copilot page had the warning and the error, and with Ollama none of the Copilot controls is visible or
        // effectively visible, while every Ollama control that has something to show is
        Assert.True(costWarningForCopilot);
        Assert.True(cliErrorForCopilot);
        Assert.True(viewModel.IsOllamaSelected);
        Assert.All(copilot, item => Assert.False(item.Control.IsVisible, $"{item.Id} is visible while Ollama is selected."));
        Assert.All(copilot, item => Assert.False(item.Control.IsEffectivelyVisible, $"{item.Id} is effectively visible while Ollama is selected."));
        var always = new[] { "AiContextWindowLabel", "AiContextWindowTextBox", "AiOllamaEndpointLabel", "AiOllamaEndpointTextBox", "AiOllamaEndpointError" };
        Assert.All(ollama.Where(item => always.Contains(item.Id)), item => Assert.True(item.Control.IsEffectivelyVisible, $"{item.Id} is hidden while Ollama is selected."));
        Assert.False(ollama.Single(item => item.Id == "AiContextWindowError").Control.IsVisible);
        Assert.False(viewModel.IsValid);

        window.Close();
    }

    /// <summary>
    ///     Test that no text of the page names the other provider's entry while a provider is selected: the labels of the
    ///     hidden entries are not shown at all.
    /// </summary>
    [AvaloniaFact]
    public void AiProviderSettingsPageView_ProviderSelected_ShowsNoLabelOfTheOtherProvidersEntries()
    {
        // Arrange: a shown page with GitHub Copilot selected
        var (window, viewModel) = ShowPage();
        string[] VisibleTexts() =>
        [
            .. window.GetVisualDescendants().OfType<TextBlock>().Where(block => block.IsEffectivelyVisible).Select(block => block.Text ?? string.Empty),
        ];
        var forCopilot = VisibleTexts();

        // Act: choose Ollama
        viewModel.SelectedProvider = viewModel.Providers[1];
        var forOllama = VisibleTexts();

        // Assert: each provider shows its own labels and none of the other's
        Assert.Contains("GitHub Copilot command-line tool path", forCopilot);
        Assert.DoesNotContain("Ollama endpoint", forCopilot);
        Assert.DoesNotContain(forCopilot, text => text.StartsWith("Context window", StringComparison.Ordinal));
        Assert.Contains("Ollama endpoint", forOllama);
        Assert.Contains("Context window (tokens)", forOllama);
        Assert.DoesNotContain("GitHub Copilot command-line tool path", forOllama);
        Assert.DoesNotContain(forOllama, text => text.Contains("copilot.exe", StringComparison.Ordinal));

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
    /// <param name="provider">The provider that the settings select.</param>
    /// <returns>The window and the page's view model.</returns>
    private static (Window Window, AiProviderSettingsPageViewModel ViewModel) ShowPage(AiProviderKind provider = AiProviderKind.GitHubCopilot)
    {
        var viewModel = new AiProviderSettingsPageViewModel();
        viewModel.Load(new AppSettings { AiProvider = new AiProviderSettings { Provider = provider } });
        var window = new Window { Content = new AiProviderSettingsPageView { DataContext = viewModel } };
        window.Show();
        return (window, viewModel);
    }
}
