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
using System.Reflection;
using System.Text.Json.Nodes;
using DemaConsulting.AgentKit.Agents.Copilot;
using DemaConsulting.AgentKit.Core;
using DemaConsulting.CanvasPilot.AgentSubsystem;
using DemaConsulting.CanvasPilot.KnowledgeSubsystem;
using DemaConsulting.CanvasPilot.StyleSubsystem;
using Microsoft.Extensions.AI;

namespace DemaConsulting.CanvasPilot.Tests;

/// <summary>
///     Unit tests for <see cref="AgentToolRegistry" />: the path policy that confines the tools to the project folder, the
///     built-in packs made of the existing tool descriptions, the packs that the application adds per project, the empty
///     catalog of optional packs, and the check of the set of tools.
/// </summary>
public sealed class AgentToolRegistryTests : IDisposable
{
    /// <summary>
    ///     The folder that holds the project and the places beside it that the tools must not reach.
    /// </summary>
    private readonly string _root = Directory.CreateTempSubdirectory("canvaspilot-registry-tests-").FullName;

    /// <summary>
    ///     Initializes a new instance of the <see cref="AgentToolRegistryTests" /> class by making the project folder.
    /// </summary>
    public AgentToolRegistryTests()
    {
        Directory.CreateDirectory(Project);
    }

    /// <summary>
    ///     Gets the folder of the project of the tests.
    /// </summary>
    private string Project => Path.Combine(_root, "project");

    /// <inheritdoc />
    public void Dispose()
    {
        // The folder link of a test is removed as a link first, so that deleting the folders never reaches what it leads to
        var link = Path.Combine(Project, "link");
        if (Directory.Exists(link) && new DirectoryInfo(link).Attributes.HasFlag(FileAttributes.ReparsePoint))
        {
            Directory.Delete(link);
        }

        Directory.Delete(_root, true);
    }

    /// <summary>
    ///     Test that the policy has the project folder as its working directory and one read-write grant on it that denies
    ///     the data folder and the project files, that a trailing separator does not matter, and that the limits are the
    ///     ones given.
    /// </summary>
    [Fact]
    public void AgentToolRegistry_CreatePolicy_ProjectFolder_HasOneReadWriteGrantThatDeniesTheDataFolderAndProjectFiles()
    {
        // Arrange: the limits to carry, and the folder spelled with and without a trailing separator
        var limits = new ToolLimits(maxResultCharacters: 1234);

        // Act: make the policy for both spellings
        var policy = AgentToolRegistry.CreatePolicy(Project + Path.DirectorySeparatorChar, limits);
        var plain = AgentToolRegistry.CreatePolicy(Project);

        // Assert: the working directory is the folder, the one grant is read-write on it with the two deny patterns
        Assert.Equal(Project, policy.WorkingDirectory);
        Assert.Equal(Project, plain.WorkingDirectory);
        var grant = Assert.Single(policy.Grants);
        Assert.Equal(AccessLevel.ReadWrite, grant.Access);
        Assert.Equal(Project, grant.Root);
        Assert.Equal([".canvaspilot", "*.canvaspilot"], grant.DenyPatterns);
        Assert.True(policy.AnyLocationIsWritable);
        Assert.Equal(1234, policy.Limits.MaxResultCharacters);
        Assert.Equal(ToolLimits.Default.MaxResultCharacters, plain.Limits.MaxResultCharacters);
    }

    /// <summary>
    ///     Test that the policy permits reading and writing a file in the project and below it, and writing a new file
    ///     there.
    /// </summary>
    [Fact]
    public void AgentToolRegistry_CreatePolicy_PathsInsideTheProject_ArePermittedForReadAndWrite()
    {
        // Arrange: the policy of the project
        var policy = AgentToolRegistry.CreatePolicy(Project);

        // Act: resolve a file at the top, one below a folder that does not exist yet, and one by its full path
        var top = policy.TryResolveWrite("drawing.svg", out var topPath, out _);
        var nested = policy.TryResolveWrite(Path.Combine("figures", "box.svg"), out var nestedPath, out _);
        var full = policy.TryResolveRead(Path.Combine(Project, "notes.txt"), out var fullPath, out _);

        // Assert: all are permitted and resolve inside the project
        Assert.True(top);
        Assert.Equal(Path.Combine(Project, "drawing.svg"), topPath);
        Assert.True(nested);
        Assert.Equal(Path.Combine(Project, "figures", "box.svg"), nestedPath);
        Assert.True(full);
        Assert.Equal(Path.Combine(Project, "notes.txt"), fullPath);
    }

    /// <summary>
    ///     Test that the policy refuses, for reading and for writing, the data folder, a project file, a data folder below
    ///     a folder, and a project file below a folder, each with a message and not an exception.
    /// </summary>
    [Fact]
    public void AgentToolRegistry_CreatePolicy_DataFolderAndProjectFiles_AreRefusedForReadAndWrite()
    {
        // Arrange: the policy of the project, and the paths that belong to the application
        var policy = AgentToolRegistry.CreatePolicy(Project);
        string[] protectedPaths =
        [
            Path.Combine(".canvaspilot", "chat", "history.json"),
            ".canvaspilot",
            "Demo.canvaspilot",
            Path.Combine("sub", ".canvaspilot", "state.json"),
            Path.Combine("sub", "Other.canvaspilot"),
        ];

        // Act / Assert: each is refused for reading and for writing, with a message that says why
        foreach (var path in protectedPaths)
        {
            Assert.False(policy.TryResolveRead(path, out _, out var readMessage), path);
            Assert.False(policy.TryResolveWrite(path, out _, out var writeMessage), path);
            Assert.False(string.IsNullOrWhiteSpace(readMessage), path);
            Assert.False(string.IsNullOrWhiteSpace(writeMessage), path);
        }
    }

    /// <summary>
    ///     Test that the policy refuses a path that climbs out of the project, a full path outside it, a folder beside it
    ///     whose name starts like the project's, and a path on a network share.
    /// </summary>
    [Fact]
    public void AgentToolRegistry_CreatePolicy_PathsOutsideTheProject_AreRefusedForReadAndWrite()
    {
        // Arrange: the policy, and a folder beside the project whose name begins with the project folder's name
        var policy = AgentToolRegistry.CreatePolicy(Project);
        Directory.CreateDirectory(Path.Combine(_root, "project-evil"));
        string[] outside =
        [
            Path.Combine("..", "outside.txt"),
            Path.Combine("sub", "..", "..", "outside.txt"),
            Path.Combine(_root, "outside.txt"),
            Path.Combine(_root, "project-evil", "a.svg"),
            Path.Combine("..", "project-evil", "a.svg"),
            TestPlatformPaths.NetworkFolder,
        ];

        // Act / Assert: each is refused for reading and for writing
        foreach (var path in outside)
        {
            Assert.False(policy.TryResolveRead(path, out _, out _), path);
            Assert.False(policy.TryResolveWrite(path, out _, out _), path);
        }
    }

    /// <summary>
    ///     Test that a folder link inside the project that leads outside it is permitted by the policy, because the AI
    ///     library judges a path by its spelling and does not follow links. The test pins the finding so that the project
    ///     tools, which must refuse links themselves, are not forgotten when the library changes.
    /// </summary>
    [Fact]
    public void AgentToolRegistry_CreatePolicy_FolderLinkInsideTheProject_IsPermittedAndMustBeRefusedByTheTools()
    {
        // Arrange: a folder outside the project, and a link to it inside the project
        Assert.SkipUnless(OperatingSystem.IsWindows(), "The test makes a folder junction, which only Windows has.");
        var secret = Path.Combine(_root, "secret");
        Directory.CreateDirectory(secret);
        File.WriteAllText(Path.Combine(secret, "key.txt"), "secret");
        var link = Path.Combine(Project, "link");
        CreateFolderJunction(link, secret);
        var policy = AgentToolRegistry.CreatePolicy(Project);

        // Act: resolve a file through the link
        var read = policy.TryResolveRead(Path.Combine("link", "key.txt"), out var path, out _);

        // Assert: it is permitted, the path keeps the spelling inside the project, and the link is a reparse point that
        // a tool can recognize
        Assert.True(read);
        Assert.Equal(Path.Combine(link, "key.txt"), path);
        Assert.True(new DirectoryInfo(link).Attributes.HasFlag(FileAttributes.ReparsePoint));
    }

    /// <summary>
    ///     Test that a policy is refused for a folder that is empty or is not a full path.
    /// </summary>
    [Fact]
    public void AgentToolRegistry_CreatePolicy_EmptyOrRelativeFolder_IsRefused()
    {
        // Act / Assert: nothing, white space, and a relative folder are refused
        Assert.Throws<ArgumentException>(() => AgentToolRegistry.CreatePolicy(string.Empty));
        Assert.Throws<ArgumentException>(() => AgentToolRegistry.CreatePolicy("  "));
        Assert.Throws<ArgumentException>(() => AgentToolRegistry.CreatePolicy(Path.Combine("relative", "project")));
        Assert.Throws<ArgumentException>(() => AgentToolRegistry.CreatePolicy(null!));
    }

    /// <summary>
    ///     Test that the registry offers the drawing knowledge, then the style tools, in that order, as functions that
    ///     carry the schemas of the descriptions, and that calling them reaches the descriptions.
    /// </summary>
    [Fact]
    public async Task AgentToolRegistry_CreateTools_BuiltInPacks_OfferKnowledgeThenStyleToolsWithTheirSchemas()
    {
        // Arrange: the real description of the drawing knowledge and descriptions of the two style tools with the real
        // schemas and names
        var calls = new List<string>();
        var registry = CreateRegistry(calls);

        // Act: create the tools of the project, and call the style check
        var tools = registry.CreateTools(new AgentToolContext(Project), new HashSet<string>());
        var check = tools.Single(tool => tool.Name == StyleTools.CheckToolName);
        await check.InvokeAsync(new AIFunctionArguments { ["file"] = "a.svg" }, TestContext.Current.CancellationToken);

        // Assert: the names are in order, the schemas are those of the descriptions, and the call reached the description
        Assert.Equal(["canvasnet_help", "style_get", "style_check"], tools.Select(tool => tool.Name).ToArray());
        Assert.True(JsonNode.DeepEquals(JsonNode.Parse(KnowledgeTools.HelpParametersSchema), JsonNode.Parse(tools[0].JsonSchema.GetRawText())));
        Assert.True(JsonNode.DeepEquals(JsonNode.Parse(StyleTools.GetParametersSchema), JsonNode.Parse(tools[1].JsonSchema.GetRawText())));
        Assert.True(JsonNode.DeepEquals(JsonNode.Parse(StyleTools.CheckParametersSchema), JsonNode.Parse(tools[2].JsonSchema.GetRawText())));
        Assert.Equal("a.svg", ToolTestDoubles.Parse(Assert.Single(calls))["file"].GetString());
    }

    /// <summary>
    ///     Test that a supplier of project packs is asked with the context of the project, that its pack is given the
    ///     confined policy, and that its tools come after the built-in ones.
    /// </summary>
    [Fact]
    public void AgentToolRegistry_CreateTools_ProjectPacks_AreGivenTheContextAndTheConfinedPolicyAndComeLast()
    {
        // Arrange: a pack with one tool, and a supplier that records the context it gets
        var pack = new ToolTestDoubles.FakePack("project", [GuardedToolFactory.Create(() => ToolResult.Text("pong"), "project_ping", "Pings.")]);
        AgentToolContext? seen = null;
        var registry = CreateRegistry(
            [],
            projectPacks: context =>
            {
                seen = context;
                return [pack];
            });
        var context = new AgentToolContext(Project);

        // Act: create the tools
        var tools = registry.CreateTools(context, new HashSet<string>());

        // Assert: the supplier got the context, the pack got the policy of the project, and its tool is last
        Assert.Same(context, seen);
        Assert.Equal(Project, pack.LastPolicy!.WorkingDirectory);
        Assert.Equal([".canvaspilot", "*.canvaspilot"], Assert.Single(pack.LastPolicy.Grants).DenyPatterns);
        Assert.Equal(["canvasnet_help", "style_get", "style_check", "project_ping"], tools.Select(tool => tool.Name).ToArray());
    }

    /// <summary>
    ///     Test that the catalog of optional packs is empty by default, that an empty choice is accepted, and that a choice
    ///     that is not in the catalog is refused with the name of the choice.
    /// </summary>
    [Fact]
    public void AgentToolRegistry_CreateTools_EmptyOptionalCatalog_AcceptsNoChoiceAndRefusesAnyId()
    {
        // Arrange: a registry with the default catalog
        var registry = CreateRegistry([]);

        // Act: create the tools with no choice, and with a choice
        var tools = registry.CreateTools(new AgentToolContext(Project), new HashSet<string>());
        var error = Assert.Throws<ArgumentException>(() => registry.CreateTools(new AgentToolContext(Project), new HashSet<string> { "text_file" }));

        // Assert: the catalog has no pack, three tools came out, and the error names the choice and says none is known
        Assert.Empty(registry.OptionalPackIds);
        Assert.Empty(registry.OptionalPacks);
        Assert.Equal(3, tools.Count);
        Assert.Contains("text_file", error.Message, StringComparison.Ordinal);
        Assert.Contains("none", error.Message, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Test that an optional pack that is chosen is made for the project and its tools are added, in the order of the
    ///     identifiers, and that the factory of a pack that is not chosen is never called.
    /// </summary>
    [Fact]
    public void AgentToolRegistry_CreateTools_OptionalCatalog_AddsOnlyTheChosenPacksInOrderOfTheirIds()
    {
        // Arrange: a catalog of three packs, each with a factory that counts its calls
        var made = new List<string>();
        var catalog = new Dictionary<string, OptionalPackEntry>
        {
            ["zeta"] = Entry("Zeta", "zeta", made),
            ["alpha"] = Entry("Alpha", "alpha", made),
            ["mid"] = Entry("Middle", "mid", made),
        };
        var registry = CreateRegistry([], optionalPacks: catalog);

        // Act: choose two of the three, in the opposite order of their identifiers
        var tools = registry.CreateTools(new AgentToolContext(Project), new HashSet<string> { "zeta", "alpha" });

        // Assert: the identifiers are listed in order, only the chosen packs were made, and their tools follow the built-in ones
        Assert.Equal(["alpha", "mid", "zeta"], registry.OptionalPackIds);
        Assert.Equal(["alpha", "zeta"], made);
        Assert.Equal(["canvasnet_help", "style_get", "style_check", "alpha_ping", "zeta_ping"], tools.Select(tool => tool.Name).ToArray());
    }

    /// <summary>
    ///     Test that a pack that needs an ability the host does not have is not offered, and that the same pack is offered
    ///     when the host has the ability.
    /// </summary>
    [Fact]
    public void AgentToolRegistry_CreateTools_OptionalPackNeedingVision_FollowsTheHostCapabilities()
    {
        // Arrange: a catalog with a pack that needs vision, and two registries that differ only in the host's abilities
        var catalog = new Dictionary<string, OptionalPackEntry>
        {
            ["see"] = new("See", _ => new ToolTestDoubles.FakePack("see", [GuardedToolFactory.Create(() => ToolResult.Text("x"), "see_look", "Looks.")], HostCapabilities.Vision), HostCapabilities.Vision),
        };
        var blind = CreateRegistry([], optionalPacks: catalog);
        var seeing = CreateRegistry([], optionalPacks: catalog, hostCapabilities: HostCapabilities.Vision);
        var chosen = new HashSet<string> { "see" };

        // Act / Assert: without vision the pack is not offered, and with vision its tool is added
        Assert.DoesNotContain("see_look", blind.CreateTools(new AgentToolContext(Project), chosen).Select(tool => tool.Name));
        Assert.Contains("see_look", seeing.CreateTools(new AgentToolContext(Project), chosen).Select(tool => tool.Name));
    }

    /// <summary>
    ///     Test that a set in which one pack offers two tools with the same name is refused, naming the name. (The AI library
    ///     already refuses a tool whose name does not start with the prefix of its pack, so a repeat across packs cannot
    ///     happen; a repeat inside one pack can.)
    /// </summary>
    [Fact]
    public void AgentToolRegistry_CreateTools_RepeatedToolName_IsRefusedWithTheName()
    {
        // Arrange: a pack that offers the same tool twice
        var pack = new ToolTestDoubles.FakePack("extra", [new ToolTestDoubles.RawFunction("extra_one"), new ToolTestDoubles.RawFunction("extra_one")]);
        var registry = CreateRegistry([], projectPacks: _ => [pack]);

        // Act / Assert: the set is refused and the message names the repeated name
        var error = Assert.Throws<InvalidOperationException>(() => registry.CreateTools(new AgentToolContext(Project), new HashSet<string>()));
        Assert.Contains("extra_one", error.Message, StringComparison.Ordinal);
        Assert.Contains("more than one tool", error.Message, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Test that a set with a tool named like a built-in tool of the Copilot CLI is refused, with the name.
    /// </summary>
    [Fact]
    public void AgentToolRegistry_CreateTools_CopilotBuiltInName_IsRefusedWithTheName()
    {
        // Arrange: a pack with the prefix "report" that offers a tool named like the built-in report_intent
        var pack = new ToolTestDoubles.FakePack("report", [new ToolTestDoubles.RawFunction("report_intent")]);
        var registry = CreateRegistry([], projectPacks: _ => [pack]);

        // Act / Assert: the set is refused, and the message names the tool and the CLI
        var error = Assert.Throws<InvalidOperationException>(() => registry.CreateTools(new AgentToolContext(Project), new HashSet<string>()));
        Assert.Contains("report_intent", error.Message, StringComparison.Ordinal);
        Assert.Contains("Copilot CLI", error.Message, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Test that two packs with the same prefix are refused, naming the prefix.
    /// </summary>
    [Fact]
    public void AgentToolRegistry_CreateTools_RepeatedPackPrefix_IsRefusedWithThePrefix()
    {
        // Arrange: a project pack that uses the prefix of the style pack
        var pack = new ToolTestDoubles.FakePack("style", [GuardedToolFactory.Create(() => ToolResult.Text("x"), "style_other", "Other.")]);
        var registry = CreateRegistry([], projectPacks: _ => [pack]);

        // Act / Assert: the set is refused and the message names the prefix
        var error = Assert.Throws<InvalidOperationException>(() => registry.CreateTools(new AgentToolContext(Project), new HashSet<string>()));
        Assert.Contains("'style'", error.Message, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Test that a set without any tool is refused.
    /// </summary>
    [Fact]
    public void AgentToolRegistry_CreateTools_NoTool_IsRefused()
    {
        // Arrange: a registry whose built-in packs are empty and that has no other pack
        var registry = new AgentToolRegistry([], []);

        // Act / Assert: the set is refused and the message says that it has no tool
        var error = Assert.Throws<InvalidOperationException>(() => registry.CreateTools(new AgentToolContext(Project), new HashSet<string>()));
        Assert.Contains("no tool", error.Message, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Test that the arguments of the registry are checked: no descriptions, no context, no choice, and a project folder
    ///     that is not a full path.
    /// </summary>
    [Fact]
    public void AgentToolRegistry_ArgumentsAreChecked_NullsAndRelativeFolder_AreRefused()
    {
        // Arrange: a good registry
        var registry = CreateRegistry([]);

        // Act / Assert: each mistake is refused with the right kind of exception
        Assert.Throws<ArgumentNullException>(() => new AgentToolRegistry(null!, []));
        Assert.Throws<ArgumentNullException>(() => new AgentToolRegistry([], null!));
        Assert.Throws<ArgumentNullException>(() => registry.CreateTools(null!, new HashSet<string>()));
        Assert.Throws<ArgumentNullException>(() => registry.CreateTools(new AgentToolContext(Project), null!));
        Assert.Throws<ArgumentException>(() => registry.CreateTools(new AgentToolContext("relative"), new HashSet<string>()));
    }

    /// <summary>
    ///     Test that a built-in description that breaks the rules is refused when the registry is made, and not in the
    ///     middle of a conversation.
    /// </summary>
    [Fact]
    public void AgentToolRegistry_Constructor_BuiltInDescriptionWithTheWrongPrefix_IsRefused()
    {
        // Act / Assert: a style tool in the list of the drawing knowledge, and a knowledge tool in the list of the style
        // tools, are refused
        Assert.Throws<ArgumentException>(() => new AgentToolRegistry([ToolTestDoubles.Plain("style_get")], []));
        Assert.Throws<ArgumentException>(() => new AgentToolRegistry([], [ToolTestDoubles.Plain("canvasnet_help")]));
    }

    /// <summary>
    ///     Makes a registry over the real description of the drawing knowledge and stand-ins for the two style tools that
    ///     have their real names and schemas, and records the calls of the style tools.
    /// </summary>
    /// <param name="calls">Receives the JSON text of every call of a style tool.</param>
    /// <param name="projectPacks">Supplies the packs of the project, or <see langword="null" /> for none.</param>
    /// <param name="optionalPacks">The catalog of optional packs, or <see langword="null" /> for the empty one.</param>
    /// <param name="hostCapabilities">The abilities of the host.</param>
    /// <returns>The registry.</returns>
    private static AgentToolRegistry CreateRegistry(
        List<string> calls,
        Func<AgentToolContext, IEnumerable<IToolPack>>? projectPacks = null,
        IReadOnlyDictionary<string, OptionalPackEntry>? optionalPacks = null,
        HostCapabilities hostCapabilities = HostCapabilities.None)
    {
        var catalog = new FakeKnowledgeCatalog(FakeKnowledgeCatalog.Card("svg", "SVG Basics"));
        KnowledgeTool[] style =
        [
            ToolTestDoubles.Recording(StyleTools.GetToolName, calls, "the style", StyleTools.GetParametersSchema),
            ToolTestDoubles.Recording(StyleTools.CheckToolName, calls, "the check", StyleTools.CheckParametersSchema),
        ];
        return new AgentToolRegistry(KnowledgeTools.CreateAll(catalog), style, projectPacks, optionalPacks, null, hostCapabilities);
    }

    /// <summary>
    ///     Makes a catalog entry whose pack has one tool and whose factory records that it was called.
    /// </summary>
    /// <param name="displayName">The name that a settings page would show.</param>
    /// <param name="prefix">The prefix of the pack.</param>
    /// <param name="made">Receives the prefix each time the factory is called.</param>
    /// <returns>The entry.</returns>
    private static OptionalPackEntry Entry(string displayName, string prefix, List<string> made)
    {
        return new OptionalPackEntry(
            displayName,
            _ =>
            {
                made.Add(prefix);
                return new ToolTestDoubles.FakePack(prefix, [GuardedToolFactory.Create(() => ToolResult.Text("pong"), prefix + "_ping", "Pings.")]);
            },
            HostCapabilities.None);
    }

    /// <summary>
    ///     Makes a folder junction, which is a folder link that needs no privilege.
    /// </summary>
    /// <param name="link">The path of the link to make.</param>
    /// <param name="target">The folder that the link leads to.</param>
    private static void CreateFolderJunction(string link, string target)
    {
        var start = new ProcessStartInfo("cmd.exe")
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        start.ArgumentList.Add("/c");
        start.ArgumentList.Add("mklink");
        start.ArgumentList.Add("/J");
        start.ArgumentList.Add(link);
        start.ArgumentList.Add(target);
        using var process = Process.Start(start) ?? throw new InvalidOperationException("cmd.exe did not start.");
        process.StandardOutput.ReadToEnd();
        process.StandardError.ReadToEnd();
        process.WaitForExit();
        Assert.Equal(0, process.ExitCode);
    }
}
