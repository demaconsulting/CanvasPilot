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

using DemaConsulting.CanvasPilot.PreviewSubsystem;
using DemaConsulting.CanvasPilot.ProjectSubsystem;
using DemaConsulting.CanvasPilot.SettingsSubsystem;
using DemaConsulting.CanvasPilot.StyleSubsystem;
using NSubstitute;

namespace DemaConsulting.CanvasPilot.Tests;

/// <summary>
///     Builds the places that hold style kits in a temporary folder, with a project service and a settings service, for the
///     tests of the units of the Style subsystem that find and manage kits.
/// </summary>
public sealed class StyleKitPlaces : IDisposable
{
    /// <summary>
    ///     The project service, whose open project a test sets.
    /// </summary>
    private readonly IProjectService _projects = Substitute.For<IProjectService>();

    /// <summary>
    ///     Initializes a new instance of the <see cref="StyleKitPlaces" /> class with empty places.
    /// </summary>
    public StyleKitPlaces()
    {
        Root = Directory.CreateTempSubdirectory("canvaspilot-style-places-").FullName;
        BuiltIn = Directory.CreateDirectory(Path.Combine(Root, "builtin")).FullName;
        User = Path.Combine(Root, "user");
        Shared1 = Path.Combine(Root, "shared1");
        Shared2 = Path.Combine(Root, "shared2");
        ProjectFolder = Directory.CreateDirectory(Path.Combine(Root, "project")).FullName;
        Settings = new SettingsService(Path.Combine(Root, "settings"));
        Settings.Load();
    }

    /// <summary>
    ///     Gets the temporary folder that holds everything.
    /// </summary>
    public string Root { get; }

    /// <summary>
    ///     Gets the folder of the kits that come with the program.
    /// </summary>
    public string BuiltIn { get; }

    /// <summary>
    ///     Gets the folder of the kits of the user, which does not exist until a test writes to it.
    /// </summary>
    public string User { get; }

    /// <summary>
    ///     Gets the first shared folder, which does not exist until a test writes to it.
    /// </summary>
    public string Shared1 { get; }

    /// <summary>
    ///     Gets the second shared folder, which does not exist until a test writes to it.
    /// </summary>
    public string Shared2 { get; }

    /// <summary>
    ///     Gets the folder of the project that a test may open.
    /// </summary>
    public string ProjectFolder { get; }

    /// <summary>
    ///     Gets the folder of the kits of the project.
    /// </summary>
    public string ProjectStyles => Path.Combine(ProjectFolder, "styles");

    /// <summary>
    ///     Gets the settings service, over a folder of its own.
    /// </summary>
    public SettingsService Settings { get; }

    /// <summary>
    ///     Gets or sets a catalog that a test made over these places and that is disposed with them, or
    ///     <see langword="null" /> when the test made none.
    /// </summary>
    public StyleKitCatalog? Catalog { get; set; }

    /// <summary>
    ///     Gets the project service.
    /// </summary>
    public IProjectService Projects => _projects;

    /// <summary>
    ///     Makes the project open and announces the change.
    /// </summary>
    public void OpenProject()
    {
        var file = Path.Combine(ProjectFolder, "P.canvaspilot");
        _projects.Current.Returns(new ProjectInfo("P", ProjectFolder, file, new ProjectDocument { Name = "P" }));
        _projects.CurrentChanged += Raise.Event<EventHandler>(_projects, EventArgs.Empty);
    }

    /// <summary>
    ///     Closes the project and announces the change.
    /// </summary>
    public void CloseProject()
    {
        _projects.Current.Returns((ProjectInfo?)null);
        _projects.CurrentChanged += Raise.Event<EventHandler>(_projects, EventArgs.Empty);
    }

    /// <summary>
    ///     Sets the shared folders in the settings, which announces the change.
    /// </summary>
    /// <param name="folders">The folders, in order.</param>
    public void SetSharedFolders(params string[] folders)
    {
        Settings.Update(s => s with { Styles = s.Styles with { SharedKitFolders = folders } });
    }

    /// <summary>
    ///     Creates a catalog over these places.
    /// </summary>
    /// <param name="timeout">How long a shared folder is given to answer; 5 seconds by default.</param>
    /// <returns>The catalog.</returns>
    public StyleKitCatalog CreateCatalog(TimeSpan? timeout = null) =>
        new(_projects, Settings, BuiltIn, User, timeout ?? TimeSpan.FromSeconds(5));

    /// <summary>
    ///     Creates a catalog over these places whose folder lister is replaced.
    /// </summary>
    /// <param name="timeout">How long a shared folder is given to answer.</param>
    /// <param name="lister">Lists the kit folders of a folder.</param>
    /// <returns>The catalog.</returns>
    internal StyleKitCatalog CreateCatalog(TimeSpan timeout, Func<string, IReadOnlyList<string>> lister) =>
        new(_projects, Settings, BuiltIn, User, timeout, null, lister);

    /// <summary>
    ///     Makes a small valid kit.
    /// </summary>
    /// <param name="id">The id.</param>
    /// <param name="description">The description, which tests use to tell copies of an id apart.</param>
    /// <param name="name">The name, or <see langword="null" /> for the id.</param>
    /// <returns>The kit.</returns>
    public static StyleKit Kit(string id, string description = "", string? name = null) =>
        new(
            id,
            name ?? id,
            description,
            new Dictionary<string, string> { ["background"] = "#FFFFFF", ["text"] = "#0F172A", ["primary"] = "#2563EB" },
            new StyleFonts("sans-serif", "sans-serif", "monospace"),
            2,
            8,
            16,
            null,
            null,
            new StyleGuidanceText(string.Empty, [], []),
            [],
            []);

    /// <summary>
    ///     Writes a kit as a folder named as its id in a place, with example files of the given names.
    /// </summary>
    /// <param name="root">The place.</param>
    /// <param name="kit">The kit.</param>
    /// <param name="examples">The names of the example files to create; each holds its own name as text.</param>
    /// <returns>The full path of the kit folder.</returns>
    public static string WriteKit(string root, StyleKit kit, params string[] examples)
    {
        var folder = Directory.CreateDirectory(Path.Combine(root, kit.Id)).FullName;
        File.WriteAllText(Path.Combine(folder, "style.json"), StyleKitSerializer.Write(kit));
        if (examples.Length > 0)
        {
            var examplesFolder = Directory.CreateDirectory(Path.Combine(folder, "examples")).FullName;
            foreach (var example in examples)
            {
                File.WriteAllText(Path.Combine(examplesFolder, example), example);
            }
        }

        return folder;
    }

    /// <inheritdoc />
    public void Dispose()
    {
        Catalog?.Dispose();
        if (Directory.Exists(Root))
        {
            Directory.Delete(Root, recursive: true);
        }
    }
}

/// <summary>
///     Hand-written stand-in for the style that the open project uses, for the tests of the units that read it: the test
///     sets the kit, the id, and whether the kit is missing, and the stand-in announces the change.
/// </summary>
public sealed class FakeActiveStyle : IActiveStyle
{
    /// <inheritdoc />
    public event EventHandler? Changed;

    /// <inheritdoc />
    public StyleKitEntry? Active { get; private set; }

    /// <inheritdoc />
    public string? ActiveId { get; private set; }

    /// <inheritdoc />
    public bool IsMissing { get; private set; }

    /// <summary>
    ///     Makes a kit the active one and announces the change.
    /// </summary>
    /// <param name="entry">The kit, or <see langword="null" /> for none.</param>
    public void Use(StyleKitEntry? entry)
    {
        Active = entry;
        ActiveId = entry?.Kit.Id;
        IsMissing = false;
        Changed?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    ///     Makes the project name a kit that cannot be found and announces the change.
    /// </summary>
    /// <param name="id">The id that the project names.</param>
    public void UseMissing(string id)
    {
        Active = null;
        ActiveId = id;
        IsMissing = true;
        Changed?.Invoke(this, EventArgs.Empty);
    }

    /// <inheritdoc />
    public Task SetActiveAsync(string? id)
    {
        ActiveId = id;
        return Task.CompletedTask;
    }
}

/// <summary>
///     Hand-written windows and pickers of the Style Kits window for tests: it records what was asked for and answers as the
///     test says, so no window and no platform picker is needed.
/// </summary>
public sealed class FakeStyleKitDialogs : IStyleKitDialogs
{
    /// <summary>
    ///     Gets the windows of the manager that were shown.
    /// </summary>
    public List<StyleKitManagerViewModel> Managers { get; } = [];

    /// <summary>
    ///     Gets the editors that were shown, in order.
    /// </summary>
    public List<StyleKitEditorViewModel> Editors { get; } = [];

    /// <summary>
    ///     Gets or sets what the user does in the editor; when <see langword="null" />, the user cancels.
    /// </summary>
    public Action<StyleKitEditorViewModel>? OnEditor { get; set; }

    /// <summary>
    ///     Gets or sets the zip file that the user chooses to import, or <see langword="null" /> for cancel.
    /// </summary>
    public string? ZipToImport { get; set; }

    /// <summary>
    ///     Gets or sets the zip file that the user chooses to write, or <see langword="null" /> for cancel.
    /// </summary>
    public string? ZipToExport { get; set; }

    /// <summary>
    ///     Gets the file names that were suggested for an export.
    /// </summary>
    public List<string> SuggestedZipNames { get; } = [];

    /// <summary>
    ///     Gets or sets the example files that the user chooses.
    /// </summary>
    public IReadOnlyList<string> ExampleFiles { get; set; } = [];

    /// <summary>
    ///     Gets or sets what the user answers to a confirmation, by the label of the button; a label that is not in the
    ///     dictionary is answered with <see cref="ConfirmDefault" />.
    /// </summary>
    public Dictionary<string, bool> ConfirmAnswers { get; } = [];

    /// <summary>
    ///     Gets or sets what the user answers to a confirmation that has no answer in <see cref="ConfirmAnswers" />.
    /// </summary>
    public bool ConfirmDefault { get; set; }

    /// <summary>
    ///     Gets or sets the text that the user types in a prompt, or <see langword="null" /> for cancel.
    /// </summary>
    public string? PromptResult { get; set; }

    /// <summary>
    ///     Gets the confirmations that were asked, as title, message, and the label of the button.
    /// </summary>
    public List<(string Title, string Message, string ConfirmText)> Confirmations { get; } = [];

    /// <summary>
    ///     Gets the prompts that were asked, as title, message, and the text to start with.
    /// </summary>
    public List<(string Title, string Message, string InitialText)> Prompts { get; } = [];

    /// <inheritdoc />
    public Task ShowStyleKitManagerAsync(StyleKitManagerViewModel viewModel)
    {
        Managers.Add(viewModel);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task ShowStyleKitEditorAsync(StyleKitEditorViewModel viewModel)
    {
        Editors.Add(viewModel);
        if (OnEditor is null)
        {
            viewModel.CancelCommand.Execute(null);
        }
        else
        {
            OnEditor(viewModel);
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task<string?> PickZipToImportAsync(string? initialFolder) => Task.FromResult(ZipToImport);

    /// <inheritdoc />
    public Task<string?> PickZipToExportAsync(string suggestedFileName)
    {
        SuggestedZipNames.Add(suggestedFileName);
        return Task.FromResult(ZipToExport);
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<string>> PickExampleFilesAsync(string? initialFolder) => Task.FromResult(ExampleFiles);

    /// <inheritdoc />
    public Task<bool> ConfirmAsync(string title, string message, string confirmText)
    {
        Confirmations.Add((title, message, confirmText));
        return Task.FromResult(ConfirmAnswers.TryGetValue(confirmText, out var answer) ? answer : ConfirmDefault);
    }

    /// <inheritdoc />
    public Task<string?> PromptAsync(string title, string message, string initialText, string confirmText)
    {
        Prompts.Add((title, message, initialText));
        return Task.FromResult(PromptResult);
    }
}

/// <summary>
///     Hand-written render service for the pictures of the examples: every render completes at once with a small picture,
///     fails for the files whose names the test lists, and is recorded.
/// </summary>
public sealed class RecordingRenderService : IImageRenderService
{
    /// <summary>
    ///     Holds the renders that were requested, which may come from several threads.
    /// </summary>
    private readonly List<(string File, string Folder, RenderSettings? Settings)> _requests = [];

    /// <summary>
    ///     Gets the names of the files that fail to render.
    /// </summary>
    public HashSet<string> FailingFiles { get; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    ///     Gets or sets a task that every render waits for before it completes, or <see langword="null" /> for none.
    /// </summary>
    public Task? Gate { get; set; }

    /// <summary>
    ///     Gets the renders that were requested, in order.
    /// </summary>
    public IReadOnlyList<(string File, string Folder, RenderSettings? Settings)> Requests
    {
        get
        {
            lock (_requests)
            {
                return _requests.ToArray();
            }
        }
    }

    /// <inheritdoc />
    public Task<RenderResult> RenderAsync(string filePath, string projectFolder, CancellationToken cancellationToken) =>
        RenderAsync(filePath, projectFolder, null, cancellationToken);

    /// <inheritdoc />
    public async Task<RenderResult> RenderAsync(string filePath, string projectFolder, RenderSettings? settings, CancellationToken cancellationToken)
    {
        lock (_requests)
        {
            _requests.Add((filePath, projectFolder, settings));
        }

        if (Gate is { } gate)
        {
            await gate.WaitAsync(cancellationToken);
        }

        return FailingFiles.Contains(Path.GetFileName(filePath))
            ? RenderResult.Failure(new RenderError(RenderErrorCodes.CannotDraw, $"{Path.GetFileName(filePath)} has a syntax error."))
            : RenderResult.Success(TestImages.Create(filePath, 24, 12));
    }

    /// <inheritdoc />
    public byte[] EncodePng(RenderedImage image) => [];

    /// <inheritdoc />
    public byte[] EncodePng(RenderedImage image, double? dpi) => [];
}
