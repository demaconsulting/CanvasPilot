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

namespace DemaConsulting.CanvasPilot.StyleSubsystem;

/// <summary>
///     Reads, normalizes, and compares the opaque <c>#RRGGBB</c> colors that a style kit holds.
/// </summary>
/// <remarks>
///     A kit holds only opaque colors in one written form, so that two kits can be compared and a check of a picture has a
///     single palette to measure against. Stateless and safe to call from any thread.
/// </remarks>
public static class StyleColors
{
    /// <summary>
    ///     Reads a color of the form <c>#RRGGBB</c>, in any case.
    /// </summary>
    /// <param name="text">The text to read; may be <see langword="null" />.</param>
    /// <param name="red">Receives the red channel, 0 to 255.</param>
    /// <param name="green">Receives the green channel, 0 to 255.</param>
    /// <param name="blue">Receives the blue channel, 0 to 255.</param>
    /// <returns><see langword="true" /> when the text is a hash sign and exactly six hexadecimal digits; otherwise <see langword="false" />.</returns>
    public static bool TryParse(string? text, out byte red, out byte green, out byte blue)
    {
        red = green = blue = 0;

        // Accept only the seven-character form: a color name, a short form, or a color with an alpha channel is refused
        // so that every color in a kit means one opaque RGB value
        if (text is not { Length: 7 } || text[0] != '#')
        {
            return false;
        }

        return byte.TryParse(text.AsSpan(1, 2), NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out red)
               && byte.TryParse(text.AsSpan(3, 2), NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out green)
               && byte.TryParse(text.AsSpan(5, 2), NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out blue);
    }

    /// <summary>
    ///     Writes a valid color in its one normal form, <c>#RRGGBB</c> in upper case.
    /// </summary>
    /// <param name="text">The text to normalize; may be <see langword="null" />.</param>
    /// <param name="normalized">Receives the upper-case form, or an empty string when the text is not a color.</param>
    /// <returns><see langword="true" /> when the text is a color; otherwise <see langword="false" />.</returns>
    public static bool TryNormalize(string? text, out string normalized)
    {
        if (!TryParse(text, out var red, out var green, out var blue))
        {
            normalized = string.Empty;
            return false;
        }

        normalized = string.Create(CultureInfo.InvariantCulture, $"#{red:X2}{green:X2}{blue:X2}");
        return true;
    }

    /// <summary>
    ///     Writes a color in its one normal form, <c>#RRGGBB</c> in upper case.
    /// </summary>
    /// <param name="text">The color to normalize. Must be a hash sign and six hexadecimal digits.</param>
    /// <returns>The upper-case form of the color.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="text" /> is <see langword="null" />.</exception>
    /// <exception cref="FormatException">Thrown when <paramref name="text" /> is not a color of the form <c>#RRGGBB</c>.</exception>
    public static string Normalize(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        return TryNormalize(text, out var normalized)
            ? normalized
            : throw new FormatException($"'{text}' is not a color; use #RRGGBB.");
    }

    /// <summary>
    ///     Measures the contrast between two colors with the WCAG formula: the lighter relative luminance plus 0.05 over the
    ///     darker plus 0.05.
    /// </summary>
    /// <remarks>
    ///     The ratio runs from 1 (the same color) to 21 (black on white). Text that is read for a long time should have at
    ///     least 4.5; the editor uses it as a hint and the tests of the built-in kits use it as a check.
    /// </remarks>
    /// <param name="first">One color, <c>#RRGGBB</c>.</param>
    /// <param name="second">The other color, <c>#RRGGBB</c>.</param>
    /// <returns>The contrast ratio, at least 1; it does not depend on the order of the arguments.</returns>
    /// <exception cref="ArgumentNullException">Thrown when an argument is <see langword="null" />.</exception>
    /// <exception cref="FormatException">Thrown when an argument is not a color of the form <c>#RRGGBB</c>.</exception>
    public static double Contrast(string first, string second)
    {
        var a = RelativeLuminance(first);
        var b = RelativeLuminance(second);
        var lighter = Math.Max(a, b);
        var darker = Math.Min(a, b);
        return (lighter + 0.05) / (darker + 0.05);
    }

    /// <summary>
    ///     Computes the WCAG relative luminance of a color from the linearized sRGB channels.
    /// </summary>
    /// <param name="color">The color, <c>#RRGGBB</c>.</param>
    /// <returns>The luminance from 0 (black) to 1 (white).</returns>
    private static double RelativeLuminance(string color)
    {
        ArgumentNullException.ThrowIfNull(color);
        if (!TryParse(color, out var red, out var green, out var blue))
        {
            throw new FormatException($"'{color}' is not a color; use #RRGGBB.");
        }

        // Weights of the three channels in the luminance of the eye, as the WCAG definition fixes them
        return (0.2126 * Linearize(red)) + (0.7152 * Linearize(green)) + (0.0722 * Linearize(blue));
    }

    /// <summary>
    ///     Converts one sRGB channel to linear light.
    /// </summary>
    /// <param name="channel">The channel value, 0 to 255.</param>
    /// <returns>The linear value from 0 to 1.</returns>
    private static double Linearize(byte channel)
    {
        var value = channel / 255.0;

        // The two parts of the sRGB transfer curve, split at the point where they meet
        return value <= 0.04045 ? value / 12.92 : Math.Pow((value + 0.055) / 1.055, 2.4);
    }
}
