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
///     Unit tests for <see cref="CopilotCliLocator" />, with a fake file system, environment, and process runner, so that no
///     program is run and the layout of a real machine (the paths and sizes of the development machine) is reproduced
///     exactly.
/// </summary>
public sealed class CopilotCliLocatorTests
{
    /// <summary>
    ///     Test that the text of <c>--version</c> yields the three numbers of the version, with or without the trailing
    ///     period, the line that suggests an update, or a pre-release suffix, and that anything else yields none.
    /// </summary>
    /// <param name="output">The text that the program printed.</param>
    /// <param name="expected">The version that is expected, or <see langword="null" /> for none.</param>
    [Theory]
    [InlineData("GitHub Copilot CLI 1.0.114.", "1.0.114")]
    [InlineData("GitHub Copilot CLI 1.0.114.\nRun 'copilot update' to check for updates.", "1.0.114")]
    [InlineData("GitHub Copilot CLI 1.0.114.\r\nRun 'copilot update' to check for updates.\r\n", "1.0.114")]
    [InlineData("GitHub Copilot CLI 2.10.3-beta.1", "2.10.3")]
    [InlineData("warning: something\nGitHub Copilot CLI 1.0.113", "1.0.113")]
    [InlineData("1.0.114", null)]
    [InlineData("GitHub Copilot CLI 1.0", null)]
    [InlineData("GitHub Copilot CLI x.y.z", null)]
    [InlineData("GitHub Copilot CLI 99999999999.0.0", null)]
    [InlineData("hello world", null)]
    [InlineData("", null)]
    [InlineData(null, null)]
    public void CopilotCliLocator_TryParseVersion_OutputText_ReadsVersionOrNone(string? output, string? expected)
    {
        // Arrange / Act: read the version from the text
        var parsed = CopilotCliLocator.TryParseVersion(output, out var version);

        // Assert: a version is read exactly when one is expected
        Assert.Equal(expected is not null, parsed);
        Assert.Equal(expected is null ? null : Version.Parse(expected), version);
    }

    /// <summary>
    ///     Test that the minimum version is 1.0.113 and that comparing with it accepts it and every newer version and
    ///     rejects every older one.
    /// </summary>
    /// <param name="version">The version to compare.</param>
    /// <param name="expected">Whether it is new enough.</param>
    [Theory]
    [InlineData("1.0.106", false)]
    [InlineData("1.0.112", false)]
    [InlineData("0.9.999", false)]
    [InlineData("1.0.113", true)]
    [InlineData("1.0.114", true)]
    [InlineData("1.1.0", true)]
    [InlineData("2.0.0", true)]
    public void CopilotCliLocator_IsSupportedVersion_Versions_AcceptsOnlyTheMinimumAndNewer(string version, bool expected)
    {
        // Arrange / Act: compare with the minimum
        var supported = CopilotCliLocator.IsSupportedVersion(Version.Parse(version));

        // Assert: the minimum is 1.0.113 and the answer follows from it
        Assert.Equal(new Version(1, 0, 113), CopilotCliLocator.MinimumCliVersion);
        Assert.Equal(expected, supported);
    }

    /// <summary>
    ///     Test that each folder name of the shim list, and the start of the name of a VS Code installation, marks a path as
    ///     a shim, in any case and with either separator, and that the real installation, and a file that only has such a
    ///     name, do not.
    /// </summary>
    /// <param name="path">The full path of a file.</param>
    /// <param name="expected">Whether it lies in a shim folder.</param>
    [Theory]
    [InlineData(@"C:\Users\dev\AppData\Local\Programs\Microsoft VS Code\bin\copilot-shim\copilot.exe", true)]
    [InlineData(@"C:\tools\copilot-shim\copilot.exe", true)]
    [InlineData(@"C:\Users\dev\.vscode\extensions\x\copilot.exe", true)]
    [InlineData(@"C:\Users\dev\.vscode-insiders\copilot.exe", true)]
    [InlineData(@"C:\Users\dev\.vscode\extensions\github.copilot-chat-0.1\copilot.exe", true)]
    [InlineData(@"C:\Users\dev\x\GITHUB.COPILOT-CHAT\bin\copilot.exe", true)]
    [InlineData(@"C:\Program Files\Microsoft VS Code Insiders\bin\copilot.exe", true)]
    [InlineData("C:/Program Files/microsoft vs code/bin/copilot.exe", true)]
    [InlineData(DevMachine.RealCli, false)]
    [InlineData(@"C:\tools\copilot.exe", false)]
    [InlineData(@"C:\tools\copilot-shim.exe", false)]
    [InlineData(@"C:\tools\vscode\copilot.exe", false)]
    public void CopilotCliLocator_IsShimPath_Paths_MatchesTheShimFolderList(string path, bool expected)
    {
        // Arrange / Act: judge the path
        var shim = CopilotCliLocator.IsShimPath(path);

        // Assert: only a folder of the list, or one that starts with the VS Code name, makes a shim
        Assert.Equal(expected, shim);
    }

    /// <summary>
    ///     Test that the list of shim folder names is exactly the one of the rule, so that a change of the rule is a
    ///     deliberate change of this test.
    /// </summary>
    [Fact]
    public void CopilotCliLocator_ShimDirectoryNames_Rule_ListsTheDocumentedNames()
    {
        // Arrange / Act: read the constants of the rule
        var names = CopilotCliLocator.ShimDirectoryNames;

        // Assert: the four names and the prefix of the rule
        Assert.Equal(["copilot-shim", ".vscode", ".vscode-insiders", "github.copilot-chat"], names);
        Assert.Equal("Microsoft VS Code", CopilotCliLocator.ShimDirectoryPrefix);
        Assert.Equal(20L * 1024 * 1024, CopilotCliLocator.MinimumCliBytes);
        Assert.Equal(TimeSpan.FromSeconds(10), CopilotCliLocator.VersionProbeTimeout);
    }

    /// <summary>
    ///     Test that on the layout and the PATH of the development machine the real CLI is found: the winget package
    ///     folder first, the link to it counted once, the shim and its script never started, and the check run once with
    ///     the documented arguments and time limit.
    /// </summary>
    [Fact]
    public async Task CopilotCliLocator_Locate_DevelopmentMachine_FindsTheRealCliAndNotTheShim()
    {
        // Arrange: the files and PATH of the development machine, where the real CLI answers
        var runner = new FakeProcessRunner();
        runner.AnswerVersion(DevMachine.RealCli, "1.0.114");
        runner.AnswerVersion(DevMachine.ShimCli, "1.0.114");
        var locator = new CopilotCliLocator(runner, DevMachine.CreateFileSystem(), DevMachine.CreateEnvironment());

        // Act: search automatically
        var info = await locator.LocateAsync(cancellationToken: TestContext.Current.CancellationToken);

        // Assert: the real CLI was found, the shim was rejected for its folder, and only the real CLI was run
        Assert.Equal(CopilotCliStatus.Found, info.Status);
        Assert.True(info.IsFound);
        Assert.Equal(DevMachine.RealCli, info.Path);
        Assert.Equal(new Version(1, 0, 114), info.Version);
        Assert.Contains("1.0.114", info.Message, StringComparison.Ordinal);
        var rejected = Assert.Single(info.Rejected);
        Assert.Equal(DevMachine.ShimCli, rejected.Path);
        Assert.Equal(CopilotCliRejection.Shim, rejected.Reason);
        var run = Assert.Single(runner.Runs);
        Assert.Equal(DevMachine.RealCli, run.Path);
        Assert.Equal(["--version"], run.Arguments);
        Assert.Equal(TimeSpan.FromSeconds(10), run.Timeout);
    }

    /// <summary>
    ///     Test that a shim that is first on the PATH is rejected and the real CLI after it is used.
    /// </summary>
    [Fact]
    public async Task CopilotCliLocator_Locate_ShimFirstOnPath_StillRejectsTheShim()
    {
        // Arrange: a PATH that names the shim folder before the real CLI folder
        var runner = new FakeProcessRunner();
        runner.AnswerVersion(DevMachine.RealCli, "1.0.113");
        var environment = new FakeEnvironment(null, DevMachine.ShimFolder + ";" + DevMachine.PackageFolder);
        var locator = new CopilotCliLocator(runner, DevMachine.CreateFileSystem(), environment);

        // Act: search automatically
        var info = await locator.LocateAsync(cancellationToken: TestContext.Current.CancellationToken);

        // Assert: the real CLI at the minimum version is used and the shim was not started
        Assert.Equal(CopilotCliStatus.Found, info.Status);
        Assert.Equal(DevMachine.RealCli, info.Path);
        Assert.DoesNotContain(runner.Runs, run => run.Path == DevMachine.ShimCli);
    }

    /// <summary>
    ///     Test that a machine with only the VS Code shim on the PATH reports that the CLI was not found, names the
    ///     shim in the message, lists it with its reason, and starts no program.
    /// </summary>
    [Fact]
    public async Task CopilotCliLocator_Locate_OnlyShimOnPath_NotFoundWithShimHint()
    {
        // Arrange: the shim folder is the only PATH entry with a copilot.exe
        var runner = new FakeProcessRunner();
        runner.AnswerVersion(DevMachine.ShimCli, "1.0.114");
        var environment = new FakeEnvironment(null, DevMachine.NpmFolder + ";" + DevMachine.ShimFolder);
        var locator = new CopilotCliLocator(runner, DevMachine.CreateFileSystem(), environment);

        // Act: search automatically
        var info = await locator.LocateAsync(cancellationToken: TestContext.Current.CancellationToken);

        // Assert: not found, with the install advice, the shim path, and the diagnostic entry
        Assert.Equal(CopilotCliStatus.NotFound, info.Status);
        Assert.Null(info.Path);
        Assert.Null(info.Version);
        Assert.Contains("`winget install GitHub.Copilot`", info.Message, StringComparison.Ordinal);
        Assert.Contains("set its path in Settings", info.Message, StringComparison.Ordinal);
        Assert.Contains("Only the VS Code copilot shim was found at " + DevMachine.ShimCli, info.Message, StringComparison.Ordinal);
        var rejected = Assert.Single(info.Rejected);
        Assert.Equal(CopilotCliRejection.Shim, rejected.Reason);
        Assert.Empty(runner.Runs);
    }

    /// <summary>
    ///     Test that a machine with no CLI at all, and no PATH or application data folder, reports not found without the
    ///     shim sentence.
    /// </summary>
    [Fact]
    public async Task CopilotCliLocator_Locate_NothingInstalled_NotFoundWithoutShimHint()
    {
        // Arrange: no variables and an empty file system
        var runner = new FakeProcessRunner();
        var locator = new CopilotCliLocator(runner, new FakeCliFileSystem(), new FakeEnvironment(null, null));

        // Act: search automatically
        var info = await locator.LocateAsync(cancellationToken: TestContext.Current.CancellationToken);

        // Assert: not found, nothing rejected, no program run, no shim sentence
        Assert.Equal(CopilotCliStatus.NotFound, info.Status);
        Assert.Empty(info.Rejected);
        Assert.Empty(runner.Runs);
        Assert.DoesNotContain("shim", info.Message, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Test that scripts that are not programs are never candidates: the npm <c>copilot.cmd</c> and the file without an
    ///     extension next to the shim are neither run nor listed.
    /// </summary>
    [Fact]
    public async Task CopilotCliLocator_Locate_NpmCmdAndScriptWithoutExtension_AreIgnored()
    {
        // Arrange: only folders that hold copilot.cmd and a copilot file without an extension
        var fileSystem = new FakeCliFileSystem();
        fileSystem.AddFile(DevMachine.NpmFolder + @"\copilot.cmd", 512);
        fileSystem.AddFile(DevMachine.ShimScript, 2_048);
        var runner = new FakeProcessRunner();
        var environment = new FakeEnvironment(null, DevMachine.NpmFolder + ";" + DevMachine.ShimFolder);
        var locator = new CopilotCliLocator(runner, fileSystem, environment);

        // Act: search automatically
        var info = await locator.LocateAsync(cancellationToken: TestContext.Current.CancellationToken);

        // Assert: nothing was found, rejected, or run
        Assert.Equal(CopilotCliStatus.NotFound, info.Status);
        Assert.Empty(info.Rejected);
        Assert.Empty(runner.Runs);
    }

    /// <summary>
    ///     Test that an <c>.exe</c> that is smaller than 20 MiB is rejected as too small, and one of exactly that size is
    ///     used.
    /// </summary>
    [Fact]
    public async Task CopilotCliLocator_Locate_FileSize_RejectsBelowTheLimitAndAcceptsTheLimit()
    {
        // Arrange: two folders on the PATH, one with a file a byte too small and one with a file at the limit
        var fileSystem = new FakeCliFileSystem();
        fileSystem.AddFile(@"C:\small\copilot.exe", CopilotCliLocator.MinimumCliBytes - 1);
        fileSystem.AddFile(@"C:\limit\copilot.exe", CopilotCliLocator.MinimumCliBytes);
        var runner = new FakeProcessRunner();
        runner.AnswerVersion(@"C:\small\copilot.exe", "1.0.114");
        runner.AnswerVersion(@"C:\limit\copilot.exe", "1.0.114");
        var locator = new CopilotCliLocator(runner, fileSystem, new FakeEnvironment(null, @"C:\small;C:\limit"));

        // Act: search automatically
        var info = await locator.LocateAsync(cancellationToken: TestContext.Current.CancellationToken);

        // Assert: the file at the limit is used and the small file is listed with its size
        Assert.Equal(@"C:\limit\copilot.exe", info.Path);
        var rejected = Assert.Single(info.Rejected);
        Assert.Equal(@"C:\small\copilot.exe", rejected.Path);
        Assert.Equal(CopilotCliRejection.TooSmall, rejected.Reason);
        Assert.Contains("20,971,519", rejected.Detail, StringComparison.Ordinal);
        Assert.Equal([@"C:\limit\copilot.exe"], runner.Runs.Select(run => run.Path));
    }

    /// <summary>
    ///     Test that the winget link is followed to its target and counted once with the package file, so that the check
    ///     runs once, and that a link whose target is not an <c>.exe</c> or does not exist is rejected for that.
    /// </summary>
    /// <param name="target">The target of the link.</param>
    /// <param name="registerTarget">Whether the target file exists.</param>
    /// <param name="expected">The reason for rejection that is expected.</param>
    [Theory]
    [InlineData(@"C:\other\copilot.cmd", true, CopilotCliRejection.NotExe)]
    [InlineData(@"C:\gone\copilot.exe", false, CopilotCliRejection.Missing)]
    public async Task CopilotCliLocator_Locate_LinkWithUnusableTarget_IsRejectedForTheTarget(
        string target,
        bool registerTarget,
        CopilotCliRejection expected)
    {
        // Arrange: only the winget link, which leads to a script or to nothing
        var fileSystem = new FakeCliFileSystem();
        fileSystem.AddLink(DevMachine.LinkedCli, target);
        if (registerTarget)
        {
            fileSystem.AddFile(target, DevMachine.RealCliBytes);
        }

        var runner = new FakeProcessRunner();
        var environment = new FakeEnvironment(DevMachine.LocalAppData, string.Empty);
        var locator = new CopilotCliLocator(runner, fileSystem, environment);

        // Act: search automatically
        var info = await locator.LocateAsync(cancellationToken: TestContext.Current.CancellationToken);

        // Assert: nothing was found and the target, not the link, is listed with the reason
        Assert.Equal(CopilotCliStatus.NotFound, info.Status);
        var rejected = Assert.Single(info.Rejected);
        Assert.Equal(target, rejected.Path);
        Assert.Equal(expected, rejected.Reason);
        Assert.Empty(runner.Runs);
    }

    /// <summary>
    ///     Test that the link of winget alone is resolved to the file of the package and used, and that the same file
    ///     named on the PATH again is not run a second time.
    /// </summary>
    [Fact]
    public async Task CopilotCliLocator_Locate_LinkAndPathNameTheSameFile_CheckedOnce()
    {
        // Arrange: the link folder and the package folder both on the PATH, and the link folder first
        var runner = new FakeProcessRunner();
        runner.AnswerVersion(DevMachine.RealCli, "1.0.114");
        var environment = new FakeEnvironment(DevMachine.LocalAppData, DevMachine.LinksFolder.ToUpperInvariant() + ";" + DevMachine.PackageFolder);
        var fileSystem = DevMachine.CreateFileSystem();
        var locator = new CopilotCliLocator(runner, fileSystem, environment);

        // Act: search automatically
        var info = await locator.LocateAsync(cancellationToken: TestContext.Current.CancellationToken);

        // Assert: found at the resolved path, and the check ran once
        Assert.Equal(DevMachine.RealCli, info.Path);
        Assert.Single(runner.Runs);
    }

    /// <summary>
    ///     Test that with two winget package folders the one whose name sorts last is tried first.
    /// </summary>
    [Fact]
    public async Task CopilotCliLocator_Locate_TwoPackageFolders_TriesTheLastNameFirst()
    {
        // Arrange: two package folders that both hold a large CLI, and both answer
        var older = DevMachine.LocalAppData + @"\Microsoft\WinGet\Packages\GitHub.Copilot_A\copilot.exe";
        var newer = DevMachine.LocalAppData + @"\Microsoft\WinGet\Packages\GitHub.Copilot_Z\copilot.exe";
        var fileSystem = new FakeCliFileSystem();
        fileSystem.AddFile(older, DevMachine.RealCliBytes);
        fileSystem.AddFile(newer, DevMachine.RealCliBytes);
        var runner = new FakeProcessRunner();
        runner.AnswerVersion(older, "1.0.113");
        runner.AnswerVersion(newer, "1.0.114");
        var locator = new CopilotCliLocator(runner, fileSystem, new FakeEnvironment(DevMachine.LocalAppData, null));

        // Act: search automatically
        var info = await locator.LocateAsync(cancellationToken: TestContext.Current.CancellationToken);

        // Assert: the newer folder was used and the older was never run
        Assert.Equal(newer, info.Path);
        Assert.Equal([newer], runner.Runs.Select(run => run.Path));
    }

    /// <summary>
    ///     Test that the path of the settings is the only candidate even when it lies in a shim folder and is small, and
    ///     that automatic detection is not used even though a real CLI exists.
    /// </summary>
    [Fact]
    public async Task CopilotCliLocator_Locate_OverrideInShimFolder_WinsWithoutFiltersOrFallback()
    {
        // Arrange: the real CLI is installed, but the settings name the shim, and the shim answers
        var runner = new FakeProcessRunner();
        runner.AnswerVersion(DevMachine.RealCli, "1.0.114");
        runner.AnswerVersion(DevMachine.ShimCli, "1.0.120");
        var locator = new CopilotCliLocator(runner, DevMachine.CreateFileSystem(), DevMachine.CreateEnvironment());

        // Act: search with the override
        var info = await locator.LocateAsync(DevMachine.ShimCli, cancellationToken: TestContext.Current.CancellationToken);

        // Assert: the shim is accepted as an explicit choice after its version was validated, and nothing else was run
        Assert.Equal(CopilotCliStatus.Found, info.Status);
        Assert.Equal(DevMachine.ShimCli, info.Path);
        Assert.Equal(new Version(1, 0, 120), info.Version);
        Assert.Equal([DevMachine.ShimCli], runner.Runs.Select(run => run.Path));
    }

    /// <summary>
    ///     Test that an override in quotes with white space around it is read as the path inside the quotes.
    /// </summary>
    [Fact]
    public async Task CopilotCliLocator_Locate_OverrideInQuotes_IsTrimmed()
    {
        // Arrange: a path as Windows copies it, in quotes, with a trailing space
        var runner = new FakeProcessRunner();
        runner.AnswerVersion(DevMachine.RealCli, "1.0.114");
        var locator = new CopilotCliLocator(runner, DevMachine.CreateFileSystem(), DevMachine.CreateEnvironment());

        // Act: search with the override
        var info = await locator.LocateAsync($" \"{DevMachine.RealCli}\" ", cancellationToken: TestContext.Current.CancellationToken);

        // Assert: the path inside the quotes was used
        Assert.Equal(CopilotCliStatus.Found, info.Status);
        Assert.Equal(DevMachine.RealCli, info.Path);
    }

    /// <summary>
    ///     Test that an override that does not exist fails visibly, names the path, and does not fall back to a CLI that is
    ///     installed.
    /// </summary>
    [Fact]
    public async Task CopilotCliLocator_Locate_OverrideMissing_NotFoundWithoutFallback()
    {
        // Arrange: the real CLI is installed, and the settings name a file that is not
        var runner = new FakeProcessRunner();
        runner.AnswerVersion(DevMachine.RealCli, "1.0.114");
        var locator = new CopilotCliLocator(runner, DevMachine.CreateFileSystem(), DevMachine.CreateEnvironment());

        // Act: search with the override
        var info = await locator.LocateAsync(@"C:\nowhere\copilot.exe", cancellationToken: TestContext.Current.CancellationToken);

        // Assert: not found, with the path in the message, and no program was run
        Assert.Equal(CopilotCliStatus.NotFound, info.Status);
        Assert.Equal(@"C:\nowhere\copilot.exe", info.Path);
        Assert.Contains(@"C:\nowhere\copilot.exe", info.Message, StringComparison.Ordinal);
        Assert.Empty(runner.Runs);
    }

    /// <summary>
    ///     Test that an override that is not an <c>.exe</c> is refused, even when the file exists, and does not fall back.
    /// </summary>
    [Fact]
    public async Task CopilotCliLocator_Locate_OverrideNotExe_NotAnExecutableWithoutFallback()
    {
        // Arrange: the settings name the npm script, which exists
        var runner = new FakeProcessRunner();
        runner.AnswerVersion(DevMachine.RealCli, "1.0.114");
        var locator = new CopilotCliLocator(runner, DevMachine.CreateFileSystem(), DevMachine.CreateEnvironment());

        // Act: search with the override
        var info = await locator.LocateAsync(DevMachine.NpmFolder + @"\copilot.cmd", cancellationToken: TestContext.Current.CancellationToken);

        // Assert: the status says so, the message says what to choose, and no program was run
        Assert.Equal(CopilotCliStatus.NotAnExecutable, info.Status);
        Assert.Contains("not an .exe file", info.Message, StringComparison.Ordinal);
        Assert.Empty(runner.Runs);
    }

    /// <summary>
    ///     Test that an override that cannot report a version, or reports an old one, is reported as such and does not fall
    ///     back to another CLI.
    /// </summary>
    /// <param name="version">The version that the program reports; <see langword="null" /> for garbage.</param>
    /// <param name="expected">The status that is expected.</param>
    [Theory]
    [InlineData(null, CopilotCliStatus.Unreadable)]
    [InlineData("1.0.112", CopilotCliStatus.TooOld)]
    public async Task CopilotCliLocator_Locate_OverrideFailsValidation_ReportsItWithoutFallback(string? version, CopilotCliStatus expected)
    {
        // Arrange: the settings name an existing program that is bad, while the real CLI is fine
        var runner = new FakeProcessRunner();
        runner.AnswerVersion(DevMachine.RealCli, "1.0.114");
        var fileSystem = DevMachine.CreateFileSystem();
        fileSystem.AddFile(@"C:\mine\copilot.exe", 1_000);
        if (version is null)
        {
            runner.Answer(@"C:\mine\copilot.exe", new ProcessResult(true, 0, "usage: mine", false));
        }
        else
        {
            runner.AnswerVersion(@"C:\mine\copilot.exe", version);
        }

        var locator = new CopilotCliLocator(runner, fileSystem, DevMachine.CreateEnvironment());

        // Act: search with the override
        var info = await locator.LocateAsync(@"C:\mine\copilot.exe", cancellationToken: TestContext.Current.CancellationToken);

        // Assert: the failure is reported for the override and only the override was run
        Assert.Equal(expected, info.Status);
        Assert.Equal(@"C:\mine\copilot.exe", info.Path);
        Assert.Equal([@"C:\mine\copilot.exe"], runner.Runs.Select(run => run.Path));
    }

    /// <summary>
    ///     Test that a candidate that cannot be started, that does not answer in time, or whose answer has no version
    ///     falls through to the next candidate, which is used, and that the failures are listed with their reasons.
    /// </summary>
    /// <param name="failure">What the first candidate does: <c>start</c>, <c>timeout</c>, or <c>garbage</c>.</param>
    /// <param name="detail">A part of the reason that is expected.</param>
    [Theory]
    [InlineData("start", "could not be started")]
    [InlineData("timeout", "within 10 seconds")]
    [InlineData("garbage", "has no version")]
    public async Task CopilotCliLocator_Locate_FirstCandidateUnreadable_FallsThroughToTheNext(string failure, string detail)
    {
        // Arrange: two large CLIs on the PATH, the first of which fails the check
        var fileSystem = new FakeCliFileSystem();
        fileSystem.AddFile(@"C:\first\copilot.exe", DevMachine.RealCliBytes);
        fileSystem.AddFile(@"C:\second\copilot.exe", DevMachine.RealCliBytes);
        var runner = new FakeProcessRunner();
        runner.Answer(
            @"C:\first\copilot.exe",
            failure switch
            {
                "start" => ProcessResult.NotStarted("denied"),
                "timeout" => new ProcessResult(true, -1, string.Empty, true),
                _ => new ProcessResult(true, 0, "not a copilot", false),
            });
        runner.AnswerVersion(@"C:\second\copilot.exe", "1.0.114");
        var locator = new CopilotCliLocator(runner, fileSystem, new FakeEnvironment(null, @"C:\first;C:\second"));

        // Act: search automatically
        var info = await locator.LocateAsync(cancellationToken: TestContext.Current.CancellationToken);

        // Assert: the second candidate wins, and the first is listed as unreadable with the reason
        Assert.Equal(CopilotCliStatus.Found, info.Status);
        Assert.Equal(@"C:\second\copilot.exe", info.Path);
        var rejected = Assert.Single(info.Rejected);
        Assert.Equal(@"C:\first\copilot.exe", rejected.Path);
        Assert.Equal(CopilotCliRejection.Unreadable, rejected.Reason);
        Assert.Contains(detail, rejected.Detail, StringComparison.Ordinal);
        Assert.Equal([@"C:\first\copilot.exe", @"C:\second\copilot.exe"], runner.Runs.Select(run => run.Path));
    }

    /// <summary>
    ///     Test that a CLI that is too old is passed over when a later candidate is new enough, and reported as too old,
    ///     with the upgrade advice and its version, only when no candidate passes, and that it is preferred to an
    ///     unreadable candidate as the explanation.
    /// </summary>
    [Fact]
    public async Task CopilotCliLocator_Locate_OldCli_IsReportedOnlyWhenNoLaterCandidatePasses()
    {
        // Arrange: three CLIs: an unreadable one, an old one, and a new one
        var fileSystem = new FakeCliFileSystem();
        foreach (var folder in new[] { "bad", "old", "new" })
        {
            fileSystem.AddFile($@"C:\{folder}\copilot.exe", DevMachine.RealCliBytes);
        }

        var runner = new FakeProcessRunner();
        runner.Answer(@"C:\bad\copilot.exe", new ProcessResult(true, 0, "?", false));
        runner.AnswerVersion(@"C:\old\copilot.exe", "1.0.112");
        runner.AnswerVersion(@"C:\new\copilot.exe", "1.0.114");

        // Act: search with the new one present, then without it
        var withNew = await new CopilotCliLocator(runner, fileSystem, new FakeEnvironment(null, @"C:\bad;C:\old;C:\new"))
            .LocateAsync(cancellationToken: TestContext.Current.CancellationToken);
        var withoutNew = await new CopilotCliLocator(runner, fileSystem, new FakeEnvironment(null, @"C:\bad;C:\old"))
            .LocateAsync(cancellationToken: TestContext.Current.CancellationToken);

        // Assert: the new one wins when present; otherwise the old one is the explanation, with the advice and version
        Assert.Equal(CopilotCliStatus.Found, withNew.Status);
        Assert.Equal(@"C:\new\copilot.exe", withNew.Path);
        Assert.Equal([CopilotCliRejection.Unreadable, CopilotCliRejection.TooOld], withNew.Rejected.Select(r => r.Reason));
        Assert.Equal(CopilotCliStatus.TooOld, withoutNew.Status);
        Assert.Equal(@"C:\old\copilot.exe", withoutNew.Path);
        Assert.Equal(new Version(1, 0, 112), withoutNew.Version);
        Assert.Equal(
            "Copilot CLI 1.0.112 is older than 1.0.113. Run `winget upgrade GitHub.Copilot`.",
            withoutNew.Message);
    }

    /// <summary>
    ///     Test that when every candidate is unreadable the result is unreadable, names the first one, and tells what to do.
    /// </summary>
    [Fact]
    public async Task CopilotCliLocator_Locate_AllCandidatesUnreadable_ReportsTheFirst()
    {
        // Arrange: two large CLIs that cannot be started
        var fileSystem = new FakeCliFileSystem();
        fileSystem.AddFile(@"C:\first\copilot.exe", DevMachine.RealCliBytes);
        fileSystem.AddFile(@"C:\second\copilot.exe", DevMachine.RealCliBytes);
        var locator = new CopilotCliLocator(new FakeProcessRunner(), fileSystem, new FakeEnvironment(null, @"C:\first;C:\second"));

        // Act: search automatically
        var info = await locator.LocateAsync(cancellationToken: TestContext.Current.CancellationToken);

        // Assert: unreadable, at the first path, with both listed and a remedy
        Assert.Equal(CopilotCliStatus.Unreadable, info.Status);
        Assert.Equal(@"C:\first\copilot.exe", info.Path);
        Assert.Equal(2, info.Rejected.Count);
        Assert.Contains("did not report its version", info.Message, StringComparison.Ordinal);
        Assert.Contains("winget install GitHub.Copilot", info.Message, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Test that what a program prints is never copied into the message or the diagnostics, so that a program that
    ///     prints a secret cannot leak it through the locator.
    /// </summary>
    [Fact]
    public async Task CopilotCliLocator_Locate_ProgramPrintsSecret_IsNeverKept()
    {
        // Arrange: a CLI whose answer carries a token and no version
        var fileSystem = new FakeCliFileSystem();
        fileSystem.AddFile(@"C:\first\copilot.exe", DevMachine.RealCliBytes);
        var runner = new FakeProcessRunner();
        runner.Answer(@"C:\first\copilot.exe", new ProcessResult(true, 1, "token ghp_SECRET0123456789", false));
        var locator = new CopilotCliLocator(runner, fileSystem, new FakeEnvironment(null, @"C:\first"));

        // Act: search automatically
        var info = await locator.LocateAsync(cancellationToken: TestContext.Current.CancellationToken);

        // Assert: neither the message nor any diagnostic holds the printed text
        Assert.DoesNotContain("SECRET", info.Message, StringComparison.Ordinal);
        Assert.All(info.Rejected, r => Assert.DoesNotContain("SECRET", r.Detail, StringComparison.Ordinal));
    }

    /// <summary>
    ///     Test that the answer is cached for the same setting, searched again for a refresh or another setting, and not
    ///     cached when the search is cancelled.
    /// </summary>
    [Fact]
    public async Task CopilotCliLocator_Locate_Caching_ReusesUntilRefreshOrSettingChange()
    {
        // Arrange: the development machine
        var token = TestContext.Current.CancellationToken;
        var runner = new FakeProcessRunner();
        runner.AnswerVersion(DevMachine.RealCli, "1.0.114");
        var locator = new CopilotCliLocator(runner, DevMachine.CreateFileSystem(), DevMachine.CreateEnvironment());

        // Act: search twice, then refresh, then change the setting
        var first = await locator.LocateAsync(cancellationToken: token);
        var second = await locator.LocateAsync("  ", cancellationToken: token);
        var runsAfterCache = runner.Runs.Count;
        var refreshed = await locator.LocateAsync(refresh: true, cancellationToken: token);
        var runsAfterRefresh = runner.Runs.Count;
        var overridden = await locator.LocateAsync(DevMachine.RealCli, cancellationToken: token);

        // Assert: the second call reused the answer, the refresh and the changed setting searched again
        Assert.Same(first, second);
        Assert.Equal(1, runsAfterCache);
        Assert.NotSame(first, refreshed);
        Assert.Equal(2, runsAfterRefresh);
        Assert.Equal(3, runner.Runs.Count);
        Assert.Equal(DevMachine.RealCli, overridden.Path);
    }

    /// <summary>
    ///     Test that a cancelled search throws and leaves nothing in the cache, so that the next search runs.
    /// </summary>
    [Fact]
    public async Task CopilotCliLocator_Locate_Cancelled_ThrowsAndIsNotCached()
    {
        // Arrange: the development machine and a token that is already cancelled
        var runner = new FakeProcessRunner();
        runner.AnswerVersion(DevMachine.RealCli, "1.0.114");
        var locator = new CopilotCliLocator(runner, DevMachine.CreateFileSystem(), DevMachine.CreateEnvironment());
        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync();

        // Act / Assert: the cancelled search throws
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => locator.LocateAsync(cancellationToken: cancelled.Token));

        // Act: search again with a live token
        var info = await locator.LocateAsync(cancellationToken: TestContext.Current.CancellationToken);

        // Assert: the search ran and found the CLI
        Assert.Equal(CopilotCliStatus.Found, info.Status);
    }

    /// <summary>
    ///     Test that a PATH with quotes, empty entries, and white space is read entry by entry.
    /// </summary>
    [Fact]
    public async Task CopilotCliLocator_Locate_PathWithQuotesAndEmptyEntries_IsReadEntryByEntry()
    {
        // Arrange: a PATH whose second entry is in quotes with spaces around, between empty entries
        var fileSystem = new FakeCliFileSystem();
        fileSystem.AddFile(@"C:\Program Files\Copilot\copilot.exe", DevMachine.RealCliBytes);
        var runner = new FakeProcessRunner();
        runner.AnswerVersion(@"C:\Program Files\Copilot\copilot.exe", "1.0.114");
        var locator = new CopilotCliLocator(runner, fileSystem, new FakeEnvironment(null, @";; C:\none ; ""C:\Program Files\Copilot"" ;"));

        // Act: search automatically
        var info = await locator.LocateAsync(cancellationToken: TestContext.Current.CancellationToken);

        // Assert: the file in the quoted folder was found
        Assert.Equal(@"C:\Program Files\Copilot\copilot.exe", info.Path);
    }

    /// <summary>
    ///     Test that the result record refuses a missing list of rejected candidates and an empty message, and keeps what it
    ///     is given.
    /// </summary>
    [Fact]
    public void CopilotCliInfo_Constructor_InvalidArguments_AreRefused()
    {
        // Arrange / Act / Assert: a missing list, a missing message, and an empty message are refused
        Assert.Throws<ArgumentNullException>(() => new CopilotCliInfo(CopilotCliStatus.Found, null, null, null!, "x"));
        Assert.Throws<ArgumentNullException>(() => new CopilotCliInfo(CopilotCliStatus.Found, null, null, [], null!));
        Assert.Throws<ArgumentException>(() => new CopilotCliInfo(CopilotCliStatus.Found, null, null, [], " "));

        // Act: build a valid result
        var info = new CopilotCliInfo(CopilotCliStatus.TooOld, "p", new Version(1, 0, 1), [], "old");

        // Assert: it keeps its values and is not found
        Assert.Equal(CopilotCliStatus.TooOld, info.Status);
        Assert.Equal("p", info.Path);
        Assert.Equal(new Version(1, 0, 1), info.Version);
        Assert.False(info.IsFound);
    }
}
