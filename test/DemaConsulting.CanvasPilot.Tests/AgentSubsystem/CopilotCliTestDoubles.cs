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
///     The paths and sizes of a real Windows machine that has the winget Copilot CLI and VS Code, as measured on the
///     development machine, for the tests of the Copilot CLI locator. The user name is replaced by <c>dev</c>.
/// </summary>
internal static class DevMachine
{
    /// <summary>The folder of the local application data of the user.</summary>
    internal const string LocalAppData = @"C:\Users\dev\AppData\Local";

    /// <summary>The size of the real Copilot CLI that winget installed, in bytes.</summary>
    internal const long RealCliBytes = 196_113_184;

    /// <summary>The size of the <c>copilot.exe</c> of the VS Code shim, in bytes.</summary>
    internal const long ShimBytes = 791_904;

    /// <summary>The winget package folder of the Copilot CLI.</summary>
    internal const string PackageFolder = LocalAppData + @"\Microsoft\WinGet\Packages\GitHub.Copilot_Microsoft.Winget.Source_publisher";

    /// <summary>The real Copilot CLI.</summary>
    internal const string RealCli = PackageFolder + @"\copilot.exe";

    /// <summary>The folder of the winget links.</summary>
    internal const string LinksFolder = LocalAppData + @"\Microsoft\WinGet\Links";

    /// <summary>The link that winget makes to the real Copilot CLI.</summary>
    internal const string LinkedCli = LinksFolder + @"\copilot.exe";

    /// <summary>The folder of the shim of VS Code.</summary>
    internal const string ShimFolder = LocalAppData + @"\Programs\Microsoft VS Code\bin\copilot-shim";

    /// <summary>The <c>copilot.exe</c> of the shim of VS Code.</summary>
    internal const string ShimCli = ShimFolder + @"\copilot.exe";

    /// <summary>The file without an extension that sits next to the shim.</summary>
    internal const string ShimScript = ShimFolder + @"\copilot";

    /// <summary>The folder of the npm programs of the user.</summary>
    internal const string NpmFolder = @"C:\Users\dev\AppData\Roaming\npm";

    /// <summary>The text that <c>copilot --version</c> prints, with the line that suggests an update.</summary>
    /// <param name="version">The version.</param>
    /// <returns>The text.</returns>
    internal static string VersionText(string version)
    {
        return $"GitHub Copilot CLI {version}.\nRun 'copilot update' to check for updates.";
    }

    /// <summary>
    ///     Makes the file system of the development machine: the real CLI, the link to it, the shim with its script, and
    ///     the npm script.
    /// </summary>
    /// <returns>The file system, to which a test may add more.</returns>
    internal static FakeCliFileSystem CreateFileSystem()
    {
        var fileSystem = new FakeCliFileSystem();
        fileSystem.AddFile(RealCli, RealCliBytes);
        fileSystem.AddLink(LinkedCli, RealCli);
        fileSystem.AddFile(ShimCli, ShimBytes);
        fileSystem.AddFile(ShimScript, 2_048);
        fileSystem.AddFile(NpmFolder + @"\copilot.cmd", 512);
        return fileSystem;
    }

    /// <summary>
    ///     Makes the environment of the development machine, with the PATH of its user: npm, the winget links, the winget
    ///     package, VS Code, and the shim folder last.
    /// </summary>
    /// <returns>The environment.</returns>
    internal static FakeEnvironment CreateEnvironment()
    {
        return new FakeEnvironment(
            LocalAppData,
            string.Join(';', NpmFolder, LinksFolder, PackageFolder, LocalAppData + @"\Programs\Microsoft VS Code\bin", ShimFolder));
    }
}

/// <summary>
///     A file system of files, links, and folders that exist only in the test, with the sizes that the test gives.
/// </summary>
internal sealed class FakeCliFileSystem : ICliFileSystem
{
    /// <summary>The sizes of the files by full path.</summary>
    private readonly Dictionary<string, long> _files = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>The targets of the links by full path.</summary>
    private readonly Dictionary<string, string> _links = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>The folders by full path.</summary>
    private readonly HashSet<string> _folders = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    ///     Adds a file of a size, and the folder that holds it.
    /// </summary>
    /// <param name="path">The full path of the file.</param>
    /// <param name="length">The size in bytes.</param>
    internal void AddFile(string path, long length)
    {
        _files[path] = length;
        AddFolder(path[..path.LastIndexOf('\\')]);
    }

    /// <summary>
    ///     Adds a link to a target, and the folder that holds it. The target need not exist.
    /// </summary>
    /// <param name="path">The full path of the link.</param>
    /// <param name="target">The full path of what it names.</param>
    internal void AddLink(string path, string target)
    {
        _links[path] = target;
        AddFolder(path[..path.LastIndexOf('\\')]);
    }

    /// <summary>
    ///     Adds a folder.
    /// </summary>
    /// <param name="path">The full path of the folder.</param>
    internal void AddFolder(string path)
    {
        _folders.Add(path);
    }

    /// <inheritdoc />
    public bool FileExists(string path)
    {
        return _files.ContainsKey(path) || _links.ContainsKey(path);
    }

    /// <inheritdoc />
    public long GetLength(string path)
    {
        return _files.TryGetValue(path, out var length) ? length : 0;
    }

    /// <inheritdoc />
    public string ResolveLinkTarget(string path)
    {
        return _links.TryGetValue(path, out var target) ? target : path;
    }

    /// <inheritdoc />
    public IReadOnlyList<string> EnumerateDirectories(string parent, string pattern)
    {
        var prefix = pattern.TrimEnd('*');
        return
        [
            .. _folders.Where(folder =>
                string.Equals(folder[..Math.Max(0, folder.LastIndexOf('\\'))], parent, StringComparison.OrdinalIgnoreCase)
                && folder[(folder.LastIndexOf('\\') + 1)..].StartsWith(prefix, StringComparison.OrdinalIgnoreCase)),
        ];
    }
}

/// <summary>
///     An environment with the variables that the locator and the readiness checks read.
/// </summary>
internal sealed class FakeEnvironment : IEnvironmentReader
{
    /// <summary>The variables by name.</summary>
    private readonly Dictionary<string, string?> _variables = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    ///     Initializes a new instance of the <see cref="FakeEnvironment" /> class.
    /// </summary>
    /// <param name="localAppData">The value of <c>LOCALAPPDATA</c>; <see langword="null" /> for not set.</param>
    /// <param name="path">The value of <c>PATH</c>; <see langword="null" /> for not set.</param>
    internal FakeEnvironment(string? localAppData, string? path)
    {
        _variables["LOCALAPPDATA"] = localAppData;
        _variables["PATH"] = path;
    }

    /// <summary>
    ///     Sets a variable, such as a token variable or <c>ProgramFiles</c>.
    /// </summary>
    /// <param name="name">The name of the variable.</param>
    /// <param name="value">The value; <see langword="null" /> for not set.</param>
    internal void Set(string name, string? value)
    {
        _variables[name] = value;
    }

    /// <inheritdoc />
    public string? GetVariable(string name)
    {
        return _variables.GetValueOrDefault(name);
    }
}

/// <summary>
///     A process runner that answers by the path of the program, and records every run, so that a test can tell which
///     programs were started with which arguments and time limit.
/// </summary>
internal sealed class FakeProcessRunner : IProcessRunner
{
    /// <summary>The answers by full path.</summary>
    private readonly Dictionary<string, ProcessResult> _results = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    ///     Gets the programs that were run, in order.
    /// </summary>
    internal List<(string Path, IReadOnlyList<string> Arguments, TimeSpan Timeout)> Runs { get; } = [];

    /// <summary>
    ///     Makes a program answer with the text of <c>--version</c> of a version.
    /// </summary>
    /// <param name="path">The full path of the program.</param>
    /// <param name="version">The version, such as <c>1.0.114</c>.</param>
    internal void AnswerVersion(string path, string version)
    {
        _results[path] = new ProcessResult(true, 0, DevMachine.VersionText(version), false);
    }

    /// <summary>
    ///     Makes a program answer with the given result.
    /// </summary>
    /// <param name="path">The full path of the program.</param>
    /// <param name="result">The result.</param>
    internal void Answer(string path, ProcessResult result)
    {
        _results[path] = result;
    }

    /// <inheritdoc />
    public Task<ProcessResult> RunAsync(
        string executablePath,
        IReadOnlyList<string> arguments,
        TimeSpan timeout,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Runs.Add((executablePath, arguments, timeout));
        return Task.FromResult(_results.GetValueOrDefault(executablePath) ?? ProcessResult.NotStarted("no such program"));
    }
}
