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

using DemaConsulting.CanvasPilot.LoggingSubsystem;

namespace DemaConsulting.CanvasPilot.AgentSubsystem;

/// <summary>
///     Writes one line of metadata to the log for each call of a project tool: the name of the tool, the path of the file
///     below the project folder, a size, and the outcome. It never writes the content of a file, the text of a message, or a
///     path that a model sent and the tool refused.
/// </summary>
/// <remarks>
///     The log is a file on the user's computer that people attach to bug reports, and the copilot works on the user's
///     drawings and words, so the log is allowed to say what happened and never what was in it. The path that is logged is
///     the one the tool decided to use (inside the project, after the checks); a refused request is logged without its
///     path. A missing logger makes the calls silent. The class holds no state beyond the logger and is safe for
///     concurrent use as far as the logger is.
/// </remarks>
internal sealed class ToolCallLog
{
    /// <summary>
    ///     The outcome of a call that did what was asked.
    /// </summary>
    internal const string Done = "done";

    /// <summary>
    ///     The outcome of a call that was refused by a rule, with a sentence for the model.
    /// </summary>
    internal const string Refused = "refused";

    /// <summary>
    ///     The outcome of a call that failed for a reason outside the rules (the file system, the renderer).
    /// </summary>
    internal const string Failed = "failed";

    /// <summary>
    ///     Receives the lines, or <see langword="null" /> for none.
    /// </summary>
    private readonly RollingFileLogger? _logger;

    /// <summary>
    ///     Initializes a new instance of the <see cref="ToolCallLog" /> class.
    /// </summary>
    /// <param name="logger">The logger, or <see langword="null" /> to log nothing.</param>
    internal ToolCallLog(RollingFileLogger? logger)
    {
        _logger = logger;
    }

    /// <summary>
    ///     Writes the line of one call.
    /// </summary>
    /// <param name="tool">The name of the tool.</param>
    /// <param name="relativePath">The path below the project folder that the call used, or <see langword="null" /> when the call used none.</param>
    /// <param name="bytes">A size in bytes that the call read or wrote, or <see langword="null" /> for none.</param>
    /// <param name="outcome">The outcome: <see cref="Done" />, <see cref="Refused" />, or <see cref="Failed" />.</param>
    internal void Record(string tool, string? relativePath, long? bytes, string outcome)
    {
        if (_logger is null)
        {
            return;
        }

        // Only facts of the call: no content, and no path that was not accepted
        var path = relativePath is null ? string.Empty : $" path='{relativePath}'";
        var size = bytes is null ? string.Empty : $" bytes={bytes.Value}";
        try
        {
            _logger.Log(outcome == Done ? LogLevel.Info : LogLevel.Warning, $"Tool {tool}:{path}{size} outcome={outcome}");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // A log that cannot be written must not turn a good tool call into a failure; the call has happened
        }
    }
}
