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

using DemaConsulting.CanvasPilot.SettingsSubsystem;
using NSubstitute;

namespace DemaConsulting.CanvasPilot.Tests;

/// <summary>
///     Unit tests for <see cref="SettingsDialogViewModel" />.
/// </summary>
public sealed class SettingsDialogTests : IDisposable
{
    /// <summary>
    ///     Temporary root created fresh for each test and removed on disposal.
    /// </summary>
    private readonly string _tempRoot = Directory.CreateTempSubdirectory("canvaspilot-tests-").FullName;

    /// <inheritdoc />
    public void Dispose()
    {
        if (Directory.Exists(_tempRoot))
        {
            Directory.Delete(_tempRoot, recursive: true);
        }
    }

    /// <summary>
    ///     Test that every page is loaded from the settings in effect and the first page is selected.
    /// </summary>
    [Fact]
    public void SettingsDialogViewModel_New_Pages_LoadsEachPageAndSelectsFirst()
    {
        // Arrange: stored settings and two pages
        var service = new SettingsService(_tempRoot);
        service.Save(new AppSettings { RecentProjects = ["C:\\a"] });
        var first = new FakeSettingsPage("First");
        var second = new FakeSettingsPage("Second");

        // Act: create the dialog view model
        var dialog = new SettingsDialogViewModel(service, [first, second]);

        // Assert: both pages hold the stored settings and the first is selected
        Assert.Equal("Settings", dialog.Title);
        Assert.Equal([first, second], dialog.Pages);
        Assert.Same(first, dialog.SelectedPage);
        Assert.Same(service.Current, first.LoadedSettings);
        Assert.Same(service.Current, second.LoadedSettings);
    }

    /// <summary>
    ///     Test that OK merges every page into the latest settings, saves them, and asks the host to close with
    ///     acceptance.
    /// </summary>
    [Fact]
    public void SettingsDialogViewModel_Ok_ValidPages_SavesMergedSettingsAndRequestsClose()
    {
        // Arrange: two pages that each contribute a value, over settings that already hold another value
        var service = new SettingsService(_tempRoot);
        service.Save(new AppSettings { RecentProjects = ["C:\\existing"] });
        var first = new FakeSettingsPage("First") { EntryToApply = "C:\\first" };
        var second = new FakeSettingsPage("Second") { EntryToApply = "C:\\second" };
        var dialog = new SettingsDialogViewModel(service, [first, second]);
        bool? accepted = null;
        dialog.CloseRequested += (_, e) => accepted = e.Accepted;

        // Act: choose OK
        dialog.OkCommand.Execute(null);

        // Assert: the merged settings were saved and the dialog asked to close as accepted
        Assert.Equal(["C:\\existing", "C:\\first", "C:\\second"], new SettingsService(_tempRoot).Load().RecentProjects);
        Assert.True(accepted);
        Assert.Equal(string.Empty, dialog.ErrorMessage);
    }

    /// <summary>
    ///     Test that Apply saves the settings and does not ask the host to close.
    /// </summary>
    [Fact]
    public void SettingsDialogViewModel_Apply_ValidPages_SavesAndKeepsDialogOpen()
    {
        // Arrange: one page that contributes a value
        var service = new SettingsService(_tempRoot);
        var page = new FakeSettingsPage("Page") { EntryToApply = "C:\\applied" };
        var dialog = new SettingsDialogViewModel(service, [page]);
        var closeRequests = 0;
        dialog.CloseRequested += (_, _) => closeRequests++;

        // Act: choose Apply
        dialog.ApplyCommand.Execute(null);

        // Assert: the value is saved, the dialog is still open, and the page was not told it closed
        Assert.Equal(["C:\\applied"], new SettingsService(_tempRoot).Load().RecentProjects);
        Assert.Equal(0, closeRequests);
        Assert.Equal(0, page.ClosedCount);
    }

    /// <summary>
    ///     Test that Cancel discards the working copies and asks the host to close without acceptance.
    /// </summary>
    [Fact]
    public void SettingsDialogViewModel_Cancel_PendingEdits_DoesNotSaveAndRequestsClose()
    {
        // Arrange: a page with a pending value and a service that records saves
        var service = Substitute.For<ISettingsService>();
        service.Current.Returns(new AppSettings());
        var page = new FakeSettingsPage("Page") { EntryToApply = "C:\\pending" };
        var dialog = new SettingsDialogViewModel(service, [page]);
        bool? accepted = null;
        dialog.CloseRequested += (_, e) => accepted = e.Accepted;

        // Act: choose Cancel
        dialog.CancelCommand.Execute(null);

        // Assert: nothing was saved and the dialog asked to close as cancelled
        service.DidNotReceiveWithAnyArgs().Save(default!);
        Assert.False(accepted);
    }

    /// <summary>
    ///     Test that OK and Apply are disabled and save nothing while a page is invalid.
    /// </summary>
    [Fact]
    public void SettingsDialogViewModel_Commands_InvalidPage_AreDisabledAndDoNotSave()
    {
        // Arrange: a service that records saves and a page that is invalid
        var service = Substitute.For<ISettingsService>();
        service.Current.Returns(new AppSettings());
        var page = new FakeSettingsPage("Page") { IsValid = false };
        var dialog = new SettingsDialogViewModel(service, [page]);

        // Act: look at the commands and try to run them
        var okEnabled = dialog.OkCommand.CanExecute(null);
        var applyEnabled = dialog.ApplyCommand.CanExecute(null);
        dialog.OkCommand.Execute(null);
        dialog.ApplyCommand.Execute(null);

        // Assert: both are disabled, cancel stays available, and nothing was saved
        Assert.False(okEnabled);
        Assert.False(applyEnabled);
        Assert.False(dialog.AllPagesValid);
        Assert.True(dialog.CancelCommand.CanExecute(null));
        service.DidNotReceiveWithAnyArgs().Save(default!);
    }

    /// <summary>
    ///     Test that the commands become enabled again when an invalid page becomes valid.
    /// </summary>
    [Fact]
    public void SettingsDialogViewModel_PageBecomesValid_EnablesCommands()
    {
        // Arrange: an invalid page and a listener for command state changes
        var service = new SettingsService(_tempRoot);
        var page = new FakeSettingsPage("Page") { IsValid = false };
        var dialog = new SettingsDialogViewModel(service, [page]);
        var okChanges = 0;
        dialog.OkCommand.CanExecuteChanged += (_, _) => okChanges++;

        // Act: the page becomes valid
        page.SetValidity(true);

        // Assert: the dialog reports valid pages, enables OK and Apply, and signaled the change
        Assert.True(dialog.AllPagesValid);
        Assert.True(dialog.OkCommand.CanExecute(null));
        Assert.True(dialog.ApplyCommand.CanExecute(null));
        Assert.True(okChanges > 0);
    }

    /// <summary>
    ///     Test that a failed save shows an error message, keeps the dialog open, and can be retried.
    /// </summary>
    [Fact]
    public void SettingsDialogViewModel_Ok_SaveFails_ShowsErrorAndStaysOpen()
    {
        // Arrange: a service whose save fails
        var service = Substitute.For<ISettingsService>();
        service.Current.Returns(new AppSettings());
        service.When(s => s.Save(Arg.Any<AppSettings>())).Do(_ => throw new IOException("disk full"));
        var page = new FakeSettingsPage("Page");
        var dialog = new SettingsDialogViewModel(service, [page]);
        var closeRequests = 0;
        dialog.CloseRequested += (_, _) => closeRequests++;

        // Act: choose OK
        dialog.OkCommand.Execute(null);

        // Assert: the error is shown, the dialog did not close, and the page was not told it closed
        Assert.Contains("disk full", dialog.ErrorMessage);
        Assert.Equal(0, closeRequests);
        Assert.Equal(0, page.ClosedCount);
    }

    /// <summary>
    ///     Test that closing through Cancel tells each page exactly once.
    /// </summary>
    [Fact]
    public void SettingsDialogViewModel_Cancel_NotifiesPagesOnce()
    {
        // Arrange: two pages
        var service = new SettingsService(_tempRoot);
        var first = new FakeSettingsPage("First");
        var second = new FakeSettingsPage("Second");
        var dialog = new SettingsDialogViewModel(service, [first, second]);

        // Act: cancel, then report the window closing as the view does
        dialog.CancelCommand.Execute(null);
        dialog.NotifyClosed();

        // Assert: each page was told once
        Assert.Equal(1, first.ClosedCount);
        Assert.Equal(1, second.ClosedCount);
    }

    /// <summary>
    ///     Test that closing the window without a command still tells the pages, once.
    /// </summary>
    [Fact]
    public void SettingsDialogViewModel_NotifyClosed_CalledTwice_NotifiesPagesOnce()
    {
        // Arrange: a page
        var service = new SettingsService(_tempRoot);
        var page = new FakeSettingsPage("Page");
        var dialog = new SettingsDialogViewModel(service, [page]);

        // Act: the window is closed (twice, to prove idempotence)
        dialog.NotifyClosed();
        dialog.NotifyClosed();

        // Assert: the page was told once
        Assert.Equal(1, page.ClosedCount);
    }

    /// <summary>
    ///     Test that a dialog without pages is valid and has no selection.
    /// </summary>
    [Fact]
    public void SettingsDialogViewModel_New_NoPages_HasNoSelectionAndIsValid()
    {
        // Arrange / Act: a dialog without pages
        var dialog = new SettingsDialogViewModel(new SettingsService(_tempRoot), []);

        // Assert: nothing is selected and the dialog can be applied
        Assert.Null(dialog.SelectedPage);
        Assert.True(dialog.AllPagesValid);
    }
}
