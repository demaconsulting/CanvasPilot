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

using DemaConsulting.AgentKit.Core;
using DemaConsulting.CanvasPilot.SettingsSubsystem;

namespace DemaConsulting.CanvasPilot.AgentSubsystem;

/// <summary>
///     What CanvasPilot asks of a model, and how the model choices of the settings become the settings that the AI library
///     validates. The one place that states the policy, so that the readiness check, the factory of agents, and the model
///     list agree.
/// </summary>
/// <remarks>
///     CanvasPilot needs tool calling, because every drawing is made by calling tools, so a model without it cannot be
///     used and fails the validation. It also wants vision, so that the model sees the pictures of the tools, but a model
///     without it still works for text and file work; that is a warning and never a failure. The class is stateless and
///     thread-safe.
/// </remarks>
internal static class ModelRequirements
{
    /// <summary>
    ///     The capabilities that a model must have to be used at all.
    /// </summary>
    internal const ModelCapabilities Required = ModelCapabilities.ToolCalls;

    /// <summary>
    ///     Makes the model settings that the AI library validates from the choices of the user.
    /// </summary>
    /// <param name="settings">The provider choices.</param>
    /// <param name="withContextWindow">
    ///     <see langword="true" /> to state the context window of the settings (Ollama is sized with it);
    ///     <see langword="false" /> for a provider that sets its own window.
    /// </param>
    /// <returns>
    ///     The model settings, or <see langword="null" /> when no model is named, which means the default model of the
    ///     provider and no choice to validate; a reasoning effort cannot be checked without a model and is not used then.
    /// </returns>
    internal static ModelSettings? ToModelSettings(AiProviderSettings settings, bool withContextWindow)
    {
        ArgumentNullException.ThrowIfNull(settings);

        var model = settings.ModelName?.Trim();
        if (string.IsNullOrEmpty(model))
        {
            return null;
        }

        var effort = settings.ReasoningEffort?.Trim();
        return new ModelSettings(model)
        {
            ReasoningEffort = string.IsNullOrEmpty(effort) ? null : effort,
            ContextWindowTokens = withContextWindow ? settings.ContextWindowTokens : null,
        };
    }

    /// <summary>
    ///     Words the problems of a refused model selection as one text for the user.
    /// </summary>
    /// <param name="exception">The refusal of the AI library.</param>
    /// <returns>The problems, each of which is a sentence, joined with a space.</returns>
    internal static string Describe(ModelSettingsException exception)
    {
        ArgumentNullException.ThrowIfNull(exception);

        return exception.Problems.Count > 0 ? string.Join(" ", exception.Problems) : exception.Message;
    }

    /// <summary>
    ///     Gets the sentence for a model that does not take pictures.
    /// </summary>
    /// <param name="model">The model.</param>
    /// <returns>The sentence, or <see langword="null" /> when the model takes pictures.</returns>
    internal static string? VisionWarning(ModelInfo model)
    {
        ArgumentNullException.ThrowIfNull(model);

        return model.Supports(ModelCapabilities.Vision)
            ? null
            : $"The model {model.Id} does not accept pictures, so it cannot see the drawings and marks; choose a model with vision for the best results.";
    }
}
