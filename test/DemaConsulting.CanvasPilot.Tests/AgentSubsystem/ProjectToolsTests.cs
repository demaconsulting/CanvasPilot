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
using DemaConsulting.CanvasPilot.KnowledgeSubsystem;
using DemaConsulting.CanvasPilot.LoggingSubsystem;

namespace DemaConsulting.CanvasPilot.Tests;

/// <summary>
///     Unit tests for <see cref="ProjectTools" />, the file tools: <c>project_list</c>, <c>project_read</c>, and
///     <c>project_write</c> as the copilot meets them, built the way the application builds them (the path policy of the tool
///     registry and the packs of the supplier) over a real temporary project folder. The tests cover what the tools do, that
///     every class of path that could leave the project or reach a file of the application is refused (including links),
///     that a file is replaced directly with no backup anywhere, and that the log holds metadata only. The picture tool is
///     tested in <see cref="RenderImageToolTests" />.
/// </summary>
public sealed class ProjectToolsTests : ProjectToolsTestBase
{
    /// <summary>
    ///     Makes the paths that must be refused for reading and writing, each of a class of its own.
    /// </summary>
    /// <returns>The paths, as the model might send them.</returns>
    public static TheoryData<string> EscapingPaths()
    {
        var data = new TheoryData<string>
        {
            "../outside.txt",
            "sub/../../outside.txt",
            "..\\outside.txt",
            ".canvaspilot/draft.txt",
            "sub/.canvaspilot/draft.txt",
            ".CanvasPilot/draft.txt",
            "Brochure.canvaspilot",
            "sub/Brochure.canvaspilot",
            "other.canvaspilot",
            "CANVAS~1/draft.txt",
            "A~1.CAN",
            "notes.txt:hidden",
            "NUL",
            "con.txt",
            "",
            "   ",
        };
        if (OperatingSystem.IsWindows())
        {
            data.Add(TestPlatformPaths.NetworkFolder + "\\outside.txt");
            data.Add("C:outside.txt");
            data.Add("\\\\.\\C:\\Windows\\win.ini");
        }

        return data;
    }

    /// <summary>
    ///     Test that the constructor and the supplier refuse a missing renderer and a missing context.
    /// </summary>
    [Fact]
    public void ProjectTools_ArgumentsAreChecked_NullRendererAndContext_AreRefused()
    {
        // Act / Assert: the renderer is needed, and the supplier needs the project
        Assert.Throws<ArgumentNullException>(() => new ProjectTools(null!));
        Assert.Throws<ArgumentNullException>(() => Tools.CreatePacks(null!));
    }

    /// <summary>
    ///     Test that the tool registry offers the project tools after the drawing knowledge and the style tools when
    ///     <see cref="ProjectTools.CreatePacks" /> is its supplier of project packs, under the confining policy: two packs
    ///     with the prefixes <c>project</c> and <c>render</c>, and the four tools in order.
    /// </summary>
    [Fact]
    public void ProjectTools_CreatePacks_AsTheRegistrySupplier_OffersTheFourToolsAfterTheBuiltInTools()
    {
        // Arrange: a registry over the real drawing knowledge and a stand-in style tool, supplied by the project tools
        var registry = new AgentToolRegistry(
            KnowledgeTools.CreateAll(KnowledgeCatalog.Default),
            [ToolTestDoubles.Plain("style_get")],
            Tools.CreatePacks);

        // Act: build the tools of the project and the packs of the supplier
        var tools = registry.CreateTools(new AgentToolContext(Project), new HashSet<string>());
        var packs = Tools.CreatePacks(new AgentToolContext(Project)).ToList();

        // Assert: the order of the names, the two prefixes, and a description for the model on each
        Assert.Equal(
            ["canvasnet_help", "style_get", "project_list", "project_read", "project_write", "render_image"],
            tools.Select(tool => tool.Name).ToArray());
        Assert.Equal([ProjectTools.ProjectPrefix, ProjectTools.RenderPrefix], packs.Select(pack => pack.FamilyPrefix).ToArray());
        Assert.All(tools.Skip(2), tool => Assert.False(string.IsNullOrWhiteSpace(tool.Description)));
    }

    /// <summary>
    ///     Test that the listing shows the relative path, the size, and the image source flag of the files, in a fixed order
    ///     with the top of the project first, and hides the data folder, the project file, a folder that starts with a dot,
    ///     and hidden files.
    /// </summary>
    [Fact]
    public async Task ProjectTools_List_Project_ShowsRelativePathsSizesAndImageSourceFlagAndHidesApplicationFiles()
    {
        // Arrange: pictures, data, a nested file, and the places that must stay hidden
        Put(Path.Combine(Project, "cover.svg"), "<svg/>");
        Put(Path.Combine(Project, "notes.txt"), "hello");
        Directory.CreateDirectory(Path.Combine(Project, "data", "deep"));
        Put(Path.Combine(Project, "data", "sales.csv"), "a,b");
        Put(Path.Combine(Project, "data", "deep", "flow.mmd"), "graph TD");
        Directory.CreateDirectory(Path.Combine(Project, ".git"));
        Put(Path.Combine(Project, ".git", "config"), "git");
        var hidden = Path.Combine(Project, "desktop.ini");
        Put(hidden, "hidden");
        File.SetAttributes(hidden, FileAttributes.Hidden);

        // Act
        var answer = ToolAnswers.Text(await Call("project_list"));

        // Assert: relative paths with sizes and the flag, the top of the project first, and no application file
        Assert.Contains("- cover.svg (6 bytes, image source)", answer, StringComparison.Ordinal);
        Assert.Contains("- notes.txt (5 bytes)", answer, StringComparison.Ordinal);
        Assert.Contains("- data/sales.csv (3 bytes)", answer, StringComparison.Ordinal);
        Assert.Contains("- data/deep/flow.mmd (8 bytes, image source)", answer, StringComparison.Ordinal);
        Assert.True(answer.IndexOf("notes.txt", StringComparison.Ordinal) < answer.IndexOf("data/sales.csv", StringComparison.Ordinal));
        Assert.DoesNotContain(".canvaspilot", answer, StringComparison.Ordinal);
        Assert.DoesNotContain("Brochure", answer, StringComparison.Ordinal);
        Assert.DoesNotContain(".git", answer, StringComparison.Ordinal);
        Assert.DoesNotContain("desktop.ini", answer, StringComparison.Ordinal);
        Assert.DoesNotContain(Root, answer, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Test that a listing of a folder shows only that folder with paths relative to the project, and that a folder that
    ///     is not there is a refusal.
    /// </summary>
    [Fact]
    public async Task ProjectTools_List_Folder_ShowsOnlyThatFolderAndRefusesAMissingOne()
    {
        // Arrange
        Put(Path.Combine(Project, "cover.svg"), "<svg/>");
        Directory.CreateDirectory(Path.Combine(Project, "data"));
        Put(Path.Combine(Project, "data", "sales.csv"), "a,b");

        // Act
        var folder = ToolAnswers.Text(await Call("project_list", ("folder", "data")));
        var missing = ToolAnswers.Text(await Call("project_list", ("folder", "nothing")));
        var dot = ToolAnswers.Text(await Call("project_list", ("folder", ".")));

        // Assert
        Assert.Contains("- data/sales.csv (3 bytes)", folder, StringComparison.Ordinal);
        Assert.DoesNotContain("cover.svg", folder, StringComparison.Ordinal);
        Assert.Contains("no such folder", missing, StringComparison.Ordinal);
        Assert.Contains("cover.svg", dot, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Test that a listing stops at its ceiling and says so, so that a large project cannot flood an answer.
    /// </summary>
    [Fact]
    public async Task ProjectTools_List_MoreFilesThanTheCeiling_StopsAndSaysItIsNotComplete()
    {
        // Arrange: more files than one answer lists
        for (var index = 0; index < 210; index++)
        {
            Put(Path.Combine(Project, $"f{index:D3}.txt"), "x");
        }

        // Act
        var answer = ToolAnswers.Text(await Call("project_list"));

        // Assert: the first files and not the last, with a note
        Assert.Contains("Files in the project (200):", answer, StringComparison.Ordinal);
        Assert.Contains("f199.txt", answer, StringComparison.Ordinal);
        Assert.DoesNotContain("f200.txt", answer, StringComparison.Ordinal);
        Assert.Contains("The list is not complete", answer, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Test that a listing of a folder that leaves the project or is the data folder is refused, and that a link inside
    ///     the project is neither listed nor followed.
    /// </summary>
    [Fact]
    public async Task ProjectTools_List_EscapingFolderAndLinks_AreRefusedAndNotFollowed()
    {
        // Arrange: a junction inside the project that leads to a folder with a secret
        var secret = Path.Combine(Root, "secret");
        Directory.CreateDirectory(secret);
        Put(Path.Combine(secret, "key.txt"), Secret);
        Links.CreateJunction(Path.Combine(Project, "link"), secret);
        Put(Path.Combine(Project, "cover.svg"), "<svg/>");

        // Act
        var all = ToolAnswers.Text(await Call("project_list"));
        var through = ToolAnswers.Text(await Call("project_list", ("folder", "link")));
        var up = ToolAnswers.Text(await Call("project_list", ("folder", "..")));
        var data = ToolAnswers.Text(await Call("project_list", ("folder", ".canvaspilot")));

        // Assert: the link is not listed, nothing is shown through it, and the other folders are refused
        Assert.Contains("cover.svg", all, StringComparison.Ordinal);
        Assert.DoesNotContain("link", all, StringComparison.Ordinal);
        Assert.DoesNotContain("key.txt", all, StringComparison.Ordinal);
        Assert.Contains("link", through, StringComparison.Ordinal);
        Assert.DoesNotContain("key.txt", through, StringComparison.Ordinal);
        Assert.DoesNotContain("draft.txt", up + data, StringComparison.Ordinal);
        Assert.DoesNotContain("outside.txt", up, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Test that a text image source, a data file, and a nested file are read exactly, that a byte order mark is not
    ///     part of the text, and that an empty file is said to be empty.
    /// </summary>
    [Fact]
    public async Task ProjectTools_Read_TextFiles_ReturnTheirText()
    {
        // Arrange
        var svg = "<svg xmlns=\"http://www.w3.org/2000/svg\"><text>é Ünï</text></svg>";
        Put(Path.Combine(Project, "cover.svg"), svg);
        Directory.CreateDirectory(Path.Combine(Project, "data"));
        Put(Path.Combine(Project, "data", "sales.csv"), "a,b\n1,2\n");
        Put(Path.Combine(Project, "empty.txt"), string.Empty);
        PutBytes(Path.Combine(Project, "bom.txt"), [0xEF, 0xBB, 0xBF, (byte)'h', (byte)'i']);

        // Act
        var cover = ToolAnswers.Text(await Call("project_read", ("path", "cover.svg")));
        var sales = ToolAnswers.Text(await Call("project_read", ("path", "data/sales.csv")));
        var viaFullPath = ToolAnswers.Text(await Call("project_read", ("path", Path.Combine(Project, "cover.svg"))));
        var empty = ToolAnswers.Text(await Call("project_read", ("path", "empty.txt")));
        var bom = ToolAnswers.Text(await Call("project_read", ("path", "bom.txt")));

        // Assert
        Assert.Equal(svg, cover);
        Assert.Equal("a,b\n1,2\n", sales);
        Assert.Equal(svg, viaFullPath);
        Assert.Contains("empty", empty, StringComparison.Ordinal);
        Assert.Equal("hi", bom);
    }

    /// <summary>
    ///     Test that a file that is not a text image source or a small data file is not read, with the reason: a picture in a
    ///     binary format (which is left to <c>render_image</c>), another kind of file, a missing file, a folder, a file over
    ///     the limit, and a file that is not UTF-8; and that a file exactly at the limit is read.
    /// </summary>
    [Fact]
    public async Task ProjectTools_Read_FilesThatAreNotSmallTextSources_AreRefusedWithTheReason()
    {
        // Arrange
        PutBytes(Path.Combine(Project, "photo.png"), [0x89, 0x50, 0x4E, 0x47]);
        Put(Path.Combine(Project, "tool.exe"), "MZ");
        Directory.CreateDirectory(Path.Combine(Project, "folder.txt"));
        Put(Path.Combine(Project, "big.txt"), new string('x', (64 * 1024) + 1));
        PutBytes(Path.Combine(Project, "latin.txt"), [0x63, 0x61, 0x66, 0xE9]);
        Put(Path.Combine(Project, "wide.txt"), new string('w', 40_000));
        Put(Path.Combine(Project, "limit.txt"), new string('y', 32_000));

        // Act
        var picture = ToolAnswers.Text(await Call("project_read", ("path", "photo.png")));
        var other = ToolAnswers.Text(await Call("project_read", ("path", "tool.exe")));
        var missing = ToolAnswers.Text(await Call("project_read", ("path", "nothing.svg")));
        var folder = ToolAnswers.Text(await Call("project_read", ("path", "folder.txt")));
        var big = ToolAnswers.Text(await Call("project_read", ("path", "big.txt")));
        var latin = ToolAnswers.Text(await Call("project_read", ("path", "latin.txt")));
        var wide = ToolAnswers.Text(await Call("project_read", ("path", "wide.txt")));
        var atLimit = ToolAnswers.Text(await Call("project_read", ("path", "limit.txt")));

        // Assert: each answer names its reason; a file with as many characters as an answer can hold is read
        Assert.Contains("render_image", picture, StringComparison.Ordinal);
        Assert.Contains("Only", other, StringComparison.Ordinal);
        Assert.Contains("no file", missing, StringComparison.Ordinal);
        Assert.Contains("no file", folder, StringComparison.Ordinal);
        Assert.Contains("64 KiB", big, StringComparison.Ordinal);
        Assert.Contains("not UTF-8", latin, StringComparison.Ordinal);
        Assert.Contains("40000 characters", wide, StringComparison.Ordinal);
        Assert.Equal(new string('y', 32_000), atLimit);
    }

    /// <summary>
    ///     Test that no path that leaves the project, reaches the data folder or a project file, or is spelled to mean
    ///     something else is read, and that no content of anything the path might have meant appears in an answer.
    /// </summary>
    /// <param name="path">The path the model sends.</param>
    [Theory]
    [MemberData(nameof(EscapingPaths))]
    public async Task ProjectTools_Read_EscapingPaths_AreRefusedForEveryClass(string path)
    {
        // Act
        var answer = ToolAnswers.Text(await Call("project_read", ("path", path)));

        // Assert: no content of anything the path might have meant
        Assert.DoesNotContain(Secret, answer, StringComparison.Ordinal);
        Assert.DoesNotContain("PROJECT-FILE", answer, StringComparison.Ordinal);
        Assert.DoesNotContain("DATA-FOLDER-FILE", answer, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Test that an absolute path outside the project, which reads the secret if it is allowed, is refused.
    /// </summary>
    [Fact]
    public async Task ProjectTools_Read_AbsolutePathOutsideTheProject_IsRefused()
    {
        // Act
        var answer = ToolAnswers.Text(await Call("project_read", ("path", Path.Combine(Root, "outside.txt"))));

        // Assert
        Assert.DoesNotContain(Secret, answer, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Test that a file cannot be read through a folder junction, a symbolic link to a folder, or a symbolic link to a
    ///     file, all of which lead outside the project and are permitted by the path policy of the AI library.
    /// </summary>
    [Fact]
    public async Task ProjectTools_Read_ThroughJunction_IsRefused()
    {
        // Arrange: the secret behind a junction
        var secret = Path.Combine(Root, "secret");
        Directory.CreateDirectory(secret);
        Put(Path.Combine(secret, "key.txt"), Secret);
        Links.CreateJunction(Path.Combine(Project, "junction"), secret);

        // Act
        var junction = ToolAnswers.Text(await Call("project_read", ("path", "junction/key.txt")));
        var junctionFull = ToolAnswers.Text(await Call("project_read", ("path", Path.Combine(Project, "junction", "key.txt"))));

        // Assert
        Assert.DoesNotContain(Secret, junction, StringComparison.Ordinal);
        Assert.DoesNotContain(Secret, junctionFull, StringComparison.Ordinal);
        Assert.Contains("link", junction, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Test that a file cannot be read through a symbolic link to a folder or a symbolic link to a file, which lead
    ///     outside the project and are permitted by the path policy of the AI library. The test is skipped where the user may
    ///     not make a symbolic link.
    /// </summary>
    [Fact]
    public async Task ProjectTools_Read_ThroughSymbolicLinks_IsRefused()
    {
        // Arrange: the secret behind a symbolic link to a folder and one to a file
        var secret = Path.Combine(Root, "secret");
        Directory.CreateDirectory(secret);
        Put(Path.Combine(secret, "key.txt"), Secret);
        Links.CreateFolderSymlinkOrSkip(Path.Combine(Project, "folder-link"), secret);
        Links.CreateFileSymlinkOrSkip(Path.Combine(Project, "file-link.txt"), Path.Combine(Root, "outside.txt"));

        // Act
        var viaFolder = ToolAnswers.Text(await Call("project_read", ("path", "folder-link/key.txt")));
        var viaFile = ToolAnswers.Text(await Call("project_read", ("path", "file-link.txt")));

        // Assert
        Assert.DoesNotContain(Secret, viaFolder, StringComparison.Ordinal);
        Assert.DoesNotContain(Secret, viaFile, StringComparison.Ordinal);
        Assert.Contains("link", viaFile, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Test that a new file is created with its folders, that the answer says it was created, that the file holds exactly
    ///     the text in UTF-8 without a byte order mark, and that the shell is told with the relative path and the flag.
    /// </summary>
    [Fact]
    public async Task ProjectTools_Write_NewFile_IsCreatedWithItsFoldersAndTheShellIsTold()
    {
        // Act
        var answer = ToolAnswers.Text(await Call("project_write", ("path", "drawings/2026/flow.mmd"), ("content", "graph TD\n  A-->B é\n")));

        // Assert
        var path = Path.Combine(Project, "drawings", "2026", "flow.mmd");
        Assert.Contains("created", answer, StringComparison.Ordinal);
        Assert.DoesNotContain("replaced", answer, StringComparison.Ordinal);
        Assert.Equal("graph TD\n  A-->B é\n", Get(path));
        Assert.False(GetBytes(path).AsSpan().StartsWith(new byte[] { 0xEF, 0xBB, 0xBF }));
        var written = Assert.Single(Written);
        Assert.Equal("drawings/2026/flow.mmd", written.RelativePath);
        Assert.True(written.Created);
    }

    /// <summary>
    ///     Test that a file that exists is replaced directly: the new text is there, the answer says it was replaced with
    ///     both sizes, the shell is told, and nothing else appears in the project: no backup folder, no backup file, no
    ///     temporary file, no data folder created.
    /// </summary>
    [Fact]
    public async Task ProjectTools_Write_ExistingFile_IsReplacedDirectlyAndLeavesNothingElseInTheProject()
    {
        // Arrange: a project with a file and the state of the project before the write
        var path = Path.Combine(Project, "cover.svg");
        Put(path, "<svg>old</svg>");
        var before = Snapshot(Project);

        // Act
        var answer = ToolAnswers.Text(await Call("project_write", ("path", "cover.svg"), ("content", "<svg>new content</svg>")));

        // Assert: replaced, and the only change in the project is the content of that one file
        Assert.Contains("replaced", answer, StringComparison.Ordinal);
        Assert.DoesNotContain("created", answer, StringComparison.Ordinal);
        Assert.Contains("14 bytes -> 22 bytes", answer, StringComparison.Ordinal);
        Assert.Equal("<svg>new content</svg>", Get(path));
        var after = Snapshot(Project);
        Assert.Equal(before.Keys.Order(StringComparer.Ordinal), after.Keys.Order(StringComparer.Ordinal));
        Assert.False(Directory.Exists(Path.Combine(Project, ".canvaspilot", "backups")));
        Assert.DoesNotContain(Directory.EnumerateFileSystemEntries(Root, "*", SearchOption.AllDirectories), entry => entry.Contains("backup", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(after.Keys, key => key.EndsWith(".tmp", StringComparison.OrdinalIgnoreCase));
        var written = Assert.Single(Written);
        Assert.Equal("cover.svg", written.RelativePath);
        Assert.False(written.Created);
    }

    /// <summary>
    ///     Test that every kind of file that a drawing is made of can be written, and that other kinds, pictures in a binary
    ///     format, and names that only end like an allowed one are refused without writing anything.
    /// </summary>
    /// <param name="name">The name of the file.</param>
    /// <param name="allowed">Whether the write is allowed.</param>
    [Theory]
    [InlineData("a.svg", true)]
    [InlineData("a.SVG", true)]
    [InlineData("a.mmd", true)]
    [InlineData("a.mermaid", true)]
    [InlineData("a.puml", true)]
    [InlineData("a.plantuml", true)]
    [InlineData("a.pu", true)]
    [InlineData("a.scad", true)]
    [InlineData("a.vl", true)]
    [InlineData("a.vl.json", true)]
    [InlineData("a.csv", true)]
    [InlineData("a.json", true)]
    [InlineData("a.txt", true)]
    [InlineData("a.png", false)]
    [InlineData("a.jpg", false)]
    [InlineData("a.pdf", false)]
    [InlineData("a.pptx", false)]
    [InlineData("a.exe", false)]
    [InlineData("a.cs", false)]
    [InlineData("a.ps1", false)]
    [InlineData("a.svg.exe", false)]
    [InlineData("a", false)]
    public async Task ProjectTools_Write_Extension_IsAllowedOnlyForTextImageSourcesAndSmallDataFiles(string name, bool allowed)
    {
        // Act
        var answer = ToolAnswers.Text(await Call("project_write", ("path", name), ("content", "x")));

        // Assert: the file exists exactly when the write is allowed, and a refusal says what is allowed
        Assert.Equal(allowed, File.Exists(Path.Combine(Project, name)));
        Assert.Equal(allowed, answer.Contains("created", StringComparison.Ordinal));
        Assert.Equal(allowed, Written.Count == 1);
        if (!allowed)
        {
            Assert.Contains("Allowed", answer, StringComparison.Ordinal);
        }
    }

    /// <summary>
    ///     Test that a file of exactly 256 KiB is written and one byte more is refused, counted in bytes of UTF-8 and not in
    ///     characters, and that a refused write leaves an old file as it was.
    /// </summary>
    [Fact]
    public async Task ProjectTools_Write_SizeCeiling_Is256KiBInBytesAndARefusedWriteLeavesTheOldFile()
    {
        // Arrange
        Put(Path.Combine(Project, "old.txt"), "old");

        // Act
        var exact = ToolAnswers.Text(await Call("project_write", ("path", "exact.txt"), ("content", new string('x', 256 * 1024))));
        var over = ToolAnswers.Text(await Call("project_write", ("path", "over.txt"), ("content", new string('x', (256 * 1024) + 1))));
        var wide = ToolAnswers.Text(await Call("project_write", ("path", "old.txt"), ("content", new string('é', 130 * 1024))));

        // Assert
        Assert.Contains("created", exact, StringComparison.Ordinal);
        Assert.Equal(256 * 1024, new FileInfo(Path.Combine(Project, "exact.txt")).Length);
        Assert.Contains("at most 256 KiB", over, StringComparison.Ordinal);
        Assert.False(File.Exists(Path.Combine(Project, "over.txt")));
        Assert.Contains("at most 256 KiB", wide, StringComparison.Ordinal);
        Assert.Equal("old", Get(Path.Combine(Project, "old.txt")));
        Assert.Single(Written);
    }

    /// <summary>
    ///     Test that the places of the application and of the user are never written: the data folder, the project file, and
    ///     every file of a style kit of the project at any depth, with any case. The files stay as they were and the shell is
    ///     not told.
    /// </summary>
    /// <param name="path">The path the model sends.</param>
    [Theory]
    [InlineData(".canvaspilot/backups/old.svg")]
    [InlineData(".canvaspilot/notes.txt")]
    [InlineData("Brochure.canvaspilot")]
    [InlineData("new.canvaspilot")]
    [InlineData("styles/brand/style.json")]
    [InlineData("styles/brand/examples/title.svg")]
    [InlineData("Styles/brand/examples/title.svg")]
    [InlineData("STYLES/brand.json")]
    [InlineData("styles/loose.svg")]
    public async Task ProjectTools_Write_ApplicationFilesAndStyleKits_AreNeverWritten(string path)
    {
        // Arrange: the state of the project before
        var before = Snapshot(Project);
        var projectFile = Get(Path.Combine(Project, "Brochure.canvaspilot"));

        // Act
        var answer = ToolAnswers.Text(await Call("project_write", ("path", path), ("content", "{\"x\":1}")));

        // Assert: nothing changed anywhere, and the answer does not claim a write
        Assert.DoesNotContain("created", answer, StringComparison.Ordinal);
        Assert.DoesNotContain("replaced", answer, StringComparison.Ordinal);
        Assert.Equal(before, Snapshot(Project));
        Assert.Equal(projectFile, Get(Path.Combine(Project, "Brochure.canvaspilot")));
        Assert.Empty(Written);
    }

    /// <summary>
    ///     Test that a name that starts with a dot is never written or created, at any depth, whatever the extension: the
    ///     folders of other tools (.vscode, .git, .devcontainer), the answer says what to do instead, and nothing is created.
    /// </summary>
    /// <param name="path">The path the model sends.</param>
    [Theory]
    [InlineData(".vscode/tasks.json")]
    [InlineData(".git/config.txt")]
    [InlineData(".devcontainer/devcontainer.json")]
    [InlineData("drawings/.hidden/notes.txt")]
    [InlineData("drawings/.env.txt")]
    [InlineData(".gitignore.txt")]
    [InlineData(".svg")]
    public async Task ProjectTools_Write_NamesThatStartWithADot_AreRefusedWithAnActionableAnswerAndNothingIsCreated(string path)
    {
        // Arrange
        var before = Snapshot(Project);

        // Act
        var answer = ToolAnswers.Text(await Call("project_write", ("path", path), ("content", "{\"x\":1}")));

        // Assert: the answer names the reason and a way forward, and no file or folder appeared
        Assert.Contains("starts with a dot", answer, StringComparison.Ordinal);
        Assert.Contains("Choose a path", answer, StringComparison.Ordinal);
        Assert.DoesNotContain("created", answer, StringComparison.Ordinal);
        Assert.Equal(before, Snapshot(Project));
        Assert.False(Directory.Exists(Path.Combine(Project, ".vscode")));
        Assert.False(Directory.Exists(Path.Combine(Project, ".devcontainer")));
        Assert.Empty(Written);
    }

    /// <summary>
    ///     Test that a hidden file that already exists is not replaced.
    /// </summary>
    [Fact]
    public async Task ProjectTools_Write_ExistingFileInAHiddenFolder_IsNotReplaced()
    {
        // Arrange
        var folder = Path.Combine(Project, ".vscode");
        Directory.CreateDirectory(folder);
        Put(Path.Combine(folder, "tasks.json"), "KEEP");

        // Act
        await Call("project_write", ("path", ".vscode/tasks.json"), ("content", "{}"));

        // Assert
        Assert.Equal("KEEP", Get(Path.Combine(folder, "tasks.json")));
        Assert.Empty(Written);
    }

    /// <summary>
    ///     Test that a style kit file that exists is not replaced, and that the answer says why.
    /// </summary>
    [Fact]
    public async Task ProjectTools_Write_ExistingStyleKitFile_IsNotReplacedAndTheAnswerSaysWhy()
    {
        // Arrange
        var kit = Path.Combine(Project, "styles", "brand");
        Directory.CreateDirectory(kit);
        Put(Path.Combine(kit, "style.json"), "KIT");

        // Act
        var answer = ToolAnswers.Text(await Call("project_write", ("path", "styles/brand/style.json"), ("content", "{}")));

        // Assert
        Assert.Equal("KIT", Get(Path.Combine(kit, "style.json")));
        Assert.Contains("style kits", answer, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Test that no path that leaves the project or means something else is written, for every class of path, that
    ///     nothing is created outside the project or inside it, and that the secret stays as it was.
    /// </summary>
    /// <param name="path">The path the model sends.</param>
    [Theory]
    [MemberData(nameof(EscapingPaths))]
    public async Task ProjectTools_Write_EscapingPaths_AreRefusedForEveryClassAndNothingIsWritten(string path)
    {
        // Arrange
        var before = Snapshot(Root);

        // Act
        var answer = ToolAnswers.Text(await Call("project_write", ("path", path), ("content", "pwned")));

        // Assert: every file under the folder of the test is as it was, including the project file and the secret
        Assert.DoesNotContain("created", answer, StringComparison.Ordinal);
        Assert.DoesNotContain("replaced", answer, StringComparison.Ordinal);
        Assert.Equal(before, Snapshot(Root));
        Assert.Equal(Secret, Get(Path.Combine(Root, "outside.txt")));
        Assert.Empty(Written);
    }

    /// <summary>
    ///     Test that full paths outside the project, a folder beside the project whose name starts like it, and the secret by
    ///     its full path are not written.
    /// </summary>
    [Fact]
    public async Task ProjectTools_Write_AbsolutePathsOutsideTheProject_AreRefused()
    {
        // Arrange
        Directory.CreateDirectory(Path.Combine(Root, "project-evil"));
        var before = Snapshot(Root);

        // Act
        foreach (var path in new[] { Path.Combine(Root, "outside.txt"), Path.Combine(Root, "new.txt"), Path.Combine(Root, "project-evil", "a.svg") })
        {
            var answer = ToolAnswers.Text(await Call("project_write", ("path", path), ("content", "pwned")));
            Assert.DoesNotContain("created", answer, StringComparison.Ordinal);
        }

        // Assert
        Assert.Equal(before, Snapshot(Root));
        Assert.Empty(Written);
    }

    /// <summary>
    ///     Test that a write cannot go through a folder junction, a symbolic link to a folder, or a symbolic link to a file
    ///     that lead outside the project: the target keeps its content, nothing is created behind the link, and the shell is
    ///     not told. The symbolic links are skipped where they cannot be made.
    /// </summary>
    [Fact]
    public async Task ProjectTools_Write_ThroughJunction_IsRefusedAndNothingIsWrittenOutside()
    {
        // Arrange: the places behind the links
        var behind = Path.Combine(Root, "behind");
        Directory.CreateDirectory(behind);
        Put(Path.Combine(behind, "key.svg"), "KEEP");
        Links.CreateJunction(Path.Combine(Project, "junction"), behind);
        var before = Snapshot(behind);

        // Act: through the junction, to a new file, to an existing file, below a folder that does not exist yet
        var created = ToolAnswers.Text(await Call("project_write", ("path", "junction/new.svg"), ("content", "pwned")));
        var replaced = ToolAnswers.Text(await Call("project_write", ("path", "junction/key.svg"), ("content", "pwned")));
        var deep = ToolAnswers.Text(await Call("project_write", ("path", "junction/sub/new.svg"), ("content", "pwned")));

        // Assert
        Assert.DoesNotContain("created", created + deep, StringComparison.Ordinal);
        Assert.DoesNotContain("replaced", replaced, StringComparison.Ordinal);
        Assert.Equal(before, Snapshot(behind));
        Assert.Empty(Written);
    }

    /// <summary>
    ///     Test that a write cannot go through a symbolic link to a folder or a symbolic link to a file that lead outside the
    ///     project: the targets keep their content, nothing is created behind the link, and the shell is not told. The test is
    ///     skipped where the user may not make a symbolic link.
    /// </summary>
    [Fact]
    public async Task ProjectTools_Write_ThroughSymbolicLinks_IsRefusedAndNothingIsWrittenOutside()
    {
        // Arrange: a folder and a file outside, and a symbolic link to each inside the project
        var behind = Path.Combine(Root, "behind");
        Directory.CreateDirectory(behind);
        Put(Path.Combine(behind, "key.svg"), "KEEP");
        var before = Snapshot(behind);
        Links.CreateFolderSymlinkOrSkip(Path.Combine(Project, "folder-link"), behind);
        Links.CreateFileSymlinkOrSkip(Path.Combine(Project, "file-link.txt"), Path.Combine(Root, "outside.txt"));

        // Act
        var viaFolder = ToolAnswers.Text(await Call("project_write", ("path", "folder-link/new.svg"), ("content", "pwned")));
        var viaFile = ToolAnswers.Text(await Call("project_write", ("path", "file-link.txt"), ("content", "pwned")));

        // Assert
        Assert.DoesNotContain("created", viaFolder, StringComparison.Ordinal);
        Assert.DoesNotContain("replaced", viaFile, StringComparison.Ordinal);
        Assert.Equal(before, Snapshot(behind));
        Assert.Equal(Secret, Get(Path.Combine(Root, "outside.txt")));
        Assert.Empty(Written);
    }

    /// <summary>
    ///     Test that a file with a second name outside the project (a hard link) is replaced as a name and not written
    ///     through, so the other name keeps its content.
    /// </summary>
    [Fact]
    public async Task ProjectTools_Write_HardLinkToAFileOutside_IsNotWrittenThrough()
    {
        // Arrange: a file inside the project that is another name of the secret
        var link = Path.Combine(Project, "hard.txt");
        Links.CreateHardLink(link, Path.Combine(Root, "outside.txt"));

        // Act
        var answer = ToolAnswers.Text(await Call("project_write", ("path", "hard.txt"), ("content", "inside only")));

        // Assert: the project name has the new text and the other name is unchanged
        Assert.Contains("replaced", answer, StringComparison.Ordinal);
        Assert.Equal("inside only", Get(link));
        Assert.Equal(Secret, Get(Path.Combine(Root, "outside.txt")));
    }

    /// <summary>
    ///     Test that a missing content, a folder in place of the file, and a file that cannot be replaced are answers and
    ///     not exceptions, that the old file survives a failed write, and that no temporary file is left behind.
    /// </summary>
    [Fact]
    public async Task ProjectTools_Write_BadRequestsAndFailures_AreAnswersAndLeaveTheOldFileAndNoTemporaryFile()
    {
        // Arrange: a file that is locked, so that it cannot be replaced, and a folder with the name of a file
        Assert.SkipUnless(OperatingSystem.IsWindows(), "Only Windows refuses to replace a file that is open without sharing.");
        Directory.CreateDirectory(Path.Combine(Project, "folder.svg"));
        var locked = Path.Combine(Project, "locked.svg");
        Put(locked, "OLD");

        // Act
        var noContent = ToolAnswers.Text(await Call("project_write", ("path", "a.svg"), ("content", null)));
        var folder = ToolAnswers.Text(await Call("project_write", ("path", "folder.svg"), ("content", "x")));
        string failure;
        using (new FileStream(locked, FileMode.Open, FileAccess.Read, FileShare.None))
        {
            failure = ToolAnswers.Text(await Call("project_write", ("path", "locked.svg"), ("content", "NEW")));
        }

        // Assert
        Assert.Contains("content", noContent, StringComparison.Ordinal);
        Assert.False(File.Exists(Path.Combine(Project, "a.svg")));
        Assert.Contains("folder", folder, StringComparison.Ordinal);
        Assert.Contains("could not be written (", failure, StringComparison.Ordinal);
        Assert.DoesNotContain(Project, failure, StringComparison.Ordinal);
        Assert.Equal("OLD", Get(locked));
        Assert.DoesNotContain(Snapshot(Project).Keys, key => key.EndsWith(".tmp", StringComparison.OrdinalIgnoreCase));
        Assert.Empty(Written);
    }

    /// <summary>
    ///     Test that a listener of <see cref="ProjectTools.FileWritten" /> that fails does not fail the tool: the file is
    ///     written and the answer says so.
    /// </summary>
    [Fact]
    public async Task ProjectTools_Write_ListenerThatThrows_DoesNotFailTheTool()
    {
        // Arrange
        Tools.FileWritten += (_, _) => throw new InvalidOperationException("listener failed");

        // Act
        var answer = ToolAnswers.Text(await Call("project_write", ("path", "a.svg"), ("content", "<svg/>")));

        // Assert
        Assert.Contains("created", answer, StringComparison.Ordinal);
        Assert.Equal("<svg/>", Get(Path.Combine(Project, "a.svg")));
    }

    /// <summary>
    ///     Test that the log of the tools holds the metadata of a call (the tool, the project-relative path, the size, the
    ///     outcome) and never the content of a file or the path of a refused request.
    /// </summary>
    [Fact]
    public async Task ProjectTools_Log_CallsOfTheTools_RecordOnlyMetadata()
    {
        // Arrange: tools with a logger, and a text that must never reach the log
        var logger = new RollingFileLogger(Path.Combine(Root, "logs"));
        var tools = new ProjectTools(Renderer, Style, null, logger);
        const string sentinel = "SENTINEL-CONTENT-SECRET";
        const string evilPath = "../EVIL-REQUEST-PATH.txt";

        // Act
        await Call(tools, "project_write", ("path", "notes.txt"), ("content", sentinel));
        await Call(tools, "project_read", ("path", "notes.txt"));
        await Call(tools, "project_write", ("path", evilPath), ("content", sentinel));
        await Call(tools, "render_image", ("path", "notes.txt"));

        // Assert: the facts are there and the words are not
        var log = Get(logger.ActiveFilePath);
        Assert.Contains("Tool project_write: path='notes.txt' bytes=23 outcome=done", log, StringComparison.Ordinal);
        Assert.Contains("Tool project_read: path='notes.txt' bytes=23 outcome=done", log, StringComparison.Ordinal);
        Assert.Contains("outcome=refused", log, StringComparison.Ordinal);
        Assert.DoesNotContain(sentinel, log, StringComparison.Ordinal);
        Assert.DoesNotContain("EVIL-REQUEST-PATH", log, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Test that a tool whose logger cannot write still answers: the log is not part of the call.
    /// </summary>
    [Fact]
    public async Task ProjectTools_Log_LoggerThatCannotWrite_DoesNotFailTheTool()
    {
        // Arrange: a log whose file cannot be appended to, because a folder has its name
        var logger = new RollingFileLogger(Path.Combine(Root, "logs"));
        Directory.CreateDirectory(logger.ActiveFilePath);
        var tools = new ProjectTools(Renderer, Style, null, logger);

        // Act
        var answer = ToolAnswers.Text(await Call(tools, "project_write", ("path", "a.txt"), ("content", "x")));

        // Assert
        Assert.Contains("created", answer, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Test that a missing path argument is an answer for every file tool and not an exception.
    /// </summary>
    [Fact]
    public async Task ProjectTools_Calls_MissingOrEmptyPath_AreAnswers()
    {
        // Act
        var read = ToolAnswers.Text(await Call("project_read", ("path", null)));
        var write = ToolAnswers.Text(await Call("project_write", ("path", null), ("content", "x")));

        // Assert
        Assert.Contains("empty", read, StringComparison.Ordinal);
        Assert.Contains("empty", write, StringComparison.Ordinal);
        Assert.Empty(Written);
    }
}
