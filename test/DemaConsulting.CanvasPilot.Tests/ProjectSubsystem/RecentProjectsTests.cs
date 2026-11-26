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
using NSubstitute.ExceptionExtensions;

namespace DemaConsulting.CanvasPilot.Tests;

/// <summary>
///     Unit tests for <see cref="RecentProjects" />.
/// </summary>
public sealed class RecentProjectsTests : IDisposable
{
    /// <summary>
    ///     Temporary root created fresh for each test and removed on disposal.
    /// </summary>
    private readonly string _tempRoot = Directory.CreateTempSubdirectory("canvaspilot-tests-").FullName;

    /// <summary>
    ///     Service holding the settings that contain the list.
    /// </summary>
    private readonly SettingsService _settings;

    /// <summary>
    ///     Creates the test fixture.
    /// </summary>
    public RecentProjectsTests()
    {
        _settings = new SettingsService(Path.Combine(_tempRoot, "settings"));
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (Directory.Exists(_tempRoot))
        {
            Directory.Delete(_tempRoot, recursive: true);
        }
    }

    /// <summary>
    ///     Test that a new list is empty.
    /// </summary>
    [Fact]
    public void RecentProjects_GetRecent_NothingAdded_IsEmpty()
    {
        // Arrange / Act: read the list of a fresh settings file
        var recent = new RecentProjects(_settings).GetRecent();

        // Assert: it is empty
        Assert.Empty(recent);
    }

    /// <summary>
    ///     Test that added projects are listed most recent first.
    /// </summary>
    [Fact]
    public void RecentProjects_Add_ThreeProjects_ListsMostRecentFirst()
    {
        // Arrange: three project files and an empty list
        var recent = new RecentProjects(_settings);
        var first = CreateProjectFile("one");
        var second = CreateProjectFile("two");
        var third = CreateProjectFile("three");

        // Act: add them in order
        recent.Add(first);
        recent.Add(second);
        recent.Add(third);

        // Assert: the most recent is first
        Assert.Equal([third, second, first], recent.GetRecent());
    }

    /// <summary>
    ///     Test that adding a project that is already listed moves it to the top rather than duplicating it.
    /// </summary>
    [Fact]
    public void RecentProjects_Add_ExistingProject_MovesToTopWithoutDuplicate()
    {
        // Arrange: a list of two projects
        var recent = new RecentProjects(_settings);
        var first = CreateProjectFile("one");
        var second = CreateProjectFile("two");
        recent.Add(first);
        recent.Add(second);

        // Act: add the first one again
        recent.Add(first);

        // Assert: it is on top and listed once
        Assert.Equal([first, second], recent.GetRecent());
    }

    /// <summary>
    ///     Test that two paths that differ only in letter case are the same entry.
    /// </summary>
    [Fact]
    public void RecentProjects_Add_PathDifferingInCase_ReplacesEarlierEntry()
    {
        // Arrange: a listed project
        var recent = new RecentProjects(_settings);
        var path = CreateProjectFile("MixedCase");
        recent.Add(path);

        // Act: add the same path in upper case
        var upper = path.ToUpperInvariant();
        recent.Add(upper);

        // Assert: one entry remains and it is the later spelling
        Assert.Equal([upper], _settings.Current.RecentProjects);
    }

    /// <summary>
    ///     Test that a relative path is stored as a full path.
    /// </summary>
    [Fact]
    public void RecentProjects_Add_RelativePath_StoresFullPath()
    {
        // Arrange: a relative path to an existing file
        var recent = new RecentProjects(_settings);
        var path = CreateProjectFile("relative");
        var relative = Path.GetRelativePath(Directory.GetCurrentDirectory(), path);

        // Act: add the relative path
        recent.Add(relative);

        // Assert: the full path is stored
        Assert.Equal([Path.GetFullPath(relative)], _settings.Current.RecentProjects);
    }

    /// <summary>
    ///     Test that the list holds at most ten entries and drops the oldest.
    /// </summary>
    [Fact]
    public void RecentProjects_Add_MoreThanTen_KeepsNewestTen()
    {
        // Arrange: twelve project files
        var recent = new RecentProjects(_settings);
        var files = Enumerable.Range(1, 12).Select(number => CreateProjectFile($"p{number}")).ToList();

        // Act: add them all
        foreach (var file in files)
        {
            recent.Add(file);
        }

        // Assert: only the newest ten remain, newest first
        Assert.Equal(10, RecentProjects.MaxCount);
        Assert.Equal(Enumerable.Reverse(files).Take(10), recent.GetRecent());
        Assert.Equal(10, _settings.Current.RecentProjects.Count);
    }

    /// <summary>
    ///     Test that files that no longer exist are not returned and are dropped from the stored list.
    /// </summary>
    [Fact]
    public void RecentProjects_GetRecent_MissingFile_IsDroppedAndStoredListPruned()
    {
        // Arrange: two listed projects, one of which is deleted
        var recent = new RecentProjects(_settings);
        var kept = CreateProjectFile("kept");
        var gone = CreateProjectFile("gone");
        recent.Add(kept);
        recent.Add(gone);
        File.Delete(gone);
        var changes = 0;
        recent.Changed += (_, _) => changes++;

        // Act: read the list
        var list = recent.GetRecent();

        // Assert: only the existing file is returned, the stored list is pruned, and the change was announced
        Assert.Equal([kept], list);
        Assert.Equal([kept], _settings.Current.RecentProjects);
        Assert.Equal(1, changes);
    }

    /// <summary>
    ///     Test that reading a list with nothing missing stores nothing and announces nothing.
    /// </summary>
    [Fact]
    public void RecentProjects_GetRecent_NothingMissing_DoesNotAnnounceChange()
    {
        // Arrange: one listed project
        var recent = new RecentProjects(_settings);
        recent.Add(CreateProjectFile("only"));
        var changes = 0;
        recent.Changed += (_, _) => changes++;

        // Act: read twice
        recent.GetRecent();
        recent.GetRecent();

        // Assert: no change was announced
        Assert.Equal(0, changes);
    }

    /// <summary>
    ///     Test that a project can be removed from the list.
    /// </summary>
    [Fact]
    public void RecentProjects_Remove_ListedProject_IsRemoved()
    {
        // Arrange: two listed projects
        var recent = new RecentProjects(_settings);
        var first = CreateProjectFile("one");
        var second = CreateProjectFile("two");
        recent.Add(first);
        recent.Add(second);

        // Act: remove the first, spelled in a different case
        var stored = recent.Remove(first.ToUpperInvariant());

        // Assert: only the second remains
        Assert.True(stored);
        Assert.Equal([second], recent.GetRecent());
    }

    /// <summary>
    ///     Test that the list can be cleared.
    /// </summary>
    [Fact]
    public void RecentProjects_Clear_ListedProjects_EmptiesList()
    {
        // Arrange: a listed project
        var recent = new RecentProjects(_settings);
        recent.Add(CreateProjectFile("one"));

        // Act: clear
        var stored = recent.Clear();

        // Assert: the list is empty
        Assert.True(stored);
        Assert.Empty(recent.GetRecent());
    }

    /// <summary>
    ///     Test that the list is stored in the settings file and read back by a new instance, and that other
    ///     settings are kept.
    /// </summary>
    [Fact]
    public void RecentProjects_Add_Project_PersistsInSettingsAndKeepsOtherSettings()
    {
        // Arrange: settings with another value, and a project
        var folder = Path.Combine(_tempRoot, "settings");
        _settings.Save(new AppSettings { AiProvider = new AiProviderSettings { ModelName = "keep-me" } });
        var path = CreateProjectFile("persisted");

        // Act: add the project, then load the settings as after a restart
        new RecentProjects(_settings).Add(path);
        var restarted = new SettingsService(folder);
        restarted.Load();

        // Assert: the list and the other setting are both there
        Assert.Equal([path], new RecentProjects(restarted).GetRecent());
        Assert.Equal("keep-me", restarted.Current.AiProvider.ModelName);
    }

    /// <summary>
    ///     Test that empty and invalid paths are ignored.
    /// </summary>
    /// <param name="path">The path to add.</param>
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("bad\0path")]
    public void RecentProjects_Add_EmptyOrInvalidPath_IsIgnored(string path)
    {
        // Arrange: an empty list
        var recent = new RecentProjects(_settings);

        // Act: add the path
        var stored = recent.Add(path);

        // Assert: nothing was stored
        Assert.False(stored);
        Assert.Empty(_settings.Current.RecentProjects);
    }

    /// <summary>
    ///     Test that a settings file that cannot be written does not throw and is reported by the result.
    /// </summary>
    [Fact]
    public void RecentProjects_Add_SettingsWriteFails_ReturnsFalseAndDoesNotAnnounce()
    {
        // Arrange: a settings service whose update fails
        var settings = Substitute.For<ISettingsService>();
        settings.Current.Returns(new AppSettings());
        settings.Update(Arg.Any<Func<AppSettings, AppSettings>>()).Throws(new IOException("disk full"));
        var recent = new RecentProjects(settings);
        var changes = 0;
        recent.Changed += (_, _) => changes++;

        // Act: add a project
        var stored = recent.Add(CreateProjectFile("unlucky"));

        // Assert: the failure is reported by the result only
        Assert.False(stored);
        Assert.Equal(0, changes);
    }

    /// <summary>
    ///     Test that the picture last shown is kept for each listed project, relative to its folder, and read back by a new
    ///     list over the same settings, as after a restart, without announcing a change of the list.
    /// </summary>
    [Fact]
    public void RecentProjects_RememberShownPicture_StoredPerProjectAndReadBack()
    {
        // Arrange: two listed projects
        var recent = new RecentProjects(_settings);
        var one = CreateProjectFile("one");
        var two = CreateProjectFile("two");
        recent.Add(one);
        recent.Add(two);
        var changes = 0;
        recent.Changed += (_, _) => changes++;

        // Act: remember a picture of each, the second one twice, and read them back after a restart
        var storedOne = recent.RememberShownPicture(one, "flow.svg");
        var storedTwo = recent.RememberShownPicture(two, Path.Combine("diagrams", "first.svg"));
        recent.RememberShownPicture(two, Path.Combine("diagrams", "chart.vl.json"));
        var restartedSettings = new SettingsService(Path.Combine(_tempRoot, "settings"));
        restartedSettings.Load();
        var restarted = new RecentProjects(restartedSettings);

        // Assert: each project has its own picture, the newest one, and the list was not announced as changed
        Assert.True(storedOne && storedTwo);
        Assert.Equal("flow.svg", restarted.GetShownPicture(one));
        Assert.Equal(Path.Combine("diagrams", "chart.vl.json"), restarted.GetShownPicture(two));
        Assert.Equal("flow.svg", recent.GetShownPicture(one.ToUpperInvariant()));
        Assert.Equal(0, changes);
    }

    /// <summary>
    ///     Test that a picture is refused for a project that is not listed and for a path that is empty, rooted, or leaves
    ///     the folder of the project, and that nothing is given back for them.
    /// </summary>
    [Fact]
    public void RecentProjects_RememberShownPicture_UnlistedProjectOrPathOutsideTheFolder_IsRefused()
    {
        // Arrange: one listed project and one that is not listed
        var recent = new RecentProjects(_settings);
        var listed = CreateProjectFile("listed");
        var unlisted = CreateProjectFile("unlisted");
        recent.Add(listed);

        // Act: remember pictures that cannot be kept
        var results = new[]
        {
            recent.RememberShownPicture(unlisted, "flow.svg"),
            recent.RememberShownPicture(listed, string.Empty),
            recent.RememberShownPicture(listed, "   "),
            recent.RememberShownPicture(listed, Path.Combine(_tempRoot, "outside.svg")),
            recent.RememberShownPicture(listed, Path.Combine("..", "other", "flow.svg")),
            recent.RememberShownPicture(string.Empty, "flow.svg"),
        };

        // Assert: every one was refused and nothing is remembered
        Assert.All(results, Assert.False);
        Assert.Null(recent.GetShownPicture(listed));
        Assert.Null(recent.GetShownPicture(unlisted));
        Assert.Empty(_settings.Current.RecentProjectPicture);
    }

    /// <summary>
    ///     Test that remembering the picture that is already remembered writes nothing to the settings and does not announce
    ///     a change.
    /// </summary>
    [Fact]
    public void RecentProjects_RememberShownPicture_SamePicture_WritesNothingAndDoesNotAnnounce()
    {
        // Arrange: a listed project with a remembered picture
        var recent = new RecentProjects(_settings);
        var project = CreateProjectFile("same");
        recent.Add(project);
        recent.RememberShownPicture(project, "flow.svg");
        var writes = 0;
        _settings.Changed += (_, _) => writes++;
        var changes = 0;
        recent.Changed += (_, _) => changes++;

        // Act: remember the same picture again
        var stored = recent.RememberShownPicture(project, "flow.svg");

        // Assert: it is remembered, and nothing was written or announced
        Assert.True(stored);
        Assert.Equal(0, writes);
        Assert.Equal(0, changes);
    }

    /// <summary>
    ///     Test that a settings file that cannot be written does not throw when a picture is remembered, and is reported
    ///     by the result.
    /// </summary>
    [Fact]
    public void RecentProjects_RememberShownPicture_SettingsWriteFails_ReturnsFalse()
    {
        // Arrange: a settings service with a listed project whose update fails
        var project = CreateProjectFile("unlucky");
        var settings = Substitute.For<ISettingsService>();
        settings.Current.Returns(new AppSettings { RecentProjects = [project] });
        settings.Update(Arg.Any<Func<AppSettings, AppSettings>>()).Throws(new IOException("disk full"));
        var recent = new RecentProjects(settings);
        var changes = 0;
        recent.Changed += (_, _) => changes++;

        // Act: remember a picture
        var stored = recent.RememberShownPicture(project, "flow.svg");

        // Assert: the failure is reported by the result only
        Assert.False(stored);
        Assert.Equal(0, changes);
    }

    /// <summary>
    ///     Test that a project that is removed from the list takes its picture along, so it opens with nothing selected if
    ///     it is added again.
    /// </summary>
    [Fact]
    public void RecentProjects_Remove_ForgetsTheShownPicture()
    {
        // Arrange: a listed project with a remembered picture
        var recent = new RecentProjects(_settings);
        var project = CreateProjectFile("gone");
        recent.Add(project);
        recent.RememberShownPicture(project, "flow.svg");

        // Act: remove it and add it again
        recent.Remove(project);
        recent.Add(project);

        // Assert: the picture was forgotten
        Assert.Null(recent.GetShownPicture(project));
        Assert.Empty(_settings.Current.RecentProjectPicture);
    }

    /// <summary>
    ///     Test that a null settings service is refused.
    /// </summary>
    [Fact]
    public void RecentProjects_Constructor_NullSettings_Throws()
    {
        // Arrange / Act / Assert: a null service is refused
        Assert.Throws<ArgumentNullException>(() => new RecentProjects(null!));
    }

    /// <summary>
    ///     Creates an empty project file.
    /// </summary>
    /// <param name="name">The project name.</param>
    /// <returns>The full path of the file.</returns>
    private string CreateProjectFile(string name)
    {
        var folder = Directory.CreateDirectory(Path.Combine(_tempRoot, "projects", name)).FullName;
        var file = Path.Combine(folder, name + ".canvaspilot");
        File.WriteAllText(file, "{}");
        return file;
    }
}
