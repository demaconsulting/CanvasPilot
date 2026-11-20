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

using System.Text.RegularExpressions;
using DemaConsulting.CanvasPilot.UtilitiesSubsystem;

namespace DemaConsulting.CanvasPilot.ComposerSubsystem;

/// <summary>
///     The rules for names and paths that come from outside the application, shared by the intake of dropped content,
///     which refuses paths that are not safe to refer to, and the attachment store, which makes names that are safe to
///     create. The rules are those of the strictest platform the application runs on, so a name that passes is a
///     valid, ordinary name everywhere.
/// </summary>
internal static partial class AttachmentNames
{
    /// <summary>
    ///     The longest name, in characters, that the application creates for an attached file or folder.
    /// </summary>
    internal const int MaximumNameLength = 100;

    /// <summary>
    ///     The longest extension that is kept when a long name is shortened.
    /// </summary>
    private const int MaximumExtensionLength = 20;

    /// <summary>
    ///     The names of devices that Windows reserves, with or without an extension.
    /// </summary>
    private static readonly HashSet<string> ReservedNames =
        new(["CON", "PRN", "AUX", "NUL", "CONIN$", "CONOUT$"], StringComparer.OrdinalIgnoreCase);

    /// <summary>
    ///     The prefixes of the numbered device names, which end with one digit.
    /// </summary>
    private static readonly string[] NumberedDevicePrefixes = ["COM", "LPT"];

    /// <summary>
    ///     The prefixes that put a path in the device namespace of the operating system.
    /// </summary>
    private static readonly string[] DevicePathPrefixes = [@"\\.\", @"\\?\", "//./", "//?/"];

    /// <summary>
    ///     Gets the path comparison of the platform: file names differ by case only on platforms whose file system
    ///     keeps case.
    /// </summary>
    internal static StringComparison PathComparison =>
        OperatingSystem.IsWindows() || OperatingSystem.IsMacOS() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;

    /// <summary>
    ///     Gets the comparer of paths that matches <see cref="PathComparison" />.
    /// </summary>
    internal static StringComparer PathComparer =>
        OperatingSystem.IsWindows() || OperatingSystem.IsMacOS() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;

    /// <summary>
    ///     Finds out whether a path that came from outside can be referred to safely.
    /// </summary>
    /// <remarks>
    ///     A path is refused when it is empty, holds control characters, is in the device namespace of the operating
    ///     system, is not a complete path, has a part that is <c>.</c> or <c>..</c>, a part with characters that a name
    ///     cannot hold (which includes the colon that names an alternate data stream), a part that ends with a dot or
    ///     a space (which the operating system silently shortens to another name), or a part that names a device. The
    ///     check works on the text of the path only and never touches the file system.
    /// </remarks>
    /// <param name="path">The path.</param>
    /// <returns>A sentence that says what is wrong with the path, or <see langword="null" /> when the path is acceptable.</returns>
    internal static string? FindProblemWithPath(string? path)
    {
        // Refuse what has no meaning or points outside the file system the user sees
        if (string.IsNullOrWhiteSpace(path))
        {
            return "The path is empty.";
        }

        if (path.Any(char.IsControl))
        {
            return "The path contains control characters.";
        }

        if (DevicePathPrefixes.Any(prefix => path.StartsWith(prefix, StringComparison.Ordinal)))
        {
            return "The path points into the device namespace of the operating system.";
        }

        if (!Path.IsPathFullyQualified(path))
        {
            return "The path is not a complete path.";
        }

        // Check every part below the root, because any one of them can lead somewhere else than it seems
        var root = Path.GetPathRoot(path) ?? string.Empty;
        var parts = path[root.Length..].Split(['\\', '/'], StringSplitOptions.RemoveEmptyEntries);
        foreach (var part in parts)
        {
            if (part is "." or "..")
            {
                return "The path contains a '.' or '..' part.";
            }

            if (part.Any(IsInvalidNameCharacter))
            {
                return "The path contains characters that a name cannot hold.";
            }

            if (part.EndsWith('.') || part.EndsWith(' '))
            {
                return "A part of the path ends with a dot or a space.";
            }

            if (IsReservedDeviceName(part))
            {
                return "A part of the path is the name of a device.";
            }
        }

        return null;
    }

    /// <summary>
    ///     Makes a name that is safe to create from a name that came from outside.
    /// </summary>
    /// <remarks>
    ///     Characters that a name cannot hold become underscores, leading and trailing spaces and trailing dots are
    ///     removed, a device name gets an underscore in front, an empty result becomes <c>attachment</c>, and a name
    ///     longer than <see cref="MaximumNameLength" /> is shortened in its stem, keeping a short extension.
    /// </remarks>
    /// <param name="name">The name, which is one part of a path and holds no separators.</param>
    /// <returns>The safe name.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="name" /> is <see langword="null" />.</exception>
    internal static string SanitizeName(string name)
    {
        ArgumentNullException.ThrowIfNull(name);

        // Replace characters that cannot be in a name and drop what the operating system would drop silently
        var replaced = new string([.. name.Select(character => IsInvalidNameCharacter(character) ? '_' : character)]);
        var result = replaced.Trim().TrimEnd('.', ' ');
        if (result.Length == 0)
        {
            return "attachment";
        }

        if (IsReservedDeviceName(result))
        {
            result = "_" + result;
        }

        // Shorten long names in the stem, so that the extension that tells the kind of file stays
        if (result.Length > MaximumNameLength)
        {
            var extension = Path.GetExtension(result);
            if (extension.Length > MaximumExtensionLength)
            {
                extension = string.Empty;
            }

            result = result[..(MaximumNameLength - extension.Length)].TrimEnd('.', ' ') + extension;
        }

        return result;
    }

    /// <summary>
    ///     Finds a name that is not used in a folder, by putting a number in parentheses before the extension when the
    ///     name is taken: <c>photo.png</c>, <c>photo (2).png</c>, <c>photo (3).png</c>.
    /// </summary>
    /// <param name="folder">The full path of the folder.</param>
    /// <param name="name">The wanted name, already safe.</param>
    /// <returns>The first name that no file or folder in the folder has.</returns>
    /// <exception cref="IOException">Thrown when thousands of names are taken, which means something is wrong.</exception>
    internal static string UniqueName(string folder, string name)
    {
        // Most names are free
        if (!Exists(folder, name))
        {
            return name;
        }

        var extension = Path.GetExtension(name);
        var stem = name[..^extension.Length];
        for (var number = 2; number < 10000; number++)
        {
            var candidate = $"{stem} ({number}){extension}";
            if (!Exists(folder, candidate))
            {
                return candidate;
            }
        }

        throw new IOException($"No free name was found for '{name}'.");
    }

    /// <summary>
    ///     Finds a name that is not in a set of names, the way the overload for a folder does, and adds it
    ///     to the set.
    /// </summary>
    /// <param name="taken">The names taken so far, compared without regard to case.</param>
    /// <param name="name">The wanted name, already safe.</param>
    /// <returns>The first name that is not taken, which is now in the set.</returns>
    internal static string UniqueName(HashSet<string> taken, string name)
    {
        var extension = Path.GetExtension(name);
        var stem = name[..^extension.Length];
        var candidate = name;
        var number = 2;
        while (!taken.Add(candidate))
        {
            candidate = $"{stem} ({number}){extension}";
            number++;
        }

        return candidate;
    }

    /// <summary>
    ///     Determines whether a name is, or starts as, the name of a device that Windows reserves.
    /// </summary>
    /// <param name="name">The name, one part of a path.</param>
    /// <returns><see langword="true" /> for names such as <c>CON</c>, <c>aux.txt</c>, and <c>COM1</c>.</returns>
    internal static bool IsReservedDeviceName(string name)
    {
        // Windows ignores the extension and trailing spaces and dots of a device name
        var dot = name.IndexOf('.', StringComparison.Ordinal);
        var stem = (dot >= 0 ? name[..dot] : name).TrimEnd(' ');
        if (ReservedNames.Contains(stem))
        {
            return true;
        }

        return stem.Length == 4 &&
               NumberedDevicePrefixes.Any(prefix => stem.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) &&
               NumberedDeviceDigits().IsMatch(stem[3..]);
    }

    /// <summary>
    ///     Determines whether a character cannot be part of a name on the strictest platform the application runs on.
    /// </summary>
    /// <param name="character">The character.</param>
    /// <returns><see langword="true" /> for control characters, separators, and the characters Windows forbids.</returns>
    private static bool IsInvalidNameCharacter(char character) =>
        char.IsControl(character) || character is '<' or '>' or ':' or '"' or '/' or '\\' or '|' or '?' or '*' ||
        Path.GetInvalidFileNameChars().Contains(character);

    /// <summary>
    ///     Determines whether a file or folder of a name is in a folder.
    /// </summary>
    /// <param name="folder">The folder.</param>
    /// <param name="name">The name.</param>
    /// <returns><see langword="true" /> when something has that name.</returns>
    private static bool Exists(string folder, string name)
    {
        var path = PathHelpers.SafePathCombine(folder, name);
        return File.Exists(path) || Directory.Exists(path);
    }

    /// <summary>
    ///     Matches the last character of a numbered device name: a digit, including the superscript digits.
    /// </summary>
    /// <returns>The regular expression.</returns>
    [GeneratedRegex("^[0-9\u00B9\u00B2\u00B3]$", RegexOptions.CultureInvariant)]
    private static partial Regex NumberedDeviceDigits();
}
