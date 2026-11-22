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

using System.Reflection;
using Microsoft.Copilot;

namespace OtsSoftwareTests;

/// <summary>
///     Self-validation tests for the Microsoft.Copilot.SDK off-the-shelf software item: the client library that
///     AgentKit's Copilot package drives the GitHub Copilot CLI with. No Copilot CLI is started and no language model
///     is used; the tests check the facts that the gating of Copilot in CanvasPilot rests on.
/// </summary>
public class CopilotSdkTests
{
    /// <summary>
    ///     The oldest Copilot CLI that CanvasPilot documents as usable: the first version whose protocol is the one
    ///     that the SDK speaks.
    /// </summary>
    private static readonly Version CanvasPilotMinimumCliVersion = new(1, 0, 113);

    /// <summary>
    ///     Test that the Copilot CLI version that the SDK is built for is not older than the minimum that CanvasPilot
    ///     documents, so that the version check that CanvasPilot shows its users never asks for less than the SDK needs.
    /// </summary>
    [Fact]
    public void CopilotSdk_BuiltForCliVersion_IsNotBelowCanvasPilotMinimum()
    {
        // Arrange: the version of the CLI that the SDK package says it is built for, recorded in the test assembly
        var recorded = typeof(CopilotSdkTests).Assembly
            .GetCustomAttributes<AssemblyMetadataAttribute>()
            .Single(attribute => attribute.Key == "CopilotCliVersion")
            .Value;

        // Act: read it as a version
        var parsed = Version.TryParse(recorded, out var builtFor);

        // Assert: it is a version, and CanvasPilot's minimum is not above it
        Assert.True(parsed, $"The recorded CLI version '{recorded}' is not a version.");
        Assert.True(
            CanvasPilotMinimumCliVersion <= builtFor,
            $"CanvasPilot's minimum CLI {CanvasPilotMinimumCliVersion} is above the CLI {builtFor} that the SDK is built for.");
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
