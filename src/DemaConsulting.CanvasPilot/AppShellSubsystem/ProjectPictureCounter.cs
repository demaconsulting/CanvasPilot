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

using DemaConsulting.CanvasPilot.ProjectSubsystem;

namespace DemaConsulting.CanvasPilot.AppShellSubsystem;

/// <summary>
///     Counts the pictures of a project folder for the welcome screen and finds the one its card shows. A picture is a file
///     that the explorer lists as an image source. The folders of the attachments and of the style kits, and the folders
///     that start with a dot, are not looked into, and the search stops at a depth and a count that keep it quick.
/// </summary>
internal static class ProjectPictureCounter
{
    /// <summary>
    ///     The most pictures that are counted.
    /// </summary>
    public const int MaximumCount = 999;

    /// <summary>
    ///     How many folders deep the search goes.
    /// </summary>
    private const int MaximumDepth = 4;

    /// <summary>
    ///     The folders at the top of a project that hold the files of the project and not its pictures.
    /// </summary>
    private static readonly string[] SkippedTopFolders = ["attachments", "styles"];

    /// <summary>
    ///     Counts the pictures of a project.
    /// </summary>
    /// <param name="projectFolder">The folder of the project.</param>
    /// <param name="filter">Tells which files are pictures.</param>
    /// <returns>The number of pictures and the path of the first one, which is <see langword="null" /> when there is none.</returns>
    public static (int Count, string? First) Count(string projectFolder, IImageSourceFilter filter)
    {
        var count = 0;
        string? first = null;
        Walk(projectFolder, 0);
        return (count, first);

        void Walk(string folder, int depth)
        {
            try
            {
                var pictures = Directory.EnumerateFiles(folder).Order(StringComparer.OrdinalIgnoreCase).Where(filter.IsImageSource);
                foreach (var file in pictures.Take(MaximumCount - count))
                {
                    count++;
                    first ??= file;
                }

                if (depth >= MaximumDepth)
                {
                    return;
                }

                foreach (var child in Directory.EnumerateDirectories(folder).Order(StringComparer.OrdinalIgnoreCase)
                             .Where(child => !IsSkipped(child, depth)))
                {
                    Walk(child, depth + 1);
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // A folder that cannot be read has no pictures to count; the card still opens the project
            }
        }
    }

    /// <summary>
    ///     Tells whether a folder is left out of the search.
    /// </summary>
    /// <param name="folder">The folder.</param>
    /// <param name="depth">How deep the parent of the folder is, with the project folder at 0.</param>
    /// <returns><see langword="true" /> when the folder is hidden or holds the attachments or the style kits of the project.</returns>
    private static bool IsSkipped(string folder, int depth)
    {
        var name = Path.GetFileName(folder);
        return name.StartsWith('.') || (depth == 0 && SkippedTopFolders.Contains(name, StringComparer.OrdinalIgnoreCase));
    }
}
