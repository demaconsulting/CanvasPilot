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
using Avalonia.Data.Converters;
using Avalonia.Interactivity;
using Avalonia.Media.Imaging;
using DemaConsulting.CanvasPilot.PreviewSubsystem;

namespace DemaConsulting.CanvasPilot.StyleSubsystem;

/// <summary>
///     The window in which style kits are managed, bound to a <see cref="StyleKitManagerViewModel" />: the kits in groups by
///     place on one side, the preview of the chosen kit on the other, and the actions along the bottom.
/// </summary>
/// <remarks>
///     The window only shows what its view model holds. It is not modal: it is shown with the main window as its owner and
///     the user can keep working in the application while it is open. It disposes its view model when it is closed.
/// </remarks>
public sealed partial class StyleKitManagerView : Window
{
    /// <summary>
    ///     Converts a rendered picture to the bitmap that an image control shows; no picture gives no bitmap.
    /// </summary>
    public static readonly IValueConverter ImageToBitmap = new FuncValueConverter<RenderedImage?, Bitmap?>(
        image => image is null ? null : RenderedImageBitmap.Create(image));

    /// <summary>
    ///     The view model bound to the window, or <see langword="null" /> when the window was made for the XAML loader.
    /// </summary>
    private readonly StyleKitManagerViewModel? _viewModel;

    /// <summary>
    ///     Initializes a new instance of the <see cref="StyleKitManagerView" /> class for the XAML loader. The window has no
    ///     content until a view model is supplied.
    /// </summary>
    public StyleKitManagerView()
    {
        InitializeComponent();
    }

    /// <summary>
    ///     Initializes a new instance of the <see cref="StyleKitManagerView" /> class.
    /// </summary>
    /// <param name="viewModel">The view model that backs the window.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="viewModel" /> is <see langword="null" />.</exception>
    public StyleKitManagerView(StyleKitManagerViewModel viewModel)
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
            _viewModel.Dispose();
        }

        base.OnClosed(e);
    }

    /// <summary>
    ///     Chooses the kit whose card was clicked.
    /// </summary>
    /// <param name="sender">The button of the card.</param>
    /// <param name="e">The event data.</param>
    private void OnKitCardClick(object? sender, RoutedEventArgs e)
    {
        if (_viewModel is not null && sender is Control { DataContext: StyleKitItemViewModel item })
        {
            _viewModel.SelectItemCommand.Execute(item);
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
