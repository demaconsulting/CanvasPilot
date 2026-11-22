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
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace DemaConsulting.CanvasPilot.UiTests;

/// <summary>
///     Headless subsystem test of the themes of the AppShell subsystem: the palettes of the theme make the stock
///     controls follow the brand in each theme variant.
/// </summary>
public sealed class AppShellThemeTests
{
    /// <summary>
    ///     Test that a stock window and the text on it take the surface and the text color of the requested theme
    ///     variant, and that the system accent is the brand accent in both variants.
    /// </summary>
    [AvaloniaFact]
    public void AppShellSubsystem_Themes_PaletteThemesStockControlsInBothVariants()
    {
        foreach (var variant in ThemeUiHelpers.Variants)
        {
            // Arrange: a stock window with a text block, in the variant
            var text = new TextBlock { Text = "Sample" };
            var window = new Window { Content = text, RequestedThemeVariant = variant };

            // Act: show the window so the theme styles are applied
            window.Show();
            var background = Assert.IsAssignableFrom<ISolidColorBrush>(window.Background).Color;
            var foreground = Assert.IsAssignableFrom<ISolidColorBrush>(text.Foreground).Color;
            window.Close();

            // Assert: the window is the pane surface of the variant, the text is its primary text color, and the
            // accent of the system is the brand accent
            Assert.Equal(ThemeUiHelpers.ColorOf("CpSurfacePane", variant), background);
            Assert.Equal(ThemeUiHelpers.ColorOf("CpTextPrimary", variant), foreground);
            Assert.Equal(Color.Parse("#4f46e5"), ThemeUiHelpers.Resolve("SystemAccentColor", variant));
        }

        // Assert: the two variants really differ
        Assert.NotEqual(
            ThemeUiHelpers.ColorOf("CpSurfacePane", ThemeVariant.Dark),
            ThemeUiHelpers.ColorOf("CpSurfacePane", ThemeVariant.Light));
    }

    /// <summary>
    ///     Test that walking through a window with the Tab key shows the focus ring on the control that has the focus
    ///     and on no other, on an icon button, a tool toggle, a pill button, and a splitter.
    /// </summary>
    [AvaloniaFact]
    public void AppShellSubsystem_Themes_TabKeyShowsTheFocusRingOnTheFocusedControlOnly()
    {
        // Arrange: four controls with the shared looks, one after the other, in a window
        var controls = new Control[]
        {
            new Button { Theme = (ControlTheme)ThemeUiHelpers.Resolve("IconButton", ThemeVariant.Dark)!, Content = "A" },
            new ToggleButton { Theme = (ControlTheme)ThemeUiHelpers.Resolve("ToolToggle", ThemeVariant.Dark)!, Content = "B" },
            new Button { Theme = (ControlTheme)ThemeUiHelpers.Resolve("PillButton", ThemeVariant.Dark)!, Content = "C" },
            new GridSplitter { Theme = (ControlTheme)ThemeUiHelpers.Resolve("SplitterGrip", ThemeVariant.Dark)!, Width = 9, Height = 40 }
        };
        var panel = new StackPanel { Margin = new Thickness(20) };
        panel.Children.AddRange(controls);
        var window = new Window { Content = panel, RequestedThemeVariant = ThemeVariant.Dark };
        window.Show();
        Dispatcher.UIThread.RunJobs();
        var expected = ThemeUiHelpers.ColorOf("CpFocusRing", ThemeVariant.Dark);

        foreach (var focused in controls)
        {
            // Act: press the Tab key, which moves the focus to the next control
            window.KeyPressQwerty(PhysicalKey.Tab, RawInputModifiers.None);
            Dispatcher.UIThread.RunJobs();

            // Assert: only that control shows the ring, in the focus color
            foreach (var control in controls)
            {
                var ring = control.GetVisualDescendants().OfType<Border>().Single(part => part.Name == "PART_FocusRing");
                var color = Assert.IsAssignableFrom<ISolidColorBrush>(ring.BorderBrush).Color;
                Assert.Equal(ReferenceEquals(control, focused) ? expected : Colors.Transparent, color);
            }
        }

        window.Close();
    }

    /// <summary>
    ///     Test that an icon in a button takes the primary text color of the theme variant that is asked for, in
    ///     both variants, so the icon is as readable as the text of the button.
    /// </summary>
    [AvaloniaFact]
    public void AppShellSubsystem_Icons_IconInButtonTakesThePrimaryTextColorOfTheVariant()
    {
        foreach (var variant in ThemeUiHelpers.Variants)
        {
            // Arrange: a stock button with an icon of the application, in a window of the variant
            var icon = new PathIcon { Data = (Geometry)ThemeUiHelpers.Resolve("CpIconSettings", variant)! };
            icon.Classes.Add("icon");
            var window = new Window { Content = new Button { Content = icon }, RequestedThemeVariant = variant };

            // Act: show the window so the theme styles are applied and read the color of the icon
            window.Show();
            var color = Assert.IsAssignableFrom<ISolidColorBrush>(icon.Foreground).Color;
            window.Close();

            // Assert: the icon has the primary text color of the variant, and is 16 by 16
            Assert.Equal(ThemeUiHelpers.ColorOf("CpTextPrimary", variant), color);
            Assert.Equal(new Size(16, 16), icon.Bounds.Size);
        }
    }
}
