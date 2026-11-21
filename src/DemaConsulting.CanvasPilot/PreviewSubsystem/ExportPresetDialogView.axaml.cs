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

namespace DemaConsulting.CanvasPilot.PreviewSubsystem;

/// <summary>
///     The modal Export PNG dialog window, bound to an <see cref="ExportPresetDialogViewModel" />. The window closes
///     when the view model asks it to.
/// </summary>
public sealed partial class ExportPresetDialogView : Window
{
    /// <summary>
    ///     View model bound to the window.
    /// </summary>
    private readonly ExportPresetDialogViewModel? _viewModel;

    /// <summary>
    ///     Initializes a new instance of the <see cref="ExportPresetDialogView" /> class for the XAML loader. The
    ///     window has no data until a view model is supplied.
    /// </summary>
    public ExportPresetDialogView()
    {
        InitializeComponent();
    }

    /// <summary>
    ///     Initializes a new instance of the <see cref="ExportPresetDialogView" /> class.
    /// </summary>
    /// <param name="viewModel">The view model that backs the window.</param>
    public ExportPresetDialogView(ExportPresetDialogViewModel viewModel)
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
    ///     Closes the window, reporting whether the dialog was accepted.
    /// </summary>
    /// <param name="sender">The view model.</param>
    /// <param name="e">The close request.</param>
    private void OnCloseRequested(object? sender, ExportPresetCloseRequestedEventArgs e)
    {
        Close(e.Accepted);
    }
}
