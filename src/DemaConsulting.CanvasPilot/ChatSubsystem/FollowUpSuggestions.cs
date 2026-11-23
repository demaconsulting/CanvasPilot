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

namespace DemaConsulting.CanvasPilot.ChatSubsystem;

/// <summary>
///     Plans which prompts the chat panel offers as chips for what the user may want to say next, from what the project
///     has: a project with no drawing yet is offered ways to start, a project with a drawing is offered ways to refine it,
///     and nothing is offered while the copilot works.
/// </summary>
/// <remarks>
///     <para>
///         The planner is a pure function of three facts and has no state, so the panel can ask it again whenever one of
///         them changes. It knows nothing of the window, the files, or the copilot: the panel finds out whether a drawing
///         exists (a picture is shown, the copilot wrote one, or the project folder holds one) and whether a picture is
///         shown.
///     </para>
///     <para>
///         The prompts are plain sentences that are written in the message for the user to change and send. A prompt that
///         only makes sense for a picture that is shown (the export) is offered only then.
///     </para>
/// </remarks>
public static class FollowUpSuggestions
{
    /// <summary>
    ///     The prompt that exports the picture that is shown; it is offered only while a picture is shown.
    /// </summary>
    public const string ExportPrompt = "Export as PNG";

    /// <summary>
    ///     The prompts that a project without a drawing is offered, to get a first drawing.
    /// </summary>
    public static IReadOnlyList<string> StarterPrompts { get; } =
    [
        "Draw a flowchart of our release process",
        "Make a title card for a launch announcement",
        "Chart something from a small table of numbers",
    ];

    /// <summary>
    ///     The prompts that a project with a drawing is offered, to refine it.
    /// </summary>
    public static IReadOnlyList<string> RefinementPrompts { get; } =
    [
        "Make the title larger",
        "Add a legend",
        "Use my style kit",
    ];

    /// <summary>
    ///     Plans the prompts to offer.
    /// </summary>
    /// <param name="hasDrawing"><see langword="true" /> when the project has a drawing.</param>
    /// <param name="hasPictureShown"><see langword="true" /> when a picture is shown, so that it can be exported.</param>
    /// <param name="busy"><see langword="true" /> while the copilot works on a message.</param>
    /// <returns>
    ///     The prompts, in the order to show them; empty while the copilot works. The list is new each time and may be
    ///     changed by the caller.
    /// </returns>
    public static IReadOnlyList<string> Plan(bool hasDrawing, bool hasPictureShown, bool busy)
    {
        if (busy)
        {
            return [];
        }

        if (!hasDrawing)
        {
            return [.. StarterPrompts];
        }

        List<string> prompts = [.. RefinementPrompts];
        if (hasPictureShown)
        {
            prompts.Add(ExportPrompt);
        }

        return prompts;
    }
}
