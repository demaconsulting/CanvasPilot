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

using System.Collections.Concurrent;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using DemaConsulting.CanvasPilot.LoggingSubsystem;
using DemaConsulting.CanvasPilot.ProjectSubsystem;
using DemaConsulting.CanvasPilot.SettingsSubsystem;
using DemaConsulting.CanvasPilot.StyleSubsystem;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace DemaConsulting.CanvasPilot.AgentSubsystem;

/// <summary>
///     The AI copilot as the chat uses it: it keeps one conversation with the AI provider for the open project, runs one
///     turn at a time on it, and streams what happens as provider-independent events.
/// </summary>
/// <remarks>
///     <para>
///         <b>Lazy and per project.</b> Nothing is made until the first turn. The first turn builds the tools, the
///         instructions, and the agent for the open project through the factory (which starts the Copilot runtime, once for
///         the whole application, only if the provider is GitHub Copilot) and one provider session for it. The conversation
///         is kept for the next turns and is dropped, and made again by the next turn, when the open project changes or
///         closes, when a setting that decides the agent changes (the provider, the model, the Ollama address, the context
///         window, or the path of the Copilot CLI), or when <see cref="ResetConversation" /> is called. Dropping a
///         conversation deletes what the provider kept for it (the Copilot session) through the factory, and never blocks
///         the caller.
///     </para>
///     <para>
///         <b>One turn at a time.</b> A turn begins when its stream is first read. A request made while a turn runs gets one
///         <see cref="AgentEvent.Failed" /> of kind <see cref="AgentFailureKind.Busy" /> and nothing else. The token of the
///         turn is linked to the token of the caller, to the end of the service, and to a reset or a change of the project,
///         so Stop, closing the project, and closing the application all end the turn the same way, with no failure.
///     </para>
///     <para>
///         <b>Restored history.</b> The text of the earlier turns in the request becomes one block of at most
///         <see cref="RestoredHistoryDigest.MaxCharacters" /> characters at the start of the first message of a new provider
///         session, once. It lives in memory only; it is never written to a file or to the log.
///     </para>
///     <para>
///         <b>Data stays out of the project.</b> The Copilot runtime gets an explicit data folder below the local data root
///         of the user (<see cref="GetCopilotDataDirectory" />), never its default and never a place in the project. A turn
///         is refused with <see cref="AgentFailureKind.DataInsideProject" /> when that folder, or the folder of the chat
///         history, lies inside the project folder.
///     </para>
///     <para>
///         <b>Logging.</b> The log gets the metadata of the lifecycle (provider, model, tier, counts, outcome, duration, the
///         type of an exception) and never the words of a message, a tool's arguments or result, or a file's content; the
///         runtime of Copilot logs through <see cref="RuntimeLogAdapter" />, which drops message text too.
///     </para>
///     <para>
///         The service is safe for concurrent use. The events of the project tools and of the settings may arrive on any
///         thread. The service owns the factory it is given and disposes it, which stops the Copilot runtime.
///     </para>
/// </remarks>
public sealed class AgentService : IAgentService
{
    /// <summary>
    ///     The name of the folder of the application below the local data root.
    /// </summary>
    public const string ApplicationFolderName = "CanvasPilot";

    /// <summary>
    ///     The name of the folder of the Copilot runtime data below the folder of the application.
    /// </summary>
    public const string CopilotFolderName = "copilot";

    /// <summary>
    ///     The name of the folder of the chat history below the folder of the application.
    /// </summary>
    public const string HistoryFolderName = "history";

    /// <summary>
    ///     The sentence for a request made while a turn runs.
    /// </summary>
    public const string BusyMessage = "The copilot is still working on the previous message. Press Stop to cancel it, then send again.";

    /// <summary>
    ///     The sentence for a request made when no project is open.
    /// </summary>
    public const string NoProjectMessage = "Open or create a project first; the copilot works on the files of a project.";

    /// <summary>
    ///     The sentence for a request made while the application closes.
    /// </summary>
    public const string ClosingMessage = "CanvasPilot is closing, so the copilot cannot answer.";

    /// <summary>
    ///     The sentence for a data folder of the application that lies inside the project; <c>{0}</c> is that folder.
    /// </summary>
    public const string DataInsideProjectMessage =
        "CanvasPilot keeps its own data (chat history and Copilot files) in '{0}', which lies inside this project folder. "
        + "Open a project outside of that folder, so that nothing of the copilot is written into your project.";

    /// <summary>
    ///     The most files of the project that are collected for the instructions.
    /// </summary>
    internal const int MaxListedFiles = 200;

    /// <summary>
    ///     The most entries that the search for the files of the project looks at.
    /// </summary>
    private const int MaxScannedFiles = 5_000;

    /// <summary>
    ///     The longest time that releasing a conversation waits for its running turn to end.
    /// </summary>
    private static readonly TimeSpan ReleaseWait = TimeSpan.FromSeconds(10);

    /// <summary>
    ///     The longest time that disposing the service waits for the conversations to be released and the factory to stop.
    /// </summary>
    private static readonly TimeSpan DisposeWait = TimeSpan.FromSeconds(10);

    /// <summary>
    ///     The set of optional tool packs of the user, which is empty in this version.
    /// </summary>
    private static readonly IReadOnlySet<string> NoOptionalPacks = new HashSet<string>();

    /// <summary>
    ///     Guards the conversation, the running turn, the clean-up list, and the disposed flag.
    /// </summary>
    private readonly Lock _sync = new();

    /// <summary>
    ///     Gives the open project.
    /// </summary>
    private readonly IProjectService _projects;

    /// <summary>
    ///     Gives the settings and tells when they change.
    /// </summary>
    private readonly ISettingsService _settings;

    /// <summary>
    ///     Builds the tools of a project.
    /// </summary>
    private readonly AgentToolRegistry _registry;

    /// <summary>
    ///     Writes the instructions.
    /// </summary>
    private readonly AgentInstructions _instructions;

    /// <summary>
    ///     Makes the agents; owned by the service.
    /// </summary>
    private readonly IAgentFactory _factory;

    /// <summary>
    ///     Checks the provider.
    /// </summary>
    private readonly AgentReadiness _readiness;

    /// <summary>
    ///     The project tools whose <see cref="ProjectTools.FileWritten" /> event the service passes on, or
    ///     <see langword="null" /> for none.
    /// </summary>
    private readonly ProjectTools? _projectTools;

    /// <summary>
    ///     Gives the style kit of the open project, or <see langword="null" /> for none.
    /// </summary>
    private readonly IActiveStyle? _activeStyle;

    /// <summary>
    ///     Receives the metadata lines, or <see langword="null" /> for none.
    /// </summary>
    private readonly RollingFileLogger? _logger;

    /// <summary>
    ///     The logger that the Copilot runtime writes to, or <see langword="null" /> without a log.
    /// </summary>
    private readonly RuntimeLogAdapter? _runtimeLogger;

    /// <summary>
    ///     The folder where the Copilot runtime keeps its data.
    /// </summary>
    private readonly string _copilotDataDirectory;

    /// <summary>
    ///     The folder where the chat history is kept; only checked here.
    /// </summary>
    private readonly string _historyDirectory;

    /// <summary>
    ///     Cancels every turn when the service ends.
    /// </summary>
    private readonly CancellationTokenSource _lifetime = new();

    /// <summary>
    ///     The conversations that are being released.
    /// </summary>
    private readonly List<Task> _cleanups = [];

    /// <summary>
    ///     The conversation of the open project, or <see langword="null" /> when none was made.
    /// </summary>
    private Conversation? _conversation;

    /// <summary>
    ///     The running turn, or <see langword="null" /> when none runs.
    /// </summary>
    private TurnState? _turn;

    /// <summary>
    ///     Whether the service was disposed.
    /// </summary>
    private bool _disposed;

    /// <summary>
    ///     Initializes a new instance of the <see cref="AgentService" /> class.
    /// </summary>
    /// <param name="projects">Gives the open project.</param>
    /// <param name="settings">Gives the settings and tells when they change.</param>
    /// <param name="registry">Builds the tools of a project.</param>
    /// <param name="instructions">Writes the instructions.</param>
    /// <param name="factory">Makes the agents; the service owns it and disposes it.</param>
    /// <param name="readiness">Checks whether the provider can be used.</param>
    /// <param name="projectTools">
    ///     The project tools whose <see cref="ProjectTools.FileWritten" /> event the service passes on and merges into the
    ///     events of a turn; <see langword="null" /> for none. The registry must offer the packs of the same instance.
    /// </param>
    /// <param name="activeStyle">Gives the style kit of the open project; <see langword="null" /> for none.</param>
    /// <param name="logger">Receives the metadata lines; <see langword="null" /> for none.</param>
    /// <param name="localDataRoot">
    ///     The folder that holds the data folder of the application, which is the local application data of the user when
    ///     <see langword="null" />. The Copilot data and the chat history are below it.
    /// </param>
    /// <param name="historyRoot">
    ///     The folder of the chat history, which is checked to lie outside the project; <see langword="null" /> for the
    ///     folder below <paramref name="localDataRoot" />.
    /// </param>
    /// <exception cref="ArgumentNullException">Thrown when a required argument is <see langword="null" />.</exception>
    public AgentService(
        IProjectService projects,
        ISettingsService settings,
        AgentToolRegistry registry,
        AgentInstructions instructions,
        IAgentFactory factory,
        AgentReadiness readiness,
        ProjectTools? projectTools = null,
        IActiveStyle? activeStyle = null,
        RollingFileLogger? logger = null,
        string? localDataRoot = null,
        string? historyRoot = null)
    {
        ArgumentNullException.ThrowIfNull(projects);
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(registry);
        ArgumentNullException.ThrowIfNull(instructions);
        ArgumentNullException.ThrowIfNull(factory);
        ArgumentNullException.ThrowIfNull(readiness);

        _projects = projects;
        _settings = settings;
        _registry = registry;
        _instructions = instructions;
        _factory = factory;
        _readiness = readiness;
        _projectTools = projectTools;
        _activeStyle = activeStyle;
        _logger = logger;
        _runtimeLogger = logger is null ? null : new RuntimeLogAdapter(logger, "CopilotRuntime");
        _copilotDataDirectory = GetCopilotDataDirectory(localDataRoot);
        _historyDirectory = historyRoot is null ? GetHistoryDirectory(localDataRoot) : Path.GetFullPath(historyRoot);

        _settings.Changed += OnSettingsChanged;
        _projects.CurrentChanged += OnProjectChanged;
        if (_projectTools is not null)
        {
            _projectTools.FileWritten += OnProjectToolsFileWritten;
        }
    }

    /// <inheritdoc />
    public event EventHandler<FileWrittenEventArgs>? FileWritten;

    /// <summary>
    ///     Gets the folder where the Copilot runtime keeps its data, below the local data root. It is never the default
    ///     folder of the AI library and never a folder of a project.
    /// </summary>
    /// <param name="localDataRoot">The local data root; <see langword="null" /> for the local application data of the user.</param>
    /// <returns>The full path of the folder.</returns>
    public static string GetCopilotDataDirectory(string? localDataRoot = null)
    {
        return Path.Combine(Path.GetFullPath(localDataRoot ?? GetDefaultLocalDataRoot()), ApplicationFolderName, CopilotFolderName);
    }

    /// <summary>
    ///     Gets the folder where the chat history is kept, below the local data root.
    /// </summary>
    /// <param name="localDataRoot">The local data root; <see langword="null" /> for the local application data of the user.</param>
    /// <returns>The full path of the folder.</returns>
    public static string GetHistoryDirectory(string? localDataRoot = null)
    {
        return Path.Combine(Path.GetFullPath(localDataRoot ?? GetDefaultLocalDataRoot()), ApplicationFolderName, HistoryFolderName);
    }

    /// <summary>
    ///     Gets the local data root of the user: the local application data folder, or the user profile or the current
    ///     folder when the system reports none.
    /// </summary>
    /// <returns>The folder.</returns>
    public static string GetDefaultLocalDataRoot()
    {
        var root = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        if (string.IsNullOrWhiteSpace(root))
        {
            root = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        }

        return string.IsNullOrWhiteSpace(root) ? "." : root;
    }

    /// <summary>
    ///     Determines whether a folder lies inside another, or is the same folder, by the spelling of their full paths,
    ///     whole names at a time, and without regard to case on Windows and macOS, whose usual file systems ignore it.
    /// </summary>
    /// <param name="root">The folder that is tested.</param>
    /// <param name="container">The folder that may contain it.</param>
    /// <returns><see langword="true" /> when <paramref name="root" /> is <paramref name="container" /> or below it.</returns>
    /// <exception cref="ArgumentException">Thrown when a path is empty.</exception>
    public static bool IsInside(string root, string container)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(root);
        ArgumentException.ThrowIfNullOrWhiteSpace(container);

        // The same comparison as the path guard of the project tools: case is ignored on Windows and macOS only, and
        // whole names are compared, so 'proj2' is not inside 'proj'
        return ProjectPathGuard.IsInside(Path.GetFullPath(container), Path.GetFullPath(root));
    }

    /// <inheritdoc />
    public Task<AgentReadinessReport> GetReadinessAsync(CancellationToken cancellationToken = default)
    {
        return _readiness.GetReportAsync(_settings.Current.AiProvider, refresh: false, cancellationToken);
    }

    /// <inheritdoc />
    public IAsyncEnumerable<AgentEvent> RunTurnAsync(AgentTurnRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        return RunTurnCoreAsync(request, cancellationToken);
    }

    /// <summary>
    ///     Runs one turn; the iterator behind <see cref="RunTurnAsync" />.
    /// </summary>
    /// <param name="request">The request.</param>
    /// <param name="cancellationToken">The token that stops the turn.</param>
    /// <returns>The events of the turn.</returns>
    private async IAsyncEnumerable<AgentEvent> RunTurnCoreAsync(
        AgentTurnRequest request,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var turn = TryBeginTurn(cancellationToken, out var refusal);
        if (turn is null)
        {
            Write(LogLevel.Warning, $"Turn refused: reason={refusal!.Kind}");
            yield return refusal;
            yield break;
        }

        var clock = Stopwatch.StartNew();
        var outcome = "Canceled";
        var events = 0;
        try
        {
            var prepared = await PrepareAsync(request, turn).ConfigureAwait(false);
            if (prepared.Failure is not null)
            {
                outcome = $"Failed:{prepared.Failure.Kind}";
                yield return prepared.Failure;
                yield break;
            }

            if (prepared.Conversation is null)
            {
                yield break;
            }

            var conversation = prepared.Conversation;
            var model = _settings.Current.AiProvider.ModelName;
            var mapper = new AgentEventMapper(conversation.Provider, model, _logger, turn.Token);
            var updates = conversation.Agent.RunStreamingAsync(prepared.Message!, conversation.Session, cancellationToken: turn.Token);
            var digestPending = prepared.DigestAdded;
            await foreach (var mapped in mapper.MapAsync(updates).ConfigureAwait(false))
            {
                // A file that a tool wrote while the model was working comes before the answer of the tool
                while (turn.Writes.TryDequeue(out var written))
                {
                    events++;
                    yield return written;
                }

                // The provider has the digest once something other than a failure came back
                if (digestPending && mapped is not AgentEvent.Failed)
                {
                    conversation.HistoryPrefixed = true;
                    digestPending = false;
                }

                events++;
                outcome = mapped switch
                {
                    AgentEvent.Completed => "Completed",
                    AgentEvent.Failed failed => $"Failed:{failed.Kind}",
                    _ => "Running",
                };
                yield return mapped;
            }

            if (turn.Token.IsCancellationRequested)
            {
                outcome = "Canceled";
            }
        }
        finally
        {
            EndTurn(turn);
            Write(LogLevel.Info, $"Turn ended: outcome={outcome} events={events} milliseconds={clock.ElapsedMilliseconds}");
        }
    }

    /// <inheritdoc />
    public void ResetConversation()
    {
        DropConversation(_ => true, cancelTurn: true, "reset");
    }

    /// <inheritdoc />
    public void Dispose()
    {
        Task[] pending;
        lock (_sync)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
        }

        _settings.Changed -= OnSettingsChanged;
        _projects.CurrentChanged -= OnProjectChanged;
        if (_projectTools is not null)
        {
            _projectTools.FileWritten -= OnProjectToolsFileWritten;
        }

        // End the running turn, release the conversation, and then stop what the factory started
        _lifetime.Cancel();
        DropConversation(_ => true, cancelTurn: true, "dispose");
        lock (_sync)
        {
            pending = [.. _cleanups];
        }

        try
        {
            var stopped = Task.Run(
                async () =>
                {
                    await Task.WhenAll(pending).ConfigureAwait(false);
                    await _factory.DisposeAsync().ConfigureAwait(false);
                },
                CancellationToken.None);
            if (!stopped.Wait(DisposeWait, CancellationToken.None))
            {
                Write(LogLevel.Warning, "Dispose gave up waiting for the conversation and the runtime to stop");
            }
        }
        catch (AggregateException ex)
        {
            Write(LogLevel.Warning, $"Dispose failed: exception={ex.InnerException?.GetType().Name ?? ex.GetType().Name}");
        }

        _lifetime.Dispose();
    }

    /// <summary>
    ///     Makes the signature of the settings that decide the agent.
    /// </summary>
    /// <param name="settings">The provider settings.</param>
    /// <returns>The signature; two settings with the same signature need the same agent.</returns>
    private static string SignatureOf(AiProviderSettings settings)
    {
        return string.Join(
            '\u001F',
            settings.Provider,
            settings.ModelName?.Trim() ?? string.Empty,
            settings.OllamaEndpoint?.Trim() ?? string.Empty,
            settings.ContextWindowTokens,
            settings.CopilotCliPath?.Trim() ?? string.Empty);
    }

    /// <summary>
    ///     Lists the files of a project for the instructions: paths below the folder with forward slashes, sorted, without
    ///     the data folder, the project files, links, and hidden files.
    /// </summary>
    /// <param name="folder">The project folder.</param>
    /// <returns>The paths, at most <see cref="MaxListedFiles" />.</returns>
    private static List<string> ListProjectFiles(string folder)
    {
        var found = new List<string>();
        var options = new EnumerationOptions
        {
            RecurseSubdirectories = true,
            IgnoreInaccessible = true,
            AttributesToSkip = FileAttributes.ReparsePoint | FileAttributes.Hidden | FileAttributes.System,
        };
        try
        {
            foreach (var file in Directory.EnumerateFiles(folder, "*", options))
            {
                if (file.EndsWith(ProjectDocument.FileExtension, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                found.Add(Path.GetRelativePath(folder, file).Replace('\\', '/'));
                if (found.Count >= MaxScannedFiles)
                {
                    break;
                }
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // The list only helps the model; a folder that cannot be listed gives a shorter list
        }

        found.Sort(StringComparer.OrdinalIgnoreCase);
        return found.Count > MaxListedFiles ? found.GetRange(0, MaxListedFiles) : found;
    }

    /// <summary>
    ///     Starts a turn when none runs.
    /// </summary>
    /// <param name="cancellationToken">The token of the caller.</param>
    /// <param name="refusal">The failure to report when no turn could start.</param>
    /// <returns>The turn, or <see langword="null" /> when it could not start.</returns>
    private TurnState? TryBeginTurn(CancellationToken cancellationToken, out AgentEvent.Failed? refusal)
    {
        lock (_sync)
        {
            if (_disposed)
            {
                refusal = new AgentEvent.Failed(AgentFailureKind.NotReady, ClosingMessage);
                return null;
            }

            if (_turn is not null)
            {
                refusal = new AgentEvent.Failed(AgentFailureKind.Busy, BusyMessage);
                return null;
            }

            _turn = new TurnState(CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _lifetime.Token));
            refusal = null;
            return _turn;
        }
    }

    /// <summary>
    ///     Ends a turn.
    /// </summary>
    /// <param name="turn">The turn.</param>
    private void EndTurn(TurnState turn)
    {
        lock (_sync)
        {
            if (ReferenceEquals(_turn, turn))
            {
                _turn = null;
            }
        }

        turn.Finish();
    }

    /// <summary>
    ///     Gets the conversation ready for a turn: checks that nothing of the application lies inside the project, keeps or
    ///     drops the conversation that exists, makes a new one when needed, and builds the message.
    /// </summary>
    /// <param name="request">The request.</param>
    /// <param name="turn">The turn.</param>
    /// <returns>The conversation and message, or the failure to report; neither when the turn was canceled.</returns>
    private async Task<PreparedTurn> PrepareAsync(AgentTurnRequest request, TurnState turn)
    {
        var token = turn.Token;
        try
        {
            var project = _projects.Current;
            if (project is null)
            {
                return PreparedTurn.Fail(AgentFailureKind.NotReady, NoProjectMessage);
            }

            // Nothing of the copilot may be written into the project, so a data folder inside it stops the turn
            var misplaced = new[] { _copilotDataDirectory, _historyDirectory }.FirstOrDefault(root => IsInside(root, project.FolderPath));
            if (misplaced is not null)
            {
                return PreparedTurn.Fail(AgentFailureKind.DataInsideProject, string.Format(System.Globalization.CultureInfo.CurrentCulture, DataInsideProjectMessage, misplaced));
            }

            var settings = _settings.Current.AiProvider;
            var signature = SignatureOf(settings);
            DropConversation(
                existing => !string.Equals(existing.ProjectFile, project.FilePath, StringComparison.OrdinalIgnoreCase) || existing.Signature != signature,
                cancelTurn: false,
                "changed");

            Conversation? conversation;
            lock (_sync)
            {
                conversation = _conversation;
            }

            if (conversation is null)
            {
                var made = await CreateConversationAsync(project, settings, signature, token).ConfigureAwait(false);
                if (made.Failure is not null || made.Conversation is null)
                {
                    return made;
                }

                conversation = made.Conversation;
            }

            // A conversation that a reset or a closed project dropped while it was made is not used
            lock (_sync)
            {
                if (!ReferenceEquals(_conversation, conversation) || token.IsCancellationRequested)
                {
                    token.ThrowIfCancellationRequested();
                    return PreparedTurn.Fail(AgentFailureKind.NotReady, "The conversation was reset. Send the message again.");
                }

                turn.Conversation = conversation;
            }

            var message = PayloadMessageMapper.Map(request.Payload, project.FolderPath);
            var digestAdded = false;
            if (!conversation.HistoryPrefixed)
            {
                var digest = RestoredHistoryDigest.Build(request.RestoredHistory);
                if (digest is not null)
                {
                    message.Contents.Insert(0, new TextContent(digest + "\n\n"));
                    digestAdded = true;
                }
                else
                {
                    conversation.HistoryPrefixed = true;
                }
            }

            Write(LogLevel.Info, $"Turn started: provider={conversation.Provider} restoredTurns={request.RestoredHistory.Count} digestAdded={digestAdded}");
            return new PreparedTurn(conversation, message, digestAdded, null);
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
            return PreparedTurn.Canceled;
        }
        catch (Exception ex)
        {
            // The details of the exception may hold paths or words, so only its type goes to the log
            Write(LogLevel.Error, $"Turn could not start: exception={ex.GetType().Name}");
            return PreparedTurn.Fail(AgentFailureKind.Unexpected, $"The copilot could not be started: {ex.Message}");
        }
    }

    /// <summary>
    ///     Makes the conversation of a project: the tools, the instructions, the agent, and a provider session.
    /// </summary>
    /// <param name="project">The open project.</param>
    /// <param name="settings">The provider settings.</param>
    /// <param name="signature">The signature of the settings.</param>
    /// <param name="cancellationToken">A token that cancels the work.</param>
    /// <returns>The conversation, which is also kept as the current one, or the failure to report.</returns>
    private async Task<PreparedTurn> CreateConversationAsync(
        ProjectInfo project,
        AiProviderSettings settings,
        string signature,
        CancellationToken cancellationToken)
    {
        var files = await Task.Run(() => ListProjectFiles(project.FolderPath), cancellationToken).ConfigureAwait(false);
        var facts = new AgentProjectFacts(project.Name, project.FolderPath, files, _activeStyle?.Active, _activeStyle?.ActiveId);
        var tier = AgentInstructions.ChooseTier(settings.Provider, settings.ContextWindowTokens);
        var instructions = _instructions.Build(facts, tier);
        var tools = _registry.CreateTools(new AgentToolContext(project.FolderPath), NoOptionalPacks);

        var request = new AgentCreationRequest(settings, tools, instructions, project.FolderPath)
        {
            CopilotDataDirectory = _copilotDataDirectory,
            CopilotLogger = _runtimeLogger,
        };
        var creation = await _factory.CreateAsync(request, cancellationToken).ConfigureAwait(false);
        if (!creation.IsCreated || creation.Agent is null)
        {
            await creation.DisposeAsync().ConfigureAwait(false);
            var failure = creation.Failure;
            return PreparedTurn.Fail(AgentFailureKind.NotReady, failure?.Message ?? "The AI provider cannot be used.");
        }

        AgentSession session;
        try
        {
            session = await creation.Agent.CreateSessionAsync(cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            await creation.DisposeAsync().ConfigureAwait(false);
            throw;
        }

        var conversation = new Conversation(project.FilePath, signature, settings.Provider, creation, creation.Agent, session);
        bool kept;
        lock (_sync)
        {
            kept = !_disposed && !cancellationToken.IsCancellationRequested;
            if (kept)
            {
                _conversation = conversation;
            }
        }

        if (!kept)
        {
            await ReleaseAsync(conversation, Task.CompletedTask, "unused").ConfigureAwait(false);
            return PreparedTurn.Canceled;
        }

        Write(LogLevel.Info, $"Conversation started: provider={settings.Provider} tier={tier} tools={tools.Count} files={files.Count}");
        return new PreparedTurn(conversation, null, false, null);
    }

    /// <summary>
    ///     Drops the current conversation when it matches, and releases it in the background once its turn is over.
    /// </summary>
    /// <param name="stale">Tells whether the conversation must go.</param>
    /// <param name="cancelTurn">Whether to cancel the turn that runs on the conversation.</param>
    /// <param name="reason">The reason, for the log.</param>
    private void DropConversation(Func<Conversation, bool> stale, bool cancelTurn, string reason)
    {
        Conversation? dropped;
        Task done = Task.CompletedTask;
        TurnState? running = null;
        lock (_sync)
        {
            dropped = _conversation;
            if (dropped is null || !stale(dropped))
            {
                return;
            }

            _conversation = null;
            if (_turn is not null && ReferenceEquals(_turn.Conversation, dropped))
            {
                running = _turn;
                done = _turn.Done;
            }
        }

        if (cancelTurn)
        {
            running?.Cancel();
        }

        lock (_sync)
        {
            _cleanups.RemoveAll(task => task.IsCompleted);
            _cleanups.Add(Task.Run(() => ReleaseAsync(dropped, done, reason), CancellationToken.None));
        }
    }

    /// <summary>
    ///     Releases a conversation: waits for its turn to end, deletes what the provider kept, and disposes what the agent
    ///     owns. Nothing is thrown.
    /// </summary>
    /// <param name="conversation">The conversation.</param>
    /// <param name="turnDone">Completes when the turn that runs on the conversation is over.</param>
    /// <param name="reason">The reason, for the log.</param>
    /// <returns>A task that completes when the conversation is released.</returns>
    private async Task ReleaseAsync(Conversation conversation, Task turnDone, string reason)
    {
        try
        {
            await turnDone.WaitAsync(ReleaseWait, CancellationToken.None).ConfigureAwait(false);
        }
        catch (TimeoutException)
        {
            Write(LogLevel.Warning, "Release did not wait any longer for the running turn");
        }

        try
        {
            await _factory.DeleteSessionAsync(conversation.Session, CancellationToken.None).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            Write(LogLevel.Warning, $"Session could not be deleted: exception={ex.GetType().Name}");
        }

        try
        {
            await conversation.Creation.DisposeAsync().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            Write(LogLevel.Warning, $"Conversation could not be disposed: exception={ex.GetType().Name}");
        }

        Write(LogLevel.Info, $"Conversation released: provider={conversation.Provider} reason={reason}");
    }

    /// <summary>
    ///     Reacts to a change of the settings: a conversation that the new settings no longer fit is dropped.
    /// </summary>
    /// <param name="sender">The settings service.</param>
    /// <param name="e">The new settings.</param>
    private void OnSettingsChanged(object? sender, SettingsChangedEventArgs e)
    {
        var signature = SignatureOf(e.Settings.AiProvider);
        DropConversation(existing => existing.Signature != signature, cancelTurn: false, "settings");
    }

    /// <summary>
    ///     Reacts to a change of the open project: the conversation of another project, or of none, is dropped and its turn
    ///     is stopped.
    /// </summary>
    /// <param name="sender">The project service.</param>
    /// <param name="e">Nothing.</param>
    private void OnProjectChanged(object? sender, EventArgs e)
    {
        var project = _projects.Current;
        DropConversation(
            existing => project is null || !string.Equals(existing.ProjectFile, project.FilePath, StringComparison.OrdinalIgnoreCase),
            cancelTurn: true,
            "project");
    }

    /// <summary>
    ///     Passes on that the copilot wrote a file: to the running turn, so that the stream shows it, and to the shell.
    /// </summary>
    /// <param name="sender">The project tools.</param>
    /// <param name="e">What was written.</param>
    private void OnProjectToolsFileWritten(object? sender, FileWrittenEventArgs e)
    {
        TurnState? turn;
        lock (_sync)
        {
            turn = _turn;
        }

        turn?.Writes.Enqueue(new AgentEvent.FileWritten(e.RelativePath, !e.Created));
        FileWritten?.Invoke(this, e);
    }

    /// <summary>
    ///     Writes a line of the log, if there is a log.
    /// </summary>
    /// <param name="level">The level.</param>
    /// <param name="message">The line; it holds metadata only.</param>
    private void Write(LogLevel level, string message)
    {
        if (_logger is null)
        {
            return;
        }

        try
        {
            _logger.Log(level, message);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // A log that cannot be written is not a reason to fail a turn
        }
    }

    /// <summary>
    ///     The conversation of one project with one provider.
    /// </summary>
    private sealed class Conversation
    {
        /// <summary>
        ///     Initializes a new instance of the <see cref="Conversation" /> class.
        /// </summary>
        /// <param name="projectFile">The project file of the project.</param>
        /// <param name="signature">The signature of the settings.</param>
        /// <param name="provider">The provider.</param>
        /// <param name="creation">What owns the agent.</param>
        /// <param name="agent">The agent.</param>
        /// <param name="session">The provider session.</param>
        internal Conversation(
            string projectFile,
            string signature,
            AiProviderKind provider,
            AgentCreationResult creation,
            AIAgent agent,
            AgentSession session)
        {
            ProjectFile = projectFile;
            Signature = signature;
            Provider = provider;
            Creation = creation;
            Agent = agent;
            Session = session;
        }

        /// <summary>Gets the project file of the project.</summary>
        internal string ProjectFile { get; }

        /// <summary>Gets the signature of the settings that the agent was made for.</summary>
        internal string Signature { get; }

        /// <summary>Gets the provider.</summary>
        internal AiProviderKind Provider { get; }

        /// <summary>Gets what owns the agent.</summary>
        internal AgentCreationResult Creation { get; }

        /// <summary>Gets the agent.</summary>
        internal AIAgent Agent { get; }

        /// <summary>Gets the provider session.</summary>
        internal AgentSession Session { get; }

        /// <summary>Gets or sets a value indicating whether the restored history was given to the provider.</summary>
        internal bool HistoryPrefixed { get; set; }
    }

    /// <summary>
    ///     The state of the running turn.
    /// </summary>
    private sealed class TurnState
    {
        /// <summary>
        ///     The source of the token of the turn.
        /// </summary>
        private readonly CancellationTokenSource _cancellation;

        /// <summary>
        ///     Completes when the turn is over.
        /// </summary>
        private readonly TaskCompletionSource _done = new(TaskCreationOptions.RunContinuationsAsynchronously);

        /// <summary>
        ///     Initializes a new instance of the <see cref="TurnState" /> class.
        /// </summary>
        /// <param name="cancellation">The source of the token of the turn; the turn disposes it.</param>
        internal TurnState(CancellationTokenSource cancellation)
        {
            _cancellation = cancellation;
            Token = cancellation.Token;
        }

        /// <summary>Gets the token that stops the turn.</summary>
        internal CancellationToken Token { get; }

        /// <summary>Gets the files that tools wrote during the turn and that the stream has not shown yet.</summary>
        internal ConcurrentQueue<AgentEvent.FileWritten> Writes { get; } = new();

        /// <summary>Gets a task that completes when the turn is over.</summary>
        internal Task Done => _done.Task;

        /// <summary>Gets or sets the conversation that the turn runs on.</summary>
        internal Conversation? Conversation { get; set; }

        /// <summary>
        ///     Stops the turn.
        /// </summary>
        internal void Cancel()
        {
            try
            {
                _cancellation.Cancel();
            }
            catch (ObjectDisposedException)
            {
                // The turn is over already
            }
        }

        /// <summary>
        ///     Marks the turn as over and releases its token source.
        /// </summary>
        internal void Finish()
        {
            _done.TrySetResult();
            _cancellation.Dispose();
        }
    }

    /// <summary>
    ///     The outcome of getting a turn ready.
    /// </summary>
    /// <param name="Conversation">The conversation to run on, or <see langword="null" />.</param>
    /// <param name="Message">The message to send, or <see langword="null" /> before it is built.</param>
    /// <param name="DigestAdded">Whether the message starts with the digest of the restored history.</param>
    /// <param name="Failure">The failure to report, or <see langword="null" />.</param>
    private sealed record PreparedTurn(Conversation? Conversation, ChatMessage? Message, bool DigestAdded, AgentEvent.Failed? Failure)
    {
        /// <summary>
        ///     Gets the outcome of a turn that was canceled while it got ready: nothing to run and nothing to report.
        /// </summary>
        internal static PreparedTurn Canceled { get; } = new(null, null, false, null);

        /// <summary>
        ///     Makes the outcome of a turn that cannot run.
        /// </summary>
        /// <param name="kind">The kind of the failure.</param>
        /// <param name="message">The sentence for the user.</param>
        /// <returns>The outcome.</returns>
        internal static PreparedTurn Fail(AgentFailureKind kind, string message)
        {
            return new PreparedTurn(null, null, false, new AgentEvent.Failed(kind, message));
        }
    }
}
