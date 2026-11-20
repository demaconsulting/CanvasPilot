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

using DemaConsulting.CanvasPilot.LoggingSubsystem;
using DemaConsulting.CanvasPilot.PreviewSubsystem;
using DemaConsulting.CanvasPilot.ProjectSubsystem;
using DemaConsulting.CanvasPilot.SettingsSubsystem;
using DemaConsulting.CanvasPilot.SpeechSubsystem;

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
    ///     The speech model service this composition created and must dispose, or <see langword="null" />.
    /// </summary>
    private readonly IDisposable? _ownedModelService;

    /// <summary>
    ///     Initializes a new instance of the <see cref="ApplicationComposition" /> class.
    /// </summary>
    /// <param name="logger">The application log.</param>
    /// <param name="settings">The settings service.</param>
    /// <param name="projects">The project service.</param>
    /// <param name="recentProjects">The recent projects list.</param>
    /// <param name="preview">The preview pane view model.</param>
    /// <param name="shell">The main window view model.</param>
    /// <param name="ownedModelService">The speech model service the composition created, or <see langword="null" />.</param>
    private ApplicationComposition(
        RollingFileLogger logger,
        ISettingsService settings,
        IProjectService projects,
        IRecentProjects recentProjects,
        PreviewPaneViewModel preview,
        MainWindowShell shell,
        IDisposable? ownedModelService)
    {
        Logger = logger;
        Settings = settings;
        Projects = projects;
        RecentProjects = recentProjects;
        Preview = preview;
        Shell = shell;
        _ownedModelService = ownedModelService;
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
            defaultProjectsFolder: string.IsNullOrWhiteSpace(documents) ? null : documents);
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
    ///     Lists the audio devices for the Speech settings page, or <see langword="null" /> for the service over the
    ///     speech library. Not used when <paramref name="settingsPages" /> is given.
    /// </param>
    /// <param name="speechModels">
    ///     Lists and downloads the speech models for the Speech settings page, or <see langword="null" /> for the
    ///     service over the speech library's shared model store, which the composition then disposes. Not used when
    ///     <paramref name="settingsPages" /> is given.
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
        ISpeechModelService? speechModels = null)
    {
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(scheduler);

        settings.Load();

        var projects = new ProjectService(logger);
        var recentProjects = new RecentProjects(settings);
        var explorer = new ProjectExplorerViewModel(new CanvasNetImageSourceFilter(), scheduler, logger: logger);
        var renderer = renderService ?? new ImageRenderService(logger);
        var preview = new PreviewPaneViewModel(renderer, explorer, scheduler, logger: logger);
        var workspace = new ProjectWorkspaceViewModel(explorer) { Preview = preview };
        var exporter = imageExporter ?? new ImageExporter(preview, renderer, logger);

        // The speech services are created only when the standard pages are, and the one created here is the
        // composition's to dispose; it touches the model store and the audio system only when the Speech page loads.
        IDisposable? ownedModelService = null;
        IReadOnlyList<ISettingsPage> pages;
        if (settingsPages is not null)
        {
            pages = settingsPages;
        }
        else
        {
            if (speechModels is null)
            {
                var modelService = new SpeechModelService(logger);
                ownedModelService = modelService;
                speechModels = modelService;
            }

            pages =
            [
                new AiProviderSettingsPageViewModel(),
                new SpeechSettingsPageViewModel(audioDevices ?? new AudioDeviceService(logger), speechModels),
            ];
        }

        var shell = new MainWindowShell(projects, recentProjects, settings, workspace, pages, exporter, defaultProjectsFolder);

        logger.Log(LogLevel.Info, "Application started.");
        return new ApplicationComposition(logger, settings, projects, recentProjects, preview, shell, ownedModelService);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        Shell.Dispose();
        Preview.Dispose();
        _ownedModelService?.Dispose();
        Logger.Log(LogLevel.Info, "Application stopped.");
    }
}
