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
using Avalonia.VisualTree;

namespace DemaConsulting.CanvasPilot.AppShellSubsystem;

/// <summary>
///     The welcome screen that fills the main window while no project is open, bound to a <see cref="WelcomeViewModel" />. The
///     recent project cards are buttons, so Tab, Enter, and Space work as on any button; the arrow keys move between the
///     cards, as on a grid, and Delete (or the context menu) removes a card from the list.
/// </summary>
public sealed partial class WelcomeView : UserControl
{
    /// <summary>
    ///     Initializes a new instance of the <see cref="WelcomeView" /> class.
    /// </summary>
    public WelcomeView()
    {
        InitializeComponent();
        RecentList.AddHandler(KeyDownEvent, OnCardKeyDown, Avalonia.Interactivity.RoutingStrategies.Bubble);
    }

    /// <summary>
    ///     Moves the focus between the cards with the arrow keys: left and right to the card before and after, up and down to the
    ///     card in the row above and below.
    /// </summary>
    /// <param name="sender">The list of cards.</param>
    /// <param name="e">The key event.</param>
    private void OnCardKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key is not (Key.Left or Key.Right or Key.Up or Key.Down) || e.Source is not Visual source)
        {
            return;
        }

        var cards = RecentList.GetVisualDescendants().OfType<Button>().Where(button => button.Classes.Contains("recent-card")).ToList();
        var index = cards.FindIndex(card => ReferenceEquals(card, source) || card.IsKeyboardFocusWithin);
        if (index < 0)
        {
            return;
        }

        // A row holds the cards that start at the same height as the first one
        var columns = Math.Max(1, cards.Count(card => Math.Abs(card.Bounds.Y - cards[0].Bounds.Y) < 1));
        var next = e.Key switch
        {
            Key.Left => index - 1,
            Key.Right => index + 1,
            Key.Up => index - columns,
            _ => index + columns,
        };

        if (next >= 0 && next < cards.Count)
        {
            cards[next].Focus(NavigationMethod.Directional);
        }

        e.Handled = true;
    }
}
