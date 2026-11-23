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
using DemaConsulting.CanvasPilot.SettingsSubsystem;

namespace DemaConsulting.CanvasPilot.AppShellSubsystem;

/// <summary>
///     View of the main window's content area, bound to a <see cref="ProjectWorkspaceViewModel" /> by the view
///     locator: the project explorer, a splitter, the stage that shows the preview, and, while a project is open, a second
///     splitter and the chat column. The widths of the explorer and the chat column are the ones the user chose, kept within
///     their limits and made to fit the width of the window (see <see cref="WorkspaceLayout" />); a choice made with a
///     splitter is given back to the view model, which the shell saves.
/// </summary>
public sealed partial class ProjectWorkspaceView : UserControl
{
    /// <summary>
    ///     The view model the view follows, or null.
    /// </summary>
    private ProjectWorkspaceViewModel? _viewModel;

    /// <summary>
    ///     Initializes a new instance of the <see cref="ProjectWorkspaceView" /> class.
    /// </summary>
    public ProjectWorkspaceView()
    {
        InitializeComponent();

        // A choice with a splitter ends when the pointer is released or when a key is released
        ExplorerSplitter.DragCompleted += (_, _) => CommitExplorerWidth();
        ChatSplitter.DragCompleted += (_, _) => CommitChatWidth();
        ExplorerSplitter.KeyUp += (_, _) => CommitExplorerWidth();
        ChatSplitter.KeyUp += (_, _) => CommitChatWidth();
    }

    /// <inheritdoc />
    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);

        if (_viewModel is not null)
        {
            _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
        }

        _viewModel = DataContext as ProjectWorkspaceViewModel;
        if (_viewModel is not null)
        {
            _viewModel.PropertyChanged += OnViewModelPropertyChanged;
        }

        ApplyLayout();
    }

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        // The widths are made to fit again when the window changes its width (safe while the XAML loads: ApplyLayout
        // returns at once until the view model is set, and that happens only after the load)
        if (change.Property == BoundsProperty)
        {
            ApplyLayout();
        }
    }

    /// <summary>
    ///     Follows the view model when the chat column appears or goes away, or when other widths were chosen or restored.
    /// </summary>
    /// <param name="sender">The view model.</param>
    /// <param name="e">The change details.</param>
    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(ProjectWorkspaceViewModel.IsChatVisible)
            or nameof(ProjectWorkspaceViewModel.ExplorerWidth)
            or nameof(ProjectWorkspaceViewModel.ChatWidth))
        {
            ApplyLayout();
        }
    }

    /// <summary>
    ///     Gives the explorer, the splitters, and the chat column their widths: what the user chose, made to fit the window,
    ///     and none for the chat column and its splitter while the chat is not shown.
    /// </summary>
    private void ApplyLayout()
    {
        if (_viewModel is null)
        {
            return;
        }

        var columns = WorkspaceGrid.ColumnDefinitions;
        var chatVisible = _viewModel.IsChatVisible;
        var (explorer, chat) = WorkspaceLayout.Fit(Bounds.Width > 0 ? Bounds.Width : double.MaxValue, _viewModel.ExplorerWidth, _viewModel.ChatWidth, chatVisible);
        columns[0].Width = new GridLength(explorer);
        columns[3].Width = new GridLength(chatVisible ? WorkspaceLayout.SplitterWidth : 0);
        columns[4].MinWidth = chatVisible ? WindowSettings.MinimumChatWidth : 0;
        columns[4].Width = new GridLength(chat);
    }

    /// <summary>
    ///     Gives the view model the width of the explorer that the user chose with its splitter.
    /// </summary>
    private void CommitExplorerWidth()
    {
        _viewModel?.SetColumnWidths(WidthOf(WorkspaceGrid.ColumnDefinitions[0]), _viewModel.ChatWidth);
    }

    /// <summary>
    ///     Gives the view model the width of the chat column that the user chose with its splitter.
    /// </summary>
    private void CommitChatWidth()
    {
        _viewModel?.SetColumnWidths(_viewModel.ExplorerWidth, WidthOf(WorkspaceGrid.ColumnDefinitions[4]));
    }

    /// <summary>
    ///     Reads the width of a column that a splitter has just changed: the width that the splitter set, which is known
    ///     before the next layout pass, or the width that the column has when it is not a fixed one.
    /// </summary>
    /// <param name="column">The column.</param>
    /// <returns>The width.</returns>
    private static double WidthOf(ColumnDefinition column)
    {
        return column.Width.IsAbsolute ? column.Width.Value : column.ActualWidth;
    }
}
