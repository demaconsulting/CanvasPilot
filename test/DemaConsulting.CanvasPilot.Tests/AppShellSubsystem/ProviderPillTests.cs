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

using CommunityToolkit.Mvvm.Input;
using DemaConsulting.CanvasPilot.AgentSubsystem;
using DemaConsulting.CanvasPilot.AnnotationSubsystem;
using DemaConsulting.CanvasPilot.AppShellSubsystem;
using DemaConsulting.CanvasPilot.ChatSubsystem;
using DemaConsulting.CanvasPilot.ComposerSubsystem;
using DemaConsulting.CanvasPilot.PayloadSubsystem;
using DemaConsulting.CanvasPilot.SettingsSubsystem;
using NSubstitute;

namespace DemaConsulting.CanvasPilot.Tests;

/// <summary>
///     Unit tests for the AI provider pill view model over a real chat panel with a controlled copilot and real settings on
///     a temporary folder.
/// </summary>
public sealed class ProviderPillTests : IDisposable
{
    /// <summary>
    ///     Temporary folder that holds the settings.
    /// </summary>
    private readonly string _root = Directory.CreateTempSubdirectory("canvaspilot-provider-pill-tests-").FullName;

    /// <summary>
    ///     The copilot, whose readiness the tests choose.
    /// </summary>
    private readonly ControlledAgentService _agent = new();

    /// <summary>
    ///     The settings that choose the provider and the model.
    /// </summary>
    private readonly SettingsService _settings;

    /// <summary>
    ///     The composer of the chat panel.
    /// </summary>
    private readonly ComposerViewModel _composer;

    /// <summary>
    ///     The annotation layer of the chat panel.
    /// </summary>
    private readonly AnnotationOverlayViewModel _annotation;

    /// <summary>
    ///     The chat panel whose check the pill reports.
    /// </summary>
    private readonly ChatPanelViewModel _chat;

    /// <summary>
    ///     How many times the command of the pill ran.
    /// </summary>
    private int _opened;

    /// <summary>
    ///     Initializes a new instance of the <see cref="ProviderPillTests" /> class.
    /// </summary>
    public ProviderPillTests()
    {
        _settings = new SettingsService(Path.Combine(_root, "settings"));
        _settings.Load();
        var marks = new MarkRegistry();
        var markImages = Substitute.For<IMarkImageComposer>();
        _composer = new ComposerViewModel(new PayloadBuilder(markImages), marks, new FakeDictationService());
        _annotation = new AnnotationOverlayViewModel(marks, new FakeRenderedImageSource(), _ => { });
        _chat = new ChatPanelViewModel(_composer, marks, _annotation, markImages, agent: _agent, clock: new ManualTimeProvider());
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _chat.Dispose();
        _annotation.Dispose();
        _composer.Dispose();
        _agent.Dispose();
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    /// <summary>
    ///     Test that the pill is checking before the first answer, and that a ready provider shows the provider in bold and
    ///     the model after it, with the sentence of the check as its tooltip.
    /// </summary>
    [Fact]
    public async Task ProviderPill_CheckThenReady_ShowsCheckingThenProviderAndModel()
    {
        // Arrange: settings that choose a model, and a pill that has not been told anything yet
        _settings.Update(s => s with { AiProvider = s.AiProvider with { ModelName = "claude-haiku-5.5" } });
        _agent.Readiness = new AgentReadinessReport(AgentReadinessStatus.Ready, "GitHub Copilot is ready.");
        using var pill = CreatePill();
        var before = (pill.State, pill.IsChecking, pill.DetailText);

        // Act: the chat panel checks the provider
        await _chat.RefreshReadinessAsync();

        // Assert: checking at first, and ready with the model after the answer
        Assert.Equal((ProviderPillState.Checking, true, "checking"), before);
        Assert.Equal(ProviderPillState.Ready, pill.State);
        Assert.True(pill.IsReady);
        Assert.Equal("Copilot", pill.ProviderName);
        Assert.Equal("claude-haiku-5.5", pill.DetailText);
        Assert.Equal(" \u00B7 claude-haiku-5.5", pill.SeparatedDetailText);
        Assert.Equal("GitHub Copilot is ready.", pill.ToolTipText);
        Assert.Equal("AI provider: Copilot, claude-haiku-5.5. Opens the settings.", pill.AccessibleName);
    }

    /// <summary>
    ///     Test that a ready provider whose check names the account and the kind of sign-in shows that sentence in the
    ///     tooltip of the pill, unchanged, so that the user can see which account Copilot uses.
    /// </summary>
    [Fact]
    public async Task ProviderPill_ReadyWithSignIn_ToolTipNamesTheAccountAndTheKindOfSignIn()
    {
        // Arrange: a check that is ready and says how Copilot signed in
        const string sentence = "GitHub Copilot CLI 1.0.114 is ready. Signed in as mnixon on github.com (Copilot CLI sign-in).";
        _agent.Readiness = new AgentReadinessReport(AgentReadinessStatus.Ready, sentence);
        using var pill = CreatePill();

        // Act: the chat panel checks the provider
        await _chat.RefreshReadinessAsync();

        // Assert: the tooltip is the sentence of the check, with the account and the kind of sign-in
        Assert.Equal(ProviderPillState.Ready, pill.State);
        Assert.Equal(sentence, pill.ToolTipText);
        Assert.Contains("mnixon on github.com (Copilot CLI sign-in)", pill.ToolTipText, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Test that every result of the check that is not ready has its own few words, and that the colors of the state,
    ///     amber or red, follow what the user can do about it.
    /// </summary>
    /// <param name="status">What the check found.</param>
    /// <param name="state">The state of the pill that is expected.</param>
    /// <param name="words">The words that are expected.</param>
    [Theory]
    [InlineData(AgentReadinessStatus.NotConfigured, ProviderPillState.NeedsAttention, "not set up")]
    [InlineData(AgentReadinessStatus.CopilotCliMissing, ProviderPillState.NeedsAttention, "CLI not found")]
    [InlineData(AgentReadinessStatus.CopilotCliTooOld, ProviderPillState.NeedsAttention, "CLI too old")]
    [InlineData(AgentReadinessStatus.CopilotNotSignedIn, ProviderPillState.NeedsAttention, "sign-in needed")]
    [InlineData(AgentReadinessStatus.CopilotRuntimeUnavailable, ProviderPillState.Error, "unavailable")]
    [InlineData(AgentReadinessStatus.OllamaUnreachable, ProviderPillState.NeedsAttention, "Ollama not running")]
    [InlineData(AgentReadinessStatus.OllamaModelMissing, ProviderPillState.NeedsAttention, "model missing")]
    [InlineData(AgentReadinessStatus.OllamaContextTooSmall, ProviderPillState.NeedsAttention, "small context")]
    [InlineData(AgentReadinessStatus.ModelSettingsInvalid, ProviderPillState.NeedsAttention, "model not usable")]
    [InlineData(AgentReadinessStatus.ModelLacksVision, ProviderPillState.NeedsAttention, "cannot see pictures")]
    public async Task ProviderPill_CheckFindsAProblem_SaysItInWordsAndPicksTheState(AgentReadinessStatus status, ProviderPillState state, string words)
    {
        // Arrange: a pill over a check that finds the problem
        _agent.Readiness = new AgentReadinessReport(status, "The sentence of the check.");
        using var pill = CreatePill();

        // Act: the chat panel checks the provider
        await _chat.RefreshReadinessAsync();

        // Assert: the state, the words, and the tooltip that repeats the sentence of the banner
        Assert.Equal(state, pill.State);
        Assert.Equal(state == ProviderPillState.Error, pill.IsError);
        Assert.Equal(state == ProviderPillState.NeedsAttention, pill.NeedsAttention);
        Assert.Equal(words, pill.DetailText);
        Assert.Equal("The sentence of the check.", pill.ToolTipText);
        Assert.Contains(words, pill.AccessibleName, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Test that a change of the provider and the model in the settings changes the name of the provider and the model
    ///     that the pill shows, and announces it.
    /// </summary>
    [Fact]
    public async Task ProviderPill_SettingsChooseOllama_ShowsTheOtherProviderAndTheDefaultModel()
    {
        // Arrange: a ready pill that announces its changes
        _agent.Readiness = new AgentReadinessReport(AgentReadinessStatus.Ready, "Ready.");
        using var pill = CreatePill();
        await _chat.RefreshReadinessAsync();
        var announced = 0;
        pill.PropertyChanged += (_, _) => announced++;

        // Act: the settings choose Ollama and leave the model to the provider
        _settings.Update(s => s with { AiProvider = s.AiProvider with { Provider = AiProviderKind.Ollama, ModelName = string.Empty } });

        // Assert: the pill names the other provider and the default model, and said so
        Assert.Equal("Ollama", pill.ProviderName);
        Assert.Equal("default model", pill.ModelName);
        Assert.Equal("default model", pill.DetailText);
        Assert.True(announced > 0);
    }

    /// <summary>
    ///     Test that a second check shows checking again while it runs, that using the pill runs its command, and that a
    ///     pill over a chat panel with no copilot is in error and says so.
    /// </summary>
    [Fact]
    public async Task ProviderPill_RecheckAndNoCopilot_CheckingAgainAndErrorWithoutCopilot()
    {
        // Arrange: a ready pill
        _agent.Readiness = new AgentReadinessReport(AgentReadinessStatus.Ready, "Ready.");
        using var pill = CreatePill();
        await _chat.RefreshReadinessAsync();
        var states = new List<ProviderPillState>();
        pill.PropertyChanged += (_, _) => states.Add(pill.State);

        // Act: check again, and use the pill
        await _chat.RefreshReadinessAsync();
        pill.OpenSettingsCommand.Execute(null);
        using var bare = new ChatPanelViewModel(_composer, new MarkRegistry(), _annotation, Substitute.For<IMarkImageComposer>());
        using var withoutCopilot = new ProviderPillViewModel(bare, _settings, new RelayCommand(() => { }));

        // Assert: the pill went through checking and back, the command ran, and with no copilot the pill is in error
        Assert.Contains(ProviderPillState.Checking, states);
        Assert.Equal(ProviderPillState.Ready, states[^1]);
        Assert.Equal(1, _opened);
        Assert.Equal(ProviderPillState.Error, withoutCopilot.State);
        Assert.Equal("There is no AI provider in this application.", withoutCopilot.ToolTipText);
    }

    /// <summary>
    ///     Test that the pill refuses missing arguments and stops following the chat panel and the settings once it is
    ///     disposed.
    /// </summary>
    [Fact]
    public async Task ProviderPill_NullArgumentsAndDispose_RefusesNullAndStopsFollowing()
    {
        // Arrange: a pill that is disposed
        var pill = CreatePill();
        pill.Dispose();
        var announced = 0;
        pill.PropertyChanged += (_, _) => announced++;

        // Act: the chat panel checks and the settings change
        await _chat.RefreshReadinessAsync();
        _settings.Update(s => s with { AiProvider = s.AiProvider with { ModelName = "other" } });

        // Assert: nothing was announced, and missing arguments are refused
        Assert.Equal(0, announced);
        Assert.Throws<ArgumentNullException>(() => new ProviderPillViewModel(null!, _settings, new RelayCommand(() => { })));
        Assert.Throws<ArgumentNullException>(() => new ProviderPillViewModel(_chat, null!, new RelayCommand(() => { })));
        Assert.Throws<ArgumentNullException>(() => new ProviderPillViewModel(_chat, _settings, null!));
    }

    /// <summary>
    ///     Creates a pill over the chat panel and the settings whose command counts its runs.
    /// </summary>
    /// <returns>The pill.</returns>
    private ProviderPillViewModel CreatePill()
    {
        return new ProviderPillViewModel(_chat, _settings, new RelayCommand(() => _opened++));
    }
}
