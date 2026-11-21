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

namespace DemaConsulting.CanvasPilot.ChatSubsystem;

/// <summary>
///     The window of the payload inspector: the description of what the assistant would receive for a sent message on
///     one side, and the pictures of the payload on the other.
/// </summary>
/// <remarks>
///     The window only shows what its <see cref="PayloadInspectorViewModel" /> holds. The text is selectable and cannot
///     be edited, because the inspector is for reading what was sent, not for changing it. The view model is disposed
///     with the window, which releases the decoded pictures.
/// </remarks>
public sealed partial class PayloadInspectorView : Window
{
    /// <summary>
    ///     The view model bound to the window, or <see langword="null" /> when the window was made for the XAML loader.
    /// </summary>
    private readonly PayloadInspectorViewModel? _viewModel;

    /// <summary>
    ///     Initializes a new instance of the <see cref="PayloadInspectorView" /> class for the XAML loader. The window
    ///     has no content until a view model is supplied.
    /// </summary>
    public PayloadInspectorView()
    {
        InitializeComponent();
    }

    /// <summary>
    ///     Initializes a new instance of the <see cref="PayloadInspectorView" /> class.
    /// </summary>
    /// <param name="viewModel">The view model that backs the window.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="viewModel" /> is <see langword="null" />.</exception>
    public PayloadInspectorView(PayloadInspectorViewModel viewModel)
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
        // Stop listening and release the decoded pictures, which only this window shows
        if (_viewModel is not null)
        {
            _viewModel.CloseRequested -= OnCloseRequested;
            _viewModel.Dispose();
        }

        base.OnClosed(e);
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
