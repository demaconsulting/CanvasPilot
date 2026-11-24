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
using CommunityToolkit.Mvvm.ComponentModel;
using DemaConsulting.CanvasPilot.PreviewSubsystem;
using DemaConsulting.CanvasPilot.ProjectSubsystem;
using DemaConsulting.CanvasPilot.SpeechSubsystem;

namespace DemaConsulting.CanvasPilot.AppShellSubsystem;

/// <summary>
///     What the status bar at the bottom of the main window says about the picture that is shown.
/// </summary>
public enum StatusBarState
{
    /// <summary>
    ///     No picture is shown, because no project is open or no file is selected.
    /// </summary>
    Idle,

    /// <summary>
    ///     The selected file is being rendered.
    /// </summary>
    Rendering,

    /// <summary>
    ///     The selected file could not be rendered.
    /// </summary>
    Failed,

    /// <summary>
    ///     A picture is shown.
    /// </summary>
    Ready
}

/// <summary>
///     View model of the status bar at the bottom of the main window. It follows the preview pane of the workspace and
///     says, with words as well as with an icon, whether a picture is ready, rendering, or failed, and what it is: its file
///     name, size, format, and render time. The bar is always there; with no project open it says so.
/// </summary>
public sealed class StatusBarViewModel : ObservableObject, IDisposable
{
    /// <summary>
    ///     The words that start the text that says when the project file was saved.
    /// </summary>
    public const string SavedPrefix = "Project file saved";

    /// <summary>
    ///     The talk key that is in effect, which the hint shows.
    /// </summary>
    private readonly PushToTalkSetting _talkKey;

    /// <summary>
    ///     The project service whose open project says when its file was last saved, or <see langword="null" /> for none.
    /// </summary>
    private readonly IProjectService? _projects;

    /// <summary>
    ///     The clock that tells what day it is now, for the text of the time.
    /// </summary>
    private readonly TimeProvider _clock;

    /// <summary>
    ///     The synchronization context of the thread that made the bar, which owns the user interface, or null.
    /// </summary>
    private readonly SynchronizationContext? _context = SynchronizationContext.Current;

    /// <summary>
    ///     The workspace whose project and preview the bar follows.
    /// </summary>
    private readonly ProjectWorkspaceViewModel _workspace;

    /// <summary>
    ///     The preview pane that is followed now, or null.
    /// </summary>
    private PreviewPaneViewModel? _preview;

    /// <summary>
    ///     Creates the status bar over a workspace. The bar does not own the workspace.
    /// </summary>
    /// <param name="workspace">The workspace whose project and preview the bar follows.</param>
    /// <param name="talkKey">The talk key that the hint shows; <see langword="null" /> for the default key.</param>
    /// <param name="projects">
    ///     The project service whose open project says when its file was saved; <see langword="null" /> for none, when no
    ///     time is shown.
    /// </param>
    /// <param name="clock">The clock that tells what day it is now; <see langword="null" /> for the system's.</param>
    public StatusBarViewModel(
        ProjectWorkspaceViewModel workspace,
        PushToTalkSetting? talkKey = null,
        IProjectService? projects = null,
        TimeProvider? clock = null)
    {
        ArgumentNullException.ThrowIfNull(workspace);

        _workspace = workspace;
        _talkKey = talkKey ?? PushToTalkSetting.Fixed();
        _projects = projects;
        _clock = clock ?? TimeProvider.System;
        _workspace.PropertyChanged += OnWorkspacePropertyChanged;
        _talkKey.PropertyChanged += OnTalkKeyChanged;
        if (_projects is not null)
        {
            _projects.CurrentChanged += OnProjectsChanged;
        }

        FollowPreview();
    }

    /// <summary>
    ///     Gets the text that says when the file of the open project was last saved, such as
    ///     <c>Project file saved 14:32</c> (the time alone for today, otherwise with the date), or an empty string when
    ///     nothing was saved: no project is open, or the file has no time. Only what is true is said: the drawings are files
    ///     that the copilot writes and the chat history is saved apart, so neither is claimed.
    /// </summary>
    public string SavedText
    {
        get
        {
            if (_projects?.Current?.Document.ModifiedUtc is not { } modified)
            {
                return string.Empty;
            }

            return $"{SavedPrefix} {FormatSavedTime(modified, _clock.GetUtcNow(), CultureInfo.CurrentCulture)}";
        }
    }

    /// <summary>
    ///     Gets a value indicating whether there is a <see cref="SavedText" /> to show.
    /// </summary>
    public bool HasSavedText => SavedText.Length > 0;

    /// <summary>
    ///     Writes the time of a save for the status bar: the time of day when it was today, and otherwise the date and the
    ///     time, in the local time zone.
    /// </summary>
    /// <param name="modified">When the file was saved.</param>
    /// <param name="now">The present time.</param>
    /// <param name="culture">The culture of the user.</param>
    /// <returns>The text, for example <c>14:32</c> or <c>11/21/2026 14:32</c>.</returns>
    public static string FormatSavedTime(DateTimeOffset modified, DateTimeOffset now, CultureInfo culture)
    {
        var local = modified.ToLocalTime();
        return local.Date == now.ToLocalTime().Date
            ? local.ToString("t", culture)
            : local.ToString("g", culture);
    }

    /// <summary>
    ///     Gets the name of the key that is held to talk, shown as a key cap in the hint. It follows the setting.
    /// </summary>
    public string TalkKeyText => _talkKey.Text;

    /// <summary>
    ///     Gets the state of the picture that the bar reports.
    /// </summary>
    public StatusBarState State
    {
        get
        {
            if (_preview is not { } preview)
            {
                return StatusBarState.Idle;
            }

            if (preview.IsBusy)
            {
                return StatusBarState.Rendering;
            }

            if (preview.HasError)
            {
                return StatusBarState.Failed;
            }

            return preview.HasImage ? StatusBarState.Ready : StatusBarState.Idle;
        }
    }

    /// <summary>
    ///     Gets a value indicating whether no picture is shown.
    /// </summary>
    public bool IsIdle => State == StatusBarState.Idle;

    /// <summary>
    ///     Gets a value indicating whether the selected file is being rendered.
    /// </summary>
    public bool IsRendering => State == StatusBarState.Rendering;

    /// <summary>
    ///     Gets a value indicating whether the selected file could not be rendered.
    /// </summary>
    public bool IsFailed => State == StatusBarState.Failed;

    /// <summary>
    ///     Gets a value indicating whether a picture is shown.
    /// </summary>
    public bool IsReady => State == StatusBarState.Ready;

    /// <summary>
    ///     Gets the sentence that sums up the bar for a screen reader: what is being rendered, the picture with its size and
    ///     format, that the render failed, or that there is no picture.
    /// </summary>
    public string Summary
    {
        get
        {
            if (State == StatusBarState.Idle)
            {
                return _workspace.HasProject ? "No picture selected" : "No project open";
            }

            return _preview!.StatusText;
        }
    }

    /// <summary>
    ///     Gets the name of the file that the bar reports on, or an empty string.
    /// </summary>
    public string FileName => _preview?.FileName ?? string.Empty;

    /// <summary>
    ///     Gets the size of the shown picture, such as <c>900 × 500 px</c>, or an empty string.
    /// </summary>
    public string SizeText => IsReady ? _preview!.ImageSizeText : string.Empty;

    /// <summary>
    ///     Gets the format of the shown picture, such as <c>SVG</c>, or an empty string.
    /// </summary>
    public string FormatText => IsReady ? _preview!.FormatText : string.Empty;

    /// <summary>
    ///     Gets how long the render took, such as <c>Rendered in 84 ms</c>, or an empty string.
    /// </summary>
    public string RenderTimeText => IsReady ? _preview!.RenderTimeText : string.Empty;

    /// <summary>
    ///     Gets the number of notes of the render, such as <c>2 notes</c>, or an empty string when there are none.
    /// </summary>
    public string NotesText => IsReady && _preview!.HasDiagnostics ? _preview.DiagnosticsHeader : string.Empty;

    /// <summary>
    ///     Gets a value indicating whether the render has notes.
    /// </summary>
    public bool HasNotes => NotesText.Length > 0;

    /// <summary>
    ///     Gets a value indicating whether a project is open, which is when the project and the talk key are mentioned.
    /// </summary>
    public bool HasProject => _workspace.HasProject;

    /// <inheritdoc />
    public void Dispose()
    {
        _workspace.PropertyChanged -= OnWorkspacePropertyChanged;
        _talkKey.PropertyChanged -= OnTalkKeyChanged;
        if (_projects is not null)
        {
            _projects.CurrentChanged -= OnProjectsChanged;
        }
        if (_preview is not null)
        {
            _preview.PropertyChanged -= OnPreviewPropertyChanged;
            _preview = null;
        }
    }

    /// <summary>
    ///     Says that the text of the time of the save changed when the open project changed or was saved. The service may
    ///     raise the event on another thread, so the announcement goes to the thread of the user interface.
    /// </summary>
    /// <param name="sender">The project service.</param>
    /// <param name="e">The event data.</param>
    private void OnProjectsChanged(object? sender, EventArgs e)
    {
        void Announce()
        {
            OnPropertyChanged(nameof(SavedText));
            OnPropertyChanged(nameof(HasSavedText));
        }

        if (_context is null)
        {
            Announce();
        }
        else
        {
            _context.Post(_ => Announce(), null);
        }
    }

    /// <summary>
    ///     Says that the talk key changed.
    /// </summary>
    /// <param name="sender">The setting of the talk key.</param>
    /// <param name="e">The change details.</param>
    private void OnTalkKeyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(PushToTalkSetting.Text))
        {
            OnPropertyChanged(nameof(TalkKeyText));
        }
    }

    /// <summary>
    ///     Follows the preview pane that the workspace holds now, and drops the one that it held before.
    /// </summary>
    private void FollowPreview()
    {
        var current = _workspace.Preview as PreviewPaneViewModel;
        if (ReferenceEquals(current, _preview))
        {
            return;
        }

        if (_preview is not null)
        {
            _preview.PropertyChanged -= OnPreviewPropertyChanged;
        }

        _preview = current;
        if (_preview is not null)
        {
            _preview.PropertyChanged += OnPreviewPropertyChanged;
        }
    }

    /// <summary>
    ///     Follows the workspace when the project is opened or closed or the preview pane is replaced.
    /// </summary>
    /// <param name="sender">The workspace.</param>
    /// <param name="e">The change details.</param>
    private void OnWorkspacePropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(ProjectWorkspaceViewModel.Preview) or nameof(ProjectWorkspaceViewModel.HasProject))
        {
            FollowPreview();
            OnPropertyChanged(string.Empty);
        }
    }

    /// <summary>
    ///     Refreshes everything the bar says when the preview pane changed, because every part of it depends on the same
    ///     few facts.
    /// </summary>
    /// <param name="sender">The preview pane.</param>
    /// <param name="e">The change details.</param>
    private void OnPreviewPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        OnPropertyChanged(string.Empty);
    }
}
