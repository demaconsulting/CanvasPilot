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

using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;

namespace DemaConsulting.CanvasPilot.StyleSubsystem;

/// <summary>
///     One export size in the editor of a kit: a name, a width, a height, a resolution, and whether the margin stays
///     transparent. The numbers are kept as the text that the user typed, so that a half-typed number is not lost, and are
///     read when the kit is built.
/// </summary>
public sealed partial class StylePresetRowViewModel : ObservableObject
{
    /// <summary>
    ///     The name that the user sees in the export dialog.
    /// </summary>
    [ObservableProperty]
    private string _name;

    /// <summary>
    ///     The width in pixels as text; empty to scale from the height.
    /// </summary>
    [ObservableProperty]
    private string _widthText;

    /// <summary>
    ///     The height in pixels as text; empty to scale from the width.
    /// </summary>
    [ObservableProperty]
    private string _heightText;

    /// <summary>
    ///     The resolution in dots per inch as text; empty for none.
    /// </summary>
    [ObservableProperty]
    private string _dpiText;

    /// <summary>
    ///     Whether the margin and the transparent parts stay transparent.
    /// </summary>
    [ObservableProperty]
    private bool _transparent;

    /// <summary>
    ///     Initializes a new instance of the <see cref="StylePresetRowViewModel" /> class.
    /// </summary>
    /// <param name="name">The name of the size.</param>
    /// <param name="width">The width in pixels, or <see langword="null" /> for none.</param>
    /// <param name="height">The height in pixels, or <see langword="null" /> for none.</param>
    /// <param name="dpi">The resolution, or <see langword="null" /> for none.</param>
    /// <param name="transparent">Whether the margin stays transparent.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="name" /> is <see langword="null" />.</exception>
    public StylePresetRowViewModel(string name, int? width, int? height, double? dpi, bool transparent)
    {
        ArgumentNullException.ThrowIfNull(name);

        _name = name;
        _widthText = width?.ToString(CultureInfo.InvariantCulture) ?? string.Empty;
        _heightText = height?.ToString(CultureInfo.InvariantCulture) ?? string.Empty;
        _dpiText = dpi?.ToString("0.##", CultureInfo.InvariantCulture) ?? string.Empty;
        _transparent = transparent;
    }

    /// <summary>
    ///     Reads the row as a size preset.
    /// </summary>
    /// <param name="problems">
    ///     Receives what could not be read, as phrases that complete a sentence that starts with the name of the property,
    ///     paired with the name of the property: <c>width</c>, <c>height</c>, or <c>dpi</c>.
    /// </param>
    /// <returns>The preset; a number that is not one is left out of it and is listed in <paramref name="problems" />.</returns>
    public StyleSizePreset ToPreset(List<(string Property, string Message)> problems)
    {
        ArgumentNullException.ThrowIfNull(problems);

        var width = ReadWholeNumber("width", WidthText, problems);
        var height = ReadWholeNumber("height", HeightText, problems);

        double? dpi = null;
        var dpiText = DpiText.Trim();
        if (dpiText.Length > 0)
        {
            if (double.TryParse(dpiText, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed) && double.IsFinite(parsed))
            {
                dpi = parsed;
            }
            else
            {
                problems.Add(("dpi", $"'{dpiText}' is not a number."));
            }
        }

        return new StyleSizePreset(Name, width, height, dpi, Transparent);
    }

    /// <summary>
    ///     Reads a whole number that may be empty.
    /// </summary>
    /// <param name="property">The name of the property, for a message.</param>
    /// <param name="text">The text to read.</param>
    /// <param name="problems">The list that a problem is added to.</param>
    /// <returns>The number, or <see langword="null" /> when the text is empty or is not a whole number.</returns>
    private static int? ReadWholeNumber(string property, string text, List<(string Property, string Message)> problems)
    {
        var trimmed = text.Trim();
        if (trimmed.Length == 0)
        {
            return null;
        }

        if (int.TryParse(trimmed, NumberStyles.None, CultureInfo.InvariantCulture, out var value))
        {
            return value;
        }

        problems.Add((property, $"'{trimmed}' is not a whole number of pixels."));
        return null;
    }
}
