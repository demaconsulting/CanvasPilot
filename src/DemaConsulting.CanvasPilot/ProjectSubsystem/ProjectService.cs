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

using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using DemaConsulting.CanvasPilot.LoggingSubsystem;
using DemaConsulting.CanvasPilot.UtilitiesSubsystem;

namespace DemaConsulting.CanvasPilot.ProjectSubsystem;

/// <summary>
///     ProjectService creates, opens, saves, and closes projects. A project is a folder holding a
///     <c>{name}.canvaspilot</c> JSON project file, a hidden <c>.canvaspilot</c> data folder reserved for future
///     application data, and the image source files. Ordinary user problems are returned as failed results and
///     logged; they are never thrown.
/// </summary>
public sealed class ProjectService : IProjectService
{
    /// <summary>
    ///     JSON options shared by every read and write: indented camel-case output, and case-insensitive,
    ///     comment-tolerant input that keeps unknown properties.
    /// </summary>
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    /// <summary>
    ///     Guards the open project.
    /// </summary>
    private readonly Lock _syncRoot = new();

    /// <summary>
    ///     Optional logger for diagnostics.
    /// </summary>
    private readonly RollingFileLogger? _logger;

    /// <summary>
    ///     Source of the current time.
    /// </summary>
    private readonly TimeProvider _timeProvider;

    /// <summary>
    ///     The open project, or null.
    /// </summary>
    private ProjectInfo? _current;

    /// <summary>
    ///     Creates the service with no project open.
    /// </summary>
    /// <param name="logger">Optional logger for diagnostics.</param>
    /// <param name="timeProvider">Optional source of the current time; the system clock by default.</param>
    public ProjectService(RollingFileLogger? logger = null, TimeProvider? timeProvider = null)
    {
        _logger = logger;
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    /// <inheritdoc />
    public event EventHandler? CurrentChanged;

    /// <inheritdoc />
    public ProjectInfo? Current
    {
        get
        {
            lock (_syncRoot)
            {
                return _current;
            }
        }
    }

    /// <inheritdoc />
    public string? ValidateProjectName(string? name) => ProjectNameRules.Validate(name, "project name");

    /// <inheritdoc />
    public string? ValidateParentFolder(string? parentFolder)
    {
        var trimmed = parentFolder?.Trim() ?? string.Empty;
        if (trimmed.Length == 0)
        {
            return "Choose the folder that will contain the project.";
        }

        if (trimmed.IndexOfAny(Path.GetInvalidPathChars()) >= 0 || !Path.IsPathRooted(trimmed))
        {
            return "Enter the full path of an existing folder.";
        }

        return Directory.Exists(trimmed) ? null : "The folder does not exist.";
    }

    /// <inheritdoc />
    public string? ValidateNewProject(string? parentFolder, string? name)
    {
        var problem = ValidateProjectName(name) ?? ValidateParentFolder(parentFolder);
        if (problem is not null)
        {
            return problem;
        }

        var trimmedName = name!.Trim();
        string target;
        try
        {
            target = PathHelpers.SafePathCombine(Path.GetFullPath(parentFolder!.Trim()), trimmedName);
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return "The project location is not a valid path.";
        }

        if (File.Exists(target))
        {
            return $"A file named '{trimmedName}' already exists in that folder.";
        }

        if (Directory.Exists(target) && Directory.EnumerateFileSystemEntries(target).Any())
        {
            return $"A folder named '{trimmedName}' already exists in that folder and is not empty.";
        }

        return null;
    }

    /// <inheritdoc />
    public async Task<ProjectResult> CreateAsync(string parentFolder, string name, CancellationToken cancellationToken = default)
    {
        var problem = ValidateNewProject(parentFolder, name);
        if (problem is not null)
        {
            Log(LogLevel.Warning, $"Project '{name}' was not created in '{parentFolder}': {problem}");
            return ProjectResult.Failure(problem);
        }

        var trimmedName = name.Trim();
        var folder = PathHelpers.SafePathCombine(Path.GetFullPath(parentFolder.Trim()), trimmedName);
        var existedBefore = Directory.Exists(folder);
        var completed = false;
        try
        {
            var filePath = PathHelpers.SafePathCombine(folder, trimmedName + ProjectDocument.FileExtension);
            var document = new ProjectDocument { Name = trimmedName, CreatedUtc = _timeProvider.GetUtcNow() };

            Directory.CreateDirectory(folder);
            CreateDataFolder(folder);
            await WriteDocumentAsync(filePath, document, cancellationToken).ConfigureAwait(false);
            completed = true;

            var project = new ProjectInfo(trimmedName, folder, filePath, document);
            SetCurrent(project);
            Log(LogLevel.Info, $"Created project '{trimmedName}' in '{folder}'.");
            return ProjectResult.Success(project);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException or ArgumentException)
        {
            Log(LogLevel.Error, $"Project '{trimmedName}' could not be created in '{folder}'.", ex);
            return ProjectResult.Failure($"The project could not be created: {ex.Message}");
        }
        finally
        {
            if (!completed)
            {
                RemoveCreatedContent(folder, trimmedName, existedBefore);
            }
        }
    }

    /// <inheritdoc />
    public async Task<ProjectResult> OpenAsync(string projectFile, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(projectFile))
        {
            return Fail("Choose a project file to open.", "Open refused: no project file was given.");
        }

        string filePath;
        try
        {
            filePath = Path.GetFullPath(projectFile.Trim());
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return Fail($"'{projectFile}' is not a valid file path.", $"Open refused: '{projectFile}' is not a valid path.", ex);
        }

        if (!string.Equals(Path.GetExtension(filePath), ProjectDocument.FileExtension, StringComparison.OrdinalIgnoreCase))
        {
            return Fail(
                $"'{Path.GetFileName(filePath)}' is not a CanvasPilot project file ({ProjectDocument.FileExtension}).",
                $"Open refused: '{filePath}' is not a project file.");
        }

        if (!File.Exists(filePath))
        {
            return Fail($"The project file '{filePath}' was not found.", $"Open refused: '{filePath}' does not exist.");
        }

        string text;
        try
        {
            text = await File.ReadAllTextAsync(filePath, Encoding.UTF8, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return Fail($"The project file could not be read: {ex.Message}", $"Project file '{filePath}' could not be read.", ex);
        }

        var parsed = Parse(text, filePath);
        if (parsed.Error is not null)
        {
            Log(LogLevel.Warning, $"Project file '{filePath}' was not opened: {parsed.Error}");
            return ProjectResult.Failure(parsed.Error);
        }

        var stem = Path.GetFileNameWithoutExtension(filePath);
        var name = string.IsNullOrWhiteSpace(parsed.Document!.Name) ? stem : parsed.Document.Name.Trim();
        var document = parsed.Document with { Name = name };
        var project = new ProjectInfo(name, Path.GetDirectoryName(filePath)!, filePath, document);
        SetCurrent(project);
        Log(LogLevel.Info, $"Opened project '{name}' from '{filePath}'.");
        return ProjectResult.Success(project);
    }

    /// <inheritdoc />
    public async Task<ProjectResult> SaveAsync(CancellationToken cancellationToken = default)
    {
        var project = Current;
        if (project is null)
        {
            return Fail("There is no open project to save.", "Save refused: no project is open.");
        }

        return await PersistAsync(project, project.Document, announce: true, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<ProjectResult> SetActiveStyleKitAsync(string? styleKitId, CancellationToken cancellationToken = default)
    {
        // Refuse an id that no kit can have before anything is written, so the project file never names a kit that cannot
        // exist; null is allowed and means no style
        if (styleKitId is not null && !IsValidStyleKitId(styleKitId))
        {
            return Fail(
                $"'{styleKitId}' is not a valid style kit id; use 1 to 48 lower-case letters, digits, and dashes, starting with a letter or a digit.",
                $"Style kit change refused: '{styleKitId}' is not a valid id.");
        }

        var project = Current;
        if (project is null)
        {
            return Fail("There is no open project to change.", "Style kit change refused: no project is open.");
        }

        // A change to what is already there writes nothing, so the file is not touched and its modified time stays
        if (string.Equals(project.Document.ActiveStyleKit, styleKitId, StringComparison.Ordinal))
        {
            return ProjectResult.Success(project);
        }

        var result = await PersistAsync(
            project,
            project.Document with { ActiveStyleKit = styleKitId },
            announce: true,
            cancellationToken).ConfigureAwait(false);
        if (result.Succeeded)
        {
            Log(LogLevel.Info, $"Project '{project.Name}' now uses the style kit '{styleKitId ?? "(none)"}'.");
        }

        return result;
    }

    /// <inheritdoc />
    public void Close()
    {
        ProjectInfo? closed;
        lock (_syncRoot)
        {
            closed = _current;
            _current = null;
        }

        if (closed is null)
        {
            return;
        }

        Log(LogLevel.Info, $"Closed project '{closed.Name}'.");
        CurrentChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    ///     Writes a document as the project file of an open project and, when the project is still open, makes the saved
    ///     document part of it.
    /// </summary>
    /// <param name="project">The open project that the document belongs to.</param>
    /// <param name="edited">The document to write, with the changes the caller made; the format version, name, and
    ///     modified time are set here.</param>
    /// <param name="announce">Whether to raise <see cref="CurrentChanged" /> when the saved project replaced the open one.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns>The saved project, or the reason it could not be saved.</returns>
    private async Task<ProjectResult> PersistAsync(ProjectInfo project, ProjectDocument edited, bool announce, CancellationToken cancellationToken)
    {
        var document = edited with
        {
            FormatVersion = ProjectDocument.CurrentFormatVersion,
            Name = project.Name,
            ModifiedUtc = _timeProvider.GetUtcNow(),
        };

        try
        {
            await WriteDocumentAsync(project.FilePath, document, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return Fail($"The project could not be saved: {ex.Message}", $"Project file '{project.FilePath}' could not be saved.", ex);
        }

        // Replace the open project only when it is still the one that was saved, because another project may have been
        // opened while the file was written
        var saved = project with { Document = document };
        var replaced = false;
        lock (_syncRoot)
        {
            if (ReferenceEquals(_current, project))
            {
                _current = saved;
                replaced = true;
            }
        }

        Log(LogLevel.Info, $"Saved project '{project.Name}' to '{project.FilePath}'.");
        if (announce && replaced)
        {
            CurrentChanged?.Invoke(this, EventArgs.Empty);
        }

        return ProjectResult.Success(saved);
    }

    /// <summary>
    ///     Tells whether a text can be the identifier of a style kit: 1 to 48 lower-case letters, digits, and dashes that
    ///     start with a letter or a digit.
    /// </summary>
    /// <remarks>
    ///     The rule is the one the style kits follow. It is kept here as a plain check because the project subsystem does
    ///     not depend on the style subsystem; the identifier is only text to it.
    /// </remarks>
    /// <param name="id">The text to test.</param>
    /// <returns><see langword="true" /> when the text can be the identifier of a style kit.</returns>
    private static bool IsValidStyleKitId(string id)
    {
        if (id.Length is < 1 or > 48)
        {
            return false;
        }

        // The first character may not be a dash; every character is a lower-case ASCII letter, a digit, or a dash
        return id[0] != '-' && id.All(c => c is (>= 'a' and <= 'z') or (>= '0' and <= '9') or '-');
    }

    /// <summary>
    ///     Parses and validates the text of a project file.
    /// </summary>
    /// <param name="text">The file text.</param>
    /// <param name="filePath">The file path, for messages.</param>
    /// <returns>The document, or the user-readable problem.</returns>
    private static (ProjectDocument? Document, string? Error) Parse(string text, string filePath)
    {
        var fileName = Path.GetFileName(filePath);
        try
        {
            using var json = JsonDocument.Parse(
                text,
                new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true });
            if (json.RootElement.ValueKind != JsonValueKind.Object)
            {
                return (null, $"'{fileName}' is not a valid CanvasPilot project file.");
            }

            var version = json.RootElement.EnumerateObject()
                .FirstOrDefault(property => string.Equals(property.Name, "formatVersion", StringComparison.OrdinalIgnoreCase));
            if (version.Value.ValueKind != JsonValueKind.Number || !version.Value.TryGetInt32(out var formatVersion) || formatVersion < 1)
            {
                return (null, $"'{fileName}' is not a valid CanvasPilot project file: it has no valid formatVersion.");
            }

            if (formatVersion > ProjectDocument.CurrentFormatVersion)
            {
                return (
                    null,
                    $"'{fileName}' was created by a newer version of CanvasPilot (project format {formatVersion}; this " +
                    $"version supports up to {ProjectDocument.CurrentFormatVersion}). Update CanvasPilot to open it.");
            }

            var document = json.RootElement.Deserialize<ProjectDocument>(JsonOptions);
            return document is null
                ? (null, $"'{fileName}' is not a valid CanvasPilot project file.")
                : (document, null);
        }
        catch (JsonException ex)
        {
            return (null, $"'{fileName}' is damaged and cannot be read as a CanvasPilot project file: {ex.Message}");
        }
    }

    /// <summary>
    ///     Writes a project document atomically: to a temporary file in the same folder, then over the target.
    /// </summary>
    /// <param name="filePath">The project file.</param>
    /// <param name="document">The document to write.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <exception cref="IOException">Thrown when the file cannot be written.</exception>
    private static async Task WriteDocumentAsync(string filePath, ProjectDocument document, CancellationToken cancellationToken)
    {
        var json = JsonSerializer.Serialize(document, JsonOptions);
        var folder = Path.GetDirectoryName(filePath)!;
        var tempPath = PathHelpers.SafePathCombine(folder, $"{Path.GetFileName(filePath)}.{Guid.NewGuid():N}.tmp");
        try
        {
            await File.WriteAllTextAsync(tempPath, json, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false), cancellationToken)
                .ConfigureAwait(false);
            File.Move(tempPath, filePath, overwrite: true);
        }
        catch
        {
            DeleteQuietly(tempPath);
            throw;
        }
    }

    /// <summary>
    ///     Creates the hidden data folder reserved for application data. Nothing is written into it.
    /// </summary>
    /// <param name="projectFolder">The project folder.</param>
    private void CreateDataFolder(string projectFolder)
    {
        var dataFolder = PathHelpers.SafePathCombine(projectFolder, ProjectDocument.DataFolderName);
        Directory.CreateDirectory(dataFolder);
        try
        {
            File.SetAttributes(dataFolder, File.GetAttributes(dataFolder) | FileAttributes.Hidden);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or PlatformNotSupportedException)
        {
            // The folder is still hidden by its leading period on platforms that use that convention.
            Log(LogLevel.Warning, $"The data folder '{dataFolder}' could not be marked hidden.", ex);
        }
    }

    /// <summary>
    ///     Removes what a failed creation left behind: the whole project folder when the operation created it,
    ///     otherwise only the project file and data folder it added to a folder that was already empty.
    /// </summary>
    /// <param name="folder">The project folder.</param>
    /// <param name="name">The project name.</param>
    /// <param name="existedBefore">Whether the folder existed before the operation.</param>
    private static void RemoveCreatedContent(string folder, string name, bool existedBefore)
    {
        try
        {
            if (!existedBefore)
            {
                Directory.Delete(folder, recursive: true);
                return;
            }

            DeleteQuietly(PathHelpers.SafePathCombine(folder, name + ProjectDocument.FileExtension));
            Directory.Delete(PathHelpers.SafePathCombine(folder, ProjectDocument.DataFolderName), recursive: true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
        {
            // Best effort: leftovers do not prevent a later attempt, which checks the folder again.
        }
    }

    /// <summary>
    ///     Deletes a file, ignoring failures.
    /// </summary>
    /// <param name="path">The file to delete.</param>
    private static void DeleteQuietly(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Best effort: a leftover temporary file does not affect the project file.
        }
    }

    /// <summary>
    ///     Makes a project the open project and announces the change.
    /// </summary>
    /// <param name="project">The project.</param>
    private void SetCurrent(ProjectInfo project)
    {
        lock (_syncRoot)
        {
            _current = project;
        }

        CurrentChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    ///     Logs a problem and creates the failed result for the user.
    /// </summary>
    /// <param name="userMessage">The message for the user.</param>
    /// <param name="logMessage">The diagnostic entry.</param>
    /// <param name="exception">Optional failure context.</param>
    /// <returns>The failed result.</returns>
    private ProjectResult Fail(string userMessage, string logMessage, Exception? exception = null)
    {
        Log(exception is null ? LogLevel.Warning : LogLevel.Error, logMessage, exception);
        return ProjectResult.Failure(userMessage);
    }

    /// <summary>
    ///     Writes a diagnostic entry when a logger is available. Failing to log never blocks project access.
    /// </summary>
    /// <param name="level">Severity of the event.</param>
    /// <param name="message">Event text.</param>
    /// <param name="exception">Optional failure context.</param>
    private void Log(LogLevel level, string message, Exception? exception = null)
    {
        try
        {
            _logger?.Log(level, message, exception);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Best effort: diagnostics must not make a project problem worse.
        }
    }
}
