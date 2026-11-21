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
///     An example image of a kit: a file in the <c>examples</c> folder of the kit that shows the style in use, with an
///     optional caption.
/// </summary>
/// <remarks>
///     The files of the folder are the truth: a file is an example whether or not the kit file has a caption for it, so a
///     zip, a copy, and a hand edit all behave the same. The full path is given so that an assistant can be handed a real
///     file to look at.
/// </remarks>
/// <param name="RelativePath">The path below the kit folder with forward slashes, for example <c>examples/title-card.svg</c>.</param>
/// <param name="FullPath">The full path of the file, or an empty string when the kit has no folder (a kit that is being edited).</param>
/// <param name="Caption">What the example shows, or <see langword="null" /> when the kit file has no caption for it.</param>
public sealed record StyleExample(string RelativePath, string FullPath, string? Caption);
