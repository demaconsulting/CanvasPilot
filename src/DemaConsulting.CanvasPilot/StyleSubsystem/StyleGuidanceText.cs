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
///     The written advice of a kit: free notes and short lists of what to do and what to avoid.
/// </summary>
/// <remarks>
///     This is data that a person wrote into a kit, possibly a kit that came from a shared folder or a zip file. When it
///     is given to an assistant it is labeled as style data and not as instructions.
/// </remarks>
/// <param name="Notes">Free text, 0 to 1,500 characters.</param>
/// <param name="Do">Things to do, at most 10 entries of 1 to 160 characters.</param>
/// <param name="Dont">Things to avoid, at most 10 entries of 1 to 160 characters.</param>
public sealed record StyleGuidanceText(string Notes, IReadOnlyList<string> Do, IReadOnlyList<string> Dont);
