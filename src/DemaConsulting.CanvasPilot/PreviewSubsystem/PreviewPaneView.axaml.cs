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

using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Media;
using Avalonia.Media.Imaging;

namespace DemaConsulting.CanvasPilot.PreviewSubsystem;

/// <summary>
///     View of the preview pane, bound to a <see cref="PreviewPaneViewModel" /> by the view locator: the picture
///     (fitted to the pane or scrollable at its own size), the overlay slot placed exactly over the picture in the
///     picture's own pixel coordinates, a busy indicator, the render error with its hint, a status line, and the
///     list of notes. It converts the view model's pixels to an Avalonia bitmap, because the view model itself has
///     no dependency on Avalonia's imaging.
/// </summary>
public sealed partial class PreviewPaneView : UserControl
{
    /// <summary>
    ///     The view model the view is currently showing, or null.
    /// </summary>
    private PreviewPaneViewModel? _viewModel;

    /// <summary>
    ///     The bitmap currently shown, which the view owns.
    /// </summary>
    private WriteableBitmap? _bitmap;

    /// <summary>
    ///     Initializes a new instance of the <see cref="PreviewPaneView" /> class.
    /// </summary>
    public PreviewPaneView()
    {
        InitializeComponent();
    }

    /// <inheritdoc />
    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);

        if (_viewModel is not null)
        {
            _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
        }

        _viewModel = DataContext as PreviewPaneViewModel;
        if (_viewModel is not null)
        {
            _viewModel.PropertyChanged += OnViewModelPropertyChanged;
        }

        ShowImage();
        ApplyFit();
    }

    /// <inheritdoc />
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);

        // Re-attach to the view model that was released when the view was detached.
        if (_viewModel is null && DataContext is PreviewPaneViewModel viewModel)
        {
            _viewModel = viewModel;
            _viewModel.PropertyChanged += OnViewModelPropertyChanged;
            ShowImage();
            ApplyFit();
        }
    }

    /// <inheritdoc />
    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        if (_viewModel is not null)
        {
            _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
            _viewModel = null;
        }

        PreviewImage.Source = null;
        PreviewSurface.Width = double.NaN;
        PreviewSurface.Height = double.NaN;
        _bitmap?.Dispose();
        _bitmap = null;
        base.OnDetachedFromVisualTree(e);
    }

    /// <summary>
    ///     Updates the picture or the fit mode when the view model changes.
    /// </summary>
    /// <param name="sender">The view model.</param>
    /// <param name="e">The change details.</param>
    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(PreviewPaneViewModel.Image):
                ShowImage();
                break;

            case nameof(PreviewPaneViewModel.FitToWindow):
                ApplyFit();
                break;
        }
    }

    /// <summary>
    ///     Shows the view model's picture, replacing and disposing the earlier bitmap.
    /// </summary>
    private void ShowImage()
    {
        var previous = _bitmap;
        _bitmap = _viewModel?.Image is { } image ? RenderedImageBitmap.Create(image) : null;
        PreviewImage.Source = _bitmap;

        // The surface has the size of the picture in pixels, so the picture and the overlay on it share one
        // coordinate system, which the Viewbox scales.
        PreviewSurface.Width = _viewModel?.Image is { } shown ? shown.Width : double.NaN;
        PreviewSurface.Height = _viewModel?.Image is { } shownImage ? shownImage.Height : double.NaN;
        previous?.Dispose();
        ApplyFit();
    }

    /// <summary>
    ///     Applies the fit mode: scaled down to the pane without scroll bars, or at its own size with scroll
    ///     bars when it is larger than the pane. The picture itself is never stretched; the Viewbox around the
    ///     picture and its overlay scales both together.
    /// </summary>
    private void ApplyFit()
    {
        var fit = _viewModel?.FitToWindow ?? true;
        if (fit)
        {
            PreviewViewbox.Stretch = Stretch.Uniform;
            PreviewViewbox.StretchDirection = StretchDirection.DownOnly;
            ImageScroller.HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled;
            ImageScroller.VerticalScrollBarVisibility = ScrollBarVisibility.Disabled;
        }
        else
        {
            PreviewViewbox.Stretch = Stretch.None;
            PreviewViewbox.StretchDirection = StretchDirection.Both;
            ImageScroller.HorizontalScrollBarVisibility = ScrollBarVisibility.Auto;
            ImageScroller.VerticalScrollBarVisibility = ScrollBarVisibility.Auto;
        }
    }
}
