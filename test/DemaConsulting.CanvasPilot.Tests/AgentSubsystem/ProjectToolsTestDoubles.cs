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

using System.Diagnostics;
using System.Text.Json;
using DemaConsulting.AgentKit.Core;
using DemaConsulting.CanvasPilot.AgentSubsystem;
using DemaConsulting.CanvasPilot.PreviewSubsystem;
using Microsoft.Extensions.AI;

namespace DemaConsulting.CanvasPilot.Tests;

/// <summary>
///     Makes the kinds of file link that the project tools must refuse, in a temporary folder: a folder junction, a
///     symbolic link to a folder or a file, and a hard link. Every link that was made is removed as a link by
///     <see cref="Dispose" />, so deleting the folder never reaches what a link leads to.
/// </summary>
public sealed class TestLinks : IDisposable
{
    /// <summary>
    ///     The links that were made, in order, as paths of a file or folder.
    /// </summary>
    private readonly List<(string Path, bool IsFolder)> _links = [];

    /// <summary>
    ///     Makes a folder junction, which is a folder link that needs no privilege.
    /// </summary>
    /// <param name="link">The path of the link to make.</param>
    /// <param name="target">The folder that the link leads to.</param>
    internal void CreateJunction(string link, string target)
    {
        Run("mklink", "/J", link, target);
        _links.Add((link, true));
    }

    /// <summary>
    ///     Makes a hard link, which is a second name of a file on the same volume and needs no privilege.
    /// </summary>
    /// <param name="link">The path of the new name.</param>
    /// <param name="target">The file that the new name is another name of.</param>
    internal void CreateHardLink(string link, string target)
    {
        Run("mklink", "/H", link, target);
        _links.Add((link, false));
    }

    /// <summary>
    ///     Makes a symbolic link to a folder, or skips the test when the platform or the user may not make one.
    /// </summary>
    /// <param name="link">The path of the link to make.</param>
    /// <param name="target">The folder that the link leads to.</param>
    internal void CreateFolderSymlinkOrSkip(string link, string target)
    {
        try
        {
            Directory.CreateSymbolicLink(link, target);
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or IOException or PlatformNotSupportedException)
        {
            Assert.Skip("Symbolic links cannot be made here: " + ex.GetType().Name);
        }

        _links.Add((link, true));
    }

    /// <summary>
    ///     Makes a symbolic link to a file, or skips the test when the platform or the user may not make one.
    /// </summary>
    /// <param name="link">The path of the link to make.</param>
    /// <param name="target">The file that the link leads to.</param>
    internal void CreateFileSymlinkOrSkip(string link, string target)
    {
        try
        {
            File.CreateSymbolicLink(link, target);
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or IOException or PlatformNotSupportedException)
        {
            Assert.Skip("Symbolic links cannot be made here: " + ex.GetType().Name);
        }

        _links.Add((link, false));
    }

    /// <inheritdoc />
    public void Dispose()
    {
        // A link goes first and as a link, so that deleting the folder around it never reaches the target
        foreach (var (path, isFolder) in _links.AsEnumerable().Reverse())
        {
            if (isFolder && Directory.Exists(path))
            {
                Directory.Delete(path);
            }
            else if (File.Exists(path))
            {
                File.Delete(path);
            }
        }

        _links.Clear();
    }

    /// <summary>
    ///     Runs a command of the Windows command line and expects it to succeed.
    /// </summary>
    /// <param name="arguments">The command and its arguments.</param>
    private static void Run(params string[] arguments)
    {
        Assert.SkipUnless(OperatingSystem.IsWindows(), "The test makes a link with the command line of Windows.");
        var start = new ProcessStartInfo("cmd.exe")
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        start.ArgumentList.Add("/c");
        foreach (var argument in arguments)
        {
            start.ArgumentList.Add(argument);
        }

        using var process = Process.Start(start) ?? throw new InvalidOperationException("cmd.exe did not start.");
        process.StandardOutput.ReadToEnd();
        process.StandardError.ReadToEnd();
        process.WaitForExit();
        Assert.Equal(0, process.ExitCode);
    }
}

/// <summary>
///     Reads what a tool of the AI library answered, whatever shape the library gave it: text, a JSON value, or a list of
///     parts with a picture.
/// </summary>
internal static class ToolAnswers
{
    /// <summary>
    ///     Reads the text of an answer; the text parts of a list are joined with a line break.
    /// </summary>
    /// <param name="answer">The answer of a tool.</param>
    /// <returns>The text.</returns>
    internal static string Text(object? answer)
    {
        return answer switch
        {
            null => string.Empty,
            string text => text,
            JsonElement { ValueKind: JsonValueKind.String } element => element.GetString() ?? string.Empty,
            JsonElement element => element.GetRawText(),
            TextContent content => content.Text,
            IEnumerable<AIContent> parts => string.Join("\n", parts.OfType<TextContent>().Select(part => part.Text)),
            _ => answer.ToString() ?? string.Empty,
        };
    }

    /// <summary>
    ///     Reads the picture of an answer.
    /// </summary>
    /// <param name="answer">The answer of a tool.</param>
    /// <returns>The picture part, or <see langword="null" /> when the answer has none.</returns>
    internal static DataContent? Picture(object? answer)
    {
        return answer is IEnumerable<AIContent> parts ? parts.OfType<DataContent>().FirstOrDefault() : null;
    }
}

/// <summary>
///     The common ground of the tests of the project tools: a temporary folder with a project in it, a secret beside the
///     project, the tools built the way the application builds them (the path policy of the tool registry and the packs of
///     the supplier), calls of the tools, and helpers that write, read, and compare files.
/// </summary>
public abstract class ProjectToolsTestBase : IDisposable
{
    /// <summary>
    ///     The text that stands for a secret that sits outside the project; no answer may contain it.
    /// </summary>
    protected const string Secret = "OUTSIDE-SECRET-4711";

    /// <summary>
    ///     The folder that holds the project and the places beside it that the tools must not reach.
    /// </summary>
    private readonly string _root = Directory.CreateTempSubdirectory("canvaspilot-project-tools-").FullName;

    /// <summary>
    ///     Initializes a new instance of the <see cref="ProjectToolsTestBase" /> class: a project folder with a project file
    ///     and its data folder, and a secret beside the project.
    /// </summary>
    protected ProjectToolsTestBase()
    {
        Directory.CreateDirectory(Project);
        Put(Path.Combine(Project, "Brochure.canvaspilot"), "PROJECT-FILE");
        Directory.CreateDirectory(Path.Combine(Project, ".canvaspilot"));
        Put(Path.Combine(Project, ".canvaspilot", "draft.txt"), "DATA-FOLDER-FILE");
        Put(Path.Combine(_root, "outside.txt"), Secret);
        Tools = new ProjectTools(Renderer, Style);
        Tools.FileWritten += (_, args) => Written.Add(args);
    }

    /// <summary>
    ///     Gets the folder that holds the project and the places beside it.
    /// </summary>
    protected string Root => _root;

    /// <summary>
    ///     Gets the folder of the project of the tests.
    /// </summary>
    protected string Project => Path.Combine(_root, "project");

    /// <summary>
    ///     Gets the object that makes and removes the links of a test.
    /// </summary>
    protected TestLinks Links { get; } = new();

    /// <summary>
    ///     Gets the render service of the tests.
    /// </summary>
    protected ScriptedRenderService Renderer { get; } = new();

    /// <summary>
    ///     Gets the style of the tests.
    /// </summary>
    protected FakeActiveStyle Style { get; } = new();

    /// <summary>
    ///     Gets the files that the tools reported as written.
    /// </summary>
    protected List<FileWrittenEventArgs> Written { get; } = [];

    /// <summary>
    ///     Gets the project tools of the tests.
    /// </summary>
    protected ProjectTools Tools { get; }

    /// <inheritdoc />
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    ///     Removes the links and the folder of the test.
    /// </summary>
    /// <param name="disposing"><see langword="true" /> when called from <see cref="Dispose()" />.</param>
    protected virtual void Dispose(bool disposing)
    {
        if (disposing)
        {
            // The links go first and as links, so that deleting the folder never reaches what they lead to
            Links.Dispose();
            Directory.Delete(_root, true);
        }
    }

    /// <summary>
    ///     Writes a text file as UTF-8 without a byte order mark.
    /// </summary>
    /// <param name="path">The path of the file.</param>
    /// <param name="text">The text.</param>
    protected static void Put(string path, string text)
    {
        File.WriteAllText(path, text);
    }

    /// <summary>
    ///     Writes a file of bytes.
    /// </summary>
    /// <param name="path">The path of the file.</param>
    /// <param name="bytes">The bytes.</param>
    protected static void PutBytes(string path, byte[] bytes)
    {
        File.WriteAllBytes(path, bytes);
    }

    /// <summary>
    ///     Reads a text file.
    /// </summary>
    /// <param name="path">The path of the file.</param>
    /// <returns>The text.</returns>
    protected static string Get(string path)
    {
        return File.ReadAllText(path);
    }

    /// <summary>
    ///     Reads a file as bytes.
    /// </summary>
    /// <param name="path">The path of the file.</param>
    /// <returns>The bytes.</returns>
    protected static byte[] GetBytes(string path)
    {
        return File.ReadAllBytes(path);
    }

    /// <summary>
    ///     Makes the function of a tool of the project tools, built the way the application builds it.
    /// </summary>
    /// <param name="name">The name of the tool.</param>
    /// <param name="tools">The project tools; the tools of the test by default.</param>
    /// <returns>The function.</returns>
    protected AIFunction Tool(string name, ProjectTools? tools = null)
    {
        var builder = new ToolPackBuilder(AgentToolRegistry.CreatePolicy(Project));
        foreach (var pack in (tools ?? Tools).CreatePacks(new AgentToolContext(Project)))
        {
            builder.Add(pack);
        }

        return builder.Build().Single(tool => tool.Name == name);
    }

    /// <summary>
    ///     Calls a tool of the tools of the test with arguments.
    /// </summary>
    /// <param name="name">The name of the tool.</param>
    /// <param name="arguments">The arguments by name.</param>
    /// <returns>The answer of the tool.</returns>
    protected Task<object?> Call(string name, params (string Name, object? Value)[] arguments)
    {
        return Call(Tools, name, arguments);
    }

    /// <summary>
    ///     Calls a tool of other project tools with arguments.
    /// </summary>
    /// <param name="tools">The project tools.</param>
    /// <param name="name">The name of the tool.</param>
    /// <param name="arguments">The arguments by name.</param>
    /// <returns>The answer of the tool.</returns>
    protected async Task<object?> Call(ProjectTools tools, string name, params (string Name, object? Value)[] arguments)
    {
        var values = new AIFunctionArguments();
        foreach (var (argument, value) in arguments)
        {
            values[argument] = value;
        }

        return await Tool(name, tools).InvokeAsync(values, TestContext.Current.CancellationToken);
    }

    /// <summary>
    ///     Lists every file and folder below a folder with a fingerprint of its content, without following links.
    /// </summary>
    /// <param name="folder">The folder.</param>
    /// <returns>The kind or the content hash of each entry by its path below the folder.</returns>
    protected static Dictionary<string, string> Snapshot(string folder)
    {
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var entry in Directory.EnumerateFileSystemEntries(folder))
        {
            AddToSnapshot(folder, entry, result);
        }

        return result;
    }

    /// <summary>
    ///     Adds an entry of a snapshot and, for a folder that is not a link, the entries below it.
    /// </summary>
    /// <param name="root">The folder of the snapshot.</param>
    /// <param name="entry">The file or folder.</param>
    /// <param name="result">Receives the entries.</param>
    private static void AddToSnapshot(string root, string entry, Dictionary<string, string> result)
    {
        var key = Path.GetRelativePath(root, entry);
        var attributes = File.GetAttributes(entry);
        if (attributes.HasFlag(FileAttributes.ReparsePoint))
        {
            result[key] = "link";
        }
        else if (attributes.HasFlag(FileAttributes.Directory))
        {
            result[key] = "folder";
            foreach (var child in Directory.EnumerateFileSystemEntries(entry))
            {
                AddToSnapshot(root, child, result);
            }
        }
        else
        {
            result[key] = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(entry)));
        }
    }
}

/// <summary>
///     A render service for the tests of the picture tool: it answers every render with the result that a test sets,
///     records what it was asked, and encodes a picture as a PNG signature followed by its size, so that a test can read
///     the size that the tool asked it to encode.
/// </summary>
public sealed class ScriptedRenderService : IImageRenderService
{
    /// <summary>
    ///     Gets the renders that were asked for: the file and the folder the viewer may read from.
    /// </summary>
    internal List<(string File, string Folder)> Requests { get; } = [];

    /// <summary>
    ///     Gets or sets what a render answers; a small picture by default.
    /// </summary>
    internal Func<string, RenderResult> Answer { get; set; } = file => RenderResult.Success(TestImages.Create(file, 8, 4));

    /// <summary>
    ///     Gets or sets the number of bytes that an encoded picture is padded to, or zero for the small default.
    /// </summary>
    internal Func<RenderedImage, int> PaddedLength { get; set; } = _ => 0;

    /// <summary>
    ///     Gets the sizes of the pictures that were encoded, in order.
    /// </summary>
    internal List<(int Width, int Height)> Encoded { get; } = [];

    /// <inheritdoc />
    public Task<RenderResult> RenderAsync(string filePath, string projectFolder, CancellationToken cancellationToken)
    {
        return RenderAsync(filePath, projectFolder, null, cancellationToken);
    }

    /// <inheritdoc />
    public Task<RenderResult> RenderAsync(string filePath, string projectFolder, RenderSettings? settings, CancellationToken cancellationToken)
    {
        Requests.Add((filePath, projectFolder));
        return Task.FromResult(Answer(filePath));
    }

    /// <inheritdoc />
    public byte[] EncodePng(RenderedImage image)
    {
        return EncodePng(image, null);
    }

    /// <inheritdoc />
    public byte[] EncodePng(RenderedImage image, double? dpi)
    {
        Encoded.Add((image.Width, image.Height));
        var length = Math.Max(PaddedLength(image), 16);
        var bytes = new byte[length];
        new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }.CopyTo(bytes, 0);
        return bytes;
    }
}
