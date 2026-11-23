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

using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;
using DemaConsulting.CanvasPilot.AppShellSubsystem;
using DemaConsulting.CanvasPilot.LoggingSubsystem;
using DemaConsulting.CanvasPilot.PreviewSubsystem;
using DemaConsulting.CanvasPilot.ProjectSubsystem;
using DemaConsulting.CanvasPilot.SettingsSubsystem;

namespace DemaConsulting.CanvasPilot.UiTests;

/// <summary>
///     Refresh scheduler for UI tests: it never runs the callback, so a test refreshes the explorer explicitly and
///     no test waits for a delay.
/// </summary>
internal sealed class NeverRefreshScheduler : IRefreshScheduler
{
    /// <inheritdoc />
    public IDisposable Schedule(TimeSpan delay, Action callback)
    {
        return new NoopDisposable();
    }

    /// <summary>
    ///     A disposable that does nothing.
    /// </summary>
    private sealed class NoopDisposable : IDisposable
    {
        /// <inheritdoc />
        public void Dispose()
        {
            // Nothing was scheduled, so there is nothing to cancel.
        }
    }
}

/// <summary>
///     Image exporter for UI tests that can be switched on and counts its calls.
/// </summary>
internal sealed class SwitchableImageExporter : IImageExporter
{
    /// <summary>
    ///     Whether an image can be exported.
    /// </summary>
    private bool _canExport;

    /// <inheritdoc />
    public event EventHandler? CanExportChanged;

    /// <inheritdoc />
    public bool CanExport
    {
        get => _canExport;
        set
        {
            _canExport = value;
            CanExportChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>
    ///     Gets how many exports were requested.
    /// </summary>
    public int Exports { get; private set; }

    /// <summary>
    ///     Gets how many copies were requested.
    /// </summary>
    public int Copies { get; private set; }

    /// <inheritdoc />
    public Task ExportPngAsync(IExportDialogs dialogs)
    {
        Exports++;
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task CopyImageAsync(IExportDialogs dialogs)
    {
        Copies++;
        return Task.CompletedTask;
    }
}

/// <summary>
///     Helpers shared by the headless tests of the main window and its dialogs.
/// </summary>
internal static class AppShellUiHelpers
{
    /// <summary>
    ///     Composes the application on the given temporary root, as it is at startup but with a scheduler that never
    ///     refreshes by itself.
    /// </summary>
    /// <param name="root">The temporary root that holds the log, settings, and projects folders.</param>
    /// <param name="exporter">What the Image menu exports, or <see langword="null" /> for none.</param>
    /// <param name="application">The application whose theme the composition follows the settings for, or <see langword="null" />.</param>
    /// <param name="styleFolder">The folder of the style kits of the user, or <see langword="null" /> for the <c>styles</c> folder of the root.</param>
    /// <returns>The composition.</returns>
    public static ApplicationComposition Compose(string root, IImageExporter? exporter = null, Application? application = null, string? styleFolder = null)
    {
        var projects = Directory.CreateDirectory(Path.Combine(root, "projects")).FullName;
        var logger = new RollingFileLogger(Path.Combine(root, "logs"));
        var settings = new SettingsService(Path.Combine(root, "settings"), logger);
        return ApplicationComposition.Create(
            logger,
            settings,
            new NeverRefreshScheduler(),
            imageExporter: exporter,
            defaultProjectsFolder: projects,
            audioDevices: new FakeAudioDeviceService(),
            speechModels: new FakeSpeechModelService(),
            styleUserFolder: styleFolder ?? Path.Combine(root, "styles"),
            application: application);
    }

    /// <summary>
    ///     Lets the dispatcher run the work that is queued, so a dialog that a command started is shown.
    /// </summary>
    public static void Flush()
    {
        Dispatcher.UIThread.RunJobs();
    }

    /// <summary>
    ///     Finds the open dialog of a given type that is owned by a window.
    /// </summary>
    /// <typeparam name="T">The dialog window type.</typeparam>
    /// <param name="owner">The owner window.</param>
    /// <returns>The dialog.</returns>
    public static T FindDialog<T>(Window owner)
        where T : Window
    {
        Flush();
        return owner.OwnedWindows.OfType<T>().Single();
    }
}
