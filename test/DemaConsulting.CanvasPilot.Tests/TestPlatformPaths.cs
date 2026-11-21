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

namespace DemaConsulting.CanvasPilot.Tests;

/// <summary>
///     Paths for tests whose subject depends on what a full path is on the platform that runs them: a drive path on
///     Windows and a path from the root of the file system elsewhere.
/// </summary>
internal static class TestPlatformPaths
{
    /// <summary>
    ///     Gets the root that every full path of the tests starts with.
    /// </summary>
    internal static string Root { get; } = OperatingSystem.IsWindows() ? "C:\\" : "/";

    /// <summary>
    ///     Gets a full path of a folder on a network share, which is a UNC path on Windows and a mounted folder elsewhere.
    /// </summary>
    internal static string NetworkFolder { get; } = OperatingSystem.IsWindows()
        ? "\\\\server\\share\\styles"
        : "/mnt/server/share/styles";

    /// <summary>
    ///     Gets a value that tells whether two paths that differ only in case name the same file or folder here: true on
    ///     Windows and macOS, where the usual file systems ignore case, and false elsewhere.
    /// </summary>
    internal static bool IgnoresCase { get; } = OperatingSystem.IsWindows() || OperatingSystem.IsMacOS();

    /// <summary>
    ///     Makes a full path from the root and the parts below it.
    /// </summary>
    /// <param name="parts">The folder and file names below the root.</param>
    /// <returns>The full path, with the separator of the platform.</returns>
    internal static string FullPath(params string[] parts)
    {
        return Path.Combine([Root, .. parts]);
    }
}
