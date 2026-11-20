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
        Assert.Equal("8192", page.ContextWindowText);
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
}
