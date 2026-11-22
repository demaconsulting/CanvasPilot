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
using Microsoft.Extensions.Logging;

namespace DemaConsulting.CanvasPilot.AgentSubsystem;

/// <summary>
///     The logger that the Copilot runtime of the AI library writes to, adapted to the log file of the application. It
///     records the facts of each entry (the level, the category, the number of the event, and the type of an exception)
///     and never the message, because the runtime's messages may hold the words of the user, the contents of files, or
///     paths.
/// </summary>
/// <remarks>
///     The class holds no state beyond the log file and is safe for concurrent use as far as the log file is. A log that
///     cannot be written never fails the runtime.
/// </remarks>
internal sealed class RuntimeLogAdapter : ILogger
{
    /// <summary>
    ///     Receives the lines.
    /// </summary>
    private readonly RollingFileLogger _logger;

    /// <summary>
    ///     The category that the lines carry.
    /// </summary>
    private readonly string _category;

    /// <summary>
    ///     Initializes a new instance of the <see cref="RuntimeLogAdapter" /> class.
    /// </summary>
    /// <param name="logger">The log file of the application.</param>
    /// <param name="category">The category that the lines carry.</param>
    /// <exception cref="ArgumentNullException">Thrown when an argument is <see langword="null" />.</exception>
    internal RuntimeLogAdapter(RollingFileLogger logger, string category)
    {
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentNullException.ThrowIfNull(category);

        _logger = logger;
        _category = category;
    }

    /// <inheritdoc />
    public IDisposable? BeginScope<TState>(TState state)
        where TState : notnull
    {
        // A scope holds values of the caller that may be user text, so none is kept
        return null;
    }

    /// <inheritdoc />
    public bool IsEnabled(Microsoft.Extensions.Logging.LogLevel logLevel)
    {
        return logLevel is not Microsoft.Extensions.Logging.LogLevel.None;
    }

    /// <inheritdoc />
    public void Log<TState>(
        Microsoft.Extensions.Logging.LogLevel logLevel,
        EventId eventId,
        TState state,
        Exception? exception,
        Func<TState, Exception?, string> formatter)
    {
        if (!IsEnabled(logLevel))
        {
            return;
        }

        // Only facts: the formatter is never called, so the message text is never even built
        var level = logLevel switch
        {
            Microsoft.Extensions.Logging.LogLevel.Critical or Microsoft.Extensions.Logging.LogLevel.Error => DemaConsulting.CanvasPilot.LoggingSubsystem.LogLevel.Error,
            Microsoft.Extensions.Logging.LogLevel.Warning => DemaConsulting.CanvasPilot.LoggingSubsystem.LogLevel.Warning,
            _ => DemaConsulting.CanvasPilot.LoggingSubsystem.LogLevel.Info,
        };
        var error = exception is null ? string.Empty : $" exception={exception.GetType().Name}";
        try
        {
            _logger.Log(level, $"Copilot runtime: category={_category} level={logLevel} event={eventId.Id}{error}");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // A log that cannot be written is not a reason to fail the runtime
        }
    }
}
