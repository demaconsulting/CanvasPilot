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
///     Creates, opens, saves, and closes projects. A project is a folder holding a <c>{name}.canvaspilot</c>
///     project file and the image source files. Ordinary user problems are returned as failed
///     <see cref="ProjectResult" /> values, not thrown.
/// </summary>
public interface IProjectService
{
    /// <summary>
    ///     Raised after <see cref="Current" /> changed because a project was created, opened, or closed, or because the
    ///     open project was saved (its modified time changed, as did, when it was the cause, the style kit it uses).
    /// </summary>
    event EventHandler? CurrentChanged;

    /// <summary>
    ///     Gets the open project, or <see langword="null" /> when no project is open.
    /// </summary>
    ProjectInfo? Current { get; }

    /// <summary>
    ///     Checks a project name.
    /// </summary>
    /// <param name="name">The name as typed; surrounding white space is ignored.</param>
    /// <returns>A user-readable problem, or <see langword="null" /> when the name is acceptable.</returns>
    string? ValidateProjectName(string? name);

    /// <summary>
    ///     Checks the folder that will contain a new project folder.
    /// </summary>
    /// <param name="parentFolder">The folder as typed.</param>
    /// <returns>A user-readable problem, or <see langword="null" /> when the folder is acceptable.</returns>
    string? ValidateParentFolder(string? parentFolder);

    /// <summary>
    ///     Checks everything needed to create a project: the name, the parent folder, and that the project folder
    ///     does not already exist with content.
    /// </summary>
    /// <param name="parentFolder">The folder that will contain the project folder.</param>
    /// <param name="name">The project name.</param>
    /// <returns>A user-readable problem, or <see langword="null" /> when the project can be created.</returns>
    string? ValidateNewProject(string? parentFolder, string? name);

    /// <summary>
    ///     Creates the folder <c>{parentFolder}/{name}</c> with its project file and reserved data folder, and
    ///     makes the new project the open project.
    /// </summary>
    /// <param name="parentFolder">The folder that will contain the project folder.</param>
    /// <param name="name">The project name.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The new project, or the reason it could not be created.</returns>
    Task<ProjectResult> CreateAsync(string parentFolder, string name, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Reads a project file and makes its project the open project.
    /// </summary>
    /// <param name="projectFile">The path of the <c>.canvaspilot</c> file.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The project, or the reason it could not be opened; the open project is unchanged on failure.</returns>
    Task<ProjectResult> OpenAsync(string projectFile, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Writes the open project's file, updating its modified time.
    /// </summary>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The saved project, or the reason it could not be saved.</returns>
    Task<ProjectResult> SaveAsync(CancellationToken cancellationToken = default);

    /// <summary>
    ///     Sets the style kit that the open project uses and writes the project file, so the choice is the same for everyone
    ///     who opens the project.
    /// </summary>
    /// <remarks>
    ///     The id is only text to the project subsystem. Setting the id the project already has writes nothing. The write
    ///     is atomic like <see cref="SaveAsync" />, and <see cref="CurrentChanged" /> is raised when the open project's
    ///     document changed.
    /// </remarks>
    /// <param name="styleKitId">
    ///     The id of the style kit: 1 to 48 lower-case letters, digits, and dashes that start with a letter or a digit; or
    ///     <see langword="null" /> for no style, which removes the property from the project file.
    /// </param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The saved project, or the reason it could not be changed (no project is open, an invalid id, or a file that cannot be written).</returns>
    Task<ProjectResult> SetActiveStyleKitAsync(string? styleKitId, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Closes the open project, if any.
    /// </summary>
    void Close();
}
