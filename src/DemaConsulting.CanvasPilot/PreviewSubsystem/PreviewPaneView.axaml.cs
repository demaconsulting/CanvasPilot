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
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.VisualTree;

namespace DemaConsulting.CanvasPilot.PreviewSubsystem;

/// <summary>
///     View of the preview pane, bound to a <see cref="PreviewPaneViewModel" /> by the view locator: the picture
///     (fitted to the pane or scrollable at its own size), the overlay slot placed exactly over the picture in the
///     picture's own pixel coordinates, a busy indicator, the render error with its hint, a status line, and the
///     list of notes. It converts the view model's pixels to an Avalonia bitmap, because the view model itself has
///     no dependency on Avalonia's imaging. The stage carries the menu of the picture, whose items run the copy and
///     export commands the host puts in the view model; the view keeps it closed while the host turns it off and over
///     the floating controls, and opens it for Shift+F10 from any control in the stage where the framework does not
///     (it counts Shift+F10 as a menu key only on some platforms and only while Shift is held).
/// </summary>
public sealed partial class PreviewPaneView : UserControl
{
    /// <summary>
    ///     How far, in device-independent pixels along either axis, the pointer must move with the left button down
    ///     before a drag out of the application starts, so that a click does not start one.
    /// </summary>
    private const double DragThreshold = 4;
    /// <summary>
    ///     The view model the view is currently showing, or null.
    /// </summary>
    private PreviewPaneViewModel? _viewModel;

    /// <summary>
    ///     The bitmap currently shown, which the view owns.
    /// </summary>
    private WriteableBitmap? _bitmap;

    /// <summary>
    ///     The press of the left button on the picture that may become a drag, or null.
    /// </summary>
    private PointerPressedEventArgs? _dragPress;

    /// <summary>
    ///     Where the pointer was pressed, in the view's coordinates.
    /// </summary>
    private Point _dragOrigin;

    /// <summary>
    ///     Where the pointer was pressed with the middle button to move the zoomed picture, or null.
    /// </summary>
    private Point? _panOrigin;

    /// <summary>
    ///     How far the zoomed picture was scrolled when the move started.
    /// </summary>
    private Vector _panStartOffset;

    /// <summary>
    ///     The control inside the stage's scroller that had the keyboard focus when Shift+F10 was pressed, while that F10
    ///     is not yet released, or null; the menu is requested from it on the release, whichever key is released first.
    /// </summary>
    private Interactive? _contextKeySource;

    /// <summary>
    ///     Initializes a new instance of the <see cref="PreviewPaneView" /> class.
    /// </summary>
    public PreviewPaneView()
    {
        InitializeComponent();
        PreviewSurface.PointerPressed += OnSurfacePointerPressed;
        PreviewSurface.PointerMoved += OnSurfacePointerMoved;
        PreviewSurface.PointerReleased += OnSurfacePointerEnded;
        PreviewSurface.PointerCaptureLost += OnSurfacePointerEnded;
        ImageScroller.AddHandler(PointerWheelChangedEvent, OnScrollerWheel, RoutingStrategies.Tunnel);
        ImageScroller.AddHandler(PointerPressedEvent, OnScrollerPointerPressed, RoutingStrategies.Tunnel);
        ImageScroller.AddHandler(PointerMovedEvent, OnScrollerPointerMoved, RoutingStrategies.Tunnel);
        ImageScroller.AddHandler(PointerReleasedEvent, OnScrollerPointerReleased, RoutingStrategies.Tunnel);

        // The menu of the picture stays closed while the host has turned it off, and a request from a control that
        // floats over the stage is not a request for the picture's menu. The framework counts Shift+F10 as a menu key
        // only on some platforms, such as Windows, and only while Shift is held at the release, so the scroller handles
        // it for whatever control inside it has the focus, as the framework does for the Menu key, and forgets a press
        // when that focus moves.
        PictureMenu.Opening += OnPictureMenuOpening;
        PreviewToolbar.AddHandler(ContextRequestedEvent, OnFloatingContextRequested);
        ZoomPill.AddHandler(ContextRequestedEvent, OnFloatingContextRequested);
        DiagnosticsDrawer.AddHandler(ContextRequestedEvent, OnFloatingContextRequested);
        ImageScroller.AddHandler(KeyDownEvent, OnScrollerKeyDown);
        ImageScroller.AddHandler(KeyUpEvent, OnScrollerKeyUp);
        ImageScroller.LostFocus += OnScrollerLostFocus;

        // The picture is scaled to the pane while it is fitted, so the zoom in effect follows the size of the Viewbox.
        PreviewViewbox.SizeChanged += (_, _) => ReportFitPercent();
    }

    /// <summary>
    ///     Gets or sets the operation that starts the drag of the prepared data out of the application. It is the
    ///     operating system's drag and drop by default, which cannot complete without a real window, so tests
    ///     replace it to see what the view offers.
    /// </summary>
    internal Func<PointerPressedEventArgs, IDataTransfer, DragDropEffects, Task<DragDropEffects>> DragStarter { get; set; } =
        (press, data, effects) => DragDrop.DoDragDropAsync(press, data, effects);

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
            case nameof(PreviewPaneViewModel.ZoomPercent):
                ApplyFit();
                break;
        }
    }

    /// <summary>
    ///     Remembers a press of the left button on the picture, which becomes a drag out of the application when the
    ///     pointer moves far enough while the button stays down.
    /// </summary>
    /// <param name="sender">The surface that holds the picture.</param>
    /// <param name="e">The press.</param>
    private void OnSurfacePointerPressed(object? sender, PointerPressedEventArgs e)
    {
        var canDrag = _viewModel is { IsDragOutEnabled: true, HasImage: true, DragSource: not null }
                      && e.GetCurrentPoint(PreviewSurface).Properties.IsLeftButtonPressed;
        _dragPress = canDrag ? e : null;
        _dragOrigin = e.GetPosition(this);
    }

    /// <summary>
    ///     Starts the drag when the pointer has moved by the threshold since the press.
    /// </summary>
    /// <param name="sender">The surface that holds the picture.</param>
    /// <param name="e">The move.</param>
    private void OnSurfacePointerMoved(object? sender, PointerEventArgs e)
    {
        if (_dragPress is not { } press)
        {
            return;
        }

        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            _dragPress = null;
            return;
        }

        var moved = e.GetPosition(this) - _dragOrigin;
        if (Math.Abs(moved.X) < DragThreshold && Math.Abs(moved.Y) < DragThreshold)
        {
            return;
        }

        _dragPress = null;
        _ = StartDragAsync(press);
    }

    /// <summary>
    ///     Forgets the press when the button is released or the pointer is taken away, so a click starts no drag.
    /// </summary>
    /// <param name="sender">The surface that holds the picture.</param>
    /// <param name="e">The event data.</param>
    private void OnSurfacePointerEnded(object? sender, RoutedEventArgs e)
    {
        _dragPress = null;
    }

    /// <summary>
    ///     Prepares the shown picture as a PNG file and as a bitmap and offers both to the program the user drops
    ///     them on. Problems are logged, never thrown, because nothing awaits this method.
    /// </summary>
    /// <param name="press">The press that started the drag.</param>
    /// <returns>A task that completes when the drag has ended.</returns>
    private async Task StartDragAsync(PointerPressedEventArgs press)
    {
        var viewModel = _viewModel;
        if (viewModel is null)
        {
            return;
        }

        Bitmap? bitmap = null;
        try
        {
            var path = viewModel.PrepareDragFile();
            if (path is null)
            {
                return;
            }

            var item = new DataTransferItem();
            var storage = TopLevel.GetTopLevel(this)?.StorageProvider;
            if (storage is not null && await storage.TryGetFileFromPathAsync(new Uri(path)) is { } file)
            {
                item.SetFile(file);
            }

            bitmap = new Bitmap(path);
            item.SetBitmap(bitmap);
            var data = new DataTransfer();
            data.Add(item);
            await DragStarter(press, data, DragDropEffects.Copy);
        }
        catch (Exception ex)
        {
            // The drag is started from a fire-and-forget call, so no failure may escape; whatever the operating
            // system or the drop target does wrong, the user can still use the application.
            viewModel.ReportDragProblem(ex);
        }
        finally
        {
            bitmap?.Dispose();
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
    ///     Applies the zoom: scaled down to the pane without scroll bars while fitted, otherwise at the chosen
    ///     percentage, with scroll bars when the picture is larger than the pane. The picture itself is never
    ///     stretched; the Viewbox around the picture and its overlay scales both together.
    /// </summary>
    private void ApplyFit()
    {
        var fit = _viewModel?.FitToWindow ?? true;
        if (fit)
        {
            PreviewViewbox.Width = double.NaN;
            PreviewViewbox.Height = double.NaN;
            PreviewViewbox.Stretch = Stretch.Uniform;
            PreviewViewbox.StretchDirection = StretchDirection.DownOnly;
            ImageScroller.HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled;
            ImageScroller.VerticalScrollBarVisibility = ScrollBarVisibility.Disabled;
            ReportFitPercent();
        }
        else
        {
            var percent = _viewModel?.ZoomPercent ?? PreviewPaneViewModel.ActualSizePercent;
            if (percent == PreviewPaneViewModel.ActualSizePercent || _viewModel?.Image is not { } image)
            {
                PreviewViewbox.Width = double.NaN;
                PreviewViewbox.Height = double.NaN;
                PreviewViewbox.Stretch = Stretch.None;
                PreviewViewbox.StretchDirection = StretchDirection.Both;
            }
            else
            {
                // The Viewbox is as large as the zoomed picture, and scales the surface to that size.
                PreviewViewbox.Width = image.Width * percent / 100.0;
                PreviewViewbox.Height = image.Height * percent / 100.0;
                PreviewViewbox.Stretch = Stretch.Uniform;
                PreviewViewbox.StretchDirection = StretchDirection.Both;
            }

            ImageScroller.HorizontalScrollBarVisibility = ScrollBarVisibility.Auto;
            ImageScroller.VerticalScrollBarVisibility = ScrollBarVisibility.Auto;
        }
    }

    /// <summary>
    ///     Tells the view model at which zoom the fitted picture is shown, which only the laid out view knows.
    /// </summary>
    private void ReportFitPercent()
    {
        if (_viewModel is not { FitToWindow: true, Image: { Width: > 0 } image } viewModel)
        {
            return;
        }

        var width = PreviewViewbox.Bounds.Width;
        if (width <= 0)
        {
            return;
        }

        viewModel.FitPercent = Math.Clamp((int)Math.Round(width * 100.0 / image.Width), 1, PreviewPaneViewModel.ActualSizePercent);
    }

    /// <summary>
    ///     Zooms with the wheel while Control is held, and lets the wheel scroll otherwise.
    /// </summary>
    /// <param name="sender">The scroller.</param>
    /// <param name="e">The wheel turn.</param>
    private void OnScrollerWheel(object? sender, PointerWheelEventArgs e)
    {
        if (_viewModel is null || !e.KeyModifiers.HasFlag(KeyModifiers.Control) || e.Delta.Y == 0)
        {
            return;
        }

        if (e.Delta.Y > 0)
        {
            _viewModel.ZoomIn();
        }
        else
        {
            _viewModel.ZoomOut();
        }

        e.Handled = true;
    }

    /// <summary>
    ///     Starts moving the zoomed picture with the middle button, and focuses the stage so that the zoom keys work.
    /// </summary>
    /// <param name="sender">The scroller.</param>
    /// <param name="e">The press.</param>
    private void OnScrollerPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        // Focus the stage when the empty stage is pressed, so that the zoom keys work; a press on the picture belongs
        // to the picture's own tools and must not move the scroll position.
        if (e.Source is Visual source && !source.GetSelfAndVisualAncestors().Contains(PreviewSurface))
        {
            ImageScroller.Focus();
        }
        if (e.GetCurrentPoint(ImageScroller).Properties.IsMiddleButtonPressed)
        {
            _panOrigin = e.GetPosition(ImageScroller);
            _panStartOffset = ImageScroller.Offset;
            e.Pointer.Capture(ImageScroller);
            e.Handled = true;
        }
    }

    /// <summary>
    ///     Moves the zoomed picture while the middle button is held.
    /// </summary>
    /// <param name="sender">The scroller.</param>
    /// <param name="e">The move.</param>
    private void OnScrollerPointerMoved(object? sender, PointerEventArgs e)
    {
        if (_panOrigin is not { } origin)
        {
            return;
        }

        var moved = e.GetPosition(ImageScroller) - origin;
        ImageScroller.Offset = new Vector(Math.Max(0, _panStartOffset.X - moved.X), Math.Max(0, _panStartOffset.Y - moved.Y));
        e.Handled = true;
    }

    /// <summary>
    ///     Ends moving the picture.
    /// </summary>
    /// <param name="sender">The scroller.</param>
    /// <param name="e">The event data.</param>
    private void OnScrollerPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (_panOrigin is null)
        {
            return;
        }

        _panOrigin = null;
        e.Pointer.Capture(null);
        e.Handled = true;
    }

    /// <summary>
    ///     Keeps the menu of the picture closed while the host has turned it off, such as while a marking tool is
    ///     chosen, because a menu that opened during a stroke would take the pointer from the stroke and the mark
    ///     would be lost.
    /// </summary>
    /// <remarks>
    ///     Canceling the opening, instead of marking every request on the stage handled, covers the right-click and
    ///     the keys alike and leaves the context menus of the controls on the stage, such as the text box of a note,
    ///     working while a tool is chosen.
    /// </remarks>
    /// <param name="sender">The menu of the picture.</param>
    /// <param name="e">The opening, which is canceled while the menu is off or no view model is shown.</param>
    private void OnPictureMenuOpening(object? sender, CancelEventArgs e)
    {
        if (_viewModel is not { IsPictureMenuEnabled: true })
        {
            e.Cancel = true;
        }
    }

    /// <summary>
    ///     Stops a request for a context menu on a control that floats over the stage (the host's tool bar, the zoom
    ///     pill, and the notes drawer) from opening the menu of the picture, because those controls are not the
    ///     picture.
    /// </summary>
    /// <remarks>
    ///     The handler runs as the request bubbles, after the control's children, so a child with a menu of its own,
    ///     such as the text box of a note, still opens it.
    /// </remarks>
    /// <param name="sender">The floating control.</param>
    /// <param name="e">The request, which is marked handled.</param>
    private static void OnFloatingContextRequested(object? sender, ContextRequestedEventArgs e)
    {
        e.Handled = true;
    }

    /// <summary>
    ///     Remembers a press of Shift+F10, the key that opens a context menu on Windows, while the stage or a control
    ///     inside it has the focus, together with the control that has the focus; the framework treats Shift+F10 as a
    ///     menu key only on some platforms.
    /// </summary>
    /// <param name="sender">The scroller, where the handler sits.</param>
    /// <param name="e">
    ///     The key press, whose source is the focused control; it is marked handled when it is Shift+F10.
    /// </param>
    private void OnScrollerKeyDown(object? sender, KeyEventArgs e)
    {
        // Any other key pressed in the scroller forgets an earlier Shift+F10 whose release was never seen here
        _contextKeySource = e.Key == Key.F10 && e.KeyModifiers == KeyModifiers.Shift ? e.Source as Interactive : null;
        if (_contextKeySource is not null)
        {
            e.Handled = true;
        }
    }

    /// <summary>
    ///     Requests a context menu from the control that has the focus when the F10 of a Shift+F10 press is released,
    ///     so the request bubbles as the Menu key's does: a control with a menu of its own opens it, and otherwise the
    ///     stage opens the menu of the picture through the same check whether it may open. It does nothing where the
    ///     platform already treats the release as a menu key, because the framework has requested the menu then.
    /// </summary>
    /// <param name="sender">The scroller, where the handler sits.</param>
    /// <param name="e">The key release, which is marked handled when it raises the request.</param>
    private void OnScrollerKeyUp(object? sender, KeyEventArgs e)
    {
        if (e.Key != Key.F10)
        {
            return;
        }

        // The press is used once, and only by a release on the control it was made on, as the framework's Menu key is
        var source = _contextKeySource;
        _contextKeySource = null;
        if (source is null || !ReferenceEquals(e.Source, source))
        {
            return;
        }

        // Where the platform counts this release as a menu key (Windows, with Shift still held) the framework has
        // already requested the menu from the focused control, so a second request is not raised
        var gestures = this.GetPlatformSettings()?.HotkeyConfiguration.OpenContextMenu;
        if (gestures is not null && gestures.Any(gesture => gesture.Matches(e)))
        {
            return;
        }

        e.Handled = true;
        source.RaiseEvent(new ContextRequestedEventArgs());
    }

    /// <summary>
    ///     Forgets a Shift+F10 press when the focus leaves the scroller or a control inside it, so a later release on
    ///     another control opens nothing.
    /// </summary>
    /// <param name="sender">The scroller, where the handler sits.</param>
    /// <param name="e">The loss of focus, which bubbles from the control that lost it.</param>
    private void OnScrollerLostFocus(object? sender, RoutedEventArgs e)
    {
        _contextKeySource = null;
    }
}
