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
///     The result of reading a kit: the kit when it has no error, and every problem that was found.
/// </summary>
/// <remarks>
///     Reading never throws for a bad file. Every error is collected, not only the first, so that the person who edits the
///     file can fix all of them in one pass.
/// </remarks>
/// <param name="Kit">The kit, or <see langword="null" /> when any issue is an error.</param>
/// <param name="Issues">Every error and warning, in the order they were found.</param>
public sealed record StyleKitParseResult(StyleKit? Kit, IReadOnlyList<StyleIssue> Issues)
{
    /// <summary>
    ///     Gets a value that says whether a kit was read, which is when there is no error.
    /// </summary>
    public bool Succeeded => Kit is not null;

    /// <summary>
    ///     Gets the issues that stop the kit from being used.
    /// </summary>
    public IEnumerable<StyleIssue> Errors => Issues.Where(i => i.IsError);

    /// <summary>
    ///     Gets the issues that leave the kit usable.
    /// </summary>
    public IEnumerable<StyleIssue> Warnings => Issues.Where(i => !i.IsError);
}
