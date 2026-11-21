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

using System.Text.Json;
using System.Text.Json.Serialization;

namespace DemaConsulting.CanvasPilot.ProjectSubsystem;

/// <summary>
///     The content of a <c>{name}.canvaspilot</c> project file. Properties this version does not know are kept in
///     <see cref="ExtensionData" /> so saving the project does not discard them.
/// </summary>
public sealed record ProjectDocument
{
    /// <summary>
    ///     The project file format version this build reads and writes.
    /// </summary>
    public const int CurrentFormatVersion = 1;

    /// <summary>
    ///     The file name extension of a project file, including the dot.
    /// </summary>
    public const string FileExtension = ".canvaspilot";

    /// <summary>
    ///     The name of the hidden folder inside a project folder that is reserved for application data.
    /// </summary>
    public const string DataFolderName = ".canvaspilot";

    /// <summary>
    ///     Gets the project file format version.
    /// </summary>
    public int FormatVersion { get; init; } = CurrentFormatVersion;

    /// <summary>
    ///     Gets the project name.
    /// </summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>
    ///     Gets when the project was created, in UTC.
    /// </summary>
    public DateTimeOffset CreatedUtc { get; init; }

    /// <summary>
    ///     Gets when the project file was last saved, in UTC, or <see langword="null" /> when it has not been
    ///     saved since it was created.
    /// </summary>
    public DateTimeOffset? ModifiedUtc { get; init; }

    /// <summary>
    ///     Gets the id of the style kit that the project uses, or <see langword="null" /> when the project has no style.
    /// </summary>
    /// <remarks>
    ///     It is only text to the project: which kit it names, and whether that kit exists, is the business of the style
    ///     subsystem. A project file that lacks the property, such as one made by an earlier version, has no style.
    /// </remarks>
    public string? ActiveStyleKit { get; init; }

    /// <summary>
    ///     Gets the properties of the file that this version does not know.
    /// </summary>
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? ExtensionData { get; init; }
}

/// <summary>
///     An open project: its document and where it lives.
/// </summary>
/// <param name="Name">The project name.</param>
/// <param name="FolderPath">The full path of the project folder.</param>
/// <param name="FilePath">The full path of the project file.</param>
/// <param name="Document">The project document as last read or saved.</param>
public sealed record ProjectInfo(string Name, string FolderPath, string FilePath, ProjectDocument Document);

/// <summary>
///     The outcome of a project operation. Ordinary user problems, such as a bad name or an unreadable file, are
///     reported here rather than thrown.
/// </summary>
public sealed record ProjectResult
{
    /// <summary>
    ///     Initializes a new instance of the <see cref="ProjectResult" /> class.
    /// </summary>
    /// <param name="project">The project, when the operation succeeded.</param>
    /// <param name="error">The user-readable problem, when it failed.</param>
    private ProjectResult(ProjectInfo? project, string error)
    {
        Project = project;
        Error = error;
    }

    /// <summary>
    ///     Gets the project the operation produced, or <see langword="null" /> when it failed.
    /// </summary>
    public ProjectInfo? Project { get; }

    /// <summary>
    ///     Gets the user-readable problem, or an empty string when the operation succeeded.
    /// </summary>
    public string Error { get; }

    /// <summary>
    ///     Gets a value indicating whether the operation succeeded.
    /// </summary>
    public bool Succeeded => Project is not null;

    /// <summary>
    ///     Creates a successful result.
    /// </summary>
    /// <param name="project">The project.</param>
    /// <returns>The result.</returns>
    public static ProjectResult Success(ProjectInfo project)
    {
        ArgumentNullException.ThrowIfNull(project);
        return new ProjectResult(project, string.Empty);
    }

    /// <summary>
    ///     Creates a failed result.
    /// </summary>
    /// <param name="error">The user-readable problem.</param>
    /// <returns>The result.</returns>
    public static ProjectResult Failure(string error)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(error);
        return new ProjectResult(null, error);
    }
}
