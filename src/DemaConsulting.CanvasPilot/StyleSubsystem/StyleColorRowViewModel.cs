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
///     One color of the palette in the editor of a kit: the name of its role and its value as text, which the user can
///     type as <c>#RRGGBB</c> or set with three sliders for the red, green, and blue channels.
/// </summary>
/// <remarks>
///     The text is the truth: the sliders read the channels from it and write it, so the two never disagree. A text that is
///     not a color is kept as typed and reported by <see cref="HexError" />; the channels then read as 0 until it is
///     fixed. The row keeps no rule about the palette as a whole, which the editor applies.
/// </remarks>
public sealed partial class StyleColorRowViewModel : ObservableObject
{
    /// <summary>
    ///     The name of the role of the color, which a required role keeps.
    /// </summary>
    [ObservableProperty]
    private string _role;

    /// <summary>
    ///     The value of the color as the user typed it.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsHexValid), nameof(HexError), nameof(Red), nameof(Green), nameof(Blue))]
    private string _hex;

    /// <summary>
    ///     Initializes a new instance of the <see cref="StyleColorRowViewModel" /> class.
    /// </summary>
    /// <param name="role">The name of the role.</param>
    /// <param name="hex">The color as text.</param>
    /// <param name="isRequired"><see langword="true" /> for a role that every kit must have, which cannot be renamed or removed.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="role" /> or <paramref name="hex" /> is <see langword="null" />.</exception>
    public StyleColorRowViewModel(string role, string hex, bool isRequired)
    {
        ArgumentNullException.ThrowIfNull(role);
        ArgumentNullException.ThrowIfNull(hex);

        _role = role;
        _hex = hex;
        IsRequired = isRequired;
    }

    /// <summary>
    ///     Gets a value indicating whether the role is one that every kit must have.
    /// </summary>
    public bool IsRequired { get; }

    /// <summary>
    ///     Gets a value indicating whether the name of the role can be changed, which it can unless it is required.
    /// </summary>
    public bool IsRoleEditable => !IsRequired;

    /// <summary>
    ///     Gets a value indicating whether the text is a color of the form <c>#RRGGBB</c>.
    /// </summary>
    public bool IsHexValid => StyleColors.TryParse(Hex.Trim(), out _, out _, out _);

    /// <summary>
    ///     Gets why the text is not a color, or an empty string when it is.
    /// </summary>
    public string HexError => IsHexValid ? string.Empty : $"'{Hex.Trim()}' is not a color; use #RRGGBB.";

    /// <summary>
    ///     Gets or sets the red channel, 0 to 255, read from the text and written to it.
    /// </summary>
    public double Red
    {
        get => ReadChannels().Red;
        set => WriteChannels(value, Green, Blue);
    }

    /// <summary>
    ///     Gets or sets the green channel, 0 to 255, read from the text and written to it.
    /// </summary>
    public double Green
    {
        get => ReadChannels().Green;
        set => WriteChannels(Red, value, Blue);
    }

    /// <summary>
    ///     Gets or sets the blue channel, 0 to 255, read from the text and written to it.
    /// </summary>
    public double Blue
    {
        get => ReadChannels().Blue;
        set => WriteChannels(Red, Green, value);
    }

    /// <summary>
    ///     Reads the channels from the text.
    /// </summary>
    /// <returns>The three channels, or black when the text is not a color.</returns>
    private (byte Red, byte Green, byte Blue) ReadChannels()
    {
        return StyleColors.TryParse(Hex.Trim(), out var red, out var green, out var blue) ? (red, green, blue) : ((byte)0, (byte)0, (byte)0);
    }

    /// <summary>
    ///     Writes the text from three channels, each rounded and held to 0 to 255.
    /// </summary>
    /// <param name="red">The red channel.</param>
    /// <param name="green">The green channel.</param>
    /// <param name="blue">The blue channel.</param>
    private void WriteChannels(double red, double green, double blue)
    {
        Hex = string.Create(CultureInfo.InvariantCulture, $"#{ToByte(red):X2}{ToByte(green):X2}{ToByte(blue):X2}");
    }

    /// <summary>
    ///     Rounds a channel and holds it to the range of a byte.
    /// </summary>
    /// <param name="value">The channel.</param>
    /// <returns>The byte.</returns>
    private static byte ToByte(double value)
    {
        return double.IsNaN(value) ? (byte)0 : (byte)Math.Clamp(Math.Round(value), 0, 255);
    }
}
