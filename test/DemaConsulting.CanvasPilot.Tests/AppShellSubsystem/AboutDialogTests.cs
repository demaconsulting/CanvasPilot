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

using DemaConsulting.CanvasPilot.AppShellSubsystem;

namespace DemaConsulting.CanvasPilot.Tests;

/// <summary>
///     Unit tests for the AboutDialog unit: the About view model.
/// </summary>
public sealed class AboutDialogTests
{
    /// <summary>
    ///     Test that the dialog shows the application name, tagline, copyright, and an informational version
    ///     without build metadata.
    /// </summary>
    [Fact]
    public void AboutDialogViewModel_New_ShowsIdentityAndVersion()
    {
        // Arrange & Act: create the dialog with the assembly's version
        var viewModel = new AboutDialogViewModel();

        // Assert: the identity texts are shown and the version has no build metadata
        Assert.Equal("About CanvasPilot", viewModel.Title);
        Assert.Equal("CanvasPilot", viewModel.Name);
        Assert.Equal("AI-assisted drawing creation with CanvasNet", viewModel.Tagline);
        Assert.Equal("Copyright (c) DEMA Consulting", viewModel.Copyright);
        Assert.False(string.IsNullOrWhiteSpace(viewModel.Version));
        Assert.DoesNotContain('+', viewModel.Version);
        Assert.Equal("Version " + viewModel.Version, viewModel.VersionText);
    }

    /// <summary>
    ///     Test that a supplied version is shown trimmed, and that a blank one falls back to the assembly version.
    /// </summary>
    [Theory]
    [InlineData(" 2.5.1 ", "2.5.1")]
    [InlineData("1.0.0-beta.3", "1.0.0-beta.3")]
    public void AboutDialogViewModel_New_SuppliedVersion_IsShownTrimmed(string supplied, string expected)
    {
        // Arrange & Act: create the dialog with a supplied version
        var viewModel = new AboutDialogViewModel(supplied);

        // Assert: the supplied version is shown
        Assert.Equal(expected, viewModel.Version);
    }

    /// <summary>
    ///     Test that a blank version is replaced by the assembly version.
    /// </summary>
    [Fact]
    public void AboutDialogViewModel_New_BlankVersion_UsesAssemblyVersion()
    {
        // Arrange & Act: create one dialog with a blank version and one with none
        var blank = new AboutDialogViewModel("  ");
        var none = new AboutDialogViewModel();

        // Assert: both show the assembly version
        Assert.Equal(none.Version, blank.Version);
    }

    /// <summary>
    ///     Test that the dialog lists the open source libraries the application is built on.
    /// </summary>
    [Fact]
    public void AboutDialogViewModel_Dependencies_ListsOpenSourceLibraries()
    {
        // Arrange & Act: create the dialog
        var viewModel = new AboutDialogViewModel();

        // Assert: the six libraries are listed with a description each
        Assert.Equal(
            ["Avalonia", "Avalonia.AvaloniaEdit", "CommunityToolkit.Mvvm", "DemaConsulting.CanvasNet", "DemaConsulting.Speech", "Fluent System Icons"],
            viewModel.Dependencies.Select(dependency => dependency.Name));
        Assert.All(viewModel.Dependencies, dependency => Assert.False(string.IsNullOrWhiteSpace(dependency.Description)));
    }

    /// <summary>
    ///     Test that Close asks the host to close the dialog.
    /// </summary>
    [Fact]
    public void AboutDialogViewModel_Close_RequestsClose()
    {
        // Arrange: a dialog with a listener
        var viewModel = new AboutDialogViewModel();
        var requests = 0;
        viewModel.CloseRequested += (_, _) => requests++;

        // Act: run Close
        viewModel.CloseCommand.Execute(null);

        // Assert: one close was requested
        Assert.Equal(1, requests);
    }
}
