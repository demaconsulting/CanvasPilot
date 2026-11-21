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

namespace DemaConsulting.CanvasPilot.StyleSubsystem;

/// <summary>
///     Finds the style kits in the places that hold them, chooses the kit that applies to an id, and creates, changes, and
///     removes the kits of the user and of the open project.
/// </summary>
/// <remarks>
///     <para>
///         The places, in the order in which a kit wins when the same id is in more than one: the <c>styles</c> folder of
///         the open project, the <c>styles</c> folder of the user, the shared folders of the settings (an earlier folder
///         wins), and the kits that come with the program. The kits of the last two are read only.
///     </para>
///     <para>
///         Implementations are safe for concurrent use. <see cref="Entries" /> and <see cref="Locations" /> are
///         snapshots: a refresh replaces them as a whole, so a reader never sees half of a refresh.
///     </para>
/// </remarks>
public interface IStyleKitCatalog
{
    /// <summary>
    ///     Raised after the entries or the locations changed: after a refresh, and after a kit was saved, created,
    ///     duplicated, copied, or deleted. It may be raised on any thread.
    /// </summary>
    event EventHandler? Changed;

    /// <summary>
    ///     Gets every kit that was found, including those that are overridden, ordered by place and then by name. It is empty
    ///     until the first refresh has finished.
    /// </summary>
    IReadOnlyList<StyleKitEntry> Entries { get; }

    /// <summary>
    ///     Gets what was found out about each place, in the order of precedence: whether it could be read and which kits in
    ///     it are broken. The place of a project is absent while no project is open.
    /// </summary>
    IReadOnlyList<StyleLocationStatus> Locations { get; }

    /// <summary>
    ///     Finds the kit that applies to an id: the kit of the most local place that has it.
    /// </summary>
    /// <param name="id">The id of a kit.</param>
    /// <returns>The entry that is not overridden, or <see langword="null" /> when no kit has the id.</returns>
    StyleKitEntry? Resolve(string id);

    /// <summary>
    ///     Reads the places again. The kits that come with the program, of the user, and of the project are read on a worker
    ///     thread, and each shared folder is read on its own worker with a time limit, so a folder that does not answer is
    ///     reported as not reachable and neither blocks the caller nor stops the other places.
    /// </summary>
    /// <param name="cancellationToken">Cancels the refresh; the entries are then left as they were.</param>
    /// <returns>A task that completes when the new entries are in place.</returns>
    /// <exception cref="OperationCanceledException">Thrown when the token is cancelled before the refresh is applied.</exception>
    Task RefreshAsync(CancellationToken cancellationToken = default);

    /// <summary>
    ///     Writes a kit into the folder of the user or of the project, in a folder named as its id, keeping the example
    ///     files that are already in that folder.
    /// </summary>
    /// <param name="kit">The kit to write. It must have no error when it is checked.</param>
    /// <param name="target">Where to write: <see cref="StyleKitLocation.User" /> or <see cref="StyleKitLocation.Project" />.</param>
    /// <returns>The entry of the kit as it is now found.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="kit" /> is <see langword="null" />.</exception>
    /// <exception cref="ArgumentException">Thrown when the kit has an error; the message lists the errors.</exception>
    /// <exception cref="InvalidOperationException">Thrown when the target is a place that is read only, or the project and no project is open.</exception>
    /// <exception cref="IOException">Thrown when the files cannot be written.</exception>
    StyleKitEntry Save(StyleKit kit, StyleKitLocation target);

    /// <summary>
    ///     Creates a kit with a small valid palette and a new id that is made from the name and that no known kit has.
    /// </summary>
    /// <param name="name">The name of the new kit; an empty name becomes <c>New kit</c>.</param>
    /// <param name="target">Where to write: <see cref="StyleKitLocation.User" /> or <see cref="StyleKitLocation.Project" />.</param>
    /// <returns>The entry of the new kit.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the target is a place that is read only, or the project and no project is open.</exception>
    /// <exception cref="IOException">Thrown when the files cannot be written.</exception>
    StyleKitEntry CreateBlank(string name, StyleKitLocation target);

    /// <summary>
    ///     Copies a kit, with its example files, as a new kit that has a new name and an id that no known kit has.
    /// </summary>
    /// <param name="source">The kit to copy; it may be of any place.</param>
    /// <param name="newName">The name of the copy.</param>
    /// <param name="target">Where to write the copy: <see cref="StyleKitLocation.User" /> or <see cref="StyleKitLocation.Project" />.</param>
    /// <returns>The entry of the copy.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="source" /> is <see langword="null" />.</exception>
    /// <exception cref="InvalidOperationException">Thrown when the target is a place that is read only, or the project and no project is open.</exception>
    /// <exception cref="IOException">Thrown when the files cannot be written.</exception>
    StyleKitEntry Duplicate(StyleKitEntry source, string newName, StyleKitLocation target);

    /// <summary>
    ///     Removes a kit of the user or of the project, with its folder.
    /// </summary>
    /// <param name="entry">The kit to remove.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="entry" /> is <see langword="null" />.</exception>
    /// <exception cref="InvalidOperationException">Thrown when the kit comes with the program or is shared, or is not in the place that it claims.</exception>
    /// <exception cref="IOException">Thrown when the folder cannot be removed.</exception>
    void Delete(StyleKitEntry entry);

    /// <summary>
    ///     Copies a kit, with its example files and with the same id, into the <c>styles</c> folder of the open project, so
    ///     that the project keeps the style it was made with even when the original changes.
    /// </summary>
    /// <param name="source">The kit to copy.</param>
    /// <returns>The entry of the copy.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="source" /> is <see langword="null" />.</exception>
    /// <exception cref="InvalidOperationException">Thrown when no project is open, or the project already has a kit with that id.</exception>
    /// <exception cref="IOException">Thrown when the files cannot be written.</exception>
    StyleKitEntry CopyIntoProject(StyleKitEntry source);
}
