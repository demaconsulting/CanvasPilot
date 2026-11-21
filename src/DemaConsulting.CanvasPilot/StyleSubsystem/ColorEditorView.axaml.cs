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

using Avalonia.Controls;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace DemaConsulting.CanvasPilot.StyleSubsystem;

/// <summary>
///     The control that edits one color of the palette of a kit: the name of its role, a swatch, the color as <c>#RRGGBB</c>
///     text, and a flyout with sliders for the red, green, and blue values that stay in step with the text. It is made
///     with the controls that Avalonia has, so no package is needed for a color picker.
/// </summary>
/// <remarks>
///     The control holds no state: its data context is a <see cref="StyleColorRowViewModel" />, which keeps the text as the
///     truth and reads and writes the channels from it.
/// </remarks>
public sealed partial class ColorEditorView : UserControl
{
    /// <summary>
    ///     Converts the text of a color to the brush that paints a swatch: the color when the text is one and no paint when it
    ///     is not.
    /// </summary>
    public static readonly IValueConverter HexToBrush = new FuncValueConverter<string?, IBrush?>(
        hex => Color.TryParse(hex?.Trim(), out var color) && hex?.Trim().Length == 7 ? new SolidColorBrush(color) : Brushes.Transparent);

    /// <summary>
    ///     Initializes a new instance of the <see cref="ColorEditorView" /> class.
    /// </summary>
    public ColorEditorView()
    {
        InitializeComponent();
    }
}
