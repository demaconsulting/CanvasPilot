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

using System.Globalization;
using DemaConsulting.CanvasPilot.AppShellSubsystem;
using DemaConsulting.CanvasPilot.PreviewSubsystem;
using DemaConsulting.CanvasPilot.ProjectSubsystem;

namespace DemaConsulting.CanvasPilot.Tests;

/// <summary>
///     Unit tests for what the status bar says about the saving of the open project: the real modified time of the project
///     file, and nothing when no project is open or the file has no time, so that the bar never claims what it does not
///     know.
/// </summary>
public sealed class StatusBarSavedTimeTests : IDisposable
{
    /// <summary>
    ///     The time at which the tests start.
    /// </summary>
    private static readonly DateTimeOffset Start = new(2026, 11, 22, 15, 0, 0, TimeSpan.Zero);

    /// <summary>
    ///     Temporary folder removed on disposal.
    /// </summary>
    private readonly string _root = Directory.CreateTempSubdirectory("canvaspilot-saved-tests-").FullName;

    /// <summary>
    ///     The clock of the project service and the bar, which a test sets.
    /// </summary>
    private readonly SavedClock _clock = new(Start);

    /// <summary>
    ///     The real project service over the temporary folder.
    /// </summary>
    private readonly ProjectService _projects;

    /// <summary>
    ///     The workspace that the bar follows.
    /// </summary>
    private readonly ProjectWorkspaceViewModel _workspace;

    /// <summary>
    ///     The bar under test.
    /// </summary>
    private readonly StatusBarViewModel _bar;

    /// <summary>
    ///     Initializes a new instance of the <see cref="StatusBarSavedTimeTests" /> class.
    /// </summary>
    public StatusBarSavedTimeTests()
    {
        _projects = new ProjectService(timeProvider: _clock);
        _workspace = new ProjectWorkspaceViewModel(new ProjectExplorerViewModel(new FakeImageSourceFilter(".svg"), new FakeRefreshScheduler()));
        _bar = new StatusBarViewModel(_workspace, projects: _projects, clock: _clock);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _bar.Dispose();
        _workspace.Dispose();
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    /// <summary>
    ///     Test that the bar says nothing about saving while no project is open, nothing for a project whose file has no
    ///     saved time (a new project), the time of the save after the project is saved, and nothing again when the project is
    ///     closed, announcing each change.
    /// </summary>
    [Fact]
    public async Task StatusBar_SavedText_FollowsTheModifiedTimeOfTheOpenProject()
    {
        // Arrange
        var token = TestContext.Current.CancellationToken;
        var announced = new List<string?>();
        _bar.PropertyChanged += (_, e) => announced.Add(e.PropertyName);
        var noProject = (_bar.SavedText, _bar.HasSavedText);

        // Act: create a project, save it a while later, and close it
        var created = await _projects.CreateAsync(_root, "Demo", token);
        Assert.True(created.Succeeded);
        var newProject = (_bar.SavedText, _bar.HasSavedText);
        _clock.Now = Start.AddMinutes(95);
        var saved = await _projects.SaveAsync(token);
        Assert.True(saved.Succeeded);
        var afterSave = (_bar.SavedText, _bar.HasSavedText);
        _projects.Close();
        var afterClose = (_bar.SavedText, _bar.HasSavedText);

        // Assert
        Assert.Equal((string.Empty, false), noProject);
        Assert.Equal((string.Empty, false), newProject);
        var expectedTime = StatusBarViewModel.FormatSavedTime(Start.AddMinutes(95), _clock.Now, CultureInfo.CurrentCulture);
        Assert.Equal(($"{StatusBarViewModel.SavedPrefix} {expectedTime}", true), afterSave);
        Assert.Equal((string.Empty, false), afterClose);
        Assert.Contains(nameof(StatusBarViewModel.SavedText), announced);
        Assert.Contains(nameof(StatusBarViewModel.HasSavedText), announced);
    }

    /// <summary>
    ///     Test that the time is written as the time of day for today and with the date for another day, in the culture of
    ///     the user, and in the local time zone.
    /// </summary>
    [Fact]
    public void StatusBar_FormatSavedTime_TimeOfDayForTodayAndDateOtherwise()
    {
        // Arrange: a save at noon UTC, seen on the same day and two days later
        var saved = new DateTimeOffset(2026, 11, 22, 12, 0, 0, TimeSpan.Zero);
        var culture = CultureInfo.GetCultureInfo("en-US");

        // Act
        var today = StatusBarViewModel.FormatSavedTime(saved, saved.AddMinutes(30), culture);
        var later = StatusBarViewModel.FormatSavedTime(saved, saved.AddDays(2), culture);

        // Assert
        Assert.Equal(saved.ToLocalTime().ToString("t", culture), today);
        Assert.Equal(saved.ToLocalTime().ToString("g", culture), later);
        Assert.NotEqual(today, later);
    }

    /// <summary>
    ///     Test that a bar made without a project service says nothing about saving.
    /// </summary>
    [Fact]
    public void StatusBar_WithoutProjectService_SaysNothingAboutSaving()
    {
        // Arrange and act
        using var bar = new StatusBarViewModel(_workspace);

        // Assert
        Assert.Equal((string.Empty, false), (bar.SavedText, bar.HasSavedText));
    }

    /// <summary>
    ///     A clock whose time the test sets.
    /// </summary>
    /// <param name="now">The time to start at.</param>
    private sealed class SavedClock(DateTimeOffset now) : TimeProvider
    {
        /// <summary>
        ///     Gets or sets the present time.
        /// </summary>
        public DateTimeOffset Now { get; set; } = now;

        /// <inheritdoc />
        public override DateTimeOffset GetUtcNow() => Now.ToUniversalTime();
    }
}
