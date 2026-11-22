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

using DemaConsulting.CanvasPilot.AgentSubsystem;

namespace DemaConsulting.CanvasPilot.Tests;

/// <summary>
///     Unit tests for <see cref="ProcessRunner" />, which start the command interpreter of the operating system with a
///     trivial command: the only real programs that every machine that runs the tests has.
/// </summary>
public sealed class ProcessRunnerTests
{
    /// <summary>
    ///     Makes the program and the arguments that run a one-line script in the shell of the platform.
    /// </summary>
    /// <param name="windowsScript">The script for <c>cmd.exe</c>.</param>
    /// <param name="unixScript">The script for <c>/bin/sh</c>.</param>
    /// <returns>The full path of the shell and the arguments.</returns>
    private static (string Path, string[] Arguments) Shell(string windowsScript, string unixScript)
    {
        return OperatingSystem.IsWindows()
            ? (Path.Combine(Environment.SystemDirectory, "cmd.exe"), ["/c", windowsScript])
            : ("/bin/sh", ["-c", unixScript]);
    }

    /// <summary>
    ///     Test that a command that prints a line is run, and that the line, the exit code, and the start are reported.
    /// </summary>
    [Fact]
    public async Task ProcessRunner_RunAsync_EchoCommand_ReturnsItsOutput()
    {
        // Arrange: a shell that prints one line
        var (path, arguments) = Shell("echo hello", "echo hello");

        // Act: run it
        var result = await new ProcessRunner().RunAsync(path, arguments, TimeSpan.FromSeconds(30), TestContext.Current.CancellationToken);

        // Assert: it started, ended with zero, did not time out, and printed the line
        Assert.True(result.Started);
        Assert.Equal(0, result.ExitCode);
        Assert.False(result.TimedOut);
        Assert.Equal("hello", result.Output);
    }

    /// <summary>
    ///     Test that the exit code of the program is reported, and that standard output and standard error are joined in
    ///     the output.
    /// </summary>
    [Fact]
    public async Task ProcessRunner_RunAsync_FailingCommand_ReturnsExitCodeAndBothStreams()
    {
        // Arrange: a shell that prints to both streams and exits with 3
        var (path, arguments) = Shell("echo out& echo err>&2& exit 3", "echo out; echo err >&2; exit 3");

        // Act: run it
        var result = await new ProcessRunner().RunAsync(path, arguments, TimeSpan.FromSeconds(30), TestContext.Current.CancellationToken);

        // Assert: the code is kept and both lines are in the output, the standard output first
        Assert.True(result.Started);
        Assert.Equal(3, result.ExitCode);
        Assert.Equal("out" + Environment.NewLine + "err", result.Output);
    }

    /// <summary>
    ///     Test that each argument reaches the program as one argument, so that an argument with a space is not split.
    /// </summary>
    [Fact]
    public async Task ProcessRunner_RunAsync_ArgumentWithSpaces_ArrivesAsOneArgument()
    {
        // Arrange: a shell whose script has spaces and is passed as a single list entry
        var (path, arguments) = Shell("echo one two  three", "echo 'one two  three'");

        // Act: run it
        var result = await new ProcessRunner().RunAsync(path, arguments, TimeSpan.FromSeconds(30), TestContext.Current.CancellationToken);

        // Assert: the script ran as one command
        Assert.Equal("one two  three", result.Output);
    }

    /// <summary>
    ///     Test that a program that runs longer than the time allowed is reported as timed out, and that it, with the
    ///     program that it started, was killed: the output that the program printed before is complete only when the
    ///     streams were closed by the end of the whole tree.
    /// </summary>
    [Fact]
    public async Task ProcessRunner_RunAsync_LongRunningCommand_TimesOutAndKillsTheTree()
    {
        // Arrange: a shell that prints a line and then starts a program that would run for half a minute
        var (path, arguments) = Shell(
            "echo started& ping -n 30 127.0.0.1 >nul",
            "echo started; sleep 30");

        // Act: run it with a time limit that is long enough for the line to be printed
        var result = await new ProcessRunner().RunAsync(path, arguments, TimeSpan.FromSeconds(4), TestContext.Current.CancellationToken);

        // Assert: it timed out, and the line was collected, which needs the pipes to have been closed by the kill
        Assert.True(result.Started);
        Assert.True(result.TimedOut);
        Assert.Equal(-1, result.ExitCode);
        Assert.Equal("started", result.Output);
    }

    /// <summary>
    ///     Test that canceling the token stops the program and throws, and is not reported as a time out.
    /// </summary>
    [Fact]
    public async Task ProcessRunner_RunAsync_CancelledToken_ThrowsOperationCanceled()
    {
        // Arrange: a shell that would run for half a minute, and a token that is cancelled soon
        var (path, arguments) = Shell("ping -n 30 127.0.0.1 >nul", "sleep 30");
        using var source = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        source.CancelAfter(TimeSpan.FromMilliseconds(500));

        // Act / Assert: the run is cancelled
        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => new ProcessRunner().RunAsync(path, arguments, TimeSpan.FromSeconds(60), source.Token));
    }

    /// <summary>
    ///     Test that a program that does not exist is a result that says it did not start, and not an exception.
    /// </summary>
    [Fact]
    public async Task ProcessRunner_RunAsync_MissingProgram_ReturnsNotStarted()
    {
        // Arrange: a path in a folder that does not exist
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"), "no-such-program.exe");

        // Act: run it
        var result = await new ProcessRunner().RunAsync(path, [], TimeSpan.FromSeconds(30), TestContext.Current.CancellationToken);

        // Assert: it did not start, with a reason and no exit code
        Assert.False(result.Started);
        Assert.False(result.TimedOut);
        Assert.Equal(-1, result.ExitCode);
        Assert.NotEmpty(result.Output);
    }

    /// <summary>
    ///     Test that a missing path, a missing list of arguments, and a time limit of zero are refused as programming errors.
    /// </summary>
    [Fact]
    public async Task ProcessRunner_RunAsync_InvalidArguments_AreRefused()
    {
        // Arrange: a runner
        var runner = new ProcessRunner();

        // Act / Assert: each invalid argument raises its exception
        var token = TestContext.Current.CancellationToken;
        await Assert.ThrowsAsync<ArgumentException>(() => runner.RunAsync(" ", [], TimeSpan.FromSeconds(1), token));
        await Assert.ThrowsAsync<ArgumentNullException>(() => runner.RunAsync("x", null!, TimeSpan.FromSeconds(1), token));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => runner.RunAsync("x", [], TimeSpan.Zero, token));
    }

    /// <summary>
    ///     Test that the result of a program that did not start has the documented shape.
    /// </summary>
    [Fact]
    public void ProcessResult_NotStarted_Reason_HasNoExitCode()
    {
        // Arrange / Act: make the result
        var result = ProcessResult.NotStarted("denied");

        // Assert: not started, not timed out, and the reason is the output
        Assert.False(result.Started);
        Assert.False(result.TimedOut);
        Assert.Equal(-1, result.ExitCode);
        Assert.Equal("denied", result.Output);
    }

    /// <summary>
    ///     Test that the real environment reader returns a variable that is set and nothing for one that is not.
    /// </summary>
    [Fact]
    public void SystemEnvironmentReader_GetVariable_SetAndUnsetVariables_ReturnValueOrNull()
    {
        // Arrange: a reader and a name that cannot be set
        var reader = new SystemEnvironmentReader();

        // Act / Assert: PATH is set on every machine that runs the tests; the made-up name is not
        Assert.False(string.IsNullOrEmpty(reader.GetVariable("PATH")));
        Assert.Null(reader.GetVariable("CANVASPILOT_TEST_VARIABLE_THAT_IS_NEVER_SET"));
    }

    /// <summary>
    ///     Test that the real file system answers for a file that exists, for one that does not, for a folder listing, and
    ///     for a path that is not a link.
    /// </summary>
    [Fact]
    public void CliFileSystem_Queries_RealFiles_AnswerFromTheDisk()
    {
        // Arrange: a folder with one file and a sub-folder that match the pattern
        var folder = Path.Combine(Path.GetTempPath(), "CanvasPilotCli-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(folder, "Pack_one"));
        Directory.CreateDirectory(Path.Combine(folder, "Other"));
        var file = Path.Combine(folder, "Pack_one", "copilot.exe");
        File.WriteAllBytes(file, new byte[1234]);
        var fileSystem = new CliFileSystem();

        try
        {
            // Act: ask the file system
            var exists = fileSystem.FileExists(file);
            var missing = fileSystem.FileExists(file + ".missing");
            var length = fileSystem.GetLength(file);
            var missingLength = fileSystem.GetLength(file + ".missing");
            var resolved = fileSystem.ResolveLinkTarget(file);
            var matching = fileSystem.EnumerateDirectories(folder, "Pack_*");
            var none = fileSystem.EnumerateDirectories(Path.Combine(folder, "nowhere"), "*");

            // Assert: the answers follow the disk, and a path that is not a link resolves to itself
            Assert.True(exists);
            Assert.False(missing);
            Assert.Equal(1234, length);
            Assert.Equal(0, missingLength);
            Assert.Equal(file, resolved);
            Assert.Equal([Path.Combine(folder, "Pack_one")], matching);
            Assert.Empty(none);
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }
}
