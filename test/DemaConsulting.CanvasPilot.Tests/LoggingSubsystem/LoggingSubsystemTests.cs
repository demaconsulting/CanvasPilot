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

namespace DemaConsulting.CanvasPilot.Tests;

/// <summary>
///     Subsystem tests for the Logging subsystem covering RollingFileLogger integration workflows.
/// </summary>
public sealed class LoggingSubsystemTests : IDisposable
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
    ///     Test that an application event and a failure are persisted to a local log file.
    /// </summary>
    [Fact]
    public void LoggingSubsystem_PersistEvents_EventAndFailure_WritesLocalEntries()
    {
        // Arrange: a logger writing into a temporary directory, as the application does for its log folder
        var logger = new RollingFileLogger(_tempRoot);

        // Act: record an event and a failure the way application subsystems do
        logger.Log(LogLevel.Info, "Session started");
        logger.Log(LogLevel.Error, "Project load failed", new IOException("disk unavailable"));

        // Assert: both entries exist locally on disk, with the failure details attached
        Assert.True(File.Exists(logger.ActiveFilePath));
        var content = File.ReadAllText(logger.ActiveFilePath);
        Assert.Contains("Session started", content);
        Assert.Contains("Project load failed", content);
        Assert.Contains("disk unavailable", content);
    }

    /// <summary>
    ///     Test that sustained logging stays within the retention limit instead of growing without bound.
    /// </summary>
    [Fact]
    public void LoggingSubsystem_RetainHistory_SustainedLogging_PrunesOldArchives()
    {
        // Arrange: five older archives, a tiny threshold so every entry forces a rotation, and a retention
        // policy of three archives
        var logger = new RollingFileLogger(_tempRoot, maximumFileSizeBytes: 5, retainedFileCount: 3);
        for (var day = 1; day <= 5; day++)
        {
            File.WriteAllText(Path.Combine(_tempRoot, $"canvaspilot-2020010{day}000000000.log"), $"archive {day}");
        }

        // Act: log well beyond the retention limit
        for (var i = 0; i < 10; i++)
        {
            logger.Log(LogLevel.Info, $"Entry {i}");
        }

        // Assert: exactly the retained number of archives remains and the oldest history was pruned
        var archives = Directory.GetFiles(_tempRoot, "canvaspilot-*.log");
        Assert.Equal(3, archives.Length);
        Assert.False(File.Exists(Path.Combine(_tempRoot, "canvaspilot-20200101000000000.log")));
    }

    /// <summary>
    ///     Test that the logger resolves its log files through the safe path utilities, rejecting a base file
    ///     name that would escape the log directory.
    /// </summary>
    [Fact]
    public void LoggingSubsystem_SafePaths_TraversalBaseFileName_ThrowsArgumentException()
    {
        // Arrange: a logger whose base file name attempts to leave the log directory
        var logger = new RollingFileLogger(_tempRoot, baseFileName: "../escape");

        // Act / Assert: logging is refused rather than writing outside the log directory
        Assert.Throws<ArgumentException>(() => logger.Log(LogLevel.Info, "must not be written"));
        Assert.False(File.Exists(Path.Combine(_tempRoot, "..", "escape.log")));
    }
}
