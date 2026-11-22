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
using DemaConsulting.AgentKit.Core;
using DemaConsulting.CanvasPilot.ProjectSubsystem;

namespace DemaConsulting.CanvasPilot.AgentSubsystem;

/// <summary>
///     Turns a path that a model sent into a file of the project, or into a sentence that says why it cannot be used. It is
///     the one place where the project tools decide which paths are allowed, so that no tool has a rule of its own.
/// </summary>
/// <remarks>
///     <para>
///         The path policy of the AI library does most of the work: it refuses <c>..</c>, full paths and network paths
///         outside the project, drive-relative paths, the data folder, and the project file. It judges a path by its
///         spelling and does not follow links, so this class adds what the policy lacks: it refuses a path that passes
///         through a link (a symbolic link or a junction) below the project folder, however the link is spelled, because
///         a link inside the project can lead anywhere. It also refuses the spellings that name something other than the
///         file they seem to name: an alternate data stream (<c>name:stream</c>), a device name (<c>NUL</c>, <c>COM1</c>),
///         a name with a trailing dot or space, and a short 8.3 name (<c>CANVAS~1</c>) that could stand for the data folder
///         or a project file that the policy denies by their long names. For writing it also refuses any name that starts
///         with a dot (<c>.vscode</c>, <c>.git</c>), because those folders hold settings that other tools run.
///     </para>
///     <para>
///         The link check looks at every existing part of the path from the project folder down; the project folder itself
///         is the user's choice and is not judged. A link that is made between the check and the use (a race) is outside
///         what a check can prevent. All methods are static and safe for concurrent use; the file system is only read.
///     </para>
/// </remarks>
internal static partial class ProjectPathGuard
{
    /// <summary>
    ///     The names that Windows gives to devices; a file with one of them as its name (with or without an extension) is a
    ///     device and not a file.
    /// </summary>
    private static readonly HashSet<string> DeviceNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "CON", "PRN", "AUX", "NUL", "CONIN$", "CONOUT$",
        "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9",
        "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9",
    };

    /// <summary>
    ///     Gets how paths are compared on this platform: ignoring case where the usual file systems do.
    /// </summary>
    private static StringComparison PathComparison { get; } =
        OperatingSystem.IsWindows() || OperatingSystem.IsMacOS() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;

    /// <summary>
    ///     Finds a path of the project, for reading or for writing, with the decision of the path policy and the checks that
    ///     the policy lacks.
    /// </summary>
    /// <param name="policy">The policy of the project; its working directory is the project folder.</param>
    /// <param name="request">The path as the model sent it, relative to the project folder or a full path inside it.</param>
    /// <param name="forWrite">Whether the file will be written (the policy then checks the write grant).</param>
    /// <param name="fullPath">Receives the full path inside the project when the result is <see langword="true" />.</param>
    /// <param name="relativePath">
    ///     Receives the path below the project folder, with forward slashes (<c>.</c> for the folder itself), when the
    ///     result is <see langword="true" />.
    /// </param>
    /// <param name="refusal">Receives a sentence that says why the path cannot be used, when the result is <see langword="false" />.</param>
    /// <returns><see langword="true" /> when the path may be used.</returns>
    internal static bool TryResolve(
        PathPolicy policy,
        string? request,
        bool forWrite,
        out string fullPath,
        out string relativePath,
        out string refusal)
    {
        fullPath = string.Empty;
        relativePath = string.Empty;

        // A model sometimes sends nothing; say so rather than let the policy guess a meaning
        if (string.IsNullOrWhiteSpace(request))
        {
            refusal = "The path is empty. Give a path relative to the project folder.";
            return false;
        }

        // The policy decides first: it knows the grant, the denied names, and every way a path can leave the project
        var allowed = forWrite
            ? policy.TryResolveWrite(request, out var resolved, out var denial)
            : policy.TryResolveRead(request, out resolved, out denial);
        if (!allowed)
        {
            refusal = string.IsNullOrWhiteSpace(denial) ? "The path is not permitted." : denial;
            return false;
        }

        if (!Validate(policy.WorkingDirectory, resolved ?? string.Empty, out fullPath, out relativePath, out refusal))
        {
            return false;
        }

        // A hidden folder or file (.vscode, .git, .devcontainer) holds the settings of tools that run code when the
        // project is opened, so the copilot may not create or change anything there, whatever the extension
        if (forWrite)
        {
            var hidden = FindHiddenSegment(relativePath);
            if (hidden is not null)
            {
                refusal = $"'{relativePath}' contains the name '{hidden}', which starts with a dot. Names that start with a dot hold settings of other tools and are not written. "
                          + "Choose a path whose folders and file name do not start with a dot, such as 'drawings/cover.svg'.";
                fullPath = string.Empty;
                relativePath = string.Empty;
                return false;
            }
        }

        return true;
    }

    /// <summary>
    ///     Finds the first name of a relative path that starts with a dot.
    /// </summary>
    /// <param name="relativePath">The path below the project folder, with forward slashes.</param>
    /// <returns>The first name that starts with a dot, or <see langword="null" /> when there is none.</returns>
    private static string? FindHiddenSegment(string relativePath)
    {
        return relativePath.Split('/', StringSplitOptions.RemoveEmptyEntries)
            .FirstOrDefault(segment => segment.StartsWith('.'));
    }

    /// <summary>
    ///     Finds a file below a folder that is not the project (the folder of the active style kit), with the same checks
    ///     as a path of the project but without a path policy: the caller has decided that the folder may be read.
    /// </summary>
    /// <param name="root">The full path of the folder that the file must be in.</param>
    /// <param name="request">The full path that the model sent.</param>
    /// <param name="fullPath">Receives the full path of the file when the result is <see langword="true" />.</param>
    /// <param name="relativePath">Receives the path below the folder, with forward slashes, when the result is <see langword="true" />.</param>
    /// <param name="refusal">Receives a sentence that says why the path cannot be used, when the result is <see langword="false" />.</param>
    /// <returns><see langword="true" /> when the path is inside the folder and may be used.</returns>
    internal static bool TryResolveUnder(string root, string request, out string fullPath, out string relativePath, out string refusal)
    {
        string candidate;
        try
        {
            candidate = Path.GetFullPath(request);
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            fullPath = string.Empty;
            relativePath = string.Empty;
            refusal = "The path is not a valid path.";
            return false;
        }

        return Validate(root, candidate, out fullPath, out relativePath, out refusal);
    }

    /// <summary>
    ///     Tells whether a full path is inside a folder, by its spelling.
    /// </summary>
    /// <param name="root">The full path of the folder.</param>
    /// <param name="fullPath">The full path to test.</param>
    /// <returns><see langword="true" /> when the path is the folder or below it.</returns>
    internal static bool IsInside(string root, string fullPath)
    {
        var folder = Path.TrimEndingDirectorySeparator(root);
        return string.Equals(Path.TrimEndingDirectorySeparator(fullPath), folder, PathComparison)
               || fullPath.StartsWith(folder + Path.DirectorySeparatorChar, PathComparison)
               || fullPath.StartsWith(folder + Path.AltDirectorySeparatorChar, PathComparison);
    }

    /// <summary>
    ///     Tells whether a file or folder is a link that the operating system follows to somewhere else: a symbolic link,
    ///     or a junction (which is a folder that leads to another folder).
    /// </summary>
    /// <remarks>
    ///     A folder that is a reparse point of any kind is a link here, because a folder is where a path can be redirected
    ///     to another place. A file is a link when the library reports a link target (a symbolic link); other reparse points
    ///     of files (the placeholders of a cloud drive, for example) do not redirect and are allowed, so that a project in a
    ///     synchronized folder keeps working. A reparse point whose target cannot be read is treated as a link.
    /// </remarks>
    /// <param name="path">The full path of the file or folder.</param>
    /// <returns>
    ///     <see langword="true" /> when the path is a link; <see langword="false" /> when it is not, or does not exist.
    /// </returns>
    internal static bool IsLink(string path)
    {
        FileAttributes attributes;
        try
        {
            attributes = File.GetAttributes(path);
        }
        catch (Exception ex) when (ex is FileNotFoundException or DirectoryNotFoundException)
        {
            // Nothing is there, so nothing is a link
            return false;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            // What cannot be examined is not trusted
            return true;
        }

        if (!attributes.HasFlag(FileAttributes.ReparsePoint))
        {
            return false;
        }

        // A folder that is a reparse point can lead anywhere
        if (attributes.HasFlag(FileAttributes.Directory))
        {
            return true;
        }

        try
        {
            return new FileInfo(path).LinkTarget is not null;
        }
        catch (IOException)
        {
            return true;
        }
    }

    /// <summary>
    ///     Finds the first link on the way from a folder to a path below it.
    /// </summary>
    /// <param name="root">The full path of the folder that the check starts below; it is not judged.</param>
    /// <param name="relativePath">The path below the folder.</param>
    /// <returns>The path of the first link below the folder, or <see langword="null" /> when there is none.</returns>
    internal static string? FindLink(string root, string relativePath)
    {
        var current = root;
        var shown = string.Empty;
        foreach (var segment in relativePath.Split(['/', '\\'], StringSplitOptions.RemoveEmptyEntries))
        {
            current = Path.Combine(current, segment);
            shown = shown.Length == 0 ? segment : shown + "/" + segment;

            // A link is judged before its existence, because a link whose target is missing does not exist as a file
            // or folder but is still a link
            if (IsLink(current))
            {
                return shown;
            }

            // Parts that do not exist cannot be links, and neither can anything below them
            if (!File.Exists(current) && !Directory.Exists(current))
            {
                return null;
            }
        }

        return null;
    }

    /// <summary>
    ///     Checks a full path that the policy (or the caller) has accepted: it must be inside the folder, no part of it may
    ///     be a name that means something else, and no part of it may be a link.
    /// </summary>
    /// <param name="root">The full path of the folder.</param>
    /// <param name="candidate">The full path to check.</param>
    /// <param name="fullPath">Receives the full path when the result is <see langword="true" />.</param>
    /// <param name="relativePath">Receives the path below the folder with forward slashes (<c>.</c> for the folder), when the result is <see langword="true" />.</param>
    /// <param name="refusal">Receives the reason when the result is <see langword="false" />.</param>
    /// <returns><see langword="true" /> when the path may be used.</returns>
    private static bool Validate(string root, string candidate, out string fullPath, out string relativePath, out string refusal)
    {
        fullPath = string.Empty;
        relativePath = string.Empty;

        // The policy has done this already; the second look costs nothing and keeps the guarantee in one place
        if (!IsInside(root, candidate))
        {
            refusal = "The path is outside the project folder.";
            return false;
        }

        var relative = Path.GetRelativePath(root, candidate).Replace('\\', '/');
        if (relative != ".")
        {
            foreach (var segment in relative.Split('/', StringSplitOptions.RemoveEmptyEntries))
            {
                var reason = CheckName(segment);
                if (reason is not null)
                {
                    refusal = reason;
                    return false;
                }
            }

            // A link below the project folder can lead outside it, and the policy does not look
            var link = FindLink(root, relative);
            if (link is not null)
            {
                refusal = $"The path goes through '{link}', which is a link to another place. Links are not followed.";
                return false;
            }
        }

        fullPath = candidate;
        relativePath = relative;
        refusal = string.Empty;
        return true;
    }

    /// <summary>
    ///     Checks one name of a path for the spellings that name something other than the file they seem to name.
    /// </summary>
    /// <param name="name">One folder or file name.</param>
    /// <returns>The reason the name is not allowed, or <see langword="null" /> when it is.</returns>
    private static string? CheckName(string name)
    {
        // The data folder and the project file belong to the application (the policy denies them too)
        if (string.Equals(name, ProjectDocument.DataFolderName, StringComparison.OrdinalIgnoreCase)
            || name.EndsWith(ProjectDocument.FileExtension, StringComparison.OrdinalIgnoreCase))
        {
            return $"'{name}' belongs to CanvasPilot and is not available.";
        }

        // 'name:stream' is a hidden stream of a file, and 'C:' is a drive; neither is a name of a file of the project
        if (name.Contains(':'))
        {
            return $"The name '{name}' contains a colon, which is not allowed.";
        }

        // Windows ignores a trailing dot or space, so the name would mean another one
        if (name.EndsWith('.') || name.EndsWith(' '))
        {
            return $"The name '{name}' ends with a dot or a space, which is not allowed.";
        }

        // A short name such as CANVAS~1 can stand for a long one that is denied
        if (ShortNameExpression().IsMatch(name))
        {
            return $"The name '{name}' looks like a short (8.3) name, which is not allowed. Use the full name.";
        }

        // A device name is a device and not a file
        var stem = name.Split('.')[0].TrimEnd(' ');
        if (DeviceNames.Contains(stem))
        {
            return $"The name '{name}' is the name of a device, which is not allowed.";
        }

        return null;
    }

    /// <summary>
    ///     Gets the expression that matches a short (8.3) name: a tilde followed by a digit.
    /// </summary>
    /// <returns>The expression.</returns>
    [GeneratedRegex(@"~\d", RegexOptions.CultureInvariant)]
    private static partial Regex ShortNameExpression();
}
