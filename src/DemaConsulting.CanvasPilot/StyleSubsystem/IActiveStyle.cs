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
///     The style kit that the open project uses: which one it is, whether it can be found, and the means to change it.
/// </summary>
/// <remarks>
///     The choice is kept in the project file as the id of a kit; this interface turns the id into the kit that applies
///     to it by the order of the places, and keeps the project file up to date when the choice changes. Implementations
///     are safe for concurrent use; <see cref="Changed" /> may be raised on any thread.
/// </remarks>
public interface IActiveStyle
{
    /// <summary>
    ///     Raised after <see cref="Active" />, <see cref="ActiveId" />, or <see cref="IsMissing" /> may have changed: when
    ///     the open project changes or uses another kit, when the kits are found again, and after the choice was set. It
    ///     may be raised on any thread, and it may be raised when nothing changed.
    /// </summary>
    event EventHandler? Changed;

    /// <summary>
    ///     Gets the kit that the open project uses, or <see langword="null" /> when no project is open, the project has no
    ///     style, or the kit that it names cannot be found.
    /// </summary>
    StyleKitEntry? Active { get; }

    /// <summary>
    ///     Gets the id of the kit that the project file names, or <see langword="null" /> when no project is open or it
    ///     names none. The kit with the id may be missing.
    /// </summary>
    string? ActiveId { get; }

    /// <summary>
    ///     Gets a value indicating whether the project file names a kit that cannot be found; the project is then treated
    ///     as having no style until another kit is chosen.
    /// </summary>
    bool IsMissing { get; }

    /// <summary>
    ///     Makes a kit the style of the open project and writes the choice into the project file.
    /// </summary>
    /// <param name="id">The id of the kit, or <see langword="null" /> for no style.</param>
    /// <returns>A task that completes when the project file is written.</returns>
    /// <exception cref="InvalidOperationException">Thrown when no project is open, the id is not an id of a kit, or the project file cannot be written; the message says why.</exception>
    Task SetActiveAsync(string? id);
}
