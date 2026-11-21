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
///     service, recent projects, explorer, workspace, shell) and holds them for the application's lifetime.
///     The application creates it at startup and disposes it on shutdown; system tests create it with
///     temporary folders and no window.
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
    /// <param name="stylePicker">The list in the status bar with which the user chooses the style kit.</param>
    /// <param name="styleCheck">What the Check Against Style item of the Image menu runs.</param>
    /// <param name="styleTools">The descriptors of the tools that give and check the style, for an assistant.</param>
    /// <param name="ownedServices">The services the composition created and must dispose.</param>
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
        IReadOnlyList<IDisposable> ownedServices)
    {
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
        _ownedServices = ownedServices;

        // The message is written in the open project: follow it, and start with the one that is open now
        _projectChanged = (_, _) => Composer.SetProject(Projects.Current?.FolderPath);
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
    ///     Gets the style kit that the open project uses, and the means to change it.
    /// </summary>
    public IActiveStyle ActiveStyle { get; }

    /// <summary>
    ///     Gets the list in the status bar of the main window with which the user chooses the style kit of the open project.
    /// </summary>
    public StylePickerViewModel StylePicker { get; }

    /// <summary>
    ///     Gets what the Check Against Style item of the Image menu runs: the check of the shown picture against the style
    ///     kit of the open project.
    /// </summary>
    public ICheckAgainstStyle StyleCheck { get; }

    /// <summary>
    ///     Gets the descriptors of the two tools that give the style (<c>style_get</c>) and check a picture against it
    ///     (<c>style_check</c>), in the form of the descriptors of the drawing knowledge, to be given to an assistant
    ///     together with them when one is added. Nothing in the application calls them yet.
    /// </summary>
    public IReadOnlyList<KnowledgeTool> StyleToolDescriptors { get; }

    /// <summary>
    ///     Creates the units for the running application: the log under the per-user local application data
    ///     folder, the settings under the per-user application data folder, the Documents folder as the place
    ///     offered for new projects, and the dispatcher-based refresh scheduler.
    /// </summary>
    /// <returns>The composition, with the settings already loaded.</returns>
    public static ApplicationComposition CreateDefault()
    {
        var logger = RollingFileLogger.CreateDefault();
        var settings = SettingsService.CreateDefault(logger);
        var documents = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        return Create(
            logger,
            settings,
            new DispatcherRefreshScheduler(),
            defaultProjectsFolder: string.IsNullOrWhiteSpace(documents) ? null : documents,
            dragFolder: ImageDragSource.DefaultFolder);
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
        string? styleUserFolder = null)
    {
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(scheduler);

        settings.Load();

        var projects = new ProjectService(logger);
        var recentProjects = new RecentProjects(settings);
        var explorer = new ProjectExplorerViewModel(new CanvasNetImageSourceFilter(), scheduler, logger: logger);
        var renderer = renderService ?? new ImageRenderService(logger);
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
        _ = RefreshStyleKitsAsync(styleCatalog, logger);

        var exporter = imageExporter ?? new ImageExporter(preview, renderer, logger, styleSelection);

        // The speech services are shared by the Speech settings page and by dictation. They touch the model store
        // and the audio system only when they are first used, so creating them costs nothing at startup; the ones
        // created here are the composition's to dispose, the dictation service before the model service it uses.
        var owned = new List<IDisposable>();
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

        // The Style Kits page browses for folders through the dialog service of the main window, which the shell has only
        // once the window attaches itself; the page asks the shell when the user asks to browse
        MainWindowShell? pickerOwner = null;
        IReadOnlyList<ISettingsPage> pages = settingsPages ??
        [
            new AiProviderSettingsPageViewModel(),
            new SpeechSettingsPageViewModel(audioDevices, speechModels),
            new StyleSettingsPageViewModel(styleCatalog, () => pickerOwner?.FolderPicker),
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
            logger);
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
        // marks whose chips the message holds
        var chat = new ChatPanelViewModel(composer, marks, annotation, markImages, dictation, logger);

        // The chat panel is the right-hand column of the workspace, which shows it while a project is open, and it is
        // what the talk key of the window listens for
        workspace.Chat = chat;
        shell.PushToTalk = chat;

        logger.Log(LogLevel.Info, "Application started.");
        return new ApplicationComposition(
            logger, settings, projects, recentProjects, preview, shell, dictation, composer, marks, annotation, chat,
            styleCatalog, styleSelection, stylePicker, styleCheck, styleTools, owned);
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
