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

using System.Runtime.CompilerServices;
using DemaConsulting.CanvasPilot.AgentSubsystem;
using DemaConsulting.CanvasPilot.KnowledgeSubsystem;
using DemaConsulting.CanvasPilot.LoggingSubsystem;
using DemaConsulting.CanvasPilot.PreviewSubsystem;
using DemaConsulting.CanvasPilot.ProjectSubsystem;
using DemaConsulting.CanvasPilot.SettingsSubsystem;
using Microsoft.Extensions.AI;
using NSubstitute;

namespace DemaConsulting.CanvasPilot.Tests;

/// <summary>
///     A chat client whose answer to each request comes from a function, so that a test can make a model that answers
///     with text, waits until it is released, or fails, and records every request.
/// </summary>
internal sealed class ProgrammableChatClient : IChatClient
{
    /// <summary>The function that makes the updates of an answer from the number of the request and the token.</summary>
    private readonly Func<int, CancellationToken, IAsyncEnumerable<ChatResponseUpdate>> _script;

    /// <summary>
    ///     Initializes a new instance of the <see cref="ProgrammableChatClient" /> class.
    /// </summary>
    /// <param name="script">Makes the answer to the request with the given number, which starts at 1.</param>
    internal ProgrammableChatClient(Func<int, CancellationToken, IAsyncEnumerable<ChatResponseUpdate>> script)
    {
        _script = script;
    }

    /// <summary>
    ///     Gets a copy of the messages of each request, in order.
    /// </summary>
    internal List<List<ChatMessage>> Requests { get; } = [];

    /// <summary>
    ///     Makes an answer of one text update.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <returns>The updates.</returns>
    internal static async IAsyncEnumerable<ChatResponseUpdate> Say(string text)
    {
        await Task.Yield();
        yield return new ChatResponseUpdate(ChatRole.Assistant, text);
    }

    /// <summary>
    ///     Makes an answer that starts, tells that it started, and then waits until it is released or canceled.
    /// </summary>
    /// <param name="entered">Completed when the answer started.</param>
    /// <param name="release">Completes when the answer may go on.</param>
    /// <param name="text">The text to give after the release.</param>
    /// <param name="cancellationToken">The token of the request.</param>
    /// <returns>The updates.</returns>
    internal static async IAsyncEnumerable<ChatResponseUpdate> Hang(
        TaskCompletionSource entered,
        Task release,
        string text,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        yield return new ChatResponseUpdate(ChatRole.Assistant, "working ");
        entered.TrySetResult();
        await release.WaitAsync(cancellationToken);
        yield return new ChatResponseUpdate(ChatRole.Assistant, text);
    }

    /// <summary>
    ///     Makes an answer that waits until it is released or canceled before it says anything.
    /// </summary>
    /// <param name="entered">Completed when the answer started.</param>
    /// <param name="release">Completes when the answer may go on.</param>
    /// <param name="text">The text to give after the release.</param>
    /// <param name="cancellationToken">The token of the request.</param>
    /// <returns>The updates.</returns>
    internal static async IAsyncEnumerable<ChatResponseUpdate> HangBeforeFirstUpdate(
        TaskCompletionSource entered,
        Task release,
        string text,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        entered.TrySetResult();
        await release.WaitAsync(cancellationToken);
        yield return new ChatResponseUpdate(ChatRole.Assistant, text);
    }

    /// <summary>
    ///     Makes an answer that says something, tells that it started, and then waits until it is released without ever
    ///     looking at the token, as a provider that cannot be canceled does.
    /// </summary>
    /// <param name="entered">Completed when the answer started.</param>
    /// <param name="release">Completes when the answer may go on.</param>
    /// <returns>The updates.</returns>
    internal static async IAsyncEnumerable<ChatResponseUpdate> HangIgnoringCancellation(TaskCompletionSource entered, Task release)
    {
        yield return new ChatResponseUpdate(ChatRole.Assistant, "working ");
        entered.TrySetResult();
        await release;
        yield return new ChatResponseUpdate(ChatRole.Assistant, "late");
    }

    /// <summary>
    ///     Makes an answer that fails before it says anything, as a provider does that cannot use a session.
    /// </summary>
    /// <param name="exception">The exception.</param>
    /// <returns>The updates.</returns>
    internal static async IAsyncEnumerable<ChatResponseUpdate> Fail(Exception exception)
    {
        await Task.Yield();
        if (exception is not null)
        {
            throw exception;
        }

        yield return new ChatResponseUpdate(ChatRole.Assistant, string.Empty);
    }

    /// <summary>
    ///     Makes an answer that asks to call a tool, as a model does, so that the next request of the same turn carries the
    ///     result of the call.
    /// </summary>
    /// <param name="callId">The id of the call.</param>
    /// <param name="tool">The name of the tool.</param>
    /// <returns>The updates.</returns>
    internal static async IAsyncEnumerable<ChatResponseUpdate> CallTool(string callId, string tool)
    {
        await Task.Yield();
        yield return new ChatResponseUpdate(ChatRole.Assistant, [new FunctionCallContent(callId, tool, new Dictionary<string, object?>())]);
    }

    /// <inheritdoc />
    public async Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        var updates = new List<ChatResponseUpdate>();
        await foreach (var update in GetStreamingResponseAsync(messages, options, cancellationToken))
        {
            updates.Add(update);
        }

        return updates.ToChatResponse();
    }

    /// <inheritdoc />
    public IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        Requests.Add([.. messages]);
        return _script(Requests.Count, cancellationToken);
    }

    /// <inheritdoc />
    public object? GetService(Type serviceType, object? serviceKey = null)
    {
        return null;
    }

    /// <inheritdoc />
    public void Dispose()
    {
    }
}

/// <summary>
///     Everything that an <see cref="AgentService" /> needs, on temporary folders: an open project, settings for Ollama,
///     the real tools and instructions, a factory that makes agents on a scripted chat client, and a log file. The test
///     changes the project and the settings through the rig, which raises the events that the real services raise.
/// </summary>
internal sealed class AgentServiceRig : IDisposable
{
    /// <summary>The settings that the settings service gives now.</summary>
    private AppSettings _app;

    /// <summary>The project that the project service gives now.</summary>
    private ProjectInfo? _current;

    /// <summary>
    ///     Initializes a new instance of the <see cref="AgentServiceRig" /> class.
    /// </summary>
    /// <param name="client">The chat client that the agents talk to.</param>
    /// <param name="fakeProbe">The Ollama probe of the readiness check; <see langword="null" /> for an empty one.</param>
    /// <param name="nestLocalDataRoot">
    ///     <see langword="true" /> to put the local data root inside the project, which the service must refuse.
    /// </param>
    /// <param name="factory">The factory that the service uses; the scripted factory of the rig when <see langword="null" />.</param>
    /// <param name="settings">The AI provider settings; Ollama on the default address when <see langword="null" />.</param>
    /// <param name="clock">The clock of the service; the system's when <see langword="null" />.</param>
    internal AgentServiceRig(
        IChatClient client,
        FakeOllamaProbe? fakeProbe = null,
        bool nestLocalDataRoot = false,
        IAgentFactory? factory = null,
        AiProviderSettings? settings = null,
        TimeProvider? clock = null)
    {
        Root = Directory.CreateTempSubdirectory("canvaspilot-agent-service-").FullName;
        ProjectFolder = Directory.CreateDirectory(Path.Combine(Root, "project")).FullName;
        File.WriteAllText(Path.Combine(ProjectFolder, "Brochure.canvaspilot"), "{}");
        LocalDataRoot = nestLocalDataRoot ? Path.Combine(ProjectFolder, "appdata") : Path.Combine(Root, "local-data");
        LogFolder = Path.Combine(Root, "logs");
        Logger = new RollingFileLogger(LogFolder);
        _app = new AppSettings
        {
            AiProvider = settings ?? new AiProviderSettings
            {
                Provider = AiProviderKind.Ollama,
                ModelName = "gemma3:12b",
                OllamaEndpoint = "http://localhost:11434",
                ContextWindowTokens = 32768,
            },
        };

        Client = client;
        Factory = new ScriptedAgentFactory(client);
        Probe = fakeProbe ?? new FakeOllamaProbe();
        Projects = Substitute.For<IProjectService>();
        Settings = Substitute.For<ISettingsService>();
        Projects.Current.Returns(_ => _current);
        Settings.Current.Returns(_ => _app);
        _current = MakeProject(ProjectFolder, "Brochure");

        Tools = new ProjectTools(new ImageRenderService(), logger: Logger);
        var registry = new AgentToolRegistry(KnowledgeTools.CreateAll(KnowledgeCatalog.Default), [], Tools.CreatePacks);
        var instructions = new AgentInstructions(new KnowledgePromptBuilder(KnowledgeCatalog.Default));
        Service = new AgentService(
            Projects,
            Settings,
            registry,
            instructions,
            factory ?? Factory,
            new AgentReadiness(ollama: Probe),
            Tools,
            null,
            Logger,
            LocalDataRoot,
            clock: clock);
    }

    /// <summary>Gets the temporary folder that holds everything of the rig.</summary>
    internal string Root { get; }

    /// <summary>Gets the folder of the first project.</summary>
    internal string ProjectFolder { get; }

    /// <summary>Gets the local data root that the service is given.</summary>
    internal string LocalDataRoot { get; }

    /// <summary>Gets the folder of the log files.</summary>
    internal string LogFolder { get; }

    /// <summary>Gets the logger of the service.</summary>
    internal RollingFileLogger Logger { get; }

    /// <summary>Gets the chat client of the agents.</summary>
    internal IChatClient Client { get; }

    /// <summary>Gets the factory that makes the agents.</summary>
    internal ScriptedAgentFactory Factory { get; }

    /// <summary>Gets the Ollama probe of the readiness check.</summary>
    internal FakeOllamaProbe Probe { get; }

    /// <summary>Gets the stand-in for the project service.</summary>
    internal IProjectService Projects { get; }

    /// <summary>Gets the stand-in for the settings service.</summary>
    internal ISettingsService Settings { get; }

    /// <summary>Gets the project tools whose packs the registry offers.</summary>
    internal ProjectTools Tools { get; }

    /// <summary>Gets the service under test.</summary>
    internal AgentService Service { get; }

    /// <summary>
    ///     Makes the information of a project in a folder.
    /// </summary>
    /// <param name="folder">The folder of the project.</param>
    /// <param name="name">The name of the project.</param>
    /// <returns>The project.</returns>
    internal static ProjectInfo MakeProject(string folder, string name)
    {
        return new ProjectInfo(name, folder, Path.Combine(folder, name + ProjectDocument.FileExtension), new ProjectDocument { Name = name });
    }

    /// <summary>
    ///     Creates a second project folder beside the first.
    /// </summary>
    /// <param name="name">The name of the project.</param>
    /// <returns>The project; it is not open yet.</returns>
    internal ProjectInfo CreateOtherProject(string name)
    {
        var folder = Directory.CreateDirectory(Path.Combine(Root, name)).FullName;
        return MakeProject(folder, name);
    }

    /// <summary>
    ///     Opens another project, or closes the project with <see langword="null" />, and raises the event of the project
    ///     service.
    /// </summary>
    /// <param name="project">The project that is open now.</param>
    internal void SwitchProject(ProjectInfo? project)
    {
        _current = project;
        Projects.CurrentChanged += Raise.Event();
    }

    /// <summary>
    ///     Changes the AI provider settings and raises the event of the settings service.
    /// </summary>
    /// <param name="change">Makes the new settings from the old ones.</param>
    internal void ChangeAiSettings(Func<AiProviderSettings, AiProviderSettings> change)
    {
        _app = _app with { AiProvider = change(_app.AiProvider) };
        Settings.Changed += Raise.EventWith(Settings, new SettingsChangedEventArgs(_app));
    }

    /// <summary>
    ///     Reads the text of every log file of the rig.
    /// </summary>
    /// <returns>The text; empty when nothing was logged.</returns>
    internal string ReadLog()
    {
        RollingFileLogger.Flush();
        if (!Directory.Exists(LogFolder))
        {
            return string.Empty;
        }

        // The log may be written by a background release at the same moment, so it is opened for sharing
        return string.Concat(Directory.EnumerateFiles(LogFolder, "*.log*").Select(path =>
        {
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            using var reader = new StreamReader(stream);
            return reader.ReadToEnd();
        }));
    }

    /// <summary>
    ///     Lists every file and folder below a folder, relative and sorted, so that two lists can be compared.
    /// </summary>
    /// <param name="folder">The folder.</param>
    /// <returns>The entries.</returns>
    internal static string[] Snapshot(string folder)
    {
        return Directory.EnumerateFileSystemEntries(folder, "*", SearchOption.AllDirectories)
            .Select(entry => Path.GetRelativePath(folder, entry))
            .Order(StringComparer.Ordinal)
            .ToArray();
    }

    /// <summary>
    ///     Runs a turn to its end and collects the events.
    /// </summary>
    /// <param name="events">The stream of the turn.</param>
    /// <param name="cancellationToken">A token that ends the wait.</param>
    /// <returns>The events.</returns>
    internal static async Task<List<AgentEvent>> CollectAsync(IAsyncEnumerable<AgentEvent> events, CancellationToken cancellationToken = default)
    {
        var all = new List<AgentEvent>();
        await foreach (var item in events.WithCancellation(cancellationToken))
        {
            all.Add(item);
        }

        return all;
    }

    /// <summary>
    ///     Waits until a condition holds, which the background release of a conversation needs.
    /// </summary>
    /// <param name="condition">The condition.</param>
    /// <param name="cancellationToken">A token that ends the wait.</param>
    /// <returns>A task that completes when the condition holds; it fails after ten seconds.</returns>
    internal static async Task WaitUntilAsync(Func<bool> condition, CancellationToken cancellationToken = default)
    {
        var limit = DateTime.UtcNow.AddSeconds(10);
        while (!condition())
        {
            if (DateTime.UtcNow > limit)
            {
                throw new TimeoutException("The condition did not hold within ten seconds.");
            }

            await Task.Delay(10, cancellationToken);
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        Service.Dispose();
        Directory.Delete(Root, true);
    }
}
