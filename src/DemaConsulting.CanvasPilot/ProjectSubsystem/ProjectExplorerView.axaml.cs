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

using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Platform.Storage;
using Avalonia.VisualTree;

namespace DemaConsulting.CanvasPilot.ProjectSubsystem;

/// <summary>
///     View of the project explorer: the header with its buttons, the tree of the project's folders and files, and the drop
///     zone, bound to a <see cref="ProjectExplorerViewModel" /> by the view locator. Pictures and PDF files that are dropped on
///     the drop zone are copied into the <c>attachments</c> folder; dropped on a folder row they are copied into that folder,
///     and on a file row into the folder of the file.
/// </summary>
public sealed partial class ProjectExplorerView : UserControl
{
    /// <summary>
    ///     The control that shows where a drop would go, or null.
    /// </summary>
    private Control? _highlighted;

    /// <summary>
    ///     Initializes a new instance of the <see cref="ProjectExplorerView" /> class.
    /// </summary>
    public ProjectExplorerView()
    {
        InitializeComponent();
        DragDrop.SetAllowDrop(this, true);
        AddHandler(DragDrop.DragOverEvent, OnDragOver);
        AddHandler(DragDrop.DragLeaveEvent, OnDragLeave);
        AddHandler(DragDrop.DropEvent, OnDrop);
    }

    /// <summary>
    ///     Shows where a drop would go and says whether it is accepted: only files can be dropped, and only on the drop
    ///     zone or on a row of the tree.
    /// </summary>
    /// <param name="sender">The view.</param>
    /// <param name="e">The event data.</param>
    private void OnDragOver(object? sender, DragEventArgs e)
    {
        var accepts = DataContext is ProjectExplorerViewModel { HasProject: true } && e.DataTransfer.Contains(DataFormat.File);
        var target = accepts ? FindTarget(e.Source as Visual) : null;
        Highlight(target?.Highlight);
        e.DragEffects = target is null ? DragDropEffects.None : DragDropEffects.Copy;
        e.Handled = true;
    }

    /// <summary>
    ///     Takes the highlight away when the drag leaves the view.
    /// </summary>
    /// <param name="sender">The view.</param>
    /// <param name="e">The event data.</param>
    private void OnDragLeave(object? sender, DragEventArgs e)
    {
        Highlight(null);
    }

    /// <summary>
    ///     Adds the dropped files to the project.
    /// </summary>
    /// <param name="sender">The view.</param>
    /// <param name="e">The event data.</param>
    private void OnDrop(object? sender, DragEventArgs e)
    {
        Highlight(null);
        e.Handled = true;
        if (DataContext is not ProjectExplorerViewModel { HasProject: true } viewModel || FindTarget(e.Source as Visual) is not { } target)
        {
            return;
        }

        var paths = (e.DataTransfer.TryGetFiles() ?? [])
            .Select(item => item.TryGetLocalPath())
            .OfType<string>()
            .ToList();
        if (paths.Count > 0)
        {
            viewModel.AddFiles(paths, target.Folder);
        }
    }

    /// <summary>
    ///     Finds what a drop at an element would go to: the drop zone, which takes the files into the attachments folder, or
    ///     the row of a folder or of a file, which takes them into that folder or the folder of the file.
    /// </summary>
    /// <param name="source">The element under the pointer.</param>
    /// <returns>The folder to copy into (<see langword="null" /> for the attachments folder) and the control to highlight, or <see langword="null" /> when nothing there takes a drop.</returns>
    private (string? Folder, Control? Highlight)? FindTarget(Visual? source)
    {
        for (var visual = source; visual is not null; visual = visual.GetVisualParent())
        {
            if (ReferenceEquals(visual, DropZone))
            {
                return (null, DropZone);
            }

            if (visual is TreeViewItem { DataContext: ProjectNodeViewModel node } row)
            {
                return node.IsFolder
                    ? (node.FullPath, row)
                    : (Path.GetDirectoryName(node.FullPath), null);
            }
        }

        return null;
    }

    /// <summary>
    ///     Shows a control as the place where a drop would go, and the one before as it was.
    /// </summary>
    /// <param name="control">The control, or <see langword="null" /> for none.</param>
    private void Highlight(Control? control)
    {
        if (ReferenceEquals(control, _highlighted))
        {
            return;
        }

        _highlighted?.Classes.Set("droptarget", false);
        _highlighted?.Classes.Set("drag-active", false);
        _highlighted = control;
        if (control is not null)
        {
            control.Classes.Set(control is TreeViewItem ? "droptarget" : "drag-active", true);
        }
    }
}
