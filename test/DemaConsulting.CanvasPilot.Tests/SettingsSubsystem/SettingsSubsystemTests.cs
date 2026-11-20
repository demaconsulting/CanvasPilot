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
