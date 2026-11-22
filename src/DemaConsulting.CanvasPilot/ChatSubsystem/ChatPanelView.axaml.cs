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

using System.Collections.Specialized;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using DemaConsulting.CanvasPilot.ComposerSubsystem;

namespace DemaConsulting.CanvasPilot.ChatSubsystem;

/// <summary>
///     The chat panel: the transcript of the messages that were sent above the message being composed, with the button
///     that listens while it is held and the button that sends. It is made to be placed in a column of the window and
///     knows nothing of the window around it.
/// </summary>
/// <remarks>
///     <para>
///         <b>Hold to talk.</b> The talk button listens for exactly as long as the user holds it: pressing the pointer on it
///         starts listening and captures the pointer, so that letting go anywhere ends it; releasing the pointer, losing the
///         capture (the window was deactivated, the button was disabled, a dialog opened), losing the keyboard focus, or
///         releasing the space or the enter key all end listening. The view keeps one flag for "the button is held", so
///         that these ways of ending do not end it twice and the repeats of a held key start it only once. The rules of
///         what listening does are the composer's.
///     </para>
///     <para>
///         <b>Transcript.</b> The list shows one row per sent message, part of the answer, tool, and notice through the
///         application's view locator, and scrolls to the newest row when one is added and while a reply grows. The banner
///         of the readiness of the copilot, the indicator that it works, and the Stop button are bound to the view model.
///         <b>Pictures.</b> The composer's editing control gets the panel's
///         provider of chip pictures, so that mark chips show the area around the mark.
///     </para>
///     <para>
///         The panel prepares dictation when it is shown, so the first press does not wait for the engine to load. It
///         holds no state besides the flag and the view model it follows. User interface thread only.
///     </para>
/// </remarks>
public sealed partial class ChatPanelView : UserControl
{
    /// <summary>
    ///     The view model whose transcript is followed, or <see langword="null" /> before there is one.
    /// </summary>
    private ChatPanelViewModel? _viewModel;

    /// <summary>
    ///     Whether the talk button is held down, so that listening is started and ended once for each hold.
    /// </summary>
    private bool _holding;

    /// <summary>
    ///     Initializes a new instance of the <see cref="ChatPanelView" /> class.
    /// </summary>
    public ChatPanelView()
    {
        InitializeComponent();

        // Tunnel and bubble both: the button handles the press itself, and the hold must not depend on who handled it
        HoldToTalkButton.AddHandler(PointerPressedEvent, OnTalkPointerPressed, RoutingStrategies.Tunnel | RoutingStrategies.Bubble, handledEventsToo: true);
        HoldToTalkButton.AddHandler(PointerReleasedEvent, OnTalkPointerReleased, RoutingStrategies.Tunnel | RoutingStrategies.Bubble, handledEventsToo: true);
        HoldToTalkButton.AddHandler(PointerCaptureLostEvent, OnTalkPointerCaptureLost, RoutingStrategies.Direct, handledEventsToo: true);
        HoldToTalkButton.AddHandler(KeyDownEvent, OnTalkKeyDown, RoutingStrategies.Tunnel | RoutingStrategies.Bubble, handledEventsToo: true);
        HoldToTalkButton.AddHandler(KeyUpEvent, OnTalkKeyUp, RoutingStrategies.Tunnel | RoutingStrategies.Bubble, handledEventsToo: true);
        HoldToTalkButton.LostFocus += OnTalkLostFocus;
    }

    /// <summary>
    ///     Gets a value indicating whether the talk button is held down now.
    /// </summary>
    public bool IsTalkHeld => _holding;

    /// <inheritdoc />
    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);

        // Follow the transcript of the new view model, and give the editing control the pictures of the chips
        if (_viewModel is not null)
        {
            _viewModel.Transcript.CollectionChanged -= OnTranscriptChanged;
        }

        _viewModel = DataContext as ChatPanelViewModel;
        if (_viewModel is not null)
        {
            _viewModel.Transcript.CollectionChanged += OnTranscriptChanged;
            ComposerView.ThumbnailProvider = _viewModel.Thumbnails;
        }
        else
        {
            ComposerView.ThumbnailProvider = null;
        }
    }

    /// <inheritdoc />
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);

        // Load the speech engine now so that the first press of the talk button does not wait for it, and find out whether
        // the copilot can be used so that the banner is right from the start
        if (_viewModel is not null)
        {
            _ = _viewModel.PrepareDictationAsync();
            _ = _viewModel.RefreshReadinessAsync();
        }
    }

    /// <inheritdoc />
    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        // A panel that goes away while the button is held must not leave the microphone open
        EndHold();
        base.OnDetachedFromVisualTree(e);
    }

    /// <summary>
    ///     Starts listening because the pointer was pressed on the talk button.
    /// </summary>
    /// <param name="sender">The button.</param>
    /// <param name="e">The pointer event.</param>
    private void OnTalkPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (!e.GetCurrentPoint(HoldToTalkButton).Properties.IsLeftButtonPressed)
        {
            return;
        }

        // Capture the pointer so that the release is seen wherever the pointer is when it comes
        if (BeginHold())
        {
            e.Pointer.Capture(HoldToTalkButton);
        }
    }

    /// <summary>
    ///     Ends listening because the pointer was released.
    /// </summary>
    /// <param name="sender">The button.</param>
    /// <param name="e">The pointer event.</param>
    private void OnTalkPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        EndHold();
    }

    /// <summary>
    ///     Ends listening because the button lost the pointer, which is a release as far as the user is concerned.
    /// </summary>
    /// <param name="sender">The button.</param>
    /// <param name="e">The event data.</param>
    private void OnTalkPointerCaptureLost(object? sender, PointerCaptureLostEventArgs e)
    {
        EndHold();
    }

    /// <summary>
    ///     Starts listening because the space or the enter key was pressed on the focused talk button, so that the
    ///     keyboard can hold to talk as well.
    /// </summary>
    /// <param name="sender">The button.</param>
    /// <param name="e">The key event.</param>
    private void OnTalkKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key is Key.Space or Key.Enter)
        {
            BeginHold();
        }
    }

    /// <summary>
    ///     Ends listening because the space or the enter key was released.
    /// </summary>
    /// <param name="sender">The button.</param>
    /// <param name="e">The key event.</param>
    private void OnTalkKeyUp(object? sender, KeyEventArgs e)
    {
        if (e.Key is Key.Space or Key.Enter)
        {
            EndHold();
        }
    }

    /// <summary>
    ///     Ends listening because the button lost the keyboard focus.
    /// </summary>
    /// <param name="sender">The button.</param>
    /// <param name="e">The event data.</param>
    private void OnTalkLostFocus(object? sender, RoutedEventArgs e)
    {
        EndHold();
    }

    /// <summary>
    ///     Starts a hold when the button is not held already and a message can be written.
    /// </summary>
    /// <returns><see langword="true" /> when a hold was started.</returns>
    private bool BeginHold()
    {
        if (_holding || _viewModel is not { CanTalk: true } viewModel)
        {
            return false;
        }

        _holding = true;
        _ = viewModel.BeginPushToTalkAsync();
        return true;
    }

    /// <summary>
    ///     Ends the hold, if there is one.
    /// </summary>
    private void EndHold()
    {
        if (!_holding)
        {
            return;
        }

        _holding = false;
        if (_viewModel is not null)
        {
            _ = _viewModel.EndPushToTalkAsync();
        }
    }

    /// <summary>
    ///     Scrolls to the newest row when one was added, and to the end again while a reply of the copilot grows.
    /// </summary>
    /// <param name="sender">The transcript.</param>
    /// <param name="e">What changed.</param>
    private void OnTranscriptChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.Action == NotifyCollectionChangedAction.Add)
        {
            foreach (var reply in e.NewItems?.OfType<AssistantReplyViewModel>() ?? [])
            {
                reply.PropertyChanged += OnReplyChanged;
            }

            ScrollToEndLater();
        }
    }

    /// <summary>
    ///     Scrolls to the end because the text of a reply grew.
    /// </summary>
    /// <param name="sender">The reply.</param>
    /// <param name="e">The name of the property that changed.</param>
    private void OnReplyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(AssistantReplyViewModel.Text))
        {
            ScrollToEndLater();
        }
    }

    /// <summary>
    ///     Scrolls to the end when the layout has caught up.
    /// </summary>
    private void ScrollToEndLater() =>
        Dispatcher.UIThread.Post(() => TranscriptScroll.ScrollToEnd(), DispatcherPriority.Background);
}
