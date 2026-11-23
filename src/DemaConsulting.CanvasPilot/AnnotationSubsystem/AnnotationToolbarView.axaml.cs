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
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using DemaConsulting.CanvasPilot.PreviewSubsystem;

namespace DemaConsulting.CanvasPilot.AnnotationSubsystem;

/// <summary>
///     View of the tool bar of the annotation layer, bound to an <see cref="AnnotationToolbarViewModel" /> by the view
///     locator: one button per tool, the note of a finished shape with its two buttons, and what is said to the user.
///     It puts the keyboard focus in the note when a finished shape asks for one, and takes Enter and Escape in it.
/// </summary>
public sealed partial class AnnotationToolbarView : UserControl
{
    /// <summary>
    ///     The layer's view model the view is following, or null.
    /// </summary>
    private AnnotationOverlayViewModel? _annotation;

    /// <summary>
    ///     The pane that shows the tool bar, whose keys choose the tools, or null.
    /// </summary>
    private PreviewPaneView? _pane;

    /// <summary>
    ///     Initializes a new instance of the <see cref="AnnotationToolbarView" /> class.
    /// </summary>
    public AnnotationToolbarView()
    {
        InitializeComponent();
        NoteBox.MaxLength = MarkRegistry.MaximumNoteLength;
        NoteBox.AddHandler(KeyDownEvent, OnNoteKeyDown, RoutingStrategies.Tunnel);
    }

    /// <inheritdoc />
    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);
        Follow((DataContext as AnnotationToolbarViewModel)?.Annotation);
    }

    /// <inheritdoc />
    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        Follow(null);
        if (_pane is not null)
        {
            _pane.RemoveHandler(KeyDownEvent, OnPaneKeyDown);
            _pane = null;
        }

        base.OnDetachedFromVisualTree(e);
    }

    /// <inheritdoc />
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        Follow((DataContext as AnnotationToolbarViewModel)?.Annotation);

        // The tool keys work while the focus is anywhere in the pane that shows the tool bar, except in a box for text
        _pane = this.FindAncestorOfType<PreviewPaneView>();
        _pane?.AddHandler(KeyDownEvent, OnPaneKeyDown);
    }

    /// <summary>
    ///     Chooses the tool of a key: V for Select, R for rectangle, E for ellipse, F for freehand, and A for arrow.
    /// </summary>
    /// <param name="sender">The pane.</param>
    /// <param name="e">The key.</param>
    private void OnPaneKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Handled || e.KeyModifiers != KeyModifiers.None || _annotation is not { ToolsEnabled: true } || e.Source is TextBox)
        {
            return;
        }

        AnnotationTool? tool = e.Key switch
        {
            Key.V => AnnotationTool.None,
            Key.R => AnnotationTool.Rectangle,
            Key.E => AnnotationTool.Ellipse,
            Key.F => AnnotationTool.Freehand,
            Key.A => AnnotationTool.Arrow,
            _ => null,
        };
        if (tool is { } chosen)
        {
            _annotation.SelectTool(chosen);
            e.Handled = true;
        }
    }

    /// <summary>
    ///     Follows a layer's view model, and stops following the one that was followed before.
    /// </summary>
    /// <param name="annotation">The view model to follow, or null for none.</param>
    private void Follow(AnnotationOverlayViewModel? annotation)
    {
        if (ReferenceEquals(_annotation, annotation))
        {
            return;
        }

        if (_annotation is not null)
        {
            _annotation.PropertyChanged -= OnAnnotationPropertyChanged;
        }

        _annotation = annotation;
        if (_annotation is not null)
        {
            _annotation.PropertyChanged += OnAnnotationPropertyChanged;
        }
    }

    /// <summary>
    ///     Puts the focus in the note when a finished shape starts to wait for it.
    /// </summary>
    /// <param name="sender">The layer's view model.</param>
    /// <param name="e">The change details.</param>
    private void OnAnnotationPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(AnnotationOverlayViewModel.IsNoteEditing) && _annotation is { IsNoteEditing: true })
        {
            // The note becomes visible in this pass, so the focus is moved after the layout has made it focusable
            Dispatcher.UIThread.Post(() =>
            {
                if (_annotation is { IsNoteEditing: true })
                {
                    NoteBox.Focus();
                }
            });
        }
    }

    /// <summary>
    ///     Makes the mark on Enter and throws the shape away on Escape while the focus is in the note.
    /// </summary>
    /// <param name="sender">The note.</param>
    /// <param name="e">The key.</param>
    private void OnNoteKeyDown(object? sender, KeyEventArgs e)
    {
        if (_annotation is null)
        {
            return;
        }

        if (e.Key is Key.Enter && e.KeyModifiers == KeyModifiers.None)
        {
            _annotation.CommitNote();
            e.Handled = true;
        }
        else if (e.Key is Key.Escape)
        {
            _annotation.Cancel();
            e.Handled = true;
        }
    }
}
