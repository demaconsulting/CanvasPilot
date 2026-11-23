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
///     One choice of the style picker: no style, a kit, or the kit that the project names and that cannot be found.
/// </summary>
/// <param name="Id">The id of the kit, or <see langword="null" /> for no style.</param>
/// <param name="Label">The text shown in the list: the name of the kit and the place it comes from, or <c>(No style)</c>.</param>
/// <param name="ToolTip">The description of the kit and where it is, or what the choice means.</param>
/// <param name="IsMissing"><see langword="true" /> for the kit that the project names and that cannot be found.</param>
/// <param name="Name">The name of the kit without its place, shown in the pill, or <see langword="null" /> to show the label.</param>
/// <param name="SwatchList">The main colors of the palette of the kit, up to three, as <c>#RRGGBB</c> separated by semicolons.</param>
public sealed record StylePickerItem(
    string? Id,
    string Label,
    string ToolTip,
    bool IsMissing,
    string? Name = null,
    string SwatchList = "")
{
    /// <summary>
    ///     Gets the text shown in the pill: the name of the kit, or the label for no style and a kit that is missing.
    /// </summary>
    public string DisplayName => Name ?? Label;

    /// <summary>
    ///     Gets the main colors of the palette of the kit, up to three, as <c>#RRGGBB</c>; none for no style.
    /// </summary>
    public IReadOnlyList<string> Swatches => SwatchList.Length == 0 ? [] : SwatchList.Split(';');

    /// <summary>
    ///     Gets a value indicating whether the choice has no colors to show, which is drawn as an empty dashed swatch.
    /// </summary>
    public bool HasNoSwatches => SwatchList.Length == 0;

    /// <summary>
    ///     Gets the name of the choice as the pill and its screen reader announce it: the name of the kit, or the label.
    ///     Without this the text of a record with all its values would be announced.
    /// </summary>
    /// <returns>The display name.</returns>
    public override string ToString() => DisplayName;
}
