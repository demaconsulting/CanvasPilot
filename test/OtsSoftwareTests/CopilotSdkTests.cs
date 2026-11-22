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
using Microsoft.Copilot;

namespace OtsSoftwareTests;

/// <summary>
///     Self-validation tests for the Microsoft.Copilot.SDK off-the-shelf software item: the client library that
///     AgentKit's Copilot package drives the GitHub Copilot CLI with. No Copilot CLI is started and no language model
///     is used; the tests check the facts that the use of an installed Copilot CLI in CanvasPilot rests on: the path
///     of a CLI that a connection names is the one that the SDK runs, and the oldest CLI that the library runs is not
///     newer than the one that CanvasPilot asks the user to install.
/// </summary>
public class CopilotSdkTests
{
    /// <summary>
    ///     Test that the oldest Copilot CLI version that the AgentKit Copilot library runs (the one that matches the
    ///     protocol of the SDK it is built on) is not newer than the minimum that CanvasPilot documents and checks, so
    ///     that the version check that CanvasPilot shows its users never passes a CLI that the library then refuses.
    /// </summary>
    [Fact]
    public void CopilotSdk_AgentKitMinimumCliVersion_IsNotAboveCanvasPilotMinimum()
    {
        // Act: read the minimum of the library and the minimum that CanvasPilot checks
        var library = CopilotCli.MinimumVersion;
        var canvasPilot = CopilotCliLocator.MinimumCliVersion;

        // Assert: CanvasPilot never asks for less than the library needs
        Assert.True(
            library <= canvasPilot,
            $"The Copilot CLI {library} that the library needs is newer than the CLI {canvasPilot} that CanvasPilot asks for.");
    }

    /// <summary>
    ///     Test that a connection that names the path of a Copilot CLI takes the SDK to that path: starting a client
    ///     with a path that does not exist fails and the error names that path, which shows that an explicit path has
    ///     the top precedence over the bundled runtime.
    /// </summary>
    [Fact]
    public async Task CopilotSdk_Client_StartAsync_ExplicitMissingCliPath_FailsNamingThePath()
    {
        // Arrange: a client whose connection names a CLI that does not exist
        using var folder = new TestFolder();
        var missing = Path.Combine(folder.Path, OperatingSystem.IsWindows() ? "no-such-copilot.exe" : "no-such-copilot");
        var options = new CopilotClientOptions
        {
            Mode = CopilotClientMode.Empty,
            Runtime = RuntimeConnection.ForSpawnedStdio(path: missing, baseDirectory: Path.Combine(folder.Path, "data"))
        };
        await using var client = new CopilotClient(options);
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(60));

        // Act: try to start it
        var failure = await Assert.ThrowsAnyAsync<Exception>(() => client.StartAsync(timeout.Token));

        // Assert: the error names the path that was given, and not the bundled runtime wrapper
        Assert.Contains("no-such-copilot", failure.ToString(), StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    ///     Test that a client whose connection supplies its own environment, which holds no CLI path, fails to start
    ///     in an application that carries no Copilot runtime, with an error that names the missing runtime wrapper,
    ///     whatever <c>COPILOT_CLI_PATH</c> the host process has: the SDK reads the variable from the environment of the
    ///     connection and not from the process.
    /// </summary>
    [Fact]
    public async Task CopilotSdk_Client_StartAsync_OwnEnvironmentWithoutCliPath_FailsNamingTheWrapper()
    {
        // Arrange: a client with its own empty environment, which looks for the runtime beside the application
        using var folder = new TestFolder();
        var options = new CopilotClientOptions
        {
            Mode = CopilotClientMode.Empty,
            Runtime = RuntimeConnection.ForSpawnedStdio(
                environment: new Dictionary<string, string>(),
                baseDirectory: Path.Combine(folder.Path, "data"))
        };
        await using var client = new CopilotClient(options);
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(60));

        // Act: try to start it
        var failure = await Assert.ThrowsAnyAsync<Exception>(() => client.StartAsync(timeout.Token));

        // Assert: the error names the wrapper that is missing
        Assert.Contains("copilot-runtime", failure.ToString(), StringComparison.OrdinalIgnoreCase);
    }
}
