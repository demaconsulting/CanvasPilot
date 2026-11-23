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
using Avalonia.Controls.Primitives;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace DemaConsulting.CanvasPilot.StyleSubsystem;

/// <summary>
///     View of the style picker in the title bar of the main window, bound to a <see cref="StylePickerViewModel" /> by
///     the view locator. The list is drawn as a pill, and the footer entry of its list closes it when it is used.
/// </summary>
public sealed partial class StylePickerView : UserControl
{
    /// <summary>
    ///     Turns a color text such as <c>#4F46E5</c> into a brush, for the swatches; text that is no color gives no brush.
    /// </summary>
    public static readonly IValueConverter HexToBrush = new FuncValueConverter<string?, IBrush?>(
        hex => hex is not null && Color.TryParse(hex, out var color) ? new SolidColorBrush(color) : null);

    /// <summary>
    ///     Initializes a new instance of the <see cref="StylePickerView" /> class.
    /// </summary>
    public StylePickerView()
    {
        InitializeComponent();
        PickerBox.TemplateApplied += OnPickerTemplateApplied;
    }

    /// <summary>
    ///     Closes the list when its footer entry is used, because the window that it opens needs the room.
    /// </summary>
    /// <param name="sender">The list.</param>
    /// <param name="e">The event data, with the names of the parts of the template.</param>
    private void OnPickerTemplateApplied(object? sender, TemplateAppliedEventArgs e)
    {
        if (e.NameScope.Find<Button>("PART_ManageKits") is { } button)
        {
            button.Click += (_, _) => PickerBox.IsDropDownOpen = false;
        }
    }
}
