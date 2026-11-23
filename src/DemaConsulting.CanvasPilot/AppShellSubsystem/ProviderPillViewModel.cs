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

using System.ComponentModel;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using DemaConsulting.CanvasPilot.AgentSubsystem;
using DemaConsulting.CanvasPilot.ChatSubsystem;
using DemaConsulting.CanvasPilot.SettingsSubsystem;

namespace DemaConsulting.CanvasPilot.AppShellSubsystem;

/// <summary>
///     How well the AI provider can be used, as the pill in the title bar tells it.
/// </summary>
public enum ProviderPillState
{
    /// <summary>The provider is being checked, or the first check has not answered yet.</summary>
    Checking,

    /// <summary>The provider can be used.</summary>
    Ready,

    /// <summary>The provider needs something from the user, such as a sign-in, or can be used with a warning.</summary>
    NeedsAttention,

    /// <summary>The provider cannot be used and the user cannot fix it from here.</summary>
    Error
}

/// <summary>
///     View model of the AI provider pill in the title bar. It says which provider and model the settings choose and
///     whether the provider can be used, with words as well as a colored dot, from the same check that the readiness banner
///     of the chat panel shows. Using the pill opens the settings.
/// </summary>
public sealed class ProviderPillViewModel : ObservableObject, IDisposable
{
    /// <summary>
    ///     The chat panel whose check of the provider the pill reports.
    /// </summary>
    private readonly ChatPanelViewModel _chat;

    /// <summary>
    ///     The settings that choose the provider and the model.
    /// </summary>
    private readonly ISettingsService _settings;

    /// <summary>
    ///     The synchronization context of the thread that created the pill, which owns the user interface, or null.
    /// </summary>
    private readonly SynchronizationContext? _context = SynchronizationContext.Current;

    /// <summary>
    ///     Whether the pill was disposed.
    /// </summary>
    private bool _disposed;

    /// <summary>
    ///     Creates the pill.
    /// </summary>
    /// <param name="chat">The chat panel whose check of the provider the pill reports.</param>
    /// <param name="settings">The settings that choose the provider and the model.</param>
    /// <param name="openSettingsCommand">What using the pill does, which is to open the settings.</param>
    public ProviderPillViewModel(ChatPanelViewModel chat, ISettingsService settings, ICommand openSettingsCommand)
    {
        ArgumentNullException.ThrowIfNull(chat);
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(openSettingsCommand);

        _chat = chat;
        _settings = settings;
        OpenSettingsCommand = openSettingsCommand;
        _chat.PropertyChanged += OnChatPropertyChanged;
        _settings.Changed += OnSettingsChanged;
    }

    /// <summary>
    ///     Gets what using the pill does, which is to open the settings on their first page, the AI provider.
    /// </summary>
    public ICommand OpenSettingsCommand { get; }

    /// <summary>
    ///     Gets how well the provider can be used.
    /// </summary>
    public ProviderPillState State
    {
        get
        {
            if (!_chat.HasCopilot)
            {
                return ProviderPillState.Error;
            }

            if (_chat.IsCheckingReadiness || _chat.Readiness is null)
            {
                return ProviderPillState.Checking;
            }

            return _chat.Readiness.Status switch
            {
                AgentReadinessStatus.Ready => ProviderPillState.Ready,
                AgentReadinessStatus.CopilotRuntimeUnavailable => ProviderPillState.Error,
                _ => ProviderPillState.NeedsAttention
            };
        }
    }

    /// <summary>
    ///     Gets a value indicating whether the provider is being checked.
    /// </summary>
    public bool IsChecking => State == ProviderPillState.Checking;

    /// <summary>
    ///     Gets a value indicating whether the provider can be used.
    /// </summary>
    public bool IsReady => State == ProviderPillState.Ready;

    /// <summary>
    ///     Gets a value indicating whether the provider needs something from the user.
    /// </summary>
    public bool NeedsAttention => State == ProviderPillState.NeedsAttention;

    /// <summary>
    ///     Gets a value indicating whether the provider cannot be used.
    /// </summary>
    public bool IsError => State == ProviderPillState.Error;

    /// <summary>
    ///     Gets the name of the provider that the settings choose, shown in bold.
    /// </summary>
    public string ProviderName => _settings.Current.AiProvider.Provider == AiProviderKind.Ollama ? "Ollama" : "Copilot";

    /// <summary>
    ///     Gets the model that the settings choose, or <c>default model</c> when they leave it to the provider.
    /// </summary>
    public string ModelName => _settings.Current.AiProvider.ModelName is { Length: > 0 } model ? model : "default model";

    /// <summary>
    ///     Gets the words that say how the provider is: the model while it is ready, and otherwise a few words about what is
    ///     wrong or that it is being checked. The state is never told by the color of the dot alone.
    /// </summary>
    public string DetailText => State switch
    {
        ProviderPillState.Ready => ModelName,
        ProviderPillState.Checking => "checking",
        _ => StateWords(_chat.Readiness?.Status)
    };

    /// <summary>
    ///     Gets the text shown after the provider name: a middle dot and the detail.
    /// </summary>
    public string SeparatedDetailText => " \u00B7 " + DetailText;

    /// <summary>
    ///     Gets the sentence that a screen reader reads for the pill.
    /// </summary>
    public string AccessibleName => $"AI provider: {ProviderName}, {DetailText}. Opens the settings.";

    /// <summary>
    ///     Gets the tooltip of the pill: what the check of the provider found, the same sentence as the banner of the chat.
    /// </summary>
    public string ToolTipText
    {
        get
        {
            if (!_chat.HasCopilot)
            {
                return "There is no AI provider in this application.";
            }

            return _chat.Readiness?.Message ?? "Checking whether the AI provider can be used...";
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _disposed = true;
        _chat.PropertyChanged -= OnChatPropertyChanged;
        _settings.Changed -= OnSettingsChanged;
    }

    /// <summary>
    ///     Gives the few words that say what is wrong for a result of the check.
    /// </summary>
    /// <param name="status">What the check found, or <see langword="null" /> when it found nothing.</param>
    /// <returns>The words, such as <c>sign-in needed</c>.</returns>
    private static string StateWords(AgentReadinessStatus? status)
    {
        return status switch
        {
            AgentReadinessStatus.NotConfigured => "not set up",
            AgentReadinessStatus.CopilotCliMissing => "CLI not found",
            AgentReadinessStatus.CopilotCliTooOld => "CLI too old",
            AgentReadinessStatus.CopilotNotSignedIn => "sign-in needed",
            AgentReadinessStatus.CopilotRuntimeUnavailable => "unavailable",
            AgentReadinessStatus.OllamaUnreachable => "Ollama not running",
            AgentReadinessStatus.OllamaModelMissing => "model missing",
            AgentReadinessStatus.OllamaContextTooSmall => "small context",
            AgentReadinessStatus.ModelSettingsInvalid => "model not usable",
            AgentReadinessStatus.ModelLacksVision => "cannot see pictures",
            _ => "unavailable"
        };
    }

    /// <summary>
    ///     Follows the check of the provider that the chat panel runs.
    /// </summary>
    /// <param name="sender">The chat panel.</param>
    /// <param name="e">The change details.</param>
    private void OnChatPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(ChatPanelViewModel.Readiness) or nameof(ChatPanelViewModel.IsCheckingReadiness))
        {
            RunOnOwnerThread(() => OnPropertyChanged(string.Empty));
        }
    }

    /// <summary>
    ///     Follows a change of the settings, which can choose another provider or model.
    /// </summary>
    /// <param name="sender">The settings service.</param>
    /// <param name="e">The settings now in effect.</param>
    private void OnSettingsChanged(object? sender, SettingsChangedEventArgs e)
    {
        RunOnOwnerThread(() => OnPropertyChanged(string.Empty));
    }

    /// <summary>
    ///     Runs an action on the user interface thread: directly when already there (or when the pill was created outside a
    ///     user interface), otherwise posted to it.
    /// </summary>
    /// <param name="action">The action.</param>
    private void RunOnOwnerThread(Action action)
    {
        if (_disposed)
        {
            return;
        }

        if (_context is null || SynchronizationContext.Current == _context)
        {
            action();
            return;
        }

        _context.Post(
            _ =>
            {
                if (!_disposed)
                {
                    action();
                }
            },
            null);
    }
}
