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
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using DemaConsulting.CanvasPilot.AppShellSubsystem;
using DemaConsulting.CanvasPilot.LoggingSubsystem;

namespace DemaConsulting.CanvasPilot;

/// <summary>
///     Avalonia application entry point. When the classic desktop lifetime starts, it builds the units with
///     <see cref="ApplicationComposition" />, shows the <see cref="MainWindowView" />, records unhandled errors
///     in the application log, and disposes the units when the application exits.
/// </summary>
public sealed class App : Application
{
    /// <summary>
    ///     The units of the running application, or null before startup and after shutdown.
    /// </summary>
    private ApplicationComposition? _composition;

    /// <inheritdoc />
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    /// <inheritdoc />
    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var composition = ApplicationComposition.CreateDefault();
            _composition = composition;
            HookUnhandledErrors();

            desktop.MainWindow = new MainWindowView(composition.Shell);
            desktop.Exit += OnExit;
        }

        base.OnFrameworkInitializationCompleted();
    }

    /// <summary>
    ///     Starts recording errors that nothing else handles in the application log.
    /// </summary>
    private void HookUnhandledErrors()
    {
        AppDomain.CurrentDomain.UnhandledException += OnDomainUnhandledException;
        TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;
        Dispatcher.UIThread.UnhandledException += OnUiThreadUnhandledException;
    }

    /// <summary>
    ///     Stops recording unhandled errors.
    /// </summary>
    private void UnhookUnhandledErrors()
    {
        AppDomain.CurrentDomain.UnhandledException -= OnDomainUnhandledException;
        TaskScheduler.UnobservedTaskException -= OnUnobservedTaskException;
        Dispatcher.UIThread.UnhandledException -= OnUiThreadUnhandledException;
    }

    /// <summary>
    ///     Disposes the units when the application exits.
    /// </summary>
    /// <param name="sender">The application lifetime.</param>
    /// <param name="e">The exit details.</param>
    private void OnExit(object? sender, ControlledApplicationLifetimeExitEventArgs e)
    {
        UnhookUnhandledErrors();
        _composition?.Dispose();
        _composition = null;
    }

    /// <summary>
    ///     Records an exception that reached the top of any thread.
    /// </summary>
    /// <param name="sender">The application domain.</param>
    /// <param name="e">The error details.</param>
    private void OnDomainUnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        _composition?.Logger.Log(LogLevel.Error, "Unhandled exception.", e.ExceptionObject as Exception);
    }

    /// <summary>
    ///     Records the exception of a task that nobody awaited.
    /// </summary>
    /// <param name="sender">The task scheduler.</param>
    /// <param name="e">The error details.</param>
    private void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
    {
        _composition?.Logger.Log(LogLevel.Error, "Unobserved task exception.", e.Exception);
    }

    /// <summary>
    ///     Records an exception that reached the top of the user interface thread. It is not marked as handled.
    /// </summary>
    /// <param name="sender">The dispatcher.</param>
    /// <param name="e">The error details.</param>
    private void OnUiThreadUnhandledException(object? sender, DispatcherUnhandledExceptionEventArgs e)
    {
        _composition?.Logger.Log(LogLevel.Error, "Unhandled exception on the user interface thread.", e.Exception);
    }
}
