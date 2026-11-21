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
///     One group of kits in the list of the Style Kits window: the kits of one place, with the notes about places that
///     could not be read and about folders that hold a kit that cannot be used.
/// </summary>
public sealed class StyleKitGroupViewModel
{
    /// <summary>
    ///     Initializes a new instance of the <see cref="StyleKitGroupViewModel" /> class.
    /// </summary>
    /// <param name="location">The kind of place.</param>
    /// <param name="title">The title of the group.</param>
    /// <param name="items">The kits of the place, in the order of the catalog.</param>
    /// <param name="notes">What is to be said about the place: that it cannot be reached, or which kits cannot be used.</param>
    /// <param name="emptyText">What to say when the group has no kit.</param>
    /// <exception cref="ArgumentNullException">Thrown when an argument is <see langword="null" />.</exception>
    public StyleKitGroupViewModel(
        StyleKitLocation location,
        string title,
        IReadOnlyList<StyleKitItemViewModel> items,
        IReadOnlyList<string> notes,
        string emptyText)
    {
        ArgumentNullException.ThrowIfNull(title);
        ArgumentNullException.ThrowIfNull(items);
        ArgumentNullException.ThrowIfNull(notes);
        ArgumentNullException.ThrowIfNull(emptyText);

        Location = location;
        Title = title;
        Items = items;
        Notes = notes;
        EmptyText = emptyText;
    }

    /// <summary>
    ///     Gets the kind of place.
    /// </summary>
    public StyleKitLocation Location { get; }

    /// <summary>
    ///     Gets the title of the group.
    /// </summary>
    public string Title { get; }

    /// <summary>
    ///     Gets the kits of the place.
    /// </summary>
    public IReadOnlyList<StyleKitItemViewModel> Items { get; }

    /// <summary>
    ///     Gets what is to be said about the place.
    /// </summary>
    public IReadOnlyList<string> Notes { get; }

    /// <summary>
    ///     Gets a value indicating whether there is something to say about the place.
    /// </summary>
    public bool HasNotes => Notes.Count > 0;

    /// <summary>
    ///     Gets what to say when the group has no kit.
    /// </summary>
    public string EmptyText { get; }

    /// <summary>
    ///     Gets a value indicating whether the group has no kit.
    /// </summary>
    public bool IsEmpty => Items.Count == 0;
}
