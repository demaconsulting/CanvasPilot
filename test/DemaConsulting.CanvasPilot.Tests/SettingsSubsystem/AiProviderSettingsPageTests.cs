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
        Assert.Equal(string.Empty, page.SelectedReasoningEffort!.Value);
        Assert.False(page.IsReasoningEffortEnabled);
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
                ReasoningEffort = "medium",
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
        page.ModelName = "  model-x  ";
        page.ContextWindowText = " 4096 ";
        page.OllamaEndpoint = "  http://server:1234  ";
        page.UnrestrictedAgentTools = true;

        // Act: apply
        var applied = page.Apply(new AppSettings()).AiProvider;

        // Assert: values are trimmed and typed
        Assert.Equal(AiProviderKind.Ollama, applied.Provider);
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
        // Arrange: a loaded page with Ollama selected, the provider that the context window belongs to
        var page = new AiProviderSettingsPageViewModel();
        page.Load(new AppSettings { AiProvider = new AiProviderSettings { Provider = AiProviderKind.Ollama } });

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
        // Arrange: a page with Ollama selected and an invalid context window
        var page = new AiProviderSettingsPageViewModel();
        page.Load(new AppSettings { AiProvider = new AiProviderSettings { Provider = AiProviderKind.Ollama } });
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
        // Arrange: a loaded page with Ollama selected and a listener
        var page = new AiProviderSettingsPageViewModel();
        page.Load(new AppSettings { AiProvider = new AiProviderSettings { Provider = AiProviderKind.Ollama } });
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
    ///     through a load and an apply unchanged, together with a reasoning effort of a model that is not known.
    /// </summary>
    [Fact]
    public void AiProviderSettingsPageViewModel_UnrestrictedAgentTools_Stored_IsKeptUnchanged()
    {
        // Arrange: settings with the flag on and a reasoning effort, and a page that edits another value
        var stored = new AppSettings
        {
            AiProvider = new AiProviderSettings { UnrestrictedAgentTools = true, ModelName = "typed", ReasoningEffort = "high" },
        };
        var page = new AiProviderSettingsPageViewModel();
        page.Load(stored);
        page.ContextWindowText = "4096";

        // Act: apply
        var applied = page.Apply(stored).AiProvider;

        // Assert: both values that the page does not let the user change are as they were
        Assert.True(applied.UnrestrictedAgentTools);
        Assert.Equal("high", applied.ReasoningEffort);
        Assert.Equal(4096, applied.ContextWindowTokens);
    }

    /// <summary>
    ///     Test that the note of the CLI path names the executable, the minimum version, and the shims.
    /// </summary>
    [Fact]
    public void AiProviderSettingsPageViewModel_Notes_CliPath_ExplainTheLimits()
    {
        // Assert: the CLI note names the executable and the minimum version
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
    ///     Test that an invalid context window gets an error and no warning while Ollama is selected, and that while GitHub
    ///     Copilot is selected, which the entry does not belong to, there is neither a warning nor an error.
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

        // Act: a small valid window, with GitHub Copilot selected
        page.ContextWindowText = "8192";
        page.SelectedProvider = page.Providers[0];

        // Assert: the invalid entry is an error only, and Copilot has no warning for it
        Assert.Equal(string.Empty, invalidWarning);
        Assert.NotEmpty(invalidError);
        Assert.Equal(string.Empty, page.ContextWindowWarning);
        Assert.Equal(string.Empty, page.ContextWindowError);
    }

    /// <summary>
    ///     Test that entries that belong to the provider that is not selected never block the page: a context window that is
    ///     not valid while GitHub Copilot is selected is not an error, and applying keeps the stored value.
    /// </summary>
    [Fact]
    public void AiProviderSettingsPageViewModel_ContextWindowText_InvalidWhileCopilotSelected_IsIgnoredAndStoredValueKept()
    {
        // Arrange: a page with GitHub Copilot selected, a stored window, and junk typed in the hidden window entry
        var stored = new AppSettings { AiProvider = new AiProviderSettings { ContextWindowTokens = 65536 } };
        var page = new AiProviderSettingsPageViewModel();
        page.Load(stored);
        page.ContextWindowText = "junk";

        // Act: apply, then choose Ollama, for which the same text is an error
        var applied = page.Apply(stored);
        var validForCopilot = page.IsValid;
        page.SelectedProvider = page.Providers[1];

        // Assert: the page was valid and kept the stored window, and the entry blocks the page for Ollama only
        Assert.True(validForCopilot);
        Assert.Equal(65536, applied.AiProvider.ContextWindowTokens);
        Assert.False(page.IsValid);
        Assert.NotEmpty(page.ContextWindowError);
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
        page.Load(new AppSettings { AiProvider = new AiProviderSettings { Provider = AiProviderKind.Ollama } });

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

    /// <summary>
    ///     A source of models whose answers the test sets, which can hold an answer back until the test lets it go and
    ///     records what it was asked.
    /// </summary>
    private sealed class FakeModelSource : IProviderModelSource
    {
        /// <summary>Gets or sets the models to answer with.</summary>
        public List<ProviderModelInfo> Models { get; set; } = [];

        /// <summary>Gets or sets the message to answer with.</summary>
        public string Message { get; set; } = string.Empty;

        /// <summary>Gets or sets the exception that the source throws instead of answering.</summary>
        public Exception? Failure { get; set; }

        /// <summary>Gets or sets a task that the source waits for before it answers, or <see langword="null" /> for no wait.</summary>
        public TaskCompletionSource? Gate { get; set; }

        /// <summary>Gets the settings and the refresh flag of each question.</summary>
        public List<(AiProviderSettings Settings, bool Refresh)> Questions { get; } = [];

        /// <summary>Gets a value indicating whether the token of the last question was cancelled when the gate opened.</summary>
        public bool LastWasCancelled { get; private set; }

        /// <inheritdoc />
        public async Task<ProviderModelList> ListModelsAsync(AiProviderSettings settings, bool refresh = false, CancellationToken cancellationToken = default)
        {
            Questions.Add((settings, refresh));
            var answer = new ProviderModelList([.. Models], Message);
            if (Failure is not null)
            {
                throw Failure;
            }

            if (Gate is not null)
            {
                await Gate.Task;
                LastWasCancelled = cancellationToken.IsCancellationRequested;
            }

            return answer;
        }
    }

    /// <summary>
    ///     Makes a model of the provider.
    /// </summary>
    /// <param name="id">The id of the model.</param>
    /// <param name="tools">Whether the model can call tools.</param>
    /// <param name="vision">Whether the model accepts pictures.</param>
    /// <param name="efforts">The ids of the efforts that the model offers.</param>
    /// <param name="usable">Whether the model can be used.</param>
    /// <param name="name">The name to show; the id when <see langword="null" />.</param>
    /// <returns>The model.</returns>
    private static ProviderModelInfo Model(string id, bool tools = true, bool vision = true, string[]? efforts = null, bool usable = true, string? name = null)
    {
        var offered = efforts ?? [];
        return new ProviderModelInfo(
            id,
            name ?? id,
            tools,
            vision,
            offered.Length > 0,
            200_000,
            [.. offered.Select(effort => new ProviderEffortChoice(effort, effort.ToUpperInvariant()))],
            null,
            usable,
            usable ? null : "Disabled by your organization.",
            []);
    }

    /// <summary>
    ///     Makes a page over the source with no delay for typing, loaded with the settings, and waits for the list.
    /// </summary>
    /// <param name="source">The source of the models.</param>
    /// <param name="ai">The AI provider settings to load.</param>
    /// <returns>The page.</returns>
    private static async Task<AiProviderSettingsPageViewModel> LoadedPage(FakeModelSource source, AiProviderSettings? ai = null)
    {
        var page = new AiProviderSettingsPageViewModel(null, source, TimeSpan.Zero);
        page.Load(new AppSettings { AiProvider = ai ?? new AiProviderSettings() });
        await page.ModelListLoaded;
        return page;
    }

    /// <summary>
    ///     Test that a page loads the models of the provider when it is shown, offers the default entry first, chooses the
    ///     stored model in the list, and shows what the model can do.
    /// </summary>
    [Fact]
    public async Task AiProviderSettingsPageViewModel_Load_WithModelSource_ListsTheModelsAndChoosesTheStoredOne()
    {
        // Arrange: a provider with two models, one of which is stored in the settings
        var source = new FakeModelSource { Models = [Model("alpha", efforts: ["low", "high"]), Model("beta", vision: false)] };

        // Act
        var page = await LoadedPage(source, new AiProviderSettings { ModelName = "alpha" });

        // Assert: the default entry leads, the stored model is chosen, and its capabilities are shown
        Assert.True(page.HasModelSource);
        Assert.True(page.HasModelList);
        Assert.Equal(["", "alpha", "beta"], page.Models.Select(choice => choice.Id).ToArray());
        Assert.Equal(AiProviderSettingsPageViewModel.DefaultModelLabel, page.Models[0].Label);
        Assert.Equal("alpha", page.SelectedModel!.Id);
        Assert.Equal(string.Empty, page.ModelListMessage);
        Assert.False(page.IsLoadingModels);
        Assert.Equal("Tool calling: yes\nPictures: yes\nReasoning levels: low, high\nContext window: up to 200,000 tokens", page.ModelDetails);
        Assert.Equal(string.Empty, page.ModelWarning);
        var question = Assert.Single(source.Questions);
        Assert.Equal((AiProviderKind.GitHubCopilot, false), (question.Settings.Provider, question.Refresh));
    }

    /// <summary>
    ///     Test that every model is listed and that what a model lacks is shown: a note in the list, and a warning under it
    ///     that says that a model without tool calling cannot be used, that a model without pictures cannot see, and why a
    ///     model that cannot be used is disabled.
    /// </summary>
    [Fact]
    public async Task AiProviderSettingsPageViewModel_ModelWithoutCapabilities_IsListedWithNoteAndWarning()
    {
        // Arrange: models that lack tools, pictures, or availability
        var source = new FakeModelSource
        {
            Models = [Model("full"), Model("blind", vision: false), Model("no-tools", tools: false), Model("off", usable: false, name: "Off Model")],
        };
        var page = await LoadedPage(source);

        // Act: choose each model by typing its name, which is how the choice is made when the list cannot be clicked
        page.ModelName = "blind";
        var blindWarning = page.ModelWarning;
        page.ModelName = "no-tools";
        var toolsWarning = page.ModelWarning;
        page.ModelName = "off";
        var offWarning = page.ModelWarning;
        page.ModelName = "full";
        var fullWarning = page.ModelWarning;

        // Assert: the list holds all of them with notes, the warnings name the problem, and a full model has none
        var labels = page.Models.ToDictionary(choice => choice.Id, choice => choice.Label);
        Assert.Equal("blind - no pictures", labels["blind"]);
        Assert.Equal("no-tools - no tool calling", labels["no-tools"]);
        Assert.Equal("Off Model (off) - unavailable", labels["off"]);
        Assert.Equal("full", labels["full"]);
        Assert.Contains("does not accept pictures", blindWarning, StringComparison.Ordinal);
        Assert.Contains("cannot call tools", toolsWarning, StringComparison.Ordinal);
        Assert.Contains("cannot use this model", toolsWarning, StringComparison.Ordinal);
        Assert.Contains("Off Model cannot be used: Disabled by your organization.", offWarning, StringComparison.Ordinal);
        Assert.Equal(string.Empty, fullWarning);
        Assert.False(page.Models.Single(choice => choice.Id == "off").IsSelectable);
        Assert.True(page.Models.Single(choice => choice.Id == "blind").IsSelectable);
    }

    /// <summary>
    ///     Test that a choice in the list is the model name that is applied, that a model name that is typed chooses the
    ///     entry of the list (a name without a tag stands for the latest tag), and that a name that is not in the list is
    ///     kept, with a note, and is not a choice of the list.
    /// </summary>
    [Fact]
    public async Task AiProviderSettingsPageViewModel_ModelChoiceAndTypedName_StayInStep()
    {
        // Arrange: a list with a tagged model
        var source = new FakeModelSource { Models = [Model("gemma3:latest"), Model("other:7b")] };
        var page = await LoadedPage(source);

        // Act: choose in the list, type a name without a tag, type a name that is not listed, and choose the default entry
        page.SelectedModel = page.Models.Single(choice => choice.Id == "other:7b");
        var applied = page.Apply(new AppSettings()).AiProvider.ModelName;
        page.ModelName = "gemma3";
        var byTyping = page.SelectedModel?.Id;
        page.ModelName = "unlisted";
        var unlistedChoice = page.SelectedModel;
        var unlistedDetails = page.ModelDetails;
        var unlistedApplied = page.Apply(new AppSettings()).AiProvider.ModelName;
        page.SelectedModel = page.Models[0];

        // Assert
        Assert.Equal("other:7b", applied);
        Assert.Equal("gemma3:latest", byTyping);
        Assert.Null(unlistedChoice);
        Assert.Contains("unlisted is not in the list", unlistedDetails, StringComparison.Ordinal);
        Assert.Equal("unlisted", unlistedApplied);
        Assert.Equal(string.Empty, page.ModelName);
        Assert.Contains("default model of the provider", page.ModelDetails, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Test that a model that is not usable cannot be chosen from the list, which would otherwise store a model that
    ///     cannot be used.
    /// </summary>
    [Fact]
    public async Task AiProviderSettingsPageViewModel_ChoosingAnUnusableModel_IsNotTakenAsTheModelName()
    {
        // Arrange
        var source = new FakeModelSource { Models = [Model("good"), Model("off", usable: false)] };
        var page = await LoadedPage(source, new AiProviderSettings { ModelName = "good" });

        // Act: the list offers the disabled entry
        page.SelectedModel = page.Models.Single(choice => choice.Id == "off");

        // Assert: the name stays
        Assert.Equal("good", page.ModelName);
    }

    /// <summary>
    ///     Test that the reasoning level is enabled only while the chosen model offers levels, that its entries are the ids
    ///     of the model (not a fixed set), that the chosen level is applied, and that a model without levels clears it.
    /// </summary>
    [Fact]
    public async Task AiProviderSettingsPageViewModel_ReasoningEffort_IsEnabledByTheModelAndAppliedByItsId()
    {
        // Arrange: a model with levels that a fixed enumeration could not hold, and one without
        var source = new FakeModelSource { Models = [Model("thinker", efforts: ["none", "low", "xhigh", "max"]), Model("plain")] };
        var page = await LoadedPage(source);
        var enabledWithoutModel = page.IsReasoningEffortEnabled;
        var noteWithoutModel = page.ReasoningNote;

        // Act: choose the thinker and a level, then the plain model
        page.SelectedModel = page.Models.Single(choice => choice.Id == "thinker");
        var enabled = page.IsReasoningEffortEnabled;
        var entries = page.ReasoningEfforts.Select(choice => choice.Value).ToArray();
        page.SelectedReasoningEffort = page.ReasoningEfforts.Single(choice => choice.Value == "xhigh");
        var applied = page.Apply(new AppSettings()).AiProvider;
        page.SelectedModel = page.Models.Single(choice => choice.Id == "plain");

        // Assert
        Assert.False(enabledWithoutModel);
        Assert.Contains("Choose a model", noteWithoutModel, StringComparison.Ordinal);
        Assert.True(enabled);
        Assert.Equal(["", "none", "low", "xhigh", "max"], entries);
        Assert.Equal(("thinker", "xhigh"), (applied.ModelName, applied.ReasoningEffort));
        Assert.False(page.IsReasoningEffortEnabled);
        Assert.Equal(string.Empty, page.Apply(new AppSettings()).AiProvider.ReasoningEffort);
        Assert.Contains("no reasoning levels", page.ReasoningNote, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Test that a stored reasoning level that the model offers is shown chosen, that one that it does not offer is
    ///     reset with a note once the model is known, and that the page does not touch it while nothing is known.
    /// </summary>
    [Fact]
    public async Task AiProviderSettingsPageViewModel_StoredReasoningEffort_IsKeptWhenOfferedAndResetWhenNot()
    {
        // Arrange: a model that offers two levels
        var source = new FakeModelSource { Models = [Model("thinker", efforts: ["low", "high"])] };

        // Act: load a level that it offers, then one that it does not
        var offered = await LoadedPage(source, new AiProviderSettings { ModelName = "thinker", ReasoningEffort = "high" });
        var offeredChoice = offered.SelectedReasoningEffort!.Value;
        var offeredApplied = offered.Apply(new AppSettings()).AiProvider.ReasoningEffort;
        var offeredEnabled = offered.IsReasoningEffortEnabled;
        var refused = await LoadedPage(source, new AiProviderSettings { ModelName = "thinker", ReasoningEffort = "xhigh" });

        // Assert: the offered level is kept and shown; the other is reset with the note that says so
        Assert.Equal(("high", "high", true), (offeredChoice, offeredApplied, offeredEnabled));
        Assert.Equal(string.Empty, refused.SelectedReasoningEffort!.Value);
        Assert.Equal(string.Empty, refused.Apply(new AppSettings()).AiProvider.ReasoningEffort);
        Assert.Contains("does not offer the reasoning level xhigh", refused.ReasoningNote, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Test that the page warns that a high reasoning level on a large model is slow and costly, with the multiplier of
    ///     the model, and that a lower level, a small model, and another provider show no warning.
    /// </summary>
    [Fact]
    public async Task AiProviderSettingsPageViewModel_EffortCostWarning_AppearsOnlyForHighEffortOnLargeModels()
    {
        // Arrange: a large model (15 premium requests per message) and a small one, both with levels
        var large = Model("opus", efforts: ["low", "high"]) with { BillingMultiplier = 15, Category = "powerful" };
        var small = Model("mini", efforts: ["low", "high"]) with { BillingMultiplier = 0.33, Category = "lightweight" };
        var source = new FakeModelSource { Models = [large, small] };
        var page = await LoadedPage(source, new AiProviderSettings { ModelName = "opus", ReasoningEffort = "low" });
        var atLow = (page.HasEffortCostWarning, page.EffortCostWarning);

        // Act: choose the high level, then the small model, then back to the large model with the default level
        page.SelectedReasoningEffort = page.ReasoningEfforts.Single(choice => choice.Value == "high");
        var atHigh = (page.HasEffortCostWarning, page.EffortCostWarning);
        page.SelectedModel = page.Models.Single(choice => choice.Id == "mini");
        var onSmall = page.HasEffortCostWarning;

        // Assert
        Assert.Equal((false, string.Empty), atLow);
        Assert.True(atHigh.HasEffortCostWarning);
        Assert.Contains("slow", atHigh.EffortCostWarning, StringComparison.Ordinal);
        Assert.Contains("costly", atHigh.EffortCostWarning, StringComparison.Ordinal);
        Assert.Contains("15 premium requests", atHigh.EffortCostWarning, StringComparison.Ordinal);
        Assert.False(onSmall);
    }

    /// <summary>
    ///     Test that the effort that the provider chooses counts when none was chosen, and that a model that the provider
    ///     does not describe gets no warning.
    /// </summary>
    [Fact]
    public void AiProviderSettingsPageViewModel_BuildEffortCostWarning_UsesTheDefaultEffortAndNeedsAModel()
    {
        // Arrange
        var large = Model("opus", efforts: ["low", "high"]) with { BillingMultiplier = 10, DefaultReasoningEffort = "high" };

        // Act and assert
        Assert.NotEqual(string.Empty, AiProviderSettingsPageViewModel.BuildEffortCostWarning(large, string.Empty));
        Assert.Equal(string.Empty, AiProviderSettingsPageViewModel.BuildEffortCostWarning(large, "low"));
        Assert.Equal(string.Empty, AiProviderSettingsPageViewModel.BuildEffortCostWarning(null, "high"));
    }

    /// <summary>
    ///     Test that the stored reasoning level is kept as it is while the models cannot be listed, and that the choice is
    ///     disabled with a note, so that a server that is down does not erase a setting.
    /// </summary>
    [Fact]
    public async Task AiProviderSettingsPageViewModel_ListFails_KeepsTheTypedNameAndTheStoredEffort()
    {
        // Arrange: a provider that cannot be asked
        var source = new FakeModelSource { Message = "Ollama did not answer at http://localhost:11434." };

        // Act: load a stored model and level
        var page = await LoadedPage(source, new AiProviderSettings { ModelName = "typed", ReasoningEffort = "max" });

        // Assert: no list, the message, the typed name, the level kept, and the choice disabled with its reason
        Assert.False(page.HasModelList);
        Assert.Empty(page.Models);
        Assert.Equal("Ollama did not answer at http://localhost:11434.", page.ModelListMessage);
        Assert.True(page.HasModelListMessage);
        Assert.Equal("typed", page.ModelName);
        Assert.False(page.IsReasoningEffortEnabled);
        Assert.Equal("max", page.SelectedReasoningEffort!.Value);
        var applied = page.Apply(new AppSettings()).AiProvider;
        Assert.Equal(("typed", "max"), (applied.ModelName, applied.ReasoningEffort));
        Assert.Contains("not known", page.ReasoningNote, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Test that a source that throws is a message on the page and not a crash, and that Refresh models asks the source
    ///     again, with the refresh flag, and shows the new list.
    /// </summary>
    [Fact]
    public async Task AiProviderSettingsPageViewModel_RefreshModels_AsksAgainWithRefreshAndShowsTheNewList()
    {
        // Arrange: a source that throws at first
        var source = new FakeModelSource { Failure = new InvalidOperationException("The runtime stopped.") };
        var page = await LoadedPage(source);
        var messageBefore = page.ModelListMessage;

        // Act: the source recovers and the user chooses Refresh models
        source.Failure = null;
        source.Models = [Model("alpha")];
        await page.RefreshModelsCommand.ExecuteAsync(null);

        // Assert: the failure was a sentence, the second question refreshed, and the list is shown
        Assert.Equal("The models could not be listed: The runtime stopped.", messageBefore);
        Assert.Equal([false, true], source.Questions.Select(question => question.Refresh).ToArray());
        Assert.Equal(["", "alpha"], page.Models.Select(choice => choice.Id).ToArray());
        Assert.Equal(string.Empty, page.ModelListMessage);
        Assert.True(page.RefreshModelsCommand.CanExecute(null));
    }

    /// <summary>
    ///     Test that the page shows that the models are being listed, and that an answer that arrives after the provider
    ///     was changed is dropped, because it is about the other provider.
    /// </summary>
    [Fact]
    public async Task AiProviderSettingsPageViewModel_StaleAnswer_IsDroppedWhenTheProviderChanged()
    {
        // Arrange: a source that holds the first answer back
        var source = new FakeModelSource { Models = [Model("copilot-model")], Gate = new TaskCompletionSource() };
        var page = new AiProviderSettingsPageViewModel(null, source, TimeSpan.Zero);
        page.Load(new AppSettings());
        var firstLoad = page.ModelListLoaded;
        var loadingText = page.ModelListMessage;
        var loading = page.IsLoadingModels;

        // Act: the user chooses Ollama, whose answer comes at once, and then the answer of Copilot is let go
        source.Models = [Model("ollama-model")];
        page.SelectedProvider = page.Providers[1];
        var secondLoad = page.ModelListLoaded;
        source.Gate.SetResult();
        await Task.WhenAll(firstLoad, secondLoad);

        // Assert: the page said that it was loading, and shows the models of the provider that is selected now
        Assert.Equal(AiProviderSettingsPageViewModel.LoadingModelsText, loadingText);
        Assert.True(loading);
        Assert.Equal(["", "ollama-model"], page.Models.Select(choice => choice.Id).ToArray());
        Assert.False(page.IsLoadingModels);
        Assert.Equal(AiProviderKind.Ollama, source.Questions[^1].Settings.Provider);
    }

    /// <summary>
    ///     Test that choosing another provider starts the choice again (a model of one provider means nothing to the other),
    ///     asks the source about the new provider, and that closing the page ends a listing that runs.
    /// </summary>
    [Fact]
    public async Task AiProviderSettingsPageViewModel_ProviderChange_ClearsTheModelAndAsksTheNewProvider()
    {
        // Arrange: a page with a chosen model and level on GitHub Copilot
        var source = new FakeModelSource { Models = [Model("thinker", efforts: ["low"])] };
        var page = await LoadedPage(source, new AiProviderSettings { ModelName = "thinker", ReasoningEffort = "low" });

        // Act: choose Ollama
        page.SelectedProvider = page.Providers[1];
        await page.ModelListLoaded;
        var applied = page.Apply(new AppSettings()).AiProvider;

        // Assert: the choice started again, and the source was asked about Ollama with the endpoint of the page
        Assert.Equal((string.Empty, string.Empty), (applied.ModelName, applied.ReasoningEffort));
        Assert.Equal(2, source.Questions.Count);
        Assert.Equal(AiProviderKind.Ollama, source.Questions[1].Settings.Provider);
        Assert.Equal(AiProviderSettings.DefaultOllamaEndpoint, source.Questions[1].Settings.OllamaEndpoint);
    }

    /// <summary>
    ///     Test that an edit of the Ollama address lists the models again only while Ollama is selected, and an edit of the
    ///     Copilot CLI path only while GitHub Copilot is selected, and that entries that are not valid ask nothing.
    /// </summary>
    [Fact]
    public async Task AiProviderSettingsPageViewModel_EditedAddressOrPath_ListsAgainOnlyForTheSelectedProvider()
    {
        // Arrange: a page on GitHub Copilot
        var source = new FakeModelSource { Models = [Model("alpha")] };
        var page = await LoadedPage(source);
        var token = TestContext.Current.CancellationToken;

        // Act: edit the address (not the provider that is selected), edit the CLI path, then use Ollama and edit the address
        page.OllamaEndpoint = "http://other:11434";
        await page.ModelListLoaded.WaitAsync(token);
        var afterAddressOnCopilot = source.Questions.Count;
        page.CopilotCliPath = @"C:\Tools\copilot.exe";
        await page.ModelListLoaded.WaitAsync(token);
        var afterPath = source.Questions.Count;
        page.CopilotCliPath = "bad|path\0";
        var afterBadPath = source.Questions.Count;
        page.SelectedProvider = page.Providers[1];
        await page.ModelListLoaded.WaitAsync(token);
        page.OllamaEndpoint = "http://third:11434";
        await page.ModelListLoaded.WaitAsync(token);

        // Assert
        Assert.Equal(1, afterAddressOnCopilot);
        Assert.Equal(2, afterPath);
        Assert.Equal(2, afterBadPath);
        Assert.Equal("http://third:11434", source.Questions[^1].Settings.OllamaEndpoint);
        Assert.Equal(@"C:\Tools\copilot.exe", source.Questions[1].Settings.CopilotCliPath);
    }

    /// <summary>
    ///     Test that entries that stop being valid while the models are being listed end that listing and take away its
    ///     loading text, and that an answer or a failure that was already shown stays with the list that stays.
    /// </summary>
    [Fact]
    public async Task AiProviderSettingsPageViewModel_InvalidEntriesDuringListing_ClearTheLoadingText()
    {
        // Arrange: a loaded page on GitHub Copilot, and a source that holds the next answer back
        var token = TestContext.Current.CancellationToken;
        var source = new FakeModelSource { Models = [Model("alpha")] };
        var page = await LoadedPage(source);
        source.Gate = new TaskCompletionSource();

        // Act: a valid path starts a listing, and then the path stops being valid
        page.CopilotCliPath = @"C:\Tools\copilot.exe";
        var loadingText = page.ModelListMessage;
        var loading = page.IsLoadingModels;
        page.CopilotCliPath = "bad|path\0";
        var textAfterInvalid = page.ModelListMessage;
        var loadingAfterInvalid = page.IsLoadingModels;
        var hasMessageAfterInvalid = page.HasModelListMessage;

        // The held-back listing was ended; letting it go changes nothing
        source.Gate.SetResult();
        await page.ModelListLoaded.WaitAsync(token);

        // Assert
        Assert.Equal(AiProviderSettingsPageViewModel.LoadingModelsText, loadingText);
        Assert.True(loading);
        Assert.Equal(string.Empty, textAfterInvalid);
        Assert.False(loadingAfterInvalid);
        Assert.False(hasMessageAfterInvalid);
        Assert.Equal(string.Empty, page.ModelListMessage);
        Assert.False(page.IsLoadingModels);
        Assert.Equal(["", "alpha"], page.Models.Select(choice => choice.Id).ToArray());

        // Act: a failure is shown for a valid path, and then the path stops being valid
        var failing = new FakeModelSource { Failure = new InvalidOperationException("Offline.") };
        var failed = await LoadedPage(failing);
        var failure = failed.ModelListMessage;
        failed.CopilotCliPath = "bad|path\0";

        // Assert: the failure that belongs to the list that is shown stays
        Assert.Contains("Offline.", failure);
        Assert.Equal(failure, failed.ModelListMessage);
        Assert.True(failed.HasModelListMessage);
    }

    /// <summary>
    ///     Test that a page without a model source offers no list and no Refresh models button, and that closing the page
    ///     ends a listing that still runs, so that its answer is not shown.
    /// </summary>
    [Fact]
    public async Task AiProviderSettingsPageViewModel_NoSourceOrClosed_ShowsNoList()
    {
        // Arrange: a page without a source, and one whose source is held back
        var plain = new AiProviderSettingsPageViewModel();
        plain.Load(new AppSettings());
        var source = new FakeModelSource { Models = [Model("alpha")], Gate = new TaskCompletionSource() };
        var closing = new AiProviderSettingsPageViewModel(null, source, TimeSpan.Zero);
        closing.Load(new AppSettings());

        // Act: close the page while the source is held back, then let the answer go
        closing.OnClosed();
        source.Gate.SetResult();
        await closing.ModelListLoaded;

        // Assert
        Assert.False(plain.HasModelSource);
        Assert.False(plain.RefreshModelsCommand.CanExecute(null));
        Assert.False(plain.HasModelList);
        Assert.Empty(closing.Models);
        Assert.False(closing.IsLoadingModels);
        Assert.True(source.LastWasCancelled);
    }

    /// <summary>
    ///     Test that a change of the model or the level forgets the answer of a connection check, which was about other
    ///     entries.
    /// </summary>
    [Fact]
    public async Task AiProviderSettingsPageViewModel_ChangedModelOrEffort_ForgetsTheConnectionCheck()
    {
        // Arrange: a page with a check answer shown
        var source = new FakeModelSource { Models = [Model("thinker", efforts: ["low", "high"])] };
        var check = new RecordingCheck(new ProviderCheckResult(true, "Ready."));
        var page = new AiProviderSettingsPageViewModel(check, source, TimeSpan.Zero);
        page.Load(new AppSettings { AiProvider = new AiProviderSettings { ModelName = "thinker" } });
        await page.ModelListLoaded;
        await page.CheckConnectionCommand.ExecuteAsync(null);
        var shown = page.CheckConnectionResult;

        // Act: choose another level
        page.SelectedReasoningEffort = page.ReasoningEfforts.Single(choice => choice.Value == "high");

        // Assert
        Assert.Equal("Ready.", shown);
        Assert.Equal(string.Empty, page.CheckConnectionResult);
    }
}
