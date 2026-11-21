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

using DemaConsulting.CanvasPilot.PreviewSubsystem;

namespace DemaConsulting.CanvasPilot.Tests;

/// <summary>
///     Tests for the <see cref="ExportPresetDialogViewModel" /> and the <see cref="ExportPreset" /> value of the Export
///     PNG dialog unit.
/// </summary>
public sealed class ExportPresetDialogTests
{
    /// <summary>
    ///     A size of the application with a width and a height.
    /// </summary>
    private static readonly ExportPreset Slide =
        new("app:slide", "PowerPoint 16:9", 1920, 1080, null, false, "#FFFFFF", ExportPresetOrigin.Application);

    /// <summary>
    ///     A size of the style kit with only a width.
    /// </summary>
    private static readonly ExportPreset Mail =
        new("kit:mail", "Mail", 800, null, 150, false, "#102030", ExportPresetOrigin.StyleKit);

    /// <summary>
    ///     A transparent size of the style kit with only a height.
    /// </summary>
    private static readonly ExportPreset Logo =
        new("kit:logo", "Logo", null, 256, null, true, null, ExportPresetOrigin.StyleKit);

    /// <summary>
    ///     Test that the dialog lists the choices in the order given, with a size line for each, and selects the first
    ///     when nothing was chosen before.
    /// </summary>
    [Fact]
    public void ExportPresetDialogViewModel_New_Presets_ListsInOrderWithSizeLinesAndSelectsFirst()
    {
        // Arrange / Act: a dialog over as rendered and three sizes
        var viewModel = new ExportPresetDialogViewModel([ExportPreset.AsRendered, Slide, Mail, Logo]);

        // Assert: names, size lines and groups are those of the presets, and as rendered is selected
        Assert.Equal("Export PNG", viewModel.Title);
        Assert.Equal(["As rendered", "PowerPoint 16:9", "Mail", "Logo"], viewModel.Items.Select(i => i.Name));
        Assert.Equal(
            ["As shown", "1920 x 1080 px, on #FFFFFF", "800 px wide, on #102030, 150 dpi", "256 px high, transparent"],
            viewModel.Items.Select(i => i.SizeLine));
        Assert.Equal(["As shown", "Application", "Style kit", "Style kit"], viewModel.Items.Select(i => i.Group));
        Assert.Same(ExportPreset.AsRendered, viewModel.SelectedPreset);
        Assert.True(viewModel.ExportCommand.CanExecute(null));
    }

    /// <summary>
    ///     Test that the choice of the last time is selected when it is still offered, found by identifier, and the first
    ///     is selected when it is not.
    /// </summary>
    [Fact]
    public void ExportPresetDialogViewModel_New_LastChoice_IsSelectedWhenStillOffered()
    {
        // Arrange / Act: dialogs with the last choice present (as an equal value), and absent
        var present = new ExportPresetDialogViewModel([ExportPreset.AsRendered, Slide, Mail], Mail with { Name = "Mail" });
        var absent = new ExportPresetDialogViewModel([ExportPreset.AsRendered, Slide], Mail);

        // Assert: the one that is still offered is selected, else the first
        Assert.Equal("kit:mail", present.SelectedPreset!.Id);
        Assert.Same(ExportPreset.AsRendered, absent.SelectedPreset);
    }

    /// <summary>
    ///     Test that the result line says what the file will be for each kind of choice and follows the selection.
    /// </summary>
    [Fact]
    public void ExportPresetDialogViewModel_ResultText_EachKindOfChoice_SaysWhatTheFileWillBe()
    {
        // Arrange: a dialog and the changes of its result text
        var viewModel = new ExportPresetDialogViewModel([ExportPreset.AsRendered, Slide, Mail, Logo]);
        var changes = new List<string?>();
        viewModel.PropertyChanged += (_, e) => changes.Add(e.PropertyName);
        var texts = new List<string>();

        // Act: select each line in turn
        foreach (var item in viewModel.Items)
        {
            viewModel.SelectedItem = item;
            texts.Add(viewModel.ResultText);
        }

        // Assert: the sentences name the size, the proportions, the background or transparency, and the change was announced
        Assert.Equal("Result: the picture as it is shown, with its own size and background.", texts[0]);
        Assert.Equal("Result: 1920 x 1080 px, the picture fitted inside with its proportions kept. The background is #FFFFFF.", texts[1]);
        Assert.Equal("Result: 800 px wide, as high as the picture needs. The background is #102030.", texts[2]);
        Assert.Equal("Result: 256 px high, as wide as the picture needs. The background stays transparent.", texts[3]);
        Assert.Contains(nameof(ExportPresetDialogViewModel.ResultText), changes);
        Assert.Contains(nameof(ExportPresetDialogViewModel.SelectedPreset), changes);
    }

    /// <summary>
    ///     Test that Export accepts the selection and asks the host to close, that Cancel does not accept, and that with
    ///     nothing to choose Export cannot run.
    /// </summary>
    [Fact]
    public void ExportPresetDialogViewModel_ExportAndCancel_AcceptOrNot()
    {
        // Arrange: a dialog with a chosen size and one with nothing to choose
        var viewModel = new ExportPresetDialogViewModel([ExportPreset.AsRendered, Slide]);
        var closes = new List<bool>();
        viewModel.CloseRequested += (_, e) => closes.Add(e.Accepted);
        viewModel.SelectedItem = viewModel.Items[1];
        var empty = new ExportPresetDialogViewModel([]);

        // Act: cancel, then export
        viewModel.CancelCommand.Execute(null);
        var acceptedAfterCancel = viewModel.Accepted;
        viewModel.ExportCommand.Execute(null);

        // Assert: cancel reported not accepted, export reported accepted with the selection, and the empty dialog cannot export
        Assert.False(acceptedAfterCancel);
        Assert.Equal([false, true], closes);
        Assert.True(viewModel.Accepted);
        Assert.Equal("app:slide", viewModel.SelectedPreset!.Id);
        Assert.False(empty.ExportCommand.CanExecute(null));
        Assert.Null(empty.SelectedPreset);
        Assert.Equal(string.Empty, empty.ResultText);
    }

    /// <summary>
    ///     Test the value of a preset: the settings that it gives for the renderer, with a transparent preset dropping its
    ///     background, and the picture as shown asking for nothing.
    /// </summary>
    [Fact]
    public void ExportPreset_ToRenderSettings_GivesTheSettingsOfThePreset()
    {
        // Act / Assert: an opaque size keeps its background and resolution, a transparent one drops its background
        Assert.Equal(new RenderSettings(1920, 1080, false, "#FFFFFF", null), Slide.ToRenderSettings());
        Assert.Equal(new RenderSettings(800, null, false, "#102030", 150), Mail.ToRenderSettings());
        var transparentWithBackground = Logo with { BackgroundHex = "#123456" };
        Assert.Equal(new RenderSettings(null, 256, true, null, null), transparentWithBackground.ToRenderSettings());
        Assert.True(ExportPreset.AsRendered.IsAsRendered);
        Assert.True(ExportPreset.AsRendered.ToRenderSettings().IsAsRendered);
        Assert.Equal("Natural size", (Slide with { Width = null, Height = null, BackgroundHex = null }).DescribeSize());
    }

    /// <summary>
    ///     Test that a missing preset list or preset is refused.
    /// </summary>
    [Fact]
    public void ExportPresetDialogViewModel_InvalidArguments_Throw()
    {
        // Act / Assert: nulls are refused
        Assert.Throws<ArgumentNullException>(() => new ExportPresetDialogViewModel(null!));
        Assert.Throws<ArgumentNullException>(() => new ExportPresetItemViewModel(null!));
    }
}
