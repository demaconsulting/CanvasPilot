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

using DemaConsulting.CanvasPilot.AgentSubsystem;
using DemaConsulting.CanvasPilot.PreviewSubsystem;
using DemaConsulting.CanvasPilot.ProjectSubsystem;

namespace DemaConsulting.CanvasPilot.Tests;

/// <summary>
///     Unit tests for the supporting types of <see cref="ProjectTools" />: <see cref="ProjectPathGuard" /> (links, spellings
///     that mean something else, and containment), <see cref="ImageScaler" /> (the scaling of a picture to the size a model
///     takes), and <see cref="ProjectFileKinds" /> (which files are text and which are pictures).
/// </summary>
public sealed class ProjectToolsSupportTests : ProjectToolsTestBase
{
    /// <summary>
    ///     Test that a folder junction is a link and that a file, a folder, and something that does not exist are not.
    /// </summary>
    [Fact]
    public void ProjectPathGuard_IsLink_JunctionIsALinkAndOrdinaryEntriesAreNot()
    {
        // Arrange
        var folder = Path.Combine(Project, "folder");
        Directory.CreateDirectory(folder);
        var file = Path.Combine(Project, "a.txt");
        Put(file, "x");
        var junction = Path.Combine(Project, "junction");
        Links.CreateJunction(junction, folder);

        // Act / Assert
        Assert.True(ProjectPathGuard.IsLink(junction));
        Assert.False(ProjectPathGuard.IsLink(folder));
        Assert.False(ProjectPathGuard.IsLink(file));
        Assert.False(ProjectPathGuard.IsLink(Path.Combine(Project, "missing")));
    }

    /// <summary>
    ///     Test that a junction whose target is gone is still found as a link, and that the first link on the way is named.
    /// </summary>
    [Fact]
    public void ProjectPathGuard_FindLink_DanglingJunctionAndNestedPath_NamesTheFirstLink()
    {
        // Arrange: a junction to a folder that is then removed, and a path below a real folder and the junction
        var target = Path.Combine(Root, "gone");
        Directory.CreateDirectory(target);
        Directory.CreateDirectory(Path.Combine(Project, "real"));
        Links.CreateJunction(Path.Combine(Project, "real", "dangling"), target);
        Directory.Delete(target);

        // Act / Assert
        Assert.Equal("real/dangling", ProjectPathGuard.FindLink(Project, "real/dangling/a.svg"));
        Assert.Null(ProjectPathGuard.FindLink(Project, "real/other/a.svg"));
        Assert.Null(ProjectPathGuard.FindLink(Project, "nothing/a.svg"));
    }

    /// <summary>
    ///     Test that containment is judged by whole folder names, so a folder beside the project whose name starts like it is
    ///     outside, and that the folder itself is inside.
    /// </summary>
    [Fact]
    public void ProjectPathGuard_IsInside_FolderBesideWithTheSamePrefix_IsOutside()
    {
        // Act / Assert
        Assert.True(ProjectPathGuard.IsInside(Project, Path.Combine(Project, "a", "b.svg")));
        Assert.True(ProjectPathGuard.IsInside(Project, Project));
        Assert.True(ProjectPathGuard.IsInside(Project + Path.DirectorySeparatorChar, Path.Combine(Project, "a.svg")));
        Assert.False(ProjectPathGuard.IsInside(Project, Project + "-evil"));
        Assert.False(ProjectPathGuard.IsInside(Project, Path.Combine(Root, "outside.txt")));
    }

    /// <summary>
    ///     Test that names that mean something other than the file they seem to name are refused with a reason, and that an
    ///     ordinary name is accepted.
    /// </summary>
    /// <param name="relative">The path below the folder.</param>
    /// <param name="accepted">Whether the path is accepted.</param>
    [Theory]
    [InlineData("a/b.svg", true)]
    [InlineData("a b/c-d_e.svg", true)]
    [InlineData("é/ü.svg", true)]
    [InlineData("a.svg:stream", false)]
    [InlineData("a/.canvaspilot/b.svg", false)]
    [InlineData("a/Brochure.CanvasPilot", false)]
    [InlineData("a/CANVAS~1/b.svg", false)]
    [InlineData("a/b.svg.", false)]
    [InlineData("a/b.svg ", false)]
    [InlineData("a/NUL.svg", false)]
    [InlineData("a/com1", false)]
    [InlineData("a/LPT9.txt", false)]
    [InlineData("a/CONIN$", false)]
    public void ProjectPathGuard_TryResolveUnder_Names_AreCheckedForSpellingsThatMeanSomethingElse(string relative, bool accepted)
    {
        // Act: the path is below the project folder; Windows would trim a trailing dot or space, so the full path is
        // given as it is spelled
        var request = Project + Path.DirectorySeparatorChar + relative.Replace('/', Path.DirectorySeparatorChar);
        var result = ProjectPathGuard.TryResolveUnder(Project, request, out var full, out _, out var refusal);

        // Assert: Windows removes a trailing dot or space when it makes the full path, so those two are accepted there as
        // the name that remains
        var trimsBySystem = OperatingSystem.IsWindows() && (relative.EndsWith('.') || relative.EndsWith(' '));
        Assert.Equal(accepted || trimsBySystem, result);
        if (!result)
        {
            Assert.False(string.IsNullOrWhiteSpace(refusal));
            Assert.Empty(full);
        }
    }

    /// <summary>
    ///     Test that a full path outside the folder, and one that climbs out by <c>..</c>, is refused by the folder check.
    /// </summary>
    [Fact]
    public void ProjectPathGuard_TryResolveUnder_PathsOutsideTheFolder_AreRefused()
    {
        // Act
        var outside = ProjectPathGuard.TryResolveUnder(Project, Path.Combine(Root, "outside.txt"), out _, out _, out var first);
        var climb = ProjectPathGuard.TryResolveUnder(Project, Path.Combine(Project, "..", "outside.txt"), out _, out _, out var second);
        var inside = ProjectPathGuard.TryResolveUnder(Project, Path.Combine(Project, "a", "..", "b.svg"), out var full, out var relative, out _);

        // Assert
        Assert.False(outside);
        Assert.False(climb);
        Assert.Contains("outside", first + second, StringComparison.Ordinal);
        Assert.True(inside);
        Assert.Equal(Path.Combine(Project, "b.svg"), full);
        Assert.Equal("b.svg", relative);
    }

    /// <summary>
    ///     Test that a picture that fits is returned as it is, that a larger one is scaled with its proportions kept and at
    ///     least one pixel on the short side, and that the notes and the source are kept.
    /// </summary>
    [Fact]
    public void ImageScaler_FitWithin_SizesAreScaledByTheLongerSideAndSmallOnesAreKept()
    {
        // Arrange
        var small = TestImages.Create("a.svg", 100, 50);
        var wide = TestImages.Create("w.svg", 4000, 1000, [new RenderDiagnostic(RenderDiagnosticSeverity.Info, "note", "a note")]);
        var thin = TestImages.Create("t.svg", 5000, 3);

        // Act
        var same = ImageScaler.FitWithin(small, 1280);
        var exact = ImageScaler.FitWithin(TestImages.Create("e.svg", 1280, 10), 1280);
        var scaled = ImageScaler.FitWithin(wide, 1280);
        var thinScaled = ImageScaler.FitWithin(thin, 1280);

        // Assert
        Assert.Same(small, same);
        Assert.Equal((1280, 10), (exact.Width, exact.Height));
        Assert.Equal((1280, 320), (scaled.Width, scaled.Height));
        Assert.Equal("w.svg", scaled.SourcePath);
        Assert.Equal("note", Assert.Single(scaled.Diagnostics).Code);
        Assert.Equal(scaled.Width * scaled.Height * 4, scaled.RgbaPixels.Length);
        Assert.Equal((1280, 1), (thinScaled.Width, thinScaled.Height));
    }

    /// <summary>
    ///     Test that each pixel of a smaller picture is the average of its block with the colors weighted by opacity: two
    ///     colors average, a clear pixel does not tint, and a block with no opacity stays clear.
    /// </summary>
    [Fact]
    public void ImageScaler_FitWithin_Pixels_AreAveragedWeightedByOpacity()
    {
        // Arrange: a 4 x 1 picture: black, white, opaque red beside clear green, and clear
        byte[] pixels =
        [
            0, 0, 0, 255, 255, 255, 255, 255,
            255, 0, 0, 255, 0, 255, 0, 0,
        ];
        var image = new RenderedImage("p.svg", "svg", 4, 1, pixels);

        // Act: scale to 2 pixels wide, so that two pixels make one
        var scaled = ImageScaler.FitWithin(image, 2);
        var result = scaled.RgbaPixels.ToArray();

        // Assert: grey from black and white; red kept at half opacity, and not darkened or greened by the clear pixel
        Assert.Equal((2, 1), (scaled.Width, scaled.Height));
        Assert.Equal([128, 128, 128, 255, 255, 0, 0, 128], result);
        var clear = ImageScaler.FitWithin(new RenderedImage("c.svg", "svg", 2, 1, new byte[8]), 1);
        Assert.Equal(new byte[4], clear.RgbaPixels.ToArray());
    }

    /// <summary>
    ///     Test that the scaler refuses a missing picture and a size below one pixel.
    /// </summary>
    [Fact]
    public void ImageScaler_FitWithin_BadArguments_AreRefused()
    {
        // Act / Assert
        Assert.Throws<ArgumentNullException>(() => ImageScaler.FitWithin(null!, 10));
        Assert.Throws<ArgumentOutOfRangeException>(() => ImageScaler.FitWithin(TestImages.Create("a.svg", 2, 2), 0));
    }

    /// <summary>
    ///     Test that text image sources and the data files are text files, that the binary image sources are pictures and not
    ///     text, and that other files are neither.
    /// </summary>
    [Fact]
    public void ProjectFileKinds_Classify_TextSourcesDataFilesAndBinaryPictures()
    {
        // Arrange
        var kinds = new ProjectFileKinds(new CanvasNetImageSourceFilter());

        // Act / Assert: text image sources and data files
        foreach (var name in new[] { "a.svg", "A.MMD", "a.puml", "a.scad", "a.vl.json", "a.csv", "a.json", "a.txt" })
        {
            Assert.True(kinds.IsTextFile(name), name);
            Assert.False(kinds.IsBinaryImage(name), name);
        }

        // Binary pictures are image sources and not text files
        foreach (var name in new[] { "a.png", "a.JPG", "a.pdf", "a.pptx", "a.vsdx", "a.tif" })
        {
            Assert.False(kinds.IsTextFile(name), name);
            Assert.True(kinds.IsBinaryImage(name), name);
            Assert.True(kinds.IsImageSource(name), name);
        }

        // Anything else is neither
        foreach (var name in new[] { "a.exe", "a", ".svg", "a.svg.bak" })
        {
            Assert.False(kinds.IsTextFile(name), name);
            Assert.False(kinds.IsBinaryImage(name), name);
        }

        Assert.Throws<ArgumentNullException>(() => new ProjectFileKinds(null!));
    }
}
