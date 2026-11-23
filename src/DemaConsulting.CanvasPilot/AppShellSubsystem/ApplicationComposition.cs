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

using Avalonia;
using DemaConsulting.CanvasPilot.AgentSubsystem;
using DemaConsulting.CanvasPilot.AnnotationSubsystem;
using DemaConsulting.CanvasPilot.ChatSubsystem;
using DemaConsulting.CanvasPilot.ComposerSubsystem;
using DemaConsulting.CanvasPilot.KnowledgeSubsystem;
using DemaConsulting.CanvasPilot.LoggingSubsystem;
using DemaConsulting.CanvasPilot.PayloadSubsystem;
using DemaConsulting.CanvasPilot.PreviewSubsystem;
using DemaConsulting.CanvasPilot.ProjectSubsystem;
using DemaConsulting.CanvasPilot.SettingsSubsystem;
using DemaConsulting.CanvasPilot.SpeechSubsystem;
using DemaConsulting.CanvasPilot.StyleSubsystem;

namespace DemaConsulting.CanvasPilot.AppShellSubsystem;

/// <summary>
///     The application's composition root: creates the units in dependency order (logger, settings, project
///     service, recent projects, explorer, workspace, shell, and, when the folders for its data are given, the AI
///     copilot) and holds them for the application's lifetime. The application creates it at startup and disposes it
///     on shutdown; system tests create it with temporary folders and no window.
/// </summary>
public sealed class ApplicationComposition : IDisposable
{
    /// <summary>
    ///     Whether the composition has been disposed.
    /// </summary>
    private bool _disposed;

    /// <summary>
    ///     The services this composition created and must dispose, in the order they are disposed.
    /// </summary>
    private readonly IReadOnlyList<IDisposable> _ownedServices;

    /// <summary>
    ///     The handler that moves the composer to the project that is open, kept so that it can be removed.
    /// </summary>
    private readonly EventHandler _projectChanged;

    /// <summary>
    ///     Initializes a new instance of the <see cref="ApplicationComposition" /> class.
    /// </summary>
    /// <param name="logger">The application log.</param>
    /// <param name="settings">The settings service.</param>
    /// <param name="projects">The project service.</param>
    /// <param name="recentProjects">The recent projects list.</param>
    /// <param name="preview">The preview pane view model.</param>
    /// <param name="shell">The main window view model.</param>
    /// <param name="dictation">The dictation service.</param>
    /// <param name="composer">The composer of the message.</param>
    /// <param name="marks">The marks made for the draft message.</param>
    /// <param name="annotation">The layer on which the user marks the shown picture.</param>
    /// <param name="chat">The chat panel, which joins the composer and the annotation layer.</param>
    /// <param name="styleCatalog">The catalog of style kits.</param>
    /// <param name="activeStyle">The style kit that the open project uses.</param>
    /// <param name="stylePicker">The pill in the title bar with which the user chooses the style kit.</param>
    /// <param name="styleCheck">What the Check Against Style item of the Image menu runs.</param>
    /// <param name="styleTools">The descriptors of the tools that give and check the style, for an assistant.</param>
    /// <param name="agent">The AI copilot, or <see langword="null" /> when the composition has none.</param>
    /// <param name="ownedServices">The services the composition created and must dispose.</param>
    /// <param name="styleStartup">The task that reads the style kits when the application starts.</param>
    private ApplicationComposition(
        RollingFileLogger logger,
        ISettingsService settings,
        IProjectService projects,
        IRecentProjects recentProjects,
        PreviewPaneViewModel preview,
        MainWindowShell shell,
        IDictationService dictation,
        ComposerViewModel composer,
        MarkRegistry marks,
        AnnotationOverlayViewModel annotation,
        ChatPanelViewModel chat,
        IStyleKitCatalog styleCatalog,
        IActiveStyle activeStyle,
        StylePickerViewModel stylePicker,
        ICheckAgainstStyle styleCheck,
        IReadOnlyList<KnowledgeTool> styleTools,
        IAgentService? agent,
        IReadOnlyList<IDisposable> ownedServices,
        Task styleStartup)
    {
        StyleStartupRefresh = styleStartup;
        Logger = logger;
        Settings = settings;
        Projects = projects;
        RecentProjects = recentProjects;
        Preview = preview;
        Shell = shell;
        Dictation = dictation;
        Composer = composer;
        Marks = marks;
        Annotation = annotation;
        Chat = chat;
        StyleCatalog = styleCatalog;
        ActiveStyle = activeStyle;
        StylePicker = stylePicker;
        StyleCheck = styleCheck;
        StyleToolDescriptors = styleTools;
        Agent = agent;
        _ownedServices = ownedServices;

        // The message is written in the open project: follow it, and start with the one that is open now. A project that is
        // opened may be announced from a worker thread, and the composer drives the editor, so it is told on the thread of the
        // user interface that created the composition.
        var context = SynchronizationContext.Current;
        _projectChanged = (_, _) =>
        {
            if (context is null || SynchronizationContext.Current == context)
            {
                Composer.SetProject(Projects.Current?.FolderPath);
            }
            else
            {
                context.Post(_ => Composer.SetProject(Projects.Current?.FolderPath), null);
            }
        };
        Projects.CurrentChanged += _projectChanged;
        Composer.SetProject(Projects.Current?.FolderPath);
    }

    /// <summary>
    ///     Gets the application log.
    /// </summary>
    public RollingFileLogger Logger { get; }

    /// <summary>
    ///     Gets the settings service.
    /// </summary>
    public ISettingsService Settings { get; }

    /// <summary>
    ///     Gets the project service.
    /// </summary>
    public IProjectService Projects { get; }

    /// <summary>
    ///     Gets the recent projects list.
    /// </summary>
    public IRecentProjects RecentProjects { get; }

    /// <summary>
    ///     Gets the view model of the preview pane shown in the workspace.
    /// </summary>
    public PreviewPaneViewModel Preview { get; }

    /// <summary>
    ///     Gets the view model of the main window.
    /// </summary>
    public MainWindowShell Shell { get; }

    /// <summary>
    ///     Gets the dictation service, which turns speech held to talk into text with the speech settings in effect.
    /// </summary>
    public IDictationService Dictation { get; }

    /// <summary>
    ///     Gets the composer of the message to the assistant: its text, attachments, marks, dictation, undo history, and
    ///     sending. It works in the open project and has no message while none is open.
    /// </summary>
    public ComposerViewModel Composer { get; }

    /// <summary>
    ///     Gets the marks that are drawn on the shown picture for the message being composed.
    /// </summary>
    public MarkRegistry Marks { get; }

    /// <summary>
    ///     Gets the layer on which the user marks the shown picture. Its tools are in the top bar of the preview pane, it
    ///     is the overlay of the preview pane, and the chip of every mark drawn on it is added to the message by the chat
    ///     panel.
    /// </summary>
    public AnnotationOverlayViewModel Annotation { get; }

    /// <summary>
    ///     Gets the chat panel: the transcript of the sent messages, the message being composed with its send and
    ///     hold-to-talk buttons, and the joining of the marks to the message. It is the chat column of the workspace,
    ///     which shows it while a project is open, and the target of the talk key.
    /// </summary>
    public ChatPanelViewModel Chat { get; }

    /// <summary>
    ///     Gets the catalog of style kits: the kits that come with the program, of the user, of the open project, and in
    ///     the shared folders. It is read in the background when the application starts, so it is empty for a moment.
    /// </summary>
    public IStyleKitCatalog StyleCatalog { get; }

    /// <summary>
    ///     Gets the task of the first reading of the style kits, which starts with the application. It completes after the
    ///     catalog announced what it read, so a test that waits for it gets no announcement from the start of the
    ///     application afterwards.
    /// </summary>
    internal Task StyleStartupRefresh { get; }

    /// <summary>
    ///     Gets the style kit that the open project uses, and the means to change it.
    /// </summary>
    public IActiveStyle ActiveStyle { get; }

    /// <summary>
    ///     Gets the pill in the title bar of the main window with which the user chooses the style kit of the open project.
    /// </summary>
    public StylePickerViewModel StylePicker { get; }

    /// <summary>
    ///     Gets what the Check Against Style item of the Image menu runs: the check of the shown picture against the style
    ///     kit of the open project.
    /// </summary>
    public ICheckAgainstStyle StyleCheck { get; }

    /// <summary>
    ///     Gets the descriptors of the two tools that give the style (<c>style_get</c>) and check a picture against it
    ///     (<c>style_check</c>), in the form of the descriptors of the drawing knowledge. The composition gives them, with
    ///     the descriptors of the drawing knowledge, to the registry of the tools of the AI copilot.
    /// </summary>
    public IReadOnlyList<KnowledgeTool> StyleToolDescriptors { get; }

    /// <summary>
    ///     Gets the AI copilot that the chat panel talks to, or <see langword="null" /> when the composition was made
    ///     without the folders for its data, in which case the panel has no copilot.
    /// </summary>
    public IAgentService? Agent { get; }

    /// <summary>
    ///     Creates the units for the running application: the log under the per-user local application data
    ///     folder, the settings under the per-user application data folder, the Documents folder as the place
    ///     offered for new projects, the dispatcher-based refresh scheduler, and the AI copilot, which keeps its data
    ///     (the Copilot runtime files and the chat history) under <c>CanvasPilot</c> in the local application data folder.
    /// </summary>
    /// <param name="application">
    ///     The running application, whose theme variant follows the theme choice of the settings, or <see langword="null" />
    ///     for a composition that has no application (the theme then stays as it is).
    /// </param>
    /// <returns>The composition, with the settings already loaded.</returns>
    public static ApplicationComposition CreateDefault(Application? application = null)
    {
        var logger = RollingFileLogger.CreateDefault();
        var settings = SettingsService.CreateDefault(logger);
        var documents = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        var localDataRoot = AgentService.GetDefaultLocalDataRoot();
        return Create(
            logger,
            settings,
            new DispatcherRefreshScheduler(),
            defaultProjectsFolder: string.IsNullOrWhiteSpace(documents) ? null : documents,
            dragFolder: ImageDragSource.DefaultFolder,
            localDataRoot: localDataRoot,
            historyRoot: ChatHistoryStore.DefaultHistoryRoot,
            application: application);
    }

    /// <summary>
    ///     Creates the units around the given logger, settings service, and refresh scheduler, and loads the
    ///     settings.
    /// </summary>
    /// <param name="logger">The application log.</param>
    /// <param name="settings">The settings service; its settings are loaded by this method.</param>
    /// <param name="scheduler">Delays the project explorer's refreshes and the preview's renders.</param>
    /// <param name="settingsPages">
    ///     The pages of the settings dialog, or <see langword="null" /> for the standard pages.
    /// </param>
    /// <param name="imageExporter">
    ///     What the Image menu exports, or <see langword="null" /> for the exporter of the preview pane.
    /// </param>
    /// <param name="defaultProjectsFolder">The folder offered for new projects, or <see langword="null" />.</param>
    /// <param name="renderService">
    ///     Draws the image source files, or <see langword="null" /> for the CanvasNet-based service.
    /// </param>
    /// <param name="audioDevices">
    ///     Lists the audio devices for the Speech settings page and creates the microphone for dictation, or
    ///     <see langword="null" /> for the service over the speech library.
    /// </param>
    /// <param name="speechModels">
    ///     Lists and downloads the speech models for the Speech settings page and loads the speech engine for
    ///     dictation, or <see langword="null" /> for the service over the speech library's shared model store, which
    ///     the composition then disposes.
    /// </param>
    /// <param name="dragFolder">
    ///     The folder in which the pictures that the user drags out of the preview are written, or
    ///     <see langword="null" /> for a folder in the system's temporary folder (the running application passes
    ///     the per-user local application data folder). Files of earlier runs in it are deleted at start and the
    ///     files of this run when the composition is disposed.
    /// </param>
    /// <param name="dictation">
    ///     The dictation service, or <see langword="null" /> for the service over the speech settings, the speech
    ///     models, and the audio devices, which the composition then disposes. It touches neither the model store, the
    ///     audio system, nor the speech engine until dictation is first used.
    /// </param>
    /// <param name="messageBuilder">
    ///     What turns a sent message into what is kept and given to the assistant, or <see langword="null" /> for the
    ///     payload builder, which draws the marks with the render service.
    /// </param>
    /// <param name="styleUserFolder">
    ///     The folder of the style kits of the user, or <see langword="null" /> for <c>styles</c> in the data folder of the
    ///     application. Tests pass a temporary folder so that they do not read the kits of the person who runs them.
    /// </param>
    /// <param name="localDataRoot">
    ///     The folder that holds the data folder of the application (<c>CanvasPilot</c>), which is where the Copilot runtime
    ///     keeps its files. The running application passes the local application data folder of the user. When this is
    ///     <see langword="null" /> the composition has no AI copilot and the chat panel works without one, so a test that
    ///     does not need it never touches the folders, the processes, or the network of the user.
    /// </param>
    /// <param name="historyRoot">
    ///     The folder of the chat history, or <see langword="null" /> for <c>CanvasPilot\history</c> below
    ///     <paramref name="localDataRoot" />. It is never a place in a project. It is used only with
    ///     <paramref name="localDataRoot" />.
    /// </param>
    /// <param name="agentFactory">
    ///     Makes the agents of the AI copilot, or <see langword="null" /> for the factory of the providers (Ollama, and
    ///     GitHub Copilot on the installed CLI). The composition passes it to the AI copilot, which disposes it.
    /// </param>
    /// <param name="processRunner">
    ///     Runs the Copilot CLI for the readiness check, or <see langword="null" /> for the real
    ///     processes.
    /// </param>
    /// <param name="ollamaProbe">Asks the Ollama server for its models, or <see langword="null" /> for the real server.</param>
    /// <param name="environment">Reads the environment for the readiness check, or <see langword="null" /> for the real one.</param>
    /// <param name="cliFileSystem">Looks for the Copilot CLI in the file system, or <see langword="null" /> for the real one.</param>
    /// <param name="copilotStarter">
    ///     Starts the Copilot runtime that the readiness check and the factory of the agents share, or <see langword="null" />
    ///     for a starter of the real runtime on the Copilot CLI that the user installed. The composition stops the starter that it
    ///     makes itself and never one that it is given.
    /// </param>
    /// <param name="ollamaCatalogs">
    ///     Gives the catalog of the Ollama server for the readiness check, the factory of the agents, and the model list of
    ///     the settings page, or <see langword="null" /> for catalogs on the real network.
    /// </param>
    /// <param name="folderLauncher">
    ///     Shows a folder in the file manager for the Open Log Folder item of the Help menu, or <see langword="null" /> for
    ///     the file manager of the real system.
    /// </param>
    /// <param name="application">
    ///     The running application, whose theme variant follows the theme choice of the settings (it is applied before the
    ///     composition is returned, so the first window opens in the right theme), or <see langword="null" /> for a
    ///     composition that has no application, such as a test without a window, which leaves the theme as it is.
    /// </param>
    /// <returns>The composition.</returns>
    public static ApplicationComposition Create(
        RollingFileLogger logger,
        ISettingsService settings,
        IRefreshScheduler scheduler,
        IReadOnlyList<ISettingsPage>? settingsPages = null,
        IImageExporter? imageExporter = null,
        string? defaultProjectsFolder = null,
        IImageRenderService? renderService = null,
        IAudioDeviceService? audioDevices = null,
        ISpeechModelService? speechModels = null,
        string? dragFolder = null,
        IDictationService? dictation = null,
        IMessageBuilder? messageBuilder = null,
        string? styleUserFolder = null,
        string? localDataRoot = null,
        string? historyRoot = null,
        IAgentFactory? agentFactory = null,
        IProcessRunner? processRunner = null,
        IOllamaProbe? ollamaProbe = null,
        IEnvironmentReader? environment = null,
        ICliFileSystem? cliFileSystem = null,
        ICopilotRuntimeStarter? copilotStarter = null,
        IOllamaModelCatalogs? ollamaCatalogs = null,
        IFolderLauncher? folderLauncher = null,
        Application? application = null)
    {
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(scheduler);

        settings.Load();

        var projects = new ProjectService(logger);
        var recentProjects = new RecentProjects(settings);
        var renderer = renderService ?? new ImageRenderService(logger);
        var thumbnails = new ThumbnailProvider(renderer);
        var explorer = new ProjectExplorerViewModel(new CanvasNetImageSourceFilter(), scheduler, logger: logger, thumbnails: thumbnails);
        explorer.ShowThumbnails = settings.Current.Appearance.ShowThumbnails;
        explorer.ComfortableRows = settings.Current.Appearance.ComfortableRows;
        var dragSource = new ImageDragSource(
            renderer,
            dragFolder ?? Path.Combine(Path.GetTempPath(), "CanvasPilot", "drag"),
            logger);
        dragSource.Purge();
        var preview = new PreviewPaneViewModel(renderer, explorer, scheduler, logger: logger, dragSource: dragSource);
        var workspace = new ProjectWorkspaceViewModel(explorer) { Preview = preview };

        // The style kits: where they are, which one the open project uses, the list that chooses it, and the sizes the
        // exporter offers. The kits are read in the background, so the start of the application never waits for them.
        var styleCatalog = new StyleKitCatalog(projects, settings, userFolder: styleUserFolder, logger: logger);
        var styleSelection = new StyleSelection(projects, styleCatalog, settings, logger);
        var stylePicker = new StylePickerViewModel(styleSelection, styleCatalog, projects, styleSelection.ApplyDefaultToNewProjectAsync, logger);
        var styleCheck = new StyleCheckCommand(preview, styleSelection, preview, logger);
        var styleTools = StyleTools.CreateAll(
            styleSelection,
            styleCatalog,
            () => projects.Current?.FolderPath,
            () => preview.CurrentImage?.SourcePath,
            renderer);
        var styleStartup = RefreshStyleKitsAsync(styleCatalog, logger);

        var exporter = imageExporter ?? new ImageExporter(preview, renderer, logger, styleSelection);

        // The speech services are shared by the Speech settings page and by dictation. They touch the model store
        // and the audio system only when they are first used, so creating them costs nothing at startup; the ones
        // created here are the composition's to dispose, the dictation service before the model service it uses.
        var owned = new List<IDisposable> { thumbnails };

        // The renders that run in the background write to the log when they fail; when the composition is disposed it waits
        // for them (they are cancelled by then), so that nothing writes to the log after it has gone
        if (renderer is ImageRenderService drawing)
        {
            owned.Add(new RenderDrain(drawing));
        }

        if (speechModels is null)
        {
            var modelService = new SpeechModelService(logger);
            speechModels = modelService;
            owned.Add(modelService);
        }

        audioDevices ??= new AudioDeviceService(logger);
        if (dictation is null)
        {
            var dictationService = new DictationService(settings, speechModels, audioDevices, logger);
            dictation = dictationService;
            owned.Insert(0, dictationService);
        }

        // The style units listen to the project and the settings; they are disposed before the services they listen to
        owned.Insert(0, styleCatalog);
        owned.Insert(0, styleSelection);
        owned.Insert(0, stylePicker);
        owned.Insert(0, styleCheck);

        // The AI copilot: the tools of the drawing knowledge, the style, and the project, the instructions, the factory of
        // the agents of the provider, and the check that tells whether the provider can be used, which the Settings page
        // also offers as Check connection. Its data is kept in the folders given here, outside every project. It is
        // disposed first of all, so it stops listening to the project and the settings before they go.
        AgentService? agent = null;
        AgentReadiness? readiness = null;
        ProviderModelCatalog? modelSource = null;
        ChatHistoryStore? chatHistory = null;
        if (localDataRoot is not null)
        {
            var runner = processRunner ?? new ProcessRunner();
            var fileSystem = cliFileSystem ?? new CliFileSystem();
            var environmentReader = environment ?? new SystemEnvironmentReader();
            var locator = new CopilotCliLocator(runner, fileSystem, environmentReader);

            // One Copilot runtime is shared by the check of the readiness and by the agents: the runtime keeps its data in
            // one folder, which two runtimes must not use. The runtime is stopped with the factory that owns the starter,
            // or with the composition when the factory is not the one made here.
            var ownedStarter = copilotStarter is null ? new CopilotRuntimeStarter(AgentService.GetCopilotDataDirectory(localDataRoot)) : null;
            var starter = copilotStarter ?? ownedStarter!;

            // The catalogs of Ollama are shared as well, so that the check, the agents, and the model list describe the
            // models of the server once
            var catalogs = ollamaCatalogs ?? new OllamaModelCatalogs();
            readiness = new AgentReadiness(locator, starter, ollamaProbe, catalogs);
            modelSource = new ProviderModelCatalog(locator, starter, catalogs);
            var projectTools = new ProjectTools(renderer, styleSelection, logger: logger);
            var registry = new AgentToolRegistry(
                KnowledgeTools.CreateAll(KnowledgeCatalog.Default),
                styleTools,
                projectTools.CreatePacks);
            var instructions = new AgentInstructions(new KnowledgePromptBuilder(KnowledgeCatalog.Default));
            var factory = agentFactory ?? new AgentProviderFactory(locator, starter, null, logger, ownsStarter: ownedStarter is not null, ollamaCatalogs: catalogs);
            if (agentFactory is not null && ownedStarter is not null)
            {
                owned.Add(new AsyncOwner(ownedStarter));
            }

            var historyFolder = historyRoot ?? AgentService.GetHistoryDirectory(localDataRoot);
            agent = new AgentService(
                projects,
                settings,
                registry,
                instructions,
                factory,
                readiness,
                projectTools,
                styleSelection,
                logger,
                localDataRoot,
                historyFolder);
            chatHistory = new ChatHistoryStore(historyFolder, logger);
            owned.Insert(0, agent);
        }

        // The Style Kits page browses for folders through the dialog service of the main window, which the shell has only
        // once the window attaches itself; the page asks the shell when the user asks to browse
        MainWindowShell? pickerOwner = null;
        IReadOnlyList<ISettingsPage> pages = settingsPages ??
        [
            new AiProviderSettingsPageViewModel(readiness, modelSource),
            new SpeechSettingsPageViewModel(audioDevices, speechModels),
            new StyleSettingsPageViewModel(styleCatalog, () => pickerOwner?.FolderPicker),
            new AppearanceSettingsPageViewModel(),
        ];

        var shell = new MainWindowShell(
            projects,
            recentProjects,
            settings,
            workspace,
            pages,
            exporter,
            defaultProjectsFolder,
            () => KnowledgeCatalog.Default,
            stylePicker,
            styleCheck,
            dialogs => new StyleKitManagerViewModel(styleCatalog, styleSelection, renderer, dialogs, projects, logger),
            logger,
            thumbnails,
            folderLauncher ?? new SystemFolderLauncher());
        pickerOwner = shell;

        // The composer of the message: marks drawn on the picture, the message builder, and dictation, working in the open project
        var marks = new MarkRegistry();
        var markImages = new MarkImageComposer(renderer);
        var composer = new ComposerViewModel(
            messageBuilder ?? new PayloadBuilder(markImages),
            marks,
            dictation,
            logger: logger);

        // The layer over the picture: drawing a mark turns off dragging the picture out, and its tools are in the top bar
        var annotation = new AnnotationOverlayViewModel(marks, preview, allowed => preview.IsDragOutEnabled = allowed, logger);
        preview.Overlay = annotation;
        preview.Toolbar = new AnnotationToolbarViewModel(annotation);

        // The chat panel joins the two: every mark that is drawn becomes a chip of the message, and the layer shows the
        // marks whose chips the message holds. It talks to the AI copilot and keeps its conversation in the history
        var chat = new ChatPanelViewModel(
            composer,
            marks,
            annotation,
            markImages,
            dictation,
            logger,
            agent,
            history: chatHistory,
            projects: projects);

        // The chat panel is the right-hand column of the workspace, which shows it while a project is open, and it is
        // what the talk key of the window listens for
        workspace.Chat = chat;
        shell.PushToTalk = chat;
        shell.ProviderPill = new ProviderPillViewModel(chat, settings, shell.OpenSettingsCommand);

        // What the copilot does to the rest of the application: a file it writes is shown in the explorer, the chat asks
        // for the settings, and a change of the provider is checked again. These are released before the copilot.
        if (agent is not null)
        {
            owned.Insert(0, new CopilotWiring(agent, settings, explorer, chat, shell, logger));
        }

        // The theme variant of the application follows the choice of the settings, from the first window on
        if (application is not null)
        {
            owned.Add(new ThemeVariantApplier(application, settings));
        }

        logger.Log(LogLevel.Info, "Application started.");
        return new ApplicationComposition(
            logger, settings, projects, recentProjects, preview, shell, dictation, composer, marks, annotation, chat,
            styleCatalog, styleSelection, stylePicker, styleCheck, styleTools, agent, owned, styleStartup);
    }

    /// <summary>
    ///     Waits, when the composition is disposed, for the renders that are still running in the background, which the
    ///     units that asked for them have cancelled; the last of the owned services, after the units that start renders.
    /// </summary>
    private sealed class RenderDrain : IDisposable
    {
        /// <summary>The longest time that a render may take to end.</summary>
        private static readonly TimeSpan DrainWait = TimeSpan.FromSeconds(10);

        /// <summary>The service whose renders are waited for.</summary>
        private readonly ImageRenderService _service;

        /// <summary>
        ///     Initializes a new instance of the <see cref="RenderDrain" /> class.
        /// </summary>
        /// <param name="service">The service whose renders are waited for.</param>
        internal RenderDrain(ImageRenderService service)
        {
            _service = service;
        }

        /// <inheritdoc />
        public void Dispose()
        {
            // The wait is bounded so that a render that does not end cannot keep the application open
            _ = _service.WhenIdleAsync().Wait(DrainWait);
        }
    }

    /// <summary>
    ///     Stops an asynchronously disposable service that the composition made, when the composition is disposed.
    /// </summary>
    private sealed class AsyncOwner : IDisposable
    {
        /// <summary>The longest time that the service may take to stop.</summary>
        private static readonly TimeSpan StopWait = TimeSpan.FromSeconds(10);

        /// <summary>The service to stop.</summary>
        private readonly IAsyncDisposable _service;

        /// <summary>
        ///     Initializes a new instance of the <see cref="AsyncOwner" /> class.
        /// </summary>
        /// <param name="service">The service to stop; the owner stops it once.</param>
        internal AsyncOwner(IAsyncDisposable service)
        {
            _service = service;
        }

        /// <inheritdoc />
        public void Dispose()
        {
            // The wait is bounded so that a runtime that does not stop cannot keep the application open
            _ = Task.Run(async () => await _service.DisposeAsync().ConfigureAwait(false)).Wait(StopWait);
        }
    }

    /// <summary>
    ///     What the AI copilot does to the rest of the application, joined in one place because none of the units knows
    ///     the others: a file that the copilot writes is shown in the project explorer and selected, which shows its
    ///     picture; the chat panel's request for the settings opens the settings dialog of the shell; and a change of the
    ///     AI provider settings makes the chat panel check the provider again. Everything runs on the thread of the user
    ///     interface that created the composition, or on the calling thread when there was none (in tests).
    /// </summary>
    private sealed class CopilotWiring : IDisposable
    {
        /// <summary>The thread context of the user interface, or <see langword="null" /> without one.</summary>
        private readonly SynchronizationContext? _context = SynchronizationContext.Current;

        /// <summary>The copilot that reports the files it wrote.</summary>
        private readonly IAgentService _agent;

        /// <summary>The settings whose changes are watched.</summary>
        private readonly ISettingsService _settings;

        /// <summary>The explorer that shows the files of the project.</summary>
        private readonly ProjectExplorerViewModel _explorer;

        /// <summary>The chat panel that checks the provider and asks for the settings.</summary>
        private readonly ChatPanelViewModel _chat;

        /// <summary>The shell whose settings command is run.</summary>
        private readonly MainWindowShell _shell;

        /// <summary>The application log.</summary>
        private readonly RollingFileLogger _logger;

        /// <summary>The AI provider settings that the provider was last checked for.</summary>
        private AiProviderSettings _provider;

        /// <summary>Whether the wiring was disposed.</summary>
        private volatile bool _disposed;

        /// <summary>
        ///     Initializes a new instance of the <see cref="CopilotWiring" /> class and starts listening.
        /// </summary>
        /// <param name="agent">The copilot.</param>
        /// <param name="settings">The settings service.</param>
        /// <param name="explorer">The project explorer.</param>
        /// <param name="chat">The chat panel.</param>
        /// <param name="shell">The main window shell.</param>
        /// <param name="logger">The application log.</param>
        internal CopilotWiring(
            IAgentService agent,
            ISettingsService settings,
            ProjectExplorerViewModel explorer,
            ChatPanelViewModel chat,
            MainWindowShell shell,
            RollingFileLogger logger)
        {
            _agent = agent;
            _settings = settings;
            _explorer = explorer;
            _chat = chat;
            _shell = shell;
            _logger = logger;
            _provider = settings.Current.AiProvider;

            _agent.FileWritten += OnFileWritten;
            _chat.OpenSettingsRequested += OnOpenSettingsRequested;
            _settings.Changed += OnSettingsChanged;
        }

        /// <inheritdoc />
        public void Dispose()
        {
            _disposed = true;
            _agent.FileWritten -= OnFileWritten;
            _chat.OpenSettingsRequested -= OnOpenSettingsRequested;
            _settings.Changed -= OnSettingsChanged;
        }

        /// <summary>
        ///     Shows a file that the copilot wrote: the explorer reads the folder again and selects the file.
        /// </summary>
        /// <param name="sender">The copilot.</param>
        /// <param name="e">The project-relative path of the file.</param>
        private void OnFileWritten(object? sender, FileWrittenEventArgs e)
        {
            Post(() =>
            {
                _explorer.Refresh();
                if (_explorer.FolderPath is { } folder)
                {
                    _explorer.SelectFile(Path.GetFullPath(Path.Combine(folder, e.RelativePath)));
                }
            });
        }

        /// <summary>
        ///     Opens the settings dialog of the shell when the chat panel asks for the settings.
        /// </summary>
        /// <param name="sender">The chat panel.</param>
        /// <param name="e">Nothing.</param>
        private void OnOpenSettingsRequested(object? sender, EventArgs e)
        {
            Post(() => _ = _shell.OpenSettingsCommand.ExecuteAsync(null));
        }

        /// <summary>
        ///     Checks the provider again when the AI provider settings changed; other changes of the settings are not its business.
        /// </summary>
        /// <param name="sender">The settings service.</param>
        /// <param name="e">The settings now in effect.</param>
        private void OnSettingsChanged(object? sender, SettingsChangedEventArgs e)
        {
            var provider = e.Settings.AiProvider;
            if (provider == _provider)
            {
                return;
            }

            _provider = provider;
            Post(() => _ = _chat.RefreshReadinessAsync());
        }

        /// <summary>
        ///     Runs an action on the thread of the user interface, and logs a failure instead of letting it reach the caller.
        /// </summary>
        /// <param name="action">The action.</param>
        private void Post(Action action)
        {
            if (_context is null || SynchronizationContext.Current == _context)
            {
                Run(action);
            }
            else
            {
                _context.Post(_ => Run(action), null);
            }
        }

        /// <summary>
        ///     Runs an action unless the wiring was disposed, and logs a failure.
        /// </summary>
        /// <param name="action">The action.</param>
        private void Run(Action action)
        {
            if (_disposed)
            {
                return;
            }

            try
            {
                action();
            }
            catch (Exception ex) when (ex is not OutOfMemoryException)
            {
                _logger.Log(LogLevel.Warning, "The copilot's change could not be shown.", ex);
            }
        }
    }

    /// <summary>
    ///     Reads the style kits in the background and logs a failure, which leaves the list of kits empty until the next
    ///     change of the project or of the shared folders.
    /// </summary>
    /// <param name="catalog">The catalog.</param>
    /// <param name="logger">The application log.</param>
    /// <returns>A task that completes when the kits have been read or the failure was logged.</returns>
    private static async Task RefreshStyleKitsAsync(IStyleKitCatalog catalog, RollingFileLogger logger)
    {
        try
        {
            await Task.Run(() => catalog.RefreshAsync()).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            logger.Log(LogLevel.Warning, "The style kits could not be read at start.", ex);
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        Projects.CurrentChanged -= _projectChanged;
        Chat.Dispose();
        Annotation.Dispose();
        Composer.Dispose();
        Shell.Dispose();
        Preview.Dispose();
        Preview.DragSource?.Purge();
        foreach (var service in _ownedServices)
        {
            service.Dispose();
        }

        Logger.Log(LogLevel.Info, "Application stopped.");
    }
}
