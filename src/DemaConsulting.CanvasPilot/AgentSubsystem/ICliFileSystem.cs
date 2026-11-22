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

namespace DemaConsulting.CanvasPilot.AgentSubsystem;

/// <summary>
///     The few questions about the file system that the Copilot CLI locator asks, as a seam so that the locator can be
///     tested against any layout of folders without touching the disk.
/// </summary>
/// <remarks>
///     No member throws for an ordinary problem with a path (missing, no permission, in use); each answers with the value
///     that says "nothing there".
/// </remarks>
public interface ICliFileSystem
{
    /// <summary>
    ///     Tells whether a file exists at the path.
    /// </summary>
    /// <param name="path">The full path of the file.</param>
    /// <returns><see langword="true" /> when the path names an existing file.</returns>
    bool FileExists(string path);

    /// <summary>
    ///     Gets the length of a file.
    /// </summary>
    /// <param name="path">The full path of the file.</param>
    /// <returns>The length in bytes; <c>0</c> when the file does not exist or its length cannot be read.</returns>
    long GetLength(string path);

    /// <summary>
    ///     Follows a link, such as the entry that WinGet puts in its <c>Links</c> folder, to the file it finally names.
    /// </summary>
    /// <param name="path">The full path of the file, which may be a link.</param>
    /// <returns>The full path of the file that the link finally names, or the path itself when it is not a link.</returns>
    string ResolveLinkTarget(string path);

    /// <summary>
    ///     Lists the sub-folders of a folder whose names match a pattern.
    /// </summary>
    /// <param name="parent">The full path of the folder.</param>
    /// <param name="pattern">The pattern of the folder names; <c>*</c> matches any text.</param>
    /// <returns>The full paths of the matching folders; empty when the folder does not exist.</returns>
    IReadOnlyList<string> EnumerateDirectories(string parent, string pattern);
}

/// <summary>
///     The <see cref="ICliFileSystem" /> that asks the real file system.
/// </summary>
/// <remarks>
///     The class holds no state and is safe for concurrent use. It only reads: it never creates, changes, or deletes a
///     file or a folder.
/// </remarks>
public sealed class CliFileSystem : ICliFileSystem
{
    /// <inheritdoc />
    public bool FileExists(string path)
    {
        return File.Exists(path);
    }

    /// <inheritdoc />
    public long GetLength(string path)
    {
        try
        {
            return new FileInfo(path).Length;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // A file that cannot be measured counts as empty, which the locator rejects as too small
            return 0;
        }
    }

    /// <inheritdoc />
    public string ResolveLinkTarget(string path)
    {
        try
        {
            // The final target follows a chain of links to the file that is really run
            var target = new FileInfo(path).ResolveLinkTarget(returnFinalTarget: true);
            return target?.FullName ?? path;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // A link that cannot be followed is judged by its own path
            return path;
        }
    }

    /// <inheritdoc />
    public IReadOnlyList<string> EnumerateDirectories(string parent, string pattern)
    {
        try
        {
            return Directory.Exists(parent) ? [.. Directory.EnumerateDirectories(parent, pattern)] : [];
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // A folder that cannot be listed has no candidates
            return [];
        }
    }
}
