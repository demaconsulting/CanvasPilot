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

using DemaConsulting.CanvasPilot.PreviewSubsystem;
using DemaConsulting.CanvasPilot.StyleSubsystem;

namespace DemaConsulting.CanvasPilot.Tests;

/// <summary>
///     Unit tests for <see cref="StyleChecker" />, <see cref="StyleCheckOptions" />, <see cref="StyleCheckReport" />, and
///     the color arithmetic that they use, with pixel buffers that the tests make: the palette colors, a color that is off
///     the palette, the edge of an anti-aliased shape, gradients, empty and translucent pixels, many colors, and a large
///     picture.
/// </summary>
public sealed class StyleCheckerTests
{
    /// <summary>
    ///     White, the background of the kit of the tests.
    /// </summary>
    private static readonly (byte R, byte G, byte B) White = (255, 255, 255);

    /// <summary>
    ///     The text color of the kit, <c>#0F172A</c>.
    /// </summary>
    private static readonly (byte R, byte G, byte B) Text = (0x0F, 0x17, 0x2A);

    /// <summary>
    ///     The primary color of the kit, <c>#2563EB</c>.
    /// </summary>
    private static readonly (byte R, byte G, byte B) Primary = (0x25, 0x63, 0xEB);

    /// <summary>
    ///     The accent color of the kit, <c>#F59E0B</c>.
    /// </summary>
    private static readonly (byte R, byte G, byte B) Accent = (0xF5, 0x9E, 0x0B);

    /// <summary>
    ///     A red that is not in the palette, <c>#D32F2F</c>.
    /// </summary>
    private static readonly (byte R, byte G, byte B) Red = (0xD3, 0x2F, 0x2F);

    /// <summary>
    ///     The kit that the pictures are checked against: white, a dark text color, a blue, and an amber.
    /// </summary>
    private static readonly StyleKit Kit = StyleKitPlaces.Kit("check-kit", name: "Check Kit") with
    {
        Palette = new Dictionary<string, string>
        {
            ["background"] = "#FFFFFF",
            ["text"] = "#0F172A",
            ["primary"] = "#2563EB",
            ["accent"] = "#F59E0B"
        }
    };

    /// <summary>
    ///     Validates the conversion to CIELAB for colors whose values are known: white, black, a gray, and pure red.
    /// </summary>
    [Fact]
    public void ColorMath_ToLab_KnownColors_AreCloseToTheReferenceValues()
    {
        // Act
        var white = ColorMath.ToLab(255, 255, 255);
        var black = ColorMath.ToLab(0, 0, 0);
        var gray = ColorMath.ToLab(119, 119, 119);
        var red = ColorMath.ToLab(255, 0, 0);

        // Assert: the reference values of sRGB under D65 to a hundredth or so
        Assert.Equal(100, white.L, 2);
        Assert.Equal(0, white.A, 2);
        Assert.Equal(0, white.B, 2);
        Assert.Equal(0, black.L, 6);
        Assert.Equal(50.0, gray.L, 0);
        Assert.Equal(53.24, red.L, 1);
        Assert.Equal(80.09, red.A, 0);
        Assert.Equal(67.20, red.B, 0);
        Assert.Equal(100, ColorMath.DeltaE76(white, black), 2);
        Assert.Equal(0, ColorMath.DeltaE76(red, red), 12);
    }

    /// <summary>
    ///     Validates the distance from a line between two colors: zero on the line, small beside it, and not near for a color
    ///     that is far from it or for a line that is only a point.
    /// </summary>
    [Fact]
    public void ColorMath_BlendDistance_PointOnBesideAndFarFromTheLine_IsZeroSmallAndInfinite()
    {
        // Arrange: the line from white to the primary blue and three colors
        var white = new ColorMath.Rgb(255, 255, 255);
        var blue = new ColorMath.Rgb(0x25, 0x63, 0xEB);
        var middle = (255 + 0x25) / 2.0;

        // Act
        var onLine = ColorMath.BlendDistance(middle, (255 + 0x63) / 2.0, (255 + 0xEB) / 2.0, white, blue);
        var beside = ColorMath.BlendDistance(middle + 4, (255 + 0x63) / 2.0, (255 + 0xEB) / 2.0, white, blue);
        var far = ColorMath.BlendDistance(211, 47, 47, white, blue);
        var point = ColorMath.BlendDistance(1, 2, 3, white, white);

        // Assert
        Assert.Equal(0, onLine, 9);
        Assert.InRange(beside, 0.5, 5);
        Assert.True(double.IsPositiveInfinity(far));
        Assert.True(double.IsPositiveInfinity(point));
    }

    /// <summary>
    ///     Validates that stripes of exact palette colors are all on the palette, with the share of each, and pass.
    /// </summary>
    [Fact]
    public void StyleChecker_Check_StripesOfPaletteColors_AreOnThePaletteAndPass()
    {
        // Arrange: four stripes of the palette colors with different widths
        var stripes = new[] { White, Text, Primary, Accent };
        var widths = new[] { 40, 30, 20, 10 };
        var pixels = Picture(100, 30, (x, _) => stripes[StripeIndex(x, widths)]);

        // Act
        var report = StyleChecker.Check(pixels, 100, 30, Kit);

        // Assert: each role has its share, nothing is off the palette or blended, the shares add up, and it passes
        Assert.True(report.Passed);
        Assert.Equal(["background", "text", "primary", "accent"], report.OnPalette.Select(u => u.Role));
        Assert.Equal(["#FFFFFF", "#0F172A", "#2563EB", "#F59E0B"], report.OnPalette.Select(u => u.Hex));
        Assert.Equal([0.4, 0.3, 0.2, 0.1], report.OnPalette.Select(u => u.Share));
        Assert.Equal(3000, report.ConsideredPixels);
        Assert.Equal(0, report.TransparentPixels);
        Assert.Equal(0, report.BlendShare);
        Assert.Empty(report.OffPalette);
        Assert.Equal(0, report.OffPaletteShare);
        Assert.Equal(("check-kit", "Check Kit", 100, 30), (report.KitId, report.KitName, report.Width, report.Height));
    }

    /// <summary>
    ///     Validates that a block of a color that is not in the palette is reported with its color, its share, its nearest
    ///     palette color and the difference, and that the picture fails.
    /// </summary>
    [Fact]
    public void StyleChecker_Check_TenPercentRedBlock_ReportsColorShareAndNearestRole()
    {
        // Arrange: a white picture with a block of red that is a tenth of it
        var pixels = Picture(100, 100, (x, y) => x < 10 ? Red : White);

        // Act
        var report = StyleChecker.Check(pixels, 100, 100, Kit);

        // Assert: the color is exact (it fills its bucket), the share is a tenth, and the nearest color is one of the palette
        var off = Assert.Single(report.OffPalette);
        Assert.Equal("#D32F2F", off.Hex);
        Assert.Equal(0.10, off.Share, 3);
        var expected = Kit.Palette
            .Select(p => (Role: p.Key, Hex: p.Value, Distance: ColorMath.DeltaE76(ColorMath.ToLab(Red.R, Red.G, Red.B), LabOf(p.Value))))
            .MinBy(p => p.Distance);
        Assert.Equal((expected.Role, expected.Hex), (off.NearestRole, off.NearestHex));
        Assert.Equal(expected.Distance, off.DeltaE, 9);
        Assert.True(off.DeltaE > StyleCheckOptions.DefaultTolerance);
        Assert.Equal(0.10, report.OffPaletteShare, 3);
        Assert.Equal(0, report.OtherOffPaletteShare, 9);
        Assert.False(report.Passed);
        Assert.Equal(0.90, Assert.Single(report.OnPalette).Share, 3);
    }

    /// <summary>
    ///     Validates the tolerance: a color within it counts as the palette color, a color just outside it is off the
    ///     palette, and a larger tolerance accepts it.
    /// </summary>
    [Fact]
    public void StyleChecker_Check_ColorAroundTheTolerance_IsOnThePaletteInsideAndOffJustOutside()
    {
        // Arrange: a kit with only white, so that no line between two palette colors can make a blend, and the colors that
        // are the largest and the smallest steps from white on the blue channel with a color difference of at most 10 and
        // of more than 10, found with the same arithmetic as the check
        var whiteKit = Kit with { Palette = new Dictionary<string, string> { ["background"] = "#FFFFFF" } };
        var reference = ColorMath.ToLab(255, 255, 255);
        byte inside = 255;
        byte outside = 255;
        for (var blue = 254; blue > 0; blue--)
        {
            if (ColorMath.DeltaE76(reference, ColorMath.ToLab(255, 255, blue)) > StyleCheckOptions.DefaultTolerance)
            {
                outside = (byte)blue;
                inside = (byte)(blue + 1);
                break;
            }
        }

        // Act: check pictures that are only one of the two colors, and the outside one with a larger tolerance
        var near = StyleChecker.Check(Picture(10, 10, (_, _) => (255, 255, inside)), 10, 10, whiteKit);
        var far = StyleChecker.Check(Picture(10, 10, (_, _) => (255, 255, outside)), 10, 10, whiteKit);
        var lenient = StyleChecker.Check(Picture(10, 10, (_, _) => (255, 255, outside)), 10, 10, whiteKit, new StyleCheckOptions { Tolerance = 20 });

        // Assert: inside is the white and passes, just outside is off the palette and fails, and 20 accepts it
        Assert.True(near.Passed);
        Assert.Equal("background", Assert.Single(near.OnPalette).Role);
        Assert.False(far.Passed);
        Assert.Equal(1.0, far.OffPaletteShare, 9);
        Assert.InRange(Assert.Single(far.OffPalette).DeltaE, StyleCheckOptions.DefaultTolerance, StyleCheckOptions.DefaultTolerance + 1.5);
        Assert.True(lenient.Passed);
    }

    /// <summary>
    ///     Validates the blend rule: a column of the color half way between two palette colors where a shape meets its
    ///     background is blended and not off the palette, and so is a short ramp of such colors.
    /// </summary>
    [Fact]
    public void StyleChecker_Check_AntiAliasedEdgeBetweenTwoPaletteColors_IsBlendedNotOffPalette()
    {
        // Arrange: white on the left, the blue on the right, and a column between them of the color half way, and a
        // second picture with an edge that is a ramp of seven steps
        var half = Mix(White, Primary, 0.5);
        var sharp = Picture(100, 20, (x, _) => SharpEdge(x, half));
        var ramp = Picture(100, 20, (x, _) => RampEdge(x));

        // Act
        var sharpReport = StyleChecker.Check(sharp, 100, 20, Kit);
        var rampReport = StyleChecker.Check(ramp, 100, 20, Kit);

        // Assert: the one column is a hundredth blended, the ramp is eight hundredths blended, nothing is off the palette
        Assert.True(sharpReport.Passed);
        Assert.Equal(0.01, sharpReport.BlendShare, 6);
        Assert.Equal(0, sharpReport.OffPaletteShare);
        Assert.Equal(0.49, sharpReport.OnPalette.First(u => u.Role == "background").Share, 6);
        Assert.True(rampReport.Passed);
        Assert.Equal(0.08, rampReport.BlendShare, 6);
        Assert.Equal(0, rampReport.OffPaletteShare);
    }

    /// <summary>
    ///     Validates that a gradient from a palette color to a color that is not in the palette fails, with most of the
    ///     picture off the palette.
    /// </summary>
    [Fact]
    public void StyleChecker_Check_GradientToAColorOffThePalette_Fails()
    {
        // Arrange: a ramp from white to the red, one column a step
        var pixels = Picture(256, 4, (x, _) => Mix(White, Red, x / 255.0));

        // Act
        var report = StyleChecker.Check(pixels, 256, 4, Kit);

        // Assert: most of the picture is off the palette, and the picture fails
        Assert.False(report.Passed);
        Assert.True(report.OffPaletteShare > 0.5, $"{report.OffPaletteShare}");
        Assert.NotEmpty(report.OffPalette);
    }

    /// <summary>
    ///     Validates that pixels that are nearly empty are not counted (alpha below 16) and are counted as transparent, and
    ///     that a pixel with alpha 16 is counted.
    /// </summary>
    [Fact]
    public void StyleChecker_Check_TransparentPixels_AreIgnoredAndCounted()
    {
        // Arrange: the first quarter is red with alpha 0, the second is red with alpha 15, the third is white with alpha 16,
        // the last is the blue
        var pixels = new byte[100 * 4];
        for (var i = 0; i < 100; i++)
        {
            var (r, g, b, a) = TransparencyPixel(i);
            pixels[(i * 4) + 0] = r;
            pixels[(i * 4) + 1] = g;
            pixels[(i * 4) + 2] = b;
            pixels[(i * 4) + 3] = a;
        }

        // Act
        var report = StyleChecker.Check(pixels, 100, 1, Kit);

        // Assert: fifty pixels were not counted, and the rest are on the palette (a pixel of alpha 16 over white is white)
        Assert.Equal(50, report.TransparentPixels);
        Assert.Equal(50, report.ConsideredPixels);
        Assert.True(report.Passed);
        Assert.Equal(0, report.OffPaletteShare);
        Assert.Equal(0.5, report.OnPalette.Single(u => u.Role == "background").Share, 9);
    }

    /// <summary>
    ///     Validates that a pixel that is partly see-through is flattened over the background of the kit before it is
    ///     measured: a faint red looks white, and the blue at half alpha looks like the blend with white.
    /// </summary>
    [Fact]
    public void StyleChecker_Check_TranslucentPixels_AreFlattenedOverTheBackground()
    {
        // Arrange: half the picture is red at a faint alpha, half is the blue at half alpha
        var pixels = new byte[100 * 4];
        for (var i = 0; i < 100; i++)
        {
            var faint = i < 50;
            pixels[(i * 4) + 0] = faint ? Red.R : Primary.R;
            pixels[(i * 4) + 1] = faint ? Red.G : Primary.G;
            pixels[(i * 4) + 2] = faint ? Red.B : Primary.B;
            pixels[(i * 4) + 3] = faint ? (byte)20 : (byte)128;
        }

        // Act
        var report = StyleChecker.Check(pixels, 100, 1, Kit);

        // Assert: the faint red is the background, and the half blue is a blend of the background and the blue
        Assert.True(report.Passed);
        Assert.Equal(0.5, report.OnPalette.Single(u => u.Role == "background").Share, 9);
        Assert.Equal(0.5, report.BlendShare, 9);
        Assert.Equal(0, report.OffPaletteShare);
    }

    /// <summary>
    ///     Validates that an off-palette color of less than a hundredth of the picture is counted in the share but not
    ///     listed, so the picture passes and the share is in the other colors.
    /// </summary>
    [Fact]
    public void StyleChecker_Check_TinyOffPaletteShare_IsCountedNotListed()
    {
        // Arrange: a white picture with one column of red in 200 (half a hundredth)
        var pixels = Picture(200, 10, (x, _) => x == 0 ? Red : White);

        // Act
        var report = StyleChecker.Check(pixels, 200, 10, Kit);

        // Assert: nothing is listed, the share is counted as other colors, and the picture passes
        Assert.Empty(report.OffPalette);
        Assert.Equal(0.005, report.OffPaletteShare, 9);
        Assert.Equal(0.005, report.OtherOffPaletteShare, 9);
        Assert.True(report.Passed);
        Assert.DoesNotContain("Off the palette", report.ToText("a.svg").Split('\n')[2], StringComparison.Ordinal);
        Assert.Contains("other colors 0.5%", report.ToText("a.svg"), StringComparison.Ordinal);
    }

    /// <summary>
    ///     Validates that at most eight colors are listed, the largest first, and that the others are summed in the other
    ///     share, so that everything adds up to the whole picture.
    /// </summary>
    [Fact]
    public void StyleChecker_Check_TwelveOffPaletteColors_ListCappedAtEightWithOtherShare()
    {
        // Arrange: twelve colors that are far from each other and from the palette, in stripes of 12 to 1 columns wide
        (byte R, byte G, byte B)[] colors =
        [
            (255, 0, 0), (0, 255, 0), (255, 0, 255), (0, 255, 255), (255, 255, 0), (128, 0, 0),
            (0, 128, 0), (0, 0, 128), (128, 128, 0), (128, 0, 128), (0, 128, 128), (128, 0, 255)
        ];
        var widths = Enumerable.Range(0, 12).Select(i => 12 - i).ToArray();
        var total = widths.Sum();
        var pixels = Picture(total, 10, (x, _) => colors[StripeIndex(x, widths)]);

        // Act
        var report = StyleChecker.Check(pixels, total, 10, Kit);

        // Assert: eight listed from the widest, the other four summed, and the whole is the picture
        Assert.Equal(8, report.OffPalette.Count);
        Assert.Equal(["#FF0000", "#00FF00", "#FF00FF", "#00FFFF", "#FFFF00", "#800000", "#008000", "#000080"], report.OffPalette.Select(c => c.Hex));
        Assert.Equal(Enumerable.Range(0, 8).Select(i => (12 - i) / (double)total), report.OffPalette.Select(c => c.Share));
        Assert.Equal(Enumerable.Range(8, 4).Sum(i => (12 - i) / (double)total), report.OtherOffPaletteShare, 9);
        Assert.Equal(1.0, report.OffPaletteShare, 9);
        Assert.False(report.Passed);
    }

    /// <summary>
    ///     Validates that the on-palette, blended, and off-palette shares add up to one for a picture that has all three.
    /// </summary>
    [Fact]
    public void StyleChecker_Check_MixedPicture_SharesAddUpToOne()
    {
        // Arrange: white, a blue, an edge between them, and some red
        var pixels = Picture(120, 12, (x, _) => MixedColumn(x));

        // Act
        var report = StyleChecker.Check(pixels, 120, 12, Kit);

        // Assert: the three kinds of share make the whole picture
        Assert.Equal(1.0, report.OnPaletteShare + report.BlendShare + report.OffPaletteShare, 9);
        Assert.Equal(report.OffPalette.Sum(c => c.Share) + report.OtherOffPaletteShare, report.OffPaletteShare, 9);
    }

    /// <summary>
    ///     Validates that the same pixels give the same report again, and that the same pixels in another order give the
    ///     same report, so that the result does not depend on how the picture was made.
    /// </summary>
    [Fact]
    public void StyleChecker_Check_SameOrShuffledPixels_GiveEqualReports()
    {
        // Arrange: a picture of several kinds of pixel and a copy with the pixels in another order
        var colors = new (byte R, byte G, byte B)[] { White, Primary, Red, Mix(White, Primary, 0.3), (0x40, 0x80, 0x20), Accent };
        var original = Picture(60, 40, (x, y) => colors[((x * 7) + (y * 3)) % colors.Length]);
        var order = Enumerable.Range(0, 60 * 40).OrderBy(i => (i * 2654435761L) % 4294967296L).ToArray();
        var shuffled = new byte[original.Length];
        for (var i = 0; i < order.Length; i++)
        {
            Array.Copy(original, order[i] * 4, shuffled, i * 4, 4);
        }

        // Act
        var first = StyleChecker.Check(original, 60, 40, Kit);
        var again = StyleChecker.Check(original, 60, 40, Kit);
        var other = StyleChecker.Check(shuffled, 60, 40, Kit);

        // Assert: the texts of the reports are equal, which says every value that they show is
        Assert.Equal(first.ToText("a.svg"), again.ToText("a.svg"));
        Assert.Equal(first.ToText("a.svg"), other.ToText("a.svg"));
        Assert.Equal(first.ToNotes().Select(n => n.DisplayText), other.ToNotes().Select(n => n.DisplayText));
        Assert.Equal((first.OffPaletteShare, first.BlendShare, first.Passed), (other.OffPaletteShare, other.BlendShare, other.Passed));
    }

    /// <summary>
    ///     Validates the pictures with nothing in them: none at all, and one pixel on and off the palette.
    /// </summary>
    [Fact]
    public void StyleChecker_Check_EmptyAndOnePixelPictures_AreHandled()
    {
        // Act
        var empty = StyleChecker.Check([], 0, 0, Kit);
        var onPalette = StyleChecker.Check([0x25, 0x63, 0xEB, 255], 1, 1, Kit);
        var offPalette = StyleChecker.Check([0xD3, 0x2F, 0x2F, 255], 1, 1, Kit);
        var invisible = StyleChecker.Check([1, 2, 3, 0], 1, 1, Kit);

        // Assert
        Assert.True(empty.Passed);
        Assert.Equal((0L, 0L), (empty.ConsideredPixels, empty.TransparentPixels));
        Assert.Empty(empty.OnPalette);
        Assert.True(onPalette.Passed);
        Assert.Equal(1.0, Assert.Single(onPalette.OnPalette).Share, 12);
        Assert.False(offPalette.Passed);
        Assert.Equal(1.0, offPalette.OffPaletteShare, 12);
        Assert.Equal("#D32F2F", Assert.Single(offPalette.OffPalette).Hex);
        Assert.True(invisible.Passed);
        Assert.Equal(1L, invisible.TransparentPixels);
    }

    /// <summary>
    ///     Validates that a rendered picture is checked through its pixels, and that the notes and the text of the report
    ///     for a failing picture have the codes, severities, and sentences of the design.
    /// </summary>
    [Fact]
    public void StyleCheckReport_ToNotesAndToText_FailingPicture_HaveTheShapeOfTheDesign()
    {
        // Arrange: a rendered picture that is mostly white, with an edge, and with some red
        var pixels = Picture(100, 100, (x, _) => LogoColumn(x));
        var image = new RenderedImage("logo.svg", "svg", 100, 100, pixels);

        // Act
        var report = StyleChecker.Check(image, Kit);
        var notes = report.ToNotes();
        var text = report.ToText("logo.svg");
        var lines = text.TrimEnd('\n').Split('\n');

        // Assert: the notes start with the summary, then the colors used, the edge, and the red as a warning
        Assert.Equal(["style-summary", "style-ok", "style-blend", "style-off-palette"], notes.Select(n => n.Code));
        Assert.Equal([RenderDiagnosticSeverity.Info, RenderDiagnosticSeverity.Info, RenderDiagnosticSeverity.Info, RenderDiagnosticSeverity.Warning], notes.Select(n => n.Severity));
        Assert.All(notes, n => Assert.StartsWith(StyleCheckReport.NoteGroup + "-", n.Code, StringComparison.Ordinal));
        Assert.Equal("Result: FAILED, 10.0% of the pixels are off the palette (limit 5%).", notes[0].Message);
        Assert.StartsWith("On the palette (89.0%): #FFFFFF background 79.0%; #2563EB primary 10.0%.", notes[1].Message, StringComparison.Ordinal);
        Assert.Equal("Blended edges: 1.0%.", notes[2].Message);
        Assert.StartsWith("#D32F2F 10.0% (nearest: ", notes[3].Message, StringComparison.Ordinal);

        // The text has the four lines of the design
        Assert.Equal(4, lines.Length);
        Assert.Equal("Style check of logo.svg against \"Check Kit\" (check-kit): 100 x 100 px, 10,000 pixels counted (0 transparent).", lines[0]);
        Assert.Equal(notes[0].Message, lines[1]);
        Assert.Equal(notes[1].Message + " " + notes[2].Message, lines[2]);
        Assert.StartsWith("Off the palette: #D32F2F 10.0% (nearest: ", lines[3], StringComparison.Ordinal);
        Assert.EndsWith(". Use the palette colors above.", lines[3], StringComparison.Ordinal);
    }

    /// <summary>
    ///     Validates the text for a picture that passes and for no file name: no line about colors off the palette, and "the
    ///     picture" in place of the file name.
    /// </summary>
    [Fact]
    public void StyleCheckReport_ToText_PassingPictureWithoutName_HasNoOffPaletteLine()
    {
        // Arrange
        var report = StyleChecker.Check(Picture(10, 10, (_, _) => White), 10, 10, Kit);

        // Act
        var text = report.ToText(null);
        var notes = report.ToNotes();

        // Assert
        Assert.StartsWith("Style check of the picture against \"Check Kit\" (check-kit): 10 x 10 px, 100 pixels counted (0 transparent).\n", text, StringComparison.Ordinal);
        Assert.Contains("Result: PASSED, 0.0% of the pixels are off the palette (limit 5%).", text, StringComparison.Ordinal);
        Assert.DoesNotContain("Off the palette", text, StringComparison.Ordinal);
        Assert.Equal(["style-summary", "style-ok"], notes.Select(n => n.Code));
    }

    /// <summary>
    ///     Validates that a large picture, 2048 by 2000 pixels, is counted pixel by pixel: every pixel is considered and the
    ///     share of the pixels off the palette is exact.
    /// </summary>
    [Fact]
    public void StyleChecker_Check_LargePicture_CountsEveryPixelAndTheOffPaletteShare()
    {
        // Arrange: a picture of 4.1 million pixels in vertical stripes of the palette colors and a color off it
        const int width = 2048;
        const int height = 2000;
        var stripe = new[] { White, Text, Primary, Accent, Red };
        var row = new byte[width * 4];
        for (var x = 0; x < width; x++)
        {
            var (r, g, b) = stripe[(x / 64) % stripe.Length];
            row[x * 4] = r;
            row[(x * 4) + 1] = g;
            row[(x * 4) + 2] = b;
            row[(x * 4) + 3] = 255;
        }

        var pixels = new byte[width * height * 4];
        for (var y = 0; y < height; y++)
        {
            row.CopyTo(pixels, y * row.Length);
        }

        // Act
        var report = StyleChecker.Check(pixels, width, height, Kit);

        // Assert: every pixel was counted and the share of the columns of the color off the palette is exact
        Assert.Equal((long)width * height, report.ConsideredPixels);
        var redColumns = Enumerable.Range(0, width).Count(x => (x / 64) % stripe.Length == 4);
        Assert.Equal((double)redColumns / width, report.OffPaletteShare, 6);
    }

    /// <summary>
    ///     Validates the defaults of the options and that a limit that is not in its range is refused.
    /// </summary>
    [Fact]
    public void StyleCheckOptions_DefaultsAndValidate_AreTheOnesOfTheDesign()
    {
        // Arrange / Act
        var defaults = new StyleCheckOptions();

        // Assert: the design values, and every limit is checked
        Assert.Equal((10.0, 8.0, 5.0, 0.01, 0.05), (defaults.Tolerance, defaults.ClusterDelta, defaults.BlendTolerance, defaults.MinReportShare, defaults.MaxOffPaletteShare));
        Assert.Equal(8, StyleCheckOptions.MaxListed);
        defaults.Validate();
        Assert.Throws<ArgumentOutOfRangeException>(() => new StyleCheckOptions { Tolerance = -1 }.Validate());
        Assert.Throws<ArgumentOutOfRangeException>(() => new StyleCheckOptions { ClusterDelta = 101 }.Validate());
        Assert.Throws<ArgumentOutOfRangeException>(() => new StyleCheckOptions { BlendTolerance = double.NaN }.Validate());
        Assert.Throws<ArgumentOutOfRangeException>(() => new StyleCheckOptions { MinReportShare = 1.5 }.Validate());
        Assert.Throws<ArgumentOutOfRangeException>(() => new StyleCheckOptions { MaxOffPaletteShare = double.PositiveInfinity }.Validate());
    }

    /// <summary>
    ///     Validates that a missing picture or kit, a size that does not fit the pixels, a limit out of range, and a kit with
    ///     no color are refused.
    /// </summary>
    [Fact]
    public void StyleChecker_Check_InvalidArguments_Throw()
    {
        // Arrange
        var noColors = Kit with { Palette = new Dictionary<string, string> { ["background"] = "white" } };

        // Act / Assert
        Assert.Throws<ArgumentNullException>(() => StyleChecker.Check((RenderedImage)null!, Kit));
        Assert.Throws<ArgumentNullException>(() => StyleChecker.Check(new byte[4], 1, 1, null!));
        Assert.Throws<ArgumentOutOfRangeException>(() => StyleChecker.Check(new byte[4], -1, 1, Kit));
        Assert.Throws<ArgumentOutOfRangeException>(() => StyleChecker.Check(new byte[4], 1, -1, Kit));
        Assert.Throws<ArgumentException>(() => StyleChecker.Check(new byte[7], 1, 2, Kit));
        Assert.Throws<ArgumentOutOfRangeException>(() => StyleChecker.Check(new byte[4], 1, 1, Kit, new StyleCheckOptions { Tolerance = 500 }));
        Assert.Throws<ArgumentException>(() => StyleChecker.Check(new byte[4], 1, 1, noColors));
    }

    /// <summary>
    ///     Gives the color of a column of a picture of white on the left and blue on the right with one column of the color
    ///     half way between.
    /// </summary>
    /// <param name="x">The column.</param>
    /// <param name="half">The color half way.</param>
    /// <returns>The color.</returns>
    private static (byte R, byte G, byte B) SharpEdge(int x, (byte R, byte G, byte B) half)
    {
        if (x < 49)
        {
            return White;
        }

        return x == 49 ? half : Primary;
    }

    /// <summary>
    ///     Gives the color of a column of a picture of white on the left and blue on the right with a ramp of eight
    ///     columns between.
    /// </summary>
    /// <param name="x">The column.</param>
    /// <returns>The color.</returns>
    private static (byte R, byte G, byte B) RampEdge(int x)
    {
        if (x < 46)
        {
            return White;
        }

        return x > 53 ? Primary : Mix(White, Primary, (x - 45) / 9.0);
    }

    /// <summary>
    ///     Gives the pixel of the picture of the test of the empty pixels.
    /// </summary>
    /// <param name="index">The position of the pixel, 0 to 99.</param>
    /// <returns>The channels and the alpha.</returns>
    private static (byte R, byte G, byte B, byte A) TransparencyPixel(int index)
    {
        if (index < 25)
        {
            return (Red.R, Red.G, Red.B, 0);
        }

        if (index < 50)
        {
            return (Red.R, Red.G, Red.B, 15);
        }

        if (index < 75)
        {
            return (White.R, White.G, White.B, 16);
        }

        return (Primary.R, Primary.G, Primary.B, 255);
    }

    /// <summary>
    ///     Gives the color of a column of the picture that has palette colors, an edge, and red.
    /// </summary>
    /// <param name="x">The column.</param>
    /// <returns>The color.</returns>
    private static (byte R, byte G, byte B) MixedColumn(int x)
    {
        if (x < 50)
        {
            return White;
        }

        if (x == 50)
        {
            return Mix(White, Primary, 0.5);
        }

        return x < 100 ? Primary : Red;
    }

    /// <summary>
    ///     Gives the color of a column of the picture of the test of the notes: white with an edge, red, and blue.
    /// </summary>
    /// <param name="x">The column.</param>
    /// <returns>The color.</returns>
    private static (byte R, byte G, byte B) LogoColumn(int x)
    {
        if (x < 80)
        {
            return x == 40 ? Mix(White, Primary, 0.5) : White;
        }

        return x < 90 ? Red : Primary;
    }

    /// <summary>
    ///     Makes the pixels of a picture from a function of the position.
    /// </summary>
    /// <param name="width">The width.</param>
    /// <param name="height">The height.</param>
    /// <param name="color">The opaque color at a column and a row.</param>
    /// <returns>The pixels, four bytes each.</returns>
    private static byte[] Picture(int width, int height, Func<int, int, (byte R, byte G, byte B)> color)
    {
        var pixels = new byte[width * height * 4];
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                var (r, g, b) = color(x, y);
                var offset = ((y * width) + x) * 4;
                pixels[offset] = r;
                pixels[offset + 1] = g;
                pixels[offset + 2] = b;
                pixels[offset + 3] = 255;
            }
        }

        return pixels;
    }

    /// <summary>
    ///     Finds the stripe that a column is in, for stripes of the given widths one after the other.
    /// </summary>
    /// <param name="column">The column.</param>
    /// <param name="widths">The widths of the stripes.</param>
    /// <returns>The index of the stripe.</returns>
    private static int StripeIndex(int column, int[] widths)
    {
        var start = 0;
        for (var i = 0; i < widths.Length; i++)
        {
            start += widths[i];
            if (column < start)
            {
                return i;
            }
        }

        return widths.Length - 1;
    }

    /// <summary>
    ///     Mixes two colors in sRGB, as an anti-aliased edge is made.
    /// </summary>
    /// <param name="from">The color at 0.</param>
    /// <param name="to">The color at 1.</param>
    /// <param name="amount">How much of <paramref name="to" />, 0 to 1.</param>
    /// <returns>The mixed color, rounded to channels.</returns>
    private static (byte R, byte G, byte B) Mix((byte R, byte G, byte B) from, (byte R, byte G, byte B) to, double amount) =>
        ((byte)Math.Round(from.R + ((to.R - from.R) * amount)), (byte)Math.Round(from.G + ((to.G - from.G) * amount)), (byte)Math.Round(from.B + ((to.B - from.B) * amount)));

    /// <summary>
    ///     Converts a color of the form <c>#RRGGBB</c> to CIELAB.
    /// </summary>
    /// <param name="hex">The color.</param>
    /// <returns>The color in CIELAB.</returns>
    private static ColorMath.Lab LabOf(string hex)
    {
        Assert.True(StyleColors.TryParse(hex, out var r, out var g, out var b));
        return ColorMath.ToLab(r, g, b);
    }
}
