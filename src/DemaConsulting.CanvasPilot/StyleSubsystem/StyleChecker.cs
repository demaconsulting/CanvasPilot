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

using System.Diagnostics.CodeAnalysis;
using DemaConsulting.CanvasPilot.PreviewSubsystem;

namespace DemaConsulting.CanvasPilot.StyleSubsystem;

/// <summary>
///     Checks the colors of a picture against the palette of a style kit: how much of the picture is on the palette, how
///     much is a blend of palette colors at an edge, and which colors are off the palette.
/// </summary>
/// <remarks>
///     <para>
///         The check works on the pixels that the application already holds for a rendered picture (straight-alpha RGBA),
///         so it needs no second render. It counts the pixels in a histogram of 32 levels per channel (32,768 buckets: the
///         number of pixels and the sums of the channels), so a picture of any size costs one pass and a small, fixed
///         amount of work afterwards, and takes the mean of each occupied bucket as its color.
///     </para>
///     <para>
///         Each bucket is classified in this order. It is on the palette when its color is within the tolerance (CIE76 in
///         CIELAB) of a palette color, which gets the credit. Otherwise it is a blend when its color is within the blend
///         tolerance of the straight line between two palette colors (in sRGB, the space in which the edges of a shape are
///         blended), which is what anti-aliasing makes. Otherwise it is off the palette. Buckets that are off the palette
///         are taken together, the largest first, into colors that are within the cluster difference of each other; a
///         cluster that has the minimum share is listed (at most eight), and the rest is summed. The picture passes when
///         the whole off-palette share is within the limit.
///     </para>
///     <para>
///         A smooth gradient between two palette colors lies on the line between them, so it counts as blended; a gradient
///         to a color that is not in the palette does not. The public image inspector of CanvasNet was evaluated and not
///         used: it needs the path of the file (a second render under a second access policy), it merges near colors of one
///         family into a mean, and it lists at most 32 colors, which is right for dominant colors and wrong for the
///         question whether a color is in a palette. The result does not depend on the order of the pixels or on the machine.
///     </para>
/// </remarks>
public static class StyleChecker
{
    /// <summary>
    ///     The number of bits of each channel that make the key of a bucket.
    /// </summary>
    private const int BucketBits = 5;

    /// <summary>
    ///     Why the per-pixel methods are left out of the measure of code coverage: the coverage tool counts every branch of
    ///     them, which slows a picture of 65 million pixels by a factor of a hundred, so the run that measures coverage could
    ///     not show that the check is as quick as its requirement says. The unit tests run every line of them.
    /// </summary>
    private const string HotLoopJustification = "Per-pixel loop; instrumenting it makes the check too slow to measure its speed in a coverage run.";

    /// <summary>
    ///     The number of buckets: 32 levels for each of the three channels.
    /// </summary>
    private const int BucketCount = 1 << (3 * BucketBits);

    /// <summary>
    ///     The color of the background when a kit has no color for the role <c>background</c>.
    /// </summary>
    private const byte DefaultBackground = 255;

    /// <summary>
    ///     Checks a rendered picture against a kit.
    /// </summary>
    /// <param name="image">The picture.</param>
    /// <param name="kit">The kit whose palette the picture is checked against.</param>
    /// <param name="options">The limits, or <see langword="null" /> for the defaults.</param>
    /// <returns>The report.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="image" /> or <paramref name="kit" /> is <see langword="null" />.</exception>
    /// <exception cref="ArgumentException">Thrown when the kit has no usable color.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when a limit of the options is not in its range.</exception>
    public static StyleCheckReport Check(RenderedImage image, StyleKit kit, StyleCheckOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(image);
        return Check(image.RgbaPixels.Span, image.Width, image.Height, kit, options);
    }

    /// <summary>
    ///     Checks pixels against a kit.
    /// </summary>
    /// <param name="rgba">The pixels: four bytes per pixel, red, green, blue, alpha, with straight alpha, rows from the top with no padding.</param>
    /// <param name="width">The width in pixels, 0 or more.</param>
    /// <param name="height">The height in pixels, 0 or more.</param>
    /// <param name="kit">The kit whose palette the pixels are checked against.</param>
    /// <param name="options">The limits, or <see langword="null" /> for the defaults.</param>
    /// <returns>The report.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="kit" /> is <see langword="null" />.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when the width or the height is negative, or a limit of the options is not in its range.</exception>
    /// <exception cref="ArgumentException">Thrown when the pixels are not <c>width * height * 4</c> bytes, or the kit has no usable color.</exception>
    public static StyleCheckReport Check(ReadOnlySpan<byte> rgba, int width, int height, StyleKit kit, StyleCheckOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(kit);
        ArgumentOutOfRangeException.ThrowIfNegative(width);
        ArgumentOutOfRangeException.ThrowIfNegative(height);
        if (rgba.Length != (long)width * height * 4)
        {
            throw new ArgumentException($"A {width} x {height} picture needs {(long)width * height * 4} bytes of pixels, not {rgba.Length}.", nameof(rgba));
        }

        var limits = options ?? new StyleCheckOptions();
        limits.Validate();
        var palette = ReadPalette(kit);
        if (palette.Count == 0)
        {
            throw new ArgumentException("The kit has no color in the form #RRGGBB to check against.", nameof(kit));
        }

        // Count the pixels in the histogram, flattening what is partly see-through over the background of the kit
        var background = palette.FirstOrDefault(c => c.Role == "background");
        var bucketCounts = new long[BucketCount];
        var sums = new long[3 * BucketCount];
        var (considered, transparent) = CountPixels(rgba, background?.Red ?? DefaultBackground, background?.Green ?? DefaultBackground, background?.Blue ?? DefaultBackground, bucketCounts, sums);

        // Sort the buckets into palette colors, blends, and colors that are off the palette
        var onCounts = new long[palette.Count];
        long blendCount = 0;
        var offBuckets = new List<OffBucket>();
        for (var key = 0; key < BucketCount; key++)
        {
            var count = bucketCounts[key];
            if (count == 0)
            {
                continue;
            }

            var red = sums[3 * key] / (double)count;
            var green = sums[(3 * key) + 1] / (double)count;
            var blue = sums[(3 * key) + 2] / (double)count;
            var lab = ColorMath.ToLab(red, green, blue);
            var (nearest, distance) = Nearest(palette, lab);
            if (distance <= limits.Tolerance)
            {
                onCounts[nearest] += count;
            }
            else if (IsBlend(palette, red, green, blue, limits.BlendTolerance))
            {
                blendCount += count;
            }
            else
            {
                offBuckets.Add(new OffBucket(key, count, sums[3 * key], sums[(3 * key) + 1], sums[(3 * key) + 2], lab));
            }
        }

        var clusters = GroupBuckets(offBuckets, limits.ClusterDelta);
        return BuildReport(kit, palette, width, height, considered, transparent, onCounts, blendCount, clusters, limits);
    }

    /// <summary>
    ///     Reads the colors of the palette of a kit in the order of the kit, leaving out an entry that is not a color.
    /// </summary>
    /// <param name="kit">The kit.</param>
    /// <returns>The palette colors with their conversion to CIELAB.</returns>
    private static List<PaletteColor> ReadPalette(StyleKit kit)
    {
        var palette = new List<PaletteColor>(kit.Palette.Count);
        foreach (var (role, color) in kit.Palette)
        {
            if (StyleColors.TryParse(color, out var red, out var green, out var blue))
            {
                palette.Add(new PaletteColor(role, StyleColors.Normalize(color), red, green, blue, ColorMath.ToLab(red, green, blue)));
            }
        }

        return palette;
    }

    /// <summary>
    ///     Counts the pixels into the buckets of the histogram. A pixel that is nearly empty is not counted, and a pixel that
    ///     is partly see-through is flattened over the background first. A run of equal pixels is added in one step.
    /// </summary>
    /// <param name="rgba">The pixels.</param>
    /// <param name="backgroundRed">The red channel of the background.</param>
    /// <param name="backgroundGreen">The green channel of the background.</param>
    /// <param name="backgroundBlue">The blue channel of the background.</param>
    /// <param name="bucketCounts">Receives the number of pixels in each bucket.</param>
    /// <param name="sums">Receives the sums of the red, green, and blue channels of the pixels of each bucket, three numbers a bucket.</param>
    /// <returns>The number of pixels that were counted and the number that were empty.</returns>
    [ExcludeFromCodeCoverage(Justification = HotLoopJustification)]
    private static (long Considered, long Transparent) CountPixels(
        ReadOnlySpan<byte> rgba,
        byte backgroundRed,
        byte backgroundGreen,
        byte backgroundBlue,
        long[] bucketCounts,
        long[] sums)
    {
        long considered = 0;
        long transparent = 0;
        var previous = -1;
        long run = 0;
        for (var offset = 0; offset < rgba.Length; offset += 4)
        {
            int red = rgba[offset];
            int green = rgba[offset + 1];
            int blue = rgba[offset + 2];
            int alpha = rgba[offset + 3];

            // An empty pixel is not part of the picture; a partly see-through one looks as it does over the background
            if (alpha < StyleCheckOptions.TransparentBelowAlpha)
            {
                transparent++;
                continue;
            }

            if (alpha < 255)
            {
                red = Flatten(red, backgroundRed, alpha);
                green = Flatten(green, backgroundGreen, alpha);
                blue = Flatten(blue, backgroundBlue, alpha);
            }

            considered++;

            // Equal pixels follow each other in flat areas, so they are added together when the run ends
            var packed = (red << 16) | (green << 8) | blue;
            if (packed == previous)
            {
                run++;
                continue;
            }

            Flush(previous, run, bucketCounts, sums);
            previous = packed;
            run = 1;
        }

        Flush(previous, run, bucketCounts, sums);
        return (considered, transparent);
    }

    /// <summary>
    ///     Adds a run of equal pixels to its bucket.
    /// </summary>
    /// <param name="packed">The color of the run, <c>0xRRGGBB</c>, or -1 for no run.</param>
    /// <param name="run">The number of pixels in the run.</param>
    /// <param name="bucketCounts">The number of pixels in each bucket.</param>
    /// <param name="sums">The sums of the channels of each bucket.</param>
    [ExcludeFromCodeCoverage(Justification = HotLoopJustification)]
    private static void Flush(int packed, long run, long[] bucketCounts, long[] sums)
    {
        if (packed < 0 || run == 0)
        {
            return;
        }

        var red = (packed >> 16) & 0xFF;
        var green = (packed >> 8) & 0xFF;
        var blue = packed & 0xFF;
        var key = ((red >> 3) << 10) | ((green >> 3) << 5) | (blue >> 3);
        bucketCounts[key] += run;
        sums[3 * key] += run * red;
        sums[(3 * key) + 1] += run * green;
        sums[(3 * key) + 2] += run * blue;
    }

    /// <summary>
    ///     Mixes a channel of a pixel with the background by the alpha of the pixel, rounding to the nearest value.
    /// </summary>
    /// <param name="channel">The channel of the pixel.</param>
    /// <param name="background">The same channel of the background.</param>
    /// <param name="alpha">The alpha of the pixel, 16 to 254.</param>
    /// <returns>The channel that the pixel has over the background.</returns>
    [ExcludeFromCodeCoverage(Justification = HotLoopJustification)]
    private static int Flatten(int channel, int background, int alpha) => ((channel * alpha) + (background * (255 - alpha)) + 127) / 255;

    /// <summary>
    ///     Finds the palette color that is nearest to a color.
    /// </summary>
    /// <param name="palette">The palette.</param>
    /// <param name="lab">The color.</param>
    /// <returns>The index of the nearest color and its difference; a tie goes to the color that comes first in the palette.</returns>
    private static (int Index, double Distance) Nearest(List<PaletteColor> palette, ColorMath.Lab lab)
    {
        var index = 0;
        var distance = double.PositiveInfinity;
        for (var i = 0; i < palette.Count; i++)
        {
            var candidate = ColorMath.DeltaE76(lab, palette[i].Lab);
            if (candidate < distance)
            {
                distance = candidate;
                index = i;
            }
        }

        return (index, distance);
    }

    /// <summary>
    ///     Tells whether a color is within a tolerance of the straight line between two palette colors.
    /// </summary>
    /// <param name="palette">The palette.</param>
    /// <param name="red">The red channel of the color.</param>
    /// <param name="green">The green channel of the color.</param>
    /// <param name="blue">The blue channel of the color.</param>
    /// <param name="tolerance">The color difference that is allowed.</param>
    /// <returns><see langword="true" /> when the color is a blend of two palette colors.</returns>
    private static bool IsBlend(List<PaletteColor> palette, double red, double green, double blue, double tolerance)
    {
        for (var i = 0; i < palette.Count; i++)
        {
            var from = new ColorMath.Rgb(palette[i].Red, palette[i].Green, palette[i].Blue);
            for (var j = i + 1; j < palette.Count; j++)
            {
                var to = new ColorMath.Rgb(palette[j].Red, palette[j].Green, palette[j].Blue);
                if (ColorMath.BlendDistance(red, green, blue, from, to) <= tolerance)
                {
                    return true;
                }
            }
        }

        return false;
    }

    /// <summary>
    ///     Takes the buckets that are off the palette together into colors: the largest first, a bucket joins the first color
    ///     whose first bucket is within the difference, and otherwise starts a color.
    /// </summary>
    /// <param name="buckets">The buckets that are off the palette.</param>
    /// <param name="delta">The color difference within which buckets are one color.</param>
    /// <returns>The colors, the largest first; a tie goes to the bucket with the lower key.</returns>
    private static List<Cluster> GroupBuckets(List<OffBucket> buckets, double delta)
    {
        // The order of the buckets decides the result, so it is fixed: by size, then by key
        buckets.Sort((a, b) => b.Count != a.Count ? b.Count.CompareTo(a.Count) : a.Key.CompareTo(b.Key));
        var clusters = new List<Cluster>();
        foreach (var bucket in buckets)
        {
            var joined = clusters.FirstOrDefault(c => ColorMath.DeltaE76(c.SeedLab, bucket.Lab) <= delta);
            if (joined is null)
            {
                clusters.Add(new Cluster(bucket));
            }
            else
            {
                joined.Add(bucket);
            }
        }

        clusters.Sort((a, b) => b.Count != a.Count ? b.Count.CompareTo(a.Count) : a.SeedKey.CompareTo(b.SeedKey));
        return clusters;
    }

    /// <summary>
    ///     Builds the report from the counts.
    /// </summary>
    /// <param name="kit">The kit.</param>
    /// <param name="palette">The palette.</param>
    /// <param name="width">The width of the picture.</param>
    /// <param name="height">The height of the picture.</param>
    /// <param name="considered">The number of pixels that were counted.</param>
    /// <param name="transparent">The number of pixels that were empty.</param>
    /// <param name="onCounts">The number of pixels credited to each palette color.</param>
    /// <param name="blendCount">The number of pixels that are blends.</param>
    /// <param name="clusters">The colors that are off the palette.</param>
    /// <param name="limits">The limits.</param>
    /// <returns>The report.</returns>
    private static StyleCheckReport BuildReport(
        StyleKit kit,
        List<PaletteColor> palette,
        int width,
        int height,
        long considered,
        long transparent,
        long[] onCounts,
        long blendCount,
        List<Cluster> clusters,
        StyleCheckOptions limits)
    {
        // A picture with nothing counted has no shares and nothing to hold against it
        if (considered == 0)
        {
            return new StyleCheckReport(kit.Id, kit.Name, width, height, 0, transparent, [], 0, [], 0, 0, true, limits.MaxOffPaletteShare);
        }

        double Share(long count) => count / (double)considered;

        // The palette colors that are used, the largest share first and the order of the palette for equal shares
        var onPalette = Enumerable.Range(0, palette.Count)
            .Where(i => onCounts[i] > 0)
            .OrderByDescending(i => onCounts[i])
            .ThenBy(i => i)
            .Select(i => new StylePaletteUse(palette[i].Role, palette[i].Hex, Share(onCounts[i])))
            .ToList();

        // The colors off the palette: those that are large enough are listed, at most the limit, and the rest is summed
        var offTotal = clusters.Sum(c => c.Count);
        var listed = new List<StyleOffPaletteColor>();
        long listedCount = 0;
        foreach (var cluster in clusters.Where(c => Share(c.Count) >= limits.MinReportShare).Take(StyleCheckOptions.MaxListed))
        {
            var mean = cluster.Mean();
            var (nearest, distance) = Nearest(palette, ColorMath.ToLab(mean.Red, mean.Green, mean.Blue));
            listed.Add(new StyleOffPaletteColor(
                $"#{mean.Red:X2}{mean.Green:X2}{mean.Blue:X2}",
                Share(cluster.Count),
                palette[nearest].Role,
                palette[nearest].Hex,
                distance));
            listedCount += cluster.Count;
        }

        var offShare = Share(offTotal);
        return new StyleCheckReport(
            kit.Id,
            kit.Name,
            width,
            height,
            considered,
            transparent,
            onPalette,
            Share(blendCount),
            listed,
            Share(offTotal - listedCount),
            offShare,
            offShare <= limits.MaxOffPaletteShare,
            limits.MaxOffPaletteShare);
    }

    /// <summary>
    ///     A color of the palette in the forms that the check needs.
    /// </summary>
    /// <param name="Role">The role of the color.</param>
    /// <param name="Hex">The color, <c>#RRGGBB</c>.</param>
    /// <param name="Red">The red channel.</param>
    /// <param name="Green">The green channel.</param>
    /// <param name="Blue">The blue channel.</param>
    /// <param name="Lab">The color in CIELAB.</param>
    private sealed record PaletteColor(string Role, string Hex, byte Red, byte Green, byte Blue, ColorMath.Lab Lab);

    /// <summary>
    ///     A bucket of the histogram that is off the palette.
    /// </summary>
    /// <param name="Key">The key of the bucket.</param>
    /// <param name="Count">The number of pixels.</param>
    /// <param name="SumRed">The sum of the red channels.</param>
    /// <param name="SumGreen">The sum of the green channels.</param>
    /// <param name="SumBlue">The sum of the blue channels.</param>
    /// <param name="Lab">The mean color in CIELAB.</param>
    private sealed record OffBucket(int Key, long Count, long SumRed, long SumGreen, long SumBlue, ColorMath.Lab Lab);

    /// <summary>
    ///     A color off the palette that several buckets were taken together into.
    /// </summary>
    private sealed class Cluster
    {
        /// <summary>
        ///     The sum of the red channels.
        /// </summary>
        private long _red;

        /// <summary>
        ///     The sum of the green channels.
        /// </summary>
        private long _green;

        /// <summary>
        ///     The sum of the blue channels.
        /// </summary>
        private long _blue;

        /// <summary>
        ///     Initializes a new instance of the <see cref="Cluster" /> class from its first bucket.
        /// </summary>
        /// <param name="seed">The first and largest bucket.</param>
        public Cluster(OffBucket seed)
        {
            SeedKey = seed.Key;
            SeedLab = seed.Lab;
            Add(seed);
        }

        /// <summary>
        ///     Gets the key of the first bucket, which breaks a tie in the order of the colors.
        /// </summary>
        public int SeedKey { get; }

        /// <summary>
        ///     Gets the color of the first bucket, which the other buckets are compared with.
        /// </summary>
        public ColorMath.Lab SeedLab { get; }

        /// <summary>
        ///     Gets the number of pixels.
        /// </summary>
        public long Count { get; private set; }

        /// <summary>
        ///     Adds a bucket.
        /// </summary>
        /// <param name="bucket">The bucket.</param>
        public void Add(OffBucket bucket)
        {
            Count += bucket.Count;
            _red += bucket.SumRed;
            _green += bucket.SumGreen;
            _blue += bucket.SumBlue;
        }

        /// <summary>
        ///     Gets the mean color of the pixels, rounded to channels.
        /// </summary>
        /// <returns>The channels.</returns>
        public (byte Red, byte Green, byte Blue) Mean() =>
            ((byte)Math.Round(_red / (double)Count), (byte)Math.Round(_green / (double)Count), (byte)Math.Round(_blue / (double)Count));
    }
}
