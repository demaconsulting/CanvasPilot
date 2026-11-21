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

using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using DemaConsulting.CanvasPilot.PreviewSubsystem;

namespace DemaConsulting.CanvasPilot.UiTests;

/// <summary>
///     Headless UI tests for the Export PNG dialog window.
/// </summary>
public sealed class ExportPresetDialogViewTests
{
    /// <summary>
    ///     The sizes the dialog under test offers after the picture as it is shown.
    /// </summary>
    private static readonly ExportPreset[] Presets =
    [
        ExportPreset.AsRendered,
        new("kit:slide", "Slide", 1920, 1080, null, false, "#0F172A", ExportPresetOrigin.StyleKit),
        new("app:mail", "Email", 800, null, null, false, "#FFFFFF", ExportPresetOrigin.Application)
    ];

    /// <summary>
    ///     Test that the dialog shows a line for each choice with the first selected, the result line, and Export
    ///     enabled.
    /// </summary>
    [AvaloniaFact]
    public void ExportPresetDialogView_Show_ListsChoicesAndSelectsFirst()
    {
        // Arrange / Act: show the dialog
        var (window, viewModel) = ShowDialog();

        // Assert: three lines, the first selected, a result sentence, and both buttons usable
        Assert.Equal("Export PNG", window.Title);
        var list = SettingsUiHelpers.Find<ListBox>(window, "ExportPresetList");
        Assert.Equal(3, list.ItemCount);
        Assert.Same(viewModel.Items[0], list.SelectedItem);
        Assert.StartsWith("Result: the picture as it is shown", SettingsUiHelpers.Find<TextBlock>(window, "ExportPresetResult").Text, StringComparison.Ordinal);
        Assert.True(SettingsUiHelpers.Find<Button>(window, "ExportPresetOkButton").IsEffectivelyEnabled);
        Assert.True(SettingsUiHelpers.Find<Button>(window, "ExportPresetCancelButton").IsEffectivelyEnabled);

        window.Close();
    }

    /// <summary>
    ///     Test that selecting another line changes the result sentence, and that Export closes the window and accepts
    ///     the selected choice.
    /// </summary>
    [AvaloniaFact]
    public void ExportPresetDialogView_SelectSizeAndExport_ShowsResultAndAccepts()
    {
        // Arrange: a shown dialog
        var (window, viewModel) = ShowDialog();
        var closed = false;
        window.Closed += (_, _) => closed = true;

        // Act: select the slide size and click Export
        SettingsUiHelpers.Find<ListBox>(window, "ExportPresetList").SelectedItem = viewModel.Items[1];
        var result = SettingsUiHelpers.Find<TextBlock>(window, "ExportPresetResult").Text;
        SettingsUiHelpers.Click(window, SettingsUiHelpers.Find<Button>(window, "ExportPresetOkButton"));

        // Assert: the sentence named the size, the window closed, and the dialog accepted the slide size
        Assert.Contains("1920 x 1080 px", result, StringComparison.Ordinal);
        Assert.True(closed);
        Assert.True(viewModel.Accepted);
        Assert.Equal("kit:slide", viewModel.SelectedPreset!.Id);
    }

    /// <summary>
    ///     Test that Cancel closes the window without accepting.
    /// </summary>
    [AvaloniaFact]
    public void ExportPresetDialogView_CancelButton_ClosesWindowWithoutAccepting()
    {
        // Arrange: a shown dialog
        var (window, viewModel) = ShowDialog();

        // Act: click Cancel
        SettingsUiHelpers.Click(window, SettingsUiHelpers.Find<Button>(window, "ExportPresetCancelButton"));

        // Assert: the window closed and nothing was accepted
        Assert.False(window.IsVisible);
        Assert.False(viewModel.Accepted);
    }

    /// <summary>
    ///     Shows the dialog over the sizes of the test.
    /// </summary>
    /// <returns>The window and its view model.</returns>
    private static (ExportPresetDialogView Window, ExportPresetDialogViewModel ViewModel) ShowDialog()
    {
        var viewModel = new ExportPresetDialogViewModel(Presets);
        var window = new ExportPresetDialogView(viewModel);
        window.Show();
        return (window, viewModel);
    }
}
