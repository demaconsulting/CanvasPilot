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
///     The fonts of a kit, each as a CSS <c>font-family</c> list such as <c>"Segoe UI", sans-serif</c>.
/// </summary>
/// <remarks>
///     A list may be empty, which means the kit says nothing about that use of text. A family that CanvasNet does not
///     bundle is allowed but produces a warning, because only some formats draw it (see
///     <see cref="StyleKitSerializer.Validate" />).
/// </remarks>
/// <param name="Heading">The font list for titles, 0 to 120 characters.</param>
/// <param name="Body">The font list for running text and labels, 0 to 120 characters.</param>
/// <param name="Monospace">The font list for code and numbers, 0 to 120 characters.</param>
public sealed record StyleFonts(string Heading, string Body, string Monospace);
