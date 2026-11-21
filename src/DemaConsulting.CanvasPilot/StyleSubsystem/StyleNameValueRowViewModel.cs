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

using CommunityToolkit.Mvvm.ComponentModel;

namespace DemaConsulting.CanvasPilot.StyleSubsystem;

/// <summary>
///     One theme variable of the Mermaid part in the editor of a kit: a name and its value.
/// </summary>
public sealed partial class StyleNameValueRowViewModel : ObservableObject
{
    /// <summary>
    ///     The name of the variable, for example <c>primaryColor</c>.
    /// </summary>
    [ObservableProperty]
    private string _name;

    /// <summary>
    ///     The value of the variable, for example <c>#2563EB</c>.
    /// </summary>
    [ObservableProperty]
    private string _value;

    /// <summary>
    ///     Initializes a new instance of the <see cref="StyleNameValueRowViewModel" /> class.
    /// </summary>
    /// <param name="name">The name of the variable.</param>
    /// <param name="value">The value of the variable.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="name" /> or <paramref name="value" /> is <see langword="null" />.</exception>
    public StyleNameValueRowViewModel(string name, string value)
    {
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(value);

        _name = name;
        _value = value;
    }
}
