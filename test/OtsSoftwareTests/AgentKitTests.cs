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
using DemaConsulting.AgentKit.Agents.ChatClient;
using DemaConsulting.AgentKit.Agents.Copilot;
using DemaConsulting.AgentKit.Agents.Ollama;
using DemaConsulting.AgentKit.Core;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace OtsSoftwareTests;

/// <summary>
///     Self-validation tests for the AgentKit off-the-shelf software item (the Core, Agents.Copilot,
///     Agents.ChatClient, and Agents.Ollama packages): the confined tool framework and the agent factories that
///     CanvasPilot's AI copilot is built on. They run without a language model or a Copilot CLI, using a scripted chat
///     client, and they prove that no Copilot runtime comes with the application.
/// </summary>
public class AgentKitTests
{
    /// <summary>
    ///     Test that a path policy that grants one folder for reading and writing refuses a write that climbs out of
    ///     it, a write to another location, and a write into a protected folder, and permits a write inside.
    /// </summary>
    [Fact]
    public void AgentKit_PathPolicy_ResolveWrite_RefusesEscapesAndProtectedFolders()
    {
        // Arrange: a project folder that is granted for writing, except for the folder of the application's own data
        using var folder = new TestFolder();
        var project = Path.Combine(folder.Path, "project");
        Directory.CreateDirectory(project);
        var policy = new PathPolicy(project, [PathRule.ReadWrite(project, [".canvaspilot"])]);
        var outside = Path.Combine(folder.Path, "outside.txt");

        // Act: resolve a write inside, a climb out with "..", the absolute outside location, and a protected folder
        var inside = policy.TryResolveWrite("drawing.svg", out var insidePath, out _);
        var climbed = policy.TryResolveWrite(Path.Combine("..", "outside.txt"), out _, out var climbMessage);
        var absolute = policy.TryResolveWrite(outside, out _, out _);
        var protectedFolder = policy.TryResolveWrite(Path.Combine(".canvaspilot", "state.json"), out _, out _);

        // Assert: only the write inside the project and outside the protected folder is permitted, and the refusal
        // is a message and not an exception
        Assert.True(inside);
        Assert.Equal(Path.Combine(project, "drawing.svg"), insidePath);
        Assert.False(climbed);
        Assert.False(string.IsNullOrWhiteSpace(climbMessage));
        Assert.False(absolute);
        Assert.False(protectedFolder);
    }

    /// <summary>
    ///     Test that a path policy judges a path by its normalized spelling and does not follow a folder link, so a link
    ///     inside the project that leads outside it is permitted by the policy. The application's own tools must
    ///     therefore refuse links themselves; this test pins the behavior so that a change of the library is noticed.
    /// </summary>
    [Fact]
    public void AgentKit_PathPolicy_ResolveRead_LinkInsideTheGrant_IsJudgedBySpellingAndNotFollowed()
    {
        // Arrange: a granted project folder that holds a link to a folder beside it
        using var folder = new TestFolder();
        var project = Path.Combine(folder.Path, "project");
        var secret = Path.Combine(folder.Path, "secret");
        Directory.CreateDirectory(project);
        Directory.CreateDirectory(secret);
        File.WriteAllText(Path.Combine(secret, "key.txt"), "secret");
        var link = Path.Combine(project, "link");
        CreateFolderLink(link, secret);
        var policy = new PathPolicy(project, [PathRule.ReadWrite(project)]);

        // Act: resolve a file through the link for reading and for writing
        var read = policy.TryResolveRead(Path.Combine("link", "key.txt"), out var readPath, out _);
        var write = policy.TryResolveWrite(Path.Combine("link", "new.txt"), out var writePath, out _);

        // Assert: both are permitted, and the resolved paths keep the spelling inside the project, which shows that
        // the link was not followed and that a link check belongs to the application's tools
        Assert.True(read);
        Assert.Equal(Path.Combine(link, "key.txt"), readPath);
        Assert.True(write);
        Assert.Equal(Path.Combine(link, "new.txt"), writePath);
        Assert.True(new FileInfo(link).Attributes.HasFlag(FileAttributes.ReparsePoint));
    }

    /// <summary>
    ///     Test that a tool pack builder refuses a second pack with the same family prefix and publishes the tools of
    ///     the packs added, in order.
    /// </summary>
    [Fact]
    public void AgentKit_ToolPackBuilder_Add_RefusesDuplicatePrefixAndBuildsInOrder()
    {
        // Arrange: a policy and two packs with different prefixes
        using var folder = new TestFolder();
        var policy = new PathPolicy(folder.Path, [PathRule.ReadWrite(folder.Path)]);
        var builder = new ToolPackBuilder(policy).Add(new DemoPack("alpha")).Add(new DemoPack("beta"));

        // Act: add a third pack with a prefix that is taken, and build
        var duplicate = Assert.Throws<ArgumentException>(() => builder.Add(new DemoPack("alpha")));
        var tools = builder.Build();

        // Assert: the duplicate is refused, and the tools come in the order of the packs, each with its prefix
        Assert.Contains("prefix", duplicate.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(["alpha_ping", "beta_ping"], tools.Select(tool => tool.Name).ToArray());
    }

    /// <summary>
    ///     Test that the guarded tool factory refuses tool names that break the naming convention: a bare reserved
    ///     name, a name with a capital letter or a space, and a name that is too long.
    /// </summary>
    [Fact]
    public void AgentKit_GuardedToolFactory_Create_RefusesInvalidToolNames()
    {
        // Arrange: a delegate to wrap and a list of names the convention does not allow
        Func<object> method = () => ToolResult.Text("ok");
        string[] invalid = ["read", "Project_Write", "project write", "p" + new string('x', ToolName.MaxLength)];

        // Act / Assert: each is refused with an argument exception, and a good name is accepted
        foreach (var name in invalid)
        {
            Assert.Throws<ArgumentException>(() => GuardedToolFactory.Create(method, name, "A tool."));
        }

        Assert.Equal("project_list", GuardedToolFactory.Create(method, "project_list", "A tool.").Name);
    }

    /// <summary>
    ///     Test that bounding a tool by tool limits refuses a binary result over the ceiling with a message that
    ///     names the reason, and leaves a result within the ceiling unchanged.
    /// </summary>
    [Fact]
    public async Task AgentKit_GuardedToolFactory_AsGuarded_BinaryOverCeiling_IsRefusedAndWithinCeilingPasses()
    {
        // Arrange: a tool whose result size is chosen by its caller, bounded to 1 KiB
        var tool = GuardedToolFactory
            .Create((int size) => ToolResult.Binary(new byte[size], "application/octet-stream"), "demo_blob", "A blob.")
            .AsGuarded(new ToolLimits(maxBinaryBytes: 1024));

        // Act: ask for a result within the ceiling and one over it
        var small = await tool.InvokeAsync(new AIFunctionArguments { ["size"] = 100 }, TestContext.Current.CancellationToken);
        var large = await tool.InvokeAsync(new AIFunctionArguments { ["size"] = 5000 }, TestContext.Current.CancellationToken);

        // Assert: the small one is delivered as data, and the large one is a refusal that names its reason
        Assert.IsType<DataContent>(small);
        Assert.Contains("ResourceTooLarge", large?.ToString(), StringComparison.Ordinal);
    }

    /// <summary>
    ///     Test that an image result carries its caption first and the picture second with the media type given, and
    ///     that bytes that are not an image are refused.
    /// </summary>
    [Fact]
    public void AgentKit_ToolResult_Image_ReturnsCaptionThenPictureAndRefusesOtherMediaTypes()
    {
        // Arrange: a few picture bytes
        byte[] png = [0x89, 0x50, 0x4E, 0x47];

        // Act: build an image result with a caption, one without, and one with a media type that is not an image
        var withCaption = Assert.IsType<List<AIContent>>(ToolResult.Image(png, "image/png", "A picture"));
        var withoutCaption = Assert.IsType<DataContent>(ToolResult.Image(png, "image/png"));
        var notAnImage = Assert.Throws<ArgumentException>(() => ToolResult.Image(png, "application/pdf"));

        // Assert: the caption comes first, the picture second, and the refusal names the media type rule
        Assert.Equal("A picture", Assert.IsType<TextContent>(withCaption[0]).Text);
        var picture = Assert.IsType<DataContent>(withCaption[1]);
        Assert.Equal("image/png", picture.MediaType);
        Assert.Equal(png, picture.Data.ToArray());
        Assert.Equal("image/png", withoutCaption.MediaType);
        Assert.Contains("image", notAnImage.Message, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    ///     Test that an agent built on a chat client runs a whole tool-calling turn: it streams the tool call, then
    ///     the tool result, then the text of the answer, and the tool receives the argument the model gave.
    /// </summary>
    [Fact]
    public async Task AgentKit_ChatClientAgentFactory_ScriptedTurn_StreamsCallResultAndText()
    {
        // Arrange: a tool that echoes its argument and a model that calls it once and then answers
        var received = new List<string>();
        var echo = GuardedToolFactory.Create(
            (string text) =>
            {
                received.Add(text);
                return ToolResult.Text("echo " + text);
            },
            "demo_echo",
            "Echoes the text.");
        var client = new ScriptedChatClient(
            [new ChatResponseUpdate(ChatRole.Assistant, [new FunctionCallContent("call-1", "demo_echo", new Dictionary<string, object?> { ["text"] = "hello" })])],
            [new ChatResponseUpdate(ChatRole.Assistant, "Done"), new ChatResponseUpdate(ChatRole.Assistant, " now.")]);
        var agent = ChatClientAgentFactory.Create(client, [echo], "You are a test.", "ots-agent");
        var session = await agent.CreateSessionAsync(TestContext.Current.CancellationToken);

        // Act: run one streamed turn and note the kinds of content in the order they arrive
        var kinds = new List<string>();
        var text = new System.Text.StringBuilder();
        await foreach (var update in agent.RunStreamingAsync("Say hello.", session, cancellationToken: TestContext.Current.CancellationToken))
        {
            foreach (var content in update.Contents)
            {
                switch (content)
                {
                    case FunctionCallContent call:
                        kinds.Add("call:" + call.Name);
                        break;
                    case FunctionResultContent:
                        kinds.Add("result");
                        break;
                    case TextContent chunk:
                        kinds.Add("text");
                        text.Append(chunk.Text);
                        break;
                }
            }
        }

        // Assert: call, result, and text came in that order, the tool got the argument, and the model was asked twice
        Assert.Equal("call:demo_echo", kinds[0]);
        Assert.Equal("result", kinds[1]);
        Assert.All(kinds.Skip(2), kind => Assert.Equal("text", kind));
        Assert.Equal("Done now.", text.ToString());
        Assert.Equal(["hello"], received);
        Assert.Equal(2, client.Requests.Count);
    }

    /// <summary>
    ///     Test that a picture that a tool returns reaches the model as picture content of a following user message,
    ///     which is what lets a model that cannot take pictures in a tool result see its own drawing.
    /// </summary>
    [Fact]
    public async Task AgentKit_ChatClientAgentFactory_ToolImageResult_IsPromotedToAUserMessage()
    {
        // Arrange: a tool that returns a picture and a model that calls it and then answers
        byte[] png = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];
        var render = GuardedToolFactory.Create(
            () => ToolResult.Image(png, "image/png", "The drawing."),
            "demo_render",
            "Draws a picture.");
        var client = new ScriptedChatClient(
            [new ChatResponseUpdate(ChatRole.Assistant, [new FunctionCallContent("call-1", "demo_render", new Dictionary<string, object?>())])],
            [new ChatResponseUpdate(ChatRole.Assistant, "Looks fine.")]);
        var agent = ChatClientAgentFactory.Create(client, [render], "You are a test.", "ots-agent");
        var session = await agent.CreateSessionAsync(TestContext.Current.CancellationToken);

        // Act: run one turn to the end
        await foreach (var _ in agent.RunStreamingAsync("Draw.", session, cancellationToken: TestContext.Current.CancellationToken))
        {
            // The updates are not needed; the request the model got after the tool ran is.
        }

        // Assert: the second request holds the picture as content of a user message
        var second = client.Requests[1];
        var promoted = second
            .Where(message => message.Role == ChatRole.User)
            .SelectMany(message => message.Contents)
            .OfType<DataContent>()
            .Single();
        Assert.Equal("image/png", promoted.MediaType);
        Assert.Equal(png, promoted.Data.ToArray());
    }

    /// <summary>
    ///     Test that the Ollama context sizing client adds the context length to the options of every request, does
    ///     not change the options its caller passed, and leaves a context length that the caller named alone.
    /// </summary>
    [Fact]
    public async Task AgentKit_OllamaContextSizingChatClient_GetResponse_AddsContextLengthWithoutChangingCallersOptions()
    {
        // Arrange: a sizing client over a scripted client, and the caller's own options
        var inner = new ScriptedChatClient(
            [new ChatResponseUpdate(ChatRole.Assistant, "one")],
            [new ChatResponseUpdate(ChatRole.Assistant, "two")],
            [new ChatResponseUpdate(ChatRole.Assistant, "three")]);
        using var sizing = new OllamaContextSizingChatClient(inner, 32768);
        var callerOptions = new ChatOptions { Temperature = 0.2f };
        var namedOptions = new ChatOptions { AdditionalProperties = new AdditionalPropertiesDictionary { ["num_ctx"] = 4096 } };
        List<ChatMessage> messages = [new ChatMessage(ChatRole.User, "hi")];

        // Act: send a request without options, one with options of the caller, and one that names the length itself
        await sizing.GetResponseAsync(messages, null, TestContext.Current.CancellationToken);
        await sizing.GetResponseAsync(messages, callerOptions, TestContext.Current.CancellationToken);
        await sizing.GetResponseAsync(messages, namedOptions, TestContext.Current.CancellationToken);

        // Assert: the first two carry the length, the caller's options are untouched, and the named length wins
        Assert.Equal(32768, inner.Options[0]!.AdditionalProperties!["num_ctx"]);
        Assert.Equal(32768, inner.Options[1]!.AdditionalProperties!["num_ctx"]);
        Assert.Equal(0.2f, inner.Options[1]!.Temperature);
        Assert.Null(callerOptions.AdditionalProperties);
        Assert.Equal(4096, inner.Options[2]!.AdditionalProperties!["num_ctx"]);
        Assert.Throws<ArgumentOutOfRangeException>(() => new OllamaContextSizingChatClient(inner, 0));
    }

    /// <summary>
    ///     Test that starting the Copilot runtime in this build, which carries no Copilot runtime, fails with an
    ///     error that says why, starts no runtime process, and leaves only the ownership marker of AgentKit in the
    ///     data directory.
    /// </summary>
    [Fact]
    public async Task AgentKit_CopilotRuntime_StartAsync_WithoutBundledRuntime_FailsAndStartsNothing()
    {
        // Arrange: a data directory that does not exist yet, and the runtime processes that run before the attempt
        using var folder = new TestFolder();
        var dataDirectory = Path.Combine(folder.Path, "copilot");
        var before = RuntimeProcessIds();
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(60));

        // Act: try to start the runtime
        var failure = await Assert.ThrowsAsync<InvalidOperationException>(
            () => CopilotRuntime.StartAsync(new CopilotRuntimeOptions(dataDirectory), timeout.Token));

        // Assert: the error names the missing runtime wrapper, no new runtime process exists, and the data directory
        // holds nothing but the marker
        Assert.Contains("copilot-runtime", failure.ToString(), StringComparison.OrdinalIgnoreCase);
        Assert.Empty(RuntimeProcessIds().Except(before));
        Assert.True(Directory.GetFiles(dataDirectory, "*", SearchOption.AllDirectories).Length <= 1);
    }

    /// <summary>
    ///     Test that the Copilot runtime of AgentKit ignores the <c>COPILOT_CLI_PATH</c> variable of the host process,
    ///     so that an installed Copilot CLI cannot be named that way: the start still fails for the missing runtime
    ///     wrapper, and the file that the variable names is never run.
    /// </summary>
    [Fact]
    public async Task AgentKit_CopilotRuntime_StartAsync_CliPathVariable_IsIgnored()
    {
        // Arrange: a file that the variable names, which would leave a marker if it were ever run
        using var folder = new TestFolder();
        var marker = Path.Combine(folder.Path, "was-run.txt");
        var fakeCli = Path.Combine(folder.Path, OperatingSystem.IsWindows() ? "copilot.cmd" : "copilot");
        await File.WriteAllTextAsync(
            fakeCli,
            OperatingSystem.IsWindows() ? $"@echo ran> \"{marker}\"" : $"#!/bin/sh\necho ran > \"{marker}\"",
            TestContext.Current.CancellationToken);
        var previous = Environment.GetEnvironmentVariable("COPILOT_CLI_PATH");
        Environment.SetEnvironmentVariable("COPILOT_CLI_PATH", fakeCli);
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(60));

        try
        {
            // Act: try to start the runtime
            var failure = await Assert.ThrowsAsync<InvalidOperationException>(
                () => CopilotRuntime.StartAsync(new CopilotRuntimeOptions(Path.Combine(folder.Path, "copilot")), timeout.Token));

            // Assert: the error is still the missing wrapper, and the named file was not run
            Assert.Contains("copilot-runtime", failure.ToString(), StringComparison.OrdinalIgnoreCase);
            Assert.False(File.Exists(marker));
        }
        finally
        {
            Environment.SetEnvironmentVariable("COPILOT_CLI_PATH", previous);
        }
    }

    /// <summary>
    ///     Test that the build output of the test project, which references the same packages as the application,
    ///     holds no Copilot runtime file: no wrapper, no CLI, no runtime library, and no <c>runtime.node</c>.
    /// </summary>
    [Fact]
    public void AgentKit_BuildOutput_HasNoBundledCopilotRuntime()
    {
        // Arrange: the output folder, and the place where the Copilot SDK would put its runtime for Windows
        var output = AppContext.BaseDirectory;
        var wrapper = Path.Combine(output, "runtimes", "win-x64", "native", "copilot-runtime.exe");

        // Act: look for the wrapper and for any file that the Copilot runtime consists of, anywhere in the output
        var runtimeFiles = Directory.EnumerateFiles(output, "*", SearchOption.AllDirectories)
            .Where(file => IsCopilotRuntimeFile(Path.GetFileName(file)))
            .Select(file => Path.GetRelativePath(output, file))
            .ToArray();

        // Assert: none exists
        Assert.False(File.Exists(wrapper));
        Assert.Empty(runtimeFiles);
    }

    /// <summary>
    ///     Gets whether a file name is one of the files that the Copilot runtime consists of.
    /// </summary>
    /// <param name="name">The file name without a folder.</param>
    /// <returns><see langword="true"/> when the file belongs to a bundled Copilot runtime.</returns>
    internal static bool IsCopilotRuntimeFile(string name)
    {
        return name.StartsWith("copilot", StringComparison.OrdinalIgnoreCase)
            || name.StartsWith("libcopilot", StringComparison.OrdinalIgnoreCase)
            || name.Equals("runtime.node", StringComparison.OrdinalIgnoreCase)
            || name.Equals(".copilot-explicit-cli", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    ///     Gets the ids of the processes that are the Copilot runtime wrapper.
    /// </summary>
    /// <returns>The process ids.</returns>
    private static int[] RuntimeProcessIds()
    {
        var processes = Process.GetProcessesByName("copilot-runtime");
        try
        {
            return [.. processes.Select(process => process.Id)];
        }
        finally
        {
            foreach (var process in processes)
            {
                process.Dispose();
            }
        }
    }

    /// <summary>
    ///     Creates a link to a folder: a junction on Windows, which needs no privilege, and a symbolic link elsewhere.
    /// </summary>
    /// <param name="link">The path of the link to create.</param>
    /// <param name="target">The folder that the link leads to.</param>
    private static void CreateFolderLink(string link, string target)
    {
        if (!OperatingSystem.IsWindows())
        {
            Directory.CreateSymbolicLink(link, target);
            return;
        }

        var start = new ProcessStartInfo("cmd.exe")
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
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

    /// <summary>
    ///     A pack with one tool, used to check how packs are composed.
    /// </summary>
    /// <param name="prefix">The family prefix of the pack.</param>
    private sealed class DemoPack(string prefix) : IToolPack
    {
        /// <summary>
        ///     Gets the family prefix.
        /// </summary>
        public string FamilyPrefix => prefix;

        /// <summary>
        ///     Gets the capabilities the pack needs; none.
        /// </summary>
        public HostCapabilities RequiredCapabilities => HostCapabilities.None;

        /// <summary>
        ///     Creates the one tool of the pack.
        /// </summary>
        /// <param name="policy">The path policy, which the tool does not use.</param>
        /// <returns>The tools.</returns>
        public IEnumerable<AIFunction> CreateTools(PathPolicy policy)
        {
            _ = policy;
            return [GuardedToolFactory.Create(() => ToolResult.Text("pong"), prefix + "_ping", "Answers a ping.")];
        }
    }
}
