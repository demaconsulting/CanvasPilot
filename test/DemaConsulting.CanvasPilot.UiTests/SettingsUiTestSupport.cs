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
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.VisualTree;
using CommunityToolkit.Mvvm.ComponentModel;
using DemaConsulting.CanvasPilot.SettingsSubsystem;

namespace DemaConsulting.CanvasPilot.UiTests;

/// <summary>
///     Shared helpers for headless tests of the settings dialog.
/// </summary>
internal static class SettingsUiHelpers
{
    /// <summary>
    ///     Finds the control with the given automation identifier in a window's visual tree.
    /// </summary>
    /// <typeparam name="T">The expected control type.</typeparam>
    /// <param name="root">The window or control to search.</param>
    /// <param name="automationId">The automation identifier.</param>
    /// <returns>The control.</returns>
    public static T Find<T>(Visual root, string automationId)
        where T : Control
    {
        return root.GetVisualDescendants()
            .OfType<T>()
            .First(control => AutomationProperties.GetAutomationId(control) == automationId);
    }

    /// <summary>
    ///     Clicks a button with the mouse at its center, the way a user would.
    /// </summary>
    /// <param name="window">The window containing the button.</param>
    /// <param name="button">The button to click.</param>
    public static void Click(Window window, Button button)
    {
        var center = button.TranslatePoint(new Point(button.Bounds.Width / 2, button.Bounds.Height / 2), window)
                     ?? throw new InvalidOperationException("The button is not part of the window's visual tree.");
        window.MouseDown(center, MouseButton.Left);
        window.MouseUp(center, MouseButton.Left);
    }

    /// <summary>
    ///     Reads the value that a drop-down list tells a screen reader, which is the text of its chosen entry.
    /// </summary>
    /// <param name="combo">The drop-down list, closed as a user finds it.</param>
    /// <returns>The value of its automation peer, or <see langword="null" /> when nothing is chosen.</returns>
    public static string? AnnouncedValue(ComboBox combo)
    {
        var peer = ControlAutomationPeer.CreatePeerForElement(combo);
        return ((IValueProvider)peer).Value;
    }

    /// <summary>
    ///     Reads the names that a screen reader announces for the shown rows of a list.
    /// </summary>
    /// <param name="list">The list, shown in a window.</param>
    /// <returns>The name of the automation peer of each realized row, in order.</returns>
    public static IReadOnlyList<string> AnnouncedRowNames(ListBox list)
    {
        return list.GetVisualDescendants()
            .OfType<ListBoxItem>()
            .Select(item => ControlAutomationPeer.CreatePeerForElement(item).GetName())
            .ToList();
    }
}

/// <summary>
///     View model of a minimal second page, found by the view locator through its <c>TestSettingsPageView</c>.
/// </summary>
/// <param name="title">The page title.</param>
public sealed class TestSettingsPageViewModel(string title) : ObservableObject, ISettingsPage
{
    /// <summary>
    ///     Whether the page currently reports valid entries.
    /// </summary>
    private bool _isValid = true;

    /// <inheritdoc />
    public string Title { get; } = title;

    /// <inheritdoc />
    public bool IsValid
    {
        get => _isValid;
        set => SetProperty(ref _isValid, value);
    }

    /// <summary>
    ///     Gets how many times <see cref="OnClosed" /> was called.
    /// </summary>
    public int ClosedCount { get; private set; }

    /// <inheritdoc />
    public void Load(AppSettings settings)
    {
        // The page holds no settings.
    }

    /// <inheritdoc />
    public AppSettings Apply(AppSettings settings) => settings;

    /// <inheritdoc />
    public void OnClosed()
    {
        ClosedCount++;
    }
}

/// <summary>
///     View for <see cref="TestSettingsPageViewModel" />: a single text block.
/// </summary>
public sealed class TestSettingsPageView : UserControl
{
    /// <summary>
    ///     Initializes a new instance of the <see cref="TestSettingsPageView" /> class.
    /// </summary>
    public TestSettingsPageView()
    {
        Content = new TextBlock { Text = "Test page content" };
    }
}
