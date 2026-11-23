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

using DemaConsulting.CanvasPilot.UtilitiesSubsystem;

namespace DemaConsulting.CanvasPilot.Tests;

/// <summary>
///     Tests for the PathHelpers class.
/// </summary>
public class PathHelpersTests
{
    /// <summary>
    ///     Test that SafePathCombine correctly combines valid paths.
    /// </summary>
    [Fact]
    public void PathHelpers_SafePathCombine_ValidPaths_CombinesCorrectly()
    {
        // Arrange: setup valid base path and relative path for combining
        var basePath = "/home/user/project";
        var relativePath = "subfolder/file.txt";

        // Act: execute the operation being tested
        var result = PathHelpers.SafePathCombine(basePath, relativePath);

        // Assert: verify expected behavior
        Assert.Equal(Path.Combine(basePath, relativePath), result);
    }

    /// <summary>
    ///     Test that SafePathCombine throws ArgumentException for path traversal with double dots.
    /// </summary>
    [Fact]
    public void PathHelpers_SafePathCombine_PathTraversalWithDoubleDots_ThrowsArgumentException()
    {
        // Arrange: setup base path and dangerous path traversal using double dots
        var basePath = "/home/user/project";
        var relativePath = "../etc/passwd";

        // Act & Assert: attempt path traversal and verify ArgumentException is thrown with expected message
        var exception = Assert.Throws<ArgumentException>(() =>
            PathHelpers.SafePathCombine(basePath, relativePath));
        Assert.Contains("Invalid path component", exception.Message);
    }

    /// <summary>
    ///     Test that SafePathCombine throws ArgumentException for path with double dots in middle.
    /// </summary>
    [Fact]
    public void PathHelpers_SafePathCombine_DoubleDotsInMiddle_ThrowsArgumentException()
    {
        // Arrange: setup base path and path with double dots in middle for traversal attempt
        var basePath = "/home/user/project";
        var relativePath = "subfolder/../../../etc/passwd";

        // Act & Assert: attempt path traversal in middle and verify ArgumentException is thrown
        var exception = Assert.Throws<ArgumentException>(() =>
            PathHelpers.SafePathCombine(basePath, relativePath));
        Assert.Contains("Invalid path component", exception.Message);
    }

    /// <summary>
    ///     Test that SafePathCombine throws ArgumentException for absolute paths.
    /// </summary>
    [Fact]
    public void PathHelpers_SafePathCombine_AbsolutePath_ThrowsArgumentException()
    {
        // Arrange & Act & Assert: test Unix absolute path rejection
        var unixBasePath = "/home/user/project";
        var unixRelativePath = "/etc/passwd";
        var unixException = Assert.Throws<ArgumentException>(() =>
            PathHelpers.SafePathCombine(unixBasePath, unixRelativePath));
        Assert.Contains("Invalid path component", unixException.Message);

        // Arrange & Act & Assert: test Windows absolute path rejection (only on Windows)
        if (OperatingSystem.IsWindows())
        {
            var windowsBasePath = "C:\\Users\\project";
            var windowsRelativePath = "C:\\Windows\\System32\\file.txt";
            var windowsException = Assert.Throws<ArgumentException>(() =>
                PathHelpers.SafePathCombine(windowsBasePath, windowsRelativePath));
            Assert.Contains("Invalid path component", windowsException.Message);
        }
    }

    /// <summary>
    ///     Test that SafePathCombine correctly handles current directory reference.
    /// </summary>
    [Fact]
    public void PathHelpers_SafePathCombine_CurrentDirectoryReference_CombinesCorrectly()
    {
        // Arrange: setup base path and current directory reference for testing
        var basePath = "/home/user/project";
        var relativePath = "./subfolder/file.txt";

        // Act: execute the operation being tested
        var result = PathHelpers.SafePathCombine(basePath, relativePath);

        // Assert: verify expected behavior
        Assert.Equal(Path.Combine(basePath, relativePath), result);
    }

    /// <summary>
    ///     Test that SafePathCombine correctly handles nested paths.
    /// </summary>
    [Fact]
    public void PathHelpers_SafePathCombine_NestedPaths_CombinesCorrectly()
    {
        // Arrange: setup base path and deeply nested relative path
        var basePath = "/home/user/project";
        var relativePath = "level1/level2/level3/file.txt";

        // Act: execute the operation being tested
        var result = PathHelpers.SafePathCombine(basePath, relativePath);

        // Assert: verify expected behavior
        Assert.Equal(Path.Combine(basePath, relativePath), result);
    }

    /// <summary>
    ///     Test that SafePathCombine correctly handles empty relative path.
    /// </summary>
    [Fact]
    public void PathHelpers_SafePathCombine_EmptyRelativePath_ReturnsBasePath()
    {
        // Arrange: setup base path and empty relative path for testing edge case
        var basePath = "/home/user/project";
        var relativePath = "";

        // Act: execute the operation being tested
        var result = PathHelpers.SafePathCombine(basePath, relativePath);

        // Assert: verify expected behavior
        Assert.Equal(Path.Combine(basePath, relativePath), result);
    }

    /// <summary>
    ///     Test that SafePathCombine correctly handles paths with segments beginning with double dots (not traversal).
    /// </summary>
    [Fact]
    public void PathHelpers_SafePathCombine_DotDotPrefixedName_CombinesCorrectly()
    {
        // Arrange: setup base path and directory name starting with double dots (not traversal)
        var basePath = "/home/user/project";
        var relativePath = "..data/file.txt";

        // Act: execute the operation being tested
        var result = PathHelpers.SafePathCombine(basePath, relativePath);

        // Assert: verify expected behavior
        Assert.Equal(Path.Combine(basePath, relativePath), result);
    }

    /// <summary>
    ///     Test that SafePathCombine throws ArgumentNullException when basePath is null.
    /// </summary>
    [Fact]
    public void PathHelpers_SafePathCombine_NullBasePath_ThrowsArgumentNullException()
    {
        // Arrange & Act & Assert: null basePath throws ArgumentNullException
        Assert.Throws<ArgumentNullException>(() =>
            PathHelpers.SafePathCombine(null!, "file.txt"));
    }

    /// <summary>
    ///     Test that SafePathCombine throws ArgumentNullException when relativePath is null.
    /// </summary>
    [Fact]
    public void PathHelpers_SafePathCombine_NullRelativePath_ThrowsArgumentNullException()
    {
        // Arrange & Act & Assert: null relativePath throws ArgumentNullException
        Assert.Throws<ArgumentNullException>(() =>
            PathHelpers.SafePathCombine("/home/user", null!));
    }

    /// <summary>
    ///     Test that UniqueFileName gives the wanted name when it is free, and numbers it before the extension when a file or a
    ///     folder has it, and refuses a folder that is missing as an argument and a blank name.
    /// </summary>
    [Fact]
    public void PathHelpers_UniqueFileName_FreeAndTakenNames_NumbersBeforeTheExtension()
    {
        // Arrange: a folder that holds a file and a folder that take names
        var folder = Directory.CreateTempSubdirectory("canvaspilot-unique-").FullName;
        try
        {
            File.WriteAllText(Path.Combine(folder, "photo.png"), string.Empty);
            File.WriteAllText(Path.Combine(folder, "photo (2).png"), string.Empty);
            Directory.CreateDirectory(Path.Combine(folder, "plans"));

            // Act: ask for names that are free and taken
            var free = PathHelpers.UniqueFileName(folder, "chart.png");
            var taken = PathHelpers.UniqueFileName(folder, "photo.png");
            var byFolder = PathHelpers.UniqueFileName(folder, "plans");

            // Assert: the free name is kept; the taken ones get the first free number
            Assert.Equal("chart.png", free);
            Assert.Equal("photo (3).png", taken);
            Assert.Equal("plans (2)", byFolder);
            Assert.Throws<ArgumentNullException>(() => PathHelpers.UniqueFileName(null!, "a.png"));
            Assert.Throws<ArgumentException>(() => PathHelpers.UniqueFileName(folder, " "));
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }
}


