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

namespace DemaConsulting.CanvasPilot.StyleSubsystem;

/// <summary>
///     The style kit that the open project uses, found by the id that the project file names through the catalog of kits,
///     and the export sizes that go with it. It is the supplier of sizes for the export dialog of the Preview subsystem.
/// </summary>
/// <remarks>
///     <para>
///         The unit keeps no copy of the choice: every question is answered from the open project and the catalog as they
///         are, so the answer is never stale; it only announces when an answer may have changed. It listens to the
///         project service and to the catalog until it is disposed.
///     </para>
///     <para>
///         The export sizes are the sizes of the kit, then the sizes of the application that the kit does not rename. A
///         size that is not transparent is flattened over the background of the kit, or over white when there is no kit.
///     </para>
/// </remarks>
public sealed class StyleSelection : IActiveStyle, IExportPresetSource, IDisposable
{
    /// <summary>
    ///     The background that a size that is not transparent has when there is no kit, or the kit has no background.
    /// </summary>
    private const string DefaultBackground = "#FFFFFF";

    /// <summary>
    ///     The role in a palette that names the background.
    /// </summary>
    private const string BackgroundRole = "background";

    /// <summary>
    ///     The sizes that the application offers whatever the style.
    /// </summary>
    private static readonly IReadOnlyList<ExportPreset> ApplicationPresets =
    [
        new("app:powerpoint-16-9", "PowerPoint 16:9", 1920, 1080, null, false, DefaultBackground, ExportPresetOrigin.Application),
        new("app:email-800", "Email (800 wide)", 800, null, null, false, DefaultBackground, ExportPresetOrigin.Application),
        new("app:a4-portrait-150", "A4 portrait @150 DPI", 1240, 1754, 150, false, DefaultBackground, ExportPresetOrigin.Application)
    ];

    /// <summary>
    ///     Says which project is open and keeps the choice in its file.
    /// </summary>
    private readonly IProjectService _projects;

    /// <summary>
    ///     Finds the kit for an id.
    /// </summary>
    private readonly IStyleKitCatalog _catalog;

    /// <summary>
    ///     Holds the default kit of the user.
    /// </summary>
    private readonly ISettingsService _settings;

    /// <summary>
    ///     Optional logger.
    /// </summary>
    private readonly RollingFileLogger? _logger;

    /// <summary>
    ///     How many times <see cref="Changed" /> has been raised, to tell whether a call raised it.
    /// </summary>
    private int _raised;

    /// <summary>
    ///     Initializes a new instance of the <see cref="StyleSelection" /> class.
    /// </summary>
    /// <param name="projects">Says which project is open and keeps the choice in its file.</param>
    /// <param name="catalog">Finds the kit for an id.</param>
    /// <param name="settings">Holds the default kit that a new project starts with.</param>
    /// <param name="logger">Optional logger for diagnostics.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="projects" />, <paramref name="catalog" />, or <paramref name="settings" /> is <see langword="null" />.</exception>
    public StyleSelection(IProjectService projects, IStyleKitCatalog catalog, ISettingsService settings, RollingFileLogger? logger = null)
    {
        ArgumentNullException.ThrowIfNull(projects);
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(settings);

        _projects = projects;
        _catalog = catalog;
        _settings = settings;
        _logger = logger;
        _projects.CurrentChanged += OnSourceChanged;
        _catalog.Changed += OnSourceChanged;
    }

    /// <inheritdoc />
    public event EventHandler? Changed;

    /// <summary>
    ///     Gets the sizes that the application offers whatever the style: a slide, an email, and a printed page.
    /// </summary>
    /// <remarks>
    ///     The background of each is white; <see cref="GetPresets" /> replaces it with the background of the kit.
    /// </remarks>
    public static IReadOnlyList<ExportPreset> AppPresets => ApplicationPresets;

    /// <inheritdoc />
    public string? ActiveId => _projects.Current?.Document.ActiveStyleKit;

    /// <inheritdoc />
    public StyleKitEntry? Active => ActiveId is { } id ? _catalog.Resolve(id) : null;

    /// <inheritdoc />
    public bool IsMissing => ActiveId is { } id && _catalog.Resolve(id) is null;

    /// <inheritdoc />
    public async Task SetActiveAsync(string? id)
    {
        // Write the choice through the project service, which refuses an id that no kit can have and a missing project
        var before = Volatile.Read(ref _raised);
        var result = await _projects.SetActiveStyleKitAsync(id).ConfigureAwait(false);
        if (!result.Succeeded)
        {
            Log(LogLevel.Warning, $"The style kit '{id ?? "(none)"}' could not be chosen: {result.Error}");
            throw new InvalidOperationException(result.Error);
        }

        // The project service announces a change; announce again when it did not (the same kit was chosen again), so a
        // caller can always rely on one announcement after a choice
        if (Volatile.Read(ref _raised) == before)
        {
            RaiseChanged();
        }
    }

    /// <summary>
    ///     Makes the kit that the user prefers the style of a project that has none yet. The shell calls it after it
    ///     created a project, so a project says which style it uses and a later change of the preference does not move it.
    /// </summary>
    /// <remarks>
    ///     Nothing happens when no project is open, when the project already has a style (even one that cannot be found),
    ///     when the user has no preferred kit, or when no kit has the id of the preferred kit.
    /// </remarks>
    /// <returns>A task that completes when the project file is written, or at once when nothing is to be done.</returns>
    public async Task ApplyDefaultToNewProjectAsync()
    {
        // A project that already says which style it uses keeps it
        var project = _projects.Current;
        if (project is null || project.Document.ActiveStyleKit is not null)
        {
            return;
        }

        // Only a kit that can be found is a default worth writing into a project
        var defaultId = _settings.Current.Styles.DefaultKitId;
        if (string.IsNullOrEmpty(defaultId) || _catalog.Resolve(defaultId) is null)
        {
            return;
        }

        try
        {
            await SetActiveAsync(defaultId).ConfigureAwait(false);
        }
        catch (InvalidOperationException ex)
        {
            // The project file could not be written; the project simply starts with no style
            Log(LogLevel.Warning, $"The default style kit '{defaultId}' was not applied to the new project: {ex.Message}");
        }
    }

    /// <inheritdoc />
    public IReadOnlyList<ExportPreset> GetPresets()
    {
        var kit = Active?.Kit;
        var background = kit is not null && kit.Palette.TryGetValue(BackgroundRole, out var color) ? color : DefaultBackground;

        // The sizes of the kit come first
        var presets = new List<ExportPreset>();
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (kit is not null)
        {
            foreach (var size in kit.SizePresets)
            {
                names.Add(size.Name);
                presets.Add(new ExportPreset(
                    $"kit:{kit.Id}:{size.Name.ToLowerInvariant()}",
                    size.Name,
                    size.Width,
                    size.Height,
                    size.Dpi,
                    size.Transparent,
                    size.Transparent ? null : background,
                    ExportPresetOrigin.StyleKit));
            }
        }

        // Then the sizes of the application that the kit has not renamed, over the background of the kit
        foreach (var size in ApplicationPresets.Where(size => !names.Contains(size.Name)))
        {
            presets.Add(size with { BackgroundHex = background });
        }

        return presets;
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _projects.CurrentChanged -= OnSourceChanged;
        _catalog.Changed -= OnSourceChanged;
    }

    /// <summary>
    ///     Announces that the style may have changed because the project or the kits did.
    /// </summary>
    /// <param name="sender">The project service or the catalog.</param>
    /// <param name="e">The event data.</param>
    private void OnSourceChanged(object? sender, EventArgs e)
    {
        RaiseChanged();
    }

    /// <summary>
    ///     Raises <see cref="Changed" />, counting it.
    /// </summary>
    private void RaiseChanged()
    {
        Interlocked.Increment(ref _raised);
        Changed?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    ///     Writes a diagnostic entry when a logger is available. Failing to log never blocks the caller.
    /// </summary>
    /// <param name="level">Severity of the event.</param>
    /// <param name="message">Event text.</param>
    private void Log(LogLevel level, string message)
    {
        try
        {
            _logger?.Log(level, message);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ObjectDisposedException)
        {
            // Logging is best effort
        }
    }
}
