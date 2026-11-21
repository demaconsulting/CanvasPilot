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

namespace DemaConsulting.CanvasPilot.KnowledgeSubsystem;

/// <summary>
///     The window of the browser of the drawing knowledge: the search box, the tree of cards or the hits of the search on
///     one side, and the card that is shown on the other, with its text, the cards below it, its related cards, and its
///     examples, each of which can be copied.
/// </summary>
/// <remarks>
///     The window only shows what its <see cref="KnowledgeBrowserViewModel" /> holds. It is not modal: it is shown with
///     the main window as its owner and the user can keep working in the application while it is open. It gives the view
///     model the clipboard of the window, unless the view model already has one.
/// </remarks>
public sealed partial class KnowledgeBrowserView : Window
{
    /// <summary>
    ///     The view model bound to the window, or <see langword="null" /> when the window was made for the XAML loader.
    /// </summary>
    private readonly KnowledgeBrowserViewModel? _viewModel;

    /// <summary>
    ///     Initializes a new instance of the <see cref="KnowledgeBrowserView" /> class for the XAML loader. The window has
    ///     no content until a view model is supplied.
    /// </summary>
    public KnowledgeBrowserView()
    {
        InitializeComponent();
    }

    /// <summary>
    ///     Initializes a new instance of the <see cref="KnowledgeBrowserView" /> class.
    /// </summary>
    /// <param name="viewModel">The view model that backs the window.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="viewModel" /> is <see langword="null" />.</exception>
    public KnowledgeBrowserView(KnowledgeBrowserViewModel viewModel)
        : this()
    {
        ArgumentNullException.ThrowIfNull(viewModel);

        _viewModel = viewModel;
        DataContext = viewModel;
        viewModel.Clipboard ??= new WindowKnowledgeClipboard(this);
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
    ///     Closes the window.
    /// </summary>
    /// <param name="sender">The view model.</param>
    /// <param name="e">The close request.</param>
    private void OnCloseRequested(object? sender, EventArgs e)
    {
        Close();
    }
}
