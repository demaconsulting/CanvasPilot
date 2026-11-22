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
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace DemaConsulting.CanvasPilot.UiTests;

/// <summary>
///     Headless UI tests of the control themes of <c>ThemeResources</c> (icon button, tool toggle, pill button,
///     chip border, and splitter grip): the keyboard focus ring, the size of the targets, the checked look, the
///     chip look, and the resize of the splitter by the keyboard.
/// </summary>
public sealed class ControlThemesViewTests
{
    /// <summary>
    ///     Looks up a control theme of the application by its key.
    /// </summary>
    /// <param name="key">The key of the theme.</param>
    /// <returns>The theme.</returns>
    private static ControlTheme ThemeOf(string key)
    {
        return Assert.IsType<ControlTheme>(ThemeUiHelpers.Resolve(key, ThemeVariant.Light));
    }

    /// <summary>
    ///     Shows a control alone in a window of a theme variant, so its theme is applied and laid out.
    /// </summary>
    /// <param name="control">The control.</param>
    /// <param name="variant">The theme variant.</param>
    /// <returns>The window, which the caller closes.</returns>
    private static Window Show(Control control, ThemeVariant variant)
    {
        var window = new Window
        {
            Width = 300,
            Height = 200,
            RequestedThemeVariant = variant,
            Content = new StackPanel { Margin = new Thickness(20), Children = { control, new Button { Name = "Other" } } }
        };
        window.Show();
        Dispatcher.UIThread.RunJobs();
        return window;
    }

    /// <summary>
    ///     Finds a part of the template of a control by its name.
    /// </summary>
    /// <typeparam name="T">The type of the part.</typeparam>
    /// <param name="control">The control that has the template.</param>
    /// <param name="name">The name of the part.</param>
    /// <returns>The part.</returns>
    private static T Part<T>(Control control, string name)
        where T : Control
    {
        return control.GetVisualDescendants().OfType<T>().Single(part => part.Name == name);
    }

    /// <summary>
    ///     Test that the icon button, the tool toggle, the pill button, and the splitter grip show the focus ring
    ///     in the focus color when they are focused with the keyboard, and show no ring when they are focused with
    ///     the pointer, in both theme variants.
    /// </summary>
    [AvaloniaFact]
    public void ThemeResources_Controls_FocusRingShowsForKeyboardFocusOnly()
    {
        foreach (var variant in ThemeUiHelpers.Variants)
        {
            var focusRing = ThemeUiHelpers.ColorOf("CpFocusRing", variant);
            Control[] controls =
            [
                new Button { Theme = ThemeOf("IconButton"), Content = "A" },
                new ToggleButton { Theme = ThemeOf("ToolToggle"), Content = "B" },
                new Button { Theme = ThemeOf("PillButton"), Content = "C" },
                new GridSplitter { Theme = ThemeOf("SplitterGrip"), Width = 9, Height = 60 }
            ];

            foreach (var control in controls)
            {
                // Arrange: the control alone in a window
                var window = Show(control, variant);
                var ring = Part<Border>(control, "PART_FocusRing");

                // Act / Assert: focused with the pointer there is no ring
                control.Focus(NavigationMethod.Pointer);
                Dispatcher.UIThread.RunJobs();
                Assert.Equal(Colors.Transparent, Assert.IsAssignableFrom<ISolidColorBrush>(ring.BorderBrush).Color);

                // Act / Assert: focused with the keyboard the ring is in the focus color, 2 px thick
                window.GetVisualDescendants().OfType<Button>().Single(other => other.Name == "Other").Focus();
                control.Focus(NavigationMethod.Tab);
                Dispatcher.UIThread.RunJobs();
                Assert.Equal(focusRing, Assert.IsAssignableFrom<ISolidColorBrush>(ring.BorderBrush).Color);
                Assert.Equal(new Thickness(2), ring.BorderThickness);

                window.Close();
            }
        }
    }

    /// <summary>
    ///     Test that the focus ring of a button is really drawn outside the button, where it is not clipped away:
    ///     the pixel just above the middle of the top edge has the focus color after keyboard focus.
    /// </summary>
    [AvaloniaFact]
    public void ThemeResources_Controls_FocusRingIsDrawnOutsideTheButton()
    {
        foreach (var variant in ThemeUiHelpers.Variants)
        {
            // Arrange: an icon button in a window of the variant
            var button = new Button
            {
                Theme = ThemeOf("IconButton"),
                HorizontalAlignment = HorizontalAlignment.Left,
                Content = new PathIcon { Width = 16, Height = 16 }
            };
            var window = Show(button, variant);

            // Act: focus it with the keyboard, draw the window, and read the pixel 2 px above the top edge
            button.Focus(NavigationMethod.Tab);
            Dispatcher.UIThread.RunJobs();
            var frame = window.CaptureRenderedFrame();
            Assert.NotNull(frame);
            var (pixels, stride) = ScreenshotSupport.ReadPixels(frame);
            var top = button.TranslatePoint(new Point(16, -2), window)!.Value;
            var offset = ((int)top.Y * stride) + ((int)top.X * 4);
            window.Close();

            // Assert: the pixel is the focus color
            var expected = ThemeUiHelpers.ColorOf("CpFocusRing", variant);
            Assert.Equal(expected, Color.FromRgb(pixels[offset], pixels[offset + 1], pixels[offset + 2]));
        }
    }

    /// <summary>
    ///     Test that the icon button, the tool toggle, and the pill button are at least 28 by 28 (the icon button and
    ///     the tool toggle 32 by 32), so they are easy to hit.
    /// </summary>
    [AvaloniaFact]
    public void ThemeResources_Controls_ButtonsHaveTargetsOfAtLeast28()
    {
        // Arrange / Act: each button alone with a small icon
        var sizes = new List<(string Name, Size Size)>();
        foreach (var (name, control) in new (string, Control)[]
                 {
                     ("IconButton", new Button { Theme = ThemeOf("IconButton"), HorizontalAlignment = HorizontalAlignment.Left, Content = new PathIcon { Width = 16, Height = 16 } }),
                     ("ToolToggle", new ToggleButton { Theme = ThemeOf("ToolToggle"), HorizontalAlignment = HorizontalAlignment.Left, Content = new PathIcon { Width = 16, Height = 16 } }),
                     ("PillButton", new Button { Theme = ThemeOf("PillButton"), HorizontalAlignment = HorizontalAlignment.Left, Content = "Add a legend" })
                 })
        {
            var window = Show(control, ThemeVariant.Light);
            sizes.Add((name, control.Bounds.Size));
            window.Close();
        }

        // Assert: the sizes
        Assert.Equal(new Size(32, 32), sizes.Single(size => size.Name == "IconButton").Size);
        Assert.Equal(new Size(32, 32), sizes.Single(size => size.Name == "ToolToggle").Size);
        var pill = sizes.Single(size => size.Name == "PillButton").Size;
        Assert.True(pill.Width >= 28 && pill.Height >= 28, $"The pill is {pill}.");
    }

    /// <summary>
    ///     Test that a checked tool toggle is filled with the accent and its content is white, and an unchecked one
    ///     is not filled, in both theme variants.
    /// </summary>
    [AvaloniaFact]
    public void ThemeResources_Controls_CheckedToolToggleIsFilledWithAccent()
    {
        foreach (var variant in ThemeUiHelpers.Variants)
        {
            // Arrange: an unchecked toggle
            var toggle = new ToggleButton { Theme = ThemeOf("ToolToggle"), Content = "R" };
            var window = Show(toggle, variant);
            var fill = Part<ContentPresenter>(toggle, "PART_ContentPresenter");
            var unchecked1 = Assert.IsAssignableFrom<ISolidColorBrush>(fill.Background).Color;

            // Act: check it
            toggle.IsChecked = true;
            Dispatcher.UIThread.RunJobs();
            var checkedFill = Assert.IsAssignableFrom<ISolidColorBrush>(fill.Background).Color;
            var checkedText = Assert.IsAssignableFrom<ISolidColorBrush>(toggle.Foreground).Color;
            window.Close();

            // Assert: unchecked is clear, checked is the accent with white text
            Assert.Equal(Colors.Transparent, unchecked1);
            Assert.Equal(ThemeUiHelpers.ColorOf("CpAccent", variant), checkedFill);
            Assert.Equal(Colors.White, checkedText);
        }
    }

    /// <summary>
    ///     Test that a border with the chip theme has the raised surface, the subtle outline, a pill shape, and a
    ///     height of at least 24, in both theme variants.
    /// </summary>
    [AvaloniaFact]
    public void ThemeResources_Controls_ChipBorderIsARaisedPill()
    {
        foreach (var variant in ThemeUiHelpers.Variants)
        {
            // Arrange / Act: a chip with a short text
            var chip = new Border { Theme = ThemeOf("ChipBorder"), Child = new TextBlock { Text = "Wrote release-flow.mmd" } };
            var window = Show(chip, variant);
            var height = chip.Bounds.Height;
            window.Close();

            // Assert: the look
            Assert.Equal(ThemeUiHelpers.ColorOf("CpSurfaceRaised", variant), Assert.IsAssignableFrom<ISolidColorBrush>(chip.Background).Color);
            Assert.Equal(ThemeUiHelpers.ColorOf("CpBorderSubtle", variant), Assert.IsAssignableFrom<ISolidColorBrush>(chip.BorderBrush).Color);
            Assert.Equal((CornerRadius)ThemeUiHelpers.Resolve("CpRadiusPill", variant)!, chip.CornerRadius);
            Assert.True(height >= 24, $"The chip is {height} high.");
        }
    }

    /// <summary>
    ///     Test that a splitter with the grip theme draws a 1 px line and a 4 by 40 grip, and that the arrow keys
    ///     resize the column before it by 16 px steps once it has the focus.
    /// </summary>
    [AvaloniaFact]
    public void ThemeResources_Controls_SplitterGripResizesByKeyboardInStepsOf16()
    {
        // Arrange: a grid of a column of 200, the splitter of 9, and the rest
        var splitter = new GridSplitter { Theme = ThemeOf("SplitterGrip"), Width = 9 };
        Grid.SetColumn(splitter, 1);
        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("200,9,*") };
        grid.Children.Add(splitter);
        var window = new Window { Width = 600, Height = 300, Content = grid, RequestedThemeVariant = ThemeVariant.Light };
        window.Show();
        Dispatcher.UIThread.RunJobs();
        splitter.Focus(NavigationMethod.Tab);
        Dispatcher.UIThread.RunJobs();

        // Act: read the parts, then press the right arrow and the left arrow
        var line = Part<Border>(splitter, "PART_Line");
        var grip = Part<Border>(splitter, "PART_Grip");
        window.KeyPressQwerty(PhysicalKey.ArrowRight, RawInputModifiers.None);
        Dispatcher.UIThread.RunJobs();
        var afterRight = grid.ColumnDefinitions[0].ActualWidth;
        window.KeyPressQwerty(PhysicalKey.ArrowLeft, RawInputModifiers.None);
        Dispatcher.UIThread.RunJobs();
        var afterLeft = grid.ColumnDefinitions[0].ActualWidth;
        window.Close();

        // Assert: a 1 px line, a 4 by 40 grip, a step of 16 each way
        Assert.Equal(1, line.Bounds.Width);
        Assert.Equal(new Size(4, 40), grip.Bounds.Size);
        Assert.Equal(216, afterRight);
        Assert.Equal(200, afterLeft);
    }
}
