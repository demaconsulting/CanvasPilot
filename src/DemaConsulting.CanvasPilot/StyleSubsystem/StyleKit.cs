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
///     A style kit: a reusable set of colors, fonts, numbers, advice, export sizes, and example images that describe how
///     the pictures of a person or a team should look.
/// </summary>
/// <remarks>
///     The record holds values and checks nothing; a kit that is read from a file has been checked by
///     <see cref="StyleKitSerializer.Parse" />, and a kit that is built in memory (by the editor) is checked by
///     <see cref="StyleKitSerializer.Validate" /> before it is written. The dictionaries and lists keep the order of the
///     file. The record is immutable; it is safe to share between threads as long as nobody changes the collections it was
///     built from.
/// </remarks>
/// <param name="Id">The identifier, a lower-case kebab id that matches <c>^[a-z0-9][a-z0-9-]{0,47}$</c>; it names the folder of the kit.</param>
/// <param name="Name">The name that the user sees, 1 to 60 characters.</param>
/// <param name="Description">What the kit is for, 0 to 300 characters.</param>
/// <param name="Palette">Role or name to color, each color <c>#RRGGBB</c> in upper case; the roles <c>background</c>, <c>text</c>, and <c>primary</c> are required.</param>
/// <param name="Fonts">The font lists.</param>
/// <param name="StrokeWidth">The usual line width in pixels, 0 to 32.</param>
/// <param name="CornerRadius">The usual corner radius in pixels, 0 to 64.</param>
/// <param name="Spacing">The usual gap between elements in pixels, 0 to 128.</param>
/// <param name="Mermaid">The Mermaid theme, or <see langword="null" /> when the kit has none.</param>
/// <param name="VegaLiteConfigJson">The compact JSON text of the Vega-Lite <c>config</c> object, or <see langword="null" /> when the kit has none.</param>
/// <param name="Guidance">The written advice.</param>
/// <param name="SizePresets">The export sizes, at most 16.</param>
/// <param name="Examples">The example images, at most 24, sorted by file name.</param>
public sealed record StyleKit(
    string Id,
    string Name,
    string Description,
    IReadOnlyDictionary<string, string> Palette,
    StyleFonts Fonts,
    double StrokeWidth,
    double CornerRadius,
    double Spacing,
    StyleMermaid? Mermaid,
    string? VegaLiteConfigJson,
    StyleGuidanceText Guidance,
    IReadOnlyList<StyleSizePreset> SizePresets,
    IReadOnlyList<StyleExample> Examples);
