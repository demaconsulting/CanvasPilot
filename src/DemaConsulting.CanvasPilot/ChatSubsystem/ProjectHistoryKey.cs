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

using System.Security.Cryptography;
using System.Text;

namespace DemaConsulting.CanvasPilot.ChatSubsystem;

/// <summary>
///     Makes the name of the folder in which the chat history of a project is kept on this computer: the first 16 bytes
///     of the SHA-256 hash of the full path of the project file, as 32 lowercase hexadecimal characters.
/// </summary>
/// <remarks>
///     <para>
///         The key is made from the place of the project file, not from anything inside the project, so that the history
///         never has to be written into the project folder, which may be copied or put under source control. The path is
///         made full, its separators are those of the platform, a trailing separator is dropped, and, where the file
///         system ignores case (Windows and macOS), it is lowered, so that the spellings of one path give one key.
///     </para>
///     <para>
///         <b>A project that is moved or renamed gets another key, and therefore a fresh history.</b> The history of the
///         old place stays where it was, and the sidecar file next to it names the project it belonged to. This is a
///         known and accepted limit.
///     </para>
///     <para>
///         The class is stateless and thread-safe, and does no file access.
///     </para>
/// </remarks>
public static class ProjectHistoryKey
{
    /// <summary>
    ///     The number of characters of a key.
    /// </summary>
    public const int Length = 32;

    /// <summary>
    ///     The number of bytes of the hash that the key keeps.
    /// </summary>
    private const int KeyBytes = 16;

    /// <summary>
    ///     Makes the key of a project file.
    /// </summary>
    /// <param name="projectFilePath">The path of the project file; <see cref="ProjectSubsystem.ProjectInfo.FilePath" />.</param>
    /// <returns>The key: 32 lowercase hexadecimal characters.</returns>
    /// <exception cref="ArgumentException">Thrown when the path is empty or white space, or is not a usable path.</exception>
    /// <exception cref="ArgumentNullException">Thrown when the path is <see langword="null" />.</exception>
    public static string Compute(string projectFilePath)
    {
        return Compute(projectFilePath, OperatingSystem.IsWindows() || OperatingSystem.IsMacOS());
    }

    /// <summary>
    ///     Makes the key of a project file for a file system that does or does not ignore case.
    /// </summary>
    /// <param name="projectFilePath">The path of the project file.</param>
    /// <param name="ignoreCase">Whether two spellings that differ only in case are one path.</param>
    /// <returns>The key: 32 lowercase hexadecimal characters.</returns>
    /// <exception cref="ArgumentException">Thrown when the path is empty or white space, or is not a usable path.</exception>
    /// <exception cref="ArgumentNullException">Thrown when the path is <see langword="null" />.</exception>
    internal static string Compute(string projectFilePath, bool ignoreCase)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(projectFilePath);

        var full = Path.GetFullPath(projectFilePath).Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar);
        full = Path.TrimEndingDirectorySeparator(full);
        if (ignoreCase)
        {
            full = full.ToLowerInvariant();
        }

        return HashOf(full);
    }

    /// <summary>
    ///     Makes the key of a path that is already normalized.
    /// </summary>
    /// <param name="normalizedPath">The text that is hashed.</param>
    /// <returns>The key: 32 lowercase hexadecimal characters.</returns>
    internal static string HashOf(string normalizedPath)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(normalizedPath));
        return Convert.ToHexStringLower(hash.AsSpan(0, KeyBytes));
    }
}
