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

using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.CanvasPilot.AnnotationSubsystem;
using DemaConsulting.CanvasPilot.PayloadSubsystem;
using DemaConsulting.CanvasPilot.PreviewSubsystem;

namespace DemaConsulting.CanvasPilot.Tests;

/// <summary>
///     Unit tests for <see cref="MarkImageComposer" />, which draws with the real CanvasNet viewer and the real PNG codec.
/// </summary>
public sealed class MarkImageComposerTests
{
    /// <summary>
    ///     The value of the red, green, and blue bytes of the flat pictures of these tests.
    /// </summary>
    private const byte Fill = 235;

    /// <summary>
    ///     The color of the flat pictures of these tests.
    /// </summary>
    private static readonly Rgba32 Background = new(Fill, Fill, Fill, 255);

    /// <summary>
    ///     The white of the lines under the marks and of the badge edges and numbers.
    /// </summary>
    private static readonly Rgba32 White = new(255, 255, 255, 255);

    /// <summary>
    ///     The composer under test, over the real render service.
    /// </summary>
    private readonly MarkImageComposer _composer = new(new ImageRenderService());

    /// <summary>
    ///     Draws marks on a flat picture and decodes the annotated picture.
    /// </summary>
    /// <param name="picture">The picture.</param>
    /// <param name="marks">The marks.</param>
    /// <returns>The images and the decoded annotated picture, which the caller disposes.</returns>
    private (MarkedPictureImages Images, Surface Annotated) Draw(RenderedImage picture, IReadOnlyList<Mark> marks)
    {
        var images = _composer.Compose(picture, marks);
        return (images, PayloadTestSupport.Decode(images.AnnotatedPng));
    }

    /// <summary>
    ///     Test that the annotated picture has the size of the picture that was marked and is a PNG file.
    /// </summary>
    [Theory]
    [InlineData(200, 100)]
    [InlineData(97, 53)]
    [InlineData(1000, 40)]
    public void MarkImageComposer_Compose_Marks_AnnotatedPictureHasSizeOfPicture(int width, int height)
    {
        // Arrange: a flat picture of an odd or ordinary size with a rectangle on it
        var picture = AnnotationTestPictures.Picture(width: width, height: height, fill: Fill);
        var mark = new MarkRegistry().Add(picture, MarkKind.Rectangle, [new RenderPoint(5, 5), new RenderPoint(width - 5, height - 5)]);

        // Act: draw the mark
        var (images, annotated) = Draw(picture, [mark]);
        using var _ = annotated;

        // Assert: a PNG file of the same size
        Assert.Equal([0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A], images.AnnotatedPng.ToArray()[..8]);
        Assert.Equal(width, annotated.Width);
        Assert.Equal(height, annotated.Height);
    }

    /// <summary>
    ///     Test that the lines of a rectangle are drawn in the color of its number, with a white line under them, and that
    ///     the pixels inside and outside the rectangle are the pixels of the picture.
    /// </summary>
    [Fact]
    public void MarkImageComposer_Compose_Rectangle_DrawsColoredLinesOverWhiteAndLeavesOtherPixelsAsShown()
    {
        // Arrange: a rectangle from (50, 40) to (150, 80) on a flat picture of 200 by 100 pixels
        var picture = AnnotationTestPictures.Picture(fill: Fill);
        var mark = new MarkRegistry().Add(picture, MarkKind.Rectangle, [new RenderPoint(50, 40), new RenderPoint(150, 80)]);
        var color = PayloadTestSupport.ColorOf(MarkImageComposer.ColorOf(1));

        // Act: draw the mark
        var (_, annotated) = Draw(picture, [mark]);
        using var _ = annotated;

        // Assert: the top and bottom lines have the color of mark one, the pixels next to them are the white line, and the
        // inside, the outside, and the corners of the picture are as they were shown
        Assert.Equal(color, annotated[100, 40]);
        Assert.Equal(color, annotated[100, 80]);
        Assert.Equal(color, annotated[150, 60]);
        Assert.Equal(White, annotated[100, 37]);
        Assert.Equal(White, annotated[100, 42]);
        Assert.Equal(Background, annotated[100, 60]);
        Assert.Equal(Background, annotated[190, 95]);
        Assert.Equal(Background, annotated[0, 0]);
        Assert.Equal(Background, annotated[199, 99]);
    }

    /// <summary>
    ///     Test that the badge with the number is drawn at the top-left corner of the mark: a circle in the color of the mark
    ///     with a white edge and a white number.
    /// </summary>
    [Fact]
    public void MarkImageComposer_Compose_Mark_DrawsNumberedBadgeAtTopLeftOfMark()
    {
        // Arrange: a rectangle whose top-left corner is at (50, 40), so that the badge is centered there
        var picture = AnnotationTestPictures.Picture(fill: Fill);
        var mark = new MarkRegistry().Add(picture, MarkKind.Rectangle, [new RenderPoint(50, 40), new RenderPoint(150, 80)]);
        var color = PayloadTestSupport.ColorOf(MarkImageComposer.ColorOf(1));
        var radius = (int)MarkImageComposer.BadgeRadius(picture.Width, picture.Height);

        // Act: draw the mark
        var (_, annotated) = Draw(picture, [mark]);
        using var _ = annotated;

        // Assert: the badge is filled with the color left of the number, has a white edge, and the number is white
        Assert.Equal(color, annotated[50 - radius + 3, 40]);
        Assert.Equal(White, annotated[50 - radius, 40]);
        var whitePixelsOfNumber = 0;
        for (var y = 33; y <= 47; y++)
        {
            for (var x = 46; x <= 54; x++)
            {
                if (annotated[x, y] == White)
                {
                    whitePixelsOfNumber++;
                }
            }
        }

        Assert.True(whitePixelsOfNumber >= 6, $"The number was not drawn: {whitePixelsOfNumber} white pixels.");
        Assert.Equal(Background, annotated[50 - radius - 6, 40 - radius - 6]);
    }

    /// <summary>
    ///     Test that an ellipse, an arrow, and a freehand mark are drawn where their geometry says, in the color of their
    ///     number.
    /// </summary>
    [Fact]
    public void MarkImageComposer_Compose_EllipseArrowAndFreehand_AreDrawnWhereTheirGeometrySays()
    {
        // Arrange: three marks on a flat picture: an ellipse, an arrow to the upper right, and a zigzag
        var picture = AnnotationTestPictures.Picture(width: 300, height: 200, fill: Fill);
        var registry = new MarkRegistry();
        var ellipse = registry.Add(picture, MarkKind.Ellipse, [new RenderPoint(50, 20), new RenderPoint(150, 80)]);
        var arrow = registry.Add(picture, MarkKind.Arrow, [new RenderPoint(180, 180), new RenderPoint(280, 120)]);
        var freehand = registry.Add(picture, MarkKind.Freehand, [new RenderPoint(20, 100), new RenderPoint(60, 150), new RenderPoint(100, 110), new RenderPoint(140, 160)]);

        // Act: draw the marks
        var (_, annotated) = Draw(picture, registry.Marks);
        using var _ = annotated;

        // Assert: the left end of the ellipse, the middle of the shaft of the arrow, and the middle of the first segment of
        // the stroke have the colors of marks one, two, and three; the center of the ellipse is as shown
        Assert.Equal(PayloadTestSupport.ColorOf(MarkImageComposer.ColorOf(ellipse.Number)), annotated[50, 50]);
        Assert.Equal(PayloadTestSupport.ColorOf(MarkImageComposer.ColorOf(arrow.Number)), annotated[230, 150]);
        Assert.Equal(PayloadTestSupport.ColorOf(MarkImageComposer.ColorOf(freehand.Number)), annotated[40, 125]);
        Assert.Equal(Background, annotated[100, 50]);
        Assert.NotEqual(MarkImageComposer.ColorOf(1), MarkImageComposer.ColorOf(2));
    }

    /// <summary>
    ///     Test that an arrow has a head: the pixels of the color near the head are many more than those of the shaft alone.
    /// </summary>
    [Fact]
    public void MarkImageComposer_Compose_Arrow_HasAHeadAtItsPointedEnd()
    {
        // Arrange: an arrow to the upper right and the same arrow's tail end for comparison
        var picture = AnnotationTestPictures.Picture(width: 300, height: 200, fill: Fill);
        var mark = new MarkRegistry().Add(picture, MarkKind.Arrow, [new RenderPoint(20, 180), new RenderPoint(200, 60)]);
        var color = PayloadTestSupport.ColorOf(MarkImageComposer.ColorOf(1));

        // Act: draw the mark and count the pixels of the color within 18 pixels of the head and of the tail
        var (_, annotated) = Draw(picture, [mark]);
        using var _ = annotated;
        var nearHead = CountNear(annotated, color, 200, 60, 18);
        var nearTail = CountNear(annotated, color, 20, 180, 18);

        // Assert: more of the color near the head, where the two lines of the head are, than near the tail
        Assert.True(nearHead > nearTail + 25, $"Head {nearHead}, tail {nearTail}.");
    }

    /// <summary>
    ///     Test that the same number has the same color on every picture, that the colors of the first marks differ, and
    ///     that they repeat after six.
    /// </summary>
    [Fact]
    public void MarkImageComposer_ColorOf_Numbers_GiveDistinctColorsThatRepeatAfterSix()
    {
        // Arrange / Act: the colors of the first seven numbers
        var colors = Enumerable.Range(1, 7).Select(MarkImageComposer.ColorOf).ToArray();

        // Assert: six distinct colors, and the seventh is the first again
        Assert.Equal(6, colors.Take(6).Distinct().Count());
        Assert.Equal(colors[0], colors[6]);
    }

    /// <summary>
    ///     Test that drawing leaves the picture that was marked unchanged.
    /// </summary>
    [Fact]
    public void MarkImageComposer_Compose_Marks_DoNotChangeTheRenderedPicture()
    {
        // Arrange: a picture with a recognizable gradient, a copy of its pixels, and a mark
        var pixels = new byte[200 * 100 * 4];
        for (var i = 0; i < pixels.Length; i++)
        {
            pixels[i] = (byte)(i % 251);
        }

        var picture = new RenderedImage(@"C:\project\gradient.svg", "svg", 200, 100, pixels);
        var before = picture.RgbaPixels.ToArray();
        var hash = picture.ContentHash;
        var mark = new MarkRegistry().Add(picture, MarkKind.Rectangle, [new RenderPoint(20, 20), new RenderPoint(120, 80)]);

        // Act: draw the mark
        _composer.Compose(picture, [mark]);

        // Assert: the pixels and the fingerprint of the picture are what they were
        Assert.Equal(before, picture.RgbaPixels.ToArray());
        Assert.Equal(hash, picture.ContentHash);
    }

    /// <summary>
    ///     Test that a picture with transparent pixels keeps them where no mark is drawn.
    /// </summary>
    [Fact]
    public void MarkImageComposer_Compose_PictureWithTransparency_KeepsTransparentPixelsAwayFromMarks()
    {
        // Arrange: a fully transparent picture with a mark in one corner
        var picture = new RenderedImage(@"C:\project\clear.svg", "svg", 200, 100, new byte[200 * 100 * 4]);
        var mark = new MarkRegistry().Add(picture, MarkKind.Rectangle, [new RenderPoint(20, 20), new RenderPoint(60, 60)]);

        // Act: draw the mark
        var (_, annotated) = Draw(picture, [mark]);
        using var _ = annotated;

        // Assert: far from the mark the pixel is still fully transparent
        Assert.Equal(0, annotated[180, 90].A);
        Assert.Equal(255, annotated[40, 20].A);
    }

    /// <summary>
    ///     Test that without marks the annotated picture is the picture itself and no areas are cut.
    /// </summary>
    [Fact]
    public void MarkImageComposer_Compose_NoMarks_ReturnsPictureWithoutAreas()
    {
        // Arrange: a flat picture
        var picture = AnnotationTestPictures.Picture(fill: Fill);

        // Act: compose without marks
        var (images, annotated) = Draw(picture, []);
        using var _ = annotated;

        // Assert: every pixel is the shown pixel and there is no area
        Assert.Empty(images.Crops);
        Assert.Equal(Background, annotated[0, 0]);
        Assert.Equal(Background, annotated[100, 50]);
        Assert.Equal(200, annotated.Width);
    }

    /// <summary>
    ///     Test that the area of a mark is its box and badge with a margin of 16 pixels, made at least 64 pixels high, and
    ///     that it is the corresponding region of the annotated picture.
    /// </summary>
    [Fact]
    public void MarkImageComposer_Compose_Mark_CutsAreaOfBoxAndBadgeWithMarginFromAnnotatedPicture()
    {
        // Arrange: a rectangle from (80, 50) to (120, 70) on a 300 by 200 picture, and a second mark inside the area
        var picture = AnnotationTestPictures.Picture(width: 300, height: 200, fill: Fill);
        var registry = new MarkRegistry();
        var mark = registry.Add(picture, MarkKind.Rectangle, [new RenderPoint(80, 50), new RenderPoint(120, 70)]);
        var neighbor = registry.Add(picture, MarkKind.Rectangle, [new RenderPoint(100, 60), new RenderPoint(130, 76)]);

        // Act: draw the marks
        var (images, annotated) = Draw(picture, registry.Marks);
        using var _ = annotated;
        var crop = images.Crops[0];
        using var cut = PayloadTestSupport.Decode(crop.Png);

        // Assert: the area holds the box and the badge (radius 9, centered at 80, 50) with 16 pixels around: x from 55 to
        // 136, y from 25 to 86 (61 high) grown to 64 around its center; the picture is the region of the annotated picture,
        // including the second mark that falls in it
        Assert.Equal(mark.Number, crop.Number);
        Assert.Equal(new RenderBox(55, 24, 81, 64), crop.Box);
        Assert.Equal(81, cut.Width);
        Assert.Equal(64, cut.Height);
        AssertRegionEquals(annotated, cut, 55, 24);
        Assert.Equal(neighbor.Number, images.Crops[1].Number);
    }

    /// <summary>
    ///     Test that the area of a mark near a corner of the picture is moved inside the picture and keeps its size of at
    ///     least 64 pixels, and that for a picture smaller than that it is the whole picture.
    /// </summary>
    [Fact]
    public void MarkImageComposer_Compose_MarkNearCornerOrOnSmallPicture_KeepsAreaInsidePicture()
    {
        // Arrange: a mark in the top-left corner of a large picture, and a mark on a picture of 50 by 40 pixels
        var large = AnnotationTestPictures.Picture(width: 200, height: 100, fill: Fill);
        var small = AnnotationTestPictures.Picture(width: 50, height: 40, fill: Fill);
        var cornerMark = new MarkRegistry().Add(large, MarkKind.Rectangle, [new RenderPoint(2, 2), new RenderPoint(30, 30)]);
        var smallMark = new MarkRegistry().Add(small, MarkKind.Rectangle, [new RenderPoint(10, 10), new RenderPoint(30, 30)]);

        // Act: draw both
        var corner = _composer.Compose(large, [cornerMark]).Crops.Single();
        var whole = _composer.Compose(small, [smallMark]).Crops.Single();
        using var wholePicture = PayloadTestSupport.Decode(whole.Png);

        // Assert: the corner area starts at the corner and is 64 by 64; the small picture's area is all of it
        Assert.Equal(new RenderBox(0, 0, 64, 64), corner.Box);
        Assert.Equal(new RenderBox(0, 0, 50, 40), whole.Box);
        Assert.Equal(50, wholePicture.Width);
        Assert.Equal(40, wholePicture.Height);
    }

    /// <summary>
    ///     Test that one, two, and ten marks give one numbered area each, in the order of their numbers, including marks
    ///     whose number has two digits.
    /// </summary>
    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(10)]
    public void MarkImageComposer_Compose_SeveralMarks_GivesOneAreaForEachInNumberOrder(int count)
    {
        // Arrange: a picture with that many small rectangles, added out of order
        var picture = AnnotationTestPictures.Picture(width: 400, height: 300, fill: Fill);
        var registry = new MarkRegistry();
        for (var i = 0; i < count; i++)
        {
            registry.Add(picture, MarkKind.Rectangle, [new RenderPoint(20 + (i * 30), 60), new RenderPoint(40 + (i * 30), 90)]);
        }

        var shuffled = registry.Marks.Reverse().ToList();

        // Act: draw the marks
        var images = _composer.Compose(picture, shuffled);

        // Assert: one area for each mark, in the order of the numbers, each a PNG file of at least 64 by 64
        Assert.Equal(Enumerable.Range(1, count), images.Crops.Select(crop => crop.Number));
        Assert.All(images.Crops, crop =>
        {
            using var cut = PayloadTestSupport.Decode(crop.Png);
            Assert.True(cut.Width >= 64 && cut.Height >= 64);
            Assert.Equal((int)crop.Box.Width, cut.Width);
        });
    }

    /// <summary>
    ///     Test that invalid arguments are refused: missing arguments, and a mark that was drawn on another picture.
    /// </summary>
    [Fact]
    public void MarkImageComposer_Compose_InvalidArguments_Throw()
    {
        // Arrange: a picture, a mark on it, and another picture with other pixels
        var picture = AnnotationTestPictures.Picture(fill: Fill);
        var other = AnnotationTestPictures.Picture(fill: 100);
        var mark = new MarkRegistry().Add(picture, MarkKind.Rectangle, [new RenderPoint(5, 5), new RenderPoint(50, 50)]);

        // Act / Assert: each invalid call is refused
        Assert.Throws<ArgumentNullException>(() => new MarkImageComposer(null!));
        Assert.Throws<ArgumentNullException>(() => _composer.Compose(null!, [mark]));
        Assert.Throws<ArgumentNullException>(() => _composer.Compose(picture, null!));
        Assert.Throws<ArgumentException>(() => _composer.Compose(other, [mark]));
    }

    /// <summary>
    ///     Test the sizes that the composer derives from the size of the picture.
    /// </summary>
    [Theory]
    [InlineData(100, 100, 3, 9)]
    [InlineData(800, 600, 8, 20)]
    [InlineData(400, 400, 5, 14)]
    public void MarkImageComposer_Sizes_GrowWithPictureWithinLimits(int width, int height, double stroke, double radius)
    {
        // Act / Assert: the line width and the badge radius for the size, within their limits
        Assert.Equal(stroke, MarkImageComposer.StrokeWidth(width, height));
        Assert.Equal(radius, MarkImageComposer.BadgeRadius(width, height));
    }

    /// <summary>
    ///     Counts the pixels of a color within a distance of a point.
    /// </summary>
    /// <param name="surface">The picture.</param>
    /// <param name="color">The color to count.</param>
    /// <param name="centerX">The x of the point.</param>
    /// <param name="centerY">The y of the point.</param>
    /// <param name="distance">The distance in pixels.</param>
    /// <returns>The number of pixels.</returns>
    private static int CountNear(Surface surface, Rgba32 color, int centerX, int centerY, int distance)
    {
        var count = 0;
        for (var y = Math.Max(0, centerY - distance); y <= Math.Min(surface.Height - 1, centerY + distance); y++)
        {
            for (var x = Math.Max(0, centerX - distance); x <= Math.Min(surface.Width - 1, centerX + distance); x++)
            {
                if (surface[x, y] == color && (((x - centerX) * (x - centerX)) + ((y - centerY) * (y - centerY))) <= distance * distance)
                {
                    count++;
                }
            }
        }

        return count;
    }

    /// <summary>
    ///     Asserts that a picture is the region of another picture that starts at a position.
    /// </summary>
    /// <param name="whole">The larger picture.</param>
    /// <param name="part">The cut-out.</param>
    /// <param name="left">The x of the region in <paramref name="whole" />.</param>
    /// <param name="top">The y of the region in <paramref name="whole" />.</param>
    private static void AssertRegionEquals(Surface whole, Surface part, int left, int top)
    {
        var different = 0;
        for (var y = 0; y < part.Height; y++)
        {
            for (var x = 0; x < part.Width; x++)
            {
                if (whole[left + x, top + y] != part[x, y])
                {
                    different++;
                }
            }
        }

        Assert.Equal(0, different);
    }
}
