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
using NSubstitute;

namespace DemaConsulting.CanvasPilot.Tests;

/// <summary>
///     Subsystem tests for the Project subsystem: the project service, the recent projects list, the New Project
///     dialog, and the project explorer working together on temporary folders.
/// </summary>
public sealed class ProjectSubsystemTests : IDisposable
{
    /// <summary>
    ///     Temporary root created fresh for each test and removed on disposal.
    /// </summary>
    private readonly string _tempRoot = Directory.CreateTempSubdirectory("canvaspilot-tests-").FullName;

    /// <summary>
    ///     The folder that holds the projects the tests create.
    /// </summary>
    private string Projects => Path.Combine(_tempRoot, "projects");

    /// <inheritdoc />
    public void Dispose()
    {
        if (Directory.Exists(_tempRoot))
        {
            Directory.Delete(_tempRoot, recursive: true);
        }
    }

    /// <summary>
    ///     Test that a project created through the New Project dialog exists on disk, can be saved, and is opened
    ///     again by a new service as after a restart.
    /// </summary>
    [Fact]
    public async Task ProjectSubsystem_ManageProjects_DialogCreateSaveReopen_RoundTrips()
    {
        // Arrange: the New Project dialog over a real service, with entries typed by the user
        Directory.CreateDirectory(Projects);
        var service = new ProjectService();
        var dialog = new NewProjectDialogViewModel(service, Substitute.For<IFolderPicker>(), Projects)
        {
            ProjectName = "Poster Set",
        };

        // Act: OK, create the project from the dialog values, save, and open it with a second service
        dialog.OkCommand.Execute(null);
        var created = await service.CreateAsync(dialog.ParentFolder, dialog.ProjectName, TestContext.Current.CancellationToken);
        var saved = await service.SaveAsync(TestContext.Current.CancellationToken);
        var second = new ProjectService();
        var reopened = await second.OpenAsync(created.Project!.FilePath, TestContext.Current.CancellationToken);

        // Assert: the dialog was accepted, the project is on disk and reopens with its creation data
        Assert.True(dialog.Accepted);
        Assert.True(created.Succeeded, created.Error);
        Assert.True(saved.Succeeded, saved.Error);
        Assert.True(reopened.Succeeded, reopened.Error);
        Assert.Equal("Poster Set", second.Current!.Name);
        Assert.Equal(Path.Combine(Projects, "Poster Set"), second.Current.FolderPath);
        Assert.Equal(created.Project.Document.CreatedUtc, second.Current.Document.CreatedUtc);
        Assert.NotNull(second.Current.Document.ModifiedUtc);
        Assert.True(Directory.Exists(Path.Combine(second.Current.FolderPath, ".canvaspilot")));
    }

    /// <summary>
    ///     Test that opened projects are remembered, most recent first, across a restart, and that a project whose
    ///     file was deleted is dropped from the list.
    /// </summary>
    [Fact]
    public async Task ProjectSubsystem_RememberRecentProjects_OpenedProjects_ListedMostRecentFirst()
    {
        // Arrange: two projects, each recorded in the recent list when opened, then a restart
        Directory.CreateDirectory(Projects);
        var settingsFolder = Path.Combine(_tempRoot, "settings");
        var service = new ProjectService();
        var settings = new SettingsService(settingsFolder);
        var recent = new RecentProjects(settings);
        var first = (await service.CreateAsync(Projects, "First", TestContext.Current.CancellationToken)).Project!;
        var second = (await service.CreateAsync(Projects, "Second", TestContext.Current.CancellationToken)).Project!;
        recent.Add(first.FilePath);
        recent.Add(second.FilePath);
        var restarted = new SettingsService(settingsFolder);
        restarted.Load();
        var afterRestart = new RecentProjects(restarted).GetRecent();

        // Act: the first project's folder is deleted and the list is read again
        Directory.Delete(first.FolderPath, recursive: true);
        var afterDelete = new RecentProjects(restarted).GetRecent();

        // Assert: both were listed newest first after the restart, then only the surviving project
        Assert.Equal([second.FilePath, first.FilePath], afterRestart);
        Assert.Equal([second.FilePath], afterDelete);
        Assert.Equal([second.FilePath], new SettingsService(settingsFolder).Load().RecentProjects);
    }

    /// <summary>
    ///     Test that the picture last shown in each of two real projects is remembered with the recent projects, read back
    ///     after a restart for the right project, kept out of the project file, and forgotten when the project leaves the
    ///     list.
    /// </summary>
    [Fact]
    public async Task ProjectSubsystem_RememberShownPicture_SetAndRestart_IsReadBackForTheProject()
    {
        // Arrange: two real projects in the recent list
        Directory.CreateDirectory(Projects);
        var settingsFolder = Path.Combine(_tempRoot, "settings");
        var service = new ProjectService();
        var recent = new RecentProjects(new SettingsService(settingsFolder));
        var first = (await service.CreateAsync(Projects, "First", TestContext.Current.CancellationToken)).Project!;
        var second = (await service.CreateAsync(Projects, "Second", TestContext.Current.CancellationToken)).Project!;
        recent.Add(first.FilePath);
        recent.Add(second.FilePath);
        var projectFileBefore = await File.ReadAllTextAsync(first.FilePath, TestContext.Current.CancellationToken);

        // Act: remember a picture of each, restart, and then remove the second project from the list
        recent.RememberShownPicture(first.FilePath, "flow.svg");
        recent.RememberShownPicture(second.FilePath, Path.Combine("charts", "sales.vl.json"));
        var restarted = new RecentProjects(Loaded(settingsFolder));
        var afterRestart = (restarted.GetShownPicture(first.FilePath), restarted.GetShownPicture(second.FilePath));
        restarted.Remove(second.FilePath);

        // Assert: each project had its own picture, the project file was not touched, and a removed project forgot it
        Assert.Equal(("flow.svg", Path.Combine("charts", "sales.vl.json")), afterRestart);
        Assert.Equal(projectFileBefore, await File.ReadAllTextAsync(first.FilePath, TestContext.Current.CancellationToken));
        Assert.Null(new RecentProjects(Loaded(settingsFolder)).GetShownPicture(second.FilePath));
        Assert.Equal("flow.svg", new RecentProjects(Loaded(settingsFolder)).GetShownPicture(first.FilePath));
    }

    /// <summary>
    ///     Test that the explorer of a created project lists the image source files of its folders and sub-folders but not the
    ///     data folder, and reflects a rename and delete of the selected file.
    /// </summary>
    [Fact]
    public async Task ProjectSubsystem_BrowseImages_ProjectFolder_ListsImagesOfTheTreeButNotTheDataFolder()
    {
        // Arrange: a created project with images, a text file, a sub-folder asset, and data-folder content
        Directory.CreateDirectory(Projects);
        var token = TestContext.Current.CancellationToken;
        var project = (await new ProjectService().CreateAsync(Projects, "Gallery", token)).Project!;
        await File.WriteAllTextAsync(Path.Combine(project.FolderPath, "logo.svg"), "<svg/>", token);
        await File.WriteAllTextAsync(Path.Combine(project.FolderPath, "diagram.svg"), "<svg/>", token);
        await File.WriteAllTextAsync(Path.Combine(project.FolderPath, "readme.txt"), "text", token);
        Directory.CreateDirectory(Path.Combine(project.FolderPath, "assets"));
        await File.WriteAllTextAsync(Path.Combine(project.FolderPath, "assets", "model.svg"), "<svg/>", token);
        await File.WriteAllTextAsync(Path.Combine(project.FolderPath, ".canvaspilot", "history.svg"), "<svg/>", token);
        using var explorer = new ProjectExplorerViewModel(new CanvasNetImageSourceFilter(), new FakeRefreshScheduler());

        // Act: open the project folder, then rename one file and delete another
        explorer.Open(project.FolderPath);
        var listed = explorer.Items.Select(item => item.Name).ToList();
        explorer.SelectFile(Path.Combine(project.FolderPath, "logo.svg"));
        var renameProblem = explorer.RenameSelected("brand.svg");
        explorer.SelectFile(Path.Combine(project.FolderPath, "diagram.svg"));
        var deleteProblem = explorer.DeleteSelected();

        // Assert: the images of the tree were listed with the sub-folder's one in tree order, and the rename and delete changed
        // the list and the folder
        Assert.Equal(["model.svg", "diagram.svg", "logo.svg"], listed);
        Assert.Equal(string.Empty, renameProblem);
        Assert.Equal(string.Empty, deleteProblem);
        Assert.Equal(["model.svg", "brand.svg"], explorer.Items.Select(item => item.Name));
        Assert.True(File.Exists(Path.Combine(project.FolderPath, "assets", "model.svg")));
        Assert.True(File.Exists(project.FilePath));
    }

    /// <summary>
    ///     Test that the style kit chosen for a project is kept in the project file, that a second service that opens the
    ///     file reads the same choice, and that the choice leaves the other properties of the file, the image files, and
    ///     the data folder as they were.
    /// </summary>
    [Fact]
    public async Task ProjectSubsystem_KeepTheStyleChoice_SetAndReopen_GivesTheSameChoiceAndKeepsTheProject()
    {
        // Arrange: a created project with an image file and a property that this version does not know in its file
        Directory.CreateDirectory(Projects);
        var service = new ProjectService();
        var project = (await service.CreateAsync(Projects, "Styled Set", TestContext.Current.CancellationToken)).Project!;
        await File.WriteAllTextAsync(Path.Combine(project.FolderPath, "logo.svg"), "<svg/>", TestContext.Current.CancellationToken);
        var text = await File.ReadAllTextAsync(project.FilePath, TestContext.Current.CancellationToken);
        await File.WriteAllTextAsync(project.FilePath, text.Replace("\"formatVersion\": 1,", "\"formatVersion\": 1, \"futureThing\": 7,", StringComparison.Ordinal), TestContext.Current.CancellationToken);
        await service.OpenAsync(project.FilePath, TestContext.Current.CancellationToken);

        // Act: choose a style, open the file with a second service as after a restart, and then choose none
        var chosen = await service.SetActiveStyleKitAsync("slides-dark", TestContext.Current.CancellationToken);
        var second = new ProjectService();
        var reopened = await second.OpenAsync(project.FilePath, TestContext.Current.CancellationToken);
        var chosenText = await File.ReadAllTextAsync(project.FilePath, TestContext.Current.CancellationToken);
        var cleared = await second.SetActiveStyleKitAsync(null, TestContext.Current.CancellationToken);
        var clearedText = await File.ReadAllTextAsync(project.FilePath, TestContext.Current.CancellationToken);

        // Assert: the choice is in the file and is read back, the unknown property and the files are untouched, and
        // choosing none removes the property
        Assert.True(chosen.Succeeded, chosen.Error);
        Assert.Equal("slides-dark", reopened.Project!.Document.ActiveStyleKit);
        Assert.Contains("futureThing", chosenText, StringComparison.Ordinal);
        Assert.True(cleared.Succeeded, cleared.Error);
        Assert.DoesNotContain("activeStyleKit", clearedText, StringComparison.Ordinal);
        Assert.Contains("futureThing", clearedText, StringComparison.Ordinal);
        Assert.True(File.Exists(Path.Combine(project.FolderPath, "logo.svg")));
        Assert.True(Directory.Exists(Path.Combine(project.FolderPath, ".canvaspilot")));
    }

    /// <summary>
    ///     Reads the settings in a folder with a new service, as the application does when it starts.
    /// </summary>
    /// <param name="folder">The settings folder.</param>
    /// <returns>The service, with the settings read.</returns>
    private static SettingsService Loaded(string folder)
    {
        var settings = new SettingsService(folder);
        settings.Load();
        return settings;
    }
}
