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
using Avalonia.Interactivity;

namespace DemaConsulting.CanvasPilot.StyleSubsystem;

/// <summary>
///     The window in which one style kit is edited, bound to a <see cref="StyleKitEditorViewModel" />: tabs for the general
///     values, the palette, the fonts and numbers, the Mermaid and Vega-Lite parts, the advice, the export sizes, and the
///     example images, with the errors and warnings of the draft and the Save and Cancel buttons at the bottom.
/// </summary>
/// <remarks>
///     The window only shows what its view model holds and passes the removal of a row to it. It is shown modally over the
///     window that manages kits and closes when the view model asks it to, after a save or a cancel.
/// </remarks>
public sealed partial class StyleKitEditorView : Window
{
    /// <summary>
    ///     The view model bound to the window, or <see langword="null" /> when the window was made for the XAML loader.
    /// </summary>
    private readonly StyleKitEditorViewModel? _viewModel;

    /// <summary>
    ///     Initializes a new instance of the <see cref="StyleKitEditorView" /> class for the XAML loader. The window has no
    ///     content until a view model is supplied.
    /// </summary>
    public StyleKitEditorView()
    {
        InitializeComponent();
    }

    /// <summary>
    ///     Initializes a new instance of the <see cref="StyleKitEditorView" /> class.
    /// </summary>
    /// <param name="viewModel">The view model that backs the window.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="viewModel" /> is <see langword="null" />.</exception>
    public StyleKitEditorView(StyleKitEditorViewModel viewModel)
        : this()
    {
        ArgumentNullException.ThrowIfNull(viewModel);

        _viewModel = viewModel;
        DataContext = viewModel;
        viewModel.CloseRequested += OnCloseRequested;
    }

    /// <inheritdoc />
    protected override void OnClosed(EventArgs e)
    {
        if (_viewModel is not null)
        {
            _viewModel.CloseRequested -= OnCloseRequested;
        }

        base.OnClosed(e);
    }

    /// <summary>
    ///     Removes the row whose Remove button was clicked from the list that holds it.
    /// </summary>
    /// <param name="sender">The button.</param>
    /// <param name="e">The event data.</param>
    private void OnRemoveRowClick(object? sender, RoutedEventArgs e)
    {
        if (_viewModel is null || sender is not Control { DataContext: { } row })
        {
            return;
        }

        switch (row)
        {
            case StyleColorRowViewModel color:
                _viewModel.RemoveColorCommand.Execute(color);
                break;
            case StyleNameValueRowViewModel variable:
                _viewModel.RemoveMermaidVariableCommand.Execute(variable);
                break;
            case StyleTextRowViewModel line:
                _viewModel.RemoveGuidanceItemCommand.Execute(line);
                break;
            case StylePresetRowViewModel preset:
                _viewModel.RemovePresetCommand.Execute(preset);
                break;
            case StyleExampleRowViewModel example:
                _viewModel.RemoveExampleCommand.Execute(example);
                break;
        }
    }

    /// <summary>
    ///     Closes the window.
    /// </summary>
    /// <param name="sender">The view model.</param>
    /// <param name="e">The close request.</param>
    private void OnCloseRequested(object? sender, EventArgs e)
    {
        Close();
    }
}
