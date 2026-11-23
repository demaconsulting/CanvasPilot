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

using SdkUsageEvent = Microsoft.Copilot.AssistantUsageEvent;

namespace DemaConsulting.CanvasPilot.AgentSubsystem;

/// <summary>
///     Reads what the Copilot runtime reports about the usage of one model call from the raw event that the AI library
///     carries next to its usage content: the model multiplier (how many premium requests a message counts), the tokens that
///     were spent on reasoning, and the model that served the call.
/// </summary>
/// <remarks>
///     The multiplier (<c>AssistantUsageData.Cost</c>) is an experimental member of the Copilot SDK (diagnostic
///     <c>MCPSDK001</c>). This class is the only place that uses it, so that a change of the SDK touches one method and the
///     rest of CanvasPilot only sees <see cref="CopilotUsage" />. A raw event of any other kind gives no usage.
/// </remarks>
internal static class CopilotUsageReader
{
    /// <summary>
    ///     What the runtime reported about one model call; a member is <see langword="null" /> when it was not reported.
    /// </summary>
    /// <param name="Multiplier">The model multiplier that the call is billed with.</param>
    /// <param name="ReasoningTokens">The output tokens that were spent on reasoning.</param>
    /// <param name="Model">The model that served the call.</param>
    internal readonly record struct CopilotUsage(double? Multiplier, long? ReasoningTokens, string? Model);

    /// <summary>
    ///     Reads the usage from the raw representation of a usage content or update.
    /// </summary>
    /// <param name="raw">The raw representation; may be <see langword="null" />.</param>
    /// <returns>What the runtime reported; every member is <see langword="null" /> for a raw event of another kind.</returns>
    internal static CopilotUsage Read(object? raw)
    {
        if (raw is not SdkUsageEvent { Data: { } data })
        {
            return default;
        }

#pragma warning disable MCPSDK001 // The multiplier is experimental; this adapter is the only user
        double? multiplier = data.Cost;
#pragma warning restore MCPSDK001

        long? reasoning = data.ReasoningTokens;
        var model = string.IsNullOrWhiteSpace(data.Model) ? null : data.Model;
        return new CopilotUsage(multiplier, reasoning, model);
    }
}
