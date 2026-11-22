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

using DemaConsulting.AgentKit.Agents.Copilot;
using DemaConsulting.CanvasPilot.AgentSubsystem;
using NSubstitute;

namespace DemaConsulting.CanvasPilot.Tests;

/// <summary>
///     Unit tests for <see cref="CopilotRuntimeStarter" /> over an injected start function, so that no Copilot CLI is run
///     and no credentials are needed: the options that reach the library (the installed CLI, the path that the locator
///     found, the data folder, and no token), the one runtime that is shared, the restart on another CLI or folder, the
///     disposal, and every way that a start fails turned into a status and a sentence.
/// </summary>
public sealed class CopilotRuntimeStarterTests
{
    /// <summary>The version that the locator reports for the CLI.</summary>
    private static readonly Version CliVersion = new(1, 0, 114);

    /// <summary>The full path of the CLI that the locator found; it need not exist because the start function is a stand-in.</summary>
    private static readonly string CliPath = Path.Combine(Path.GetTempPath(), "canvaspilot-starter-cli", "copilot.exe");

    /// <summary>The folder for the data of the runtime that the starter is made with.</summary>
    private static readonly string DataDirectory = Path.Combine(Path.GetTempPath(), "canvaspilot-starter-data");

    /// <summary>
    ///     Makes the request for the CLI that the locator found.
    /// </summary>
    /// <param name="cliPath">The path of the CLI; the default CLI when <see langword="null" />.</param>
    /// <returns>The request.</returns>
    private static CopilotStartRequest Request(string? cliPath = null)
    {
        return new CopilotStartRequest(cliPath ?? CliPath, CliVersion);
    }

    /// <summary>
    ///     Makes a start function that records the options and gives a new fake runtime each time.
    /// </summary>
    /// <param name="options">Receives the options of each start, in order.</param>
    /// <param name="sources">Receives the runtimes that were made, in order.</param>
    /// <returns>The function.</returns>
    private static Func<CopilotRuntimeOptions, CancellationToken, Task<ICopilotAgentSource>> Recording(
        List<CopilotRuntimeOptions> options,
        List<FakeCopilotAgentSource> sources)
    {
        return (given, _) =>
        {
            options.Add(given);
            var source = new FakeCopilotAgentSource(new ScriptedChatClient());
            sources.Add(source);
            return Task.FromResult<ICopilotAgentSource>(source);
        };
    }

    /// <summary>
    ///     Test that the constants of the unit are the documented sentences.
    /// </summary>
    [Fact]
    public void CopilotRuntimeStarter_Constants_Rule_AreTheDocumentedSentences()
    {
        // Assert: the sign-in sentence names the ways that work and says that the CLI's own login does not apply
        Assert.Equal(
            "Copilot is not signed in for CanvasPilot. Sign in with `gh auth login` (or set the COPILOT_GITHUB_TOKEN, GH_TOKEN, or GITHUB_TOKEN environment variable), then choose Check connection. Signing in to the Copilot CLI with /login does not apply to CanvasPilot.",
            CopilotRuntimeStarter.NotSignedInMessage);
        Assert.Equal("The Copilot CLI path in Settings cannot be used: ", CopilotRuntimeStarter.CliPathMessage);
        Assert.Equal("The Copilot runtime could not be started. Choose Check connection to try again.", CopilotRuntimeStarter.StartFailedMessage);
    }

    /// <summary>
    ///     Test that a start runs the installed CLI at the path that the locator found, with the data folder of the starter,
    ///     the logger of the request, and no token, and answers started with the runtime as the source of agents.
    /// </summary>
    [Fact]
    public async Task CopilotRuntimeStarter_StartAsync_InstalledCli_PassesThePathTheFolderAndNoToken()
    {
        // Arrange
        var options = new List<CopilotRuntimeOptions>();
        var sources = new List<FakeCopilotAgentSource>();
        await using var starter = new CopilotRuntimeStarter(DataDirectory, Recording(options, sources));
        var logger = Substitute.For<Microsoft.Extensions.Logging.ILogger>();

        // Act
        var result = await starter.StartAsync(Request() with { Logger = logger }, TestContext.Current.CancellationToken);

        // Assert: the installed CLI at the found path, the folder, the logger, and no token
        Assert.Equal(CopilotStartStatus.Started, result.Status);
        Assert.Equal("GitHub Copilot CLI 1.0.114 is running.", result.Message);
        Assert.Same(Assert.Single(sources), result.Agents);
        var given = Assert.Single(options);
        Assert.Equal(CopilotRuntimeSource.InstalledCli, given.RuntimeSource);
        Assert.Equal(CliPath, given.CliPath);
        Assert.Equal(DataDirectory, given.DataDirectory);
        Assert.Same(logger, given.Logger);
        Assert.Null(given.GitHubToken);
    }

    /// <summary>
    ///     Test that the folder of the request wins over the folder that the starter was made with.
    /// </summary>
    [Fact]
    public async Task CopilotRuntimeStarter_StartAsync_RequestNamesAFolder_TheFolderOfTheRequestIsUsed()
    {
        // Arrange
        var options = new List<CopilotRuntimeOptions>();
        await using var starter = new CopilotRuntimeStarter(DataDirectory, Recording(options, []));
        var requested = Path.Combine(Path.GetTempPath(), "canvaspilot-starter-requested");

        // Act
        await starter.StartAsync(Request() with { DataDirectory = requested }, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(requested, Assert.Single(options).DataDirectory);
    }

    /// <summary>
    ///     Test that a starter without a folder of its own uses the folder of the application below the local data of the
    ///     user, never a folder that the library chooses.
    /// </summary>
    [Fact]
    public async Task CopilotRuntimeStarter_StartAsync_NoFolderAnywhere_UsesTheApplicationFolder()
    {
        // Arrange
        var options = new List<CopilotRuntimeOptions>();
        await using var starter = new CopilotRuntimeStarter(start: Recording(options, []));

        // Act
        await starter.StartAsync(Request(), TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(AgentService.GetCopilotDataDirectory(), Assert.Single(options).DataDirectory);
    }

    /// <summary>
    ///     Test that asking again for the same CLI and folder gives the same running runtime without starting another, also
    ///     when many ask at once.
    /// </summary>
    [Fact]
    public async Task CopilotRuntimeStarter_StartAsync_SameCliAndFolder_SharesOneRuntime()
    {
        // Arrange: a start function that takes a moment, so that concurrent asks overlap
        var token = TestContext.Current.CancellationToken;
        var starts = 0;
        var source = new FakeCopilotAgentSource(new ScriptedChatClient());
        await using var starter = new CopilotRuntimeStarter(
            DataDirectory,
            async (_, cancellation) =>
            {
                Interlocked.Increment(ref starts);
                await Task.Delay(50, cancellation);
                return source;
            });

        // Act: eight asks at once, and one more afterwards
        var results = await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => starter.StartAsync(Request(), token)));
        var later = await starter.StartAsync(Request(), token);

        // Assert: one runtime was started and everybody got it, and it was not disposed
        Assert.Equal(1, starts);
        Assert.All(results, result => Assert.Same(source, result.Agents));
        Assert.Same(source, later.Agents);
        Assert.Equal(0, source.DisposeCount);
    }

    /// <summary>
    ///     Test that a request for another CLI, or for another folder, stops the old runtime and starts a new one, because
    ///     only one runtime may use a data folder.
    /// </summary>
    [Fact]
    public async Task CopilotRuntimeStarter_StartAsync_OtherCliOrOtherFolder_ReplacesTheRuntime()
    {
        // Arrange
        var token = TestContext.Current.CancellationToken;
        var sources = new List<FakeCopilotAgentSource>();
        await using var starter = new CopilotRuntimeStarter(DataDirectory, Recording([], sources));
        var other = Path.Combine(Path.GetTempPath(), "canvaspilot-starter-other", "copilot.exe");

        // Act: start, ask for another CLI, and ask for another folder
        var first = await starter.StartAsync(Request(), token);
        var second = await starter.StartAsync(Request(other), token);
        var third = await starter.StartAsync(Request(other) with { DataDirectory = Path.Combine(Path.GetTempPath(), "canvaspilot-starter-third") }, token);

        // Assert: three runtimes, each replaced runtime was stopped once, and the last one runs
        Assert.Equal(3, sources.Count);
        Assert.Same(sources[0], first.Agents);
        Assert.Same(sources[1], second.Agents);
        Assert.Same(sources[2], third.Agents);
        Assert.Equal([1, 1, 0], sources.Select(source => source.DisposeCount).ToArray());
    }

    /// <summary>
    ///     Test that a failed start is not remembered: the next ask starts again and can succeed.
    /// </summary>
    [Fact]
    public async Task CopilotRuntimeStarter_StartAsync_FailureThenSuccess_TriesAgain()
    {
        // Arrange: a start function that fails once
        var token = TestContext.Current.CancellationToken;
        var calls = 0;
        var source = new FakeCopilotAgentSource(new ScriptedChatClient());
        await using var starter = new CopilotRuntimeStarter(
            DataDirectory,
            (_, _) => ++calls == 1 ? throw new InvalidOperationException("Not this time.") : Task.FromResult<ICopilotAgentSource>(source));

        // Act
        var failed = await starter.StartAsync(Request(), token);
        var started = await starter.StartAsync(Request(), token);

        // Assert
        Assert.Equal((CopilotStartStatus.Failed, "Not this time.", null), (failed.Status, failed.Message, failed.Agents));
        Assert.Equal(CopilotStartStatus.Started, started.Status);
        Assert.Same(source, started.Agents);
    }

    /// <summary>
    ///     Test that each way that the library says a start failed becomes the matching status and sentence, with no
    ///     runtime handed out.
    /// </summary>
    /// <param name="message">The message of the exception of the library.</param>
    /// <param name="expected">The status that the answer must have.</param>
    /// <param name="sentence">The sentence that the answer must carry; <see langword="null" /> for the message of the library.</param>
    [Theory]
    [InlineData("The Copilot runtime started but is not signed in to GitHub (the runtime reports: Not authenticated), so AgentKit stopped it.", CopilotStartStatus.NotSignedIn, null)]
    [InlineData("The Copilot CLI at 'C:\\x\\copilot.exe' is version 1.0.106, which is too old for AgentKit: it needs version 1.0.113 or later.", CopilotStartStatus.CliTooOld, null)]
    [InlineData("The Copilot runtime refused: protocol version mismatch, the runtime speaks 3.", CopilotStartStatus.CliTooOld, null)]
    [InlineData("The Copilot CLI named in the options does not exist.", CopilotStartStatus.Failed, null)]
    [InlineData("   ", CopilotStartStatus.Failed, CopilotRuntimeStarter.StartFailedMessage)]
    public async Task CopilotRuntimeStarter_StartAsync_LibraryFailure_BecomesTheMatchingStatusAndSentence(
        string message,
        CopilotStartStatus expected,
        string? sentence)
    {
        // Arrange: a start function that fails the way the library does
        await using var starter = new CopilotRuntimeStarter(DataDirectory, (_, _) => throw new InvalidOperationException(message));

        // Act
        var result = await starter.StartAsync(Request(), TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(expected, result.Status);
        Assert.Equal(sentence ?? (expected == CopilotStartStatus.NotSignedIn ? CopilotRuntimeStarter.NotSignedInMessage : message), result.Message);
        Assert.Null(result.Agents);
    }

    /// <summary>
    ///     Test that a path that the library refuses (one that is not fully qualified) is answered with a sentence that
    ///     names the Settings and the cause, without the name of the parameter, and that nothing is started.
    /// </summary>
    [Fact]
    public async Task CopilotRuntimeStarter_StartAsync_PathTheLibraryRefuses_ReportsItAsASentenceAndStartsNothing()
    {
        // Arrange
        var options = new List<CopilotRuntimeOptions>();
        await using var starter = new CopilotRuntimeStarter(DataDirectory, Recording(options, []));

        // Act: a relative path, which the library refuses
        var result = await starter.StartAsync(Request("copilot.exe"), TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(CopilotStartStatus.Failed, result.Status);
        Assert.StartsWith(CopilotRuntimeStarter.CliPathMessage, result.Message, StringComparison.Ordinal);
        Assert.Contains("not fully qualified", result.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("(Parameter", result.Message, StringComparison.Ordinal);
        Assert.Empty(options);
    }

    /// <summary>
    ///     Test that a cancelled start is an exception and not a result, and is not remembered.
    /// </summary>
    [Fact]
    public async Task CopilotRuntimeStarter_StartAsync_Cancelled_ThrowsAndTheNextAskStartsAgain()
    {
        // Arrange: a start function that waits for its token, and then one that works
        var calls = 0;
        var source = new FakeCopilotAgentSource(new ScriptedChatClient());
        await using var starter = new CopilotRuntimeStarter(
            DataDirectory,
            async (_, cancellation) =>
            {
                if (++calls == 1)
                {
                    await Task.Delay(Timeout.Infinite, cancellation);
                }

                return source;
            });
        using var cancel = new CancellationTokenSource(TimeSpan.FromMilliseconds(100));

        // Act and assert: the first ask is cancelled; the second one starts a runtime
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => starter.StartAsync(Request(), cancel.Token));
        var result = await starter.StartAsync(Request(), TestContext.Current.CancellationToken);
        Assert.Equal(CopilotStartStatus.Started, result.Status);
    }

    /// <summary>
    ///     Test that disposing stops the runtime once, that disposing again does nothing, and that a start after that is
    ///     refused.
    /// </summary>
    [Fact]
    public async Task CopilotRuntimeStarter_DisposeAsync_StopsTheRuntimeOnceAndRefusesFurtherStarts()
    {
        // Arrange
        var token = TestContext.Current.CancellationToken;
        var sources = new List<FakeCopilotAgentSource>();
        var starter = new CopilotRuntimeStarter(DataDirectory, Recording([], sources));
        await starter.StartAsync(Request(), token);

        // Act
        await starter.DisposeAsync();
        await starter.DisposeAsync();

        // Assert
        Assert.Equal(1, Assert.Single(sources).DisposeCount);
        await Assert.ThrowsAsync<ObjectDisposedException>(() => starter.StartAsync(Request(), token));
    }

    /// <summary>
    ///     Test that missing arguments are refused.
    /// </summary>
    [Fact]
    public async Task CopilotRuntimeStarter_StartAsync_BadArguments_AreRefused()
    {
        // Arrange
        await using var starter = new CopilotRuntimeStarter(DataDirectory, Recording([], []));

        // Act and assert
        await Assert.ThrowsAsync<ArgumentNullException>(() => starter.StartAsync(null!, TestContext.Current.CancellationToken));
        await Assert.ThrowsAsync<ArgumentException>(() => starter.StartAsync(new CopilotStartRequest(" ", CliVersion), TestContext.Current.CancellationToken));
    }
}
