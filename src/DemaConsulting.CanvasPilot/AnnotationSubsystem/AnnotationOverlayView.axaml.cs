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
using System.Globalization;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using DemaConsulting.CanvasPilot.PreviewSubsystem;

using Path = Avalonia.Controls.Shapes.Path;
namespace DemaConsulting.CanvasPilot.AnnotationSubsystem;

/// <summary>
///     View of the layer over the shown picture, bound to an <see cref="AnnotationOverlayViewModel" /> by the view
///     locator. It is laid out in the pixels of the picture, so the pointer's position on it is already a point of the
///     picture at any zoom. It reports the pointer and the keys to the view model and draws what the view model holds:
///     the shown marks with their numbers, the shape that is being drawn, and the cursor of the keyboard.
/// </summary>
public sealed partial class AnnotationOverlayView : UserControl
{
    /// <summary>
    ///     How far, in picture pixels, each arm of the cursor of the keyboard reaches from its center.
    /// </summary>
    private const double CursorArm = 12;

    /// <summary>
    ///     The extra width of the white line that is drawn under the colored line of a mark.
    /// </summary>
    private const double HaloExtra = 3;

    /// <summary>
    ///     The white of the lines under the marks and of the edges of the badges.
    /// </summary>
    private static readonly IBrush White = Brushes.White;

    /// <summary>
    ///     The view model the view follows, or null.
    /// </summary>
    private AnnotationOverlayViewModel? _viewModel;

    /// <summary>
    ///     Whether the shape that is being drawn was started with the pointer, which holds the pointer until it ends.
    /// </summary>
    private bool _pointerStroke;

    /// <summary>
    ///     The window whose keys are watched for Escape while a shape is under construction, or null.
    /// </summary>
    private TopLevel? _topLevel;

    /// <summary>
    ///     Initializes a new instance of the <see cref="AnnotationOverlayView" /> class.
    /// </summary>
    public AnnotationOverlayView()
    {
        InitializeComponent();
        Surface.PointerPressed += OnPointerPressed;
        Surface.PointerMoved += OnPointerMoved;
        Surface.PointerReleased += OnPointerReleased;
        Surface.PointerCaptureLost += OnPointerCaptureLost;
        LostFocus += OnViewLostFocus;
    }

    /// <inheritdoc />
    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);
        Follow(DataContext as AnnotationOverlayViewModel);
    }

    /// <inheritdoc />
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        Follow(DataContext as AnnotationOverlayViewModel);

        // Escape must stop a shape wherever the keyboard focus is, for example in the message while the pointer draws
        _topLevel = TopLevel.GetTopLevel(this);
        _topLevel?.AddHandler(KeyDownEvent, OnTopLevelKeyDown, RoutingStrategies.Tunnel);
    }

    /// <inheritdoc />
    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        _topLevel?.RemoveHandler(KeyDownEvent, OnTopLevelKeyDown);
        _topLevel = null;
        Follow(null);
        base.OnDetachedFromVisualTree(e);
    }

    /// <summary>
    ///     Gives up a shape that is made with the keyboard when the keyboard goes elsewhere, and hides the cursor.
    /// </summary>
    /// <param name="sender">The view.</param>
    /// <param name="e">The event data.</param>
    private void OnViewLostFocus(object? sender, RoutedEventArgs e)
    {
        // A shape that is made with the keyboard cannot go on when the keyboard goes elsewhere
        if (_viewModel is { IsDrawing: true } && !_pointerStroke)
        {
            _viewModel.Cancel();
        }

        _viewModel?.HideCursor();
    }

    /// <summary>
    ///     Follows a view model, and stops following the one that was followed before.
    /// </summary>
    /// <param name="viewModel">The view model to follow, or null for none.</param>
    private void Follow(AnnotationOverlayViewModel? viewModel)
    {
        if (ReferenceEquals(_viewModel, viewModel))
        {
            return;
        }

        if (_viewModel is not null)
        {
            _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
        }

        _viewModel = viewModel;
        if (_viewModel is not null)
        {
            _viewModel.PropertyChanged += OnViewModelPropertyChanged;
        }

        ApplyTool();
        Redraw();
    }

    /// <summary>
    ///     Draws again when something that is drawn changes.
    /// </summary>
    /// <param name="sender">The view model.</param>
    /// <param name="e">The change details.</param>
    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(AnnotationOverlayViewModel.CurrentTool):
                ApplyTool();
                Redraw();
                break;

            case nameof(AnnotationOverlayViewModel.VisibleMarks):
            case nameof(AnnotationOverlayViewModel.DraftPoints):
            case nameof(AnnotationOverlayViewModel.HasDraft):
            case nameof(AnnotationOverlayViewModel.PendingNumber):
            case nameof(AnnotationOverlayViewModel.KeyboardCursor):
            case nameof(AnnotationOverlayViewModel.PictureWidth):
                Redraw();
                break;
        }
    }

    /// <summary>
    ///     Makes the layer take the keyboard and show a cross-hair pointer only while a tool is chosen.
    /// </summary>
    private void ApplyTool()
    {
        var active = _viewModel is { IsToolActive: true };
        Focusable = active;
        IsTabStop = active;
        Cursor = active ? new Cursor(StandardCursorType.Cross) : null;
    }

    /// <summary>
    ///     Starts a shape on a press of the left button while a tool is chosen, and takes the pointer, so that the shape
    ///     goes on when the pointer leaves the picture.
    /// </summary>
    /// <param name="sender">The surface.</param>
    /// <param name="e">The press.</param>
    private void OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (_viewModel is not { IsToolActive: true } viewModel || !e.GetCurrentPoint(Surface).Properties.IsLeftButtonPressed)
        {
            return;
        }

        if (viewModel.BeginStroke(ToPoint(e)))
        {
            _pointerStroke = true;
            e.Pointer.Capture(Surface);
            Focus();
            e.Handled = true;
        }
    }

    /// <summary>
    ///     Reshapes the shape that the pointer is drawing.
    /// </summary>
    /// <param name="sender">The surface.</param>
    /// <param name="e">The move.</param>
    private void OnPointerMoved(object? sender, PointerEventArgs e)
    {
        if (_pointerStroke && _viewModel is not null)
        {
            _viewModel.ContinueStroke(ToPoint(e));
            e.Handled = true;
        }
    }

    /// <summary>
    ///     Finishes the shape that the pointer is drawing.
    /// </summary>
    /// <param name="sender">The surface.</param>
    /// <param name="e">The release.</param>
    private void OnPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (!_pointerStroke || _viewModel is null)
        {
            return;
        }

        _pointerStroke = false;
        _viewModel.EndStroke(ToPoint(e));
        e.Pointer.Capture(null);
        e.Handled = true;
    }

    /// <summary>
    ///     Throws the shape away when the pointer is taken from the layer without a release, for example when the window
    ///     is deactivated.
    /// </summary>
    /// <param name="sender">The surface.</param>
    /// <param name="e">The event data.</param>
    private void OnPointerCaptureLost(object? sender, PointerCaptureLostEventArgs e)
    {
        if (_pointerStroke)
        {
            _pointerStroke = false;
            _viewModel?.Cancel();
        }
    }

    /// <inheritdoc />
    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (_viewModel is null || e.Handled)
        {
            return;
        }

        var step = AnnotationOverlayViewModel.CursorStep;
        if (e.KeyModifiers.HasFlag(KeyModifiers.Control))
        {
            step = AnnotationOverlayViewModel.LargeCursorStep;
        }
        else if (e.KeyModifiers.HasFlag(KeyModifiers.Shift))
        {
            step = AnnotationOverlayViewModel.SmallCursorStep;
        }

        e.Handled = e.Key switch
        {
            Key.Escape => _viewModel.Escape(),
            Key.Left => _viewModel.MoveCursor(-step, 0),
            Key.Right => _viewModel.MoveCursor(step, 0),
            Key.Up => _viewModel.MoveCursor(0, -step),
            Key.Down => _viewModel.MoveCursor(0, step),
            Key.Enter or Key.Space => _viewModel.ConfirmCursor(),
            _ => false,
        };
    }

    /// <summary>
    ///     Throws the shape away on Escape wherever the focus is, while there is a shape to throw away.
    /// </summary>
    /// <param name="sender">The window.</param>
    /// <param name="e">The key.</param>
    private void OnTopLevelKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape && _viewModel is { HasDraft: true } viewModel)
        {
            viewModel.Cancel();
            e.Handled = true;
        }
    }

    /// <summary>
    ///     Gets the point of the picture that an event of the pointer is at.
    /// </summary>
    /// <param name="e">The event.</param>
    /// <returns>The point, in the pixels of the picture.</returns>
    private RenderPoint ToPoint(PointerEventArgs e)
    {
        var position = e.GetPosition(Surface);
        return new RenderPoint(position.X, position.Y);
    }

    /// <summary>
    ///     Builds the shapes and badges of the shown marks, of the shape under construction, and the cursor again.
    /// </summary>
    private void Redraw()
    {
        MarkLayer.Children.Clear();
        if (_viewModel is null)
        {
            return;
        }

        // The lines of every shape first, so that no line is drawn over a number
        var badges = new List<Control>();
        foreach (var mark in _viewModel.VisibleMarks)
        {
            var width = mark.Snapshot.Width;
            var height = mark.Snapshot.Height;
            var path = AddShape(mark.Kind, mark.Geometry.Points, mark.Number, width, height, dashed: false);
            path.Tag = mark;
            AutomationProperties.SetAutomationId(path, "AnnotationMarkShape");
            AutomationProperties.SetName(path, Describe(mark));
            badges.Add(MakeBadge(mark.Number, mark.Geometry.BoundingBox, width, height));
        }

        if (_viewModel.HasDraft && _viewModel.DraftPoints.Count > 0 && _viewModel.PictureWidth > 0)
        {
            var path = AddShape(_viewModel.DraftKind, _viewModel.DraftPoints, _viewModel.PendingNumber, _viewModel.PictureWidth, _viewModel.PictureHeight, dashed: true);
            AutomationProperties.SetAutomationId(path, "AnnotationDraftShape");
            badges.Add(MakeBadge(_viewModel.PendingNumber, BoxOf(_viewModel.DraftPoints), _viewModel.PictureWidth, _viewModel.PictureHeight));
        }

        foreach (var badge in badges)
        {
            MarkLayer.Children.Add(badge);
        }

        if (_viewModel is { IsToolActive: true, KeyboardCursor: { } cursor })
        {
            AddCursor(cursor);
        }
    }

    /// <summary>
    ///     Adds the white and the colored line of one shape to the layer.
    /// </summary>
    /// <param name="kind">The shape.</param>
    /// <param name="points">The points of the shape in picture pixels.</param>
    /// <param name="number">The number of the mark, which decides the color.</param>
    /// <param name="width">The width of the picture.</param>
    /// <param name="height">The height of the picture.</param>
    /// <param name="dashed">Whether the line is dashed, as the line of a shape that is not finished is.</param>
    /// <returns>The colored line.</returns>
    private Path AddShape(MarkKind kind, IReadOnlyList<RenderPoint> points, int number, int width, int height, bool dashed)
    {
        var stroke = AnnotationOverlayStyle.StrokeWidth(width, height);
        var geometry = BuildGeometry(kind, points, stroke);
        var halo = MakeLine(geometry, White, stroke + HaloExtra, dashed: false);
        var line = MakeLine(geometry, new SolidColorBrush(Color.Parse(AnnotationOverlayStyle.ColorOf(number))), stroke, dashed);
        MarkLayer.Children.Add(halo);
        MarkLayer.Children.Add(line);
        return line;
    }

    /// <summary>
    ///     Makes the line of a shape.
    /// </summary>
    /// <param name="geometry">The geometry.</param>
    /// <param name="brush">The color of the line.</param>
    /// <param name="thickness">The width of the line.</param>
    /// <param name="dashed">Whether the line is dashed.</param>
    /// <returns>The line.</returns>
    private static Path MakeLine(Geometry geometry, IBrush brush, double thickness, bool dashed)
    {
        var path = new Path
        {
            Data = geometry,
            Stroke = brush,
            StrokeThickness = thickness,
            StrokeLineCap = PenLineCap.Round,
            StrokeJoin = PenLineJoin.Round,
            IsHitTestVisible = false,
        };
        if (dashed)
        {
            path.StrokeDashArray = new AvaloniaList<double> { 3, 2 };
        }

        return path;
    }

    /// <summary>
    ///     Makes the geometry of a shape.
    /// </summary>
    /// <param name="kind">The shape.</param>
    /// <param name="points">The points of the shape.</param>
    /// <param name="stroke">The width of the lines, from which the size of the head of an arrow is taken.</param>
    /// <returns>The geometry.</returns>
    private static Geometry BuildGeometry(MarkKind kind, IReadOnlyList<RenderPoint> points, double stroke)
    {
        var box = BoxOf(points);
        var rectangle = new Rect(box.X, box.Y, box.Width, box.Height);
        switch (kind)
        {
            case MarkKind.Rectangle:
                return new RectangleGeometry(rectangle);

            case MarkKind.Ellipse:
                return new EllipseGeometry(rectangle);

            case MarkKind.Arrow:
                return MakeArrow(points[0], points[^1], stroke);

            default:
                return MakePolyline(points);
        }
    }

    /// <summary>
    ///     Makes the geometry of a line through points.
    /// </summary>
    /// <param name="points">The points; a single point makes a dot.</param>
    /// <returns>The geometry.</returns>
    private static StreamGeometry MakePolyline(IReadOnlyList<RenderPoint> points)
    {
        var geometry = new StreamGeometry();
        using var context = geometry.Open();
        context.BeginFigure(new Point(points[0].X, points[0].Y), false);
        context.LineTo(new Point(points[0].X, points[0].Y));
        for (var i = 1; i < points.Count; i++)
        {
            context.LineTo(new Point(points[i].X, points[i].Y));
        }

        context.EndFigure(false);
        return geometry;
    }

    /// <summary>
    ///     Makes the geometry of an arrow: the shaft and the two lines of the head, as the annotated picture draws them.
    /// </summary>
    /// <param name="tail">The tail.</param>
    /// <param name="head">The head, the point the arrow points at.</param>
    /// <param name="stroke">The width of the lines.</param>
    /// <returns>The geometry.</returns>
    private static StreamGeometry MakeArrow(RenderPoint tail, RenderPoint head, double stroke)
    {
        var geometry = new StreamGeometry();
        using var context = geometry.Open();
        context.BeginFigure(new Point(tail.X, tail.Y), false);
        context.LineTo(new Point(head.X, head.Y));
        context.EndFigure(false);

        var length = MarkGeometry.Distance(tail, head);
        if (length > 0.001)
        {
            var backX = (tail.X - head.X) / length;
            var backY = (tail.Y - head.Y) / length;
            var wing = AnnotationOverlayStyle.ArrowWing(stroke, length);
            var cos = Math.Cos(AnnotationOverlayStyle.ArrowHeadAngle);
            var sin = Math.Sin(AnnotationOverlayStyle.ArrowHeadAngle);
            var first = new Point(head.X + (wing * ((backX * cos) - (backY * sin))), head.Y + (wing * ((backX * sin) + (backY * cos))));
            var second = new Point(head.X + (wing * ((backX * cos) + (backY * sin))), head.Y + (wing * ((-backX * sin) + (backY * cos))));
            context.BeginFigure(first, false);
            context.LineTo(new Point(head.X, head.Y));
            context.EndFigure(false);
            context.BeginFigure(second, false);
            context.LineTo(new Point(head.X, head.Y));
            context.EndFigure(false);
        }

        return geometry;
    }

    /// <summary>
    ///     Makes the badge that shows the number of a mark at its place.
    /// </summary>
    /// <param name="number">The number.</param>
    /// <param name="box">The bounding box of the mark.</param>
    /// <param name="width">The width of the picture.</param>
    /// <param name="height">The height of the picture.</param>
    /// <returns>The badge, placed on the layer.</returns>
    private static Border MakeBadge(int number, RenderBox box, int width, int height)
    {
        var radius = AnnotationOverlayStyle.BadgeRadius(width, height);
        var center = AnnotationOverlayStyle.BadgeCenter(box, width, height);
        var label = number.ToString(CultureInfo.InvariantCulture);
        var badge = new Border
        {
            Width = radius * 2,
            Height = radius * 2,
            CornerRadius = new CornerRadius(radius),
            Background = new SolidColorBrush(Color.Parse(AnnotationOverlayStyle.ColorOf(number))),
            BorderBrush = White,
            BorderThickness = new Thickness(2),
            IsHitTestVisible = false,
            Child = new TextBlock
            {
                Text = label,
                Foreground = White,
                FontWeight = FontWeight.Bold,
                FontSize = radius * (label.Length switch { 1 => 1.3, 2 => 1.05, _ => 0.8 }),
                HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center,
                VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
            },
        };
        AutomationProperties.SetAutomationId(badge, "AnnotationMarkBadge");
        AutomationProperties.SetName(badge, $"Mark {label}");
        Canvas.SetLeft(badge, center.X - radius);
        Canvas.SetTop(badge, center.Y - radius);
        return badge;
    }

    /// <summary>
    ///     Adds the cursor of the keyboard: a cross with a white line under it so that it shows on any picture.
    /// </summary>
    /// <param name="cursor">The place of the cursor in picture pixels.</param>
    private void AddCursor(RenderPoint cursor)
    {
        var geometry = new StreamGeometry();
        using (var context = geometry.Open())
        {
            context.BeginFigure(new Point(cursor.X - CursorArm, cursor.Y), false);
            context.LineTo(new Point(cursor.X + CursorArm, cursor.Y));
            context.EndFigure(false);
            context.BeginFigure(new Point(cursor.X, cursor.Y - CursorArm), false);
            context.LineTo(new Point(cursor.X, cursor.Y + CursorArm));
            context.EndFigure(false);
        }

        var halo = MakeLine(geometry, White, 4, dashed: false);
        var line = MakeLine(geometry, Brushes.Black, 2, dashed: false);
        AutomationProperties.SetAutomationId(line, "AnnotationKeyboardCursor");
        MarkLayer.Children.Add(halo);
        MarkLayer.Children.Add(line);
    }

    /// <summary>
    ///     Gets the smallest box that holds points.
    /// </summary>
    /// <param name="points">The points; at least one.</param>
    /// <returns>The box.</returns>
    private static RenderBox BoxOf(IReadOnlyList<RenderPoint> points)
    {
        var left = points.Min(point => point.X);
        var top = points.Min(point => point.Y);
        var right = points.Max(point => point.X);
        var bottom = points.Max(point => point.Y);
        return new RenderBox(left, top, right - left, bottom - top);
    }

    /// <summary>
    ///     Describes a mark for a screen reader.
    /// </summary>
    /// <param name="mark">The mark.</param>
    /// <returns>The description: number, shape, and note.</returns>
    private static string Describe(Mark mark)
    {
        var text = $"Mark {mark.Number}, {AnnotationOverlayViewModel.ToolName(ToolOf(mark.Kind))}";
        return mark.Note is null ? text : $"{text}, note: {mark.Note}";
    }

    /// <summary>
    ///     Gets the tool that draws a shape.
    /// </summary>
    /// <param name="kind">The shape.</param>
    /// <returns>The tool.</returns>
    private static AnnotationTool ToolOf(MarkKind kind) => kind switch
    {
        MarkKind.Rectangle => AnnotationTool.Rectangle,
        MarkKind.Ellipse => AnnotationTool.Ellipse,
        MarkKind.Arrow => AnnotationTool.Arrow,
        _ => AnnotationTool.Freehand,
    };
}
