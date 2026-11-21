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

using System.Text.Json;
using DemaConsulting.CanvasPilot.ProjectSubsystem;
using DemaConsulting.CanvasPilot.SettingsSubsystem;
using DemaConsulting.CanvasPilot.StyleSubsystem;

namespace DemaConsulting.CanvasPilot.Tests;

/// <summary>
///     Subsystem tests for the Style subsystem: the units of the subsystem working together on temporary folders, and
///     the rule that the subsystem depends on no language model or agent library.
/// </summary>
public sealed class StyleSubsystemTests : IDisposable
{
    /// <summary>
    ///     The names of packages of language models and agent libraries that the Style subsystem must not depend on.
    /// </summary>
    private static readonly string[] LanguageModelPackageNames =
    [
        "Microsoft.Copilot.SDK",
        "GitHub.Copilot.SDK",
        "OllamaSharp",
        "Microsoft.Extensions.AI",
        "AgentKit",
        "Microsoft.SemanticKernel",
        "OpenAI",
        "Azure.AI",
        "Anthropic",
        "LLamaSharp"
    ];

    /// <summary>
    ///     Temporary folder removed on disposal.
    /// </summary>
    private readonly string _root = Directory.CreateTempSubdirectory("canvaspilot-style-").FullName;

    /// <inheritdoc />
    public void Dispose()
    {
        // A refresh that a project change started may still be reading the files; try again for a moment
        for (var attempt = 0; attempt < 40 && Directory.Exists(_root); attempt++)
        {
            try
            {
                Directory.Delete(_root, recursive: true);
            }
            catch (IOException)
            {
                Thread.Sleep(50);
            }
        }
    }

    /// <summary>
    ///     Validates that a kit that is built in memory, checked, written to a folder with its example files, and read
    ///     back from that folder is the same kit, with its examples found and its warnings reported: the format that the
    ///     other units of the subsystem share.
    /// </summary>
    [Fact]
    public void StyleSubsystem_DefineKits_WrittenKitFolder_IsReadBackWithItsExamplesAndWarnings()
    {
        // Arrange: a kit with a font that is not bundled, and a folder named as the kit with two example files
        var kit = new StyleKit(
            "team-look",
            "Team Look",
            "The look of the team.",
            new Dictionary<string, string> { ["background"] = "#FFFFFF", ["text"] = "#111827", ["primary"] = "#0EA5E9", ["highlight"] = "#F43F5E" },
            new StyleFonts("Inter, sans-serif", "sans-serif", "monospace"),
            2,
            8,
            12,
            new StyleMermaid("base", new Dictionary<string, string> { ["primaryColor"] = "#0EA5E9" }),
            """{"range":{"category":["#0EA5E9"]}}""",
            new StyleGuidanceText("Keep margins wide.", ["Use the palette."], ["Use shadows."]),
            [new StyleSizePreset("Slide", 1920, 1080, null, false)],
            [new StyleExample("examples/logo.svg", string.Empty, "The logo.")]);
        var folder = Directory.CreateDirectory(Path.Combine(_root, "styles", "team-look")).FullName;
        Directory.CreateDirectory(Path.Combine(folder, "examples"));
        File.WriteAllText(Path.Combine(folder, "examples", "logo.svg"), "<svg xmlns='http://www.w3.org/2000/svg'/>");
        File.WriteAllText(Path.Combine(folder, "examples", "chart.vl.json"), "{}");

        // Act: check the kit, write it, and read the folder
        var problems = StyleKitSerializer.Validate(kit);
        File.WriteAllText(Path.Combine(folder, StyleKitSerializer.FileName), StyleKitSerializer.Write(kit));
        var result = StyleKitSerializer.ReadFolder(folder);

        // Assert: one warning about the font and none about anything else, no error, and the kit read back whole
        Assert.Equal(["fonts.heading"], problems.Select(p => p.Path));
        Assert.True(result.Succeeded, string.Join("; ", result.Errors));
        Assert.Equal(["fonts.heading"], result.Warnings.Select(w => w.Path));
        var read = result.Kit!;
        Assert.Equal(StyleKitSerializer.Write(kit), StyleKitSerializer.Write(read));
        Assert.Equal(["examples/chart.vl.json", "examples/logo.svg"], read.Examples.Select(e => e.RelativePath));
        Assert.Equal(["#FFFFFF", "#111827", "#0EA5E9", "#F43F5E"], read.Palette.Values);
        Assert.Equal("The logo.", read.Examples.Single(e => e.RelativePath == "examples/logo.svg").Caption);
        Assert.True(File.Exists(read.Examples[0].FullPath));
    }

    /// <summary>
    ///     Validates that the kits of every place are found with the real project service and settings service: the kit of
    ///     the project wins over the kit of the user, which wins over the shared and built-in copies; a shared folder that
    ///     is missing is reported and does not stop the others; a copy into the project freezes a shared kit; and the
    ///     choice and the folders survive a refresh by a fresh catalog.
    /// </summary>
    [Fact]
    public async Task StyleSubsystem_FindKitsInLocations_AllPlaces_ResolveByPrecedenceAndSurviveAnUnreachableFolder()
    {
        // Arrange: a real project, real settings with a missing and a real shared folder, and kits in the places
        var ct = TestContext.Current.CancellationToken;
        Directory.CreateDirectory(Path.Combine(_root, "projects"));
        var projects = new ProjectService();
        var project = (await projects.CreateAsync(Path.Combine(_root, "projects"), "Look", ct)).Project!;
        var builtIn = Directory.CreateDirectory(Path.Combine(_root, "builtin")).FullName;
        var user = Path.Combine(_root, "user");
        var shared = Path.Combine(_root, "shared");
        var offline = Path.Combine(_root, "offline");
        var settings = new SettingsService(Path.Combine(_root, "settings"));
        settings.Save(new AppSettings { Styles = new StyleSettings { SharedKitFolders = [offline, shared] } });
        WriteKit(builtIn, "clean", "program");
        WriteKit(shared, "clean", "team");
        WriteKit(shared, "team-only", "team");
        WriteKit(user, "clean", "mine");
        WriteKit(Path.Combine(project.FolderPath, "styles"), "frozen", "project");
        using var catalog = new StyleKitCatalog(projects, settings, builtIn, user, TimeSpan.FromSeconds(5));

        // Act: read, then freeze the shared kit in the project, and read with a second catalog as after a restart
        await catalog.RefreshAsync(ct);
        var beforeFreeze = catalog.Resolve("clean")!.Kit.Description;
        var frozen = catalog.CopyIntoProject(catalog.Entries.Single(e => e.Location == StyleKitLocation.Shared && e.Kit.Id == "team-only"));
        using var restarted = new StyleKitCatalog(projects, settings, builtIn, user, TimeSpan.FromSeconds(5));
        await restarted.RefreshAsync(ct);

        // Assert: the user's copy wins over the team's and the program's, the offline folder is reported and the other
        // places are still read, and the copy in the project is the kit of its id
        Assert.Equal("mine", beforeFreeze);
        Assert.Equal(["clean", "clean", "clean", "frozen", "team-only", "team-only"], restarted.Entries.Select(e => e.Kit.Id).Order());
        Assert.Equal(StyleKitLocation.Project, restarted.Resolve("team-only")!.Location);
        Assert.Equal(StyleKitLocation.Project, frozen.Location);
        Assert.Equal(["clean", "clean", "team-only"], restarted.Entries.Where(e => e.IsOverridden).Select(e => e.Kit.Id).Order());
        var unavailable = Assert.Single(restarted.Locations, l => !l.IsAvailable);
        Assert.Equal(offline, unavailable.Path);
        Assert.Equal(5, restarted.Locations.Count);
    }

    /// <summary>
    ///     Validates that the default kit of the user becomes the style of a new project, that the choice is kept in the
    ///     project file, that another project service that opens the file resolves the same kit through a new catalog, and
    ///     that a kit that cannot be found is reported as missing.
    /// </summary>
    [Fact]
    public async Task StyleSubsystem_ChooseActiveKit_DefaultAndChoice_AreKeptInTheProjectFileAndSurviveReopening()
    {
        // Arrange: a kit of the user that is the default, real settings, and a new project
        var ct = TestContext.Current.CancellationToken;
        Directory.CreateDirectory(Path.Combine(_root, "projects"));
        var user = Path.Combine(_root, "user");
        WriteKit(user, "team-look", "mine");
        var settings = new SettingsService(Path.Combine(_root, "settings"));
        settings.Save(new AppSettings { Styles = new StyleSettings { DefaultKitId = "team-look" } });
        var projects = new ProjectService();
        using var catalog = new StyleKitCatalog(projects, settings, Path.Combine(_root, "builtin"), user, TimeSpan.FromSeconds(5));
        using var selection = new StyleSelection(projects, catalog, settings);
        await catalog.RefreshAsync(ct);
        var project = (await projects.CreateAsync(Path.Combine(_root, "projects"), "Look", ct)).Project!;
        await catalog.RefreshAsync(ct);

        // Act: apply the default, read the file, open the file with a second set of services, and then name a kit that
        // does not exist
        await selection.ApplyDefaultToNewProjectAsync();
        using var json = JsonDocument.Parse(await File.ReadAllTextAsync(project.FilePath, ct));
        var secondProjects = new ProjectService();
        using var secondCatalog = new StyleKitCatalog(secondProjects, settings, Path.Combine(_root, "builtin"), user, TimeSpan.FromSeconds(5));
        using var second = new StyleSelection(secondProjects, secondCatalog, settings);
        await secondProjects.OpenAsync(project.FilePath, ct);
        await secondCatalog.RefreshAsync(ct);
        var reopened = second.Active?.Kit.Id;
        await second.SetActiveAsync("lost-kit");
        var missing = (second.Active, second.IsMissing, second.ActiveId);

        // Assert: the file named the default, the kit was resolved after reopening, and a lost kit is reported
        Assert.Equal("team-look", json.RootElement.GetProperty("activeStyleKit").GetString());
        Assert.Equal("team-look", reopened);
        Assert.Equal((null, true, "lost-kit"), missing);
    }

    /// <summary>
    ///     Validates that the sizes of the kit of the project are offered by the exporter of the Preview subsystem, ahead of
    ///     the sizes of the application, and that exporting at the size of the kit writes a PNG of exactly that size over
    ///     the background of the kit.
    /// </summary>
    [Fact]
    public async Task StyleSubsystem_OfferExportPresets_KitSizes_AreOfferedByTheExporterAndWriteAPngOfTheSize()
    {
        // Arrange: a real project with a vector picture, a kit of the user with a size and a dark background that the
        // project uses, and a shown picture
        var ct = TestContext.Current.CancellationToken;
        Directory.CreateDirectory(Path.Combine(_root, "projects"));
        var user = Path.Combine(_root, "user");
        var kit = new StyleKit(
            "slides",
            "Slides",
            string.Empty,
            new Dictionary<string, string> { ["background"] = "#102030", ["text"] = "#FFFFFF", ["primary"] = "#2563EB" },
            new StyleFonts("sans-serif", "sans-serif", "monospace"),
            2,
            8,
            16,
            null,
            null,
            new StyleGuidanceText(string.Empty, [], []),
            [new StyleSizePreset("Square", 320, 320, 96, false)],
            []);
        Directory.CreateDirectory(user);
        var folder = Directory.CreateDirectory(Path.Combine(user, "slides")).FullName;
        await File.WriteAllTextAsync(Path.Combine(folder, StyleKitSerializer.FileName), StyleKitSerializer.Write(kit), ct);
        var settings = new SettingsService(Path.Combine(_root, "settings"));
        var projects = new ProjectService();
        using var catalog = new StyleKitCatalog(projects, settings, Path.Combine(_root, "builtin"), user, TimeSpan.FromSeconds(5));
        using var selection = new StyleSelection(projects, catalog, settings);
        var project = (await projects.CreateAsync(Path.Combine(_root, "projects"), "Look", ct)).Project!;
        await catalog.RefreshAsync(ct);
        await selection.SetActiveAsync("slides");
        var svg = Path.Combine(project.FolderPath, "wide.svg");
        await File.WriteAllTextAsync(svg, "<svg xmlns='http://www.w3.org/2000/svg' width='40' height='20'><rect width='40' height='20' fill='#2563EB'/></svg>", ct);
        var renderer = new PreviewSubsystem.ImageRenderService();
        var source = new FakeRenderedImageSource
        {
            CurrentImage = (await renderer.RenderAsync(svg, project.FolderPath, ct)).Image,
            ProjectFolder = project.FolderPath
        };
        var exporter = new PreviewSubsystem.ImageExporter(source, renderer, null, selection);
        var dialogs = new FakeExportDialogs { PresetNameToPick = "Square", SaveFileToPick = Path.Combine(project.FolderPath, "square.png") };

        // Act: export
        await exporter.ExportPngAsync(dialogs);

        // Assert: the user was offered the picture as shown, the size of the kit, and then the sizes of the application,
        // and the file is a 320 x 320 PNG whose corner has the background of the kit
        var offered = Assert.Single(dialogs.PresetOffers).Presets.Select(p => p.Name);
        Assert.Equal(["As rendered", "Square", "PowerPoint 16:9", "Email (800 wide)", "A4 portrait @150 DPI"], offered);
        using var stream = File.OpenRead(Path.Combine(project.FolderPath, "square.png"));
        using var decoded = DemaConsulting.CanvasNet.Codecs.PngCodec.Load(stream);
        Assert.Equal((320, 320), (decoded.Width, decoded.Height));
        Assert.Equal(new DemaConsulting.CanvasNet.Canvas.Rgba32(16, 32, 48, 255), decoded[0, 0]);
        Assert.Empty(dialogs.Messages);
    }

    /// <summary>
    ///     Validates that a picture that is really drawn from a file is checked against the kit that the catalog finds: a
    ///     picture that uses only the colors of the kit passes, a picture with another color fails and names it with the
    ///     nearest role, and the notes of the check have the codes that the list of notes of a picture groups by.
    /// </summary>
    [Fact]
    public async Task StyleSubsystem_CheckPictures_RealRenderedPictures_PassOnAndFailOffThePalette()
    {
        // Arrange: a kit of the user, found through the catalog, and two SVG files drawn by the real render service
        var ct = TestContext.Current.CancellationToken;
        var user = Path.Combine(_root, "user");
        WriteKit(user, "checked", "mine");
        var settings = new SettingsService(Path.Combine(_root, "settings"));
        var projects = new ProjectService();
        using var catalog = new StyleKitCatalog(projects, settings, Path.Combine(_root, "builtin"), user, TimeSpan.FromSeconds(5));
        await catalog.RefreshAsync(ct);
        var kit = catalog.Resolve("checked")!.Kit;
        var folder = Directory.CreateDirectory(Path.Combine(_root, "pictures")).FullName;
        var good = Path.Combine(folder, "good.svg");
        var bad = Path.Combine(folder, "bad.svg");
        await File.WriteAllTextAsync(good, "<svg xmlns='http://www.w3.org/2000/svg' width='100' height='100'><rect width='100' height='100' fill='#FFFFFF'/><rect x='20' y='20' width='60' height='60' fill='#2563EB'/></svg>", ct);
        await File.WriteAllTextAsync(bad, "<svg xmlns='http://www.w3.org/2000/svg' width='100' height='100'><rect width='100' height='100' fill='#FFFFFF'/><rect x='0' y='0' width='50' height='100' fill='#D32F2F'/></svg>", ct);
        var renderer = new PreviewSubsystem.ImageRenderService();

        // Act: draw and check both
        var goodReport = StyleChecker.Check((await renderer.RenderAsync(good, folder, ct)).Image!, kit);
        var badReport = StyleChecker.Check((await renderer.RenderAsync(bad, folder, ct)).Image!, kit);

        // Assert: the first passes with the two palette colors, the second fails and names the red
        Assert.True(goodReport.Passed, goodReport.ToText("good.svg"));
        Assert.Equal(["background", "primary"], goodReport.OnPalette.Select(u => u.Role));
        Assert.False(badReport.Passed);
        Assert.Equal("#D32F2F", Assert.Single(badReport.OffPalette).Hex);
        Assert.Equal(0.5, badReport.OffPaletteShare, 2);
        Assert.Equal("style-summary", badReport.ToNotes()[0].Code);
        Assert.All(badReport.ToNotes(), n => Assert.StartsWith("style-", n.Code, StringComparison.Ordinal));
        Assert.Contains("Off the palette: #D32F2F", badReport.ToText("bad.svg"), StringComparison.Ordinal);
    }

    /// <summary>
    ///     Validates that the two tools over the real units give an assistant the style of the project: <c>style_get</c> gives
    ///     the block of the kit that the project file names, within the budget and naming its own tool; <c>style_check</c>
    ///     checks a picture of the project, fails a picture with another color and names it, and refuses a file outside
    ///     the project.
    /// </summary>
    [Fact]
    public async Task StyleSubsystem_GuideTheAssistant_ToolsOverRealUnits_GiveTheStyleAndCheckAPictureOfTheProject()
    {
        // Arrange: a real project that uses a kit of the user, with pictures in it and a picture outside it
        var ct = TestContext.Current.CancellationToken;
        Directory.CreateDirectory(Path.Combine(_root, "projects"));
        var user = Path.Combine(_root, "user");
        WriteKit(user, "guided", "mine");
        var settings = new SettingsService(Path.Combine(_root, "settings"));
        var projects = new ProjectService();
        using var catalog = new StyleKitCatalog(projects, settings, Path.Combine(_root, "builtin"), user, TimeSpan.FromSeconds(5));
        using var selection = new StyleSelection(projects, catalog, settings);
        var project = (await projects.CreateAsync(Path.Combine(_root, "projects"), "Look", ct)).Project!;
        await catalog.RefreshAsync(ct);
        await selection.SetActiveAsync("guided");
        await File.WriteAllTextAsync(Path.Combine(project.FolderPath, "off.svg"), "<svg xmlns='http://www.w3.org/2000/svg' width='40' height='40'><rect width='40' height='40' fill='#FFFFFF'/><rect width='20' height='40' fill='#D32F2F'/></svg>", ct);
        await File.WriteAllTextAsync(Path.Combine(_root, "outside.svg"), "<svg xmlns='http://www.w3.org/2000/svg' width='10' height='10'/>", ct);
        var tools = StyleTools.CreateAll(selection, catalog, () => projects.Current?.FolderPath, () => Path.Combine(project.FolderPath, "off.svg"), new PreviewSubsystem.ImageRenderService());

        // Act: ask for the style, check the shown picture, and try the picture outside the project
        var style = tools[0].Invoke("{}");
        var checkedPicture = tools[1].Invoke("{}");
        var outside = tools[1].Invoke("{\"path\":\"..\\\\outside.svg\"}");

        // Assert: the block is the guidance of the kit within the budget, the check fails the red, and the outside file is refused
        Assert.Equal(StyleGuidance.Build(catalog.Resolve("guided")!), style);
        Assert.True(style.Length <= StyleGuidance.BudgetCharacters);
        Assert.Contains(StyleTools.GetToolName, StyleGuidance.TruncationNote, StringComparison.Ordinal);
        Assert.Contains("Result: FAILED, 50.0% of the pixels are off the palette", checkedPicture, StringComparison.Ordinal);
        Assert.Contains("Off the palette: #D32F2F 50.0%", checkedPicture, StringComparison.Ordinal);
        Assert.StartsWith("Error: ", outside, StringComparison.Ordinal);
        Assert.Contains("is not a path inside the project folder", outside, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Writes a kit as a folder in a place.
    /// </summary>
    /// <param name="root">The place.</param>
    /// <param name="id">The id of the kit.</param>
    /// <param name="description">The description that tells copies apart.</param>
    private static void WriteKit(string root, string id, string description)
    {
        var kit = new StyleKit(
            id,
            id,
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
        var folder = Directory.CreateDirectory(Path.Combine(root, id)).FullName;
        File.WriteAllText(Path.Combine(folder, StyleKitSerializer.FileName), StyleKitSerializer.Write(kit));
    }

    /// <summary>
    ///     Validates that a kit that is exported to a zip file comes back as the same kit in another place, that a second
    ///     import with the same id is refused, and that a hostile archive with a name that climbs out of the target is
    ///     refused as a whole, with nothing created beside the target.
    /// </summary>
    [Fact]
    public void StyleSubsystem_ExchangeKitsAsZipFiles_ExportImportAndHostileArchive_MoveKitsAndChangeNothingOutside()
    {
        // Arrange: a kit in a folder of the user, a target folder inside a parent that holds nothing else
        var source = Directory.CreateDirectory(Path.Combine(_root, "source")).FullName;
        WriteKit(source, "shared-look", "The kit that travels.");
        var read = StyleKitSerializer.ReadFolder(Path.Combine(source, "shared-look"));
        var entry = new StyleKitEntry(read.Kit!, StyleKitLocation.User, Path.Combine(source, "shared-look"), null, false, false, null, []);
        var parent = Directory.CreateDirectory(Path.Combine(_root, "parent")).FullName;
        var target = Path.Combine(parent, "target");
        var zip = Path.Combine(_root, "shared-look.zip");
        var hostile = Path.Combine(_root, "hostile.zip");
        using (var stream = File.Create(hostile))
        using (var archive = new System.IO.Compression.ZipArchive(stream, System.IO.Compression.ZipArchiveMode.Create))
        {
            using var writer = new StreamWriter(archive.CreateEntry("../../escaped.txt").Open());
            writer.Write("escaped");
        }

        // Act: export, import, import again, and import the hostile archive
        StyleKitArchive.Export(entry, zip);
        var imported = StyleKitArchive.Import(zip, target, StyleImportConflict.Refuse);
        var again = StyleKitArchive.Import(zip, target, StyleImportConflict.Refuse);
        var refused = StyleKitArchive.Import(hostile, target, StyleImportConflict.Replace);

        // Assert: the kit arrived equal, the repeat and the hostile archive were refused, and nothing was created outside
        Assert.True(imported.Succeeded);
        Assert.Equal(StyleKitSerializer.Write(read.Kit!), StyleKitSerializer.Write(imported.Kit!));
        Assert.False(again.Succeeded);
        Assert.False(refused.Succeeded);
        Assert.Equal(["shared-look"], Directory.GetFileSystemEntries(target).Select(Path.GetFileName));
        Assert.False(File.Exists(Path.Combine(parent, "escaped.txt")));
        Assert.False(File.Exists(Path.Combine(_root, "escaped.txt")));
    }

    /// <summary>
    ///     Validates that the window that manages kits, the editor, the catalog, and the archive work together on temporary
    ///     folders: a kit is made, edited with an example image, drawn as a small picture, exported, deleted, and imported
    ///     again, and every step shows in the list of the window.
    /// </summary>
    [Fact]
    public async Task StyleSubsystem_ManageKitsInWindow_ManagerAndEditor_CreateEditExportImportAndDeleteAKit()
    {
        // Arrange: places that hold kits, a window over them, and a picture to use as an example
        using var places = new StyleKitPlaces();
        using var catalog = places.CreateCatalog();
        var dialogs = new FakeStyleKitDialogs();
        var renderer = new RecordingRenderService();
        await catalog.RefreshAsync(TestContext.Current.CancellationToken);
        using var manager = new StyleKitManagerViewModel(catalog, new FakeActiveStyle(), renderer, dialogs, places.Projects);
        var picture = Path.Combine(places.Root, "logo.svg");
        await File.WriteAllTextAsync(picture, "<svg xmlns='http://www.w3.org/2000/svg'/>", TestContext.Current.CancellationToken);
        var zip = Path.Combine(places.Root, "poster.zip");

        // Act: make a kit and, in the editor, change its name and palette and add the picture as an example with a caption
        dialogs.PromptResult = "Poster";
        dialogs.OnEditor = editor =>
        {
            editor.Name = "Poster Look";
            editor.AddColorCommand.Execute(null);
            editor.PaletteRows[^1].Hex = "#DB2777";
            editor.AddExampleFiles([picture]);
            editor.Examples[0].Caption = "The logo.";
            editor.SaveCommand.Execute(null);
        };
        await manager.NewCommand.ExecuteAsync(null);
        await manager.ThumbnailTask;
        var made = manager.SelectedItem!;

        // Act: export the kit, delete it, and import it again
        dialogs.ZipToExport = zip;
        await manager.ExportCommand.ExecuteAsync(null);
        dialogs.ConfirmDefault = true;
        await manager.DeleteCommand.ExecuteAsync(null);
        var deleted = !Directory.Exists(Path.Combine(places.User, "poster"));
        dialogs.ZipToImport = zip;
        await manager.ImportCommand.ExecuteAsync(null);
        await manager.ThumbnailTask;

        // Assert: the kit was made and edited, drawn, deleted, and is back with the example and the new color
        var back = manager.SelectedItem!;
        Assert.Equal("Poster Look", made.Name);
        Assert.Equal(["#DB2777"], made.Swatches.Where(swatch => swatch.Role == "secondary").Select(swatch => swatch.Hex));
        Assert.Equal(["logo.svg: The logo."], made.Thumbnails.Select(thumbnail => thumbnail.Label));
        Assert.True(made.Thumbnails[0].HasImage);
        Assert.Contains(renderer.Requests, request => Path.GetFileName(request.File) == "logo.svg" && request.Folder == made.Entry.FolderPath);
        Assert.True(deleted);
        Assert.Equal("poster", back.Id);
        Assert.Equal("Poster Look", back.Name);
        Assert.Equal(["The logo."], back.Entry.Kit.Examples.Select(example => example.Caption));
        Assert.True(File.Exists(Path.Combine(places.User, "poster", "examples", "logo.svg")));
        Assert.Equal(["Poster Look"], manager.Groups.Single(group => group.Location == StyleKitLocation.User).Items.Select(item => item.Name));
    }
    /// <summary>
    ///     Validates that the library of the subsystem refers to no package of a language model or agent, by its
    ///     referenced assemblies and by the packages that its dependency file lists.
    /// </summary>
    [Fact]
    public void StyleSubsystem_Assembly_ReferencesNoLanguageModelPackage()
    {
        // Arrange: the assembly of the application library, the packages that the application library and the test
        // project resolve (the dependency file of the test project lists the packages of the project it references), and
        // the project file of the application library
        var assembly = typeof(StyleKitSerializer).Assembly;
        var references = assembly.GetReferencedAssemblies().Select(a => a.Name ?? string.Empty).ToList();
        var dependencyFile = Path.Combine(AppContext.BaseDirectory, "DemaConsulting.CanvasPilot.Tests.deps.json");
        Assert.True(File.Exists(dependencyFile), "The dependency file of the test project is missing.");
        var projectFile = FindProjectFile();

        // Act
        using var document = JsonDocument.Parse(File.ReadAllText(dependencyFile));
        var packages = document.RootElement.GetProperty("libraries").EnumerateObject()
            .Select(p => p.Name)
            .ToList();
        var declared = File.ReadAllLines(projectFile)
            .Where(l => l.Contains("PackageReference", StringComparison.Ordinal))
            .ToList();

        // Assert: no referenced assembly, resolved package, or declared package is a language model or agent library
        Assert.NotEmpty(references);
        Assert.NotEmpty(packages);
        Assert.NotEmpty(declared);
        foreach (var name in LanguageModelPackageNames)
        {
            Assert.DoesNotContain(references, r => r.StartsWith(name, StringComparison.OrdinalIgnoreCase));
            Assert.DoesNotContain(packages, p => p.StartsWith(name + "/", StringComparison.OrdinalIgnoreCase));
            Assert.DoesNotContain(declared, l => l.Contains($"\"{name}", StringComparison.OrdinalIgnoreCase));
        }
    }

    /// <summary>
    ///     Finds the project file of the application library by walking up from the folder of the tests.
    /// </summary>
    /// <returns>The full path of the project file.</returns>
    private static string FindProjectFile()
    {
        var folder = new DirectoryInfo(AppContext.BaseDirectory);
        while (folder is not null)
        {
            var candidate = Path.Combine(folder.FullName, "src", "DemaConsulting.CanvasPilot", "DemaConsulting.CanvasPilot.csproj");
            if (File.Exists(candidate))
            {
                return candidate;
            }

            folder = folder.Parent;
        }

        throw new InvalidOperationException("The project file of the application library was not found.");
    }
}
