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
///     The list of recently opened project files, most recent first.
/// </summary>
public interface IRecentProjects
{
    /// <summary>
    ///     Raised after the list was changed by <see cref="Add" />, <see cref="Remove" />, <see cref="Clear" />, or
    ///     by dropping missing files in <see cref="GetRecent" />.
    /// </summary>
    event EventHandler? Changed;

    /// <summary>
    ///     Gets the recent project files that still exist, most recent first.
    /// </summary>
    /// <returns>The full paths of the project files.</returns>
    IReadOnlyList<string> GetRecent();

    /// <summary>
    ///     Puts a project file at the top of the list.
    /// </summary>
    /// <param name="projectFile">The path of the project file.</param>
    /// <returns><see langword="true" /> when the list was stored.</returns>
    bool Add(string projectFile);

    /// <summary>
    ///     Removes a project file from the list.
    /// </summary>
    /// <param name="projectFile">The path of the project file.</param>
    /// <returns><see langword="true" /> when the list was stored.</returns>
    bool Remove(string projectFile);

    /// <summary>
    ///     Empties the list.
    /// </summary>
    /// <returns><see langword="true" /> when the list was stored.</returns>
    bool Clear();

    /// <summary>
    ///     Gets the picture that was last shown in a listed project on this computer, so that the project opens at it
    ///     again.
    /// </summary>
    /// <param name="projectFile">The path of the project file.</param>
    /// <returns>
    ///     The path of the picture relative to the folder of the project, or <see langword="null" /> when the project is
    ///     not listed, has no picture, or the path is empty or not valid. Whether the file still exists is not checked.
    /// </returns>
    string? GetShownPicture(string projectFile);

    /// <summary>
    ///     Remembers the picture that is shown now in a listed project, so that the project opens at it the next time. The
    ///     picture is kept with the recent projects on this computer, never in the project, and is forgotten with the
    ///     project when it leaves the list.
    /// </summary>
    /// <remarks>
    ///     Nothing is written when the picture is the one already remembered, so selecting the same picture again costs
    ///     nothing. <see cref="Changed" /> is not raised, because the list did not change and the welcome screen that
    ///     follows it need not be built again.
    /// </remarks>
    /// <param name="projectFile">The path of the project file.</param>
    /// <param name="picture">The path of the picture relative to the folder of the project.</param>
    /// <returns>
    ///     <see langword="true" /> when the picture is remembered (stored now or already); <see langword="false" /> when the
    ///     project is not listed, the path is empty, rooted, or leaves the folder, or the settings could not be stored.
    /// </returns>
    bool RememberShownPicture(string projectFile, string picture);
}
