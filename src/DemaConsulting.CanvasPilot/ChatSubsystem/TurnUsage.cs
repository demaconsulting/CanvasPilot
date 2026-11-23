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

using System.Globalization;
using DemaConsulting.CanvasPilot.AgentSubsystem;

namespace DemaConsulting.CanvasPilot.ChatSubsystem;

/// <summary>
///     What a turn cost, summed from the usage that the provider reported for each model call of the turn, and the one
///     line of words that shows it under the last reply of the turn.
/// </summary>
/// <remarks>
///     <para>
///         Tokens are added up over the calls of the turn. The model multiplier is not added: a provider bills a message
///         once, with the multiplier of the model, however many model calls the message needed, so the largest multiplier
///         that was reported is kept. Only what the provider really reported is shown; a figure that was not reported is
///         left out, and a turn without any figure has no caption unless it was stopped.
///     </para>
///     <para>
///         A turn that was stopped says so and that the request may still be counted, because a provider counts a
///         request when it starts the model, and a stop does not give it back.
///     </para>
/// </remarks>
public sealed class TurnUsage
{
    /// <summary>
    ///     The words that the caption of a stopped turn ends with.
    /// </summary>
    public const string StoppedNote = "stopped, the request may still count";

    /// <summary>
    ///     The tokens sent to the model so far, or <see langword="null" /> when none were reported.
    /// </summary>
    private long? _input;

    /// <summary>
    ///     The tokens that the model wrote so far, or <see langword="null" /> when none were reported.
    /// </summary>
    private long? _output;

    /// <summary>
    ///     The tokens that the model spent on reasoning so far, or <see langword="null" /> when none were reported.
    /// </summary>
    private long? _reasoning;

    /// <summary>
    ///     The largest model multiplier reported, or <see langword="null" /> when none was.
    /// </summary>
    private double? _multiplier;

    /// <summary>
    ///     Gets the tokens sent to the model, or <see langword="null" /> when none were reported.
    /// </summary>
    public long? InputTokens => _input;

    /// <summary>
    ///     Gets the tokens that the model wrote, or <see langword="null" /> when none were reported.
    /// </summary>
    public long? OutputTokens => _output;

    /// <summary>
    ///     Gets the tokens that the model spent on reasoning, or <see langword="null" /> when none were reported.
    /// </summary>
    public long? ReasoningTokens => _reasoning;

    /// <summary>
    ///     Gets the model multiplier that the provider bills a message with, or <see langword="null" /> when none was
    ///     reported.
    /// </summary>
    public double? Multiplier => _multiplier;

    /// <summary>
    ///     Adds the usage that the provider reported for one model call.
    /// </summary>
    /// <param name="usage">The usage.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="usage" /> is <see langword="null" />.</exception>
    public void Add(AgentEvent.Usage usage)
    {
        ArgumentNullException.ThrowIfNull(usage);

        _input = Sum(_input, usage.InputTokens);
        _output = Sum(_output, usage.OutputTokens);
        _reasoning = Sum(_reasoning, usage.ReasoningTokens);
        if (usage.Multiplier is { } multiplier)
        {
            _multiplier = Math.Max(_multiplier ?? 0, multiplier);
        }
    }

    /// <summary>
    ///     Writes the caption: the figures that were reported, the time the turn took, and a note when it was stopped.
    /// </summary>
    /// <param name="elapsed">The time the turn took.</param>
    /// <param name="stopped"><see langword="true" /> when the user stopped the turn.</param>
    /// <returns>The caption, or an empty string when there is nothing to say.</returns>
    public string Format(TimeSpan elapsed, bool stopped)
    {
        var parts = new List<string>();
        if (_input is { } input)
        {
            parts.Add($"{FormatTokens(input)} in");
        }

        if (_output is { } output)
        {
            parts.Add($"{FormatTokens(output)} out");
        }

        if (_reasoning is > 0)
        {
            parts.Add($"{FormatTokens(_reasoning.Value)} reasoning");
        }

        if (_multiplier is { } multiplier)
        {
            parts.Add(multiplier <= 0 ? "no premium requests" : $"model x{FormatMultiplier(multiplier)} premium");
        }

        if (parts.Count == 0 && !stopped)
        {
            return string.Empty;
        }

        parts.Add(ChatTurnRunner.FormatElapsed(elapsed));
        if (stopped)
        {
            parts.Add(StoppedNote);
        }

        return string.Join(", ", parts);
    }

    /// <summary>
    ///     Writes a count of tokens for a person: the number below a thousand, otherwise thousands with one decimal.
    /// </summary>
    /// <param name="tokens">The count.</param>
    /// <returns>The text, for example <c>850</c> or <c>12.4k</c>.</returns>
    public static string FormatTokens(long tokens)
    {
        if (tokens < 1000)
        {
            return tokens.ToString(CultureInfo.InvariantCulture);
        }

        return tokens < 1_000_000
            ? (tokens / 1000.0).ToString("0.#", CultureInfo.InvariantCulture) + "k"
            : (tokens / 1_000_000.0).ToString("0.##", CultureInfo.InvariantCulture) + "M";
    }

    /// <summary>
    ///     Writes a model multiplier without trailing zeros.
    /// </summary>
    /// <param name="multiplier">The multiplier.</param>
    /// <returns>The text, for example <c>15</c> or <c>0.33</c>.</returns>
    private static string FormatMultiplier(double multiplier) => multiplier.ToString("0.##", CultureInfo.InvariantCulture);

    /// <summary>
    ///     Adds a reported figure to a sum, leaving the sum unchanged when nothing was reported.
    /// </summary>
    /// <param name="sum">The sum so far, or <see langword="null" /> when nothing was reported yet.</param>
    /// <param name="value">The figure, or <see langword="null" /> when it was not reported.</param>
    /// <returns>The new sum.</returns>
    private static long? Sum(long? sum, long? value) => value is { } figure ? (sum ?? 0) + figure : sum;
}
