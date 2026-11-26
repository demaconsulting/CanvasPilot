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
///         application's view locator, and follows the latest row: whenever the content or the height of the list changes
///         (rows are restored or added, a reply grows, the panel is laid out for the first time) while the reader is at the
///         end, it scrolls to the end again, so a project opens at its latest message. A reader who scrolls up stops the
///         following and keeps the place; scrolling back to the end, sending a message, showing another conversation,
///         showing the panel again, or a change of the layout that leaves the reader at the end follows again. Following
///         is decided from the scroll changes themselves, because restored rows are added before
///         the list has a size, when scrolling to the end at once does nothing. The banner
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
    ///     How far from the end, in device independent pixels, the reader may be and still count as at the end: about a
    ///     line, so that rounding of the layout and a small nudge do not stop the following.
    /// </summary>
    private const double FollowTolerance = 24;

    /// <summary>
    ///     The view model whose transcript is followed, or <see langword="null" /> before there is one.
    /// </summary>
    private ChatPanelViewModel? _viewModel;

    /// <summary>
    ///     Whether the transcript keeps its latest row in view; false while the reader has scrolled up to read.
    /// </summary>
    private bool _followLatest = true;

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

        // Enter sends the message, and Shift+Enter (or any other modifier) writes a new line in it
        ComposerView.AddHandler(KeyDownEvent, OnComposerKeyDown, RoutingStrategies.Tunnel);

        // The transcript follows its latest row unless the reader scrolled up
        TranscriptScroll.ScrollChanged += OnTranscriptScrollChanged;
    }

    /// <summary>
    ///     Sends the message when Enter is pressed on its own and the message can be sent; in every other case the editing
    ///     control gets the key, so a line break is written.
    /// </summary>
    /// <param name="sender">The editing control.</param>
    /// <param name="e">The key event.</param>
    private void OnComposerKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && e.KeyModifiers == KeyModifiers.None && _viewModel?.SendCommand.CanExecute(null) == true)
        {
            e.Handled = true;
            _viewModel.SendCommand.Execute(null);
        }
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

        // Another conversation is shown from its latest row
        _followLatest = true;
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

        // A panel that is shown again starts at the latest row: its content and height may be as they were, so no change
        // of the scroll viewer would bring it there
        _followLatest = true;
        TranscriptScroll.ScrollToEnd();

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
    ///     Writes the prompt of the button in the message and puts the keyboard focus there, so that the user can change it
    ///     and send it.
    /// </summary>
    /// <param name="sender">The button of the prompt.</param>
    /// <param name="e">The event data.</param>
    private void OnPromptClicked(object? sender, RoutedEventArgs e)
    {
        if (sender is Button { Content: string prompt } && _viewModel?.UsePrompt(prompt) == true)
        {
            ComposerView.FocusEditor();
        }
    }

    /// <summary>
    ///     Follows the latest row again when another conversation is shown (the transcript was emptied) or when the user
    ///     sent a message, which the user wants to see together with its answer.
    /// </summary>
    /// <param name="sender">The transcript.</param>
    /// <param name="e">What changed.</param>
    private void OnTranscriptChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        var userSent = e.Action == NotifyCollectionChangedAction.Add
                       && e.NewItems?.OfType<TranscriptEntryViewModel>().Any() == true;
        if (e.Action == NotifyCollectionChangedAction.Reset || userSent)
        {
            _followLatest = true;
        }
    }

    /// <summary>
    ///     Keeps the latest row in view while following, and tells from the movements of the reader whether to follow.
    /// </summary>
    /// <remarks>
    ///     A change of the content or of the height of the list (a row added or restored, a reply that grows, the first
    ///     layout, the composer that grows or shrinks, the window that is resized) scrolls to the end while following. A
    ///     movement of the offset is the reader's when nothing else changed, or when it went up while the content did not
    ///     shrink and the view did not grow, which are the only reasons for the layout to move the offset up; after a
    ///     movement of the reader, following goes on only when it left the reader at the end. A change that the layout
    ///     made is not the reader's, so it never stops the following; it starts it again when it leaves the reader at the
    ///     end, as when a reader who scrolled up a little makes the panel taller.
    /// </remarks>
    /// <param name="sender">The scroll viewer of the transcript.</param>
    /// <param name="e">How the content, the offset, and the height changed.</param>
    private void OnTranscriptScrollChanged(object? sender, ScrollChangedEventArgs e)
    {
        var contentChanged = e.ExtentDelta.Y != 0 || e.ViewportDelta.Y != 0;
        var layoutMovesUp = e.ExtentDelta.Y < 0 || e.ViewportDelta.Y > 0;
        var readerMoved = e.OffsetDelta.Y != 0 && (!contentChanged || (e.OffsetDelta.Y < 0 && !layoutMovesUp));
        if (readerMoved)
        {
            _followLatest = IsAtEnd();
        }
        else if (contentChanged)
        {
            _followLatest |= IsAtEnd();
        }

        if (contentChanged && _followLatest)
        {
            TranscriptScroll.ScrollToEnd();
        }
    }

    /// <summary>
    ///     Tells whether the reader is at the end of the transcript, within <see cref="FollowTolerance" />.
    /// </summary>
    /// <returns><see langword="true" /> when the bottom of the view is at most the tolerance above the end.</returns>
    private bool IsAtEnd() =>
        TranscriptScroll.Offset.Y + TranscriptScroll.Viewport.Height >= TranscriptScroll.Extent.Height - FollowTolerance;
}
