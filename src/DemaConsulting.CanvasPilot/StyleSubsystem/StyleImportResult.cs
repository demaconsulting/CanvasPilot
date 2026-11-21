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
///     The outcome of importing a kit from a zip file: the kit and where it is, or why it was not imported.
/// </summary>
/// <remarks>
///     An import never throws for a bad archive. When it fails nothing is left in the target folder.
/// </remarks>
/// <param name="Kit">The kit that was imported, read from its new folder, or <see langword="null" /> when the import failed.</param>
/// <param name="FolderPath">The full path of the folder of the imported kit, or <see langword="null" /> when the import failed.</param>
/// <param name="Error">What is wrong, as a sentence, or <see langword="null" /> when the import succeeded.</param>
/// <param name="Issues">The warnings of the kit when it was imported, or the errors of its style file when it has them.</param>
public sealed record StyleImportResult(StyleKit? Kit, string? FolderPath, string? Error, IReadOnlyList<StyleIssue> Issues)
{
    /// <summary>
    ///     Gets a value that says whether the kit was imported.
    /// </summary>
    public bool Succeeded => Kit is not null;
}
