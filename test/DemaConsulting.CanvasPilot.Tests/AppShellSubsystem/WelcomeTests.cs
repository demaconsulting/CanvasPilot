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

using CommunityToolkit.Mvvm.Input;
using DemaConsulting.CanvasPilot.AgentSubsystem;
using DemaConsulting.CanvasPilot.AnnotationSubsystem;
using DemaConsulting.CanvasPilot.AppShellSubsystem;
using DemaConsulting.CanvasPilot.ChatSubsystem;
using DemaConsulting.CanvasPilot.ComposerSubsystem;
using DemaConsulting.CanvasPilot.LoggingSubsystem;
using DemaConsulting.CanvasPilot.PayloadSubsystem;
using DemaConsulting.CanvasPilot.ProjectSubsystem;
using DemaConsulting.CanvasPilot.SettingsSubsystem;
using NSubstitute;

namespace DemaConsulting.CanvasPilot.Tests;

/// <summary>
///     Unit tests for the welcome screen: the words of a recent project card, the count of pictures of a project, the time of
///     opening that the recent projects list records, and the view model that follows the list and the AI provider pill.
/// </summary>
public sealed class WelcomeTests : IDisposable
{
    /// <summary>
    ///     A fixed present time for the words of the cards.
    /// </summary>
    private static readonly DateTimeOffset Now = new(2026, 11, 22, 15, 0, 0, TimeSpan.Zero);

    /// <summary>
    ///     Temporary root created fresh for each test and removed on disposal.
    /// </summary>
    private readonly string _root = Directory.CreateTempSubdirectory("canvaspilot-welcome-tests-").FullName;

    /// <summary>
    ///     The units that a test created, disposed with the fixture.
    /// </summary>
    private readonly List<IDisposable> _owned = [];

    /// <inheritdoc />
    public void Dispose()
    {
        foreach (var owned in _owned)
        {
            owned.Dispose();
        }

        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    /// <summary>
    ///     Test that the time of opening is told in words that grow coarser as the time goes by, and that a project with no
    ///     time says so.
    /// </summary>
    /// <param name="minutesAgo">How long ago the project was opened, in minutes; negative for no time.</param>
    /// <param name="expected">The words that are expected.</param>
    [Theory]
    [InlineData(0, "Opened just now")]
    [InlineData(5, "Opened 5 min ago")]
    [InlineData(60, "Opened 1 hour ago")]
    [InlineData(120, "Opened 2 hours ago")]
    [InlineData(60 * 24, "Opened yesterday")]
    [InlineData(60 * 24 * 3, "Opened 3 days ago")]
    [InlineData(60 * 24 * 8, "Opened last week")]
    [InlineData(60 * 24 * 21, "Opened 3 weeks ago")]
    [InlineData(60 * 24 * 40, "Opened last month")]
    [InlineData(60 * 24 * 100, "Opened Aug 14, 2026")]
    [InlineData(-1, "Opened earlier")]
    public void RecentProjectItem_OpenedTextFor_TellsTheTimeInWords(int minutesAgo, string expected)
    {
        // Arrange: the time of opening, or none
        DateTimeOffset? opened = minutesAgo < 0 ? null : Now.AddMinutes(-minutesAgo);

        // Act: tell it
        var text = RecentProjectItem.OpenedTextFor(opened, Now);

        // Assert: the words
        Assert.Equal(expected, text);
    }

    /// <summary>
    ///     Test that the number of pictures is told with the right plural and that a project with none says so.
    /// </summary>
    /// <param name="count">The number of pictures.</param>
    /// <param name="expected">The words that are expected.</param>
    [Theory]
    [InlineData(0, "no pictures yet")]
    [InlineData(1, "1 picture")]
    [InlineData(3, "3 pictures")]
    public void RecentProjectItem_PictureCountTextFor_UsesThePlural(int count, string expected)
    {
        // Act & Assert: the words
        Assert.Equal(expected, RecentProjectItem.PictureCountTextFor(count));
    }

    /// <summary>
    ///     Test that a card gives the line under its path, the sentence for a screen reader, and the folder of the project.
    /// </summary>
    [Fact]
    public void RecentProjectItem_New_GivesDetailLineAccessibleNameAndFolder()
    {
        // Arrange & Act: a card for a project opened 2 hours ago that holds 3 pictures
        var path = Path.Combine(_root, "Release", "Release.canvaspilot");
        using var item = new RecentProjectItem("Release", path, new AsyncRelayCommand(() => Task.CompletedTask), Now.AddHours(-2), 3, now: Now);

        // Assert: the words and the folder
        Assert.Equal("Opened 2 hours ago \u00B7 3 pictures", item.DetailText);
        Assert.Equal("Release, opened 2 hours ago, 3 pictures", item.AccessibleName);
        Assert.Equal(Path.Combine(_root, "Release"), item.FolderPath);
        Assert.True(item.HasNoThumbnail);
        Assert.False(item.HasThumbnail);
    }

    /// <summary>
    ///     Test that the pictures of a project are counted in its folders, that the attachments, the style kits, and the hidden
    ///     folders are left out, and that the first picture is the first by name.
    /// </summary>
    [Fact]
    public void ProjectPictureCounter_Count_CountsPicturesAndLeavesOutAttachmentsStylesAndHiddenFolders()
    {
        // Arrange: pictures at the top and in a folder, and files that are not pictures or are in folders that are left out
        var folder = Directory.CreateDirectory(Path.Combine(_root, "Gallery")).FullName;
        var nested = Directory.CreateDirectory(Path.Combine(folder, "more")).FullName;
        File.WriteAllText(Path.Combine(folder, "b.svg"), "<svg/>");
        File.WriteAllText(Path.Combine(folder, "a.svg"), "<svg/>");
        File.WriteAllText(Path.Combine(folder, "notes.txt"), "text");
        File.WriteAllText(Path.Combine(nested, "c.svg"), "<svg/>");
        foreach (var skipped in new[] { "attachments", "styles", ".hidden" })
        {
            var skippedFolder = Directory.CreateDirectory(Path.Combine(folder, skipped)).FullName;
            File.WriteAllText(Path.Combine(skippedFolder, "x.svg"), "<svg/>");
        }

        // Act: count
        var (count, first) = ProjectPictureCounter.Count(folder, new FakeImageSourceFilter(".svg"));

        // Assert: three pictures, the first by name
        Assert.Equal(3, count);
        Assert.Equal(Path.Combine(folder, "a.svg"), first);
    }

    /// <summary>
    ///     Test that a folder that is not there has no pictures instead of failing.
    /// </summary>
    [Fact]
    public void ProjectPictureCounter_Count_MissingFolder_HasNoPictures()
    {
        // Act: count a folder that does not exist
        var (count, first) = ProjectPictureCounter.Count(Path.Combine(_root, "missing"), new FakeImageSourceFilter(".svg"));

        // Assert: none
        Assert.Equal(0, count);
        Assert.Null(first);
    }

    /// <summary>
    ///     Test that adding a project to the recent projects list records the time of opening in the settings, that opening it
    ///     again renews the time, and that removing it takes the time away.
    /// </summary>
    [Fact]
    public void RecentProjects_Add_RecordsTheTimeOfOpeningAndRemoveForgetsIt()
    {
        // Arrange: a list over real settings with a clock that the test sets
        var settings = new SettingsService(Path.Combine(_root, "settings"));
        settings.Load();
        var clock = new SettableTime(Now);
        var recent = new RecentProjects(settings, clock);
        var file = Path.Combine(_root, "p.canvaspilot");
        File.WriteAllText(file, "{}");

        // Act: add, add again later, and remove
        recent.Add(file);
        var first = settings.Current.RecentProjectOpened[file];
        clock.Present = Now.AddHours(3);
        recent.Add(file);
        var second = settings.Current.RecentProjectOpened[file];
        recent.Remove(file);

        // Assert: the time followed the openings and was forgotten with the project
        Assert.Equal(Now, first);
        Assert.Equal(Now.AddHours(3), second);
        Assert.Empty(settings.Current.RecentProjectOpened);
    }

    /// <summary>
    ///     Test that the welcome screen shows at most six cards, the most recent first, says how many of the projects it shows
    ///     when it does not show all, and that it asks for a first project when there is none.
    /// </summary>
    [Fact]
    public void WelcomeViewModel_Cards_ShowTheSixMostRecentAndCountTheRest()
    {
        // Arrange: a shell with no recent project, and then eight projects that were opened one after the other
        var (shell, recent) = CreateShell();
        var welcome = shell.Welcome;
        var empty = (welcome.HasRecent, welcome.HasNoRecent, welcome.CountText, welcome.Cards.Count);
        for (var i = 1; i <= 8; i++)
        {
            var file = Path.Combine(_root, $"p{i}", $"p{i}.canvaspilot");
            Directory.CreateDirectory(Path.GetDirectoryName(file)!);
            File.WriteAllText(file, "{}");
            recent.Add(file);
        }

        // Assert: the six most recent in order, with the count of the rest
        Assert.Equal((false, true, string.Empty, 0), empty);
        Assert.True(welcome.HasRecent);
        Assert.False(welcome.HasNoRecent);
        Assert.Equal(WelcomeViewModel.MaximumCards, welcome.Cards.Count);
        Assert.Equal(["p8", "p7", "p6", "p5", "p4", "p3"], welcome.Cards.Select(card => card.Name));
        Assert.Equal("6 of 8", welcome.CountText);
        Assert.Equal(8, shell.RecentProjects.Count);
    }

    /// <summary>
    ///     Test that a card's remove command takes the project off the list without touching its files.
    /// </summary>
    [Fact]
    public void WelcomeViewModel_Card_RemoveCommand_RemovesTheProjectFromTheListOnly()
    {
        // Arrange: a shell with one recent project
        var (shell, recent) = CreateShell();
        var file = Path.Combine(_root, "p", "p.canvaspilot");
        Directory.CreateDirectory(Path.GetDirectoryName(file)!);
        File.WriteAllText(file, "{}");
        recent.Add(file);

        // Act: remove the card
        shell.Welcome.Cards[0].RemoveCommand!.Execute(null);

        // Assert: the list and the cards are empty and the file is still there
        Assert.Empty(shell.Welcome.Cards);
        Assert.True(shell.Welcome.HasNoRecent);
        Assert.True(File.Exists(file));
    }

    /// <summary>
    ///     Test that the notice about the AI provider shows only while the provider needs something or cannot be used, with the
    ///     name of the provider and the sentence of the check, and that it goes away when the provider is ready.
    /// </summary>
    [Fact]
    public async Task WelcomeViewModel_ProviderBanner_FollowsTheCheckOfTheProvider()
    {
        // Arrange: a shell with a provider pill over a copilot whose readiness the test chooses
        var (shell, _) = CreateShell();
        var agent = new ControlledAgentService { Readiness = new AgentReadinessReport(AgentReadinessStatus.CopilotNotSignedIn, "Sign in to GitHub Copilot.") };
        var settings = new SettingsService(Path.Combine(_root, "pill-settings"));
        settings.Load();
        var marks = new MarkRegistry();
        var markImages = Substitute.For<IMarkImageComposer>();
        var composer = new ComposerViewModel(new PayloadBuilder(markImages), marks, new FakeDictationService());
        var annotation = new AnnotationOverlayViewModel(marks, new FakeRenderedImageSource(), _ => { });
        var chat = new ChatPanelViewModel(composer, marks, annotation, markImages, agent: agent, clock: new ManualTimeProvider());
        _owned.AddRange([chat, annotation, composer, agent]);
        shell.ProviderPill = new ProviderPillViewModel(chat, settings, shell.OpenSettingsCommand);
        var welcome = shell.Welcome;
        var changed = new List<string?>();
        welcome.PropertyChanged += (_, e) => changed.Add(e.PropertyName);
        var whileChecking = welcome.ShowProviderBanner;

        // Act: the check finds a problem, and then the provider is ready
        await chat.RefreshReadinessAsync();
        var (shown, title, text) = (welcome.ShowProviderBanner, welcome.ProviderBannerTitle, welcome.ProviderBannerText);
        agent.Readiness = new AgentReadinessReport(AgentReadinessStatus.Ready, "GitHub Copilot is ready.");
        await chat.RefreshReadinessAsync();

        // Assert: nothing shows while checking, the problem shows with its sentence, and nothing shows when ready
        Assert.False(whileChecking);
        Assert.True(shown);
        Assert.Equal("Copilot is not ready", title);
        Assert.Equal("Sign in to GitHub Copilot.", text);
        Assert.False(welcome.ShowProviderBanner);
        Assert.Contains(nameof(WelcomeViewModel.ShowProviderBanner), changed);
        Assert.Same(shell.OpenSettingsCommand, welcome.OpenSettingsCommand);
    }

    /// <summary>
    ///     Test that a welcome screen without a shell is refused.
    /// </summary>
    [Fact]
    public void WelcomeViewModel_Constructor_NullShell_Throws()
    {
        // Act & Assert: the shell is required
        Assert.Throws<ArgumentNullException>(() => new WelcomeViewModel(null!));
    }

    /// <summary>
    ///     Creates a shell over real recent projects and settings on the temporary folder.
    /// </summary>
    /// <returns>The shell, disposed with the fixture, and its recent projects list.</returns>
    private (MainWindowShell Shell, RecentProjects Recent) CreateShell()
    {
        var logger = new RollingFileLogger(Path.Combine(_root, "logs"));
        var settings = new SettingsService(Path.Combine(_root, "settings"), logger);
        settings.Load();
        var recent = new RecentProjects(settings);
        var explorer = new ProjectExplorerViewModel(new FakeImageSourceFilter(".svg"), new FakeRefreshScheduler(), logger: logger);
        var shell = new MainWindowShell(new ProjectService(logger), recent, settings, new ProjectWorkspaceViewModel(explorer), []);
        _owned.Add(shell);
        return (shell, recent);
    }

    /// <summary>
    ///     A clock whose time the test sets.
    /// </summary>
    /// <param name="now">The time to start at.</param>
    private sealed class SettableTime(DateTimeOffset now) : TimeProvider
    {
        /// <summary>
        ///     Gets or sets the present time.
        /// </summary>
        public DateTimeOffset Present { get; set; } = now;

        /// <inheritdoc />
        public override DateTimeOffset GetUtcNow() => Present.ToUniversalTime();

        /// <inheritdoc />
        public override TimeZoneInfo LocalTimeZone => TimeZoneInfo.Utc;
    }
}
