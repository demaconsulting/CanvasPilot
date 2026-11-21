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
using System.Text;
using DemaConsulting.CanvasPilot.PreviewSubsystem;

namespace DemaConsulting.CanvasPilot.StyleSubsystem;

/// <summary>
///     A color of the palette and the share of the counted pixels that is that color or near it.
/// </summary>
/// <param name="Role">The role (the name) of the color in the palette.</param>
/// <param name="Hex">The color, <c>#RRGGBB</c>.</param>
/// <param name="Share">The share of the counted pixels, more than 0 and at most 1.</param>
public sealed record StylePaletteUse(string Role, string Hex, double Share);

/// <summary>
///     A color that is not on the palette, with the share of the counted pixels that has it or a color near it, and the
///     nearest color of the palette.
/// </summary>
/// <param name="Hex">The color, <c>#RRGGBB</c>: the mean of the pixels that were taken together.</param>
/// <param name="Share">The share of the counted pixels, 0 to 1.</param>
/// <param name="NearestRole">The role of the nearest palette color.</param>
/// <param name="NearestHex">The nearest palette color, <c>#RRGGBB</c>.</param>
/// <param name="DeltaE">The color difference (CIE76) between the color and the nearest palette color.</param>
public sealed record StyleOffPaletteColor(string Hex, double Share, string NearestRole, string NearestHex, double DeltaE);

/// <summary>
///     The result of checking a picture against the palette of a kit.
/// </summary>
/// <remarks>
///     Every counted pixel is on the palette, a blend of two palette colors at an edge, or off the palette, so
///     <see cref="OnPalette" />, <see cref="BlendShare" />, and <see cref="OffPaletteShare" /> add up to 1 (to 0 for a
///     picture with nothing counted). The report is immutable and the same picture gives the same report every time.
/// </remarks>
/// <param name="KitId">The id of the kit that the picture was checked against.</param>
/// <param name="KitName">The name of the kit.</param>
/// <param name="Width">The width of the picture in pixels.</param>
/// <param name="Height">The height of the picture in pixels.</param>
/// <param name="ConsideredPixels">The number of pixels that were counted: those that are not empty.</param>
/// <param name="TransparentPixels">The number of pixels that were not counted because they are empty.</param>
/// <param name="OnPalette">The palette colors that the picture uses, with their shares, the largest first.</param>
/// <param name="BlendShare">The share of the counted pixels that are blends of two palette colors, as at an anti-aliased edge.</param>
/// <param name="OffPalette">The colors off the palette that have a share large enough to be listed, the largest first, at most <see cref="StyleCheckOptions.MaxListed" />.</param>
/// <param name="OtherOffPaletteShare">The share of the counted pixels that is off the palette and is not in <paramref name="OffPalette" />.</param>
/// <param name="OffPaletteShare">The share of the counted pixels that is off the palette, listed or not.</param>
/// <param name="Passed"><see langword="true" /> when the share off the palette is no more than the limit.</param>
/// <param name="MaxOffPaletteShare">The limit that <paramref name="Passed" /> was decided by.</param>
public sealed record StyleCheckReport(
    string KitId,
    string KitName,
    int Width,
    int Height,
    long ConsideredPixels,
    long TransparentPixels,
    IReadOnlyList<StylePaletteUse> OnPalette,
    double BlendShare,
    IReadOnlyList<StyleOffPaletteColor> OffPalette,
    double OtherOffPaletteShare,
    double OffPaletteShare,
    bool Passed,
    double MaxOffPaletteShare = StyleCheckOptions.DefaultMaxOffPaletteShare)
{
    /// <summary>
    ///     The prefix of the code of every note of a check, which is also the name of their group in the list of notes of
    ///     a picture.
    /// </summary>
    public const string NoteGroup = "style";

    /// <summary>
    ///     Gets the share of the counted pixels that is on the palette.
    /// </summary>
    public double OnPaletteShare => OnPalette.Sum(use => use.Share);

    /// <summary>
    ///     Writes the report as notes for the list of notes of a picture: a summary, the palette colors used, the blended
    ///     edges, and one note for each color that is off the palette.
    /// </summary>
    /// <remarks>
    ///     The codes are <c>style-summary</c> and <c>style-ok</c> and <c>style-blend</c> (information) and
    ///     <c>style-off-palette</c> (a warning); each starts with <see cref="NoteGroup" /> and a hyphen.
    /// </remarks>
    /// <returns>The notes, the summary first.</returns>
    public IReadOnlyList<RenderDiagnostic> ToNotes()
    {
        var notes = new List<RenderDiagnostic>
        {
            new(RenderDiagnosticSeverity.Info, "style-summary", ResultSentence())
        };
        if (OnPalette.Count > 0)
        {
            notes.Add(new RenderDiagnostic(RenderDiagnosticSeverity.Info, "style-ok", OnPaletteSentence()));
        }

        if (BlendShare > 0)
        {
            notes.Add(new RenderDiagnostic(RenderDiagnosticSeverity.Info, "style-blend", BlendSentence()));
        }

        foreach (var color in OffPalette)
        {
            notes.Add(new RenderDiagnostic(RenderDiagnosticSeverity.Warning, "style-off-palette", OffPaletteSentence(color)));
        }

        if (OtherOffPaletteShare > 0)
        {
            notes.Add(new RenderDiagnostic(
                RenderDiagnosticSeverity.Warning,
                "style-off-palette",
                $"Other colors off the palette: {Percent(OtherOffPaletteShare)}."));
        }

        return notes;
    }

    /// <summary>
    ///     Writes the report as text for a reader that is not looking at the picture, such as an assistant.
    /// </summary>
    /// <param name="fileName">The name of the checked file, or <see langword="null" /> to say only "the picture".</param>
    /// <returns>Up to four lines: what was checked, the result, the palette colors used, and the colors off the palette.</returns>
    public string ToText(string? fileName)
    {
        var text = new StringBuilder();
        var subject = string.IsNullOrWhiteSpace(fileName) ? "the picture" : fileName.Trim();
        text.Append(CultureInfo.InvariantCulture, $"Style check of {subject} against \"{KitName}\" ({KitId}): {Width} x {Height} px, ")
            .Append(ConsideredPixels.ToString("N0", CultureInfo.InvariantCulture)).Append(" pixels counted (")
            .Append(TransparentPixels.ToString("N0", CultureInfo.InvariantCulture)).Append(" transparent).\n");
        text.Append(ResultSentence()).Append('\n');
        if (OnPalette.Count > 0 || BlendShare > 0)
        {
            text.Append(OnPaletteSentence());
            if (BlendShare > 0)
            {
                text.Append(' ').Append(BlendSentence());
            }

            text.Append('\n');
        }

        if (OffPalette.Count > 0 || OtherOffPaletteShare > 0)
        {
            text.Append("Off the palette: ").Append(string.Join("; ", OffPalette.Select(OffPaletteSentence)));
            if (OtherOffPaletteShare > 0)
            {
                text.Append(OffPalette.Count > 0 ? "; other colors " : "other colors ").Append(Percent(OtherOffPaletteShare));
            }

            text.Append(". Use the palette colors above.\n");
        }

        return text.ToString();
    }

    /// <summary>
    ///     Formats a share as a percentage with one decimal.
    /// </summary>
    /// <param name="share">The share, 0 to 1.</param>
    /// <returns>For example <c>7.4%</c>.</returns>
    private static string Percent(double share) => (share * 100).ToString("0.0", CultureInfo.InvariantCulture) + "%";

    /// <summary>
    ///     Writes the sentence about the result.
    /// </summary>
    /// <returns>For example <c>Result: FAILED, 7.4% of the pixels are off the palette (limit 5%).</c></returns>
    private string ResultSentence()
    {
        var limit = (MaxOffPaletteShare * 100).ToString("0.##", CultureInfo.InvariantCulture);
        return string.Create(
            CultureInfo.InvariantCulture,
            $"Result: {(Passed ? "PASSED" : "FAILED")}, {Percent(OffPaletteShare)} of the pixels are off the palette (limit {limit}%).");
    }

    /// <summary>
    ///     Writes the sentence about the palette colors that the picture uses.
    /// </summary>
    /// <returns>For example <c>On the palette (92.6%): #FFFFFF background 61.0%; #2563EB primary 20.3%.</c></returns>
    private string OnPaletteSentence()
    {
        var uses = string.Join("; ", OnPalette.Select(use => $"{use.Hex} {use.Role} {Percent(use.Share)}"));
        return $"On the palette ({Percent(OnPaletteShare)}): {uses}.";
    }

    /// <summary>
    ///     Writes the sentence about the blended edges.
    /// </summary>
    /// <returns>For example <c>Blended edges: 3.1%.</c></returns>
    private string BlendSentence() => $"Blended edges: {Percent(BlendShare)}.";

    /// <summary>
    ///     Writes the sentence about one color that is off the palette.
    /// </summary>
    /// <param name="color">The color.</param>
    /// <returns>For example <c>#D32F2F 4.1% (nearest: accent #F59E0B, dE 31.2)</c>.</returns>
    private static string OffPaletteSentence(StyleOffPaletteColor color) =>
        string.Create(
            CultureInfo.InvariantCulture,
            $"{color.Hex} {Percent(color.Share)} (nearest: {color.NearestRole} {color.NearestHex}, dE {color.DeltaE:0.0})");
}
