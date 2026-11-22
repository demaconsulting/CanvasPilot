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

namespace DemaConsulting.CanvasPilot.SettingsSubsystem;

/// <summary>
///     One choice of reasoning effort that a model offers.
/// </summary>
/// <param name="Id">The id that the provider uses and the settings store, for example <c>low</c> or <c>xhigh</c>.</param>
/// <param name="DisplayName">The name to show to the user.</param>
public sealed record ProviderEffortChoice(string Id, string DisplayName);

/// <summary>
///     What a provider says about one of its models, in the terms that the settings page needs. The record is plain data of
///     the Settings subsystem so that the page names no type of the AI library.
/// </summary>
/// <param name="Id">The id of the model as the provider names it; this is what the settings store.</param>
/// <param name="DisplayName">The name to show to the user; the id when the provider gives no better name.</param>
/// <param name="SupportsToolCalls"><see langword="true" /> when the model can call tools, which CanvasPilot needs.</param>
/// <param name="SupportsVision"><see langword="true" /> when the model accepts pictures, which CanvasPilot needs to see drawings.</param>
/// <param name="SupportsReasoning"><see langword="true" /> when the model can reason before it answers.</param>
/// <param name="ContextWindowTokens">The largest context window of the model in tokens, or <see langword="null" /> when not known.</param>
/// <param name="ReasoningEfforts">The efforts that the model offers; empty when the effort cannot be chosen.</param>
/// <param name="DefaultReasoningEffort">The id of the effort that the provider uses when none is chosen, or <see langword="null" />.</param>
/// <param name="IsUsable"><see langword="false" /> when the provider lists the model but it cannot be used.</param>
/// <param name="UnusableReason">Why the model cannot be used; <see langword="null" /> when it can.</param>
/// <param name="Warnings">Further sentences that the provider gives with the model, such as a note about its terms.</param>
public sealed record ProviderModelInfo(
    string Id,
    string DisplayName,
    bool SupportsToolCalls,
    bool SupportsVision,
    bool SupportsReasoning,
    int? ContextWindowTokens,
    IReadOnlyList<ProviderEffortChoice> ReasoningEfforts,
    string? DefaultReasoningEffort,
    bool IsUsable,
    string? UnusableReason,
    IReadOnlyList<string> Warnings);

/// <summary>
///     The answer of a provider to the question which models it has.
/// </summary>
/// <param name="Models">The models, in the order to show them; empty when none could be listed.</param>
/// <param name="Message">
///     An empty string when the list is complete, or one sentence for the user that says why the list is empty or missing
///     and what to do, for example that the server does not answer.
/// </param>
public sealed record ProviderModelList(IReadOnlyList<ProviderModelInfo> Models, string Message)
{
    /// <summary>
    ///     Gets the answer for a provider whose models could not be listed.
    /// </summary>
    /// <param name="message">One sentence that says why and what to do.</param>
    /// <returns>An answer with no models and the message.</returns>
    public static ProviderModelList Failed(string message)
    {
        return new ProviderModelList([], message);
    }
}

/// <summary>
///     Lists the models that the AI provider of the settings offers, with what each can do. The Settings subsystem owns
///     this interface so that the settings page can offer a model list without depending on the subsystem that
///     implements it.
/// </summary>
public interface IProviderModelSource
{
    /// <summary>
    ///     Lists the models of the provider that the settings describe.
    /// </summary>
    /// <param name="settings">The AI provider choices, which may not have been saved yet.</param>
    /// <param name="refresh">
    ///     <see langword="true" /> to ask the provider again even when it was asked a short while ago.
    /// </param>
    /// <param name="cancellationToken">A token that cancels the request.</param>
    /// <returns>The answer; a provider that cannot be asked is a result with a message, not an exception.</returns>
    /// <exception cref="OperationCanceledException">Thrown when the token is cancelled.</exception>
    Task<ProviderModelList> ListModelsAsync(
        AiProviderSettings settings,
        bool refresh = false,
        CancellationToken cancellationToken = default);
}
