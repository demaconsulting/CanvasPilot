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

using Avalonia;
using Avalonia.Data.Converters;

namespace DemaConsulting.CanvasPilot.AppShellSubsystem;

/// <summary>
///     Converters that the control themes of the application use, which a theme cannot do in markup alone.
/// </summary>
public static class ThemeConverters
{
    /// <summary>
    ///     The width of one level of a tree, in device-independent pixels.
    /// </summary>
    public const double TreeIndent = 16;

    /// <summary>
    ///     Turns the level of an item of a tree (0 for the top) into the margin that indents its row by
    ///     <see cref="TreeIndent" /> for each level.
    /// </summary>
    public static readonly IValueConverter TreeLevelIndent = new FuncValueConverter<int, Thickness>(
        level => new Thickness(level * TreeIndent, 0, 0, 0));
}
