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

namespace DemaConsulting.CanvasPilot.PreviewSubsystem;

/// <summary>
///     Where an export preset comes from, which decides how the export dialog groups it.
/// </summary>
public enum ExportPresetOrigin
{
    /// <summary>The picture as it is shown: it is not drawn again.</summary>
    AsRendered,

    /// <summary>A size that the style kit of the project offers.</summary>
    StyleKit,

    /// <summary>A size that the application offers whatever the style.</summary>
    Application
}

/// <summary>
///     A way to export the shown picture as a PNG file: the size, the background or transparency, and the resolution
///     that the file is drawn at, or the picture as it is shown.
/// </summary>
/// <remarks>
///     <para>
///         A preset with a width and a height is a box that the picture is fitted into keeping its ratio (the margin has
///         the background, or is transparent); with one of them the picture is scaled to it keeping its ratio; with
///         neither the picture has its natural size. The values are checked when the file is drawn, not here.
///     </para>
///     <para>
///         The preset is a plain value. The Preview subsystem declares it and knows nothing about where the presets come
///         from: a source (<see cref="IExportPresetSource" />) supplies them.
///     </para>
/// </remarks>
/// <param name="Id">A stable identifier that tells presets apart and lets the dialog find the one that was chosen last.</param>
/// <param name="Name">The name the user sees.</param>
/// <param name="Width">The width in pixels, or <see langword="null" /> for the natural width or the one that keeps the ratio.</param>
/// <param name="Height">The height in pixels, or <see langword="null" /> for the natural height or the one that keeps the ratio.</param>
/// <param name="Dpi">The resolution in dots per inch that is written into the file, or <see langword="null" /> for none.</param>
/// <param name="Transparent"><see langword="true" /> to keep the margin and the transparent parts transparent.</param>
/// <param name="BackgroundHex">The color, <c>#RRGGBB</c>, that the picture is flattened over, or <see langword="null" /> for none.</param>
/// <param name="Origin">Where the preset comes from.</param>
public sealed record ExportPreset(
    string Id,
    string Name,
    int? Width,
    int? Height,
    double? Dpi,
    bool Transparent,
    string? BackgroundHex,
    ExportPresetOrigin Origin)
{
    /// <summary>
    ///     The identifier of <see cref="AsRendered" />.
    /// </summary>
    public const string AsRenderedId = "as-rendered";

    /// <summary>
    ///     Gets the preset that exports the picture as it is shown, with no new drawing.
    /// </summary>
    public static ExportPreset AsRendered { get; } =
        new(AsRenderedId, "As rendered", null, null, null, false, null, ExportPresetOrigin.AsRendered);

    /// <summary>
    ///     Gets a value indicating whether this preset exports the picture as it is shown.
    /// </summary>
    public bool IsAsRendered => Origin == ExportPresetOrigin.AsRendered;

    /// <summary>
    ///     Gets the settings that draw the source file the way this preset asks. A transparent preset ignores its
    ///     background.
    /// </summary>
    /// <returns>The settings for <see cref="IImageRenderService.RenderAsync(string, string, RenderSettings?, CancellationToken)" />.</returns>
    public RenderSettings ToRenderSettings()
    {
        return new RenderSettings(Width, Height, Transparent, Transparent ? null : BackgroundHex, Dpi);
    }

    /// <summary>
    ///     Gets a short description of the size and the background that the preset gives, for the export dialog.
    /// </summary>
    /// <returns>For example <c>1920 x 1080 px</c>, <c>800 px wide</c>, or <c>As shown</c>.</returns>
    public string DescribeSize()
    {
        // Name the size first: a box, one side, or the picture as it is
        var size = (Width, Height) switch
        {
            (null, null) => IsAsRendered ? "As shown" : "Natural size",
            ({ } w, { } h) => string.Create(CultureInfo.InvariantCulture, $"{w} x {h} px"),
            ({ } w, null) => string.Create(CultureInfo.InvariantCulture, $"{w} px wide"),
            (null, { } h) => string.Create(CultureInfo.InvariantCulture, $"{h} px high")
        };

        // Then say what the margin and the transparent parts are filled with, and the resolution
        var parts = new List<string> { size };
        if (!IsAsRendered)
        {
            if (Transparent)
            {
                parts.Add("transparent");
            }
            else if (BackgroundHex is { Length: > 0 } background)
            {
                parts.Add("on " + background);
            }

            if (Dpi is { } dpi)
            {
                parts.Add(string.Create(CultureInfo.InvariantCulture, $"{dpi:0.##} dpi"));
            }
        }

        return string.Join(", ", parts);
    }
}
