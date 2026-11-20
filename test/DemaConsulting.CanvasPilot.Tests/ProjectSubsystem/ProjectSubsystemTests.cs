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
    ///     Test that the explorer of a created project lists the image source files in its root only, and reflects a
    ///     rename and delete of the selected file.
    /// </summary>
    [Fact]
    public async Task ProjectSubsystem_BrowseImages_ProjectFolder_ListsOnlyRootImageSources()
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

        // Assert: only the root images were listed, and the rename and delete changed the list and the folder
        Assert.Equal(["diagram.svg", "logo.svg"], listed);
        Assert.Equal(string.Empty, renameProblem);
        Assert.Equal(string.Empty, deleteProblem);
        Assert.Equal(["brand.svg"], explorer.Items.Select(item => item.Name));
        Assert.True(File.Exists(Path.Combine(project.FolderPath, "assets", "model.svg")));
        Assert.True(File.Exists(project.FilePath));
    }
}
