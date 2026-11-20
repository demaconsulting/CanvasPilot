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
using DemaConsulting.CanvasPilot.LoggingSubsystem;

namespace DemaConsulting.CanvasPilot.Tests;

/// <summary>
///     Unit tests for <see cref="RollingFileLogger" />.
/// </summary>
public sealed class RollingFileLoggerTests : IDisposable
{
    /// <summary>
    ///     Temporary log directory created fresh for each test and removed on disposal.
    /// </summary>
    private readonly string _tempRoot = Directory.CreateTempSubdirectory("canvaspilot-tests-").FullName;

    /// <inheritdoc />
    public void Dispose()
    {
        if (Directory.Exists(_tempRoot))
        {
            Directory.Delete(_tempRoot, recursive: true);
        }
    }

    /// <summary>
    ///     Test that logging an event writes a timestamped, leveled entry to the active log file.
    /// </summary>
    [Fact]
    public void RollingFileLogger_Log_Event_WritesTimestampedEntry()
    {
        // Arrange: a logger writing into a fresh temporary directory
        var logger = new RollingFileLogger(_tempRoot);

        // Act: log one informational event
        logger.Log(LogLevel.Info, "Project opened");

        // Assert: the active file exists and holds a leveled entry
        Assert.True(File.Exists(logger.ActiveFilePath));
        var content = File.ReadAllText(logger.ActiveFilePath);
        Assert.Contains("[Info]", content);
        Assert.Contains("Project opened", content);

        // Assert: the entry begins with a parseable ISO-8601 timestamp
        var firstToken = content.Split(' ')[0];
        Assert.True(DateTimeOffset.TryParse(firstToken, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out _));
    }

    /// <summary>
    ///     Test that each severity level is recorded in the entry.
    /// </summary>
    [Fact]
    public void RollingFileLogger_Log_AllLevels_RecordsLevelTag()
    {
        // Arrange: a logger writing into a fresh temporary directory
        var logger = new RollingFileLogger(_tempRoot);

        // Act: log one event per severity
        logger.Log(LogLevel.Info, "info entry");
        logger.Log(LogLevel.Warning, "warning entry");
        logger.Log(LogLevel.Error, "error entry");

        // Assert: each level tag appears in the active file
        var content = File.ReadAllText(logger.ActiveFilePath);
        Assert.Contains("[Info] info entry", content);
        Assert.Contains("[Warning] warning entry", content);
        Assert.Contains("[Error] error entry", content);
    }

    /// <summary>
    ///     Test that logging captures optional exception details alongside the message.
    /// </summary>
    [Fact]
    public void RollingFileLogger_Log_WithException_IncludesExceptionDetails()
    {
        // Arrange: a logger and a failure to record
        var logger = new RollingFileLogger(_tempRoot);
        var exception = new InvalidOperationException("boom");

        // Act: log the failure with its exception
        logger.Log(LogLevel.Error, "Save failed", exception);

        // Assert: both the message and the exception details are present
        var content = File.ReadAllText(logger.ActiveFilePath);
        Assert.Contains("Save failed", content);
        Assert.Contains("boom", content);
    }

    /// <summary>
    ///     Test that logging a whitespace message throws ArgumentException.
    /// </summary>
    [Fact]
    public void RollingFileLogger_Log_WhitespaceMessage_ThrowsArgumentException()
    {
        // Arrange: a logger writing into a fresh temporary directory
        var logger = new RollingFileLogger(_tempRoot);

        // Act / Assert: a whitespace-only message is rejected and nothing is written
        Assert.Throws<ArgumentException>(() => logger.Log(LogLevel.Info, "   "));
        Assert.False(File.Exists(logger.ActiveFilePath));
    }

    /// <summary>
    ///     Test that concurrent writers each have their entries recorded intact.
    /// </summary>
    [Fact]
    public void RollingFileLogger_Log_ConcurrentWriters_RecordsEveryEntry()
    {
        // Arrange: a logger with a large threshold so no rotation occurs during the test
        var logger = new RollingFileLogger(_tempRoot, maximumFileSizeBytes: 10_000_000);
        const int writers = 8;
        const int entriesPerWriter = 25;

        // Act: write from several threads at once
        Parallel.For(0, writers, writer =>
        {
            for (var i = 0; i < entriesPerWriter; i++)
            {
                logger.Log(LogLevel.Info, $"writer-{writer}-entry-{i}");
            }
        });

        // Assert: one line was recorded per write
        var lines = File.ReadAllLines(logger.ActiveFilePath);
        Assert.Equal(writers * entriesPerWriter, lines.Length);
        Assert.All(lines, line => Assert.Contains("[Info] writer-", line));
    }

    /// <summary>
    ///     Test that once the active file reaches the size threshold, further logging rotates it into an
    ///     archive and prunes archives beyond the retained count.
    /// </summary>
    [Fact]
    public void RollingFileLogger_RotateIfNeeded_ThresholdReached_RotatesAndPrunesArchives()
    {
        // Arrange: three older archives, a tiny threshold so the next entry triggers a rotation, and a
        // retention policy of only two archives
        var logger = new RollingFileLogger(_tempRoot, maximumFileSizeBytes: 10, retainedFileCount: 2);
        File.WriteAllText(Path.Combine(_tempRoot, "canvaspilot-20200101000000000.log"), "oldest archive");
        File.WriteAllText(Path.Combine(_tempRoot, "canvaspilot-20200102000000000.log"), "older archive");
        File.WriteAllText(Path.Combine(_tempRoot, "canvaspilot-20200103000000000.log"), "newer archive");
        File.WriteAllText(logger.ActiveFilePath, "active file content at the threshold");

        // Act: log an entry, which rotates the full active file into a new archive and prunes
        logger.Log(LogLevel.Info, "Event after rotation");

        // Assert: only the two newest archives remain, plus the new active file
        var archives = Directory.GetFiles(_tempRoot, "canvaspilot-*.log").Select(Path.GetFileName).Order().ToList();
        Assert.Equal(2, archives.Count);
        Assert.DoesNotContain("canvaspilot-20200101000000000.log", archives);
        Assert.DoesNotContain("canvaspilot-20200102000000000.log", archives);
        Assert.Contains("canvaspilot-20200103000000000.log", archives);
        Assert.Contains("Event after rotation", File.ReadAllText(logger.ActiveFilePath));
    }

    /// <summary>
    ///     Test that rotation does not occur while the active file is below the size threshold.
    /// </summary>
    [Fact]
    public void RollingFileLogger_RotateIfNeeded_BelowThreshold_KeepsActiveFile()
    {
        // Arrange: a logger with a large threshold and one small entry
        var logger = new RollingFileLogger(_tempRoot, maximumFileSizeBytes: 10_000_000);
        logger.Log(LogLevel.Info, "small entry");

        // Act: request rotation explicitly
        logger.RotateIfNeeded();

        // Assert: the active file remains and no archive was created
        Assert.True(File.Exists(logger.ActiveFilePath));
        Assert.Empty(Directory.GetFiles(_tempRoot, "canvaspilot-*.log"));
    }

    /// <summary>
    ///     Test that flushing does not throw even when no entry has yet been written.
    /// </summary>
    [Fact]
    public void RollingFileLogger_Flush_BeforeAnyLogEntry_DoesNotThrow()
    {
        // Arrange: a logger that has not yet written any entry
        _ = new RollingFileLogger(_tempRoot);

        // Act: flush the logger
        var exception = Record.Exception(RollingFileLogger.Flush);

        // Assert: no exception is thrown
        Assert.Null(exception);
    }

    /// <summary>
    ///     Test that construction creates a missing log directory.
    /// </summary>
    [Fact]
    public void RollingFileLogger_Constructor_MissingDirectory_CreatesDirectory()
    {
        // Arrange: a nested directory that does not yet exist
        var directory = Path.Combine(_tempRoot, "nested", "logs");

        // Act: construct a logger for that directory
        var logger = new RollingFileLogger(directory);

        // Assert: the directory was created and is reported by the logger
        Assert.True(Directory.Exists(directory));
        Assert.Equal(directory, logger.LogDirectory);
    }

    /// <summary>
    ///     Test that construction rejects a non-positive maximum file size.
    /// </summary>
    [Fact]
    public void RollingFileLogger_Constructor_NonPositiveMaximumFileSize_ThrowsArgumentException()
    {
        // Arrange / Act / Assert: a zero threshold is rejected
        Assert.Throws<ArgumentException>(() => new RollingFileLogger(_tempRoot, maximumFileSizeBytes: 0));
    }

    /// <summary>
    ///     Test that construction rejects a non-positive retained file count.
    /// </summary>
    [Fact]
    public void RollingFileLogger_Constructor_NonPositiveRetainedFileCount_ThrowsArgumentException()
    {
        // Arrange / Act / Assert: a zero retention count is rejected
        Assert.Throws<ArgumentException>(() => new RollingFileLogger(_tempRoot, retainedFileCount: 0));
    }

    /// <summary>
    ///     Test that construction rejects a whitespace log directory.
    /// </summary>
    [Fact]
    public void RollingFileLogger_Constructor_WhitespaceDirectory_ThrowsArgumentException()
    {
        // Arrange / Act / Assert: a whitespace directory is rejected
        Assert.Throws<ArgumentException>(() => new RollingFileLogger("  "));
    }

    /// <summary>
    ///     Test that a base file name attempting path traversal is rejected when the log file path is resolved.
    /// </summary>
    [Fact]
    public void RollingFileLogger_Log_TraversalBaseFileName_ThrowsArgumentException()
    {
        // Arrange: a logger whose base file name attempts to leave the log directory
        var logger = new RollingFileLogger(_tempRoot, baseFileName: "../escape");

        // Act / Assert: logging is refused and nothing is written outside the log directory
        Assert.Throws<ArgumentException>(() => logger.Log(LogLevel.Info, "must not be written"));
        Assert.False(File.Exists(Path.Combine(_tempRoot, "..", "escape.log")));
    }

    /// <summary>
    ///     Test that the default log directory is the CanvasPilot logs folder under local application data.
    /// </summary>
    [Fact]
    public void RollingFileLogger_DefaultLogDirectory_Default_IsCanvasPilotLogsUnderLocalAppData()
    {
        // Arrange: the platform local application data root
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        Assert.False(string.IsNullOrWhiteSpace(localAppData), "Test host reports no local application data folder.");

        // Act: read the default log directory
        var directory = RollingFileLogger.DefaultLogDirectory;

        // Assert: it is the CanvasPilot\logs folder beneath that root
        Assert.Equal(Path.Combine(localAppData, "CanvasPilot", "logs"), directory);
    }
}
