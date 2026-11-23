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

using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;

namespace DemaConsulting.CanvasPilot.SpeechSubsystem;

/// <summary>
///     View of the "Speech" settings page, bound to a <see cref="SpeechSettingsPageViewModel" /> by the view
///     locator. It stacks the views of the device pickers, the model catalog panel, and the parameter editors.
/// </summary>
public sealed partial class SpeechSettingsPageView : UserControl
{
    /// <summary>
    ///     Initializes a new instance of the <see cref="SpeechSettingsPageView" /> class.
    /// </summary>
    public SpeechSettingsPageView()
    {
        InitializeComponent();

        // The box captures the key that is pressed in it, before the box itself would use the key to edit its text or to
        // move the focus; the tunnel route is used so that Space and the arrows reach the page model as well
        PushToTalkKeyBox.AddHandler(KeyDownEvent, OnPushToTalkKeyDown, RoutingStrategies.Tunnel);
    }

    /// <summary>
    ///     Gives the key that was pressed in the box to the page model, and keeps the key from doing what it normally does.
    /// </summary>
    /// <param name="sender">The box.</param>
    /// <param name="e">The event data.</param>
    private void OnPushToTalkKeyDown(object? sender, KeyEventArgs e)
    {
        if (DataContext is not SpeechSettingsPageViewModel page)
        {
            return;
        }

        // Tab and Shift+Tab are left alone so that the keyboard user can leave the box; Tab is refused as a talk key anyway
        if (e.Key == Key.Tab && (e.KeyModifiers & (KeyModifiers.Control | KeyModifiers.Alt | KeyModifiers.Meta)) == 0)
        {
            return;
        }

        // A modifier alone is waited out; Esc cancels. Everything else is offered to the model, and the focus stays
        page.TryCapturePushToTalk(e.Key, e.KeyModifiers);
        e.Handled = true;
    }
}
