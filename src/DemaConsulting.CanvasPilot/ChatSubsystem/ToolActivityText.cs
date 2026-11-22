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

using System.Text.RegularExpressions;
using DemaConsulting.CanvasPilot.AgentSubsystem;

namespace DemaConsulting.CanvasPilot.ChatSubsystem;

/// <summary>
///     Writes the sentences of the tool rows and the notices of the chat turn runner: what the copilot is doing with a
///     tool (<c>Rendering `cover.svg`</c>), what it did (<c>Rendered `cover.svg`</c>, <c>Created `cover.svg`</c>), and
///     what to do when a turn fails.
/// </summary>
/// <remarks>
///     The tool names are those of the tools that CanvasPilot gives the copilot. A tool that is not known gets a plain
///     sentence with its name, so a new tool needs no change here to be shown. The sentences are made from the name and
///     the short description of the arguments only and never hold the contents of a file.
/// </remarks>
internal static partial class ToolActivityText
{
    /// <summary>
    ///     What the sentences of a known tool say, as pieces around the thing the tool works on.
    /// </summary>
    /// <param name="Start">What is under way, for example <c>Rendering</c>.</param>
    /// <param name="Done">What was done, for example <c>Rendered</c>.</param>
    /// <param name="Fail">What could not be done, for example <c>Could not render</c>.</param>
    /// <param name="Argument">The name of the argument that names the thing, or <see langword="null" /> for none.</param>
    /// <param name="Fallback">What to say when the argument is missing; empty when the sentence needs no thing.</param>
    private sealed record ToolWording(string Start, string Done, string Fail, string? Argument, string Fallback);

    /// <summary>
    ///     The wording of each tool of CanvasPilot, by name.
    /// </summary>
    private static readonly Dictionary<string, ToolWording> Wordings = new(StringComparer.Ordinal)
    {
        ["project_write"] = new("Writing", "Wrote", "Could not write", "path", "a file"),
        ["project_read"] = new("Reading", "Read", "Could not read", "path", "a file"),
        ["project_list"] = new("Listing", "Listed", "Could not list", "folder", "the project files"),
        ["render_image"] = new("Rendering", "Rendered", "Could not render", "path", "a picture"),
        ["canvasnet_help"] = new("Looking up CanvasNet help", "Looked up CanvasNet help", "Could not look up CanvasNet help", null, string.Empty),
        ["style_get"] = new("Reading the style guidance", "Read the style guidance", "Could not read the style guidance", null, string.Empty),
        ["style_check"] = new("Checking the style", "Checked the style", "Could not check the style", null, string.Empty),
    };

    /// <summary>
    ///     Says what is under way when a tool call starts.
    /// </summary>
    /// <param name="toolName">The name of the tool.</param>
    /// <param name="argumentSummary">The short description of the arguments.</param>
    /// <returns>The sentence, for example <c>Rendering `cover.svg`</c>.</returns>
    internal static string Started(string toolName, string argumentSummary)
    {
        if (!Wordings.TryGetValue(toolName, out var wording))
        {
            return $"Running {toolName}";
        }

        return Compose(wording.Start, wording, argumentSummary);
    }

    /// <summary>
    ///     Says what a tool call did when its result arrives.
    /// </summary>
    /// <param name="toolName">The name of the tool.</param>
    /// <param name="argumentSummary">The short description of the arguments.</param>
    /// <param name="succeeded">Whether the tool did what was asked.</param>
    /// <param name="resultSummary">The short description of the result, which explains a failure.</param>
    /// <returns>The sentence, for example <c>Rendered `cover.svg`</c> or <c>Could not render `cover.svg`: ...</c>.</returns>
    internal static string Finished(string toolName, string argumentSummary, bool succeeded, string resultSummary)
    {
        if (!Wordings.TryGetValue(toolName, out var wording))
        {
            return succeeded ? $"Ran {toolName}" : WithReason($"{toolName} failed", resultSummary);
        }

        return succeeded
            ? Compose(wording.Done, wording, argumentSummary)
            : WithReason(Compose(wording.Fail, wording, argumentSummary), resultSummary);
    }

    /// <summary>
    ///     Says that a file was written.
    /// </summary>
    /// <param name="relativePath">The path of the file, relative to the project folder.</param>
    /// <param name="replaced"><see langword="true" /> when the file existed and was replaced.</param>
    /// <returns>The sentence, <c>Created `x.svg`</c> or <c>Replaced `x.svg`</c>.</returns>
    internal static string Written(string relativePath, bool replaced) =>
        $"{(replaced ? "Replaced" : "Created")} `{relativePath}`";

    /// <summary>
    ///     Says that a call ended because the turn ended before its result arrived.
    /// </summary>
    /// <param name="startedSentence">What was said when the call started.</param>
    /// <param name="stopped"><see langword="true" /> when the user stopped the turn; otherwise the turn failed or ended.</param>
    /// <returns>The sentence.</returns>
    internal static string Abandoned(string startedSentence, bool stopped) =>
        $"{startedSentence} ({(stopped ? "stopped" : "no result")})";

    /// <summary>
    ///     Gives the sentence that tells what to do about a failed turn, for the notice of the failure.
    /// </summary>
    /// <param name="kind">Why the turn failed.</param>
    /// <returns>The remedy; never empty.</returns>
    internal static string RemedyFor(AgentFailureKind kind) => kind switch
    {
        AgentFailureKind.NotReady => "Check the AI provider in Settings.",
        AgentFailureKind.Authentication => "Check the sign-in of the AI provider in Settings.",
        AgentFailureKind.ProtocolMismatch => "Update the AI provider, or choose another one in Settings.",
        AgentFailureKind.ModelNotFound => "Choose a model that the AI provider has in Settings.",
        AgentFailureKind.ConnectionLost => "Check that the AI provider is running and its address in Settings, then send the message again.",
        AgentFailureKind.Busy => "Wait for the running turn to end, or press Stop.",
        AgentFailureKind.DataInsideProject => "Open a project folder that does not contain the data folders of CanvasPilot.",
        _ => "Send the message again. If it keeps failing, look at the application log.",
    };

    /// <summary>
    ///     Tells whether the Settings can help with a failed turn, for the notice of the failure.
    /// </summary>
    /// <param name="kind">Why the turn failed.</param>
    /// <returns><see langword="true" /> when the cause is in the settings of the AI provider.</returns>
    internal static bool SettingsCanHelp(AgentFailureKind kind) => kind is
        AgentFailureKind.NotReady or
        AgentFailureKind.Authentication or
        AgentFailureKind.ProtocolMismatch or
        AgentFailureKind.ModelNotFound or
        AgentFailureKind.ConnectionLost;

    /// <summary>
    ///     Puts the pieces of a sentence together.
    /// </summary>
    /// <param name="lead">What is done, for example <c>Rendering</c>.</param>
    /// <param name="wording">The wording of the tool.</param>
    /// <param name="argumentSummary">The short description of the arguments.</param>
    /// <returns>The sentence.</returns>
    private static string Compose(string lead, ToolWording wording, string argumentSummary)
    {
        if (wording.Argument is null)
        {
            return lead;
        }

        var value = ValueOf(argumentSummary, wording.Argument);
        var thing = value.Length > 0 ? $"`{value}`" : wording.Fallback;
        return $"{lead} {thing}";
    }

    /// <summary>
    ///     Adds the reason to a sentence when there is one.
    /// </summary>
    /// <param name="sentence">The sentence.</param>
    /// <param name="reason">The reason, or an empty string.</param>
    /// <returns>The sentence with the reason after a colon, or with a full stop.</returns>
    private static string WithReason(string sentence, string reason) =>
        reason.Length > 0 ? $"{sentence}: {reason}" : $"{sentence}.";

    /// <summary>
    ///     Finds the value of an argument in the short description that the event mapper writes: <c>name=value</c> pieces
    ///     joined by a comma and a space.
    /// </summary>
    /// <param name="argumentSummary">The short description.</param>
    /// <param name="name">The name of the argument.</param>
    /// <returns>The value, or an empty string when the argument is missing or empty.</returns>
    private static string ValueOf(string argumentSummary, string name)
    {
        var match = ArgumentPattern()
            .Matches(argumentSummary)
            .FirstOrDefault(candidate => string.Equals(candidate.Groups["name"].Value, name, StringComparison.Ordinal));
        return match?.Groups["value"].Value.Trim() ?? string.Empty;
    }

    /// <summary>
    ///     Matches one <c>name=value</c> piece: the value runs up to the next piece or the end.
    /// </summary>
    /// <returns>The pattern.</returns>
    [GeneratedRegex(@"(?:^|, )(?<name>[A-Za-z_][A-Za-z0-9_]*)=(?<value>.*?)(?=, [A-Za-z_][A-Za-z0-9_]*=|$)", RegexOptions.CultureInvariant)]
    private static partial Regex ArgumentPattern();
}
