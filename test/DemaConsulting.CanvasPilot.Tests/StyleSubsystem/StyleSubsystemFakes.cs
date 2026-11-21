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
