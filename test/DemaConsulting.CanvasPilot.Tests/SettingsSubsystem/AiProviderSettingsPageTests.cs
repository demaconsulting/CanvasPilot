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

using DemaConsulting.CanvasPilot.SettingsSubsystem;

namespace DemaConsulting.CanvasPilot.Tests;

/// <summary>
///     Unit tests for <see cref="AiProviderSettingsPageViewModel" />.
/// </summary>
public sealed class AiProviderSettingsPageTests
{
    /// <summary>
    ///     Test that loading default settings shows the documented defaults, with unrestricted agent tools off.
    /// </summary>
    [Fact]
    public void AiProviderSettingsPageViewModel_Load_DefaultSettings_ShowsDefaults()
    {
        // Arrange: a new page
        var page = new AiProviderSettingsPageViewModel();

        // Act: load default settings
        page.Load(new AppSettings());

        // Assert: the defaults are shown and the page is valid
        Assert.Equal("AI Provider", page.Title);
        Assert.Equal(AiProviderKind.GitHubCopilot, page.SelectedProvider.Value);
        Assert.Equal(string.Empty, page.ModelName);
        Assert.Equal("32768", page.ContextWindowText);
        Assert.Equal(ReasoningLevel.Auto, page.SelectedReasoningLevel.Value);
        Assert.Equal("http://localhost:11434", page.OllamaEndpoint);
        Assert.False(page.UnrestrictedAgentTools);
        Assert.Equal(string.Empty, page.CopilotCliPath);
        Assert.True(page.IsValid);
    }

    /// <summary>
    ///     Test that every choice survives a load followed by an apply.
    /// </summary>
    [Fact]
    public void AiProviderSettingsPageViewModel_LoadApply_AllValues_RoundTrips()
    {
        // Arrange: settings with a non-default value everywhere
        var settings = new AppSettings
        {
            AiProvider = new AiProviderSettings
            {
                Provider = AiProviderKind.Ollama,
                ModelName = "llama3",
                ContextWindowTokens = 65536,
                ReasoningLevel = ReasoningLevel.Medium,
                OllamaEndpoint = "https://ollama.example.test",
                UnrestrictedAgentTools = true,
                CopilotCliPath = "C:\\Tools\\copilot.exe",
            },
        };
        var page = new AiProviderSettingsPageViewModel();
        page.Load(settings);

        // Act: apply onto default settings
        var applied = page.Apply(new AppSettings());

        // Assert: every value was carried over
        Assert.Equal(settings.AiProvider, applied.AiProvider);
    }

    /// <summary>
    ///     Test that edited values are applied trimmed.
    /// </summary>
    [Fact]
    public void AiProviderSettingsPageViewModel_Apply_EditedValues_AreAppliedTrimmed()
    {
        // Arrange: a page with padded entries
        var page = new AiProviderSettingsPageViewModel();
        page.Load(new AppSettings());
        page.SelectedProvider = page.Providers[1];
        page.SelectedReasoningLevel = page.ReasoningLevels[3];
        page.ModelName = "  model-x  ";
        page.ContextWindowText = " 4096 ";
        page.OllamaEndpoint = "  http://server:1234  ";
        page.UnrestrictedAgentTools = true;

        // Act: apply
        var applied = page.Apply(new AppSettings()).AiProvider;

        // Assert: values are trimmed and typed
        Assert.Equal(AiProviderKind.Ollama, applied.Provider);
        Assert.Equal(ReasoningLevel.High, applied.ReasoningLevel);
        Assert.Equal("model-x", applied.ModelName);
        Assert.Equal(4096, applied.ContextWindowTokens);
        Assert.Equal("http://server:1234", applied.OllamaEndpoint);
        Assert.True(applied.UnrestrictedAgentTools);
    }

    /// <summary>
    ///     Test that applying leaves values owned by other pages unchanged.
    /// </summary>
    [Fact]
    public void AiProviderSettingsPageViewModel_Apply_OtherSettings_AreUnchanged()
    {
        // Arrange: settings holding recent projects and speech values
        var other = new AppSettings
        {
            RecentProjects = ["C:\\a"],
            Speech = new SpeechSettings { CaptureDevice = "mic" },
        };
        var page = new AiProviderSettingsPageViewModel();
        page.Load(other);
        page.ModelName = "changed";

        // Act: apply
        var applied = page.Apply(other);

        // Assert: only the AI provider section changed
        Assert.Equal(other.RecentProjects, applied.RecentProjects);
        Assert.Same(other.Speech, applied.Speech);
        Assert.Equal("changed", applied.AiProvider.ModelName);
    }

    /// <summary>
    ///     Test that a context window outside the bounds or not a whole number is invalid and reported.
    /// </summary>
    /// <param name="text">The text typed.</param>
    [Theory]
    [InlineData("511")]
    [InlineData("2000001")]
    [InlineData("abc")]
    [InlineData("")]
    [InlineData("-5")]
    [InlineData("1.5")]
    [InlineData("8,192")]
    public void AiProviderSettingsPageViewModel_ContextWindowText_OutOfBounds_IsInvalid(string text)
    {
        // Arrange: a loaded page
        var page = new AiProviderSettingsPageViewModel();
        page.Load(new AppSettings());

        // Act: type the text
        page.ContextWindowText = text;

        // Assert: the page is invalid with an error message that states the bounds
        Assert.False(page.IsValid);
        Assert.Contains("512", page.ContextWindowError);
        Assert.Contains("2000000", page.ContextWindowError);
    }

    /// <summary>
    ///     Test that the smallest and largest context windows are accepted.
    /// </summary>
    /// <param name="text">The text typed.</param>
    [Theory]
    [InlineData("512")]
    [InlineData("2000000")]
    public void AiProviderSettingsPageViewModel_ContextWindowText_AtBounds_IsValid(string text)
    {
        // Arrange: a loaded page
        var page = new AiProviderSettingsPageViewModel();
        page.Load(new AppSettings());

        // Act: type the text
        page.ContextWindowText = text;

        // Assert: the page is valid and applies the value
        Assert.True(page.IsValid);
        Assert.Equal(int.Parse(text, System.Globalization.CultureInfo.InvariantCulture), page.Apply(new AppSettings()).AiProvider.ContextWindowTokens);
    }

    /// <summary>
    ///     Test that applying an invalid page is refused.
    /// </summary>
    [Fact]
    public void AiProviderSettingsPageViewModel_Apply_InvalidEntries_ThrowsInvalidOperationException()
    {
        // Arrange: a page with an invalid context window
        var page = new AiProviderSettingsPageViewModel();
        page.Load(new AppSettings());
        page.ContextWindowText = "1";

        // Act / Assert: applying is refused
        Assert.Throws<InvalidOperationException>(() => page.Apply(new AppSettings()));
    }

    /// <summary>
    ///     Test that a changing validity is reported through property change notification.
    /// </summary>
    [Fact]
    public void AiProviderSettingsPageViewModel_ContextWindowText_Edited_RaisesIsValidChanged()
    {
        // Arrange: a loaded page and a listener
        var page = new AiProviderSettingsPageViewModel();
        page.Load(new AppSettings());
        var names = new List<string?>();
        page.PropertyChanged += (_, e) => names.Add(e.PropertyName);

        // Act: type an invalid value
        page.ContextWindowText = "x";

        // Assert: validity and the error were announced
        Assert.Contains(nameof(AiProviderSettingsPageViewModel.IsValid), names);
        Assert.Contains(nameof(AiProviderSettingsPageViewModel.ContextWindowError), names);
    }

    /// <summary>
    ///     Test that an invalid Ollama endpoint is an error only while Ollama is selected.
    /// </summary>
    /// <param name="endpoint">The endpoint typed.</param>
    [Theory]
    [InlineData("")]
    [InlineData("localhost:11434")]
    [InlineData("ftp://server")]
    [InlineData("not a url")]
    public void AiProviderSettingsPageViewModel_OllamaEndpoint_InvalidWhenOllamaSelected_IsInvalid(string endpoint)
    {
        // Arrange: a loaded page with Ollama selected
        var page = new AiProviderSettingsPageViewModel();
        page.Load(new AppSettings());
        page.SelectedProvider = page.Providers[1];

        // Act: type the endpoint
        page.OllamaEndpoint = endpoint;

        // Assert: the page is invalid and says why
        Assert.True(page.IsOllamaSelected);
        Assert.False(page.IsValid);
        Assert.NotEmpty(page.OllamaEndpointError);
    }

    /// <summary>
    ///     Test that an invalid endpoint is ignored while another provider is selected, and the stored endpoint is
    ///     kept.
    /// </summary>
    [Fact]
    public void AiProviderSettingsPageViewModel_OllamaEndpoint_InvalidWhenCopilotSelected_IsIgnoredAndStoredValueKept()
    {
        // Arrange: a loaded page with Copilot selected and a junk endpoint typed
        var stored = new AppSettings { AiProvider = new AiProviderSettings { OllamaEndpoint = "http://stored:1" } };
        var page = new AiProviderSettingsPageViewModel();
        page.Load(stored);
        page.OllamaEndpoint = "junk";

        // Act: apply
        var applied = page.Apply(stored);

        // Assert: the page is valid, the endpoint is not editable in effect, and the stored value is kept
        Assert.False(page.IsOllamaSelected);
        Assert.True(page.IsValid);
        Assert.Equal("http://stored:1", applied.AiProvider.OllamaEndpoint);
    }

    /// <summary>
    ///     Test that selecting a provider switches which provider-specific entry is enabled.
    /// </summary>
    [Fact]
    public void AiProviderSettingsPageViewModel_SelectedProvider_Switched_EnablesMatchingEntry()
    {
        // Arrange: a loaded page, which defaults to Copilot
        var page = new AiProviderSettingsPageViewModel();
        page.Load(new AppSettings());
        Assert.True(page.IsCopilotSelected);
        Assert.False(page.IsOllamaSelected);

        // Act: select Ollama
        page.SelectedProvider = page.Providers[1];

        // Assert: Ollama's entry is enabled and Copilot's is not
        Assert.True(page.IsOllamaSelected);
        Assert.False(page.IsCopilotSelected);
    }

    /// <summary>
    ///     Test that the Copilot tool path is applied trimmed and an empty path clears the override.
    /// </summary>
    [Fact]
    public void AiProviderSettingsPageViewModel_CopilotCliPath_EditedAndCleared_IsApplied()
    {
        // Arrange: a loaded page with a stored override
        var stored = new AppSettings { AiProvider = new AiProviderSettings { CopilotCliPath = "C:\\old\\copilot.exe" } };
        var page = new AiProviderSettingsPageViewModel();
        page.Load(stored);

        // Act: edit the path, apply, then clear it and apply again
        page.CopilotCliPath = "  C:\\new\\copilot.exe ";
        var edited = page.Apply(stored).AiProvider.CopilotCliPath;
        page.CopilotCliPath = string.Empty;
        var cleared = page.Apply(stored).AiProvider.CopilotCliPath;

        // Assert: the edit was trimmed and the clear removed the override
        Assert.Equal("C:\\new\\copilot.exe", edited);
        Assert.Equal(string.Empty, cleared);
    }

    /// <summary>
    ///     Test that a path with invalid characters is an error only while Copilot is selected.
    /// </summary>
    [Fact]
    public void AiProviderSettingsPageViewModel_CopilotCliPath_InvalidCharacters_IsErrorOnlyForCopilot()
    {
        // Arrange: a loaded page with Copilot selected
        var page = new AiProviderSettingsPageViewModel();
        page.Load(new AppSettings());

        // Act: type a path with a character no path can contain
        page.CopilotCliPath = "C:\\bad\0path";
        var invalidForCopilot = !page.IsValid;
        page.SelectedProvider = page.Providers[1];

        // Assert: invalid for Copilot, and ignored once Ollama is selected
        Assert.True(invalidForCopilot);
        Assert.True(page.IsValid);
        Assert.Equal(string.Empty, page.CopilotCliPathError);
    }

    /// <summary>
    ///     Test that unrestricted agent tools stay off unless explicitly switched on.
    /// </summary>
    [Fact]
    public void AiProviderSettingsPageViewModel_UnrestrictedAgentTools_NotChosen_StaysOff()
    {
        // Arrange: a loaded page with default settings and edits to unrelated values
        var page = new AiProviderSettingsPageViewModel();
        page.Load(new AppSettings());
        page.ModelName = "anything";

        // Act: apply without touching the opt-in
        var applied = page.Apply(new AppSettings());

        // Assert: the opt-in remains off
        Assert.False(page.UnrestrictedAgentTools);
        Assert.False(applied.AiProvider.UnrestrictedAgentTools);
    }

    /// <summary>
    ///     Test that the stored value of the unrestricted agent tools flag, which the page does not offer, is carried
    ///     through a load and an apply unchanged, together with the reasoning level that the page shows disabled.
    /// </summary>
    [Fact]
    public void AiProviderSettingsPageViewModel_UnrestrictedAgentTools_Stored_IsKeptUnchanged()
    {
        // Arrange: settings with the flag on and a reasoning level, and a page that edits another value
        var stored = new AppSettings
        {
            AiProvider = new AiProviderSettings { UnrestrictedAgentTools = true, ReasoningLevel = ReasoningLevel.High },
        };
        var page = new AiProviderSettingsPageViewModel();
        page.Load(stored);
        page.ModelName = "other";

        // Act: apply
        var applied = page.Apply(stored).AiProvider;

        // Assert: both values that the page does not let the user change are as they were
        Assert.True(applied.UnrestrictedAgentTools);
        Assert.Equal(ReasoningLevel.High, applied.ReasoningLevel);
        Assert.Equal("other", applied.ModelName);
    }

    /// <summary>
    ///     Test that the page says why the reasoning level cannot be chosen, and where the CLI is looked for.
    /// </summary>
    [Fact]
    public void AiProviderSettingsPageViewModel_Notes_ReasoningAndCliPath_ExplainTheLimits()
    {
        // Assert: the reasoning note is the agreed sentence, and the CLI note names the executable and the minimum version
        Assert.Equal("Not available yet: the AI library does not expose it", AiProviderSettingsPageViewModel.ReasoningLevelNoteText);
        Assert.Contains("copilot.exe", AiProviderSettingsPageViewModel.CopilotCliPathNoteText, StringComparison.Ordinal);
        Assert.Contains("1.0.113", AiProviderSettingsPageViewModel.CopilotCliPathNoteText, StringComparison.Ordinal);
        Assert.Contains("shims", AiProviderSettingsPageViewModel.CopilotCliPathNoteText, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Test that the minimum version in the note of the CLI path is the one that the locator of the Agent subsystem
    ///     checks, so that the two cannot drift apart.
    /// </summary>
    [Fact]
    public void AiProviderSettingsPageViewModel_CopilotCliPathNote_MinimumVersion_IsTheLocatorsMinimum()
    {
        // Arrange / Act: the note of the page
        var note = AiProviderSettingsPageViewModel.CopilotCliPathNoteText;

        // Assert: it names the version that the locator requires
        Assert.Contains(DemaConsulting.CanvasPilot.AgentSubsystem.CopilotCliLocator.MinimumCliVersion.ToString(), note, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Test that a context window below the recommended minimum is warned about while Ollama is selected, that the
    ///     recommended minimum itself is not, and that the warning does not make the page invalid.
    /// </summary>
    [Fact]
    public void AiProviderSettingsPageViewModel_ContextWindowWarning_BelowRecommendedMinimum_WarnsWithoutInvalidating()
    {
        // Arrange: a page with Ollama selected
        var page = new AiProviderSettingsPageViewModel();
        page.Load(new AppSettings { AiProvider = new AiProviderSettings { Provider = AiProviderKind.Ollama } });
        var names = new List<string?>();
        page.PropertyChanged += (_, e) => names.Add(e.PropertyName);

        // Act: type the value just below the minimum, the minimum, and the default
        page.ContextWindowText = "16383";
        var below = page.ContextWindowWarning;
        var belowValid = page.IsValid;
        page.ContextWindowText = "16384";
        var atMinimum = page.ContextWindowWarning;
        page.ContextWindowText = AiProviderSettings.DefaultContextWindowTokens.ToString(System.Globalization.CultureInfo.InvariantCulture);
        var atDefault = page.ContextWindowWarning;

        // Assert: only the smaller value is warned about, with the recommendation, and it stays valid and announced
        Assert.Contains("16,384", below, StringComparison.Ordinal);
        Assert.Contains("32,768", below, StringComparison.Ordinal);
        Assert.True(belowValid);
        Assert.Empty(page.ContextWindowError);
        Assert.Equal(string.Empty, atMinimum);
        Assert.Equal(string.Empty, atDefault);
        Assert.Contains(nameof(AiProviderSettingsPageViewModel.ContextWindowWarning), names);
    }

    /// <summary>
    ///     Test that an invalid context window gets an error and no warning, and that the context window is explained as
    ///     Ollama only: no warning while GitHub Copilot is selected, and a note instead.
    /// </summary>
    [Fact]
    public void AiProviderSettingsPageViewModel_ContextWindowWarning_InvalidOrCopilot_GivesNoWarning()
    {
        // Arrange: a page with Ollama selected and a value that is not valid
        var page = new AiProviderSettingsPageViewModel();
        page.Load(new AppSettings { AiProvider = new AiProviderSettings { Provider = AiProviderKind.Ollama } });
        page.ContextWindowText = "0";
        var invalidWarning = page.ContextWindowWarning;
        var invalidError = page.ContextWindowError;
        var ollamaNote = page.ContextWindowNote;

        // Act: a small valid window, with GitHub Copilot selected
        page.ContextWindowText = "8192";
        page.SelectedProvider = page.Providers[0];

        // Assert: the invalid entry is an error only, the Ollama page has no note, and Copilot has the note and no warning
        Assert.Equal(string.Empty, invalidWarning);
        Assert.NotEmpty(invalidError);
        Assert.Equal(string.Empty, ollamaNote);
        Assert.Equal(string.Empty, page.ContextWindowWarning);
        Assert.Contains("Ollama only", page.ContextWindowNote, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Test that a page without a provider check offers no connection check.
    /// </summary>
    [Fact]
    public void AiProviderSettingsPageViewModel_CheckConnection_NoProviderCheck_IsNotOffered()
    {
        // Arrange: a page without a check
        var page = new AiProviderSettingsPageViewModel();
        page.Load(new AppSettings());

        // Assert: the button is not offered and cannot run
        Assert.False(page.HasConnectionCheck);
        Assert.False(page.CanCheckConnection);
        Assert.False(page.CheckConnectionCommand.CanExecute(null));
    }

    /// <summary>
    ///     Test that Check connection gives the entries that are on the page, merged into the loaded settings and not saved,
    ///     to the provider check and shows its answer, which an edit of an entry then removes.
    /// </summary>
    [Fact]
    public async Task AiProviderSettingsPageViewModel_CheckConnection_ValidEntries_ChecksTheEntriesAndShowsTheAnswer()
    {
        // Arrange: a page over a check that records what it is given, with entries that were edited and not applied
        var check = new RecordingCheck(new ProviderCheckResult(true, "Ollama is ready."));
        var page = new AiProviderSettingsPageViewModel(check);
        page.Load(new AppSettings { AiProvider = new AiProviderSettings { UnrestrictedAgentTools = true } });
        page.SelectedProvider = page.Providers[1];
        page.ModelName = " gemma3 ";
        page.ContextWindowText = "8192";

        // Act: check the connection, then edit an entry
        await page.CheckConnectionCommand.ExecuteAsync(null);
        var answer = page.CheckConnectionResult;
        var ok = page.IsCheckConnectionOk;
        var failed = page.IsCheckConnectionFailed;
        page.ModelName = "other";

        // Assert: the check saw the edited entries with the stored values it does not own, its answer was shown as a
        // success, and the answer went away with the edit
        var given = Assert.Single(check.Received);
        Assert.Equal(AiProviderKind.Ollama, given.Provider);
        Assert.Equal("gemma3", given.ModelName);
        Assert.Equal(8192, given.ContextWindowTokens);
        Assert.True(given.UnrestrictedAgentTools);
        Assert.Equal("Ollama is ready.", answer);
        Assert.True(ok);
        Assert.False(failed);
        Assert.True(page.HasConnectionCheck);
        Assert.Equal(string.Empty, page.CheckConnectionResult);
        Assert.False(page.HasCheckConnectionResult);
    }

    /// <summary>
    ///     Test that a provider that cannot be used is shown as a failure, and that the button is not available while the
    ///     entries are invalid or a check runs.
    /// </summary>
    [Fact]
    public async Task AiProviderSettingsPageViewModel_CheckConnection_FailureAndInvalidEntries_ShowFailureAndBlockTheButton()
    {
        // Arrange: a page over a check that fails, and a gate that holds the answer back
        var check = new RecordingCheck(new ProviderCheckResult(false, "Not signed in."));
        var page = new AiProviderSettingsPageViewModel(check);
        page.Load(new AppSettings());

        // Act: check, then make the entries invalid, then restore them and start a check that is held
        await page.CheckConnectionCommand.ExecuteAsync(null);
        var failed = page.IsCheckConnectionFailed;
        var failedText = page.CheckConnectionResult;
        page.ContextWindowText = "x";
        var invalidBlocked = !page.CheckConnectionCommand.CanExecute(null);
        page.ContextWindowText = "32768";
        check.Gate = new TaskCompletionSource();
        var running = page.CheckConnectionCommand.ExecuteAsync(null);
        var duringCheckText = page.CheckConnectionResult;
        var duringCheckBlocked = !page.CheckConnectionCommand.CanExecute(null);
        var duringCheckFailed = page.IsCheckConnectionFailed;
        check.Gate.SetResult();
        await running;

        // Assert: the failure was shown as one, and the button was blocked by invalid entries and by the running check
        Assert.True(failed);
        Assert.Equal("Not signed in.", failedText);
        Assert.True(invalidBlocked);
        Assert.Equal(AiProviderSettingsPageViewModel.CheckingText, duringCheckText);
        Assert.True(duringCheckBlocked);
        Assert.False(duringCheckFailed);
        Assert.False(page.IsCheckingConnection);
        Assert.Equal("Not signed in.", page.CheckConnectionResult);
    }

    /// <summary>
    ///     Test that a check that throws is shown as a sentence, and that an answer that arrives after the dialog was
    ///     closed or after an entry was edited is dropped.
    /// </summary>
    [Fact]
    public async Task AiProviderSettingsPageViewModel_CheckConnection_ThrowsOrIsOvertaken_ShowsSentenceOrDropsAnswer()
    {
        // Arrange: a page over a check that throws, and one over a check that is held
        var throwing = new AiProviderSettingsPageViewModel(new RecordingCheck(new ProviderCheckResult(true, "unused")) { Failure = new InvalidOperationException("boom") });
        throwing.Load(new AppSettings());
        var held = new RecordingCheck(new ProviderCheckResult(true, "Late answer.")) { Gate = new TaskCompletionSource() };
        var overtaken = new AiProviderSettingsPageViewModel(held);
        overtaken.Load(new AppSettings());

        // Act: check with the throwing check, then start a held check and close the page before it answers
        await throwing.CheckConnectionCommand.ExecuteAsync(null);
        var running = overtaken.CheckConnectionCommand.ExecuteAsync(null);
        overtaken.OnClosed();
        held.Gate.SetResult();
        await running;

        // Assert: the failure is a sentence, and the late answer was not shown
        Assert.Equal("The connection could not be checked: boom", throwing.CheckConnectionResult);
        Assert.False(throwing.IsCheckConnectionOk);
        Assert.Equal(string.Empty, overtaken.CheckConnectionResult);
        Assert.False(overtaken.IsCheckingConnection);
        Assert.True(held.WasCancelled);
    }

    /// <summary>
    ///     A provider check that records what it is given and gives a fixed answer, which can be held back or made to fail.
    /// </summary>
    private sealed class RecordingCheck(ProviderCheckResult result) : IProviderCheck
    {
        /// <summary>Gets the settings that the check was given, in order.</summary>
        public List<AiProviderSettings> Received { get; } = [];

        /// <summary>Gets or sets a task that holds the answer back until it completes.</summary>
        public TaskCompletionSource? Gate { get; set; }

        /// <summary>Gets or sets an exception that the check throws instead of answering.</summary>
        public Exception? Failure { get; init; }

        /// <summary>Gets a value indicating whether the token of the check was cancelled while it waited.</summary>
        public bool WasCancelled { get; private set; }

        /// <inheritdoc />
        public async Task<ProviderCheckResult> CheckAsync(AiProviderSettings settings, CancellationToken cancellationToken = default)
        {
            Received.Add(settings);
            if (Failure is not null)
            {
                throw Failure;
            }

            if (Gate is not null)
            {
                await Gate.Task;
                WasCancelled = cancellationToken.IsCancellationRequested;
            }

            return result;
        }
    }

    /// <summary>
    ///     Test that a provider check that the Settings subsystem owns can be implemented without any type of the Agent
    ///     subsystem, and that its result compares by value.
    /// </summary>
    [Fact]
    public async Task ProviderCheck_CheckAsync_Implementation_ReturnsResultForTheSettingsGiven()
    {
        // Arrange: a check that looks only at the settings it is given
        var token = TestContext.Current.CancellationToken;
        IProviderCheck check = new ContextWindowCheck();

        // Act: check settings with a small and with the default context window
        var small = await check.CheckAsync(new AiProviderSettings { ContextWindowTokens = 8192 }, token);
        var normal = await check.CheckAsync(new AiProviderSettings(), token);

        // Assert: the results follow the settings and compare by value
        Assert.Equal(new ProviderCheckResult(false, "Too small."), small);
        Assert.Equal(new ProviderCheckResult(true, "Fine."), normal);
    }

    /// <summary>
    ///     A provider check that depends only on the settings.
    /// </summary>
    private sealed class ContextWindowCheck : IProviderCheck
    {
        /// <inheritdoc />
        public Task<ProviderCheckResult> CheckAsync(AiProviderSettings settings, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(
                settings.ContextWindowTokens < AiProviderSettings.RecommendedMinimumContextTokens
                    ? new ProviderCheckResult(false, "Too small.")
                    : new ProviderCheckResult(true, "Fine."));
        }
    }
}
