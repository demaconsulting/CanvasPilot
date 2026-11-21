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
///     The Mermaid part of a kit: the theme and theme variables that a Mermaid file should carry in its front matter.
/// </summary>
/// <remarks>
///     CanvasNet reads the Mermaid theme from the source file, so these values are authoring input for the files that the
///     user or an assistant writes and not render options that the application can inject.
/// </remarks>
/// <param name="Theme">One of <c>default</c>, <c>base</c>, <c>dark</c>, <c>forest</c>, and <c>neutral</c>.</param>
/// <param name="ThemeVariables">The theme variables, such as <c>primaryColor</c>, with values of 0 to 60 characters, in file order.</param>
public sealed record StyleMermaid(string Theme, IReadOnlyDictionary<string, string> ThemeVariables);
