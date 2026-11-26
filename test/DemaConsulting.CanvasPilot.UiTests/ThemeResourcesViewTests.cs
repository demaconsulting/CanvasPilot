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
using Avalonia.Controls.Documents;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using DemaConsulting.CanvasPilot.KnowledgeSubsystem;

namespace DemaConsulting.CanvasPilot.UiTests;

/// <summary>
///     Helpers for the tests that read the theme resources of the running application: they resolve a resource in
///     a theme variant and compute the contrast between two colors the way the Web Content Accessibility Guidelines
///     define it, so that a test can state "at least 4.5:1" about what the user sees.
/// </summary>
internal static class ThemeUiHelpers
{
    /// <summary>
    ///     The theme variants that the application supports.
    /// </summary>
    public static readonly ThemeVariant[] Variants = [ThemeVariant.Dark, ThemeVariant.Light];

    /// <summary>
    ///     Resolves an application resource in a theme variant.
    /// </summary>
    /// <param name="key">The resource key.</param>
    /// <param name="variant">The theme variant to resolve it in.</param>
    /// <returns>The resource, or <see langword="null" /> when the application has none under the key.</returns>
    public static object? Resolve(string key, ThemeVariant variant)
    {
        return Application.Current!.TryGetResource(key, variant, out var value) ? value : null;
    }

    /// <summary>
    ///     Resolves a brush resource of the application in a theme variant and returns its color.
    /// </summary>
    /// <param name="key">The resource key of a solid color brush.</param>
    /// <param name="variant">The theme variant to resolve it in.</param>
    /// <returns>The color of the brush.</returns>
    public static Color ColorOf(string key, ThemeVariant variant)
    {
        var brush = Assert.IsAssignableFrom<ISolidColorBrush>(Resolve(key, variant));
        return brush.Color;
    }

    /// <summary>
    ///     Resolves a gradient brush resource of the application in a theme variant and returns the colors of its stops, in order.
    /// </summary>
    /// <param name="key">The resource key of a gradient brush.</param>
    /// <param name="variant">The theme variant to resolve it in.</param>
    /// <returns>The colors of the gradient stops.</returns>
    public static IReadOnlyList<Color> StopColorsOf(string key, ThemeVariant variant)
    {
        var brush = Assert.IsAssignableFrom<IGradientBrush>(Resolve(key, variant));
        return [.. brush.GradientStops.Select(stop => stop.Color)];
    }

    /// <summary>
    ///     Lays a color that may be translucent over an opaque background.
    /// </summary>
    /// <param name="top">The color on top.</param>
    /// <param name="background">The opaque color below it.</param>
    /// <returns>The opaque color the user sees.</returns>
    public static Color Over(Color top, Color background)
    {
        var alpha = top.A / 255.0;
        byte Mix(byte over, byte under) => (byte)Math.Round((over * alpha) + (under * (1 - alpha)));
        return Color.FromRgb(Mix(top.R, background.R), Mix(top.G, background.G), Mix(top.B, background.B));
    }

    /// <summary>
    ///     Computes the contrast ratio of two opaque colors.
    /// </summary>
    /// <param name="first">The first color.</param>
    /// <param name="second">The second color.</param>
    /// <returns>The ratio, from 1 (no contrast) to 21 (black on white).</returns>
    public static double Contrast(Color first, Color second)
    {
        var a = RelativeLuminance(first);
        var b = RelativeLuminance(second);
        return (Math.Max(a, b) + 0.05) / (Math.Min(a, b) + 0.05);
    }

    /// <summary>
    ///     Computes the relative luminance of an opaque color.
    /// </summary>
    /// <param name="color">The color.</param>
    /// <returns>The luminance, from 0 (black) to 1 (white).</returns>
    private static double RelativeLuminance(Color color)
    {
        static double Linear(byte channel)
        {
            var c = channel / 255.0;
            return c <= 0.03928 ? c / 12.92 : Math.Pow((c + 0.055) / 1.055, 2.4);
        }

        return (0.2126 * Linear(color.R)) + (0.7152 * Linear(color.G)) + (0.0722 * Linear(color.B));
    }
}

/// <summary>
///     Headless UI tests of the theme resources: the design tokens resolve in both theme variants, the colors meet
///     the contrast that the design promises, and a control that refers to a token follows a change of variant.
/// </summary>
public sealed class ThemeResourcesViewTests
{
    /// <summary>
    ///     The brush tokens that differ per theme variant.
    /// </summary>
    private static readonly string[] VariantBrushKeys =
    [
        "CpSurfaceWindow", "CpSurfacePane", "CpSurfaceStage", "CpSurfaceRaised", "CpSurfaceOverlay",
        "CpBorderSubtle", "CpBorderStrong", "CpBorderControl",
        "CpTextPrimary", "CpTextSecondary", "CpTextTertiary",
        "CpAccentText", "CpAccentTint", "CpAccentTintHover", "CpFocusRing",
        "CpSuccess", "CpWarning", "CpDanger",
        "CpFillDictated", "CpFillCode"
    ];

    /// <summary>
    ///     The brush tokens that are the same in both theme variants.
    /// </summary>
    private static readonly string[] SharedBrushKeys =
        ["CpAccent", "CpTextOnAccent", "CpMark", "CpMarkTint", "CpAccentGradient"];

    /// <summary>
    ///     The shadow tokens.
    /// </summary>
    private static readonly string[] ShadowKeys = ["CpShadowFloating", "CpShadowDialog", "CpShadowPicture"];

    /// <summary>
    ///     The spacing tokens, 4 to 48.
    /// </summary>
    private static readonly string[] SpaceKeys =
        ["CpSpace1", "CpSpace2", "CpSpace3", "CpSpace4", "CpSpace5", "CpSpace6", "CpSpace7"];

    /// <summary>
    ///     The corner radius tokens.
    /// </summary>
    private static readonly string[] RadiusKeys =
        ["CpRadiusKey", "CpRadiusRow", "CpRadiusInput", "CpRadiusCard", "CpRadiusWelcome", "CpRadiusPill"];

    /// <summary>
    ///     The type scale tokens, the sizes and the line heights.
    /// </summary>
    private static readonly string[] TypeKeys =
    [
        "CpFontCaption", "CpFontSmall", "CpFontBody", "CpFontBodyLarge", "CpFontSubtitle", "CpFontTitle",
        "CpFontDisplay", "CpLineCaption", "CpLineSmall", "CpLineBody", "CpLineBodyLarge", "CpLineSubtitle",
        "CpLineTitle", "CpLineDisplay"
    ];

    /// <summary>
    ///     The vendored icons: every glyph that the views are allowed to use.
    /// </summary>
    private static readonly string[] IconKeys =
    [
        "CpIconAdd", "CpIconArrowClockwise", "CpIconArrowUp", "CpIconArrowUpRight", "CpIconArrowUpload",
        "CpIconAttach", "CpIconBraces", "CpIconCheckmark", "CpIconCheckmarkCircle", "CpIconChevronDown",
        "CpIconChevronLeft", "CpIconChevronRight", "CpIconChevronUp", "CpIconCircle", "CpIconCode", "CpIconCopy",
        "CpIconCursor", "CpIconDataBarVertical", "CpIconDelete", "CpIconDismiss", "CpIconDocument",
        "CpIconDocumentAdd", "CpIconDocumentPdf", "CpIconEdit", "CpIconErrorCircle", "CpIconFlow", "CpIconFolder",
        "CpIconFolderAdd", "CpIconFolderOpen", "CpIconFullScreenMaximize", "CpIconHistory", "CpIconImage",
        "CpIconInfo", "CpIconMic", "CpIconMoreHorizontal", "CpIconPanelRight", "CpIconPen",
        "CpIconRectangleLandscape", "CpIconSettings", "CpIconSparkle", "CpIconStop", "CpIconSubtract",
        "CpIconWarning"
    ];

    /// <summary>
    ///     Test that every token of the design resolves in both theme variants with the type it is documented to
    ///     have, and that the colors of a variant differ from the other variant where the design says they do.
    /// </summary>
    [AvaloniaFact]
    public void ThemeResources_Tokens_ResolveInBothVariants()
    {
        // Arrange: the running application, which merges the token dictionary
        Assert.NotNull(Application.Current);

        foreach (var variant in ThemeUiHelpers.Variants)
        {
            // Act / Assert: the brushes resolve as colors, the shared ones too
            foreach (var key in VariantBrushKeys)
            {
                Assert.True(
                    ThemeUiHelpers.Resolve(key, variant) is ISolidColorBrush,
                    $"{key} must resolve to a solid brush in {variant}.");
            }

            foreach (var key in SharedBrushKeys)
            {
                Assert.True(
                    ThemeUiHelpers.Resolve(key, variant) is IBrush,
                    $"{key} must resolve to a brush in {variant}.");
            }

            // Assert: the scales resolve with their types
            foreach (var key in ShadowKeys)
            {
                Assert.True(
                    ThemeUiHelpers.Resolve(key, variant) is BoxShadows,
                    $"{key} must resolve to shadows in {variant}.");
            }

            foreach (var key in SpaceKeys.Concat(TypeKeys))
            {
                Assert.True(
                    ThemeUiHelpers.Resolve(key, variant) is double,
                    $"{key} must resolve to a number in {variant}.");
            }

            foreach (var key in RadiusKeys)
            {
                Assert.True(
                    ThemeUiHelpers.Resolve(key, variant) is CornerRadius,
                    $"{key} must resolve to a corner radius in {variant}.");
            }
        }

        // Assert: the surfaces and the text differ between the variants, and the scales are as documented
        foreach (var key in new[] { "CpSurfaceWindow", "CpSurfacePane", "CpSurfaceStage", "CpTextPrimary", "CpFocusRing" })
        {
            Assert.NotEqual(
                ThemeUiHelpers.ColorOf(key, ThemeVariant.Dark),
                ThemeUiHelpers.ColorOf(key, ThemeVariant.Light));
        }

        Assert.Equal(
            [4.0, 8.0, 12.0, 16.0, 24.0, 32.0, 48.0],
            SpaceKeys.Select(key => (double)ThemeUiHelpers.Resolve(key, ThemeVariant.Dark)!));
        Assert.Equal(
            Color.Parse("#4f46e5"),
            ThemeUiHelpers.ColorOf("CpAccent", ThemeVariant.Light));
    }

    /// <summary>
    ///     Test that every text color has at least 4.5:1 contrast on each surface it is documented for, in both
    ///     theme variants.
    /// </summary>
    [AvaloniaFact]
    public void ThemeResources_Tokens_TextColorsMeetContrastOnTheirSurfaces()
    {
        // Arrange: the surfaces that each text token is used on (the tertiary text and the state colors are not used on
        // the stage, and the tertiary text is not used on overlays)
        var allSurfaces = new[] { "CpSurfaceWindow", "CpSurfacePane", "CpSurfaceStage", "CpSurfaceRaised", "CpSurfaceOverlay" };
        var calmSurfaces = new[] { "CpSurfaceWindow", "CpSurfacePane", "CpSurfaceRaised" };
        var texts = new (string Text, string[] Surfaces)[]
        {
            ("CpTextPrimary", allSurfaces),
            ("CpTextSecondary", allSurfaces),
            ("CpTextTertiary", calmSurfaces),
            ("CpAccentText", allSurfaces),
            ("CpSuccess", calmSurfaces),
            ("CpWarning", calmSurfaces),
            ("CpDanger", calmSurfaces)
        };

        // Act / Assert: compute the contrast of each text on each of its surfaces, in both variants, and check it
        foreach (var variant in ThemeUiHelpers.Variants)
        {
            foreach (var (text, surfaces) in texts)
            {
                foreach (var surface in surfaces)
                {
                    // Act: compute the contrast of the text on the surface
                    var ratio = ThemeUiHelpers.Contrast(
                        ThemeUiHelpers.ColorOf(text, variant),
                        ThemeUiHelpers.ColorOf(surface, variant));

                    // Assert: it is at least 4.5:1
                    Assert.True(ratio >= 4.5, $"{text} on {surface} in {variant} is {ratio:F2}:1, below 4.5:1.");
                }
            }

            // Act / Assert: white on the accent fill, and primary, secondary, and accent text on a selected row
            var accent = ThemeUiHelpers.ColorOf("CpAccent", variant);
            Assert.True(
                ThemeUiHelpers.Contrast(ThemeUiHelpers.ColorOf("CpTextOnAccent", variant), accent) >= 4.5,
                $"White on the accent in {variant} is below 4.5:1.");

            var pane = ThemeUiHelpers.ColorOf("CpSurfacePane", variant);
            var selectedRow = ThemeUiHelpers.Over(ThemeUiHelpers.ColorOf("CpAccentTint", variant), pane);
            foreach (var text in new[] { "CpTextPrimary", "CpTextSecondary", "CpAccentText" })
            {
                var ratio = ThemeUiHelpers.Contrast(ThemeUiHelpers.ColorOf(text, variant), selectedRow);
                Assert.True(ratio >= 4.5, $"{text} on a selected row in {variant} is {ratio:F2}:1, below 4.5:1.");
            }
        }
    }

    /// <summary>
    ///     Test that the colors of things that are not text (the focus ring, the outline of an input, and the
    ///     marks) have at least 3:1 contrast on the pane and on raised surfaces, in both theme variants.
    /// </summary>
    [AvaloniaFact]
    public void ThemeResources_Tokens_NonTextColorsMeetContrast()
    {
        // Arrange: the running application, which merges the token dictionary
        Assert.NotNull(Application.Current);

        // Act / Assert: compute the contrast of each line or shape on each surface, in both variants, and check it
        foreach (var variant in ThemeUiHelpers.Variants)
        {
            foreach (var color in new[] { "CpFocusRing", "CpBorderControl", "CpMark" })
            {
                foreach (var surface in new[] { "CpSurfacePane", "CpSurfaceRaised" })
                {
                    // Act: compute the contrast of the line or shape on the surface
                    var ratio = ThemeUiHelpers.Contrast(
                        ThemeUiHelpers.ColorOf(color, variant),
                        ThemeUiHelpers.ColorOf(surface, variant));

                    // Assert: it is at least 3:1
                    Assert.True(ratio >= 3.0, $"{color} on {surface} in {variant} is {ratio:F2}:1, below 3:1.");
                }
            }
        }
    }

    /// <summary>
    ///     Test that every vendored icon resolves to a geometry that has a shape and lies on its 20 by 20 grid.
    /// </summary>
    [AvaloniaFact]
    public void ThemeResources_Icons_EveryIconResolvesToGeometryOnItsGrid()
    {
        // Arrange: the running application, which merges the icon dictionary
        Assert.NotNull(Application.Current);

        foreach (var key in IconKeys)
        {
            // Act: look the icon up in the application resources
            var geometry = Assert.IsType<StreamGeometry>(ThemeUiHelpers.Resolve(key, ThemeVariant.Light));
            var bounds = geometry.Bounds;

            // Assert: it has a shape, and the shape lies on the 20 by 20 grid of the glyphs
            Assert.True(bounds.Width >= 1 && bounds.Height >= 1, $"{key} has no visible shape.");
            Assert.True(
                bounds.X >= -0.1 && bounds.Y >= -0.1 && bounds.Right <= 20.1 && bounds.Bottom <= 20.1,
                $"{key} lies outside the 20 by 20 grid: {bounds}.");
        }
    }

    /// <summary>
    ///     Test that an icon in a button is drawn 16 by 16 in the foreground color of the button, so an icon follows
    ///     the color of the control that holds it.
    /// </summary>
    [AvaloniaFact]
    public void ThemeResources_Icons_PathIconIsDrawnInTheForegroundOfItsButton()
    {
        // Arrange: a button with a red foreground that holds an icon of class icon, in a window of 100 by 100
        var icon = new PathIcon { Data = (Geometry)ThemeUiHelpers.Resolve("CpIconAdd", ThemeVariant.Light)! };
        icon.Classes.Add("icon");
        var button = new Button
        {
            Content = icon,
            Foreground = Brushes.Red,
            Background = Brushes.White,
            BorderThickness = new Thickness(0),
            Padding = new Thickness(0),
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top
        };
        var window = new Window
        {
            Width = 100,
            Height = 100,
            Background = Brushes.White,
            RequestedThemeVariant = ThemeVariant.Light,
            Content = button
        };

        // Act: show the window, draw it, and read the pixels
        window.Show();
        Dispatcher.UIThread.RunJobs();
        var frame = window.CaptureRenderedFrame();
        Assert.NotNull(frame);
        var (pixels, stride) = ScreenshotSupport.ReadPixels(frame);
        window.Close();

        // Assert: the icon is 16 by 16, and red pixels are drawn inside its box and nowhere else
        Assert.Equal(new Size(16, 16), icon.Bounds.Size);
        var inside = 0;
        var outside = 0;
        for (var y = 0; y < frame.PixelSize.Height; y++)
        {
            for (var x = 0; x < frame.PixelSize.Width; x++)
            {
                var offset = (y * stride) + (x * 4);
                // A red pixel, also an anti-aliased one that is mixed with the white behind it, has much more red than green
                var isRed = pixels[offset] - pixels[offset + 1] > 60;
                if (!isRed)
                {
                    continue;
                }

                if (x < 16 && y < 16)
                {
                    inside++;
                }
                else
                {
                    outside++;
                }
            }
        }

        Assert.True(inside > 10, $"Only {inside} red pixels were drawn by the icon.");
        Assert.Equal(0, outside);
    }

    /// <summary>
    ///     Test that the shade behind code in Markdown text, such as a reply of the copilot, is the fill token of the theme, and
    ///     that it is found again when the theme variant of the window is switched.
    /// </summary>
    [AvaloniaFact]
    public void ThemeResources_FillTokens_CodeInTextShowsTheTokenOfTheVariant()
    {
        // Arrange: a panel with a paragraph that has a code span, in a window that asks for dark
        var panel = new MarkdownBlocksPanel
        {
            Blocks = [new ParagraphBlock([new MarkdownSpan("plain ", MarkdownSpanStyle.Normal), new MarkdownSpan("code", MarkdownSpanStyle.Code)])]
        };
        var window = new Window { Content = panel, RequestedThemeVariant = ThemeVariant.Dark };
        window.Show();
        var text = panel.GetVisualDescendants().OfType<SelectableTextBlock>().Single();
        Assert.NotNull(text.Inlines);
        var runs = text.Inlines.OfType<Run>().ToList();

        // Act: read the backgrounds of the runs, switch the window to light, and read the code run again
        var plain = runs[0].Background;
        var dark = Assert.IsAssignableFrom<ISolidColorBrush>(runs[1].Background).Color;
        window.RequestedThemeVariant = ThemeVariant.Light;
        var light = Assert.IsAssignableFrom<ISolidColorBrush>(runs[1].Background).Color;
        window.Close();

        // Assert: plain text has none, and the code run shows the token of each variant
        Assert.Null(plain);
        Assert.Equal(ThemeUiHelpers.ColorOf("CpFillCode", ThemeVariant.Dark), dark);
        Assert.Equal(ThemeUiHelpers.ColorOf("CpFillCode", ThemeVariant.Light), light);
    }

    /// <summary>
    ///     Test that a control that refers to a token with a dynamic resource shows the color of the variant that
    ///     is requested, and shows the other color after the variant is switched, without being created again.
    /// </summary>
    [AvaloniaFact]
    public void ThemeResources_DynamicResource_FollowsVariantSwitch()
    {
        // Arrange: a border that takes its background from a surface token, in a window that asks for dark
        var border = new Border();
        border.Bind(Border.BackgroundProperty, border.GetResourceObservable("CpSurfacePane"));
        var window = new Window { Content = border, RequestedThemeVariant = ThemeVariant.Dark };
        window.Show();
        var dark = Assert.IsAssignableFrom<ISolidColorBrush>(border.Background).Color;

        // Act: switch the window to the light variant
        window.RequestedThemeVariant = ThemeVariant.Light;
        var light = Assert.IsAssignableFrom<ISolidColorBrush>(border.Background).Color;
        window.Close();

        // Assert: the border showed each variant's pane color
        Assert.Equal(ThemeUiHelpers.ColorOf("CpSurfacePane", ThemeVariant.Dark), dark);
        Assert.Equal(ThemeUiHelpers.ColorOf("CpSurfacePane", ThemeVariant.Light), light);
        Assert.NotEqual(dark, light);
    }
}
