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

using System.Reflection;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace DemaConsulting.CanvasPilot.AppShellSubsystem;

/// <summary>
///     An open source library that the application is built on, as listed in the About dialog.
/// </summary>
/// <param name="Name">The library name.</param>
/// <param name="Description">What the application uses the library for.</param>
public sealed record AboutDependency(string Name, string Description);

/// <summary>
///     View model of the About dialog: the application name, tagline, version, copyright, and the open source
///     libraries it is built on.
/// </summary>
public sealed partial class AboutDialogViewModel : ObservableObject
{
    /// <summary>
    ///     The application name.
    /// </summary>
    public const string ApplicationName = "CanvasPilot";

    /// <summary>
    ///     The application tagline.
    /// </summary>
    public const string ApplicationTagline = "AI-assisted drawing creation with CanvasNet";

    /// <summary>
    ///     The copyright notice.
    /// </summary>
    public const string CopyrightNotice = "Copyright (c) DEMA Consulting";

    /// <summary>
    ///     Creates the dialog view model.
    /// </summary>
    /// <param name="version">
    ///     The version to show, or <see langword="null" /> to use the informational version of the application
    ///     assembly.
    /// </param>
    public AboutDialogViewModel(string? version = null)
    {
        Version = string.IsNullOrWhiteSpace(version) ? GetApplicationVersion() : version.Trim();
    }

    /// <summary>
    ///     Raised when the dialog asks its host to close.
    /// </summary>
    public event EventHandler? CloseRequested;

    /// <summary>
    ///     Gets the dialog title.
    /// </summary>
    public string Title { get; } = "About " + ApplicationName;

    /// <summary>
    ///     Gets the application name.
    /// </summary>
    public string Name { get; } = ApplicationName;

    /// <summary>
    ///     Gets the application tagline.
    /// </summary>
    public string Tagline { get; } = ApplicationTagline;

    /// <summary>
    ///     Gets the application version, without build metadata.
    /// </summary>
    public string Version { get; }

    /// <summary>
    ///     Gets the version as shown to the user.
    /// </summary>
    public string VersionText => "Version " + Version;

    /// <summary>
    ///     Gets the copyright notice.
    /// </summary>
    public string Copyright { get; } = CopyrightNotice;

    /// <summary>
    ///     Gets the open source libraries the application is built on.
    /// </summary>
    public IReadOnlyList<AboutDependency> Dependencies { get; } =
    [
        new("Avalonia", "Cross-platform user interface framework"),
        new("Avalonia.AvaloniaEdit", "Text editing control that the message composer is built on"),
        new("CommunityToolkit.Mvvm", "Model-view-view model helpers"),
        new("DemaConsulting.CanvasNet", "Composes SVG, Mermaid, Vega-Lite, and other sources into images"),
        new("DemaConsulting.Speech", "Speech recognition and synthesis models and audio devices"),
        new("Fluent System Icons", "The line icons of the user interface, by Microsoft Corporation (MIT license)")
    ];

    /// <summary>
    ///     Closes the dialog.
    /// </summary>
    [RelayCommand]
    private void Close()
    {
        CloseRequested?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    ///     Reads the informational version of the application assembly, dropping build metadata after a plus sign.
    /// </summary>
    /// <returns>The version text, or <c>0.0.0</c> when the assembly has none.</returns>
    private static string GetApplicationVersion()
    {
        var informational = typeof(AboutDialogViewModel).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
        if (string.IsNullOrWhiteSpace(informational))
        {
            return "0.0.0";
        }

        var plus = informational.IndexOf('+', StringComparison.Ordinal);
        return plus > 0 ? informational[..plus] : informational;
    }
}
