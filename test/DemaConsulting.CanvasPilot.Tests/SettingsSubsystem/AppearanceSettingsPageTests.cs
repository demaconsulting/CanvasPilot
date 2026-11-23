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

namespace DemaConsulting.CanvasPilot.Tests;

/// <summary>
///     Unit tests for <see cref="AppearanceSettingsPageViewModel" />.
/// </summary>
public sealed class AppearanceSettingsPageTests
{
    /// <summary>
    ///     Test that a new page is titled Appearance, lists the three themes in order, shows the default look, and is
    ///     always valid.
    /// </summary>
    [Fact]
    public void AppearanceSettingsPage_New_ListsThemesInOrderAndShowsTheDefaultLook()
    {
        // Arrange / Act: a new page
        var page = new AppearanceSettingsPageViewModel();

        // Assert: title, themes, defaults, and validity
        Assert.Equal("Appearance", page.Title);
        Assert.Equal(
            [("Follow system", ThemeChoice.FollowSystem), ("Dark", ThemeChoice.Dark), ("Light", ThemeChoice.Light)],
            page.Themes.Select(choice => (choice.Label, choice.Value)));
        Assert.Equal(ThemeChoice.FollowSystem, page.SelectedTheme.Value);
        Assert.True(page.ShowThumbnails);
        Assert.False(page.ComfortableRows);
        Assert.True(page.IsValid);
    }

    /// <summary>
    ///     Test that loading settings shows the stored theme and explorer options.
    /// </summary>
    [Fact]
    public void AppearanceSettingsPage_Load_StoredAppearance_IsShown()
    {
        // Arrange: settings with a pinned light theme, no thumbnails, and tall rows
        var page = new AppearanceSettingsPageViewModel();
        var settings = new AppSettings
        {
            Appearance = new AppearanceSettings { Theme = ThemeChoice.Light, ShowThumbnails = false, ComfortableRows = true }
        };

        // Act: load them
        page.Load(settings);

        // Assert: the page shows them
        Assert.Equal(ThemeChoice.Light, page.SelectedTheme.Value);
        Assert.False(page.ShowThumbnails);
        Assert.True(page.ComfortableRows);
    }

    /// <summary>
    ///     Test that applying merges the edited appearance and leaves every other setting as it was.
    /// </summary>
    [Fact]
    public void AppearanceSettingsPage_Apply_EditedValues_MergesOnlyTheAppearance()
    {
        // Arrange: settings with other values, loaded into the page, and then edited
        var original = new AppSettings
        {
            RecentProjects = ["C:\\a"],
            AiProvider = new AiProviderSettings { ModelName = "gpt-x" },
            Window = new WindowSettings { Width = 1200 }
        };
        var page = new AppearanceSettingsPageViewModel();
        page.Load(original);
        page.SelectedTheme = page.Themes[1];
        page.ShowThumbnails = false;
        page.ComfortableRows = true;

        // Act: apply
        var applied = page.Apply(original);

        // Assert: the appearance is the edited one, and everything else is the same
        Assert.Equal(new AppearanceSettings { Theme = ThemeChoice.Dark, ShowThumbnails = false, ComfortableRows = true }, applied.Appearance);
        Assert.Equal(original with { Appearance = applied.Appearance }, applied);
        Assert.Equal(["C:\\a"], applied.RecentProjects);
        Assert.Equal("gpt-x", applied.AiProvider.ModelName);
        Assert.Equal(1200, applied.Window.Width);
    }

    /// <summary>
    ///     Test that a page that was loaded and applied without editing gives back the same appearance.
    /// </summary>
    [Fact]
    public void AppearanceSettingsPage_LoadThenApply_NoEdit_KeepsTheAppearance()
    {
        // Arrange: stored appearance loaded into the page
        var settings = new AppSettings
        {
            Appearance = new AppearanceSettings { Theme = ThemeChoice.Dark, ShowThumbnails = false }
        };
        var page = new AppearanceSettingsPageViewModel();
        page.Load(settings);

        // Act / Assert: applying without an edit changes nothing
        Assert.Equal(settings.Appearance, page.Apply(settings).Appearance);
    }

    /// <summary>
    ///     Test that a null argument is refused by Load and Apply.
    /// </summary>
    [Fact]
    public void AppearanceSettingsPage_LoadAndApply_NullSettings_Throw()
    {
        // Arrange
        var page = new AppearanceSettingsPageViewModel();

        // Act / Assert
        Assert.Throws<ArgumentNullException>(() => page.Load(null!));
        Assert.Throws<ArgumentNullException>(() => page.Apply(null!));
    }
}
