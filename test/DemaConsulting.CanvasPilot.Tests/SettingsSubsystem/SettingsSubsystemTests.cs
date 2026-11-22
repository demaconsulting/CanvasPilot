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
///     Subsystem tests for the Settings subsystem: <see cref="SettingsService" />, the dialog view model, and the
///     AI provider page working together on a temporary settings folder.
/// </summary>
public sealed class SettingsSubsystemTests : IDisposable
{
    /// <summary>
    ///     Temporary settings folder created fresh for each test and removed on disposal.
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
    ///     Test that the layout of the window, the look of the application, and the times of opening of projects are
    ///     stored with the other settings, survive a restart, and are kept when the dialog stores its own choices.
    /// </summary>
    [Fact]
    public void SettingsSubsystem_RememberLayoutAndAppearance_SavedWithOthers_SurviveRestartAndDialogChanges()
    {
        // Arrange: a stored layout, a pinned theme, a listed project with its time, and a dialog over the AI provider page
        var opened = new DateTimeOffset(2026, 11, 20, 9, 15, 0, TimeSpan.Zero);
        var service = new SettingsService(_tempRoot);
        service.Save(new AppSettings
        {
            RecentProjects = ["C:\\a"],
            RecentProjectOpened = new Dictionary<string, DateTimeOffset> { ["C:\\a"] = opened },
            Appearance = new AppearanceSettings { Theme = ThemeChoice.Light, ComfortableRows = true },
            Window = new WindowSettings { Width = 1200, Height = 760, ExplorerWidth = 280, ChatWidth = 480 }
        });
        var page = new AiProviderSettingsPageViewModel();
        var dialog = new SettingsDialogViewModel(service, [page]);
        page.ModelName = "llama3";

        // Act: choose OK, then start a fresh service as after a restart
        dialog.OkCommand.Execute(null);
        var restarted = new SettingsService(_tempRoot).Load();

        // Assert: the dialog's choice was stored, and the layout, the look, and the time are as they were
        Assert.Equal("llama3", restarted.AiProvider.ModelName);
        Assert.Equal(ThemeChoice.Light, restarted.Appearance.Theme);
        Assert.True(restarted.Appearance.ComfortableRows);
        Assert.Equal(1200, restarted.Window.Width);
        Assert.Equal(760, restarted.Window.Height);
        Assert.Equal(280, restarted.Window.ExplorerWidth);
        Assert.Equal(480, restarted.Window.ChatWidth);
        Assert.Equal(opened, restarted.RecentProjectOpened["C:\\a"]);
    }

    /// <summary>
    ///     Test that the theme and the explorer options chosen on the Appearance page and accepted with OK are stored,
    ///     are present after a restart, and leave the choices of the other pages as they were.
    /// </summary>
    [Fact]
    public void SettingsSubsystem_Appearance_DialogOk_StoresThemeAndOptions()
    {
        // Arrange: stored AI provider settings, and a dialog over the AI provider page and the Appearance page
        var service = new SettingsService(_tempRoot);
        service.Save(new AppSettings { AiProvider = new AiProviderSettings { ModelName = "gpt-x" } });
        var provider = new AiProviderSettingsPageViewModel();
        var appearance = new AppearanceSettingsPageViewModel();
        var dialog = new SettingsDialogViewModel(service, [provider, appearance]);

        // Act: choose the dark theme without thumbnails on the Appearance page, choose OK, and restart
        dialog.SelectedPage = appearance;
        appearance.SelectedTheme = appearance.Themes[1];
        appearance.ShowThumbnails = false;
        dialog.OkCommand.Execute(null);
        var restarted = new SettingsService(_tempRoot).Load();

        // Assert: the appearance was stored and the model name of the other page is unchanged
        Assert.Equal(ThemeChoice.Dark, restarted.Appearance.Theme);
        Assert.False(restarted.Appearance.ShowThumbnails);
        Assert.False(restarted.Appearance.ComfortableRows);
        Assert.Equal("gpt-x", restarted.AiProvider.ModelName);
    }

    /// <summary>
    ///     Test that choices made in the dialog survive an application restart.
    /// </summary>
    [Fact]
    public void SettingsSubsystem_PersistSettings_DialogOk_SurvivesRestart()
    {
        // Arrange: a dialog over the AI provider page, with a recent project already stored
        var service = new SettingsService(_tempRoot);
        service.Save(new AppSettings { RecentProjects = ["C:\\a"] });
        var page = new AiProviderSettingsPageViewModel();
        var dialog = new SettingsDialogViewModel(service, [page]);
        page.SelectedProvider = page.Providers[1];
        page.ModelName = "llama3";
        page.UnrestrictedAgentTools = true;

        // Act: choose OK, then start a fresh service as after a restart
        dialog.OkCommand.Execute(null);
        var restarted = new SettingsService(_tempRoot).Load();

        // Assert: the choices were stored and the value owned by another subsystem was preserved
        Assert.Equal(AiProviderKind.Ollama, restarted.AiProvider.Provider);
        Assert.Equal("llama3", restarted.AiProvider.ModelName);
        Assert.True(restarted.AiProvider.UnrestrictedAgentTools);
        Assert.Equal(["C:\\a"], restarted.RecentProjects);
    }

    /// <summary>
    ///     Test that the style settings are stored with the other settings, survive a restart, and are kept when the
    ///     dialog stores its own choices.
    /// </summary>
    [Fact]
    public void SettingsSubsystem_KeepStyleSettings_SavedWithOthers_SurviveRestartAndOtherChanges()
    {
        // Arrange: stored style settings, and a dialog over the AI provider page
        var service = new SettingsService(_tempRoot);
        var shared = Path.Combine(_tempRoot, "team-styles");
        service.Save(new AppSettings { Styles = new StyleSettings { SharedKitFolders = [shared], DefaultKitId = "dema" } });
        var page = new AiProviderSettingsPageViewModel();
        var dialog = new SettingsDialogViewModel(service, [page]);
        page.ModelName = "llama3";

        // Act: choose OK, then start a fresh service as after a restart
        dialog.OkCommand.Execute(null);
        var restarted = new SettingsService(_tempRoot).Load();

        // Assert: the dialog's choice was stored and the style settings are as they were
        Assert.Equal("llama3", restarted.AiProvider.ModelName);
        Assert.Equal([shared], restarted.Styles.SharedKitFolders);
        Assert.Equal("dema", restarted.Styles.DefaultKitId);
    }

    /// <summary>
    ///     Test that canceling the dialog leaves the stored settings untouched.
    /// </summary>
    [Fact]
    public void SettingsSubsystem_EditSettings_DialogCancel_LeavesStoredSettingsUnchanged()
    {
        // Arrange: stored default settings and a dialog with a pending edit
        var service = new SettingsService(_tempRoot);
        service.Save(new AppSettings());
        var page = new AiProviderSettingsPageViewModel();
        var dialog = new SettingsDialogViewModel(service, [page]);
        page.ModelName = "should-not-persist";

        // Act: cancel
        dialog.CancelCommand.Execute(null);

        // Assert: neither the settings in effect nor the stored file changed
        Assert.Equal(string.Empty, service.Current.AiProvider.ModelName);
        Assert.Equal(string.Empty, new SettingsService(_tempRoot).Load().AiProvider.ModelName);
    }

    /// <summary>
    ///     Test that a damaged settings file does not stop the dialog from opening and saving.
    /// </summary>
    [Fact]
    public void SettingsSubsystem_ToleratePoorFiles_CorruptFile_DialogStillWorks()
    {
        // Arrange: a corrupt settings file, loaded at startup
        File.WriteAllText(Path.Combine(_tempRoot, "settings.json"), "}{");
        var service = new SettingsService(_tempRoot);
        service.Load();
        var page = new AiProviderSettingsPageViewModel();
        var dialog = new SettingsDialogViewModel(service, [page]);
        page.ModelName = "recovered";

        // Act: choose OK
        dialog.OkCommand.Execute(null);

        // Assert: a fresh settings file holds the choice and the damaged file was kept aside
        Assert.Equal("recovered", new SettingsService(_tempRoot).Load().AiProvider.ModelName);
        Assert.True(File.Exists(Path.Combine(_tempRoot, "settings.json.bad")));
    }
}
