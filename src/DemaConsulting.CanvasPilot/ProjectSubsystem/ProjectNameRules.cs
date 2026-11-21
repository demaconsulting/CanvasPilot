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

namespace DemaConsulting.CanvasPilot.ProjectSubsystem;

/// <summary>
///     Rules for names that become file or folder names: project names and the names of files in a project.
///     The rules are the same on every platform so a project made on one platform can be opened on another.
/// </summary>
internal static class ProjectNameRules
{
    /// <summary>
    ///     The longest accepted name, in characters.
    /// </summary>
    internal const int MaxNameLength = 100;

    /// <summary>
    ///     Characters that are not allowed in a name on any supported platform.
    /// </summary>
    private static readonly char[] ForbiddenCharacters =
        [.. Path.GetInvalidFileNameChars().Concat("<>:\"/\\|?*")
            .Concat(Enumerable.Range(0, 32).Select(code => (char)code))
            .Distinct()];

    /// <summary>
    ///     Names that Windows reserves for devices, with or without an extension.
    /// </summary>
    private static readonly HashSet<string> ReservedNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "CON", "PRN", "AUX", "NUL",
        "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9",
        "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9",
    };

    /// <summary>
    ///     Checks a name that will become a file or folder name.
    /// </summary>
    /// <param name="name">The name as typed; surrounding white space is ignored.</param>
    /// <param name="label">What the name is, for messages, such as <c>project name</c>.</param>
    /// <returns>A user-readable problem, or <see langword="null" /> when the name is acceptable.</returns>
    internal static string? Validate(string? name, string label)
    {
        var trimmed = name?.Trim() ?? string.Empty;
        if (trimmed.Length == 0)
        {
            return $"Enter a {label}.";
        }

        if (trimmed.Length > MaxNameLength)
        {
            return $"The {label} must be at most {MaxNameLength} characters long.";
        }

        if (trimmed.IndexOfAny(ForbiddenCharacters) >= 0)
        {
            return $"The {label} must not contain any of these characters: < > : \" / \\ | ? * or control characters.";
        }

        if (trimmed[0] == '.')
        {
            return $"The {label} must not start with a period.";
        }

        if (trimmed[^1] == '.')
        {
            return $"The {label} must not end with a period.";
        }

        var stem = trimmed.Split('.')[0].TrimEnd();
        if (ReservedNames.Contains(stem))
        {
            return $"'{stem}' is a reserved name and cannot be used as a {label}.";
        }

        return null;
    }
}
