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
using Avalonia.Controls;

namespace DemaConsulting.CanvasPilot.AppShellSubsystem;

/// <summary>
///     View of the main window's content area, bound to a <see cref="ProjectWorkspaceViewModel" /> by the view
///     locator: the project explorer, a splitter, the preview area, and, while a project is open, a second
///     splitter and the chat column. The chat column starts 380 wide; the width the user chose is kept while the
///     column is hidden.
/// </summary>
public sealed partial class ProjectWorkspaceView : UserControl
{
    /// <summary>
    ///     The width of the chat column when it is first shown.
    /// </summary>
    private const double DefaultChatWidth = 380;

    /// <summary>
    ///     The narrowest the chat column can be dragged while it is shown.
    /// </summary>
    private const double MinimumChatWidth = 280;

    /// <summary>
    ///     The view model the view follows, or null.
    /// </summary>
    private ProjectWorkspaceViewModel? _viewModel;

    /// <summary>
    ///     The width of the chat column the last time it was shown.
    /// </summary>
    private double _chatWidth = DefaultChatWidth;

    /// <summary>
    ///     Initializes a new instance of the <see cref="ProjectWorkspaceView" /> class.
    /// </summary>
    public ProjectWorkspaceView()
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

        _viewModel = DataContext as ProjectWorkspaceViewModel;
        if (_viewModel is not null)
        {
            _viewModel.PropertyChanged += OnViewModelPropertyChanged;
        }

        UpdateChatColumn();
    }

    /// <summary>
    ///     Follows the view model when the chat column appears or goes away.
    /// </summary>
    /// <param name="sender">The view model.</param>
    /// <param name="e">The change details.</param>
    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ProjectWorkspaceViewModel.IsChatVisible))
        {
            UpdateChatColumn();
        }
    }

    /// <summary>
    ///     Gives the chat column and its splitter their widths: the remembered width and an automatic width while the chat
    ///     is shown, and none while it is not.
    /// </summary>
    private void UpdateChatColumn()
    {
        var splitterColumn = this.FindControl<Grid>("WorkspaceGrid")!.ColumnDefinitions[3];
        var chatColumn = this.FindControl<Grid>("WorkspaceGrid")!.ColumnDefinitions[4];

        if (_viewModel?.IsChatVisible == true)
        {
            splitterColumn.Width = GridLength.Auto;
            chatColumn.MinWidth = MinimumChatWidth;
            chatColumn.Width = new GridLength(_chatWidth);
        }
        else
        {
            // Remember what the user chose, then close the column
            if (chatColumn.Width.IsAbsolute && chatColumn.Width.Value > 0)
            {
                _chatWidth = chatColumn.Width.Value;
            }

            splitterColumn.Width = new GridLength(0);
            chatColumn.MinWidth = 0;
            chatColumn.Width = new GridLength(0);
        }
    }
}
